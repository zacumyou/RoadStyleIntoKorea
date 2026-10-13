using System.Collections.Generic;using Colossal.Mathematics;using Game.Net;using Unity.Entities;using Unity.Mathematics;
namespace RoadStyleIntoKorea {public partial class StyleSystem {
int CenterSeamChoice(Entity road,float3 point){
if(!EntityManager.HasComponent<CenterChoice>(road))return 0;var choice=EntityManager.GetComponentData<CenterChoice>(road);if(!CenterDivided(road))return HasOpposingTraffic(road)?choice.Left:0;
float best=float.MaxValue;bool left=false;foreach(var path in CenterPaths(road,Lanes(road),EdgeStyle.YellowDouble)){
var arc=new Geometry.Arc(path.Curve);float d=math.distancesq(point,Geometry.Position(path.Curve,arc.Nearest(point)));if(d<best){best=d;left=path.Left;}}
return left?choice.Left:choice.Right;
}
float CenterSeamDistance(Entity road,float3 point){float best=float.MaxValue;foreach(var path in CenterPaths(road,Lanes(road),EdgeStyle.YellowDouble)){best=math.min(best,math.min(math.distancesq(point,path.Curve.a),math.distancesq(point,path.Curve.d)));}return best;}
void JoinCenterSeam(Entity node,Record record,List<Entity> roads,bool inspect){
var native=new List<Entity>();foreach(var lane in Lanes(node))if(EntityManager.HasComponent<Game.Net.SecondaryLane>(lane)&&!EntityManager.HasComponent<CarLane>(lane)&&UTurnPolicy.CenterLine(assets.Name(Prefab(lane))))native.Add(lane);
if(native.Count==0){JoinMissingCenterSeam(node,record,roads,inspect);return;}
var endpoints=new[]{CenterEndpoints(roads[0],node),CenterEndpoints(roads[1],node)};
var painted=new List<Bezier4x3>();
foreach(var lane in native){var source=CurveOf(lane);var curve=source;bool duplicate=false;foreach(var done in painted)if(Geometry.SameCurve(curve,done)||Geometry.SameCurve(Geometry.Reverse(curve),done)){duplicate=true;break;}
if(duplicate){if(!inspect)Hide(lane,record);continue;}
int first=CenterSeamDistance(roads[0],curve.a)<=CenterSeamDistance(roads[1],curve.a)?0:1;
int start=CenterSeamChoice(roads[first],curve.a),end=CenterSeamChoice(roads[1-first],curve.d);if(start==0&&end==0)continue;
int a=MarkingPolicy.InheritCenter(start,end),b=MarkingPolicy.InheritCenter(end,start);
var styles=new[]{(EdgeStyle)(a-1),(EdgeStyle)(b-1)};bool valid=true;foreach(var style in styles)if(style!=EdgeStyle.None){var asset=assets.Get(CenterLinePolicy.Asset((int)style));if(asset==Entity.Null||!EntityManager.HasComponent<Game.Prefabs.NetLaneGeometryData>(asset))valid=false;}if(!valid)continue;
{
 var from=ClosestCenterEndpoint(endpoints[first],curve.a);var to=ClosestCenterEndpoint(endpoints[1-first],curve.d);
 if(from==null||to==null)continue;
 if(math.distance(from.Point,to.Point)<.05f){if(!inspect)Hide(lane,record);continue;}
 if(!EdgeJoinPolicy.TryCurve(from.Point,from.Into,to.Point,-to.Into,out var adjusted))continue;
 curve=adjusted;
}
var arc=new Geometry.Arc(curve);for(int side=0;side<2;side++){
var style=styles[side];if(style==EdgeStyle.None)continue;var part=arc.Slice(side*arc.Length*.5f,(side+1)*arc.Length*.5f);
foreach(var safe in BoundaryPaint(node,part,S.PaintClearance)){if(!inspect)PaintLane(node,record,assets.Get(CenterLinePolicy.Asset((int)style)),safe);}
}
painted.Add(source);if(!inspect)Hide(lane,record);
}
}
sealed class CenterEndpoint {internal float3 Point,Into;internal int Code;}
static CenterEndpoint ClosestCenterEndpoint(List<CenterEndpoint> endpoints,float3 point){CenterEndpoint best=null;float distance=float.MaxValue;foreach(var e in endpoints){float d=math.distancesq(point,e.Point);if(d<distance){best=e;distance=d;}}return best;}
List<CenterEndpoint> CenterEndpoints(Entity road,Entity node){
 var result=new List<CenterEndpoint>();var position=EntityManager.GetComponentData<Node>(node).m_Position;
 var lanes=Lanes(road);var choice=EntityManager.HasComponent<CenterChoice>(road)?EntityManager.GetComponentData<CenterChoice>(road):default;
 bool divided=CenterDivided(road);
 foreach(bool left in new[]{true,false}){
  if(!divided&&!left)continue;int code=left?choice.Left:choice.Right;
  var style=code>1?(EdgeStyle)(code-1):EdgeStyle.YellowSolid;
  CenterEndpoint best=null;float distance=float.MaxValue;
  foreach(var path in CenterPaths(road,lanes,style)){
   if(divided&&path.Left!=left)continue;
   foreach(var curve in CenterVisible(road,path,style,lanes)){
    bool end=math.distancesq(curve.d,position)<math.distancesq(curve.a,position);var point=end?curve.d:curve.a;
    float d=math.distancesq(point,position);if(d>=distance)continue;distance=d;
    best=new CenterEndpoint{Point=point,Into=Geometry.Tangent(curve,end?1:0)*(end?1:-1),Code=code};
   }
  }
  if(best!=null&&!result.Exists(e=>math.distance(e.Point,best.Point)<.02f))result.Add(best);
 }
 return result;
}
void JoinMissingCenterSeam(Entity node,Record record,List<Entity> roads,bool inspect){
 if(!EntityManager.HasComponent<CenterChoice>(roads[0])&&!EntityManager.HasComponent<CenterChoice>(roads[1]))return;
 var first=CenterEndpoints(roads[0],node);var second=CenterEndpoints(roads[1],node);
 // Both branches of a terminating median must reach the single undivided center.
 if(first.Count<second.Count){var swap=first;first=second;second=swap;}
 var used=new HashSet<CenterEndpoint>();
 foreach(var a in first){
  CenterEndpoint best=null;Bezier4x3 seam=default;float distance=float.MaxValue;
  foreach(var b in second){
   if(second.Count>1&&used.Contains(b))continue;
   // Two-road corners may turn sharply; the fitted curve still passes road/sidewalk masks.
   if(math.distancesq(a.Point,b.Point)>=distance||!EdgeJoinPolicy.TryCurve(a.Point,a.Into,b.Point,-b.Into,out var candidate))continue;
   best=b;seam=candidate;distance=math.distancesq(a.Point,b.Point);
  }
  if(best==null)continue;used.Add(best);int x=a.Code,y=best.Code;if(x==0&&y==0)continue;
  int ca=MarkingPolicy.InheritCenter(x,y),cb=MarkingPolicy.InheritCenter(y,x);
  var arc=new Geometry.Arc(seam);int parts=ca==cb?1:2;
  for(int side=0;side<parts;side++){
   var style=(EdgeStyle)((side==0?ca:cb)-1);if(style==EdgeStyle.None||style==EdgeStyle.Original)continue;
   var prefab=assets.Get(CenterLinePolicy.Asset((int)style));if(prefab==Entity.Null)continue;
   var section=parts==1?seam:arc.Slice(side*arc.Length/2,(side+1)*arc.Length/2);
   foreach(var safe in RoadOnlyEdge(node,style,section,paintHalf:CenterLinePolicy.HalfWidth((int)style),unionSurface:true))foreach(var clear in BoundaryPaint(node,safe,S.PaintClearance)){
if(!inspect)PaintLane(node,record,prefab,clear);
   }
  }
 }
}
}}
