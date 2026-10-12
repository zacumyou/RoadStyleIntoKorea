using System.Collections.Generic;
using Colossal.Mathematics;
using Game.Net;
using Unity.Entities;
using Unity.Mathematics;
namespace RoadStyleIntoKorea {
 public partial class StyleSystem {
  sealed class DetailEnd {internal Entity Road;internal int Kind;internal float3 Point,Tangent;}
  void QueueDetailJoins(Entity owner){
   if(!Live(owner))return;
   if(EntityManager.HasComponent<Node>(owner)){manualDirty.Add(owner);return;}
   if(EntityManager.HasComponent<Edge>(owner)){var edge=EntityManager.GetComponentData<Edge>(owner);manualDirty.Add(edge.m_Start);manualDirty.Add(edge.m_End);}
  }
  List<DetailEnd> DetailEnds(Entity road,Entity node){
   var result=new List<DetailEnd>();if(!EntityManager.HasBuffer<ManualDecalChoice>(road)||!EntityManager.HasComponent<EdgeGeometry>(road))return result;
   var edge=EntityManager.GetComponentData<Edge>(road);bool start=edge.m_Start==node;
   var geometry=EntityManager.GetComponentData<EdgeGeometry>(road);var section=start?geometry.m_Start:geometry.m_End;float t=start?0:1;
   List<PavingRegion> regions=null;
   foreach(var choice in EntityManager.GetBuffer<ManualDecalChoice>(road,true)){
    if(choice.Kind!=9&&choice.Kind!=10)continue;float half=SidewalkDetailWidth(choice.Kind)*.5f+.02f;
    if(regions==null)regions=PavingRegions(road);
    var region=regions.Find(r=>ResolveDetailOffset(road,r,choice,out _));if(region==null||!ResolveDetailOffset(road,region,choice,out float offset))continue;
    var curve=Between(section.m_Left,section.m_Right,.5f+offset/region.RoadWidth,DetailSurfaceHeight(road,region)+.015f);
    result.Add(new DetailEnd{Road=road,Kind=choice.Kind,Point=Geometry.Position(curve,t),Tangent=Geometry.Tangent(curve,t)*(start?-1:1)});
   }
   return result;
  }
  static bool DetailHeight(List<SidewalkFootprint> surfaces,float3 point,out float height){
   height=float.MinValue;bool found=false;foreach(var surface in surfaces)if(surface.TryHeight(point,out float y)){height=math.max(height,y);found=true;}return found;
  }
  static bool DetailCovered(List<SidewalkFootprint> surfaces,float3 point){
   foreach(var surface in surfaces)if(surface.TryHeight(point,out _))return true;
   return false;
  }
  bool DetailJoinFits(Bezier4x3 curve,int kind,List<SidewalkFootprint> surfaces){
   var arc=new Geometry.Arc(curve);int count=math.clamp((int)math.ceil(arc.Length/.25f),2,256);
   float half=SidewalkDetailWidth(kind)*.5f+.02f;
   for(int i=0;i<=count;i++){
    float t=arc.T(arc.Length*i/count);var point=Geometry.Position(curve,t);var across=Geometry.Left(Geometry.Tangent(curve,t));
    int columns=math.max(2,(int)math.ceil(half*2/.2f));
    for(int j=0;j<=columns;j++)if(!DetailCovered(surfaces,point+across*math.lerp(-half,half,(float)j/columns)))return false;
   }
   return true;
  }
  void BuildDetailJoins(Entity node){{
   if(!EntityManager.HasBuffer<ConnectedEdge>(node)||!CachedAssetsReady())return;
   var roads=new List<Entity>();foreach(var link in EntityManager.GetBuffer<ConnectedEdge>(node,true))if(IsRoad(link.m_Edge)&&!roads.Contains(link.m_Edge))roads.Add(link.m_Edge);
   if(roads.Count<2||roads.Count>8)return;
   var ends=new List<DetailEnd>();foreach(var road in roads)ends.AddRange(DetailEnds(road,node));if(ends.Count<2)return;
   var pairs=new List<(DetailEnd A,DetailEnd B,float Distance)>();
   for(int i=0;i<ends.Count;i++)for(int j=i+1;j<ends.Count;j++){
    var a=ends[i];var b=ends[j];float gap=math.distance(a.Point,b.Point);
    if(a.Road==b.Road||a.Kind!=b.Kind||gap<.05f||gap>64||math.abs(a.Point.y-b.Point.y)>1.5f||math.dot(a.Tangent,b.Tangent)>.75f)continue;
    var delta=b.Point-a.Point;
    if(math.dot(delta,a.Tangent)<-.25f||math.dot(-delta,b.Tangent)<-.25f)continue;
    pairs.Add((a,b,gap));
   }
   if(pairs.Count==0)return;
   var surfaces=new List<SidewalkFootprint>{EdgeSidewalks(node)};bool overlay=false;
   var owners=new List<Entity>(roads){node};
   foreach(var owner in owners)if(records.TryGetValue(owner,out var paving)&&paving.WalkSurface!=null){surfaces.Add(paving.WalkSurface);overlay=true;}
   pairs.Sort((a,b)=>a.Distance.CompareTo(b.Distance));
   var record=new Record();manualRecords[node]=record;var used=new HashSet<DetailEnd>();
   foreach(var pair in pairs){
    var a=pair.A;var b=pair.B;if(used.Contains(a)||used.Contains(b))continue;
    Bezier4x3 curve=default;bool fits=false;
    // Try a small bounded set of bend radii; never cross a roadway to force a join.
    foreach(float ratio in new[]{1f/3f,.5f,.2f,.7f}){
     float handle=pair.Distance*ratio;
     curve=new Bezier4x3(a.Point,a.Point+a.Tangent*handle,b.Point+b.Tangent*handle,b.Point);
     if(DetailJoinFits(curve,a.Kind,surfaces)){fits=true;break;}
    }
    if(!fits){continue;}
    var prefab=assets.Get(SidewalkDetailAsset(a.Kind));if(!Live(prefab))continue;
    PaintLane(node,record,prefab,curve);if(overlay)PaintSidewalkDetailSurface(node,record,a.Kind,curve,surfaces);
    used.Add(a);used.Add(b);   }
  }}
 }
}
