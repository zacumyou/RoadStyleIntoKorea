using Colossal.Mathematics;
using Unity.Mathematics;
namespace RoadStyleIntoKorea
{
    internal static class CurbLaneGeometry
    {
        static float3 Center(float3 outside,float3 inside,float inset)
        {float width=math.distance(outside.xz,inside.xz);return math.lerp(outside,inside,math.min(1,inset/math.max(.0001f,width)));}
        internal static Bezier4x3 Curve(Bezier4x3 a,Bezier4x3 b,bool fromA,float inset=.51f)
        {
            var outside=fromA?a:b;var inside=fromA?b:a;
            var curve=new Bezier4x3(Center(outside.a,inside.a,inset),Center(outside.b,inside.b,inset),Center(outside.c,inside.c,inset),Center(outside.d,inside.d,inset));
            var inward=Geometry.Position(inside,.5f)-Geometry.Position(outside,.5f);
            // The native module has the raised top on local -X, i.e. left of travel.
            if(math.dot(inward,Geometry.Left(Geometry.Tangent(curve,.5f)))<0)curve=Geometry.Reverse(curve);
            return curve;
        }
    }
}
