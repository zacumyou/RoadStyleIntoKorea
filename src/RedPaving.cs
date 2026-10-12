using System;
using System.Collections.Generic;
using Colossal.IO.AssetDatabase;
using Colossal.Mathematics;
using Game.Net;
using Game.Prefabs;
using Unity.Entities;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.Rendering.HighDefinition;
namespace RoadStyleIntoKorea {public partial class StyleSystem {
 internal int RedPavingBrush;
 internal string RedPavingHint="";
 bool redPavingMode;float redMaterialRetry;
 internal void CycleRedPaving()=>RedPavingBrush=1-RedPavingBrush;
 Material RedPavingMaterial(){
  const int key=-100;
  if(pavingMaterials.TryGetValue(key,out var cached))return cached;
  if(UnityEngine.Time.realtimeSinceStartup<redMaterialRetry)return null;
  redMaterialRetry=UnityEngine.Time.realtimeSinceStartup+2;
  var ps=World.GetExistingSystemManaged<PrefabSystem>();if(ps==null)return null;
  // Read original SurfaceAsset textures, never the batch texture hidden by HideTransportColor.
  foreach(var entry in assets.Named){
   if(!ps.TryGetPrefab<PrefabBase>(entry.Value,out var prefab)||!(prefab is RenderPrefab render))continue;
   foreach(var surface in render.surfaceAssets){
    if(surface==null||!(surface.name=="BusLane"||surface.name.StartsWith("BusLane_",StringComparison.Ordinal)))continue;
    var acquired=new List<TextureAsset>();Material material=null;
    try{
     surface.LoadProperties(false);
     if(!surface.TryGetTexture("_BaseColorMap",out var colorAsset)||colorAsset==null)continue;
     var template=AssetDatabase.global.resources.materialLibrary.GetMaterial("DefaultDecal");
     if(template==null)throw new InvalidOperationException("Native DefaultDecal template unavailable");
     // The library template does not initialize the shader feature keywords.
     material=new Material(template){name="RSIK red pavement (native BusLane)",hideFlags=HideFlags.HideAndDontSave};
     foreach(var property in new[]{"_BaseColorMap"})if(surface.TryGetTexture(property,out var texture)&&texture!=null){material.SetTexture(property,texture.Load());acquired.Add(texture);}
     material.SetColor("_BaseColor",surface.TryGetColorProperty("_BaseColor",out var color)?color:Color.white);
     material.SetFloat("_DecalBlend",1);
     material.SetFloat("_AffectAlbedo",1);
     // Installed DefaultDecal only ships the all-affects and no-affects variants.
     // The no-affects variant writes zero alpha, even with _AffectAlbedo=1.
     // Select the compiled non-VT variant; MRT write masks below keep this albedo-only.
     material.EnableKeyword("_MATERIAL_AFFECTS_ALBEDO");
     material.EnableKeyword("_MATERIAL_AFFECTS_NORMAL");
     material.EnableKeyword("_MATERIAL_AFFECTS_MASKMAP");
     material.DisableKeyword("ENABLE_VT");
     material.DisableKeyword("DOTS_INSTANCING_ON");
     material.DisableKeyword("_TANGENTSPACE_OCTO");
     material.SetFloat("_NormalOpacity",0);material.SetFloat("_MetallicOpacity",0);
     material.SetFloat("_DecalColorMask0",15);
     foreach(var property in new[]{"_DecalColorMask1","_DecalColorMask2","_DecalColorMask3"})material.SetFloat(property,0);
     material.SetFloat("colossal_DecalLayerMask",math.asfloat((uint)Game.Rendering.DecalLayers.Roads));
     material.SetFloat("_DrawOrder",-100);
     // Volume decals must retain their actual depth: bias is clip-space, not metres.
     material.SetFloat("_DecalMeshBiasType",0);material.SetFloat("_DecalMeshDepthBias",0);
     material.SetFloat("_DecalMeshViewBias",0);
     // Keep it in the opaque decal range, before ordinary marking decals.
     material.renderQueue=(int)UnityEngine.Rendering.RenderQueue.Geometry-100;
     if(material.FindPass("DBufferMesh")<0)throw new InvalidOperationException("Red pavement requires the depth-free DBufferMesh pass");
     pavingMaterials.Add(key,material);pavingTextures.AddRange(acquired);
     RedPavingHint="";return material;
    }catch(Exception ){foreach(var texture in acquired)texture.Unload();if(material!=null)UnityEngine.Object.Destroy(material);}
    finally{surface.Unload();}
   }
  }
  RedPavingHint="RSIK.RedPaving.Unavailable";return null;
 }
 internal void CancelRedPavingClick(){lineStarted=false;lineRoute.Clear();RedPavingHint="";}
 LinePiece RedCarriageway(LinePiece piece,PenLane reference){
  if(reference==null||!EntityManager.HasComponent<Game.Net.CarLane>(reference.Lane))return piece;
  var center=Geometry.Position(piece.Curve,.5f);var forward=Geometry.Tangent(piece.Curve,.5f);var across=Geometry.Left(forward);
  var nativeDirection=Geometry.Tangent(reference.Curve,reference.Arc.Nearest(center));
  int group=EntityManager.GetComponentData<Game.Net.CarLane>(reference.Lane).m_CarriagewayGroup&255;
  float min=-piece.Width*.5f,max=piece.Width*.5f;
  foreach(var candidate in penLanes){
   if(candidate.Owner!=piece.Road||!EntityManager.HasComponent<Game.Net.CarLane>(candidate.Lane)||(EntityManager.GetComponentData<Game.Net.CarLane>(candidate.Lane).m_CarriagewayGroup&255)!=group)continue;
   if(!RedPavingPolicy.CrossSection(candidate.Curve,center,forward,out float t))continue;
   var position=Geometry.Position(candidate.Curve,t);
   if(!ManualPlacementPolicy.SameDirection(nativeDirection,center,Geometry.Tangent(candidate.Curve,t),position))continue;
   float offset=math.dot(position-center,across);min=math.min(min,offset-candidate.Width*.5f);max=math.max(max,offset+candidate.Width*.5f);
  }
  piece.Curve=Geometry.Offset(piece.Curve,(min+max)*.5f);piece.Width=max-min;return piece;
 }
 PenLane RedReference(LinePiece piece){
  var center=Geometry.Position(piece.Curve,.5f);PenLane best=null;float distance=.25f;
  foreach(var candidate in penLanes){if(candidate.Owner!=piece.Road)continue;float t=candidate.Arc.Nearest(center);float d=math.distancesq(Geometry.Position(candidate.Curve,t),center);if(d<distance){distance=d;best=candidate;}}
  return best;
 }
 internal void RedPavingTool(Entity target,float3 hit,Game.Rendering.OverlayRenderSystem.Buffer overlay,bool press,bool reset,bool allLanes){
  if(!redPavingMode){EndLineEdit();redPavingMode=true;ColorGuideMode=true;}
  if(!S.Enabled||!ready||!LineOwner(target)){if(press||reset)CancelRedPavingClick();return;}
  CacheLinePoints(target);
  if(reset){if(lineStarted){lineStarted=false;return;}foreach(var entry in lineHits){if(!entry.Arc.MayBeNear(hit,entry.Width*.5f))continue;float t=entry.Arc.Nearest(hit);if(math.distance(Geometry.Position(entry.Curve,t).xz,hit.xz)<=entry.Width*.5f){QueueDrawnLine(entry.Owner,entry.Index,default,0,true,false);return;}}return;}
  if(!lineStarted)foreach(var entry in lineHits)if(entry.Arc.MayBeNear(hit,entry.Width*.5f)&&math.distance(Geometry.Position(entry.Curve,entry.Arc.Nearest(hit)).xz,hit.xz)<entry.Width*.5f)overlay.DrawCurve(new Color(1,.45f,.05f,.8f),entry.Curve,entry.Width);
  if(!SnapRedEndpoint(hit,out var point,out var owner)&&!PenSnap(hit,out point,out owner,true)){if(press)CancelRedPavingClick();return;}
  if(allLanes&&lineStarted&&lineStartOwner==owner&&lineStart.Lane!=point.Lane){
   var startLane=penLanes.Find(p=>p.Lane==lineStart.Lane);
   if(startLane!=null&&EntityManager.HasComponent<Game.Net.CarLane>(point.Lane)&&EntityManager.HasComponent<Game.Net.CarLane>(lineStart.Lane)&&
      (EntityManager.GetComponentData<Game.Net.CarLane>(point.Lane).m_CarriagewayGroup&255)==(EntityManager.GetComponentData<Game.Net.CarLane>(lineStart.Lane).m_CarriagewayGroup&255)){
    float t=startLane.Arc.Nearest(point.Position);var tangent=Geometry.Tangent(startLane.Curve,t);
    if(math.dot(tangent,point.Tangent)>.8f){point.Lane=startLane.Lane;point.T=t;point.Position=Geometry.Position(startLane.Curve,t);point.Tangent=tangent;}
   }
  }
  var lane=penLanes.Find(p=>p.Lane==point.Lane);if(lane==null)return;
  var cursor=new LinePiece{Road=owner,Curve=Geometry.Line(point.Position-point.Tangent*.1f,point.Position+point.Tangent*.1f),Width=point.RedCapWidth>0?point.RedCapWidth:lane.Width};
  if(point.RedCapWidth<=0){if(allLanes)cursor=RedCarriageway(cursor,lane);cursor=RedFillCenterGap(cursor);}
  var across=Geometry.Left(point.Tangent)*cursor.Width*.5f;var cursorCenter=Geometry.Position(cursor.Curve,.5f);
  overlay.DrawCurve(new Color(.85f,.12f,.1f,1),Geometry.Line(cursorCenter-across,cursorCenter+across),.12f);
  DrawGuidePoint(overlay,point.Position,Color.white);
  bool valid=false;
  if(lineStarted){
   DrawGuidePoint(overlay,lineStart.Position,Color.white);
   // No free connection, lateral lane change, road-center fallback or opposite carriageway.
   valid=(lineStartOwner!=owner||lineStart.Lane==point.Lane)&&(lineStart.Lane==point.Lane||math.dot(lineStart.Tangent,point.Tangent)>.5f)&&PenRoute(lineStart,point);
   if(valid&&allLanes)for(int i=0;i<lineRoute.Count;i++)lineRoute[i]=RedCarriageway(lineRoute[i],RedReference(lineRoute[i]));
   if(valid)for(int i=0;i<lineRoute.Count;i++)lineRoute[i]=RedFillCenterGap(lineRoute[i]);
   if(valid)AlignRedCaps(lineStart,point);
   if(valid){float total=0;foreach(var p in lineRoute)total+=new Geometry.Arc(p.Curve).Length;float phase=RedPavingBrush==1?RedPavingPolicy.Phase(total):0;
    foreach(var p in lineRoute){var arc=new Geometry.Arc(p.Curve);foreach(var span in RedPavingPolicy.Spans(arc.Length,RedPavingBrush==1,phase))overlay.DrawCurve(new Color(.85f,.1f,.08f,.6f),arc.Slice(span.x,span.y),p.Width);phase+=arc.Length;}}
   RedPavingHint=valid?"":"RSIK.RedPaving.SameLane";
  }else RedPavingHint="";
  if(!press)return;
  if(!lineStarted){lineStart=point;lineStartOwner=owner;lineStarted=true;return;}
  if(valid&&math.distance(lineStart.Position,point.Position)>.25f)QueueRedPaving();
  CancelRedPavingClick();
 }
 void QueueRedPaving(){
  if(lineMutationPending)return;var pieces=lineRoute.ToArray();int style=RedPavingBrush==0?RedPavingPolicy.Solid:RedPavingPolicy.Striped;lineMutationPending=true;
  localStyleCommands.Enqueue(()=>{try{
   if(RedPavingMaterial()==null)return;
   var counts=new Dictionary<Entity,int>();float total=0;
   foreach(var p in pieces){if(!LineOwner(p.Road)||p.Width<=0||p.Width>32)return;counts.TryGetValue(p.Road,out int count);counts[p.Road]=count+1;total+=new Geometry.Arc(p.Curve).Length;}
   foreach(var pair in counts)if(pair.Value+(EntityManager.HasBuffer<DrawnLineChoice>(pair.Key)?EntityManager.GetBuffer<DrawnLineChoice>(pair.Key,true).Length:0)>128)return;
   float phase=style==RedPavingPolicy.Striped?RedPavingPolicy.Phase(total):0;long group=BitConverter.ToInt64(Guid.NewGuid().ToByteArray(),0);
   foreach(var p in pieces){var choices=EntityManager.HasBuffer<DrawnLineChoice>(p.Road)?EntityManager.GetBuffer<DrawnLineChoice>(p.Road):EntityManager.AddBuffer<DrawnLineChoice>(p.Road);
    choices.Add(new DrawnLineChoice{Style=style,Group=group,Local=LineTransform(p.Road,p.Curve,true),Width=p.Width,Phase=phase});phase+=new Geometry.Arc(p.Curve).Length;manualDirty.Add(p.Road);}
   InvalidateToolPreview();lineRevision=-1;targetedActive=true;  }finally{lineMutationPending=false;}});
 }
 SidewalkFootprint RedRoadSurface(Entity owner){
  if(!EntityManager.HasComponent<EdgeGeometry>(owner)||!EntityManager.HasComponent<Composition>(owner))return null;
  var cp=EntityManager.GetComponentData<Composition>(owner).m_Edge;
  if(!EntityManager.HasBuffer<NetCompositionPiece>(cp)||!EntityManager.HasComponent<NetCompositionData>(cp))return null;
  float width=EntityManager.GetComponentData<NetCompositionData>(cp).m_Width;var shape=EntityManager.GetComponentData<EdgeGeometry>(owner);var result=new SidewalkFootprint();
  foreach(var piece in EntityManager.GetBuffer<NetCompositionPiece>(cp,true)){
   if((piece.m_SectionFlags&(NetSectionFlags.Hidden|NetSectionFlags.HiddenSurface|NetSectionFlags.Underground))!=0||(piece.m_PieceFlags&(NetPieceFlags.Side|NetPieceFlags.Bottom|NetPieceFlags.HasRoadLanes))!=NetPieceFlags.HasRoadLanes)continue;
   var template=NativePavingTemplate(piece.m_Piece);if(template==null)continue;
   var sign=new float2((piece.m_SectionFlags&NetSectionFlags.Invert)!=0?1:-1,(piece.m_SectionFlags&NetSectionFlags.FlipMesh)!=0?1:-1);var offset=piece.m_Offset;
   if((piece.m_SectionFlags&NetSectionFlags.HalfLength)!=0){sign.y*=2;offset.z+=piece.m_Size.z*.5f;}
   PavingMeshGeometry.Build(template.Vertices,template.AllIndices,offset,sign,piece.m_Size.z,width,-width*.5f,width*.5f,shape.m_Start.m_Left,shape.m_Start.m_Right,shape.m_End.m_Left,shape.m_End.m_Right,out var vertices,out var indices,out var uv);
   result.Add(vertices,indices);
  }return result;
 }
 void BuildRedPaving(Entity owner,Record record,DrawnLineChoice choice,SidewalkFootprint surface){
  var material=RedPavingMaterial();if(material==null||!LineOwner(owner)||choice.Width<=0){return;}
  int before=record.PavingSurfaces.Count;
  var curve=LineTransform(owner,choice.Local,false);var arc=new Geometry.Arc(curve);
  foreach(var span in RedPavingPolicy.Spans(arc.Length,choice.Style==RedPavingPolicy.Striped,choice.Phase)){
   // Native DefaultDecal projects inside a closed volume, not a surface ribbon.
   for(float start=span.x;start<span.y-.001f;start+=4)
    CreateRedPavingVolume(record,material,arc,start,math.min(start+4,span.y),choice.Width,choice.Phase,surface);
  }
 }
}}
