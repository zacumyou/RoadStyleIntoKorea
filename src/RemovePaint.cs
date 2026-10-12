using System.Collections.Generic;
using Colossal.Serialization.Entities;
using Game.Common;
using Game.Net;
using Unity.Entities;
using Unity.Mathematics;
namespace RoadStyleIntoKorea { 
 public struct RemovedPaintChoice:IBufferElementData,ISerializable {
  public Entity Prefab;public float3 Position;
  public void Serialize<T>(T w) where T:IWriter{w.Write(1);w.Write(Prefab);w.Write(Position);}
  public void Deserialize<T>(T r) where T:IReader{r.Read(out int v);r.Read(out Prefab);r.Read(out Position);if(v!=1||!math.all(math.isfinite(Position)))Prefab=Entity.Null;}
 }
 public partial class StyleSystem {
  readonly HashSet<Entity> removedPaint=new HashSet<Entity>();
  float3 RemovalPosition(Entity owner,float3 world){
   if(EntityManager.HasComponent<Curve>(owner)){
    var arc=new Geometry.Arc(CurveOf(owner));float t=arc.Nearest(world);var p=Geometry.Position(arc.Curve,t);return new float3(t,math.dot(world-p,Geometry.Left(Geometry.Tangent(arc.Curve,t))),world.y-p.y);
   }
   return EntityManager.HasComponent<Node>(owner)?world-EntityManager.GetComponentData<Node>(owner).m_Position:world;
  }
  bool RemovalMatch(Entity owner,float3 a,float3 b)=>RemovalPolicy.Match(EntityManager.HasComponent<Curve>(owner),a,b);
  void ApplyPaintRemoval(Entity owner,Entity paint){
   if(!EntityManager.HasBuffer<RemovedPaintChoice>(owner))return;
   var p=EntityManager.HasComponent<Curve>(paint)?Geometry.Position(CurveOf(paint),.5f):EntityManager.GetComponentData<Game.Objects.Transform>(paint).m_Position;
   var local=RemovalPosition(owner,p);var prefab=Prefab(paint);
   foreach(var choice in EntityManager.GetBuffer<RemovedPaintChoice>(owner,true))if(choice.Prefab==prefab&&RemovalMatch(owner,choice.Position,local)){
    removedPaint.Add(paint);EntityManager.AddComponent<Game.Tools.Hidden>(paint);DirtyBatch(paint);break;
   }
  }
  internal void EnforceAllVisibility(){if(saving||!ready)return;EnforceVisibility();EnforceRemovedPaint();CheckPaintRegistration();} void EnforceRemovedPaint(){foreach(var e in removedPaint)if(Live(e)&&!EntityManager.HasComponent<Game.Tools.Hidden>(e)){EntityManager.CompleteAllTrackedJobs();EntityManager.AddComponent<Game.Tools.Hidden>(e);DirtyBatch(e);}}
  internal bool RemovePaintTool(Entity target,float3 hit,Game.Rendering.OverlayRenderSystem.Buffer draw,bool apply,bool reset){
   var owners=new HashSet<Entity>{target};
   if(IsRoad(target)){var edge=EntityManager.GetComponentData<Edge>(target);owners.Add(edge.m_Start);owners.Add(edge.m_End);}
   else if(SafeNode(target))foreach(var e in EntityManager.GetBuffer<ConnectedEdge>(target,true))owners.Add(e.m_Edge);
   Entity picked=Entity.Null,owner=Entity.Null;float best=float.MaxValue;float3 center=default;Colossal.Mathematics.Bezier4x3 preview=default;float width=0;
   foreach(var o in owners)foreach(var map in new[]{records,approachRecords,approachStopRecords,manualRecords,guideRecords})if(map.TryGetValue(o,out var record))foreach(var e in record.Paint){
    if(!Live(e)||(!reset&&removedPaint.Contains(e))||reset&&!removedPaint.Contains(e))continue;
    var prefab=Prefab(e);var name=assets.Name(prefab);
    if(name.Contains("Gutter")||name.Contains("BrickPed"))continue;
    float d;float3 p;Colossal.Mathematics.Bezier4x3 curve;float w;
    if(EntityManager.HasComponent<Curve>(e)){
     curve=CurveOf(e);var arc=new Geometry.Arc(curve);p=Geometry.Position(curve,.5f);var nearest=Geometry.Position(curve,arc.Nearest(hit));w=EntityManager.HasComponent<Game.Prefabs.NetLaneData>(prefab)?EntityManager.GetComponentData<Game.Prefabs.NetLaneData>(prefab).m_Width:.3f;
     d=math.distance(nearest.xz,hit.xz);if(d>math.max(.25f,w*.5f)||math.abs(nearest.y-hit.y)>1.5f)continue;
    }else if(EntityManager.HasComponent<Game.Objects.Transform>(e)){
     var tr=EntityManager.GetComponentData<Game.Objects.Transform>(e);p=tr.m_Position;var f=math.mul(tr.m_Rotation,new float3(0,0,1));var delta=hit-p;float half=DecalHalfLength(prefab,2f);w=2.3f;
     if(math.abs(delta.y)>1.5f||math.abs(math.dot(delta,f))>half+.25f||math.abs(math.dot(delta,Geometry.Left(f)))>w*.5f)continue;
     d=math.length(delta.xz)*.1f;curve=Geometry.Line(p-f*half,p+f*half);
    }else continue;
    if(d>=best)continue;best=d;picked=e;owner=o;center=p;preview=curve;width=w;
   }
   if(picked==Entity.Null)return false;
   draw.DrawCurve(reset?new UnityEngine.Color(.2f,.7f,1,.65f):new UnityEngine.Color(1,.25f,.3f,.65f),preview,math.max(.2f,width));
   if((apply||reset)&&S.Enabled){var entity=picked;var selectedOwner=owner;var prefab=Prefab(entity);var position=RemovalPosition(owner,center);
    localStyleCommands.Enqueue(()=>{
     if(!Live(selectedOwner))return;
     if(!EntityManager.HasBuffer<RemovedPaintChoice>(selectedOwner))EntityManager.AddBuffer<RemovedPaintChoice>(selectedOwner);
     var choices=EntityManager.GetBuffer<RemovedPaintChoice>(selectedOwner);int index=-1;for(int i=0;i<choices.Length;i++)if(choices[i].Prefab==prefab&&RemovalMatch(selectedOwner,choices[i].Position,position)){index=i;break;}
     if(reset){if(index>=0)choices.RemoveAt(index);removedPaint.Remove(entity);if(Live(entity)){EntityManager.RemoveComponent<Game.Tools.Hidden>(entity);DirtyBatch(entity);}}
     else{if(index<0)choices.Add(new RemovedPaintChoice{Prefab=prefab,Position=position});if(Live(entity)){removedPaint.Add(entity);EntityManager.AddComponent<Game.Tools.Hidden>(entity);DirtyBatch(entity);}}
     Status=reset?"개별 표시 복원 / Marking restored":"하나씩 표시 제거 / Marking removed";
    });targetedActive=true;
   }
   return true;
  }
 }
}

