using Game.Net;
using Game.Prefabs;
using Unity.Entities;
using Unity.Mathematics;
namespace RoadStyleIntoKorea
{
    public partial class StyleSystem
    {
        void AddNativeSidewalkMask(SidewalkFootprint result,Entity cp,Segment first,Segment second,float origin,float width,bool node,NodeSurfaceSync sync=default)
        {
            if(width<=.01f||!EntityManager.HasBuffer<NetCompositionPiece>(cp))return;
            foreach(var piece in EntityManager.GetBuffer<NetCompositionPiece>(cp,true))
            {
                if(PavingPolicy.Kind(PieceName(piece.m_Piece))!=1||
                    (piece.m_SectionFlags&(NetSectionFlags.Hidden|NetSectionFlags.HiddenSurface|NetSectionFlags.Underground|NetSectionFlags.Overhead))!=0||
                    (piece.m_PieceFlags&(NetPieceFlags.Side|NetPieceFlags.Bottom))!=0)continue;
                var template=NativePavingTemplate(piece.m_Piece);
                if(template==null){continue;}
                var offset=piece.m_Offset;offset.x-=origin+width*.5f;
                var sign=new float2((piece.m_SectionFlags&NetSectionFlags.Invert)!=0?1:-1,(piece.m_SectionFlags&NetSectionFlags.FlipMesh)!=0?1:-1);
                if((piece.m_SectionFlags&NetSectionFlags.HalfLength)!=0){sign.y*=2;offset.z+=piece.m_Size.z*.5f;}
                PavingMeshGeometry.Build(template.Vertices,template.AllIndices,offset,sign,piece.m_Size.z,width,-width*.5f,width*.5f,
                    first.m_Left,first.m_Right,second.m_Left,second.m_Right,out var vertices,out var indices,out _,node,sync);
                result.Add(vertices,indices);
                foreach(var rim in template.Rims)
                {
                    var a=template.Vertices[rim.x];var b=template.Vertices[rim.y];
                    if(node&&(a.z*sign.y+offset.z>.01f||b.z*sign.y+offset.z>.01f||a.z*sign.y+offset.z<-.5f*piece.m_Size.z-.01f||b.z*sign.y+offset.z<-.5f*piece.m_Size.z-.01f))continue;
                    float xa=a.x*sign.x+offset.x,xb=b.x*sign.x+offset.x;
                    if(xa<-width*.5f-.01f||xa>width*.5f+.01f||xb<-width*.5f-.01f||xb>width*.5f+.01f)continue;
                    var p=PavingMeshGeometry.Deform(a,offset,sign,piece.m_Size.z,width,first.m_Left,first.m_Right,second.m_Left,second.m_Right,node,sync);
                    var q=PavingMeshGeometry.Deform(b,offset,sign,piece.m_Size.z,width,first.m_Left,first.m_Right,second.m_Left,second.m_Right,node,sync);
                    if(math.distance(p,q)>.01f)result.Boundaries.Add(Geometry.Line(p,q));
                }
            }
        }
    }
}
