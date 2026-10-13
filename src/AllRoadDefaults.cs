using System.Collections.Generic;
using Game.Net;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
namespace RoadStyleIntoKorea { public partial class StyleSystem {
 readonly WorkQueue<Entity> defaultsRoads=new WorkQueue<Entity>();
 readonly List<Entity> defaultsBatch=new List<Entity>();
 Entity defaultsCurrent;bool defaultsReplacing,defaultsFinalizing;
 int defaultsScope,defaultsJunctionFailureStart;float defaultsBatchStarted;
 readonly WorkQueue<Entity> defaultsNodeOrder=new WorkQueue<Entity>();
 readonly Dictionary<Entity,int> defaultsNodes=new Dictionary<Entity,int>();
 readonly HashSet<Entity> defaultsGuideNodes=new HashSet<Entity>();
 bool DeferDefaultNode(Entity node,int flags){
  if(!defaultsReplacing||defaultsFinalizing||!SafeNode(node))return false;
  if(!defaultsNodes.TryGetValue(node,out int old))defaultsNodeOrder.Enqueue(node);defaultsNodes[node]=old|flags;return true;
 }
 bool DefaultNodeScope(Entity node){
  if(defaultsScope==0)return true;
  foreach(var edge in EntityManager.GetBuffer<ConnectedEdge>(node,true))if(IsRoad(edge.m_Edge)&&!ApplyAllPolicy.Includes(defaultsScope,HighwayRoad(edge.m_Edge)))return false;
  return true;
 }
 double DefaultFrameBudget(){
  var simulation=World.GetExistingSystemManaged<Game.Simulation.SimulationSystem>();
  var screen=World.GetExistingSystemManaged<Game.UI.InGame.GameScreenUISystem>();
  return ApplyAllPolicy.Budget(simulation!=null&&simulation.selectedSpeed==0||screen!=null&&screen.isMenuActive);
 }
 bool DrainDefaultNodes(){
  if(!defaultsFinalizing){defaultsFinalizing=true;defaultsJunctionFailureStart=failed;defaultsState="교차로 마무리 / Finishing junctions";}
  int admitted=0;
  while(defaultsNodeOrder.Count>0&&admitted<ApplyAllPolicy.AdmissionLimit){
   var node=defaultsNodeOrder.Dequeue();admitted++;
   int flags=defaultsNodes[node];defaultsNodes.Remove(node);if(!SafeNode(node))continue;
   if((flags&1)!=0)EnqueueCenterOnly(node);if((flags&2)!=0)EnqueueEdgeOnly(node);if((flags&4)!=0)EnqueueCrossOnly(node);if((flags&8)!=0)EnqueuePaving(node);if((flags&128)!=0)curbDirty.Add(node);
   if(defaultsGuideNodes.Remove(node)&&DefaultNodeScope(node)){
    EntityManager.RemoveComponent<RemovedPaintChoice>(node);
    var selections=EntityManager.HasBuffer<GuideChoice>(node)?EntityManager.GetBuffer<GuideChoice>(node):EntityManager.AddBuffer<GuideChoice>(node);
    selections.Clear();if(IsGuideJunction(node))selections.Add(new GuideChoice{CleanupOnly=true});
   }
   if((flags&32)!=0)guideDirty.Add(node);
  }
  if(admitted>0){Status=AllRoadProgress;return true;}
  return false;
 }

 int defaultsTotal,defaultsDone,defaultsFailed,defaultsSkipped,defaultsFailureStart;
 EdgeStyle replacementCenter,replacementEdge;int replacementCrosswalk,replacementPaving;
 string defaultsState="대기 / Ready";
 internal bool CannotReplaceAll=>!ready||saving||defaultsReplacing||applyRequested||restoring||loadRestorePending;
 internal string AllRoadProgress=>defaultsState+" — 도로 segment / road segments: "+defaultsDone+" / "+defaultsTotal+
  ", 처리 중 / in progress: "+defaultsBatch.Count+", 실패 / failed: "+defaultsFailed+", 제외 / skipped: "+defaultsSkipped+(defaultsReplacing&&defaultsFinalizing?", 남음 / remaining: "+defaultsNodes.Count:"");
 void CancelDefaultReplacement(){defaultsNodes.Clear();defaultsNodeOrder.Clear();defaultsGuideNodes.Clear();defaultsFinalizing=false;defaultsRoads.Clear();defaultsBatch.Clear();defaultsCurrent=Entity.Null;if(defaultsReplacing)defaultsState="취소됨 / Cancelled";defaultsReplacing=false;}
 void BeginDefaultReplacement(){
  if(!ready||!S.Enabled)return;
  Inspect();if(assets.Missing(EntityManager,S).Count!=0)return;
  string center=CenterLinePolicy.Asset((int)S.DefaultCenter);
  if(center.Length>0&&assets.Get(center)==Entity.Null){Status="중앙선 에셋 누락 / Missing: "+center;return;}
  // Admission is deferred to ModificationEnd, after native jobs have completed.
  CancelLoadRestore();bulkRoads.Clear();ClearLinePlans();active=false;
  var camera=World.GetExistingSystemManaged<Game.Rendering.CameraUpdateSystem>();
  float3 focus=camera?.activeCameraController!=null?camera.activeCameraController.pivot:float3.zero;
  var sorted=new List<KeyValuePair<Entity,float>>();
  using(var list=roads.ToEntityArray(Allocator.Temp))foreach(var road in list)if(IsRoad(road)&&ApplyAllPolicy.Includes(defaultsScope,HighwayRoad(road)))sorted.Add(new KeyValuePair<Entity,float>(road,LoadDistance(road,focus)));
  sorted.Sort((a,b)=>{int order=a.Value.CompareTo(b.Value);return order!=0?order:a.Key.Index.CompareTo(b.Key.Index);});
  defaultsNodes.Clear();defaultsNodeOrder.Clear();defaultsGuideNodes.Clear();defaultsFinalizing=false;
  defaultsRoads.Clear();foreach(var road in sorted)defaultsRoads.Enqueue(road.Key);
  replacementCenter=(EdgeStyle)(int)S.DefaultCenter;replacementEdge=S.EdgeLines?S.DefaultEdge:EdgeStyle.Original;
  replacementCrosswalk=S.Crosswalks?(int)S.DefaultCrosswalk:0;replacementPaving=S.Paving?(int)S.DefaultPaving:0;
  defaultsBatch.Clear();defaultsCurrent=Entity.Null;defaultsTotal=sorted.Count;defaultsDone=defaultsFailed=defaultsSkipped=0;
  defaultsReplacing=true;defaultsState="교체 중 / Replacing";targetedActive=true;
  Status=AllRoadProgress; }
 void TickDefaultReplacement(){
  if(!defaultsReplacing||HasStyleLoadWork())return;
  if(defaultsBatch.Count>0){foreach(var completed in defaultsBatch){if(!IsRoad(completed))defaultsSkipped++;else if(failed>defaultsFailureStart)defaultsFailed++;else defaultsDone++;}defaultsBatch.Clear();defaultsCurrent=Entity.Null;}
  if(defaultsRoads.Count==0){if(DrainDefaultNodes())return;defaultsReplacing=false;defaultsState="완료 / Finished";Status=AllRoadProgress;return;}
  defaultsBatchStarted=UnityEngine.Time.realtimeSinceStartup;int attempts=defaultsRoads.Count;var admission=System.Diagnostics.Stopwatch.StartNew();defaultsFailureStart=failed;
  while(defaultsRoads.Count>0&&attempts-->0&&defaultsBatch.Count<ApplyAllPolicy.AdmissionLimit&&admission.Elapsed.TotalMilliseconds<DefaultFrameBudget()*.5){
   var road=defaultsRoads.Dequeue();if(!IsRoad(road)){defaultsSkipped++;continue;}
   var ends=EntityManager.GetComponentData<Edge>(road);
   if(PaintStillCreated(road)||PaintStillCreated(ends.m_Start)||PaintStillCreated(ends.m_End)){defaultsRoads.Enqueue(road);continue;}
   if(defaultsBatch.Count==0)EntityManager.CompleteAllTrackedJobs();
   defaultsBatch.Add(road);defaultsCurrent=road;ApplyDefaultRoad(road);
  }
  Status=AllRoadProgress;
 }
 void ApplyDefaultRoad(Entity road){
  try {
   Release(road);EntityManager.RemoveComponent<RemovedPaintChoice>(road);
   // Optional structures have no global replacement style: defaults restore
   // their native state, rather than retaining a per-road override.
   bool hadStructures=EntityManager.HasBuffer<CurbChoice>(road)||EntityManager.HasBuffer<GutterChoice>(road);
   EntityManager.RemoveComponent<CurbChoice>(road);EntityManager.RemoveComponent<GutterChoice>(road);
   if(hadStructures)EnqueueCurb(road);
   EntityManager.RemoveComponent<UTurnChoice>(road);
   if(ManualUTurnAllowed(road)){EntityManager.AddComponentData(road,new UTurnChoice{Position=.5f,Length=UTurnRoutePolicy.ClampLength(S.UTurnZoneLength),Automatic=true});}
   if(!EntityManager.HasComponent<CenterChoice>(road))EntityManager.AddComponent<CenterChoice>(road);
   EntityManager.RemoveComponent<CenterPathChoice>(road);EntityManager.SetComponentData(road,new CenterChoice{Left=(int)replacementCenter+1,Right=(int)replacementCenter+1});
   if(!EntityManager.HasComponent<EdgeChoice>(road))EntityManager.AddComponent<EdgeChoice>(road);
   EntityManager.SetComponentData(road,new EdgeChoice{Left=(int)replacementEdge+1,Right=(int)replacementEdge+1});
   if(!EntityManager.HasComponent<CrosswalkChoice>(road))EntityManager.AddComponent<CrosswalkChoice>(road);
   int pavingStyle=ApplyAllPavingStyle(road),crosswalkStyle=ApplyAllPolicy.Crosswalk(replacementCrosswalk,pavingStyle);
   EntityManager.SetComponentData(road,new CrosswalkChoice{Start=crosswalkStyle,End=crosswalkStyle});
   var regions=PavingRegions(road);
   if(!EntityManager.HasBuffer<PavingChoice>(road))EntityManager.AddBuffer<PavingChoice>(road);
   var paving=EntityManager.GetBuffer<PavingChoice>(road);paving.Clear();
   if(replacementPaving>0&&(!HighwayRoad(road)||S.HighwayPaving))foreach(var region in regions)if(AutomaticPavingRegion(road,region))paving.Add(new PavingChoice{Section=region.Section,Kind=region.Kind,Style=pavingStyle,Min=region.Min,Max=region.Max});
   ApplyBulkStructures(road,true);
   crossingPlans.Clear();crossingTrafficPlans.Clear();crossingSizes.Clear();ClearLinePlans();
   var ends=EntityManager.GetComponentData<Edge>(road);
   foreach(var node in new[]{ends.m_Start,ends.m_End})if(SafeNode(node)){
    defaultsGuideNodes.Add(node);DeferDefaultNode(node,1|2|4|8|32|128);
   }
   foreach(var owner in new[]{road,ends.m_Start,ends.m_End}){EnqueueCenterOnly(owner);EnqueueEdgeOnly(owner);EnqueueCrossOnly(owner);EnqueuePaving(owner);}
   // Persist the default approach instructions too, so arrows/stops survive reload.
   if(EntityManager.HasBuffer<ManualDecalChoice>(road)){var choices=EntityManager.GetBuffer<ManualDecalChoice>(road);for(int i=choices.Length-1;i>=0;i--)if(choices[i].Kind==7)choices.RemoveAt(i);}
   ChooseApproach(road,false,!ApplyAllPolicy.SpeedSigns(Geometry.Position(CurveOf(road),.5f)));
  }catch(System.Exception ){failed++;}
  Status=AllRoadProgress;
 }
}}
