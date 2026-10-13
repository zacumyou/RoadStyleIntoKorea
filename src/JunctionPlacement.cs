using System.Collections.Generic;
using Colossal.Mathematics;
using Game.Common;
using Game.Net;
using Unity.Entities;
using Unity.Mathematics;
namespace RoadStyleIntoKorea
{
    public partial class StyleSystem
    {
        readonly Dictionary<Entity,Bezier4x3> crossingTrafficPlans=new Dictionary<Entity,Bezier4x3>();
        readonly Dictionary<Entity,Bezier4x3> crossingPlans=new Dictionary<Entity,Bezier4x3>();
        Bezier4x3 CrosswalkCurve(Entity lane)
        {
            if(!StyleCrossing(lane))return CurveOf(lane);
            if(crossingPlans.TryGetValue(lane,out var curve))return curve;
            curve=PlanCrosswalkCurve(lane,out int size);crossingSizes[lane]=size;crossingPlans[lane]=curve;return curve;
        }
        readonly Dictionary<Entity,int> crossingSizes=new Dictionary<Entity,int>();
        float CrossingTargetClearance(Entity road,Entity node,float3 outward)
        {
            if(EntityManager.HasComponent<CrossingDistanceChoice>(node))return AppliedCrossingDistanceValue(node);
 if(IsNarrowRoad(road))return 0;
            float target=S.CrosswalkClearance;
            if(target<=0||!EntityManager.HasBuffer<ConnectedEdge>(node))return target;
            foreach(var connection in EntityManager.GetBuffer<ConnectedEdge>(node,true))
            {
                var other=connection.m_Edge;if(other==road||!IsRoad(other)||!EntityManager.HasComponent<Curve>(other))continue;
                var edge=EntityManager.GetComponentData<Edge>(other);
                var direction=Geometry.Tangent(CurveOf(other),edge.m_Start==node?0:1);
                float dot=math.abs(math.dot(direction,outward));if(dot>.7f)continue;
                float width=0;var prefab=Prefab(other);
                if(EntityManager.HasComponent<Game.Prefabs.NetGeometryData>(prefab))width=EntityManager.GetComponentData<Game.Prefabs.NetGeometryData>(prefab).m_DefaultWidth;
                if(EntityManager.HasComponent<Composition>(other))
                {
                    var composition=EntityManager.GetComponentData<Composition>(other).m_Edge;
                    if(EntityManager.HasComponent<Game.Prefabs.NetCompositionData>(composition))width=EntityManager.GetComponentData<Game.Prefabs.NetCompositionData>(composition).m_Width;
                }
                target=math.max(target,MarkingPolicy.JunctionClearance(S.CrosswalkClearance,width,math.sqrt(1-dot*dot)));
            }
            return target;
        }
        Bezier4x3 PlanCrosswalkCurve(Entity lane,out int selectedSize,int requestedOverride=0)
        {
            var original=CurveOf(lane);int requested=requestedOverride>0?requestedOverride:RequestedCrossingSize(lane);
            selectedSize=requested;
            if(requestedOverride==0)crossingTrafficPlans[lane]=original;
            if(!S.Crosswalks)return original;
            bool explicitChoice=requestedOverride>0||HasCrosswalkOverride(lane);
            var road=CrossingRoad(lane,out bool start);
            if(road==Entity.Null||TransitionCrossing(lane))return original;
            var edge=EntityManager.GetComponentData<Edge>(road);var node=start?edge.m_Start:edge.m_End;
            if(!EntityManager.HasComponent<Node>(node))return original;
            var origin=EntityManager.GetComponentData<Node>(node).m_Position;
            var arc=new Geometry.Arc(CurveOf(road));var mid=Geometry.Position(original,.5f);float nearest=arc.Nearest(mid);
            float sign=start?1:-1;var outward=Geometry.Tangent(arc.Curve,nearest)*sign;
            float target=CrossingTargetClearance(road,node,outward);
            bool manualDistance=EntityManager.HasComponent<CrossingDistanceChoice>(node);
            float approachEnd=manualDistance?CrossingApproachEnd(road,start,lane,arc):arc.Length;
            bool canMove=CanRelocateCrosswalk(lane,node);
            var nodeData=EntityManager.HasComponent<NodeLane>(lane)?EntityManager.GetComponentData<NodeLane>(lane):default;
            var rawCuts=new List<Bounds1>();
            if(EntityManager.HasBuffer<CutRange>(lane))foreach(var cut in EntityManager.GetBuffer<CutRange>(lane,true))rawCuts.Add(cut.m_CurveDelta);
            int minimum=explicitChoice?requested:CrosswalkStylePolicy.Minimum(requested,S.AutoCrosswalkSize);
            for(int size=requested;size>=minimum;size--)
            {
                float half=CrosswalkStylePolicy.Width(size)*.5f;
                float near=math.min(math.dot(original.a-origin,outward),math.dot(original.d-origin,outward))-half*math.abs(math.dot(Geometry.Left(Geometry.Tangent(original,.5f)),outward));
                float desired=EntityManager.HasComponent<CrossingDistanceChoice>(node)?math.max(0,target-near):IsNarrowRoad(road)?.5f:(IsGuideJunction(node)&&S.CrosswalkClearance>0?math.max(0,target-near):0);
                if(explicitChoice)desired=math.max(desired,CrosswalkStylePolicy.Setback(Width(lane)+math.cmin(nodeData.m_WidthOffset),half*2,math.dot(Geometry.Left(Geometry.Tangent(original,.5f)),outward)));
                float currentFromNode=start?arc.Distance(nearest):arc.Length-arc.Distance(nearest);
                float delta=canMove?(manualDistance?MarkingPolicy.CrosswalkAvailableShift(desired,currentFromNode,approachEnd,RoadStopGap(road),half):MarkingPolicy.CrosswalkShift(desired,currentFromNode,arc.Length,RoadStopGap(road),half,HasOppositeApproach(road,start))):0;
                // A small safe retreat is useful even when the full requested setback cannot fit.
                while(delta>.05f&&math.dot(outward,Geometry.Tangent(arc.Curve,arc.T(arc.Distance(nearest)+sign*delta))*sign)<=(manualDistance?.866f:.985f))delta=math.max(0,delta-.5f);
                float at=arc.Distance(nearest)+sign*delta;float fromNode=start?at:arc.Length-at;
                bool room=desired<=.05f||delta>.05f;
                bool visible=MarkingPolicy.VisibleFraction(MarkingPolicy.CrosswalkCuts(rawCuts,Width(lane)+nodeData.m_WidthOffset,half*2))>=.35f;
                if((!room||!visible)&&size>minimum)continue;
                selectedSize=size;
                var shift=arc.At(at)-Geometry.Position(arc.Curve,nearest);
                bool relocate=canMove&&room&&delta>.05f&&delta<=12&&math.length(shift)<=12&&at>=0&&at<=arc.Length;
                var placed=relocate?new Bezier4x3(original.a+shift,original.b+shift,original.c+shift,original.d+shift):original;
                if(requestedOverride==0)crossingTrafficPlans[lane]=placed; // orientation affects paint, not pedestrian endpoints
                var direction=Geometry.Tangent(arc.Curve,arc.T(math.clamp((relocate?at:arc.Distance(nearest))+sign*(half+RoadStopGap(road)),0,arc.Length)));
                var aligned=explicitChoice&&!manualDistance&&!HighwayRoad(road)?placed:CrossingAlignmentPolicy.Align(placed,direction);
                return aligned;
            }
            return original;
        }
        void AlignStopLines(Entity owner,Record record,List<Drive> drives,bool draw=true)
        {
            if(draw&&(EntityManager.HasBuffer<ManualDecalChoice>(owner)||HasColorGuide(owner)))manualDirty.Add(owner);
            var roadArc=new Geometry.Arc(CurveOf(owner));var lanes=Lanes(owner);
            var centerCurves=new Dictionary<bool,List<Bezier4x3>>();bool divided=CenterDivided(owner);
            var used=new HashSet<Drive>();
            foreach(var seed in drives)
            {
                if(seed.Stop<0||used.Contains(seed))continue;
                var forward=seed.Arc.Direction(seed.Stop);var left=Geometry.Left(forward);
                var candidates=drives.FindAll(d=>d.Stop>=0&&!used.Contains(d)&&d.End==seed.End&&d.Crossing==seed.Crossing&&math.dot(d.Arc.Direction(d.Stop),forward)>.8f);
                candidates.Sort((a,b)=>math.dot(b.Arc.At(b.Stop),left).CompareTo(math.dot(a.Arc.At(a.Stop),left)));
                while(candidates.Count>0)
                {
                    var group=new List<Drive>{candidates[0]};candidates.RemoveAt(0);
                    while(candidates.Count>0)
                    {
                        var previous=group[group.Count-1];var next=candidates[0];
                        float gap=math.abs(math.dot(previous.Arc.At(previous.Stop)-next.Arc.At(next.Stop),left));
                        if(gap>(previous.Width+next.Width)*.5f+1)break; // no line across a median
                        group.Add(next);candidates.RemoveAt(0);
                    }
                    foreach(var d in group)used.Add(d);
                    var first=group[0];var normal=math.normalizesafe(new float3(first.Arc.Direction(first.Stop).x,0,first.Arc.Direction(first.Stop).z));
                    if(first.Crossing!=Entity.Null)normal=CrossingAlignmentPolicy.StopNormal(CrosswalkCurve(first.Crossing),normal);
                    var across=Geometry.Left(normal);var origin=first.Arc.At(first.Stop);
                    // Move the common plane upstream when needed to keep every lane clear.
                    float retreat=0;
                    foreach(var d in group)retreat=math.min(retreat,math.dot(d.Arc.At(d.Stop)-origin,normal));
                    origin+=normal*retreat;
                    var middle=Geometry.Position(first.Curve,.5f);float roadT=roadArc.Nearest(middle);
                    bool side=math.dot(middle-Geometry.Position(roadArc.Curve,roadT),Geometry.Left(Geometry.Tangent(roadArc.Curve,roadT)))>=0;
                    if(!divided)side=true;
                    if(!centerCurves.TryGetValue(side,out var centers))centerCurves[side]=centers=StopCenterCurves(owner,lanes,side);
                    bool limited=StopCenterPolicy.Tip(centers,first.Curve.d,out var tip);
                    if(limited)
                    {
                        float centerRetreat=StopCenterPolicy.Retreat(origin,normal,tip);
                        origin+=normal*centerRetreat;
                    }
                    var stops=new List<float>();bool fits=true;
                    foreach(var d in group)
                    {
                        if(!d.Arc.IntersectPlane(origin,normal,out float at)||at<.5f||at>d.Stop+.1f){fits=false;break;}
                        stops.Add(at);
                    }
                    if(!fits)
                    {
                        foreach(var d in group)if(S.StopLines&&draw){
                            var p=d.Arc.At(d.Stop);var f=d.Arc.Direction(d.Stop);var sideVector=Geometry.Left(f)*d.Width*.5f;
                            int before=record.Paint.Count;
                            foreach(var bar in StopCenterPolicy.ClearBar(Geometry.Line(p-sideVector,p+sideVector),StopBarCenters(owner,lanes)))PaintLane(owner,record,assets.Get("NA Car Stop Line"),bar);
                            d.StopPainted=record.Paint.Count>before;
                        }
                        continue;
                    }
                    float min=float.MaxValue,max=float.MinValue;float3 right=default,leftEnd=default;
                    for(int i=0;i<group.Count;i++)
                    {
                        var d=group[i];d.Stop=stops[i];var p=d.Arc.At(d.Stop);
                        float lateral=math.dot(p-origin,across);
                        float half=d.Width*.5f/math.max(.5f,math.abs(math.dot(Geometry.Left(d.Arc.Direction(d.Stop)),across)));
                        if(lateral-half<min){min=lateral-half;right=origin+across*min;right.y=p.y;}
                        if(lateral+half>max){max=lateral+half;leftEnd=origin+across*max;leftEnd.y=p.y;}
                    }
                    if(S.StopLines&&draw){int before=record.Paint.Count;foreach(var bar in StopCenterPolicy.ClearBar(Geometry.Line(right,leftEnd),StopBarCenters(owner,lanes)))PaintLane(owner,record,assets.Get("NA Car Stop Line"),bar);foreach(var d in group)d.StopPainted=record.Paint.Count>before;}
                }
            }
        }
        IEnumerable<Bezier4x3> DividerBeforeStops(Entity road,Bezier4x3 curve)
        {
            var pieces=new List<Bezier4x3>{curve};if(!IsRoad(road))return pieces;
            var bars=new HashSet<Entity>();
            if(records.TryGetValue(road,out var main))foreach(var e in main.Paint)bars.Add(e);
            if(approachStopRecords.TryGetValue(road,out var stops))foreach(var e in stops.Paint)bars.Add(e);
            var center=new Geometry.Arc(CurveOf(road));
            foreach(var e in bars){
                if(!Live(e)||!EntityManager.HasComponent<Curve>(e)||assets.Name(Prefab(e)).IndexOf("Stop Line",System.StringComparison.OrdinalIgnoreCase)<0)continue;
                var bar=CurveOf(e);float t=center.Nearest(Geometry.Position(bar,.5f));var forward=Geometry.Tangent(center.Curve,t)*(t<.5f?-1:1);
                var clipped=new List<Bezier4x3>();foreach(var piece in pieces)clipped.AddRange(StopCenterPolicy.BeforeBar(piece,bar,forward));pieces=clipped;
            }
            return pieces;
        }
        internal void CycleEdgeBrush()
        {
            if(S==null)return;
            S.Brush=(EdgeStyle)MarkingPolicy.NextEdgeBrush((int)S.Brush);
            Status="가장자리 브러시 / Edge brush: "+S.Brush;
        }
    }
}
