using System.Collections.Generic;
using Colossal.Mathematics;
using Game.Net;
using Unity.Entities;
using Unity.Mathematics;
namespace RoadStyleIntoKorea { public partial class StyleSystem {
 readonly Dictionary<(Entity,float,bool),List<EdgeApproachPolicy.Limit>> boundaryFrameCache=new Dictionary<(Entity,float,bool),List<EdgeApproachPolicy.Limit>>();
 List<EdgeApproachPolicy.Limit> CrossingBoundaries(Entity node,float clearance,bool interior=false){
  CheckCrossingFrameCache();var key=(node,clearance,interior);
  if(boundaryFrameCache.TryGetValue(key,out var cached))return cached;
  var limits=new List<EdgeApproachPolicy.Limit>();
  foreach(var crossing in NearbyCrossings(node)){
   var road=CrossingRoad(crossing,out bool start);if(!IsRoad(road))continue;
   var edge=EntityManager.GetComponentData<Edge>(road);if((start?edge.m_Start:edge.m_End)!=node)continue;
   var c=CrosswalkCurve(crossing);var mid=Geometry.Position(c,.5f);var center=new Geometry.Arc(CurveOf(road));
   var outward=Geometry.Tangent(center.Curve,center.Nearest(mid))*(start?1:-1);
   float extent=CrossingWidth(crossing)*.5f*math.abs(math.dot(Geometry.Left(Geometry.Tangent(c,.5f)),outward));
   float far=math.max(math.dot(c.a-mid,outward),math.dot(c.d-mid,outward))+extent+clearance;
   float near=math.min(math.dot(c.a-mid,outward),math.dot(c.d-mid,outward))-extent-clearance;
   limits.Add(new EdgeApproachPolicy.Limit{Origin=mid+outward*(interior?near:far),Inward=interior?-outward:outward});
  }
  boundaryFrameCache[key]=limits;return limits;
 }
 IEnumerable<Bezier4x3> BoundaryPaint(Entity owner,Bezier4x3 curve,float clearance){
  var crossings=NearbyCrossings(owner);
  if(IsRoad(owner)){foreach(var part in ClearPaint(curve,crossings,clearance,owner))yield return part;yield break;}
  var limits=CrossingBoundaries(owner,clearance);
  foreach(var part in EdgeApproachPolicy.OutsideJunction(curve,limits))
   foreach(var clear in ClearPaint(part,crossings,clearance))yield return clear;
 }
 void QueueBoundaryGuides(Entity owner){
  QueueDerivedGuide(owner);
  if(!EntityManager.HasComponent<Edge>(owner))return;
  var edge=EntityManager.GetComponentData<Edge>(owner);
  foreach(var node in new[]{edge.m_Start,edge.m_End})QueueDerivedGuide(node);
 }
 void CleanGuideInterior(Entity node,Record record){
  var owners=new HashSet<Entity>{node};
  if(EntityManager.HasBuffer<ConnectedEdge>(node))foreach(var item in EntityManager.GetBuffer<ConnectedEdge>(node,true))if(IsRoad(item.m_Edge))owners.Add(item.m_Edge);
  var limits=CrossingBoundaries(node,.35f);
  foreach(var owner in owners){
   var candidates=new HashSet<Entity>(Lanes(owner));
   if(records.TryGetValue(owner,out var existing))foreach(var e in existing.Paint)candidates.Add(e);
   if(approachStopRecords.TryGetValue(owner,out var stops))foreach(var e in stops.Paint)candidates.Add(e);
   foreach(var lane in candidates){
    if(!Live(lane)||hiddenOwners.Contains(lane)||EntityManager.HasComponent<Game.Tools.Hidden>(lane)||!EntityManager.HasComponent<Curve>(lane)||EntityManager.HasComponent<CarLane>(lane)||EntityManager.HasComponent<PedestrianLane>(lane))continue;
    string name=assets.Name(Prefab(lane));if(!MarkingPolicy.IsNativeJunctionMark(name))continue;
    if(!EntityManager.HasComponent<Game.Prefabs.NetLaneGeometryData>(Prefab(lane)))continue;
    var source=CurveOf(lane);var pieces=new List<Bezier4x3>();
    // Without crossings retain the original node-only divider cleanup scope.
    if(limits.Count==0){if(owner!=node||!MarkingPolicy.IsGuideDivider(name))continue;}
    else pieces.AddRange(EdgeApproachPolicy.OutsideJunction(source,limits));
    if(EntityManager.HasComponent<Edge>(owner)){var ends=EntityManager.GetComponentData<Edge>(owner);var other=ends.m_Start==node?ends.m_End:ends.m_Start;
     if(other!=node&&EntityManager.HasBuffer<GuideChoice>(other)&&EntityManager.GetBuffer<GuideChoice>(other,true).Length>0){
      var both=new List<Bezier4x3>();foreach(var piece in pieces)both.AddRange(EdgeApproachPolicy.OutsideJunction(piece,CrossingBoundaries(other,.35f)));pieces=both;
     }
    }
    float kept=0;foreach(var piece in pieces)kept+=new Geometry.Arc(piece).Length;
    if(new Geometry.Arc(source).Length-kept<.025f)continue;
    foreach(var piece in pieces)PaintLane(node,record,Prefab(lane),piece);
    Hide(lane,record);
   }
  }
 }
}}
