using System.Collections.Generic;
using Unity.Mathematics;
namespace RoadStyleIntoKorea {internal static class ManualStopPolicy {
 internal static bool Overlap(Colossal.Mathematics.Bezier4x3 curve,float3 stop,float3 forward,float width,out float2 interval){
  interval=default;
  // Only parallel bars on the same road surface, within two metres longitudinally.
  for(int i=0;i<=4;i++){
   float t=i*.25f;var delta=Geometry.Position(curve,t)-stop;
   if(math.abs(delta.y)>.6f||math.abs(math.dot(delta,forward))>2||math.abs(math.dot(Geometry.Tangent(curve,t),forward))>.2f)return false;
  }
  var left=Geometry.Left(forward);float a=math.dot(curve.a-stop,left),b=math.dot(curve.d-stop,left);
  if(math.abs(b-a)<.01f)return false;
  float lo=(-width*.5f-a)/(b-a),hi=(width*.5f-a)/(b-a);
  interval=new float2(math.max(0,math.min(lo,hi)),math.min(1,math.max(lo,hi)));
  return interval.y-interval.x>.001f;
 }
 // Parameters belong to the source curve, so reversing its storage order is harmless.
 internal static List<float2> Ranges(Geometry.Arc arc,float3 stop,float3 forward){
  var cuts=new List<float>{0,1};forward=math.normalizesafe(new float3(forward.x,0,forward.z));
  foreach(float plane in new[]{-.28f,-4.28f}){
   float last=math.dot(arc.Curve.a-stop,forward)-plane;
   for(int i=1;i<=64;i++){float t=i/64f,v=math.dot(Geometry.Position(arc.Curve,t)-stop,forward)-plane;
    if((last<0&&v>=0)||(last>0&&v<=0)){float lo=(i-1)/64f,hi=t;bool negative=last<0;
     for(int j=0;j<22;j++){float mid=(lo+hi)*.5f;if((math.dot(Geometry.Position(arc.Curve,mid)-stop,forward)-plane<0)==negative)lo=mid;else hi=mid;}cuts.Add((lo+hi)*.5f);
    }last=v;
   }
  }
  cuts.Sort();var result=new List<float2>();
  for(int i=1;i<cuts.Count;i++){float lo=cuts[i-1],hi=cuts[i],mid=(lo+hi)*.5f;float d=math.dot(Geometry.Position(arc.Curve,mid)-stop,forward);
   if(hi-lo<.000001f||d< -4.28001f||d>-.27999f)continue;
   if(arc.Distance(hi)-arc.Distance(lo)>4){if(math.dot(Geometry.Tangent(arc.Curve,mid),forward)>0)lo=arc.T(arc.Distance(hi)-4);else hi=arc.T(arc.Distance(lo)+4);}
   result.Add(new float2(lo,hi));
  }return Merge(result);
 }
 internal static List<float2> Merge(List<float2> ranges){
  ranges.Sort((a,b)=>a.x.CompareTo(b.x));var result=new List<float2>();
  foreach(var r in ranges){if(result.Count==0||r.x>result[result.Count-1].y+.000001f)result.Add(r);else {var last=result[result.Count-1];last.y=math.max(last.y,r.y);result[result.Count-1]=last;}}
  return result;
 }
}}
