using System;
using System.Collections.Generic;
using Game.Net;
using Unity.Entities;
using Unity.Mathematics;
namespace RoadStyleIntoKorea {
 public partial class StyleSystem {
  readonly WorkQueue<Action> localStyleCommands=new WorkQueue<Action>();
  bool TickLocalStyles(){{
   if(TickCrossingDistanceEdits())return true;
   if(localStyleCommands.Count==0||queue.Count>0||approachWork!=null||approachPending.Count>0||approachStopsPending.Count>0)return false;
   EntityManager.CompleteAllTrackedJobs();
   try{localStyleCommands.Dequeue()();}catch(Exception ){}
   return true;
  }}
  internal bool LocalStyleTarget(Entity target)=>IsRoad(target)||SafeNode(target)&&EntityManager.HasBuffer<ConnectedEdge>(target);
  internal bool ChooseLocalStyles(Entity target,bool remove,bool defaults=false,bool suppressRoadSigns=false){
   if(!S.Enabled||!LocalStyleTarget(target))return false;
   var roadsHere=new List<Entity>();var nodesHere=new HashSet<Entity>();
   bool segment=IsRoad(target);
   if(segment){roadsHere.Add(target);var ends=EntityManager.GetComponentData<Edge>(target);nodesHere.Add(ends.m_Start);nodesHere.Add(ends.m_End);}
   else {nodesHere.Add(target);foreach(var c in EntityManager.GetBuffer<ConnectedEdge>(target,true))if(IsRoad(c.m_Edge)&&!roadsHere.Contains(c.m_Edge))roadsHere.Add(c.m_Edge);}
   bool affected=!remove||localStyleCommands.Count>0;
   foreach(var road in roadsHere)affected|=EntityManager.HasBuffer<SideRoadChoice>(road)||EntityManager.HasBuffer<DrawnLineChoice>(road)||EntityManager.HasBuffer<ManualDecalChoice>(road)||(EntityManager.HasBuffer<CurbChoice>(road)||EntityManager.HasBuffer<GutterChoice>(road));
   foreach(var road in roadsHere)affected|=EntityManager.HasComponent<CenterChoice>(road)||EntityManager.HasComponent<EdgeChoice>(road)||EntityManager.HasComponent<CrosswalkChoice>(road)||EntityManager.HasComponent<UTurnChoice>(road)||EntityManager.HasComponent<ApproachChoice>(road)||EntityManager.HasBuffer<PavingChoice>(road);
   foreach(var node in nodesHere)affected|=Live(node)&&EntityManager.HasBuffer<DrawnLineChoice>(node)||Live(node)&&EntityManager.HasBuffer<GuideChoice>(node)&&EntityManager.GetBuffer<GuideChoice>(node,true).Length>0;
   if(!affected)return false;
   foreach(var road in roadsHere){
    var owner=road;
    if(remove){
     localStyleCommands.Enqueue(()=>RemoveLocalRoad(owner,segment,target));
     continue;
    }
    localStyleCommands.Enqueue(()=>{
     if(!IsRoad(owner))return;PrepareToolTarget(owner);var ends=EntityManager.GetComponentData<Edge>(owner);
     if(remove){EntityManager.RemoveComponent<CurbChoice>(owner);EntityManager.RemoveComponent<GutterChoice>(owner);EntityManager.RemoveComponent<RemovedPaintChoice>(owner);EnqueueCurb(owner);EntityManager.RemoveComponent<ManualDecalChoice>(owner);EntityManager.RemoveComponent<DrawnLineChoice>(owner);manualDirty.Add(owner);EntityManager.RemoveComponent<CenterChoice>(owner);EntityManager.RemoveComponent<CenterPathChoice>(owner);EntityManager.RemoveComponent<EdgeChoice>(owner);EntityManager.RemoveComponent<UTurnChoice>(owner);}
     else {
      int center=defaults?(int)S.DefaultCenter:(int)(CenterDivided(owner)?EdgeStyle.YellowSolid:EdgeStyle.YellowDouble);
      int edge=defaults?(int)(S.EdgeLines?S.DefaultEdge:EdgeStyle.Original):(int)S.Brush;if(!defaults&&edge<2)edge=(int)EdgeStyle.YellowSolid;
      if(!EntityManager.HasComponent<CenterChoice>(owner))EntityManager.AddComponent<CenterChoice>(owner);
      EntityManager.RemoveComponent<CenterPathChoice>(owner);EntityManager.SetComponentData(owner,new CenterChoice{Left=center+1,Right=center+1});
      if(!EntityManager.HasComponent<EdgeChoice>(owner))EntityManager.AddComponent<EdgeChoice>(owner);
      EntityManager.SetComponentData(owner,new EdgeChoice{Left=edge+1,Right=edge+1});
      if((!defaults||S.UTurnZones)&&ManualUTurnAllowed(owner)){if(!EntityManager.HasComponent<UTurnChoice>(owner))EntityManager.AddComponent<UTurnChoice>(owner);EntityManager.SetComponentData(owner,new UTurnChoice{Position=.5f,Length=UTurnRoutePolicy.ClampLength(S.UTurnZoneLength),Automatic=true});}
     }
     EnqueueCenterOnly(owner);EnqueueCenterOnly(ends.m_Start);EnqueueCenterOnly(ends.m_End);
     EnqueueEdgeOnly(owner);EnqueueEdgeOnly(ends.m_Start);EnqueueEdgeOnly(ends.m_End);InvalidateToolPreview();
    });
    localStyleCommands.Enqueue(()=>{
     if(!IsRoad(owner))return;
     if(remove){EntityManager.RemoveComponent<PavingChoice>(owner);EnqueuePaving(owner);var e=EntityManager.GetComponentData<Edge>(owner);EnqueuePaving(e.m_Start);EnqueuePaving(e.m_End);}
     else {ApplyBulkStructures(owner,defaults);if(HighwayRoad(owner)&&!S.HighwayPaving)return;var old=S.PavingBrush;try{if(defaults)S.PavingBrush=S.DefaultPaving;var regions=PavingRegions(owner);
      if(EntityManager.HasBuffer<PavingChoice>(owner)){var choices=EntityManager.GetBuffer<PavingChoice>(owner);for(int i=choices.Length-1;i>=0;i--){var choice=choices[i];if(regions.Exists(r=>Matches(choice,r)&&!AutomaticPavingRegion(owner,r)))choices.RemoveAt(i);}}
      foreach(var region in regions)if(AutomaticPavingRegion(owner,region))ChoosePaving(owner,region,false);EnqueuePaving(owner);}finally{S.PavingBrush=old;}}
    });
    if(remove)foreach(bool atStart in new[]{true,false}){
     var ends=EntityManager.GetComponentData<Edge>(owner);if(!segment&&(atStart?ends.m_Start:ends.m_End)!=target)continue;
     bool start=atStart;
     localStyleCommands.Enqueue(()=>{
      if(!IsRoad(owner))return;var e=EntityManager.GetComponentData<Edge>(owner);
      if(!remove){bool exists=false;foreach(var lane in Crossings(owner,Lanes(owner))){if(CrossingRoad(lane,out bool side)==owner&&side==start){exists=true;break;}}if(!exists)QueueNativeCrossing(owner,start,false);}else QueueNativeCrossing(owner,start,true);
      var c=EntityManager.HasComponent<CrosswalkChoice>(owner)?EntityManager.GetComponentData<CrosswalkChoice>(owner):default;
      if(start)c.Start=remove?0:(int)S.CrosswalkBrush;else c.End=remove?0:(int)S.CrosswalkBrush;
      if(c.Start==0&&c.End==0)EntityManager.RemoveComponent<CrosswalkChoice>(owner);
      else {if(!EntityManager.HasComponent<CrosswalkChoice>(owner))EntityManager.AddComponent<CrosswalkChoice>(owner);EntityManager.SetComponentData(owner,c);}
      crossingPlans.Clear();crossingSizes.Clear();crossingTrafficPlans.Clear();InvalidateToolPreview();RefreshCrosswalkDependents(owner,start?e.m_Start:e.m_End);
     });
    }
    localStyleCommands.Enqueue(()=>{if(!IsRoad(owner))return;ChooseApproach(owner,remove,defaults||suppressRoadSigns);if(remove)EntityManager.RemoveComponent<ApproachChoice>(owner);});
   }
   foreach(var node in nodesHere){if(segment&&!remove)continue;var owner=node;localStyleCommands.Enqueue(()=>{if(remove){EntityManager.RemoveComponent<RemovedPaintChoice>(owner);EntityManager.RemoveComponent<DrawnLineChoice>(owner);manualDirty.Add(owner);}if(SafeNode(owner)&&(remove||IsGuideJunction(owner)))guideCommands.Enqueue(new GuideCommand{Node=owner,Kind=remove?3:1});});}
   targetedActive=true;Status=remove?"선택 범위 모두 복원 대기 / Restore local styles queued":"선택 범위 모두 추가 대기 / Apply local styles queued";
   return true;
  }
  void RemoveLocalRoad(Entity owner,bool segment,Entity target){{
   if(!IsRoad(owner))return;
   PrepareToolTarget(owner);var ends=EntityManager.GetComponentData<Edge>(owner);
   // Remove selections together before scheduling any dependent reconstruction.
   EntityManager.RemoveComponent<CurbChoice>(owner);EntityManager.RemoveComponent<GutterChoice>(owner);
   EntityManager.RemoveComponent<RemovedPaintChoice>(owner);EntityManager.RemoveComponent<ManualDecalChoice>(owner);
   EntityManager.RemoveComponent<DrawnLineChoice>(owner);EntityManager.RemoveComponent<CenterChoice>(owner);EntityManager.RemoveComponent<CenterPathChoice>(owner);
   EntityManager.RemoveComponent<EdgeChoice>(owner);EntityManager.RemoveComponent<UTurnChoice>(owner);
   int sideRoads=EntityManager.HasBuffer<SideRoadChoice>(owner)?EntityManager.GetBuffer<SideRoadChoice>(owner,true).Length:0;
   EntityManager.RemoveComponent<SideRoadChoice>(owner);sideRoadWork.Remove(owner);
   EntityManager.RemoveComponent<PavingChoice>(owner);
   foreach(bool start in new[]{true,false}){
    if(!segment&&(start?ends.m_Start:ends.m_End)!=target)continue;
    QueueNativeCrossing(owner,start,true);
    var c=EntityManager.HasComponent<CrosswalkChoice>(owner)?EntityManager.GetComponentData<CrosswalkChoice>(owner):default;
    if(start)c.Start=0;else c.End=0;
    if(c.Start==0&&c.End==0)EntityManager.RemoveComponent<CrosswalkChoice>(owner);
    else EntityManager.SetComponentData(owner,c);
    RefreshCrosswalkDependents(owner,start?ends.m_Start:ends.m_End);
   }
   ChooseApproach(owner,true);EntityManager.RemoveComponent<ApproachChoice>(owner);
   EnqueueCurb(owner);manualDirty.Add(owner);
   foreach(var entity in new[]{owner,ends.m_Start,ends.m_End}){
    EnqueueCenterOnly(entity);EnqueueEdgeOnly(entity);EnqueuePaving(entity);
   }
   crossingPlans.Clear();crossingSizes.Clear();crossingTrafficPlans.Clear();InvalidateToolPreview();
  }}
  void ApplyBulkStructures(Entity road,bool defaults){
   bool highway=HighwayRoad(road),curb=!highway||S.HighwayCurbs;
   if(!curb)return;
   var regions=PavingRegions(road);
   if(curb&&!EntityManager.HasBuffer<CurbChoice>(road))EntityManager.AddBuffer<CurbChoice>(road);
   foreach(var region in regions){
    if(!AutomaticPavingRegion(road,region))continue;
    if(curb&&!CurbSelected(road,region)){CurbLimits(region,out float min,out float max);EntityManager.GetBuffer<CurbChoice>(road).Add(new CurbChoice{Kind=region.Kind,Min=min,Max=max});}
   }
   EnqueueCurb(road);EnqueuePaving(road);var ends=EntityManager.GetComponentData<Edge>(road);EnqueuePaving(ends.m_Start);EnqueuePaving(ends.m_End);
  }
 }
}
