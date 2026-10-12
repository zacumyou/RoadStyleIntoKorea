using System.Collections.Generic;
using Colossal.Mathematics;
using Game.Common;
using Game.Net;
using Game.Prefabs;
using Game.Tools;
using Unity.Entities;
using Unity.Mathematics;
using CarLane=Game.Net.CarLane;
using SecondaryLane=Game.Net.SecondaryLane;

namespace RoadStyleIntoKorea
{
    public partial class StyleSystem
    {
        sealed class CenterPiece
        {
            internal Entity Source,Prefab;internal Bezier4x3 Curve;
        }
        bool HasUTurn(Entity owner,Drive drive,List<Entity> roadLanes)
            =>(drive.Turns&(CarLaneFlags.UTurnLeft|CarLaneFlags.UTurnRight))!=0&&IsGuideJunction(drive.End);
        CarLaneFlags ReadDriveTurns(Entity owner,Drive drive,List<Entity> roadLanes)
        {
            if(!Live(drive.End))return 0;
            var keys=new HashSet<Game.Pathfind.PathNode>();
            foreach(var lane in roadLanes)
                if(EntityManager.HasComponent<CarLane>(lane)&&!EntityManager.HasComponent<MasterLane>(lane)&&EntityManager.HasComponent<Lane>(lane)
                   &&math.distance(CurveOf(lane).d,drive.Curve.d)<.05f)
                    keys.Add(EntityManager.GetComponentData<Lane>(lane).m_EndNode);
            CarLaneFlags turns=0;
            drive.MergeDirection=MergeDirection(owner,drive.End,keys);
            foreach(var lane in Lanes(drive.End))
            {
                if(!EntityManager.HasComponent<CarLane>(lane)||EntityManager.HasComponent<MasterLane>(lane)||!EntityManager.HasComponent<Lane>(lane))continue;
                var flags=EntityManager.GetComponentData<CarLane>(lane).m_Flags;
                if((flags&(CarLaneFlags.Forbidden|CarLaneFlags.SideConnection))!=0)continue;
                var link=EntityManager.GetComponentData<Lane>(lane);
                if(!keys.Contains(link.m_StartNode))continue;                var turnCurve=CurveOf(lane);
                float degrees=math.degrees(math.acos(math.clamp(math.dot(math.normalizesafe(Geometry.Tangent(turnCurve,0).xz),math.normalizesafe(Geometry.Tangent(turnCurve,1).xz)),-1,1)));
                flags=ArrowPolicy.GentleAngle(flags,degrees);
                if((flags&(CarLaneFlags.UTurnLeft|CarLaneFlags.UTurnRight))!=0)
                {if(link.m_EndNode.OwnerEquals(new Game.Pathfind.PathNode(owner,0)))turns|=flags&(CarLaneFlags.UTurnLeft|CarLaneFlags.UTurnRight);}
                else
                {
                    turns|=flags&(CarLaneFlags.Forward|CarLaneFlags.TurnLeft|CarLaneFlags.TurnRight|CarLaneFlags.GentleTurnLeft|CarLaneFlags.GentleTurnRight);
                    if((flags&(CarLaneFlags.Forward|CarLaneFlags.TurnLeft|CarLaneFlags.TurnRight|CarLaneFlags.GentleTurnLeft|CarLaneFlags.GentleTurnRight))==0)
                        turns|=CarLaneFlags.Forward; // Unlabelled non-turn continuation must not become left-only.
                }
            }
            return turns;
        }
        bool UTurnStop(Drive drive,List<Entity> lanes,out float3 point,out float3 direction)
        {
            point=default;direction=default;
            if(drive.StopPainted){point=drive.Arc.At(drive.Stop);direction=drive.Arc.Direction(drive.Stop);return true;}
            // A disabled/failed stop replacement must use the actual native paint, not a planned anchor.
            var sources=new List<Entity>(lanes);if(Live(drive.End))sources.AddRange(Lanes(drive.End));
            float best=float.MaxValue;bool found=false;
            foreach(var lane in sources)
            {
                if(EntityManager.HasComponent<Hidden>(lane)||assets.Name(Prefab(lane)).IndexOf("Stop Line",System.StringComparison.OrdinalIgnoreCase)<0)continue;
                var stop=new Geometry.Arc(CurveOf(lane));var p=stop.At(stop.Distance(stop.Nearest(drive.Curve.d)));
                float at=drive.Arc.Nearest(p,out var onRoute,out var heading),error=math.distance(p,drive.Curve.d);
                if(error>20||error>=best||at<.5f||math.distance(p,onRoute)>drive.Width||math.abs(math.dot(Geometry.Tangent(stop.Curve,.5f),heading))>.5f)continue;
                point=p;direction=heading;best=error;found=true;
            }
            return found;
        }
        bool LargeTwoWayRoad(Entity owner,List<Drive> drives)
        {
            if(!EntityManager.HasComponent<Curve>(owner))return false;
            var curve=CurveOf(owner);var mid=Geometry.Position(curve,.5f);var heading=Geometry.Tangent(curve,.5f);
            int forward=0,backward=0;var counted=new List<float3>();
            foreach(var drive in drives)
            {
                drive.Arc.Nearest(mid,out var p,out var direction);
                if(math.abs(math.dot(p-mid,heading))>.75f||math.abs(p.y-mid.y)>2||math.abs(math.dot(direction,heading))<.8f)continue;
                bool duplicate=false;foreach(var old in counted)if(math.distance(p,old)<.25f){duplicate=true;break;}
                if(duplicate)continue;counted.Add(p);
                if(math.dot(direction,heading)>0)forward++;else backward++;
            }
            return UTurnPolicy.EligibleRoad(forward,backward);
        }
        static bool BesideCenter(float3 p,float3 heading,Drive drive,List<Drive> drives)
        {
            drive.Arc.Nearest(p,out var nearest,out var direction);
            if(math.distance(nearest.xz,p.xz)>drive.Width*.5f+.7f||math.abs(nearest.y-p.y)>.5f||math.abs(math.dot(direction,heading))<.85f)return false;
            foreach(var other in drives)
            {
                other.Arc.Nearest(p,out var opposite,out var otherDirection);
                if(math.dot(direction,otherDirection)<-.85f&&math.dot(nearest-p,opposite-p)<0
                    &&math.distance(opposite.xz,p.xz)<other.Width*.5f+.7f&&math.abs(opposite.y-p.y)<.5f)return true;
            }
            return false;
        }
        void ReplaceUTurnZones(Entity owner,Record record,List<Entity> lanes,List<Drive> drives)
        {
            if(!S.UTurnZones||!LargeTwoWayRoad(owner,drives))return;
            var candidates=new List<Drive>();
            foreach(var drive in drives)if(drive.Stop>=0&&HasUTurn(owner,drive,lanes))candidates.Add(drive);
            if(candidates.Count==0)return;
            var pendingLines=new List<CenterPiece>();
            foreach(var lane in lanes)
                if(!EntityManager.HasComponent<Hidden>(lane)&&EntityManager.HasComponent<SecondaryLane>(lane)&&
                    !EntityManager.HasComponent<CarLane>(lane)&&UTurnPolicy.CenterLine(assets.Name(Prefab(lane)))&&
                    EntityManager.HasComponent<NetLaneGeometryData>(Prefab(lane)))
                {
                    // Width offsets/cuts can encode a physical median. Do not manufacture paint across it.
                    if(EntityManager.HasBuffer<CutRange>(lane)&&EntityManager.GetBuffer<CutRange>(lane,true).Length>0)continue;
                    pendingLines.Add(new CenterPiece{Source=lane,Prefab=Prefab(lane),Curve=CurveOf(lane)});
                }
            while(pendingLines.Count>0)
            {
                var pieces=new List<CenterPiece>{pendingLines[0]};pendingLines.RemoveAt(0);
                bool grew=true;
                while(grew)
                {
                    grew=false;
                    for(int i=0;i<pendingLines.Count;i++)
                    {
                        var next=pendingLines[i];if(next.Prefab!=pieces[0].Prefab)continue;
                        var first=pieces[0].Curve;var last=pieces[pieces.Count-1].Curve;var c=next.Curve;
                        bool append=math.distance(last.d,c.a)<.05f||math.distance(last.d,c.d)<.05f;
                        bool prepend=math.distance(first.a,c.d)<.05f||math.distance(first.a,c.a)<.05f;
                        if(!append&&!prepend)continue;
                        if(append&&math.distance(last.d,c.d)<.05f||!append&&math.distance(first.a,c.a)<.05f)c=Geometry.Reverse(c);
                        if(math.dot(Geometry.Tangent(append?last:c,1),Geometry.Tangent(append?c:first,0))<.95f)continue;
                        next.Curve=c;if(append)pieces.Add(next);else pieces.Insert(0,next);
                        pendingLines.RemoveAt(i);grew=true;break;
                    }
                }
                var run=new Geometry.Route(pieces[0].Curve);for(int i=1;i<pieces.Count;i++)run.Append(new Geometry.Arc(pieces[i].Curve));
                var anchors=new List<UTurnPolicy.Anchor>();
                foreach(var drive in drives)
                {
                    if(!UTurnStop(drive,lanes,out var origin,out var normal)||!UTurnPolicy.StopPlane(run,origin,normal,out float at))continue;
                    var p=run.At(at);if(!BesideCenter(p,run.Direction(at),drive,drives))continue;
                    anchors.Add(new UTurnPolicy.Anchor(at,math.dot(run.Direction(at),normal)>0,candidates.Contains(drive)));
                }
                var zones=UTurnPolicy.Zones(run.Length,anchors,S.UTurnZoneLength);if(zones.Count==0)continue;
                // Reject any zone that leaves the paved opposing-lane boundary or intersects a crossing.
                var crossings=NearbyCrossings(owner);var accepted=new List<Bounds1>();
                foreach(var zone in zones)
                {
                    bool safe=true;
                    for(float at=zone.min;at<=zone.max+.01f;at+=.5f)
                    {
                        bool beside=false;foreach(var drive in candidates)if(BesideCenter(run.At(at),run.Direction(at),drive,drives)){beside=true;break;}
                        if(!beside){safe=false;break;}
                    }
                    if(!safe)continue;
                    float offset=0;
                    foreach(var piece in pieces)
                    {
                        var arc=new Geometry.Arc(piece.Curve);float lo=math.max(zone.min-offset,0),hi=math.min(zone.max-offset,arc.Length);
                        if(hi>lo)
                        {
                            var cut=arc.Slice(lo,hi);float visible=0;foreach(var clear in ClearPaint(cut,crossings,.35f))visible+=new Geometry.Arc(clear).Length;
                            if(visible<hi-lo-.025f){safe=false;break;}
                        }
                        offset+=arc.Length;
                    }
                    if(safe)accepted.Add(zone);
                }
                if(accepted.Count==0)continue;
                float start=0;
                foreach(var piece in pieces)
                {
                    var arc=new Geometry.Arc(piece.Curve);float cursor=0;bool changed=false;
                    foreach(var zone in accepted)
                    {
                        float lo=math.max(0,zone.min-start),hi=math.min(arc.Length,zone.max-start);if(hi<=lo)continue;
                        if(lo>cursor+.001f)PaintDivider(owner,record,piece.Prefab,arc.Slice(cursor,lo));
                        PaintLane(owner,record,assets.Get(AssetCatalog.UTurnZone),arc.Slice(lo,hi));cursor=hi;changed=true;
                    }
                    if(changed){if(cursor<arc.Length-.001f)PaintDivider(owner,record,piece.Prefab,arc.Slice(cursor,arc.Length));Hide(piece.Source,record);}
                    start+=arc.Length;
                }
            }
        }
    }
}



