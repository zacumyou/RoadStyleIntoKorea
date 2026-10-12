using System.Collections.Generic;
using Game.Net;
using Unity.Entities;
using Unity.Mathematics;
namespace RoadStyleIntoKorea {public partial class StyleSystem {
 void HideNearbyAutomaticStops(Entity road,Record record,List<ManualDecalChoice> choices){
  if(!choices.Exists(c=>c.Kind==1))return;
  var ends=EntityManager.GetComponentData<Edge>(road);
  var candidates=new HashSet<Entity>();
  foreach(var owner in new[]{road,ends.m_Start,ends.m_End}){
   foreach(var lane in Lanes(owner))candidates.Add(lane);
   foreach(var map in new[]{records,approachRecords,approachStopRecords})
    if(map.TryGetValue(owner,out var r))foreach(var lane in r.Paint)candidates.Add(lane);
  }
  foreach(var lane in candidates){
   if(!Live(lane)||hiddenOwners.Contains(lane)||EntityManager.HasComponent<Game.Tools.Hidden>(lane)||!EntityManager.HasComponent<Curve>(lane))continue;
   if(assets.Name(Prefab(lane)).IndexOf("Stop Line",System.StringComparison.OrdinalIgnoreCase)<0)continue;
   var curve=CurveOf(lane);var cuts=new List<float2>();
   foreach(var choice in choices){
    if(choice.Kind!=1)continue;ManualPose(road,choice,out var stop,out var forward);
    if(ManualStopPolicy.Overlap(curve,stop,forward,choice.Width,out var interval))cuts.Add(interval);
   }
   cuts=ManualStopPolicy.Merge(cuts);if(cuts.Count==0)continue;
   float cursor=0;
   foreach(var cut in cuts){if(cut.x>cursor+.001f)PaintLane(road,record,Prefab(lane),Geometry.Cut(curve,cursor,cut.x));cursor=cut.y;}
   if(cursor<.999f)PaintLane(road,record,Prefab(lane),Geometry.Cut(curve,cursor,1));
   Hide(lane,record);
  }
 }
}}
