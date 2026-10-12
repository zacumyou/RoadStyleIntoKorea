using System.Collections.Generic;
using Colossal.Mathematics;
using Unity.Entities;
using Unity.Mathematics;
namespace RoadStyleIntoKorea {public partial class StyleSystem {
 List<Bounds1> CrosswalkSurfaceCuts(Entity lane,Bezier4x3 curve,List<Bounds1> cuts,float width){
  var road=CrossingRoad(lane,out _);if(road==Entity.Null)return cuts;
  return CrosswalkSurfacePolicy.Cuts(curve,cuts,width,EdgeSidewalks(road));
 }
}}
