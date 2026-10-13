using System;using System.Collections.Generic;using Game.Net;using Game.Tools;using Unity.Entities;using Unity.Mathematics;
namespace RoadStyleIntoKorea {public partial class StyleSystem {
readonly HashSet<Entity> crossOnlyPending=new HashSet<Entity>();
void EnqueueCrossOnly(Entity e,bool updateGuides=true){if(EntityManager.HasBuffer<DrawnLineChoice>(e))manualDirty.Add(e);if(SafeNode(e))foreach(var link in EntityManager.GetBuffer<ConnectedEdge>(e,true))if(EntityManager.HasBuffer<DrawnLineChoice>(link.m_Edge))manualDirty.Add(link.m_Edge);ClearLinePlans();if(updateGuides&&EntityManager.Exists(e)&&EntityManager.HasBuffer<GuideChoice>(e))QueueDerivedGuide(e);EnqueueSelective(e,crossOnlyPending);}
void TrackCross(Record r,Action draw){int before=r.Paint.Count;var hidden=new HashSet<Entity>(r.Hidden);draw();for(int i=before;i<r.Paint.Count;i++)r.CrossPaint.Add(r.Paint[i]);foreach(var e in r.Hidden)if(!hidden.Contains(e))r.CrossHidden.Add(e);}
void ClearCross(Record r){foreach(var e in r.CrossPaint){if(EntityManager.Exists(e)&&!EntityManager.HasComponent<Game.Common.Deleted>(e))RetirePaint(e);r.Paint.Remove(e);}r.CrossPaint.Clear();foreach(var e in r.CrossHidden){RestoreHidden(e,r);r.Hidden.Remove(e);}r.CrossHidden.Clear();}
void HideApproachStops(Record r,List<Entity> lanes,List<Drive> drives){
 if(!S.StopLines)return;
 var stops=new HashSet<Entity>(lanes);
 foreach(var d in drives)if(d.StopPainted||d.StopSuppressed)foreach(var lane in Lanes(d.End))stops.Add(lane);
 foreach(var lane in stops){
  if(assets.Name(Prefab(lane)).IndexOf("Stop Line",StringComparison.OrdinalIgnoreCase)<0)continue;
  var arc=new Geometry.Arc(CurveOf(lane));
  foreach(var d in drives){
   if(!d.StopPainted&&!d.StopSuppressed)continue;
   var direction=d.Arc.Direction(d.Arc.Length);float t=arc.Nearest(d.Curve.d);var delta=Geometry.Position(arc.Curve,t)-d.Curve.d;
   if(math.abs(delta.y)>2||math.abs(math.dot(Geometry.Tangent(arc.Curve,t),direction))>.5f||math.abs(math.dot(delta,direction))>=15||math.abs(math.dot(delta,Geometry.Left(direction)))>d.Width*.5f+.3f)continue;
   Hide(lane,r);break;
  }
 }
}
void ProtectCrosswalkStops(Entity owner,Record record,List<Entity> lanes){
 var crossings=NearbyCrossings(owner);var affected=new HashSet<Entity>();
 foreach(var lane in lanes){
  if(assets.Name(Prefab(lane)).IndexOf("Stop Line",StringComparison.OrdinalIgnoreCase)<0)continue;
  foreach(var crossing in crossings){
   if(!StyleCrossing(crossing)||!MarkingPolicy.StopOverlapsCrosswalk(CurveOf(lane),CrossingSpan(crossing),CrossingWidth(crossing)))continue;
   affected.Add(CanonicalCrossing(crossing));Hide(lane,record);
   break;
  }
 }
 if(!IsRoad(owner)||EntityManager.HasComponent<ApproachChoice>(owner)&&EntityManager.GetComponentData<ApproachChoice>(owner).Enabled)return;
 bool restore=false;foreach(var lane in lanes)restore|=flowCurves.ContainsKey(lane);
 if(affected.Count==0&&!restore)return;
 var drives=Drives(lanes);FindStops(owner,lanes,drives);
 foreach(var drive in drives)if(!affected.Contains(drive.Crossing))drive.Stop=-1;
 AlignStopLines(owner,record,drives);PlanStopFlow(owner,lanes,drives);
}
void PaintCrossCategory(Entity owner,Record r){if(!IsRoad(owner)&&!SafeNode(owner))return;var lanes=Lanes(owner);ReplaceCrosswalks(owner,r,lanes);ProtectCrosswalkStops(owner,r,lanes);if(SafeNode(owner))PlanPedestrianFlow(owner,lanes);}
}}
