using System.Collections.Generic;
using Colossal.Mathematics;
using Unity.Entities;
using Unity.Mathematics;
namespace RoadStyleIntoKorea {public partial class StyleSystem {
 DrawnLineChoice[] SnapshotDrawnLines(Entity road,Bezier4x3 source){
  var choices=ChoiceBuffer<DrawnLineChoice>(road);if(choices==null)return null;
  LineFrame(road,out _,out _,out _,out float width);
  var forward=math.normalizesafe(new float3(source.d.x-source.a.x,0,source.d.z-source.a.z),new float3(0,0,1));
  float length=math.max(1,math.distance(source.a.xz,source.d.xz));var left=Geometry.Left(forward);
  System.Func<float3,float3> decode=p=>source.a+forward*(p.x*length)+new float3(0,p.y,0)+left*(p.z*width);
  for(int i=0;i<choices.Length;i++){var c=choices[i].Local;choices[i].Local=new Bezier4x3(decode(c.a),decode(c.b),decode(c.c),decode(c.d));}
  return choices;
 }
 bool InheritDrawnLines(Entity road,DrawnLineChoice[] world,bool replace=false){
  if(world==null||world.Length==0)return false;
  var result=new List<DrawnLineChoice>();
  foreach(var item in world){
   if(item.Style<0)continue;
   var local=LineTransform(road,item.Local,true);
   // Clip at new segment end planes. Sample only during topology edits.
   var cuts=new List<float>{0,1};
   foreach(float plane in new[]{0f,1f}){
    float last=local.a.x-plane;
    for(int i=1;i<=64;i++){float t=i/64f,current=Geometry.Position(local,t).x-plane;
     if((last<0&&current>=0)||(last>0&&current<=0)){
      float lo=(i-1)/64f,hi=t;bool negative=last<0;
      for(int j=0;j<20;j++){float mid=(lo+hi)*.5f;if((Geometry.Position(local,mid).x-plane<0)==negative)lo=mid;else hi=mid;}
      cuts.Add((lo+hi)*.5f);
     }last=current;
    }
   }
   cuts.Sort();for(int i=1;i<cuts.Count;i++){
    float a=cuts[i-1],b=cuts[i];if(b-a<.0001f)continue;float x=Geometry.Position(local,(a+b)*.5f).x;
    if(x<-.0001f||x>1.0001f)continue;var part=Geometry.Cut(local,a,b);
    result.Add(new DrawnLineChoice{Style=item.Style,Group=item.Group,Local=part,Width=item.Width,Phase=item.Phase+(RedPavingPolicy.IsRed(item.Style)?new Geometry.Arc(item.Local).Distance(a):0)});
   }
  }
  if(result.Count==0&&!replace)return false;
  var target=EntityManager.HasBuffer<DrawnLineChoice>(road)?EntityManager.GetBuffer<DrawnLineChoice>(road):EntityManager.AddBuffer<DrawnLineChoice>(road);
  if(replace)target.Clear();else if(target.Length>0)return false;
  foreach(var choice in result)target.Add(choice);manualDirty.Add(road);return true;
 }
}}
