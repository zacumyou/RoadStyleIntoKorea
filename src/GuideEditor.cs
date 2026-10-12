using System.Collections.Generic;
using Colossal.Mathematics;
using Colossal.Serialization.Entities;
using Game.Common;
using Game.Net;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using UnityEngine;
namespace RoadStyleIntoKorea {
 public struct GuideChoice:IBufferElementData,ISerializable {
  public float3 Start,End;
  public void Serialize<T>(T w) where T:IWriter {w.Write(1);w.Write(Start);w.Write(End);}
  public void Deserialize<T>(T r) where T:IReader {r.Read(out int v);r.Read(out Start);r.Read(out End);if(v!=1||!math.all(math.isfinite(Start))||!math.all(math.isfinite(End)))Start=End=default;}
 }
 public partial class StyleSystem {
  sealed class GuideCandidate {internal Bezier4x3 Source,Paint;internal CarLaneFlags Flags;internal Geometry.Arc Arc;}
  struct GuideCommand {internal Entity Node;internal GuideChoice Choice;internal int Kind;}
  readonly WorkQueue<GuideCommand> guideCommands=new WorkQueue<GuideCommand>();
  readonly HashSet<Entity> guideDirty=new HashSet<Entity>();
  readonly Dictionary<Entity,Record> guideRecords=new Dictionary<Entity,Record>();
  readonly List<GuideCandidate> guideCandidates=new List<GuideCandidate>();
  readonly List<float3> guideHoverPoints=new List<float3>();
  static bool ContainsGuidePoint(List<float3> points,float3 point){foreach(var p in points)if(GuidePolicy.Point(p,point))return true;return false;}
  Entity guideNode;int guideRevision=-1;float guideCacheTime;bool guideStarted;float3 guideStart;
  internal bool ToolResetHandled;
  internal bool GuideReady=>Live(guideNode)&&guideCandidates.Count>0;
  internal int GuideStep=>!GuideReady?0:guideStarted?2:1;
  internal void EndGuideEdit(){guideNode=Entity.Null;guideStarted=false;guideCandidates.Clear();guideRevision=-1;}
  internal void GuideAutoTool(Entity target,float3 hit,Game.Rendering.OverlayRenderSystem.Buffer buffer,bool apply,bool reset){
   var node=target;if(IsRoad(target)){var ends=EntityManager.GetComponentData<Edge>(target);node=math.distancesq(hit,CurveOf(target).a)<math.distancesq(hit,CurveOf(target).d)?ends.m_Start:ends.m_End;}
   if(!SafeNode(node)||!IsGuideJunction(node))return;
   var origin=EntityManager.GetComponentData<Node>(node).m_Position;
   float now=UnityEngine.Time.realtimeSinceStartup;
   if(node!=guideNode||guideRevision!=toolRevision){guideNode=node;guideRevision=toolRevision;guideCacheTime=now;guideCandidates.Clear();guideCandidates.AddRange(GuideCandidates(node));}
   float radius=6;foreach(var c in guideCandidates){radius=math.max(radius,math.distance(origin.xz,c.Source.a.xz)+2);radius=math.max(radius,math.distance(origin.xz,c.Source.d.xz)+2);}
   if(math.distance(hit.xz,origin.xz)>radius||math.abs(hit.y-origin.y)>4)return;
   buffer.DrawCircle(new Color(.12f,.8f,1,.18f),origin+new float3(0,.15f,0),radius*2);
   if(reset&&EntityManager.HasBuffer<GuideChoice>(node)&&EntityManager.GetBuffer<GuideChoice>(node,true).Length>0)ToolResetHandled=true;
   if(apply||reset&&ToolResetHandled)guideCommands.Enqueue(new GuideCommand{Node=node,Kind=reset?3:1});
  }
  List<GuideCandidate> GuideCandidates(Entity node){
   var result=new List<GuideCandidate>();if(!SafeNode(node))return result;
   foreach(var lane in Lanes(node)){
    if(!EntityManager.HasComponent<CarLane>(lane)||EntityManager.HasComponent<MasterLane>(lane))continue;
    var flags=EntityManager.GetComponentData<CarLane>(lane).m_Flags;if(!RoadSupportPolicy.Drive(flags))continue;
    var c=CurveOf(lane);if(math.distance(c.a,c.d)<1||math.abs(c.a.y-c.d.y)>12)continue;
    float offset=math.min(math.max(.5f,S.GuideLeftOffset),math.max(.5f,Width(lane)*.5f));
    var candidate=new GuideCandidate{Source=c,Paint=Geometry.Offset(c,offset),Flags=flags};
    candidate.Arc=new Geometry.Arc(candidate.Paint);
    if(!result.Exists(x=>GuidePolicy.Pair(x.Source.a,x.Source.d,c.a,c.d)))result.Add(candidate);
   }
   return result;
  }
  internal void GuideTool(Entity target,float3 hit,Game.Rendering.OverlayRenderSystem.Buffer buffer,bool apply,bool reset){
   Entity node=target;
   if(IsRoad(target)){var ends=EntityManager.GetComponentData<Edge>(target);node=math.distancesq(hit,CurveOf(target).a)<math.distancesq(hit,CurveOf(target).d)?ends.m_Start:ends.m_End;}
   if(guideStarted&&Live(guideNode))node=guideNode;
   if(!SafeNode(node)){if(reset)guideStarted=false;return;}
   float now=UnityEngine.Time.realtimeSinceStartup;
   if(node!=guideNode||guideRevision!=toolRevision){
    if(node!=guideNode)guideStarted=false;guideNode=node;guideRevision=toolRevision;guideCacheTime=now;
    guideCandidates.Clear();guideCandidates.AddRange(GuideCandidates(node));
    if(guideStarted&&!guideCandidates.Exists(x=>GuidePolicy.Point(x.Source.a,guideStart)))guideStarted=false;
   }
   GuideCandidate picked=null;float best=4;
   var points=guideHoverPoints;points.Clear();
   foreach(var candidate in guideCandidates){
    if(guideStarted&&!GuidePolicy.Point(candidate.Source.a,guideStart))continue;
    var p=guideStarted?candidate.Paint.d:candidate.Paint.a;
    float distance=math.distancesq(p.xz,hit.xz);
    if(math.abs(p.y-hit.y)<3&&distance<best){best=distance;picked=candidate;}
    if(!ContainsGuidePoint(points,p))points.Add(p);
   }
   foreach(var p in points)DrawGuidePoint(buffer,p,guideStarted?new Color(1,.72f,.15f,1):new Color(.12f,.85f,1,1));
   if(guideStarted){var start=guideCandidates.Find(x=>GuidePolicy.Point(x.Source.a,guideStart));if(start!=null)DrawGuidePoint(buffer,start.Paint.a,Color.white);}
   if(picked!=null){DrawGuidePoint(buffer,guideStarted?picked.Paint.d:picked.Paint.a,Color.white);if(guideStarted)buffer.DrawCurve(new Color(.2f,.9f,1,.85f),picked.Paint,.22f);}
   if(!guideStarted&&EntityManager.HasBuffer<GuideChoice>(node)){
    var origin=EntityManager.GetComponentData<Node>(node).m_Position;float nearest=2.25f;GuideCandidate hovered=null;
    foreach(var c in guideCandidates){if(!HasGuide(node,c.Source.a-origin,c.Source.d-origin))continue;float d=math.distancesq(Geometry.Position(c.Paint,c.Arc.Nearest(hit)),hit);if(d<nearest){nearest=d;hovered=c;}}
    if(hovered!=null)buffer.DrawCurve(new Color(1,.65f,.15f,.95f),hovered.Paint,.3f);
   }
   if(reset){
    if(guideStarted){ToolResetHandled=true;guideStarted=false;return;}
    if(EntityManager.HasBuffer<GuideChoice>(node)){
     var origin=EntityManager.GetComponentData<Node>(node).m_Position;float nearest=2.25f;GuideCandidate remove=null;
     foreach(var c in guideCandidates){if(!HasGuide(node,c.Source.a-origin,c.Source.d-origin))continue;var p=Geometry.Position(c.Paint,c.Arc.Nearest(hit));float d=math.distancesq(p,hit);if(d<nearest){nearest=d;remove=c;}}
     if(remove!=null){ToolResetHandled=true;guideCommands.Enqueue(new GuideCommand{Node=node,Kind=2,Choice=new GuideChoice{Start=remove.Source.a-origin,End=remove.Source.d-origin}});}
    }return;
   }
   if(!apply||picked==null)return;
   if(!guideStarted){guideStart=picked.Source.a;guideStarted=true;return;}
   var at=EntityManager.GetComponentData<Node>(node).m_Position;
   guideCommands.Enqueue(new GuideCommand{Node=node,Choice=new GuideChoice{Start=picked.Source.a-at,End=picked.Source.d-at}});guideStarted=false;
  }
  static Camera guidePointCamera;static int guidePointCameraFrame=-1;
  static void DrawGuidePoint(Game.Rendering.OverlayRenderSystem.Buffer buffer,float3 p,Color color){
   if(guidePointCameraFrame!=UnityEngine.Time.frameCount){guidePointCameraFrame=UnityEngine.Time.frameCount;guidePointCamera=Camera.main;}
   var camera=guidePointCamera;float scale=1;
   if(camera!=null&&camera.pixelHeight>0){
    var screen=camera.WorldToScreenPoint((Vector3)p);if(screen.z<=camera.nearClipPlane)return;
    // Horizontal world direction aligned with the camera: measure actual pixel
    // projection rather than assuming a fixed FOV or perspective camera.
    var right=camera.transform.right;right.y=0;right.Normalize();
    var other=camera.WorldToScreenPoint((Vector3)p+right);
    float pixels=Vector2.Distance(new Vector2(screen.x,screen.y),new Vector2(other.x,other.y));
    if(pixels>.0001f)scale=24f/(pixels*1.35f);
   }
   p.y+=.12f*scale;buffer.DrawCircle(new Color(.03f,.04f,.05f,.95f),p,1.35f*scale);buffer.DrawCircle(color,p+new float3(0,.015f*scale,0),.95f*scale);buffer.DrawCircle(Color.white,p+new float3(0,.03f*scale,0),.25f*scale);
  }
  internal void GuideAction(bool clear){if(GuideReady)guideCommands.Enqueue(new GuideCommand{Node=guideNode,Kind=clear?3:1});guideStarted=false;}
  bool HasGuide(Entity node,float3 start,float3 end){if(!EntityManager.HasBuffer<GuideChoice>(node))return false;foreach(var c in EntityManager.GetBuffer<GuideChoice>(node,true))if(GuidePolicy.Pair(c.Start,c.End,start,end))return true;return false;}
  void ReleaseGuides(Entity node){if(!guideRecords.TryGetValue(node,out var record))return;foreach(var e in record.Paint)if(Live(e)&&!EntityManager.HasComponent<Deleted>(e))RetirePaint(e);foreach(var e in record.Hidden)RestoreHidden(e,record);guideRecords.Remove(node);}
  void ClearGuides(){guideCommands.Clear();guideDirty.Clear();foreach(var node in new List<Entity>(guideRecords.Keys))ReleaseGuides(node);EndGuideEdit();}
  void RestoreGuideSelections(){using(var query=EntityManager.CreateEntityQuery(ComponentType.ReadOnly<GuideChoice>(),ComponentType.ReadOnly<Node>()))using(var nodes=query.ToEntityArray(Allocator.Temp))foreach(var node in nodes)if(EntityManager.GetBuffer<GuideChoice>(node,true).Length>0){guideDirty.Add(node);targetedActive=true;}}
  void TickGuides(){{
   if(guideCommands.Count==0&&guideDirty.Count==0)return;EntityManager.CompleteAllTrackedJobs();
   if(guideCommands.Count>0){var command=guideCommands.Dequeue();var node=command.Node;
    if(SafeNode(node)&&CachedAssetsReady()){
     var origin=EntityManager.GetComponentData<Node>(node).m_Position;var candidates=GuideCandidates(node);
     if(!EntityManager.HasBuffer<GuideChoice>(node))EntityManager.AddBuffer<GuideChoice>(node);
     var choices=EntityManager.GetBuffer<GuideChoice>(node);
     if(command.Kind==3)choices.Clear();
     else if(command.Kind==2){for(int i=choices.Length-1;i>=0;i--)if(GuidePolicy.Pair(choices[i].Start,choices[i].End,command.Choice.Start,command.Choice.End))choices.RemoveAt(i);}
     else foreach(var candidate in candidates){
      var c=candidate.Source;bool add=command.Kind==0?GuidePolicy.Pair(c.a-origin,c.d-origin,command.Choice.Start,command.Choice.End):MarkingPolicy.DrawLeftGuide(candidate.Flags,Geometry.LeftAngle(c),S.TurnAngle,true);
      if(add&&command.Kind==1)foreach(var other in candidates)if(other!=candidate&&MarkingPolicy.DrawLeftGuide(other.Flags,Geometry.LeftAngle(other.Source),S.TurnAngle,true)&&MarkingPolicy.GuidePrecedes(c,other.Source,candidates.IndexOf(other)<candidates.IndexOf(candidate))){add=false;break;}
      bool duplicate=false;foreach(var choice in choices)duplicate|=GuidePolicy.Pair(choice.Start,choice.End,c.a-origin,c.d-origin);
      if(add&&!duplicate)choices.Add(new GuideChoice{Start=c.a-origin,End=c.d-origin});
     }
     guideDirty.Add(node);if(EntityManager.HasBuffer<ConnectedEdge>(node))foreach(var edge in EntityManager.GetBuffer<ConnectedEdge>(node,true))QueueBoundaryGuides(edge.m_Edge);targetedActive=true;
    }
   }
   if(progressiveLoad&&guideCommands.Count==0){foreach(var node in new List<Entity>(guideDirty))if(GuideDependenciesPending(node)){guideDirty.Remove(node);DeferLoad(node,32);}}
   if(guideDirty.Count==0)return;Entity work=Entity.Null;foreach(var node in guideDirty){if(GuideDependenciesPending(node))continue;work=node;break;}if(work==Entity.Null)return;
   if(guideRecords.TryGetValue(work,out var previous))foreach(var e in previous.Paint)if(Live(e)&&EntityManager.HasComponent<Created>(e))return;
   guideDirty.Remove(work);ObserveOwnerShape(work);ReleaseGuides(work);if(!SafeNode(work)||!EntityManager.HasBuffer<GuideChoice>(work))return;
   var selected=new List<GuideChoice>();foreach(var c in EntityManager.GetBuffer<GuideChoice>(work,true))selected.Add(c);
   if(selected.Count==0)return;
   var position=EntityManager.GetComponentData<Node>(work).m_Position;var available=GuideCandidates(work);var crossings=NearbyCrossings(work);var limits=CrossingBoundaries(work,S.PaintClearance+.15f,true);var record=new Record();guideRecords[work]=record;
      try{
    if(selected.Count>0)CleanGuideInterior(work,record);
    foreach(var choice in selected){var candidate=available.Find(x=>GuidePolicy.Pair(x.Source.a-position,x.Source.d-position,choice.Start,choice.End));if(candidate==null)continue;
    foreach(var interior in EdgeApproachPolicy.Clip(candidate.Paint,limits))
    foreach(var clear in ClearPaint(interior,crossings,S.PaintClearance)){var arc=new Geometry.Arc(clear);float dash=math.clamp(S.GuideDash,.1f,5),gap=math.clamp(S.GuideGap,.5f,5);for(float from=.25f;from<arc.Length-.25f;from+=dash+gap)PaintLane(work,record,assets.Get(AssetCatalog.White),arc.Slice(from,math.min(from+dash,arc.Length-.25f)));}
   }if(EntityManager.HasBuffer<ConnectedEdge>(work))foreach(var edge in EntityManager.GetBuffer<ConnectedEdge>(work,true))if(EntityManager.HasBuffer<ManualDecalChoice>(edge.m_Edge))manualDirty.Add(edge.m_Edge);
   Status="유도선 적용 완료 / Guides applied: "+selected.Count;}
   catch(System.Exception ){failed++;ReleaseGuides(work);}  }}
 }
}


