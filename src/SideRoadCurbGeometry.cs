using System.Collections.Generic;
using Unity.Mathematics;
namespace RoadStyleIntoKorea {
 internal static class SideRoadCurbGeometry {
  internal static void Build(SideRoadOpening opening,int end,out List<float3> vertices,out List<int> indices,out List<float2> uv){
   vertices=new List<float3>();indices=new List<int>();uv=new List<float2>();
   var join=opening.NativeCurbDepths==null?new float2(opening.CurbJoinDepth-.2f,opening.CurbJoinDepth):opening.NativeCurbDepths[end<0?0:1];
   float length=opening.Outer-opening.Inner,start=math.min(join.y,length);
   var depths=new List<float>{start};
   for(int i=1;i<=32;i++){float depth=length*i/32f;if(depth>start+.0001f)depths.Add(depth);}
   if(depths.Count<2)return;
   float distance=0;var previous=float3.zero;
   float3 joinFront=default,joinBack=default,joinMiter=default;bool nativeJoin=join.x>=0&&start>join.x;
   for(int i=0;i<depths.Count;i++){
    float depth=depths[i];var back=opening.CutBoundary(end,depth);
    var before=opening.CutBoundary(end,depths[math.max(0,i-1)]);
    var after=opening.CutBoundary(end,depths[math.min(depths.Count-1,i+1)]);
    var inward=Geometry.Left(after-before);
    var middle=opening.Point(0,depth);
    if(math.dot(inward,middle-back)<0)inward=-inward;
    var front=back+inward*.2f;
    if(i==0&&!nativeJoin&&start<=.0001f){
     // A generated shoulder curb extends 20cm toward the road. Its joining
     // cross-section must share that direction, not the first polygon chord.
     var roadOut=Geometry.Left(Geometry.Tangent(opening.Road.Curve,opening.Road.Nearest(back)))*opening.Side;
     front=back-roadOut*NativeCurbGeometry.RaisedBand;
    }
    if(i==0&&nativeJoin){
     joinFront=opening.CutBoundary(end,join.x);
     // Intersect the offset rail with the native front plane. Moving the whole
     // first rail to the cut intersection made a long diagonal spike instead.
     var outward=Geometry.Left(Geometry.Tangent(opening.Road.Curve,opening.Road.Nearest(back)))*opening.Side;
     var direction=math.normalizesafe(after-before);direction.y=0;
     float denominator=math.dot(direction,outward);
     if(math.abs(denominator)>.1f){
      float travel=math.dot(joinFront-front,outward)/denominator;
      if(math.abs(travel)<1f)front+=direction*travel;
     }
    }
    float bottom=opening.SurfaceHeight(front)-.006f;
    float top=math.max(bottom+.002f,opening.NativeTop(back));
    back.y=top;front.y=top;
    if(i==0){joinBack=back;joinMiter=front;joinFront.y=top;}
    if(i>0)distance+=math.distance(previous.xz,back.xz);previous=back;
    var lowFront=front;lowFront.y=bottom;
    var lowBack=back;lowBack.y=math.min(bottom,opening.SurfaceHeight(back)-.006f);
    vertices.AddRange(new[]{front,back,lowFront,lowBack});
    uv.AddRange(new[]{new float2(.28173828f,-distance/32),new float2(.29638672f,-distance/32),new float2(.28173828f,-distance/32),new float2(.29638672f,-distance/32)});
    if(i>0){
     int a=(i-1)*4,b=i*4;
     Face(vertices,indices,a,a+1,b,new float3(0,1,0));Face(vertices,indices,a+1,b+1,b,new float3(0,1,0));
     Face(vertices,indices,a+2,a,b+2,inward);Face(vertices,indices,a,b,b+2,inward);
     Face(vertices,indices,a+1,a+3,b+1,-inward);Face(vertices,indices,a+3,b+3,b+1,-inward);
    }
   }
   var tangent=vertices[4]-vertices[0];
   if(!nativeJoin){Face(vertices,indices,0,2,1,-tangent);Face(vertices,indices,1,2,3,-tangent);}
   int k=vertices.Count-4;tangent=vertices[k]-vertices[k-4];
   Face(vertices,indices,k,k+1,k+2,tangent);Face(vertices,indices,k+1,k+3,k+2,tangent);
   // Separate atlas rails and normals for top, front, back and end caps.
   // Sharing top/bottom U collapsed every vertical face into a texture line.
   var mappedVertices=new List<float3>();var mappedUV=new List<float2>();var mappedIndices=new List<int>();
   var rails=new Dictionary<int,int>();
   for(int i=0;i<indices.Count;i+=3){
    int a=indices[i],b=indices[i+1],c=indices[i+2];
    bool topFace=a%4<2&&b%4<2&&c%4<2;
    bool cap=a/4==b/4&&a/4==c/4;
    foreach(int index in new[]{a,b,c}){
     var tex=uv[index];int corner=index%4;
     if(!topFace)tex.x=corner<2?.29638672f:.28173828f;
     if(cap)tex.y+=(corner%2)*.2f/32f;
     // Share vertices along each face to avoid triangle-by-triangle lighting,
     // but keep separate vertices across the stone's sharp top/side edges.
     int face=topFace?0:cap?3+i:(a%2==0&&b%2==0&&c%2==0?1:2);
     int key=face*vertices.Count+index;
     if(!rails.TryGetValue(key,out int mapped)){mapped= mappedVertices.Count;rails.Add(key,mapped);mappedVertices.Add(vertices[index]);mappedUV.Add(tex);}
     mappedIndices.Add(mapped);
    }
   }
   vertices=mappedVertices;indices=mappedIndices;uv=mappedUV;
   if(nativeJoin){
    int n=vertices.Count;var low=joinFront;low.y=opening.SurfaceHeight(joinFront)-.006f;
    var lowMiter=joinMiter;lowMiter.y=opening.SurfaceHeight(joinMiter)-.006f;
    float span=math.distance(joinFront.xz,joinMiter.xz)/32f;
    vertices.AddRange(new[]{joinFront,joinBack,joinMiter,joinFront,low,joinMiter,lowMiter});
    uv.AddRange(new[]{new float2(.28173828f,span),new float2(.29638672f,0),new float2(.28173828f,0),
     new float2(.29638672f,span),new float2(.28173828f,span),new float2(.29638672f,0),new float2(.28173828f,0)});
    Face(vertices,indices,n,n+1,n+2,new float3(0,1,0));
    var outward=Geometry.Left(Geometry.Tangent(opening.Road.Curve,opening.Road.Nearest(joinFront)))*-opening.Side;
    Face(vertices,indices,n+3,n+4,n+5,outward);Face(vertices,indices,n+4,n+6,n+5,outward);
   }
  }
  static void Face(List<float3> v,List<int> ix,int a,int b,int c,float3 normal){
   var cross=math.cross(v[b]-v[a],v[c]-v[a]);if(math.lengthsq(cross)<1e-12f)return;
   ix.Add(a);ix.Add(math.dot(cross,normal)>=0?b:c);ix.Add(math.dot(cross,normal)>=0?c:b);
  }
 }
}
