using Colossal.Mathematics;
using Unity.Mathematics;

namespace RoadStyleIntoKorea
{
    internal static class EdgeJoinPolicy
    {
        internal static bool NativeChain(System.Collections.Generic.List<Bezier4x3> candidates,float3 start,float3 end,out System.Collections.Generic.List<Bezier4x3> path)
        {
            path=new System.Collections.Generic.List<Bezier4x3>();
            return FindChain(candidates,start,end,new bool[candidates.Count],path,0);
        }
        static bool FindChain(System.Collections.Generic.List<Bezier4x3> candidates,float3 point,float3 end,bool[] used,System.Collections.Generic.List<Bezier4x3> path,int depth)
        {
            const float tolerance=.35f;
            if(path.Count>0&&math.distance(point,end)<=tolerance)return true;
            if(depth>=4)return false;
            for(int i=0;i<candidates.Count;i++)
            {
                if(used[i])continue;var c=candidates[i];
                if(math.distance(c.d,point)<math.distance(c.a,point))c=Geometry.Reverse(c);
                if(math.distance(c.a,point)>tolerance||math.distance(c.a,c.d)<.03f)continue;
                used[i]=true;path.Add(c);
                if(FindChain(candidates,c.d,end,used,path,depth+1))return true;
                path.RemoveAt(path.Count-1);used[i]=false;
            }
            return false;
        }
        internal static System.Collections.Generic.IEnumerable<Bezier4x3> FitSections(Bezier4x3 curve,System.Func<float3,float3?> fit)
        {
            Bezier4x3? pending=null;
            foreach(var next in FitSectionsRaw(curve,fit))
            {
                if(pending.HasValue)
                {
                    var p=pending.Value;var direction=math.normalizesafe(next.d-p.a);
                    bool straight=math.distance(p.d,next.a)<.001f;
                    foreach(var point in new[]{p.b,p.c,p.d,next.a,next.b,next.c})
                        straight&=math.length(math.cross(point-p.a,direction))<.001f;
                    if(straight&&math.dot(p.d-p.a,next.d-next.a)>0){pending=Geometry.Line(p.a,next.d);continue;}
                    yield return p;
                }
                pending=next;
            }
            if(pending.HasValue)yield return pending.Value;
        }
        static System.Collections.Generic.IEnumerable<Bezier4x3> FitSectionsRaw(Bezier4x3 curve,System.Func<float3,float3?> fit)
        {
            var arc=new Geometry.Arc(curve);int count=math.max(1,(int)math.ceil(arc.Length/.25f));float unchanged=-1;
            for(int i=0;i<count;i++)
            {
                float lo=arc.Length*i/count,hi=arc.Length*(i+1)/count;
                var a=arc.At(lo);var b=arc.At(hi);var m=arc.At((lo+hi)/2);
                var fa=fit(a);var fb=fit(b);var fm=fit(m);
                bool valid=fa.HasValue&&fb.HasValue&&fm.HasValue;
                bool same=valid&&math.distancesq(a,fa.Value)<.000001f&&math.distancesq(b,fb.Value)<.000001f&&math.distancesq(m,fm.Value)<.000001f;
                if(same){if(unchanged<0)unchanged=lo;continue;}
                if(unchanged>=0){yield return arc.Slice(unchanged,lo);unchanged=-1;}
                if(!valid)continue;
                // Only repaired sections become short segments; intact runs retain
                // their original cubic and continuous dash phase.
                foreach(var line in new[]{Geometry.Line(fa.Value,fm.Value),Geometry.Line(fm.Value,fb.Value)})
                {
                    var midpoint=Geometry.Position(line,.5f);var checkedPoint=fit(midpoint);
                    if(checkedPoint.HasValue&&math.distance(checkedPoint.Value,midpoint)<.01f&&math.distance(line.a,line.d)>.01f)yield return line;
                }
            }
            if(unchanged>=0)yield return arc.Slice(unchanged,arc.Length);
        }
        internal static float InsetOffset(float laneWidth,float paintHalfWidth,float inset)=>math.max(0,laneWidth*.5f-math.max(.075f,paintHalfWidth)-.05f-math.clamp(inset,0,1));
        internal static bool OtherSide(bool left,bool firstStart,bool secondStart)=>firstStart==secondStart?!left:left;
        internal static bool TryCurve(float3 start,float3 into,float3 end,float3 away,out Bezier4x3 curve)
        {
            curve=default;float distance=math.distance(start,end);
            if(!math.isfinite(distance)||distance<.03f||distance>40||math.abs(start.y-end.y)>distance*.3f+.2f)return false;
            var chord=math.normalizesafe(end-start);into=math.normalizesafe(into);away=math.normalizesafe(away);
            if(math.dot(into,chord)<.15f||math.dot(away,chord)<.15f||math.dot(into,away)<-.1f)return false;
            float handle=distance/3;
            curve=new Bezier4x3(start,start+into*handle,end-away*handle,end);return true;
        }
    }
}
