using System.Collections.Generic;
using Game.Net;
using Unity.Entities;
using Unity.Mathematics;
namespace RoadStyleIntoKorea {public partial class StyleSystem {
 readonly Dictionary<Entity,int> deferredLoad=new Dictionary<Entity,int>();
 readonly RotationQueue<Entity> loadCandidates=new RotationQueue<Entity>();
 readonly HashSet<Entity> loadCandidateSet=new HashSet<Entity>();
 int loadScanFrame=-1;
 bool progressiveLoad,loadBatchActive;float loadReadyAt,lastUserEdit=-100;
 float loadStartedAt;bool visibleLoadReported;
 void CancelProgressiveLoad(){deferredLoad.Clear();loadCandidates.Clear();loadCandidateSet.Clear();loadScanFrame=-1;progressiveLoad=loadBatchActive=false;}
 internal void PrioritizeUserEdit(){lastUserEdit=UnityEngine.Time.realtimeSinceStartup;loadBatchActive=false;}
 void DeferLoad(Entity owner,int flags){if(owner==Entity.Null)return;deferredLoad.TryGetValue(owner,out int old);deferredLoad[owner]=old|flags;if(loadCandidateSet.Add(owner))loadCandidates.Enqueue(owner);}
 void StageLoadWork(){
  foreach(var e in centerOnlyPending)DeferLoad(e,1);
  foreach(var e in edgeOnlyPending)DeferLoad(e,2);
  foreach(var e in crossOnlyPending)DeferLoad(e,4);
  foreach(var e in pavingOnlyPending)DeferLoad(e,8);
  foreach(var e in approachPending)DeferLoad(e,16);
  foreach(var e in guideDirty)DeferLoad(e,32);
  foreach(var e in manualDirty)DeferLoad(e,64);manualDirty.Clear();
  foreach(var e in curbDirty)DeferLoad(e,128);curbDirty.Clear();
  queue.Clear();pending.Clear();centerOnlyPending.Clear();edgeOnlyPending.Clear();crossOnlyPending.Clear();pavingOnlyPending.Clear();approachPending.Clear();guideDirty.Clear();
  progressiveLoad=deferredLoad.Count>0;loadBatchActive=false;
  loadStartedAt=UnityEngine.Time.realtimeSinceStartup;visibleLoadReported=false;loadReadyAt=loadStartedAt;
  if(progressiveLoad)targetedActive=true;
 }
 void QueueDerivedGuide(Entity node){
  if(!Live(node)||!EntityManager.HasBuffer<GuideChoice>(node))return;
  if(EntityManager.GetBuffer<GuideChoice>(node,true).Length==0&&!guideRecords.ContainsKey(node))return;
  if(progressiveLoad)DeferLoad(node,32);else guideDirty.Add(node);
 }
 bool BoundaryWorkPending(Entity owner)=>pending.Contains(owner)&&(!pavingOnlyPending.Contains(owner)||centerOnlyPending.Contains(owner)||crossOnlyPending.Contains(owner)||edgeOnlyPending.Contains(owner)||markingsOnlyPending.Contains(owner));
 bool GuideDependenciesPending(Entity node){
  if(BoundaryWorkPending(node))return true;
  if(deferredLoad.TryGetValue(node,out int own)&&PaintRecoveryPolicy.GuideDependency(own))return true;
  if(!Live(node)||!EntityManager.HasBuffer<ConnectedEdge>(node))return false;
  foreach(var edge in EntityManager.GetBuffer<ConnectedEdge>(node,true))
   if(BoundaryWorkPending(edge.m_Edge)||approachPending.Contains(edge.m_Edge)||approachStopsPending.Contains(edge.m_Edge)||manualDirty.Contains(edge.m_Edge)||(approachWork!=null&&approachOwner==edge.m_Edge)||(deferredLoad.TryGetValue(edge.m_Edge,out int mask)&&PaintRecoveryPolicy.GuideDependency(mask)))return true;
  return false;
 }
 float LoadDistance(Entity owner,float3 focus){
  if(!Live(owner))return float.MaxValue;
  if(EntityManager.HasComponent<Node>(owner))return math.distancesq(focus.xz,EntityManager.GetComponentData<Node>(owner).m_Position.xz);
  if(EntityManager.HasComponent<Curve>(owner)){var c=CurveOf(owner);return math.min(math.distancesq(focus.xz,c.a.xz),math.min(math.distancesq(focus.xz,c.d.xz),math.distancesq(focus.xz,Geometry.Position(c,.5f).xz)));}
  return float.MaxValue;
 }
 bool HasStyleLoadWork()=>curbDirty.Count>0||manualDirty.Count>0||queue.Count>0||approachWork!=null||approachPending.Count>0||approachStopsPending.Count>0||guideDirty.Count>0||guideCommands.Count>0||localStyleCommands.Count>0;
 bool AdmitLoadWork(){{
  if(!progressiveLoad)return false;
  if(restoreRequested||!S.Enabled){CancelProgressiveLoad();return false;}
  if(localStyleCommands.Count>0||guideCommands.Count>0)return false;
  if(!HasStyleLoadWork())loadBatchActive=false;
  if(deferredLoad.Count==0){if(HasStyleLoadWork()||flowPlans.Count>0||nativeCrossingRequests.Count>0||nativeCrossingWaiting.Count>0)return false;progressiveLoad=false;loadCandidates.Clear();loadCandidateSet.Clear();return false;}
  float now=UnityEngine.Time.realtimeSinceStartup;if(now<loadReadyAt)return false;
  if(!LoadBudgetPolicy.VisibleAdmission(queue.Count+approachPending.Count+manualDirty.Count+curbDirty.Count,now-lastUserEdit))return false;
  var camera=World.GetExistingSystemManaged<Game.Rendering.CameraUpdateSystem>();if(camera?.activeCameraController==null)return false;
  // A city-sized dictionary must not be walked six times each frame as the camera moves.
  // Rotate a bounded window; retain invisible entries for a later camera position.
  if(loadScanFrame==UnityEngine.Time.frameCount)return false;
  loadScanFrame=UnityEngine.Time.frameCount;
  float3 focus=camera.activeCameraController.pivot;
  var candidates=new List<(Entity Owner,float Distance)>();
  int scanned=math.min(64,loadCandidates.Count);
  for(int i=0;i<scanned;i++){
   var owner=loadCandidates.Dequeue();
   if(!deferredLoad.TryGetValue(owner,out int flags)){loadCandidateSet.Remove(owner);continue;}
   if(!Live(owner)){deferredLoad.Remove(owner);loadCandidateSet.Remove(owner);continue;}
   loadCandidates.Enqueue(owner);
   if(!LoadEligible(owner)||pending.Contains(owner)||approachPending.Contains(owner)||approachStopsPending.Contains(owner)||manualDirty.Contains(owner)||curbDirty.Contains(owner)||(approachWork!=null&&approachOwner==owner))continue;
   if(LoadBudgetPolicy.First(flags)==32&&GuideDependenciesPending(owner))continue;
   float distance=LoadDistance(owner,focus);candidates.Add((owner,distance));
  }
  if(candidates.Count==0){if(scanned>=loadCandidates.Count&&!HasStyleLoadWork()&&!visibleLoadReported){visibleLoadReported=true;}if(scanned>=loadCandidates.Count)loadReadyAt=now+.1f;return false;}
  candidates.Sort((a,b)=>a.Distance.CompareTo(b.Distance));
  int limit=LoadBudgetPolicy.AdmissionCount(queue.Count+approachPending.Count+manualDirty.Count+curbDirty.Count,UnityEngine.Time.unscaledDeltaTime);
  bool admitted=false;
  for(int i=0;i<math.min(limit,candidates.Count);i++){
  var selected=candidates[i].Owner;float best=candidates[i].Distance;
  // Re-evaluate camera distance before the timer so approaching promotes distant work.
  if(!LoadBudgetPolicy.VisibleAdmission(queue.Count+approachPending.Count+manualDirty.Count+curbDirty.Count,now-lastUserEdit))break;
  int mask=deferredLoad[selected],part=LoadBudgetPolicy.First(mask);
  if(part==32&&GuideDependenciesPending(selected))continue;
  mask&=~part;
  if(mask==0)deferredLoad.Remove(selected);else deferredLoad[selected]=mask;
  visibleLoadReported=false;loadBatchActive=true;
  switch(part){case 1:EnqueueCenterOnly(selected);break;case 2:EnqueueEdgeOnly(selected);break;case 4:EnqueueCrossOnly(selected);break;case 8:EnqueuePaving(selected);break;case 16:approachPending.Add(selected);break;case 32:guideDirty.Add(selected);break;case 64:manualDirty.Add(selected);break;case 128:curbDirty.Add(selected);break;}
admitted=true;
  }
  return admitted;
 }}
}}







