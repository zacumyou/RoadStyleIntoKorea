using System.Collections.Generic;
using Colossal.Serialization.Entities;
using Game.Common;
using Game.Net;
using Game.Tools;
using Unity.Entities;
using Unity.Mathematics;
using ObjectTransform=Game.Objects.Transform;
namespace RoadStyleIntoKorea {
 public partial class StyleSystem {
  readonly HashSet<Entity> approachPending=new HashSet<Entity>();
  readonly HashSet<Entity> approachStopsPending=new HashSet<Entity>();
  readonly Dictionary<Entity,Record> approachStopRecords=new Dictionary<Entity,Record>();
  void RequestApproachStops(Entity road){if(IsRoad(road)&&EntityManager.HasComponent<ApproachChoice>(road)&&EntityManager.GetComponentData<ApproachChoice>(road).Enabled)approachStopsPending.Add(road);}
  readonly Dictionary<Entity,Record> approachRecords=new Dictionary<Entity,Record>();
  IEnumerator<int> approachWork;Entity approachOwner;
  internal void QueueApproach(Entity road,bool reset){localStyleCommands.Enqueue(()=>ChooseApproach(road,reset));}
  internal void ChooseApproach(Entity road,bool reset,bool suppressRoadSigns=false){
   if(!IsRoad(road)||!S.Enabled)return;
   if(!reset){if(EntityManager.HasBuffer<ManualDecalChoice>(road)){var choices=EntityManager.GetBuffer<ManualDecalChoice>(road);for(int i=choices.Length-1;i>=0;i--)if(choices[i].Kind<5)choices.RemoveAt(i);}manualDirty.Add(road);}
   bool wasEnabled=EntityManager.HasComponent<ApproachChoice>(road)&&EntityManager.GetComponentData<ApproachChoice>(road).Enabled;
   if(!reset&&!wasEnabled)EnqueueCenterOnly(road);
   if(!EntityManager.HasComponent<ApproachChoice>(road))EntityManager.AddComponent<ApproachChoice>(road);
   EntityManager.SetComponentData(road,new ApproachChoice{Enabled=!reset,SuppressRoadSigns=!reset&&suppressRoadSigns});approachPending.Add(road);targetedActive=true;
  }
  void ReleaseApproachStops(Entity road){
   if(!approachStopRecords.TryGetValue(road,out var record))return;
   foreach(var e in record.Paint)if(Live(e)&&!EntityManager.HasComponent<Deleted>(e))RetirePaint(e);
   foreach(var e in record.Hidden)RestoreHidden(e,record);
   approachStopRecords.Remove(road);
  }
  void ReleaseApproach(Entity road){ReleaseApproachStops(road);
   if(!approachRecords.TryGetValue(road,out var record))return;
   foreach(var e in record.Paint)if(Live(e)&&!EntityManager.HasComponent<Deleted>(e))RetirePaint(e);
   foreach(var e in record.Hidden)RestoreHidden(e,record);
   approachRecords.Remove(road);
  }
  void ClearApproaches(){approachWork?.Dispose();approachWork=null;approachPending.Clear();approachStopsPending.Clear();foreach(var road in new List<Entity>(approachStopRecords.Keys))ReleaseApproachStops(road);foreach(var road in new List<Entity>(approachRecords.Keys))ReleaseApproach(road);}
  bool TickApproaches(){{
   if(approachWork==null){
    bool stopOnly=approachPending.Count==0;if(stopOnly&&approachStopsPending.Count==0)return false;Entity road=Entity.Null;foreach(var e in stopOnly?approachStopsPending:approachPending){road=e;break;}
    if(pending.Contains(road))return false;
    if(approachRecords.TryGetValue(road,out var old))foreach(var e in old.Paint)if(Live(e)&&EntityManager.HasComponent<Created>(e))return false;
    if(approachStopRecords.TryGetValue(road,out var stopRecord))foreach(var e in stopRecord.Paint)if(Live(e)&&EntityManager.HasComponent<Created>(e))return false;
    approachPending.Remove(road);approachStopsPending.Remove(road);approachOwner=road;approachWork=(stopOnly?BuildApproachStops(road):BuildApproach(road)).GetEnumerator();
   }
   
   EntityManager.CompleteAllTrackedJobs();laneSnapshots.Clear();inWorkItem=true;
   try{if(!Live(approachOwner)||!approachWork.MoveNext()){approachWork.Dispose();approachWork=null;}}
   catch(System.Exception ){failed++;approachWork?.Dispose();approachWork=null;ReleaseApproach(approachOwner);}
   finally{ObserveOwnerShape(approachOwner);inWorkItem=false;laneSnapshots.Clear();}
   return true;
  }}
  IEnumerable<int> BuildApproachStops(Entity road){
   if(!IsRoad(road)||!EntityManager.HasComponent<ApproachChoice>(road)||!EntityManager.GetComponentData<ApproachChoice>(road).Enabled)yield break;
   ReleaseApproachStops(road);var lanes=Lanes(road);var drives=Drives(lanes);FindStops(road,lanes,drives);
   var stops=new Record();approachStopRecords[road]=stops;
   AlignStopLines(road,stops,drives);HideApproachStops(stops,lanes,drives);PlanStopFlow(road,lanes,drives);yield return 0;
   ApplyDividerStyles(road,stops,lanes,drives);
   if(EntityManager.HasBuffer<ManualDecalChoice>(road))manualDirty.Add(road);QueueBoundaryGuides(road);  }
  IEnumerable<int> BuildApproach(Entity road){
   ReleaseApproach(road);if(!IsRoad(road))yield break;
   var lanes=Lanes(road);var drives=Drives(lanes);FindStops(road,lanes,drives);
   if(!EntityManager.HasComponent<ApproachChoice>(road)||!EntityManager.GetComponentData<ApproachChoice>(road).Enabled){PlanStopFlow(road,lanes,drives);yield break;}
   if(!CachedAssetsReady())yield break;
   var record=new Record();approachRecords[road]=record;
   HideTransportLabels(road,record,lanes);
   var stops=new Record();approachStopRecords[road]=stops;
   AlignStopLines(road,stops,drives);HideApproachStops(stops,lanes,drives);PlanStopFlow(road,lanes,drives);yield return 1;
   ApplyDividerStyles(road,stops,lanes,drives);yield return 5;
   ReplaceArrows(road,record,drives);yield return 3;
   EnsureApproachArrows(road,record,drives);yield return 4;
   PaintApproachDiamonds(road,record,drives);yield return 2;
   PaintUTurnWarnings(road,record,lanes,drives);PaintSpeedSigns(road,record,drives);ReconcileDecalSpacing(road);
   QueueBoundaryGuides(road);Status="정지선·데칼 적용 완료 / Approach markings updated";  }
 }
}
