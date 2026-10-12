using Game.Pathfind;
namespace RoadStyleIntoKorea {
 internal static class ColorGuideNetworkPolicy {
  internal static bool Connected(PathNode end,PathNode start)=>end.GetOwnerIndex()!=0&&end.Equals(start);
  internal static bool Prefer(bool candidatePreferred,bool bestPreferred,float distance,float bestDistance)=>
   candidatePreferred!=bestPreferred?candidatePreferred:distance<bestDistance;
 }
}
