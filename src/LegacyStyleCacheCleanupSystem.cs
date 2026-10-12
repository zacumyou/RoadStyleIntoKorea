using System.Collections.Generic;
using Colossal.Serialization.Entities;
using Game;
using Game.Common;
using Game.Net;
using Game.Tools;
using Unity.Collections;
using Unity.Entities;
namespace RoadStyleIntoKorea {
 // Read compatibility only for 0.1.67-71 saves. Never adopt or create visual caches.
 // Runs before loaded entities enter prefab resolution/render registration.
 public partial class LegacyStyleCacheCleanupSystem:GameSystemBase {
  bool pending=true;
  protected override void OnGamePreload(Purpose purpose,GameMode mode){pending=true;base.OnGamePreload(purpose,mode);}
  void Remove<T>() where T:unmanaged {using(var q=EntityManager.CreateEntityQuery(ComponentType.ReadOnly<T>()))EntityManager.RemoveComponent<T>(q);}
  protected override void OnUpdate(){
   if(!pending)return;pending=false;EntityManager.CompleteAllTrackedJobs();
   var paint=new HashSet<Entity>();
   using(var q=EntityManager.CreateEntityQuery(ComponentType.ReadOnly<GeneratedPaint>()))using(var list=q.ToEntityArray(Allocator.Temp))foreach(var e in list)paint.Add(e);
   // Remove serialized references before destroying pre-render cached entities.
   using(var q=EntityManager.CreateEntityQuery(ComponentType.ReadOnly<SubLane>()))using(var list=q.ToEntityArray(Allocator.Temp))foreach(var owner in list){
    var b=EntityManager.GetBuffer<SubLane>(owner);for(int i=b.Length-1;i>=0;i--)if(paint.Contains(b[i].m_SubLane))b.RemoveAt(i);
   }
   using(var q=EntityManager.CreateEntityQuery(ComponentType.ReadOnly<Game.Objects.SubObject>()))using(var list=q.ToEntityArray(Allocator.Temp))foreach(var owner in list){
    var b=EntityManager.GetBuffer<Game.Objects.SubObject>(owner);for(int i=b.Length-1;i>=0;i--)if(paint.Contains(b[i].m_SubObject))b.RemoveAt(i);
   }
   int flows=0,hidden=0;
   using(var q=EntityManager.CreateEntityQuery(ComponentType.ReadOnly<SavedStyleFlow>(),ComponentType.ReadWrite<Curve>()))using(var list=q.ToEntityArray(Allocator.Temp))foreach(var e in list){
    var saved=EntityManager.GetComponentData<SavedStyleFlow>(e);
    if(Geometry.SameCurve(EntityManager.GetComponentData<Curve>(e).m_Bezier,saved.Applied.m_Bezier)){EntityManager.SetComponentData(e,saved.Original);flows++;}
   }
   using(var q=EntityManager.CreateEntityQuery(ComponentType.ReadOnly<OriginalPaintHidden>()))using(var list=q.ToEntityArray(Allocator.Temp))foreach(var e in list){EntityManager.RemoveComponent<Hidden>(e);if(!EntityManager.HasComponent<BatchesUpdated>(e))EntityManager.AddComponent<BatchesUpdated>(e);hidden++;}
   Remove<OriginalPaintHidden>();Remove<SavedStyleState>();Remove<SavedStyleEntry>();Remove<SavedStyleShape>();Remove<SavedStyleFlow>();
   Remove<SavedPavingMesh>();Remove<SavedPavingVertex>();Remove<SavedPavingIndex>();
   foreach(var e in paint)EntityManager.DestroyEntity(e);
  }
 }
}
