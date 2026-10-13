using System;
using System.Collections.Generic;
using Game.Net;
using Game.Prefabs;
using Unity.Mathematics;
namespace RoadStyleIntoKorea {public partial class StyleSystem {
 // Only an explicitly empty asphalt strip may bridge a gap to the road centre.
 // Tiled empties, shoulders, medians and vehicle lanes are never inferred empty.
 LinePiece RedFillCenterGap(LinePiece piece){
  if(!EntityManager.HasComponent<Composition>(piece.Road)||!EntityManager.HasComponent<EdgeGeometry>(piece.Road))return piece;
  var cp=EntityManager.GetComponentData<Composition>(piece.Road).m_Edge;
  if(!EntityManager.HasBuffer<NetCompositionPiece>(cp)||!EntityManager.HasComponent<NetCompositionData>(cp))return piece;
  float width=EntityManager.GetComponentData<NetCompositionData>(cp).m_Width;
  if(width<=0)return piece;
  var shape=EntityManager.GetComponentData<EdgeGeometry>(piece.Road);
  float extension=float.MaxValue;int direction=0;
  foreach(float t in new[]{.05f,.5f,.95f}){
   var point=Geometry.Position(piece.Curve,t);var forward=Geometry.Tangent(piece.Curve,t);var across=Geometry.Left(forward);
   bool found=false;float centre=0;var spans=new List<float2>();
   foreach(var segment in new[]{shape.m_Start,shape.m_End}){
    var axis=Between(segment.m_Left,segment.m_Right,.5f,0);
    if(!RedPavingPolicy.CrossSection(axis,point,forward,out float at))continue;
    centre=math.dot(Geometry.Position(axis,at)-point,across);found=true;
    foreach(var part in EntityManager.GetBuffer<NetCompositionPiece>(cp,true)){
     string name=PieceName(part.m_Piece);
     if(!name.StartsWith("RB Empty Piece",StringComparison.OrdinalIgnoreCase))continue;
     if((part.m_PieceFlags&NetPieceFlags.HasRoadLanes)!=0||(part.m_SectionFlags&(NetSectionFlags.Hidden|NetSectionFlags.HiddenSurface|NetSectionFlags.Underground|NetSectionFlags.Overhead))!=0)continue;
     float lo=part.m_Offset.x-part.m_Size.x*.5f,hi=part.m_Offset.x+part.m_Size.x*.5f;
     var a=Between(segment.m_Left,segment.m_Right,.5f+lo/width,0);
     var b=Between(segment.m_Left,segment.m_Right,.5f+hi/width,0);
     if(!RedPavingPolicy.CrossSection(a,point,forward,out float ta)||!RedPavingPolicy.CrossSection(b,point,forward,out float tb))continue;
     float x=math.dot(Geometry.Position(a,ta)-point,across),y=math.dot(Geometry.Position(b,tb)-point,across);
     spans.Add(new float2(math.min(x,y),math.max(x,y)));
    }
    break;
   }
   if(!found)return piece;
   int side=centre>0?1:-1;
   if(direction!=0&&direction!=side)return piece;direction=side;
   float edge=side*piece.Width*.5f;
   if(!RedPavingPolicy.EmptyBridge(edge,centre,spans))return piece;
   extension=math.min(extension,math.abs(centre-edge));
  }
  if(extension>.005f&&extension<float.MaxValue){piece.Curve=Geometry.Offset(piece.Curve,direction*extension*.5f);piece.Width+=extension;}
  return piece;
 }
}}
