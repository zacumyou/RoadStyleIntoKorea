using System.Collections.Generic;
using Game.Net;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
namespace RoadStyleIntoKorea { public partial class StyleSystem {
 readonly WorkQueue<Entity> defaultsRoads=new WorkQueue<Entity>();
 Entity defaultsCurrent;bool defaultsReplacing;
 int defaultsTotal,defaultsDone,defaultsFailed,defaultsSkipped,defaultsFailureStart;
 EdgeStyle replacementCenter,replacementEdge;int replacementCrosswalk,replacementPaving;
 string defaultsState="대기 / Ready";
 internal bool CannotReplaceAll=>!ready||saving||defaultsReplacing||applyRequested||restoring||loadRestorePending;
 internal string AllRoadProgress=>defaultsState+" — 도로 segment / road segments: "+defaultsDone+" / "+defaultsTotal+
  ", 처리 중 / in progress: "+(defaultsCurrent==Entity.Null?0:1)+", 실패 / failed: "+defaultsFailed+", 제외 / skipped: "+defaultsSkipped;
 void CancelDefaultReplacement(){defaultsRoads.Clear();defaultsCurrent=Entity.Null;if(defaultsReplacing)defaultsState="취소됨 / Cancelled";defaultsReplacing=false;}
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
  using(var list=roads.ToEntityArray(Allocator.Temp))foreach(var road in list)if(IsRoad(road))sorted.Add(new KeyValuePair<Entity,float>(road,LoadDistance(road,focus)));
  sorted.Sort((a,b)=>{int order=a.Value.CompareTo(b.Value);return order!=0?order:a.Key.Index.CompareTo(b.Key.Index);});
  defaultsRoads.Clear();foreach(var road in sorted)defaultsRoads.Enqueue(road.Key);
  replacementCenter=(EdgeStyle)(int)S.DefaultCenter;replacementEdge=S.EdgeLines?S.DefaultEdge:EdgeStyle.Original;
  replacementCrosswalk=S.Crosswalks?(int)S.DefaultCrosswalk:0;replacementPaving=S.Paving?(int)S.DefaultPaving:0;
  defaultsCurrent=Entity.Null;defaultsTotal=sorted.Count;defaultsDone=defaultsFailed=defaultsSkipped=0;
  defaultsReplacing=true;defaultsState="교체 중 / Replacing";targetedActive=true;
  Status=AllRoadProgress; }
 void TickDefaultReplacement(){
  if(!defaultsReplacing||HasStyleLoadWork())return;
  if(defaultsCurrent!=Entity.Null){if(!IsRoad(defaultsCurrent))defaultsSkipped++;else if(failed>defaultsFailureStart)defaultsFailed++;else defaultsDone++;defaultsCurrent=Entity.Null;}
  if(defaultsRoads.Count==0){defaultsReplacing=false;defaultsState="완료 / Finished";Status=AllRoadProgress;return;}
  var road=defaultsRoads.Dequeue();if(!IsRoad(road)){defaultsSkipped++;return;}
  var roadEnds=EntityManager.GetComponentData<Edge>(road);
  if(PaintStillCreated(road)||PaintStillCreated(roadEnds.m_Start)||PaintStillCreated(roadEnds.m_End)){defaultsRoads.Enqueue(road);return;}
  EntityManager.CompleteAllTrackedJobs();defaultsCurrent=road;defaultsFailureStart=failed;
  try {
   Release(road);EntityManager.RemoveComponent<RemovedPaintChoice>(road);
   // Optional structures have no global replacement style: defaults restore
   // their native state, rather than retaining a per-road override.
   bool hadStructures=EntityManager.HasBuffer<CurbChoice>(road)||EntityManager.HasBuffer<GutterChoice>(road);
   EntityManager.RemoveComponent<CurbChoice>(road);EntityManager.RemoveComponent<GutterChoice>(road);
   if(hadStructures)EnqueueCurb(road);
   EntityManager.RemoveComponent<UTurnChoice>(road);
   if(S.UTurnZones&&ManualUTurnAllowed(road)){EntityManager.AddComponentData(road,new UTurnChoice{Position=.5f,Length=UTurnRoutePolicy.ClampLength(S.UTurnZoneLength),Automatic=true});}
   if(!EntityManager.HasComponent<CenterChoice>(road))EntityManager.AddComponent<CenterChoice>(road);
   EntityManager.SetComponentData(road,new CenterChoice{Left=(int)replacementCenter+1,Right=(int)replacementCenter+1});
   if(!EntityManager.HasComponent<EdgeChoice>(road))EntityManager.AddComponent<EdgeChoice>(road);
   EntityManager.SetComponentData(road,new EdgeChoice{Left=(int)replacementEdge+1,Right=(int)replacementEdge+1});
   if(!EntityManager.HasComponent<CrosswalkChoice>(road))EntityManager.AddComponent<CrosswalkChoice>(road);
   EntityManager.SetComponentData(road,new CrosswalkChoice{Start=replacementCrosswalk,End=replacementCrosswalk});
   var regions=PavingRegions(road);
   if(!EntityManager.HasBuffer<PavingChoice>(road))EntityManager.AddBuffer<PavingChoice>(road);
   var paving=EntityManager.GetBuffer<PavingChoice>(road);paving.Clear();
   if(replacementPaving>0&&(!HighwayRoad(road)||S.HighwayPaving))foreach(var region in regions)if(AutomaticPavingRegion(road,region))paving.Add(new PavingChoice{Section=region.Section,Kind=region.Kind,Style=replacementPaving,Min=region.Min,Max=region.Max});
   ApplyBulkStructures(road,true);
   crossingPlans.Clear();crossingTrafficPlans.Clear();crossingSizes.Clear();ClearLinePlans();
   var ends=EntityManager.GetComponentData<Edge>(road);
   foreach(var node in new[]{ends.m_Start,ends.m_End})if(SafeNode(node)){
    Release(node);
    EntityManager.RemoveComponent<RemovedPaintChoice>(node);
    if(EntityManager.HasBuffer<GuideChoice>(node))EntityManager.GetBuffer<GuideChoice>(node).Clear();
    guideDirty.Add(node);
    if(S.LeftGuides&&IsGuideJunction(node))guideCommands.Enqueue(new GuideCommand{Node=node,Kind=1});
   }
   foreach(var owner in new[]{road,ends.m_Start,ends.m_End}){EnqueueCenterOnly(owner);EnqueueEdgeOnly(owner);EnqueueCrossOnly(owner);EnqueuePaving(owner);}
   // Persist the default approach instructions too, so arrows/stops survive reload.
   if(EntityManager.HasBuffer<ManualDecalChoice>(road)){var choices=EntityManager.GetBuffer<ManualDecalChoice>(road);for(int i=choices.Length-1;i>=0;i--)if(choices[i].Kind==7)choices.RemoveAt(i);}
   ChooseApproach(road,false);
  }catch(System.Exception ){failed++;}
  Status=AllRoadProgress;
 }
}}

