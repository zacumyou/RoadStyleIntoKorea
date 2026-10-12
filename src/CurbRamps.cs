using Unity.Entities;
namespace RoadStyleIntoKorea {public partial class StyleSystem {
 CurbRampPolicy CurbRamps(Entity owner){
  var ramps=new CurbRampPolicy();
  foreach(var crossing in NearbyCrossings(owner))ramps.Add(CrossingSpan(crossing),CrossingWidth(crossing));
  var connections=Driveways(owner);
  if(connections!=null)foreach(var connection in connections)ramps.AddEntrance(connection.Curve,Unity.Mathematics.math.cmax(connection.Width));
  return ramps;
 }
}}
