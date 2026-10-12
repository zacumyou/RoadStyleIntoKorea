using System.Collections.Generic;
using Game;
using Game.Common;
using Game.Net;
using Unity.Collections;
using Unity.Entities;
namespace RoadStyleIntoKorea {
 public partial class StyleSystem {
  void DetachPaint(Entity entity){
   if(!EntityManager.Exists(entity)||!EntityManager.HasComponent<GeneratedPaint>(entity)||!EntityManager.HasComponent<Owner>(entity))return;
   var owner=EntityManager.GetComponentData<Owner>(entity).m_Owner;
   if(EntityManager.HasBuffer<SubLane>(owner)){var lanes=EntityManager.GetBuffer<SubLane>(owner);for(int i=lanes.Length-1;i>=0;i--)if(lanes[i].m_SubLane==entity)lanes.RemoveAt(i);}
   if(EntityManager.HasBuffer<Game.Objects.SubObject>(owner)){var objects=EntityManager.GetBuffer<Game.Objects.SubObject>(owner);for(int i=objects.Length-1;i>=0;i--)if(objects[i].m_SubObject==entity)objects.RemoveAt(i);}
  }
  void RetirePaint(Entity entity){
   if(!EntityManager.Exists(entity))return;
   removedPaint.Remove(entity);suppressedWarnings.Remove(entity);DetachPaint(entity);
   if(!EntityManager.HasComponent<Deleted>(entity))EntityManager.AddComponent<Deleted>(entity);
  }
 }
}

