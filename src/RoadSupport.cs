using Game.Net;
using Game.Prefabs;
using Unity.Entities;
using Unity.Mathematics;
using CarLane=Game.Net.CarLane;
namespace RoadStyleIntoKorea {
public partial class StyleSystem {
 bool MotorLanePrefab(Entity prefab)=>!EntityManager.HasComponent<CarLaneData>(prefab)||RoadSupportPolicy.Motor(EntityManager.GetComponentData<CarLaneData>(prefab).m_RoadTypes);
 bool HighwayRoad(Entity road){
  if(!IsRoad(road))return false;
  return (EntityManager.GetComponentData<RoadData>(Prefab(road)).m_Flags&Game.Prefabs.RoadFlags.UseHighwayRules)!=0;
 }
 bool HighwayNode(Entity node){
  if(!EntityManager.HasBuffer<ConnectedEdge>(node))return false;
  foreach(var connection in EntityManager.GetBuffer<ConnectedEdge>(node,true))if(HighwayRoad(connection.m_Edge))return true;
  return false;
 }
 bool HasOpposingTraffic(Entity road){
  if(!IsRoad(road)||!EntityManager.HasComponent<Curve>(road))return false;
  bool forward=false,backward=false;var arc=new Geometry.Arc(CurveOf(road));
  foreach(var lane in Lanes(road)){
   if(!EntityManager.HasComponent<CarLane>(lane)||EntityManager.HasComponent<MasterLane>(lane)||EntityManager.HasComponent<Game.Net.ParkingLane>(lane))continue;
   if(!RoadSupportPolicy.Drive(EntityManager.GetComponentData<CarLane>(lane).m_Flags))continue;
   var curve=CurveOf(lane);float t=arc.Nearest(Geometry.Position(curve,.5f));
   float dot=math.dot(Geometry.Tangent(curve,.5f),Geometry.Tangent(arc.Curve,t));
   forward|=dot>.5f;backward|=dot<-.5f;
  }
  return RoadSupportPolicy.Center(false,forward,backward);
 }
}}
