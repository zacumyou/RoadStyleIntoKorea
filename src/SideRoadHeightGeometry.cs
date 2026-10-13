using System.Collections.Generic;
using Unity.Mathematics;
namespace RoadStyleIntoKorea {
 internal sealed class SideRoadHeightGeometry {
  struct Triangle {
   internal float3 A;
   internal float2 Min,Max,V,W,Height;
   internal float Det;
  }
  readonly Triangle[] triangles;
  internal SideRoadHeightGeometry(List<float3> points){
   triangles=new Triangle[points.Count/3];
   for(int i=0;i<triangles.Length;i++){
    var a=points[i*3];var b=points[i*3+1];var c=points[i*3+2];
    var v=b.xz-a.xz;var w=c.xz-a.xz;
    triangles[i]=new Triangle{A=a,Min=math.min(a.xz,math.min(b.xz,c.xz))-.0001f,
     Max=math.max(a.xz,math.max(b.xz,c.xz))+.0001f,V=v,W=w,
     Height=new float2(b.y-a.y,c.y-a.y),Det=v.x*w.y-v.y*w.x};
   }
  }
  internal static float2 CurbDepths(SideRoadOpening opening,int end,System.Func<float3,float> stoneHeight){
   float length=opening.Outer-opening.Inner,step=length/128f;
   System.Func<float,bool> inside=depth=>{
    var p=opening.CutBoundaryPlan(end,depth);
    var a=opening.CutBoundaryPlan(end,math.max(0,depth-.01f));
    var b=opening.CutBoundaryPlan(end,math.min(length,depth+.01f));
    var outward=Geometry.Left(b-a);if(math.dot(outward,opening.RawPoint(0,depth)-p)>0)outward=-outward;
    return math.isfinite(stoneHeight(p+outward*.005f));
   };
   float first=-1,last=-1;
   for(int i=0;i<=128;i++){
    float d=i*step;
    if(inside(d)){if(first<0)first=d;last=d;}
    else if(first>=0)break;
   }
   if(first<0)return new float2(opening.CurbJoinDepth-.2f,opening.CurbJoinDepth);
   float lo=math.max(0,first-step),hi=first;
   for(int i=0;i<10;i++){float mid=(lo+hi)*.5f;if(inside(mid))hi=mid;else lo=mid;}
   float front=(lo+hi)*.5f;lo=last;hi=math.min(length,last+step);
   for(int i=0;i<10;i++){float mid=(lo+hi)*.5f;if(inside(mid))lo=mid;else hi=mid;}
   return new float2(front,(lo+hi)*.5f);
  }
  internal float Sample(float3 p){
   float height=float.NegativeInfinity;
   for(int i=0;i<triangles.Length;i++){
    var triangle=triangles[i];
    if(math.any(p.xz<triangle.Min)||math.any(p.xz>triangle.Max))continue;
    var v=triangle.V;var w=triangle.W;var q=p.xz-triangle.A.xz;
    float det=triangle.Det;if(math.abs(det)<1e-8f)continue;
    float u=(q.x*w.y-q.y*w.x)/det,t=(v.x*q.y-v.y*q.x)/det;
    if(u<-.0001f||t<-.0001f||u+t>1.0001f)continue;
    height=math.max(height,triangle.A.y+u*triangle.Height.x+t*triangle.Height.y);
   }
   return math.isfinite(height)?height:float.NaN;
  }
 }
}
