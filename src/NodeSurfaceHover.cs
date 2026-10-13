using System.Collections.Generic;
using Game.Net;
using Unity.Entities;
using Unity.Mathematics;
namespace RoadStyleIntoKorea {public partial class StyleSystem {
 sealed class SurfaceHoverCandidate {internal Entity Edge;internal Geometry.Arc Arc,PickArc;}
 sealed class SurfaceHoverStamp {internal Entity Edge;internal EdgeGeometry Shape;internal Composition Composition;internal EdgeNodeGeometry Node;internal bool Start;}
 readonly List<SurfaceHoverCandidate> nodeSurfaceCandidates=new List<SurfaceHoverCandidate>();
 readonly List<SurfaceHoverStamp> nodeSurfaceStamps=new List<SurfaceHoverStamp>();
 Entity nodeSurfaceTarget;int nodeSurfaceRevision=-1,nodeSurfaceMode,nodeSurfaceConnections;
 bool NodeSurfaceCacheValid(Entity node,int mode){
  if(nodeSurfaceTarget!=node||nodeSurfaceMode!=mode||nodeSurfaceRevision!=toolRevision||!EntityManager.HasBuffer<ConnectedEdge>(node)||EntityManager.GetBuffer<ConnectedEdge>(node,true).Length!=nodeSurfaceConnections)return false;
  foreach(var stamp in nodeSurfaceStamps){
   if(!IsRoad(stamp.Edge)||!EntityManager.HasComponent<EdgeGeometry>(stamp.Edge)||!EntityManager.HasComponent<Composition>(stamp.Edge))return false;
   var shape=EntityManager.GetComponentData<EdgeGeometry>(stamp.Edge);var composition=EntityManager.GetComponentData<Composition>(stamp.Edge);
   if(!SameSegment(shape.m_Start,stamp.Shape.m_Start)||!SameSegment(shape.m_End,stamp.Shape.m_End)||composition.m_Edge!=stamp.Composition.m_Edge||composition.m_StartNode!=stamp.Composition.m_StartNode||composition.m_EndNode!=stamp.Composition.m_EndNode)return false;
   if(stamp.Start&&!EntityManager.HasComponent<StartNodeGeometry>(stamp.Edge)||!stamp.Start&&!EntityManager.HasComponent<EndNodeGeometry>(stamp.Edge))return false;
   var end=stamp.Start?EntityManager.GetComponentData<StartNodeGeometry>(stamp.Edge).m_Geometry:EntityManager.GetComponentData<EndNodeGeometry>(stamp.Edge).m_Geometry;
   if(!SameNode(end,stamp.Node)||!math.all(end.m_SyncVertexTargetsLeft==stamp.Node.m_SyncVertexTargetsLeft)||!math.all(end.m_SyncVertexTargetsRight==stamp.Node.m_SyncVertexTargetsRight))return false;
  }
  foreach(var edge in EntityManager.GetBuffer<ConnectedEdge>(node,true)){
   if(!IsRoad(edge.m_Edge)||!EntityManager.HasComponent<EdgeGeometry>(edge.m_Edge)||!EntityManager.HasComponent<Composition>(edge.m_Edge))continue;
   bool start=EntityManager.GetComponentData<Edge>(edge.m_Edge).m_Start==node;
   if(start&&!EntityManager.HasComponent<StartNodeGeometry>(edge.m_Edge)||!start&&!EntityManager.HasComponent<EndNodeGeometry>(edge.m_Edge))continue;
   bool found=false;foreach(var stamp in nodeSurfaceStamps)if(stamp.Edge==edge.m_Edge){found=true;break;}if(!found)return false;
  }
  return true;
 }
 // Called only for a raycast node, never for an arbitrary point in the city.
 internal bool ResolveNodeSurfaceTarget(Entity node,float3 hit,int mode,out Entity edge,out float3 pickHit){
  edge=Entity.Null;pickHit=hit;if(!Live(node)||!EntityManager.HasBuffer<ConnectedEdge>(node))return false;
  if(!NodeSurfaceCacheValid(node,mode)){
   nodeSurfaceTarget=node;nodeSurfaceMode=mode;nodeSurfaceRevision=toolRevision;nodeSurfaceConnections=EntityManager.GetBuffer<ConnectedEdge>(node,true).Length;
   nodeSurfaceCandidates.Clear();nodeSurfaceStamps.Clear();
   foreach(var connection in EntityManager.GetBuffer<ConnectedEdge>(node,true)){
    var road=connection.m_Edge;if(!IsRoad(road)||!EntityManager.HasComponent<EdgeGeometry>(road)||!EntityManager.HasComponent<Composition>(road))continue;
    bool start=EntityManager.GetComponentData<Edge>(road).m_Start==node;
    if(start&&!EntityManager.HasComponent<StartNodeGeometry>(road)||!start&&!EntityManager.HasComponent<EndNodeGeometry>(road))continue;
    var shape=EntityManager.GetComponentData<EdgeGeometry>(road);
    nodeSurfaceStamps.Add(new SurfaceHoverStamp{Edge=road,Shape=shape,Composition=EntityManager.GetComponentData<Composition>(road),Start=start,Node=start?EntityManager.GetComponentData<StartNodeGeometry>(road).m_Geometry:EntityManager.GetComponentData<EndNodeGeometry>(road).m_Geometry});
    foreach(var region in PavingRegions(road)){
     if(mode==2){GutterLimits(region,out float min,out _);if(math.abs(region.Min-min)>.01f)continue;foreach(var curve in GutterEdgeCurves(road,region)){var arc=new Geometry.Arc(curve);nodeSurfaceCandidates.Add(new SurfaceHoverCandidate{Edge=road,Arc=arc,PickArc=arc});}}
     else for(int part=0;part<2;part++){
      var pick=part==0?region.FirstArc:region.SecondArc;var arc=pick;
      if(mode==1){CurbLimits(region,out float min,out float max);bool fromA=min+max>=0;if(region.Kind==1){if(fromA)min-=.6f;else max+=.6f;}var s=part==0?shape.m_Start:shape.m_End;arc=new Geometry.Arc(CurbLaneGeometry.Curve(Between(s.m_Left,s.m_Right,.5f+min/region.RoadWidth,CurbHeight(region)),Between(s.m_Left,s.m_Right,.5f+max/region.RoadWidth,CurbHeight(region)),fromA,0));}
      nodeSurfaceCandidates.Add(new SurfaceHoverCandidate{Edge=road,Arc=arc,PickArc=pick});
     }
    }
   }
  }
  float best=float.MaxValue;
  foreach(var candidate in nodeSurfaceCandidates){
   if(!NodeSurfaceHoverPolicy.Nearer(candidate.Arc,hit,best,out var point,out float distance))continue;
   best=distance;edge=candidate.Edge;pickHit=Geometry.Position(candidate.PickArc.Curve,candidate.PickArc.Nearest(point));
  }
  return edge!=Entity.Null;
 }
}}
