using System.Collections.Generic;
using Colossal.Mathematics;
using Unity.Mathematics;
namespace RoadStyleIntoKorea
{
    // Clip AFTER deformation: interpolating a clipped vertex on the Bezier would
    // put it below the original planar triangle at a change of grade.
    internal static class PavingMeshGeometry
    {
        struct Vertex { internal float X;internal float3 Position;internal float2 UV; }
        internal static void Build(float3[] source,int[] triangles,float3 offset,float2 sign,float length,float width,
            float min,float max,Bezier4x3 startLeft,Bezier4x3 startRight,Bezier4x3 endLeft,Bezier4x3 endRight,
            out List<float3> positions,out List<int> indices,out List<float2> uv,bool node=false,NodeSurfaceSync sync=default,List<float3> minBorder=null,List<float3> maxBorder=null)
        {
            positions=new List<float3>();indices=new List<int>();uv=new List<float2>();
            if(source.Length==0||length<=0||width<=0)return;
            float top=float.MinValue;foreach(var p in source)top=math.max(top,p.y);
            float mid=.5f+(min+max)*.5f/width;
            var first=new Geometry.Arc(Lerp(startLeft,startRight,mid));var second=new Geometry.Arc(Lerp(endLeft,endRight,mid));
            var transformed=new Vertex[source.Length];
            for(int i=0;i<source.Length;i++)
            {
                var local=source[i]*new float3(sign.x,1,sign.y)+offset;
                float s=math.clamp(1+local.z/(length*.5f),0,node?1:2),t=s<=1?s:s-1;
                var p=Deform(source[i],offset,sign,length,width,startLeft,startRight,endLeft,endRight,node,sync);
                transformed[i]=new Vertex{X=local.x,Position=p,UV=new float2((local.x-min)/8,(s<=1?first.Distance(t):first.Length+second.Distance(t))/8)};
            }
            for(int i=0;i+2<triangles.Length;i+=3)
            {
                int ia=triangles[i],ib=triangles[i+1],ic=triangles[i+2];
                if(ia<0||ib<0||ic<0||ia>=source.Length||ib>=source.Length||ic>=source.Length)continue;
                if(node&&(OutsideNode(source[ia].z,sign.y,offset.z,length)||OutsideNode(source[ib].z,sign.y,offset.z,length)||OutsideNode(source[ic].z,sign.y,offset.z,length)))continue;
                // Lower curb/bed faces are excluded in addition to the planted
                // material mask supplied by the caller (grass may be top-height).
                if(source[ia].y<top-.015f||source[ib].y<top-.015f||source[ic].y<top-.015f)continue;
                var polygon=new List<Vertex>{transformed[ia],transformed[ib],transformed[ic]};
                polygon=Clip(Clip(polygon,min,true),max,false);
                if(polygon.Count<3)continue;
                // Export the already deformed AND clipped triangle edges. A new
                // Bezier evaluated at the clip coordinate does not follow these
                // edges around a node, even when source/target sync knots match.
                for(int j=0;j<polygon.Count;j++){
                    var a=polygon[j];var b=polygon[(j+1)%polygon.Count];
                    if(math.distancesq(a.Position,b.Position)<.00000001f)continue;
                    if(minBorder!=null&&math.abs(a.X-min)<.0001f&&math.abs(b.X-min)<.0001f){minBorder.Add(a.Position);minBorder.Add(b.Position);}
                    if(maxBorder!=null&&math.abs(a.X-max)<.0001f&&math.abs(b.X-max)<.0001f){maxBorder.Add(a.Position);maxBorder.Add(b.Position);}
                }
                int begin=positions.Count;
                foreach(var v in polygon){positions.Add(v.Position);uv.Add(v.UV);}
                for(int j=1;j+1<polygon.Count;j++)
                {
                    float y=math.cross(polygon[j].Position-polygon[0].Position,polygon[j+1].Position-polygon[0].Position).y;
                    if(!math.isfinite(y)||math.abs(y)<.000001f)continue;
                    indices.Add(begin);indices.Add(begin+(y>0?j:j+1));indices.Add(begin+(y>0?j+1:j));
                }
            }
        }
        static bool OutsideNode(float z,float sign,float offset,float length){float local=z*sign+offset;return local>.01f||local<-length*.5f-.01f;}
        internal static float3 Deform(float3 source,float3 offset,float2 sign,float length,float width,Bezier4x3 startLeft,Bezier4x3 startRight,Bezier4x3 endLeft,Bezier4x3 endRight,bool node,NodeSurfaceSync sync=default)
        {
            var local=source*new float3(sign.x,1,sign.y)+offset;
            float x=math.saturate(.5f+local.x/width),s=math.clamp(1+local.z/(length*.5f),0,node?1:2),t=s<=1?s:s-1;
            if(node)x=sync.Map(x,t,startLeft,startRight);
            return math.lerp(Geometry.Position(s<=1?startLeft:endLeft,t),Geometry.Position(s<=1?startRight:endRight,t),x)+new float3(0,local.y,0);
        }
        static Bezier4x3 Lerp(Bezier4x3 a,Bezier4x3 b,float t)=>new Bezier4x3(math.lerp(a.a,b.a,t),math.lerp(a.b,b.b,t),math.lerp(a.c,b.c,t),math.lerp(a.d,b.d,t));
        static List<Vertex> Clip(List<Vertex> input,float limit,bool lower)
        {
            var result=new List<Vertex>();if(input.Count==0)return result;
            var previous=input[input.Count-1];bool wasInside=lower?previous.X>=limit:previous.X<=limit;
            foreach(var current in input)
            {
                bool inside=lower?current.X>=limit:current.X<=limit;
                if(inside!=wasInside)
                {
                    float t=(limit-previous.X)/(current.X-previous.X);
                    result.Add(new Vertex{X=limit,Position=math.lerp(previous.Position,current.Position,t),UV=math.lerp(previous.UV,current.UV,t)});
                }
                if(inside)result.Add(current);previous=current;wasInside=inside;
            }
            return result;
        }
    }
}
