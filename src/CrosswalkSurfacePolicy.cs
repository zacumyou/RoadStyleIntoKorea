using System.Collections.Generic;
using Colossal.Mathematics;
using Unity.Mathematics;
namespace RoadStyleIntoKorea {internal static class CrosswalkSurfacePolicy {
 internal static List<Bounds1> Cuts(Bezier4x3 curve,List<Bounds1> cuts,float width,SidewalkFootprint sidewalk){
  var arc=new Geometry.Arc(curve);
  int count=math.clamp((int)math.ceil(arc.Length/.2f),1,512);float begin=-1;
  for(int i=0;i<count;i++){
   float a=i/(float)count,b=(i+1)/(float)count;bool blocked=false;
   foreach(float t in new[]{a,(a+b)*.5f,b}){
    var p=Geometry.Position(curve,t);var across=Geometry.Left(Geometry.Tangent(curve,t));
    int steps=math.max(1,(int)math.ceil(width/.25f));
    for(int j=0;j<=steps&&!blocked;j++)blocked|=sidewalk.Overlaps(p+across*math.lerp(-width*.5f,width*.5f,j/(float)steps),.14f);
   }
   if(blocked&&begin<0)begin=a;
   if(!blocked&&begin>=0){cuts.Add(new Bounds1(begin,a));begin=-1;}
  }
  if(begin>=0)cuts.Add(new Bounds1(begin,1));
  return MarkingPolicy.CrosswalkCuts(cuts,new float2(1),1);
 }
}}
