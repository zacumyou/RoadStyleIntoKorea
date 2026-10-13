using System;
using System.Collections.Generic;
using Game.Net;
using Game.Tools;
using Unity.Collections;
using Unity.Entities;
namespace RoadStyleIntoKorea {
public enum ReplaceCategory { Center, Edge, Crosswalk, Paving }
public enum CenterStyle { None=1,WhiteSolid=2,YellowSolid=3,YellowDouble=4 }
public partial class StyleSystem {
 readonly HashSet<Entity> centerOnlyPending=new HashSet<Entity>();
 readonly WorkQueue<Entity> bulkRoads=new WorkQueue<Entity>();
 ReplaceCategory bulkCategory;int bulkStyle;
 void TrackCenter(Record r,Action draw){int before=r.Paint.Count;var hidden=new HashSet<Entity>(r.Hidden);draw();for(int i=before;i<r.Paint.Count;i++)r.CenterPaint.Add(r.Paint[i]);foreach(var e in r.Hidden)if(!hidden.Contains(e))r.CenterHidden.Add(e);}
 void ClearCenter(Record r){
  foreach(var e in r.CenterPaint){if(EntityManager.Exists(e)&&!EntityManager.HasComponent<Game.Common.Deleted>(e))RetirePaint(e);r.Paint.Remove(e);}r.CenterPaint.Clear();
  foreach(var e in r.CenterHidden){RestoreHidden(e,r);r.Hidden.Remove(e);}r.CenterHidden.Clear();
 }
 bool targetedActive;
 void EnqueueSelective(Entity owner,HashSet<Entity> category){
  if(owner==Entity.Null)return;targetedActive=true;
  int mask=category==centerOnlyPending?1:category==edgeOnlyPending?2:category==crossOnlyPending?4:category==pavingOnlyPending?8:0;
  if(mask!=0&&DeferDefaultNode(owner,mask))return;
  if(pending.Contains(owner)){if(centerOnlyPending.Contains(owner)||crossOnlyPending.Contains(owner)||edgeOnlyPending.Contains(owner)||pavingOnlyPending.Contains(owner))category.Add(owner);return;}
  Enqueue(owner);category.Add(owner);
 }
 void EnqueueCenterOnly(Entity owner){EnqueueSelective(owner,centerOnlyPending);}

 internal void RequestBulk(){
  if(!ready||!S.Enabled)return;
  if(assets.Named.Count==0)assets.Refresh(EntityManager,World.GetExistingSystemManaged<Game.Prefabs.PrefabSystem>());
  if(S.BulkCategory==ReplaceCategory.Edge&&!S.EdgeLines||S.BulkCategory==ReplaceCategory.Crosswalk&&!S.Crosswalks||S.BulkCategory==ReplaceCategory.Paving&&!S.Paving){Status="해당 분야 사용 옵션을 먼저 켜세요 / Enable this category first";return;}
  bulkCategory=S.BulkCategory;bulkStyle=bulkCategory==ReplaceCategory.Center?(int)S.BulkCenter:bulkCategory==ReplaceCategory.Edge?(int)S.BulkEdge:bulkCategory==ReplaceCategory.Crosswalk?(int)S.BulkCrosswalk:(int)S.BulkPaving;
  string name=bulkCategory==ReplaceCategory.Center?CenterLinePolicy.Asset(bulkStyle):bulkCategory==ReplaceCategory.Edge?AssetCatalog.Edge((EdgeStyle)bulkStyle):bulkCategory==ReplaceCategory.Crosswalk?AssetCatalog.Crosswalk(bulkStyle):"";
  if(!string.IsNullOrEmpty(name)&&assets.Get(name)==Entity.Null){Status="에셋 누락 / Missing: "+name;return;}
  bulkRoads.Clear();using(var a=roads.ToEntityArray(Allocator.Temp))foreach(var edge in a)if(IsRoad(edge))bulkRoads.Enqueue(edge);
  Status="분야별 교체 대기 / Category replacement queued: "+bulkRoads.Count;
 }
 void TickBulk(){
  if(!S.Enabled){bulkRoads.Clear();return;}if(bulkRoads.Count==0)return;
  var timer=System.Diagnostics.Stopwatch.StartNew();int n=0;
  while(bulkRoads.Count>0&&n++<4&&timer.Elapsed.TotalMilliseconds<1){
   var edge=bulkRoads.Dequeue();if(!IsRoad(edge))continue;var ends=EntityManager.GetComponentData<Edge>(edge);
   if(bulkCategory==ReplaceCategory.Center){if(!EntityManager.HasComponent<CenterChoice>(edge))EntityManager.AddComponent<CenterChoice>(edge);EntityManager.RemoveComponent<CenterPathChoice>(edge);EntityManager.SetComponentData(edge,new CenterChoice{Left=bulkStyle+1,Right=bulkStyle+1});EnqueueCenterOnly(edge);EnqueueCenterOnly(ends.m_Start);EnqueueCenterOnly(ends.m_End);}
   else if(bulkCategory==ReplaceCategory.Edge){if(!EntityManager.HasComponent<EdgeChoice>(edge))EntityManager.AddComponent<EdgeChoice>(edge);EntityManager.SetComponentData(edge,new EdgeChoice{Left=bulkStyle+1,Right=bulkStyle+1});EnqueueEdgeOnly(edge);EnqueueEdgeOnly(ends.m_Start);EnqueueEdgeOnly(ends.m_End);}
   else if(bulkCategory==ReplaceCategory.Crosswalk){if(!EntityManager.HasComponent<CrosswalkChoice>(edge))EntityManager.AddComponent<CrosswalkChoice>(edge);EntityManager.SetComponentData(edge,new CrosswalkChoice{Start=bulkStyle,End=bulkStyle});crossingPlans.Clear();crossingTrafficPlans.Clear();crossingSizes.Clear();EnqueueCrossOnly(edge);EnqueueCrossOnly(ends.m_Start);EnqueueCrossOnly(ends.m_End);RefreshCrosswalkApproach(edge);}
   else {var old=S.PavingBrush;try{S.PavingBrush=(PavingStyle)bulkStyle;foreach(var region in PavingRegions(edge))ChoosePaving(edge,region,false);}finally{S.PavingBrush=old;}}
  }
 }
}}


