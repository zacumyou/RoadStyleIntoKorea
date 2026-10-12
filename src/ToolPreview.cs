using Colossal.Mathematics;
using Game.Net;
using Unity.Entities;
namespace RoadStyleIntoKorea
{
    public partial class StyleSystem
    {
        int toolRevision;
        Entity toolTarget;
        EdgeGeometry toolShape;
        EdgeNodeGeometry toolStart,toolEnd;
        Bezier4x3 toolCenter;
        Composition toolComposition;
        bool toolHasShape;
        void InvalidateToolPreview(){unchecked{toolRevision++;}}
        static bool SameSegment(Segment a,Segment b)=>Geometry.SameCurve(a.m_Left,b.m_Left)&&Geometry.SameCurve(a.m_Right,b.m_Right);
        static bool SameNode(EdgeNodeGeometry a,EdgeNodeGeometry b)=>SameSegment(a.m_Left,b.m_Left)&&SameSegment(a.m_Right,b.m_Right)&&Geometry.SameCurve(a.m_Middle,b.m_Middle);
        internal void PrepareToolTarget(Entity edge)
        {
            bool hasShape=EntityManager.HasComponent<EdgeGeometry>(edge);
            var shape=hasShape?EntityManager.GetComponentData<EdgeGeometry>(edge):default;
            var center=CurveOf(edge);
            var start=EntityManager.HasComponent<StartNodeGeometry>(edge)?EntityManager.GetComponentData<StartNodeGeometry>(edge).m_Geometry:default;
            var end=EntityManager.HasComponent<EndNodeGeometry>(edge)?EntityManager.GetComponentData<EndNodeGeometry>(edge).m_Geometry:default;
            var composition=EntityManager.HasComponent<Composition>(edge)?EntityManager.GetComponentData<Composition>(edge):default;
            if(toolTarget!=edge||toolHasShape!=hasShape||!Geometry.SameCurve(toolCenter,center)||
               !SameSegment(shape.m_Start,toolShape.m_Start)||!SameSegment(shape.m_End,toolShape.m_End)||
               !SameNode(start,toolStart)||!SameNode(end,toolEnd)||
               composition.m_Edge!=toolComposition.m_Edge||composition.m_StartNode!=toolComposition.m_StartNode||composition.m_EndNode!=toolComposition.m_EndNode)
            {
                toolTarget=edge;toolHasShape=hasShape;toolShape=shape;toolCenter=center;toolComposition=composition;
                toolStart=start;toolEnd=end;
                ClearLinePlans();InvalidateToolPreview();
            }
        }
    }
}
