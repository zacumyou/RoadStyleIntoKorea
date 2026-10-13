using System.Collections.Generic;
using Colossal.Mathematics;
using Game.Net;
using Game.Prefabs;
using Unity.Entities;
using Unity.Mathematics;
namespace RoadStyleIntoKorea
{
    public partial class StyleSystem
    {
        sealed class EdgeClearanceContext
        {
            internal Entity Owner;internal List<RoadRibbon> Ribbons;internal SidewalkFootprint Sidewalks;
            internal EdgeTaperPolicy Tapers;
        }
        sealed class EdgePaintPlan
        {
            internal Entity Owner;internal EdgeStyle Style;internal Bezier4x3 Source;
            internal float Inset,Clearance;internal List<Bezier4x3> Parts;
        }
        readonly List<EdgePaintPlan> edgePaintPlans=new List<EdgePaintPlan>();
        List<Bezier4x3> EdgePaintParts(Entity owner,EdgeStyle style,Bezier4x3 curve,List<Entity> crossings,EdgeClearanceContext context)
        {
            foreach(var plan in edgePaintPlans)
                if(plan.Owner==owner&&plan.Style==style&&plan.Inset==S.EdgeLaneGap&&plan.Clearance==S.PaintClearance&&Geometry.SameCurve(plan.Source,curve))return plan.Parts;
            if(context.Ribbons==null)context.Ribbons=EdgeRoadRibbons(owner);
            if(context.Sidewalks==null)context.Sidewalks=EdgeSidewalks(owner);
            if(context.Tapers==null)
            {
                context.Tapers=new EdgeTaperPolicy(CurveOf(owner));
                foreach(var ribbon in context.Ribbons){context.Tapers.Add(ribbon.Left);context.Tapers.Add(ribbon.Right);}
                foreach(var boundary in context.Sidewalks.Boundaries)context.Tapers.Add(boundary);
            }
            var limits=EdgeApproachLimits(owner,EdgePaintHalfWidth(style));
            var parts=new List<Bezier4x3>();
            
            var approaches=new List<Bezier4x3>(EdgeApproachPolicy.Clip(curve,limits));
            if(approaches.Count==0){approaches.Add(curve);}
            foreach(var approach in approaches)
            {
                foreach(var untapered in Geometry.ClearSlices(approach,context.Tapers.Clear))
                {
                    foreach(var roadPart in RoadOnlyEdge(owner,style,untapered,context.Ribbons,context.Sidewalks))
                    {
                        foreach(var clear in ClearPaint(roadPart,crossings,S.PaintClearance,owner)){parts.Add(clear);}
                    }
                }
            }
            if(edgePaintPlans.Count>=128)edgePaintPlans.Clear();
            edgePaintPlans.Add(new EdgePaintPlan{Owner=owner,Style=style,Source=curve,Inset=S.EdgeLaneGap,Clearance=S.PaintClearance,Parts=parts});
            return parts;
        }
        List<EdgeApproachPolicy.Limit> EdgeApproachLimits(Entity edge,float half)
        {
            var result=new List<EdgeApproachPolicy.Limit>();
            if(!EntityManager.HasComponent<Edge>(edge)||!EntityManager.HasComponent<Curve>(edge))return result;
            var ends=EntityManager.GetComponentData<Edge>(edge);var center=CurveOf(edge);
            foreach(bool start in new[]{true,false})
            {
                var node=start?ends.m_Start:ends.m_End;
                if(!EntityManager.HasComponent<Node>(node)||!EntityManager.HasBuffer<ConnectedEdge>(node))continue;
                var origin=EntityManager.GetComponentData<Node>(node).m_Position;
                var inward=Geometry.Tangent(center,start?0:1)*(start?1:-1);
                int count=0;bool corner=false;float fallback=4;
                foreach(var connected in EntityManager.GetBuffer<ConnectedEdge>(node,true))
                {
                    var other=connected.m_Edge;if(!IsRoad(other))continue;count++;
                    if(other==edge||!EntityManager.HasComponent<Curve>(other))continue;
                    var otherEnds=EntityManager.GetComponentData<Edge>(other);
                    var tangent=Geometry.Tangent(CurveOf(other),otherEnds.m_Start==node?0:1);
                    corner|=math.abs(math.dot(tangent,inward))<.97f;
                    if(EntityManager.HasComponent<Composition>(other))
                    {var cp=EntityManager.GetComponentData<Composition>(other).m_Edge;if(EntityManager.HasComponent<NetCompositionData>(cp))fallback=math.max(fallback,EntityManager.GetComponentData<NetCompositionData>(cp).m_Width*.5f+3);}
                }
                if(count<3&&!corner)continue;
                float extent=0;
                bool geometry=start?EntityManager.HasComponent<StartNodeGeometry>(edge):EntityManager.HasComponent<EndNodeGeometry>(edge);
                if(geometry)
                {
                    var shape=start?EntityManager.GetComponentData<StartNodeGeometry>(edge).m_Geometry:EntityManager.GetComponentData<EndNodeGeometry>(edge).m_Geometry;
                    // m_Middle can be a side-connection flag (d.xy = 1), not a
                    // world-space curve. The four real boundaries bound the corner.
                    foreach(var boundary in new[]{shape.m_Left.m_Left,shape.m_Left.m_Right,shape.m_Right.m_Left,shape.m_Right.m_Right})
                        extent=math.max(extent,EdgeApproachPolicy.Extent(boundary,origin,inward));
                }
                if(extent<.1f)extent=fallback;
                result.Add(new EdgeApproachPolicy.Limit{Origin=origin+inward*(extent+half+.5f),Inward=inward});
            }
            // When corners consume the entire connector, exact roadway and
            // sidewalk masks below replace the conservative approach planes.
            if(EdgeApproachPolicy.Conflicting(result))result.Clear();
            return result;
        }
        sealed class RoadRibbon
        {
            internal Bezier4x3 Left,Right;internal Geometry.Arc Center;
            internal bool HasLeftCurb,HasRightCurb;
            RoadSurfaceFootprint surface;
            internal bool ContainsPaint(float3 point,float half)
            {if(surface==null)surface=new RoadSurfaceFootprint(Left,Right);return surface.ContainsPaint(point,half);}
        }
        void AddRoadRibbon(List<RoadRibbon> result,Entity cp,Segment segment,float origin,float width)
        {
            if(width<=.01f||!EntityManager.HasBuffer<NetCompositionPiece>(cp))return;
            float min=origin,max=origin+width;
            foreach(var piece in EntityManager.GetBuffer<NetCompositionPiece>(cp,true))
            {
                // Transition pieces can include carriageway as well as sidewalk;
                // their entire width must not be subtracted from the road.
                if(!PieceName(piece.m_Piece).StartsWith("Sidewalk",System.StringComparison.OrdinalIgnoreCase)||
                   PieceName(piece.m_Piece).IndexOf("To",System.StringComparison.OrdinalIgnoreCase)>=0||
                   PieceName(piece.m_Piece).IndexOf("Drive",System.StringComparison.OrdinalIgnoreCase)>=0||
                   PieceName(piece.m_Piece).IndexOf("Parking",System.StringComparison.OrdinalIgnoreCase)>=0||
                   (piece.m_SectionFlags&(NetSectionFlags.Median|NetSectionFlags.Hidden|NetSectionFlags.HiddenSurface))!=0)continue;
                // Exclude the complete sidewalk piece, including its curb/gutter.
                if(piece.m_Offset.x<0)min=math.max(min,piece.m_Offset.x+piece.m_Size.x/2-SidewalkGutter(piece));
                else max=math.min(max,piece.m_Offset.x-piece.m_Size.x/2+SidewalkGutter(piece));
            }
            // Mixed parking/stop transition pieces cannot be removed as whole
            // rectangles. Their composition lane extents still locate the road
            // cross-section, which is then deformed by the native segment curves.
            // Lane width is not the asphalt width: retain the composition's
            // shoulder/gutter beyond the last driving lane.
            if(max-min<.1f)return;
            var a=Between(segment.m_Left,segment.m_Right,(min-origin)/width,0);
            var b=Between(segment.m_Left,segment.m_Right,(max-origin)/width,0);
            result.Add(new RoadRibbon{Left=a,Right=b,Center=new Geometry.Arc(Between(a,b,.5f,0)),HasLeftCurb=min>origin+.01f,HasRightCurb=max<origin+width-.01f});
        }
        readonly Dictionary<Entity,List<RoadRibbon>> roadRibbonFrameCache=new Dictionary<Entity,List<RoadRibbon>>();
        readonly Dictionary<Entity,SidewalkFootprint> sidewalkFrameCache=new Dictionary<Entity,SidewalkFootprint>();
        List<RoadRibbon> EdgeRoadRibbons(Entity owner){if(roadRibbonFrameCache.Count>=128)roadRibbonFrameCache.Clear();if(!roadRibbonFrameCache.TryGetValue(owner,out var value))roadRibbonFrameCache[owner]=value=BuildRoadRibbons(owner);return value;}
        List<RoadRibbon> BuildRoadRibbons(Entity owner)
        {
            var result=new List<RoadRibbon>();
            if(EntityManager.HasComponent<Edge>(owner))
            {
                if(!EntityManager.HasComponent<Composition>(owner)||!EntityManager.HasComponent<EdgeGeometry>(owner))return result;
                var cp=EntityManager.GetComponentData<Composition>(owner).m_Edge;
                if(!EntityManager.HasComponent<NetCompositionData>(cp))return result;
                float width=EntityManager.GetComponentData<NetCompositionData>(cp).m_Width;
                var g=EntityManager.GetComponentData<EdgeGeometry>(owner);
                AddRoadRibbon(result,cp,g.m_Start,-width/2,width);AddRoadRibbon(result,cp,g.m_End,-width/2,width);
            }
            else if(EntityManager.HasBuffer<ConnectedEdge>(owner))
                foreach(var connection in EntityManager.GetBuffer<ConnectedEdge>(owner,true))
                {
                    var edge=connection.m_Edge;
                    if(!IsRoad(edge)||!EntityManager.HasComponent<Composition>(edge))continue;
                    bool start=EntityManager.GetComponentData<Edge>(edge).m_Start==owner;
                    if(start&&!EntityManager.HasComponent<StartNodeGeometry>(edge)||!start&&!EntityManager.HasComponent<EndNodeGeometry>(edge))continue;
                    var composition=EntityManager.GetComponentData<Composition>(edge);var cp=start?composition.m_StartNode:composition.m_EndNode;
                    if(!EntityManager.HasComponent<NetCompositionData>(cp))continue;
                    var data=EntityManager.GetComponentData<NetCompositionData>(cp);
                    var g=start?EntityManager.GetComponentData<StartNodeGeometry>(edge).m_Geometry:EntityManager.GetComponentData<EndNodeGeometry>(edge).m_Geometry;
                    AddRoadRibbon(result,cp,g.m_Left,-data.m_Width/2,data.m_Width/2+data.m_MiddleOffset);
                    AddRoadRibbon(result,cp,g.m_Right,data.m_MiddleOffset,data.m_Width/2-data.m_MiddleOffset);
                }
            return result;
        }
        float EdgePaintHalfWidth(EdgeStyle style)
        {
            // The 1.2m NetLane projection box includes transparent pixels.
            // Actual alpha footprints measured from the supplied atlas, rounded outwards.
            return EdgePaintPolicy.HalfWidth((int)style);
        }
        void AddSidewalkFootprint(SidewalkFootprint result,Entity cp,Segment segment,float origin,float width)
        {
            if(width<=.01f||!EntityManager.HasBuffer<NetCompositionPiece>(cp))return;
            foreach(var piece in EntityManager.GetBuffer<NetCompositionPiece>(cp,true))
            {
                var name=PieceName(piece.m_Piece);
                if(!name.StartsWith("Sidewalk",System.StringComparison.OrdinalIgnoreCase)||
                   name.IndexOf("To",System.StringComparison.OrdinalIgnoreCase)>=0||
                   name.IndexOf("Drive",System.StringComparison.OrdinalIgnoreCase)>=0||
                   name.IndexOf("Parking",System.StringComparison.OrdinalIgnoreCase)>=0||
                   (piece.m_SectionFlags&(NetSectionFlags.Hidden|NetSectionFlags.HiddenSurface|NetSectionFlags.Underground|NetSectionFlags.Overhead))!=0||
                   (piece.m_PieceFlags&(NetPieceFlags.Side|NetPieceFlags.Bottom))!=0)continue;
                float min=math.max(origin,piece.m_Offset.x-piece.m_Size.x/2),max=math.min(origin+width,piece.m_Offset.x+piece.m_Size.x/2);
                float gutter=SidewalkGutter(piece);
                if(piece.m_Offset.x<0)max-=gutter;else min+=gutter;
                if(max<=min)continue;
                result.Add(Between(segment.m_Left,segment.m_Right,(min-origin)/width,piece.m_Offset.y),
                    Between(segment.m_Left,segment.m_Right,(max-origin)/width,piece.m_Offset.y));
            }
        }
        SidewalkFootprint EdgeSidewalks(Entity owner){if(sidewalkFrameCache.Count>=128)sidewalkFrameCache.Clear();if(!sidewalkFrameCache.TryGetValue(owner,out var value))sidewalkFrameCache[owner]=value=BuildEdgeSidewalks(owner);return value;}
        SidewalkFootprint BuildEdgeSidewalks(Entity owner)
        {
            var result=new SidewalkFootprint();var roads=new HashSet<Entity>();var nodes=new HashSet<Entity>();
            if(IsRoad(owner)){roads.Add(owner);var ends=EntityManager.GetComponentData<Edge>(owner);nodes.Add(ends.m_Start);nodes.Add(ends.m_End);}
            else nodes.Add(owner);
            foreach(var node in nodes)if(EntityManager.HasBuffer<ConnectedEdge>(node))
                foreach(var e in EntityManager.GetBuffer<ConnectedEdge>(node,true))if(IsRoad(e.m_Edge))roads.Add(e.m_Edge);
            foreach(var edge in roads)
            {
                if(!EntityManager.HasComponent<Composition>(edge))continue;
                var cp=EntityManager.GetComponentData<Composition>(edge);var ends=EntityManager.GetComponentData<Edge>(edge);
                if(EntityManager.HasComponent<EdgeGeometry>(edge)&&EntityManager.HasComponent<NetCompositionData>(cp.m_Edge))
                {
                    float width=EntityManager.GetComponentData<NetCompositionData>(cp.m_Edge).m_Width;
                    var shape=EntityManager.GetComponentData<EdgeGeometry>(edge);
                    AddNativeSidewalkMask(result,cp.m_Edge,shape.m_Start,shape.m_End,-width/2,width,false);
                }
                foreach(bool start in new[]{true,false})
                {
                    if(!nodes.Contains(start?ends.m_Start:ends.m_End)||
                       start&&!EntityManager.HasComponent<StartNodeGeometry>(edge)||!start&&!EntityManager.HasComponent<EndNodeGeometry>(edge))continue;
                    var composition=start?cp.m_StartNode:cp.m_EndNode;
                    if(!EntityManager.HasComponent<NetCompositionData>(composition))continue;
                    var data=EntityManager.GetComponentData<NetCompositionData>(composition);
                    var shape=start?EntityManager.GetComponentData<StartNodeGeometry>(edge).m_Geometry:EntityManager.GetComponentData<EndNodeGeometry>(edge).m_Geometry;
                    AddNativeSidewalkMask(result,composition,shape.m_Left,shape.m_Left,-data.m_Width/2,data.m_Width/2+data.m_MiddleOffset,true,new NodeSurfaceSync{Enabled=true,Source=data.m_SyncVertexOffsetsLeft,Target=shape.m_SyncVertexTargetsLeft});
                    AddNativeSidewalkMask(result,composition,shape.m_Right,shape.m_Right,data.m_MiddleOffset,data.m_Width/2-data.m_MiddleOffset,true,new NodeSurfaceSync{Enabled=true,Source=data.m_SyncVertexOffsetsRight,Target=shape.m_SyncVertexTargetsRight});
                }
            }
            return result;
        }
        IEnumerable<Bezier4x3> RoadOnlyEdge(Entity owner,EdgeStyle style,Bezier4x3 curve,List<RoadRibbon> ribbons=null,SidewalkFootprint sidewalks=null,float? paintHalf=null,bool unionSurface=false)
        {
            if(ribbons==null)ribbons=EdgeRoadRibbons(owner);if(sidewalks==null)sidewalks=EdgeSidewalks(owner);float half=paintHalf??EdgePaintHalfWidth(style);
            var surfaces=new List<RoadSurfaceFootprint>();Geometry.Arc unionArc=null;
            if(unionSurface){foreach(var ribbon in ribbons)surfaces.Add(new RoadSurfaceFootprint(ribbon.Left,ribbon.Right));unionArc=new Geometry.Arc(curve);}
            // Keep intact cubics and cut unsafe runs. Do not turn a curved
            // correction into hundreds of .125 m paint entities.
            foreach(var part in Geometry.ClearSlices(curve,p=>
            {
                bool inside=false;
                foreach(var ribbon in ribbons)
                    if(ribbon.ContainsPaint(p,half)){inside=true;break;}
                if(!inside&&unionSurface)inside=RoadSurfaceFootprint.UnionContains(surfaces,p,Geometry.Left(Geometry.Tangent(curve,unionArc.Nearest(p))),half);
                return inside&&!sidewalks.Overlaps(p,half+.1f);
            }))yield return part;
        }
    }
}
