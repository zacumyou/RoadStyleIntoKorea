using Unity.Mathematics;
namespace RoadStyleIntoKorea {
internal static class NarrowPolicy {
 internal static System.Collections.Generic.List<string> Symbols(Game.Prefabs.LaneDirectionData direction,Game.Net.CarLaneFlags turns){
  if(direction.m_Left==Game.Prefabs.LaneDirectionType.UTurn)direction.m_Left=Game.Prefabs.LaneDirectionType.None;
  if(direction.m_Right==Game.Prefabs.LaneDirectionType.UTurn)direction.m_Right=Game.Prefabs.LaneDirectionType.None;
  return ArrowPolicy.RouteSymbols(direction,turns & ~(Game.Net.CarLaneFlags.UTurnLeft|Game.Net.CarLaneFlags.UTurnRight));
 }
 internal static bool Narrow(int count)=>count>0&&count<=2;
 internal static float StopGap(int count,float gap)=>Narrow(count)?math.min(1,gap):gap;
 internal static float ArrowGap(int count,float gap)=>Narrow(count)?math.min(4,gap):gap;
}
}
