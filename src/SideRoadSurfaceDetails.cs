using System;
using System.Collections.Generic;
using Colossal.Mathematics;
using Game.Prefabs;
using Unity.Entities;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.Rendering.HighDefinition;
namespace RoadStyleIntoKorea {
 public partial class StyleSystem {
  readonly Dictionary<int,Bounds2> sideRoadMarkUV=new Dictionary<int,Bounds2>();
  Material SideRoadMarkMaterial(EdgeStyle style,out Bounds2 area){
   int key=200+(int)style;
   if(pavingMaterials.TryGetValue(key,out var cached)){area=sideRoadMarkUV[key];return cached;}
   var prefab=assets.Get(AssetCatalog.Edge(style));
   var system=World.GetExistingSystemManaged<PrefabSystem>();
   if(!system.TryGetPrefab<PrefabBase>(prefab,out var source)||!(source is NetLaneGeometryPrefab lane)||lane.m_Meshes.Length==0)throw new InvalidOperationException("Side road marking mesh unavailable");
   var render=lane.m_Meshes[0].m_Mesh;
   area=render.GetComponent<DecalProperties>().m_TextureArea;
   var surface=PavingSurfaceAsset(prefab);surface.LoadProperties(false);
   Material material=null;
   try{
    material=new Material(Shader.Find("HDRP/Lit")){name="RSIK side road marking "+style,hideFlags=HideFlags.HideAndDontSave};
    if(!surface.TryGetTexture("_BaseColorMap",out var asset)||asset==null)throw new InvalidOperationException("Side road marking texture unavailable");
    var texture=asset.Load();pavingTextures.Add(asset);material.SetTexture("_BaseColorMap",texture);
    material.SetColor("_BaseColor",surface.TryGetColorProperty("_BaseColor",out var color)?color:Color.white);
    material.SetFloat("_Metallic",0);material.SetFloat("_Smoothness",.12f);
    material.SetFloat("_AlphaCutoffEnable",1);material.SetFloat("_AlphaCutoff",.1f);
    HDMaterial.ValidateMaterial(material);pavingMaterials.Add(key,material);sideRoadMarkUV[key]=area;return material;
   }catch{if(material!=null)UnityEngine.Object.Destroy(material);throw;}
   finally{surface.Unload();}
  }
  static void ClearSideRoadMarks(Record record){
   if(record.SideRoadMarks==null)return;
   foreach(var surface in record.SideRoadMarks.PavingSurfaces)surface.Dispose();
   record.SideRoadMarks=null;
  }
  void PaintSideRoadMarkPath(Record record,EdgeStyle style,SideRoadOpening opening,List<float3> path){
   if(record.SideRoadMarks==null)record.SideRoadMarks=new Record();
   style=style==EdgeStyle.WhiteSolid?EdgeStyle.WhiteSolid:EdgeStyle.YellowSolid;
   var material=SideRoadMarkMaterial(style,out var area);
   SideRoadMarkGeometry.Build(opening,path,area.min.x,area.max.x,out var vertices,out var indices,out var uv);
   CreatePavingMesh(record.SideRoadMarks,material,vertices,indices,uv,0);
  }
  float PaintSideRoadMark(Record record,EdgeStyle style,SideRoadOpening opening,Bezier4x3 curve,float phase=0){
   if(record.SideRoadMarks==null)record.SideRoadMarks=new Record();
   style=style==EdgeStyle.WhiteSolid?EdgeStyle.WhiteSolid:EdgeStyle.YellowSolid;
   var material=SideRoadMarkMaterial(style,out var area);
   // Physical 16 cm dashes; the decal projection box is not the paint width.
   // Carry the dash phase across the bend and its straight continuation.
   var arc=new Geometry.Arc(curve);
   for(float from=0;from<arc.Length;){
    float cycle=math.frac((phase+from)/1f),span=cycle<.5f?.5f-cycle:1f-cycle;
    float to=math.min(arc.Length,from+math.max(.001f,span));
    if(cycle<.5f&&to-from>.005f)PaintSideRoadRibbon(record.SideRoadMarks,material,opening,arc.Slice(from,to),.16f,area.min.x,area.max.x,6f,.025f);
    from=to;
   }
   return phase+arc.Length;
  }
  void PaintSideRoadRibbon(Record record,Material material,SideRoadOpening opening,Bezier4x3 curve,float width,float u0,float u1,float repeat,float lift){
   var arc=new Geometry.Arc(curve);int rows=math.clamp((int)math.ceil(arc.Length/.2f),2,128);
   var vertices=new List<float3>();var indices=new List<int>();var uv=new List<float2>();
   const int columns=4;
   for(int row=0;row<=rows;row++){
    float distance=arc.Length*row/rows,t=arc.T(distance);var center=Geometry.Position(curve,t);var left=Geometry.Left(Geometry.Tangent(curve,t));
    for(int col=0;col<=columns;col++){
     float f=col/(float)columns;var p=center+left*((f-.5f)*width);
     p.y=opening.SurfaceHeight(p)+lift;vertices.Add(p);uv.Add(new float2(math.lerp(u0,u1,f),distance/repeat));
    }
   }
   for(int row=0;row<rows;row++)for(int col=0;col<columns;col++){
    int a=row*(columns+1)+col,b=a+1,c=a+columns+1,d=c+1;
    if(math.cross(vertices[b]-vertices[a],vertices[c]-vertices[a]).y>0){indices.AddRange(new[]{a,b,c,b,d,c});}
    else{indices.AddRange(new[]{a,c,b,b,c,d});}
   }
   CreatePavingMesh(record,material,vertices,indices,uv,0);
  }
  void PaintSideRoadCurbRims(Entity road,Record record){{
   var openings=SideRoadOpenings(road);if(openings.Count==0)return;
   var material=CurbMaterial();
   foreach(var opening in openings)foreach(int end in new[]{-1,1}){
    if(opening.CutSurface){
     SideRoadCurbGeometry.Build(opening,end,out var vertices,out var indices,out var uv);
     CreatePavingMesh(record,material,vertices,indices,uv,.002f,true);continue;
    }
    var bend=opening.Bend(end,0);
    // The existing stone is centred 0.5 m outside the apron inner edge.
    // Start at that intersection so the thin rim meets the existing curb.
    float depth=math.min(opening.CutSurface?opening.CurbJoinDepth:.5f,opening.Radius*.8f),lo=0,hi=1;
    for(int i=0;i<12;i++){
     float t=(lo+hi)*.5f;var p=Geometry.Position(bend,t);
     float rt=opening.Road.Nearest(p);var center=Geometry.Position(opening.Road.Curve,rt);
     float d=math.dot(p-center,Geometry.Left(Geometry.Tangent(opening.Road.Curve,rt)))*opening.Side-opening.Inner;
     if(d<depth)lo=t;else hi=t;
    }
    bend=Geometry.Cut(bend,(lo+hi)*.5f,1);
    PaintSideRoadRibbon(record,material,opening,bend,.2f,.2817382812f,.2963867188f,-32.016f,.028f);
    var finish=opening.Point(end*opening.Width*.5f,opening.Outer-opening.Inner);
    if(math.distance(bend.d,finish)>.025f)PaintSideRoadRibbon(record,material,opening,Geometry.Line(bend.d,finish),.2f,.2817382812f,.2963867188f,-32.016f,.028f);
   }
  }}
 }
}
