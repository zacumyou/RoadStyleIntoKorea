using Colossal.Mathematics;
using Unity.Mathematics;
using System.Collections.Generic;
namespace RoadStyleIntoKorea {
 internal static class GutterPolicy {
  internal const float Width=.55f, PavingGap=.01f;
  internal static float CurbInset(int lanes,float pavingBorder,float raisedFront,bool right)
   =>lanes>0&&lanes<=2?math.max(0,right?pavingBorder-raisedFront:raisedFront-pavingBorder):0;
  internal static Bezier4x3 Curve(Bezier4x3 a,Bezier4x3 b,bool fromA,float curbBand=0)
   =>Geometry.Offset(CurbLaneGeometry.Curve(a,b,fromA,0),-(math.max(0,curbBand)+PavingGap+Width*.5f));
  internal static bool Hit(Geometry.Arc arc,float3 hit,out float distance){
   var point=arc.At(arc.Distance(arc.Nearest(hit)));distance=math.distance(point.xz,hit.xz);
   return distance<=Width*.5f+.03f&&math.abs(point.y-hit.y)<=.75f;
  }
  internal static float3 SyncedPoint(Bezier4x3 left,Bezier4x3 right,float border,float inside,float height,NodeSurfaceSync sync,float t,float curbBand=0){
   float3 At(float u)=>math.lerp(Geometry.Position(left,u),Geometry.Position(right,u),sync.Map(border,u,left,right));
   var p=At(t);var tangent=math.normalizesafe(At(math.min(1,t+.002f))-At(math.max(0,t-.002f)),Geometry.Tangent(left,t));
   // Determine the side away from the collapsed endpoint of a dead-end cap.
   var inward=math.lerp(Geometry.Position(left,.5f),Geometry.Position(right,.5f),sync.Map(inside,.5f,left,right))-At(.5f);
   float sign=math.dot(inward,Geometry.Left(math.normalizesafe(At(.502f)-At(.498f))))>=0?1:-1;
   return p-Geometry.Left(tangent)*(sign*(math.max(0,curbBand)+PavingGap+Width*.5f))+new float3(0,height,0);
  }
  internal static List<Bezier4x3> SyncedCurves(Bezier4x3 left,Bezier4x3 right,float min,float max,bool fromA,float height,NodeSurfaceSync sync,float curbBand=0){
   float border=fromA?min:max,inside=fromA?max:min;
   float3 At(float t)=>SyncedPoint(left,right,border,inside,height,sync,t,curbBand);
   var result=new List<Bezier4x3>();
   void Fit(float lo,float hi,int depth){
    var a=At(lo);var d=At(hi);var u=At(math.lerp(lo,hi,1f/3))-a;var v=At(math.lerp(lo,hi,2f/3))-a;var w=d-a;
    var c=new Bezier4x3(a,a+(18*u-9*v+2*w)/6,a+(-9*u+18*v-5*w)/6,d);
    float error=0;foreach(float t in new[]{1f/6,.5f,5f/6})error=math.max(error,math.distance(Geometry.Position(c,t),At(math.lerp(lo,hi,t))));
    if(error>.003f&&depth<5){float mid=(lo+hi)*.5f;Fit(lo,mid,depth+1);Fit(mid,hi,depth+1);}else result.Add(c);
   }
   Fit(0,1,0);
   var mid=math.lerp(Geometry.Position(left,.5f),Geometry.Position(right,.5f),sync.Map(inside,.5f,left,right));
   if(math.dot(mid-At(.5f),Geometry.Left(math.normalizesafe(At(.51f)-At(.49f))))<0){result.Reverse();for(int i=0;i<result.Count;i++)result[i]=Geometry.Reverse(result[i]);}
   return result;
  }
 }
}
