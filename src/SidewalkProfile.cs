using System.Collections.Generic;
using Game.Net;
using Game.Prefabs;
using Unity.Entities;
using Unity.Mathematics;
namespace RoadStyleIntoKorea
{
    public partial class StyleSystem
    {
        readonly Dictionary<Entity,float2> sidewalkGutters=new Dictionary<Entity,float2>();
        float SidewalkGutter(NetCompositionPiece piece)
        {
            if(!sidewalkGutters.TryGetValue(piece.m_Piece,out var gutter))
            {
                var mesh=NativePavingTemplate(piece.m_Piece);
                gutter=float2.zero;
                if(mesh!=null)
                {
                    gutter=EdgePaintPolicy.Gutter(mesh.Vertices,piece.m_Size.x);
                }
                sidewalkGutters[piece.m_Piece]=gutter;
            }
            bool positive=piece.m_Offset.x<0;
            if((piece.m_SectionFlags&NetSectionFlags.Invert)==0)positive=!positive;
            return positive?gutter.y:gutter.x;
        }
    }
}
