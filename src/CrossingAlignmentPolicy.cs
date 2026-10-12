using Colossal.Mathematics;using Unity.Mathematics;
namespace RoadStyleIntoKorea {
internal static class CrossingAlignmentPolicy {
internal static Bezier4x3 Align(Bezier4x3 original,float3 forward){
var across=Geometry.Left(forward);var center=(original.a+original.d)*.5f;
if(math.dot(original.d-original.a,across)<0)across=-across;
float span=math.abs(math.dot(original.d-original.a,across));
if(span<.5f)return original;
var a=center-across*span*.5f;var b=center+across*span*.5f;a.y=original.a.y;b.y=original.d.y;
return Geometry.Line(a,b);
}
internal static float3 StopNormal(Bezier4x3 crossing,float3 forward){
var normal=Geometry.Left(Geometry.Tangent(crossing,.5f));if(math.dot(normal,forward)<0)normal=-normal;return normal;
}
}}
