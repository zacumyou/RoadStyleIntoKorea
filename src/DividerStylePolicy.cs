namespace RoadStyleIntoKorea {
 internal static class DividerStylePolicy {
  // Persisted style IDs. Zero follows the automatic rule.
  internal static readonly string[] Assets={"","CSKRHighwayWhiteRightToLeft5mNetLane","CSKRHighwayWhiteLeftToRight5mNetLane","CSKRBusBlueSolid6mNetLane","CSKRBusBlueDashed6mNetLane","CSKRBusBlueRightToLeft6mNetLane","CSKRBusBlueLeftToRight6mNetLane"};
  internal static int Default(bool highway,bool leftBus,bool rightBus,int leftMerge,int rightMerge){
   if(leftBus!=rightBus)return 3;
   if(!highway)return 0;
   if(rightMerge!=0&&leftMerge==0)return rightMerge>0?1:2;
   if(leftMerge!=0&&rightMerge==0)return leftMerge>0?1:2;
   return 0;
  }
  internal static int Next(int group,int value)=>group==1?(value>=2?0:value+1):(value==0?3:value>=6?0:value+1);
 }
}
