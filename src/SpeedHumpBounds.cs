using System;
using System.Collections.Generic;
using Game.Net;
using Unity.Entities;
using Unity.Mathematics;
namespace RoadStyleIntoKorea {
 public partial class StyleSystem {
  Entity humpBoundsRoad;int humpBoundsRevision=-1;
  readonly List<Geometry.Route> humpBounds=new List<Geometry.Route>();
  void CacheHumpBoundaries(Entity road){
   if(humpBoundsRoad==road&&humpBoundsRevision==toolRevision)return;
   humpBoundsRoad=road;humpBoundsRevision=toolRevision;humpBounds.Clear();
   var lanes=Lanes(road);var boundaries=new List<EdgeBoundary>();
   if(S.EdgeLines)Edges(road,null,lanes,manualPreviewDrives,boundaries);
   foreach(var boundary in boundaries)if(boundary.Style!=EdgeStyle.None)humpBounds.Add(new Geometry.Route(boundary.Curve));
   var edgeChoice=EntityManager.HasComponent<EdgeChoice>(road)?EntityManager.GetComponentData<EdgeChoice>(road):default;
   foreach(var lane in lanes){
    if(!EntityManager.HasComponent<SecondaryLane>(lane)||!EntityManager.HasComponent<Curve>(lane)||assets.Name(Prefab(lane)).IndexOf("Edge Line",StringComparison.OrdinalIgnoreCase)<0)continue;
    var curve=CurveOf(lane);var p=Geometry.Position(curve,.5f);float t=manualPreviewCenter.Nearest(p);
    bool left=math.dot(p-Geometry.Position(manualPreviewCenter.Curve,t),Geometry.Left(Geometry.Tangent(manualPreviewCenter.Curve,t)))>=0;
    int code=left?edgeChoice.Left:edgeChoice.Right;var style=code==0?(active?S.DefaultEdge:EdgeStyle.Original):(EdgeStyle)math.clamp(code-1,0,5);
    if(!S.EdgeLines||style==EdgeStyle.Original)humpBounds.Add(new Geometry.Route(curve));
   }
   foreach(var path in StyledCenterPaths(road,lanes))if(path.Code!=2)humpBounds.Add(new Geometry.Route(path.Curve));
  }
  ManualDecalChoice FitHumpBoundaries(Entity road,ManualDecalChoice bar,List<ManualDecalChoice> lanes){
   CacheHumpBoundaries(road);
   var origin=Geometry.Position(manualPreviewCenter.Curve,bar.T);var forward=Geometry.Tangent(manualPreviewCenter.Curve,bar.T);var across=Geometry.Left(forward);
   float minCenter=float.MaxValue,maxCenter=float.MinValue;
   foreach(var lane in lanes){minCenter=math.min(minCenter,lane.Offset);maxCenter=math.max(maxCenter,lane.Offset);}
   float lo=bar.Offset-bar.Width*.5f,hi=bar.Offset+bar.Width*.5f,loError=float.MaxValue,hiError=float.MaxValue;
   float originalLo=lo,originalHi=hi;
   foreach(var boundary in humpBounds){
    var normal=math.dot(boundary.Direction(boundary.Length*.5f),forward)<0?-forward:forward;
    if(!boundary.IntersectPlane(origin,normal,out float at))continue;
    var point=boundary.At(at);if(math.abs(point.y-origin.y-bar.Height)>2)continue;
    float offset=math.dot(point-origin,across);
    // Only outer boundaries qualify; a center line inside a Shift span cannot split it.
    if(offset<minCenter&&math.abs(offset-originalLo)<loError){lo=offset;loError=math.abs(offset-originalLo);}
    if(offset>maxCenter&&math.abs(offset-originalHi)<hiError){hi=offset;hiError=math.abs(offset-originalHi);}
   }
   if(hi>lo&&hi-lo<=128){bar.Offset=(lo+hi)*.5f;bar.Width=hi-lo;}return bar;
  }
 }
}

