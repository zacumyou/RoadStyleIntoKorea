namespace RoadStyleIntoKorea {internal static class ApplyAllPolicy {
 internal const double FrameMilliseconds=12;
 internal static double Budget(bool paused)=>paused?60:FrameMilliseconds;
 internal static bool Includes(int scope,bool highway)=>scope==0||(scope==1?!highway:highway);
 internal const int StageLimit=128,OwnerLimit=64,AdmissionLimit=32;
 // Four standard 3m motor lanes; parking, cycle lanes and sidewalks do not count.
 internal static int Paving(int lanes,float motorWidth)=>lanes>=4||motorWidth>=12f-.01f?1:2;
 internal static int Crosswalk(int requested,int pavingStyle)=>requested==0?0:pavingStyle==2?CrosswalkStylePolicy.SingleThree:requested;
 internal static bool SpeedSigns(Unity.Mathematics.float3 center){
  unchecked {uint h=Unity.Mathematics.math.hash((Unity.Mathematics.int2)Unity.Mathematics.math.round(center.xz*10));h^=h>>16;h*=0x7feb352du;h^=h>>15;h*=0x846ca68bu;h^=h>>16;return(h&3u)==0;}
 }
}}
