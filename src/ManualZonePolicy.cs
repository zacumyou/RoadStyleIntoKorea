using Colossal.Mathematics;using Unity.Mathematics;
namespace RoadStyleIntoKorea {internal static class ManualZonePolicy {internal static bool Interval(Bezier4x3 roadCurve,Bezier4x3 curve,float position,float length,out float lo,out float hi){
  return Interval(new Geometry.Arc(roadCurve),new Geometry.Arc(curve),position,length,out lo,out hi);
 }
 internal static bool Interval(Geometry.Arc road,Geometry.Arc arc,float position,float length,out float lo,out float hi){
  var curve=arc.Curve;
  float at=road.Distance(position),half=length*.5f;
  float a=road.Distance(road.Nearest(curve.a)),b=road.Distance(road.Nearest(curve.d));
  lo=hi=0;if(math.max(a,b)<at-half||math.min(a,b)>at+half)return false;
  float t0=arc.Nearest(road.At(math.max(0,at-half))),t1=arc.Nearest(road.At(math.min(road.Length,at+half)));
  lo=arc.Distance(math.min(t0,t1));hi=arc.Distance(math.max(t0,t1));return hi-lo>.1f;
}
 internal static System.Collections.Generic.IEnumerable<Bezier4x3> Outside(Geometry.Arc arc,float lo,float hi){
  lo=math.clamp(lo,0,arc.Length);hi=math.clamp(hi,lo,arc.Length);
  if(lo>.025f)yield return arc.Slice(0,lo);
  if(hi<arc.Length-.025f)yield return arc.Slice(hi,arc.Length);
 }
}}
