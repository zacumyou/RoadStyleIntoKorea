using System.Collections.Generic;
using Colossal.Mathematics;
using Game.Net;
using Game.Prefabs;
using Unity.Entities;
using Unity.Mathematics;
using CarLane=Game.Net.CarLane;
namespace RoadStyleIntoKorea
{
    public partial class StyleSystem
    {
        readonly Dictionary<Entity,List<DrivewayPolicy.Connection>> drivewayWork=new Dictionary<Entity,List<DrivewayPolicy.Connection>>();
        readonly HashSet<Entity> drivewayDirty=new HashSet<Entity>(),drivewayQueued=new HashSet<Entity>();
        float drivewaySince=-1,drivewayLast;
        bool SideVehicleConnection(Entity lane)=>EntityManager.HasComponent<CarLane>(lane)
            &&(EntityManager.GetComponentData<CarLane>(lane).m_Flags&CarLaneFlags.SideConnection)!=0;
        float2 DrivewayWidths(Entity lane)
        {
            var widths=new float2(Width(lane));
            if(EntityManager.HasComponent<NodeLane>(lane))widths+=EntityManager.GetComponentData<NodeLane>(lane).m_WidthOffset;
            return math.max(.1f,widths);
        }
        List<DrivewayPolicy.Connection> Driveways(Entity owner,List<Entity> lanes=null)
        {
            if(inWorkItem&&drivewayWork.TryGetValue(owner,out var cached))return cached;
            List<DrivewayPolicy.Connection> result=null;
            AddDriveways(owner,lanes??Lanes(owner),ref result);
            if(EntityManager.HasComponent<Edge>(owner)){
                var edge=EntityManager.GetComponentData<Edge>(owner);
                AddDriveways(owner,Lanes(edge.m_Start),ref result,true);
                if(edge.m_End!=edge.m_Start)AddDriveways(owner,Lanes(edge.m_End),ref result,true);
            }
            if(inWorkItem)drivewayWork[owner]=result;
            return result;
        }
        void AddDriveways(Entity owner,List<Entity> lanes,ref List<DrivewayPolicy.Connection> result,bool atNode=false)
        {
            foreach(var lane in lanes)
            {
                if(!SideVehicleConnection(lane)||!EntityManager.HasComponent<Lane>(lane)||EntityManager.HasComponent<MasterLane>(lane)||EntityManager.HasComponent<Game.Net.ParkingLane>(lane)||!MotorLanePrefab(Prefab(lane)))continue;
                if((EntityManager.GetComponentData<CarLane>(lane).m_Flags&CarLaneFlags.Forbidden)!=0)continue;
                var link=EntityManager.GetComponentData<Lane>(lane);
                if(atNode&&link.m_StartNode.GetOwnerIndex()!=owner.Index&&link.m_EndNode.GetOwnerIndex()!=owner.Index)continue;
                // Native road-side connectors have a real external path owner. Internal turns do not.
                if(link.m_StartNode.GetOwnerIndex()==owner.Index&&link.m_EndNode.GetOwnerIndex()==owner.Index)continue;
                if(result==null)result=new List<DrivewayPolicy.Connection>();
                result.Add(new DrivewayPolicy.Connection(CurveOf(lane),DrivewayWidths(lane)));
            }
        }
        void PaintDrivewayEdge(Entity owner,Record record,EdgeStyle style,Bezier4x3 curve,List<DrivewayPolicy.Connection> connections,bool left)
        {
            var openings=SideRoadOpenings(owner);
            if(openings.Count==0){PaintDrivewayEdgeCore(owner,record,style,curve,connections);return;}
            var arc=new Geometry.Arc(curve);float start=0;
            var solid=assets.Get(AssetCatalog.Edge(style));
            var bends=new List<(float Lo,float Hi,SideRoadOpening Opening,float Inset)>();
            foreach(var opening in openings){
                if(opening.Side!=(left?1:-1))continue;
                float t=opening.Road.T(opening.Distance);var center=opening.Road.At(opening.Distance);
                var at=Geometry.Position(curve,arc.Nearest(center));var tangent=Geometry.Tangent(opening.Road.Curve,t);
                if(math.abs(math.dot(at-center,tangent))>2)continue;
                float inset=math.max(.05f,opening.Inner-math.dot(at-center,Geometry.Left(tangent))*opening.Side);
                inset=math.min(inset,opening.Width*.4f);
                float3 first=opening.CutSurface?opening.MarkPath(-1)[0]:opening.Bend(-1,inset).a;
                float3 last=opening.CutSurface?opening.MarkPath(1)[0]:opening.Bend(1,inset).a;
                float a=arc.Distance(arc.Nearest(first)),b=arc.Distance(arc.Nearest(last));
                bends.Add((math.min(a,b),math.max(a,b),opening,inset));
            }
            bends.Sort((a,b)=>a.Lo.CompareTo(b.Lo));
            foreach(var item in bends){
                if(item.Lo-start>.025f)PaintDrivewayEdgeCore(owner,record,style,arc.Slice(start,item.Lo),connections);
                if(solid!=Entity.Null&&item.Opening.Width>=6f&&!item.Opening.HideMarks)foreach(int end in new[]{-1,1}){
                    if(item.Opening.CutSurface){
                        var path=item.Opening.MarkPath(end);
                        // Do not pull the shared entrance boundary toward a distant
                        // lane curve on a bus bay. Only bridge an adjacent paint join.
                        var adjacent=Geometry.Position(curve,arc.Nearest(path[0]));
                        if(math.distance(adjacent.xz,path[0].xz)>.025f&&math.distance(adjacent.xz,path[0].xz)<.5f)
                            path.Insert(0,adjacent);
                        PaintSideRoadMarkPath(record,style,item.Opening,path);
                        continue;
                    }
                    var bend=item.Opening.Bend(end,item.Inset);
                    var join=Geometry.Position(curve,arc.Nearest(bend.a));var delta=join-bend.a;
                    bend=new Bezier4x3(join,bend.b+delta,bend.c,bend.d);
                    float phase=PaintSideRoadMark(record,style,item.Opening,bend);
                    var finish=item.Opening.Point(end*(item.Opening.Width*.5f-item.Inset),item.Opening.Outer-item.Opening.Inner);
                    if(math.distance(bend.d,finish)>.025f)PaintSideRoadMark(record,style,item.Opening,Geometry.Line(bend.d,finish),phase);
                }
                start=math.max(start,item.Hi);
            }
            if(arc.Length-start>.025f)PaintDrivewayEdgeCore(owner,record,style,arc.Slice(start,arc.Length),connections);
        }
        void PaintDrivewayEdgeCore(Entity owner,Record record,EdgeStyle style,Bezier4x3 curve,List<DrivewayPolicy.Connection> connections)
        {
            var solid=assets.Get(AssetCatalog.Edge(style));
            string name=AssetCatalog.DottedEdge(style);
            var dotted=name.Length==0?Entity.Null:assets.Get(name);
            // A missing optional asset must leave the existing solid edge intact.
            if(connections==null||connections.Count==0||dotted==Entity.Null||!EntityManager.Exists(dotted)||!EntityManager.HasComponent<NetLaneGeometryData>(dotted)){
                PaintLane(owner,record,solid,curve);return;
            }
            var arc=new Geometry.Arc(curve);var intervals=DrivewayPolicy.Intervals(arc,connections);
            if(intervals.Count==0){PaintLane(owner,record,solid,curve);return;}
            float from=0;
            foreach(var interval in intervals){
                if(interval.x-from>.025f)PaintLane(owner,record,solid,arc.Slice(from,interval.x));
                if(interval.y-interval.x>.025f)PaintLane(owner,record,dotted,arc.Slice(interval.x,interval.y));
                from=interval.y;
            }
            if(arc.Length-from>.025f)PaintLane(owner,record,solid,arc.Slice(from,arc.Length));
        }
        bool HasStyledEdge(Entity owner)=>S!=null&&S.EdgeLines&&Live(owner)&&records.TryGetValue(owner,out var record)
            &&(record.EdgePaint.Count>0||record.EdgeJoins.Count>0||record.EdgeHidden.Count>0);
        void QueueDrivewayEdge(Entity owner)
        {
            QueueDrivewayOwner(owner);
            if(Live(owner)&&EntityManager.HasComponent<Node>(owner)&&EntityManager.HasBuffer<ConnectedEdge>(owner))
                foreach(var connected in EntityManager.GetBuffer<ConnectedEdge>(owner,true))QueueDrivewayOwner(connected.m_Edge);
        }
        void QueueDrivewayOwner(Entity owner)
        {
            if(!HasStyledEdge(owner))return;
            float now=UnityEngine.Time.realtimeSinceStartup;
            if(drivewayDirty.Count==0)drivewaySince=now;
            drivewayDirty.Add(owner);drivewayLast=now;
        }
        void TickDrivewayEdges()
        {
            if(drivewayDirty.Count==0||!MarkingPolicy.RefreshDue(UnityEngine.Time.realtimeSinceStartup,drivewaySince,drivewayLast))return;
            Entity owner=Entity.Null;foreach(var e in drivewayDirty){owner=e;break;}
            drivewayDirty.Remove(owner);
            if(HasStyledEdge(owner)){
                // Only this category, under the existing frame budget; no road/paint geometry changed.
                drivewayQueued.Add(owner);EnqueueEdgeOnly(owner);
                if(IsRoad(owner)&&EntityManager.HasBuffer<CurbChoice>(owner)){EnqueueCurb(owner);EnqueuePaving(owner);}
            }
            if(drivewayDirty.Count==0)drivewaySince=-1;
        }
        void ClearDriveways(){drivewayWork.Clear();drivewayDirty.Clear();drivewayQueued.Clear();drivewaySince=-1;drivewayLast=0;}
    }
}
