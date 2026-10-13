using System;
using System.Collections.Generic;
using Unity.Mathematics;

namespace RoadStyleIntoKorea {
 internal static class SideRoadCutGeometry {
  internal struct Vertex {
   internal float3 Local,World,Normal;internal float4 Tangent;internal float2 UV;
   internal static Vertex Mix(Vertex a,Vertex b,float t)=>new Vertex{Local=math.lerp(a.Local,b.Local,t),World=math.lerp(a.World,b.World,t),Normal=math.lerp(a.Normal,b.Normal,t),Tangent=math.lerp(a.Tangent,b.Tangent,t),UV=math.lerp(a.UV,b.UV,t)};
  }
  internal sealed class Cell {
   internal float2[] Points;internal float2 Min,Max;
   internal Cell(float2 a,float2 b,float2 c,float2 d){
    Points=new[]{a,b,c,d};if(Cross(b-a,c-b)<0)Array.Reverse(Points);
    Min=math.min(math.min(a,b),math.min(c,d));Max=math.max(math.max(a,b),math.max(c,d));
   }
  }
  internal static List<Cell> Cells(List<SideRoadOpening> openings){
   var cells=new List<Cell>();
   foreach(var o in openings){
    const int steps=16;
    for(int i=0;i<steps;i++){
     float d0=(o.Outer-o.Inner)*i/steps,d1=(o.Outer-o.Inner)*(i+1)/steps;
     // Extend through the outside vertical sidewalk wall too.
     if(i==steps-1)d1+=.1f;
     cells.Add(new Cell(o.Point(-o.HalfSpan(d0),d0).xz,o.Point(o.HalfSpan(d0),d0).xz,o.Point(o.HalfSpan(d1),d1).xz,o.Point(-o.HalfSpan(d1),d1).xz));
    }
   }
   return cells;
  }
  static float Cross(float2 a,float2 b)=>a.x*b.y-a.y*b.x;
  static bool Overlap(List<Vertex> p,Cell cell){
   float2 min=p[0].World.xz,max=min;foreach(var v in p){min=math.min(min,v.World.xz);max=math.max(max,v.World.xz);}
   return math.all(max>=cell.Min)&&math.all(min<=cell.Max);
  }
  static List<Vertex> Half(List<Vertex> input,float2 a,float2 b,bool inside){
   var result=new List<Vertex>();if(input.Count==0)return result;
   var previous=input[input.Count-1];float pv=Cross(b-a,previous.World.xz-a);bool pin=inside?pv>=0:pv<=0;
   foreach(var current in input){
    float cv=Cross(b-a,current.World.xz-a);bool cin=inside?cv>=0:cv<=0;
    if(cin!=pin)result.Add(Vertex.Mix(previous,current,pv/(pv-cv)));
    if(cin)result.Add(current);previous=current;pv=cv;pin=cin;
   }
   return result;
  }
  internal static List<List<Vertex>> Subtract(List<Vertex> triangle,List<Cell> cells){
   var pieces=new List<List<Vertex>>{triangle};
   foreach(var cell in cells){
    var next=new List<List<Vertex>>();
    foreach(var polygon in pieces){
     if(!Overlap(polygon,cell)){next.Add(polygon);continue;}
     var remainder=polygon;
     for(int e=0;e<4&&remainder.Count>=3;e++){
      var a=cell.Points[e];var b=cell.Points[(e+1)%4];
      var outside=Half(remainder,a,b,false);if(outside.Count>=3)next.Add(outside);
      remainder=Half(remainder,a,b,true);
     }
    }
    pieces=next;if(pieces.Count==0)break;
    if(pieces.Count>4096)throw new InvalidOperationException("Side road clipping fragment limit");
   }
   return pieces;
  }
  internal static void Emit(Vertex a,Vertex b,Vertex c,List<Cell> cells,Func<float3,float3> deform,List<Vertex> output,int depth=0){
   var triangle=new List<Vertex>{a,b,c};bool near=false;foreach(var cell in cells)if(Overlap(triangle,cell)){near=true;break;}
   float ab=math.distancesq(a.World,b.World),bc=math.distancesq(b.World,c.World),ca=math.distancesq(c.World,a.World);
   if(near&&deform!=null&&depth<16&&math.max(ab,math.max(bc,ca))>4){
    if(ab>=bc&&ab>=ca){var m=Vertex.Mix(a,b,.5f);m.World=deform(m.Local);Emit(a,m,c,cells,deform,output,depth+1);Emit(m,b,c,cells,deform,output,depth+1);}
    else if(bc>=ca){var m=Vertex.Mix(b,c,.5f);m.World=deform(m.Local);Emit(a,b,m,cells,deform,output,depth+1);Emit(a,m,c,cells,deform,output,depth+1);}
    else{var m=Vertex.Mix(c,a,.5f);m.World=deform(m.Local);Emit(a,b,m,cells,deform,output,depth+1);Emit(m,b,c,cells,deform,output,depth+1);}
    return;
   }
   foreach(var polygon in Subtract(triangle,cells))for(int j=1;j+1<polygon.Count;j++){
    if(math.lengthsq(math.cross(polygon[j].World-polygon[0].World,polygon[j+1].World-polygon[0].World))<1e-12f)continue;
    output.Add(polygon[0]);output.Add(polygon[j]);output.Add(polygon[j+1]);
   }
   if(output.Count>60000)throw new InvalidOperationException("Side road mesh vertex limit");
  }
  internal static void ClipSurface(List<Cell> cells,ref List<float3> positions,ref List<int> indices,ref List<float2> uv){
   var result=new List<Vertex>();
   for(int i=0;i<indices.Count;i+=3){
    int a=indices[i],b=indices[i+1],c=indices[i+2];
    Emit(new Vertex{Local=positions[a],World=positions[a],UV=uv[a]},new Vertex{Local=positions[b],World=positions[b],UV=uv[b]},new Vertex{Local=positions[c],World=positions[c],UV=uv[c]},cells,null,result);
   }
   positions=new List<float3>();indices=new List<int>();uv=new List<float2>();
   foreach(var v in result){indices.Add(positions.Count);positions.Add(v.World);uv.Add(v.UV);}
  }
 }
}
