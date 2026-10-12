using System.Collections.Generic;
using Colossal.Mathematics;
using Game.Common;
using Game.Net;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using UnityEngine;
namespace RoadStyleIntoKorea {public partial class StyleSystem {
 struct LinePoint {internal float3 Position,Tangent;internal Entity Lane,PointOwner;internal float T,RedCapWidth;}
 sealed class LineHit {internal Entity Owner;internal int Index;internal long Group;internal float Width;internal Bezier4x3 Curve;internal Geometry.Arc Arc;}
 readonly List<LineHit> lineHits=new List<LineHit>();
 readonly List<LinePoint> linePoints=new List<LinePoint>();
 readonly List<Entity> lineOwners=new List<Entity>();
 Entity lineContext,lineStartOwner,lineDragOwner;int lineRevision=-1,lineDragIndex=-1;
 LinePoint lineStart;bool lineStarted,lineMutationPending;Bezier4x3 lineDragOriginal,lineDragPreview;
 internal bool ColorGuideMode;internal int ColorGuideBrush;internal int SelectedLineStyle=>ColorGuideMode?DrawnLinePolicy.LineCount+DrawnLinePolicy.ColorOrder[ColorGuideBrush]:LineBrush;internal void CycleColorGuideBrush(){ColorGuideBrush=(ColorGuideBrush+1)%DrawnLinePolicy.ColorCount;}
 internal string LineAssetStatus=>assets.Get(DrawnLinePolicy.Assets[SelectedLineStyle])==Entity.Null?"line.missing":"";
 internal int LineBrush;internal void CycleLineBrush(){LineBrush=(LineBrush+1)%DrawnLinePolicy.LineCount;}
 internal void CancelLineDrag(){lineDragIndex=-1;}
 internal void EndLineEdit(){redPavingMode=false;RedPavingHint="";routeRevision=-1;lineRoute.Clear();lineMutationPending=false;lineStarted=false;lineDragIndex=-1;lineContext=Entity.Null;linePoints.Clear();lineOwners.Clear();lineHits.Clear();}
 bool LineOwner(Entity e)=>Live(e)&&(GuideRoad(e)||GuideNode(e));
 internal Entity LineTarget(Entity hit,Entity owner,bool colorGuide=false){
  bool Allowed(Entity e)=>colorGuide?LineOwner(e):Live(e)&&(IsRoad(e)||SafeNode(e));
  if(Allowed(hit))return hit;
  if(Live(hit)&&EntityManager.HasComponent<Owner>(hit)){var parent=EntityManager.GetComponentData<Owner>(hit).m_Owner;if(Allowed(parent))return parent;}
  return Allowed(owner)?owner:Entity.Null;
 }
 // Control points are relative to their owning road frame, including road width.
 // Node-owned curves translate with the junction. No references to transient lanes.
 void LineFrame(Entity owner,out float3 origin,out float3 forward,out float length,out float width){
  width=1;length=1;forward=new float3(0,0,1);
  if(EntityManager.HasComponent<Edge>(owner)){
   var c=CurveOf(owner);origin=c.a;forward=math.normalizesafe(new float3(c.d.x-c.a.x,0,c.d.z-c.a.z),forward);length=math.max(1,math.distance(c.a.xz,c.d.xz));
   if(EntityManager.HasComponent<Composition>(owner)){var cp=EntityManager.GetComponentData<Composition>(owner).m_Edge;if(EntityManager.HasComponent<Game.Prefabs.NetCompositionData>(cp))width=math.max(1,EntityManager.GetComponentData<Game.Prefabs.NetCompositionData>(cp).m_Width);}
  }else origin=EntityManager.GetComponentData<Node>(owner).m_Position;
 }
 Bezier4x3 LineTransform(Entity owner,Bezier4x3 c,bool encode){
  LineFrame(owner,out var o,out var f,out float length,out float width);var left=Geometry.Left(f);
  System.Func<float3,float3> transform=encode?(System.Func<float3,float3>)(p=>{var d=p-o;return new float3(math.dot(d,f)/length,d.y,math.dot(d,left)/width);}):p=>o+f*(p.x*length)+new float3(0,p.y,0)+left*(p.z*width);
  return new Bezier4x3(transform(c.a),transform(c.b),transform(c.c),transform(c.d));
 }
 void AddLinePoint(float3 p,float3 tangent,Entity pointOwner=default){if(linePoints.Count>=256||!math.all(math.isfinite(p)))return;foreach(var old in linePoints)if(math.distancesq(old.Position,p)<.04f)return;linePoints.Add(new LinePoint{Position=p,Tangent=tangent,PointOwner=pointOwner});}
 void LineCurvePoints(Bezier4x3 c,Entity owner=default){AddLinePoint(c.a,Geometry.Tangent(c,0),owner);AddLinePoint(c.d,Geometry.Tangent(c,1),owner);}
 void CacheLinePoints(Entity target){
  if(lineContext==target&&lineRevision==toolRevision)return;
  lineContext=target;lineRevision=toolRevision;lineOwners.Clear();linePoints.Clear();lineHits.Clear();penLanes.Clear();penDots.Clear();
  lineOwners.Add(target);
  if(ColorGuideMode&&lineStarted&&LineOwner(lineStartOwner)&&lineStartOwner!=target)lineOwners.Add(lineStartOwner);
  if(LineRoad(target)){var edge=EntityManager.GetComponentData<Edge>(target);lineOwners.Add(edge.m_Start);lineOwners.Add(edge.m_End);}
  else foreach(var connection in EntityManager.GetBuffer<ConnectedEdge>(target,true))if(LineRoad(connection.m_Edge))lineOwners.Add(connection.m_Edge);
  foreach(var node in new List<Entity>(lineOwners))if(LineNode(node))foreach(var connection in EntityManager.GetBuffer<ConnectedEdge>(node,true))if(lineOwners.Count<24&&LineRoad(connection.m_Edge)&&!lineOwners.Contains(connection.m_Edge))lineOwners.Add(connection.m_Edge);
  for(int i=0;i<lineOwners.Count&&lineOwners.Count<24;i++)if(LineRoad(lineOwners[i])){
   var road=lineOwners[i];var edge=EntityManager.GetComponentData<Edge>(road);
   foreach(var node in new[]{edge.m_Start,edge.m_End})if(LineNode(node)){
    int count=0;Entity next=Entity.Null;foreach(var link in EntityManager.GetBuffer<ConnectedEdge>(node,true))if(LineRoad(link.m_Edge)){count++;if(link.m_Edge!=road)next=link.m_Edge;}
    if(count==2&&next!=Entity.Null&&(ColorGuideMode||Prefab(next)==Prefab(road))&&lineOwners.Count<24&&!lineOwners.Contains(next))lineOwners.Add(next);
   }
  }
  if(redPavingMode){
   Entity seed=lineStarted?lineStartOwner:target;
   if(IsRoad(seed))for(int i=0;i<lineOwners.Count&&lineOwners.Count<48;i++)if(IsRoad(lineOwners[i])&&Prefab(lineOwners[i])==Prefab(seed)){
    var e=EntityManager.GetComponentData<Edge>(lineOwners[i]);
    foreach(var node in new[]{e.m_Start,e.m_End})if(LineNode(node))foreach(var c in EntityManager.GetBuffer<ConnectedEdge>(node,true))
     if(lineOwners.Count<48&&IsRoad(c.m_Edge)&&Prefab(c.m_Edge)==Prefab(seed)&&!lineOwners.Contains(c.m_Edge))lineOwners.Add(c.m_Edge);
   }
  }
  if(ColorGuideMode)foreach(var road in new List<Entity>(lineOwners))if(LineRoad(road)){var e=EntityManager.GetComponentData<Edge>(road);foreach(var node in new[]{e.m_Start,e.m_End})if(LineNode(node)&&!lineOwners.Contains(node))lineOwners.Add(node);}
  // Actual paint endpoints must precede estimated car-lane boundaries: the
  // proximity deduplication keeps the first point, including its owning road.
  if(!ColorGuideMode)foreach(var owner in lineOwners){
   if(!LineRoad(owner))continue;
   if(records.TryGetValue(owner,out var painted))foreach(var lane in painted.Paint){
    if(!Live(lane)||!EntityManager.HasComponent<Curve>(lane)||EntityManager.HasComponent<Game.Tools.Hidden>(lane))continue;
    var name=assets.Name(Prefab(lane));
    if(name.Contains("Center")||name.Contains("Edge")||MarkingPolicy.IsLaneDivider(name))LineCurvePoints(CurveOf(lane),owner);
   }
   foreach(var lane in Lanes(owner)){
    if(!Live(lane)||!EntityManager.HasComponent<Curve>(lane)||EntityManager.HasComponent<GeneratedPaint>(lane)||EntityManager.HasComponent<Game.Tools.Hidden>(lane))continue;
    var name=assets.Name(Prefab(lane));
    if(UTurnPolicy.CenterLine(name)||MarkingPolicy.IsLaneDivider(name)||name.IndexOf("Edge Line",System.StringComparison.OrdinalIgnoreCase)>=0)LineCurvePoints(CurveOf(lane),owner);
   }
   if(EntityManager.HasBuffer<DrawnLineChoice>(owner))foreach(var choice in EntityManager.GetBuffer<DrawnLineChoice>(owner,true))
    if(choice.Style>=0&&choice.Style<DrawnLinePolicy.LineCount)LineCurvePoints(LineTransform(owner,choice.Local,false),owner);
  }
  foreach(var owner in lineOwners){
   if(!LineOwner(owner))continue;
   foreach(var map in ColorGuideMode?new Dictionary<Entity,Record>[0]:new[]{records})if(map.TryGetValue(owner,out var record))foreach(var e in record.Paint){
    if(!Live(e)||!EntityManager.HasComponent<Curve>(e)||EntityManager.HasComponent<Game.Tools.Hidden>(e))continue;
    if(!LineRoad(owner))continue;var name=assets.Name(Prefab(e));if(name.Contains("Edge")||name.Contains("Center")||name.Contains("Line")||name.Contains("Divider"))LineCurvePoints(CurveOf(e));
   }
   foreach(var lane in Lanes(owner)){
    if(!EntityManager.HasComponent<Curve>(lane)||EntityManager.HasComponent<MasterLane>(lane))continue;
    if(EntityManager.HasComponent<CarLane>(lane)&&(ColorGuideMode||LineRoad(owner))){
     var c=CurveOf(lane);float half=Width(lane)*.5f;
     if(ColorGuideMode){LineCurvePoints(c);if(redPavingMode||!PenJunction(owner)||RestrictedGuideOwner(owner))CachePenLane(owner,lane);else {CachePenDot(owner,c.a,Geometry.Tangent(c,0));CachePenDot(owner,c.d,Geometry.Tangent(c,1));}}else {LineCurvePoints(Geometry.Offset(c,half));LineCurvePoints(Geometry.Offset(c,-half));}
    }else if(!ColorGuideMode&&LineRoad(owner)&&!EntityManager.HasComponent<GeneratedPaint>(lane)&&!EntityManager.HasComponent<Game.Tools.Hidden>(lane)&&MarkingPolicy.IsLaneDivider(assets.Name(Prefab(lane))))LineCurvePoints(CurveOf(lane));
   }
   if(EntityManager.HasBuffer<DrawnLineChoice>(owner)){
    var choices=EntityManager.GetBuffer<DrawnLineChoice>(owner,true);
    for(int i=0;i<choices.Length;i++)if(choices[i].Style>=0&&(redPavingMode?RedPavingPolicy.IsRed(choices[i].Style):!RedPavingPolicy.IsRed(choices[i].Style)&&DrawnLinePolicy.IsColor(choices[i].Style)==ColorGuideMode)){var c=LineTransform(owner,choices[i].Local,false);LineCurvePoints(c);lineHits.Add(new LineHit{Owner=owner,Index=i,Group=choices[i].Group,Width=choices[i].Width,Curve=c,Arc=new Geometry.Arc(c)});}
   }
  }
 }
 internal void DrawLineTool(Entity target,float3 hit,Game.Rendering.OverlayRenderSystem.Buffer buffer,bool press,bool held,bool release,bool reset,bool colorGuide=false){
  if(ColorGuideMode!=colorGuide){EndLineEdit();ColorGuideMode=colorGuide;}
  if(!S.Enabled||!CachedAssetsReady())return;
  if(lineDragIndex>=0){
   if(reset||!LineOwner(lineDragOwner)){lineDragIndex=-1;return;}
   lineDragPreview=DrawnLinePolicy.Bend(lineDragOriginal,hit);
   buffer.DrawCurve(new Color(.1f,.85f,1,.9f),lineDragPreview,.25f);DrawGuidePoint(buffer,Geometry.Position(lineDragPreview,.5f),Color.white);
   if(release||!held){var o=lineDragOwner;int index=lineDragIndex;var curve=lineDragPreview;lineDragIndex=-1;QueueDrawnLine(o,index,curve,LineBrush,false,true);}
   return;
  }
  if(!LineOwner(target))return;
  if(lineStarted&&!ColorGuideMode&&LineOwner(lineStartOwner))target=lineStartOwner;
  CacheLinePoints(target);
  Entity hoveredOwner=Entity.Null;int hovered=-1;float nearest=1.44f;Bezier4x3 hoveredCurve=default;
  foreach(var entry in lineHits){if(!entry.Arc.MayBeNear(hit,1.2f))continue;float d=math.distancesq(Geometry.Position(entry.Curve,entry.Arc.Nearest(hit)),hit);if(d<nearest){nearest=d;hoveredOwner=entry.Owner;hovered=entry.Index;hoveredCurve=entry.Curve;}}
  if(!lineStarted&&hovered>=0){
   buffer.DrawCurve(new Color(1,.45f,.05f,.95f),hoveredCurve,.4f);var mid=Geometry.Position(hoveredCurve,.5f);DrawGuidePoint(buffer,mid,new Color(1,.65f,.1f,1));
   if(reset){QueueDrawnLine(hoveredOwner,hovered,default,0,true,false);return;}
   if(press&&math.distancesq(mid,hit)<2.25f){lineDragOwner=hoveredOwner;lineDragIndex=hovered;lineDragOriginal=lineDragPreview=hoveredCurve;return;}
  }
  if(reset){lineStarted=false;return;}
  if(ColorGuideMode){ColorPenTool(target,hit,buffer,press);return;}
  int picked=-1;float best=1.44f;
  for(int i=0;i<linePoints.Count;i++){var p=linePoints[i];DrawGuidePoint(buffer,p.Position,new Color(.1f,.8f,1,.85f));float distance=math.distancesq(p.Position,hit);if(distance<best){best=distance;picked=i;}}
  if(picked>=0)DrawGuidePoint(buffer,linePoints[picked].Position,Color.white);
  if(lineStarted){DrawGuidePoint(buffer,lineStart.Position,Color.white);if(picked>=0&&math.distancesq(lineStart.Position,linePoints[picked].Position)>.25f){var end=linePoints[picked];if(BuildLineRoute(lineStart,end)){foreach(var piece in lineRoute)buffer.DrawCurve(new Color(.1f,.85f,1,.85f),piece.Curve,.25f);}else {var c=DrawnLinePolicy.Connect(lineStart.Position,end.Position,lineStart.Tangent,end.Tangent);buffer.DrawCurve(new Color(.1f,.85f,1,.85f),c,.25f);}}}
  if(!press||picked<0)return;
  if(!lineStarted){lineStart=linePoints[picked];lineStartOwner=target;lineStarted=true;return;}
  var last=linePoints[picked];if(math.distancesq(last.Position,lineStart.Position)<.25f)return;
  if(BuildLineRoute(lineStart,last))QueueLineRoute(SelectedLineStyle);else QueueDrawnLine(lineStartOwner,-1,DrawnLinePolicy.Connect(lineStart.Position,last.Position,lineStart.Tangent,last.Tangent),SelectedLineStyle,false,false);lineStarted=false;
 }
 void QueueDrawnLine(Entity owner,int index,Bezier4x3 curve,int style,bool remove,bool preserveStyle){
  if(lineMutationPending)return;lineMutationPending=true;
  localStyleCommands.Enqueue(()=>{try{
   if(!LineOwner(owner))return;
   var choices=EntityManager.HasBuffer<DrawnLineChoice>(owner)?EntityManager.GetBuffer<DrawnLineChoice>(owner):EntityManager.AddBuffer<DrawnLineChoice>(owner);
   if(preserveStyle&&index>=0&&index<choices.Length)style=choices[index].Style;
   if(!remove&&(style<0||style>=DrawnLinePolicy.Assets.Length||assets.Get(DrawnLinePolicy.Assets[style])==Entity.Null))return;
   if(index>=0){if(index>=choices.Length)return;if(remove){var group=choices[index].Group;if(group!=0){RemoveDrawnGroup(group);InvalidateToolPreview();lineRevision=-1;targetedActive=true;return;}choices.RemoveAt(index);}else choices[index]=new DrawnLineChoice{Style=preserveStyle?choices[index].Style:style,Group=choices[index].Group,Local=LineTransform(owner,curve,true)};}
   else {if(choices.Length>=128)return;var local=LineTransform(owner,curve,true);foreach(var existing in choices)if(existing.Style==style&&(Geometry.SameCurve(existing.Local,local)||Geometry.SameCurve(existing.Local,Geometry.Reverse(local))))return;choices.Add(new DrawnLineChoice{Style=style,Group=System.BitConverter.ToInt64(System.Guid.NewGuid().ToByteArray(),0),Local=local});}
   manualDirty.Add(owner);InvalidateToolPreview();lineRevision=-1;targetedActive=true;
  }finally{lineMutationPending=false;}});
 }
 void BuildDrawnLines(Entity owner){
  if(!LineOwner(owner)||!EntityManager.HasBuffer<DrawnLineChoice>(owner))return;
  if(!manualRecords.TryGetValue(owner,out var record))manualRecords[owner]=record=new Record();
  var choices=ChoiceBuffer<DrawnLineChoice>(owner);System.Array.Sort(choices,(a,b)=>DrawnLinePolicy.IsColor(a.Style).CompareTo(DrawnLinePolicy.IsColor(b.Style)));
  List<ColorObstacle> obstacles=null;SidewalkFootprint redSurface=null;bool redSurfaceRead=false;
  foreach(var choice in choices){
   if(RedPavingPolicy.IsRed(choice.Style)){try{if(!redSurfaceRead){redSurface=RedRoadSurface(owner);redSurfaceRead=true;}BuildRedPaving(owner,record,choice,redSurface);}catch(System.Exception ){}continue;}
   if(choice.Style<0||choice.Style>=DrawnLinePolicy.Assets.Length)continue;var asset=assets.Get(DrawnLinePolicy.Assets[choice.Style]);if(asset==Entity.Null)continue;
   var c=LineTransform(owner,choice.Local,false);if(math.distance(c.a,c.d)<.1f)continue;bool color=DrawnLinePolicy.IsColor(choice.Style);if(color&&obstacles==null){obstacles=ColorGuideObstacles(owner);}foreach(var clear in ClearPaint(c,NearbyCrossings(owner),color?.25f:.05f)){if(color){foreach(var part in Geometry.ClearSlices(clear,p=>ColorGuideClear(p,obstacles)))PaintLane(owner,record,asset,part);}else PaintLane(owner,record,asset,clear);}
  }
 }
 void RestoreDrawnLines(){using(var q=EntityManager.CreateEntityQuery(ComponentType.ReadOnly<DrawnLineChoice>()))using(var owners=q.ToEntityArray(Allocator.Temp))foreach(var owner in owners)if(LineOwner(owner))manualDirty.Add(owner);}
}}
