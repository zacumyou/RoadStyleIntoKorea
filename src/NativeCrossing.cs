using System.Collections.Generic;
using Colossal.Serialization.Entities;
using Game;
using Game.Common;
using Game.Net;
using Game.Prefabs;
using Unity.Entities;
namespace RoadStyleIntoKorea {
 // Store only the two crosswalk bits we own; preserve all unrelated upgrades.
 public struct NativeCrossingChoice:IComponentData,ISerializable {
  public int Owned,Left,Right;
  public void Serialize<T>(T w) where T:IWriter {w.Write(1);w.Write(Owned);w.Write(Left);w.Write(Right);}
  public void Deserialize<T>(T r) where T:IReader {r.Read(out int version);r.Read(out Owned);r.Read(out Left);r.Read(out Right);if(version!=1)Owned=0;}
 }
 public partial class NativeCrossingSystem:GameSystemBase {
  protected override void OnUpdate()=>World.GetExistingSystemManaged<StyleSystem>()?.ApplyNativeCrossingRequests();
 }
 public partial class StyleSystem {
  const CompositionFlags.Side CrossingMask=CompositionFlags.Side.AddCrosswalk|CompositionFlags.Side.RemoveCrosswalk;
  struct NativeCrossingRequest {internal Entity Edge;internal bool Start,Reset;internal float Due;}
  readonly List<NativeCrossingRequest> nativeCrossingRequests=new List<NativeCrossingRequest>();
  readonly List<NativeCrossingRequest> nativeCrossingWaiting=new List<NativeCrossingRequest>();
  void CheckNativeCrossings(){{
   float now=UnityEngine.Time.realtimeSinceStartup;
   for(int i=nativeCrossingWaiting.Count-1;i>=0;i--){
    var request=nativeCrossingWaiting[i];if(now<request.Due+.25f)continue;
    if(!IsRoad(request.Edge)){nativeCrossingWaiting.RemoveAt(i);continue;}
    bool found=false;foreach(var lane in NearbyCrossings(request.Edge))if(CrossingRoad(lane,out bool start)==request.Edge&&start==request.Start){found=true;break;}
    if(!found&&now<request.Due+3)continue;
    nativeCrossingWaiting.RemoveAt(i);
    if(found){var ends=EntityManager.GetComponentData<Edge>(request.Edge);crossingPlans.Clear();crossingSizes.Clear();RefreshCrosswalkDependents(request.Edge,request.Start?ends.m_Start:ends.m_End);}
    else Status="게임이 횡단보도를 생성하지 못했습니다. 보행 연결·도로 구성을 확인하세요 / No native crossing generated; check pedestrian connections and road configuration";
   }
  }}
  bool NativeCrossingSide(Entity edge,bool start,out bool left){
   left=false;if(!IsRoad(edge)||!EntityManager.HasComponent<Composition>(edge))return false;
   var cp=EntityManager.GetComponentData<Composition>(edge);
   var node=start?cp.m_StartNode:cp.m_EndNode;
   if(!EntityManager.HasComponent<NetCompositionData>(node))return false;
   // Same endpoint mapping as vanilla NetToolSystem.AddControlPoints.
   left=(EntityManager.GetComponentData<NetCompositionData>(node).m_Flags.m_General&CompositionFlags.General.Invert)!=0;
   return true;
  }
  bool QueueNativeCrossing(Entity edge,bool start,bool reset){
   if(!NativeCrossingSide(edge,start,out bool left))return false;
   if(reset&&!EntityManager.HasComponent<NativeCrossingChoice>(edge))return true;
   var prefab=Prefab(edge);
   if(!reset&&(!EntityManager.HasComponent<NetData>(prefab)||(EntityManager.GetComponentData<NetData>(prefab).m_SideFlagMask&CompositionFlags.Side.AddCrosswalk)==0)){
    Status="이 도로 템플릿은 횡단보도 추가를 지원하지 않습니다 / Road template does not support adding crossings";return false;
   }
   nativeCrossingRequests.RemoveAll(x=>x.Edge==edge&&x.Start==start);
   nativeCrossingRequests.Add(new NativeCrossingRequest{Edge=edge,Start=start,Reset=reset});return true;
  }
  internal void ApplyNativeCrossingRequests(){
   if(nativeCrossingRequests.Count==0)return;
   EntityManager.CompleteAllTrackedJobs();
   var requests=nativeCrossingRequests.ToArray();nativeCrossingRequests.Clear();
   foreach(var request in requests){
    var edge=request.Edge;if(!NativeCrossingSide(edge,request.Start,out bool left))continue;
    var upgraded=EntityManager.HasComponent<Upgraded>(edge)?EntityManager.GetComponentData<Upgraded>(edge):default;
    var saved=EntityManager.HasComponent<NativeCrossingChoice>(edge)?EntityManager.GetComponentData<NativeCrossingChoice>(edge):default;
    int bit=left?1:2;var flags=left?upgraded.m_Flags.m_Left:upgraded.m_Flags.m_Right;var previousFlags=flags;
    if(request.Reset){
     if((saved.Owned&bit)==0)continue;
     // A subsequent external edit wins over our earlier creation request.
     flags=NativeCrossingPolicy.Restore(flags,(CompositionFlags.Side)(left?saved.Left:saved.Right));
     saved.Owned&=~bit;
    }else{
     if((saved.Owned&bit)==0){if(left)saved.Left=(int)(flags&CrossingMask);else saved.Right=(int)(flags&CrossingMask);saved.Owned|=bit;}
     flags=NativeCrossingPolicy.Add(flags);
    }
    if(left)upgraded.m_Flags.m_Left=flags;else upgraded.m_Flags.m_Right=flags;
    if(!EntityManager.HasComponent<Upgraded>(edge))EntityManager.AddComponent<Upgraded>(edge);
    EntityManager.SetComponentData(edge,upgraded);
    if(!EntityManager.HasComponent<NativeCrossingChoice>(edge))EntityManager.AddComponent<NativeCrossingChoice>(edge);
    EntityManager.SetComponentData(edge,saved);
    nativeCrossingWaiting.RemoveAll(x=>x.Edge==edge&&x.Start==request.Start);
    if(flags==previousFlags)continue; // Ownership bookkeeping alone does not require native regeneration.
    if(!request.Reset)nativeCrossingWaiting.Add(new NativeCrossingRequest{Edge=edge,Start=request.Start,Due=UnityEngine.Time.realtimeSinceStartup});
    var ends=EntityManager.GetComponentData<Edge>(edge);
    foreach(var owner in new[]{edge,request.Start?ends.m_Start:ends.m_End})if(Live(owner)&&!EntityManager.HasComponent<Updated>(owner))EntityManager.AddComponent<Updated>(owner);
    RefreshCrosswalkDependents(edge,request.Start?ends.m_Start:ends.m_End);
   }
  }
 }
}
