using System.Collections.Generic;
using Game.Net;
using Game.Pathfind;
using Unity.Entities;
using Unity.Mathematics;

namespace RoadStyleIntoKorea
{
    public partial class StyleSystem
    {
        int MergeDirection(Entity road,Entity node,HashSet<PathNode> sourceKeys)
        {
            if(!EntityManager.HasBuffer<ConnectedEdge>(node))return 0;
            var roads=new HashSet<Entity>();foreach(var connection in EntityManager.GetBuffer<ConnectedEdge>(node,true))if(IsRoad(connection.m_Edge))roads.Add(connection.m_Edge);else return 0;
            if(roads.Count!=2)return 0;
            var starts=new HashSet<PathNode>();var ends=new HashSet<PathNode>();
            var links=new List<Entity>();
            foreach(var lane in Lanes(node))
            {
                if(!EntityManager.HasComponent<CarLane>(lane)||EntityManager.HasComponent<MasterLane>(lane)||!EntityManager.HasComponent<Lane>(lane))continue;
                var flags=EntityManager.GetComponentData<CarLane>(lane).m_Flags;
                if((flags&(CarLaneFlags.UTurnLeft|CarLaneFlags.UTurnRight|CarLaneFlags.Forbidden|CarLaneFlags.SideConnection))!=0)continue;
                var link=EntityManager.GetComponentData<Lane>(lane);
                if(link.m_StartNode.GetOwnerIndex()!=road.Index||link.m_EndNode.GetOwnerIndex()==road.Index)continue;
                bool target=false;foreach(var other in roads)if(other!=road&&link.m_EndNode.GetOwnerIndex()==other.Index)target=true;
                if(!target)continue;starts.Add(link.m_StartNode);ends.Add(link.m_EndNode);links.Add(lane);
            }
            if(starts.Count<=ends.Count)return 0;
            int result=0;
            foreach(var lane in links)
            {
                var link=EntityManager.GetComponentData<Lane>(lane);if(!sourceKeys.Contains(link.m_StartNode))continue;
                bool shared=false;
                foreach(var other in links)
                {
                    var next=EntityManager.GetComponentData<Lane>(other);
                    if(!next.m_StartNode.Equals(link.m_StartNode)&&next.m_EndNode.Equals(link.m_EndNode)){shared=true;break;}
                }
                var c=CurveOf(lane);int direction=MergePolicy.Direction(starts.Count,ends.Count,shared,c);
                // A lane that can keep going without lateral merging retains its ordinary arrows.
                if(direction==0||result!=0&&result!=direction)return 0;
                result=direction;
            }
            return result;
        }
    }
}
