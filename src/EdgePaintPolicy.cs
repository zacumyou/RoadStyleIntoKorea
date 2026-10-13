using Unity.Mathematics;
namespace RoadStyleIntoKorea
{
    internal static class EdgePaintPolicy
    {
        internal static float HalfWidth(int style)=>style==4?.13f:style==5?.33f:.08f;
        internal static float2 Gutter(float3[] vertices,float width)
        {
            float top=float.MinValue,min=float.MaxValue,max=float.MinValue;
            foreach(var v in vertices)top=math.max(top,v.y);
            foreach(var v in vertices)if(v.y>=top-.04f){min=math.min(min,v.x);max=math.max(max,v.x);}
            return min<=max?math.clamp(new float2(width*.5f+min,width*.5f-max),0,.6f):float2.zero;
        }
    }
}
