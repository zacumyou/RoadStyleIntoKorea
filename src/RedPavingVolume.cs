using System.Collections.Generic;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.Rendering;
namespace RoadStyleIntoKorea {public partial class StyleSystem {
 void CreateRedPavingVolume(Record record,Material material,Geometry.Arc arc,float start,float end,float width,float phase,SidewalkFootprint surface){
  var center=Geometry.Position(arc.Curve,arc.T((start+end)*.5f));
  var forward=Geometry.Tangent(arc.Curve,arc.T((start+end)*.5f));forward.y=0;
  var rotation=Quaternion.LookRotation((Vector3)forward,Vector3.up);var inverse=Quaternion.Inverse(rotation);
  var world=new List<float3>();var vertices=new List<Vector3>();var indices=new List<int>();
  int steps=math.max(1,(int)math.ceil((end-start)/.5f));float half=math.max(.05f,width*.5f-.025f);
  for(int i=0;i<=steps;i++){
   float t=arc.T(math.lerp(start,end,i/(float)steps));var p=Geometry.Position(arc.Curve,t);var across=Geometry.Left(Geometry.Tangent(arc.Curve,t));
   for(int level=0;level<2;level++)for(int side=0;side<2;side++){
    var v=p+across*(side==0?-half:half);
    if(surface!=null&&surface.TryHeight(v,out float height)&&math.abs(height-p.y)<1)v.y=height;
    v.y+=level==0?-.5f:.5f;world.Add(v);vertices.Add(inverse*(Vector3)(v-center));
   }
   if(i==0)continue;int a=(i-1)*4,b=i*4;
   RedVolumeQuad(world,indices,a,a+1,b+1,b,new float3(0,-1,0));
   RedVolumeQuad(world,indices,a+2,b+2,b+3,a+3,new float3(0,1,0));
   RedVolumeQuad(world,indices,a,b,b+2,a+2,-across);
   RedVolumeQuad(world,indices,a+1,a+3,b+3,b+1,across);
  }
  RedVolumeQuad(world,indices,0,2,3,1,-Geometry.Tangent(arc.Curve,arc.T(start)));
  int last=steps*4;RedVolumeQuad(world,indices,last,last+1,last+3,last+2,Geometry.Tangent(arc.Curve,arc.T(end)));
  var mesh=new Mesh{name="RSIK red pavement decal volume",hideFlags=HideFlags.HideAndDontSave};GameObject go=null;
  try{
   mesh.SetVertices(vertices);mesh.SetTriangles(indices,0);mesh.RecalculateNormals();
   // Albedo-only rendering still uses the native vertex contract.
   var uv=new List<Vector2>();var tangents=new List<Vector4>();foreach(var v in vertices){uv.Add(new Vector2(v.x,v.z));tangents.Add(new Vector4(1,0,0,-1));}
   mesh.SetUVs(0,uv);mesh.SetTangents(tangents);mesh.RecalculateBounds();
   var bounds=mesh.bounds;var extent=Vector3.Max(bounds.min*-1,bounds.max)*2;
   go=new GameObject("RSIK red pavement decal"){hideFlags=HideFlags.HideAndDontSave};go.transform.SetPositionAndRotation((Vector3)center,rotation);
   go.AddComponent<MeshFilter>().sharedMesh=mesh;var renderer=go.AddComponent<MeshRenderer>();renderer.sharedMaterial=material;
   renderer.shadowCastingMode=ShadowCastingMode.Off;renderer.receiveShadows=false;
   var properties=new MaterialPropertyBlock();properties.SetVector("colossal_MeshSize",new Vector4(extent.x+.002f,extent.y+.002f,extent.z+.002f,0));
   properties.SetVector("colossal_TextureArea",new Vector4(.45f,(phase+start)/4,.55f,(phase+end)/4));renderer.SetPropertyBlock(properties);
   record.PavingSurfaces.Add(new PavingSurfaceInstance{Object=go,Mesh=mesh,Style=-100});
  }catch{if(go!=null)Object.Destroy(go);Object.Destroy(mesh);throw;}
 }
 static void RedVolumeQuad(List<float3> vertices,List<int> indices,int a,int b,int c,int d,float3 outward){
  if(math.dot(math.cross(vertices[b]-vertices[a],vertices[c]-vertices[a]),outward)<0){int swap=b;b=d;d=swap;}
  indices.Add(a);indices.Add(b);indices.Add(c);indices.Add(a);indices.Add(c);indices.Add(d);
 }
}}
