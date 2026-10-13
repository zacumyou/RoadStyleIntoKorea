using System.Collections.Generic;
using Colossal.Mathematics;
using Colossal.Serialization.Entities;
using Game.Net;
using Game.Prefabs;
using Unity.Entities;
using Unity.Mathematics;
namespace RoadStyleIntoKorea {
 public struct GutterChoice:IBufferElementData,ISerializable {
  public int Kind,Style;public float Min,Max;
  public void Serialize<T>(T w) where T:IWriter{w.Write(1);w.Write(Kind);w.Write(Style);w.Write(Min);w.Write(Max);}
  public void Deserialize<T>(T r) where T:IReader{r.Read(out int v);r.Read(out Kind);r.Read(out Style);r.Read(out Min);r.Read(out Max);if(v!=1||Style<1||Style>2||!math.all(math.isfinite(new float2(Min,Max)))||Max<=Min)Style=0;}
 }
 public partial class StyleSystem {
  internal int GutterBrush=1;
  internal void CycleGutterBrush(){GutterBrush=3-GutterBrush;}
  static string GutterAsset(int style)=>"CSKRConcreteGutter"+(style==2?"B":"A")+"20mNetLane";
  static void GutterLimits(PavingRegion r,out float min,out float max){min=r.Min;max=r.Max;foreach(var g in r.Group){min=math.min(min,g.Min);max=math.max(max,g.Max);}}
  static bool GutterMatches(GutterChoice c,PavingRegion r){GutterLimits(r,out float min,out float max);return c.Kind==r.Kind&&math.abs(c.Min-min)<.1f&&math.abs(c.Max-max)<.1f;}
  bool HasGutterSelection(Entity edge){
   if(!EntityManager.HasBuffer<GutterChoice>(edge))return false;
   foreach(var choice in EntityManager.GetBuffer<GutterChoice>(edge,true))if(choice.Style!=0)return true;
   return false;
  }
  int GutterSelected(Entity edge,PavingRegion r){if(EntityManager.HasBuffer<GutterChoice>(edge))foreach(var c in EntityManager.GetBuffer<GutterChoice>(edge,true))if(GutterMatches(c,r))return c.Style;return 0;}
  // Native raised surfaces are checked after deformation. Only synthetic shoulder
  // curbs need an explicit profile reserve, because they are not in the native mask.
  float GutterCurbBand(Entity edge,PavingRegion region)=>region.Kind==2&&CurbSelected(edge,region)?NativeCurbGeometry.RaisedBand:0;
  List<Bezier4x3> GutterEdgeCurves(Entity edge,PavingRegion region){
   var result=new List<Bezier4x3>();var shape=EntityManager.GetComponentData<EdgeGeometry>(edge);
   // Same paving border as the curb, whether or not CurbChoice exists.
   CurbLimits(region,out float min,out float max);bool right=min+max>=0;
   float height=CurbHeight(region)-(region.Kind==1?NativeCurbGeometry.Rise:0);
   float band=GutterCurbBand(edge,region);
   foreach(var s in new[]{shape.m_Start,shape.m_End})result.AddRange(GutterClearance.Fit(GutterPolicy.Curve(Between(s.m_Left,s.m_Right,.5f+min/region.RoadWidth,height),Between(s.m_Left,s.m_Right,.5f+max/region.RoadWidth,height),right,band),EdgeSidewalks(edge),region.Kind==1));
   return result;
  }
  internal bool GutterTool(Entity edge,float3 hit,Game.Rendering.OverlayRenderSystem.Buffer draw,bool apply,bool reset){
   var region=PickGutter(edge,hit);if(region==null||!S.Enabled)return false;
   foreach(var candidate in gutterHover)if(candidate.Region==region)draw.DrawCurve(new UnityEngine.Color(.1f,.85f,1,.7f),candidate.Arc.Curve,GutterPolicy.Width);
   if(reset&&GutterSelected(edge,region)==0)return false;
   if(!apply&&!reset)return true;
   int style=GutterBrush;
   if(!reset){
    if(assets.Get(GutterAsset(style))==Entity.Null)assets.Refresh(EntityManager,World.GetExistingSystemManaged<PrefabSystem>());
    var p=assets.Get(GutterAsset(style));if(!Live(p)||!EntityManager.HasComponent<NetLaneGeometryData>(p)){Status="배수로 에셋 누락 / Missing: "+GutterAsset(style);return true;}
   }
   GutterLimits(region,out float min,out float max);var choice=new GutterChoice{Kind=region.Kind,Style=style,Min=min,Max=max};
   localStyleCommands.Enqueue(()=>{
    if(!IsRoad(edge))return;
    if(!EntityManager.HasBuffer<GutterChoice>(edge)){if(reset)return;EntityManager.AddBuffer<GutterChoice>(edge);}
    var choices=EntityManager.GetBuffer<GutterChoice>(edge);int index=-1;
    for(int i=0;i<choices.Length;i++)if(GutterMatches(choices[i],region)){index=i;break;}
    if(reset){if(index>=0)choices.RemoveAt(index);}else if(index>=0)choices[index]=choice;else choices.Add(choice);
    EnqueueCurb(edge);Status=reset?"배수로 제거 / Gutter removed":"배수로 적용 / Gutter applied";
   });targetedActive=true;return true;
  }
  Entity gutterHoverEdge;int gutterHoverRevision=-1;
  readonly List<(PavingRegion Region,Geometry.Arc Arc)> gutterHover=new List<(PavingRegion,Geometry.Arc)>();
  PavingRegion PickGutter(Entity edge,float3 hit){
   if(edge!=gutterHoverEdge||gutterHoverRevision!=toolRevision){
    gutterHoverEdge=edge;gutterHoverRevision=toolRevision;gutterHover.Clear();
    if(!IsRoad(edge)||!EntityManager.HasComponent<EdgeGeometry>(edge))return null;
    foreach(var r in PavingRegions(edge)){
     GutterLimits(r,out float min,out _);if(math.abs(r.Min-min)>.01f)continue;
     foreach(var curve in GutterEdgeCurves(edge,r))gutterHover.Add((r,new Geometry.Arc(curve)));
    }
   }
   PavingRegion best=null;float closest=float.MaxValue;
   foreach(var candidate in gutterHover)if(GutterPolicy.Hit(candidate.Arc,hit,out float distance)&&distance<closest){best=candidate.Region;closest=distance;}
   return best;
  }
  void PaintGutter(Entity owner,Record record,int style,Bezier4x3 curve){
   var prefab=assets.Get(GutterAsset(style));if(!Live(prefab)||!EntityManager.HasComponent<NetLaneGeometryData>(prefab)){Status="배수로 에셋 누락 / Missing: "+GutterAsset(style);return;}
   if(IsRoad(owner)){
    foreach(var clear in BoundaryPaint(owner,curve,GutterPolicy.Width*.5f+.15f))PaintLane(owner,record,prefab,clear);
   }else PaintLane(owner,record,prefab,curve);
  }
  void BuildEdgeGutters(Entity edge,Record record){
   if(!HasGutterSelection(edge)||!EntityManager.HasComponent<EdgeGeometry>(edge))return;
   Geometry.Arc sideRoadArc=EntityManager.HasBuffer<SideRoadChoice>(edge)&&EntityManager.GetBuffer<SideRoadChoice>(edge,true).Length>0?new Geometry.Arc(CurveOf(edge)):null;
   foreach(var region in PavingRegions(edge)){
    GutterLimits(region,out float min,out float max);if(math.abs(region.Min-min)>.01f)continue;
    int style=GutterSelected(edge,region);if(style==0)continue;
    foreach(var curve in GutterEdgeCurves(edge,region)){
     var arc=new Geometry.Arc(curve);float start=0;
     foreach(var gap in SideRoadIntervals(edge,curve,sideRoadArc==null?0:SideRoadRegionSide(sideRoadArc,region))){
      float before=math.max(start,gap.x-.4f);
      if(before-start>.05f)PaintGutter(edge,record,style,arc.Slice(start,before));
      if(gap.x>start)PaintSideRoadGutterCap(edge,record,style,arc.Slice(before,math.min(arc.Length,gap.x+.3f)));
      float after=math.min(arc.Length,gap.y+.4f);
      if(gap.y<arc.Length)PaintSideRoadGutterCap(edge,record,style,Geometry.Reverse(arc.Slice(math.max(0,gap.y-.3f),after)));
      start=after;
     }
     if(arc.Length-start>.05f)PaintGutter(edge,record,style,arc.Slice(start,arc.Length));
    }
   }
  }
  void PaintSideRoadGutterCap(Entity road,Record record,int style,Bezier4x3 curve){
   SideRoadOpening nearest=null;float best=float.MaxValue;
   foreach(var opening in SideRoadOpenings(road)){
    float t=opening.Road.Nearest(curve.d);var center=Geometry.Position(opening.Road.Curve,t);
    if(math.dot(curve.d-center,Geometry.Left(Geometry.Tangent(opening.Road.Curve,t)))*opening.Side<0)continue;
    float d=math.abs(opening.Road.Distance(t)-opening.Distance);if(d<best){best=d;nearest=opening;}
   }
   if(nearest!=null){curve.c.y=math.max(curve.c.y,nearest.SurfaceHeight(curve.c)+.028f);curve.d.y=math.max(curve.d.y,nearest.SurfaceHeight(curve.d)+.028f);}
   PaintGutter(road,record,style,curve);
  }
  void BuildCornerGutters(Entity node,Record record){
   // Native junction strips may fold across the carriageway. Only join fitted
   // endpoints at a straight two-road seam; never project gutters onto node corners.
   BuildGutterSeam(node,record);
  }
 }
}
