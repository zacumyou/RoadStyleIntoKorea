using System;
using System.Collections.Generic;
using Colossal.Mathematics;
using Game.Common;
using Game.Net;
using Game.Tools;
using Unity.Entities;
using Unity.Mathematics;

namespace RoadStyleIntoKorea
{
    public partial class StyleSystem
    {
        void ApplySurfaceDetails(Entity owner,Record record,List<Entity> lanes,List<Drive> drives)
        {
            if(!S.LaneBoundaryCracks)return;
            var crossings=NearbyCrossings(owner);
            var boundaries=new List<Bezier4x3>();
            foreach(var lane in lanes)
            {
                if(!EntityManager.HasComponent<SecondaryLane>(lane)||EntityManager.HasComponent<CarLane>(lane)||
                   assets.Name(Prefab(lane)).IndexOf("Car Lane Line",StringComparison.OrdinalIgnoreCase)<0)continue;
                // Original dividers can already be hidden by this mod's solid-approach replacement.
                if(EntityManager.HasComponent<Hidden>(lane)&&!EntityManager.HasComponent<OriginalPaintHidden>(lane))continue;
                var curve=CurveOf(lane);bool duplicate=false;
                foreach(var previous in boundaries)if(SameSurfacePath(previous,curve)){duplicate=true;break;}
                if(duplicate||!DrivesBesideBoundary(curve,drives))continue;
                foreach(var piece in ClearPaint(curve,crossings,.5f,owner))
                    PaintLane(owner,record,assets.Get(AssetCatalog.BoundaryCrack),piece);
                boundaries.Add(curve);
            }
        }
        static bool SameSurfacePath(Bezier4x3 a,Bezier4x3 b)
        {
            const float tolerance=.04f; // 20cm, squared; supports opposite stored directions.
            bool ends=(math.distancesq(a.a,b.a)<tolerance&&math.distancesq(a.d,b.d)<tolerance)||
                      (math.distancesq(a.a,b.d)<tolerance&&math.distancesq(a.d,b.a)<tolerance);
            return ends&&math.distancesq(Geometry.Position(a,.5f),Geometry.Position(b,.5f))<tolerance;
        }
        static bool DrivesBesideBoundary(Bezier4x3 curve,List<Drive> drives)
        {
            foreach(float t in new[]{.2f,.5f,.8f})
            {
                var p=Geometry.Position(curve,t);var tangent=Geometry.Tangent(curve,t);var left=Geometry.Left(tangent);
                bool onLeft=false,onRight=false;
                foreach(var d in drives)
                {
                    d.Arc.Nearest(p,out var nearest,out var direction);
                    if(math.abs(nearest.y-p.y)>.5f||math.abs(math.dot(direction,tangent))<.85f)continue;
                    float lateral=math.dot(nearest-p,left);
                    if(math.abs(math.abs(lateral)-d.Width*.5f)>.6f||math.distance(nearest.xz,p.xz)>d.Width*.5f+.6f)continue;
                    if(lateral>.3f)onLeft=true;if(lateral<-.3f)onRight=true;
                }
                if(!onLeft||!onRight)return false;
            }
            return true;
        }
    }
}
