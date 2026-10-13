using System;using System.Collections.Generic;using Colossal.Mathematics;using Game.Net;using Game.Prefabs;using Unity.Entities;using Unity.Mathematics;
namespace RoadStyleIntoKorea { public partial class StyleSystem {
bool BayEdgeCurves(Entity owner,bool left,float half,List<Bezier4x3> output){
if(!EntityManager.HasComponent<Composition>(owner)||!EntityManager.HasComponent<EdgeGeometry>(owner))return false;
var cp=EntityManager.GetComponentData<Composition>(owner).m_Edge;
if(!EntityManager.HasBuffer<NetCompositionPiece>(cp)||!EntityManager.HasComponent<NetCompositionData>(cp))return false;
var width=EntityManager.GetComponentData<NetCompositionData>(cp).m_Width;var shape=EntityManager.GetComponentData<EdgeGeometry>(owner);var center=new Geometry.Arc(CurveOf(owner));bool found=false;
foreach(var piece in EntityManager.GetBuffer<NetCompositionPiece>(cp,true)){
if(PieceName(piece.m_Piece).IndexOf("Bay",StringComparison.OrdinalIgnoreCase)<0||(piece.m_PieceFlags&NetPieceFlags.HasRoadLanes)==0)continue;
var mid=Between(shape.m_Start.m_Left,shape.m_Start.m_Right,.5f+piece.m_Offset.x/width,0).d;var t=center.Nearest(mid);
if((math.dot(mid-Geometry.Position(center.Curve,t),Geometry.Left(Geometry.Tangent(center.Curve,t)))>=0)!=left)continue;
var template=NativePavingTemplate(piece.m_Piece);if(template==null||template.Rims.Count==0)continue;
found=true;var sign=new float2((piece.m_SectionFlags&NetSectionFlags.Invert)!=0?1:-1,(piece.m_SectionFlags&NetSectionFlags.FlipMesh)!=0?1:-1);var offset=piece.m_Offset;
if((piece.m_SectionFlags&NetSectionFlags.HalfLength)!=0){sign.y*=2;offset.z+=piece.m_Size.z*.5f;}
var curves=new List<Bezier4x3>();
foreach(var rim in template.Rims){
var a=template.Vertices[rim.x];var b=template.Vertices[rim.y];
// The oblique source-mesh curb is the widening/narrowing run. Leave it unpainted.
if(math.abs(a.x-b.x)>.3f)continue;
var p=PavingMeshGeometry.Deform(a,offset,sign,piece.m_Size.z,width,shape.m_Start.m_Left,shape.m_Start.m_Right,shape.m_End.m_Left,shape.m_End.m_Right,false);
var q=PavingMeshGeometry.Deform(b,offset,sign,piece.m_Size.z,width,shape.m_Start.m_Left,shape.m_Start.m_Right,shape.m_End.m_Left,shape.m_End.m_Right,false);
if(center.Nearest(p)>center.Nearest(q)){var swap=p;p=q;q=swap;}
p.y-=.2f;q.y-=.2f;
curves.Add(BayEdgePolicy.Inside(Geometry.Line(p,q),left,half));
}
curves.Sort((a,b)=>center.Nearest(a.a).CompareTo(center.Nearest(b.a)));
foreach(var c in BayEdgePolicy.Merge(curves))output.Add(c);
}
return found;
}}}
