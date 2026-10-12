using Game.Common;
using Game.Net;
using Game.Prefabs;
using Unity.Entities;
namespace RoadStyleIntoKorea {public partial class StyleSystem {
 // Building subnets may have vehicle lanes without the ordinary Road component.
 // This eligibility is for manually drawn guides only, never automatic styling.
 bool GuideRoad(Entity e){
  if(!Live(e)||!EntityManager.HasComponent<Edge>(e)||!EntityManager.HasComponent<Curve>(e))return false;
  if(IsRoad(e))return true;
  if(!EntityManager.HasBuffer<Game.Net.SubLane>(e))return false;
  foreach(var item in EntityManager.GetBuffer<Game.Net.SubLane>(e,true)){var lane=item.m_SubLane;
   if(Live(lane)&&EntityManager.HasComponent<Curve>(lane)&&EntityManager.HasComponent<Game.Net.CarLane>(lane)&&!EntityManager.HasComponent<MasterLane>(lane)&&!EntityManager.HasComponent<GeneratedPaint>(lane)&&!EntityManager.HasComponent<ManualUTurnRoute>(lane))return true;
  }
  return false;
 }
 bool GuideNode(Entity e){
  if(!Live(e)||!EntityManager.HasComponent<Node>(e)||!EntityManager.HasBuffer<ConnectedEdge>(e))return false;
  foreach(var edge in EntityManager.GetBuffer<ConnectedEdge>(e,true))if(GuideRoad(edge.m_Edge))return true;
  return false;
 }
 bool LineRoad(Entity e)=>IsRoad(e)||(ColorGuideMode&&!redPavingMode&&GuideRoad(e));
 bool LineNode(Entity e)=>SafeNode(e)||(ColorGuideMode&&!redPavingMode&&GuideNode(e));
 bool RestrictedGuideRoad(Entity e){
  if(!GuideRoad(e))return false;
  if(!IsRoad(e)||EntityManager.HasComponent<Owner>(e))return true;
  if(EntityManager.HasComponent<Composition>(e)){
   var cp=EntityManager.GetComponentData<Composition>(e).m_Edge;
   if(EntityManager.HasComponent<NetCompositionData>(cp))return (EntityManager.GetComponentData<NetCompositionData>(cp).m_State&CompositionState.Marker)!=0;
  }return false;
 }
 bool RestrictedGuideOwner(Entity e){
  if(RestrictedGuideRoad(e))return true;
  if(GuideNode(e))foreach(var edge in EntityManager.GetBuffer<ConnectedEdge>(e,true))if(RestrictedGuideRoad(edge.m_Edge))return true;
  return false;
 }
 bool PenLanesJoin(int from,int to){
  var a=penLanes[from/2];var b=penLanes[to/2];
  if(!a.HasLink||!b.HasLink)return false;
  var first=a.Link;var second=b.Link;
  var end=from%2==0?first.m_EndNode:first.m_StartNode;
  var start=to%2==0?second.m_StartNode:second.m_EndNode;
  return ColorGuideNetworkPolicy.Connected(end,start);
 }
}}
