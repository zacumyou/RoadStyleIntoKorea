using System.Collections.Generic;
using Colossal.Mathematics;
using Unity.Mathematics;
namespace RoadStyleIntoKorea
{
    internal static class PavingSurfaceGeometry
    {
        // Metres across / metres along the sidewalk. A shared row V bends the
        // pattern with the path instead of pinning it to global X/Z.
        internal static List<float2> RibbonUV(List<float3> positions,float phase=0)
        {
            var uv=new List<float2>();float distance=phase;float3 previous=default;
            for(int i=0;i+1<positions.Count;i+=2)
            {
                var center=(positions[i]+positions[i+1])*.5f;
                if(i>0)distance+=math.distance(center,previous);
                uv.Add(new float2(0,distance/8));
                uv.Add(new float2(math.distance(positions[i],positions[i+1])/8,distance/8));
                previous=center;
            }
            return uv;
        }
        internal static void Inset(List<float3> positions,bool fromA,float distance)
        {
            for(int i=0;i+1<positions.Count;i+=2)
            {
                var a=positions[i];var b=positions[i+1];float width=math.distance(a.xz,b.xz);
                float t=math.min(1,distance/math.max(.0001f,width));
                if(fromA)positions[i]=math.lerp(a,b,t);else positions[i+1]=math.lerp(b,a,t);
            }
        }
        internal static void Curb(List<float3> full,bool fromA,out List<float3> positions,out List<int> indices)
        {
            positions=new List<float3>();indices=new List<int>();
            for(int i=0;i+1<full.Count;i+=2)
            {
                var outside=full[fromA?i:i+1];var inside=full[fromA?i+1:i];
                float width=math.distance(outside.xz,inside.xz);
                var front=math.lerp(outside,inside,math.min(1,.42f/math.max(.0001f,width)));
                var back=math.lerp(outside,inside,math.min(1,.6f/math.max(.0001f,width)));
                positions.Add(front);positions.Add(back);
            }
            for(int i=0;i+3<positions.Count;i+=2){Add(positions,indices,i,i+1,i+2);Add(positions,indices,i+1,i+3,i+2);}
            // Separate face vertices keep the concrete top/face normal hard.
            int topCount=positions.Count;
            for(int i=0;i+2<topCount;i+=2)
            {
                if(math.distance(positions[i],positions[i+1])<.01f||math.distance(positions[i+2],positions[i+3])<.01f)continue;
                int n=positions.Count;var a=positions[i];var b=positions[i+2];var down=new float3(0,-.12f,0);
                positions.Add(a);positions.Add(a+down);positions.Add(b);positions.Add(b+down);
                bool outward=math.dot(math.cross(down,b-a),positions[i]-positions[i+1])>0;
                indices.Add(n);indices.Add(outward?n+1:n+2);indices.Add(outward?n+2:n+1);
                indices.Add(n+1);indices.Add(outward?n+3:n+2);indices.Add(outward?n+2:n+3);
            }
        }
        internal static void Build(Bezier4x3 a,Bezier4x3 b,out List<float3> positions,out List<int> indices,int nativeRows=0)
        {
            positions=new List<float3>();indices=new List<int>();
            float length=math.max(new Geometry.Arc(a).Length,new Geometry.Arc(b).Length);
            if(!math.isfinite(length))return;
            int rows=nativeRows>0?nativeRows:math.clamp((int)math.ceil(length/.25f),1,2048);
            for(int i=0;i<=rows;i++){float t=i/(float)rows;positions.Add(Geometry.Position(a,t));positions.Add(Geometry.Position(b,t));}
            for(int i=0;i<rows;i++)
            {
                Add(positions,indices,2*i,2*i+1,2*i+2);Add(positions,indices,2*i+1,2*i+3,2*i+2);
            }
        }
        static void Add(List<float3> p,List<int> indices,int a,int b,int c)
        {
            float y=math.cross(p[b]-p[a],p[c]-p[a]).y;
            if(!math.isfinite(y)||math.abs(y)<.000001f)return;
            indices.Add(a);indices.Add(y>0?b:c);indices.Add(y>0?c:b);
        }
    }
}
