using Colossal.Mathematics;
using Unity.Mathematics;
namespace RoadStyleIntoKorea {internal static class CrosswalkDecalPolicy {
 internal static bool Overlaps(Bezier4x3 crossing,float width,float3 position,float3 forward,float halfLength,float halfWidth){
  var arc=new Geometry.Arc(crossing);float t=arc.Nearest(position);var point=Geometry.Position(crossing,t);
  if(math.abs(point.y-position.y)>2)return false;
  var tangent=Geometry.Tangent(crossing,t);var normal=Geometry.Left(tangent);
  float c=math.abs(math.dot(forward,normal)),s=math.abs(math.dot(forward,tangent));
  return math.abs(math.dot(position-point,tangent))<=halfLength*s+halfWidth*c+.5f
   &&math.abs(math.dot(position-point,normal))<=width*.5f+halfLength*c+halfWidth*s+.5f;
 }
}}
