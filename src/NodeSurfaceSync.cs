using Unity.Mathematics;using Colossal.Mathematics;
namespace RoadStyleIntoKorea
{
    // Matches NetCompositionMeshLitShader vertex program, source/target sync knots.
    internal struct NodeSurfaceSync
    {
        internal bool Enabled;internal float4 Source,Target;
        internal float Map(float x,float t,Bezier4x3 left,Bezier4x3 right)
        {
            if(!Enabled)return x;
            float start=math.distance(left.a.xz,right.a.xz),end=math.distance(left.d.xz,right.d.xz);
            float u=math.saturate(t*.5f),blend=2*u*u*(3-2*u);
            var target=math.lerp(Source*start,Target*end,blend)/math.max(.000001f,math.lerp(start,end,blend));
            // Shader makes coincident knot intervals strictly monotonic before division.
            float c=math.min(Source.z,Source.y),d=math.max(c+.000001f,Source.z);
            float b=math.min(c-.000001f,Source.x),e=math.max(d+.000001f,Source.w);
            float a=math.min(b-.000001f,0),f=math.max(e+.000001f,1);
            return math.saturate((x-a)/(b-a))*target.x+math.saturate((x-b)/(c-b))*(target.y-target.x)
                +math.saturate((x-c)/(d-c))*(target.z-target.y)+math.saturate((x-d)/(e-d))*(target.w-target.z)
                +math.saturate((x-e)/(f-e))*(1-target.w);
        }
    }
}
