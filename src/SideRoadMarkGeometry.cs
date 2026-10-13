using System.Collections.Generic;
using Unity.Mathematics;
namespace RoadStyleIntoKorea {
 internal static class SideRoadMarkGeometry {
  internal static void Build(SideRoadOpening opening,List<float3> path,float u0,float u1,out List<float3> vertices,out List<int> indices,out List<float2> uv){
   vertices=new List<float3>();indices=new List<int>();uv=new List<float2>();float distance=0;
   for(int i=1;i<path.Count;i++){
    float length=math.distance(path[i-1].xz,path[i].xz);if(length<.0001f)continue;
    var leftA=Geometry.Left(path[i]-path[math.max(0,i-2)]);
    var leftB=Geometry.Left(path[math.min(path.Count-1,i+1)]-path[i-1]);
    float from=0;
    while(from<length){
     float cycle=math.frac(distance+from),to=math.min(length,from+math.max(.0001f,(cycle<.5f?.5f:1f)-cycle));
     if(cycle<.5f&&to-from>.0001f){
      int k=vertices.Count;
      foreach(float at in new[]{from,to}){
       float t=at/length;var center=math.lerp(path[i-1],path[i],t);var left=math.normalizesafe(math.lerp(leftA,leftB,t),leftA);
       foreach(int side in new[]{-1,1}){
        var p=center+left*(side*.08f);p.y=opening.SurfaceHeight(p)+.025f;
        vertices.Add(p);uv.Add(new float2(side<0?u0:u1,(distance+at)/6f));
       }
      }
      if(math.cross(vertices[k+1]-vertices[k],vertices[k+2]-vertices[k]).y>0)indices.AddRange(new[]{k,k+1,k+2,k+1,k+3,k+2});
      else indices.AddRange(new[]{k,k+2,k+1,k+1,k+2,k+3});
     }
     from=to;
    }
    distance+=length;
   }
  }
 }
}
