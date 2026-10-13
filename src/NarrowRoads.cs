using Game.Net;
using Game.Prefabs;
using Unity.Entities;
using Unity.Mathematics;
namespace RoadStyleIntoKorea {
public partial class StyleSystem {
 Entity ArrowRoad(Entity entity,float3 position){
  var seen=new System.Collections.Generic.HashSet<Entity>();
  while(entity!=Entity.Null&&EntityManager.Exists(entity)&&seen.Add(entity)){
   if(IsRoad(entity))return entity;
   if(EntityManager.HasBuffer<ConnectedEdge>(entity)){float best=float.MaxValue;Entity result=Entity.Null;foreach(var link in EntityManager.GetBuffer<ConnectedEdge>(entity,true)){if(!IsRoad(link.m_Edge)||!EntityManager.HasComponent<Curve>(link.m_Edge))continue;var arc=new Geometry.Arc(CurveOf(link.m_Edge));float error=math.distancesq(position,Geometry.Position(arc.Curve,arc.Nearest(position)));if(error<best){best=error;result=link.m_Edge;}}return result;}
   if(!EntityManager.HasComponent<Game.Common.Owner>(entity))break;entity=EntityManager.GetComponentData<Game.Common.Owner>(entity).m_Owner;
  }return Entity.Null;
 }
 int RoadLaneCount(Entity edge){
  if(!EntityManager.HasComponent<Composition>(edge))return 0;
  var cp=EntityManager.GetComponentData<Composition>(edge).m_Edge;int count=0;
  if(EntityManager.HasBuffer<NetCompositionLane>(cp))foreach(var lane in EntityManager.GetBuffer<NetCompositionLane>(cp,true))
   if((lane.m_Flags&LaneFlags.Road)!=0&&(lane.m_Flags&(LaneFlags.Master|LaneFlags.Secondary|LaneFlags.Parking|LaneFlags.BicyclesOnly|LaneFlags.Virtual))==0)count++;
  return count;
 }
 int ApplyAllPavingStyle(Entity road){
  float width=0;
  if(EntityManager.HasComponent<Composition>(road)){
   var cp=EntityManager.GetComponentData<Composition>(road).m_Edge;
   if(EntityManager.HasBuffer<NetCompositionLane>(cp))foreach(var lane in EntityManager.GetBuffer<NetCompositionLane>(cp,true))
    if((lane.m_Flags&LaneFlags.Road)!=0&&(lane.m_Flags&(LaneFlags.Master|LaneFlags.Secondary|LaneFlags.Parking|LaneFlags.BicyclesOnly|LaneFlags.Virtual))==0&&EntityManager.HasComponent<NetLaneData>(lane.m_Lane))width+=EntityManager.GetComponentData<NetLaneData>(lane.m_Lane).m_Width;
  }
  return ApplyAllPolicy.Paving(RoadLaneCount(road),width);
 }
 bool IsNarrowRoad(Entity road)=>NarrowPolicy.Narrow(RoadLaneCount(road));
 float RoadStopGap(Entity road)=>NarrowPolicy.StopGap(RoadLaneCount(road),S.StopGap);
 float RoadArrowGap(Entity road)=>NarrowPolicy.ArrowGap(RoadLaneCount(road),S.ArrowStopDistance);
}}
