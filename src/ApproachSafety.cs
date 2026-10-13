using Game.Net;
using Game.Prefabs;
using Unity.Entities;
using Unity.Mathematics;
namespace RoadStyleIntoKorea {public partial class StyleSystem {
 // Inspect only the owning road during placement/rebuild, not the city each frame.
 bool HasRoadBay(Entity road){
  if(!IsRoad(road)||!EntityManager.HasComponent<Composition>(road))return false;
  var cp=EntityManager.GetComponentData<Composition>(road).m_Edge;
  if(!EntityManager.HasBuffer<NetCompositionPiece>(cp))return false;
  foreach(var piece in EntityManager.GetBuffer<NetCompositionPiece>(cp,true))
   if((piece.m_PieceFlags&NetPieceFlags.HasRoadLanes)!=0&&PieceName(piece.m_Piece).IndexOf("Bay",System.StringComparison.OrdinalIgnoreCase)>=0)return true;
  return false;
 }
 bool AutomaticDriveMarking(Entity road,Drive drive)=>!EntityManager.HasComponent<Game.Net.CarLane>(drive.Entity)||RoadSupportPolicy.AutomaticMarking(EntityManager.GetComponentData<Game.Net.CarLane>(drive.Entity).m_Flags,HasRoadBay(road));
 bool CrosswalkDecalClear(Entity road,Drive drive,float at,float half){
  var position=drive.Arc.At(at);var forward=drive.Arc.Direction(at);
  // Check both endpoints and the proposed crossing shape, even before its paint is built.
  foreach(var crossing in NearbyCrossings(road)){
   if(CrosswalkDecalPolicy.Overlaps(CrosswalkCurve(crossing),CrossingWidth(crossing),position,forward,half,drive.Width*.5f))return false;
  }
  return true;
 }
}}
