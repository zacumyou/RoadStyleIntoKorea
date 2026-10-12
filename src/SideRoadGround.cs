using System;
using System.Collections.Generic;
using Game.Net;
using Game.Prefabs;
using Game.Simulation;
using Unity.Entities;
using Unity.Mathematics;
namespace RoadStyleIntoKorea {
 public partial class StyleSystem {
  // Sample the continuous, uncut pavement top, not the clipped base mesh.
  // Raised RoadBuilder shoulders must use the same lift/ramps as PaintNativePaving.
  SideRoadHeightGeometry SideRoadPavingTop(Entity road,EdgeGeometry shape){
   var triangles=new List<float3>();
   var ramps=CurbRamps(road);
   foreach(var region in PavingRegions(road)){
    if(!AutomaticPavingRegion(road,region))continue;
    var p=region.Piece;var template=NativePavingTemplate(p.m_Piece);
    if(template==null||template.Indices.Length==0)continue;
    var sign=new float2((p.m_SectionFlags&NetSectionFlags.Invert)!=0?1:-1,(p.m_SectionFlags&NetSectionFlags.FlipMesh)!=0?1:-1);
    var offset=p.m_Offset;bool raised=region.Kind==2&&CurbSelected(road,region);
    if(raised)offset.y+=NativeCurbGeometry.Rise;
    if((p.m_SectionFlags&NetSectionFlags.HalfLength)!=0){sign.y*=2;offset.z+=p.m_Size.z*.5f;}
    // Keep the source's whole top, including the boundary that clipping removes.
    PavingMeshGeometry.Build(template.Vertices,template.Indices,offset,sign,p.m_Size.z,region.RoadWidth,
     region.Min,region.Max,shape.m_Start.m_Left,shape.m_Start.m_Right,shape.m_End.m_Left,shape.m_End.m_Right,
     out var vertices,out var indices,out var uv);
    if(raised)ramps.Paving(ref vertices,ref indices,ref uv);
    foreach(int index in indices)triangles.Add(vertices[index]);
   }
   return new SideRoadHeightGeometry(triangles);
  }
  // Managed snapshots only: never retain TerrainSystem native arrays between frames.
  void PrepareSideRoadHeights(Entity road,List<SideRoadOpening> openings){{
   if(openings.Count==0)return;
   var cp=EntityManager.GetComponentData<Composition>(road).m_Edge;
   bool clipped=nativeSideRoadCuts.TryGetValue(road,out var cut)&&cp==cut.Composition;
   var parts=clipped?cut.Mesh:cp;
   var shape=EntityManager.GetComponentData<EdgeGeometry>(road);
   float width=EntityManager.GetComponentData<NetCompositionData>(cp).m_Width;
   var triangles=new List<float3>();var stones=new List<float3>();var expanded=new List<float3>();
   foreach(var part in EntityManager.GetBuffer<NetCompositionPiece>(parts,true)){
    string name=PieceName(part.m_Piece);
    if(!SideRoadUV.AsphaltSource(name)&&PavingPolicy.Kind(name)<=0)continue;
    if((part.m_SectionFlags&(NetSectionFlags.Hidden|NetSectionFlags.HiddenSurface|NetSectionFlags.Underground|NetSectionFlags.Overhead))!=0)continue;
    try{
     bool raw=clipped&&EntityManager.HasBuffer<MeshVertex>(part.m_Piece);
     var source=raw?new CutSource{
      P=EntityManager.GetBuffer<MeshVertex>(part.m_Piece,true).Reinterpret<float3>().AsNativeArray().ToArray(),
      UV=EntityManager.GetBuffer<MeshUV0>(part.m_Piece,true).Reinterpret<float2>().AsNativeArray().ToArray(),
      N=EntityManager.GetBuffer<MeshNormal>(part.m_Piece,true).Reinterpret<float3>().AsNativeArray().ToArray(),
      I=EntityManager.GetBuffer<MeshIndex>(part.m_Piece,true).Reinterpret<int>().AsNativeArray().ToArray()
     }:ReadCutSource(part.m_Piece);
     var sign=new float2((part.m_SectionFlags&NetSectionFlags.Invert)!=0?1:-1,(part.m_SectionFlags&NetSectionFlags.FlipMesh)!=0?1:-1);var offset=part.m_Offset;
     if((part.m_SectionFlags&NetSectionFlags.HalfLength)!=0){sign.y*=2;offset.z+=part.m_Size.z*.5f;}
     var world=new float3[source.P.Length];
     for(int i=0;i<world.Length;i++)world[i]=PavingMeshGeometry.Deform(source.P[i],offset,sign,part.m_Size.z,width,shape.m_Start.m_Left,shape.m_Start.m_Right,shape.m_End.m_Left,shape.m_End.m_Right,false);
     for(int i=0;i+2<source.I.Length;i+=3){
      int a=source.I[i],b=source.I[i+1],c=source.I[i+2];
      if(source.N[a].y<.5f||source.N[b].y<.5f||source.N[c].y<.5f)continue;
      triangles.Add(world[a]);triangles.Add(world[b]);triangles.Add(world[c]);
      if(PavingPolicy.Bay(name)){expanded.Add(world[a]);expanded.Add(world[b]);expanded.Add(world[c]);}
      float lo=math.min(source.UV[a].x,math.min(source.UV[b].x,source.UV[c].x));
      float hi=math.max(source.UV[a].x,math.max(source.UV[b].x,source.UV[c].x));
      if(PavingPolicy.Kind(name)>0&&lo>=.28f&&hi<=.30f){stones.Add(world[a]);stones.Add(world[b]);stones.Add(world[c]);}
     }
    }catch(Exception ){}
   }
   var terrain=World.GetExistingSystemManaged<TerrainSystem>();
   var height=terrain!=null?terrain.GetHeightData(false):default;
   var stoneSampler=new SideRoadHeightGeometry(stones);
   var pavingTop=SideRoadPavingTop(road,shape);
   foreach(var opening in openings){
    var min=new float2(float.MaxValue);var max=new float2(float.MinValue);
    for(int i=0;i<=16;i++)foreach(int end in new[]{-1,1}){
     float depth=(opening.Outer-opening.Inner)*i/16f;
     var p=opening.Point(end*opening.HalfSpan(depth),depth);
     min=math.min(min,p.xz);max=math.max(max,p.xz);
    }
    min-=.3f;max+=.3f;var nearby=new List<float3>();
    for(int i=0;i+2<triangles.Count;i+=3){
     var a=triangles[i];var b=triangles[i+1];var c=triangles[i+2];
     if(math.any(math.max(a.xz,math.max(b.xz,c.xz))<min)||math.any(math.min(a.xz,math.min(b.xz,c.xz))>max))continue;
     nearby.Add(a);nearby.Add(b);nearby.Add(c);
    }
    opening.NativeHeight=new SideRoadHeightGeometry(nearby).Sample;
    opening.PavingTopHeight=pavingTop.Sample;
    opening.HideMarks=false;
    for(int i=0;i+2<expanded.Count;i+=3){
     var a=expanded[i].xz;var b=expanded[i+1].xz;var c=expanded[i+2].xz;
     if(math.any(math.max(a,math.max(b,c))<min)||math.any(math.min(a,math.min(b,c))>max))continue;
     opening.HideMarks=true;break;
    }
    opening.NativeCurbDepths=new float2[2];
    foreach(int end in new[]{-1,1}){
     opening.NativeCurbDepths[end<0?0:1]=SideRoadHeightGeometry.CurbDepths(opening,end,stoneSampler.Sample);
    }
    opening.GroundSpan=opening.Width*.5f+opening.Radius;
    opening.HeightsPrepared=true;
    if(terrain==null)continue;
    var samples=new float[33];bool valid=true;
    for(int i=0;i<samples.Length;i++){
     var p=opening.Point(math.lerp(-opening.GroundSpan,opening.GroundSpan,i/32f),opening.Outer-opening.Inner);
     samples[i]=TerrainUtils.SampleHeight(ref height,p);valid&=math.isfinite(samples[i]);
    }
    if(valid)opening.GroundHeights=samples;
   }
  }}
 }
}
