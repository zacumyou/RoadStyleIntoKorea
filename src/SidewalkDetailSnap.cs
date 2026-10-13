using System.Collections.Generic;
using Game.Net;
using Unity.Entities;
using Unity.Mathematics;
namespace RoadStyleIntoKorea {
 public partial class StyleSystem {
  Entity detailSnapRoad;PavingRegion detailSnapRegion;int detailSnapKind,detailSnapRevision=-1;
  readonly List<float> detailSnapOffsets=new List<float>();
  bool SnapSidewalkDetail(Entity road,PavingRegion region,int kind,ref float offset){
   if(detailSnapRoad!=road||detailSnapRegion!=region||detailSnapKind!=kind||detailSnapRevision!=toolRevision){
    detailSnapRoad=road;detailSnapRegion=region;detailSnapKind=kind;detailSnapRevision=toolRevision;detailSnapOffsets.Clear();
    var edge=EntityManager.GetComponentData<Edge>(road);var shape=EntityManager.GetComponentData<EdgeGeometry>(road);
    float half=SidewalkDetailWidth(kind)*.5f+.02f;
    foreach(bool start in new[]{true,false}){
     var node=start?edge.m_Start:edge.m_End;if(!EntityManager.HasBuffer<ConnectedEdge>(node))continue;
     var section=start?shape.m_Start:shape.m_End;float at=start?0:1;
     var left=Geometry.Position(section.m_Left,at);var right=Geometry.Position(section.m_Right,at);
     var tangent=Geometry.Tangent(start?region.First:region.Second,at);
     foreach(var connected in EntityManager.GetBuffer<ConnectedEdge>(node,true)){
      var other=connected.m_Edge;if(other==road||!IsRoad(other)||!EntityManager.HasBuffer<ManualDecalChoice>(other)||!EntityManager.HasComponent<EdgeGeometry>(other))continue;
      var otherEdge=EntityManager.GetComponentData<Edge>(other);bool otherStart=otherEdge.m_Start==node;
      var otherShape=EntityManager.GetComponentData<EdgeGeometry>(other);var otherSection=otherStart?otherShape.m_Start:otherShape.m_End;
      var regions=PavingRegions(other);
      foreach(var choice in EntityManager.GetBuffer<ManualDecalChoice>(other,true)){
       if(choice.Kind!=kind)continue;
       var source=regions.Find(r=>DetailRegionAllowed(other,r)&&choice.Offset>=r.PaintMin+half-.001f&&choice.Offset<=r.PaintMax-half+.001f);if(source==null)continue;
       var curve=Between(otherSection.m_Left,otherSection.m_Right,.5f+choice.Offset/source.RoadWidth,DetailSurfaceHeight(other,source));
       float sourceT=otherStart?0:1;var point=Geometry.Position(curve,sourceT);
       if(math.abs(math.dot(tangent,Geometry.Tangent(curve,sourceT)))<.8f)continue;
       float candidate=ManualPlacementPolicy.SidewalkOffset(point,left,right,region.RoadWidth);
       if(candidate<region.PaintMin+half||candidate>region.PaintMax-half)continue;
       var target=math.lerp(left,right,.5f+candidate/region.RoadWidth)+new float3(0,DetailSurfaceHeight(road,region),0);
       if(math.distance(point.xz,target.xz)>12||math.abs(point.y-target.y)>.75f)continue;
       if(!detailSnapOffsets.Exists(v=>math.abs(v-candidate)<.01f))detailSnapOffsets.Add(candidate);
      }
     }
    }
   }
   float best=.5f,snapped=offset;bool found=false;
   foreach(float candidate in detailSnapOffsets){float distance=math.abs(offset-candidate);if(distance<=best){best=distance;snapped=candidate;found=true;}}
   if(found)offset=snapped;return found;
  }
 }
}

