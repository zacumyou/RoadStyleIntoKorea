using System;
using System.Collections.Generic;
using Colossal.IO.AssetDatabase;
using Colossal.Mathematics;
using Game.Common;
using Game.Net;
using Game.Prefabs;
using Game.Rendering;
using Game.Tools;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using UnityEngine.Rendering;

namespace RoadStyleIntoKorea {
 public partial class StyleSystem {
  sealed class NativeSideRoadCut {
   internal Entity Original,Composition,Mesh;
   internal readonly List<Entity> Owned=new List<Entity>();
   internal List<SideRoadCutGeometry.Cell> Cells;
  }
  readonly Dictionary<Entity,NativeSideRoadCut> nativeSideRoadCuts=new Dictionary<Entity,NativeSideRoadCut>();
  readonly Dictionary<Entity,PrefabData> savedCutPrefabData=new Dictionary<Entity,PrefabData>();
  sealed class CutSource {
   internal float3[] P,N;internal float4[] T;internal float2[] UV;internal int[] I;internal MeshMaterial[] Materials;
  }
  readonly Dictionary<Entity,CutSource> sideRoadCutSources=new Dictionary<Entity,CutSource>();
  CutSource ReadCutSource(Entity piece){
   if(sideRoadCutSources.TryGetValue(piece,out var cached))return cached;
   var materialList=new List<MeshMaterial>();foreach(var m in EntityManager.GetBuffer<MeshMaterial>(piece,true))materialList.Add(m);
   var source=new CutSource{Materials=materialList.ToArray()};
   var prefabs=World.GetExistingSystemManaged<PrefabSystem>();
   if(!prefabs.TryGetPrefab<PrefabBase>(piece,out var p)||!(p is RenderPrefab render)||render.geometryAsset==null)throw new InvalidOperationException("Native cut source has no geometry asset");
   var data=new GeometryAsset.Data();var loading=new GeometryAsset.Loading();
   try{
    var asset=render.geometryAsset;
    GeometryAsset.LoadSync(asset.database.GetAsyncReadDescriptor(asset.id),GeometryAsset.Attribute.Position|GeometryAsset.Attribute.Normal|GeometryAsset.Attribute.Tangent|GeometryAsset.Attribute.TexCoord0,ref data,ref loading);
    if(data.meshCount!=1)throw new InvalidOperationException("Native sidewalk cut requires one source mesh");
    int vc=GeometryAsset.GetVertexCount(ref data,0),ic=GeometryAsset.GetIndicesCount(ref data,0);
    using(var v=new NativeArray<MeshVertex>(vc,Allocator.Temp))using(var n=new NativeArray<MeshNormal>(vc,Allocator.Temp))
    using(var t=new NativeArray<MeshTangent>(vc,Allocator.Temp))using(var uv=new NativeArray<MeshUV0>(vc,Allocator.Temp))using(var ix=new NativeArray<MeshIndex>(ic,Allocator.Temp)){
     GeometryAsset.GetAttributeFormat(ref data,0,VertexAttribute.Position,out var vf,out int vd);MeshVertex.Unpack(GeometryAsset.GetAttributeData(ref data,0,VertexAttribute.Position),v,vc,vf,vd);
     GeometryAsset.GetAttributeFormat(ref data,0,VertexAttribute.Normal,out var nf,out int nd);MeshNormal.Unpack(GeometryAsset.GetAttributeData(ref data,0,VertexAttribute.Normal),n,vc,nf,nd);
     GeometryAsset.GetAttributeFormat(ref data,0,VertexAttribute.Tangent,out var tf,out int td);MeshTangent.Unpack(GeometryAsset.GetAttributeData(ref data,0,VertexAttribute.Tangent),t,vc,tf,td);
     GeometryAsset.GetAttributeFormat(ref data,0,VertexAttribute.TexCoord0,out var uf,out int ud);MeshUV0.Unpack(GeometryAsset.GetAttributeData(ref data,0,VertexAttribute.TexCoord0),uv,vc,uf,ud);
     MeshIndex.Unpack(GeometryAsset.GetIndices(ref data,0),ix,ic,GeometryAsset.GetIndexFormat(ref data,0),0);
     source.P=v.Reinterpret<float3>().ToArray();source.N=n.Reinterpret<float3>().ToArray();source.T=t.Reinterpret<float4>().ToArray();source.UV=uv.Reinterpret<float2>().ToArray();source.I=ix.Reinterpret<int>().ToArray();
     int count=data.meshInfos[0].subMeshCount;if(count!=source.Materials.Length)throw new InvalidOperationException("Native material/submesh count mismatch");
     for(int i=0;i<count;i++){var range=data.subMeshInfos[data.meshOffsets[0].subMeshesOffset+i].ToSubMeshDescriptor();if(range.baseVertex!=0)throw new InvalidOperationException("Native cut does not support baseVertex offsets");var m=source.Materials[i];m.m_StartIndex=range.indexStart;m.m_IndexCount=range.indexCount;source.Materials[i]=m;}
    }
   }finally{loading.Dispose();data.Dispose();}
   if(sideRoadCutSources.Count<32)sideRoadCutSources[piece]=source;return source;
  }
  Entity MakeCutPiece(NetCompositionPiece part,float width,EdgeGeometry shape,List<SideRoadCutGeometry.Cell> cells,NativeSideRoadCut cut){
   var source=ReadCutSource(part.m_Piece);
   var sign=new float2((part.m_SectionFlags&NetSectionFlags.Invert)!=0?1:-1,(part.m_SectionFlags&NetSectionFlags.FlipMesh)!=0?1:-1);var offset=part.m_Offset;
   if((part.m_SectionFlags&NetSectionFlags.HalfLength)!=0){sign.y*=2;offset.z+=part.m_Size.z*.5f;}
   Func<float3,float3> deform=p=>PavingMeshGeometry.Deform(p,offset,sign,part.m_Size.z,width,shape.m_Start.m_Left,shape.m_Start.m_Right,shape.m_End.m_Left,shape.m_End.m_Right,false);
   var vertices=new SideRoadCutGeometry.Vertex[source.P.Length];
   for(int i=0;i<vertices.Length;i++)vertices[i]=new SideRoadCutGeometry.Vertex{Local=source.P[i],World=deform(source.P[i]),Normal=source.N[i],Tangent=source.T[i],UV=source.UV[i]};
   var result=new List<SideRoadCutGeometry.Vertex>();var materials=new List<MeshMaterial>();
   foreach(var material in source.Materials){
    int start=result.Count;
    for(int i=material.m_StartIndex;i+2<material.m_StartIndex+material.m_IndexCount;i+=3)SideRoadCutGeometry.Emit(vertices[source.I[i]],vertices[source.I[i+1]],vertices[source.I[i+2]],cells,deform,result);
    var m=material;m.m_StartIndex=start;m.m_IndexCount=result.Count-start;m.m_StartVertex=start;m.m_VertexCount=result.Count-start;materials.Add(m);
   }
   var entity=EntityManager.CreateEntity();cut.Owned.Add(entity);
   // BatchMeshSystem accepts raw geometry, but ManagedBatchSystem still resolves
   // each material through the piece's RenderPrefab. Keep its source identity.
   EntityManager.AddComponentData(entity,EntityManager.GetComponentData<PrefabData>(part.m_Piece));
   var mesh=EntityManager.GetComponentData<MeshData>(part.m_Piece);mesh.m_IndexCount=result.Count;EntityManager.AddComponentData(entity,mesh);
   EntityManager.AddBuffer<MeshVertex>(entity);EntityManager.AddBuffer<MeshNormal>(entity);EntityManager.AddBuffer<MeshTangent>(entity);EntityManager.AddBuffer<MeshUV0>(entity);EntityManager.AddBuffer<MeshIndex>(entity);EntityManager.AddBuffer<MeshMaterial>(entity);
   var positions=EntityManager.GetBuffer<MeshVertex>(entity).Reinterpret<float3>();var normals=EntityManager.GetBuffer<MeshNormal>(entity).Reinterpret<float3>();var tangents=EntityManager.GetBuffer<MeshTangent>(entity).Reinterpret<float4>();var tex=EntityManager.GetBuffer<MeshUV0>(entity).Reinterpret<float2>();var indices=EntityManager.GetBuffer<MeshIndex>(entity).Reinterpret<int>();
   for(int i=0;i<result.Count;i++){var v=result[i];positions.Add(v.Local);normals.Add(math.normalizesafe(v.Normal,new float3(0,1,0)));tangents.Add(v.Tangent);tex.Add(v.UV);indices.Add(i);}
   var mb=EntityManager.GetBuffer<MeshMaterial>(entity);foreach(var m in materials)mb.Add(m);
   return entity;
  }
  void PrepareNativeSideRoadCut(Entity road){{
   if(!IsRoad(road)||!EntityManager.HasComponent<EdgeGeometry>(road))return;
   ReleaseNativeSideRoadCut(road);sideRoadWork.Remove(road);
   var openings=SideRoadOpenings(road);if(openings.Count==0)return;
   var cp=EntityManager.GetComponentData<Composition>(road);var cut=new NativeSideRoadCut{Original=cp.m_Edge,Cells=SideRoadCutGeometry.Cells(openings)};
   try{
    var data=EntityManager.GetComponentData<NetCompositionData>(cp.m_Edge);var shape=EntityManager.GetComponentData<EdgeGeometry>(road);
    var parts=new List<NetCompositionPiece>();foreach(var p in EntityManager.GetBuffer<NetCompositionPiece>(cp.m_Edge,true))parts.Add(p);
    var renderParts=new List<NetCompositionPiece>();int replaced=0;
    foreach(var original in parts){
     if((original.m_PieceFlags&NetPieceFlags.HasMesh)==0||(original.m_SectionFlags&NetSectionFlags.Hidden)!=0)continue;
     var part=original;string name=PieceName(part.m_Piece);
     if((part.m_SectionFlags&(NetSectionFlags.Underground|NetSectionFlags.Overhead))==0&&(PavingPolicy.Kind(name)>0||name.StartsWith("Sidewalk Side",StringComparison.Ordinal))){
      part.m_Piece=MakeCutPiece(part,data.m_Width,shape,cut.Cells,cut);replaced++;
     }
     renderParts.Add(part);
    }
    if(replaced==0)throw new InvalidOperationException("No supported native sidewalk pieces");
    var mesh=EntityManager.CreateEntity();cut.Mesh=mesh;cut.Owned.Add(mesh);
    var md=new NetCompositionMeshData{m_Flags=data.m_Flags,m_Width=data.m_Width,m_MiddleOffset=data.m_MiddleOffset,m_HeightRange=data.m_HeightRange,m_Hash=int.MinValue+mesh.Index};
    var unique=new HashSet<int>();var mats=new List<MeshMaterial>();
    foreach(var part in renderParts){
     var pd=EntityManager.GetComponentData<MeshData>(part.m_Piece);md.m_DefaultLayers|=pd.m_DefaultLayers;md.m_State|=pd.m_State;md.m_IndexFactor+=(float)pd.m_IndexCount/math.max(1,MathUtils.Size(pd.m_Bounds.z));md.m_LodBias+=pd.m_LodBias;md.m_ShadowBias+=pd.m_ShadowBias;
     foreach(var m in EntityManager.GetBuffer<MeshMaterial>(part.m_Piece,true))if(unique.Add(m.m_MaterialIndex))mats.Add(new MeshMaterial{m_MaterialIndex=m.m_MaterialIndex});
    }
    ValidateCutMaterials(renderParts);
    md.m_LodBias/=renderParts.Count;md.m_ShadowBias/=renderParts.Count;
    EntityManager.AddComponentData(mesh,md);EntityManager.AddBuffer<NetCompositionPiece>(mesh);EntityManager.AddBuffer<MeshMaterial>(mesh);EntityManager.AddBuffer<BatchGroup>(mesh);EntityManager.AddComponent<Created>(mesh);EntityManager.AddComponent<Updated>(mesh);
    var pieces=EntityManager.GetBuffer<NetCompositionPiece>(mesh);foreach(var p in renderParts)pieces.Add(p);var mm=EntityManager.GetBuffer<MeshMaterial>(mesh);foreach(var m in mats)mm.Add(m);
    var clone=EntityManager.Instantiate(cp.m_Edge);cut.Composition=clone;cut.Owned.Add(clone);
    EntityManager.RemoveComponent<Created>(clone);EntityManager.RemoveComponent<Updated>(clone);EntityManager.RemoveComponent<PrefabData>(clone);
    EntityManager.RemoveComponent<LoadedIndex>(clone);
    EntityManager.SetComponentData(clone,new NetCompositionMeshRef{m_Mesh=mesh,m_Rotate=false});
    cp.m_Edge=clone;EntityManager.SetComponentData(road,cp);DirtyBatch(road);nativeSideRoadCuts.Add(road,cut);
    foreach(var opening in openings)opening.CutSurface=true;
    PrepareSideRoadHeights(road,openings);
    EnqueueCurb(road);EnqueueEdgeOnly(road);
   }catch(Exception ){if(EntityManager.HasComponent<Composition>(road)){var restore=EntityManager.GetComponentData<Composition>(road);if(restore.m_Edge==cut.Composition){restore.m_Edge=cut.Original;EntityManager.SetComponentData(road,restore);DirtyBatch(road);}}nativeSideRoadCuts.Remove(road);foreach(var opening in openings)opening.CutSurface=false;RetireNativeSideRoadCut(cut);}
  }}
  void ValidateCutMaterials(List<NetCompositionPiece> parts){
   var prefabs=World.GetExistingSystemManaged<PrefabSystem>();
   foreach(var part in parts){
    if(!prefabs.TryGetPrefab<RenderPrefab>(part.m_Piece,out var prefab)||prefab==null)throw new InvalidOperationException("Native cut piece has no RenderPrefab material source: "+part.m_Piece);
    var materials=EntityManager.GetBuffer<MeshMaterial>(part.m_Piece,true);
    for(int i=0;i<materials.Length;i++)if(prefab.GetSurfaceAsset(i)==null)throw new InvalidOperationException("Native cut piece has no surface at material slot "+i);
   }
  }
  void RetireNativeSideRoadCut(NativeSideRoadCut cut){foreach(var e in cut.Owned)if(EntityManager.Exists(e)){EntityManager.RemoveComponent<PrefabData>(e);if(!EntityManager.HasComponent<Temp>(e))EntityManager.AddComponent<Temp>(e);if(!EntityManager.HasComponent<Deleted>(e))EntityManager.AddComponent<Deleted>(e);}}
  void ReleaseNativeSideRoadCut(Entity road){
   if(!nativeSideRoadCuts.TryGetValue(road,out var cut))return;
   if(EntityManager.HasComponent<Composition>(road)){var cp=EntityManager.GetComponentData<Composition>(road);if(cp.m_Edge==cut.Composition){cp.m_Edge=cut.Original;EntityManager.SetComponentData(road,cp);DirtyBatch(road);}}
   nativeSideRoadCuts.Remove(road);sideRoadWork.Remove(road);RetireNativeSideRoadCut(cut);
  }
  void SuspendNativeSideRoadCuts(){
   savedCutPrefabData.Clear();
   foreach(var pair in nativeSideRoadCuts){
    if(EntityManager.HasComponent<Composition>(pair.Key)){var cp=EntityManager.GetComponentData<Composition>(pair.Key);if(cp.m_Edge==pair.Value.Composition){cp.m_Edge=pair.Value.Original;EntityManager.SetComponentData(pair.Key,cp);}}
    foreach(var e in pair.Value.Owned)if(EntityManager.Exists(e)){
     // Temp alone does not exclude an enabled PrefabData from the game's
     // BeginPrefabSerializationSystem. These render aliases are not prefabs.
     if(EntityManager.HasComponent<PrefabData>(e)){savedCutPrefabData[e]=EntityManager.GetComponentData<PrefabData>(e);EntityManager.RemoveComponent<PrefabData>(e);}
     if(!EntityManager.HasComponent<Temp>(e)){EntityManager.AddComponent<Temp>(e);saveTemps.Add(e);}
    }
   }
  }
  void ResumeNativeSideRoadCuts(){
   foreach(var pair in savedCutPrefabData)if(EntityManager.Exists(pair.Key)&&!EntityManager.HasComponent<Deleted>(pair.Key))EntityManager.AddComponentData(pair.Key,pair.Value);
   savedCutPrefabData.Clear();
   foreach(var pair in nativeSideRoadCuts)if(EntityManager.HasComponent<Composition>(pair.Key)){
    var cp=EntityManager.GetComponentData<Composition>(pair.Key);if(cp.m_Edge==pair.Value.Original){cp.m_Edge=pair.Value.Composition;EntityManager.SetComponentData(pair.Key,cp);DirtyBatch(pair.Key);}
   }
  }
 }
}

