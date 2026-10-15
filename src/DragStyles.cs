using System.Collections.Generic;
using Game.Net;
using Unity.Entities;
using Unity.Mathematics;
using UnityEngine;
namespace RoadStyleIntoKorea {
 public partial class EdgeStyleTool {
  readonly HashSet<Entity> dragRoads=new HashSet<Entity>();bool dragging;
  bool DragMode=>IsLocalStylesMode||IsApproachMode||IsDiagnosticMode;
  void CancelStyleDrag(){dragging=false;dragRoads.Clear();}
  bool UpdateStyleDrag(StyleSystem system){
   if(!DragMode){CancelStyleDrag();return false;}
   var input=Game.Input.InputManager.instance;var mouse=UnityEngine.InputSystem.Mouse.current;
   if(!Application.isFocused||input.hasInputFieldFocus||mouse==null){CancelStyleDrag();return true;}
   if(secondaryApplyAction.WasPressedThisFrame()){bool wasDragging=dragging;CancelStyleDrag();if(wasDragging)return true;return false;}
   if(dragging&&!mouse.leftButton.isPressed){
    if(input.controlOverWorld&&!input.overlayActive)system.QueueDraggedStyles(dragRoads,IsDiagnosticMode?4:IsApproachMode?0:(prefab as EdgeToolPrefab).RemoveAll?2:1);
    CancelStyleDrag();return true;
   }
   if(!input.controlOverWorld||input.overlayActive)return true;
   if(!GetRaycastResult(out Entity owner,out Game.Common.RaycastHit hit)){Hover(Entity.Null);return true;}
   var road=hit.m_HitEntity;if(!system.LocalStyleTarget(road))road=owner;
   if(system.IsRoad(road)&&EntityManager.HasComponent<Curve>(road)){
    if(applyAction.WasPressedThisFrame()){dragging=true;dragRoads.Clear();}
    if(dragging&&dragRoads.Count<256)dragRoads.Add(road);
    Hover(road);
   }else if(system.LocalStyleTarget(road)&&EntityManager.HasBuffer<ConnectedEdge>(road)){
    if(applyAction.WasPressedThisFrame()){dragging=true;dragRoads.Clear();}
    if(dragging)foreach(var c in EntityManager.GetBuffer<ConnectedEdge>(road,true))if(dragRoads.Count<256&&system.IsRoad(c.m_Edge))dragRoads.Add(c.m_Edge);
    Hover(road);
   }else Hover(Entity.Null);
   if(dragging){
    var render=World.GetOrCreateSystemManaged<Game.Rendering.OverlayRenderSystem>();var draw=render.GetBuffer(out var job);job.Complete();
    foreach(var selected in dragRoads)if(system.IsRoad(selected)&&EntityManager.HasComponent<Curve>(selected))draw.DrawCurve(new Color(.1f,.85f,1,.6f),EntityManager.GetComponentData<Curve>(selected).m_Bezier,1.2f);
    render.AddBufferWriter(default);
   }
   return true;
  }
 }
 public partial class StyleSystem {
  struct DragStyleCommand {internal Entity Road;internal int Mode;internal bool SuppressRoadSigns;internal List<Entity> RemovalRoads;}
  readonly WorkQueue<DragStyleCommand> dragStyleCommands=new WorkQueue<DragStyleCommand>();
  internal void QueueDraggedStyles(IEnumerable<Entity> selection,int mode){
   if(mode==4){CaptureNetworks(selection);return;}
   if(!S.Enabled||restoring)return;
   var camera=World.GetExistingSystemManaged<Game.Rendering.CameraUpdateSystem>();
   float3 focus=camera?.activeCameraController!=null?camera.activeCameraController.pivot:float3.zero;
   var sorted=new List<KeyValuePair<Entity,float>>();foreach(var road in selection)if(IsRoad(road))sorted.Add(new KeyValuePair<Entity,float>(road,LoadDistance(road,focus)));
   sorted.Sort((a,b)=>{int c=a.Value.CompareTo(b.Value);return c!=0?c:a.Key.Index.CompareTo(b.Key.Index);});
   Entity signRoad=Entity.Null;float longest=-1;
   if(mode==1)foreach(var road in sorted){
    float length=EntityManager.GetComponentData<Curve>(road.Key).m_Length;
    if(length>longest||(length==longest&&road.Key.Index<signRoad.Index)){longest=length;signRoad=road.Key;}
   }
   if(mode==2){
    for(int i=0;i<sorted.Count;i+=4){
     var batch=new List<Entity>();for(int j=i;j<sorted.Count&&j<i+4;j++)batch.Add(sorted[j].Key);
     dragStyleCommands.Enqueue(new DragStyleCommand{Road=batch[0],Mode=mode,RemovalRoads=batch});
    }
   }else foreach(var road in sorted)dragStyleCommands.Enqueue(new DragStyleCommand{Road=road.Key,Mode=mode,SuppressRoadSigns=mode==1&&sorted.Count>1&&road.Key!=signRoad});
   targetedActive=true;Status="선택 구간 처리 대기 / Selected segments queued: "+sorted.Count;
  }
  void TickDraggedStyles(){
   if(dragStyleCommands.Count==0||!S.Enabled||restoring||defaultsReplacing||HasStyleLoadWork()||nativeCrossingRequests.Count>0||nativeCrossingWaiting.Count>0)return;
   var command=dragStyleCommands.Dequeue();
   if(command.RemovalRoads!=null){
    var batch=command.RemovalRoads;
    localStyleCommands.Enqueue(()=>{
{
      var nodes=new HashSet<Entity>();
      foreach(var road in batch){
       if(!IsRoad(road))continue;var ends=EntityManager.GetComponentData<Edge>(road);
       nodes.Add(ends.m_Start);nodes.Add(ends.m_End);RemoveLocalRoad(road,true,road);
      }
      foreach(var node in nodes){
       if(!Live(node))continue;
       EntityManager.RemoveComponent<RemovedPaintChoice>(node);EntityManager.RemoveComponent<DrawnLineChoice>(node);manualDirty.Add(node);
       if(SafeNode(node))guideCommands.Enqueue(new GuideCommand{Node=node,Kind=3});
      }
     }
    });
    return;
   }
   if(!IsRoad(command.Road))return;
   if(command.Mode==0)QueueApproach(command.Road,false);
   else ChooseLocalStyles(command.Road,command.Mode==2,false,command.SuppressRoadSigns);
  }
 }
}
