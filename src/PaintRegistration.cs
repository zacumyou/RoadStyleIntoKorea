using System.Collections.Generic;
using Colossal.Mathematics;
using Game.Common;
using Game.Net;
using Game.Prefabs;
using Game.Rendering;
using Unity.Entities;
using Unity.Mathematics;
namespace RoadStyleIntoKorea {public partial class StyleSystem {
 // Verify the spatial entry itself, not camera visibility or an empty MeshBatch.
 // Native culling is allowed to evict batches for offscreen objects.
 readonly WorkQueue<Entity> paintRegistration=new WorkQueue<Entity>();
 readonly HashSet<Entity> paintRegistrationPending=new HashSet<Entity>();
 EntityQuery changedPaintRegistration;
 void CreatePaintRegistrationQuery(){changedPaintRegistration=GetEntityQuery(new EntityQueryDesc{
  All=new[]{ComponentType.ReadOnly<GeneratedPaint>(),ComponentType.ReadOnly<Updated>()},
  None=new[]{ComponentType.ReadOnly<Deleted>(),ComponentType.ReadOnly<Game.Tools.Temp>()}});}
 void QueuePaintRegistration(Entity e){if(paintRegistrationPending.Add(e))paintRegistration.Enqueue(e);}
 void CheckPaintRegistration(){
  if(saving||!ready||S==null||!S.Enabled)return;
  if(!changedPaintRegistration.IsEmptyIgnoreFilter)using(var changes=changedPaintRegistration.ToEntityArray(Unity.Collections.Allocator.Temp))foreach(var e in changes)QueuePaintRegistration(e);
  if(paintRegistration.Count==0)return;
  var nets=World.GetExistingSystemManaged<Game.Net.SearchSystem>();
  var objects=World.GetExistingSystemManaged<Game.Objects.SearchSystem>();
  if(nets==null||objects==null)return;
  var laneTree=nets.GetLaneSearchTree(true,out var laneJobs);laneJobs.Complete();
  var objectTree=objects.GetStaticSearchTree(true,out var objectJobs);objectJobs.Complete();
  bool laneWrite=false,objectWrite=false;
  for(int i=0;i<32&&paintRegistration.Count>0;i++){
   var e=paintRegistration.Dequeue();paintRegistrationPending.Remove(e);
   if(!Live(e)||!EntityManager.HasComponent<GeneratedPaint>(e)||EntityManager.HasComponent<Game.Tools.Temp>(e))continue;
   if(removedPaint.Contains(e)||suppressedWarnings.Contains(e)||hiddenOwners.Contains(e))continue;
   if(EntityManager.HasComponent<Owner>(e)&&EntityManager.HasComponent<Game.Tools.Hidden>(EntityManager.GetComponentData<Owner>(e).m_Owner)&&!previewVisibleOwners.Contains(EntityManager.GetComponentData<Owner>(e).m_Owner))continue;
   var prefab=Prefab(e);bool lane=EntityManager.HasComponent<Lane>(e);
   Bounds3 bounds;int lod;BoundsMask mask=BoundsMask.Debug|BoundsMask.NotOverridden;
   if(lane&&EntityManager.HasComponent<NetLaneGeometryData>(prefab)){
    var data=EntityManager.GetComponentData<NetLaneGeometryData>(prefab);var curve=EntityManager.GetComponentData<Curve>(e);
    bounds=MathUtils.Expand(MathUtils.Bounds(curve.m_Bezier),data.m_Size.xyx*.5f);lod=data.m_MinLod;
    if(curve.m_Length>.1f)mask|=CommonUtils.GetBoundsMask(data.m_GameLayers);
   }else if(!lane&&EntityManager.HasComponent<ObjectGeometryData>(prefab)){
    var data=EntityManager.GetComponentData<ObjectGeometryData>(prefab);var transform=EntityManager.GetComponentData<Game.Objects.Transform>(e);
    bounds=Game.Objects.ObjectUtils.CalculateBounds(transform.m_Position,transform.m_Rotation,data);lod=data.m_MinLod;
    mask|=CommonUtils.GetBoundsMask(data.m_Layers);
    if((data.m_Flags&Game.Objects.GeometryFlags.WalkThrough)==0)mask|=BoundsMask.NotWalkThrough;
   }else continue;
   // A not-yet-initialized prefab is retried by the existing record audit.
   if((mask&BoundsMask.AllLayers)==0||!math.all(math.isfinite(bounds.min))||!math.all(math.isfinite(bounds.max)))continue;
   var tree=lane?laneTree:objectTree;
   bool found=tree.TryGet(e,out var previous);
   bool overridden=EntityManager.HasComponent<Overridden>(e),hidden=EntityManager.HasComponent<Game.Tools.Hidden>(e);
   bool missingRender=!EntityManager.HasComponent<CullingInfo>(e)||!EntityManager.HasBuffer<MeshBatch>(e);
   var bits=BoundsMask.AllLayers|BoundsMask.NotOverridden;
   bool mismatch=!found||(previous.m_Mask&bits)!=(mask&bits)||!previous.m_Bounds.Equals(bounds)||previous.m_MinLod!=lod;
   var culling=EntityManager.HasComponent<CullingInfo>(e)?EntityManager.GetComponentData<CullingInfo>(e):default;
   bool staleCulling=!EntityManager.HasComponent<Updated>(e)&&(culling.m_Mask&BoundsMask.AllLayers)!=(mask&BoundsMask.AllLayers);
   if(!mismatch&&!overridden&&!hidden&&!missingRender&&!staleCulling)continue;
   EntityManager.CompleteAllTrackedJobs();
   if(lane&&!laneWrite){laneTree=nets.GetLaneSearchTree(false,out laneJobs);laneJobs.Complete();laneWrite=true;}
   if(!lane&&!objectWrite){objectTree=objects.GetStaticSearchTree(false,out objectJobs);objectJobs.Complete();objectWrite=true;}
   tree=lane?laneTree:objectTree;
   // Mod-owned paint is an explicit overlay, not a removable roadside prop.
   // Intentional removals and deleted roads were excluded above.
   if(overridden)EntityManager.RemoveComponent<Overridden>(e);
   if(hidden)EntityManager.RemoveComponent<Game.Tools.Hidden>(e);
   if(!EntityManager.HasComponent<CullingInfo>(e))EntityManager.AddComponent<CullingInfo>(e);
   if(!EntityManager.HasBuffer<MeshBatch>(e))EntityManager.AddBuffer<MeshBatch>(e);
   if(mismatch){var entry=new QuadTreeBoundsXZ(bounds,mask,lod);if(found)tree.Update(e,entry);else tree.Add(e,entry);}
   if(!EntityManager.HasComponent<Updated>(e))EntityManager.AddComponent<Updated>(e);
   DirtyBatch(e);
  }
  // Synchronous writes are finished; publish that state to native readers.
  if(laneWrite)nets.AddLaneSearchTreeWriter(default);if(objectWrite)objects.AddStaticSearchTreeWriter(default);
 }
}}
