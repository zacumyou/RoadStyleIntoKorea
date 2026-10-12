using System.Collections.Generic;using Unity.Mathematics;
namespace RoadStyleIntoKorea { internal static class CornerUVPolicy {
internal static bool Correct(List<float3> points,List<int> faces,List<float2> uv,float3 along,float3 across){
bool distorted=false;
for(int i=0;i+2<faces.Count;i+=3)for(int j=0;j<3;j++){int a=faces[i+j],b=faces[i+(j+1)%3];float world=math.distance(points[a],points[b]);if(world<.02f)continue;float ratio=8*math.distance(uv[a],uv[b])/world;if(ratio<.67f||ratio>1.5f)distorted=true;}
if(!distorted||points.Count==0)return false;
// A rigid local projection has no collapsing cross-section scale. Its direction
// belongs to this corner, not the global world axes. Keep the first vertex phase.
along=math.normalizesafe(along,new float3(0,0,1));across=math.normalizesafe(across-along*math.dot(across,along),Geometry.Left(along));
var origin=points[0];var phase=uv[0];for(int i=0;i<points.Count;i++){var d=points[i]-origin;uv[i]=phase+new float2(math.dot(d,across),math.dot(d,along))/8;}
return true;
}}}
