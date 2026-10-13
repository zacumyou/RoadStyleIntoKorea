using System.Collections.Generic;using Colossal.Mathematics;using Unity.Mathematics;
namespace RoadStyleIntoKorea { internal static class TurnBoundaryPolicy {
// Offset derivative preserves tangent and curvature across section boundaries.
static void Sample(Bezier4x3 c,float t,float offset,out float3 point,out float3 derivative){
float u=1-t;var d=3*(u*u*(c.b-c.a)+2*u*t*(c.c-c.b)+t*t*(c.d-c.c));
var dd=6*(u*(c.c-2*c.b+c.a)+t*(c.d-2*c.c+c.b));float length=math.length(d.xz);
point=Geometry.Position(c,t);derivative=d;if(length<.0001f)return;
var normal=new float3(-d.z,0,d.x)/length;var dn=new float3(-dd.z,0,dd.x)/length-normal*(math.dot(d.xz,dd.xz)/(length*length));
point+=normal*offset;derivative+=dn*offset;
}
static float3 Constrained(Bezier4x3 c,float t,float distance,System.Func<float3,bool> inside){
Sample(c,t,distance,out var p,out var derivative);if(inside==null||inside(p))return p;
var origin=Geometry.Position(c,t);if(!inside(origin))return p;
float lo=0,hi=1;for(int j=0;j<14;j++){float m=(lo+hi)*.5f;if(inside(math.lerp(origin,p,m)))lo=m;else hi=m;}
return math.lerp(origin,p,math.max(0,lo-.015f));
}
internal static List<Bezier4x3> Offset(Bezier4x3 curve,float distance,System.Func<float3,bool> inside=null){
var result=new List<Bezier4x3>();for(int i=0;i<8;i++){
Sample(curve,i/8f,distance,out var a,out var da);Sample(curve,(i+1)/8f,distance,out var d,out var dd);
if(inside!=null){
float t=i/8f,u=(i+1)/8f;
a=Constrained(curve,t,distance,inside);d=Constrained(curve,u,distance,inside);
float ta=math.max(0,t-.001f),tb=math.min(1,t+.001f),ua=math.max(0,u-.001f),ub=math.min(1,u+.001f);
da=(Constrained(curve,tb,distance,inside)-Constrained(curve,ta,distance,inside))/(tb-ta);
dd=(Constrained(curve,ub,distance,inside)-Constrained(curve,ua,distance,inside))/(ub-ua);
}
result.Add(new Bezier4x3(a,a+da/24,d-dd/24,d));}return result;}
}}
