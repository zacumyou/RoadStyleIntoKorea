using Colossal.Mathematics;
using Unity.Mathematics;
namespace RoadStyleIntoKorea
{
    internal static class MergePolicy
    {
        // Reversing both displacement and heading preserves the lateral sign.
        internal static int DividerDirection(int nearCount,int farCount,bool shared,Bezier4x3 curve,bool backwards)
            =>Direction(nearCount,farCount,shared,backwards?Geometry.Reverse(curve):curve);
        internal static int Direction(int incoming,int outgoing,bool sharedTarget,Bezier4x3 curve)
        {
            if(incoming<=outgoing||outgoing<1||!sharedTarget)return 0;
            var start=Geometry.Tangent(curve,0);var end=Geometry.Tangent(curve,1);
            if(math.dot(start,end)<.9f)return 0;
            // A verified lane drop requires a merge arrow even on a shallow taper.
            float side=math.dot(curve.d-curve.a,Geometry.Left(math.normalizesafe(start+end)));
            return math.abs(side)<.75f?0:side>0?1:-1;
        }
    }
}

