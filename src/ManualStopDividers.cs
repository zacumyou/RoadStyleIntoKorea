using System.Collections.Generic;
using Game.Net;
using Game.Prefabs;
using Game.Tools;
using Unity.Entities;
using Unity.Mathematics;
namespace RoadStyleIntoKorea {public partial class StyleSystem {
 void ManualStopDividers(Entity road,Record record,List<ManualDecalChoice> choices){
  if(!choices.Exists(c=>c.Kind==1))return;
  var lanes=Lanes(road);var drives=Drives(lanes);var candidates=new HashSet<Entity>(lanes);
  if(records.TryGetValue(road,out var main))foreach(var e in main.Paint)candidates.Add(e);
  if(approachStopRecords.TryGetValue(road,out var approach))foreach(var e in approach.Paint)candidates.Add(e);
  var ends=EntityManager.GetComponentData<Edge>(road);
  foreach(var node in new[]{ends.m_Start,ends.m_End})if(guideRecords.TryGetValue(node,out var guide))foreach(var e in guide.Paint)candidates.Add(e);
  foreach(var lane in candidates){
   if(!Live(lane)||hiddenOwners.Contains(lane)||EntityManager.HasComponent<Hidden>(lane)||!EntityManager.HasComponent<Curve>(lane)||!MarkingPolicy.IsLaneDivider(assets.Name(Prefab(lane)))||!EntityManager.HasComponent<NetLaneGeometryData>(Prefab(lane)))continue;
   var arc=new Geometry.Arc(CurveOf(lane));if(arc.Length<.05f)continue;var ranges=new List<float2>();
   foreach(var choice in choices){if(choice.Kind!=1)continue;ManualPose(road,choice,out var stop,out var forward);
    float t=arc.Nearest(stop);if(math.abs(math.dot(Geometry.Tangent(arc.Curve,t),forward))<.8f)continue;var point=Geometry.Position(arc.Curve,t);float lateral=math.dot(point-stop,Geometry.Left(forward));
    if(math.abs(lateral)>choice.Width*.5f+.65f||math.abs(point.y-stop.y)>1)continue;
    bool neighbor=false;
    foreach(var d in drives){d.Arc.Nearest(point,out var dp,out var df);
     if(math.dot(df,forward)>.8f&&math.dot((dp-point).xz,(stop-point).xz)<0&&math.distance(dp.xz,point.xz)<d.Width*.5f+.7f){neighbor=true;break;}}
    if(neighbor)ranges.AddRange(ManualStopPolicy.Ranges(arc,stop,forward));
   }
   ranges=ManualStopPolicy.Merge(ranges);if(ranges.Count==0)continue;
   float cursor=0;foreach(var range in ranges){if(range.x>cursor+.000001f)PaintDivider(road,record,Prefab(lane),Geometry.Cut(arc.Curve,cursor,range.x));
    PaintDivider(road,record,assets.Get(AssetCatalog.White),Geometry.Cut(arc.Curve,range.x,range.y));cursor=range.y;}
   if(cursor<.999999f)PaintDivider(road,record,Prefab(lane),Geometry.Cut(arc.Curve,cursor,1));Hide(lane,record);
  }
 }
}}
