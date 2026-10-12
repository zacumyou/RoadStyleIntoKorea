using System;
namespace RoadStyleIntoKorea {
 internal static class RoadSignPolicy {
  // Persisted IDs: append entries; never reorder these names.
  internal static readonly string[] Text={"BusOnly","Children","CityBridge","Entrance","Exit","Expressway","NoEntry","OneWay","ProtectionZone","Slow","UnprotectedTurn"};
  internal static int Kmh(float prefabSpeed)=>float.IsNaN(prefabSpeed)||float.IsInfinity(prefabSpeed)||prefabSpeed<=0?0:(int)Math.Round(prefabSpeed/2f);
  internal static int LaneSize(int group,float width)=>group==1?3:group==2?4:width>=3.5f?4:3;
  internal static string Asset(int kind,int symbol,float width,int kmh){
   int count=kind==5?13:Text.Length,group=symbol/count,index=symbol%count;
   if(symbol<0||group>2)return "";
   string token;
   if(kind==5){int speed=index==0?kmh:index*10;if(speed<10||speed>120||speed%10!=0)return "";token="Speed"+speed;}
   else if(kind==6)token=Text[index];else return "";
   return "CSKRRoadSign"+token+"Lane"+LaneSize(group,width)+"mDecal";
  }
 }
}
