using Colossal.Mathematics;
using Unity.Mathematics;

namespace RoadStyleIntoKorea
{
    internal static class Geometry
    {
        internal static bool SameCurve(Bezier4x3 a,Bezier4x3 b)=>math.distancesq(a.a,b.a)<.000001f&&math.distancesq(a.b,b.b)<.000001f&&math.distancesq(a.c,b.c)<.000001f&&math.distancesq(a.d,b.d)<.000001f;
        internal static Bezier4x3 MoveEndpoints(Bezier4x3 original,float3 start,float3 end)
        {
            if(math.distancesq(original.a,start)<.000001f&&math.distancesq(original.d,end)<.000001f)return original;
            var arc=new Arc(original);float a=arc.Nearest(start),b=arc.Nearest(end);
            // Trim an approach at its new stopping boundary instead of bending it backwards.
            if(a<b&&math.distance(Position(original,a),start)<.025f&&math.distance(Position(original,b),end)<.025f)
            {var cut=Cut(original,a,b);return new Bezier4x3(start,cut.b+(start-cut.a),cut.c+(end-cut.d),end);}
            return new Bezier4x3(start,original.b+(start-original.a),original.c+(end-original.d),end);
        }
        internal static float3 Position(Bezier4x3 c,float t)
        { float u=1-t; return u*u*u*c.a+3*u*u*t*c.b+3*u*t*t*c.c+t*t*t*c.d; }
        internal static float3 Tangent(Bezier4x3 c,float t)
        { float u=1-t; return math.normalizesafe(u*u*(c.b-c.a)+2*u*t*(c.c-c.b)+t*t*(c.d-c.c),new float3(0,0,1)); }
        internal static float3 Left(float3 forward) => math.normalizesafe(new float3(-forward.z,0,forward.x));
        internal static Bezier4x3 Line(float3 a,float3 b) => new Bezier4x3(a,math.lerp(a,b,1f/3),math.lerp(a,b,2f/3),b);
        internal static Bezier4x3 Reverse(Bezier4x3 c) => new Bezier4x3(c.d,c.c,c.b,c.a);
        internal static Bezier4x3 Cut(Bezier4x3 c,float a,float b)
        {
            a=math.saturate(a);b=math.clamp(b,a,1);float scale=b-a;
            float3 da=3*((1-a)*(1-a)*(c.b-c.a)+2*(1-a)*a*(c.c-c.b)+a*a*(c.d-c.c));
            float3 db=3*((1-b)*(1-b)*(c.b-c.a)+2*(1-b)*b*(c.c-c.b)+b*b*(c.d-c.c));
            float3 p=Position(c,a),q=Position(c,b);
            return new Bezier4x3(p,p+da*scale/3,q-db*scale/3,q);
        }
        internal static Bezier4x3 Offset(Bezier4x3 c,float left)
        {
            // Endpoint tangents are preserved. Native continuous tiling is retained on the offset cubic.
            float3 a=Left(Tangent(c,0))*left,d=Left(Tangent(c,1))*left;
            return new Bezier4x3(c.a+a,c.b+a,c.c+d,c.d+d);
        }
        internal static float LeftAngle(Bezier4x3 c)
        {var a=Tangent(c,0);var b=Tangent(c,1);return math.degrees(math.atan2(math.dot(Left(a),b),math.dot(a.xz,b.xz)));}
        internal static float SolidLength(float length,float available,Settings s)
        {return math.min(math.max(0,available),math.clamp(length*math.clamp(s.SolidPercent,25,50)*0.01f,s.SolidMinimum,math.max(s.SolidMinimum,s.SolidMaximum)));}
        internal static System.Collections.Generic.IEnumerable<Bezier4x3> ClearSlices(Bezier4x3 curve,System.Func<float3,bool> clear)
        {
            var arc=new Arc(curve);float start=-1;
            int count=math.max(1,(int)math.ceil(arc.Length/.5f));
            bool previous=clear(arc.At(0));
            for(int i=0;i<count;i++)
            {
                float lo=arc.Length*i/count,hi=arc.Length*(i+1)/count;
                bool next=clear(arc.At(hi));
                bool allowed=previous&&next&&clear(arc.At((lo+hi)*.5f));previous=next;
                if(allowed&&start<0)start=lo;
                if(!allowed&&start>=0){if(lo-start>.05f)yield return arc.Slice(start,lo);start=-1;}
            }
            if(start>=0&&arc.Length-start>.05f)yield return arc.Slice(start,arc.Length);
        }
        internal sealed class Arc
        {
            const int Steps=64;
            internal readonly Bezier4x3 Curve;
            readonly float[] distances=new float[Steps+1];
            readonly float3[] samples=new float3[Steps+1];
            readonly float2[] blockMin=new float2[8],blockMax=new float2[8];
            readonly bool uniformLine;
            readonly float3 boundsMin,boundsMax;
            internal bool MayBeNear(float3 p,float radius,float height=2)=>math.all(p.xz>=boundsMin.xz-radius)&&math.all(p.xz<=boundsMax.xz+radius)&&p.y>=boundsMin.y-height&&p.y<=boundsMax.y+height;
            internal float Length=>distances[Steps];
            internal Arc(Bezier4x3 curve)
            {
                // Native long straight roads contain millimetre control-point
                // noise. A 0.1mm threshold sent them through 64 segment searches
                // and 24 cubic evaluations for every clearance sample.
                Curve=curve;boundsMin=math.min(math.min(curve.a,curve.b),math.min(curve.c,curve.d));boundsMax=math.max(math.max(curve.a,curve.b),math.max(curve.c,curve.d));uniformLine=math.distancesq(curve.b,math.lerp(curve.a,curve.d,1f/3))<.000025f&&math.distancesq(curve.c,math.lerp(curve.a,curve.d,2f/3))<.000025f;
                samples[0]=curve.a;var previous=curve.a;
                for(int i=1;i<=Steps;i++){var p=Position(curve,i/(float)Steps);samples[i]=p;distances[i]=distances[i-1]+math.distance(previous,p);previous=p;}
                for(int block=0;block<8;block++){
                    var lo=samples[block*8].xz;var hi=lo;
                    for(int i=1;i<=8;i++){lo=math.min(lo,samples[block*8+i].xz);hi=math.max(hi,samples[block*8+i].xz);}
                    blockMin[block]=lo;blockMax[block]=hi;
                }
            }
            internal float T(float distance)
            {distance=math.clamp(distance,0,Length);int lo=0,hi=Steps;while(hi-lo>1){int m=(lo+hi)/2;if(distances[m]<distance)lo=m;else hi=m;}return (lo+(distance-distances[lo])/math.max(0.0001f,distances[hi]-distances[lo]))/Steps;}
            internal float3 At(float distance)=>Position(Curve,T(distance));
            internal float Distance(float t){float f=math.saturate(t)*Steps;int i=math.min((int)f,Steps-1);return math.lerp(distances[i],distances[i+1],f-i);}
            internal Bezier4x3 Slice(float start,float end)=>Cut(Curve,T(start),T(end));
            internal float Nearest(float3 point)
            {
                var span=Curve.d.xz-Curve.a.xz;
                if(uniformLine)return math.saturate(math.dot(point.xz-Curve.a.xz,span)/math.max(.00000001f,math.lengthsq(span)));
                float best=float.MaxValue;int segment=0;
                for(int block=0;block<8;block++)
                {
                    var outside=math.max(math.max(blockMin[block]-point.xz,point.xz-blockMax[block]),new float2(0));
                    if(math.lengthsq(outside)>best)continue;
                    for(int i=block*8;i<(block+1)*8;i++){
                    var a=samples[i].xz;var delta=samples[i+1].xz-a;
                    float f=math.saturate(math.dot(point.xz-a,delta)/math.max(.00000001f,math.lengthsq(delta)));
                    float distance=math.distancesq(a+delta*f,point.xz);
                    if(distance<best){best=distance;segment=i;}
                    }
                }
                float lo=math.max(0,(segment-1f)/Steps),hi=math.min(1,(segment+2f)/Steps);
                // Evaluate relative coordinates. Subtracting kilometre-sized
                // world positions after evaluation made a flat distance minimum
                // lose centimetres of precision on long captured highway curves.
                var p0=Curve.a.xz-point.xz;var p1=Curve.b.xz-point.xz;var p2=Curve.c.xz-point.xz;var p3=Curve.d.xz-point.xz;
                for(int i=0;i<12;i++){float a=math.lerp(lo,hi,1f/3),b=math.lerp(lo,hi,2f/3);if(LocalDistance(p0,p1,p2,p3,a)<LocalDistance(p0,p1,p2,p3,b))hi=b;else lo=a;}
                float result=(lo+hi)*.5f;
                float error=LocalDistance(p0,p1,p2,p3,result);
                float first=math.distancesq(Curve.a.xz,point.xz),last=math.distancesq(Curve.d.xz,point.xz);
                if(first<=error&&first<=last)return 0;
                if(last<=error)return 1;
                return result;
            }
            static float LocalDistance(float2 a,float2 b,float2 c,float2 d,float t){float u=1-t;return math.lengthsq(u*u*u*a+3*u*u*t*b+3*u*t*t*c+t*t*t*d);}
        }
        internal sealed class Route
        {
            readonly System.Collections.Generic.List<Arc> parts=new System.Collections.Generic.List<Arc>();
            internal float Length {get;private set;}
            internal Route(Bezier4x3 c){Append(new Arc(c));}
            internal void Append(Arc part){parts.Add(part);Length+=part.Length;}
            internal void Append(Route route){foreach(var part in route.parts)Append(part);}
            Arc Part(ref float distance)
            {distance=math.clamp(distance,0,Length);foreach(var p in parts){if(distance<=p.Length)return p;distance-=p.Length;}distance=parts[parts.Count-1].Length;return parts[parts.Count-1];}
            internal float3 At(float distance){var p=Part(ref distance);return p.At(distance);}
            internal float3 Direction(float distance){var p=Part(ref distance);return Tangent(p.Curve,p.T(distance));}
            internal float Nearest(float3 position,out float3 point,out float3 direction)
            {
                float best=float.MaxValue,offset=0,distance=0;point=default;direction=default;
                foreach(var p in parts){float t=p.Nearest(position);var q=Position(p.Curve,t);float d=math.distancesq(q.xz,position.xz);if(d<best){best=d;point=q;direction=Tangent(p.Curve,t);distance=offset+p.Distance(t);}offset+=p.Length;}
                return distance;
            }
            internal bool IntersectPlane(float3 origin,float3 normal,out float distance)
            {
                // Last crossing along travel order, bounded to the actual route.
                distance=-1;float previous=Length;float pv=math.dot(At(previous)-origin,normal);
                for(int i=1;i<=128;i++)
                {
                    float current=Length*(1-i/128f),cv=math.dot(At(current)-origin,normal);
                    if(cv<=0&&pv>=0)
                    {
                        float lo=current,hi=previous;
                        for(int j=0;j<20;j++){float mid=(lo+hi)*.5f;if(math.dot(At(mid)-origin,normal)<0)lo=mid;else hi=mid;}
                        distance=(lo+hi)*.5f;return true;
                    }
                    previous=current;pv=cv;
                }
                return false;
            }
        }
    }
}
