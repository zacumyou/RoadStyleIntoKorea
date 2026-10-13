namespace RoadStyleIntoKorea {
 internal static class PaintRecoveryPolicy {
  internal static bool EnteredView(bool known,bool previous,bool visible)=>visible&&(!known||!previous);
  internal static bool EmptyVisibleBatch(bool passedCulling,int batchCount)=>passedCulling&&batchCount==0;
  internal static bool Reveal(bool live,bool hidden,bool removed,bool suppressed,bool ownedHidden,bool ownerHidden)
   =>live&&hidden&&!removed&&!suppressed&&!ownedHidden&&!ownerHidden;
  // Paving and curbs do not change the guide's lane/crosswalk boundaries.
  internal static bool GuideDependency(int mask)=>(mask&(1|2|4|16|64))!=0;
 }
}
