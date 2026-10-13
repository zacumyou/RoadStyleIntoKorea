using System.Collections.Generic;
using Colossal.Mathematics;
using Unity.Entities;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.Rendering.HighDefinition;
namespace RoadStyleIntoKorea {
 public partial class StyleSystem {
  const string CurbLaneName="CSKRNativeCurb1mNetLane";
  Material CurbMaterial(){
   const int key=100;if(pavingMaterials.TryGetValue(key,out var material))return material;
   var surface=PavingSurfaceAsset(assets.Get("Sidewalk With Parking Piece 6"));
   var shader=Shader.Find("HDRP/Lit");
   if(surface==null||shader==null)throw new System.InvalidOperationException("Native Sidewalk With Parking Piece 6 curb material unavailable");
   bool loaded=!surface.isDataLoaded;if(loaded)surface.LoadProperties(false);
   material=new Material(shader){name="RSIK native RoadParts curb",hideFlags=HideFlags.HideAndDontSave};
   var acquired=new List<Colossal.IO.AssetDatabase.TextureAsset>();
   try{
    foreach(var name in new[]{"_BaseColorMap","_NormalMap","_MaskMap"}){
     if(!surface.TryGetTexture(name,out var asset)||asset==null)throw new System.InvalidOperationException("Native curb texture missing: "+name);
     var texture=asset.Load();acquired.Add(asset);material.SetTexture(name,texture);
    }
    material.SetColor("_BaseColor",surface.TryGetColorProperty("_BaseColor",out var color)?color:Color.white);
    foreach(var property in new[]{"_Metallic","_Smoothness","_NormalScale","_AORemapMin","_AORemapMax","_SmoothnessRemapMin","_SmoothnessRemapMax"})
     if(surface.TryGetFloatProperty(property,out var value)&&material.HasProperty(property))material.SetFloat(property,value);
    HDMaterial.ValidateMaterial(material);pavingMaterials.Add(key,material);pavingTextures.AddRange(acquired);
    return material;
   }catch{foreach(var asset in acquired)asset.Unload();UnityEngine.Object.Destroy(material);throw;}
   finally{if(loaded)surface.Unload();}
  }
  void PaintCurbLane(Entity owner,Record record,Bezier4x3 a,Bezier4x3 b,bool fromA,float inset=0,CurbRampPolicy sharedRamps=null){
   var outside=new Geometry.Arc(fromA?a:b);var inside=fromA?b:a;var curve=CurbLaneGeometry.Curve(a,b,fromA,inset);
   var ramps=sharedRamps??CurbRamps(owner);
   var back=CurbLaneGeometry.Curve(a,b,!fromA,0);
   foreach(var clear in new[]{back}){
    NativeCurbGeometry.Build(clear,out var backVertices,out var backIndices,out var backUV,null,true,ramps);
    CreatePavingMesh(record,CurbMaterial(),backVertices,backIndices,backUV,0,true);
   }
   foreach(var part in Geometry.ClearSlices(curve,p=>{float t=outside.Nearest(p);return math.distance(Geometry.Position(outside.Curve,t).xz,Geometry.Position(inside,t).xz)>=.05f;}))
    foreach(var clear in new[]{part}){
     NativeCurbGeometry.Build(clear,out var vertices,out var indices,out var uv,p=>{
      float t=outside.Nearest(p);var x=Geometry.Position(outside.Curve,t);var y=Geometry.Position(inside,t);var across=y.xz-x.xz;
      float f=math.dot(p.xz-x.xz,across)/math.max(.000001f,math.dot(across,across));
      return math.lerp(x.y,y.y,f);
     },false,ramps);
     CreatePavingMesh(record,CurbMaterial(),vertices,indices,uv,0,true);
    }
  }
 }
}
