using System.Collections.Generic;
using Colossal.Mathematics;
using Game.Net;
using Unity.Entities;
using Unity.Mathematics;
namespace RoadStyleIntoKorea {public partial class StyleSystem {
 sealed class OwnerShape {
  internal Entity Prefab,EdgeComposition,StartComposition,EndComposition;internal float3 Position;
  internal readonly List<Bezier4x3> Curves=new List<Bezier4x3>();
  internal readonly HashSet<Entity> Connections=new HashSet<Entity>();
  internal readonly List<(Bezier4x3 Curve,Entity Prefab,float Width)> Lanes=new List<(Bezier4x3,Entity,float)>();
 }
 readonly Dictionary<Entity,OwnerShape> ownerShapes=new Dictionary<Entity,OwnerShape>();
 void AddShape(OwnerShape s,Segment g){s.Curves.Add(g.m_Left);s.Curves.Add(g.m_Right);}
 void AddShape(OwnerShape s,EdgeNodeGeometry g){AddShape(s,g.m_Left);AddShape(s,g.m_Right);s.Curves.Add(g.m_Middle);}
 IEnumerable<Entity> ShapeLanes(Entity owner){
  if(!Live(owner)||!EntityManager.HasBuffer<SubLane>(owner))yield break;
  var list=new List<Entity>();foreach(var item in EntityManager.GetBuffer<SubLane>(owner,true))if(Live(item.m_SubLane)&&EntityManager.HasComponent<Curve>(item.m_SubLane)&&!EntityManager.HasComponent<ManualUTurnRoute>(item.m_SubLane))list.Add(item.m_SubLane);foreach(var lane in list)yield return lane;
 }
 OwnerShape ReadOwnerShape(Entity owner){
  if(!Live(owner))return null;var s=new OwnerShape{Prefab=Prefab(owner)};
  if(EntityManager.HasComponent<Composition>(owner)){
   var cp=OriginalPavingComposition(owner,EntityManager.GetComponentData<Composition>(owner));s.EdgeComposition=cp.m_Edge;s.StartComposition=cp.m_StartNode;s.EndComposition=cp.m_EndNode;
   // Our private asphalt cut is a render alias, not a native road change.
   if(nativeSideRoadCuts.TryGetValue(owner,out var cut)&&s.EdgeComposition==cut.Composition)s.EdgeComposition=cut.Original;
  }
  if(EntityManager.HasComponent<Curve>(owner))s.Curves.Add(CurveOf(owner));
  if(EntityManager.HasComponent<Node>(owner))s.Position=EntityManager.GetComponentData<Node>(owner).m_Position;
  if(EntityManager.HasComponent<Edge>(owner)){var e=EntityManager.GetComponentData<Edge>(owner);s.Connections.Add(e.m_Start);s.Connections.Add(e.m_End);}
  if(EntityManager.HasBuffer<ConnectedEdge>(owner))foreach(var e in EntityManager.GetBuffer<ConnectedEdge>(owner,true))s.Connections.Add(e.m_Edge);
  if(EntityManager.HasComponent<EdgeGeometry>(owner)){var g=EntityManager.GetComponentData<EdgeGeometry>(owner);AddShape(s,g.m_Start);AddShape(s,g.m_End);}
  if(EntityManager.HasComponent<StartNodeGeometry>(owner))AddShape(s,EntityManager.GetComponentData<StartNodeGeometry>(owner).m_Geometry);
  if(EntityManager.HasComponent<EndNodeGeometry>(owner))AddShape(s,EntityManager.GetComponentData<EndNodeGeometry>(owner).m_Geometry);
  // Side connections have their own edge-only invalidation; adding a building must not rebuild all road paint.
  foreach(var lane in ShapeLanes(owner))if(!SideVehicleConnection(lane)&&(EntityManager.HasComponent<CarLane>(lane)||EntityManager.HasComponent<PedestrianLane>(lane)))s.Lanes.Add((SourceCurve(lane),Prefab(lane),Width(lane)));
  return s;
 }
 bool OwnerGeometryChanged(Entity owner){
  if(!ownerShapes.ContainsKey(owner))return false;
  var current=ReadOwnerShape(owner);if(current==null){bool existed=ownerShapes.Remove(owner);return existed;}
  bool known=ownerShapes.TryGetValue(owner,out var old);ownerShapes[owner]=current;if(!known)return false;
  if(old.Prefab!=current.Prefab||old.EdgeComposition!=current.EdgeComposition||old.StartComposition!=current.StartComposition||old.EndComposition!=current.EndComposition||math.distancesq(old.Position,current.Position)>.000001f||!old.Connections.SetEquals(current.Connections)||old.Curves.Count!=current.Curves.Count||old.Lanes.Count!=current.Lanes.Count)return true;
  for(int i=0;i<old.Curves.Count;i++)if(!Geometry.SameCurve(old.Curves[i],current.Curves[i]))return true;
  return RefreshPolicy.LanesChanged(old.Lanes,current.Lanes);
 }
 void ObserveOwnerShape(Entity owner){
  var shape=ReadOwnerShape(owner);if(shape==null)return;ownerShapes[owner]=shape;
  if(EntityManager.HasComponent<Edge>(owner)){var edge=EntityManager.GetComponentData<Edge>(owner);foreach(var node in new[]{edge.m_Start,edge.m_End})if(!ownerShapes.ContainsKey(node)){var baseline=ReadOwnerShape(node);if(baseline!=null)ownerShapes[node]=baseline;}}
  foreach(var lane in ShapeLanes(owner))if(EntityManager.HasComponent<Lane>(lane)&&EntityManager.HasComponent<Game.Common.Owner>(lane)&&!laneRefreshStamps.ContainsKey(lane))LaneGeometryChanged(lane);
 }
}}
