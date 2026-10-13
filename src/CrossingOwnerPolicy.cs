using Unity.Mathematics;
namespace RoadStyleIntoKorea {internal static class CrossingOwnerPolicy {
 internal static bool Accept(int connections,bool nodeOwned,float alignment,float approachDistance)
  =>nodeOwned&&connections==2||math.abs(alignment)<=.6f&&(!nodeOwned||approachDistance>.5f);
}}
