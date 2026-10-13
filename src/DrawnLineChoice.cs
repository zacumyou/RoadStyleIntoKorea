using Colossal.Mathematics;
using Colossal.Serialization.Entities;
using Unity.Entities;
using Unity.Mathematics;
namespace RoadStyleIntoKorea {
 public struct DrawnLineChoice:IBufferElementData,ISerializable {
  public int Style;
  public long Group;
  public float Width,Phase;
  public Bezier4x3 Local;
  public void Serialize<T>(T w) where T:IWriter {w.Write(3);w.Write(Style);w.Write(Local.a);w.Write(Local.b);w.Write(Local.c);w.Write(Local.d);w.Write(Group);w.Write(Width);w.Write(Phase);}
  public void Deserialize<T>(T r) where T:IReader {
   r.Read(out int v);r.Read(out Style);r.Read(out Local.a);r.Read(out Local.b);r.Read(out Local.c);r.Read(out Local.d);
   Group=0;Width=Phase=0;
   if(v==2||v==3)r.Read(out Group);
   if(v==3){r.Read(out Width);r.Read(out Phase);}
   bool invalidPaving=RedPavingPolicy.IsRed(Style)&&(!math.isfinite(Width)||Width<=0||Width>32||!math.isfinite(Phase)||math.abs(Phase)>1000000);
   if(v<1||v>3||Style<0||Style>=RedPavingPolicy.StyleCount||invalidPaving||!math.all(math.isfinite(Local.a))||!math.all(math.isfinite(Local.b))||!math.all(math.isfinite(Local.c))||!math.all(math.isfinite(Local.d)))Style=-1;
  }
 }
 internal static class DrawnLinePolicy {
  internal static readonly int[] ColorOrder={0,1,2,6,3,4,5,7};
  internal const int LineCount=16;internal const int ColorCount=8;internal static bool IsColor(int style)=>style>=LineCount&&style<LineCount+ColorCount;
  internal static readonly string[] Assets={"CSKREdgeWhiteSolid6mNetLane","CSKREdgeYellowSolid6mNetLane","CSKREdgeYellowDouble6mNetLane","CSKREdgeYellowZigzag6mNetLane","CSKREdgeWhiteDotted6mNetLane","CSKREdgeYellowDotted6mNetLane","CSKREdgeYellowDoubleDotted6mNetLane","CSKRCenterYellowSolid6mNetLane","CSKRCenterYellowDouble6mNetLane","CSKRHighwayWhiteRightToLeft5mNetLane","CSKRHighwayWhiteLeftToRight5mNetLane","CSKRBusBlueSolid6mNetLane","CSKRBusBlueDashed6mNetLane","CSKRBusBlueRightToLeft6mNetLane","CSKRBusBlueLeftToRight6mNetLane","CSKRUTurnZone8mNetLane","CSKRColorGuidePinkChevronNetLane","CSKRColorGuideLightGreenChevronNetLane","CSKRColorGuideGreenChevronNetLane","CSKRColorGuidePinkJunctionNetLane","CSKRColorGuideLightGreenJunctionNetLane","CSKRColorGuideGreenJunctionNetLane","CSKRColorGuideBlueChevronNetLane","CSKRColorGuideBlueJunctionNetLane"};
  internal static bool ObjectBlocked(float3 local,float3 min,float3 max)=>math.abs(local.y)<1&&local.x>=min.x-.25f&&local.x<=max.x+.25f&&local.z>=min.z-.5f&&local.z<=max.z+.5f;
  // Keep each native decal projection short. Full texture-period boundaries
  // preserve dash spacing while avoiding a single large curved projection box.
  internal static System.Collections.Generic.IEnumerable<Bezier4x3> RenderPieces(Bezier4x3 curve,float period){
   var arc=new Geometry.Arc(curve);period=math.isfinite(period)&&period>.1f?period:6;
   float step=period*math.max(1,math.ceil(arc.Length/(period*128)));
   for(float from=0;from<arc.Length-.001f;from+=step)yield return arc.Slice(from,math.min(from+step,arc.Length));
  }
  // Fit Y through sampled points, never alter the authored horizontal curve.
  internal static Bezier4x3 FollowSurface(Bezier4x3 c,System.Func<float3,float?> height){
   var a=height(c.a);var d=height(c.d);var p=height(Geometry.Position(c,1f/3));var q=height(Geometry.Position(c,2f/3));
   if(!a.HasValue||!d.HasValue||!p.HasValue||!q.HasValue)return c;
   float x=27*p.Value-8*a.Value-d.Value,y=27*q.Value-a.Value-8*d.Value;
   c.a.y=a.Value;c.d.y=d.Value;c.b.y=(2*x-y)/18;c.c.y=(2*y-x)/18;return c;
  }
  internal static Bezier4x3 Bend(Bezier4x3 c,float3 midpoint){var delta=(midpoint-Geometry.Position(c,.5f))*(4f/3f);return new Bezier4x3(c.a,c.b+delta,c.c+delta,c.d);}
  internal static Bezier4x3 SmoothConnect(float3 a,float3 d,float3 first,float3 last){
   var chord=d-a;float length=math.length(chord);var direction=math.normalizesafe(chord);
   first=math.normalizesafe(first,direction);last=math.normalizesafe(last,direction);
   if(math.dot(first,direction)<0)first=-first;if(math.dot(last,direction)<0)last=-last;
   if(math.dot(first,direction)<.35f)first=direction;if(math.dot(last,direction)<.35f)last=direction;
   return new Bezier4x3(a,a+first*(length/3),d-last*(length/3),d);
  }
  internal static Bezier4x3 AlignStart(Bezier4x3 curve,float3 tangent){
   var handle=curve.b-curve.a;var direction=math.normalizesafe(tangent,math.normalizesafe(handle));
   if(math.dot(direction,handle)<0)direction=-direction;
   curve.b=curve.a+direction*math.length(handle);return curve;
  }
  internal static Bezier4x3 Connect(float3 a,float3 d,float3 first,float3 last){
   var chord=d-a;float handle=math.distance(a,d)/3;
   if(math.dot(first,chord)<0)first=-first;if(math.dot(last,chord)<0)last=-last;
   return new Bezier4x3(a,a+math.normalizesafe(first,math.normalizesafe(chord))*handle,d-math.normalizesafe(last,math.normalizesafe(chord))*handle,d);
  }
 }
}
