using System;
using System.Collections.Generic;
using Colossal.IO.AssetDatabase;
using Colossal.Mathematics;
using Game.Net;
using Game.Prefabs;
using Unity.Entities;
using Unity.Collections;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.Rendering;
namespace RoadStyleIntoKorea
{
    public partial class StyleSystem
    {
        sealed class PavingTemplate { internal float3[] Vertices;internal int[] Indices,AllIndices;internal List<int2> Rims; }
        readonly Dictionary<Entity,PavingTemplate> pavingTemplates=new Dictionary<Entity,PavingTemplate>();
        PavingTemplate NativePavingTemplate(Entity piece)
        {
            if(pavingTemplates.TryGetValue(piece,out var cached))return cached;
            var vertices=new List<float3>();var indices=new List<int>();var allIndices=new List<int>();
            var prefabs=World.GetExistingSystemManaged<PrefabSystem>();
            prefabs.TryGetPrefab<PrefabBase>(piece,out var prefab);var render=prefab as RenderPrefab;
            var skip=new List<bool>();if(render!=null)foreach(var surface in render.surfaceAssets)skip.Add(surface!=null&&PavingPolicy.PlantedSurface(surface.name));
            bool plants=skip.Contains(true);
            if(EntityManager.HasBuffer<MeshVertex>(piece)&&EntityManager.HasBuffer<MeshIndex>(piece)&&(!plants||EntityManager.HasBuffer<MeshMaterial>(piece)))
            {
                foreach(var v in EntityManager.GetBuffer<MeshVertex>(piece,true))vertices.Add(v.m_Vertex);
                var faces=EntityManager.GetBuffer<MeshIndex>(piece,true);foreach(var face in faces)allIndices.Add(face.m_Index);
                if(!plants)foreach(var i in faces)indices.Add(i.m_Index);
                else
                {
                    int material=0;
                    foreach(var range in EntityManager.GetBuffer<MeshMaterial>(piece,true))
                    {
                        bool omit=material<skip.Count&&skip[material++];if(omit)continue;
                        for(int i=range.m_StartIndex;i<range.m_StartIndex+range.m_IndexCount&&i<faces.Length;i++)indices.Add(faces[i].m_Index);
                    }
                }
            }
            else
            {
                // Independent CPU data, only on application and once per prefab.
                // Do not change the renderer's shared mesh/readability or reference count.
                if(render==null||render.geometryAsset==null)return null;
                var data=new GeometryAsset.Data();var loading=new GeometryAsset.Loading();
                try
                {
                    var asset=render.geometryAsset;
                    GeometryAsset.LoadSync(asset.database.GetAsyncReadDescriptor(asset.id),GeometryAsset.Attribute.Position,ref data,ref loading);
                    int material=0;
                    for(int mesh=0;mesh<data.meshCount;mesh++)
                    {
                        int vc=GeometryAsset.GetVertexCount(ref data,mesh),ic=GeometryAsset.GetIndicesCount(ref data,mesh);
                        using(var points=new NativeArray<MeshVertex>(vc,Allocator.Temp))
                        using(var faces=new NativeArray<MeshIndex>(ic,Allocator.Temp))
                        {
                            GeometryAsset.GetAttributeFormat(ref data,mesh,VertexAttribute.Position,out var format,out int dimension);
                            MeshVertex.Unpack(GeometryAsset.GetAttributeData(ref data,mesh,VertexAttribute.Position),points,vc,format,dimension);
                            MeshIndex.Unpack(GeometryAsset.GetIndices(ref data,mesh),faces,ic,GeometryAsset.GetIndexFormat(ref data,mesh),vertices.Count);
                            foreach(var p in points)vertices.Add(p.m_Vertex);foreach(var face in faces)allIndices.Add(face.m_Index);
                            for(int sub=0;sub<data.meshInfos[mesh].subMeshCount;sub++)
                            {
                                var range=data.subMeshInfos[data.meshOffsets[mesh].subMeshesOffset+sub].ToSubMeshDescriptor();
                                bool omit=material<skip.Count&&skip[material++];if(omit)continue;
                                for(int i=range.indexStart;i<range.indexStart+range.indexCount;i++)indices.Add(faces[i].m_Index);
                            }
                        }
                    }
                }
                catch(Exception ){return null;}
                finally{loading.Dispose();data.Dispose();}
            }
            if(vertices.Count==0||indices.Count==0)return null;
            if(pavingTemplates.Count>=256)pavingTemplates.Clear();
            var vp=vertices.ToArray();var pavement=NativeSurfacePolicy.Pavement(vp,allIndices.ToArray(),out var rims);
            var allowed=new HashSet<int>();foreach(var i in pavement)allowed.Add(i);
            var selected=new List<int>();for(int i=0;i+2<indices.Count;i+=3)if(allowed.Contains(indices[i])&&allowed.Contains(indices[i+1])&&allowed.Contains(indices[i+2])){selected.Add(indices[i]);selected.Add(indices[i+1]);selected.Add(indices[i+2]);}
            cached=new PavingTemplate{Vertices=vp,Indices=selected.ToArray(),AllIndices=allIndices.ToArray(),Rims=rims};pavingTemplates[piece]=cached;return cached;
        }
        bool PaintNativePaving(Entity edge,Record record,int style,PavingRegion region,EdgeGeometry shape)
        {{
            if(region.PaintMax-region.PaintMin<.05f)return true;
            var p=region.Piece;var template=NativePavingTemplate(p.m_Piece);if(template==null)return false;
            var sign=new float2((p.m_SectionFlags&NetSectionFlags.Invert)!=0?1:-1,(p.m_SectionFlags&NetSectionFlags.FlipMesh)!=0?1:-1);
            var offset=p.m_Offset;float clipMin=region.PaintMin,clipMax=region.PaintMax;
            if(region.Kind==1&&PavingPolicy.Bay(PieceName(p.m_Piece))&&template.Indices.Length>0){
                var bounds=NativeSurfacePolicy.PavementBounds(template.Vertices,template.Indices,sign.x,offset.x);
                clipMin=bounds.x;clipMax=bounds.y;
            }
            if(region.Kind==2&&CurbSelected(edge,region)){
                offset.y+=NativeCurbGeometry.Rise;
            }
            if(clipMax<=clipMin)return true;
            if((p.m_SectionFlags&NetSectionFlags.HalfLength)!=0){sign.y*=2;offset.z+=p.m_Size.z*.5f;}
            PavingMeshGeometry.Build(template.Vertices,template.Indices,offset,sign,p.m_Size.z,region.RoadWidth,clipMin,clipMax,
                shape.m_Start.m_Left,shape.m_Start.m_Right,shape.m_End.m_Left,shape.m_End.m_Right,out var vertices,out var indices,out var uv);
            if(indices.Count==0)return false;
            PavingUVPolicy.Subdivide(ref vertices,ref indices,ref uv);
            float mid=.5f+(clipMin+clipMax)*.5f/region.RoadWidth;
            PavingUVPolicy.Map(vertices,uv,Between(shape.m_Start.m_Left,shape.m_Start.m_Right,mid,0),
                Between(shape.m_End.m_Left,shape.m_End.m_Right,mid,0),clipMax-clipMin);
            bool ramped=region.Kind==2&&CurbSelected(edge,region);
            if(ramped)CurbRamps(edge).Paving(ref vertices,ref indices,ref uv);
            if(nativeSideRoadCuts.TryGetValue(edge,out var cut))SideRoadCutGeometry.ClipSurface(cut.Cells,ref vertices,ref indices,ref uv);
            CreatePavingMesh(record,PavingMaterial(style),vertices,indices,uv,ramped?0:.012f);
return true;
        }}
        bool PaintNativeCorner(Entity owner,Record record,int style,int kind,Entity composition,Segment segment,float origin,float width,
            float min,float max,bool right,bool start,Bezier4x3 orientedA,Bezier4x3 orientedB,float phase,bool curbAtA,float4 syncTarget,float raise=0,bool bayTransition=false)
        {{
            if(!EntityManager.HasBuffer<NetCompositionPiece>(composition))return false;
            float clipMin=kind==1&&right?min+.6f:min,clipMax=kind==1&&!right?max-.6f:max,center=origin+width*.5f;
            bool painted=false;
            var data=EntityManager.GetComponentData<NetCompositionData>(composition);
            var sync=new NodeSurfaceSync{Enabled=true,Source=right?data.m_SyncVertexOffsetsRight:data.m_SyncVertexOffsetsLeft,Target=syncTarget};
            foreach(var p in EntityManager.GetBuffer<NetCompositionPiece>(composition,true))
            {
                if(PavingPolicy.Kind(PieceName(p.m_Piece))!=kind||
                   (p.m_SectionFlags&(NetSectionFlags.Median|NetSectionFlags.Hidden|NetSectionFlags.HiddenSurface|NetSectionFlags.Underground|NetSectionFlags.Overhead))!=0||
                   (p.m_PieceFlags&(NetPieceFlags.Side|NetPieceFlags.Bottom|NetPieceFlags.PreserveShape))!=0||
                   p.m_Offset.x+p.m_Size.x*.5f<=clipMin||p.m_Offset.x-p.m_Size.x*.5f>=clipMax)continue;
                var template=NativePavingTemplate(p.m_Piece);if(template==null)continue;
                var sign=new float2((p.m_SectionFlags&NetSectionFlags.Invert)!=0?1:-1,(p.m_SectionFlags&NetSectionFlags.FlipMesh)!=0?1:-1);
                float pieceMin=clipMin,pieceMax=clipMax;
                if(kind==1&&bayTransition&&template.Indices.Length>0){
                    // Only the selected candidate piece; its filtered pavement
                    // triangles exclude the carriageway, gutter and curb faces.
                    var bounds=NativeSurfacePolicy.PavementBounds(template.Vertices,template.Indices,sign.x,p.m_Offset.x);
                    pieceMin=math.max(origin,bounds.x);pieceMax=math.min(origin+width,bounds.y);
                }
                if(pieceMax<=pieceMin)continue;
                var offset=p.m_Offset;offset.x-=center;offset.y+=raise;
                if((p.m_SectionFlags&NetSectionFlags.HalfLength)!=0){sign.y*=2;offset.z+=p.m_Size.z*.5f;}
                PavingMeshGeometry.Build(template.Vertices,template.Indices,offset,sign,p.m_Size.z,width,pieceMin-center,pieceMax-center,
                    segment.m_Left,segment.m_Right,segment.m_Left,segment.m_Right,out var vertices,out var indices,out var uv,true,sync);
                if(indices.Count==0)continue;
                var path=new Geometry.Arc(Between(segment.m_Left,segment.m_Right,((pieceMin+pieceMax)*.5f-origin)/width,0));
                var first=(orientedA.a+orientedB.a)*.5f;
                bool reverse=math.distance(path.Curve.d,first)<math.distance(path.Curve.a,first);
                float initial=float.IsNaN(phase)?-path.Length:phase;
                PavingUVPolicy.Subdivide(ref vertices,ref indices,ref uv);
                PavingUVPolicy.MapCorner(vertices,uv,reverse?Geometry.Reverse(path.Curve):path.Curve,pieceMax-pieceMin,initial);
                if(raise>0)CurbRamps(owner).Paving(ref vertices,ref indices,ref uv);
                CreatePavingMesh(record,PavingMaterial(style),vertices,indices,uv,raise>0?0:.012f);painted=true;
            }
            // Preserve the native curb; do not overlay a second synthetic lane.
            return painted;
        }}
    }
}
