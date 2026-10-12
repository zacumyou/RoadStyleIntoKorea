using System.Collections.Generic;
using Unity.Mathematics;
namespace RoadStyleIntoKorea
{
    internal static class NativeSurfacePolicy
    {
        internal static float2 PavementBounds(float3[] points,int[] pavement,float sign,float offset){
            float lo=float.MaxValue,hi=float.MinValue;
            foreach(int index in pavement){float x=points[index].x*sign+offset;lo=math.min(lo,x);hi=math.max(hi,x);}
            return new float2(lo,hi);
        }
        static int3 Key(float3 p)=>(int3)math.round(p*1000);
        internal static int[] Pavement(float3[] points,int[] faces,out List<int2> rims)
        {
            float top=float.MinValue;foreach(var p in points)top=math.max(top,p.y);
            var curb=new HashSet<int3>();rims=new List<int2>();
            for(int i=0;i+2<faces.Length;i+=3)
            {
                int a=faces[i],b=faces[i+1],c=faces[i+2];
                float lo=math.min(points[a].y,math.min(points[b].y,points[c].y));
                if(lo>top-.05f)continue;
                var high=new List<int>();foreach(int v in new[]{a,b,c})if(points[v].y>=top-.015f){high.Add(v);curb.Add(Key(points[v]));}
                if(high.Count==2&&math.distance(points[high[0]],points[high[1]])>.01f)rims.Add(new int2(high[0],high[1]));
            }
            var result=new List<int>();
            for(int i=0;i+2<faces.Length;i+=3)
            {
                int a=faces[i],b=faces[i+1],c=faces[i+2];
                if(points[a].y<top-.015f||points[b].y<top-.015f||points[c].y<top-.015f)continue;
                if(curb.Contains(Key(points[a]))||curb.Contains(Key(points[b]))||curb.Contains(Key(points[c])))continue;
                result.Add(a);result.Add(b);result.Add(c);
            }
            return result.ToArray();
        }
    }
}
