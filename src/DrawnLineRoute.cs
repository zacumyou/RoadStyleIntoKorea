using System.Collections.Generic;
using Colossal.Mathematics;
using Game.Net;
using Unity.Entities;
using Unity.Mathematics;
namespace RoadStyleIntoKorea {public partial class StyleSystem {
 struct LinePiece {internal Entity Road;internal Bezier4x3 Curve;internal float Width;}
 readonly List<LinePiece> lineRoute=new List<LinePiece>();
 float3 routeStart,routeEnd;int routeRevision=-1;
 Entity PointRoad(LinePoint point){
  if(IsRoad(point.PointOwner))return point.PointOwner;
  Entity best=Entity.Null;float distance=float.MaxValue;
  foreach(var owner in lineOwners)if(IsRoad(owner)){
   var arc=new Geometry.Arc(CurveOf(owner));float t=arc.Nearest(point.Position);var p=Geometry.Position(arc.Curve,t);
   float d=math.distancesq(p,point.Position);if(math.abs(p.y-point.Position.y)>2||math.abs(math.dot(Geometry.Tangent(arc.Curve,t),point.Tangent))<.7f)continue;
   if(d<distance){distance=d;best=owner;}
  }return best;
 }
 bool BuildLineRoute(LinePoint first,LinePoint last){
  if(routeRevision==toolRevision&&math.distancesq(routeStart,first.Position)<.0001f&&math.distancesq(routeEnd,last.Position)<.0001f)return lineRoute.Count>0;
  routeRevision=toolRevision;routeStart=first.Position;routeEnd=last.Position;lineRoute.Clear();
  var start=PointRoad(first);var end=PointRoad(last);if(start==Entity.Null||end==Entity.Null||(!ColorGuideMode&&Prefab(start)!=Prefab(end)))return false;
  var previous=new Dictionary<Entity,Entity>{{start,Entity.Null}};var queue=new WorkQueue<Entity>();queue.Enqueue(start);
  while(queue.Count>0&&previous.Count<=24&&!previous.ContainsKey(end)){
   var road=queue.Dequeue();var edge=EntityManager.GetComponentData<Edge>(road);
   foreach(var node in new[]{edge.m_Start,edge.m_End}){
    if(!SafeNode(node))continue;int count=0;Entity next=Entity.Null;
    foreach(var link in EntityManager.GetBuffer<ConnectedEdge>(node,true))if(IsRoad(link.m_Edge)){count++;if(link.m_Edge!=road)next=link.m_Edge;}
    if(count!=2||next==Entity.Null||(!ColorGuideMode&&Prefab(next)!=Prefab(start))||previous.ContainsKey(next))continue;
    previous[next]=road;queue.Enqueue(next);
   }
  }
  if(!previous.ContainsKey(end))return false;
  var roads=new List<Entity>();for(var road=end;road!=Entity.Null;road=previous[road])roads.Add(road);roads.Reverse();
  var curves=new List<Bezier4x3>();var usedRoads=new List<Entity>();
  for(int i=0;i<roads.Count;i++){
   var c=CurveOf(roads[i]);var arc=new Geometry.Arc(c);float a=i==0?arc.Nearest(first.Position):0,b=i==roads.Count-1?arc.Nearest(last.Position):1;
   if(roads.Count>1){var edge=EntityManager.GetComponentData<Edge>(roads[i]);bool reverse;
    var other=EntityManager.GetComponentData<Edge>(roads[i==0?1:i-1]);
    if(i==0)reverse=edge.m_Start==other.m_Start||edge.m_Start==other.m_End;
    else reverse=edge.m_End==other.m_Start||edge.m_End==other.m_End;
    if(i>0)a=reverse?1:0;if(i<roads.Count-1)b=reverse?0:1;
   }
   if(math.abs(a-b)<.0001f)continue;usedRoads.Add(roads[i]);
   curves.Add(a<b?Geometry.Cut(c,a,b):Geometry.Reverse(Geometry.Cut(c,b,a)));
  }
  if(curves.Count==0)return false;
  float from=math.dot(first.Position-curves[0].a,Geometry.Left(Geometry.Tangent(curves[0],0)));
  float to=math.dot(last.Position-curves[curves.Count-1].d,Geometry.Left(Geometry.Tangent(curves[curves.Count-1],1)));
  float total=0;var lengths=new List<float>();foreach(var c in curves){float length=new Geometry.Arc(c).Length;lengths.Add(length);total+=length;}
  float at=0;
  for(int i=0;i<curves.Count;i++){
   var c=curves[i];var left=Geometry.Offset(c,math.lerp(from,to,at/total));at+=lengths[i];var right=Geometry.Offset(c,math.lerp(from,to,at/total));
   lineRoute.Add(new LinePiece{Road=usedRoads[i],Curve=new Bezier4x3(left.a,math.lerp(left.b,right.b,1f/3),math.lerp(left.c,right.c,2f/3),right.d)});
  }
  for(int i=1;i<lineRoute.Count;i++){
   var before=lineRoute[i-1];var after=lineRoute[i];var joint=(before.Curve.d+after.Curve.a)*.5f;var anchor=joint;float best=4;
   if(!ColorGuideMode)foreach(var point in linePoints){float d=math.distancesq(point.Position,anchor);if(d<best){best=d;joint=point.Position;}}
   before.Curve.c+=joint-before.Curve.d;before.Curve.d=joint;after.Curve.b+=joint-after.Curve.a;after.Curve.a=joint;lineRoute[i-1]=before;lineRoute[i]=after;
  }
  var head=lineRoute[0];head.Curve.b+=first.Position-head.Curve.a;head.Curve.a=first.Position;lineRoute[0]=head;
  var tail=lineRoute[lineRoute.Count-1];tail.Curve.c+=last.Position-tail.Curve.d;tail.Curve.d=last.Position;lineRoute[lineRoute.Count-1]=tail;
  if(!ColorGuideMode){
   head=lineRoute[0];head.Curve=DrawnLinePolicy.AlignStart(head.Curve,first.Tangent);lineRoute[0]=head;
   tail=lineRoute[lineRoute.Count-1];tail.Curve=Geometry.Reverse(DrawnLinePolicy.AlignStart(Geometry.Reverse(tail.Curve),last.Tangent));lineRoute[lineRoute.Count-1]=tail;
  }
  return true;
 }
 void QueueLineRoute(int style){
  if(lineMutationPending)return;var pieces=lineRoute.ToArray();lineMutationPending=true;
  localStyleCommands.Enqueue(()=>{try{
   if(assets.Get(DrawnLinePolicy.Assets[style])==Entity.Null)return;
   foreach(var piece in pieces)if(!LineOwner(piece.Road)||EntityManager.HasBuffer<DrawnLineChoice>(piece.Road)&&EntityManager.GetBuffer<DrawnLineChoice>(piece.Road,true).Length>=128)return;
   var group=System.BitConverter.ToInt64(System.Guid.NewGuid().ToByteArray(),0);
   foreach(var piece in pieces){
    var local=LineTransform(piece.Road,piece.Curve,true);var choices=EntityManager.HasBuffer<DrawnLineChoice>(piece.Road)?EntityManager.GetBuffer<DrawnLineChoice>(piece.Road):EntityManager.AddBuffer<DrawnLineChoice>(piece.Road);
    bool duplicate=false;foreach(var old in choices)if(old.Style==style&&(Geometry.SameCurve(old.Local,local)||Geometry.SameCurve(old.Local,Geometry.Reverse(local))))duplicate=true;
    if(!duplicate)choices.Add(new DrawnLineChoice{Style=style,Group=group,Local=local});manualDirty.Add(piece.Road);
   }InvalidateToolPreview();lineRevision=routeRevision=-1;targetedActive=true;
  }finally{lineMutationPending=false;}});
 }
}}
