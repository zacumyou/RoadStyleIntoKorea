using Unity.Mathematics;
namespace RoadStyleIntoKorea {
 internal static class NodeSurfaceHoverPolicy {
  internal static bool Nearer(Geometry.Arc arc,float3 hit,float best,out float3 point,out float distance){
   point=Geometry.Position(arc.Curve,arc.Nearest(hit));distance=math.distancesq(point.xz,hit.xz);
   return math.abs(point.y-hit.y)<=1.5f&&distance<best;
 }
}
}
