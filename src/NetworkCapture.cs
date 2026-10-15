using System;
using System.IO;
using System.Text;
using System.Collections.Generic;
using Colossal.Mathematics;
using Game.Net;
using Game.Prefabs;
using Unity.Entities;
using Unity.Mathematics;
namespace RoadStyleIntoKorea
{
    public partial class StyleSystem
    {
        internal int CaptureScope;
        readonly WorkQueue<Entity> captureOwners=new WorkQueue<Entity>();
        string capturePath;
        StringBuilder captureText;
        internal void CycleCaptureScope(){CaptureScope=(CaptureScope+1)%3;}
        internal void CaptureNetworks(IEnumerable<Entity> selection)
        {
            if(captureOwners.Count>0){Status="진단 기록 중입니다 / Capture already running";return;}
            sideRoadCaptureCount=0;
            var roots=new List<Entity>();var seen=new HashSet<Entity>();
            foreach(var road in selection)if(IsRoad(road)&&seen.Add(road))roots.Add(road);
            if(roots.Count==0)return;
            var camera=World.GetExistingSystemManaged<Game.Rendering.CameraUpdateSystem>();
            float3 focus=camera?.activeCameraController!=null?camera.activeCameraController.pivot:float3.zero;
            roots.Sort((a,b)=>LoadDistance(a,focus).CompareTo(LoadDistance(b,focus)));
            try{
                string directory=Path.Combine(UnityEngine.Application.persistentDataPath,"Logs","RoadStyleIntoKorea","captures");Directory.CreateDirectory(directory);
                capturePath=Path.Combine(directory,"network-"+DateTime.UtcNow.ToString("yyyyMMdd-HHmmss-fffffff")+".log");
                if(assets.Named.Count==0)assets.Refresh(EntityManager,World.GetExistingSystemManaged<PrefabSystem>());
                // Selected segments always fit first; only optional graph expansion is capped.
                foreach(var road in roots)captureOwners.Enqueue(road);
                int radius=CaptureScope==0?1:CaptureScope==1?2:6;
                foreach(var road in roots){foreach(var owner in CaptureGraph.Collect(road,radius,64,CaptureNeighbors))if(seen.Count<1024&&seen.Add(owner))captureOwners.Enqueue(owner);if(seen.Count>=1024)break;}
                File.WriteAllText(capturePath,$"RSIK {typeof(Mod).Assembly.GetName().Version} UTC={DateTime.UtcNow:O} scope={CaptureScope} selectedSegments={roots.Count} owners={seen.Count} cap=1024\nCapture is incremental. Selected segments are recorded first in camera-distance order.\n",new UTF8Encoding(false));
                Status="도로 진단 기록 시작 / Capture started: "+capturePath;
            }catch(Exception error){captureOwners.Clear();Status="진단 저장 실패 / Capture failed: "+error.Message;}
        }
        internal void CaptureNetwork(Entity edge,float3 hit)
        {
            if(captureOwners.Count>0){Status="진단 기록 중입니다 / Capture already running";return;}
            sideRoadCaptureCount=0;
            bool nodeTarget=Live(edge)&&EntityManager.HasComponent<Node>(edge)&&EntityManager.HasBuffer<ConnectedEdge>(edge);
            if(!IsRoad(edge)&&!nodeTarget)return;
            try
            {
                string directory=Path.Combine(UnityEngine.Application.persistentDataPath,"Logs","RoadStyleIntoKorea","captures");
                Directory.CreateDirectory(directory);
                capturePath=Path.Combine(directory,"network-"+DateTime.UtcNow.ToString("yyyyMMdd-HHmmss-fffffff")+".log");
                if(assets.Named.Count==0)assets.Refresh(EntityManager,World.GetExistingSystemManaged<PrefabSystem>());
                Entity root=edge;
                if(!nodeTarget&&CaptureScope==1)
                {
                    var ends=EntityManager.GetComponentData<Edge>(edge);
                    float a=math.distancesq(hit,EntityManager.GetComponentData<Node>(ends.m_Start).m_Position);
                    float b=math.distancesq(hit,EntityManager.GetComponentData<Node>(ends.m_End).m_Position);
                    root=a<b?ends.m_Start:ends.m_End;
                }
                int radius=CaptureScope==0?1:CaptureScope==1?2:6;
                var owners=CaptureGraph.Collect(root,radius,64,CaptureNeighbors);
                foreach(var e in owners)captureOwners.Enqueue(e);
                File.WriteAllText(capturePath,$"RSIK {typeof(Mod).Assembly.GetName().Version} UTC={DateTime.UtcNow:O} scope={CaptureScope} root={edge} hit={hit} owners={owners.Count} cap=64 connectedRadius=3-road-hops\n"+
                    $"gap={S.EdgeLaneGap} brush={S.Brush} syncTraffic={S.SyncTraffic} stopGap={S.StopGap} enabled={S.Enabled}\nCapture is incremental; timestamps and pending/updated flags identify unstable topology.\n",new UTF8Encoding(false));
                Status="도로 진단 기록 시작 / Capture started: "+capturePath;
            }
            catch(Exception error){captureOwners.Clear();Status="진단 저장 실패 / Capture failed: "+error.Message;}
        }
        IEnumerable<Entity> CaptureNeighbors(Entity owner)
        {
            if(!Live(owner))yield break;
            if(EntityManager.HasComponent<Edge>(owner))
            {var e=EntityManager.GetComponentData<Edge>(owner);if(Live(e.m_Start))yield return e.m_Start;if(Live(e.m_End))yield return e.m_End;}
            else if(EntityManager.HasBuffer<ConnectedEdge>(owner))foreach(var e in EntityManager.GetBuffer<ConnectedEdge>(owner,true))if(IsRoad(e.m_Edge))yield return e.m_Edge;
        }
        void CaptureComponent<T>(Entity e) where T:unmanaged,IComponentData
        {if(EntityManager.HasComponent<T>(e))captureText.AppendLine(typeof(T).Name+" "+CaptureFields.Fields(EntityManager.GetComponentData<T>(e)));}
        void CaptureCurve(string label,Bezier4x3 c)=>captureText?.AppendLine(label+$" a={c.a} b={c.b} c={c.c} d={c.d}");
        void CaptureNextOwner()
        {
            if(captureOwners.Count==0)return;
            var owner=captureOwners.Dequeue();captureText=new StringBuilder();
            try
            {
                captureText.AppendLine($"OWNER {owner} utc={DateTime.UtcNow:O} exists={Live(owner)} pending={pending.Contains(owner)}");
                if(Live(owner))
                {
                    CaptureComponent<Edge>(owner);CaptureComponent<Node>(owner);CaptureComponent<Composition>(owner);
                    if(EntityManager.HasComponent<Composition>(owner))
                    {
                        var cp=EntityManager.GetComponentData<Composition>(owner);
                        foreach(var composition in new[]{cp.m_Edge,cp.m_StartNode,cp.m_EndNode})
                        {
                            captureText.AppendLine("COMPOSITION "+composition);CaptureComponent<NetCompositionData>(composition);
                            if(EntityManager.HasBuffer<NetCompositionPiece>(composition))foreach(var piece in EntityManager.GetBuffer<NetCompositionPiece>(composition,true))
                                captureText.AppendLine("PIECE "+PieceName(piece.m_Piece)+" "+CaptureFields.Fields(piece));
                            if(EntityManager.HasBuffer<NetCompositionLane>(composition))foreach(var lane in EntityManager.GetBuffer<NetCompositionLane>(composition,true))
                                captureText.AppendLine("COMPOSITION_LANE "+CaptureFields.Fields(lane));
                        }
                    }
                    if(EntityManager.HasComponent<StartNodeGeometry>(owner))
                    {var g=EntityManager.GetComponentData<StartNodeGeometry>(owner).m_Geometry;captureText.AppendLine($"START_SYNC left={g.m_SyncVertexTargetsLeft} right={g.m_SyncVertexTargetsRight}");CaptureCurve("START_LL",g.m_Left.m_Left);CaptureCurve("START_LR",g.m_Left.m_Right);CaptureCurve("START_RL",g.m_Right.m_Left);CaptureCurve("START_RR",g.m_Right.m_Right);}
                    if(EntityManager.HasComponent<EndNodeGeometry>(owner))
                    {var g=EntityManager.GetComponentData<EndNodeGeometry>(owner).m_Geometry;captureText.AppendLine($"END_SYNC left={g.m_SyncVertexTargetsLeft} right={g.m_SyncVertexTargetsRight}");CaptureCurve("END_LL",g.m_Left.m_Left);CaptureCurve("END_LR",g.m_Left.m_Right);CaptureCurve("END_RL",g.m_Right.m_Left);CaptureCurve("END_RR",g.m_Right.m_Right);}
                    CaptureComponent<UTurnChoice>(owner);CaptureComponent<ApproachChoice>(owner);CaptureComponent<CrosswalkChoice>(owner);CaptureComponent<CenterChoice>(owner);CaptureComponent<EdgeChoice>(owner);CaptureComponent<Game.Common.Updated>(owner);
                    if(EntityManager.HasBuffer<PavingChoice>(owner))foreach(var choice in EntityManager.GetBuffer<PavingChoice>(owner,true))captureText.AppendLine("PAVING_CHOICE "+CaptureFields.Fields(choice));
                    if(EntityManager.HasBuffer<SideRoadChoice>(owner))foreach(var choice in EntityManager.GetBuffer<SideRoadChoice>(owner,true))captureText.AppendLine("SIDE_ROAD_CHOICE "+CaptureFields.Fields(choice));
                    CaptureSideRoadGeometry(owner);
                    if(EntityManager.HasBuffer<GutterChoice>(owner))foreach(var gutter in EntityManager.GetBuffer<GutterChoice>(owner,true))captureText.AppendLine($"GUTTER_CHOICE kind={gutter.Kind} style={gutter.Style} min={gutter.Min} max={gutter.Max}");
                    if(EntityManager.HasBuffer<CurbChoice>(owner))foreach(var choice in EntityManager.GetBuffer<CurbChoice>(owner,true))
                        captureText.AppendLine($"CURB_CHOICE kind={choice.Kind} min={choice.Min} max={choice.Max}");
                    if(IsRoad(owner))foreach(var region in PavingRegions(owner))if(CurbRepresentative(region)&&CurbSelected(owner,region))
                        captureText.AppendLine($"CURB_SURFACE piece={PieceName(region.Piece.m_Piece)} kind={region.Kind} preserveOriginal={region.Kind==1} pieceHeight={region.Height} nativeTop={CurbHeight(region)} min={region.PaintMin} max={region.PaintMax} lift=0 nativeProfile=Sidewalk6 band=0.6 alignment=outside-paving preservePavingWidth=True rise=0.2 nativeMaterial=RoadParts");
                    if(curbRecords.TryGetValue(owner,out var curbRecord))captureText.AppendLine($"CURB_RENDER surfaces={curbRecord.PavingSurfaces.Count}");
                    foreach(var set in new[]{records,approachRecords,approachStopRecords,manualRecords,guideRecords,curbRecords})if(set.TryGetValue(owner,out var diagnosticRecord)){
                        foreach(var hidden in diagnosticRecord.Hidden)captureText.AppendLine($"OWNED_HIDDEN entity={hidden} live={Live(hidden)} hidden={EntityManager.HasComponent<Game.Tools.Hidden>(hidden)} marker={EntityManager.HasComponent<OriginalPaintHidden>(hidden)}");
                        int alive=0,missing=0;foreach(var paint in diagnosticRecord.Paint){if(Live(paint)){alive++;var ci=EntityManager.HasComponent<Game.Rendering.CullingInfo>(paint)?EntityManager.GetComponentData<Game.Rendering.CullingInfo>(paint):default;int batchCount=EntityManager.HasBuffer<Game.Rendering.MeshBatch>(paint)?EntityManager.GetBuffer<Game.Rendering.MeshBatch>(paint,true).Length:-1;captureText.AppendLine($"PAINT_STATE cullingIndex={ci.m_CullingIndex} passedCulling={ci.m_PassedCulling} batchCount={batchCount} entity={paint} hidden={EntityManager.HasComponent<Game.Tools.Hidden>(paint)} intentional={removedPaint.Contains(paint)||suppressedWarnings.Contains(paint)||hiddenOwners.Contains(paint)} created={EntityManager.HasComponent<Game.Common.Created>(paint)} culling={EntityManager.HasComponent<Game.Rendering.CullingInfo>(paint)} meshBatch={EntityManager.HasBuffer<Game.Rendering.MeshBatch>(paint)} owner={(EntityManager.HasComponent<Game.Common.Owner>(paint)?EntityManager.GetComponentData<Game.Common.Owner>(paint).m_Owner:Entity.Null)}");if(EntityManager.HasComponent<Curve>(paint)){captureText.AppendLine($"PAINT_PREFAB {paint} {assets.Name(Prefab(paint))} removed={removedPaint.Contains(paint)} suppressed={suppressedWarnings.Contains(paint)} ownedHidden={hiddenOwners.Contains(paint)}");CaptureCurve("PAINT_CURVE",CurveOf(paint));CaptureComponent<Game.Net.Elevation>(paint);}}else missing++;}
                        captureText.AppendLine($"PAINT_RECORD category={(set==records?"main":set==approachRecords?"approach":set==approachStopRecords?"stops":set==manualRecords?"manual":"guides")} expected={diagnosticRecord.Paint.Count} alive={alive} missing={missing} hidden={diagnosticRecord.Hidden.Count}");
                    }
                    if(EntityManager.HasBuffer<DrawnLineChoice>(owner))foreach(var choice in EntityManager.GetBuffer<DrawnLineChoice>(owner,true)){
                        captureText.AppendLine((RedPavingPolicy.IsRed(choice.Style)?"RED_PAVING_CHOICE ":"DRAWN_LINE_CHOICE ")+CaptureFields.Fields(choice));
                        var original=LineTransform(owner,choice.Local,false);CaptureCurve("DRAWN_WORLD",original);
                        if(!RedPavingPolicy.IsRed(choice.Style)){var crossings=NearbyCrossings(owner);float kept=0;int parts=0;foreach(var piece in ClearPaint(original,crossings,DrawnLinePolicy.IsColor(choice.Style)?.25f:.05f)){kept+=new Geometry.Arc(piece).Length;parts++;CaptureCurve("DRAWN_CLEAR",piece);}captureText.AppendLine($"DRAWN_CLIP crossings={crossings.Count} originalLength={new Geometry.Arc(original).Length} keptLength={kept} parts={parts}");}
                    }
                    if(manualRecords.TryGetValue(owner,out var redRecord))foreach(var surface in redRecord.PavingSurfaces)if(surface.Style==-100){
                        var go=surface.Object;var renderer=go!=null?go.GetComponent<UnityEngine.MeshRenderer>():null;var material=renderer!=null?renderer.sharedMaterial:null;
                        captureText.AppendLine($"RED_PAVING_RENDER object={go!=null} active={go!=null&&go.activeInHierarchy} enabled={renderer!=null&&renderer.enabled} visible={renderer!=null&&renderer.isVisible} bounds={renderer?.bounds} vertices={surface.Mesh?.vertexCount} material={material?.name} shader={material?.shader?.name} supported={material?.shader?.isSupported} queue={material?.renderQueue} layer={go?.layer}");
                    }
                    if(EntityManager.HasBuffer<ManualDecalChoice>(owner))foreach(var choice in EntityManager.GetBuffer<ManualDecalChoice>(owner,true))captureText.AppendLine("MANUAL_CHOICE "+CaptureFields.Fields(choice));
                    if(EntityManager.HasComponent<Curve>(owner))CaptureCurve("OWNER_CURVE",CurveOf(owner));
                    var ribbons=EdgeRoadRibbons(owner);
                    foreach(var ribbon in ribbons){CaptureCurve("ROAD_LEFT",ribbon.Left);CaptureCurve("ROAD_RIGHT",ribbon.Right);}
                    var lanes=Lanes(owner);int n=0;
                    foreach(var lane in lanes)
                    {
                        if(n++>=256){captureText.AppendLine("LANES_TRUNCATED limit=256");break;}
                        captureText.AppendLine($"LANE {lane} prefab={assets.Name(Prefab(lane))} width={Width(lane)} hidden={EntityManager.HasComponent<Game.Tools.Hidden>(lane)}");
                        CaptureCurve("SOURCE",CurveOf(lane));CaptureCurve("CURRENT",EntityManager.GetComponentData<Curve>(lane).m_Bezier);
                        CaptureComponent<Lane>(lane);CaptureComponent<Game.Net.CarLane>(lane);CaptureComponent<LaneSignal>(lane);
                        if(flowCurves.TryGetValue(lane,out var flow)){CaptureCurve("FLOW_ORIGINAL",flow.Original.m_Bezier);CaptureCurve("FLOW_APPLIED",flow.Applied.m_Bezier);}
                    }
                    if(IsRoad(owner))
                    {
                        var drives=Drives(lanes);FindStops(owner,lanes,drives);
                        foreach(var centerPath in CenterPaths(owner,lanes,S.CenterBrush))CaptureCurve("CENTER_CANDIDATE left="+centerPath.Left,centerPath.Curve);
                        AlignStopLines(owner,new Record(),drives,false);
                        foreach(var drive in drives)if(drive.Stop>=0)captureText.AppendLine($"STOP lane={drive.Entity} node={drive.End} sourceEnd={drive.Curve.d} desired={drive.Arc.At(drive.Stop)} distance={drive.Stop}");
                        var candidates=new List<EdgeBoundary>();
                        Edges(owner,new Record(),lanes,drives,candidates,S.Brush==EdgeStyle.Original||S.Brush==EdgeStyle.None?EdgeStyle.YellowSolid:S.Brush);
                        ClearLinePlans();
                        foreach(var candidate in candidates)
                        {
                            CaptureCurve("CANDIDATE side="+(candidate.Left?"left":"right"),candidate.Curve);
                            var parts=EdgePaintParts(owner,candidate.Style,candidate.Curve,Crossings(owner,lanes),new EdgeClearanceContext{Owner=owner,Ribbons=ribbons});
                            foreach(var part in parts)CaptureCurve("ACCEPTED",part);
                        }
                        ClearLinePlans();
                    }
                    captureText.AppendLine($"TRANSPORT_VISUALS colorHidden={S.HideTransportColor} textHidden={S.HideTransportText} patchedMaterials={transportMaterials.Count}");
                    if(EntityManager.HasComponent<Node>(owner))captureText.AppendLine("SMALL_JUNCTION_GUIDES_BLOCKED="+SmallRoadJunction(owner));
                    if(EntityManager.HasComponent<Node>(owner)){JoinEdgeLines(owner,new Record(),true);JoinCenterEnds(owner,new Record(),true);}
                    if(records.TryGetValue(owner,out var record))foreach(var paint in record.Paint)
                        if(Live(paint)&&EntityManager.HasComponent<Curve>(paint))CaptureCurve("GENERATED "+assets.Name(Prefab(paint)),EntityManager.GetComponentData<Curve>(paint).m_Bezier);
                }
                foreach(var collection in new[]{approachRecords,approachStopRecords})if(collection.TryGetValue(owner,out var approachRecord))foreach(var paint in approachRecord.Paint)
                    if(Live(paint)&&EntityManager.HasComponent<Curve>(paint))CaptureCurve("APPROACH_GENERATED "+assets.Name(Prefab(paint)),EntityManager.GetComponentData<Curve>(paint).m_Bezier);
                    else if(Live(paint)&&EntityManager.HasComponent<Game.Objects.Transform>(paint))captureText.AppendLine("APPROACH_DECAL "+assets.Name(Prefab(paint))+" "+CaptureFields.Fields(EntityManager.GetComponentData<Game.Objects.Transform>(paint)));
                foreach(var collection in new[]{guideRecords,manualRecords})if(collection.TryGetValue(owner,out var extra))foreach(var paint in extra.Paint)
                    if(Live(paint)&&EntityManager.HasComponent<Curve>(paint))CaptureCurve("AUX_GENERATED "+assets.Name(Prefab(paint)),EntityManager.GetComponentData<Curve>(paint).m_Bezier);
                if(records.TryGetValue(owner,out var pavingRecord))foreach(var surface in pavingRecord.PavingSurfaces)
                {
                    var mesh=surface.Mesh;var points=mesh.vertices;var uv=mesh.uv;var normals=mesh.normals;
                    captureText.AppendLine($"PAVING_MESH vertices={points.Length} triangles={mesh.triangles.Length/3} bounds={mesh.bounds}");
                    for(int i=0;i<Math.Min(points.Length,2048);i++)captureText.AppendLine($"PAVING_VERTEX {i} position={surface.Object.transform.TransformPoint(points[i]).ToString("R",System.Globalization.CultureInfo.InvariantCulture)} uv={uv[i].ToString("R",System.Globalization.CultureInfo.InvariantCulture)} normal={normals[i].ToString("R",System.Globalization.CultureInfo.InvariantCulture)}");
                    if(points.Length>2048)captureText.AppendLine("PAVING_VERTICES_TRUNCATED");
                }
                if(captureOwners.Count==0)captureText.AppendLine("CAPTURE_COMPLETE");
                if(new FileInfo(capturePath).Length>16*1024*1024)
                {captureOwners.Clear();File.AppendAllText(capturePath,"CAPTURE_TRUNCATED size limit 16MiB\n");Status="진단 용량 제한 / Capture truncated: "+capturePath;return;}
                File.AppendAllText(capturePath,captureText.ToString(),new UTF8Encoding(false));
                Status=captureOwners.Count==0?"진단 저장 완료 / Capture saved: "+capturePath:"진단 기록 중 / Owners remaining: "+captureOwners.Count;
            }
            catch(Exception error){captureOwners.Clear();Status="진단 실패 / Capture failed: "+error.Message;}
            finally{captureText=null;}
        }
    }
}
