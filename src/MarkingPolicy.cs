using Game.Net;
using System.Collections.Generic;
using Colossal.Mathematics;
using Unity.Mathematics;

namespace RoadStyleIntoKorea
{
    internal static class MarkingPolicy
    {
        internal static bool SmallJunction(IList<int> counts){if(counts.Count<3)return false;foreach(int n in counts)if(n<1||n>2)return false;return true;}
        internal static bool TransportText(string name)=>name=="NA LaneMarkings Bus Taxi"||name=="EU LaneMarkings Bus Taxi";
        internal static int InheritCenter(int value,int other)=>value==0?other:value;
        internal static int DefaultCrossingSize(int roadLanes,int fallback)=>roadLanes>0&&roadLanes<=2?CrosswalkStylePolicy.SingleThree:fallback;
        internal static bool RefreshDue(float now,float first,float last,bool urgent=false)=>urgent||now-last>=.15f||now-first>=.5f;
        internal static bool OnApproachSide(float3 crossing,float3 node,float3 outward)
            =>math.dot(crossing-node,outward)>.5f;
        internal static float VisibleFraction(IEnumerable<Bounds1> cuts)
        {float covered=0;foreach(var cut in cuts)covered+=cut.max-cut.min;return math.saturate(1-covered);}
        internal static bool OverlappingCrossings(Bezier4x3 a,Bezier4x3 b,float aw,float bw)
        {
            var direction=Geometry.Tangent(a,.5f);var am=Geometry.Position(a,.5f);var bm=Geometry.Position(b,.5f);
            if(math.abs(am.y-bm.y)>1||math.abs(math.dot(direction,Geometry.Tangent(b,.5f)))<.97f)return false;
            if(math.abs(math.dot(bm-am,Geometry.Left(direction)))>math.min(aw,bw)*.5f)return false;
            float a0=math.dot(a.a,direction),a1=math.dot(a.d,direction),b0=math.dot(b.a,direction),b1=math.dot(b.d,direction);
            float overlap=math.min(math.max(a0,a1),math.max(b0,b1))-math.max(math.min(a0,a1),math.min(b0,b1));
            return overlap>math.max(math.abs(a1-a0),math.abs(b1-b0))*.8f;
        }
        internal static bool SameCrossing(Bezier4x3 a,Bezier4x3 b)
        {
            // Full-span duplicates, including reversed pedestrian directions; not split half crossings.
            bool ends=(math.distance(a.a,b.a)<1&&math.distance(a.d,b.d)<1)||(math.distance(a.a,b.d)<1&&math.distance(a.d,b.a)<1);
            return ends&&math.distance(Geometry.Position(a,.5f),Geometry.Position(b,.5f))<1;
        }
        internal static bool SameApproach(Bezier4x3 a,Bezier4x3 b)
        {
            var direction=Geometry.Tangent(a,0);var delta=b.a-a.a;
            return math.dot(direction,Geometry.Tangent(b,0))>.94f&&math.abs(delta.y)<2&&math.abs(math.dot(delta,direction))<4&&math.abs(math.dot(delta,Geometry.Left(direction)))<24;
        }
        internal static bool GuidePrecedes(Bezier4x3 candidate,Bezier4x3 other,bool lowerID)
        {
            if(!SameApproach(candidate,other))return false;
            float lateral=math.dot(other.a-candidate.a,Geometry.Left(Geometry.Tangent(candidate,0)));
            if(lateral>.25f)return true;
            if(math.abs(lateral)>=.25f||math.dot(Geometry.Tangent(candidate,1),Geometry.Tangent(other,1))<=.94f)return false;
            float exit=math.dot(other.d-candidate.d,Geometry.Left(Geometry.Tangent(candidate,1)));
            return exit>.25f||(math.abs(exit)<.25f&&lowerID);
        }
        internal static List<Bounds1> VisibleCrosswalkCuts(IEnumerable<Bounds1> source,float2 widths,float target)
        {
            var original=new List<Bounds1>(source);var scaled=CrosswalkCuts(original,widths,target);
            float covered=0;foreach(var cut in scaled)covered+=cut.max-cut.min;
            // Do not turn a visible native crossing into an entirely clipped replacement.
            if(covered>.9f)return CrosswalkCuts(original,new float2(target),target);
            return scaled;
        }
        // Unsafe crossings are routing paths, not evidence of a painted crosswalk.
        internal static bool IsMarkedCrosswalk(PedestrianLaneFlags flags, bool hidden, bool hiddenByUs)
            => (flags & (PedestrianLaneFlags.Crosswalk | PedestrianLaneFlags.Unsafe)) == PedestrianLaneFlags.Crosswalk
               && (!hidden || hiddenByUs);

        internal static bool HasJunction(int distinctRoadEdges) => distinctRoadEdges >= 3;
        internal static bool DrawLeftGuide(CarLaneFlags flags,float angle,float minimum,bool controlled=false)
            =>((flags&CarLaneFlags.Highway)==0||controlled)
              &&(flags&(CarLaneFlags.Forbidden|CarLaneFlags.SideConnection|CarLaneFlags.UTurnLeft|CarLaneFlags.UTurnRight))==0
              &&angle+.01f>=math.max(45,minimum);
        internal static bool IsLaneDivider(string name)
            =>name.IndexOf("Car Lane Line",System.StringComparison.OrdinalIgnoreCase)>=0
            ||name.IndexOf("Car Merge Line",System.StringComparison.OrdinalIgnoreCase)>=0
            ||name.IndexOf("Highway Lane Line",System.StringComparison.OrdinalIgnoreCase)>=0
            ||name.IndexOf("Highway Merge Line",System.StringComparison.OrdinalIgnoreCase)>=0;
        internal static bool IsGuideDivider(string name)=>IsLaneDivider(name)||name.IndexOf("Guide Line",System.StringComparison.OrdinalIgnoreCase)>=0;
        internal static bool IsNativeJunctionMark(string name)=>IsJunctionPaint(name)||IsGuideDivider(name)
            ||name.IndexOf("Highway Edge Line",System.StringComparison.OrdinalIgnoreCase)>=0
            ||name.IndexOf("Car Edge Line",System.StringComparison.OrdinalIgnoreCase)>=0
            ||name.IndexOf("Car Yield Line",System.StringComparison.OrdinalIgnoreCase)>=0
            ||name.IndexOf("Car Stop Line",System.StringComparison.OrdinalIgnoreCase)>=0;
        internal static bool IsJunctionPaint(string name)
            => IsLaneDivider(name)
            ||name.IndexOf("Car Center Line",System.StringComparison.OrdinalIgnoreCase)>=0
            ||name.IndexOf("Highway Center Line",System.StringComparison.OrdinalIgnoreCase)>=0;
        internal static float StopBeforeCrosswalk(float inner,float requested,float reserve)
        {
            float gap=math.clamp(requested,.5f,8);
            return inner-gap>=reserve?inner-gap:inner-math.min(gap,1);
        }
        internal static float3 StopPlane(float3 crossing,float3 towardCrossing,float width,float gap)
            =>crossing-towardCrossing*(math.max(0,width)*.5f+math.clamp(gap,.5f,8));
        internal static bool StopOverlapsCrosswalk(Bezier4x3 stop,Bezier4x3 crossing,float width){
            var arc=new Geometry.Arc(crossing);
            for(int i=0;i<=8;i++){
                var p=Geometry.Position(stop,i/8f);float t=arc.Nearest(p);var q=Geometry.Position(crossing,t);
                if(math.abs(p.y-q.y)>2||math.abs(math.dot(Geometry.Tangent(stop,i/8f),Geometry.Tangent(crossing,t)))<.8f)continue;
                var delta=p-q;var along=math.abs(math.dot(delta,Geometry.Tangent(crossing,t)));
                if(along<=.25f&&math.distance(p.xz,q.xz)<=width*.5f+.25f)return true;
            }
            return false;
        }
        internal static int NextEdgeBrush(int style)=>style<2||style>=5?2:style+1;
        internal static bool HasSetbackRoom(float fromNode,float roadLength,float gap,float halfWidth=4.12f)
            =>fromNode>=0&&fromNode+halfWidth+math.max(3,gap)+6<=roadLength*.5f;
        internal static float CrosswalkShift(float desired,float currentFromNode,float roadLength,float gap,float halfWidth,bool oppositeApproach=true)
            =>math.max(0,math.min(math.min(desired,12),roadLength*(oppositeApproach?.5f:1f)-halfWidth-math.max(3,gap)-6-currentFromNode));
        // Manual distance uses the actual opposite crossing boundary, not half a segment.
        internal static float CrosswalkAvailableShift(float desired,float current,float end,float gap,float half)
            =>math.max(0,math.min(math.min(desired,12),end-half-math.max(3,gap)-current));
        internal static float JunctionClearance(float requested,float crossingRoadWidth,float angleSin)
            =>requested<=0?0:math.max(requested,math.min(requested+4,crossingRoadWidth*.5f/math.max(.7f,angleSin)+2));

        // The artwork has fixed 4 m bars. Original sidewalk width offsets must
        // not shrink its 8.2 m two-row footprint to the vanilla crossing width.
        internal static NodeLane CrosswalkNode(NodeLane source)
        {
            source.m_WidthOffset=0;
            source.m_Flags&=~(NodeLaneFlags.StartWidthOffset|NodeLaneFlags.EndWidthOffset);
            return source;
        }

        internal static List<Bounds1> CrosswalkEndMargins(Bezier4x3 curve,List<Bounds1> cuts)
        {
            var arc=new Geometry.Arc(curve);float a=0,b=1;
            foreach(var cut in cuts){if(cut.min<=0)a=math.max(a,cut.max);if(cut.max>=1)b=math.min(b,cut.min);}
            float begin=arc.Distance(a)+.3f,end=arc.Distance(b)-.3f;
            var result=new List<Bounds1>(cuts);
            if(begin>=end)return new List<Bounds1>{new Bounds1(0,1)};
            result.Add(new Bounds1(0,arc.T(begin)));result.Add(new Bounds1(arc.T(end),1));
            return CrosswalkCuts(result,new float2(1),1);
        }
        internal static List<Bounds1> CrosswalkCuts(IEnumerable<Bounds1> source,float2 sourceWidths,float targetWidth)
        {
            var ranges=new List<Bounds1>();
            foreach(var cut in source)
            {
                float a=math.saturate(cut.min),b=math.saturate(cut.max);
                if(!math.isfinite(a)||!math.isfinite(b)||b<=a)continue;
                // Native cuts already locate the curb along this crossing. Widening our two-row
                // artwork must not multiply that longitudinal setback (especially at bend nodes).
                if(a<=0 && sourceWidths.x>0.001f)b=math.saturate(b*math.min(1,targetWidth/sourceWidths.x));
                if(b>=1 && a>0 && sourceWidths.y>0.001f)a=1-math.saturate((1-a)*math.min(1,targetWidth/sourceWidths.y));
                ranges.Add(new Bounds1(a,b));
            }
            ranges.Sort((a,b)=>a.min.CompareTo(b.min));
            var merged=new List<Bounds1>();
            foreach(var cut in ranges)
            {
                if(merged.Count==0||cut.min>merged[merged.Count-1].max)merged.Add(cut);
                else {var last=merged[merged.Count-1];last.max=math.max(last.max,cut.max);merged[merged.Count-1]=last;}
            }
            return merged;
        }
    }
}

