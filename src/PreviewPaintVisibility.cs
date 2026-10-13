using System.Collections.Generic;
using Game.Common;
using Game.Net;
using Game.Tools;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
namespace RoadStyleIntoKorea {public partial class StyleSystem {
 EntityQuery hiddenGeneratedPaint,previewNetworks;
 readonly HashSet<Entity> previewVisibleOwners=new HashSet<Entity>();
 readonly Dictionary<Entity,(Entity Owner,SubLane Lane)> previewDetachedPaint=new Dictionary<Entity,(Entity,SubLane)>();
 readonly List<Entity> previewRestoreKeys=new List<Entity>();
 void RestorePreviewAttachments(HashSet<Entity> retained=null){
  if(previewDetachedPaint.Count==0)return;
  previewRestoreKeys.Clear();previewRestoreKeys.AddRange(previewDetachedPaint.Keys);bool synced=false;
  foreach(var e in previewRestoreKeys){
   var saved=previewDetachedPaint[e];if(retained!=null&&retained.Contains(saved.Owner)&&Live(e))continue;
   previewDetachedPaint.Remove(e);
   if(!Live(e)||!Live(saved.Owner)||!EntityManager.HasBuffer<SubLane>(saved.Owner))continue;
   if(!synced){EntityManager.CompleteAllTrackedJobs();synced=true;}var lanes=EntityManager.GetBuffer<SubLane>(saved.Owner);bool present=false;
   foreach(var lane in lanes)if(lane.m_SubLane==e){present=true;break;}
   if(!present)lanes.Add(saved.Lane);
   if(EntityManager.HasComponent<Hidden>(saved.Owner)&&!EntityManager.HasComponent<Hidden>(e)){EntityManager.AddComponent<Hidden>(e);DirtyBatch(e);}
  }
 }
 void CreatePreviewVisibilityQueries(){
  hiddenGeneratedPaint=GetEntityQuery(new EntityQueryDesc{All=new[]{ComponentType.ReadOnly<GeneratedPaint>(),ComponentType.ReadOnly<Owner>()},Any=new[]{ComponentType.ReadOnly<Hidden>(),ComponentType.ReadOnly<Overridden>()},None=new[]{ComponentType.ReadOnly<Deleted>(),ComponentType.ReadOnly<Temp>()}});
  previewNetworks=GetEntityQuery(new EntityQueryDesc{All=new[]{ComponentType.ReadOnly<Temp>()},Any=new[]{ComponentType.ReadOnly<Edge>(),ComponentType.ReadOnly<Node>()},None=new[]{ComponentType.ReadOnly<Deleted>()}});
 }
 void PreservePreviewPaint(){
  previewVisibleOwners.Clear();
  if(S==null||!S.Enabled||previewNetworks.IsEmptyIgnoreFilter){RestorePreviewAttachments();return;}
  if(hiddenGeneratedPaint.IsEmptyIgnoreFilter&&previewDetachedPaint.Count==0&&paintRegistration.Count==0&&changedPaintRegistration.IsEmptyIgnoreFilter)return;
  // Query only hidden mod paint and temporary networks; never traverse the city.
  var unchanged=previewVisibleOwners;
  using(var previews=previewNetworks.ToEntityArray(Allocator.Temp))foreach(var preview in previews){
   var temp=EntityManager.GetComponentData<Temp>(preview);var original=temp.m_Original;
   if(!Live(original)||!EntityManager.HasComponent<Hidden>(original)||!TrackedStyleOwner(original))continue;
   if((temp.m_Flags&TempFlags.Delete)!=0||Prefab(preview)!=Prefab(original))continue;
   bool same=false;
   if(EntityManager.HasComponent<Edge>(original)&&EntityManager.HasComponent<Curve>(preview)&&EntityManager.HasComponent<Curve>(original))
    same=Geometry.SameCurve(EntityManager.GetComponentData<Curve>(preview).m_Bezier,EntityManager.GetComponentData<Curve>(original).m_Bezier);
   else if(EntityManager.HasComponent<Node>(original)&&EntityManager.HasComponent<Node>(preview))
    same=math.distancesq(EntityManager.GetComponentData<Node>(preview).m_Position,EntityManager.GetComponentData<Node>(original).m_Position)<.000001f;
   if(same)unchanged.Add(original);
  }
  RestorePreviewAttachments(unchanged);
  if(unchanged.Count==0)return;
  bool synced=false;int restored=0;
  using(var paint=hiddenGeneratedPaint.ToEntityArray(Allocator.Temp))foreach(var e in paint){
   var owner=EntityManager.GetComponentData<Owner>(e).m_Owner;
   if(!unchanged.Contains(owner)||removedPaint.Contains(e)||suppressedWarnings.Contains(e)||hiddenOwners.Contains(e))continue;
   if(!synced){EntityManager.CompleteAllTrackedJobs();synced=true;}
   // Detach only our overlay from native hide propagation. Owner and our own
   // lifetime records are retained so deletion/save/reset keep their semantics.
   if(EntityManager.HasBuffer<SubLane>(owner)){
    var lanes=EntityManager.GetBuffer<SubLane>(owner);
    for(int i=lanes.Length-1;i>=0;i--)if(lanes[i].m_SubLane==e){
     if(!previewDetachedPaint.ContainsKey(e))previewDetachedPaint[e]=(owner,lanes[i]);lanes.RemoveAt(i);
    }
   }
   EntityManager.RemoveComponent<Hidden>(e);EntityManager.RemoveComponent<Overridden>(e);DirtyBatch(e);QueuePaintRegistration(e);restored++;
  }
 }
}}
