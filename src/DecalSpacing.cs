using System.Collections.Generic;
using Game.Net;
using Unity.Entities;
using Unity.Mathematics;
namespace RoadStyleIntoKorea {public partial class StyleSystem {
 bool SecondarySpaceOnRoad(Entity road,float3 position,float half){
  foreach(var map in new[]{records,approachRecords,manualRecords})if(map.TryGetValue(road,out var record))foreach(var e in record.Paint){
   if(!Live(e)||!EntityManager.HasComponent<Game.Objects.Transform>(e))continue;var name=assets.Name(Prefab(e));
   if(!name.StartsWith("CSKRLaneDirectionArrow",System.StringComparison.Ordinal)||name==AssetCatalog.Arrow("UTurn"))continue;
   var transform=EntityManager.GetComponentData<Game.Objects.Transform>(e);var forward=math.mul(transform.m_Rotation,new float3(0,0,1));var delta=position-transform.m_Position;
   if(math.abs(delta.y)>2||math.abs(math.dot(delta,Geometry.Left(forward)))>1.5f)continue;
   if(!ArrowPolicy.SecondaryClear(math.dot(delta,forward),half,0,DecalHalfLength(Prefab(e),3.8f)))return false;
  }
  return true;
 }
 readonly HashSet<Entity> suppressedWarnings=new HashSet<Entity>();
 void ReconcileDecalSpacing(Entity road,bool refreshColor=true){
  if(refreshColor)QueueColorGuides(road);
  ReconcileSpeedSigns(road);
  var groups=new List<Record>();if(records.TryGetValue(road,out var r))groups.Add(r);if(approachRecords.TryGetValue(road,out r))groups.Add(r);if(manualRecords.TryGetValue(road,out r))groups.Add(r);
  var arrows=new List<Entity>();var warnings=new List<Entity>();
  foreach(var group in groups)foreach(var e in group.Paint){
   if(!Live(e)||!EntityManager.HasComponent<Game.Objects.Transform>(e))continue;var name=assets.Name(Prefab(e));
   if(name==AssetCatalog.Diamond||name==AssetCatalog.Arrow("UTurn"))warnings.Add(e);
   else if(name.StartsWith("CSKRLaneDirectionArrow",System.StringComparison.Ordinal))arrows.Add(e);
  }
  foreach(var warning in warnings){var w=EntityManager.GetComponentData<Game.Objects.Transform>(warning);float half=DecalHalfLength(Prefab(warning),2.8f);bool blocked=false;
   foreach(var arrow in arrows){var a=EntityManager.GetComponentData<Game.Objects.Transform>(arrow);var forward=math.mul(a.m_Rotation,new float3(0,0,1));var delta=w.m_Position-a.m_Position;
    if(math.abs(delta.y)>2||math.abs(math.dot(delta,Geometry.Left(forward)))>1.5f)continue;
    if(!ArrowPolicy.SecondaryClear(math.dot(delta,forward),half,0,DecalHalfLength(Prefab(arrow),3.8f))){blocked=true;break;}
   }
   if(blocked){suppressedWarnings.Add(warning);if(!EntityManager.HasComponent<Game.Tools.Hidden>(warning)){EntityManager.AddComponent<Game.Tools.Hidden>(warning);DirtyBatch(warning);}}
   else if(suppressedWarnings.Remove(warning)){EntityManager.RemoveComponent<Game.Tools.Hidden>(warning);DirtyBatch(warning);}
  }
 }
 void PaintApproachDiamonds(Entity owner,Record record,List<Drive> drives){
  if(!S.Diamonds)return;
  var prefab=assets.Get(AssetCatalog.Diamond);float half=DecalHalfLength(prefab,2.8f);
  foreach(var drive in drives){
   if(drive.Stop<0||HighwayRoad(owner)&&drive.Crossing==Entity.Null)continue;
   for(int n=0;n<math.clamp(S.DiamondCount,1,3);n++){
    float at=drive.Stop-S.DiamondDistance-n*S.DiamondSpacing;
    if(at<half||at>drive.Arc.Length-half||!ArrowPolicy.SecondaryClear(at,half,drive.PrimaryPaint))continue;
    PaintObject(owner,record,prefab,new Game.Objects.Transform(drive.Arc.At(at),quaternion.LookRotationSafe(drive.Arc.Direction(at),new float3(0,1,0))));
    drive.WarningPaint.Add(new ArrowPolicy.Occupied(at,half));
   }
  }
 }
}}
