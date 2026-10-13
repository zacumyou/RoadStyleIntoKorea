using Unity.Mathematics;namespace RoadStyleIntoKorea {internal static class AutomaticUTurnPolicy {
 internal const float CrosswalkGap=3f;
 internal static bool CenterDistance(float roadLength,float zoneLength,float setback,bool start,out float at){
  float near=math.max(0,setback)+UTurnPolicy.Reserve;
  at=start?near+zoneLength*.5f:roadLength-near-zoneLength*.5f;
  return zoneLength>0&&near+zoneLength+UTurnPolicy.Reserve<=roadLength;
 }
}}

