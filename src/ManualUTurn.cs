using System.Collections.Generic;
using Colossal.Serialization.Entities;
using Colossal.Mathematics;
using Game.Net;
using Game.Prefabs;
using Unity.Entities;
using Unity.Mathematics;
using UnityEngine;
namespace RoadStyleIntoKorea {
public struct UTurnChoice:IComponentData,ISerializable {
 public float Position;public float Length;public bool Reverse,Automatic;
 public void Serialize<T>(T w) where T:IWriter{w.Write(3);w.Write(Position);w.Write(Length);w.Write(Reverse);w.Write(Automatic);}
 public void Deserialize<T>(T r) where T:IReader{r.Read(out int v);r.Read(out Position);if(v>=2){r.Read(out Length);r.Read(out Reverse);}else{Length=8;Reverse=false;}Automatic=false;if(v>=3)r.Read(out Automatic);Position=math.isfinite(Position)?math.saturate(Position):.5f;Length=UTurnRoutePolicy.ClampLength(Length);}

}
public partial class StyleSystem {
 bool ManualUTurnAllowed(Entity edge)=>IsRoad(edge)&&RoadLaneCount(edge)>=3&&!CenterDivided(edge)&&HasOpposingTraffic(edge);
 bool UTurnInterval(Entity edge,Bezier4x3 curve,float position,out float lo,out float hi){
  return ManualZonePolicy.Interval(CurveOf(edge),curve,position,EntityManager.HasComponent<UTurnChoice>(edge)?EntityManager.GetComponentData<UTurnChoice>(edge).Length:S.UTurnZoneLength,out lo,out hi);
 }
 void PaintCenterZone(Entity edge,Record record,Entity prefab,Bezier4x3 curve){
  if(!ManualUTurnAllowed(edge)||!EntityManager.HasComponent<UTurnChoice>(edge)||assets.Get(AssetCatalog.UTurnZone)==Entity.Null){if(prefab!=Entity.Null)PaintLane(edge,record,prefab,curve);return;}
  var choice=EntityManager.GetComponentData<UTurnChoice>(edge);
  if(choice.Automatic&&!AutoUTurnPosition(edge,choice.Length,out choice.Position)){if(prefab!=Entity.Null)PaintLane(edge,record,prefab,curve);return;}
  if(!UTurnInterval(edge,curve,choice.Position,out float lo,out float hi)){if(prefab!=Entity.Null)PaintLane(edge,record,prefab,curve);return;}
  var arc=new Geometry.Arc(curve);
  // Split real geometry: secondary NetLane renderers do not consistently honor CutRange.
  if(prefab!=Entity.Null)foreach(var part in ManualZonePolicy.Outside(arc,lo,hi))PaintLane(edge,record,prefab,part);
  foreach(var clear in ClearPaint(arc.Slice(lo,hi),NearbyCrossings(edge),S.PaintClearance,edge))PaintLane(edge,record,assets.Get(AssetCatalog.UTurnZone),clear);

 }
 void ApplyManualUTurn(Entity edge,Record record,List<Entity> lanes){
  if(!ManualUTurnAllowed(edge)||!EntityManager.HasComponent<UTurnChoice>(edge)||assets.Get(AssetCatalog.UTurnZone)==Entity.Null)return;
  if(EntityManager.HasComponent<CenterChoice>(edge)&&EntityManager.GetComponentData<CenterChoice>(edge).Left!=0)return;
  foreach(var path in CenterPaths(edge,lanes,EdgeStyle.YellowSolid)){
   var prefab=path.Source==Entity.Null?assets.Get(CenterLinePolicy.Asset((int)EdgeStyle.YellowSolid)):Prefab(path.Source);
   if(prefab==Entity.Null)continue;
   foreach(var clear in CenterVisible(edge,path,EdgeStyle.YellowSolid,lanes))PaintCenterZone(edge,record,prefab,clear);
   HideCenterOriginals(path,lanes,record);
  }
 }
 internal void AdjustUTurnLength(Entity edge,int delta){
  S.UTurnZoneLength=(int)UTurnRoutePolicy.ClampLength(S.UTurnZoneLength+delta);
  if(ManualUTurnAllowed(edge)&&EntityManager.HasComponent<UTurnChoice>(edge)){
   var c=EntityManager.GetComponentData<UTurnChoice>(edge);c.Length=UTurnRoutePolicy.ClampLength(c.Length+delta);S.UTurnZoneLength=(int)c.Length;EntityManager.SetComponentData(edge,c);EnqueueCenterOnly(edge);
  }
  InvalidateToolPreview();Status="유턴 점선 길이 / U-turn length: "+S.UTurnZoneLength+"m (4–24m)";
 }
 Entity uturnPreviewEdge;int uturnPreviewRevision=-1;
 Geometry.Arc uturnRoad;
 readonly List<Geometry.Arc> uturnPreview=new List<Geometry.Arc>();
 internal void UTurnTool(Entity edge,float3 hit,Game.Rendering.OverlayRenderSystem.Buffer buffer,bool apply,bool reset){
  if(reset&&S.Enabled){if(EntityManager.HasComponent<UTurnChoice>(edge)){EntityManager.RemoveComponent<UTurnChoice>(edge);EnqueueCenterOnly(edge);}return;}
  if(!ManualUTurnAllowed(edge)){Status="유턴 점선: 왕복 3차로 이상, 중앙분리대 없는 도로 / Requires opposing traffic, 3+ lanes, no median";return;}
  if(uturnPreviewEdge!=edge||uturnPreviewRevision!=toolRevision){
   uturnPreviewEdge=edge;uturnPreviewRevision=toolRevision;uturnRoad=new Geometry.Arc(CurveOf(edge));uturnPreview.Clear();var lanes=Lanes(edge);
   foreach(var path in CenterPaths(edge,lanes,EdgeStyle.YellowSolid))foreach(var clear in CenterVisible(edge,path,EdgeStyle.YellowSolid,lanes))uturnPreview.Add(new Geometry.Arc(clear));
  }
  float position=uturnRoad.Nearest(hit);bool found=false;
  foreach(var arc in uturnPreview){
   if(!ManualZonePolicy.Interval(uturnRoad,arc,position,S.UTurnZoneLength,out float lo,out float hi))continue;
   buffer.DrawCurve(new Color(.85f,.85f,.85f,.8f),arc.Slice(lo,hi),.35f);found=true;
  }
  if(!S.Enabled||(!apply&&!reset)||!found)return;
  if(reset){if(EntityManager.HasComponent<UTurnChoice>(edge))EntityManager.RemoveComponent<UTurnChoice>(edge);}
  else {
   if(assets.Named.Count==0)assets.Refresh(EntityManager,World.GetExistingSystemManaged<PrefabSystem>());
   if(assets.Get(AssetCatalog.UTurnZone)==Entity.Null){Status="유턴 점선 에셋 누락 / Missing U-turn zone asset";return;}
   if(!EntityManager.HasComponent<UTurnChoice>(edge))EntityManager.AddComponent<UTurnChoice>(edge);
   EntityManager.SetComponentData(edge,new UTurnChoice{Position=position,Length=UTurnRoutePolicy.ClampLength(S.UTurnZoneLength),Reverse=math.dot(hit-Geometry.Position(uturnRoad.Curve,position),Geometry.Left(Geometry.Tangent(uturnRoad.Curve,position)))>0});
  }
  EnqueueCenterOnly(edge);Status="유턴 점선 변경 대기 / U-turn zone queued";
 }
}}


