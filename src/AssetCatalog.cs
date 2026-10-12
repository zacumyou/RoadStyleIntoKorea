using System;
using System.Collections.Generic;
using Game.Prefabs;
using Unity.Collections;
using Unity.Entities;

namespace RoadStyleIntoKorea
{
    internal sealed class AssetCatalog
    {
        internal static string Crosswalk(int size)=>CrosswalkStylePolicy.Asset(size);
        internal const string White="CSKREdgeWhiteSolid6mNetLane";
        internal static string DottedEdge(EdgeStyle style)=>style==EdgeStyle.WhiteSolid?"CSKREdgeWhiteDotted6mNetLane":style==EdgeStyle.YellowSolid?"CSKREdgeYellowDotted6mNetLane":style==EdgeStyle.YellowDouble?"CSKREdgeYellowDoubleDotted6mNetLane":"";
        internal const string BoundaryCrack="CSKRLaneBoundaryCrack8mNetLane";
        internal const string UTurnZone="CSKRUTurnZone8mNetLane";
        internal const string Diamond="CSKRRoadMarkingCrosswalkWarningDecal";
        internal readonly Dictionary<string,Entity> Named=new Dictionary<string,Entity>(StringComparer.Ordinal);
        internal readonly Dictionary<Entity,string> Names=new Dictionary<Entity,string>();
        internal static string Arrow(string type)=>"CSKRLaneDirectionArrow"+type+"Decal";
        internal static string Edge(EdgeStyle s)=>s==EdgeStyle.WhiteSolid?White:s==EdgeStyle.YellowSolid?"CSKREdgeYellowSolid6mNetLane":s==EdgeStyle.YellowDouble?"CSKREdgeYellowDouble6mNetLane":s==EdgeStyle.YellowZigzag?"CSKREdgeYellowZigzag6mNetLane":"";
        internal Entity Get(string name)=>Named.TryGetValue(name,out var e)?e:Entity.Null;
        PrefabSystem prefabSystem;
        internal string Name(Entity e){
            if(Names.TryGetValue(e,out var n))return n;
            // RoadBuilder can register new prefabs after our initial city-load catalog.
            if(e!=Entity.Null&&prefabSystem!=null&&prefabSystem.TryGetPrefab<PrefabBase>(e,out var p)&&p!=null){Names[e]=p.name;Named[p.name]=e;return p.name;}
            return "";
        }
        internal void Refresh(EntityManager em,PrefabSystem ps)
        {
            prefabSystem=ps;
            Named.Clear();Names.Clear();
            using(var q=em.CreateEntityQuery(ComponentType.ReadOnly<PrefabData>()))
            using(var entities=q.ToEntityArray(Allocator.Temp))foreach(var e in entities)
                if(ps.TryGetPrefab<PrefabBase>(e,out var p)&&p!=null){Named[p.name]=e;Names[e]=p.name;}
        }
        int requirementMask=-1;
        readonly HashSet<string> required=new HashSet<string>();
        readonly List<string> missing=new List<string>();
        // Borrowed result, consumed synchronously by callers; not a readiness cache.
        internal List<string> Missing(EntityManager em,Settings s)
        {
            int mask=(s.UTurnZones?1:0)|(s.LaneBoundaryCracks?2:0)|(s.Crosswalks?4:0)|((s.SolidApproach||s.LeftGuides)?8:0)|(s.StopLines?16:0)|(s.Diamonds?32:0)|(s.EdgeLines?64:0)|(s.Arrows?128:0);
            if(mask!=requirementMask){
            requirementMask=mask;required.Clear();
            if(s.UTurnZones)required.Add(UTurnZone);
            if(s.LaneBoundaryCracks)required.Add(BoundaryCrack);
            if(s.Crosswalks)foreach(int size in new[]{2,3,4,13})required.Add(Crosswalk(size));
            if(s.SolidApproach||s.LeftGuides)required.Add(White);
            if(s.StopLines)required.Add("NA Car Stop Line");
            if(s.Diamonds)required.Add(Diamond);
            if(s.EdgeLines)foreach(var v in new[]{EdgeStyle.WhiteSolid,EdgeStyle.YellowSolid,EdgeStyle.YellowDouble,EdgeStyle.YellowZigzag})required.Add(Edge(v));
            if(s.Arrows)foreach(var v in new[]{"Straight","Left","Right","StraightLeft","StraightRight","LeftRight","StraightLeftRight","UTurn","LeftUTurn","DiagonalLeft","DiagonalRight"})required.Add(Arrow(v));
            }
            missing.Clear();
            foreach(var n in required){var e=Get(n);bool lane=n.EndsWith("NetLane")||n=="NA Car Stop Line";if(e==Entity.Null||!em.Exists(e)||(lane?!em.HasComponent<NetLaneGeometryData>(e):!em.HasComponent<ObjectData>(e)))missing.Add(n);}
            return missing;
        }
    }
}
