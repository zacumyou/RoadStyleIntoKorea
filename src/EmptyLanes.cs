using System.Collections.Generic;
using Colossal.Mathematics;
using Game.Net;
using Game.Prefabs;
using Unity.Entities;
using Unity.Mathematics;
namespace RoadStyleIntoKorea { public partial class StyleSystem {
 List<float2> EmptyLaneSpans(Entity edge) {
  var spans=new List<float2>();if(!EntityManager.HasComponent<Composition>(edge))return spans;
  var cp=EntityManager.GetComponentData<Composition>(edge).m_Edge;
  if(!EntityManager.HasBuffer<NetCompositionPiece>(cp))return spans;
  foreach(var p in EntityManager.GetBuffer<NetCompositionPiece>(cp,true)) {
   if((p.m_SectionFlags&(NetSectionFlags.Hidden|NetSectionFlags.HiddenSurface|NetSectionFlags.Underground|NetSectionFlags.Overhead))!=0||
    (p.m_PieceFlags&(NetPieceFlags.Bottom|NetPieceFlags.Side|NetPieceFlags.BlockTraffic))!=0||!EmptyLanePolicy.Empty(PieceName(p.m_Piece)))continue;
   spans.Add(new float2(p.m_Offset.x-p.m_Size.x*.5f,p.m_Offset.x+p.m_Size.x*.5f));
  }
  return spans;
 }
 bool CompositionPosition(Entity edge,float3 point,out float x) {
  x=0;if(!EntityManager.HasComponent<Composition>(edge)||!EntityManager.HasComponent<EdgeGeometry>(edge))return false;
  var cp=EntityManager.GetComponentData<Composition>(edge).m_Edge;
  if(!EntityManager.HasComponent<NetCompositionData>(cp))return false;
  var g=EntityManager.GetComponentData<EdgeGeometry>(edge);float best=float.MaxValue;
  foreach(var segment in new[]{g.m_Start,g.m_End}) {
   float value=EmptyLanePolicy.Position(point,EntityManager.GetComponentData<NetCompositionData>(cp).m_Width,segment.m_Left,segment.m_Right,out float d);
   if(d>=best)continue;best=d;x=value;
  }
  return best<float.MaxValue;
 }
 float InnerEmptyWidth(Entity edge,Bezier4x3 curve,float laneWidth) {
  var spans=EmptyLaneSpans(edge);if(spans.Count==0||!CompositionPosition(edge,Geometry.Position(curve,.5f),out float x))return 0;
  var cp=EntityManager.GetComponentData<Composition>(edge).m_Edge;if(!EntityManager.HasBuffer<NetCompositionLane>(cp))return 0;
  float best=float.MaxValue,extension=0;
  foreach(var lane in EntityManager.GetBuffer<NetCompositionLane>(cp,true)) {
   if((lane.m_Flags&LaneFlags.Road)==0||
    (lane.m_Flags&(LaneFlags.Master|LaneFlags.Parking|LaneFlags.Virtual))!=0||!EntityManager.HasComponent<NetLaneData>(lane.m_Lane))continue;
   float distance=math.abs(lane.m_Position.x-x);if(distance>=best||distance>laneWidth*.5f)continue;
   best=distance;int direction=(lane.m_Flags&LaneFlags.Invert)!=0?1:-1;
   float boundary=lane.m_Position.x+direction*EntityManager.GetComponentData<NetLaneData>(lane.m_Lane).m_Width*.5f;
   extension=math.abs(EmptyLanePolicy.Extend(boundary,direction,spans)-boundary);
  }
  return extension;
 }
}}
