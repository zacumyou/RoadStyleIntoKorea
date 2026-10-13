using System.Collections.Generic;
using Game.Net;
using Unity.Entities;
using Unity.Mathematics;
namespace RoadStyleIntoKorea {public partial class StyleSystem {
 sealed class GutterEnd {internal float3 Point,Into;internal int Style;internal bool Reverse;}
 GutterEnd[] GutterEndsAt(Entity road,Entity node,float3 axis){
  var origin=EntityManager.GetComponentData<Node>(node).m_Position;
  var best=new GutterEnd[2];var distance=new[]{float.MaxValue,float.MaxValue};
  if(!HasGutterSelection(road)||!EntityManager.HasComponent<EdgeGeometry>(road))return best;
  foreach(var region in PavingRegions(road)){
   int style=GutterSelected(road,region);if(style==0)continue;
   GutterLimits(region,out float min,out _);if(math.abs(region.Min-min)>.01f)continue;
   foreach(var curve in GutterEdgeCurves(road,region))foreach(bool start in new[]{true,false}){
    var point=start?curve.a:curve.d;
    int side=math.dot(point-origin,Geometry.Left(axis))>0?1:0;
    float d=math.distancesq(point,origin);if(d>=distance[side])continue;
    distance[side]=d;best[side]=new GutterEnd{Point=point,Into=Geometry.Tangent(curve,start?0:1)*(start?-1:1),Style=style,Reverse=start};
   }
  }
  return best;
 }
 bool BuildGutterSeam(Entity node,Record record){
  var roads=new List<Entity>();
  foreach(var item in EntityManager.GetBuffer<ConnectedEdge>(node,true)){
   if(!IsRoad(item.m_Edge))return false;
   if(!roads.Contains(item.m_Edge))roads.Add(item.m_Edge);
  }
  if(roads.Count!=2)return false;
  var a=EntityManager.GetComponentData<Edge>(roads[0]);var b=EntityManager.GetComponentData<Edge>(roads[1]);
  var into=Geometry.Tangent(CurveOf(roads[0]),a.m_Start==node?0:1)*(a.m_Start==node?-1:1);
  var away=Geometry.Tangent(CurveOf(roads[1]),b.m_Start==node?0:1)*(b.m_Start==node?1:-1);
  if(math.dot(into,away)<.97f)return false;
  if(!HasGutterSelection(roads[0])||!HasGutterSelection(roads[1]))return true;
  // Collapsed native node strips can fold at a two-edge seam. Join the actual
  // fitted edge gutters instead; coincident endpoints need no extra decal.
  var firstEnds=GutterEndsAt(roads[0],node,into);var lastEnds=GutterEndsAt(roads[1],node,into);
  foreach(int sideIndex in new[]{1,0}){
   var first=firstEnds[sideIndex];var last=lastEnds[sideIndex];
   if(first==null||last==null)continue;
   if(!EdgeJoinPolicy.TryCurve(first.Point,first.Into,last.Point,-last.Into,out var curve))continue;
   var arc=new Geometry.Arc(curve);
   for(int side=0;side<(first.Style==last.Style?1:2);side++){
    var piece=first.Style==last.Style?curve:arc.Slice(arc.Length*side/2,arc.Length*(side+1)/2);
    if(first.Reverse)piece=Geometry.Reverse(piece);
    foreach(var clear in GutterClearance.Fit(piece,EdgeSidewalks(node)))PaintGutter(node,record,side==0?first.Style:last.Style,clear);
   }
  }
  return true;
 }
}}
