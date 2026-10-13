using Colossal.Mathematics;
using Game.Net;
using Game.Prefabs;
using Unity.Entities;
using Unity.Mathematics;
namespace RoadStyleIntoKorea {public partial class StyleSystem {
 internal Entity CrosswalkJunction(Entity target,float3 hit){
  if(IsRoad(target)){var edge=EntityManager.GetComponentData<Edge>(target);target=math.distancesq(hit,CurveOf(target).a)<=math.distancesq(hit,CurveOf(target).d)?edge.m_Start:edge.m_End;}
  if(!SafeNode(target)||!EntityManager.HasBuffer<ConnectedEdge>(target))return Entity.Null;
  var roads=new System.Collections.Generic.HashSet<Entity>();foreach(var c in EntityManager.GetBuffer<ConnectedEdge>(target,true))if(IsRoad(c.m_Edge))roads.Add(c.m_Edge);
  return roads.Count>=3?target:Entity.Null;
 }
 internal void JunctionCrosswalkTool(Entity node,Game.Rendering.OverlayRenderSystem.Buffer draw,bool apply,bool forceArrows){
  if(!SafeNode(node))return;
  var roads=new System.Collections.Generic.HashSet<Entity>();foreach(var c in EntityManager.GetBuffer<ConnectedEdge>(node,true))if(IsRoad(c.m_Edge))roads.Add(c.m_Edge);
  foreach(var road in roads){
   bool start=EntityManager.GetComponentData<Edge>(road).m_Start==node;Bezier4x3 curve=default;bool exists=false;
   foreach(var lane in NearbyCrossings(road))if(CrossingRoad(lane,out bool side)==road&&side==start){curve=CrosswalkCurve(lane);exists=true;draw.DrawCurve(new UnityEngine.Color(.1f,.85f,1,.65f),curve,CrosswalkStylePolicy.Width((int)S.CrosswalkBrush),new float2(0));}
   if(!exists){if(!TryNewCrosswalkPreview(road,start,out curve))continue;draw.DrawCurve(new UnityEngine.Color(.1f,.85f,1,.65f),curve,CrosswalkStylePolicy.Width((int)S.CrosswalkBrush),new float2(0));}
   if(apply){var owner=road;var atStart=start;var point=Geometry.Position(curve,.5f);int size=(int)S.CrosswalkBrush;
    localStyleCommands.Enqueue(()=>{if(!IsRoad(owner))return;var ends=EntityManager.GetComponentData<Edge>(owner);if((atStart?ends.m_Start:ends.m_End)!=node)return;var previous=S.CrosswalkBrush;try{S.CrosswalkBrush=(CrosswalkSize)size;PrepareToolTarget(owner);ChooseCrosswalk(owner,point,false,forceArrows,atStart);}finally{S.CrosswalkBrush=previous;}});
   }
  }
  if(apply){targetedActive=true;PrioritizeUserEdit();}
 }
 bool ForceCrosswalkArrows(Entity lane){
  var road=CrossingRoad(lane,out bool start);if(road==Entity.Null||!EntityManager.HasComponent<CrosswalkChoice>(road))return false;
  var choice=EntityManager.GetComponentData<CrosswalkChoice>(road);return start?choice.ForceStartArrows:choice.ForceEndArrows;
 }
 bool TryNewCrosswalkPreview(Entity road,bool start,out Bezier4x3 curve){
  curve=default;if(!NativeCrossingSide(road,start,out _))return false;
  var prefab=Prefab(road);if(!EntityManager.HasComponent<NetData>(prefab)||(EntityManager.GetComponentData<NetData>(prefab).m_SideFlagMask&CompositionFlags.Side.AddCrosswalk)==0)return false;
  var arc=new Geometry.Arc(CurveOf(road));float width=0;
  if(EntityManager.HasComponent<Composition>(road)){var cp=EntityManager.GetComponentData<Composition>(road).m_Edge;
   if(EntityManager.HasBuffer<NetCompositionLane>(cp))foreach(var lane in EntityManager.GetBuffer<NetCompositionLane>(cp,true)){
    if((lane.m_Flags&LaneFlags.Road)==0||(lane.m_Flags&(LaneFlags.Master|LaneFlags.Secondary|LaneFlags.Parking))!=0)continue;
    float half=EntityManager.HasComponent<NetLaneData>(lane.m_Lane)?EntityManager.GetComponentData<NetLaneData>(lane.m_Lane).m_Width*.5f:1.5f;width=math.max(width,math.abs(lane.m_Position.x)+half);
   }
  }
  if(width<1)return false;
  float from=math.min(arc.Length*.4f,width+CrosswalkStylePolicy.Width((int)S.CrosswalkBrush)*.5f);
  float at=start?from:arc.Length-from;var p=arc.At(at);var left=Geometry.Left(Geometry.Tangent(arc.Curve,arc.T(at)));
  curve=Geometry.Line(p-left*width,p+left*width);return true;
 }
}}
