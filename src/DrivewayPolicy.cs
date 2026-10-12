using System.Collections.Generic;
using Colossal.Mathematics;
using Unity.Mathematics;

namespace RoadStyleIntoKorea
{
    // Work is bounded per pair of curves and runs only while rebuilding painted edges.
    internal static class DrivewayPolicy
    {
        internal sealed class Connection
        {
            internal readonly Bezier4x3 Curve;
            internal readonly float2 Width;
            internal readonly float3[] Points;
            internal readonly float2 Min,Max;
            internal Connection(Bezier4x3 curve,float2 width)
            {
                Curve=curve;Width=width;
                Min=math.min(math.min(curve.a.xz,curve.b.xz),math.min(curve.c.xz,curve.d.xz));
                Max=math.max(math.max(curve.a.xz,curve.b.xz),math.max(curve.c.xz,curve.d.xz));
                float length=math.distance(curve.a,curve.b)+math.distance(curve.b,curve.c)+math.distance(curve.c,curve.d);
                int steps=math.clamp((int)math.ceil(length/1.5f),8,64);
                Points=new float3[steps+1];
                for(int i=0;i<=steps;i++)Points[i]=Geometry.Position(curve,(float)i/steps);
            }
        }
        static float Cross(float2 a,float2 b)=>a.x*b.y-a.y*b.x;
        static bool Overlap(float2 amin,float2 amax,float2 bmin,float2 bmax)=>math.all(amax>=bmin)&&math.all(bmax>=amin);

        // Return merged distances along the painted curve; split once per entrance, not per dash.
        internal static List<float2> Intervals(Geometry.Arc edge,List<Connection> connections)
        {
            var result=new List<float2>();
            var c=edge.Curve;
            float2 min=math.min(math.min(c.a.xz,c.b.xz),math.min(c.c.xz,c.d.xz));
            float2 max=math.max(math.max(c.a.xz,c.b.xz),math.max(c.c.xz,c.d.xz));
            float3[] points=null;
            int count=math.clamp((int)math.ceil(edge.Length/2),8,128);
            foreach(var connection in connections)
            {
                if(!Overlap(min,max,connection.Min,connection.Max))continue;
                if(points==null){points=new float3[count+1];for(int i=0;i<=count;i++)points[i]=Geometry.Position(c,(float)i/count);}
                var other=connection.Points;int n=other.Length-1;
                for(int j=0;j<n;j++)
                {
                    float3 p=other[j],q=other[j+1];float2 v=q.xz-p.xz;
                    float2 lo=math.min(p.xz,q.xz),hi=math.max(p.xz,q.xz);
                    if(!Overlap(min,max,lo,hi))continue;
                    for(int i=0;i<count;i++)
                    {
                        float3 a=points[i],b=points[i+1];
                        if(!Overlap(math.min(a.xz,b.xz),math.max(a.xz,b.xz),lo,hi))continue;
                        float2 u=b.xz-a.xz;float denominator=Cross(u,v);
                        if(math.abs(denominator)<1e-6f)continue;
                        float2 delta=p.xz-a.xz;
                        float s=Cross(delta,v)/denominator,t=Cross(delta,u)/denominator;
                        if(s<0||s>1||t<0||t>1||math.abs(math.lerp(a.y,b.y,s)-math.lerp(p.y,q.y,t))>.75f)continue;
                        float et=(i+s)/count,ct=(j+t)/n;
                        float sine=math.abs(Cross(math.normalizesafe(Geometry.Tangent(c,et).xz),math.normalizesafe(Geometry.Tangent(connection.Curve,ct).xz)));
                        // A parallel lane is not an entrance crossing this boundary.
                        if(sine<.15f)continue;
                        float half=(math.lerp(connection.Width.x,connection.Width.y,ct)*.5f+.1f)/sine;
                        float at=edge.Distance(et);
                        result.Add(new float2(math.max(0,at-half),math.min(edge.Length,at+half)));
                    }
                }
            }
            Merge(result);return result;
        }
        internal static void Merge(List<float2> intervals)
        {
            if(intervals.Count<2)return;
            intervals.Sort((a,b)=>a.x.CompareTo(b.x));int count=1;
            for(int i=1;i<intervals.Count;i++){
                var last=intervals[count-1];var next=intervals[i];
                if(next.x<=last.y+.025f)intervals[count-1]=new float2(last.x,math.max(last.y,next.y));
                else intervals[count++]=next;
            }
            if(count<intervals.Count)intervals.RemoveRange(count,intervals.Count-count);
        }
    }
}
