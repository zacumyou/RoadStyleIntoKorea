using Unity.Mathematics;
namespace RoadStyleIntoKorea {internal static class LoadBudgetPolicy {
 internal static int AdmissionCount(int queued,float frameSeconds)=>math.min(math.max(0,12-queued),frameSeconds>.0334f?1:frameSeconds>.02f?2:4);
 internal static bool VisibleAdmission(int queued,float sinceEdit)=>queued<12&&sinceEdit>=.15f;
 internal static float FrameBudget(float frameSeconds)=>frameSeconds>.0334f?1f:frameSeconds>.02f?2f:3f;
 internal static float Delay(float distanceSquared,float frameSeconds){
  float delay=distanceSquared<=150*150?.02f:distanceSquared<=500*500?.15f:5f;
  return math.max(delay,frameSeconds>.05f?1f:frameSeconds>.0334f?.40f:frameSeconds>.025f?.10f:0);
 }
 internal static bool CanAdmit(float distanceSquared,float frameSeconds,float elapsed,float sinceEdit)=>
  sinceEdit>=2f&&(distanceSquared<=500*500||frameSeconds<=.0334f)&&elapsed>=Delay(distanceSquared,frameSeconds);
 internal static int First(int mask){int paint=mask&~(32|8|128);if(paint!=0)return paint&-paint;if((mask&32)!=0)return 32;return mask&-mask;}
}}

