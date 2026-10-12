using System;
using Colossal.Mathematics;
using Unity.Mathematics;
namespace RoadStyleIntoKorea {
 internal static class PedestrianHeightPolicy {
  internal static bool Fit(Bezier4x3 source,Func<float3,float?> height,out Bezier4x3 target){
   target=source;
   float first=(height(source.a)??source.a.y)-source.a.y,last=(height(source.d)??source.d.y)-source.d.y;
   // Outside the paved footprint, ease back toward the original path. This
   // also joins a raised sidewalk to a road crossing or building entrance.
   float At(float t){var p=Geometry.Position(source,t);return height(p)??(p.y+first*(1-t)*(1-t)*(1-t)+last*t*t*t);}
   float a=At(0),d=At(1),u=At(1f/3)-a,v=At(2f/3)-a,w=d-a;
   target.a.y=a;target.b.y=a+(18*u-9*v+2*w)/6;target.c.y=a+(-9*u+18*v-5*w)/6;target.d.y=d;
   for(int i=0;i<=32;i++){
    float t=i/32f;var p=Geometry.Position(target,t);
    if(!math.all(math.isfinite(p))||math.abs(p.y-At(t))>.025f||math.abs(p.y-Geometry.Position(source,t).y)>.6f)return false;
   }
   return true;
  }
 }
}
