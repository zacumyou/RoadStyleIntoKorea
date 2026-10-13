using System.Collections.Generic;
using Colossal.Mathematics;
using Unity.Mathematics;
namespace RoadStyleIntoKorea {
internal static class CrosswalkStripePolicy {
 internal static float2 ExpandedBounds(float lo,float hi,float nativeStart,float nativeEnd)
  =>new float2(math.min(lo,math.min(nativeStart,nativeEnd)),math.max(hi,math.max(nativeStart,nativeEnd)));
 // Measured source recipe: 0.5m paint + 0.5m gap, rows staggered by 0.5m.
 internal const float Pitch=1f,Bar=.5f,EndMargin=.3f;
 // Keep the 1m UV period intact; trim only the 1cm filtering/float seam at exposed boundaries.
 internal static void GuardEnds(Bezier4x3 curve,List<Bounds1> cuts){
  var arc=new Geometry.Arc(curve);const float guard=.01f;
  for(int i=0;i<cuts.Count;i++){var c=cuts[i];cuts[i]=new Bounds1(arc.T(math.max(0,arc.Distance(c.min)-guard)),arc.T(math.min(arc.Length,arc.Distance(c.max)+guard)));}
  cuts.Add(new Bounds1(0,arc.T(guard)));cuts.Add(new Bounds1(arc.T(arc.Length-guard),1));
 }
 // Materialize endpoint exclusions in geometry; renderer CutRange support is not required.
 internal static Bezier4x3 TrimEntries(Bezier4x3 curve,List<Bounds1> cuts){
  var arc=new Geometry.Arc(curve);float a=0,b=arc.Length;
  foreach(var c in cuts){if(c.min<=0)a=math.max(a,arc.Distance(c.max));if(c.max>=1)b=math.min(b,arc.Distance(c.min));}
  if(a<=0&&b>=arc.Length)return curve;
  if(b-a<Pitch)return curve;
  var result=arc.Slice(a,b);var fitted=new Geometry.Arc(result);var mapped=new List<Bounds1>();
  foreach(var c in cuts){float lo=math.max(a,arc.Distance(c.min)),hi=math.min(b,arc.Distance(c.max));if(hi-lo>.001f)mapped.Add(new Bounds1(fitted.T(lo-a),fitted.T(hi-a)));}
  cuts.Clear();cuts.AddRange(mapped);return result;
 }
 internal static bool Fit(Bezier4x3 source,List<Bounds1> cuts,out Bezier4x3 result,out List<Bounds1> mapped,bool fullWidth=false){
  result=source;mapped=new List<Bounds1>();var arc=new Geometry.Arc(source);float a=0,b=arc.Length;
  foreach(var cut in cuts){if(cut.min<=0)a=math.max(a,arc.Distance(cut.max));if(cut.max>=1)b=math.min(b,arc.Distance(cut.min));}
  if(!fullWidth){a+=EndMargin;b-=EndMargin;}float length=fullWidth?b-a:math.floor((b-a)/Pitch)*Pitch;if(length<Pitch)return false;
  a+=(b-a-length)*.5f;b=a+length;result=arc.Slice(a,b);var fitted=new Geometry.Arc(result);
  foreach(var cut in cuts){
   float lo=math.max(0,arc.Distance(cut.min)-a),hi=math.min(length,arc.Distance(cut.max)-a);if(hi<=lo)continue;
   // Expand internal exclusions to complete staggered-row bar boundaries.
   lo=math.floor(lo/Bar)*Bar;hi=math.ceil(hi/Bar)*Bar;
   mapped.Add(new Bounds1(fitted.T(lo),fitted.T(hi)));
  }
  return true;
 }
}}
