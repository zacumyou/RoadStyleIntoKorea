using System.Collections.Generic;
using Colossal.Mathematics;
using Unity.Entities;
using Unity.Mathematics;
namespace RoadStyleIntoKorea
{
    public partial class StyleSystem
    {
        Bezier4x3 InsetCurb(RoadRibbon ribbon,bool first,EdgeStyle style)
        {
            var boundary=first?ribbon.Left:ribbon.Right;
            var midpoint=Geometry.Position(boundary,.5f);
            float sign=math.dot(Geometry.Position(ribbon.Center.Curve,.5f)-midpoint,Geometry.Left(Geometry.Tangent(boundary,.5f)))>=0?1:-1;
            return Geometry.Offset(boundary,sign*(EdgePaintHalfWidth(style)+.12f+math.clamp(S.EdgeLaneGap,0,1)));
        }
        List<Bezier4x3> NativeEdgeCandidates(List<RoadRibbon> ribbons,Geometry.Arc center,bool left,EdgeStyle style)
        {
            var result=new List<Bezier4x3>();
            foreach(var ribbon in ribbons)
            {
                var midpoint=Geometry.Position(ribbon.Center.Curve,.5f);float t=center.Nearest(midpoint);
                var normal=Geometry.Left(Geometry.Tangent(center.Curve,t));
                bool firstIsLeft=math.dot(Geometry.Position(ribbon.Left,.5f)-Geometry.Position(ribbon.Right,.5f),normal)>0;
                bool first=left==firstIsLeft;
                if(first?!ribbon.HasLeftCurb:!ribbon.HasRightCurb)continue;
                var curve=InsetCurb(ribbon,first,style);
                if(math.dot(Geometry.Tangent(curve,.5f),Geometry.Tangent(center.Curve,t))<0)curve=Geometry.Reverse(curve);
                if(new Geometry.Arc(curve).Length>.05f)result.Add(curve);
            }
            return result;
        }
        List<Bezier4x3> NativeNodeCandidates(List<RoadRibbon> ribbons,EdgeStyle style)
        {
            var result=new List<Bezier4x3>();
            foreach(var ribbon in ribbons)
            {
                if(ribbon.HasLeftCurb)result.Add(InsetCurb(ribbon,true,style));
                if(ribbon.HasRightCurb)result.Add(InsetCurb(ribbon,false,style));
            }
            return result;
        }
    }
}
