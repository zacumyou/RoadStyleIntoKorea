using System.Collections.Generic;
using Colossal.Mathematics;
using Unity.Mathematics;
namespace RoadStyleIntoKorea {
 internal static class GutterClearance {
  // Evaluate only during placement/preview rebuild. A bounded inward search
  // uses the deformed raised-surface mask, never the painted edge-line position.
  internal static List<Bezier4x3> Fit(Bezier4x3 source,SidewalkFootprint mask,bool followBoundary=false){
   var arc=new Geometry.Arc(source);int n=math.clamp((int)math.ceil(arc.Length/.5f),1,1024);
   var shifts=new float[n+1];bool moved=false;
   float radius=GutterPolicy.Width*.5f+GutterPolicy.PavingGap+.015f;
   for(int i=0;i<=n;i++){
    float t=arc.T(arc.Length*i/n);var p=Geometry.Position(source,t);var inward=-Geometry.Left(Geometry.Tangent(source,t));
    float lo=0,hi=0;
    if(mask.Overlaps(p,radius)){
     hi=.05f;while(hi<=4&&mask.Overlaps(p+inward*hi,radius))hi+=.05f;
     lo=math.max(0,hi-.05f);
    }else{
     if(!followBoundary)continue;
     // Nominal piece bounds include the reserved curb band and do not follow
     // bay tapers. Find the actual raised edge instead of staying in a cycle lane.
     lo=-.05f;while(lo>=-4&&!mask.Overlaps(p+inward*lo,radius))lo-=.05f;
     if(lo< -4)continue;
     hi=lo+.05f;
    }
    for(int j=0;j<7;j++){float mid=(lo+hi)*.5f;if(mask.Overlaps(p+inward*mid,radius))lo=mid;else hi=mid;}
    shifts[i]=hi;moved=true;
   }
   if(!moved)return new List<Bezier4x3>{source};
   // Spread local offsets gently instead of making sharp steps at parking bays.
   float step=arc.Length/n*.25f;
   for(int i=1;i<=n;i++)shifts[i]=math.max(shifts[i],shifts[i-1]-step);
   for(int i=n-1;i>=0;i--)shifts[i]=math.max(shifts[i],shifts[i+1]-step);
   float3 At(float u){float x=u*n;int i=math.min((int)x,n-1);float t=arc.T(arc.Length*u);return Geometry.Position(source,t)-Geometry.Left(Geometry.Tangent(source,t))*math.lerp(shifts[i],shifts[i+1],x-i);}
   var result=new List<Bezier4x3>();
   void Split(float lo,float hi,int depth){
    var a=At(lo);var d=At(hi);var u=At(math.lerp(lo,hi,1f/3))-a;var v=At(math.lerp(lo,hi,2f/3))-a;var w=d-a;
    var c=new Bezier4x3(a,a+(18*u-9*v+2*w)/6,a+(-9*u+18*v-5*w)/6,d);
    bool clear=true;float error=0;int probes=math.max(6,(int)math.ceil(arc.Length*(hi-lo)/.25f));
    for(int j=0;j<=probes;j++){float t=(float)j/probes;var p=Geometry.Position(c,t);error=math.max(error,math.distance(p,At(math.lerp(lo,hi,t))));if(mask.Overlaps(p,GutterPolicy.Width*.5f+GutterPolicy.PavingGap))clear=false;}
    if((error>.005f||!clear)&&depth<7){float mid=(lo+hi)*.5f;Split(lo,mid,depth+1);Split(mid,hi,depth+1);}
    else if(clear)result.Add(c);
   }
   Split(0,1,0);return result;
  }
 }
}

