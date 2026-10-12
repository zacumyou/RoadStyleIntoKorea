using System.Collections.Generic;
using Colossal.Mathematics;
using Unity.Mathematics;
namespace RoadStyleIntoKorea
{
    internal static class PavingGeometry
    {
        internal struct Patch { internal Bezier4x3 Curve; internal float Width; }
        // Geometry is a ribbon, not a rectangle: use its narrowest perpendicular
        // cross-section per short slice. Never enlarge the imported decal mesh.
        internal static IEnumerable<Patch> Cover(Bezier4x3 a,Bezier4x3 b)
        {
            var mid=new Bezier4x3((a.a+b.a)/2,(a.b+b.b)/2,(a.c+b.c)/2,(a.d+b.d)/2);
            var arc=new Geometry.Arc(mid);
            var boundaryA=new Geometry.Arc(a);var boundaryB=new Geometry.Arc(b);
            int count=math.clamp((int)math.ceil(arc.Length/2),1,128);
            for(int part=0;part<count;part++)
            {
                float lo=arc.T(arc.Length*part/count),hi=arc.T(arc.Length*(part+1)/count);
                var curve=Geometry.Cut(mid,lo,hi);float width=float.MaxValue;
                for(int j=0;j<=8;j++)
                {
                    float t=math.lerp(lo,hi,j/8f);
                    float w=math.abs(math.dot(Geometry.Position(a,t)-Geometry.Position(b,t),Geometry.Left(Geometry.Tangent(mid,t))));
                    width=math.min(width,w);
                }
                // Small clearance also protects against cubic offset approximation.
                width-=.08f;if(!math.isfinite(width)||width<.5f)continue;
                foreach(var strip in PavingPolicy.Cover(-width/2,width/2))
                {
                    var offset=Geometry.Offset(curve,strip.Center);bool fits=true;
                    for(int j=0;j<=8&&fits;j++)
                    {
                        float u=j/8f,t=math.lerp(lo,hi,u);var p=Geometry.Position(offset,u);
                        var normal=Geometry.Left(Geometry.Tangent(offset,u));
                        float x=math.dot(Geometry.Position(a,t)-p,normal),y=math.dot(Geometry.Position(b,t)-p,normal);
                        fits=math.min(x,y)<=-strip.Width/2-.015f&&math.max(x,y)>=strip.Width/2+.015f;
                        if(fits)foreach(var corner in new[]{p-normal*strip.Width/2,p+normal*strip.Width/2})
                        {
                            // Projection onto the center tangent alone is insufficient
                            // at a taper: the two boundary tangents are different.
                            foreach(var boundary in new[]{boundaryA,boundaryB})
                            {
                                float nearest=boundary.Nearest(corner);
                                var bp=Geometry.Position(boundary.Curve,nearest);
                                var bn=Geometry.Left(Geometry.Tangent(boundary.Curve,nearest));
                                float inside=math.dot(Geometry.Position(mid,nearest)-bp,bn);
                                if(math.dot(corner-bp,bn)*math.sign(inside)<.01f)fits=false;
                            }
                        }
                    }
                    if(fits)yield return new Patch{Curve=offset,Width=strip.Width};
                }
            }
        }
    }
}
