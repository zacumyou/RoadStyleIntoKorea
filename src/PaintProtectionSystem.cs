using Game;
using Game.Common;
using Game.Net;
using Game.Tools;
using Unity.Collections;
using Unity.Entities;
namespace RoadStyleIntoKorea {
 // SecondaryLaneSystem owns vanilla sublanes, not our independently tracked paint.
 public partial class PaintProtectionSystem : GameSystemBase {
  EntityQuery changedOwners;
  protected override void OnCreate(){base.OnCreate();changedOwners=GetEntityQuery(new EntityQueryDesc{
   All=new[]{ComponentType.ReadOnly<SubLane>()},
   Any=new[]{ComponentType.ReadOnly<Updated>(),ComponentType.ReadOnly<Deleted>()},
   None=new[]{ComponentType.ReadOnly<Temp>()}});}
  protected override void OnUpdate(){
   if(changedOwners.IsEmptyIgnoreFilter)return;
   var styles=World.GetExistingSystemManaged<StyleSystem>();if(styles==null)return;int detached=0;
   using(var owners=changedOwners.ToEntityArray(Allocator.Temp))foreach(var owner in owners){
    if(!styles.TrackedStyleOwner(owner))continue;
    var lanes=EntityManager.GetBuffer<SubLane>(owner);
    for(int i=lanes.Length-1;i>=0;i--)if(EntityManager.HasComponent<GeneratedPaint>(lanes[i].m_SubLane)){lanes.RemoveAt(i);detached++;}
   }
  }
 }
}
