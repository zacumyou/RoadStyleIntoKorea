using Colossal.Mathematics;using Unity.Mathematics;
namespace RoadStyleIntoKorea {internal static class TrafficCurvePolicy {
 internal static bool Valid(Bezier4x3 curve,float length)=>math.all(math.isfinite(curve.a))&&math.all(math.isfinite(curve.b))&&math.all(math.isfinite(curve.c))&&math.all(math.isfinite(curve.d))&&math.isfinite(length)&&length>.001f;
}}
