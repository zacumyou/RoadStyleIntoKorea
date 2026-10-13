using System.Collections.Generic;
using Colossal.Mathematics;
using Unity.Mathematics;

namespace RoadStyleIntoKorea
{
    internal static class StopCenterPolicy
    {
        // One metre from the centerline tip to the stop-bar axis leaves room
        // for the native stop bar footprint and a visible positive separation.
        internal const float Setback=1f;
        internal static IEnumerable<Bezier4x3> BeforeBar(Bezier4x3 curve,Bezier4x3 bar,float3 forward)
        {
            var across=math.normalizesafe(new float3(bar.d.x-bar.a.x,0,bar.d.z-bar.a.z));
            var normal=Geometry.Left(across);if(math.dot(normal,forward)<0)normal=-normal;
            var mid=Geometry.Position(bar,.5f);var arc=new Geometry.Arc(curve);
            var route=new Geometry.Route(curve);float at;var probe=route.IntersectPlane(mid,normal,out at)||route.IntersectPlane(mid,-normal,out at)?route.At(at):Geometry.Position(curve,arc.Nearest(mid));
            // Only the lanes spanned by this stop bar, never the opposing carriageway.
            if(math.abs(math.dot(Geometry.Tangent(curve,arc.Nearest(probe)),normal))<.75f||math.abs(math.dot(probe-mid,normal))>20||math.abs(probe.y-mid.y)>2||math.abs(math.dot(probe-mid,across))>math.distance(bar.a,bar.d)*.5f+.6f){yield return curve;yield break;}
            foreach(var piece in EdgeApproachPolicy.Clip(curve,new List<EdgeApproachPolicy.Limit>{new EdgeApproachPolicy.Limit{Origin=mid-normal*.4f,Inward=-normal}}))yield return piece;
        }
        // Double-line half width + stop-bar half thickness + visible clearance.
        internal const float BarClearance=.16f+.25f+.1f;
        internal static IEnumerable<Bezier4x3> ClearBar(Bezier4x3 bar,List<Geometry.Arc> centers)
        {
            float length=math.distance(bar.a,bar.d);int count=math.max(1,(int)math.ceil(length/.1f));float start=-1;
            for(int i=0;i<count;i++){
                float lo=i/(float)count,hi=(i+1f)/count;var p=Geometry.Position(bar,(lo+hi)*.5f);bool clear=true;
                // Midpoint distance plus half a cell protects the entire bar cell.
                foreach(var center in centers){var q=Geometry.Position(center.Curve,center.Nearest(p));
                    if(math.abs(q.y-p.y)<=2&&math.distance(q.xz,p.xz)<BarClearance+length/(2*count)){clear=false;break;}}
                if(clear&&start<0)start=lo;
                if(!clear&&start>=0){yield return Geometry.Cut(bar,start,lo);start=-1;}
            }
            if(start>=0)yield return Geometry.Cut(bar,start,1);
        }
        internal static bool Tip(IEnumerable<Bezier4x3> curves,float3 approachEnd,out float3 tip)
        {
            tip=default;float best=float.MaxValue;
            foreach(var c in curves)foreach(var p in new[]{c.a,c.d})
            {
                if(math.abs(p.y-approachEnd.y)>2)continue;
                float d=math.distancesq(p.xz,approachEnd.xz);
                if(d<best){best=d;tip=p;}
            }
            return best<float.MaxValue;
        }
        internal static float Retreat(float3 origin,float3 normal,float3 tip)
            =>math.min(0,math.dot(tip-origin,normal)-Setback);

        internal static IEnumerable<Bezier4x3> Outside(Bezier4x3 curve,List<Bounds1> cuts)
        {
            cuts=MarkingPolicy.CrosswalkCuts(cuts,new float2(1),1);
            float from=0;
            foreach(var cut in cuts){if(cut.min>from)yield return Geometry.Cut(curve,from,cut.min);from=math.max(from,cut.max);}
            if(from<1)yield return Geometry.Cut(curve,from,1);
        }
    }
}
