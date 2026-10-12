using System.Collections.Generic;
using Colossal.Mathematics;
using Unity.Mathematics;
namespace RoadStyleIntoKorea
{
    internal sealed class EdgeTaperPolicy
    {
        struct Band { internal float Min,Max;internal bool Left; }
        readonly Geometry.Arc center;
        readonly List<Band> bands=new List<Band>();
        internal EdgeTaperPolicy(Bezier4x3 road){center=new Geometry.Arc(road);}
        internal void Add(Bezier4x3 boundary)
        {
            var arc=new Geometry.Arc(boundary);int steps=math.max(1,(int)math.ceil(arc.Length/.5f));
            // Curved parallel cubics can fluctuate slightly under nearest-point
            // projection. A taper must have a meaningful width change overall.
            var samples=new List<float3>();float minWidth=float.MaxValue,maxWidth=float.MinValue;
            for(int i=0;i<=steps;i++)
            {
                var p=arc.At(arc.Length*i/steps);float t=center.Nearest(p);
                float width=math.dot(p-Geometry.Position(center.Curve,t),Geometry.Left(Geometry.Tangent(center.Curve,t)));
                                var forward=Geometry.Tangent(center.Curve,t);
                float beyond=math.dot(p-Geometry.Position(center.Curve,t),forward);
                bool valid=!(t<.0001f&&beyond<-.05f||t>.9999f&&beyond>.05f)
                    &&math.abs(math.dot(Geometry.Tangent(boundary,arc.T(arc.Length*i/steps)),forward))>.5f;
                samples.Add(new float3(center.Distance(t),width,valid?1:0));
                if(valid){minWidth=math.min(minWidth,width);maxWidth=math.max(maxWidth,width);}
            }
            if(maxWidth-minWidth<.3f)return;
            float previousAlong=0,previousWidth=0;
            for(int i=0;i<=steps;i++)
            {
                float along=samples[i].x,width=samples[i].y;
                if(i>0&&samples[i].z>0&&samples[i-1].z>0&&math.abs(width-previousWidth)>math.max(.025f,math.abs(along-previousAlong)*.05f))
                    bands.Add(new Band{Min=math.min(along,previousAlong)-1,Max=math.max(along,previousAlong)+1,Left=width+previousWidth>=0});
                previousAlong=along;previousWidth=width;
            }
        }
        internal bool Clear(float3 point)
        {
            if(bands.Count==0)return true;
            float t=center.Nearest(point),along=center.Distance(t);
            bool left=math.dot(point-Geometry.Position(center.Curve,t),Geometry.Left(Geometry.Tangent(center.Curve,t)))>=0;
            foreach(var b in bands)if(b.Left==left&&along>=b.Min&&along<=b.Max)return false;
            return true;
        }
        internal static float OutsideOffset(float laneWidth,float halfWidth,float gap)=>math.max(0,laneWidth)*.5f+math.max(.075f,halfWidth)+math.clamp(gap,0,1);
        internal static bool SameLateralPosition(float3 a,float3 b,float3 roadForward)=>math.abs(math.dot(b-a,Geometry.Left(roadForward)))<=.15f;
    }
}

