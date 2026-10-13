using System;
using System.Collections.Generic;
using Game.Common;
using Game.Net;
using Game.Prefabs;
using Game.Tools;
using Unity.Collections;
using Unity.Entities;
namespace RoadStyleIntoKorea {public partial class StyleSystem {
 bool loadRestorePending;
 float nextLoadAssetCheck;
 void CancelLoadRestore(){loadRestorePending=false;CancelProgressiveLoad();}

 void RestoreBeforeWorldReady(){
  loadRestorePending=true;nextLoadAssetCheck=0;
 }
 // Query only saved selections; untouched roads need no restore work.
 HashSet<Entity> SavedLoadRoads(){
  var result=new HashSet<Entity>();
  using(var selected=EntityManager.CreateEntityQuery(new EntityQueryDesc{
   All=new[]{ComponentType.ReadOnly<Edge>(),ComponentType.ReadOnly<Game.Net.Road>(),ComponentType.ReadOnly<PrefabRef>()},
   Any=new[]{ComponentType.ReadOnly<CurbChoice>(),ComponentType.ReadOnly<GutterChoice>(),ComponentType.ReadOnly<ManualDecalChoice>(),ComponentType.ReadOnly<ApproachChoice>(),ComponentType.ReadOnly<CenterChoice>(),ComponentType.ReadOnly<UTurnChoice>(),ComponentType.ReadOnly<EdgeChoice>(),ComponentType.ReadOnly<CrosswalkChoice>(),ComponentType.ReadOnly<PavingChoice>(),ComponentType.ReadOnly<SideRoadChoice>()},
   None=new[]{ComponentType.ReadOnly<Deleted>(),ComponentType.ReadOnly<Temp>()}}))
  using(var list=selected.ToEntityArray(Allocator.Temp))foreach(var edge in list)result.Add(edge);
  // A crossing-distance choice lives on the junction, not its adjacent edges.
  using(var nodes=EntityManager.CreateEntityQuery(ComponentType.ReadOnly<CrossingDistanceChoice>(),ComponentType.ReadOnly<ConnectedEdge>()))
  using(var list=nodes.ToEntityArray(Allocator.Temp))foreach(var node in list)if(Live(node))foreach(var edge in EntityManager.GetBuffer<ConnectedEdge>(node,true))result.Add(edge.m_Edge);
  return result;
 }
 void BeginScheduledRestore(){{
  if(!loadRestorePending||restoreRequested)return;
  try{
   float now=UnityEngine.Time.realtimeSinceStartup;if(now<nextLoadAssetCheck)return;
   if(!CachedAssetsReady()){nextLoadAssetCheck=now+.25f;return;}
   EntityManager.CompleteAllTrackedJobs();
   loadRestorePending=false;active=false;completed=failed=unsupportedArrows=0;
   RestoreGuideSelections();RestoreDrawnLines();
   // Persisted choices only; every visual is rebuilt by the distance-priority queue.
   foreach(var edge in SavedLoadRoads())if(IsRoad(edge)){
    var ends=EntityManager.GetComponentData<Game.Net.Edge>(edge);
    if((EntityManager.HasBuffer<CurbChoice>(edge)||EntityManager.HasBuffer<GutterChoice>(edge)))DeferLoad(edge,128);
    if(EntityManager.HasBuffer<ManualDecalChoice>(edge))DeferLoad(edge,64);
    if(EntityManager.HasComponent<ApproachChoice>(edge)&&EntityManager.GetComponentData<ApproachChoice>(edge).Enabled)DeferLoad(edge,16);
    if(EntityManager.HasComponent<CenterChoice>(edge)||EntityManager.HasComponent<UTurnChoice>(edge)){DeferLoad(edge,1);DeferLoad(ends.m_Start,1);DeferLoad(ends.m_End,1);}
    if(EntityManager.HasComponent<EdgeChoice>(edge)){DeferLoad(edge,2);DeferLoad(ends.m_Start,2);DeferLoad(ends.m_End,2);}
    if(EntityManager.HasComponent<CrosswalkChoice>(edge)||EntityManager.HasComponent<CrossingDistanceChoice>(ends.m_Start)||EntityManager.HasComponent<CrossingDistanceChoice>(ends.m_End)){DeferLoad(edge,4);DeferLoad(ends.m_Start,4);DeferLoad(ends.m_End,4);}
    if(EntityManager.HasBuffer<PavingChoice>(edge)||EntityManager.HasBuffer<SideRoadChoice>(edge)){DeferLoad(edge,8);DeferLoad(ends.m_Start,8);DeferLoad(ends.m_End,8);}
    if(EntityManager.HasBuffer<SideRoadChoice>(edge)){DeferLoad(edge,2);DeferLoad(edge,128);}
   }
   total=deferredLoad.Count;StageLoadWork();  }catch(Exception ){}
 }}
 bool PaintStillCreated(Entity owner){
  if(!records.TryGetValue(owner,out var record))return false;
  foreach(var entity in record.Paint)if(EntityManager.Exists(entity)&&EntityManager.HasComponent<Game.Common.Created>(entity))return true;
  return false;
 }
}}
