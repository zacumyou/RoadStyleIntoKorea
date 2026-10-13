using System.Collections.Generic;using Colossal.Mathematics;using Game.Net;using Unity.Entities;using Unity.Mathematics;
namespace RoadStyleIntoKorea { public partial class StyleSystem {
void JoinCenterEnds(Entity node,Record record,bool inspect=false){
if(!SafeNode(node))return;var roads=new HashSet<Entity>();foreach(var e in EntityManager.GetBuffer<ConnectedEdge>(node,true))if(IsRoad(e.m_Edge))roads.Add(e.m_Edge);if(roads.Count==2){JoinCenterSeam(node,record,new List<Entity>(roads),inspect);return;}if(roads.Count!=1)return;foreach(var road in roads)JoinDeadEnd(node,record,road,true,inspect);
}
void JoinDeadEnd(Entity node,Record record,Entity road,bool central,bool inspect){
if(central&&(!CenterDivided(road)||!EntityManager.HasComponent<CenterChoice>(road)))return;
var centerChoice=EntityManager.HasComponent<CenterChoice>(road)?EntityManager.GetComponentData<CenterChoice>(road):default;
var edgeChoice=EntityManager.HasComponent<EdgeChoice>(road)?EntityManager.GetComponentData<EdgeChoice>(road):default;
var roadArc=new Geometry.Arc(CurveOf(road));var ribbons=EdgeRoadRibbons(node);var sidewalks=EdgeSidewalks(node);
foreach(var lane in Lanes(node)){
if(!EntityManager.HasComponent<CarLane>(lane)||EntityManager.HasComponent<MasterLane>(lane))continue;
var flags=EntityManager.GetComponentData<CarLane>(lane).m_Flags;
if((flags&CarLaneFlags.UTurnLeft)==0||(flags&(central?CarLaneFlags.LeftLimit:CarLaneFlags.RightLimit))==0)continue;
var source=CurveOf(lane);float st=roadArc.Nearest(source.a),et=roadArc.Nearest(source.d);
bool startLeft=math.dot(source.a-Geometry.Position(roadArc.Curve,st),Geometry.Left(Geometry.Tangent(roadArc.Curve,st)))>=0;
bool endLeft=math.dot(source.d-Geometry.Position(roadArc.Curve,et),Geometry.Left(Geometry.Tangent(roadArc.Curve,et)))>=0;
for(int side=0;side<2;side++){
bool left=side==0?startLeft:endLeft;int code=central?(left?centerChoice.Left:centerChoice.Right):(left?edgeChoice.Left:edgeChoice.Right);
var style=code==0?(central||!active?EdgeStyle.Original:S.DefaultEdge):(EdgeStyle)(code-1);if(style==EdgeStyle.None||style==EdgeStyle.Original)continue;
float half=central?CenterLinePolicy.HalfWidth((int)style):EdgePaintHalfWidth(style);float offset=central?math.max(0,Width(lane)*.5f-half-.05f):-EdgeTaperPolicy.OutsideOffset(Width(lane),half,S.EdgeLaneGap);
System.Func<float3,bool> fits=p=>{foreach(var ribbon in ribbons)if(ribbon.ContainsPaint(p,half+.08f))return true;return false;};
var sections=TurnBoundaryPolicy.Offset(source,offset,central?null:fits);
if(!central)JoinDeadEndEdgeGap(node,record,road,style,left,side==0?sections[0]:Geometry.Reverse(sections[7]),inspect);
for(int i=side*4;i<(side+1)*4;i++){
foreach(var safe in RoadOnlyEdge(node,style,sections[i],ribbons,sidewalks,half))foreach(var clear in BoundaryPaint(node,safe,S.PaintClearance)){
if(inspect){continue;}
int before=record.Paint.Count;if(central)PaintLane(node,record,assets.Get(CenterLinePolicy.Asset((int)style)),clear);else PaintDrivewayEdgeCore(node,record,style,clear,Driveways(node));if(!central)for(int j=before;j<record.Paint.Count;j++)record.EdgeJoins.Add(record.Paint[j]);
}
}
}
}
}
void JoinDeadEndEdgeGap(Entity node,Record record,Entity road,EdgeStyle style,bool left,Bezier4x3 turn,bool inspect){
 var lanes=Lanes(road);var paths=new List<EdgeBoundary>();Edges(road,record,lanes,Drives(lanes),paths);
 var crossings=Crossings(road,lanes);var context=new EdgeClearanceContext{Owner=road,Ribbons=EdgeRoadRibbons(road)};
 float best=64;Bezier4x3 join=default;bool found=false;
 foreach(var path in paths){
  if(path.Left!=left||path.Style!=style)continue;
  foreach(var visible in EdgePaintParts(road,style,path.Curve,crossings,context)){
   bool end=math.distancesq(visible.d,turn.a)<math.distancesq(visible.a,turn.a);var point=end?visible.d:visible.a;
   float distance=math.distancesq(point,turn.a);if(distance>=best||distance<.0009f)continue;
   var into=Geometry.Tangent(visible,end?1:0)*(end?1:-1);
   if(!EdgeJoinPolicy.TryCurve(point,into,turn.a,Geometry.Tangent(turn,0),out var candidate))continue;
   best=distance;join=candidate;found=true;
  }
 }
 if(found){
  // The bridge straddles the edge/node boundary. A node-only footprint rejects
  // the straight half of a valid join before it can become rendered paint.
  var surfaces=new List<RoadRibbon>(EdgeRoadRibbons(node));surfaces.AddRange(EdgeRoadRibbons(road));
PaintJoin(node,record,style,join,ribbons:surfaces,inspect:inspect);
 }
}
}}
