using System;
using System.Collections.Generic;
using System.Diagnostics;
using Colossal.Mathematics;
using Colossal.Serialization.Entities;
using Game;
using Game.Common;
using Game.Net;
using Game.Prefabs;
using Game.Rendering;
using Game.Tools;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using ObjectTransform=Game.Objects.Transform;
using SubObject=Game.Objects.SubObject;
using SubLane=Game.Net.SubLane;
using CarLane=Game.Net.CarLane;
using PedestrianLane=Game.Net.PedestrianLane;
using ParkingLane=Game.Net.ParkingLane;
namespace RoadStyleIntoKorea
{
    // Only user choices persist. Decorative paint never participates in path finding.
    public struct EdgeChoice : IComponentData, ISerializable
    {
        public int Left,Right; // 0 = default; enum + 1 = explicit choice
        public void Serialize<T>(T w) where T:IWriter {w.Write(1);w.Write(Left);w.Write(Right);}
        public void Deserialize<T>(T r) where T:IReader {r.Read(out int version);r.Read(out Left);r.Read(out Right);if(version!=1){Left=Right=0;}}
    }
    public struct GeneratedPaint : IComponentData, ISerializable { public void Serialize<T>(T w) where T:IWriter {w.Write(1);} public void Deserialize<T>(T r) where T:IReader {r.Read(out int version);} }
    public struct OriginalPaintHidden : IComponentData, ISerializable { public void Serialize<T>(T w) where T:IWriter {w.Write(1);} public void Deserialize<T>(T r) where T:IReader {r.Read(out int version);} }
    public partial class StyleSystem : GameSystemBase
    {
        sealed class Record
        {
            internal readonly List<Entity> Paint=new List<Entity>();
            internal readonly List<Entity> CrossPaint=new List<Entity>();
            internal readonly HashSet<Entity> CrossHidden=new HashSet<Entity>();
            internal bool FullMarkings;
            internal Entity WalkOwner;internal SidewalkFootprint WalkSurface;
            internal readonly List<Entity> CenterPaint=new List<Entity>();
            internal readonly HashSet<Entity> CenterHidden=new HashSet<Entity>();
            internal readonly List<Entity> EdgeJoins=new List<Entity>();
            internal readonly List<Entity> PavingPaint=new List<Entity>();
            internal readonly HashSet<string> PavingFaces=new HashSet<string>();
            internal readonly List<PavingSurfaceInstance> PavingSurfaces=new List<PavingSurfaceInstance>();
            internal Record SideRoadMarks;
            internal readonly List<Entity> EdgePaint=new List<Entity>();
            internal readonly HashSet<Entity> EdgeHidden=new HashSet<Entity>();
            internal readonly HashSet<Entity> Hidden=new HashSet<Entity>();
        }
        sealed class Drive
        {
            internal Entity Entity,Crossing,End,Owner;internal Bezier4x3 Curve;internal float Width;internal float Stop=-1;internal bool StopPainted,StopSuppressed;
            internal Geometry.Route Arc;
            internal CarLaneFlags Turns;
            internal int MergeDirection;
            internal readonly List<ArrowPolicy.Occupied> PrimaryPaint=new List<ArrowPolicy.Occupied>();
            internal readonly List<ArrowPolicy.Occupied> WarningPaint=new List<ArrowPolicy.Occupied>();
            internal bool PrimaryReady;
        }
        readonly Dictionary<Entity,Record> records=new Dictionary<Entity,Record>();
        readonly WorkQueue<Entity> queue=new WorkQueue<Entity>();
        readonly HashSet<Entity> pending=new HashSet<Entity>();
        readonly HashSet<Entity> edgeOnlyPending=new HashSet<Entity>();
        readonly Dictionary<Entity,List<Entity>> laneSnapshots=new Dictionary<Entity,List<Entity>>();
        bool inWorkItem,preservePavingForWork;
        readonly HashSet<Entity> markingsOnlyPending=new HashSet<Entity>();
        readonly AssetCatalog assets=new AssetCatalog();
        readonly List<Entity> saveTemps=new List<Entity>();
        EntityQuery roads,dirty,generated,revealed,deletedOwners;
        EntityArchetype laneArchetype;
        bool ready,active,saving,applyRequested,restoreRequested,restoring;
        int total,completed,failed,unsupportedArrows;
        public string Status {get;private set;}="도시를 불러오세요 / Load a city";
        Settings S=>Mod.Options;
        protected override void OnCreate()
        {
            base.OnCreate();CreateNewRoadQuery();CreateRefreshQueries();
            roads=GetEntityQuery(new EntityQueryDesc{All=new[]{ComponentType.ReadOnly<Edge>(),ComponentType.ReadOnly<Game.Net.Road>(),ComponentType.ReadOnly<PrefabRef>()},None=new[]{ComponentType.ReadOnly<Deleted>(),ComponentType.ReadOnly<Temp>()}});
            dirty=GetEntityQuery(new EntityQueryDesc{All=new[]{ComponentType.ReadOnly<Edge>(),ComponentType.ReadOnly<Game.Net.Road>()},Any=new[]{ComponentType.ReadOnly<Created>(),ComponentType.ReadOnly<Updated>()},None=new[]{ComponentType.ReadOnly<Temp>()}});
            generated=GetEntityQuery(ComponentType.ReadOnly<GeneratedPaint>());CreatePaintRegistrationQuery();CreatePreviewVisibilityQueries();
            revealed=GetEntityQuery(new EntityQueryDesc{All=new[]{ComponentType.ReadOnly<OriginalPaintHidden>()},None=new[]{ComponentType.ReadOnly<Hidden>(),ComponentType.ReadOnly<Deleted>()}});
            deletedOwners=GetEntityQuery(new EntityQueryDesc{All=new[]{ComponentType.ReadOnly<Deleted>()},Any=new[]{ComponentType.ReadOnly<Edge>(),ComponentType.ReadOnly<Node>()},None=new[]{ComponentType.ReadOnly<Temp>()}});
            laneArchetype=EntityManager.CreateArchetype(typeof(Lane),typeof(Game.Net.SecondaryLane),typeof(Curve),typeof(PrefabRef),typeof(Owner),typeof(LaneGeometry),typeof(CullingInfo),typeof(MeshBatch),typeof(Created),typeof(Updated),typeof(GeneratedPaint));
        }
        protected override void OnGamePreload(Purpose purpose,GameMode mode)
        {
            // Entities from the previous world must not be interpreted in the new save.
            Clear();ready=false;assets.Named.Clear();assets.Names.Clear();
            if(S!=null)S.Enabled=true;base.OnGamePreload(purpose,mode);
        }
        protected override void OnGameLoadingComplete(Purpose purpose,GameMode mode)
        {base.OnGameLoadingComplete(purpose,mode);ready=mode==GameMode.Game;active=false;if(S!=null)S.Enabled=true;if(ready){RestoreBeforeWorldReady();}}
        internal bool Live(Entity e)=>e!=Entity.Null&&EntityManager.Exists(e)&&!EntityManager.HasComponent<Deleted>(e)&&!EntityManager.HasComponent<Temp>(e);
        internal bool IsRoad(Entity e)
        {
            if(!Live(e)||!EntityManager.HasComponent<Edge>(e)||!EntityManager.HasComponent<PrefabRef>(e))return false;
            var p=EntityManager.GetComponentData<PrefabRef>(e).m_Prefab;
            return EntityManager.HasComponent<Game.Net.Road>(e)&&EntityManager.HasComponent<RoadData>(p);
        }
        bool SafeNode(Entity node)
        {
            if(!Live(node)||!EntityManager.HasBuffer<ConnectedEdge>(node))return false;
            var b=EntityManager.GetBuffer<ConnectedEdge>(node,true);
            bool road=false;
            foreach(var v in b){var e=v.m_Edge;if(!Live(e)||!EntityManager.HasComponent<PrefabRef>(e))continue;var p=EntityManager.GetComponentData<PrefabRef>(e).m_Prefab;if(!EntityManager.HasComponent<RoadData>(p))continue;if(!IsRoad(e))return false;road=true;}
            return road;
        }
        internal void RequestApply(int scope=0){if(!CannotReplaceAll){defaultsScope=math.clamp(scope,0,2);applyRequested=true;}}
        internal void RequestRestore(){restoreRequested=true;}
        internal void Inspect()
        {
            if(!ready){Status="도시를 먼저 불러오세요 / Load a city first";return;}
            assets.Refresh(EntityManager,World.GetExistingSystemManaged<PrefabSystem>());
            var missing=assets.Missing(EntityManager,S);
            Status=missing.Count==0?"필요 에셋 확인 완료 / Required assets available":"누락 / Missing: "+string.Join(", ",missing);
        }
        void Begin()
        {
            BeginDefaultReplacement();
        }
        void Enqueue(Entity e){crossOnlyPending.Remove(e);centerOnlyPending.Remove(e);InvalidateToolPreview();markingsOnlyPending.Remove(e);pavingOnlyPending.Remove(e);edgeOnlyPending.Remove(e);if(e!=Entity.Null&&pending.Add(e))queue.Enqueue(e);}
        void EnqueueEdgeOnly(Entity e)=>EnqueueSelective(e,edgeOnlyPending);
        float nextAssetRefresh;
        bool CachedAssetsReady()
        {
            var missing=assets.Missing(EntityManager,S);
            if((assets.Named.Count==0||missing.Count>0)&&UnityEngine.Time.realtimeSinceStartup>=nextAssetRefresh){
                nextAssetRefresh=UnityEngine.Time.realtimeSinceStartup+2;
                assets.Refresh(EntityManager,World.GetExistingSystemManaged<PrefabSystem>());missing=assets.Missing(EntityManager,S);
            }
            if(missing.Count==0&&assets.Named.Count>0)return true;
            Status="누락 / Missing: "+string.Join(", ",missing);return false;
        }
        void EnqueueRoad(Entity e)
        {
            crossingPlans.Clear();crossingTrafficPlans.Clear();crossingSizes.Clear();
            // Also enqueue no-longer-eligible roads so stale visual records are removed.
            if(!EntityManager.Exists(e))return;
            Enqueue(e);
            if(EntityManager.HasComponent<Edge>(e)){var v=EntityManager.GetComponentData<Edge>(e);Enqueue(v.m_Start);Enqueue(v.m_End);}
        }
        protected override void OnUpdate(){
UpdateWithTrace();
        }
        void UpdateWithTrace()
        {
            if(!ready||S==null||saving)return;
            ReleaseDeletedStyleOwners();CollectNewRoadStyles();
            var frameWork=Stopwatch.StartNew();double bulkBudget=defaultsReplacing?DefaultFrameBudget():ApplyAllPolicy.FrameMilliseconds;
            BeginScheduledRestore();
            CollectTopology();
            AdmitLoadWork();
            AuditPaint();
            CheckNativeCrossings();
            bool guideFrame=!restoreRequested&&(guideCommands.Count>0||guideDirty.Count>0);
            if(!restoreRequested&&S.Enabled)TickGuides();
            if(defaultsReplacing&&defaultsFinalizing&&!restoreRequested&&S.Enabled)
                for(int step=1;step<16&&frameWork.Elapsed.TotalMilliseconds<bulkBudget;step++){
                    int before=guideDirty.Count+guideCommands.Count;if(before==0)break;
                    TickGuides();if(guideDirty.Count+guideCommands.Count>=before)break;
                }
            if(loadBatchActive&&guideFrame&&frameWork.Elapsed.TotalMilliseconds>=LoadBudgetPolicy.FrameBudget(UnityEngine.Time.unscaledDeltaTime))return;
            bool localFrame=!restoreRequested&&S.Enabled&&TickLocalStyles();
            bool approachFrame=localFrame||crossingDistancePriority.Count==0&&(!restoreRequested&&S.Enabled&&TickCurbs()||!restoreRequested&&S.Enabled&&TickManual()||!restoreRequested&&S.Enabled&&TickApproaches());
            // Advance cheap iterator stages in the same frame, within the shared budget.
            if(defaultsReplacing&&!localFrame&&!restoreRequested&&S.Enabled&&crossingDistancePriority.Count==0){
                // Bulk application can spend more time per frame than live editing.
                // Each tick retains its native Created/ownership guards; stop if blocked.
                for(int step=1;step<ApplyAllPolicy.StageLimit&&frameWork.Elapsed.TotalMilliseconds<bulkBudget;step++)
                    if(!(TickCurbs()||TickManual()||TickApproaches()))break;
            }else if(loadBatchActive&&!localFrame&&!restoreRequested&&S.Enabled)
                for(int step=1;step<4&&approachWork!=null&&frameWork.Elapsed.TotalMilliseconds<LoadBudgetPolicy.FrameBudget(UnityEngine.Time.unscaledDeltaTime);step++)TickApproaches();
            UpdateTransportAppearance();
            if(S.Enabled&&!restoring&&!restoreRequested)RetryNativePaving();
            if(restoreRequested||(!restoring&&(active||records.Count>0||curbRecords.Count>0||manualRecords.Count>0||guideRecords.Count>0||approachRecords.Count>0||approachStopRecords.Count>0||nativePavingBindings.Count>0||queue.Count>0)&&!S.Enabled))
            {
                crossingDistanceEdits.Clear();crossingDistancePriority.Clear();localStyleCommands.Clear();ClearCurbs();ClearManual();ClearApproaches();ClearGuides();CancelLoadRestore();CancelDefaultReplacement();dragStyleCommands.Clear();newRoadStyles.Clear();targetedActive=false;bulkRoads.Clear();restoreRequested=false;applyRequested=false;active=false;pavingActive=false;restoring=true;crossOnlyPending.Clear();centerOnlyPending.Clear();queue.Clear();pending.Clear();edgeOnlyPending.Clear();pavingOnlyPending.Clear();markingsOnlyPending.Clear();
                foreach(var owner in records.Keys)Enqueue(owner);
            }
            if(restoring)
            {
                var timer=Stopwatch.StartNew();int n=0;
                while(queue.Count>0&&n++<math.clamp(S.RoadsPerFrame,1,64)&&timer.Elapsed.TotalMilliseconds<math.clamp(S.Milliseconds,1,8))Release(queue.Dequeue());
                Status=queue.Count==0?"원래 표지 복원 완료 / Originals restored":"복원 중 / Restoring: "+queue.Count;
                if(queue.Count==0){restoring=false;pending.Clear();}return;
            }
            if(applyRequested){applyRequested=false;Begin();}
            TickDefaultReplacement();
            TickBulk();
            TickDraggedStyles();
            TickNewRoadStyles();
            if(!active&&!pavingActive&&!targetedActive&&queue.Count==0)return;
            var watch=frameWork;int count=0;
            int ownerLimit=defaultsReplacing?ApplyAllPolicy.OwnerLimit:loadBatchActive?math.min(4,math.clamp(S.RoadsPerFrame,1,64)):math.clamp(S.RoadsPerFrame,1,64);
            double timeLimit=defaultsReplacing?bulkBudget:loadBatchActive?math.min(S.Milliseconds,LoadBudgetPolicy.FrameBudget(UnityEngine.Time.unscaledDeltaTime)):math.clamp(S.Milliseconds,1,8);
            int attempts=queue.Count;
            if(attempts>0)EntityManager.CompleteAllTrackedJobs();
            while((!approachFrame||loadBatchActive||defaultsReplacing)&&queue.Count>0&&attempts-->0&&count<ownerLimit&&watch.Elapsed.TotalMilliseconds<timeLimit)
            {
                
                var e=queue.Dequeue();
                // Never delete/recreate paint before native Created registration and cleanup.
                if(PaintStillCreated(e)){queue.Enqueue(e);continue;}
                bool distanceFirst=crossingDistancePriority.Remove(e);
                pending.Remove(e);bool edgeOnly=edgeOnlyPending.Remove(e);bool pavingOnly=pavingOnlyPending.Remove(e);
                bool markingsOnly=markingsOnlyPending.Remove(e);bool centerOnly=centerOnlyPending.Remove(e);bool crossOnly=crossOnlyPending.Remove(e);
                // Complete one category per frame. Keep remaining categories queued,
                // without upgrading a local edit into replacement of every decal.
                bool deferred=false;
                if(centerOnly||crossOnly||edgeOnly||pavingOnly){
                    if(distanceFirst&&crossOnly){
                        if(centerOnly){centerOnlyPending.Add(e);centerOnly=false;deferred=true;}
                        if(edgeOnly){edgeOnlyPending.Add(e);edgeOnly=false;deferred=true;}
                        if(pavingOnly){pavingOnlyPending.Add(e);pavingOnly=false;deferred=true;}
                    }
                    else if(centerOnly){if(crossOnly){crossOnlyPending.Add(e);crossOnly=false;deferred=true;}if(edgeOnly){edgeOnlyPending.Add(e);edgeOnly=false;deferred=true;}if(pavingOnly){pavingOnlyPending.Add(e);pavingOnly=false;deferred=true;}}
                    else if(crossOnly){if(edgeOnly){edgeOnlyPending.Add(e);edgeOnly=false;deferred=true;}if(pavingOnly){pavingOnlyPending.Add(e);pavingOnly=false;deferred=true;}}
                    else if(edgeOnly&&pavingOnly){pavingOnlyPending.Add(e);pavingOnly=false;deferred=true;}
                    if(deferred){pending.Add(e);queue.Enqueue(e);}
                }
                preservePavingForWork=markingsOnly&&records.ContainsKey(e);
                bool joinOnly=edgeOnly&&EntityManager.HasComponent<Node>(e);
                if(!edgeOnly&&!pavingOnly&&!centerOnly&&!crossOnly){ClearLinePlans();if(preservePavingForWork)ReleaseMarkings(e);else Release(e);}
                laneSnapshots.Clear();drivewayWork.Clear();sideRoadWork.Clear();inWorkItem=true;
                bool drivewayOnly=edgeOnly&&drivewayQueued.Remove(e);
                if(Live(e))try
                {
                    if(!records.TryGetValue(e,out var record))records[e]=record=new Record();
                    if(crossOnly||centerOnly||edgeOnly||pavingOnly){
                        if(crossOnly){ClearCross(record);TrackCross(record,()=>PaintCrossCategory(e,record));}
                        if(centerOnly){ClearCenter(record);TrackCenter(record,()=>{if(IsRoad(e)){var local=Lanes(e);ApplyCenterLines(e,record,local);ApplyManualUTurn(e,record,local);ClipNativeDividers(e,record,local);}else if(SafeNode(e))JoinCenterEnds(e,record);});}
                        if(pavingOnly){ClearPaving(record);ApplyPaving(e,record);}
                        if(joinOnly){ClearEdgeJoins(record);JoinEdgeLines(e,record);}
                        else if(edgeOnly){ClearEdgePaint(record);if(IsRoad(e)){var local=Lanes(e);Edges(e,record,local,Drives(local));}}
                    }
                    else if(EntityManager.HasComponent<Edge>(e)){if(IsRoad(e))ProcessEdge(e,record);}
                    else if(EntityManager.HasComponent<Node>(e)&&SafeNode(e))ProcessNode(e,record);
                    if(record.Paint.Count==0&&record.Hidden.Count==0&&record.PavingSurfaces.Count==0&&record.SideRoadMarks==null&&!nativePavingBindings.ContainsKey(e)&&!nativeSideRoadCuts.ContainsKey(e))records.Remove(e);
                }
                catch(Exception ){if((edgeOnly||pavingOnly||centerOnly||crossOnly)&&records.TryGetValue(e,out var partial)){if(centerOnly)ClearCenter(partial);if(crossOnly)ClearCross(partial);if(pavingOnly)ClearPaving(partial);if(joinOnly)ClearEdgeJoins(partial);else if(edgeOnly)ClearEdgePaint(partial);}else Release(e);failed++;}
                if(!pavingOnly&&!drivewayOnly){if(EntityManager.HasBuffer<ManualDecalChoice>(e)||EntityManager.HasBuffer<DrawnLineChoice>(e))manualDirty.Add(e);QueueBoundaryGuides(e);}ObserveOwnerShape(e);inWorkItem=false;preservePavingForWork=false;laneSnapshots.Clear();drivewayWork.Clear();sideRoadWork.Clear();InvalidateToolPreview();
                completed++;count++;
                if(!loadBatchActive&&!defaultsReplacing)break;
            }
            if(count>0)Status=queue.Count==0?$"교체 처리 완료 / Finished: {completed}, 실패 / failed: {failed}, 미지원 화살표 / unsupported arrows: {unsupportedArrows}":$"교체 중 / Applying: {completed}, 남음 / remaining: {queue.Count}";
        }
        List<Entity> Lanes(Entity owner)
        {
            if(inWorkItem&&laneSnapshots.TryGetValue(owner,out var cached))return cached;
            var result=new List<Entity>();if(!Live(owner)||!EntityManager.HasBuffer<SubLane>(owner))return result;
            // Copy before structural changes: DynamicBuffer handles are invalidated by CreateEntity.
            foreach(var v in EntityManager.GetBuffer<SubLane>(owner,true))if(Live(v.m_SubLane)&&!EntityManager.HasComponent<GeneratedPaint>(v.m_SubLane)&&!EntityManager.HasComponent<ManualUTurnRoute>(v.m_SubLane)&&EntityManager.HasComponent<Curve>(v.m_SubLane))result.Add(v.m_SubLane);
            if(inWorkItem)laneSnapshots[owner]=result;
            return result;
        }
        Entity Prefab(Entity e)=>EntityManager.HasComponent<PrefabRef>(e)?EntityManager.GetComponentData<PrefabRef>(e).m_Prefab:Entity.Null;
        Bezier4x3 CurveOf(Entity e)=>SourceCurve(e);
        float Width(Entity e){var p=Prefab(e);return EntityManager.HasComponent<NetLaneData>(p)?EntityManager.GetComponentData<NetLaneData>(p).m_Width:3;}
        bool IsCrosswalk(Entity e)=>Live(e)&&!EntityManager.HasComponent<MasterLane>(e)&&EntityManager.HasComponent<PedestrianLane>(e)
            &&MarkingPolicy.IsMarkedCrosswalk(EntityManager.GetComponentData<PedestrianLane>(e).m_Flags,
                EntityManager.HasComponent<Hidden>(e),EntityManager.HasComponent<OriginalPaintHidden>(e));
        bool IsGuideJunction(Entity node)
        {
            if(!SafeNode(node))return false;
            var edges=new HashSet<Entity>();
            foreach(var connection in EntityManager.GetBuffer<ConnectedEdge>(node,true))
                if(IsRoad(connection.m_Edge))edges.Add(connection.m_Edge);
            return MarkingPolicy.HasJunction(edges.Count);
        }
        void DirtyBatch(Entity e){if(EntityManager.Exists(e)&&!EntityManager.HasComponent<BatchesUpdated>(e))EntityManager.AddComponent<BatchesUpdated>(e);}
        readonly PaintHidePolicy<Entity,Record> hiddenOwners=new PaintHidePolicy<Entity,Record>();
        void RestoreHidden(Entity e,Record r){
            if(!hiddenOwners.Release(e,r))return;
            if(Live(e)){EntityManager.RemoveComponent<Hidden>(e);EntityManager.RemoveComponent<OriginalPaintHidden>(e);DirtyBatch(e);}
        }
        void Hide(Entity e,Record r)
        {
            if(!Live(e)||EntityManager.HasComponent<Hidden>(e)&&!EntityManager.HasComponent<OriginalPaintHidden>(e))return;
            bool added=hiddenOwners.Add(e,r);
            EntityManager.AddComponent<Hidden>(e);EntityManager.AddComponent<OriginalPaintHidden>(e);if(added)r.Hidden.Add(e);DirtyBatch(e);
        }
        void PaintLane(Entity owner,Record r,Entity prefab,Bezier4x3 curve,Entity sourceCrosswalk=default)
        {
            if(prefab==Entity.Null||!EntityManager.Exists(prefab)||!EntityManager.HasComponent<NetLaneGeometryData>(prefab))throw new InvalidOperationException("Missing lane geometry");
            List<Bounds1> crossingCuts=null;float3 curbStart=curve.a,curbEnd=curve.d;
            if(sourceCrosswalk!=Entity.Null){
                var node=EntityManager.HasComponent<NodeLane>(sourceCrosswalk)?EntityManager.GetComponentData<NodeLane>(sourceCrosswalk):default;
                var cuts=new List<Bounds1>();if(EntityManager.HasBuffer<CutRange>(sourceCrosswalk))foreach(var cut in EntityManager.GetBuffer<CutRange>(sourceCrosswalk,true))cuts.Add(cut.m_CurveDelta);
                cuts=MarkingPolicy.VisibleCrosswalkCuts(cuts,Width(sourceCrosswalk)+node.m_WidthOffset,EntityManager.GetComponentData<NetLaneData>(prefab).m_Width);
                foreach(var cut in cuts){if(cut.min<=0)curbStart=Geometry.Position(curve,math.max(0,cut.max));if(cut.max>=1)curbEnd=Geometry.Position(curve,math.min(1,cut.min));}
                bool fullWidth=FullRoadCrosswalk(sourceCrosswalk,ref curve,ref cuts);
                if(fullWidth){curbStart=curve.a;curbEnd=curve.d;}
                cuts=CrosswalkSurfaceCuts(sourceCrosswalk,curve,cuts,EntityManager.GetComponentData<NetLaneData>(prefab).m_Width);
                if(!CrosswalkStripePolicy.Fit(curve,cuts,out curve,out crossingCuts,fullWidth))return;
            }
            float length=new Geometry.Arc(curve).Length;if(length<0.025f)return;
            var e=EntityManager.CreateEntity(laneArchetype);r.Paint.Add(e);
            EntityManager.SetComponentData(e,new Owner(owner));
            EntityManager.SetComponentData(e,new PrefabRef{m_Prefab=prefab});
            EntityManager.SetComponentData(e,new Curve{m_Bezier=curve,m_Length=length});
            if(sourceCrosswalk!=Entity.Null)
            {
                var originalNode=EntityManager.HasComponent<NodeLane>(sourceCrosswalk)?EntityManager.GetComponentData<NodeLane>(sourceCrosswalk):default;
                float sourceWidth=Width(sourceCrosswalk);
                float targetWidth=EntityManager.GetComponentData<NetLaneData>(prefab).m_Width;
                var cuts=crossingCuts;
                EntityManager.AddComponentData(e,MarkingPolicy.CrosswalkNode(originalNode));
                if(ForceCrosswalkArrows(sourceCrosswalk))
                    PaintPedestrianArrows(owner,r,curve,CrossingSize(sourceCrosswalk),cuts,curbStart,curbEnd,ForceCrosswalkArrows(sourceCrosswalk));
                curve=CrosswalkStripePolicy.TrimEntries(curve,cuts);
                EntityManager.SetComponentData(e,new Curve{m_Bezier=curve,m_Length=new Geometry.Arc(curve).Length});
                CrosswalkStripePolicy.GuardEnds(curve,cuts);
                if(cuts.Count>0)
                {
                    var buffer=EntityManager.AddBuffer<CutRange>(e);
                    foreach(var cut in cuts)buffer.Add(new CutRange{m_CurveDelta=cut});
                }
            }
            ApplyPaintRemoval(owner,e);            // Fresh render buffers, no copied batch indexes or pathfinding. Native owner references are detached separately.
        }
        void PaintPedestrianArrows(Entity owner,Record record,Bezier4x3 crossing,float rowWidth,List<Bounds1> cuts,float3 curbStart,float3 curbEnd,bool force=false)
        {
            var name=CrosswalkStylePolicy.Single((int)rowWidth)?PedestrianArrowPolicy.Single:PedestrianArrowPolicy.Asset(rowWidth);var prefab=assets.Get(name);
            // Missing optional art must not prevent the crossing itself from rendering.
            if(prefab==Entity.Null||!EntityManager.HasComponent<ObjectData>(prefab))
            {name=PedestrianArrowPolicy.Single;prefab=assets.Get(name);}
            if(prefab==Entity.Null||!EntityManager.HasComponent<ObjectData>(prefab))return;
            var arc=new Geometry.Arc(crossing);
            // Keep at least one full cycle between entry pockets on very short crossings.
            if(arc.Length<(force?1.2f:3f))return;
            var entryCuts=new List<Bounds1>();
            for(int i=0;i<2;i++)
            {
                bool end=i==1;
                if(!PedestrianArrowPolicy.Entry(crossing,CrosswalkStylePolicy.Single((int)rowWidth)?1:rowWidth,end,out var position,out var rotation,math.distance(end?crossing.d:crossing.a,end?curbEnd:curbStart)))continue;
                position.y+=.05f;
                PaintObject(owner,record,prefab,new ObjectTransform(position,rotation));
                // These transient decals are owned by the mod record, not vanilla road subobjects.
                // Native road reconstruction must not remove them as obsolete secondary objects.
                EntityManager.RemoveComponent<Owner>(record.Paint[record.Paint.Count-1]);
                entryCuts.Add(end?new Bounds1(arc.T(arc.Length-1f),1):new Bounds1(0,arc.T(1f)));
            }
            cuts.AddRange(entryCuts);
        }
        void PaintObject(Entity owner,Record r,Entity prefab,ObjectTransform transform)
        {
            if(prefab==Entity.Null||!EntityManager.HasComponent<ObjectData>(prefab))throw new InvalidOperationException("Missing decal object");
            var e=EntityManager.CreateEntity(EntityManager.GetComponentData<ObjectData>(prefab).m_Archetype);r.Paint.Add(e);
            EntityManager.AddComponent<GeneratedPaint>(e);
            if(!EntityManager.HasComponent<Created>(e))EntityManager.AddComponent<Created>(e);
            if(!EntityManager.HasComponent<Updated>(e))EntityManager.AddComponent<Updated>(e);
            // Transient decals are tracked by our records. Native road object
            // regeneration must not delete them as obsolete owned subobjects.
            if(EntityManager.HasComponent<Owner>(e))EntityManager.RemoveComponent<Owner>(e);
            EntityManager.SetComponentData(e,new PrefabRef{m_Prefab=prefab});
            EntityManager.SetComponentData(e,transform);ApplyPaintRemoval(owner,e);
        }
        void OffsetPaint(Entity owner,Record r,Entity prefab,Bezier4x3 curve,float left)
        {
            // One continuous native curve preserves 6 m texture phase, especially the zigzag modules.
            PaintLane(owner,r,prefab,Geometry.Offset(curve,left));
        }
        void ReplaceCrosswalks(Entity owner,Record r,List<Entity> lanes)
        {
            if(!S.Crosswalks)return;
            foreach(var lane in lanes)if(IsCrosswalk(lane)&&StyleCrossing(lane)&&!EntityManager.HasComponent<Hidden>(lane))
            {
                var prefab=Prefab(lane);if(!EntityManager.HasComponent<NetLaneGeometryData>(prefab))continue;
                var canonical=CanonicalCrossing(lane);
                if(canonical==lane){int before=r.Paint.Count;PaintLane(owner,r,assets.Get(AssetCatalog.Crosswalk(CrossingSize(lane))),CrosswalkCurve(lane),lane);if(r.Paint.Count==before)continue;}
                
                Hide(lane,r);
            }
        }
        void ProcessNode(Entity owner,Record r)
        {
            r.FullMarkings=true;
            if(S.EdgeLines)JoinEdgeLines(owner,r);
            TrackCenter(r,()=>JoinCenterEnds(owner,r));
            ApplyPaving(owner,r);
            var lanes=Lanes(owner);HideTransportLabels(owner,r,lanes);TrackCross(r,()=>ReplaceCrosswalks(owner,r,lanes));PlanPedestrianFlow(owner,lanes);
            var approachDrives=new List<Drive>();
            if(EntityManager.HasBuffer<ConnectedEdge>(owner))foreach(var connected in EntityManager.GetBuffer<ConnectedEdge>(owner,true))
            {
                if(!IsRoad(connected.m_Edge))continue;
                var roadLanes=Lanes(connected.m_Edge);var local=Drives(roadLanes);FindStops(connected.m_Edge,roadLanes,local);AlignStopLines(connected.m_Edge,r,local,false);approachDrives.AddRange(local);
            }
            ReplaceArrows(owner,r,approachDrives);
            ClipNativeDividers(owner,r,lanes);
            if(EntityManager.HasBuffer<GuideChoice>(owner))guideDirty.Add(owner);
        }
        List<Entity> Crossings(Entity edge,List<Entity> lanes)
        {
            var all=new List<Entity>(lanes);var e=EntityManager.GetComponentData<Edge>(edge);
            if(SafeNode(e.m_Start))all.AddRange(Lanes(e.m_Start));if(SafeNode(e.m_End))all.AddRange(Lanes(e.m_End));
            var result=new HashSet<Entity>();foreach(var lane in all)if(IsCrosswalk(lane))result.Add(CanonicalCrossing(lane));return new List<Entity>(result);
        }
        List<Drive> Drives(List<Entity> lanes,bool includeShared=false)
        {
            var result=new List<Drive>();
            foreach(var lane in lanes)
            {
                if(!EntityManager.HasComponent<CarLane>(lane)||EntityManager.HasComponent<MasterLane>(lane))continue;
                var flags=EntityManager.GetComponentData<CarLane>(lane).m_Flags;
                if(!MotorLanePrefab(Prefab(lane))||!RoadSupportPolicy.Drive(flags,includeShared))continue;
                var c=CurveOf(lane);
                result.Add(new Drive{Entity=lane,Curve=c,Arc=new Geometry.Route(c),Width=Width(lane)});
            }
            // Vanilla commonly splits an edge at its middle. Join route pieces without flattening either cubic.
            bool changed=true;
            while(changed)
            {
                changed=false;
                for(int i=0;i<result.Count&&!changed;i++)for(int j=0;j<result.Count;j++)
                {
                    if(i==j)continue;var a=result[i];var b=result[j];
                    if(math.abs(a.Width-b.Width)>0.05f||math.distancesq(a.Curve.d,b.Curve.a)>0.0025f||math.dot(a.Arc.Direction(a.Arc.Length),b.Arc.Direction(0))<0.98f)continue;
                    a.Arc.Append(b.Arc);a.Curve=new Bezier4x3(a.Curve.a,a.Curve.b,b.Curve.c,b.Curve.d);result.RemoveAt(j);changed=true;break;
                }
            }
            return result;
        }
        void FindStops(Entity owner,List<Entity> lanes,List<Drive> drives)
        {
            var crossings=Crossings(owner,lanes);
            var edge=EntityManager.GetComponentData<Edge>(owner);
            foreach(var d in drives)
            {
                d.Owner=owner;
                // Edge car lane curves are already directed, as in vanilla SecondaryLaneSystem.
                var end=math.distancesq(d.Curve.d,EntityManager.GetComponentData<Node>(edge.m_Start).m_Position)<math.distancesq(d.Curve.d,EntityManager.GetComponentData<Node>(edge.m_End).m_Position)?edge.m_Start:edge.m_End;
                if(!SafeNode(end))continue;
                d.End=end;
                d.Turns=ReadDriveTurns(owner,d,lanes);
                if(IsNarrowRoad(owner))d.Turns &= ~(CarLaneFlags.UTurnLeft|CarLaneFlags.UTurnRight);
                float best=float.MaxValue;
                foreach(var cross in crossings)
                {
                    var crossingRoad=CrossingRoad(cross,out bool start);
                    if(crossingRoad==owner){if((start?edge.m_Start:edge.m_End)!=end)continue;}
                    else if(!SharedTransitionCrossing(owner,crossingRoad,end))continue;
                    var ca=new Geometry.Arc(CrosswalkCurve(cross));float t=ca.Nearest(d.Curve.d);var p=Geometry.Position(ca.Curve,t);
                    var forward=Geometry.Tangent(d.Curve,1);
                    if(math.abs(math.dot(Geometry.Tangent(ca.Curve,t).xz,forward.xz))>0.6f||math.abs(p.y-d.Curve.d.y)>3)continue;
                    float dist=math.distance(p.xz,d.Curve.d.xz);if(dist>25||dist>=best)continue;
                    float width=CrossingWidth(cross);
                    var normal=CrossingAlignmentPolicy.StopNormal(ca.Curve,forward);
                    var plane=MarkingPolicy.StopPlane(p,normal,width,RoadStopGap(owner));
                    float stop;
                    if(!d.Arc.IntersectPlane(plane,normal,out stop)||stop<.5f){
                        d.Stop=-1;d.Crossing=cross;d.StopSuppressed=true;best=dist;continue;
                    }
                    // Native lane ends can stop short of the crossing. Keep approach paint
                    // anchored to the available route instead of losing all dividers.
                    stop=math.min(stop,d.Arc.Length-.1f);
                    if(stop<.5f)continue;
                    d.Stop=stop;d.Crossing=cross;d.StopSuppressed=false;best=dist;
                }
                if(d.Stop<0&&!d.StopSuppressed)
                {
                    // A native stop is still a valid approach anchor even when no replacement crossing fits.
                    var stops=new List<Entity>(lanes);stops.AddRange(Lanes(end));
                    foreach(var stopLane in stops)
                    {
                        if(assets.Name(Prefab(stopLane)).IndexOf("Stop Line",StringComparison.OrdinalIgnoreCase)<0)continue;
                        var stopArc=new Geometry.Arc(CurveOf(stopLane));float t=stopArc.Nearest(d.Curve.d);var point=Geometry.Position(stopArc.Curve,t);
                        if(math.distance(point.xz,d.Curve.d.xz)>20||math.abs(math.dot(Geometry.Tangent(stopArc.Curve,t),d.Arc.Direction(d.Arc.Length)))>.5f)continue;
                        float distance=d.Arc.Nearest(point,out var onRoute,out _);
                        if(math.distance(onRoute,point)>d.Width||distance<.5f)continue;
                        d.Stop=distance;break;
                    }
                }
                if(d.Stop<0)continue;
            }
        }
        void ProcessEdge(Entity owner,Record r)
        {
            r.FullMarkings=true;
            ApplyPaving(owner,r);
            var lanes=Lanes(owner);HideTransportLabels(owner,r,lanes);TrackCross(r,()=>ReplaceCrosswalks(owner,r,lanes));
            var drives=Drives(lanes);var crossings=Crossings(owner,lanes);
            FindStops(owner,lanes,drives);
            TrackCross(r,()=>AlignStopLines(owner,r,drives));PlanStopFlow(owner,lanes,drives);
            ApplySurfaceDetails(owner,r,lanes,drives);
            TrackCross(r,()=>HideApproachStops(r,lanes,drives));
            ReplaceArrows(owner,r,drives);
            EnsureApproachArrows(owner,r,drives);
            PaintApproachDiamonds(owner,r,drives);
            PaintUTurnWarnings(owner,r,lanes,drives);PaintSpeedSigns(owner,r,drives);ReconcileDecalSpacing(owner);
            TrackCenter(r,()=>{ApplyCenterLines(owner,r,lanes);ApplyManualUTurn(owner,r,lanes);});
            ApplyDividerStyles(owner,r,lanes,drives);
            if(S.EdgeLines)Edges(owner,r,lanes,drives);else ClipSideRoadNativeLines(owner,r,lanes);
            ClipNativeDividers(owner,r,lanes);
        }
        void SolidDividers(Entity owner,Record r,List<Entity> lanes,List<Drive> drives,HashSet<Entity> styled=null)
        {
            foreach(var lane in lanes)
            {
                string name=assets.Name(Prefab(lane));
                if(styled!=null&&styled.Contains(lane))continue;
                if(!MarkingPolicy.IsLaneDivider(name)||!EntityManager.HasComponent<NetLaneGeometryData>(Prefab(lane)))continue;
                var c=CurveOf(lane);var arc=new Geometry.Arc(c);if(arc.Length<0.1f)continue;
                float cutStart=arc.Length,cutEnd=0;var solidLimits=new List<EdgeApproachPolicy.Limit>();
                foreach(var d in drives)
                {
                    if(d.Stop<0)continue;
                    var stop=d.Arc.At(d.Stop);float t=arc.Nearest(stop);var p=Geometry.Position(c,0.5f);
                    d.Arc.Nearest(p,out var drivePoint,out var driveDirection);
                    float lateral=math.dot(p-drivePoint,Geometry.Left(driveDirection));
                    if(math.abs(math.abs(lateral)-d.Width*0.5f)>0.6f||math.distance(p.xz,drivePoint.xz)>d.Width)continue;
                    // Require a same-direction driving lane on the other side: never repaint a center line.
                    bool neighbor=false;
                    foreach(var other in drives)if(other!=d)
                    {
                        other.Arc.Nearest(p,out var op,out var ot);
                        if(math.dot(ot,driveDirection)>0.8f&&math.dot(op-p,drivePoint-p)<0&&math.distance(op.xz,p.xz)<other.Width*0.5f+0.7f){neighbor=true;break;}
                    }
                    if(!neighbor)continue;
                    var stopNormal=d.Arc.Direction(d.Stop);if(d.Crossing!=Entity.Null)stopNormal=CrossingAlignmentPolicy.StopNormal(CrosswalkCurve(d.Crossing),stopNormal);
                    solidLimits.Add(new EdgeApproachPolicy.Limit{Origin=stop-stopNormal*.4f,Inward=-stopNormal});
                    float roadLength=EntityManager.HasComponent<Curve>(owner)?EntityManager.GetComponentData<Curve>(owner).m_Length:d.Arc.Length;
                    float length=Geometry.SolidLength(roadLength,d.Stop,S);
                    var start=d.Arc.At(d.Stop-length);float t0=arc.Nearest(start);
                    // Convert projected endpoints to the secondary line's own parameterization.
                    float lo=math.min(t0,t),hi=math.max(t0,t);
                    cutStart=math.min(cutStart,lo);cutEnd=math.max(cutEnd,hi);
                }
                if(cutEnd<=cutStart)continue;
                if(cutStart>0.001f)foreach(var before in EdgeApproachPolicy.Clip(Geometry.Cut(c,0,cutStart),solidLimits))PaintDivider(owner,r,Prefab(lane),before);
                foreach(var solid in EdgeApproachPolicy.Clip(Geometry.Cut(c,cutStart,cutEnd),solidLimits))PaintDivider(owner,r,assets.Get(AssetCatalog.White),solid);
                if(cutEnd<0.999f)foreach(var after in EdgeApproachPolicy.Clip(Geometry.Cut(c,cutEnd,1),solidLimits))PaintDivider(owner,r,Prefab(lane),after);Hide(lane,r);
            }
        }
        internal void ChooseEdge(Entity edge,bool left,bool reset)
        {
            if(S==null||!S.Enabled||!S.EdgeLines||!IsRoad(edge))return;
            if(!CachedAssetsReady())return;
            var choice=EntityManager.HasComponent<EdgeChoice>(edge)?EntityManager.GetComponentData<EdgeChoice>(edge):default;
            int value=reset?0:(int)S.Brush+1;
            if((!left&&S.Side!=ToolSide.BothSides||choice.Left==value)&&(left&&S.Side!=ToolSide.BothSides||choice.Right==value))return;
            if(reset)ToolResetHandled=true;
            if(left||S.Side==ToolSide.BothSides)choice.Left=value;if(!left||S.Side==ToolSide.BothSides)choice.Right=value;
            if(!EntityManager.HasComponent<EdgeChoice>(edge))EntityManager.AddComponent<EdgeChoice>(edge);
            EntityManager.SetComponentData(edge,choice);
            // Selecting an edge is an explicit local application; it does not activate automatic city-wide replacement.
            {
                EnqueueEdgeOnly(edge);
                var network=EntityManager.GetComponentData<Edge>(edge);
                EnqueueEdgeOnly(network.m_Start);EnqueueEdgeOnly(network.m_End);
                if(EntityManager.HasBuffer<PavingChoice>(edge)){EnqueuePaving(edge);EnqueuePaving(network.m_Start);EnqueuePaving(network.m_End);}
            }
            Status="가장자리 변경 대기 / Edge change queued";
        }
        void Edges(Entity owner,Record r,List<Entity> lanes,List<Drive> drives,List<EdgeBoundary> collect=null,EdgeStyle? previewStyle=null)
        {
            if(!EntityManager.HasComponent<Curve>(owner))return;
            if(collect==null)ClipSideRoadNativeLines(owner,r,lanes);
            var center=new Geometry.Arc(CurveOf(owner));
            var clearance=new EdgeClearanceContext{Owner=owner,Ribbons=EdgeRoadRibbons(owner)};
            var boundaries=new List<Entity>();
            foreach(var lane in lanes)
                if(!EntityManager.HasComponent<MasterLane>(lane)&&!EntityManager.HasComponent<ParkingLane>(lane)&&EntityManager.HasComponent<CarLane>(lane))
                {
                    if(EntityManager.HasComponent<CarLane>(lane)&&(EntityManager.GetComponentData<CarLane>(lane).m_Flags&CarLaneFlags.SideConnection)!=0)continue;
                    if(!MotorLanePrefab(Prefab(lane)))continue;
                    boundaries.Add(lane);
                }
            var crossings=collect==null?Crossings(owner,lanes):null;
            var driveways=collect==null?Driveways(owner,lanes):null;
            var choice=EntityManager.HasComponent<EdgeChoice>(owner)?EntityManager.GetComponentData<EdgeChoice>(owner):default;
            foreach(bool isLeft in new[]{true,false})
            {
                int code=isLeft?choice.Left:choice.Right;var style=code==0?(active?S.DefaultEdge:EdgeStyle.Original):(EdgeStyle)math.clamp(code-1,0,5);
                if(previewStyle.HasValue)style=previewStyle.Value;
                if(style==EdgeStyle.Original)continue;
                var limits=collect==null?EdgeApproachLimits(owner,EdgePaintHalfWidth(style)):null;
                var candidates=new List<Bezier4x3>();
                // Bus bays follow the real curb inside the roadway; ordinary roads retain the lane-gap option.
                bool bay=BayEdgeCurves(owner,isLeft,EdgePaintHalfWidth(style),candidates);
                foreach(var outside in boundaries)
                {
                    if(bay)break;
                    var c=CurveOf(outside);var sample=Geometry.Position(c,0.5f);
                    float ct=center.Nearest(sample);var forward=Geometry.Tangent(center.Curve,ct);var left=Geometry.Left(forward);
                    float width=Width(outside);float extreme=math.dot(sample-Geometry.Position(center.Curve,ct),left)+(isLeft?1:-1)*width*0.5f;
                    bool outer=true;
                    foreach(var other in boundaries)
                    {
                        if(other==outside)continue;var oa=new Geometry.Arc(CurveOf(other));float ot=oa.Nearest(sample);var p=Geometry.Position(oa.Curve,ot);
                        if(math.abs(math.dot(p-sample,forward))>0.75f)continue;
                        float position=math.dot(p-Geometry.Position(center.Curve,ct),left)+(isLeft?1:-1)*Width(other)*0.5f;
                        if((isLeft&&position>extreme+0.05f)||(!isLeft&&position<extreme-0.05f)){outer=false;break;}
                    }
                    if(!outer)continue;
                    if(math.dot(Geometry.Tangent(c,0.5f),forward)<0)c=Geometry.Reverse(c);
                    candidates.Add(Geometry.Offset(c,(isLeft?1:-1)*EdgeTaperPolicy.OutsideOffset(width,EdgePaintHalfWidth(style),S.EdgeLaneGap)));
                }
                foreach(var paint in candidates)
                {
                    if(collect!=null){collect.Add(new EdgeBoundary{Curve=paint,Style=style,Left=isLeft});continue;}
                    int edgePaintBefore=r.Paint.Count;
                    if(style!=EdgeStyle.None)
                        foreach(var clear in EdgePaintParts(owner,style,paint,crossings,clearance))
                        {
                            int before=r.Paint.Count;PaintDrivewayEdge(owner,r,style,clear,driveways,isLeft);
                            for(int i=before;i<r.Paint.Count;i++)r.EdgePaint.Add(r.Paint[i]);
                        }
                    if(style!=EdgeStyle.None&&r.Paint.Count==edgePaintBefore&&r.SideRoadMarks==null&&NearbyCrossings(owner).Count==0)continue;
                    var boundary=new Geometry.Arc(paint);
                    foreach(var lane in lanes)
                    {
                        var name=assets.Name(Prefab(lane));
                        if(name.IndexOf("Edge Line",StringComparison.OrdinalIgnoreCase)<0&&name.IndexOf("Parking",StringComparison.OrdinalIgnoreCase)<0&&name.IndexOf("Car Bay Line",StringComparison.OrdinalIgnoreCase)<0)continue;
                        if(!EntityManager.HasComponent<NetLaneGeometryData>(Prefab(lane))||!EntityManager.HasComponent<Game.Net.SecondaryLane>(lane))continue;
                        var source=CurveOf(lane);var p=Geometry.Position(source,0.5f);float bt=boundary.Nearest(p);
                        if(math.distance(p.xz,Geometry.Position(paint,bt).xz)<0.8f&&math.abs(math.dot(Geometry.Tangent(source,0.5f),Geometry.Tangent(paint,bt)))>0.8f){Hide(lane,r);if(r.Hidden.Contains(lane)&&!r.EdgeHidden.Contains(lane))r.EdgeHidden.Add(lane);}
                    }
                }
            }
        }
        List<string> AvailableArrowSymbols(List<string> symbols)=>ArrowPolicy.AvailableSymbols(symbols,s=>{var e=assets.Get(AssetCatalog.Arrow(s));return e!=Entity.Null&&EntityManager.Exists(e)&&EntityManager.HasComponent<ObjectData>(e);});
        sealed class ArrowCandidate
        {
            internal Entity Source;internal List<string> Symbols;internal ObjectTransform Transform;
            internal Drive Route;internal bool Narrow;internal float Along,Height;
        }
        List<ArrowCandidate> CollectArrowCandidates(IEnumerable<Entity> roots,List<Drive> roadDrives,bool includeOwnedHidden=false)
        {
            var todo=new WorkQueue<Entity>();var seen=new HashSet<Entity>();var candidates=new List<ArrowCandidate>();foreach(var root in roots)todo.Enqueue(root);
            while(todo.Count>0)
            {
                var parent=todo.Dequeue();if(!seen.Add(parent)||!Live(parent))continue;
                if(EntityManager.HasBuffer<SubObject>(parent))foreach(var child in EntityManager.GetBuffer<SubObject>(parent,true))todo.Enqueue(child.m_SubObject);
                if(!EntityManager.HasComponent<ObjectTransform>(parent)||(EntityManager.HasComponent<Hidden>(parent)&&!(includeOwnedHidden&&EntityManager.HasComponent<OriginalPaintHidden>(parent))))continue;
                var prefab=Prefab(parent);if(!EntityManager.HasComponent<LaneDirectionData>(prefab))continue;
                var direction=EntityManager.GetComponentData<LaneDirectionData>(prefab);
                var symbols=ArrowPolicy.ArrowSymbols(direction);if(symbols.Count==0&&direction.m_Right!=LaneDirectionType.UTurn){if(!includeOwnedHidden&&direction.m_Right==LaneDirectionType.UTurn){unsupportedArrows++;}continue;}
                var transform=EntityManager.GetComponentData<ObjectTransform>(parent);
                Drive route=null;float along=0,best=float.MaxValue,height=0;
                var facing=math.normalizesafe(math.mul(transform.m_Rotation,new float3(0,0,1)));
                if(roadDrives!=null)foreach(var drive in roadDrives)
                {
                    float distance=drive.Arc.Nearest(transform.m_Position,out var p,out var heading);
                    if(math.dot(facing,heading)<.5f)continue;
                    float error=math.distance(p,transform.m_Position);
                    if(error<drive.Width*0.5f+0.5f&&error<best){route=drive;along=distance;best=error;height=transform.m_Position.y-p.y;}
                }
                if(route!=null)direction=ArrowPolicy.MeasuredDirection(direction,route.Turns);
                symbols=route!=null&&route.MergeDirection!=0?new List<string>{route.MergeDirection>0?"DiagonalLeft":"DiagonalRight"}:ArrowPolicy.RouteSymbols(direction,route==null?0:route.Turns);
                bool narrow=IsNarrowRoad(route?.Owner??ArrowRoad(parent,transform.m_Position));
                if(narrow&&(route==null||route.MergeDirection==0))symbols=NarrowPolicy.Symbols(direction,route?.Turns??0);
                symbols=AvailableArrowSymbols(symbols);
                candidates.Add(new ArrowCandidate{Source=parent,Symbols=symbols,Transform=transform,Route=route,Narrow=narrow,Along=along,Height=height});
            }
            return candidates;
        }
        List<ArrowCandidate> ApproachArrows(Entity owner,List<Drive> drives)
        {
            var roots=new HashSet<Entity>{owner};
            if(drives!=null)foreach(var drive in drives){roots.Add(drive.Owner);roots.Add(drive.End);}
            return CollectArrowCandidates(roots,drives,true);
        }
        bool PlanArrow(ArrowCandidate item,List<ArrowCandidate> candidates,out float along,out bool relocate)
        {
            var route=item.Route;along=item.Along;relocate=false;
            bool foremost=route!=null&&route.Stop>=0;
            if(foremost)foreach(var other in candidates)
            {
                if(other.Source==item.Source||other.Route!=route)continue;
                if(other.Along>item.Along+.01f||(math.abs(other.Along-item.Along)<=.01f&&other.Source.Index<item.Source.Index)){foremost=false;break;}
            }
            if(route==null||route.StopSuppressed)return false;
            if(foremost){relocate=TryStopArrowPlacement(route,route.Stop,item.Symbols,item.Symbols.Count,out along);if(!relocate)return false;}
            if(!relocate)along=item.Along;
            if(!FitsArrowStack(route,along,item.Symbols))return false;
            for(int i=0;i<item.Symbols.Count;i++)if(!ArrowStopClear(route,ArrowStackAt(along,item.Symbols,i),item.Symbols[i]))return false;
            relocate=true;return true;
        }
        void ReplaceArrows(Entity owner,Record r,List<Drive> roadDrives=null)
        {
            var candidates=CollectArrowCandidates(new[]{owner},roadDrives);
            var all=ApproachArrows(owner,roadDrives);
            // Native arrows can belong to the node rather than the road entity.
            foreach(var item in all)if(item.Route!=null&&item.Route.Owner==owner&&!candidates.Exists(c=>c.Source==item.Source))candidates.Add(item);
            // Merge arrows are emitted once by EnsureApproachArrows, not per native decal.
            if(S.Arrows)foreach(var item in all)if(item.Route!=null&&item.Route.MergeDirection!=0)Hide(item.Source,r);
            foreach(var item in candidates)
            {
                var route=item.Route;var symbols=item.Symbols;
                if(S.Arrows&&route!=null&&route.MergeDirection!=0)continue;
                var originalDirection=EntityManager.GetComponentData<LaneDirectionData>(Prefab(item.Source));
                bool removeUTurn=item.Narrow&&(originalDirection.m_Left==LaneDirectionType.UTurn||originalDirection.m_Right==LaneDirectionType.UTurn);
                if(!S.Arrows&&!removeUTurn)continue;
                if(symbols.Count==0){if(removeUTurn)Hide(item.Source,r);continue;}
                // Do not fall back to a native decal that extends beyond the stop line.
                if(!PlanArrow(item,all,out float along,out bool relocate))
                {unsupportedArrows++;Hide(item.Source,r);continue;}
                for(int i=0;i<symbols.Count;i++)
                {
                    var at=item.Transform;
                    if(route!=null&&(relocate||i>0))
                    {
                        float distance=ArrowStackAt(along,symbols,i);at.m_Position=route.Arc.At(distance);
                        at.m_Position.y+=math.clamp(item.Height,-.4f,.4f);
                        at.m_Rotation=quaternion.LookRotationSafe(route.Arc.Direction(distance),new float3(0,1,0));
                    }
                    if(!UTurnDecalClear(owner,symbols[i],assets.Get(AssetCatalog.Arrow(symbols[i])),at))continue;
                    PaintObject(owner,r,assets.Get(AssetCatalog.Arrow(symbols[i])),at);
                    if(route!=null)route.PrimaryPaint.Add(new ArrowPolicy.Occupied(ArrowStackAt(along,symbols,i),DecalHalfLength(assets.Get(AssetCatalog.Arrow(symbols[i])),3.8f)));
                }
                Hide(item.Source,r);
            }
        }
        void EnqueueMarkings(Entity owner)
        {
            if(pending.Contains(owner))
            {
                if(pavingOnlyPending.Contains(owner)){Enqueue(owner);return;}
                bool local=edgeOnlyPending.Remove(owner);local=centerOnlyPending.Remove(owner)||local;local=crossOnlyPending.Remove(owner)||local;if(local)markingsOnlyPending.Add(owner);
                return;
            }
            Enqueue(owner);markingsOnlyPending.Add(owner);
        }
        void ReleaseMarkings(Entity owner)
        {
            if(!records.TryGetValue(owner,out var r))return;
            ClearSideRoadMarks(r);
            foreach(var entity in r.Hidden)RestoreHidden(entity,r);
            foreach(var entity in r.Paint)if(!r.PavingPaint.Contains(entity)&&EntityManager.Exists(entity)&&!EntityManager.HasComponent<Deleted>(entity))RetirePaint(entity);
            r.CrossPaint.Clear();r.CrossHidden.Clear();r.CenterPaint.Clear();r.CenterHidden.Clear();r.Paint.Clear();r.Paint.AddRange(r.PavingPaint);r.Hidden.Clear();r.EdgeHidden.Clear();r.EdgePaint.Clear();r.EdgeJoins.Clear();
        }
        void Release(Entity owner)
        {
            drivewayDirty.Remove(owner);drivewayQueued.Remove(owner);
            if(!records.TryGetValue(owner,out var r)){ReleaseNativePaving(owner);ReleaseNativeSideRoadCut(owner);return;}
            foreach(var e in r.Hidden)RestoreHidden(e,r);
            foreach(var e in r.Paint)if(EntityManager.Exists(e)&&!EntityManager.HasComponent<Deleted>(e))RetirePaint(e);
            ClearPaving(r);
            ClearSideRoadMarks(r);
            records.Remove(owner);
        }
        internal void Clear()
        {
            RestorePreviewAttachments();previewVisibleOwners.Clear();nextAssetRefresh=0;crossingDistanceEdits.Clear();crossingDistancePriority.Clear();
            EntityManager.CompleteAllTrackedJobs();
            nodeSurfaceCandidates.Clear();nodeSurfaceStamps.Clear();nodeSurfaceRevision=-1;ClearDriveways();
            RestoreWalkHeights();removedPaint.Clear();paintAudit.Clear();nextPaintAudit=0;localStyleCommands.Clear();ClearCurbs();ClearManual();ClearApproaches();ClearGuides();CancelLoadRestore();restoreRequested=false;laneRefreshStamps.Clear();ownerShapes.Clear();nativeCrossingRequests.Clear();nativeCrossingWaiting.Clear();
            CancelDefaultReplacement();dragStyleCommands.Clear();newRoadStyles.Clear();targetedActive=false;bulkRoads.Clear();crossOnlyPending.Clear();centerOnlyPending.Clear();RestoreTransportMaterials();transportScanned=false;transportSettingsSeen=false;ClearNewRoadStyles();renderRepair.Clear();renderRepairPending.Clear();
            sidewalkGutters.Clear();paintRegistration.Clear();paintRegistrationPending.Clear();
            InvalidateToolPreview();ClearLinePlans();toolTarget=Entity.Null;RestoreFlow();topologyDirty.Clear();topologyUrgent=false;dirtySince=-1;
            crossingPlans.Clear();crossingTrafficPlans.Clear();crossingSizes.Clear();
            foreach(var owner in new List<Entity>(records.Keys))Release(owner);
            ClearNativePaving();
            walkDirty.Clear();suppressedWarnings.Clear();DisposePavingMaterials();
            crossOnlyPending.Clear();centerOnlyPending.Clear();queue.Clear();pending.Clear();edgeOnlyPending.Clear();pavingOnlyPending.Clear();markingsOnlyPending.Clear();active=restoring=pavingActive=false;applyRequested=false;hoverPavingEdge=Entity.Null;hoverPavingRegions.Clear();
        }
        void ReleaseDeletedStyleOwners(){
            if(deletedOwners.IsEmptyIgnoreFilter)return;EntityManager.CompleteAllTrackedJobs();
            using(var a=deletedOwners.ToEntityArray(Allocator.Temp))foreach(var e in a)if(TrackedStyleOwner(e))ReleaseDeletedStyleOwner(e);
        }
        void ReleaseDeletedStyleOwner(Entity owner){
            // Preserve adjacency before native cleanup destroys the edge and its buffers.
            if(ownerShapes.TryGetValue(owner,out var shape))foreach(var neighbor in shape.Connections)NoteTopology(neighbor);
            foreach(var neighbor in DirectRefreshNeighbors(owner))NoteTopology(neighbor);
            NoteTopology(owner);topologyUrgent=topologyDirty.Count>0;
            Release(owner);ReleaseGuides(owner);ReleaseApproach(owner);ReleaseManual(owner);ReleaseCurb(owner);
            ownerShapes.Remove(owner);
        }
        internal void EnforceVisibility()
        {
            if(saving||!ready)return;
            PreservePreviewPaint();
            // ECS queries visit only visuals that vanilla revealed and owners being deleted, not every road each frame.
            // Native LaneHiddenSystem reveals lanes owned by visible roads.
            // Persisted mod ownership remains authoritative during that transient state. Use our ownership ledger before RequiredBatchesSystem consumes visibility.
            using(var list=revealed.ToEntityArray(Allocator.Temp))foreach(var e in list)if(Live(e)&&hiddenOwners.Contains(e)){
                EntityManager.CompleteAllTrackedJobs();EntityManager.AddComponent<Hidden>(e);EntityManager.AddComponent<OriginalPaintHidden>(e);DirtyBatch(e);
            }
        }
        readonly List<Entity> saveHidden=new List<Entity>();
        internal void BeforeSave()
        {
            if(saving)return;
            RestorePreviewAttachments();previewVisibleOwners.Clear();saving=true;saveTemps.Clear();saveHidden.Clear();EntityManager.CompleteAllTrackedJobs();
            SuspendWalkHeights();SuspendFlowForSave();
            SuspendNativeSideRoadCuts();
            SuspendNativePaving();
            using(var q=EntityManager.CreateEntityQuery(ComponentType.ReadOnly<OriginalPaintHidden>()))using(var list=q.ToEntityArray(Allocator.Temp))foreach(var e in list){
                if(EntityManager.HasComponent<Hidden>(e)){saveHidden.Add(e);EntityManager.RemoveComponent<Hidden>(e);}
            }
            using(var paint=generated.ToEntityArray(Allocator.Temp))foreach(var e in paint){
                DetachPaint(e);
                if(!EntityManager.HasComponent<Temp>(e)){EntityManager.AddComponent<Temp>(e);saveTemps.Add(e);}
            }
        }
        internal void AfterSave()
        {
            if(!saving)return;
            EntityManager.CompleteAllTrackedJobs();
            foreach(var e in saveTemps)if(EntityManager.Exists(e)){EntityManager.RemoveComponent<Temp>(e);DirtyBatch(e);}
            foreach(var e in saveHidden)if(EntityManager.Exists(e)){if(!EntityManager.HasComponent<Hidden>(e))EntityManager.AddComponent<Hidden>(e);DirtyBatch(e);}
            ResumeNativePaving();ResumeNativeSideRoadCuts();ResumeFlowAfterSave();ResumeWalkHeights();saveTemps.Clear();saveHidden.Clear();saving=false;
        }
    }
}
