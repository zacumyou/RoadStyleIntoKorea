using System.Collections.Generic;using Colossal.Mathematics;using Unity.Mathematics;
namespace RoadStyleIntoKorea { internal static class BayEdgePolicy {
internal static Bezier4x3 Inside(Bezier4x3 curb,bool left,float half)=>Geometry.Offset(curb,(left?-1:1)*(half+.12f));
internal static List<Bezier4x3> Merge(List<Bezier4x3> source){var result=new List<Bezier4x3>();foreach(var c in source){if(result.Count>0){var p=result[result.Count-1];if(math.distance(p.d,c.a)<.02f&&math.dot(Geometry.Tangent(p,1),Geometry.Tangent(c,0))>.99999f){result[result.Count-1]=Geometry.Line(p.a,c.d);continue;}}result.Add(c);}return result;}
}}