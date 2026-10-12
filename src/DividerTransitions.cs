using System.Collections.Generic;
using Game.Net;
using PathNode=Game.Pathfind.PathNode;
using Unity.Entities;
using Unity.Mathematics;
namespace RoadStyleIntoKorea { public partial class StyleSystem {
 // Per-build cache: bounded local walks, no city scan and no persistent topology cache.
 sealed class DividerWalk {
  internal readonly Dictionary<Entity,List<Entity>> Lanes=new Dictionary<Entity,List<Entity>>();
  internal readonly Dictionary<Entity,List<Drive>> Drives=new Dictionary<Entity,List<Drive>>();
 }
 List<Entity> WalkLanes(Entity owner,DividerWalk walk){if(!walk.Lanes.TryGetValue(owner,out var result)){result=Lanes(owner);walk.Lanes.Add(owner,result);}return result;}
 int DividerTransition(Entity road,Drive drive,DividerWalk walk){
  int drop=WalkDividerTransition(road,drive,false,walk);
  return drop!=0?drop:WalkDividerTransition(road,drive,true,walk);
 }
 int WalkDividerTransition(Entity road,Drive drive,bool backwards,DividerWalk walk){
  var visited=new HashSet<Entity>();float distance=0;
  for(int step=0;step<12&&distance<1200;step++){
   if(!IsRoad(road)||!HighwayRoad(road)||!visited.Add(road))return 0;
   var edge=EntityManager.GetComponentData<Edge>(road);var point=backwards?drive.Curve.a:drive.Curve.d;
   if(!SafeNode(edge.m_Start)||!SafeNode(edge.m_End))return 0;
   var node=math.distancesq(point,EntityManager.GetComponentData<Node>(edge.m_Start).m_Position)<math.distancesq(point,EntityManager.GetComponentData<Node>(edge.m_End).m_Position)?edge.m_Start:edge.m_End;
   if(!EntityManager.HasBuffer<ConnectedEdge>(node))return 0;
   Entity next=Entity.Null;int count=0;
   foreach(var e in EntityManager.GetBuffer<ConnectedEdge>(node,true)){if(!IsRoad(e.m_Edge))return 0;count++;if(e.m_Edge!=road)next=e.m_Edge;}
   if(count!=2||next==Entity.Null||!HighwayRoad(next))return 0;
   var keys=new HashSet<PathNode>();
   foreach(var lane in WalkLanes(road,walk))if(EntityManager.HasComponent<CarLane>(lane)&&!EntityManager.HasComponent<MasterLane>(lane)&&EntityManager.HasComponent<Lane>(lane)){
    var c=CurveOf(lane);if(math.distance(backwards?c.a:c.d,point)>.05f)continue;
    var link=EntityManager.GetComponentData<Lane>(lane);keys.Add(backwards?link.m_StartNode:link.m_EndNode);
   }
   var starts=new HashSet<PathNode>();var ends=new HashSet<PathNode>();var matches=new List<Entity>();var links=new List<Entity>();
   foreach(var lane in WalkLanes(node,walk)){
    if(!EntityManager.HasComponent<CarLane>(lane)||EntityManager.HasComponent<MasterLane>(lane)||!EntityManager.HasComponent<Lane>(lane))continue;
    var flags=EntityManager.GetComponentData<CarLane>(lane).m_Flags;
    if((flags&(CarLaneFlags.Forbidden|CarLaneFlags.SideConnection|CarLaneFlags.UTurnLeft|CarLaneFlags.UTurnRight))!=0)continue;
    var link=EntityManager.GetComponentData<Lane>(lane);
    var from=backwards?link.m_EndNode:link.m_StartNode;var to=backwards?link.m_StartNode:link.m_EndNode;
    if(from.GetOwnerIndex()!=road.Index||to.GetOwnerIndex()!=next.Index)continue;
    starts.Add(from);ends.Add(to);links.Add(lane);if(keys.Contains(from))matches.Add(lane);
   }
   if(starts.Count!=ends.Count){
    if(starts.Count<ends.Count||matches.Count==0)return 0;
    int result=0;
    foreach(var lane in matches){
     var link=EntityManager.GetComponentData<Lane>(lane);var from=backwards?link.m_EndNode:link.m_StartNode;var to=backwards?link.m_StartNode:link.m_EndNode;bool shared=false;
     foreach(var other in links){var l=EntityManager.GetComponentData<Lane>(other);if(!(backwards?l.m_EndNode:l.m_StartNode).Equals(from)&&(backwards?l.m_StartNode:l.m_EndNode).Equals(to)){shared=true;break;}}
     var curve=CurveOf(lane);int direction=MergePolicy.DividerDirection(starts.Count,ends.Count,shared,curve,backwards);
     if(direction==0||result!=0&&result!=direction)return 0;result=direction;
    }
    return result;
   }
   if(matches.Count!=1)return 0;
   var connection=EntityManager.GetComponentData<Lane>(matches[0]);var target=backwards?connection.m_StartNode:connection.m_EndNode;
   // Do not cross a split/merge with equal total counts or an ambiguous connection.
   foreach(var other in links)if(other!=matches[0]){var l=EntityManager.GetComponentData<Lane>(other);if((backwards?l.m_StartNode:l.m_EndNode).Equals(target))return 0;}
   var joining=CurveOf(matches[0]);if(math.dot(Geometry.Tangent(joining,0),Geometry.Tangent(joining,1))<.9f)return 0;
   if(!walk.Drives.TryGetValue(next,out var candidates)){candidates=Drives(WalkLanes(next,walk));walk.Drives.Add(next,candidates);}
   Drive found=null;var arrival=backwards?joining.a:joining.d;
   foreach(var d in candidates)if(math.distance(backwards?d.Curve.d:d.Curve.a,arrival)<.1f){if(found!=null)return 0;found=d;}
   if(found==null)return 0;distance+=drive.Arc.Length;road=next;drive=found;
  }
  return 0;
 }
}}
