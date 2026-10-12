using System.Collections.Generic;
using Unity.Mathematics;
namespace RoadStyleIntoKorea {
 internal static class RedPavingPolicy {
  internal const int Solid=24, Striped=25, StyleCount=26;
  internal static bool IsRed(int style)=>style==Solid||style==Striped;
  internal static bool WithinCaps(float3 hit,float3 point,float3 tangent,float t)=>
   !(t<.001f&&math.dot(hit-point,tangent)<-.05f)&&!(t>.999f&&math.dot(hit-point,tangent)>.05f);
  // Intersect the transverse plane, rather than measuring a remote lane endpoint.
  internal static bool CrossSection(Colossal.Mathematics.Bezier4x3 curve,float3 point,float3 forward,out float t){
   forward=math.normalizesafe(new float3(forward.x,0,forward.z));t=0;
   float previous=math.dot(curve.a-point,forward);
   if(math.abs(previous)<.001f)return true;
   for(int i=1;i<=32;i++){
    float end=i/32f,value=math.dot(Geometry.Position(curve,end)-point,forward);
    if(math.abs(value)<.001f){t=end;return true;}
    if(previous*value<0){float a=(i-1)/32f,b=end;for(int j=0;j<16;j++){float m=(a+b)*.5f,v=math.dot(Geometry.Position(curve,m)-point,forward);if(previous*v>0)a=m;else b=m;}t=(a+b)*.5f;return true;}
    previous=value;
   }return false;
  }
  internal static bool EmptyBridge(float edge,float centre,List<float2> strips){
   float lo=math.min(edge,centre),hi=math.max(edge,centre);
   if(hi-lo<.005f||hi-lo>1f)return false;
   strips.Sort((a,b)=>a.x.CompareTo(b.x));float covered=lo;
   foreach(var strip in strips){if(strip.y<covered)continue;if(strip.x>covered+.01f)return false;covered=math.max(covered,strip.y);if(covered>=hi-.005f)return true;}
   return false;
  }
  // Fixed 3m paint / 6m gap, centered over the complete selected route.
  internal static float Phase(float length){if(length<=3)return 0;int count=math.max(1,(int)math.floor((length+6)/9));return -(length-(count*3+(count-1)*6))*.5f;}
  internal static IEnumerable<float2> Spans(float length,bool striped,float phase){
   if(!math.isfinite(length)||length<=.01f||!math.isfinite(phase))yield break;
   if(!striped){yield return new float2(0,length);yield break;}
   int first=(int)math.floor(phase/9),last=(int)math.floor((phase+length)/9);
   for(int i=first;i<=last;i++){float a=math.max(0,i*9-phase),b=math.min(length,i*9+3-phase);if(b-a>.01f)yield return new float2(a,b);}
  }
 }
}
