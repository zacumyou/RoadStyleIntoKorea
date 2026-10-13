using System.Collections.Generic;
using Game.Prefabs;
using Unity.Mathematics;
namespace RoadStyleIntoKorea { public partial class StyleSystem {
 float2 ArrowBounds(string symbol){
  var prefab=assets.Get(AssetCatalog.Arrow(symbol));
  if(EntityManager.HasComponent<ObjectGeometryData>(prefab)){
   var b=EntityManager.GetComponentData<ObjectGeometryData>(prefab).m_Bounds;
   if(math.isfinite(b.min.z)&&math.isfinite(b.max.z)&&b.max.z-b.min.z>.1f)return new float2(b.min.z,b.max.z);
  }
  return new float2(-3.7f,3.7f);
 }
 float ArrowStackAt(float first,List<string> symbols,int index)=>first+ArrowBounds(symbols[0]).y-ArrowBounds(symbols[index]).y-index*8;
 bool TryStopArrowPlacement(Drive drive,float stop,List<string> symbols,int count,out float first){
  var bounds=new List<float2>();for(int i=0;i<count;i++)bounds.Add(ArrowBounds(symbols[i]));
  if(!ArrowPolicy.TryTopPlacement(drive.Arc.Length,stop,RoadArrowGap(drive.Owner),bounds,out first))return false;
  for(int i=0;i<count;i++)if(!ArrowStopClear(drive,ArrowStackAt(first,symbols,i),symbols[i]))return false;
  return true;
 }
 bool ArrowStopClear(Drive drive,float at,string symbol){
  if(drive.Stop<0)return true;
  var normal=drive.Arc.Direction(drive.Stop);
  if(drive.Crossing!=Unity.Entities.Entity.Null)normal=CrossingAlignmentPolicy.StopNormal(CrosswalkCurve(drive.Crossing),normal);
  var bounds=ArrowBounds(symbol);var prefab=assets.Get(AssetCatalog.Arrow(symbol));
  float halfWidth=drive.Width*.5f;
  if(EntityManager.HasComponent<ObjectGeometryData>(prefab)){var b=EntityManager.GetComponentData<ObjectGeometryData>(prefab).m_Bounds;halfWidth=math.max(math.abs(b.min.x),math.abs(b.max.x));}
  var forward=drive.Arc.Direction(at);var position=drive.Arc.At(at);var stop=drive.Arc.At(drive.Stop);
  return ArrowPolicy.BehindStop(position,forward,stop,normal,bounds,halfWidth)&&CrosswalkDecalClear(drive.Owner,drive,at,math.max(math.abs(bounds.x),math.abs(bounds.y)));
 }
 bool FitsArrowStack(Drive drive,float first,List<string> symbols){
  for(int i=0;i<symbols.Count;i++){float at=ArrowStackAt(first,symbols,i);var b=ArrowBounds(symbols[i]);if(at+b.x<0||at+b.y>drive.Arc.Length)return false;}
  return true;
 }
}}
