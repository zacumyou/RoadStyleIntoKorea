using System;
using System.Collections.Generic;
using Game.Common;
using Game.Tools;
using Game.Rendering;
using Game.Net;
using Game.Prefabs;
using Unity.Entities;
using Unity.Mathematics;
namespace RoadStyleIntoKorea {
 public partial class StyleSystem {
  void ClipSideRoadNativeLines(Entity road,Record record,List<Entity> lanes){
   if(!EntityManager.HasBuffer<SideRoadChoice>(road)||EntityManager.GetBuffer<SideRoadChoice>(road,true).Length==0)return;
   var center=new Geometry.Arc(CurveOf(road));
   var choice=EntityManager.HasComponent<EdgeChoice>(road)?EntityManager.GetComponentData<EdgeChoice>(road):default;
   foreach(var lane in lanes){
    if(EntityManager.HasComponent<Hidden>(lane)||!EntityManager.HasComponent<Game.Net.SecondaryLane>(lane)||!EntityManager.HasComponent<Curve>(lane))continue;
    var prefab=Prefab(lane);
    if(!EntityManager.HasComponent<NetLaneGeometryData>(prefab)||assets.Name(prefab).IndexOf("Edge Line",StringComparison.OrdinalIgnoreCase)<0)continue;
    var curve=CurveOf(lane);var sample=Geometry.Position(curve,.5f);float t=center.Nearest(sample);
    int side=math.dot(sample-Geometry.Position(center.Curve,t),Geometry.Left(Geometry.Tangent(center.Curve,t)))>=0?1:-1;
    int code=side>0?choice.Left:choice.Right;
    var style=code==0?(active?S.DefaultEdge:EdgeStyle.Original):(EdgeStyle)math.clamp(code-1,0,5);
    if(S.EdgeLines&&style!=EdgeStyle.Original)continue;
    var gaps=SideRoadIntervals(road,curve,side);if(gaps.Count==0)continue;
    var arc=new Geometry.Arc(curve);
    // Preserve any native cut ranges while replacing only the surviving pieces.
    if(EntityManager.HasBuffer<CutRange>(lane))foreach(var cut in EntityManager.GetBuffer<CutRange>(lane,true))gaps.Add(new float2(arc.Distance(math.saturate(cut.m_CurveDelta.min)),arc.Distance(math.saturate(cut.m_CurveDelta.max))));
    DrivewayPolicy.Merge(gaps);float start=0;int before=record.Paint.Count;
    foreach(var gap in gaps){if(gap.x-start>.025f)PaintLane(road,record,prefab,arc.Slice(start,gap.x));start=math.max(start,gap.y);}
    if(arc.Length-start>.025f)PaintLane(road,record,prefab,arc.Slice(start,arc.Length));
    for(int i=before;i<record.Paint.Count;i++)record.EdgePaint.Add(record.Paint[i]);
    Hide(lane,record);if(record.Hidden.Contains(lane))record.EdgeHidden.Add(lane);
   }
  }
 }
}

