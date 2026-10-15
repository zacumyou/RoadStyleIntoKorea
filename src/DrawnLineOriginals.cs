using System.Collections.Generic;
using Colossal.Mathematics;
using Game.Net;
using Unity.Entities;
using Unity.Mathematics;
namespace RoadStyleIntoKorea { public partial class StyleSystem {
 // Reversible, owner-local presentation change. Never touch driving lanes or other mod paint.
 void HideDrawnLineOriginals(Entity owner,Record record,DrawnLineChoice[] choices){
  var replacements=new List<Geometry.Arc>();
  foreach(var choice in choices)if(choice.Style>=0&&choice.Style<DrawnLinePolicy.LineCount&&assets.Get(DrawnLinePolicy.Assets[choice.Style])!=Entity.Null)
   replacements.Add(new Geometry.Arc(LineTransform(owner,choice.Local,false)));
  if(replacements.Count==0)return;
  foreach(var lane in Lanes(owner)){
   if(!Live(lane)||!EntityManager.HasComponent<SecondaryLane>(lane)||!EntityManager.HasComponent<Curve>(lane)||EntityManager.HasComponent<CarLane>(lane)||EntityManager.HasComponent<PedestrianLane>(lane)||EntityManager.HasComponent<GeneratedPaint>(lane)||EntityManager.HasComponent<Game.Tools.Hidden>(lane)||EntityManager.HasComponent<OriginalPaintHidden>(lane))continue;
   var prefab=Prefab(lane);var name=assets.Name(prefab);
   if(!UTurnPolicy.CenterLine(name)&&!MarkingPolicy.IsLaneDivider(name)&&name.IndexOf("Edge Line",System.StringComparison.OrdinalIgnoreCase)<0)continue;
   if(!EntityManager.HasComponent<Game.Prefabs.NetLaneGeometryData>(prefab))continue;
   var curve=CurveOf(lane);var arc=new Geometry.Arc(curve);var cuts=new List<Bounds1>();
   if(EntityManager.HasBuffer<CutRange>(lane))foreach(var cut in EntityManager.GetBuffer<CutRange>(lane,true))cuts.Add(cut.m_CurveDelta);
   bool blocked=false;
   bool Clear(float3 point){float t=arc.Nearest(point);foreach(var cut in cuts)if(t>=cut.min&&t<=cut.max)return false;
    foreach(var replacement in replacements)if(DrawnLinePolicy.Covers(replacement,point,Geometry.Tangent(curve,t),.45f)){blocked=true;return false;}return true;}
   var kept=new List<Bezier4x3>(Geometry.ClearSlices(curve,Clear));if(!blocked)continue;
   Hide(lane,record);
   foreach(var part in kept)PaintRoadHeightLane(owner,record,prefab,part);
  }
 }
}}
