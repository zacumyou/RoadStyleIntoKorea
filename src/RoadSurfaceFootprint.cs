using Colossal.Mathematics;
using Unity.Mathematics;
namespace RoadStyleIntoKorea
{
    internal sealed class RoadSurfaceFootprint
    {
        readonly SidewalkFootprint surface=new SidewalkFootprint();
        readonly Geometry.Arc left,right;
        internal RoadSurfaceFootprint(Bezier4x3 a,Bezier4x3 b)
        {surface.Add(a,b);left=new Geometry.Arc(a);right=new Geometry.Arc(b);}
        internal bool ContainsPaint(float3 point,float halfWidth)
        {
            // Centerline projection misclassifies widened/narrowed ends when
            // the boundary and center tangents differ. Test the actual surface.
            if(!surface.Overlaps(point,.002f))return false;
            float clearance=halfWidth+.04f;
            return (!left.MayBeNear(point,clearance,float.MaxValue)||math.distance(point.xz,Geometry.Position(left.Curve,left.Nearest(point)).xz)>=clearance)&&
                (!right.MayBeNear(point,clearance,float.MaxValue)||math.distance(point.xz,Geometry.Position(right.Curve,right.Nearest(point)).xz)>=clearance);
        }
        internal bool ContainsPoint(float3 point)=>surface.Overlaps(point,.002f);
        internal static bool UnionContains(System.Collections.Generic.List<RoadSurfaceFootprint> surfaces,float3 point,float3 normal,float half)
        {
            int steps=math.max(2,(int)math.ceil((half+.04f)*2/.05f));
            for(int i=0;i<=steps;i++){
                var sample=point+normal*math.lerp(-half-.04f,half+.04f,(float)i/steps);bool inside=false;
                foreach(var surface in surfaces)if(surface.ContainsPoint(sample)){inside=true;break;}
                if(!inside)return false;
            }
            return true;
        }
    }
}
