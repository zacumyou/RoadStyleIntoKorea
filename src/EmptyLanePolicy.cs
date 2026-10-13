using System;
using System.Collections.Generic;
using Unity.Mathematics;
namespace RoadStyleIntoKorea {
 internal static class EmptyLanePolicy {
  internal static bool Empty(string name)=>!string.IsNullOrEmpty(name)&&
   (name.StartsWith("RB Empty Piece ",StringComparison.OrdinalIgnoreCase)||name.StartsWith("RB Tiled Empty Piece ",StringComparison.OrdinalIgnoreCase));
  internal static float Position(float3 point,float width,Colossal.Mathematics.Bezier4x3 left,Colossal.Mathematics.Bezier4x3 right,out float distance){
   var middle=new Colossal.Mathematics.Bezier4x3((left.a+right.a)*.5f,(left.b+right.b)*.5f,(left.c+right.c)*.5f,(left.d+right.d)*.5f);
   float t=new Geometry.Arc(middle).Nearest(point);var a=Geometry.Position(left,t);var b=Geometry.Position(right,t);var span=b.xz-a.xz;
   float ratio=math.dot(point.xz-a.xz,span)/math.max(.0001f,math.lengthsq(span));
   distance=math.distancesq(math.lerp(a,b,ratio).xz,point.xz);return (ratio-.5f)*width;
  }
  // Only contiguous, explicitly empty road surface. Never jump a median,
  // a driving lane, a sidewalk or a gap in the composition.
  internal static float Extend(float boundary,int direction,List<float2> empty) {
   float result=boundary;
   for(int pass=0;pass<empty.Count;pass++) {
    float next=result;
    foreach(var span in empty) {
     if(direction>0&&math.abs(span.x-result)<.025f)next=math.max(next,span.y);
     if(direction<0&&math.abs(span.y-result)<.025f)next=math.min(next,span.x);
    }
    if(math.abs(next-result)<.001f)break;result=next;
   }
   return result;
  }
 }
}
