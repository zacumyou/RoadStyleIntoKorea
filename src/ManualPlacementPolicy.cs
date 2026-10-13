using Unity.Mathematics;
namespace RoadStyleIntoKorea {internal static class ManualPlacementPolicy {
 internal static float3 IntersectionNormal(float3 forward,float3 laneDirection,bool both)=>both&&math.dot(laneDirection,forward)<0?-forward:forward;
 internal static float SidewalkOffset(float3 hit,float3 left,float3 right,float width){var across=right.xz-left.xz;return (math.dot(hit.xz-left.xz,across)/math.max(.0001f,math.lengthsq(across))-.5f)*width;}
 internal static ManualDecalChoice ContinuousStop(System.Collections.Generic.List<ManualDecalChoice> lanes,ManualDecalChoice anchor){
  float min=float.MaxValue,max=float.MinValue;
  foreach(var lane in lanes){min=math.min(min,lane.Offset-lane.Width*.5f);max=math.max(max,lane.Offset+lane.Width*.5f);}
  if(lanes.Count>0){anchor.Offset=(min+max)*.5f;anchor.Width=max-min;}return anchor;
 }
 internal static Colossal.Mathematics.Bezier4x3 StopLine(float3 position,float3 forward,float width){
  var half=Geometry.Left(forward)*(width*.5f);
  return Geometry.Line(position-half,position+half);
 }
 internal static bool SameDirection(float3 forward,float3 point,float3 direction,float3 other)=>math.dot(forward,direction)>=.8f&&math.abs(other.y-point.y)<=2;
}}
