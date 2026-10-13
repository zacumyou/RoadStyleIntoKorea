using System.Collections.Generic;
using Colossal.Mathematics;
using Game.Net;
using Unity.Entities;
using Unity.Mathematics;
using UnityEngine;
namespace RoadStyleIntoKorea {public partial class StyleSystem {
 sealed class PenLane {internal Entity Owner,Lane;internal Game.Net.Lane Link;internal bool HasLink;internal Bezier4x3 Curve;internal Geometry.Arc Arc;internal float Width;}
 readonly List<PenLane> penLanes=new List<PenLane>();
 readonly List<LinePoint> penDots=new List<LinePoint>();
 void CachePenDot(Entity owner,float3 p,float3 tangent){foreach(var old in penDots)if(math.distancesq(old.Position,p)<.04f)return;penDots.Add(new LinePoint{Position=p,Tangent=tangent,PointOwner=owner});}
 bool SnapGuideEndpoint(float3 hit,out LinePoint point,out Entity owner){
  point=default;owner=Entity.Null;float best=2.25f;
  foreach(var entry in lineHits)for(int end=0;end<2;end++){
   var p=end==0?entry.Curve.a:entry.Curve.d;float distance=math.distancesq(p,hit);if(distance>=best)continue;
   bool internalJoint=false;if(entry.Group!=0)foreach(var other in lineHits){if(other==entry||other.Group!=entry.Group)continue;if(math.distancesq(other.Curve.a,p)<.04f||math.distancesq(other.Curve.d,p)<.04f){internalJoint=true;break;}}
   if(internalJoint)continue;best=distance;owner=entry.Owner;point=new LinePoint{Position=p,Tangent=Geometry.Tangent(entry.Curve,end),PointOwner=entry.Owner};
  }
  // Keep the native lane identity when an existing endpoint lies on that lane.
  // Without it the pen unnecessarily falls back to an endpoint-tangent spline.
  if(owner!=Entity.Null){float nearest=.01f;foreach(var lane in penLanes){if(lane.Owner!=owner)continue;float t=lane.Arc.Nearest(point.Position);var p=Geometry.Position(lane.Curve,t);float distance=math.distancesq(p,point.Position);if(distance>=nearest)continue;nearest=distance;point.Lane=lane.Lane;point.T=t;point.Tangent=Geometry.Tangent(lane.Curve,t);}}
  return owner!=Entity.Null;
 }
 bool PenJunction(Entity owner){if(!LineNode(owner))return false;int count=0;foreach(var e in EntityManager.GetBuffer<ConnectedEdge>(owner,true))if(LineRoad(e.m_Edge))count++;return count>=3;}
 void CachePenLane(Entity owner,Entity lane){var c=CurveOf(lane);bool linked=EntityManager.HasComponent<Lane>(lane);penLanes.Add(new PenLane{Owner=owner,Lane=lane,HasLink=linked,Link=linked?EntityManager.GetComponentData<Lane>(lane):default,Curve=c,Arc=new Geometry.Arc(c),Width=Width(lane)});}
 bool PenSnap(float3 hit,out LinePoint point,out Entity owner,bool strictCaps=false,Entity preferred=default){
  point=default;owner=Entity.Null;float best=float.MaxValue;bool preferredFound=false;
  foreach(var lane in penLanes){if(!(ColorGuideMode&&!redPavingMode?(GuideRoad(lane.Owner)||GuideNode(lane.Owner)):IsRoad(lane.Owner))||!lane.Arc.MayBeNear(hit,math.max(1,lane.Width*.5f),1))continue;
   float t=lane.Arc.Nearest(hit);var p=Geometry.Position(lane.Curve,t);float d=math.distancesq(p.xz,hit.xz);
   if(strictCaps&&!RedPavingPolicy.WithinCaps(hit,p,Geometry.Tangent(lane.Curve,t),t))continue;
   if(d>math.pow(math.max(1,lane.Width*.5f),2)||math.abs(p.y-hit.y)>1||!ColorGuideNetworkPolicy.Prefer(lane.Owner==preferred,preferredFound,d,best))continue;
   preferredFound=lane.Owner==preferred;best=d;owner=lane.Owner;point=new LinePoint{Position=p,Tangent=Geometry.Tangent(lane.Curve,t),Lane=lane.Lane,PointOwner=lane.Owner,T=t};
  }return owner!=Entity.Null;
 }
 bool PenRoute(LinePoint first,LinePoint last){
  lineRoute.Clear();int start=penLanes.FindIndex(l=>l.Lane==first.Lane),end=penLanes.FindIndex(l=>l.Lane==last.Lane);if(start<0||end<0)return false;
  if(start==end){if(math.abs(first.T-last.T)<.0001f)return false;lineRoute.Add(new LinePiece{Road=penLanes[start].Owner,Width=penLanes[start].Width,Curve=PenCut(penLanes[start].Curve,first.T,last.T)});return true;}
  // Follow geometrically joined native lane cubics, never offset the road centerline.
  var redPrefab=redPavingMode?Prefab(penLanes[start].Owner):Entity.Null;
  if(redPavingMode&&(!IsRoad(penLanes[end].Owner)||Prefab(penLanes[end].Owner)!=redPrefab))return false;
  var previous=new Dictionary<int,int>();var queue=new WorkQueue<int>();previous[start*2]=-1;previous[start*2+1]=-1;queue.Enqueue(start*2);queue.Enqueue(start*2+1);int found=-1;
  while(queue.Count>0&&previous.Count<512){int state=queue.Dequeue();var c=PenCurve(state);if(state/2==end){found=state;break;}
   for(int i=0;i<penLanes.Count;i++)for(int reverse=0;reverse<2;reverse++){int next=i*2+reverse;if(redPavingMode&&IsRoad(penLanes[i].Owner)&&Prefab(penLanes[i].Owner)!=redPrefab)continue;if(i==state/2||previous.ContainsKey(next)||!PenLanesJoin(state,next))continue;var n=PenCurve(next);if(math.distancesq(c.d,n.a)>.09f||math.dot(Geometry.Tangent(c,1),Geometry.Tangent(n,0))<.8f)continue;previous[next]=state;queue.Enqueue(next);}
  }
  if(found<0)return false;var route=new List<int>();for(int i=found;i>=0;i=previous[i])route.Add(i);route.Reverse();
  for(int i=0;i<route.Count;i++){int state=route[i];var c=PenCurve(state);float a=i==0?(state%2==0?first.T:1-first.T):0,b=i==route.Count-1?(state%2==0?last.T:1-last.T):1;if(b-a>.0001f)lineRoute.Add(new LinePiece{Road=penLanes[state/2].Owner,Width=penLanes[state/2].Width,Curve=Geometry.Cut(c,a,b)});}
  float length=0;var chord=last.Position-first.Position;foreach(var piece in lineRoute){length+=new Geometry.Arc(piece.Curve).Length;if(math.dot(piece.Curve.d-piece.Curve.a,chord)<-.1f){lineRoute.Clear();return false;}}if(length>math.length(chord)*1.6f+4){lineRoute.Clear();return false;}return lineRoute.Count>0;
 }
 Bezier4x3 PenCurve(int state)=>state%2==0?penLanes[state/2].Curve:Geometry.Reverse(penLanes[state/2].Curve);
 static Bezier4x3 PenCut(Bezier4x3 c,float a,float b)=>a<b?Geometry.Cut(c,a,b):Geometry.Reverse(Geometry.Cut(c,b,a));
 void ColorPenTool(Entity target,float3 hit,Game.Rendering.OverlayRenderSystem.Buffer buffer,bool press){
  bool snapped=PenSnap(hit,out var point,out var owner,false,lineStarted?lineStartOwner:target);float best=1.44f;
  bool restricted=RestrictedGuideOwner(target)||(lineStarted&&RestrictedGuideOwner(lineStartOwner));
  // Junction endpoint dots take precedence over the moving road marker.
  foreach(var p in penDots){DrawGuidePoint(buffer,p.Position,new Color(.1f,.8f,1,.85f));float d=math.distancesq(p.Position,hit);if(!restricted&&d<best){best=d;point=p;owner=p.PointOwner;snapped=true;}}
  if(!restricted&&SnapGuideEndpoint(hit,out var endpoint,out var endpointOwner)){point=endpoint;owner=endpointOwner;snapped=true;DrawGuidePoint(buffer,point.Position,Color.white);}
  if(!snapped)return;
  if(point.Lane!=Entity.Null){var tangent=math.normalizesafe(point.Tangent);buffer.DrawCurve(new Color(.01f,.18f,.25f,1),Geometry.Line(point.Position-tangent*1.1f,point.Position+tangent*1.1f),.16f);buffer.DrawCurve(new Color(.1f,.65f,.85f,1),Geometry.Line(point.Position-tangent*1.1f,point.Position+tangent*1.1f),.07f);}
  DrawGuidePoint(buffer,point.Position,new Color(.1f,.65f,.85f,1));
  bool route=false,connect=false;Bezier4x3 free=default;
  if(lineStarted){DrawGuidePoint(buffer,lineStart.Position,Color.white);route=PenRoute(lineStart,point);if(!restricted&&!route&&IsRoad(lineStart.PointOwner)&&IsRoad(point.PointOwner)&&lineStartOwner!=owner){routeRevision=-1;route=BuildLineRoute(lineStart,point);}connect=!restricted&&!route&&(lineStart.Lane==Entity.Null||point.Lane==Entity.Null||lineStartOwner==owner);if(route)foreach(var p in lineRoute)buffer.DrawCurve(new Color(.1f,.85f,1,.85f),p.Curve,.25f);else if(connect){free=DrawnLinePolicy.SmoothConnect(lineStart.Position,point.Position,lineStart.Tangent,point.Tangent);buffer.DrawCurve(new Color(.1f,.85f,1,.85f),free,.25f);}}
  if(!press)return;if(!lineStarted){lineStarted=true;lineStart=point;lineStartOwner=owner;return;}
  if(math.distancesq(lineStart.Position,point.Position)<.04f)return;
  if(route){QueueLineRoute(SelectedLineStyle);lineStarted=false;}else if(connect){QueueDrawnLine(lineStartOwner,-1,free,SelectedLineStyle,false,false);lineStarted=false;}
 }
}}
