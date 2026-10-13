using System.Collections.Generic;
using Colossal.Mathematics;
using Game.Net;
using CarLane=Game.Net.CarLane;
using Game.Prefabs;
using Unity.Entities;
using Unity.Mathematics;
using UnityEngine;
namespace RoadStyleIntoKorea {public partial class StyleSystem {
 sealed class DividerTarget {internal Entity Lane;internal Bezier4x3 Curve;internal Geometry.Arc Arc;internal float Offset;internal bool Reverse;internal int Default;}
 internal int MergeDividerBrush,BusDividerBrush;
 internal void CycleDivider(int group){if(group==1)MergeDividerBrush=DividerStylePolicy.Next(group,MergeDividerBrush);else BusDividerBrush=DividerStylePolicy.Next(group,BusDividerBrush);}
 Entity dividerPreviewRoad;int dividerPreviewRevision=-1;List<DividerTarget> dividerPreview;
 bool DividerReady(int style)=>style>0&&style<DividerStylePolicy.Assets.Length&&EntityManager.HasComponent<NetLaneGeometryData>(assets.Get(DividerStylePolicy.Assets[style]));
 List<DividerTarget> DividerTargets(Entity road,List<Entity> lanes,List<Drive> drives){
  var transitions=new Dictionary<Drive,int>();
  if(HighwayRoad(road)){var walk=new DividerWalk();walk.Lanes[road]=lanes;walk.Drives[road]=drives;foreach(var d in drives)transitions[d]=DividerTransition(road,d,walk);}
  var result=new List<DividerTarget>();var center=CurveOf(road);var centerArc=new Geometry.Arc(center);
  foreach(var lane in lanes){
   if(!MarkingPolicy.IsLaneDivider(assets.Name(Prefab(lane)))||!EntityManager.HasComponent<NetLaneGeometryData>(Prefab(lane)))continue;
   var c=CurveOf(lane);var p=Geometry.Position(c,.5f);var f=Geometry.Tangent(c,.5f);Drive left=null,right=null;float ld=float.MaxValue,rd=float.MaxValue;
   // First find a nearby traffic direction; native marking curves may be reversed.
   Drive nearest=null;float best=float.MaxValue;
   foreach(var d in drives){d.Arc.Nearest(p,out var dp,out var df);float distance=math.distancesq(dp,p);if(distance<best){best=distance;nearest=d;f=df;}}
   if(nearest==null)continue;
   foreach(var d in drives){d.Arc.Nearest(p,out var dp,out var df);if(math.dot(df,f)<.8f||math.abs(dp.y-p.y)>1.5f)continue;
    float side=math.dot(dp-p,Geometry.Left(f));if(math.abs(side)>d.Width*.5f+1f)continue;
    if(side>0&&side<ld){left=d;ld=side;}else if(side<0&&-side<rd){right=d;rd=-side;}
   }
   if(left==null||right==null)continue; // Never treat a road edge or opposing center line as a divider.
   if(math.dot(Geometry.Tangent(c,.5f),f)<0)c=Geometry.Reverse(c);
   float t=centerArc.Nearest(p);var cf=Geometry.Tangent(center,t);float offset=math.dot(p-Geometry.Position(center,t),Geometry.Left(cf));
   bool lb=(EntityManager.GetComponentData<CarLane>(left.Entity).m_Flags&CarLaneFlags.PublicOnly)!=0;
   bool rb=(EntityManager.GetComponentData<CarLane>(right.Entity).m_Flags&CarLaneFlags.PublicOnly)!=0;
   result.Add(new DividerTarget{Lane=lane,Curve=c,Arc=new Geometry.Arc(c),Offset=offset,Reverse=math.dot(cf,f)<0,Default=DividerStylePolicy.Default(HighwayRoad(road),lb,rb,transitions.TryGetValue(left,out int lt)?lt:0,transitions.TryGetValue(right,out int rt)?rt:0)});
  }
  // Continue onto a two-road taper only when its native marking actually joins
  // the directed edge divider. Never reach across an ordinary intersection.
  var ends=EntityManager.GetComponentData<Edge>(road);var edgeTargets=new List<DividerTarget>(result);var seen=new HashSet<Entity>();foreach(var t in result)seen.Add(t.Lane);
  foreach(var node in new[]{ends.m_Start,ends.m_End}){
   if(!EntityManager.HasBuffer<ConnectedEdge>(node))continue;int count=0;foreach(var e in EntityManager.GetBuffer<ConnectedEdge>(node,true))if(IsRoad(e.m_Edge))count++;
   if(count!=2)continue;
   foreach(var lane in Lanes(node)){
    if(seen.Contains(lane)||!MarkingPolicy.IsLaneDivider(assets.Name(Prefab(lane)))||!EntityManager.HasComponent<NetLaneGeometryData>(Prefab(lane)))continue;
    var c=CurveOf(lane);
    foreach(var t in edgeTargets){var joined=c;if(math.distance(joined.d,t.Curve.d)<.25f)joined=Geometry.Reverse(joined);
     bool downstream=math.distance(joined.a,t.Curve.d)<=.25f&&math.dot(Geometry.Tangent(joined,0),Geometry.Tangent(t.Curve,1))>=.8f;
     if(!downstream){joined=c;if(math.distance(joined.a,t.Curve.a)<.25f)joined=Geometry.Reverse(joined);if(math.distance(joined.d,t.Curve.a)>.25f||math.dot(Geometry.Tangent(joined,1),Geometry.Tangent(t.Curve,0))<.8f)continue;}
     result.Add(new DividerTarget{Lane=lane,Curve=joined,Arc=new Geometry.Arc(joined),Offset=t.Offset,Reverse=t.Reverse,Default=t.Default});seen.Add(lane);break;
    }
   }
  }
  return result;
 }
 bool DividerOverride(Entity road,DividerTarget target,out int style){style=0;if(!EntityManager.HasBuffer<ManualDecalChoice>(road))return false;
  foreach(var c in EntityManager.GetBuffer<ManualDecalChoice>(road,true))if(c.Kind==7&&c.Reverse==target.Reverse&&math.abs(c.Offset-target.Offset)<.6f){style=c.Symbol==0?target.Default:c.Symbol;return true;}return false;
 }
 HashSet<Entity> StyledDividerSources(Entity road,List<DividerTarget> targets){var result=new HashSet<Entity>();foreach(var target in targets){bool manual=DividerOverride(road,target,out int style);if(DividerReady(manual?style:target.Default))result.Add(target.Lane);}return result;}
 void ApplyDividerStyles(Entity road,Record record,List<Entity> lanes,List<Drive> drives){var targets=DividerTargets(road,lanes,drives);PaintAutomaticDividers(road,record,targets);if(S.SolidApproach)SolidDividers(road,record,lanes,drives,StyledDividerSources(road,targets));}
 void PaintAutomaticDividers(Entity road,Record record,List<DividerTarget> targets){
  foreach(var t in targets){if(DividerOverride(road,t,out _)||!DividerReady(t.Default))continue;PaintDivider(road,record,assets.Get(DividerStylePolicy.Assets[t.Default]),t.Curve);Hide(t.Lane,record);}
 }
 void PaintManualDividers(Entity road,Record record,List<ManualDecalChoice> choices){
  if(!choices.Exists(c=>c.Kind==7))return;var lanes=Lanes(road);var drives=Drives(lanes);FindStops(road,lanes,drives);
  foreach(var t in DividerTargets(road,lanes,drives)){if(!DividerOverride(road,t,out int style)||!DividerReady(style))continue;PaintDivider(road,record,assets.Get(DividerStylePolicy.Assets[style]),t.Curve);Hide(t.Lane,record);}
 }
 void RefreshDividerStyles(Entity road){manualDirty.Add(road);RequestApproachStops(road);if(active)EnqueueMarkings(road);InvalidateToolPreview();}
 internal bool DividerTool(Entity road,float3 hit,int group,Game.Rendering.OverlayRenderSystem.Buffer overlay,bool apply,bool reset){
  if(!IsRoad(road)||group==1&&!HighwayRoad(road)||!CachedAssetsReady())return false;
  if(dividerPreviewRoad!=road||dividerPreviewRevision!=toolRevision){var lanes=Lanes(road);var drives=Drives(lanes);FindStops(road,lanes,drives);dividerPreview=DividerTargets(road,lanes,drives);dividerPreviewRoad=road;dividerPreviewRevision=toolRevision;}
  DividerTarget picked=null;float best=4;
  foreach(var target in dividerPreview){var point=Geometry.Position(target.Curve,target.Arc.Nearest(hit));float d=math.distancesq(point,hit);if(d<best){best=d;picked=target;}}
  if(picked==null)return false;int brush=group==1?MergeDividerBrush:BusDividerBrush;int style=brush==0?picked.Default:brush;
  overlay.DrawCurve(new Color(.1f,.85f,1,.85f),picked.Curve,.25f);
  if(!apply&&!reset)return true;
  if(!reset&&!DividerReady(style)){Status="구분선 에셋 또는 자동 대상 없음 / Divider asset or automatic target unavailable";return true;}
  float offset=picked.Offset;bool reverse=picked.Reverse;
  localStyleCommands.Enqueue(()=>{if(!IsRoad(road))return;if(!EntityManager.HasBuffer<ManualDecalChoice>(road))EntityManager.AddBuffer<ManualDecalChoice>(road);var choices=EntityManager.GetBuffer<ManualDecalChoice>(road);
   for(int i=choices.Length-1;i>=0;i--)if(choices[i].Kind==7&&choices[i].Reverse==reverse&&math.abs(choices[i].Offset-offset)<.6f)choices.RemoveAt(i);
   if(!reset)choices.Add(new ManualDecalChoice{Kind=7,Symbol=brush,T=.5f,Offset=offset,Width=1,Reverse=reverse});
   targetedActive=true;RefreshDividerStyles(road);
  });return true;
 }
}}
