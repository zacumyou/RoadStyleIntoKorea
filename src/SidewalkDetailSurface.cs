using System;
using System.Collections.Generic;
using Colossal.IO.AssetDatabase;
using Colossal.Mathematics;
using Game.Prefabs;
using Unity.Entities;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.Rendering.HighDefinition;
namespace RoadStyleIntoKorea {
 public partial class StyleSystem {
  // Native CurvedDecal lanes project onto the game's Roads receiver. Our HDRP/Lit
  // paving is a separate renderer; draw the same texture on its measured surface.
  Material SidewalkDetailMaterial(int kind){
   if(pavingMaterials.TryGetValue(-kind,out var cached))return cached;
   var surface=PavingSurfaceAsset(assets.Get(SidewalkDetailAsset(kind)));
   var shader=Shader.Find("HDRP/Lit");if(surface==null||shader==null)return null;
   surface.LoadProperties(false);var acquired=new List<TextureAsset>();
   var material=new Material(shader){name="RSIK sidewalk detail "+kind,hideFlags=HideFlags.HideAndDontSave};
   try{
    foreach(var name in new[]{"_BaseColorMap","_NormalMap","_MaskMap"})if(surface.TryGetTexture(name,out var asset)&&asset!=null){material.SetTexture(name,asset.Load());acquired.Add(asset);}
    if(material.GetTexture("_BaseColorMap")==null)throw new InvalidOperationException("Missing sidewalk detail BaseColor");
    material.SetColor("_BaseColor",Color.white);material.SetFloat("_Metallic",0);material.SetFloat("_Smoothness",.2f);
    material.SetFloat("_AlphaCutoffEnable",1);material.SetFloat("_AlphaCutoff",.05f);material.SetFloat("_DoubleSidedEnable",1);
    HDMaterial.ValidateMaterial(material);pavingMaterials.Add(-kind,material);pavingTextures.AddRange(acquired);return material;
   }catch{foreach(var asset in acquired)asset.Unload();UnityEngine.Object.Destroy(material);throw;}
   finally{surface.Unload();}
  }
  void PaintSidewalkDetailSurface(Entity road,Record record,int kind,Bezier4x3 curve,List<SidewalkFootprint> surfaces=null){
   if(surfaces==null){if(!records.TryGetValue(road,out var paving)||paving.WalkSurface==null)return;surfaces=new List<SidewalkFootprint>{paving.WalkSurface};}
   var material=SidewalkDetailMaterial(kind);if(material==null)return;
   var prefabSystem=World.GetExistingSystemManaged<PrefabSystem>();
   if(!prefabSystem.TryGetPrefab<PrefabBase>(assets.Get(SidewalkDetailAsset(kind)),out var source)||!(source is NetLaneGeometryPrefab lane)||lane.m_Meshes.Length==0)return;
   // The shared atlas occupies only the DecalProperties area, not the full image.
   var area=lane.m_Meshes[0].m_Mesh.GetComponent<DecalProperties>().m_TextureArea;
   var arc=new Geometry.Arc(curve);int steps=math.clamp((int)math.ceil(arc.Length/.5f),1,1024);
   float half=(SidewalkDetailWidth(kind)+.02f)*.5f,repeat=kind==9?9.9f:3.6f;
   var points=new List<float3>();var indices=new List<int>();var uv=new List<float2>();var valid=new List<bool>();
   for(int i=0;i<=steps;i++){
    float distance=arc.Length*i/steps,t=arc.T(distance);var center=Geometry.Position(curve,t);var across=Geometry.Left(Geometry.Tangent(curve,t));
    for(int side=0;side<2;side++){
     var point=center+across*(side==0?-half:half);bool found=DetailHeight(surfaces,point,out float height);
     if(found)point.y=height;points.Add(point);valid.Add(found);uv.Add(new float2(math.lerp(area.min.x,area.max.x,side),math.lerp(area.min.y,area.max.y,distance/repeat)));
    }
    if(i>0){int n=i*2-2;if(valid[n]&&valid[n+1]&&valid[n+2]&&valid[n+3]){indices.Add(n);indices.Add(n+2);indices.Add(n+1);indices.Add(n+1);indices.Add(n+2);indices.Add(n+3);}}
   }
   for(int i=0;i<indices.Count;i+=3)if(math.cross(points[indices[i+1]]-points[indices[i]],points[indices[i+2]]-points[indices[i]]).y<0){int swap=indices[i+1];indices[i+1]=indices[i+2];indices[i+2]=swap;}
   CreatePavingMesh(record,material,points,indices,uv,kind==9?.012f:.008f);
  }
 }
}


