using System.Collections.Generic;
using Colossal.Mathematics;
using Game.Net;
using Unity.Entities;
using Unity.Mathematics;
namespace RoadStyleIntoKorea
{
    public partial class StyleSystem
    {
        sealed class EdgeBoundary
        {
            internal Bezier4x3 Curve;internal EdgeStyle Style;internal bool Left;
        }
        EdgeBoundary NearestBoundary(List<EdgeBoundary> paths,bool left,bool start,float3 node)
        {
            EdgeBoundary best=null;float distance=float.MaxValue;
            foreach(var path in paths)
            {
                if(path.Left!=left)continue;
                float candidate=math.distancesq(start?path.Curve.a:path.Curve.d,node);
                if(candidate<distance){distance=candidate;best=path;}
            }
            return best;
        }
        void JoinEdgeLines(Entity node,Record record,bool inspect=false)
        {
            if(!SafeNode(node)||!EntityManager.HasComponent<Node>(node))return;
            var roads=new List<Entity>();
            foreach(var connection in EntityManager.GetBuffer<ConnectedEdge>(node,true))
            {
                if(!IsRoad(connection.m_Edge))return;
                if(!roads.Contains(connection.m_Edge))roads.Add(connection.m_Edge);
            }
            // A seam joins two road sections; never draw a shortcut through a three-way junction.
            if(roads.Count==1){JoinDeadEnd(node,record,roads[0],false,inspect);return;}
            if(roads.Count>2){JoinJunctionEdges(node,record,roads,inspect);return;}
            if(roads.Count!=2)return;
            var a=EntityManager.GetComponentData<Edge>(roads[0]);var b=EntityManager.GetComponentData<Edge>(roads[1]);
            bool aStart=a.m_Start==node,bStart=b.m_Start==node;
            // Test the ROAD angle. A bay taper's curb tangent is deliberately
            // diagonal even when the two road centerlines continue straight.
            var roadInto=Geometry.Tangent(CurveOf(roads[0]),aStart?0:1)*(aStart?-1:1);
            var roadAway=Geometry.Tangent(CurveOf(roads[1]),bStart?0:1)*(bStart?1:-1);
            // Native node lanes already encode the bend; no road-angle rejection here.
            var origin=EntityManager.GetComponentData<Node>(node).m_Position;
            var ribbons=new List<RoadRibbon>(EdgeRoadRibbons(node));foreach(var road in roads)ribbons.AddRange(EdgeRoadRibbons(road));var sidewalks=EdgeSidewalks(node);
            var tapers=new EdgeTaperPolicy(Geometry.Line(origin-roadInto*50,origin+roadInto*50));
            bool bay=false;
            foreach(var road in roads)foreach(var lane in Lanes(road))bay|=assets.Name(Prefab(lane)).IndexOf("Car Bay",System.StringComparison.OrdinalIgnoreCase)>=0;
            // Ordinary two-road width transitions must connect. Only bus-bay joins
            // inherit the user's widening/narrowing omission rule.
            if(bay){foreach(var ribbon in ribbons){tapers.Add(ribbon.Left);tapers.Add(ribbon.Right);}foreach(var boundary in sidewalks.Boundaries)tapers.Add(boundary);}
            else tapers=null;
            var aLanes=Lanes(roads[0]);var bLanes=Lanes(roads[1]);
            var aPaths=new List<EdgeBoundary>();var bPaths=new List<EdgeBoundary>();
            Edges(roads[0],record,aLanes,Drives(aLanes),aPaths);Edges(roads[1],record,bLanes,Drives(bLanes),bPaths);
            // Meet rendered endpoints, not untrimmed lane endpoints one metre beyond them.
            aPaths=VisibleJoinBoundaries(roads[0],aPaths,aLanes);
            bPaths=VisibleJoinBoundaries(roads[1],bPaths,bLanes);
            foreach(bool left in new[]{true,false})
            {
                var first=NearestBoundary(aPaths,left,aStart,origin);
                var second=NearestBoundary(bPaths,EdgeJoinPolicy.OtherSide(left,aStart,bStart),bStart,origin);
                if(first==null||second==null||first.Style==EdgeStyle.None||second.Style==EdgeStyle.None){continue;}
                var p=aStart?first.Curve.a:first.Curve.d;var q=bStart?second.Curve.a:second.Curve.d;
                var into=Geometry.Tangent(first.Curve,aStart?0:1)*(aStart?-1:1);
                var away=Geometry.Tangent(second.Curve,bStart?0:1)*(bStart?1:-1);
                // Fit the seam, then keep the road and sidewalk clearance checks.
                // Native curb masks cut the unsafe subranges after connection fitting.
                List<Bezier4x3> joins;
                if(NativeEdgeJoin(node,roads[0],roads[1],p,into,q,away,out var native))joins=EdgeJoinPolicy.NativeParts(native,p,into,q,away);
                else if(EdgeJoinPolicy.TryCurve(p,into,q,away,out var fallback))joins=new List<Bezier4x3>{fallback};
                else continue;
                float total=0;foreach(var part in joins)total+=new Geometry.Arc(part).Length;
                float distance=0;
                foreach(var join in joins){
                    var arc=new Geometry.Arc(join);float split=total*.5f-distance;
                    if(first.Style==second.Style||split>=arc.Length)PaintJoin(node,record,first.Style,join,ribbons,sidewalks,tapers,inspect,seam:true);
                    else if(split<=0)PaintJoin(node,record,second.Style,join,ribbons,sidewalks,tapers,inspect,seam:true);
                    else {
                        PaintJoin(node,record,first.Style,arc.Slice(0,split),ribbons,sidewalks,tapers,inspect,seam:true);
                        PaintJoin(node,record,second.Style,arc.Slice(split,arc.Length),ribbons,sidewalks,tapers,inspect,seam:true);
                    }
                    distance+=arc.Length;
                }
            }
        }
        // Like SecondaryLaneSystem, derive markings from the game's connected node lane.
        bool NativeEdgeJoin(Entity node,Entity firstRoad,Entity secondRoad,float3 start,float3 into,float3 end,float3 away,out Bezier4x3 result)
        {
            result=default;float best=float.MaxValue;
            foreach(var lane in Lanes(node))
            {
                if(!EntityManager.HasComponent<CarLane>(lane)||EntityManager.HasComponent<MasterLane>(lane)||!EntityManager.HasComponent<Lane>(lane))continue;
                var flags=EntityManager.GetComponentData<CarLane>(lane).m_Flags;
                if((flags&(CarLaneFlags.UTurnLeft|CarLaneFlags.UTurnRight|CarLaneFlags.SideConnection|CarLaneFlags.Forbidden))!=0)continue;
                var link=EntityManager.GetComponentData<Lane>(lane);var curve=CurveOf(lane);
                int a=link.m_StartNode.GetOwnerIndex(),b=link.m_EndNode.GetOwnerIndex();
                if(a==secondRoad.Index&&b==firstRoad.Index)curve=Geometry.Reverse(curve);
                else if(a!=firstRoad.Index||b!=secondRoad.Index)continue;
                if(!EdgeJoinPolicy.NativeOffset(curve,start,into,end,away,out var candidate,out float score)||score>=best)continue;
                best=score;result=candidate;
            }
            return best<float.MaxValue;
        }
        List<EdgeBoundary> VisibleJoinBoundaries(Entity road,List<EdgeBoundary> candidates,List<Entity> lanes){
            var result=new List<EdgeBoundary>();var context=new EdgeClearanceContext{Owner=road,Ribbons=EdgeRoadRibbons(road)};
            var crossings=Crossings(road,lanes);
            foreach(var candidate in candidates){
                if(candidate.Style==EdgeStyle.None||candidate.Style==EdgeStyle.Original)continue;
                foreach(var part in EdgePaintParts(road,candidate.Style,candidate.Curve,crossings,context))
                    result.Add(new EdgeBoundary{Curve=part,Left=candidate.Left,Style=candidate.Style});
            }
            return result;
        }
        EdgeStyle JunctionEdgeStyle(Entity road,float3 point){
            var arc=new Geometry.Arc(CurveOf(road));float t=arc.Nearest(point);
            bool left=math.dot(point-Geometry.Position(arc.Curve,t),Geometry.Left(Geometry.Tangent(arc.Curve,t)))>0;
            var choice=EntityManager.HasComponent<EdgeChoice>(road)?EntityManager.GetComponentData<EdgeChoice>(road):default;
            int code=left?choice.Left:choice.Right;return code==0?(active?S.DefaultEdge:EdgeStyle.Original):(EdgeStyle)(code-1);
        }
        void JoinJunctionEdges(Entity node,Record record,List<Entity> roads,bool inspect){
            var painted=new List<Bezier4x3>();var ribbons=EdgeRoadRibbons(node);var sidewalks=EdgeSidewalks(node);
            foreach(var lane in Lanes(node)){
                if(!EntityManager.HasComponent<CarLane>(lane)||EntityManager.HasComponent<MasterLane>(lane)||!EntityManager.HasComponent<Lane>(lane))continue;
                var flags=EntityManager.GetComponentData<CarLane>(lane).m_Flags;
                if((flags&CarLaneFlags.RightLimit)==0||(flags&(CarLaneFlags.UTurnLeft|CarLaneFlags.UTurnRight|CarLaneFlags.SideConnection|CarLaneFlags.Forbidden))!=0)continue;
                var c=CurveOf(lane);if(math.dot(Geometry.Tangent(c,0),Geometry.Tangent(c,1))<.94f)continue;
                var link=EntityManager.GetComponentData<Lane>(lane);Entity a=Entity.Null,b=Entity.Null;
                foreach(var road in roads){if(road.Index==link.m_StartNode.GetOwnerIndex())a=road;if(road.Index==link.m_EndNode.GetOwnerIndex())b=road;}
                if(a==Entity.Null||b==Entity.Null||a==b)continue;
                var first=JunctionEdgeStyle(a,c.a);var last=JunctionEdgeStyle(b,c.d);
                if(first==EdgeStyle.Original||last==EdgeStyle.Original||first==EdgeStyle.None||last==EdgeStyle.None)continue;
                var boundary=Geometry.Offset(c,-EdgeTaperPolicy.OutsideOffset(Width(lane),EdgePaintHalfWidth(first),S.EdgeLaneGap));
                if(painted.Exists(x=>Geometry.SameCurve(x,boundary)))continue;painted.Add(boundary);
                // A road-surface union alone includes the branch mouth. Reject any
                // crossing of another driving path before drawing the boundary.
                var others=new List<Geometry.Arc>();foreach(var other in Lanes(node))if(other!=lane&&EntityManager.HasComponent<CarLane>(other)&&!EntityManager.HasComponent<MasterLane>(other))others.Add(new Geometry.Arc(CurveOf(other)));
                foreach(var safe in Geometry.ClearSlices(boundary,p=>{foreach(var path in others){float t=path.Nearest(p);if(math.distance(p.xz,Geometry.Position(path.Curve,t).xz)<.75f&&math.abs(math.dot(Geometry.Tangent(path.Curve,t),Geometry.Tangent(boundary,.5f)))<.85f)return false;}return true;})){
                    var arc=new Geometry.Arc(safe);if(first==last)PaintJoin(node,record,first,safe,ribbons,sidewalks,null,inspect);
                    else {PaintJoin(node,record,first,arc.Slice(0,arc.Length/2),ribbons,sidewalks,null,inspect);PaintJoin(node,record,last,arc.Slice(arc.Length/2,arc.Length),ribbons,sidewalks,null,inspect);}
                }
            }
        }
        void PaintJoin(Entity owner,Record record,EdgeStyle style,Bezier4x3 curve,List<RoadRibbon> ribbons=null,SidewalkFootprint sidewalks=null,EdgeTaperPolicy tapers=null,bool inspect=false,bool seam=false)
        {
            foreach(var untapered in Geometry.ClearSlices(curve,p=>tapers==null||tapers.Clear(p)))
            foreach(var roadPart in RoadOnlyEdge(owner,style,untapered,ribbons,sidewalks,unionSurface:true))
            foreach(var clear in seam?ClearPaint(roadPart,NearbyCrossings(owner),S.PaintClearance):BoundaryPaint(owner,roadPart,S.PaintClearance))
            {
                if(inspect)continue;
                int before=record.Paint.Count;
                PaintDrivewayEdgeCore(owner,record,style,clear,Driveways(owner));
                for(int i=before;i<record.Paint.Count;i++)record.EdgeJoins.Add(record.Paint[i]);
            }
        }
        void ClearEdgeJoins(Record record)
        {
            foreach(var entity in record.EdgeJoins)
            {
                if(EntityManager.Exists(entity)&&!EntityManager.HasComponent<Game.Common.Deleted>(entity))RetirePaint(entity);
                record.Paint.Remove(entity);
            }
            record.EdgeJoins.Clear();
        }
        void ClearEdgePaint(Record record)
        {
            ClearSideRoadMarks(record);
            foreach(var entity in record.EdgePaint)
            {
                if(EntityManager.Exists(entity)&&!EntityManager.HasComponent<Game.Common.Deleted>(entity))RetirePaint(entity);
                record.Paint.Remove(entity);
            }
            foreach(var entity in record.EdgeHidden)
            {
                RestoreHidden(entity,record);
                record.Hidden.Remove(entity);
            }
            record.EdgePaint.Clear();record.EdgeHidden.Clear();
        }
    }
}
