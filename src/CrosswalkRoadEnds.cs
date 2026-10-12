using System.Collections.Generic;
using Colossal.Mathematics;
using Game.Net;
using Game.Prefabs;
using Unity.Entities;
using Unity.Mathematics;
namespace RoadStyleIntoKorea {
public partial class StyleSystem {
 // Only known road surfaces without either sidewalk qualify. Lane count is not a sidewalk test.
 bool FullRoadCrosswalk(Entity lane,ref Bezier4x3 curve,ref List<Bounds1> cuts){
  if(TransitionCrossing(lane))return false; // retain the native bend crossing span on either road type
  var road=CrossingRoad(lane,out bool start);if(road==Entity.Null)return false;
  if(HighwayCrosswalkSpan(road,ref curve,ref cuts))return true;
  var ribbons=EdgeRoadRibbons(road);if(ribbons.Count==0)return false;
  bool hasCurb=false;foreach(var ribbon in ribbons)hasCurb|=ribbon.HasLeftCurb||ribbon.HasRightCurb;
  // Crossings can lie inside the node, beyond the edge segment's end.
  if(EntityManager.HasComponent<Composition>(road)&&
     (start?EntityManager.HasComponent<StartNodeGeometry>(road):EntityManager.HasComponent<EndNodeGeometry>(road))){
   var cp=EntityManager.GetComponentData<Composition>(road);var composition=start?cp.m_StartNode:cp.m_EndNode;
   if(EntityManager.HasComponent<NetCompositionData>(composition)){
    var data=EntityManager.GetComponentData<NetCompositionData>(composition);
    var shape=start?EntityManager.GetComponentData<StartNodeGeometry>(road).m_Geometry:EntityManager.GetComponentData<EndNodeGeometry>(road).m_Geometry;
    AddRoadRibbon(ribbons,composition,shape.m_Left,-data.m_Width/2,data.m_Width/2+data.m_MiddleOffset);
    AddRoadRibbon(ribbons,composition,shape.m_Right,data.m_MiddleOffset,data.m_Width/2-data.m_MiddleOffset);
   }
  }
  var mid=Geometry.Position(curve,.5f);var axis=Geometry.Tangent(curve,.5f);var normal=Geometry.Left(axis);
  float lo=float.PositiveInfinity,hi=float.NegativeInfinity;
  foreach(var ribbon in ribbons)foreach(var boundary in new[]{ribbon.Left,ribbon.Right}){
   // Intersect the transverse paint axis with the deformed asphalt boundary.
   for(int i=0;i<32;i++){
    float a=i/32f,b=(i+1)/32f;
    float da=math.dot(Geometry.Position(boundary,a)-mid,normal),db=math.dot(Geometry.Position(boundary,b)-mid,normal);
    if(da*db>0||math.abs(da-db)<.0001f)continue;
    for(int j=0;j<16;j++){float t=(a+b)*.5f,d=math.dot(Geometry.Position(boundary,t)-mid,normal);if(da*d>0){a=t;da=d;}else b=t;}
    float along=math.dot((Geometry.Position(boundary,(a+b)*.5f)-mid).xz,axis.xz)/math.lengthsq(axis.xz);lo=math.min(lo,along);hi=math.max(hi,along);
   }
  }
  if(!math.isfinite(lo)||!math.isfinite(hi)||hi-lo<1)return false;
  // A node can expose only one half of its asphalt ribbons. Expansion must
  // never replace an already full native crossing with that partial interval.
  float originalA=math.dot(curve.a-mid,axis),originalD=math.dot(curve.d-mid,axis);
  var expanded=CrosswalkStripePolicy.ExpandedBounds(lo,hi,originalA,originalD);lo=expanded.x;hi=expanded.y;
  var mapped=new List<Bounds1>();
  foreach(var cut in cuts){
   // Native endpoint exclusions describe pedestrian lane ends, not curbs on an alley.
   if(cut.min<=0||cut.max>=1)continue;
   float a=math.saturate((math.dot(Geometry.Position(curve,cut.min)-mid,axis)-lo)/(hi-lo));
   float b=math.saturate((math.dot(Geometry.Position(curve,cut.max)-mid,axis)-lo)/(hi-lo));
   if(b>a)mapped.Add(new Bounds1(a,b));
  }
  curve=Geometry.Line(mid+axis*lo,mid+axis*hi);cuts=mapped;return !hasCurb;
 }
}}


