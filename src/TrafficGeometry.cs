using System.Collections.Generic;
using System.Diagnostics;
using Colossal.Mathematics;
using Game;
using Game.Common;
using Game.Net;
using Game.Pathfind;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
namespace RoadStyleIntoKorea
{
    public partial class StyleSystem
    {
        sealed class FlowCurve {internal Curve Original,Applied;}
        readonly Dictionary<Entity,FlowCurve> flowCurves=new Dictionary<Entity,FlowCurve>();
        readonly Dictionary<Entity,Dictionary<PathNode,float3>> flowPlans=new Dictionary<Entity,Dictionary<PathNode,float3>>();
        readonly HashSet<Entity> flowTouched=new HashSet<Entity>();
        readonly Dictionary<Entity,Curve> saveFlow=new Dictionary<Entity,Curve>();
        Bezier4x3 SourceCurve(Entity lane)
        {
            var current=WalkBase(lane,EntityManager.GetComponentData<Curve>(lane));
            if(flowCurves.TryGetValue(lane,out var value))
            {
                if(Geometry.SameCurve(current.m_Bezier,value.Applied.m_Bezier))return value.Original.m_Bezier;
                // Native reconstruction supersedes our snapshot, even if the entity was reused.
                flowCurves.Remove(lane);
            }
            return current.m_Bezier;
        }
        List<Entity> TrafficLanes(Entity node)
        {
            var owners=new HashSet<Entity>{node};
            if(EntityManager.HasBuffer<ConnectedEdge>(node))foreach(var edge in EntityManager.GetBuffer<ConnectedEdge>(node,true))owners.Add(edge.m_Edge);
            var result=new List<Entity>();var seen=new HashSet<Entity>();
            foreach(var owner in owners)if(Live(owner)&&EntityManager.HasBuffer<SubLane>(owner))
                foreach(var item in EntityManager.GetBuffer<SubLane>(owner,true))
                {
                    var lane=item.m_SubLane;
                    if(Live(lane)&&!EntityManager.HasComponent<ManualUTurnRoute>(lane)&&seen.Add(lane)&&EntityManager.HasComponent<Lane>(lane)&&EntityManager.HasComponent<Curve>(lane)&&
                        (EntityManager.HasComponent<Game.Net.CarLane>(lane)||EntityManager.HasComponent<Game.Net.PedestrianLane>(lane)))result.Add(lane);
                }
            return result;
        }
        bool ConnectedEndpoint(Entity node,PathNode key)
        {
            if(key.Equals(default(PathNode)))return false;
            int endpoints=0;
            foreach(var lane in TrafficLanes(node))
            {
                var link=EntityManager.GetComponentData<Lane>(lane);
                if(link.m_MiddleNode.Equals(key))return false; // cannot move an interior attachment as an endpoint
                if(link.m_StartNode.Equals(key))endpoints++;
                if(link.m_EndNode.Equals(key))endpoints++;
            }
            return endpoints>=2;
        }
        bool CanRelocateCrosswalk(Entity lane,Entity node)
        {
            if(!S.SyncTraffic)return true;
            if(!EntityManager.HasComponent<Lane>(lane))return false;
            var link=EntityManager.GetComponentData<Lane>(lane);
            if(!ConnectedEndpoint(node,link.m_StartNode)||!ConnectedEndpoint(node,link.m_EndNode))return false;
            foreach(var other in TrafficLanes(node))if(other!=lane&&IsCrosswalk(other)&&!MarkingPolicy.SameCrossing(CurveOf(lane),CurveOf(other)))
            {
                var next=EntityManager.GetComponentData<Lane>(other);
                if(link.m_StartNode.Equals(next.m_StartNode)||link.m_StartNode.Equals(next.m_EndNode)||link.m_EndNode.Equals(next.m_StartNode)||link.m_EndNode.Equals(next.m_EndNode))return false;
            }
            return true;
        }
        void PlanPoint(Entity node,PathNode key,float3 point)
        {
            if(!flowPlans.TryGetValue(node,out var plan))flowPlans[node]=plan=new Dictionary<PathNode,float3>();
            plan[key]=point;
        }
        void PlanPedestrianFlow(Entity node,List<Entity> lanes)
        {
            if(!S.SyncTraffic||!EntityManager.HasComponent<Node>(node))return;
            foreach(var lane in lanes)if(IsCrosswalk(lane)&&EntityManager.HasComponent<Lane>(lane))
            {
                var target=CrosswalkCurve(lane);if(crossingTrafficPlans.TryGetValue(lane,out var pedestrian))target=pedestrian;var source=CurveOf(lane);
                if(Geometry.SameCurve(target,source)&&!flowCurves.ContainsKey(lane))continue;
                var link=EntityManager.GetComponentData<Lane>(lane);
                PlanPoint(node,link.m_StartNode,target.a);PlanPoint(node,link.m_EndNode,target.d);
            }
        }
        void PlanStopFlow(Entity edge,List<Entity> lanes,List<Drive> drives)
        {
            if(!S.SyncTraffic)return;
            foreach(var drive in drives)
            {
                if(!Live(drive.End))continue;
                foreach(var lane in lanes)
                {
                    if(!EntityManager.HasComponent<Game.Net.CarLane>(lane)||!EntityManager.HasComponent<Lane>(lane)||EntityManager.HasComponent<MasterLane>(lane))continue;
                    var source=CurveOf(lane);if(math.distance(source.d,drive.Curve.d)>.05f)continue;
                    if(!drive.StopPainted&&!flowCurves.ContainsKey(lane))continue;
                    var key=EntityManager.GetComponentData<Lane>(lane).m_EndNode;
                    if(!ConnectedEndpoint(drive.End,key)){continue;}
                    PlanPoint(drive.End,key,drive.StopPainted?drive.Arc.At(drive.Stop):source.d);
                }
            }
        }
        void MarkTrafficUpdated(Entity lane)
        {
            if(!EntityManager.HasComponent<Updated>(lane))EntityManager.AddComponent<Updated>(lane);
            if(!EntityManager.HasComponent<PathfindUpdated>(lane))EntityManager.AddComponent<PathfindUpdated>(lane);
            DirtyBatch(lane);
            if(EntityManager.HasComponent<Owner>(lane))
            {
                var owner=EntityManager.GetComponentData<Owner>(lane).m_Owner;
                if(Live(owner))flowTouched.Add(owner); // Updating the owner rebuilds native lanes and undoes the stop point.
            }
        }
        internal void ApplyTrafficGeometry()
        {
            flowTouched.Clear();
            using(var deleted=staleFlow.ToEntityArray(Allocator.Temp))foreach(var lane in deleted){flowCurves.Remove(lane);saveFlow.Remove(lane);walkCurves.Remove(lane);saveWalkCurves.Remove(lane);}
            if(saving||!ready||S==null)return;
            if(!S.Enabled||!S.SyncTraffic||restoreRequested||restoring||(!active&&records.Count==0&&approachRecords.Count==0)){RestoreFlow();return;}
            if(flowPlans.Count==0)return;
            if(loadBatchActive&&HasStyleLoadWork())return;
            EntityManager.CompleteAllTrackedJobs();
            var timer=Stopwatch.StartNew();int count=0;
            foreach(var node in new List<Entity>(flowPlans.Keys))
            {
                if(count>=(loadBatchActive?1:S.RoadsPerFrame)||timer.Elapsed.TotalMilliseconds>=S.Milliseconds)break;
                if(pending.Contains(node))continue;
                bool waiting=false;
                if(EntityManager.HasBuffer<ConnectedEdge>(node))foreach(var edge in EntityManager.GetBuffer<ConnectedEdge>(node,true))if(pending.Contains(edge.m_Edge)){waiting=true;break;}
                if(waiting)continue;
                var points=flowPlans[node];
                if(!SafeNode(node)){flowPlans.Remove(node);continue;}
                // A freshly rebuilt network must be replanned from its new lanes first.
                if(EntityManager.HasComponent<Updated>(node))
                {continue;}
                flowPlans.Remove(node);
                int changed=0;var changedOwners=new HashSet<Entity>();
                foreach(var lane in TrafficLanes(node))
                {
                    var source=SourceCurve(lane);var link=EntityManager.GetComponentData<Lane>(lane);
                    bool a=points.TryGetValue(link.m_StartNode,out var first),d=points.TryGetValue(link.m_EndNode,out var last);
                    if(!a&&!d)continue;
                    RestoreWalkLane(lane);
                    var current=EntityManager.GetComponentData<Curve>(lane).m_Bezier;
                    var target=Geometry.MoveEndpoints(source,a?first:current.a,d?last:current.d);
                    if(Geometry.SameCurve(target,EntityManager.GetComponentData<Curve>(lane).m_Bezier))continue;
                    if(!flowCurves.TryGetValue(lane,out var snapshot))flowCurves[lane]=snapshot=new FlowCurve{Original=EntityManager.GetComponentData<Curve>(lane)};
                    float length=new Geometry.Arc(target).Length;
                    if(!TrafficCurvePolicy.Valid(target,length)){continue;}
                    snapshot.Applied=new Curve{m_Bezier=target,m_Length=length};
                    EntityManager.SetComponentData(lane,snapshot.Applied);MarkTrafficUpdated(lane);changed++;
                    if(EntityManager.HasComponent<Owner>(lane))changedOwners.Add(EntityManager.GetComponentData<Owner>(lane).m_Owner);
                }
count++;
                // Native secondary paint can be regenerated by the overlap pass that follows.
                // Reclip those new visuals once, without recursively modifying the road topology.
                if(changed>0)foreach(var owner in changedOwners)EnqueueTrafficPaint(owner);
            }
        }
        bool OwnTrafficUpdate(Entity lane)
        {
            if(!flowCurves.TryGetValue(lane,out var snapshot)||!EntityManager.HasComponent<Curve>(lane))return false;
            var owner=EntityManager.GetComponentData<Owner>(lane).m_Owner;
            return !EntityManager.HasComponent<Updated>(owner)&&Geometry.SameCurve(WalkBase(lane,EntityManager.GetComponentData<Curve>(lane)).m_Bezier,snapshot.Applied.m_Bezier);
        }
        void RestoreFlow()
        {
            foreach(var lane in new List<Entity>(flowCurves.Keys))RestoreWalkLane(lane);
            foreach(var pair in flowCurves)
                if(Live(pair.Key)&&Geometry.SameCurve(EntityManager.GetComponentData<Curve>(pair.Key).m_Bezier,pair.Value.Applied.m_Bezier))
                {EntityManager.SetComponentData(pair.Key,pair.Value.Original);MarkTrafficUpdated(pair.Key);}
            flowCurves.Clear();flowPlans.Clear();
            if(active&&S!=null&&S.Enabled&&!restoring&&!restoreRequested)foreach(var owner in flowTouched)Enqueue(owner);
        }
        void SuspendFlowForSave()
        {
            saveFlow.Clear();
            foreach(var pair in flowCurves)if(Live(pair.Key)&&Geometry.SameCurve(EntityManager.GetComponentData<Curve>(pair.Key).m_Bezier,pair.Value.Applied.m_Bezier))
            {saveFlow[pair.Key]=pair.Value.Applied;EntityManager.SetComponentData(pair.Key,pair.Value.Original);}
        }
        void ResumeFlowAfterSave()
        {
            foreach(var pair in saveFlow)if(Live(pair.Key))EntityManager.SetComponentData(pair.Key,pair.Value);
            saveFlow.Clear();
        }
    }
    // Execute after native LaneSystem, before overlaps, signals and pathfinding consume coordinates.
    public partial class TrafficGeometrySystem:GameSystemBase
    {protected override void OnUpdate(){var s=World.GetExistingSystemManaged<StyleSystem>();s?.ApplyTrafficGeometry();s?.ApplyWalkHeights();}}
}
