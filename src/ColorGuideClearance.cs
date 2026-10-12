using System.Collections.Generic;
using Game.Net;
using Game.Prefabs;
using Unity.Entities;
using Unity.Mathematics;
namespace RoadStyleIntoKorea {public partial class StyleSystem {
 sealed class ColorObstacle {
  internal Geometry.Arc Arc;internal float Radius;
  internal float3 Position,Min,Max;internal quaternion Inverse;
 }
 bool HasColorGuide(Entity owner){if(!Live(owner)||!EntityManager.HasBuffer<DrawnLineChoice>(owner))return false;foreach(var c in EntityManager.GetBuffer<DrawnLineChoice>(owner,true))if(DrawnLinePolicy.IsColor(c.Style))return true;return false;}
 void QueueColorGuides(Entity owner){
  if(HasColorGuide(owner))manualDirty.Add(owner);
  if(IsRoad(owner)){var edge=EntityManager.GetComponentData<Edge>(owner);if(HasColorGuide(edge.m_Start))manualDirty.Add(edge.m_Start);if(HasColorGuide(edge.m_End))manualDirty.Add(edge.m_End);}
 }
 List<ColorObstacle> ColorGuideObstacles(Entity owner){
  var owners=new HashSet<Entity>{owner};
  if(IsRoad(owner)){var edge=EntityManager.GetComponentData<Edge>(owner);owners.Add(edge.m_Start);owners.Add(edge.m_End);}
  else if(SafeNode(owner))foreach(var edge in EntityManager.GetBuffer<ConnectedEdge>(owner,true))if(IsRoad(edge.m_Edge))owners.Add(edge.m_Edge);
  var candidates=new HashSet<Entity>();
  foreach(var o in owners){
   foreach(var lane in Lanes(o))candidates.Add(lane);
   if(EntityManager.HasBuffer<Game.Objects.SubObject>(o))foreach(var item in EntityManager.GetBuffer<Game.Objects.SubObject>(o,true))candidates.Add(item.m_SubObject);
   foreach(var map in new[]{records,approachRecords,approachStopRecords,manualRecords,guideRecords})if(map.TryGetValue(o,out var r))foreach(var e in r.Paint)candidates.Add(e);
  }
  var result=new List<ColorObstacle>();
  foreach(var e in candidates){
   if(!Live(e)||EntityManager.HasComponent<Game.Tools.Hidden>(e)||EntityManager.HasComponent<Game.Net.CarLane>(e)||EntityManager.HasComponent<Game.Net.PedestrianLane>(e))continue;
   var prefab=Prefab(e);var name=assets.Name(prefab);
   if(name.Contains("ColorGuide")||MarkingPolicy.IsGuideDivider(name)||name.Contains("CSKRHighwayWhite")||name.Contains("CSKRBusBlue")||name.Contains("Divider"))continue;
   bool arrow=name.IndexOf("Arrow",System.StringComparison.OrdinalIgnoreCase)>=0||name.Contains("LaneDirection");
   bool marking=arrow||name.Contains("Stop Line")||name.Contains("Yield Line")||name.Contains("Edge Line")||name.Contains("Center Line")||name.Contains("CSKREdge")||name.Contains("CSKRCenter")||name.Contains("CSKRStop")||name.Contains("Crosswalk")||name.Contains("SpeedHump")||name.Contains("UTurnZone")||name.Contains("RoadSign");
   if(!marking)continue;
   if(EntityManager.HasComponent<Game.Objects.Transform>(e)){
    var tr=EntityManager.GetComponentData<Game.Objects.Transform>(e);var min=new float3(-1.5f,-1,-DecalHalfLength(prefab,3));var max=-min;
    if(EntityManager.HasComponent<ObjectGeometryData>(prefab)){var b=EntityManager.GetComponentData<ObjectGeometryData>(prefab).m_Bounds;min=b.min;max=b.max;}
    
    result.Add(new ColorObstacle{Position=tr.m_Position,Inverse=math.inverse(tr.m_Rotation),Min=min,Max=max});
   }else if(EntityManager.HasComponent<Curve>(e)){
    float width=EntityManager.HasComponent<NetLaneData>(prefab)?EntityManager.GetComponentData<NetLaneData>(prefab).m_Width:.3f;
    result.Add(new ColorObstacle{Arc=new Geometry.Arc(CurveOf(e)),Radius=math.max(.1f,width*.5f)+.25f+(arrow||name.Contains("Stop Line")?.5f:.05f)});
   }
  }return result;
 }
 static bool ColorGuideClear(float3 p,List<ColorObstacle> obstacles){
  foreach(var obstacle in obstacles){
   if(obstacle.Arc!=null){
    if(!obstacle.Arc.MayBeNear(p,obstacle.Radius,1))continue;
    var nearest=Geometry.Position(obstacle.Arc.Curve,obstacle.Arc.Nearest(p));if(math.abs(nearest.y-p.y)<1&&math.distancesq(nearest.xz,p.xz)<=obstacle.Radius*obstacle.Radius)return false;
   }else {
    var local=math.mul(obstacle.Inverse,p-obstacle.Position);
    if(DrawnLinePolicy.ObjectBlocked(local,obstacle.Min,obstacle.Max))return false;
   }
  }return true;
 }
}}
