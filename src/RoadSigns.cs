using System.Collections.Generic;
using Game.Prefabs;
using Unity.Entities;
using Unity.Mathematics;
namespace RoadStyleIntoKorea { public partial class StyleSystem {
 internal int SpeedBrush,TextBrush;internal bool SignSearchFocused;internal string SignHover="";
 internal void CycleRoadSign(int kind){int count=kind==5?13:RoadSignPolicy.Text.Length;int v=kind==5?SpeedBrush:TextBrush;v=v/count*count+(v%count+1)%count;if(kind==5)SpeedBrush=v;else TextBrush=v;}
 internal void SelectRoadSign(int kind,int value){if(kind==5)SpeedBrush=math.clamp(value,0,38);else if(kind==6)TextBrush=math.clamp(value,0,32);}
 int RoadKmh(Entity road){var ps=World.GetExistingSystemManaged<PrefabSystem>();return ps!=null&&ps.TryGetPrefab<RoadPrefab>(Prefab(road),out var p)&&p!=null?RoadSignPolicy.Kmh(p.m_SpeedLimit):0;}
 string RoadSignAsset(Entity road,ManualDecalChoice c)=>RoadSignPolicy.Asset(c.Kind,c.Symbol,c.Width,c.Kind==5?RoadKmh(road):0);
 bool RoadSignReady(string name){var e=assets.Get(name);return e!=Entity.Null&&EntityManager.HasComponent<ObjectData>(e);}
 bool SignSpace(Entity road,Record record,Drive drive,float at,float half){
  if(at<half+.5f||at>drive.Arc.Length-half-.5f||!ArrowPolicy.SecondaryClear(at,half,drive.PrimaryPaint)||!ArrowPolicy.SecondaryClear(at,half,drive.WarningPaint))return false;
  if(!AutomaticDriveMarking(road,drive)||!CrosswalkDecalClear(road,drive,at,half))return false;
  var pos=drive.Arc.At(at);var f=drive.Arc.Direction(at);
  var groups=new List<Record>{record};if(manualRecords.TryGetValue(road,out var placed))groups.Add(placed);
  foreach(var group in groups)foreach(var e in group.Paint){if(!Live(e)||!EntityManager.HasComponent<Game.Objects.Transform>(e))continue;var tr=EntityManager.GetComponentData<Game.Objects.Transform>(e);var delta=tr.m_Position-pos;if(math.abs(delta.y)<2&&math.abs(math.dot(delta,Geometry.Left(f)))<drive.Width*.5f&&math.abs(math.dot(delta,f))<half+DecalHalfLength(Prefab(e),2)+1)return false;}
  return true;
 }
 void PaintSpeedSigns(Entity road,Record record,List<Drive> drives){
  if(!S.SpeedSigns||EntityManager.HasComponent<ApproachChoice>(road)&&EntityManager.GetComponentData<ApproachChoice>(road).SuppressRoadSigns)return;int kmh=RoadKmh(road);
  foreach(var d in drives){var name=RoadSignPolicy.Asset(5,0,d.Width,kmh);if(!RoadSignReady(name))continue;var prefab=assets.Get(name);float half=DecalHalfLength(prefab,2);
   // One sign per driving lane and segment; bounded placement search, never per-frame scanning.
   float end=d.Stop>=0?math.min(d.Stop-half-3,d.Arc.Length-half-.5f):d.Arc.Length-half-.5f;
   for(int n=0;n<7;n++){float at=math.min(d.Arc.Length*.5f,end)-n*(half*2+2);if(!SignSpace(road,record,d,at,half))continue;
    PaintObject(road,record,prefab,new Game.Objects.Transform(d.Arc.At(at),quaternion.LookRotationSafe(d.Arc.Direction(at),new float3(0,1,0))));break;}
  }
  ReconcileSpeedSigns(road);
 }
 void ReconcileSpeedSigns(Entity road){
  var manual=new List<ManualDecalChoice>();if(EntityManager.HasBuffer<ManualDecalChoice>(road))foreach(var c in EntityManager.GetBuffer<ManualDecalChoice>(road,true))if(c.Kind==5)manual.Add(c);
  foreach(var map in new[]{records,approachRecords})if(map.TryGetValue(road,out var record))foreach(var e in record.Paint){
   if(!Live(e)||!assets.Name(Prefab(e)).StartsWith("CSKRRoadSignSpeed",System.StringComparison.Ordinal)||!EntityManager.HasComponent<Game.Objects.Transform>(e))continue;
   var tr=EntityManager.GetComponentData<Game.Objects.Transform>(e);var f=math.mul(tr.m_Rotation,new float3(0,0,1));bool hide=false;
   foreach(var c in manual){ManualPose(road,c,out var p,out var cf);if(!RoadSignReady(RoadSignAsset(road,c)))continue;var delta=p-tr.m_Position;if(math.dot(cf,f)>.8f&&math.abs(delta.y)<2&&math.abs(math.dot(delta,Geometry.Left(f)))<1){hide=true;break;}}
   if(hide){suppressedWarnings.Add(e);if(!EntityManager.HasComponent<Game.Tools.Hidden>(e)){EntityManager.AddComponent<Game.Tools.Hidden>(e);DirtyBatch(e);}}
   else if(suppressedWarnings.Remove(e)){EntityManager.RemoveComponent<Game.Tools.Hidden>(e);ApplyPaintRemoval(road,e);DirtyBatch(e);}
  }
 }
}}
