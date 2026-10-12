using Colossal.Mathematics;using Unity.Mathematics;
namespace RoadStyleIntoKorea {internal static class UTurnRoutePolicy {
 internal static bool AllowCreate(bool occupied,int stable)=>!occupied&&stable>=2;
 internal static bool SameLinks(Game.Pathfind.PathNode start,Game.Pathfind.PathNode end,Game.Pathfind.PathNode source,Game.Pathfind.PathNode target)=>start.EqualsIgnoreCurvePos(source)&&end.EqualsIgnoreCurvePos(target);
 internal static float ClampLength(float length)=>math.isfinite(length)?math.clamp(math.round(length),4,24):8;
 internal static bool TryCurve(float3 start,float3 end,float3 forward,float length,out Bezier4x3 curve){
  float width=math.distance(start.xz,end.xz);float handle=width*.67f;
  curve=new Bezier4x3(start,start+forward*handle,end+forward*handle,end);
  return width>1&&width<15&&math.dot(end-start,Geometry.Left(forward))>1&&handle*.75f+.5f<=ClampLength(length)*.5f&&math.abs(start.y-end.y)<1.5f;
 }
}}
