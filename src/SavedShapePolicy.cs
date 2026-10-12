using System.Collections.Generic;
using Colossal.Mathematics;
using Unity.Entities;
using Unity.Mathematics;
namespace RoadStyleIntoKorea {internal static class SavedShapePolicy {
 internal static bool IsPrefabKind(int kind)=>kind==0||kind==3;
 internal static bool Matches(List<SavedStyleShape> saved,Entity prefab,float3 position,IEnumerable<Entity> connections,bool hasCurve,Bezier4x3 curve,out string reason){
  reason="";if(saved.Count==0){reason="missing-shape";return false;}
  if(saved[0].Item!=prefab){reason="road-prefab";return false;}
  if(math.distancesq(saved[0].Curve.a,position)>.0001f){reason="node-position";return false;}
  var oldConnections=new HashSet<Entity>();bool found=false;
  foreach(var item in saved){
   if(item.Kind==1)oldConnections.Add(item.Item);
   // EdgeGeometry and native lane buffers are derived during loading. They are
   // runtime edit stamps, not stable save identity. The first edge curve is stable.
   if(item.Kind==2&&hasCurve&&!found){found=true;if(!Geometry.SameCurve(item.Curve,curve)){reason="road-curve";return false;}}
  }
  if(hasCurve&&!found){reason="missing-road-curve";return false;}
  if(!oldConnections.SetEquals(connections)){reason="connections";return false;}
  return true;
 }
}}
