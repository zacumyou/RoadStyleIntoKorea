using System.Collections.Generic;
using Game.Net;
using Game.Prefabs;
using Unity.Entities;
using Unity.Mathematics;
using ObjectTransform=Game.Objects.Transform;

namespace RoadStyleIntoKorea
{
    public partial class StyleSystem
    {
        float DecalHalfLength(Entity prefab,float fallback)
        {
            if(EntityManager.HasComponent<ObjectGeometryData>(prefab))
            {
                var bounds=EntityManager.GetComponentData<ObjectGeometryData>(prefab).m_Bounds;
                float extent=math.max(math.abs(bounds.min.z),math.abs(bounds.max.z));
                if(math.isfinite(extent)&&extent>.1f)return extent;
            }
            return fallback;
        }
        void EnsureApproachArrows(Entity owner,Record record,List<Drive> drives)
        {
            if(!S.Arrows)return;
            var candidates=ApproachArrows(owner,drives);
            foreach(var drive in drives)
            {
                if(drive.Stop<0&&drive.MergeDirection==0)continue;
                float stop=drive.Stop>=0?drive.Stop:drive.Arc.Length-.1f;
                var symbols=drive.MergeDirection!=0?new List<string>{drive.MergeDirection>0?"DiagonalLeft":"DiagonalRight"}:ArrowPolicy.ConnectionSymbols(drive.Turns);symbols=AvailableArrowSymbols(symbols);if(symbols.Count==0)continue;
                bool present=false;
                foreach(var item in candidates)
                {
                    if(drive.MergeDirection!=0||item.Route!=drive)continue;
                    bool regular=item.Symbols.Exists(s=>s!="UTurn");
                    if(!regular&&symbols.Exists(s=>s!="UTurn"))continue;
                    bool planned=PlanArrow(item,candidates,out float at,out _);
                    if(!planned)at=item.Along;
                    if(at<stop&&stop-at<=math.max(12,RoadArrowGap(drive.Owner)+1)){
                        present=true;
                        if(planned)for(int i=0;i<item.Symbols.Count;i++)drive.PrimaryPaint.Add(new ArrowPolicy.Occupied(ArrowStackAt(at,item.Symbols,i),DecalHalfLength(assets.Get(AssetCatalog.Arrow(item.Symbols[i])),3.8f)));
                        else drive.PrimaryPaint.Add(new ArrowPolicy.Occupied(at,DecalHalfLength(Prefab(item.Source),3.8f)));
                        break;
                    }
                }
                if(present){drive.PrimaryReady=true;continue;}
                // Preserve the normal stop arrow even when no native decal entity was generated.
                // U-turn warnings reserve these intervals and are allowed to disappear first.
                int count=symbols.Count;
                if(!TryStopArrowPlacement(drive,stop,symbols,count,out float first))
                {
                    count=1;
                    if(!TryStopArrowPlacement(drive,stop,symbols,count,out first))
                    {continue;}
                }
                for(int i=0;i<count;i++)
                {
                    float at=ArrowStackAt(first,symbols,i);var prefab=assets.Get(AssetCatalog.Arrow(symbols[i]));
                    PaintObject(owner,record,prefab,new ObjectTransform(drive.Arc.At(at),quaternion.LookRotationSafe(drive.Arc.Direction(at),new float3(0,1,0))));
                    drive.PrimaryPaint.Add(new ArrowPolicy.Occupied(at,DecalHalfLength(prefab,3.8f)));
                }
                drive.PrimaryReady=true;
            }
        }
        void PaintUTurnWarnings(Entity owner,Record record,List<Entity> lanes,List<Drive> drives)
        {
            if(!S.Arrows||IsNarrowRoad(owner))return;
            var arrows=ApproachArrows(owner,drives);
            foreach(var drive in drives)
            {
                string symbol=ArrowPolicy.UTurnWarning(drive.Turns);
                if(symbol==null||drive.Stop<0||!drive.PrimaryReady)continue;
                var prefab=assets.Get(AssetCatalog.Arrow(symbol));
                float half=DecalHalfLength(prefab,symbol=="LeftUTurn"?3.8f:2.8f);
                var occupied=new List<ArrowPolicy.Occupied>(drive.PrimaryPaint);
                var diamonds=new List<ArrowPolicy.Occupied>();
                float before=drive.Stop;float closest=float.MinValue;
                foreach(var primary in drive.PrimaryPaint)before=math.min(before,primary.Center-primary.Half);
                foreach(var item in arrows)
                {
                    if(item.Route!=drive)continue;
                    bool planned=PlanArrow(item,arrows,out float along,out _);
                    if(!planned)
                    {
                        float nativeHalf=DecalHalfLength(Prefab(item.Source),3.8f);
                        occupied.Add(new ArrowPolicy.Occupied(item.Along,nativeHalf));
                        if(item.Along>closest){closest=item.Along;before=item.Along-nativeHalf;}
                        continue;
                    }
                    float groupStart=along;
                    for(int i=0;i<item.Symbols.Count;i++)
                    {
                        float arrowHalf=DecalHalfLength(assets.Get(AssetCatalog.Arrow(item.Symbols[i])),3.8f);
                        float at=ArrowStackAt(along,item.Symbols,i);occupied.Add(new ArrowPolicy.Occupied(at,arrowHalf));
                        groupStart=math.min(groupStart,at-arrowHalf);
                    }
                    if(along>closest){closest=along;before=groupStart;}
                }
                diamonds.AddRange(drive.WarningPaint);
                // A native arrow can sit beyond the planned stop. Never place a warning inside the junction.
                before=math.min(before,drive.Stop);
                foreach(var primary in drive.PrimaryPaint)before=math.min(before,primary.Center-primary.Half);
                if(!ArrowPolicy.TryUTurnWarning(drive.Arc.Length,before,half,occupied,diamonds,out float distance))
                {continue;}
                if(!ArrowPolicy.SecondaryClear(distance,half,occupied))continue;
                // Reject a decal whose footprint meets a crossing at the other end of a short road.
                bool clear=true;
                foreach(var crossing in NearbyCrossings(owner))
                {
                    var cross=new Geometry.Arc(CrosswalkCurve(crossing));
                    for(int i=0;i<=8;i++)
                    {
                        var p=drive.Arc.At(distance-half+2*half*i/8);var q=cross.At(cross.Distance(cross.Nearest(p)));
                        if(math.abs(p.y-q.y)<2&&math.distance(p.xz,q.xz)<CrossingWidth(crossing)*.5f+ArrowPolicy.MarkingGap){clear=false;break;}
                    }
                    if(!clear)break;
                }
                if(!clear){continue;}
                var transform=new ObjectTransform(drive.Arc.At(distance),quaternion.LookRotationSafe(drive.Arc.Direction(distance),new float3(0,1,0)));
                PaintObject(owner,record,prefab,transform);
            }
        }
    }
}
