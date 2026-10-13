using System.Collections.Generic;
using Game.Common;
using Unity.Entities;
using Unity.Collections;
namespace RoadStyleIntoKorea {public partial class StyleSystem {
 readonly WorkQueue<(Entity Owner,Record Record,int Kind)> paintAudit=new WorkQueue<(Entity,Record,int)>();
 float nextPaintAudit;
 readonly WorkQueue<Entity> renderRepair=new WorkQueue<Entity>();
 readonly HashSet<Entity> renderRepairPending=new HashSet<Entity>();
 void RepairRenderBatches(){
  for(int i=0;i<8&&renderRepair.Count>0;i++){
   var e=renderRepair.Dequeue();renderRepairPending.Remove(e);
   if(!Live(e)||EntityManager.HasComponent<Game.Tools.Hidden>(e)||EntityManager.HasComponent<Created>(e))continue;
   EntityManager.CompleteAllTrackedJobs();
   // Native PreCullingSystem reserves zero for an unregistered entry (-1 is invalid).
   if(!EntityManager.HasComponent<Game.Rendering.CullingInfo>(e))EntityManager.AddComponentData(e,new Game.Rendering.CullingInfo());
   if(!EntityManager.HasBuffer<Game.Rendering.MeshBatch>(e))EntityManager.AddBuffer<Game.Rendering.MeshBatch>(e);
   if(!EntityManager.HasComponent<Updated>(e))EntityManager.AddComponent<Updated>(e);
   DirtyBatch(e);
  }
 }
 void AuditPaint(){{
  if(!S.Enabled||restoreRequested||restoring||loadRestorePending)return;
  RepairRenderBatches();
  float now=UnityEngine.Time.realtimeSinceStartup;
  if(paintAudit.Count==0){
   if(now<nextPaintAudit)return;nextPaintAudit=now+5;
   foreach(var p in records)paintAudit.Enqueue((p.Key,p.Value,0));
   foreach(var p in approachRecords)paintAudit.Enqueue((p.Key,p.Value,1));
   foreach(var p in approachStopRecords)paintAudit.Enqueue((p.Key,p.Value,2));
   foreach(var p in manualRecords)paintAudit.Enqueue((p.Key,p.Value,3));
   foreach(var p in curbRecords)paintAudit.Enqueue((p.Key,p.Value,5)); foreach(var p in guideRecords)paintAudit.Enqueue((p.Key,p.Value,4));
  }
  if(paintAudit.Count==0)return;
  var item=paintAudit.Dequeue();if(!Live(item.Owner)){
   if(TrackedStyleOwner(item.Owner)){EntityManager.CompleteAllTrackedJobs();ReleaseDeletedStyleOwner(item.Owner);}
   return;
  }
  if(pending.Contains(item.Owner)||manualDirty.Contains(item.Owner)||curbDirty.Contains(item.Owner)||guideDirty.Contains(item.Owner)||approachPending.Contains(item.Owner)||approachStopsPending.Contains(item.Owner)||(approachWork!=null&&approachOwner==item.Owner))return;
  var dictionary=item.Kind==0?records:item.Kind==1?approachRecords:item.Kind==2?approachStopRecords:item.Kind==3?manualRecords:item.Kind==5?curbRecords:guideRecords;
  if(!dictionary.TryGetValue(item.Owner,out var current)||!object.ReferenceEquals(current,item.Record))return;
  bool near=LoadVisible(item.Owner);
  foreach(var e in current.Paint)QueuePaintRegistration(e);
  int repaired=0;
  foreach(var e in current.Paint)if(near&&Live(e)&&!EntityManager.HasComponent<Game.Tools.Hidden>(e)&&!EntityManager.HasComponent<Created>(e)){
   bool missingRender=!EntityManager.HasComponent<Game.Rendering.CullingInfo>(e)||!EntityManager.HasBuffer<Game.Rendering.MeshBatch>(e);
   if(missingRender&&renderRepairPending.Add(e)){renderRepair.Enqueue(e);repaired++;}
  }
  foreach(var surface in current.PavingSurfaces){
   if(surface.Object==null||surface.Mesh==null){if(item.Kind==5)curbDirty.Add(item.Owner);else EnqueuePaving(item.Owner);break;}
  }
  if(current.SideRoadMarks!=null)foreach(var surface in current.SideRoadMarks.PavingSurfaces){
   if(surface.Object==null||surface.Mesh==null){EnqueueEdgeOnly(item.Owner);break;}
  }
  // Native regeneration can clear rendering tags without replacing the entity.
  foreach(var e in current.Hidden)if(Live(e)&&(!EntityManager.HasComponent<Game.Tools.Hidden>(e)||!EntityManager.HasComponent<OriginalPaintHidden>(e))){
   EntityManager.AddComponent<Game.Tools.Hidden>(e);EntityManager.AddComponent<OriginalPaintHidden>(e);DirtyBatch(e);
  }
  bool missing=false;foreach(var e in current.Paint)if(!EntityManager.Exists(e)||EntityManager.HasComponent<Deleted>(e)){missing=true;break;}
  if(!missing)return;
  // Only repair the category already represented by this record. No new choices.
  if(item.Kind==1)approachPending.Add(item.Owner);
  else if(item.Kind==2)approachStopsPending.Add(item.Owner);
  else if(item.Kind==3)manualDirty.Add(item.Owner);
  else if(item.Kind==5)curbDirty.Add(item.Owner); else if(item.Kind==4)guideDirty.Add(item.Owner);
  else if(current.FullMarkings)EnqueueMarkings(item.Owner);
  else {
   if(MissingPaint(current.PavingPaint))EnqueuePaving(item.Owner);
   if(MissingPaint(current.CenterPaint))EnqueueCenterOnly(item.Owner);
   if(MissingPaint(current.CrossPaint))EnqueueCrossOnly(item.Owner);
   if(MissingPaint(current.EdgePaint)||MissingPaint(current.EdgeJoins))EnqueueEdgeOnly(item.Owner);
  }
 }}
 bool MissingPaint(List<Entity> paint){foreach(var e in paint)if(!EntityManager.Exists(e)||EntityManager.HasComponent<Deleted>(e))return true;return false;}
}}
