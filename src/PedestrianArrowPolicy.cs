using Colossal.Mathematics;
using Unity.Mathematics;
namespace RoadStyleIntoKorea {
internal static class PedestrianArrowPolicy {
 internal const string Double="CSKRCrosswalkPedArrowDoubleDecal", Single="CSKRCrosswalkPedArrowSingleDecal";
 // Exported projection footprints are 2.24/0.8m wide; painted arrows are 0.6m long.
 internal static string Asset(float rowWidth)=>rowWidth>=2f?Double:Single;
 internal static bool Entry(Bezier4x3 crossing,float rowWidth,bool end,out float3 position,out quaternion rotation,float curbMargin=0){
  position=default;rotation=quaternion.identity;
  if(!math.isfinite(rowWidth)||rowWidth<1f)return false;
  float3 forward=Geometry.Tangent(crossing,end?1:0)*(end?-1:1);
  float3 right=-Geometry.Left(forward);
  // Preserve only 20% of the former curb-to-tail clearance (margin + 0.1m).
  float oldCenter=.4f-.8f*(math.max(0,curbMargin)+.1f);
  float oldGap=1f-(oldCenter+.3f);
  position=(end?crossing.d:crossing.a)+forward*(oldCenter+.6f*oldGap)+right*(rowWidth*.5f+.1f);
  rotation=quaternion.LookRotationSafe(forward,new float3(0,1,0));
  return math.all(math.isfinite(position));
 }
}}
