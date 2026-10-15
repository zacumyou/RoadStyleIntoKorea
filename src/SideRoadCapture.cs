using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;
using Game.Net;
using Game.Prefabs;
using Game.Simulation;
using Unity.Entities;
using Unity.Mathematics;
using UnityEngine;
namespace RoadStyleIntoKorea {public partial class StyleSystem {
 int sideRoadCaptureCount;
 static string CaptureNumber(float value)=>value.ToString("R",CultureInfo.InvariantCulture);
 void CaptureSideRoadGeometry(Entity road){
  if(!IsRoad(road)||!EntityManager.HasBuffer<SideRoadChoice>(road)||EntityManager.GetBuffer<SideRoadChoice>(road,true).Length==0)return;
  if(sideRoadCaptureCount++>=8){captureText.AppendLine("SIDE_ROAD_DETAIL_SKIPPED per-capture road limit=8");return;}
  try{
   EntityManager.CompleteAllTrackedJobs();
   string stem=Path.Combine(Path.GetDirectoryName(capturePath),Path.GetFileNameWithoutExtension(capturePath)+"-side-road-"+road.Index);
   var geometry=new StringBuilder("# RSIK world-space metres, Y up. Objects are separate comparison layers.\n");
   int vertexCount=0;bool truncated=false;
   Action<string,IList<float3>,IList<int>> mesh=(name,points,faces)=>{
    if(vertexCount+points.Count>200000){truncated=true;return;}
    geometry.AppendLine("o "+name);int offset=vertexCount+1;
    foreach(var p in points)geometry.Append("v ").Append(CaptureNumber(p.x)).Append(' ').Append(CaptureNumber(p.y)).Append(' ').AppendLine(CaptureNumber(p.z));
    for(int i=0;i+2<faces.Count;i+=3)geometry.Append("f ").Append(offset+faces[i]).Append(' ').Append(offset+faces[i+1]).Append(' ').AppendLine((offset+faces[i+2]).ToString(CultureInfo.InvariantCulture));
    vertexCount+=points.Count;
   };
   var shape=EntityManager.GetComponentData<EdgeGeometry>(road);
   var cp=EntityManager.GetComponentData<Composition>(road).m_Edge;
   var data=EntityManager.GetComponentData<NetCompositionData>(cp);
   var parts=new List<NetCompositionPiece>();foreach(var part in EntityManager.GetBuffer<NetCompositionPiece>(cp,true))parts.Add(part);
   for(int i=0;i<parts.Count;i++){
    var part=parts[i];if((part.m_PieceFlags&NetPieceFlags.HasMesh)==0||(part.m_SectionFlags&(NetSectionFlags.Hidden|NetSectionFlags.Underground|NetSectionFlags.Overhead))!=0)continue;
    var source=ReadCutSource(part.m_Piece);
    var sign=new float2((part.m_SectionFlags&NetSectionFlags.Invert)!=0?1:-1,(part.m_SectionFlags&NetSectionFlags.FlipMesh)!=0?1:-1);var offset=part.m_Offset;
    if((part.m_SectionFlags&NetSectionFlags.HalfLength)!=0){sign.y*=2;offset.z+=part.m_Size.z*.5f;}
    Func<float3,float3> deform=p=>PavingMeshGeometry.Deform(p,offset,sign,part.m_Size.z,data.m_Width,shape.m_Start.m_Left,shape.m_Start.m_Right,shape.m_End.m_Left,shape.m_End.m_Right,false);
    var points=new List<float3>();foreach(var p in source.P)points.Add(deform(p));mesh("native_reference_"+i,points,source.I);
    captureText.AppendLine($"SIDE_ROAD_REFERENCE object=native_reference_{i} prefab={PieceName(part.m_Piece)} transform=reconstructed_from_composition");
   }
   if(nativeSideRoadCuts.TryGetValue(road,out var cut)&&EntityManager.HasBuffer<NetCompositionPiece>(cut.Mesh)){
    int i=0;foreach(var part in EntityManager.GetBuffer<NetCompositionPiece>(cut.Mesh,true)){
     if(!cut.Owned.Contains(part.m_Piece))continue;
     var sign=new float2((part.m_SectionFlags&NetSectionFlags.Invert)!=0?1:-1,(part.m_SectionFlags&NetSectionFlags.FlipMesh)!=0?1:-1);var offset=part.m_Offset;
     if((part.m_SectionFlags&NetSectionFlags.HalfLength)!=0){sign.y*=2;offset.z+=part.m_Size.z*.5f;}
     var points=new List<float3>();var faces=new List<int>();
     foreach(var p in EntityManager.GetBuffer<MeshVertex>(part.m_Piece,true))points.Add(PavingMeshGeometry.Deform(p.m_Vertex,offset,sign,part.m_Size.z,data.m_Width,shape.m_Start.m_Left,shape.m_Start.m_Right,shape.m_End.m_Left,shape.m_End.m_Right,false));
     foreach(var f in EntityManager.GetBuffer<MeshIndex>(part.m_Piece,true))faces.Add(f.m_Index);
     mesh("native_clipped_"+i++,points,faces);
    }
   }
   int surfaceIndex=0;
   foreach(var set in new[]{records,curbRecords})if(set.TryGetValue(road,out var record))foreach(var rendered in new[]{record,record.SideRoadMarks}){
    if(rendered==null)continue;
    foreach(var surface in rendered.PavingSurfaces){
     if(surface.Mesh==null||surface.Object==null)continue;
     var points=new List<float3>();foreach(var p in surface.Mesh.vertices)points.Add((float3)surface.Object.transform.TransformPoint(p));
     string name="rendered_surface_"+surfaceIndex++;mesh(name,points,surface.Mesh.triangles);
     var material=surface.Object.GetComponent<MeshRenderer>()?.sharedMaterial;
     captureText.AppendLine($"SIDE_ROAD_RENDER object={name} style={surface.Style} material={material?.name} vertices={points.Count}");
    }
   }
   var samples=new StringBuilder("opening,side,along,depth,x,y,z,terrain_y,apron_minus_terrain\n");
   var terrain=World.GetExistingSystemManaged<TerrainSystem>();var height=terrain!=null?terrain.GetHeightData(true):default;
   int openingIndex=0;
   foreach(var opening in SideRoadOpenings(road)){
    captureText.AppendLine($"SIDE_ROAD_PROFILE index={openingIndex} inner={opening.Inner} outer={opening.Outer} top={opening.Top} bottom={opening.Bottom} joinDepth={opening.CurbJoinDepth} cut={opening.CutSurface} width={opening.Width} distance={opening.Distance}");
    if(opening.NativeCurbDepths!=null)captureText.AppendLine($"SIDE_ROAD_NATIVE_JOIN left={opening.NativeCurbDepths[0]} right={opening.NativeCurbDepths[1]} hideMarks={opening.HideMarks}");
    for(int row=0;row<=32;row++){
     float depth=(opening.Outer-opening.Inner)*row/32f,half=opening.CutHalfSpan(depth);
     foreach(float along in new[]{-half,0f,half}){
      var p=opening.Point(along,depth);float ground=terrain!=null?TerrainUtils.SampleHeight(ref height,p):float.NaN;
      samples.Append(openingIndex).Append(',').Append(opening.Side);
      foreach(float v in new[]{along,depth,p.x,p.y,p.z,ground,p.y-ground})samples.Append(',').Append(CaptureNumber(v));samples.AppendLine();
     }
    }
    openingIndex++;
   }
   File.WriteAllText(stem+".obj",geometry.ToString(),new UTF8Encoding(false));File.WriteAllText(stem+".csv",samples.ToString(),new UTF8Encoding(false));
   captureText.AppendLine($"SIDE_ROAD_DETAIL obj={stem}.obj csv={stem}.csv vertices={vertexCount} truncated={truncated} terrain=CPU_waited rendered_positions=world native_positions=reconstructed");
  }catch(Exception error){captureText.AppendLine("SIDE_ROAD_DETAIL_ERROR "+error);}
 }
}}
