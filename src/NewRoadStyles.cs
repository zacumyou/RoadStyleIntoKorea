using System.Collections.Generic;
using Game.Common;
using Game.Net;
using PrefabRef=Game.Prefabs.PrefabRef;
using Game.Tools;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Colossal.Mathematics;
namespace RoadStyleIntoKorea { public partial class StyleSystem {
 readonly HashSet<Entity> knownRoads=new HashSet<Entity>();
 readonly Dictionary<Entity,Bezier4x3> knownRoadCurves=new Dictionary<Entity,Bezier4x3>();
 readonly List<KeyValuePair<Geometry.Arc,float>> replacedRoadCurves=new List<KeyValuePair<Geometry.Arc,float>>();
 sealed class ReplacedStyle {
  internal Geometry.Arc Arc;internal Entity Prefab;internal float Expires;
  internal CenterChoice? Center;internal EdgeChoice? Edge;internal CrosswalkChoice? Crosswalk;
  internal ApproachChoice? Approach;internal UTurnChoice? UTurn;
  internal DrawnLineChoice[] Drawn;internal PavingChoice[] Paving;internal ManualDecalChoice[] Manual;internal CurbChoice[] Curb;internal GutterChoice[] Gutter;internal SideRoadChoice[] SideRoad;
 }
 readonly List<ReplacedStyle> replacedStyles=new List<ReplacedStyle>();
 readonly WorkQueue<KeyValuePair<Entity,float>> newRoadStyles=new WorkQueue<KeyValuePair<Entity,float>>();
 EntityQuery createdRoads;bool newRoadBaseline;float newRoadAssetsAt,newRoadHistoryBusyUntil;
 void CreateNewRoadQuery(){createdRoads=GetEntityQuery(new EntityQueryDesc{All=new[]{ComponentType.ReadOnly<Edge>(),ComponentType.ReadOnly<Game.Net.Road>(),ComponentType.ReadOnly<Created>()},None=new[]{ComponentType.ReadOnly<Temp>(),ComponentType.ReadOnly<Deleted>()}});}
 void ClearNewRoadStyles(){knownRoads.Clear();knownRoadCurves.Clear();replacedRoadCurves.Clear();replacedStyles.Clear();newRoadStyles.Clear();newRoadBaseline=false;newRoadAssetsAt=newRoadHistoryBusyUntil=0;}
 T[] ChoiceBuffer<T>(Entity road) where T:unmanaged,IBufferElementData {
  if(!EntityManager.HasBuffer<T>(road))return null;
  var source=EntityManager.GetBuffer<T>(road,true);var result=new T[source.Length];for(int i=0;i<result.Length;i++)result[i]=source[i];return result;
 }
 void RememberReplacedStyle(Entity road,Bezier4x3 curve,float now){
  if(!HasRoadStyleChoice(road)||!EntityManager.HasComponent<PrefabRef>(road)||replacedStyles.Count>=128)return;
  // Native lane construction and the local work queue can outlive the short
  // automatic-style delay, especially when several roads are edited at once.
  replacedStyles.Add(new ReplacedStyle{Arc=new Geometry.Arc(curve),Prefab=Prefab(road),Expires=now+120,
   Center=EntityManager.HasComponent<CenterChoice>(road)?EntityManager.GetComponentData<CenterChoice>(road):(CenterChoice?)null,
   Edge=EntityManager.HasComponent<EdgeChoice>(road)?EntityManager.GetComponentData<EdgeChoice>(road):(EdgeChoice?)null,
   Crosswalk=EntityManager.HasComponent<CrosswalkChoice>(road)?EntityManager.GetComponentData<CrosswalkChoice>(road):(CrosswalkChoice?)null,
   Approach=EntityManager.HasComponent<ApproachChoice>(road)?EntityManager.GetComponentData<ApproachChoice>(road):(ApproachChoice?)null,
   UTurn=EntityManager.HasComponent<UTurnChoice>(road)?EntityManager.GetComponentData<UTurnChoice>(road):(UTurnChoice?)null,
   Drawn=SnapshotDrawnLines(road,curve),Paving=ChoiceBuffer<PavingChoice>(road),Manual=ChoiceBuffer<ManualDecalChoice>(road),Curb=ChoiceBuffer<CurbChoice>(road),Gutter=ChoiceBuffer<GutterChoice>(road),SideRoad=ChoiceBuffer<SideRoadChoice>(road)});
 }
 bool TryReplacedStyle(Entity road,out ReplacedStyle source,out bool reverse){
  source=null;reverse=false;if(!EntityManager.HasComponent<Curve>(road))return false;
  var curve=CurveOf(road);float best=float.MaxValue;
  foreach(var old in replacedStyles){if(old.Prefab!=Prefab(road)||!old.Arc.MayBeNear(Geometry.Position(curve,.5f),2))continue;
   float start=old.Arc.Nearest(curve.a),end=old.Arc.Nearest(curve.d);
   if(math.abs(end-start)<.001f)continue;
   bool backwards=end<start;float error=0;int overlap=0;bool middle=false;
   for(int i=0;i<=4;i++){
    float t=i*.25f,u=old.Arc.Nearest(Geometry.Position(curve,t));
    float distance=math.distancesq(Geometry.Position(curve,t),Geometry.Position(old.Arc.Curve,u));
    float direction=math.dot(Geometry.Tangent(curve,t),Geometry.Tangent(old.Arc.Curve,u));
    // Junction insertion can bend the very ends of the replacement edge.
    // Require the middle and most of the piece to follow the old road instead
    // of rejecting the entire split because one endpoint changed.
    bool aligned=distance<2.25f&&(backwards?-direction:direction)>.9f;
    if(aligned){overlap++;if(i==2&&distance<.5625f)middle=true;error+=distance;}
    else error+=4;
   }
   if(middle&&overlap>=3&&error<best){best=error;source=old;reverse=backwards;}
  }
  return source!=null;
 }
 void CopyChoiceBuffer<T>(Entity road,T[] choices) where T:unmanaged,IBufferElementData {
  if(choices==null||choices.Length==0)return;
  // A replacement edge can inherit an empty buffer while losing its contents.
  // Preserve any actual choices already present on the new edge.
  var target=EntityManager.HasBuffer<T>(road)?EntityManager.GetBuffer<T>(road):EntityManager.AddBuffer<T>(road);
  if(target.Length==0)foreach(var item in choices)target.Add(item);
 }
 bool InPiece(Geometry.Arc piece,Geometry.Arc old,float t){
  var point=old.At(old.Length*math.saturate(t));float u=piece.Nearest(point);
  return math.distancesq(point,Geometry.Position(piece.Curve,u))<.5625f;
 }
 void TransferReplacedStyle(Entity road,ReplacedStyle old,bool reverse){
  var piece=new Geometry.Arc(CurveOf(road));bool copied=InheritDrawnLines(road,old.Drawn);
  if(old.Center.HasValue&&!EntityManager.HasComponent<CenterChoice>(road)){
   var c=old.Center.Value;if(reverse){int side=c.Left;c.Left=c.Right;c.Right=side;}EntityManager.AddComponentData(road,c);copied=true;
  }
  if(old.Edge.HasValue&&!EntityManager.HasComponent<EdgeChoice>(road)){
   var c=old.Edge.Value;if(reverse){int side=c.Left;c.Left=c.Right;c.Right=side;}EntityManager.AddComponentData(road,c);copied=true;
  }
  if(old.Crosswalk.HasValue&&!EntityManager.HasComponent<CrosswalkChoice>(road)){
   var c=old.Crosswalk.Value;var mapped=new CrosswalkChoice();
   if(math.distancesq(piece.Curve.a,old.Arc.Curve.a)<1){mapped.Start=c.Start;mapped.ForceStartArrows=c.ForceStartArrows;}
   if(math.distancesq(piece.Curve.d,old.Arc.Curve.d)<1){mapped.End=c.End;mapped.ForceEndArrows=c.ForceEndArrows;}
   if(math.distancesq(piece.Curve.a,old.Arc.Curve.d)<1){mapped.Start=c.End;mapped.ForceStartArrows=c.ForceEndArrows;}
   if(math.distancesq(piece.Curve.d,old.Arc.Curve.a)<1){mapped.End=c.Start;mapped.ForceEndArrows=c.ForceStartArrows;}
   if(mapped.Start!=0||mapped.End!=0||mapped.ForceStartArrows||mapped.ForceEndArrows){EntityManager.AddComponentData(road,mapped);copied=true;}
  }
  if(old.Approach.HasValue&&!EntityManager.HasComponent<ApproachChoice>(road)){EntityManager.AddComponentData(road,old.Approach.Value);copied=true;}
  if(old.UTurn.HasValue&&!EntityManager.HasComponent<UTurnChoice>(road)&&InPiece(piece,old.Arc,old.UTurn.Value.Position)){
   var c=old.UTurn.Value;c.Position=piece.Nearest(old.Arc.At(old.Arc.Length*c.Position));if(reverse)c.Reverse=!c.Reverse;EntityManager.AddComponentData(road,c);copied=true;
  }
  // Composition sections and lateral offsets can change meaning when an edge reverses.
  if(!reverse){CopyChoiceBuffer(road,old.Paving);CopyChoiceBuffer(road,old.Curb);CopyChoiceBuffer(road,old.Gutter);copied|=old.Paving!=null||old.Curb!=null||old.Gutter!=null;}
  if(old.Manual!=null&&!EntityManager.HasBuffer<ManualDecalChoice>(road)){
   var target=EntityManager.AddBuffer<ManualDecalChoice>(road);
   foreach(var item in old.Manual)if(item.Kind==9||item.Kind==10||InPiece(piece,old.Arc,item.T)){
    var c=item;c.T=piece.Nearest(old.Arc.At(old.Arc.Length*item.T));if(reverse){c.Offset=-c.Offset;c.Reverse=!c.Reverse;}target.Add(c);copied=true;
   }
  }
  if(old.SideRoad!=null){
   if(!EntityManager.HasBuffer<SideRoadChoice>(road))EntityManager.AddBuffer<SideRoadChoice>(road);
   var target=EntityManager.GetBuffer<SideRoadChoice>(road);
   foreach(var item in old.SideRoad)if(item.Width>0&&InPiece(piece,old.Arc,item.T)){
    var c=item;c.T=piece.Nearest(old.Arc.At(old.Arc.Length*item.T));if(reverse)c.Side=-c.Side;
    bool exists=false;foreach(var previous in target)if(previous.Side==c.Side&&math.abs(previous.T-c.T)<.01f){exists=true;break;}
    if(!exists){target.Add(c);copied=true;}
   }
  }
  if(copied){EnqueueRoad(road);}
 }
 void RememberReplacedRoad(Bezier4x3 curve,float now){if(replacedRoadCurves.Count>=128){newRoadHistoryBusyUntil=now+5;return;}replacedRoadCurves.Add(new KeyValuePair<Geometry.Arc,float>(new Geometry.Arc(curve),now+5));}
 bool IsExistingRoadPiece(Entity road){
  if(!EntityManager.HasComponent<Curve>(road))return false;var curve=CurveOf(road);
  foreach(var old in replacedRoadCurves){bool matches=true;for(int i=0;i<=4;i++){
   float t=i*.25f;var point=Geometry.Position(curve,t);float u=old.Key.Nearest(point);
   if(math.distancesq(point,Geometry.Position(old.Key.Curve,u))>.04f||math.abs(math.dot(Geometry.Tangent(curve,t),Geometry.Tangent(old.Key.Curve,u)))<.98f){matches=false;break;}
  }if(matches)return true;}return false;
 }
 bool HasRoadStyleChoice(Entity road)=>EntityManager.HasBuffer<DrawnLineChoice>(road)||EntityManager.HasComponent<CenterChoice>(road)||EntityManager.HasComponent<EdgeChoice>(road)||EntityManager.HasComponent<CrosswalkChoice>(road)||EntityManager.HasComponent<ApproachChoice>(road)||EntityManager.HasComponent<UTurnChoice>(road)||EntityManager.HasBuffer<PavingChoice>(road)||EntityManager.HasBuffer<ManualDecalChoice>(road)||EntityManager.HasBuffer<CurbChoice>(road)||EntityManager.HasBuffer<GutterChoice>(road)||EntityManager.HasBuffer<SideRoadChoice>(road);
 void CollectNewRoadStyles(){{
  if(!newRoadBaseline){using(var list=roads.ToEntityArray(Allocator.Temp))foreach(var road in list){knownRoads.Add(road);if(EntityManager.HasComponent<Curve>(road))knownRoadCurves[road]=CurveOf(road);}newRoadBaseline=true;return;}
  float now=UnityEngine.Time.realtimeSinceStartup;
  replacedRoadCurves.RemoveAll(pair=>pair.Value<now);
  replacedStyles.RemoveAll(item=>item.Expires<now);
  using(var deleted=deletedOwners.ToEntityArray(Allocator.Temp))foreach(var road in deleted){knownRoads.Remove(road);if(knownRoadCurves.TryGetValue(road,out var old)){RememberReplacedRoad(old,now);RememberReplacedStyle(road,old,now);knownRoadCurves.Remove(road);}}
  using(var changed=dirty.ToEntityArray(Allocator.Temp))foreach(var road in changed)if(Live(road)&&EntityManager.HasComponent<Curve>(road)&&knownRoadCurves.TryGetValue(road,out var old)){
   var curve=CurveOf(road);if(!Geometry.SameCurve(old,curve)){
    // A split may shorten the original entity instead of deleting it.
    RememberReplacedRoad(old,now);RememberReplacedStyle(road,old,now);
    if(EntityManager.HasBuffer<DrawnLineChoice>(road)&&math.distance(curve.a,curve.d)<math.distance(old.a,old.d)*.9f)InheritDrawnLines(road,SnapshotDrawnLines(road,old),true);
    knownRoadCurves[road]=curve;
    if(EntityManager.HasBuffer<SideRoadChoice>(road)){
     var source=new Geometry.Arc(old);var piece=new Geometry.Arc(curve);var choices=EntityManager.GetBuffer<SideRoadChoice>(road);
     bool reverse=math.distancesq(curve.a,old.d)<math.distancesq(curve.a,old.a);
     for(int i=choices.Length-1;i>=0;i--){var c=choices[i];if(!InPiece(piece,source,c.T)){choices.RemoveAt(i);continue;}
      c.T=piece.Nearest(source.At(source.Length*c.T));if(reverse)c.Side=-c.Side;choices[i]=c;
     }
    }
   }
  }
  if(createdRoads.IsEmptyIgnoreFilter)return;
  using(var list=createdRoads.ToEntityArray(Allocator.Temp))foreach(var road in list){
   if(!knownRoads.Add(road))continue;
   if(EntityManager.HasComponent<Curve>(road))knownRoadCurves[road]=CurveOf(road);
   if(now<newRoadHistoryBusyUntil||!S.Enabled||restoring||restoreRequested||!IsRoad(road))continue;
   bool replaced=TryReplacedStyle(road,out _,out _);
   if(HasRoadStyleChoice(road)&&!replaced)continue;
   if(IsExistingRoadPiece(road)&&!replaced)continue;
   newRoadStyles.Enqueue(new KeyValuePair<Entity,float>(road,now+.5f));
  }
 }}
 void TickNewRoadStyles(){
  if(newRoadStyles.Count==0||loadRestorePending||defaultsReplacing||restoring||restoreRequested||!S.Enabled||HasStyleLoadWork()||dragStyleCommands.Count>0||nativeCrossingRequests.Count>0||nativeCrossingWaiting.Count>0)return;
  var next=newRoadStyles.Peek();if(UnityEngine.Time.realtimeSinceStartup<next.Value)return;
  var road=next.Key;
  if(!IsRoad(road)){newRoadStyles.Dequeue();return;}
  // Wait for native lane construction, but do not block later roads indefinitely.
  if(!EntityManager.HasBuffer<SubLane>(road)||EntityManager.GetBuffer<SubLane>(road,true).Length==0){newRoadStyles.Dequeue();if(UnityEngine.Time.realtimeSinceStartup-next.Value<5)newRoadStyles.Enqueue(next);return;}
  if(TryReplacedStyle(road,out var old,out bool reverse)){newRoadStyles.Dequeue();TransferReplacedStyle(road,old,reverse);return;}
  if(HasRoadStyleChoice(road)){newRoadStyles.Dequeue();return;}
  if(IsExistingRoadPiece(road)){newRoadStyles.Dequeue();return;}
  if(UnityEngine.Time.realtimeSinceStartup<newRoadAssetsAt)return;
  if(!CachedAssetsReady()){newRoadAssetsAt=UnityEngine.Time.realtimeSinceStartup+2;return;}
  newRoadStyles.Dequeue();ChooseLocalStyles(road,false,true);
 }
}}

