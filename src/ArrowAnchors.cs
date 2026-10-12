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
  return ArrowPolicy.TryTopPlacement(drive.Arc.Length,stop,RoadArrowGap(drive.Owner),bounds,out first);
 }
 bool FitsArrowStack(Drive drive,float first,List<string> symbols){
  for(int i=0;i<symbols.Count;i++){float at=ArrowStackAt(first,symbols,i);var b=ArrowBounds(symbols[i]);if(at+b.x<0||at+b.y>drive.Arc.Length)return false;}
  return true;
 }
}}
