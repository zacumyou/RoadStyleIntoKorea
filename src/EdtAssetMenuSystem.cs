using System;
using Colossal.Serialization.Entities;
using System.Collections.Generic;
using Game;
using Game.Common;
using Game.Prefabs;
using Game.SceneFlow;
using Game.Serialization;
using Unity.Collections;
using Unity.Entities;
namespace RoadStyleIntoKorea {
 // EDT owns these runtime categories. Never create or serialize copies of them.
 public partial class EdtAssetMenuSystem : GameSystemBase {
  readonly Dictionary<string,int> priorities=EdtAssetMenuPolicy.Create();
  readonly Dictionary<string,Entity> targets=new Dictionary<string,Entity>(StringComparer.Ordinal);
  readonly HashSet<Entity> laneMembers=new HashSet<Entity>(),laneRequirements=new HashSet<Entity>(),decalMembers=new HashSet<Entity>(),decalRequirements=new HashSet<Entity>();
  void ReadMembers(Entity group,HashSet<Entity> members,HashSet<Entity> requirements){
   members.Clear();requirements.Clear();
   foreach(var item in EntityManager.GetBuffer<UIGroupElement>(group,true))members.Add(item.m_Prefab);
   foreach(var item in EntityManager.GetBuffer<UnlockRequirement>(group,true))requirements.Add(item.m_Prefab);
  }
  EntityQuery query;float nextCheck,nextScan;string lastStatus;bool ready;
  protected override void OnCreate(){base.OnCreate();query=GetEntityQuery(ComponentType.ReadOnly<PrefabData>());}
  protected override void OnGamePreload(Purpose purpose,GameMode mode){ready=false;targets.Clear();nextCheck=nextScan=0;lastStatus=null;base.OnGamePreload(purpose,mode);}
  protected override void OnGameLoadingComplete(Purpose purpose,GameMode mode){base.OnGameLoadingComplete(purpose,mode);ready=mode==GameMode.Game||mode==GameMode.Editor;}
  bool Category(PrefabSystem ps,string name,out UIGroupPrefab group,out Entity entity){
   group=null;entity=Entity.Null;
   if(!ps.TryGetPrefab(new PrefabID("UIAssetChildCategoryPrefab",name),out var p)||p==null||p.GetType().FullName!="ExtraLib.Prefabs.UIAssetChildCategoryPrefab")return false;
   group=p as UIGroupPrefab;
   return group!=null&&ps.TryGetEntity(group,out entity)&&EntityManager.Exists(entity)&&!EntityManager.HasComponent<Created>(entity)&&EntityManager.HasBuffer<UIGroupElement>(entity)&&EntityManager.HasBuffer<UnlockRequirement>(entity);
  }
  protected override void OnUpdate(){
   float now=UnityEngine.Time.realtimeSinceStartup;if(!ready||now<nextCheck)return;nextCheck=now+2;
   var ps=World.GetExistingSystemManaged<PrefabSystem>();if(ps==null)return;
   bool lanesReady=Category(ps,"RoadMarking NetLanes",out var laneGroup,out var laneEntity);
   bool decalsReady=Category(ps,"RoadMarkings Decals",out var decalGroup,out var decalEntity);
   if(!lanesReady&&!decalsReady){Report("waiting for EDT categories; no asset groups changed");return;}
   if(targets.Count<priorities.Count&&now>=nextScan){
    nextScan=now+10;
    using(var entities=query.ToEntityArray(Allocator.Temp))foreach(var entity in entities){
     if(EntityManager.HasComponent<Created>(entity)||EntityManager.HasComponent<Deleted>(entity))continue;
     if(ps.TryGetPrefab<PrefabBase>(entity,out var p)&&p!=null&&priorities.ContainsKey(p.name)&&
       (p is NetLaneGeometryPrefab||p is StaticObjectPrefab))targets[p.name]=entity;
    }
   }
   if(lanesReady)ReadMembers(laneEntity,laneMembers,laneRequirements);
   if(decalsReady)ReadMembers(decalEntity,decalMembers,decalRequirements);
   int lanes=0,decals=0,changed=0;var stale=new List<string>();
   foreach(var target in targets){
    var entity=target.Value;if(!EntityManager.Exists(entity)||!ps.TryGetPrefab<PrefabBase>(entity,out var prefab)||prefab==null||prefab.name!=target.Key){stale.Add(target.Key);continue;}
    bool lane=prefab is NetLaneGeometryPrefab;
    if(lane?!lanesReady:!decalsReady)continue;
    if(lane?!EntityManager.HasComponent<NetLaneGeometryData>(entity):!EntityManager.HasComponent<ObjectData>(entity))continue;
    if(EntityManager.HasComponent<Created>(entity)||EntityManager.HasComponent<Deleted>(entity))continue;
    var group=lane?laneGroup:decalGroup;var groupEntity=lane?laneEntity:decalEntity;
    if(Bind(prefab,entity,group,groupEntity,priorities[target.Key],!lane,lane?laneMembers:decalMembers,lane?laneRequirements:decalRequirements))changed++;
    if(lane)lanes++;else decals++;
   }
   foreach(var name in stale)targets.Remove(name);
   Report($"netlanes={lanes}/70 decals={decals}/60 loaded={targets.Count}/130 groups={lanesReady}/{decalsReady}");
  }
  bool Bind(PrefabBase prefab,Entity entity,UIGroupPrefab group,Entity groupEntity,int priority,bool decal,HashSet<Entity> members,HashSet<Entity> unlocks){
   bool changed=false;
   // EDT uses PlaceableObject for native decals as well; preserve existing costs/flags.
   if(decal&&!EntityManager.HasComponent<PlaceableObjectData>(entity)){
    var placeable=prefab.GetComponent<PlaceableObject>();
    if(placeable==null){placeable=prefab.AddComponent<PlaceableObject>();placeable.m_ConstructionCost=0;placeable.m_XPReward=0;}
    EntityManager.AddComponentData(entity,default(PlaceableObjectData));
    if(!EntityManager.HasComponent<PlaceableInfoviewItem>(entity))EntityManager.AddComponent<PlaceableInfoviewItem>(entity);
    placeable.Initialize(EntityManager,entity);changed=true;
   }
   var ui=prefab.GetComponent<UIObject>();if(ui==null)ui=prefab.AddComponent<UIObject>();
   var data=EntityManager.HasComponent<UIObjectData>(entity)?EntityManager.GetComponentData<UIObjectData>(entity):default;
   if(data.m_Group!=groupEntity){RemoveFrom(data.m_Group,entity);changed=true;}
   if(ui.m_Group!=group||ui.m_Priority!=priority||!ui.active||ui.m_IsDebugObject)changed=true;
   ui.m_Group=group;ui.m_Priority=priority;ui.active=true;ui.m_IsDebugObject=false;
   if(!EntityManager.HasComponent<UIObjectData>(entity))EntityManager.AddComponent<UIObjectData>(entity);
   if(data.m_Group!=groupEntity||data.m_Priority!=priority||changed)EntityManager.SetComponentData(entity,new UIObjectData{m_Group=groupEntity,m_Priority=priority});
   if(members.Add(entity)){EntityManager.GetBuffer<UIGroupElement>(groupEntity).Add(new UIGroupElement(entity));changed=true;}
   if(unlocks.Add(entity)){EntityManager.GetBuffer<UnlockRequirement>(groupEntity).Add(new UnlockRequirement(entity,(UnlockFlags)2));changed=true;}
   return changed;
  }
  void RemoveFrom(Entity group,Entity entity){
   if(group==Entity.Null||!EntityManager.Exists(group))return;
   if(EntityManager.HasBuffer<UIGroupElement>(group)){var b=EntityManager.GetBuffer<UIGroupElement>(group);for(int i=b.Length-1;i>=0;i--)if(b[i].m_Prefab==entity)b.RemoveAt(i);}
   if(EntityManager.HasBuffer<UnlockRequirement>(group)){var b=EntityManager.GetBuffer<UnlockRequirement>(group);for(int i=b.Length-1;i>=0;i--)if(b[i].m_Prefab==entity)b.RemoveAt(i);}
  }
  void Report(string status){if(status==lastStatus)return;lastStatus=status;}
 }
}
