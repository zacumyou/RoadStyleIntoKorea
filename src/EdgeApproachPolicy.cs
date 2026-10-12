using System.Collections.Generic;
using Colossal.Mathematics;
using Unity.Mathematics;
namespace RoadStyleIntoKorea
{
    internal static class EdgeApproachPolicy
    {
        internal struct Limit { internal float3 Origin,Inward; }
        // The interior is before every approach boundary. Keep only exterior
        // fragments; no detached line islands between the crossings.
        internal static IEnumerable<Bezier4x3> OutsideJunction(Bezier4x3 curve,List<Limit> limits){
            if(limits.Count==0){yield return curve;yield break;}
            foreach(var part in Geometry.ClearSlices(curve,p=>{
                foreach(var limit in limits)if(math.dot(p-limit.Origin,limit.Inward)>=0)return true;
                return false;
            }))yield return part;
        }
        internal static bool Conflicting(List<Limit> limits){
            for(int i=0;i<limits.Count;i++)for(int j=i+1;j<limits.Count;j++)
                if(math.dot(limits[i].Inward,limits[j].Inward)<-.95f&&math.dot(limits[j].Origin-limits[i].Origin,limits[i].Inward)<=0)return true;
            return false;
        }
        // Control-point convex hull is deliberately conservative: the entire
        // corner, not only a guessed sidewalk piece, stays outside the paint.
        internal static float Extent(Bezier4x3 curve,float3 node,float3 inward)
            =>math.max(math.max(math.dot(curve.a-node,inward),math.dot(curve.b-node,inward)),
                       math.max(math.dot(curve.c-node,inward),math.dot(curve.d-node,inward)));
        internal static IEnumerable<Bezier4x3> Clip(Bezier4x3 curve,List<Limit> limits)
        {
            if(limits.Count==0){yield return curve;yield break;}
            foreach(var part in Geometry.ClearSlices(curve,p=>
            {foreach(var limit in limits)if(math.dot(p-limit.Origin,limit.Inward)<0)return false;return true;}))yield return part;
        }
    }
}
