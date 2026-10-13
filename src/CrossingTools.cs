using System.Collections.Generic;
using Colossal.Mathematics;
using Colossal.Serialization.Entities;
using Game.Common;
using Game.Net;
using Game.Rendering;
using Game.Tools;
using Unity.Entities;
using Unity.Mathematics;
namespace RoadStyleIntoKorea
{
    public partial class StyleSystem
    {
        readonly Dictionary<Entity,(Entity Road,bool Start)> crossingRoadCache=new Dictionary<Entity,(Entity,bool)>();
        int crossingRoadRevision=-1;
        Entity CrossingRoad(Entity cross,out bool start){
            if(crossingRoadRevision!=toolRevision){crossingRoadCache.Clear();crossingRoadRevision=toolRevision;}
            if(crossingRoadCache.TryGetValue(cross,out var cached)){start=cached.Start;return cached.Road;}
            var road=FindCrossingRoad(cross,out start);crossingRoadCache[cross]=(road,start);return road;
        }
        Entity FindCrossingRoad(Entity cross,out bool start)
        {
            start=false;if(!EntityManager.HasComponent<Owner>(cross))return Entity.Null;
            var owner=EntityManager.GetComponentData<Owner>(cross).m_Owner;
            var c=CurveOf(cross);var mid=Geometry.Position(c,.5f);
            var edges=new List<Entity>();
            if(IsRoad(owner))edges.Add(owner);
            else if(EntityManager.HasBuffer<ConnectedEdge>(owner))
                foreach(var e in EntityManager.GetBuffer<ConnectedEdge>(owner,true))if(IsRoad(e.m_Edge))edges.Add(e.m_Edge);
            bool nodeOwned=EntityManager.HasComponent<Node>(owner);
            float best=float.MaxValue;Entity road=Entity.Null;
            foreach(var e in edges)
            {
                if(!EntityManager.HasComponent<Curve>(e))continue;
                var a=new Geometry.Arc(CurveOf(e));float t=a.Nearest(mid);
                float alignment=math.dot(Geometry.Tangent(c,.5f),Geometry.Tangent(a.Curve,t));
                float approach=0;
                if(nodeOwned){
                    var edge=EntityManager.GetComponentData<Edge>(e);
                    var origin=EntityManager.GetComponentData<Node>(owner).m_Position;
                    var outward=Geometry.Tangent(a.Curve,edge.m_Start==owner?0:1)*(edge.m_Start==owner?1:-1);
                    approach=math.dot(mid-origin,outward);
                }
                if(!CrossingOwnerPolicy.Accept(edges.Count,nodeOwned,alignment,approach))continue;
                float d=math.distancesq(mid,Geometry.Position(a.Curve,t));
                if(d<best-.001f||math.abs(d-best)<=.001f&&(road==Entity.Null||e.Index<road.Index)){best=d;road=e;}
            }
            if(road!=Entity.Null){var e=EntityManager.GetComponentData<Edge>(road);start=EntityManager.HasComponent<Node>(owner)?e.m_Start==owner:math.distancesq(mid,EntityManager.GetComponentData<Node>(e.m_Start).m_Position)<math.distancesq(mid,EntityManager.GetComponentData<Node>(e.m_End).m_Position);}
            return road;
        }
        internal bool ResolveCrosswalkTarget(Entity target,float3 hit,out Entity edge,out Entity crossing,out bool atStart){
            edge=IsRoad(target)?target:Entity.Null;crossing=Entity.Null;atStart=false;float best=36;
            if(!Live(target))return false;
            foreach(var lane in NearbyCrossings(target)){
                var road=CrossingRoad(lane,out bool start);if(road==Entity.Null)continue;
                foreach(var curve in new[]{CurveOf(lane),CrosswalkCurve(lane)}){
                    var arc=new Geometry.Arc(curve);var p=Geometry.Position(curve,arc.Nearest(hit));if(math.abs(p.y-hit.y)>3)continue;
                    float distance=math.distancesq(p.xz,hit.xz);if(distance<best){best=distance;edge=road;crossing=lane;atStart=start;}
                }
            }
            if(crossing!=Entity.Null)return true;
            if(edge==Entity.Null&&EntityManager.HasBuffer<ConnectedEdge>(target)){
                float distance=float.MaxValue;
                foreach(var link in EntityManager.GetBuffer<ConnectedEdge>(target,true)){
                    var candidate=link.m_Edge;if(!IsRoad(candidate))continue;
                    bool candidateStart=EntityManager.GetComponentData<Edge>(candidate).m_Start==target;
                    if(!TryNewCrosswalkPreview(candidate,candidateStart,out var proposed))continue;
                    var arc=new Geometry.Arc(proposed);var point=Geometry.Position(proposed,arc.Nearest(hit));if(math.abs(point.y-hit.y)>3)continue;
                    float d=math.distancesq(point.xz,hit.xz);if(d<distance){distance=d;edge=candidate;atStart=candidateStart;}
                }
                return edge!=Entity.Null;
            }
            if(edge==Entity.Null)return false;
            atStart=math.distancesq(hit,CurveOf(edge).a)<math.distancesq(hit,CurveOf(edge).d);return true;
        }
        int RequestedCrossingSize(Entity lane)
        {
            var road=CrossingRoad(lane,out bool start);
            if(road!=Entity.Null&&EntityManager.HasComponent<CrosswalkChoice>(road))
            {var c=EntityManager.GetComponentData<CrosswalkChoice>(road);int size=start?c.Start:c.End;if(CrosswalkStylePolicy.Valid(size))return size;}
            if(road!=Entity.Null&&EntityManager.HasComponent<Composition>(road))
            {
                var cp=EntityManager.GetComponentData<Composition>(road).m_Edge;
                int count=0;
                if(EntityManager.HasBuffer<Game.Prefabs.NetCompositionLane>(cp))
                    foreach(var laneData in EntityManager.GetBuffer<Game.Prefabs.NetCompositionLane>(cp,true))
                        if((laneData.m_Flags&Game.Prefabs.LaneFlags.Road)!=0&&
                           (laneData.m_Flags&(Game.Prefabs.LaneFlags.Master|Game.Prefabs.LaneFlags.Secondary|Game.Prefabs.LaneFlags.Parking|Game.Prefabs.LaneFlags.BicyclesOnly|Game.Prefabs.LaneFlags.Virtual))==0)count++;
                return MarkingPolicy.DefaultCrossingSize(count,(int)S.DefaultCrosswalk);
            }
            return (int)S.DefaultCrosswalk;
        }
        int CrossingSize(Entity lane){CrosswalkCurve(lane);return crossingSizes.TryGetValue(lane,out int size)?size:RequestedCrossingSize(lane);}
        bool HasCrosswalkOverride(Entity lane){
            var road=CrossingRoad(lane,out bool start);
            if(road==Entity.Null||!EntityManager.HasComponent<CrosswalkChoice>(road))return false;
            var choice=EntityManager.GetComponentData<CrosswalkChoice>(road);
            return CrosswalkStylePolicy.Valid(start?choice.Start:choice.End);
        }
        bool StyleCrossing(Entity lane)=>S.Crosswalks&&(active||HasCrosswalkOverride(lane)||HasCrossingDistance(lane));
        bool TransitionCrossing(Entity lane){
            if(!EntityManager.HasComponent<Owner>(lane))return false;
            var node=EntityManager.GetComponentData<Owner>(lane).m_Owner;
            if(!SafeNode(node))return false;
            int count=0;foreach(var item in EntityManager.GetBuffer<ConnectedEdge>(node,true))if(IsRoad(item.m_Edge))count++;
            return count==2;
        }
        bool SharedTransitionCrossing(Entity road,Entity other,Entity node){
            if(other==Entity.Null||!SafeNode(node))return false;
            int count=0;foreach(var e in EntityManager.GetBuffer<ConnectedEdge>(node,true))if(IsRoad(e.m_Edge))count++;
            if(count!=2||!IsRoad(other))return false;
            var a=EntityManager.GetComponentData<Edge>(road);var b=EntityManager.GetComponentData<Edge>(other);
            if(b.m_Start!=node&&b.m_End!=node)return false;
            return a.m_Start==node||a.m_End==node;
        }
        void RefreshCrosswalkDependents(Entity edge,Entity node){
            EnqueueCrossOnly(edge);EnqueueCrossOnly(node);RefreshCrosswalkApproach(edge);EnqueueCenterOnly(node);
            if(EntityManager.HasBuffer<ConnectedEdge>(node))foreach(var e in EntityManager.GetBuffer<ConnectedEdge>(node,true))
                if(e.m_Edge!=edge&&SharedTransitionCrossing(edge,e.m_Edge,node)){EnqueueCrossOnly(e.m_Edge);RefreshCrosswalkApproach(e.m_Edge);}
        }
        void RefreshCrosswalkApproach(Entity road){
            if((EntityManager.HasBuffer<CurbChoice>(road)||EntityManager.HasBuffer<GutterChoice>(road))){EnqueueCurb(road);EnqueuePaving(road);var curbEnds=EntityManager.GetComponentData<Edge>(road);EnqueuePaving(curbEnds.m_Start);EnqueuePaving(curbEnds.m_End);}
            if(EntityManager.HasComponent<EdgeChoice>(road)){EnqueueEdgeOnly(road);var ends=EntityManager.GetComponentData<Edge>(road);EnqueueEdgeOnly(ends.m_Start);EnqueueEdgeOnly(ends.m_End);}
            QueueBoundaryGuides(road);
            if(EntityManager.HasComponent<CenterChoice>(road)||EntityManager.HasComponent<UTurnChoice>(road))EnqueueCenterOnly(road);
            if(EntityManager.HasComponent<ApproachChoice>(road)&&EntityManager.GetComponentData<ApproachChoice>(road).Enabled)approachPending.Add(road);
        }
        float CrossingWidth(Entity lane)=>StyleCrossing(lane)?CrosswalkStylePolicy.Width(CrossingSize(lane)):math.max(.5f,Width(lane));
        readonly Dictionary<Entity,List<Entity>> nearbyFrameCache=new Dictionary<Entity,List<Entity>>();
        readonly Dictionary<Entity,Geometry.Arc> crossingArcFrameCache=new Dictionary<Entity,Geometry.Arc>();
        int nearbyFrame=-1,nearbyRevision=-1;
        void CheckCrossingFrameCache(){if(nearbyFrame==UnityEngine.Time.frameCount&&nearbyRevision==toolRevision)return;nearbyFrame=UnityEngine.Time.frameCount;nearbyRevision=toolRevision;nearbyFrameCache.Clear();crossingArcFrameCache.Clear();boundaryFrameCache.Clear();}
        Geometry.Arc CrossingArc(Entity lane){CheckCrossingFrameCache();var c=CrosswalkCurve(lane);if(!crossingArcFrameCache.TryGetValue(lane,out var arc)||!Geometry.SameCurve(arc.Curve,c))crossingArcFrameCache[lane]=arc=new Geometry.Arc(c);return arc;}
        List<Entity> NearbyCrossings(Entity owner)
        {
            CheckCrossingFrameCache();if(nearbyFrameCache.TryGetValue(owner,out var cached))return cached;
            var owners=new HashSet<Entity>{owner};
            if(EntityManager.HasComponent<Edge>(owner)){var e=EntityManager.GetComponentData<Edge>(owner);owners.Add(e.m_Start);owners.Add(e.m_End);}
            else if(EntityManager.HasBuffer<ConnectedEdge>(owner))foreach(var e in EntityManager.GetBuffer<ConnectedEdge>(owner,true))owners.Add(e.m_Edge);
            foreach(var node in new List<Entity>(owners))if(EntityManager.HasBuffer<ConnectedEdge>(node))
                foreach(var e in EntityManager.GetBuffer<ConnectedEdge>(node,true))if(IsRoad(e.m_Edge))owners.Add(e.m_Edge);
            var result=new List<Entity>();var seen=new HashSet<Entity>();
            foreach(var o in owners)if(Live(o)&&EntityManager.HasBuffer<SubLane>(o))
                foreach(var l in EntityManager.GetBuffer<SubLane>(o,true))if(IsCrosswalk(l.m_SubLane)&&EntityManager.HasComponent<Curve>(l.m_SubLane)&&seen.Add(l.m_SubLane))result.Add(l.m_SubLane);
            nearbyFrameCache[owner]=result;return result;
        }
        readonly Dictionary<Entity,Bezier4x3> crossingSpans=new Dictionary<Entity,Bezier4x3>();
        int crossingSpanRevision=-1;
        Bezier4x3 CrossingSpan(Entity lane){
            if(crossingSpanRevision!=toolRevision){crossingSpans.Clear();crossingSpanRevision=toolRevision;}
            if(crossingSpans.TryGetValue(lane,out var span))return span;
            span=CrosswalkCurve(lane);var cuts=new List<Bounds1>();FullRoadCrosswalk(lane,ref span,ref cuts);crossingSpans[lane]=span;return span;
        }
        Entity CanonicalCrossing(Entity lane)
        {
            if(!EntityManager.HasComponent<Owner>(lane))return lane;
            Entity best=lane;var c=CrossingSpan(lane);var road=CrossingRoad(lane,out bool start);
            foreach(var other in NearbyCrossings(EntityManager.GetComponentData<Owner>(lane).m_Owner))
            {
                if(other.Index>=best.Index||!EntityManager.HasComponent<Game.Prefabs.NetLaneGeometryData>(Prefab(other)))continue;
                var otherRoad=CrossingRoad(other,out bool otherStart);
                if(otherRoad!=road||otherStart!=start||StyleCrossing(other)!=StyleCrossing(lane))continue;
                if(MarkingPolicy.SameCrossing(c,CrossingSpan(other))||
                   (road!=Entity.Null&&road==otherRoad&&start==otherStart&&MarkingPolicy.OverlappingCrossings(c,CrossingSpan(other),CrossingWidth(lane),CrossingWidth(other))))best=other;
            }
            return best;
        }
        Entity PickCrosswalk(Entity edge,float3 hit,out bool atStart)
        {
            Entity selected=Entity.Null;atStart=false;float best=float.MaxValue;
            foreach(var lane in NearbyCrossings(edge))
            {
                var road=CrossingRoad(lane,out bool start);if(road!=edge)continue;
                var a=new Geometry.Arc(CrosswalkCurve(lane));float t=a.Nearest(hit);float distance=math.distancesq(Geometry.Position(a.Curve,t),hit);
                if(distance<best){best=distance;selected=lane;atStart=start;}
            }
            return selected;
        }
        sealed class CrossingPreview
        {
            internal Geometry.Arc Pick;internal float Width;internal bool Start;internal readonly List<Bezier4x3> Parts=new List<Bezier4x3>();
        }
        Entity previewEdge;int previewRevision=-1;bool previewCrosswalk;
        EdgeStyle previewStyle;float previewInset,previewClearance,previewStopGap,previewCrossClearance;
        CrosswalkSize previewCrossSize;bool previewAuto,previewSync;
        readonly List<EdgeBoundary> previewCurves=new List<EdgeBoundary>();
        readonly List<CrossingPreview> previewCrossings=new List<CrossingPreview>();
        internal void DrawMarkingPreview(Entity edge,float3 hit,bool crosswalk,bool left,Game.Rendering.OverlayRenderSystem.Buffer buffer,UnityEngine.Color color)
        {
            if(S==null)return;
            var style=S.Brush==EdgeStyle.Original||S.Brush==EdgeStyle.None?EdgeStyle.WhiteSolid:S.Brush;
            bool changed=previewEdge!=edge||previewRevision!=toolRevision||previewCrosswalk!=crosswalk||previewStyle!=style||
                previewInset!=S.EdgeLaneGap||previewClearance!=S.PaintClearance||previewStopGap!=S.StopGap||previewCrossClearance!=S.CrosswalkClearance||
                previewCrossSize!=S.CrosswalkBrush||previewAuto!=S.AutoCrosswalkSize||previewSync!=S.SyncTraffic;
            // Reuse the displayed result while queued updates settle; no duplicate
            // expensive preview reconstruction in the same frame as road rebuilds.
            if(changed&&queue.Count==0)
            {
                var timer=System.Diagnostics.Stopwatch.StartNew();
                previewEdge=edge;previewRevision=toolRevision;previewCrosswalk=crosswalk;previewStyle=style;
                previewInset=S.EdgeLaneGap;previewClearance=S.PaintClearance;previewStopGap=S.StopGap;previewCrossClearance=S.CrosswalkClearance;
                previewCrossSize=S.CrosswalkBrush;previewAuto=S.AutoCrosswalkSize;previewSync=S.SyncTraffic;
                previewCurves.Clear();previewCrossings.Clear();
                if(crosswalk)
                {
                    foreach(var lane in NearbyCrossings(edge))
                    {
                        if(CrossingRoad(lane,out bool start)!=edge)continue;
                        var entry=new CrossingPreview{Pick=new Geometry.Arc(CrosswalkCurve(lane)),Start=start};
                        var curve=PlanCrosswalkCurve(lane,out int size,(int)S.CrosswalkBrush);entry.Width=CrosswalkStylePolicy.Width(size);
                        var node=EntityManager.HasComponent<NodeLane>(lane)?EntityManager.GetComponentData<NodeLane>(lane):default;
                        var raw=new List<Bounds1>();
                        if(EntityManager.HasBuffer<CutRange>(lane))foreach(var cut in EntityManager.GetBuffer<CutRange>(lane,true))raw.Add(cut.m_CurveDelta);
                        var cuts=MarkingPolicy.VisibleCrosswalkCuts(raw,Width(lane)+node.m_WidthOffset,entry.Width);
                        bool fullWidth=FullRoadCrosswalk(lane,ref curve,ref cuts);
                        cuts=CrosswalkSurfaceCuts(lane,curve,cuts,entry.Width);
                        if(!CrosswalkStripePolicy.Fit(curve,cuts,out curve,out cuts,fullWidth))continue;
                        float cursor=0;
                        foreach(var cut in cuts){if(cut.min>cursor)entry.Parts.Add(Geometry.Cut(curve,cursor,cut.min));cursor=math.max(cursor,cut.max);}
                        if(cursor<1)entry.Parts.Add(Geometry.Cut(curve,cursor,1));
                        previewCrossings.Add(entry);
                    }
                }
                else
                {
                    var lanes=Lanes(edge);var paths=new List<EdgeBoundary>();
                    Edges(edge,null,lanes,Drives(lanes),paths,style);
                    var crossings=Crossings(edge,lanes);var context=new EdgeClearanceContext{Owner=edge};
                    foreach(var path in paths)
                        foreach(var clear in EdgePaintParts(edge,style,path.Curve,crossings,context))
                            previewCurves.Add(new EdgeBoundary{Curve=clear,Left=path.Left,Style=style});
                }
            }
            if(previewEdge!=edge||previewCrosswalk!=crosswalk)return;
            if(crosswalk)
            {
                CrossingPreview selected=null;float best=float.MaxValue;
                ResolveCrosswalkTarget(edge,hit,out _,out _,out bool atStart);
                foreach(var entry in previewCrossings)
                {if(entry.Start!=atStart)continue;float t=entry.Pick.Nearest(hit);float distance=math.distancesq(Geometry.Position(entry.Pick.Curve,t),hit);if(distance<best){best=distance;selected=entry;}}
                if(selected!=null)foreach(var curve in selected.Parts)buffer.DrawCurve(color,curve,selected.Width,new float2(0));
                else if(TryNewCrosswalkPreview(edge,atStart,out var proposed))buffer.DrawCurve(color,proposed,CrosswalkStylePolicy.Width((int)S.CrosswalkBrush),new float2(0));
                else buffer.DrawCircle(color,hit+new float3(0,.03f,0),.75f);
            }
            else foreach(var path in previewCurves)
                if(S.Side==ToolSide.BothSides||path.Left==left)buffer.DrawCurve(color,path.Curve,math.max(.25f,EdgePaintHalfWidth(style)*2));
        }
        internal void ChooseCrosswalk(Entity edge,float3 hit,bool reset,bool forceArrows=false,bool? forcedStart=null)
        {
            if(S==null||!S.Enabled||!S.Crosswalks||!IsRoad(edge))return;
            if(!CachedAssetsReady())return;
            Entity selected=Entity.Null;bool atStart;
            if(forcedStart.HasValue){atStart=forcedStart.Value;foreach(var lane in NearbyCrossings(edge))if(CrossingRoad(lane,out bool start)==edge&&start==atStart){selected=lane;break;}}
            else if(!ResolveCrosswalkTarget(edge,hit,out edge,out selected,out atStart))return;
            if(selected==Entity.Null&&!reset&&!QueueNativeCrossing(edge,atStart,false))return;
            var choice=EntityManager.HasComponent<CrosswalkChoice>(edge)?EntityManager.GetComponentData<CrosswalkChoice>(edge):default;
            if(reset){if((atStart?choice.Start:choice.End)==0)return;ToolResetHandled=true;QueueNativeCrossing(edge,atStart,true);}
            int value=reset?0:(int)S.CrosswalkBrush;
            if(atStart)choice.ForceStartArrows=!reset;else choice.ForceEndArrows=!reset;
            // Retry the same choice after native lane regeneration or a failed replacement.
            if(atStart)choice.Start=reset?0:(int)S.CrosswalkBrush;else choice.End=reset?0:(int)S.CrosswalkBrush;
            if(!EntityManager.HasComponent<CrosswalkChoice>(edge))EntityManager.AddComponent<CrosswalkChoice>(edge);
            EntityManager.SetComponentData(edge,choice);ClearLinePlans();crossingPlans.Clear();crossingSizes.Clear();
            crossingTrafficPlans.Clear();            // Local application must also rebuild every adjacent approach affected by this node.
            var network=EntityManager.GetComponentData<Edge>(edge);var node=atStart?network.m_Start:network.m_End;
            RefreshCrosswalkDependents(edge,node);
            Status="횡단보도 변경 대기 / Crosswalk change queued";
        }
        internal void CycleCrosswalkBrush(){S.CrosswalkBrush=(CrosswalkSize)CrosswalkStylePolicy.Next((int)S.CrosswalkBrush);Status="횡단보도 / Crosswalk: "+CrosswalkStylePolicy.Label((int)S.CrosswalkBrush,true);}
        void PaintDivider(Entity owner,Record record,Entity prefab,Bezier4x3 curve)
        {
            foreach(var safe in DividerBeforeStops(owner,curve))foreach(var piece in BoundaryPaint(owner,safe,.35f))PaintLane(owner,record,prefab,piece);
        }
        void ClipNativeDividers(Entity owner,Record record,List<Entity> lanes)
        {
            var crossings=NearbyCrossings(owner);if(crossings.Count==0)return;
            foreach(var lane in lanes)
            {
                if(EntityManager.HasComponent<ApproachChoice>(owner)&&EntityManager.GetComponentData<ApproachChoice>(owner).Enabled&&MarkingPolicy.IsLaneDivider(assets.Name(Prefab(lane))))continue;
                if(EntityManager.HasComponent<Hidden>(lane)||!EntityManager.HasComponent<SecondaryLane>(lane)||!MarkingPolicy.IsJunctionPaint(assets.Name(Prefab(lane))))continue;
                if(!EntityManager.HasComponent<Game.Prefabs.NetLaneGeometryData>(Prefab(lane)))continue;
                if(SafeNode(owner)&&EntityManager.HasBuffer<GuideChoice>(owner)&&EntityManager.GetBuffer<GuideChoice>(owner,true).Length>0&&MarkingPolicy.IsGuideDivider(assets.Name(Prefab(lane))))continue;
                var original=CurveOf(lane);var pieces=new List<Bezier4x3>(BoundaryPaint(owner,original,.35f));
                float oldLength=new Geometry.Arc(original).Length,length=0;foreach(var piece in pieces)length+=new Geometry.Arc(piece).Length;
                if(oldLength-length<.05f)continue;
                foreach(var piece in pieces)PaintLane(owner,record,Prefab(lane),piece);
                Hide(lane,record);
            }
        }
        // Exclusion is evaluated against the full painted footprint, including grade separation.
        bool NearCrossing(float3 point,List<Geometry.Arc> curves,List<float> widths,float clearance)
        {
            for(int i=0;i<curves.Count;i++)
            {
                var a=curves[i];if(!a.MayBeNear(point,widths[i]*.5f+clearance))continue;float t=a.Nearest(point);var q=Geometry.Position(a.Curve,t);
                if(math.abs(point.y-q.y)>2)continue;
                if(math.distance(point.xz,q.xz)<widths[i]*.5f+clearance)return true;
            }
            return false;
        }
        IEnumerable<Bezier4x3> ClearPaint(Bezier4x3 curve,List<Entity> crossings,float clearance,Entity edge=default)
        {
            if(crossings.Count==0){yield return curve;yield break;}
            var curves=new List<Geometry.Arc>();var widths=new List<float>();
            var planeOrigins=new List<float3>();var planeNormals=new List<float3>();
            foreach(var lane in crossings)
            {
                var arc=CrossingArc(lane);var c=arc.Curve;curves.Add(arc);widths.Add(CrossingWidth(lane));
                if(edge==Entity.Null||CrossingRoad(lane,out bool atStart)!=edge)continue;
                var roadCurve=CurveOf(edge);var center=new Geometry.Arc(roadCurve);var mid=Geometry.Position(c,.5f);
                float t=center.Nearest(mid);var outward=Geometry.Tangent(roadCurve,t)*(atStart?1:-1);
                float extent=CrossingWidth(lane)*.5f*math.abs(math.dot(Geometry.Left(Geometry.Tangent(c,.5f)),outward));
                float far=math.max(math.dot(c.a-mid,outward),math.dot(c.d-mid,outward))+extent+clearance;
                planeOrigins.Add(mid+outward*far);planeNormals.Add(outward);
            }
            // Crosswalk boundaries are hard limits, even on a short connector.
            var planes=new List<EdgeApproachPolicy.Limit>();
            for(int i=0;i<planeOrigins.Count;i++)planes.Add(new EdgeApproachPolicy.Limit{Origin=planeOrigins[i],Inward=planeNormals[i]});
            foreach(var outside in EdgeApproachPolicy.Clip(curve,planes))
                foreach(var piece in Geometry.ClearSlices(outside,p=>!NearCrossing(p,curves,widths,clearance)))yield return piece;
        }
    }
}
