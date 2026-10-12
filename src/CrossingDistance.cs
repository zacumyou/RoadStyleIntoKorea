using Colossal.Serialization.Entities;
using Game.Net;
using Unity.Entities;
using Unity.Mathematics;
namespace RoadStyleIntoKorea {
 public partial class StyleSystem {
  internal Entity CrossingDistanceHover;
  readonly System.Collections.Generic.HashSet<Entity> crossingDistancePriority=new System.Collections.Generic.HashSet<Entity>();
  readonly CoalescedIntEdits<Entity> crossingDistanceEdits=new CoalescedIntEdits<Entity>();
  internal int CrossingDistanceValue(Entity node)=>crossingDistanceEdits.Value(node,AppliedCrossingDistanceValue(node));
  int AppliedCrossingDistanceValue(Entity node)=>EntityManager.HasComponent<CrossingDistanceChoice>(node)?EntityManager.GetComponentData<CrossingDistanceChoice>(node).Metres:math.clamp(S.CrosswalkClearance,0,20);
  bool HasCrossingDistance(Entity lane){
   var road=CrossingRoad(lane,out bool start);if(road==Entity.Null)return false;
   var edge=EntityManager.GetComponentData<Edge>(road);
   return EntityManager.HasComponent<CrossingDistanceChoice>(start?edge.m_Start:edge.m_End);
  }
  internal Entity CrossingDistanceTarget(Entity target,float3 hit){
   if(!Live(target))return Entity.Null;
   if(EntityManager.HasComponent<Game.Common.Owner>(target))target=EntityManager.GetComponentData<Game.Common.Owner>(target).m_Owner;
   if(SafeNode(target)&&IsGuideJunction(target))return target;
   if(!IsRoad(target))return Entity.Null;
   var edge=EntityManager.GetComponentData<Edge>(target);
   Entity best=Entity.Null;float distance=1600;
   foreach(var node in new[]{edge.m_Start,edge.m_End}){
    if(!SafeNode(node)||!IsGuideJunction(node))continue;
    float d=math.distancesq(hit,EntityManager.GetComponentData<Node>(node).m_Position);
    if(d<distance){distance=d;best=node;}
   }
   return best;
  }
  internal void DrawCrossingDistanceTarget(Entity node,Game.Rendering.OverlayRenderSystem.Buffer buffer){
   var origin=EntityManager.GetComponentData<Node>(node).m_Position;
   float radius=math.max(6,CrossingDistanceValue(node));
   buffer.DrawCircle(new UnityEngine.Color(.1f,.8f,1,.23f),origin+new float3(0,.15f,0),radius*2);
  }
  float CrossingApproachEnd(Entity road,bool start,Entity lane,Geometry.Arc arc){
   float end=arc.Length;
   foreach(var other in NearbyCrossings(road)){
    if(other==lane||CrossingRoad(other,out bool otherStart)!=road||otherStart==start)continue;
    var curve=crossingPlans.TryGetValue(other,out var planned)?planned:EntityManager.GetComponentData<Curve>(other).m_Bezier;float at=arc.Distance(arc.Nearest(Geometry.Position(curve,.5f)));
    float width=CrosswalkStylePolicy.Width(RequestedCrossingSize(other));
    // Reuse the far crossing plan/current traffic geometry without recursively planning the pair.
    end=math.min(end,(start?at:arc.Length-at)-width*.5f);
   }
   return end;
  }
  internal void AdjustCrossingDistance(Entity node,int delta){
   if(!S.Enabled||!S.Crosswalks||!SafeNode(node))return;
   PrioritizeUserEdit();
   int value=crossingDistanceEdits.Adjust(node,AppliedCrossingDistanceValue(node),delta,UnityEngine.Time.realtimeSinceStartup);
  }
  bool TickCrossingDistanceEdits(){
   if(crossingDistanceEdits.Count==0||approachWork!=null||restoreRequested)return false;
   if(!crossingDistanceEdits.Take(UnityEngine.Time.realtimeSinceStartup,out var node,out int value,out int clicks))return false;
   if(!SafeNode(node))return false;
   int old=AppliedCrossingDistanceValue(node);if(value==old)return false;
   EntityManager.CompleteAllTrackedJobs();
   if(!EntityManager.HasComponent<CrossingDistanceChoice>(node))EntityManager.AddComponent<CrossingDistanceChoice>(node);
   EntityManager.SetComponentData(node,new CrossingDistanceChoice{Metres=value});
   foreach(var lane in NearbyCrossings(node)){
    var road=CrossingRoad(lane,out bool start);if(road==Entity.Null)continue;
    var ends=EntityManager.GetComponentData<Edge>(road);if((start?ends.m_Start:ends.m_End)!=node)continue;
    crossingPlans.Remove(lane);crossingTrafficPlans.Remove(lane);crossingSizes.Remove(lane);
   }
   InvalidateToolPreview();
   EnqueueCrossOnly(node);EnqueueCenterOnly(node);crossingDistancePriority.Add(node);
   foreach(var link in EntityManager.GetBuffer<ConnectedEdge>(node,true)){
    var road=link.m_Edge;if(!IsRoad(road))continue;
    EnqueueCrossOnly(road);RefreshCrosswalkApproach(road);crossingDistancePriority.Add(road);
   }
   queue.Promote(crossingDistancePriority);
   return true;
  }
 }
}
