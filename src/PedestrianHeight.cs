using System.Collections.Generic;
using Game.Common;
using Game.Net;
using Game.Pathfind;
using Unity.Entities;
using Unity.Mathematics;
namespace RoadStyleIntoKorea {public partial class StyleSystem {
 sealed class WalkCurve {internal Curve Base,Applied;internal Entity Owner;}
 readonly Dictionary<Entity,WalkCurve> walkCurves=new Dictionary<Entity,WalkCurve>();
 readonly Dictionary<Entity,Curve> saveWalkCurves=new Dictionary<Entity,Curve>();
 readonly HashSet<Entity> walkDirty=new HashSet<Entity>();
 void QueueWalkHeight(Entity owner){if(owner!=Entity.Null)walkDirty.Add(owner);}
 Curve WalkBase(Entity lane,Curve current){
  if(walkCurves.TryGetValue(lane,out var saved)){
   if(Geometry.SameCurve(current.m_Bezier,saved.Applied.m_Bezier))return saved.Base;
   walkCurves.Remove(lane); // Native reconstruction or another mod supersedes us.
  }
  return current;
 }
 void RestoreWalkLane(Entity lane,bool requeue=true){
  if(!walkCurves.TryGetValue(lane,out var saved))return;
  walkCurves.Remove(lane);if(requeue)QueueWalkHeight(saved.Owner);
  if(Live(lane)&&EntityManager.HasComponent<Curve>(lane)&&Geometry.SameCurve(EntityManager.GetComponentData<Curve>(lane).m_Bezier,saved.Applied.m_Bezier)){
   EntityManager.SetComponentData(lane,saved.Base);MarkTrafficUpdated(lane);
  }
 }
 void RestoreWalkHeights(){
  foreach(var lane in new List<Entity>(walkCurves.Keys))RestoreWalkLane(lane);
  walkDirty.Clear();saveWalkCurves.Clear();
 }
 void SuspendWalkHeights(){
  saveWalkCurves.Clear();
  foreach(var pair in walkCurves)if(Live(pair.Key)&&Geometry.SameCurve(EntityManager.GetComponentData<Curve>(pair.Key).m_Bezier,pair.Value.Applied.m_Bezier)){
   saveWalkCurves[pair.Key]=pair.Value.Applied;EntityManager.SetComponentData(pair.Key,pair.Value.Base);
  }
 }
 void ResumeWalkHeights(){foreach(var pair in saveWalkCurves)if(Live(pair.Key)&&walkCurves.TryGetValue(pair.Key,out var saved)&&Geometry.SameCurve(EntityManager.GetComponentData<Curve>(pair.Key).m_Bezier,saved.Base.m_Bezier))EntityManager.SetComponentData(pair.Key,pair.Value);saveWalkCurves.Clear();}
 internal void ApplyWalkHeights(){
  if(saving||!ready||S==null)return;
  if(!S.Enabled||!S.Paving||restoreRequested||restoring){if(walkCurves.Count>0){EntityManager.CompleteAllTrackedJobs();RestoreWalkHeights();}return;}
  if(walkDirty.Count==0)return;
  Entity owner=Entity.Null;foreach(var e in walkDirty){owner=e;break;}
  var owners=new HashSet<Entity>{owner};
  if(Live(owner))foreach(var e in DirectRefreshNeighbors(owner))owners.Add(e);
  // Include both sides of road/node seams without a city-wide lane query.
  foreach(var e in new List<Entity>(owners))if(Live(e)&&EntityManager.HasBuffer<ConnectedEdge>(e))
   foreach(var c in EntityManager.GetBuffer<ConnectedEdge>(e,true))if(IsRoad(c.m_Edge))owners.Add(c.m_Edge);
  foreach(var e in owners)if(pending.Contains(e)||curbDirty.Contains(e)||EntityManager.HasComponent<Game.Common.Updated>(e))return;
  walkDirty.Remove(owner);EntityManager.CompleteAllTrackedJobs();
  var lanes=new HashSet<Entity>();
  foreach(var e in owners){
   if(!Live(e)||!EntityManager.HasBuffer<SubLane>(e))continue;
   foreach(var sub in EntityManager.GetBuffer<SubLane>(e,true))if(Live(sub.m_SubLane)&&EntityManager.HasComponent<PedestrianLane>(sub.m_SubLane)&&!EntityManager.HasComponent<MasterLane>(sub.m_SubLane)&&EntityManager.HasComponent<Curve>(sub.m_SubLane))lanes.Add(sub.m_SubLane);
  }
  var surfaceCache=new Dictionary<Entity,List<SidewalkFootprint>>();
  List<SidewalkFootprint> Surfaces(Entity lane){
   var root=EntityManager.HasComponent<Owner>(lane)?EntityManager.GetComponentData<Owner>(lane).m_Owner:owner;
   if(surfaceCache.TryGetValue(root,out var cached))return cached;
   var context=new HashSet<Entity>{root};if(Live(root))foreach(var neighbor in DirectRefreshNeighbors(root))context.Add(neighbor);
   foreach(var e in new List<Entity>(context))if(Live(e)&&EntityManager.HasBuffer<ConnectedEdge>(e))foreach(var edge in EntityManager.GetBuffer<ConnectedEdge>(e,true))if(IsRoad(edge.m_Edge))context.Add(edge.m_Edge);
   var result=new List<SidewalkFootprint>();foreach(var e in context)if(records.TryGetValue(e,out var record)&&record.WalkSurface!=null)result.Add(record.WalkSurface);
   surfaceCache[root]=result;return result;
  }
  int changed=0,rejected=0;
  var bases=new Dictionary<Entity,Curve>();var targets=new Dictionary<Entity,Curve>();
  var links=new Dictionary<PathNode,List<Entity>>();var blocked=new WorkQueue<Entity>();var omitted=new HashSet<Entity>();
  foreach(var lane in lanes){
   // The same lane must sample the same surfaces regardless of which adjacent
   // road scheduled this work. Otherwise two owners alternately raise/restore it.
   var surfaces=Surfaces(lane);
   float? Height(float3 p){float best=float.MaxValue,value=0;foreach(var surface in surfaces)if(surface.TryHeight(p,out float y)&&math.abs(y-p.y)<best){best=math.abs(y-p.y);value=y;}return best<float.MaxValue?(float?)value:null;}
   var source=WalkBase(lane,EntityManager.GetComponentData<Curve>(lane));bases[lane]=source;
   if(EntityManager.HasComponent<Lane>(lane)){
    var link=EntityManager.GetComponentData<Lane>(lane);
    foreach(var key in new[]{link.m_StartNode,link.m_EndNode}){
     if(key.Equals(default(PathNode)))continue;
     if(!links.TryGetValue(key,out var list))links[key]=list=new List<Entity>();list.Add(lane);
    }
   }
   if(!PedestrianHeightPolicy.Fit(source.m_Bezier,Height,out var target)){
    omitted.Add(lane);blocked.Enqueue(lane);rejected++;continue;
   }
   float length=new Geometry.Arc(target).Length;
   if(!TrafficCurvePolicy.Valid(target,length)){omitted.Add(lane);blocked.Enqueue(lane);rejected++;continue;}
   targets[lane]=new Curve{m_Bezier=target,m_Length=length};
  }
  // If a complicated surface cannot fit one native cubic, do not leave its
  // adjacent lane raised at the same path node. Propagate rollback locally.
  while(blocked.Count>0){
   var lane=blocked.Dequeue();if(!EntityManager.HasComponent<Lane>(lane))continue;
   var link=EntityManager.GetComponentData<Lane>(lane);
   foreach(var key in new[]{link.m_StartNode,link.m_EndNode})if(links.TryGetValue(key,out var neighbors))foreach(var other in neighbors){
    if(omitted.Contains(other)||!targets.TryGetValue(other,out var target))continue;
    var otherLink=EntityManager.GetComponentData<Lane>(other);bool start=otherLink.m_StartNode.Equals(key);
    float delta=start?target.m_Bezier.a.y-bases[other].m_Bezier.a.y:target.m_Bezier.d.y-bases[other].m_Bezier.d.y;
    if(math.abs(delta)>.002f&&omitted.Add(other))blocked.Enqueue(other);
   }
  }
  foreach(var lane in lanes){
   if(omitted.Contains(lane)){RestoreWalkLane(lane,false);continue;}
   var applied=targets[lane];var source=bases[lane];var current=EntityManager.GetComponentData<Curve>(lane);
   if(Geometry.SameCurve(applied.m_Bezier,current.m_Bezier))continue;
   if(Geometry.SameCurve(applied.m_Bezier,source.m_Bezier)){RestoreWalkLane(lane,false);continue;}
   walkCurves[lane]=new WalkCurve{Base=source,Applied=applied,Owner=EntityManager.HasComponent<Owner>(lane)?EntityManager.GetComponentData<Owner>(lane).m_Owner:owner};
   EntityManager.SetComponentData(lane,applied);MarkTrafficUpdated(lane);changed++;
  }   }
}}



