using System.Collections.Generic;
using Colossal.Mathematics;
using Unity.Mathematics;
namespace RoadStyleIntoKorea {
 internal static class CurbRibbonGeometry {
  // A small raised concrete strip; independent of the legacy Curved Surface shader.
  internal static void Build(Bezier4x3 curve,out List<float3> vertices,out List<int> indices,out List<float2> uv,System.Func<float3,float> surfaceHeight=null){
   vertices=new List<float3>();indices=new List<int>();uv=new List<float2>();
   var arc=new Geometry.Arc(curve);int rows=math.clamp((int)math.ceil(arc.Length),1,1024);
   for(int i=0;i<=rows;i++){
    float t=i/(float)rows;var p=Geometry.Position(curve,t);var l=Geometry.Left(Geometry.Tangent(curve,t))*.09f;
    var right=p-l;var left=p+l;
    if(surfaceHeight!=null){right.y=surfaceHeight(right);left.y=surfaceHeight(left);}
    // Separate vertices for side/top/side: smoothing across these corners makes
    // a rectangular curb look like a floating pipe. Smooth only along each face.
    var topRight=right+new float3(0,.12f,0);var topLeft=left+new float3(0,.12f,0);
    vertices.Add(right);vertices.Add(topRight);
    vertices.Add(topRight);vertices.Add(topLeft);
    vertices.Add(topLeft);vertices.Add(left);
    for(int j=0;j<6;j++)uv.Add(new float2((j/2)*.12f+(j%2)*.12f,t*arc.Length));
    if(i==0)continue;int k=(i-1)*6;
    for(int j=0;j<6;j+=2){indices.Add(k+j);indices.Add(k+j+1);indices.Add(k+j+6);indices.Add(k+j+1);indices.Add(k+j+7);indices.Add(k+j+6);}
   }
   // Close exposed ends at crossings and taper cuts with their own normals.
   foreach(bool end in new[]{false,true}){
    int row=end?rows*6:0,k=vertices.Count;
    vertices.Add(vertices[row]);vertices.Add(vertices[row+1]);vertices.Add(vertices[row+3]);vertices.Add(vertices[row+5]);
    uv.Add(new float2(0,0));uv.Add(new float2(0,.12f));uv.Add(new float2(.18f,.12f));uv.Add(new float2(.18f,0));
    indices.Add(k);indices.Add(k+(end?1:2));indices.Add(k+(end?2:1));
    indices.Add(k);indices.Add(k+(end?2:3));indices.Add(k+(end?3:2));
   }
  }
 }
}
