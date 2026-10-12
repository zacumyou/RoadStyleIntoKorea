using System.Collections.Generic;
using Colossal.Mathematics;
using Unity.Mathematics;
namespace RoadStyleIntoKorea {
 // Exact Sidewalk6 curb section and RoadParts atlas U coordinates, not a synthetic box.
 // Source/hash and vertex indices: docs/curb-0.1.78/native-profile.json.
 internal static class NativeCurbGeometry {
  // Input curve is the unchanged paving border. The profile extends outward;
  // its back edge meets the brick surface without reserving space inside it.
  internal const float Band=.6f, Rise=.2f;
  static readonly float3[] section={
   new float3(0.4000244141f,0.2000209999f,0.2817382812f),
   new float3(0.5999755859f,0.2000209999f,0.2963867188f),
   new float3(0.4000244141f,-0.0000312805f,0.2817382812f),
   new float3(0.4000244141f,0.2000209999f,0.2963867188f),
   new float3(0.0000000000f,0.0000297546f,0.0000000000f),
   new float3(0.4000244141f,-0.0000312805f,0.0248107910f)
  };
  // Only the raised stone blocks the gutter; the flat apron can overlap it.
  internal static float RaisedBand {
   get {float front=Band;foreach(var v in section)if(v.y>Rise*.5f)front=math.min(front,v.x);return math.max(0,Band-front);}
  }
  internal static void BuildPath(List<float3> path,float3 startTangent,float3 endTangent,bool outerFace,
   out List<float3> vertices,out List<int> indices,out List<float2> uv,CurbRampPolicy ramps=null){
   vertices=new List<float3>();indices=new List<int>();uv=new List<float2>();if(path.Count<2)return;if(ramps!=null)path=ramps.SplitPath(path);
   int stride=outerFace?2:6;float distance=0;
   for(int i=0;i<path.Count;i++){
    if(i>0)distance+=math.distance(path[i-1],path[i]);
    float3 left;
    if(i==0)left=Geometry.Left(startTangent);
    else if(i==path.Count-1)left=Geometry.Left(endTangent);
    else{
     var before=Geometry.Left(path[i]-path[i-1]);var after=Geometry.Left(path[i+1]-path[i]);
     var bisector=math.normalizesafe(before+after,after);
     left=bisector/math.max(.25f,math.dot(bisector,after));
    }
    for(int j=0;j<stride;j++){
     var s=section[outerFace?j+2:j];
     vertices.Add(path[i]+left*(outerFace?0:s.x-Band)+new float3(0,s.y*(ramps==null?1:ramps.Height(path[i])/Rise),0));
     uv.Add(new float2(s.z,distance*-.0312347412f));
    }
    if(i==0)continue;int k=(i-1)*stride;
    // The original asphalt already fills the gutter. Do not render a coplanar overlay.
    for(int j=0;j<(outerFace?2:4);j+=2){indices.Add(k+j);indices.Add(k+j+1);indices.Add(k+j+stride);indices.Add(k+j+1);indices.Add(k+j+stride+1);indices.Add(k+j+stride);}
   }
  }
  internal static void Build(Bezier4x3 curve,out List<float3> vertices,out List<int> indices,out List<float2> uv,System.Func<float3,float> surfaceHeight=null,bool outerFace=false,CurbRampPolicy ramps=null){
   vertices=new List<float3>();indices=new List<int>();uv=new List<float2>();
   var arc=new Geometry.Arc(curve);int rows=math.clamp((int)math.ceil(arc.Length),1,1024);
   var path=new List<float3>();for(int i=0;i<=rows;i++)path.Add(arc.At(arc.Length*i/rows));
   if(ramps!=null)path=ramps.SplitPath(path);
   int stride=outerFace?2:6;
   for(int i=0;i<path.Count;i++){
    float t=arc.Nearest(path[i]),distance=arc.Distance(t);var p=path[i];var left=Geometry.Left(Geometry.Tangent(curve,t));
    for(int j=0;j<stride;j++){var s=section[outerFace?j+2:j];
     var v=p+left*(outerFace?0:s.x-Band);v.y=(surfaceHeight==null?v.y:surfaceHeight(v))+s.y*(ramps==null?1:ramps.Height(p)/Rise);
     vertices.Add(v);uv.Add(new float2(s.z,distance*-0.0312347412f));
    }
    if(i==0)continue;int k=(i-1)*stride;
    // The original asphalt already fills the gutter. Do not render a coplanar overlay.
    for(int j=0;j<(outerFace?2:4);j+=2){indices.Add(k+j);indices.Add(k+j+1);indices.Add(k+j+stride);indices.Add(k+j+1);indices.Add(k+j+stride+1);indices.Add(k+j+stride);}
   }
  }
 }
}
