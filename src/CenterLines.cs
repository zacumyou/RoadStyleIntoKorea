using System;using System.Collections.Generic;using Colossal.Mathematics;using Colossal.Serialization.Entities;using Game.Net;using Game.Prefabs;using Unity.Entities;using Unity.Mathematics;using UnityEngine;
using CarLane=Game.Net.CarLane;using ParkingLane=Game.Net.ParkingLane;using SecondaryLane=Game.Net.SecondaryLane;
namespace RoadStyleIntoKorea
{
    public struct CenterChoice:IComponentData,ISerializable
    {
        public int Left,Right;
        public void Serialize<T>(T w) where T:IWriter{w.Write(1);w.Write(Left);w.Write(Right);}
        public void Deserialize<T>(T r) where T:IReader{r.Read(out int v);r.Read(out Left);r.Read(out Right);if(v!=1){Left=Right=0;}Left=math.clamp(Left,0,5);Right=math.clamp(Right,0,5);}
    }
    public partial class StyleSystem
    {
        sealed class CenterPath{internal Bezier4x3 Curve,Original;internal bool Left,Shifted;internal Entity Source;internal Geometry.Arc Arc;}
        bool CenterDivided(Entity edge)
        {
            if(!EntityManager.HasComponent<Composition>(edge))return false;
            var cp=EntityManager.GetComponentData<Composition>(edge).m_Edge;
            if(!EntityManager.HasBuffer<NetCompositionPiece>(cp))return false;
            foreach(var p in EntityManager.GetBuffer<NetCompositionPiece>(cp,true))
                if((p.m_SectionFlags&NetSectionFlags.Median)!=0&&(p.m_SectionFlags&(NetSectionFlags.Underground|NetSectionFlags.Hidden|NetSectionFlags.Overhead))==0&&(p.m_PieceFlags&NetPieceFlags.BlockTraffic)!=0&&p.m_Size.x>.05f)return true;
            return false;
        }
        List<CenterPath> CenterPaths(Entity edge,List<Entity> lanes,EdgeStyle style)
        {
            var result=new List<CenterPath>();bool divided=CenterDivided(edge);var center=new Geometry.Arc(CurveOf(edge));
            if(!divided)
            {
                foreach(var lane in lanes)if(EntityManager.HasComponent<SecondaryLane>(lane)&&!EntityManager.HasComponent<CarLane>(lane)&&UTurnPolicy.CenterLine(assets.Name(Prefab(lane))))
                    result.Add(new CenterPath{Curve=CurveOf(lane),Source=lane});
                if(result.Count==0&&!HasOpposingTraffic(edge))return result;
                if(result.Count==0&&EntityManager.HasComponent<EdgeGeometry>(edge)&&EntityManager.HasComponent<Composition>(edge))
                {
                    var cp=EntityManager.GetComponentData<Composition>(edge).m_Edge;
                    if(EntityManager.HasComponent<NetCompositionData>(cp)&&EntityManager.HasBuffer<NetCompositionLane>(cp))
                    {
                        float min=float.MaxValue,max=float.MinValue,height=0;int count=0;
                        foreach(var lane in EntityManager.GetBuffer<NetCompositionLane>(cp,true))
                        {
                            if((lane.m_Flags&LaneFlags.Road)==0||(lane.m_Flags&(LaneFlags.Master|LaneFlags.Parking))!=0||!EntityManager.HasComponent<NetLaneData>(lane.m_Lane))continue;
                            float half=EntityManager.GetComponentData<NetLaneData>(lane.m_Lane).m_Width*.5f;
                            min=math.min(min,lane.m_Position.x-half);max=math.max(max,lane.m_Position.x+half);height+=lane.m_Position.y;count++;
                        }
                        float width=EntityManager.GetComponentData<NetCompositionData>(cp).m_Width;
                        if(count>0&&width>0){var g=EntityManager.GetComponentData<EdgeGeometry>(edge);float t=CenterLinePolicy.RoadCenter(min,max,width);
                            result.Add(new CenterPath{Curve=Between(g.m_Start.m_Left,g.m_Start.m_Right,t,height/count)});
                            result.Add(new CenterPath{Curve=Between(g.m_End.m_Left,g.m_End.m_Right,t,height/count)});}
                    }
                }
                return result;
            }
            foreach(var lane in lanes)
            {
                if(!EntityManager.HasComponent<CarLane>(lane)||EntityManager.HasComponent<MasterLane>(lane)||EntityManager.HasComponent<ParkingLane>(lane))continue;
                var flags=EntityManager.GetComponentData<CarLane>(lane).m_Flags;
                if((flags&CarLaneFlags.LeftLimit)==0||(flags&CarLaneFlags.SideConnection)!=0)continue;
                var c=CurveOf(lane);var mid=Geometry.Position(c,.5f);float t=center.Nearest(mid);
                bool left=math.dot(mid-Geometry.Position(center.Curve,t),Geometry.Left(Geometry.Tangent(center.Curve,t)))>=0;
                var original=CenterLinePolicy.MedianSide(c,Width(lane),CenterLinePolicy.HalfWidth((int)style));
                float extra=InnerEmptyWidth(edge,c,Width(lane));
                result.Add(new CenterPath{Curve=CenterLinePolicy.MedianSide(c,Width(lane)+extra*2,CenterLinePolicy.HalfWidth((int)style)),Original=original,Shifted=extra>.001f,Left=left});
            }
            return result;
        }
        List<Bezier4x3> CenterVisible(Entity edge,CenterPath path,EdgeStyle style,List<Entity> lanes)
        {
            foreach(var plan in centerPaintPlans)if(plan.Owner==edge&&plan.Style==style&&plan.Clearance==S.PaintClearance&&Geometry.SameCurve(plan.Source,path.Curve))return plan.Parts;
            var result=new List<Bezier4x3>();var crossings=Crossings(edge,lanes);
            var approaches=new List<Bezier4x3>(EdgeApproachPolicy.Clip(path.Curve,EdgeApproachLimits(edge,CenterLinePolicy.HalfWidth((int)style))));
            if(approaches.Count==0)approaches.Add(path.Curve);
            foreach(var approach in approaches)
                foreach(var road in RoadOnlyEdge(edge,style,approach,paintHalf:CenterLinePolicy.HalfWidth((int)style)))
                    foreach(var clear in ClearPaint(road,crossings,S.PaintClearance,edge))result.Add(clear);
            if(centerPaintPlans.Count>=128)centerPaintPlans.Clear();
            centerPaintPlans.Add(new EdgePaintPlan{Owner=edge,Style=style,Clearance=S.PaintClearance,Source=path.Curve,Parts=result});
            return result;
        }
        readonly List<EdgePaintPlan> centerPaintPlans=new List<EdgePaintPlan>();
        void ClearLinePlans(){edgePaintPlans.Clear();centerPaintPlans.Clear();roadRibbonFrameCache.Clear();sidewalkFrameCache.Clear();}
        void ApplyCenterLines(Entity edge,Record record,List<Entity> lanes)
        {
            if(!EntityManager.HasComponent<CenterChoice>(edge))return;
            var choice=EntityManager.GetComponentData<CenterChoice>(edge);bool divided=CenterDivided(edge);
            foreach(bool left in new[]{true,false})
            {
                if(!divided&&!left)continue;
                int code=left?choice.Left:choice.Right;if(code==0)continue;var style=(EdgeStyle)(code-1);
                var paintedPaths=new List<Colossal.Mathematics.Bezier4x3>();
                foreach(var path in CenterPaths(edge,lanes,style))
                {
                    if(divided&&path.Left!=left)continue;
                    bool duplicate=paintedPaths.Exists(c=>SameCenterPath(c,path.Curve));
                    if(duplicate){HideCenterOriginals(path,lanes,record);continue;}paintedPaths.Add(path.Curve);
                    int before=record.Paint.Count;
                    if(style!=EdgeStyle.None||EntityManager.HasComponent<UTurnChoice>(edge))foreach(var c in CenterVisible(edge,path,style,lanes))PaintCenterZone(edge,record,style==EdgeStyle.None?Entity.Null:assets.Get(CenterLinePolicy.Asset((int)style)),c);
                    if(style!=EdgeStyle.None&&before==record.Paint.Count&&NearbyCrossings(edge).Count==0)continue;
                    HideCenterOriginals(path,lanes,record);
                }
            }
        }
        static bool SameCenterPath(Bezier4x3 a,Bezier4x3 b){
            if(math.distancesq(a.a,b.d)<.0001f)b=Geometry.Reverse(b);
            return math.distancesq(a.a,b.a)<.0001f&&math.distancesq(a.b,b.b)<.0001f&&math.distancesq(a.c,b.c)<.0001f&&math.distancesq(a.d,b.d)<.0001f;
        }
        void HideCenterOriginals(CenterPath path,List<Entity> lanes,Record record)
        {
            if(path.Source!=Entity.Null)Hide(path.Source,record);
            else {
            var reference=path.Shifted?path.Original:path.Curve;
            var arc=new Geometry.Arc(reference);
            foreach(var original in lanes)
            {
                var name=assets.Name(Prefab(original));if(!EntityManager.HasComponent<SecondaryLane>(original)||(!UTurnPolicy.CenterLine(name)&&name.IndexOf("Edge Line",StringComparison.OrdinalIgnoreCase)<0))continue;
                var p=Geometry.Position(CurveOf(original),.5f);
                if(math.distance(p,Geometry.Position(reference,arc.Nearest(p)))<.6f)Hide(original,record);
            }
            }
        }
        internal void CycleCenterBrush(){S.CenterBrush=(EdgeStyle)CenterLinePolicy.Next((int)S.CenterBrush);InvalidateToolPreview();}
        Entity centerPreviewEdge;int centerPreviewRevision=-1;EdgeStyle centerPreviewBrush;
        readonly List<CenterPath> centerPreview=new List<CenterPath>();
        internal bool DrawCenterPreview(Entity edge,float3 hit,Game.Rendering.OverlayRenderSystem.Buffer buffer)
        {
            if(centerPreviewEdge!=edge||centerPreviewRevision!=toolRevision||centerPreviewBrush!=S.CenterBrush)
            {
                centerPreviewEdge=edge;centerPreviewRevision=toolRevision;centerPreviewBrush=S.CenterBrush;centerPreview.Clear();
                var lanes=Lanes(edge);foreach(var path in CenterPaths(edge,lanes,S.CenterBrush))foreach(var c in CenterVisible(edge,path,S.CenterBrush,lanes))centerPreview.Add(new CenterPath{Curve=c,Left=path.Left,Arc=new Geometry.Arc(c)});
            }
            bool selected=false;float best=float.MaxValue;
            foreach(var p in centerPreview){float d=math.distancesq(Geometry.Position(p.Curve,p.Arc.Nearest(hit)),hit);if(d<best){best=d;selected=p.Left;}}
            bool divided=CenterDivided(edge);foreach(var p in centerPreview)if(CenterLinePolicy.Select(divided,S.CenterSide==ToolSide.BothSides,selected,p.Left))buffer.DrawCurve(S.Enabled?EdgeStyleTool.BrushColor(S.CenterBrush,.7f):new Color(1,.2f,.2f,.7f),p.Curve,math.max(.25f,CenterLinePolicy.HalfWidth((int)S.CenterBrush)*2));
            return selected;
        }
        internal void ChooseCenter(Entity edge,bool left,bool reset)
        {
            if(!S.Enabled||!IsRoad(edge)||!CachedAssetsReady())return;
            if(S.CenterBrush!=EdgeStyle.None){var asset=assets.Get(CenterLinePolicy.Asset((int)S.CenterBrush));if(asset==Entity.Null||!EntityManager.HasComponent<NetLaneGeometryData>(asset)){Status="중앙선 에셋 누락 / Center line asset missing";return;}}
            var lanes=Lanes(edge);if(CenterPaths(edge,lanes,S.CenterBrush).Count==0){Status="중앙선 대상이 없습니다 / No center line target";return;}
            var choice=EntityManager.HasComponent<CenterChoice>(edge)?EntityManager.GetComponentData<CenterChoice>(edge):default;
            int value=reset?0:(int)S.CenterBrush+1;bool both=!CenterDivided(edge)||S.CenterSide==ToolSide.BothSides;
            if((!both&&(left?choice.Left:choice.Right)==value)||(both&&choice.Left==value&&choice.Right==value))return;
            if(reset)ToolResetHandled=true;
            if(both||left)choice.Left=value;if(both||!left)choice.Right=value;
            if(!EntityManager.HasComponent<CenterChoice>(edge))EntityManager.AddComponent<CenterChoice>(edge);EntityManager.SetComponentData(edge,choice);
            if(EntityManager.HasBuffer<ManualDecalChoice>(edge))manualDirty.Add(edge);EnqueueCenterOnly(edge);var ends=EntityManager.GetComponentData<Edge>(edge);EnqueueCenterOnly(ends.m_Start);EnqueueCenterOnly(ends.m_End);InvalidateToolPreview();Status="중앙선 변경 대기 / Center line change queued";
        }
    }
}
