using Colossal.Mathematics;using Unity.Mathematics;
namespace RoadStyleIntoKorea { internal static class CenterLinePolicy {
internal static string Asset(int style)=>style==3?"CSKRCenterYellowSolid6mNetLane":style==4?"CSKRCenterYellowDouble6mNetLane":style==2?"CSKREdgeWhiteSolid6mNetLane":"";
internal static float HalfWidth(int style)=>style==4?.16f:.08f;
internal static float RoadCenter(float min,float max,float width)=>math.saturate(.5f+(min+max)*.5f/math.max(.001f,width));
internal static int Next(int value)=>value==4?3:value==3?2:value==2?1:4;
internal static Bezier4x3 MedianSide(Bezier4x3 lane,float width,float half)=>DrawnLinePolicy.OffsetFollowing(lane,math.max(0,width*.5f-half-.05f));
internal static bool FirstFromMedian(float position,float median,System.Collections.Generic.IEnumerable<float> lanes){
foreach(float other in lanes)if((position-median)*(other-median)>0&&math.abs(other-median)<math.abs(position-median)-.05f)return false;
return true;
}
internal static bool Select(bool divided,bool both,bool selectedLeft,bool pathLeft)=>!divided||both||selectedLeft==pathLeft;
}}
