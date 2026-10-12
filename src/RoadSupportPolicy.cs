using Game.Net;
namespace RoadStyleIntoKorea {
internal static class RoadSupportPolicy {
 // Highway is a traffic-rule flag, not an exclusion from cosmetic road tools.
 internal static bool Drive(CarLaneFlags flags,bool includeShared=false)=>(flags&(CarLaneFlags.Forbidden|CarLaneFlags.SideConnection|(includeShared?0:CarLaneFlags.Twoway)))==0;
 internal static bool Motor(RoadTypes types)=>(types&RoadTypes.Car)!=0;
 internal static bool Center(bool divided,bool forward,bool backward)=>divided||(forward&&backward);
 internal static bool JunctionPaint(bool highway,bool hasStopOrCrossing)=>!highway||hasStopOrCrossing;
}
}
