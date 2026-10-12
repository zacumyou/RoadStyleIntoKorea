using System.Collections.Generic;
using Colossal.Mathematics;
using Game.Net;
using Unity.Entities;
using Unity.Mathematics;

namespace RoadStyleIntoKorea
{
    public partial class StyleSystem
    {
        // Reserve full center paths, including dash gaps and U-turn markings.
        List<Geometry.Arc> StopBarCenters(Entity owner,List<Entity> lanes)
        {
            var result=new List<Geometry.Arc>();
            foreach(var path in CenterPaths(owner,lanes,EdgeStyle.YellowDouble))result.Add(new Geometry.Arc(path.Curve));
            return result;
        }
        List<Bezier4x3> StopCenterCurves(Entity owner,List<Entity> lanes,bool left)
        {
            var result=new List<Bezier4x3>();
            bool divided=CenterDivided(owner);
            var choice=EntityManager.HasComponent<CenterChoice>(owner)?EntityManager.GetComponentData<CenterChoice>(owner):default;
            int code=divided&&!left?choice.Right:choice.Left;
            var style=code==0?EdgeStyle.Original:(EdgeStyle)(code-1);
            if(style==EdgeStyle.None)return result;
            bool manual=ManualUTurnAllowed(owner)&&EntityManager.HasComponent<UTurnChoice>(owner)&&assets.Get(AssetCatalog.UTurnZone)!=Entity.Null;
            bool replacement=style!=EdgeStyle.Original||manual;
            var paths=CenterPaths(owner,lanes,style);
            foreach(var path in paths)
            {
                if(divided&&path.Left!=left)continue;
                if(!replacement&&path.Source==Entity.Null)continue;
                var visible=replacement?CenterVisible(owner,path,style,lanes):new List<Bezier4x3>(ClearPaint(path.Curve,NearbyCrossings(owner),.35f));
                foreach(var c in visible)
                {
                    var cuts=new List<Bounds1>();
                    if(manual&&UTurnInterval(owner,c,EntityManager.GetComponentData<UTurnChoice>(owner).Position,out float lo,out float hi))
                    {var arc=new Geometry.Arc(c);cuts.Add(new Bounds1(arc.T(lo),arc.T(hi)));}
                    // Replacement center paint does not inherit native renderer cuts.
                    if(!replacement&&path.Source!=Entity.Null&&EntityManager.HasBuffer<CutRange>(path.Source))
                    {
                        var source=new Geometry.Arc(path.Curve);var part=new Geometry.Arc(c);
                        foreach(var cut in EntityManager.GetBuffer<CutRange>(path.Source,true))
                        {
                            float a=source.Distance(cut.m_CurveDelta.min),b=source.Distance(cut.m_CurveDelta.max);
                            float start=source.Distance(source.Nearest(c.a)),end=source.Distance(source.Nearest(c.d));
                            if(b<=start||a>=end)continue;
                            cuts.Add(new Bounds1(part.T(math.max(0,a-start)),part.T(math.min(part.Length,b-start))));
                        }
                    }
                    result.AddRange(StopCenterPolicy.Outside(c,cuts));
                }
            }
            return result;
        }
    }
}
