using System.Collections.Generic;
using Colossal.Mathematics;
using Game.Net;
using Game.Prefabs;
using Unity.Entities;
using Unity.Mathematics;
namespace RoadStyleIntoKorea {
 public partial class StyleSystem {
  internal bool MenuSearchFocused;
  bool DetailRegionAllowed(Entity road,PavingRegion region){
   if(region==null)return false;if(region.Kind==1)return true;
   if(region.Kind!=2||!S.Paving||!EntityManager.HasBuffer<PavingChoice>(road))return false;
   var choices=new List<PavingChoice>();foreach(var choice in EntityManager.GetBuffer<PavingChoice>(road,true))choices.Add(choice);
   return PavingSelection(road,region,choices)>0;
  }
  float DetailSurfaceHeight(Entity road,PavingRegion region)=>CurbHeight(region)+(region.Kind==2&&CurbSelected(road,region)?NativeCurbGeometry.Rise:0);
  static string SidewalkDetailAsset(int kind)=>kind==9?"CSKRTactileLinear30cmNetLane":"CSKRCycleRedW1p5mNetLane";
  static float SidewalkDetailWidth(int kind)=>kind==9?.30f:1.80f;
  bool ResolveDetailOffset(Entity road,PavingRegion region,ManualDecalChoice choice,out float offset){
   offset=choice.Offset;if(!DetailRegionAllowed(road,region))return false;
   var p=region.Piece;
   return PavingPolicy.DetailOffset(PavingPolicy.Bay(PieceName(p.m_Piece)),p.m_Offset.x-p.m_Size.x*.5f,p.m_Offset.x+p.m_Size.x*.5f,
    region.PaintMin,region.PaintMax,SidewalkDetailWidth(choice.Kind)*.5f+.02f,choice.Offset,out offset);
  }
  PavingRegion detailHeightRegion;float detailHeight;
  List<Bezier4x3> SidewalkDetailCurves(Entity road,PavingRegion region,float offset){
   if(detailHeightRegion!=region){detailHeightRegion=region;detailHeight=DetailSurfaceHeight(road,region)+.015f;}
   var shape=EntityManager.GetComponentData<EdgeGeometry>(road);float t=.5f+offset/region.RoadWidth;
   int side=SideRoadRegionSide(new Geometry.Arc(CurveOf(road)),region);
   var result=new List<Bezier4x3>();
   foreach(var section in new[]{shape.m_Start,shape.m_End}){
    var curve=Between(section.m_Left,section.m_Right,t,detailHeight);
    var arc=new Geometry.Arc(curve);float start=0;
    foreach(var gap in SideRoadIntervals(road,curve,side)){
     if(gap.x-start>.025f)result.Add(arc.Slice(start,gap.x));start=math.max(start,gap.y);
    }
    if(arc.Length-start>.025f)result.Add(arc.Slice(start,arc.Length));
   }
   return result;
  }
  internal bool SidewalkDetailTool(Entity road,float3 hit,int kind,Game.Rendering.OverlayRenderSystem.Buffer draw,bool apply,bool reset){
   SignHover="detail.position";if(!S.Enabled)return false;var region=PickPaving(road,hit);if(!DetailRegionAllowed(road,region))return false;
   float width=SidewalkDetailWidth(kind),half=width*.5f+.02f;
   bool fits=region.PaintMax-region.PaintMin>=half*2;
   float t=region.FirstArc.Nearest(hit);var arc=region.FirstArc;bool second=false;
   float other=region.SecondArc.Nearest(hit);if(math.distancesq(Geometry.Position(region.Second,other),hit)<math.distancesq(Geometry.Position(region.First,t),hit)){arc=region.SecondArc;t=other;second=true;}
   var shape=EntityManager.GetComponentData<EdgeGeometry>(road);var section=second?shape.m_End:shape.m_Start;
   var left=Geometry.Position(section.m_Left,t);var right=Geometry.Position(section.m_Right,t);
   float offset=ManualPlacementPolicy.SidewalkOffset(hit,left,right,region.RoadWidth);

   offset=fits?math.clamp(offset,region.PaintMin+half,region.PaintMax-half):(region.PaintMin+region.PaintMax)*.5f;
   bool snapped=fits&&SnapSidewalkDetail(road,region,kind,ref offset);if(snapped)SignHover="detail.snapped";
   var prefab=assets.Get(SidewalkDetailAsset(kind));bool ready=Live(prefab)&&EntityManager.HasComponent<NetLaneGeometryData>(prefab);
   var color=fits&&ready?(snapped?new UnityEngine.Color(1,.75f,.15f,.85f):new UnityEngine.Color(.1f,.85f,1,.8f)):new UnityEngine.Color(1,.3f,.2f,.8f);
   foreach(var curve in SidewalkDetailCurves(road,region,offset))draw.DrawCurve(color,curve,width);
   if(!reset&&(!fits||!ready)){SignHover=!fits?"detail.narrow":"detail.missing";Status=!fits?"보도 폭 부족 / Sidewalk too narrow":"에셋 없음 / Missing: "+SidewalkDetailAsset(kind);return true;}
   if(!apply&&!reset)return true;
   var choice=new ManualDecalChoice{Kind=kind,T=.5f,Offset=offset,Width=width};
   localStyleCommands.Enqueue(()=>{
    if(!IsRoad(road))return;
    if(!EntityManager.HasBuffer<ManualDecalChoice>(road)){if(reset)return;EntityManager.AddBuffer<ManualDecalChoice>(road);}
    var choices=EntityManager.GetBuffer<ManualDecalChoice>(road);
    for(int i=choices.Length-1;i>=0;i--)if(choices[i].Kind==kind&&choices[i].Offset>=region.PaintMin-.05f&&choices[i].Offset<=region.PaintMax+.05f)choices.RemoveAt(i);
    if(!reset)choices.Add(choice);manualDirty.Add(road);InvalidateToolPreview();ObserveOwnerShape(road);
   });targetedActive=true;return true;
  }
  void PaintSidewalkDetail(Entity road,Record record,ManualDecalChoice choice){{
   var prefab=assets.Get(SidewalkDetailAsset(choice.Kind));if(!Live(prefab)||!EntityManager.HasComponent<NetLaneGeometryData>(prefab))return;
   foreach(var region in PavingRegions(road))if(ResolveDetailOffset(road,region,choice,out float offset)){
    foreach(var curve in SidewalkDetailCurves(road,region,offset)){PaintLane(road,record,prefab,curve);PaintSidewalkDetailSurface(road,record,choice.Kind,curve);}break;
   }
  }}
 }
}









