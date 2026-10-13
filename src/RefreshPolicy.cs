using System;
using System.Collections.Generic;
using Colossal.Mathematics;
using Game.Net;
using Unity.Entities;
using Unity.Mathematics;
namespace RoadStyleIntoKorea {internal static class RefreshPolicy {
 internal const CarLaneFlags VisualFlags=CarLaneFlags.PublicOnly|CarLaneFlags.Forward|CarLaneFlags.TurnLeft|CarLaneFlags.TurnRight|CarLaneFlags.GentleTurnLeft|CarLaneFlags.GentleTurnRight|CarLaneFlags.UTurnLeft|CarLaneFlags.UTurnRight|CarLaneFlags.Forbidden|CarLaneFlags.SideConnection|CarLaneFlags.Twoway|CarLaneFlags.LeftLimit|CarLaneFlags.RightLimit;
 internal static HashSet<T> DirectScope<T>(IEnumerable<T> roots,Func<T,IEnumerable<T>> neighbors){var seeds=new List<T>(roots);var result=new HashSet<T>(seeds);foreach(var seed in seeds)foreach(var neighbor in neighbors(seed))result.Add(neighbor);return result;}
 internal static bool LanesChanged(List<(Bezier4x3 Curve,Entity Prefab,float Width)> previous,List<(Bezier4x3 Curve,Entity Prefab,float Width)> current)=>!SameLanes(previous,current);
 internal static bool SameLanes(List<(Bezier4x3 Curve,Entity Prefab,float Width)> previous,List<(Bezier4x3 Curve,Entity Prefab,float Width)> current){
  if(previous.Count!=current.Count)return false;
  // Native lane buffers usually keep their order. Avoid the quadratic matching
  // and allocation for the overwhelmingly common unchanged-order case.
  bool ordered=true;for(int i=0;i<previous.Count;i++){var a=previous[i];var b=current[i];if(a.Prefab!=b.Prefab||!(math.abs(a.Width-b.Width)<.001f)||!Geometry.SameCurve(a.Curve,b.Curve)){ordered=false;break;}}
  if(ordered)return true;
  var matched=new bool[current.Count];
  foreach(var a in previous){bool found=false;for(int i=0;i<current.Count;i++){var b=current[i];if(!matched[i]&&a.Prefab==b.Prefab&&math.abs(a.Width-b.Width)<.001f&&Geometry.SameCurve(a.Curve,b.Curve)){matched[i]=true;found=true;break;}}if(!found)return false;}return true;
 }
}}
