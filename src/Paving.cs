using System;
using System.Collections.Generic;
using Colossal.Mathematics;
using Colossal.Serialization.Entities;
using Game.Common;
using Game.Net;
using Game.Prefabs;
using Unity.Entities;
using Unity.Mathematics;
namespace RoadStyleIntoKorea
{
    [InternalBufferCapacity(0)]
    public struct PavingChoice:IBufferElementData,ISerializable
    {
        public int Section,Kind,Style;public float Min,Max;
        public void Serialize<T>(T w) where T:IWriter {w.Write(1);w.Write(Section);w.Write(Kind);w.Write(Style);w.Write(Min);w.Write(Max);}
        public void Deserialize<T>(T r) where T:IReader {r.Read(out int v);r.Read(out Section);r.Read(out Kind);r.Read(out Style);r.Read(out Min);r.Read(out Max);if(v!=1||Style<0||Style>PavingPolicy.StyleCount)Style=0;}
    }
    public partial class StyleSystem
    {
        internal sealed class PavingRegion
        {
            internal int Section,Kind;internal float Min,Max,Height,RoadWidth;
            internal float PaintMin,PaintMax;
            internal NetCompositionPiece Piece;
            internal Bezier4x3 First,Second;
            internal Geometry.Arc FirstArc,SecondArc;
            internal List<PavingRegion> Group;
        }
        bool pavingActive;
        Entity hoverPavingEdge;int hoverPavingRevision=-1;
        List<PavingRegion> hoverPavingRegions=new List<PavingRegion>();
        readonly HashSet<Entity> pavingOnlyPending=new HashSet<Entity>();
        internal string PavingHint="";
        internal void CyclePavingBrush(){S.PavingBrush=(PavingStyle)PavingPolicy.NextStyle((int)S.PavingBrush);}
        string PieceName(Entity piece)
        {
            var name=assets.Name(piece);if(name.Length>0)return name;
            return World.GetExistingSystemManaged<PrefabSystem>().TryGetPrefab<PrefabBase>(piece,out var p)&&p!=null?p.name:"";
        }
        static Bezier4x3 Between(Bezier4x3 a,Bezier4x3 b,float t,float height)=>new Bezier4x3(
            math.lerp(a.a,b.a,t)+new float3(0,height,0),math.lerp(a.b,b.b,t)+new float3(0,height,0),
            math.lerp(a.c,b.c,t)+new float3(0,height,0),math.lerp(a.d,b.d,t)+new float3(0,height,0));
        List<PavingRegion> PavingRegions(Entity edge)
        {
            var result=new List<PavingRegion>();
            if(!IsRoad(edge)||!EntityManager.HasComponent<Composition>(edge)||!EntityManager.HasComponent<EdgeGeometry>(edge))return result;
            var cp=EntityManager.GetComponentData<Composition>(edge).m_Edge;
            if(!EntityManager.HasBuffer<NetCompositionPiece>(cp)||!EntityManager.HasComponent<NetCompositionData>(cp))return result;
            float width=EntityManager.GetComponentData<NetCompositionData>(cp).m_Width;if(width<1)return result;
            var shape=EntityManager.GetComponentData<EdgeGeometry>(edge);
            var edgeChoice=EntityManager.HasComponent<EdgeChoice>(edge)?EntityManager.GetComponentData<EdgeChoice>(edge):default;
            float paintHalf=math.max(.13f,math.max(EdgePaintHalfWidth((EdgeStyle)math.max(0,edgeChoice.Left-1)),EdgePaintHalfWidth((EdgeStyle)math.max(0,edgeChoice.Right-1))));
            if(active)paintHalf=math.max(paintHalf,EdgePaintHalfWidth(S.DefaultEdge));
            float paintMargin=PavingPolicy.PaintMargin(paintHalf,S.EdgeLaneGap);
            var blocks=new List<float2>();
            if(EntityManager.HasBuffer<NetCompositionLane>(cp))foreach(var lane in EntityManager.GetBuffer<NetCompositionLane>(cp,true))
            {
                if((lane.m_Flags&(LaneFlags.Road|LaneFlags.Parking|LaneFlags.Track))==0||(lane.m_Flags&(LaneFlags.Master|LaneFlags.Secondary|LaneFlags.Virtual))!=0)continue;
                if(!EntityManager.HasComponent<NetLaneData>(lane.m_Lane))continue;
                if((lane.m_Flags&LaneFlags.Road)!=0&&!MotorLanePrefab(lane.m_Lane))continue;
                float half=EntityManager.GetComponentData<NetLaneData>(lane.m_Lane).m_Width*.5f;
                blocks.Add(new float2(lane.m_Position.x-half,lane.m_Position.x+half));
            }
            foreach(var p in EntityManager.GetBuffer<NetCompositionPiece>(cp,true))
            {
                if((p.m_SectionFlags&(NetSectionFlags.Median|NetSectionFlags.Hidden|NetSectionFlags.HiddenSurface|NetSectionFlags.Underground|NetSectionFlags.Overhead))!=0||
                   (p.m_PieceFlags&(NetPieceFlags.Side|NetPieceFlags.Bottom))!=0)continue;
                int kind=PavingPolicy.Kind(PieceName(p.m_Piece));if(kind==0||p.m_Size.x<.5f)continue;
                float min=p.m_Offset.x-p.m_Size.x*.5f,max=p.m_Offset.x+p.m_Size.x*.5f;
                if(kind==1){if(p.m_Offset.x>=0)min+=.6f;else max-=.6f;}
                var spans=new List<float2>{new float2(min,max)};
                foreach(var block in blocks)
                {
                    var remaining=new List<float2>();foreach(var span in spans)
                    {
                        if(block.y<=span.x||block.x>=span.y){remaining.Add(span);continue;}
                        if(block.x>span.x)remaining.Add(new float2(span.x,block.x));
                        if(block.y<span.y)remaining.Add(new float2(block.y,span.y));
                    }
                    spans=remaining;
                }
                foreach(var span in spans)
                {
                    if(span.y-span.x<.5f)continue;
                    if(result.Exists(r=>r.Section==p.m_SectionIndex&&math.abs(r.Min-span.x)<.05f&&math.abs(r.Max-span.y)<.05f))continue;
                    float height=p.m_Offset.y;
                    var r=new PavingRegion{Section=p.m_SectionIndex,Kind=kind,Min=span.x,Max=span.y,Height=height,RoadWidth=width,Piece=p};
                    r.PaintMin=r.Min;r.PaintMax=r.Max;
                    if(kind==2)foreach(var block in blocks){
                        if(block.y<=r.Min+.05f)r.PaintMin=math.max(r.PaintMin,block.y+paintMargin);
                        if(block.x>=r.Max-.05f)r.PaintMax=math.min(r.PaintMax,block.x-paintMargin);
                    }
                    float t=.5f+(r.PaintMin+r.PaintMax)*.5f/width;
                    r.First=Between(shape.m_Start.m_Left,shape.m_Start.m_Right,t,height);
                    r.Second=Between(shape.m_End.m_Left,shape.m_End.m_Right,t,height);
                    r.FirstArc=new Geometry.Arc(r.First);r.SecondArc=new Geometry.Arc(r.Second);result.Add(r);
                }
            }
            foreach(var region in result)region.Group=ConnectedPaving(result,region);
            return result;
        }
        internal PavingRegion PickPaving(Entity edge,float3 hit)
        {
            PavingRegion best=null;float distance=float.MaxValue;
            if(edge!=hoverPavingEdge||hoverPavingRevision!=toolRevision)
            {hoverPavingEdge=edge;hoverPavingRevision=toolRevision;hoverPavingRegions=PavingRegions(edge);}
            foreach(var r in hoverPavingRegions)for(int part=0;part<2;part++)
            {
                var arc=part==0?r.FirstArc:r.SecondArc;float t=arc.Nearest(hit);var q=Geometry.Position(arc.Curve,t);
                float d=math.distance(q.xz,hit.xz);
                if(d>(r.Max-r.Min)*.5f||math.abs(hit.y-q.y)>1.5f||d>=distance)continue;
                distance=d;best=r;
            }
            PavingHint=best==null?"RSIK.Paving.NoTarget":best.Kind==1?"RSIK.Paving.Sidewalk":"RSIK.Paving.Shoulder";
            return best;
        }
        bool Matches(PavingChoice c,PavingRegion r)=>c.Section==r.Section&&c.Kind==r.Kind&&
            (PavingPolicy.Contains(r.Min,r.Max,c.Min,c.Max)||PavingPolicy.Bay(PieceName(r.Piece.m_Piece))&&PavingPolicy.Contains(c.Min,c.Max,r.Min,r.Max));
        internal List<PavingRegion> PavingGroup(Entity edge,PavingRegion selected)
            =>selected?.Group??new List<PavingRegion>();
        static List<PavingRegion> ConnectedPaving(List<PavingRegion> regions,PavingRegion selected)
        {
            var group=new List<PavingRegion>();if(selected==null)return group;group.Add(selected);
            if(selected.Kind!=2)return group;
            for(int i=0;i<group.Count;i++)foreach(var next in regions)
                if(next.Kind==2&&!group.Exists(r=>r.Section==next.Section&&math.abs(r.Min-next.Min)<.05f)&&
                   math.abs(next.Height-group[i].Height)<.05f&&
                   (math.abs(next.Min-group[i].Max)<.05f||math.abs(next.Max-group[i].Min)<.05f))group.Add(next);
            return group;
        }
        int PavingSelection(Entity edge,PavingRegion region,List<PavingChoice> choices)
        {
            foreach(var c in choices)if(Matches(c,region))return c.Style;
            // Existing saves selected the inner RB empty piece only. The adjacent
            // shoulder is part of the same physical surface, not another road.
            foreach(var part in PavingGroup(edge,region))foreach(var c in choices)if(Matches(c,part))return c.Style;
            return 0;
        }
        bool AutomaticPavingRegion(Entity edge,PavingRegion region){
            if(region.Kind!=2)return true;
            if(!EntityManager.HasComponent<Composition>(edge))return false;
            var cp=EntityManager.GetComponentData<Composition>(edge).m_Edge;
            if(!EntityManager.HasBuffer<NetCompositionLane>(cp))return false;
            float min=float.MaxValue,max=float.MinValue;
            foreach(var lane in EntityManager.GetBuffer<NetCompositionLane>(cp,true)){
                if((lane.m_Flags&LaneFlags.Road)==0||(lane.m_Flags&(LaneFlags.Master|LaneFlags.Secondary|LaneFlags.Virtual|LaneFlags.Parking))!=0||!EntityManager.HasComponent<NetLaneData>(lane.m_Lane))continue;
                if((lane.m_Flags&LaneFlags.Road)!=0&&!MotorLanePrefab(lane.m_Lane))continue;
                float half=EntityManager.GetComponentData<NetLaneData>(lane.m_Lane).m_Width*.5f;
                min=math.min(min,lane.m_Position.x-half);max=math.max(max,lane.m_Position.x+half);
            }
            return min<max&&(region.Max<=min+.05f||region.Min>=max-.05f);
        }
        internal void ChoosePaving(Entity edge,PavingRegion region,bool reset)
        {
            if(region==null||S==null||!S.Enabled||!S.Paving||!IsRoad(edge))return;
            if(!reset)
            {
                if(assets.Named.Count==0)assets.Refresh(EntityManager,World.GetExistingSystemManaged<PrefabSystem>());
                foreach(float w in PavingPolicy.Widths)
                {
                    var prefab=assets.Get(PavingPolicy.Asset((int)S.PavingBrush,w));
                    if(prefab==Entity.Null||!EntityManager.Exists(prefab)||!EntityManager.HasComponent<NetLaneGeometryData>(prefab)){Status="보도블록 NetLane 누락 / Missing: "+PavingPolicy.Asset((int)S.PavingBrush,w);return;}
                }
            }
            if(!reset)
            {
                // Resolve once before changing choices or scheduling three owners.
                // A material failure leaves the previous selection and visuals intact.
                try{PavingMaterial((int)S.PavingBrush);}
                catch(Exception error){Status="보도 재질 준비 실패 / Paving material unavailable: "+error.Message;return;}
                if(PavingPolicy.Mixed(PieceName(region.Piece.m_Piece))&&NativePavingTemplate(region.Piece.m_Piece)==null)
                {Status="원본 보도 메시를 읽지 못했습니다 / Native sidewalk mesh unavailable";return;}
            }
            if(!EntityManager.HasBuffer<PavingChoice>(edge)){if(reset)return;EntityManager.AddBuffer<PavingChoice>(edge);}
            var group=PavingGroup(edge,region);
            var choices=EntityManager.GetBuffer<PavingChoice>(edge);
            foreach(var part in group)
            {
                int index=-1;for(int i=0;i<choices.Length;i++)if(Matches(choices[i],part)){index=i;break;}
                if(reset){if(index>=0){ToolResetHandled=true;choices.RemoveAt(index);}}
                else {var c=new PavingChoice{Section=part.Section,Kind=part.Kind,Style=(int)S.PavingBrush,Min=part.Min,Max=part.Max};if(index>=0)choices[index]=c;else choices.Add(c);}
            }
            if(reset&&!ToolResetHandled)return;
            if((EntityManager.HasBuffer<CurbChoice>(edge)||EntityManager.HasBuffer<GutterChoice>(edge)))EnqueueCurb(edge);
            EnqueuePaving(edge);
            var ends=EntityManager.GetComponentData<Edge>(edge);EnqueuePaving(ends.m_Start);EnqueuePaving(ends.m_End);
            pavingActive=true;Status="보도블록 변경 대기 / Paving change queued";
        }
        void EnqueuePaving(Entity owner){if(markingsOnlyPending.Contains(owner)){Enqueue(owner);return;}EnqueueSelective(owner,pavingOnlyPending);}
        void ClearPaving(Record r)
        {
            ReleaseNativePaving(r.WalkOwner);
            ReleaseNativeSideRoadCut(r.WalkOwner);
            r.WalkSurface=null;QueueWalkHeight(r.WalkOwner);
            foreach(var e in r.PavingPaint){if(EntityManager.Exists(e)&&!EntityManager.HasComponent<Deleted>(e))RetirePaint(e);r.Paint.Remove(e);}
            r.PavingPaint.Clear();
            foreach(var surface in r.PavingSurfaces)surface.Dispose();r.PavingSurfaces.Clear();r.PavingFaces.Clear();
        }
        void ApplyPaving(Entity edge,Record r){r.WalkOwner=edge;try{ApplyPavingGeometry(edge,r);}finally{QueueWalkHeight(edge);QueueDetailJoins(edge);if(EntityManager.HasBuffer<ManualDecalChoice>(edge))foreach(var choice in EntityManager.GetBuffer<ManualDecalChoice>(edge,true))if(choice.Kind==9||choice.Kind==10){manualDirty.Add(edge);break;}}}
        void ApplyPavingGeometry(Entity edge,Record r)
        {{
            if(preservePavingForWork)return;
            if(EntityManager.HasComponent<Node>(edge)){ApplyCornerPaving(edge,r);return;}
            if(!EntityManager.HasComponent<EdgeGeometry>(edge))return;
            if(ApplyNativePaving(edge))return;
            PrepareNativeSideRoadCut(edge);
            if(!S.Paving||!EntityManager.HasBuffer<PavingChoice>(edge)){PaintSideRoadAprons(edge,r);return;}
            var choices=new List<PavingChoice>();foreach(var c in EntityManager.GetBuffer<PavingChoice>(edge,true))choices.Add(c);
            var shape=EntityManager.GetComponentData<EdgeGeometry>(edge);
            foreach(var region in PavingRegions(edge))
            {
                int style=PavingSelection(edge,region,choices);
                if(style<1||style>PavingPolicy.StyleCount)continue;
                if(PaintNativePaving(edge,r,style,region,shape))continue;
            }
            PaintSideRoadAprons(edge,r);
        }}
        void ApplyCornerPaving(Entity node,Record record)
        {
            if(!S.Paving||!EntityManager.HasBuffer<ConnectedEdge>(node))return;
            // Copy buffers before PaintLane performs structural changes.
            var edges=new List<Entity>();foreach(var c in EntityManager.GetBuffer<ConnectedEdge>(node,true))edges.Add(c.m_Edge);
            foreach(var edge in edges)
            {
                if(nativePavingBindings.ContainsKey(edge))continue;
                if(!IsRoad(edge)||!EntityManager.HasBuffer<PavingChoice>(edge)||!EntityManager.HasComponent<Composition>(edge))continue;
                var ends=EntityManager.GetComponentData<Edge>(edge);bool start=ends.m_Start==node;
                if(start&&!EntityManager.HasComponent<StartNodeGeometry>(edge)||!start&&!EntityManager.HasComponent<EndNodeGeometry>(edge))continue;
                var cp=EntityManager.GetComponentData<Composition>(edge);
                var composition=start?cp.m_StartNode:cp.m_EndNode;
                if(!EntityManager.HasComponent<NetCompositionData>(composition))continue;
                var data=EntityManager.GetComponentData<NetCompositionData>(composition);
                var shape=start?EntityManager.GetComponentData<StartNodeGeometry>(edge).m_Geometry:EntityManager.GetComponentData<EndNodeGeometry>(edge).m_Geometry;
                if(shape.m_MiddleRadius>0||data.m_Width<1)continue; // roundabout annuli need separate ownership rules
                var choices=new List<PavingChoice>();foreach(var c in EntityManager.GetBuffer<PavingChoice>(edge,true))choices.Add(c);
                foreach(var region in PavingRegions(edge))
                {
                    int style=PavingSelection(edge,region,choices);
                    if(style<1||style>PavingPolicy.StyleCount)continue;
                    // Start-node local cross-sections face into the junction, opposite
                    // to the edge. Preserve the distance from the outside road edge.
                    float min=start?-region.PaintMax:region.PaintMin,max=start?-region.PaintMin:region.PaintMax;
                    bool right=min+max>=0;
                    float shift=(data.m_Width-region.RoadWidth)*.5f*(right?1:-1);min+=shift;max+=shift;
                    // Recover the complete native sidewalk, then reserve .6 m in
                    // physical space rather than a shrinking cross-section ratio.
                    if(region.Kind==1){if(right)min-=.6f;else max+=.6f;}
                    float raise=0;
                    if(region.Kind==2&&CurbSelected(edge,region)){
                        raise=NativeCurbGeometry.Rise;
                    }
                    float split=data.m_MiddleOffset;
                    var segment=right?shape.m_Right:shape.m_Left;
                    float origin=right?split:-data.m_Width*.5f;
                    float width=right?data.m_Width*.5f-split:split+data.m_Width*.5f;
                    if(width<=0||min<origin||max>origin+width||max-min<.5f)continue;
                    var a=Between(segment.m_Left,segment.m_Right,(min-origin)/width,region.Height);
                    var b=Between(segment.m_Left,segment.m_Right,(max-origin)/width,region.Height);
                    bool curbAtA=right;
                    // Keep the same transverse UV direction at a reversed start node.
                    if(start){var swap=a;a=b;b=swap;curbAtA=!curbAtA;}
                    var endPoint=start?CurveOf(edge).a:CurveOf(edge).d;
                    float nearest=float.MaxValue;float3 seam=endPoint;
                    foreach(var point in new[]{region.First.a,region.First.d,region.Second.a,region.Second.d})
                    {float distance=math.distancesq(point,endPoint);if(distance<nearest){nearest=distance;seam=point;}}
                    endPoint=seam;
                    float da=math.distance((a.a+b.a)*.5f,endPoint),dd=math.distance((a.d+b.d)*.5f,endPoint);
                    if(start?da<dd:dd<da){a=Geometry.Reverse(a);b=Geometry.Reverse(b);}
                    float phase=start?float.NaN:region.FirstArc.Length+region.SecondArc.Length;
                    if(PaintNativeCorner(node,record,style,region.Kind,composition,segment,origin,width,min,max,right,start,a,b,phase,curbAtA,right?shape.m_SyncVertexTargetsRight:shape.m_SyncVertexTargetsLeft,raise,PavingPolicy.Bay(PieceName(region.Piece.m_Piece))))continue;
                }
            }
        }
    }
}
