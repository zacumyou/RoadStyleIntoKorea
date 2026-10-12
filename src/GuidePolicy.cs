using Unity.Mathematics;
namespace RoadStyleIntoKorea {internal static class GuidePolicy {
 internal static bool Point(float3 a,float3 b)=>math.distancesq(a,b)<.16f;
 internal static bool Pair(float3 a,float3 b,float3 start,float3 end)=>Point(a,start)&&Point(b,end);
}}
