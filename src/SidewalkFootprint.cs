using System.Collections.Generic;
using Colossal.Mathematics;
using Unity.Mathematics;
namespace RoadStyleIntoKorea
{
    internal sealed class SidewalkFootprint
    {
        struct Triangle { internal float3 A,B,C;internal float2 Min,Max,U,V,HeightDelta;internal float Det; }
        internal readonly List<Bezier4x3> Boundaries=new List<Bezier4x3>();
        const float CellSize=4;
        readonly Dictionary<long,List<Triangle>> cells=new Dictionary<long,List<Triangle>>();
        static long Key(int x,int z)=>((long)x<<32)|(uint)z;
        internal void Add(Bezier4x3 a,Bezier4x3 b)
        {
            // Collision masks do not need the renderer's fixed 25cm rows.
            // Subdivide both boundaries together, with <=2mm control-hull error
            // against their linear parameterization and <=8m cells on straights.
            var points=new List<float3>();var indices=new List<int>();
            points.Add(a.a);points.Add(b.a);
            AppendMask(a,b,points,indices,0);
            Add(points,indices);
        }
        static bool Flat(Bezier4x3 c)=>math.distancesq(c.b,math.lerp(c.a,c.d,1f/3))<=.000004f&&math.distancesq(c.c,math.lerp(c.a,c.d,2f/3))<=.000004f;
        static void AppendMask(Bezier4x3 a,Bezier4x3 b,List<float3> points,List<int> indices,int depth)
        {
            if(depth<11&&(!Flat(a)||!Flat(b)||math.distancesq(a.a,a.d)>64||math.distancesq(b.a,b.d)>64)){
                AppendMask(Geometry.Cut(a,0,.5f),Geometry.Cut(b,0,.5f),points,indices,depth+1);
                AppendMask(Geometry.Cut(a,.5f,1),Geometry.Cut(b,.5f,1),points,indices,depth+1);return;
            }
            int n=points.Count-2;points.Add(a.d);points.Add(b.d);
            indices.Add(n);indices.Add(n+1);indices.Add(n+2);
            indices.Add(n+1);indices.Add(n+3);indices.Add(n+2);
        }
        internal void Add(List<float3> points,List<int> indices)
        {
            for(int i=0;i<indices.Count;i+=3)
            {
                var x=points[indices[i]];var y=points[indices[i+1]];var z=points[indices[i+2]];
                if(math.abs(Cross(y.xz-x.xz,z.xz-x.xz))<.000001f)continue;
                var triangle=new Triangle{A=x,B=y,C=z,Min=math.min(x.xz,math.min(y.xz,z.xz)),Max=math.max(x.xz,math.max(y.xz,z.xz)),U=y.xz-x.xz,V=z.xz-x.xz,HeightDelta=new float2(y.y-x.y,z.y-x.y),Det=Cross(y.xz-x.xz,z.xz-x.xz)};
                var lo=(int2)math.floor(triangle.Min/CellSize);var hi=(int2)math.floor(triangle.Max/CellSize);
                for(int cx=lo.x;cx<=hi.x;cx++)for(int cz=lo.y;cz<=hi.y;cz++)
                {
                    long key=Key(cx,cz);
                    if(!cells.TryGetValue(key,out var bucket)){bucket=new List<Triangle>();cells.Add(key,bucket);}
                    bucket.Add(triangle);
                }
            }
        }
        static float Cross(float2 a,float2 b)=>a.x*b.y-a.y*b.x;
        internal bool TryHeight(float3 p,out float height,float maxDistance=.6f)
        {
            height=p.y;float best=float.MaxValue;bool found=false;
            var cell=(int2)math.floor(p.xz/CellSize);
            if(!cells.TryGetValue(Key(cell.x,cell.y),out var bucket))return false;
            foreach(var t in bucket){
                var u=t.U;var v=t.V;var w=p.xz-t.A.xz;
                float det=t.Det;
                float b=Cross(w,v)/det,c=Cross(u,w)/det;
                if(b<-.0001f||c<-.0001f||b+c>1.0001f)continue;
                float y=t.A.y+b*t.HeightDelta.x+c*t.HeightDelta.y,distance=math.abs(y-p.y);
                if(distance>maxDistance||distance>=best)continue;
                height=y;best=distance;found=true;
            }
            return found;
        }
        static float Distance(float2 p,float2 a,float2 b)
        {var d=b-a;return math.distance(p,a+d*math.saturate(math.dot(p-a,d)/math.max(.000001f,math.lengthsq(d))));}
        internal bool Overlaps(float3 p,float margin)
        {
            var lo=(int2)math.floor((p.xz-margin)/CellSize);var hi=(int2)math.floor((p.xz+margin)/CellSize);
            for(int x=lo.x;x<=hi.x;x++)for(int z=lo.y;z<=hi.y;z++)
            {
                if(!cells.TryGetValue(Key(x,z),out var bucket))continue;
                foreach(var t in bucket)
                {
                if(math.any(p.xz<t.Min-margin)||math.any(p.xz>t.Max+margin)||
                   p.y<math.min(t.A.y,math.min(t.B.y,t.C.y))-1||p.y>math.max(t.A.y,math.max(t.B.y,t.C.y))+1)continue;
                float a=Cross(t.B.xz-t.A.xz,p.xz-t.A.xz),b=Cross(t.C.xz-t.B.xz,p.xz-t.B.xz),c=Cross(t.A.xz-t.C.xz,p.xz-t.C.xz);
                if((a>=0&&b>=0&&c>=0)||(a<=0&&b<=0&&c<=0))return true;
                if(Distance(p.xz,t.A.xz,t.B.xz)<=margin||Distance(p.xz,t.B.xz,t.C.xz)<=margin||Distance(p.xz,t.C.xz,t.A.xz)<=margin)return true;
                }
            }
            return false;
        }
    }
}
