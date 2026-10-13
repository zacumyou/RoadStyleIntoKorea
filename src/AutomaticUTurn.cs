using Game.Net;
using Unity.Entities;
using Unity.Mathematics;
namespace RoadStyleIntoKorea {public partial class StyleSystem {
 bool AutoUTurnPosition(Entity road,float length,out float position){
  position=0;if(!IsRoad(road)||!EntityManager.HasComponent<Edge>(road))return false;
  var ends=EntityManager.GetComponentData<Edge>(road);var arc=new Geometry.Arc(CurveOf(road));
  // Evaluate both approaches; a two-edge transition/crossing is never eligible.
  foreach(bool start in new[]{false,true}){
   var node=start?ends.m_Start:ends.m_End;if(!IsGuideJunction(node))continue;
   var origin=start?arc.Curve.a:arc.Curve.d;var inward=Geometry.Tangent(arc.Curve,start?0:1)*(start?1:-1);
   float setback=0;
   foreach(var limit in EdgeApproachLimits(road,.2f))if(math.dot(limit.Inward,inward)>.9f)setback=math.max(setback,math.dot(limit.Origin-origin,inward));
   foreach(var crossing in NearbyCrossings(road)){
    var edge=CrossingRoad(crossing,out bool side);if(edge!=road||side!=start)continue;
    var c=CrosswalkCurve(crossing);
    setback=math.max(setback,math.max(math.dot(c.a-origin,inward),math.dot(c.d-origin,inward))+CrossingWidth(crossing)*.5f+AutomaticUTurnPolicy.CrosswalkGap);
   }
   if(!AutomaticUTurnPolicy.CenterDistance(arc.Length,length,setback,start,out float at))continue;
   position=arc.T(at);return true;
  }
  return false;
 }
}
}
