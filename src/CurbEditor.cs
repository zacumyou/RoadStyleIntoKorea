using System.Collections.Generic;
using Colossal.Serialization.Entities;
using Game.Common;
using Game.Net;
using Game.Prefabs;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
namespace RoadStyleIntoKorea {
 public partial class StyleSystem {
  readonly HashSet<Entity> curbDirty=new HashSet<Entity>();readonly Dictionary<Entity,Record> curbRecords=new Dictionary<Entity,Record>();
  static void CurbLimits(PavingRegion r,out float min,out float max){min=r.PaintMin;max=r.PaintMax;foreach(var g in r.Group){min=math.min(min,g.PaintMin);max=math.max(max,g.PaintMax);}}
  // Match the native pavement top, not the highway piece origin.
  float CurbHeight(PavingRegion region){
   var template=NativePavingTemplate(region.Piece.m_Piece);
   if(template==null||template.Indices.Length==0)return region.Height;
   float top=float.MinValue;foreach(var i in template.Indices)top=math.max(top,template.Vertices[i].y);
   return region.Height+top;
  }
  bool CurbSelected(Entity edge,PavingRegion region){if(!EntityManager.HasBuffer<CurbChoice>(edge))return false;CurbLimits(region,out float min,out float max);foreach(var c in EntityManager.GetBuffer<CurbChoice>(edge,true))if(c.Kind==region.Kind&&math.abs(c.Min-min)<.1f&&math.abs(c.Max-max)<.1f)return true;return false;}
  void EnqueueCurb(Entity edge){curbDirty.Add(edge);if(EntityManager.HasComponent<Edge>(edge)){var e=EntityManager.GetComponentData<Edge>(edge);curbDirty.Add(e.m_Start);curbDirty.Add(e.m_End);}targetedActive=true;}
  internal bool CurbTool(Entity edge,float3 hit,Game.Rendering.OverlayRenderSystem.Buffer draw,bool apply,bool reset){
   var region=PickPaving(edge,hit);if(region==null)return false;CurbLimits(region,out float min,out float max);
   if(!S.Enabled||!CachedAssetsReady())return false;
   var choice=new CurbChoice{Kind=region.Kind,Min=min,Max=max};var shape=EntityManager.GetComponentData<EdgeGeometry>(edge);
   bool fromA=min+max>=0;if(region.Kind==1){if(fromA)min-=.6f;else max+=.6f;}
   foreach(var s in new[]{shape.m_Start,shape.m_End}){var a=Between(s.m_Left,s.m_Right,.5f+min/region.RoadWidth,CurbHeight(region));var b=Between(s.m_Left,s.m_Right,.5f+max/region.RoadWidth,CurbHeight(region));draw.DrawCurve(new UnityEngine.Color(.1f,.85f,1,.8f),CurbLaneGeometry.Curve(a,b,fromA,0),.35f);}
   if(reset&&!CurbSelected(edge,region))return false;
   if(apply||reset)localStyleCommands.Enqueue(()=>{
    if(!IsRoad(edge))return;if(!EntityManager.HasBuffer<CurbChoice>(edge))EntityManager.AddBuffer<CurbChoice>(edge);var choices=EntityManager.GetBuffer<CurbChoice>(edge);int index=-1;
    for(int i=0;i<choices.Length;i++)if(choices[i].Kind==choice.Kind&&math.abs(choices[i].Min-choice.Min)<.1f&&math.abs(choices[i].Max-choice.Max)<.1f){index=i;break;}
    if(reset){if(index>=0)choices.RemoveAt(index);}else if(index>=0)choices[index]=choice;else choices.Add(choice);EnqueueCurb(edge);EnqueuePaving(edge);var ends=EntityManager.GetComponentData<Edge>(edge);EnqueuePaving(ends.m_Start);EnqueuePaving(ends.m_End);
   });return true;
  }
  void ReleaseCurb(Entity owner){if(!curbRecords.TryGetValue(owner,out var r))return;foreach(var e in r.Paint)RetirePaint(e);foreach(var surface in r.PavingSurfaces)surface.Dispose();r.PavingSurfaces.Clear();r.PavingFaces.Clear();curbRecords.Remove(owner);}
  void ClearCurbs(){curbDirty.Clear();foreach(var owner in new List<Entity>(curbRecords.Keys))ReleaseCurb(owner);}
  bool TickCurbs(){{
   if(curbDirty.Count==0)return false;Entity owner=Entity.Null;foreach(var e in curbDirty){owner=e;break;}
   if(Live(owner)&&!CachedAssetsReady())return false;
   if(curbRecords.TryGetValue(owner,out var old))foreach(var e in old.Paint)if(Live(e)&&EntityManager.HasComponent<Created>(e))return false;
   EntityManager.CompleteAllTrackedJobs();curbDirty.Remove(owner);ReleaseCurb(owner);if(!Live(owner))return true;
   var r=new Record();curbRecords[owner]=r;
   try{if(IsRoad(owner)){BuildEdgeCurbs(owner,r);BuildEdgeGutters(owner,r);}else if(SafeNode(owner)){BuildCornerCurbs(owner,r);BuildCornerGutters(owner,r);}}catch(System.Exception ){failed++;ReleaseCurb(owner);}
   ObserveOwnerShape(owner);return true;
  }}
  bool CurbRepresentative(PavingRegion r){CurbLimits(r,out float min,out _);return math.abs(r.PaintMin-min)<.01f;}
  void BuildEdgeCurbs(Entity edge,Record r){
   if(!EntityManager.HasBuffer<CurbChoice>(edge)||EntityManager.GetBuffer<CurbChoice>(edge,true).Length==0||!EntityManager.HasComponent<EdgeGeometry>(edge))return;var shape=EntityManager.GetComponentData<EdgeGeometry>(edge);
   Geometry.Arc sideRoadArc=EntityManager.HasBuffer<SideRoadChoice>(edge)&&EntityManager.GetBuffer<SideRoadChoice>(edge,true).Length>0?new Geometry.Arc(CurveOf(edge)):null;
   foreach(var region in PavingRegions(edge))if(region.Kind==2&&CurbRepresentative(region)&&CurbSelected(edge,region)){
    CurbLimits(region,out float min,out float max);bool fromA=min+max>=0;if(region.Kind==1){if(fromA)min-=.6f;else max+=.6f;}
    foreach(var s in new[]{shape.m_Start,shape.m_End}){
     var a=Between(s.m_Left,s.m_Right,.5f+min/region.RoadWidth,CurbHeight(region));
     var b=Between(s.m_Left,s.m_Right,.5f+max/region.RoadWidth,CurbHeight(region));
     var border=fromA?a:b;var arc=new Geometry.Arc(border);float start=0;
     var gaps=SideRoadIntervals(edge,border,sideRoadArc==null?0:SideRoadRegionSide(sideRoadArc,region));
     foreach(var gap in gaps){if(gap.x-start>.05f){float lo=arc.T(start),hi=arc.T(gap.x);PaintCurbLane(edge,r,Geometry.Cut(a,lo,hi),Geometry.Cut(b,lo,hi),fromA,0);}start=gap.y;}
     if(arc.Length-start>.05f){float lo=arc.T(start);PaintCurbLane(edge,r,Geometry.Cut(a,lo,1),Geometry.Cut(b,lo,1),fromA,0);}
    }
   }
  }
  void BuildCornerCurbs(Entity node,Record r){
   var edges=new List<Entity>();foreach(var e in EntityManager.GetBuffer<ConnectedEdge>(node,true))edges.Add(e.m_Edge);
   foreach(var edge in edges){
    if(!IsRoad(edge)||!EntityManager.HasBuffer<CurbChoice>(edge)||EntityManager.GetBuffer<CurbChoice>(edge,true).Length==0||!EntityManager.HasComponent<Composition>(edge))continue;
    bool start=EntityManager.GetComponentData<Edge>(edge).m_Start==node;
    if(start&&!EntityManager.HasComponent<StartNodeGeometry>(edge)||!start&&!EntityManager.HasComponent<EndNodeGeometry>(edge))continue;
    var cp=EntityManager.GetComponentData<Composition>(edge);var composition=start?cp.m_StartNode:cp.m_EndNode;if(!EntityManager.HasComponent<NetCompositionData>(composition))continue;
    var data=EntityManager.GetComponentData<NetCompositionData>(composition);var shape=start?EntityManager.GetComponentData<StartNodeGeometry>(edge).m_Geometry:EntityManager.GetComponentData<EndNodeGeometry>(edge).m_Geometry;
    if(shape.m_MiddleRadius>0||data.m_Width<1)continue;
    foreach(var region in PavingRegions(edge))if(region.Kind==2&&CurbRepresentative(region)&&CurbSelected(edge,region)){
     CurbLimits(region,out float lo,out float hi);float min=start?-hi:lo,max=start?-lo:hi;bool right=min+max>=0;
     float shift=(data.m_Width-region.RoadWidth)*.5f*(right?1:-1);min+=shift;max+=shift;if(region.Kind==1){if(right)min-=.6f;else max+=.6f;}
     float split=data.m_MiddleOffset;var s=right?shape.m_Right:shape.m_Left;float origin=right?split:-data.m_Width*.5f;float width=right?data.m_Width*.5f-split:split+data.m_Width*.5f;
     if(width<=0||min<origin-.01f||max>origin+width+.01f)continue;
     min=math.max(min,origin);max=math.min(max,origin+width);
     PaintNativeCornerCurbs(node,r,composition,s,origin,width,min,max,right,right?shape.m_SyncVertexTargetsRight:shape.m_SyncVertexTargetsLeft,CurbHeight(region));
    }
   }
  }
 }
}



