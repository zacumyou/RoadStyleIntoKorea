using System;
using System.Collections.Generic;
using Colossal.IO.AssetDatabase;
using Colossal.Mathematics;
using Game.Prefabs;
using Unity.Entities;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.HighDefinition;
namespace RoadStyleIntoKorea
{
    public partial class StyleSystem
    {
        readonly Dictionary<int,Material> pavingMaterials=new Dictionary<int,Material>();
        readonly List<TextureAsset> pavingTextures=new List<TextureAsset>();
        sealed class PavingSurfaceInstance
        {
            internal GameObject Object;internal Mesh Mesh;internal int Style;
            internal void Dispose(){if(Object!=null)UnityEngine.Object.Destroy(Object);if(Mesh!=null)UnityEngine.Object.Destroy(Mesh);}
        }
        SurfaceAsset PavingSurfaceAsset(Entity entity,int depth=0)
        {
            if(depth>4||entity==Entity.Null||!EntityManager.Exists(entity))return null;
            var prefabs=World.GetExistingSystemManaged<PrefabSystem>();
            if(prefabs==null)return null;
            // TryGetPrefab<T> returns true for a registered prefab even when its
            // internal `as T` cast returns null. Check the actual runtime type.
            if(prefabs.TryGetPrefab<PrefabBase>(entity,out var prefab)&&prefab!=null)
            {
                if(prefab is RenderPrefab render)
                    foreach(var surface in render.surfaceAssets)if(surface!=null)return surface;
                if(prefab is NetLaneGeometryPrefab lane&&lane.m_Meshes!=null)
                    foreach(var info in lane.m_Meshes)
                        if(info.m_Mesh!=null)foreach(var surface in info.m_Mesh.surfaceAssets)if(surface!=null)return surface;
            }
            if(EntityManager.HasBuffer<SubMesh>(entity))foreach(var sub in EntityManager.GetBuffer<SubMesh>(entity,true))
            {var surface=PavingSurfaceAsset(sub.m_SubMesh,depth+1);if(surface!=null)return surface;}
            return null;
        }
        Material PavingMaterial(int style)
        {
            if(pavingMaterials.TryGetValue(style,out var existing))return existing;
            var surface=PavingSurfaceAsset(assets.Get(PavingPolicy.Asset(style,8)));
            var shader=Shader.Find("HDRP/Lit");
            if(surface==null||shader==null)throw new InvalidOperationException("Paving surface or HDRP/Lit unavailable; keeping native sidewalk.");
            surface.LoadProperties(false);
            var material=new Material(shader){name="RSIK world paving "+style,hideFlags=HideFlags.HideAndDontSave};
            var acquired=new List<TextureAsset>();
            try
            {
                foreach(var name in new[]{"_BaseColorMap","_NormalMap","_MaskMap"})
                    if(surface.TryGetTexture(name,out var asset)&&asset!=null)
                    {
                        var texture=asset.Load();acquired.Add(asset);material.SetTexture(name,texture);
                    }
                if(material.GetTexture("_BaseColorMap")==null)throw new InvalidOperationException("Paving BaseColor unavailable");
                material.SetColor("_BaseColor",Color.white);material.SetFloat("_Metallic",0);
                material.SetFloat("_Smoothness",.25f);material.SetFloat("_NormalScale",.55f);
                HDMaterial.ValidateMaterial(material);
                pavingMaterials.Add(style,material);pavingTextures.AddRange(acquired);return material;
            }
            catch{foreach(var texture in acquired)texture.Unload();UnityEngine.Object.Destroy(material);throw;}
            finally{surface.Unload();}
        }
        float PaintPavingSurface(Record record,int style,Bezier4x3 a,Bezier4x3 b,float phase=0,bool curb=false,bool curbAtA=false,bool nativeRows=false,Entity owner=default)
        {
            var material=PavingMaterial(style);
            PavingSurfaceGeometry.Build(a,b,out var positions,out var indices,nativeRows?8:0);
            if(indices.Count==0)return phase;
            if(curb)
            {
                PaintCurbLane(owner,record,a,b,curbAtA);
            }
            // Preserve a physical curb/gutter band even where a corner narrows.
            if(curb)PavingSurfaceGeometry.Inset(positions,curbAtA,.6f);
            // Start corners end at edge phase zero, after the physical inset.
            if(float.IsNaN(phase)){var measured=PavingSurfaceGeometry.RibbonUV(positions);phase=-measured[measured.Count-1].y*8;}
            var textureUV=PavingSurfaceGeometry.RibbonUV(positions,phase);
            CreatePavingMesh(record,material,positions,indices,textureUV);
            return textureUV[textureUV.Count-1].y*8;
        }
        void CreatePavingMesh(Record record,Material material,List<float3> positions,List<int> indices,List<float2> textureUV,float lift=.012f,bool hardEdges=false)
        {{
            if(indices.Count==0)return;
            var vertices=new List<Vector3>();var uv=new List<Vector2>();var triangles=new List<int>();
            var anchor=positions[0];
            for(int i=0;i<positions.Count;i++){vertices.Add((Vector3)(positions[i]-anchor));var tex=textureUV[i];uv.Add(new Vector2(tex.x,tex.y));}
            
            for(int i=0;i+2<indices.Count;i+=3)
            {
                var keys=new[]{math.round(positions[indices[i]]*1000).ToString(),math.round(positions[indices[i+1]]*1000).ToString(),math.round(positions[indices[i+2]]*1000).ToString()};
                Array.Sort(keys,StringComparer.Ordinal);var key=string.Join("|",keys);
                if(!record.PavingFaces.Add(key))continue;
                triangles.Add(indices[i]);triangles.Add(indices[i+1]);triangles.Add(indices[i+2]);
            }
            if(triangles.Count==0)return;
            var sums=new Dictionary<int3,float3>();
            for(int i=0;i<triangles.Count;i+=3)
            {
                var n=math.cross(positions[triangles[i+1]]-positions[triangles[i]],positions[triangles[i+2]]-positions[triangles[i]]);
                for(int j=0;j<3;j++){var key=(int3)math.round(positions[triangles[i+j]]*1000);sums.TryGetValue(key,out var sum);sums[key]=sum+n;}
            }
            var normals=new List<Vector3>();foreach(var point in positions){sums.TryGetValue((int3)math.round(point*1000),out var n);normals.Add((Vector3)math.normalizesafe(n,new float3(0,1,0)));}
            var mesh=new Mesh{name="RSIK native sidewalk ribbon",hideFlags=HideFlags.HideAndDontSave,indexFormat=IndexFormat.UInt32};
            GameObject go=null;
            try
            {
                mesh.SetVertices(vertices);mesh.SetUVs(0,uv);mesh.SetTriangles(triangles,0);
                if(hardEdges)mesh.RecalculateNormals();else mesh.SetNormals(normals);
                mesh.RecalculateTangents();mesh.RecalculateBounds();
                go=new GameObject("RSIK sidewalk surface"){hideFlags=HideFlags.HideAndDontSave};
                go.transform.position=(Vector3)(anchor+new float3(0,lift,0));
                go.AddComponent<MeshFilter>().sharedMesh=mesh;
                var renderer=go.AddComponent<MeshRenderer>();renderer.sharedMaterial=material;
                renderer.shadowCastingMode=ShadowCastingMode.Off;renderer.receiveShadows=true;
                int style=0;foreach(var pair in pavingMaterials)if(pair.Value==material){style=pair.Key;break;}
                record.PavingSurfaces.Add(new PavingSurfaceInstance{Object=go,Mesh=mesh,Style=style});
                if(style>=1&&style<=PavingPolicy.StyleCount){
                    if(record.WalkSurface==null)record.WalkSurface=new SidewalkFootprint();
                    var walkPoints=new List<float3>(positions.Count);foreach(var point in positions)walkPoints.Add(point+new float3(0,lift,0));
                    record.WalkSurface.Add(walkPoints,triangles);
                }
            }
            catch{if(go!=null)UnityEngine.Object.Destroy(go);UnityEngine.Object.Destroy(mesh);throw;}
        }}
        void DisposePavingMaterials()
        {
            pavingTemplates.Clear();redMaterialRetry=0;
            foreach(var material in pavingMaterials.Values)UnityEngine.Object.Destroy(material);pavingMaterials.Clear();
            DisposeSideRoadMaterials();
            foreach(var texture in pavingTextures)texture.Unload();pavingTextures.Clear();
        }
    }
}
