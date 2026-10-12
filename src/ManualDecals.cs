using System.Collections.Generic;
using Colossal.Serialization.Entities;
using Game.Common;
using Game.Net;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using UnityEngine;
namespace RoadStyleIntoKorea {
 public partial class StyleSystem {
  internal static readonly string[] ManualArrowSymbols={"Straight","Left","Right","StraightLeft","StraightRight","LeftRight","StraightLeftRight","DiagonalLeft","DiagonalRight","LeftUTurn"};
  internal int HumpBrush;internal void CycleHump(){HumpBrush=1-HumpBrush;}
  internal int ManualArrowBrush;internal void CycleManualArrow(){ManualArrowBrush=(ManualArrowBrush+1)%ManualArrowSymbols.Length;}
  readonly HashSet<Entity> manualDirty=new HashSet<Entity>();
  readonly Dictionary<Entity,Record> manualRecords=new Dictionary<Entity,Record>();
  readonly List<ManualDecalChoice> manualHoverChoices=new List<ManualDecalChoice>(),manualHoverBatch=new List<ManualDecalChoice>();
  List<Geometry.Arc> manualPreviewStopCenters;Entity manualPreviewRoad;int manualPreviewRevision=-1;List<Drive> manualPreviewDrives;Geometry.Arc manualPreviewCenter;
  void ManualPose(Entity road,ManualDecalChoice choice,out float3 p,out float3 f){var c=CurveOf(road);f=Geometry.Tangent(c,choice.T);p=Geometry.Position(c,choice.T)+Geometry.Left(f)*choice.Offset+new float3(0,choice.Height,0);if(choice.Reverse)f=-f;if(choice.Kind==2)p+=f*(ArrowPolicy.ReferenceFront-ArrowBounds(ManualArrowSymbols[choice.Symbol]).y);}
  string ManualAsset(Entity road,ManualDecalChoice c)=>c.Kind==8?(c.Symbol==0?"CSKRSpeedHumpClean3p8mNetLane":"CSKRSpeedHumpWorn3p8mNetLane"):c.Kind>=5?RoadSignAsset(road,c):c.Kind==1?"NA Car Stop Line":c.Kind==2?AssetCatalog.Arrow(ManualArrowSymbols[c.Symbol]):c.Kind==3?AssetCatalog.Arrow("UTurn"):AssetCatalog.Diamond;
  float ManualHalf(Entity road,ManualDecalChoice c)=>c.Kind==8?1.8f:c.Kind==1?.25f:DecalHalfLength(assets.Get(ManualAsset(road,c)),c.Kind==2?3.8f:2.8f);
  bool ManualWarningClear(Entity road,ManualDecalChoice warning,List<ManualDecalChoice> choices){
   ManualPose(road,warning,out var p,out var forward);float half=ManualHalf(road,warning);if(!SecondarySpaceOnRoad(road,p,half))return false;
   foreach(var a in choices)if(a.Kind==2){ManualPose(road,a,out var ap,out var af);if(math.abs(math.dot(af,forward))<.8f||math.abs(math.dot(ap-p,Geometry.Left(forward)))>1.5f)continue;
    if(!ArrowPolicy.SecondaryClear(math.dot(p-ap,af),half,new List<ArrowPolicy.Occupied>{new ArrowPolicy.Occupied(0,ManualHalf(road,a))}))return false;}
   return true;
  }
  internal bool ManualDecalTool(Entity road,float3 hit,int kind,Game.Rendering.OverlayRenderSystem.Buffer overlay,bool apply,bool reset,bool allLanes,bool reverseShared=false){
   if(!IsRoad(road)||!CachedAssetsReady())return false;
   bool bothDirections=kind==8&&allLanes;if(kind==8)allLanes=true;
   var choices=manualHoverChoices;choices.Clear();if(EntityManager.HasBuffer<ManualDecalChoice>(road))foreach(var c in EntityManager.GetBuffer<ManualDecalChoice>(road,true))choices.Add(c);
   int picked=-1;float best=16;
   for(int i=0;i<choices.Count;i++)if(choices[i].Kind==kind){ManualPose(road,choices[i],out var p,out var f);float d=math.distancesq(p,hit);if(kind==8){var delta=hit-p;float lateral=math.max(0,math.abs(math.dot(delta,Geometry.Left(f)))-choices[i].Width*.5f);d=math.pow(math.dot(delta,f),2)+lateral*lateral+delta.y*delta.y;}if(d<best){best=d;picked=i;}}
   if(manualPreviewRoad!=road||manualPreviewRevision!=toolRevision){manualPreviewRoad=road;manualPreviewRevision=toolRevision;manualPreviewDrives=Drives(Lanes(road),true);manualPreviewCenter=new Geometry.Arc(CurveOf(road));manualPreviewStopCenters=StopBarCenters(road,Lanes(road));}
   Drive nearest=null;float distance=float.MaxValue;float3 point=default,forward=default;
   foreach(var drive in manualPreviewDrives){drive.Arc.Nearest(hit,out var p,out var direction);float d=math.distancesq(p,hit);if(d<distance){distance=d;nearest=drive;point=p;forward=direction;}}
   if(nearest!=null&&reverseShared&&(EntityManager.GetComponentData<CarLane>(nearest.Entity).m_Flags&CarLaneFlags.Twoway)!=0)forward=-forward;
   bool valid=nearest!=null&&distance<=math.pow(nearest.Width*.5f+1,2);if(!valid&&picked<0)return false;
   var candidate=default(ManualDecalChoice);
   if(nearest!=null){var center=manualPreviewCenter;float t=center.Nearest(point);var basePoint=Geometry.Position(center.Curve,t);var direction=Geometry.Tangent(center.Curve,t);
    candidate=new ManualDecalChoice{Kind=kind,Symbol=kind==8?HumpBrush:kind==5?SpeedBrush:kind==6?TextBrush:ManualArrowBrush,T=t,Offset=math.dot(point-basePoint,Geometry.Left(direction)),Height=point.y-basePoint.y,Width=nearest.Width,Reverse=math.dot(forward,direction)<0};}
   if(kind==5||kind==6){string asset=ManualAsset(road,candidate);valid=valid&&RoadSignReady(asset);SignHover=(kind==5?RoadKmh(road)+" km/h":"")+(valid?"":" · 에셋 없음 / Unavailable");}
   if(kind==8){var asset=assets.Get(ManualAsset(road,candidate));valid=valid&&asset!=Entity.Null&&EntityManager.HasComponent<Game.Prefabs.NetLaneGeometryData>(asset);if(!valid)Status="방지턱 NetLane 없음 / Speed hump NetLane unavailable";}
   var batch=manualHoverBatch;batch.Clear();if(valid)batch.Add(candidate);
   if(valid&&allLanes)foreach(var drive in manualPreviewDrives){
    if(kind!=8&&!bothDirections&&(EntityManager.GetComponentData<CarLane>(drive.Entity).m_CarriagewayGroup&255)!=(EntityManager.GetComponentData<CarLane>(nearest.Entity).m_CarriagewayGroup&255))continue;
    var planeNormal=ManualPlacementPolicy.IntersectionNormal(forward,drive.Arc.Direction(drive.Arc.Length*.5f),bothDirections);
    if(drive==nearest||!drive.Arc.IntersectPlane(point,planeNormal,out float at)||at<0||at>drive.Arc.Length)continue;
    var f=drive.Arc.Direction(at);var p=drive.Arc.At(at);if(kind==8&&math.abs(math.dot(forward,f))<.8f)continue;if(bothDirections?math.abs(p.y-point.y)>2:!ManualPlacementPolicy.SameDirection(forward,point,f,p))continue;
    var center=manualPreviewCenter;float t=center.Nearest(p);var origin=Geometry.Position(center.Curve,t);var direction=Geometry.Tangent(center.Curve,t);
    var choice=new ManualDecalChoice{Kind=kind,Symbol=kind==8?HumpBrush:kind==5?SpeedBrush:kind==6?TextBrush:ManualArrowBrush,T=t,Offset=math.dot(p-origin,Geometry.Left(direction)),Height=p.y-origin.y,Width=drive.Width,Reverse=math.dot(f,direction)<0};
    if(batch.Exists(c=>math.abs(c.Offset-choice.Offset)<.5f&&math.abs(c.T-choice.T)<.005f))continue;batch.Add(choice);
   }
   if(valid&&allLanes&&(kind==1||kind==8)){
    // One persisted bar spans the selected carriageway, including empty strips
    // between driving lanes. Independent lane choices used to leave holes.
    candidate=ManualPlacementPolicy.ContinuousStop(batch,candidate);if(kind==8)candidate=FitHumpBoundaries(road,candidate,batch);batch.Clear();batch.Add(candidate);
   }
   batch.RemoveAll(c=>(c.Kind==5||c.Kind==6)&&!RoadSignReady(ManualAsset(road,c)));
   foreach(var choice in batch)DrawManualPreview(road,choice,overlay,new Color(.1f,.85f,1,.8f));
   var preview=picked>=0?choices[picked]:candidate;
   if(picked>=0)DrawManualPreview(road,preview,overlay,new Color(1,.65f,.15f,.9f));
   if(reset){if(picked<0)return false;var selected=choices[picked];localStyleCommands.Enqueue(()=>{if(!Live(road)||!EntityManager.HasBuffer<ManualDecalChoice>(road))return;var b=EntityManager.GetBuffer<ManualDecalChoice>(road);for(int i=b.Length-1;i>=0;i--)if(b[i].Kind==selected.Kind&&math.abs(b[i].T-selected.T)<.0001f&&math.abs(b[i].Offset-selected.Offset)<.001f){b.RemoveAt(i);break;}manualDirty.Add(road);});return true;}
   if(apply&&valid){
    var placements=batch.ToArray(); // Immutable click snapshot while the pointer keeps moving.
    localStyleCommands.Enqueue(()=>{if(!IsRoad(road))return;if(!EntityManager.HasBuffer<ManualDecalChoice>(road))EntityManager.AddBuffer<ManualDecalChoice>(road);var b=EntityManager.GetBuffer<ManualDecalChoice>(road);
     if(allLanes&&(kind==1||kind==8))for(int i=b.Length-1;i>=0;i--)if(b[i].Kind==kind&&(bothDirections||b[i].Reverse==candidate.Reverse)&&math.abs(b[i].T-candidate.T)<.005f&&math.abs(b[i].Offset-candidate.Offset)<=candidate.Width*.5f)b.RemoveAt(i);
     foreach(var choice in placements){int replace=-1;for(int i=0;i<b.Length;i++)if(b[i].Kind==choice.Kind&&math.abs(b[i].T-choice.T)<.005f&&math.abs(b[i].Offset-choice.Offset)<.5f){replace=i;break;}
     if(replace>=0)b[replace]=choice;else b.Add(choice);}manualDirty.Add(road);targetedActive=true;});}
   return true;
  }
  void DrawManualPreview(Entity road,ManualDecalChoice preview,Game.Rendering.OverlayRenderSystem.Buffer overlay,Color color){
   ManualPose(road,preview,out var pos,out var facing);float half=ManualHalf(road,preview);var left=Geometry.Left(facing);
   if(preview.Kind==8){overlay.DrawCurve(color,ManualPlacementPolicy.StopLine(pos,facing,preview.Width),3.6f);}
   else if(preview.Kind==1){foreach(var bar in StopCenterPolicy.ClearBar(ManualPlacementPolicy.StopLine(pos,facing,preview.Width),manualPreviewStopCenters))overlay.DrawCurve(color,bar,.35f);}
   else {overlay.DrawCurve(color,Geometry.Line(pos-facing*half,pos+facing*half),.2f);overlay.DrawCurve(color,Geometry.Line(pos+facing*half,pos+facing*(half-1)+left*.7f),.2f);overlay.DrawCurve(color,Geometry.Line(pos+facing*half,pos+facing*(half-1)-left*.7f),.2f);}
  }
  void ReleaseManual(Entity road){if(!manualRecords.TryGetValue(road,out var r))return;foreach(var surface in r.PavingSurfaces)surface.Dispose();r.PavingSurfaces.Clear();foreach(var e in r.Paint)RetirePaint(e);foreach(var e in r.Hidden)RestoreHidden(e,r);manualRecords.Remove(road);}
  void ClearManual(){EndLineEdit();detailSnapRoad=Entity.Null;detailSnapRegion=null;detailSnapRevision=-1;detailSnapOffsets.Clear();humpBoundsRoad=Entity.Null;humpBoundsRevision=-1;humpBounds.Clear();dividerPreviewRoad=Entity.Null;dividerPreview=null;manualDirty.Clear();foreach(var road in new List<Entity>(manualRecords.Keys))ReleaseManual(road);manualPreviewRoad=Entity.Null;manualPreviewRevision=-1;}
  bool TickManual(){{
   if(manualDirty.Count==0)return false;Entity road=Entity.Null;foreach(var e in manualDirty){road=e;break;}
   // Keep the queued selection and existing visuals until assets are ready.
   if(Live(road)&&!CachedAssetsReady())return false;
   if(manualRecords.TryGetValue(road,out var old))foreach(var e in old.Paint)if(Live(e)&&EntityManager.HasComponent<Created>(e))return false;
   EntityManager.CompleteAllTrackedJobs();manualDirty.Remove(road);ReleaseManual(road);
   if(Live(road)&&EntityManager.HasComponent<Node>(road)){try{BuildDetailJoins(road);BuildDrawnLines(road);}catch(System.Exception ){ReleaseManual(road);}return true;}
   QueueDetailJoins(road);
   if(!IsRoad(road)){if(GuideRoad(road)){try{BuildDrawnLines(road);ObserveOwnerShape(road);}catch(System.Exception ){ReleaseManual(road);}}return true;}
     var choices=new List<ManualDecalChoice>();if(EntityManager.HasBuffer<ManualDecalChoice>(road))foreach(var c in EntityManager.GetBuffer<ManualDecalChoice>(road,true))if(c.Kind>0)choices.Add(c);
     if(choices.Count==0){BuildDrawnLines(road);ReconcileDecalSpacing(road,false);return true;}
   var record=new Record();manualRecords[road]=record;List<Geometry.Arc> centers=null;
   try{PaintManualDividers(road,record,choices);ManualStopDividers(road,record,choices);HideNearbyAutomaticStops(road,record,choices);foreach(var choice in choices){
    if(choice.Kind==7)continue;if(choice.Kind==9||choice.Kind==10){PaintSidewalkDetail(road,record,choice);continue;}
    ManualPose(road,choice,out var p,out var f);var prefab=assets.Get(ManualAsset(road,choice));if(prefab==Entity.Null)continue;
    if(choice.Kind==8){PaintLane(road,record,prefab,ManualPlacementPolicy.StopLine(p,f,choice.Width));}
    else if(choice.Kind==1){if(centers==null)centers=StopBarCenters(road,Lanes(road));foreach(var bar in StopCenterPolicy.ClearBar(ManualPlacementPolicy.StopLine(p,f,choice.Width),centers))PaintLane(road,record,prefab,bar);}
    else PaintObject(road,record,prefab,new Game.Objects.Transform(p,quaternion.LookRotationSafe(f,new float3(0,1,0))));
   }BuildDrawnLines(road);}catch(System.Exception ){ReleaseManual(road);}
   ReconcileDecalSpacing(road,false);ObserveOwnerShape(road);return true;
  }}
 }
}









