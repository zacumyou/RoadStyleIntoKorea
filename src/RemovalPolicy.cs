using Unity.Mathematics;
namespace RoadStyleIntoKorea {
 internal static class RemovalPolicy {
  internal static bool Match(bool road,float3 a,float3 b)=>road?math.abs(a.x-b.x)<.005f&&math.abs(a.y-b.y)<.25f&&math.abs(a.z-b.z)<.5f:math.distancesq(a,b)<.25f;
 }
}
