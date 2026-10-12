using System.Collections.Generic;
using Game;
using Game.Common;
using Game.Net;
using Game.Tools;
using Unity.Collections;
using Unity.Entities;

namespace RoadStyleIntoKorea
{
    public partial class StyleSystem
    {
        void QueueInheritedNodeLines(Entity node,bool center=true,bool edge=true){
            if(!SafeNode(node)||!EntityManager.HasBuffer<ConnectedEdge>(node))return;
            bool hasCenter=false,hasEdge=false;
            foreach(var connection in EntityManager.GetBuffer<ConnectedEdge>(node,true)){
                var road=connection.m_Edge;if(!IsRoad(road))continue;
                hasCenter|=EntityManager.HasComponent<CenterChoice>(road)||EntityManager.HasComponent<UTurnChoice>(road);
                hasEdge|=EntityManager.HasComponent<EdgeChoice>(road);
            }
            if(center&&hasCenter)EnqueueCenterOnly(node);
            if(edge&&hasEdge)EnqueueEdgeOnly(node);
        }
        void EnqueueTrafficPaint(Entity owner)
        {
            EnqueueCrossOnly(owner,false);
        }
        void RefreshSecondaryPaint(Entity owner,Entity lane){
            // Native paint regeneration after our endpoint edits is not a road rebuild.
            var name=assets.Name(Prefab(lane));if(!EntityManager.HasComponent<GeneratedPaint>(lane))QueueColorGuides(owner);
            if(EntityManager.HasComponent<Node>(owner)){
                bool center=name.IndexOf("Center Line",System.StringComparison.OrdinalIgnoreCase)>=0;
                bool edge=name.IndexOf("Edge Line",System.StringComparison.OrdinalIgnoreCase)>=0;
                if(center||edge)QueueInheritedNodeLines(owner,center,edge);
            }
            if(MarkingPolicy.IsNativeJunctionMark(name))QueueBoundaryGuides(owner);
            if(SafeNode(owner)&&EntityManager.HasBuffer<GuideChoice>(owner)&&MarkingPolicy.IsJunctionPaint(name)){QueueDerivedGuide(owner);return;}
            if(name.IndexOf("Stop Line",System.StringComparison.OrdinalIgnoreCase)>=0||MarkingPolicy.IsLaneDivider(name)){RequestApproachStops(owner);if(EntityManager.HasBuffer<ManualDecalChoice>(owner)||EntityManager.HasBuffer<DrawnLineChoice>(owner))manualDirty.Add(owner);if(name.IndexOf("Stop Line",System.StringComparison.OrdinalIgnoreCase)>=0)EnqueueCrossOnly(owner,false);return;}
            if(name.IndexOf("Center Line",System.StringComparison.OrdinalIgnoreCase)>=0&&EntityManager.HasComponent<CenterChoice>(owner))EnqueueCenterOnly(owner);
            else if(name.IndexOf("Edge Line",System.StringComparison.OrdinalIgnoreCase)>=0&&EntityManager.HasComponent<EdgeChoice>(owner))EnqueueEdgeOnly(owner);
        }
        const CarLaneFlags VisualLaneFlags=RefreshPolicy.VisualFlags;
        struct LaneRefreshStamp {internal Curve Curve;internal Entity Owner,Prefab;internal CarLaneFlags Flags;internal float Width;internal Unity.Mathematics.float2 ConnectionWidths;internal int Speed;}
        readonly Dictionary<Entity,LaneRefreshStamp> laneRefreshStamps=new Dictionary<Entity,LaneRefreshStamp>();
        bool LaneGeometryChanged(Entity lane){
            if(!Live(lane)||!EntityManager.HasComponent<Curve>(lane))return false;
            var curve=EntityManager.GetComponentData<Curve>(lane);curve.m_Bezier=SourceCurve(lane);var owner=EntityManager.GetComponentData<Owner>(lane).m_Owner;
            var prefab=Prefab(lane);var flags=EntityManager.HasComponent<Game.Net.CarLane>(lane)?EntityManager.GetComponentData<Game.Net.CarLane>(lane).m_Flags:0;float width=Width(lane);int speed=RoadKmh(owner);
            var connectionWidths=(flags&CarLaneFlags.SideConnection)!=0?DrivewayWidths(lane):default;
            bool changed=laneRefreshStamps.TryGetValue(lane,out var old)&&(old.Owner!=owner||old.Prefab!=prefab||(old.Flags&VisualLaneFlags)!=(flags&VisualLaneFlags)||old.Width!=width||!old.ConnectionWidths.Equals(connectionWidths)||old.Speed!=speed||!Geometry.SameCurve(old.Curve.m_Bezier,curve.m_Bezier));
            
            old.Curve=curve;old.Owner=owner;old.Prefab=prefab;old.Flags=flags;old.Width=width;old.ConnectionWidths=connectionWidths;old.Speed=speed;laneRefreshStamps[lane]=old;return changed;
        }
        readonly HashSet<Entity> topologyDirty=new HashSet<Entity>();
        internal bool TrackedStyleOwner(Entity owner)=>ownerShapes.ContainsKey(owner)||records.ContainsKey(owner)||approachRecords.ContainsKey(owner)||approachStopRecords.ContainsKey(owner)||manualRecords.ContainsKey(owner)||curbRecords.ContainsKey(owner)||guideRecords.ContainsKey(owner);
        bool topologyUrgent;
        float dirtySince=-1,dirtyLast;
        EntityQuery changedNodes,changedLanes,staleFlow;
        void CreateRefreshQueries()
        {
            staleFlow=GetEntityQuery(ComponentType.ReadOnly<Lane>(),ComponentType.ReadOnly<Deleted>());
            changedNodes=GetEntityQuery(new EntityQueryDesc{All=new[]{ComponentType.ReadOnly<Node>()},Any=new[]{ComponentType.ReadOnly<Created>(),ComponentType.ReadOnly<Updated>()},None=new[]{ComponentType.ReadOnly<Temp>()}});
            changedLanes=GetEntityQuery(new EntityQueryDesc{All=new[]{ComponentType.ReadOnly<Lane>(),ComponentType.ReadOnly<Owner>()},Any=new[]{ComponentType.ReadOnly<Created>(),ComponentType.ReadOnly<Updated>()},None=new[]{ComponentType.ReadOnly<Temp>(),ComponentType.ReadOnly<GeneratedPaint>(),ComponentType.ReadOnly<ManualUTurnRoute>()}});
        }
        void NoteTopology(Entity owner)
        {
            if((!active&&!pavingActive&&!targetedActive)||owner==Entity.Null||!TrackedStyleOwner(owner)||EntityManager.HasComponent<Temp>(owner)||flowTouched.Contains(owner))return;
            float now=UnityEngine.Time.realtimeSinceStartup;
            if(topologyDirty.Count==0)dirtySince=now;
            if(topologyDirty.Add(owner)){ClearLinePlans();InvalidateToolPreview();}dirtyLast=now;

        }
        IEnumerable<Entity> DirectRefreshNeighbors(Entity owner){
            if(!EntityManager.Exists(owner))yield break;
            if(EntityManager.HasComponent<Edge>(owner)){var edge=EntityManager.GetComponentData<Edge>(owner);yield return edge.m_Start;yield return edge.m_End;}
            else if(EntityManager.HasBuffer<ConnectedEdge>(owner)){var edges=new List<Entity>();foreach(var e in EntityManager.GetBuffer<ConnectedEdge>(owner,true))if(IsRoad(e.m_Edge))edges.Add(e.m_Edge);foreach(var edge in edges)yield return edge;}
        }
        void CollectTopology()
        {{
            if((!active&&!pavingActive&&!targetedActive)||S==null||!S.UpdateEditedRoads)return;
            using(var a=dirty.ToEntityArray(Allocator.Temp))foreach(var e in a)if(OwnerGeometryChanged(e))NoteTopology(e);
            using(var a=changedNodes.ToEntityArray(Allocator.Temp))foreach(var e in a)if(OwnerGeometryChanged(e))NoteTopology(e);
            using(var dead=staleFlow.ToEntityArray(Allocator.Temp))foreach(var e in dead){
                if(laneRefreshStamps.TryGetValue(e,out var previous)&&(previous.Flags&CarLaneFlags.SideConnection)!=0)QueueDrivewayEdge(previous.Owner);
                laneRefreshStamps.Remove(e);
            }
            using(var a=changedLanes.ToEntityArray(Allocator.Temp))foreach(var e in a){
                if(!ownerShapes.ContainsKey(EntityManager.GetComponentData<Owner>(e).m_Owner))continue;
                bool known=laneRefreshStamps.TryGetValue(e,out var previous);bool changed=LaneGeometryChanged(e);
                if(EntityManager.HasComponent<PedestrianLane>(e)&&(!known||changed))QueueWalkHeight(EntityManager.GetComponentData<Owner>(e).m_Owner);
                bool side=SideVehicleConnection(e),wasSide=known&&(previous.Flags&CarLaneFlags.SideConnection)!=0;
                if(side||wasSide){
                    if(changed||!known&&EntityManager.HasComponent<Created>(e)){
                        QueueDrivewayEdge(EntityManager.GetComponentData<Owner>(e).m_Owner);
                        if(wasSide&&previous.Owner!=EntityManager.GetComponentData<Owner>(e).m_Owner)QueueDrivewayEdge(previous.Owner);
                    }
                    if(side)continue;
                }
                if(!known&&EntityManager.HasComponent<Created>(e)){var local=EntityManager.GetComponentData<Owner>(e).m_Owner;if(TrackedStyleOwner(local)){if(EntityManager.HasComponent<CarLane>(e)||EntityManager.HasComponent<PedestrianLane>(e)){RequestApproachStops(local);EnqueueCrossOnly(local,false);}else RefreshSecondaryPaint(local,e);}}
                if(!changed||OwnTrafficUpdate(e))continue;
                var owner=EntityManager.GetComponentData<Owner>(e).m_Owner;
                if(EntityManager.HasComponent<CarLane>(e)||EntityManager.HasComponent<PedestrianLane>(e))NoteTopology(owner);
                else RefreshSecondaryPaint(owner,e);
            }
            TickDrivewayEdges();
            float now=UnityEngine.Time.realtimeSinceStartup;
            if(topologyDirty.Count==0||!MarkingPolicy.RefreshDue(now,dirtySince,dirtyLast,topologyUrgent))return;
            var affected=RefreshPolicy.DirectScope(topologyDirty,DirectRefreshNeighbors);
            foreach(var owner in affected)
            {
                if(EntityManager.Exists(owner)&&EntityManager.HasBuffer<GuideChoice>(owner))guideDirty.Add(owner);
                if(!Live(owner))ReleaseGuides(owner);
                // Node visuals inherit edge choices: records remain authoritative after those edges vanish.
                if(Live(owner)&&(curbRecords.ContainsKey(owner)||EntityManager.HasBuffer<CurbChoice>(owner)||EntityManager.HasBuffer<GutterChoice>(owner)))EnqueueCurb(owner);
                if(active)Enqueue(owner);
                else if(EntityManager.Exists(owner)){
                    // Nodes inherit choices from connected roads even when a removed
                    // crossing left no generated record to use as a refresh hint.
                    QueueInheritedNodeLines(owner);QueueDetailJoins(owner);
                    if(records.TryGetValue(owner,out var previous)){
                        if(previous.PavingPaint.Count>0||previous.PavingSurfaces.Count>0)EnqueuePaving(owner);
                        if(previous.CenterPaint.Count>0||previous.CenterHidden.Count>0)EnqueueCenterOnly(owner);
                        if(previous.EdgePaint.Count>0||previous.EdgeJoins.Count>0||previous.EdgeHidden.Count>0)EnqueueEdgeOnly(owner);
                        if(previous.CrossPaint.Count>0||previous.CrossHidden.Count>0)EnqueueCrossOnly(owner);
                    }
                    
                    if(EntityManager.HasBuffer<ManualDecalChoice>(owner)||EntityManager.HasBuffer<DrawnLineChoice>(owner))manualDirty.Add(owner);
                    if(EntityManager.HasComponent<ApproachChoice>(owner)&&EntityManager.GetComponentData<ApproachChoice>(owner).Enabled)approachPending.Add(owner);
                    if(EntityManager.HasComponent<CenterChoice>(owner)||EntityManager.HasComponent<UTurnChoice>(owner))EnqueueCenterOnly(owner);
                    if(EntityManager.HasComponent<EdgeChoice>(owner))EnqueueEdgeOnly(owner);
                    if(EntityManager.HasComponent<CrosswalkChoice>(owner)){
                        EnqueueCrossOnly(owner);
                        if(EntityManager.HasComponent<Edge>(owner)){var ends=EntityManager.GetComponentData<Edge>(owner);EnqueueCrossOnly(ends.m_Start);EnqueueCrossOnly(ends.m_End);}
                    }
                    if(EntityManager.HasBuffer<PavingChoice>(owner)||EntityManager.HasBuffer<SideRoadChoice>(owner)||(pavingActive&&EntityManager.HasComponent<Node>(owner)))EnqueuePaving(owner);
                }
            }
            crossingPlans.Clear();crossingSizes.Clear();
            topologyDirty.Clear();topologyUrgent=false;dirtySince=-1;
        }}
    }
}






