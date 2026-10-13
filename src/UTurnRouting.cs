using System;using System.Collections.Generic;
using Colossal.Serialization.Entities;using Game;using Game.Common;using Game.Net;using Game.Pathfind;using Game.Prefabs;using Unity.Collections;using Unity.Entities;using Unity.Mathematics;
namespace RoadStyleIntoKorea {
// A simulation lane persists normally: vehicles may reference it in a saved path.
public struct ManualUTurnRoute:IComponentData,ISerializable {
 public Entity Road,Source,Target;public float Position,Length;public bool Reverse;
 public void Serialize<T>(T w) where T:IWriter{w.Write(1);w.Write(Road);w.Write(Source);w.Write(Target);w.Write(Position);w.Write(Length);w.Write(Reverse);}
 public void Deserialize<T>(T r) where T:IReader{r.Read(out int v);r.Read(out Road);r.Read(out Source);r.Read(out Target);r.Read(out Position);r.Read(out Length);r.Read(out Reverse);}
}
// Compatibility only: old saves can contain lanes produced by 0.1.44-0.1.48.
// No simulation lane is created or repositioned for a painted U-turn zone.
public partial class StyleSystem {
 EntityQuery legacyUTurnRoutes;bool legacyRouteQuery;
 internal void UpdateManualRoutes(){
  if(!ready||saving)return;
  if(!legacyRouteQuery){legacyUTurnRoutes=GetEntityQuery(ComponentType.ReadOnly<ManualUTurnRoute>());legacyRouteQuery=true;}
  if(legacyUTurnRoutes.IsEmptyIgnoreFilter)return;
  EntityManager.CompleteAllTrackedJobs();
  using(var routes=legacyUTurnRoutes.ToEntityArray(Allocator.Temp))foreach(var route in routes){
   if(!EntityManager.Exists(route)||EntityManager.HasComponent<Deleted>(route)||EntityManager.HasComponent<Created>(route))continue;
   // Native deletion updates pathfinding and vehicle references; do not destroy directly.
   EntityManager.AddComponent<Deleted>(route);
   if(!EntityManager.HasComponent<PathfindUpdated>(route))EntityManager.AddComponent<PathfindUpdated>(route);
  }
 }
}
public partial class UTurnRoutingSystem:GameSystemBase {protected override void OnUpdate()=>World.GetExistingSystemManaged<StyleSystem>()?.UpdateManualRoutes();}
}
