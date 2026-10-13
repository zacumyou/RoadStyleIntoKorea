using System.Collections.Generic;
using Colossal.Mathematics;
using Unity.Mathematics;
namespace RoadStyleIntoKorea {
 internal static class CrosswalkLanePolicy {
  internal struct Lane { internal Geometry.Route Route;internal float Width; }
  // Intersect the crossing plane with BOTH travel directions, excluding shoulders.
  internal static bool Span(Bezier4x3 crossing,List<Lane> lanes,out Bezier4x3 result){
   result=crossing;var mid=Geometry.Position(crossing,.5f);var across=Geometry.Tangent(crossing,.5f);across.y=0;across=math.normalizesafe(across);
   var normal=Geometry.Left(across);float lo=float.MaxValue,hi=float.MinValue,lowY=mid.y,highY=mid.y;int found=0;
   foreach(var lane in lanes){
    if(lane.Width<=0)continue;
    var route=lane.Route;route.Nearest(mid,out _,out var forward);var plane=math.dot(forward,normal)<0?-normal:normal;
    float3 p,tangent;
    if(route.IntersectPlane(mid,plane,out float at)){p=route.At(at);tangent=route.Direction(at);}
    else {
     // Native edge lanes can stop just before a node crossing. Extend only
     // that small missing approach section, never borrow a distant road.
     float a=math.abs(math.dot(route.At(0)-mid,plane)),b=math.abs(math.dot(route.At(route.Length)-mid,plane));
     float end=a<b?0:route.Length;p=route.At(end);tangent=route.Direction(end);
     float dot=math.dot(tangent,plane);if(math.abs(dot)<.5f)continue;
     float extension=math.dot(mid-p,plane)/dot;
     if(math.abs(extension)>8)continue;p+=tangent*extension;
    }
    if(math.abs(p.y-mid.y)>2)continue;
    float alignment=math.abs(math.dot(Geometry.Left(tangent),across));if(alignment<.5f)continue;
    float center=math.dot(p-mid,across),half=lane.Width*.5f/alignment;found++;
    if(center-half<lo){lo=center-half;lowY=p.y;}
    if(center+half>hi){hi=center+half;highY=p.y;}
   }
   if(found==0||hi-lo<1)return false;
   var start=mid+across*lo;var finish=mid+across*hi;start.y=lowY;finish.y=highY;
   result=Geometry.Line(start,finish);return true;
  }
 }
}
