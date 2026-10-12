using Game.Net;
using Unity.Entities;
using Unity.Mathematics;
using UnityEngine;
namespace RoadStyleIntoKorea {public partial class StyleSystem {
 Plane[] loadPlanes;int loadCameraFrame=-1;
 bool LoadEligible(Entity owner){
  if(LoadVisible(owner))return true;
  // A visible junction must not wait forever on an offscreen connecting road.
  if(Live(owner)&&EntityManager.HasComponent<Edge>(owner)){
   var edge=EntityManager.GetComponentData<Edge>(owner);
   return VisibleGuide(edge.m_Start)||VisibleGuide(edge.m_End);
  }
  return false;
 }
 bool VisibleGuide(Entity node)=>Live(node)&&EntityManager.HasBuffer<GuideChoice>(node)&&EntityManager.GetBuffer<GuideChoice>(node,true).Length>0&&LoadVisible(node);
 bool LoadVisible(Entity owner){
  if(!Live(owner))return true; // admit deleted owners so their staged entries can be discarded
  var camera=World.GetExistingSystemManaged<Game.Rendering.CameraUpdateSystem>()?.activeCamera;
  if(camera==null)return false;
  if(loadCameraFrame!=UnityEngine.Time.frameCount){loadCameraFrame=UnityEngine.Time.frameCount;loadPlanes=GeometryUtility.CalculateFrustumPlanes(camera);}
  float3 min,max;
  if(EntityManager.HasComponent<Curve>(owner)){var c=CurveOf(owner);min=math.min(math.min(c.a,c.b),math.min(c.c,c.d));max=math.max(math.max(c.a,c.b),math.max(c.c,c.d));}
  else if(EntityManager.HasComponent<Node>(owner)){min=max=EntityManager.GetComponentData<Node>(owner).m_Position;}
  else return false;
  return GeometryUtility.TestPlanesAABB(loadPlanes,new Bounds((min+max)*.5f,max-min+new float3(64,16,64)));
 }
}}

