using System.Collections.Generic;
using Colossal.Mathematics;
using Unity.Mathematics;

namespace RoadStyleIntoKorea
{
    // One footprint drives the asphalt outline, paint and gutter/curb exclusions.
    internal sealed class SideRoadOpening
    {
        internal Geometry.Arc Road;
        internal float Distance,Width,Inner,Outer,Top,Bottom;
        internal int Side;
        internal bool CutSurface;
        internal bool HeightsPrepared;
        internal float CurbJoinDepth=.6f;
        internal bool HideMarks;
        internal float2[] NativeCurbDepths;
        internal System.Func<float3,float> NativeHeight;
        internal System.Func<float3,float> PavingTopHeight;
        internal float[] GroundHeights;
        internal float GroundSpan;
        internal float NativeTop(float3 p){
            float y=PavingTopHeight!=null?PavingTopHeight(p):NativeHeight==null?float.NaN:NativeHeight(p);
            return math.isfinite(y)?y:Geometry.Position(Road.Curve,Road.Nearest(p)).y+Top;
        }
        float CutHeight(float along,float depth){
            var mouth=RawPoint(along,0);float low=NativeHeight==null?float.NaN:NativeHeight(mouth);
            if(!math.isfinite(low))low=mouth.y+Bottom;
            float high=RawPoint(along,Outer-Inner).y+Top;
            if(GroundHeights!=null&&GroundHeights.Length>1){
                float f=math.saturate((along/GroundSpan+1)*.5f)*(GroundHeights.Length-1);
                int i=math.min((int)f,GroundHeights.Length-2);
                high=math.lerp(GroundHeights[i],GroundHeights[i+1],f-i);
            }
            return math.lerp(low,high,math.smoothstep(0,Outer-Inner,depth));
        }
        internal float3 CutBoundary(int end,float depth){
            var p=CutBoundaryPlan(end,depth);p.y=SurfaceHeight(p);return p;
        }
        internal float3 CutBoundaryPlan(int end,float depth){
            float step=(Outer-Inner)/16f;
            int i=math.clamp((int)math.floor(depth/step),0,15);
            float a=i*step,b=(i+1)*step+(i==15?.1f:0);
            var p=math.lerp(RawPoint(end*HalfSpan(a),a),RawPoint(end*HalfSpan(b),b),math.saturate((depth-a)/(b-a)));
            return p;
        }
        internal List<float3> MarkPath(int end){
            var path=new List<float3>();
            for(int i=0;i<=32;i++){
                float depth=(Outer-Inner)*i/32f;
                var back=CutBoundary(end,depth);
                var a=CutBoundary(end,(Outer-Inner)*math.max(0,i-1)/32f);
                var b=CutBoundary(end,(Outer-Inner)*math.min(32,i+1)/32f);
                var inward=Geometry.Left(b-a);
                if(math.dot(inward,Point(0,depth)-back)<0)inward=-inward;
                // 20cm stone + 12cm clearance + 8cm half paint width.
                var point=back+inward*.4f;point.y=SurfaceHeight(point);path.Add(point);
            }
            return path;
        }
        internal float3 RawPoint(float along,float depth){
            float t=Road.T(Distance+along);var p=Geometry.Position(Road.Curve,t);
            return p+Geometry.Left(Geometry.Tangent(Road.Curve,t))*Side*(Inner+depth);
        }
        internal float Radius=>math.min(2f,(Outer-Inner)*.8f);
        internal float Ramp=>math.min(.6f,Outer-Inner);
        internal float SurfaceHeight(float3 point){
            float t=Road.Nearest(point);var center=Geometry.Position(Road.Curve,t);
            float depth=math.dot(point-center,Geometry.Left(Geometry.Tangent(Road.Curve,t)))*Side-Inner;
            return CutSurface?CutHeight(Road.Distance(t)-Distance,depth):center.y+math.lerp(Bottom,Top,math.smoothstep(0,Ramp,depth));
        }
        internal float HalfSpan(float depth)=>Width*.5f+Radius-math.sqrt(math.max(0,Radius*Radius-math.pow(Radius-math.clamp(depth,0,Radius),2)));
        // Same piecewise boundary as the native clipping cells, including the
        // first interval where the circular mouth changes direction fastest.
        internal float CutHalfSpan(float depth){
            float step=(Outer-Inner)/16f;
            if(step<=0)return HalfSpan(depth);
            float a=math.floor(math.max(0,depth)/step)*step;
            return math.lerp(HalfSpan(a),HalfSpan(a+step),math.saturate((depth-a)/step));
        }
        internal float3 Point(float along,float depth)
        {
            var p=RawPoint(along,depth);
            p.y=CutSurface?CutHeight(along,depth):p.y+math.lerp(Bottom,Top,math.smoothstep(0,Ramp,depth));
            return p;
        }
        internal Bezier4x3 Bend(int end,float inset)
        {
            const float k=.55228475f;
            float r=Radius+inset,x=end*(Width*.5f+Radius);
            return new Bezier4x3(Point(x,-inset),Point(x-end*k*r,-inset),
                Point(end*(Width*.5f-inset),Radius-k*r),Point(end*(Width*.5f-inset),Radius));
        }
        internal void Mesh(out List<float3> vertices,out List<int> indices,out List<float2> coordinates)
        {
            vertices=new List<float3>();indices=new List<int>();coordinates=new List<float2>();
            const int columns=24;int rows=CutSurface?32:20;
            for(int row=0;row<=rows;row++){
                // The ramp occupies the curb band up to the brick's inner edge.
                // Do not extend an elevated wedge over the carriageway.
                float depth=row<=4?Ramp*row/4f:row<=16?math.lerp(Ramp,math.max(Ramp,Radius),(row-4)/12f):math.lerp(math.max(Ramp,Radius),Outer-Inner,(row-16)/4f);
                // Share the actual piecewise clipping boundary, including its
                // vertices. A smooth circle does not match the native cut cells.
                if(CutSurface)depth=(Outer-Inner)*row/rows;
                float half=CutSurface?CutHalfSpan(depth):HalfSpan(depth);
                var left=CutSurface?CutBoundary(-1,depth):default;
                var right=CutSurface?CutBoundary(1,depth):default;
                for(int col=0;col<=columns;col++){
                    float along=math.lerp(-half,half,col/(float)columns);
                    var p=CutSurface?math.lerp(left,right,col/(float)columns):Point(along,depth);
                    if(CutSurface)p.y=SurfaceHeight(p);
                    vertices.Add(p);coordinates.Add(new float2(Inner+depth,Distance+along));
                }
            }
            for(int row=0;row<rows;row++)for(int col=0;col<columns;col++){
                int a=row*(columns+1)+col,b=a+1,c=a+columns+1,d=c+1;
                Add(vertices,indices,a,b,c);Add(vertices,indices,b,d,c);
            }
        }
        static void Add(List<float3> p,List<int> indices,int a,int b,int c){
            float y=math.cross(p[b]-p[a],p[c]-p[a]).y;if(math.abs(y)<1e-7f)return;
            indices.Add(a);indices.Add(y>0?b:c);indices.Add(y>0?c:b);
        }
        internal void Skirt(List<float3> top,List<float2> topUV,out List<float3> vertices,out List<int> indices,out List<float2> uv){
            vertices=new List<float3>();indices=new List<int>();uv=new List<float2>();
            // Close the exposed apron perimeter down to road level. Keep the
            // top mesh separate so its smooth normals do not round the wall.
            const int columns=24,stride=columns+1;int rows=CutSurface?32:20;
            var rim=new List<int>();
            for(int r=0;r<=rows;r++)rim.Add(r*stride+columns);
            for(int c=columns-1;c>=0;c--)rim.Add(rows*stride+c);
            for(int r=rows-1;r>=0;r--)rim.Add(r*stride);
            for(int c=1;c<columns;c++)rim.Add(c);
            for(int i=0;i<rim.Count;i++){
                int a=rim[i],b=rim[(i+1)%rim.Count],k=vertices.Count;
                var p=top[a];var q=top[b];var lowP=p;var lowQ=q;
                lowP.y=math.min(p.y,Geometry.Position(Road.Curve,Road.Nearest(p)).y+Bottom)-.08f;
                lowQ.y=math.min(q.y,Geometry.Position(Road.Curve,Road.Nearest(q)).y+Bottom)-.08f;
                vertices.Add(p);vertices.Add(q);vertices.Add(lowP);vertices.Add(lowQ);
                uv.Add(topUV[a]);uv.Add(topUV[b]);
                uv.Add(topUV[a]+new float2(0,p.y-lowP.y));uv.Add(topUV[b]+new float2(0,q.y-lowQ.y));
                if(Side>0)indices.AddRange(new[]{k,k+1,k+2,k+1,k+3,k+2});
                else indices.AddRange(new[]{k,k+2,k+1,k+1,k+2,k+3});
            }
        }
    }
    internal struct SideRoadUV
    {
        internal static bool AsphaltSource(string name)=>!string.IsNullOrEmpty(name)&&
            (name.StartsWith("Car Drive Piece ",System.StringComparison.OrdinalIgnoreCase)||
             name.StartsWith("Highway Drive Piece ",System.StringComparison.OrdinalIgnoreCase)||
             name.StartsWith("Alley Drive Piece ",System.StringComparison.OrdinalIgnoreCase));
        internal float2 Origin,DX,DZ,Anchor;
        internal float MinX,MaxX;
        internal float2 Map(float2 metres){
            float width=MaxX-MinX;
            float phase=math.frac((metres.x-MinX)/(2*width))*2;
            float x=MinX+width*(1-math.abs(phase-1));
            return Origin+DX*(x-Anchor.x)+DZ*(metres.y-Anchor.y);
        }
    }
}
