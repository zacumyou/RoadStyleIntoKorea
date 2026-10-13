using System.Collections.Generic;
using Colossal.Mathematics;
using Unity.Mathematics;
namespace RoadStyleIntoKorea {internal sealed class CurbRampPolicy {
 internal const float Low=.01f,Run=1.5f,High=.2f;
 internal struct Zone {internal float3 Mid,Across,Normal;internal float Half,Reach;}
 readonly List<Zone> zones=new List<Zone>();
 struct Bridge {internal Zone A,B;internal float Sign,End;}
 readonly List<Bridge> bridges=new List<Bridge>();bool bridgesReady;
 void PrepareBridges(){
  if(bridgesReady)return;bridgesReady=true;bridges.Clear();
  for(int i=0;i<zones.Count;i++)for(int j=i+1;j<zones.Count;j++){
   var a=zones[i];var b=zones[j];if(math.abs(math.dot(a.Normal,b.Normal))<.95f)continue;
   float separation=math.dot(b.Mid-a.Mid,a.Normal),gap=math.abs(separation)-a.Half-b.Half;
   if(gap>=0&&gap<=3)bridges.Add(new Bridge{A=a,B=b,Sign=separation<0?-1:1,End=math.abs(separation)-b.Half});
  }
 }

 internal int Count=>zones.Count;
 internal void Add(Bezier4x3 crossing,float width){
  var across=math.normalizesafe(new float3(crossing.d.x-crossing.a.x,0,crossing.d.z-crossing.a.z));
  if(math.lengthsq(across)<.5f)return;
  bridgesReady=false;zones.Add(new Zone{Mid=Geometry.Position(crossing,.5f),Across=across,Normal=Geometry.Left(across),Half=width*.5f+.2f,Reach=math.distance(crossing.a.xz,crossing.d.xz)*.5f+8});
 }
 internal void AddEntrance(Bezier4x3 curve,float width){
  var across=math.normalizesafe(new float3(curve.d.x-curve.a.x,0,curve.d.z-curve.a.z));
  if(math.lengthsq(across)<.5f)return;
  bridgesReady=false;zones.Add(new Zone{Mid=(curve.a+curve.d)*.5f,Across=across,Normal=Geometry.Left(across),Half=width*.5f+.2f,Reach=math.distance(curve.a.xz,curve.d.xz)*.5f+.6f});
 }
 static bool Reaches(Zone z,float3 p)=>math.abs(p.y-z.Mid.y)<=2&&math.abs(math.dot(p-z.Mid,z.Across))<=z.Reach;
 internal float Height(float3 p){
  float height=High;
  foreach(var z in zones){if(math.abs(p.y-z.Mid.y)>2||math.abs(math.dot(p-z.Mid,z.Across))>z.Reach)continue;
   float distance=math.abs(math.dot(p-z.Mid,z.Normal))-z.Half;
   height=math.min(height,math.lerp(Low,High,math.saturate(distance/Run)));
  }
  // Avoid a short raised island between two nearby, similarly oriented openings.
  PrepareBridges();
  foreach(var bridge in bridges){
   if(!Reaches(bridge.A,p)||!Reaches(bridge.B,p))continue;
   float at=math.dot(p-bridge.A.Mid,bridge.A.Normal)*bridge.Sign;
   if(at>=bridge.A.Half&&at<=bridge.End)height=Low;
  }
  return height;
 }
 IEnumerable<float> Breaks(Zone z){yield return -z.Half-Run;yield return -z.Half;yield return z.Half;yield return z.Half+Run;}
 internal List<float3> SplitPath(List<float3> path){
  if(zones.Count==0)return path;var result=new List<float3>();
  for(int i=0;i+1<path.Count;i++){
   var a=path[i];var b=path[i+1];var ts=new List<float>{0};
   foreach(var z in zones){float da=math.dot(a-z.Mid,z.Normal),db=math.dot(b-z.Mid,z.Normal);if(math.abs(db-da)<.00001f)continue;
    foreach(float at in Breaks(z)){float t=(at-da)/(db-da);if(t>.00001f&&t<.99999f)ts.Add(t);}
   }ts.Sort();float last=-1;foreach(float t in ts)if(t-last>.00001f){result.Add(math.lerp(a,b,t));last=t;}
  }if(path.Count>0)result.Add(path[path.Count-1]);return result;
 }
 struct Vertex {internal float3 P;internal float2 UV;}
 // Split only triangles crossing ramp break planes. No dense uniform tessellation.
 internal void Paving(ref List<float3> positions,ref List<int> indices,ref List<float2> uv){
  if(zones.Count==0)return;
  var output=new List<float3>();var tex=new List<float2>();var tris=new List<int>();
  for(int i=0;i+2<indices.Count;i+=3){
   var poly=new List<Vertex>();for(int j=0;j<3;j++){int k=indices[i+j];poly.Add(new Vertex{P=positions[k],UV=uv[k]});}
   var pieces=new List<List<Vertex>>{poly};
   foreach(var z in zones){
    float lo=float.MaxValue,hi=float.MinValue,sideLo=float.MaxValue,sideHi=float.MinValue,yLo=float.MaxValue,yHi=float.MinValue;
    foreach(var v in poly){var delta=v.P-z.Mid;float d=math.dot(delta,z.Normal),side=math.dot(delta,z.Across);lo=math.min(lo,d);hi=math.max(hi,d);sideLo=math.min(sideLo,side);sideHi=math.max(sideHi,side);yLo=math.min(yLo,delta.y);yHi=math.max(yHi,delta.y);}
    if(lo>=z.Half+Run||hi<=-z.Half-Run||sideLo>z.Reach||sideHi< -z.Reach||yLo>2||yHi< -2)continue;
    foreach(float at in Breaks(z)){
     var split=new List<List<Vertex>>();foreach(var part in pieces){var below=new List<Vertex>();var above=new List<Vertex>();
      for(int j=0;j<part.Count;j++){var a=part[j];var b=part[(j+1)%part.Count];float da=math.dot(a.P-z.Mid,z.Normal)-at,db=math.dot(b.P-z.Mid,z.Normal)-at;
       if(da<=.000001f)below.Add(a);if(da>=-.000001f)above.Add(a);
       if(da < -.000001f&&db > .000001f||da > .000001f&&db < -.000001f){float t=da/(da-db);var v=new Vertex{P=math.lerp(a.P,b.P,t),UV=math.lerp(a.UV,b.UV,t)};below.Add(v);above.Add(v);}
      }
      bool allOn=true;foreach(var v in part)allOn &= math.abs(math.dot(v.P-z.Mid,z.Normal)-at)<=.000001f;
      if(allOn)split.Add(part);else{if(below.Count>=3)split.Add(below);if(above.Count>=3)split.Add(above);}
     }pieces=split;
    }
   }
   foreach(var part in pieces){int first=output.Count;foreach(var v in part){var p=v.P;p.y-=High-Height(p);output.Add(p);tex.Add(v.UV);}
    for(int j=1;j+1<part.Count;j++){tris.Add(first);tris.Add(first+j);tris.Add(first+j+1);}
   }
  }positions=output;indices=tris;uv=tex;
 }
}}
