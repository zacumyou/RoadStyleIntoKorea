using System.Collections.Generic;
using Colossal.Mathematics;
using Unity.Mathematics;
namespace RoadStyleIntoKorea {
 internal static class PavingUVPolicy {
  internal static void Subdivide(ref List<float3> points,ref List<int> faces,ref List<float2> uv){
   var result=new List<float3>();var indices=new List<int>();var mapped=new List<float2>();
   for(int i=0;i+2<faces.Count;i+=3)Split(points[faces[i]],points[faces[i+1]],points[faces[i+2]],uv[faces[i]],uv[faces[i+1]],uv[faces[i+2]],0,result,indices,mapped);
   // UV below is recomputed from world position. Shared triangle corners need
   // only one nearest-curve projection and one rendered vertex, not one per face.
   var unique=new Dictionary<float3,int>();var compact=new List<float3>();var compactUV=new List<float2>();
   for(int i=0;i<indices.Count;i++){int old=indices[i];var point=result[old];if(!unique.TryGetValue(point,out int index)){index=compact.Count;unique.Add(point,index);compact.Add(point);compactUV.Add(mapped[old]);}indices[i]=index;}
   points=compact;faces=indices;uv=compactUV;
  }
  static void Split(float3 a,float3 b,float3 c,float2 ua,float2 ub,float2 uc,int depth,List<float3> points,List<int> faces,List<float2> uv){
   float ab=math.distancesq(a,b),bc=math.distancesq(b,c),ca=math.distancesq(c,a);
   if(depth<8&&math.max(ab,math.max(bc,ca))>16){
    if(bc>ab&&bc>=ca){Split(b,c,a,ub,uc,ua,depth,points,faces,uv);return;}
    if(ca>ab&&ca>bc){Split(c,a,b,uc,ua,ub,depth,points,faces,uv);return;}
    var m=(a+b)*.5f;var um=(ua+ub)*.5f;
    Split(a,m,c,ua,um,uc,depth+1,points,faces,uv);Split(m,b,c,um,ub,uc,depth+1,points,faces,uv);return;
   }
   int first=points.Count;points.Add(a);points.Add(b);points.Add(c);uv.Add(ua);uv.Add(ub);uv.Add(uc);faces.Add(first);faces.Add(first+1);faces.Add(first+2);
  }
  internal static void MapCorner(List<float3> points,List<float2> uv,Bezier4x3 path,float width,float phase){
   var arc=new Geometry.Arc(path);
   for(int i=0;i<points.Count;i++){float t=arc.Nearest(points[i]);var delta=points[i]-Geometry.Position(path,t);var forward=Geometry.Tangent(path,t);
    uv[i]=new float2(width*.5f-math.dot(delta,Geometry.Left(forward)),phase+arc.Distance(t)+math.dot(delta,forward))/8;
   }
  }
  // Clipping an already bent triangle must preserve its height. Its UV, however,
  // must be measured again: interpolating source UV across that diagonal shears
  // the brick pattern at every clipped triangle on a curved road.
  internal static void Map(List<float3> points,List<float2> uv,Bezier4x3 first,Bezier4x3 second,float width) {
   var a=new Geometry.Arc(first);var b=new Geometry.Arc(second);
   for(int i=0;i<points.Count;i++) {
    var p=points[i];float ta=a.Nearest(p),tb=b.Nearest(p);
    var pa=Geometry.Position(first,ta);var pb=Geometry.Position(second,tb);
    bool start=math.distancesq(p.xz,pa.xz)<=math.distancesq(p.xz,pb.xz);
    var arc=start?a:b;float t=start?ta:tb;var origin=start?pa:pb;
    var forward=Geometry.Tangent(arc.Curve,t);var delta=p-origin;
    uv[i]=new float2(width*.5f-math.dot(delta,Geometry.Left(forward)),
     (start?0:a.Length)+arc.Distance(t)+math.dot(delta,forward))/8;
   }
  }
 }
}
