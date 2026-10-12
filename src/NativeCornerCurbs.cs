using System.Collections.Generic;
using Game.Net;
using Game.Prefabs;
using Unity.Entities;
using Unity.Mathematics;
using Colossal.Mathematics;
namespace RoadStyleIntoKorea {
 public partial class StyleSystem {
  void PaintNativeCornerCurbs(Entity node,Record record,Entity composition,Segment segment,
   float origin,float width,float min,float max,bool right,float4 target,float baseHeight){
   if(!EntityManager.HasBuffer<NetCompositionPiece>(composition))return;
   var data=EntityManager.GetComponentData<NetCompositionData>(composition);
   var sync=new NodeSurfaceSync{Enabled=true,Source=right?data.m_SyncVertexOffsetsRight:data.m_SyncVertexOffsetsLeft,Target=target};
   var lower=new List<float3>();var upper=new List<float3>();float center=origin+width*.5f;
   foreach(var p in EntityManager.GetBuffer<NetCompositionPiece>(composition,true)){
    if(PavingPolicy.Kind(PieceName(p.m_Piece))!=2||
     (p.m_SectionFlags&(NetSectionFlags.Median|NetSectionFlags.Hidden|NetSectionFlags.HiddenSurface|NetSectionFlags.Underground|NetSectionFlags.Overhead))!=0||
     (p.m_PieceFlags&(NetPieceFlags.Side|NetPieceFlags.Bottom|NetPieceFlags.PreserveShape))!=0||
     p.m_Offset.x+p.m_Size.x*.5f<=min||p.m_Offset.x-p.m_Size.x*.5f>=max)continue;
    var template=NativePavingTemplate(p.m_Piece);if(template==null)continue;
    var sign=new float2((p.m_SectionFlags&NetSectionFlags.Invert)!=0?1:-1,(p.m_SectionFlags&NetSectionFlags.FlipMesh)!=0?1:-1);
    var offset=p.m_Offset;offset.x-=center;
    if((p.m_SectionFlags&NetSectionFlags.HalfLength)!=0){sign.y*=2;offset.z+=p.m_Size.z*.5f;}
    PavingMeshGeometry.Build(template.Vertices,template.Indices,offset,sign,p.m_Size.z,width,min-center,max-center,
     segment.m_Left,segment.m_Right,segment.m_Left,segment.m_Right,out _,out _,out _,true,sync,lower,upper);
   }
   var a=Between(segment.m_Left,segment.m_Right,(min-origin)/width,baseHeight);
   var b=Between(segment.m_Left,segment.m_Right,(max-origin)/width,baseHeight);
   PaintCurbBoundary(node,record,right?lower:upper,CurbLaneGeometry.Curve(a,b,right,0),false);
   PaintCurbBoundary(node,record,right?upper:lower,CurbLaneGeometry.Curve(a,b,!right,0),true);
  }
  void PaintCurbBoundary(Entity owner,Record record,List<float3> pairs,Bezier4x3 reference,bool outerFace){
   var refArc=new Geometry.Arc(reference);var ramps=CurbRamps(owner);
   var chains=CurbBoundaryGeometry.Chains(pairs);
   if(chains.Count==0&&refArc.Length>.05f){
    // A RoadBuilder node can omit the clipped border at a strip boundary.
    // Use its actual node segment border, never a straight line across a junction.
    var path=new List<float3>();int steps=math.clamp((int)math.ceil(refArc.Length/.5f),1,128);
    for(int i=0;i<=steps;i++)path.Add(refArc.At(refArc.Length*i/steps));
    EmitCurbBoundary(record,path,refArc,outerFace,ramps);
   }
   foreach(var path in chains){
    float t=refArc.Nearest(path[path.Count/2]);
    if(math.dot(path[path.Count-1]-path[0],Geometry.Tangent(reference,t))<0)path.Reverse();
    // Keep the actual paving border, adding only the ramp breakpoints.
    EmitCurbBoundary(record,path,refArc,outerFace,ramps);
   }
  }
  void EmitCurbBoundary(Record record,List<float3> path,Geometry.Arc reference,bool outerFace,CurbRampPolicy ramps){
   if(path.Count<2)return;
   var start=Geometry.Tangent(reference.Curve,reference.Nearest(path[0]));
   var end=Geometry.Tangent(reference.Curve,reference.Nearest(path[path.Count-1]));
   NativeCurbGeometry.BuildPath(path,start,end,outerFace,out var vertices,out var indices,out var uv,ramps);
   CreatePavingMesh(record,CurbMaterial(),vertices,indices,uv,0,true);
  }
 }
}
