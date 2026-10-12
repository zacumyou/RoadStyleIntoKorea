using System.Collections.Generic;
using Colossal.Mathematics;
using Game.Net;
using Unity.Entities;
using Unity.Mathematics;
namespace RoadStyleIntoKorea { public partial class StyleSystem {
 bool HasOppositeApproach(Entity road,bool start){
  var edge=EntityManager.GetComponentData<Edge>(road);var other=start?edge.m_End:edge.m_Start;
  // A cul-de-sac has no opposing junction to reserve half the segment for.
  if(!EntityManager.HasBuffer<ConnectedEdge>(other))return true;
  foreach(var item in EntityManager.GetBuffer<ConnectedEdge>(other,true))if(item.m_Edge!=road&&IsRoad(item.m_Edge))return true;
  return false;
 }
 bool HighwayCrosswalkSpan(Entity road,ref Bezier4x3 curve,ref List<Bounds1> cuts){
  if(!HighwayRoad(road))return false;
  var lanes=new List<CrosswalkLanePolicy.Lane>();
  foreach(var drive in Drives(Lanes(road)))lanes.Add(new CrosswalkLanePolicy.Lane{Route=drive.Arc,Width=drive.Width});
  if(!CrosswalkLanePolicy.Span(curve,lanes,out var fitted))return false;
  // Endpoint exclusions belong to pedestrian lanes on the shoulder. Keep
  // internal median exclusions, remapped into the actual driving envelope.
  var mapped=new List<Bounds1>();var delta=fitted.d-fitted.a;float length=math.lengthsq(delta);
  foreach(var cut in cuts){if(cut.min<=0||cut.max>=1)continue;
   float a=math.saturate(math.dot(Geometry.Position(curve,cut.min)-fitted.a,delta)/length);
   float b=math.saturate(math.dot(Geometry.Position(curve,cut.max)-fitted.a,delta)/length);
   if(b>a)mapped.Add(new Bounds1(a,b));
  }
  curve=fitted;cuts=mapped;
  return true;
 }
}}
