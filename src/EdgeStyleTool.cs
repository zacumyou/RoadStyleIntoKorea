using System;
using Game;
using Game.Common;
using Game.Prefabs;
using Game.Tools;
using Unity.Collections;
using Unity.Entities;
using Unity.Jobs;
using Unity.Mathematics;
using UnityEngine;
namespace RoadStyleIntoKorea
{
    public class EdgeToolPrefab:PrefabBase { public bool Crosswalk; public bool CrossingDistance; public bool DrawLine; public bool ColorGuide; public bool RedPaving; public bool Paving; public bool Curb; public bool Gutter; public bool SideRoad; public bool RemovePaint; public bool Center; public bool UTurn; public bool Guide; public bool GuideAuto; public bool Approach; public bool AddAll; public bool RemoveAll; public int ManualDecal; public int Divider; }
    public partial class EdgeStyleTool:ToolBaseSystem
    {
        PrefabBase prefab;
        bool CrosswalkMode=>(prefab as EdgeToolPrefab)?.Crosswalk==true;
        internal bool IsCrosswalkMode=>CrosswalkMode;
 internal bool IsRedPavingMode=>(prefab as EdgeToolPrefab)?.RedPaving==true;
 internal bool IsColorGuideMode=>(prefab as EdgeToolPrefab)?.ColorGuide==true;
 internal bool IsDrawLineMode=>(prefab as EdgeToolPrefab)?.DrawLine==true;
 internal bool IsCrossingDistanceMode=>(prefab as EdgeToolPrefab)?.CrossingDistance==true;
        internal bool IsRemovePaintMode=>(prefab as EdgeToolPrefab)?.RemovePaint==true; internal bool IsGutterMode=>(prefab as EdgeToolPrefab)?.Gutter==true; internal bool IsCurbMode=>(prefab as EdgeToolPrefab)?.Curb==true;
        internal bool IsPavingMode=>(prefab as EdgeToolPrefab)?.Paving==true;
        internal bool IsSideRoadMode=>(prefab as EdgeToolPrefab)?.SideRoad==true;
        internal bool IsCenterMode=>(prefab as EdgeToolPrefab)?.Center==true;
        internal bool IsUTurnMode=>(prefab as EdgeToolPrefab)?.UTurn==true;
        internal bool IsGuideMode=>(prefab as EdgeToolPrefab)?.Guide==true;
        internal bool IsGuideAutoMode=>(prefab as EdgeToolPrefab)?.GuideAuto==true;
        internal int DividerGroup=>(prefab as EdgeToolPrefab)?.Divider??0;
        internal int ManualDecalKind=>(prefab as EdgeToolPrefab)?.ManualDecal??0;
        internal bool IsApproachMode=>(prefab as EdgeToolPrefab)?.Approach==true;
        internal bool IsLocalStylesMode=>(prefab as EdgeToolPrefab)?.AddAll==true||(prefab as EdgeToolPrefab)?.RemoveAll==true;
        void ExitOnReset(){if(secondaryApplyAction.WasPressedThisFrame()){Hover(Entity.Null);World.GetOrCreateSystemManaged<ToolSystem>().activeTool=World.GetOrCreateSystemManaged<DefaultToolSystem>();}}
        bool actionGuideAutoMode;
        bool actionGuideMode;
        bool actionUTurnMode;
        bool actionCrosswalkMode,actionPavingMode,actionCenterMode,actionSideRoadMode;
        Geometry.Arc hoverArc;
        Entity hovered;bool ownHighlight;
        void Hover(Entity edge)
        {
            if(edge==hovered)return;
            if(ownHighlight&&EntityManager.Exists(hovered))
            {
                EntityManager.RemoveComponent<Highlighted>(hovered);
                if(!EntityManager.HasComponent<BatchesUpdated>(hovered))EntityManager.AddComponent<BatchesUpdated>(hovered);
            }
            hovered=edge;ownHighlight=false;
            if(edge!=Entity.Null&&!EntityManager.HasComponent<Highlighted>(edge))
            {
                EntityManager.AddComponent<Highlighted>(edge);ownHighlight=true;
                if(!EntityManager.HasComponent<BatchesUpdated>(edge))EntityManager.AddComponent<BatchesUpdated>(edge);
            }
        }
        protected override void OnStopRunning(){CancelStyleDrag();World.GetExistingSystemManaged<StyleSystem>()?.EndGuideEdit();World.GetExistingSystemManaged<StyleSystem>()?.EndLineEdit();Hover(Entity.Null);base.OnStopRunning();}
        internal static Color BrushColor(EdgeStyle style,float alpha)=>style==EdgeStyle.WhiteSolid?new Color(177f/255,177f/255,176f/255,alpha):
            style>=EdgeStyle.YellowSolid?new Color(191f/255,150f/255,47f/255,alpha):new Color(.55f,.6f,.65f,alpha);
        internal static string BrushIcon(EdgeStyle style)=>"coui://rsik/Brush"+(style==EdgeStyle.WhiteSolid?"White":style>=EdgeStyle.YellowSolid?"Yellow":"None")+".svg";
        public override string toolID=>"RoadStyleIntoKorea.Edge";
        public override bool allowUnderground=>true;
        public override void ElevationDown()=>SetUnderground(true);
        public override void ElevationUp()=>SetUnderground(false);
        public override void ElevationScroll()=>SetUnderground(!requireUnderground);
        public override void SetUnderground(bool underground){if(requireUnderground==underground)return;CancelStyleDrag();Hover(Entity.Null);requireUnderground=underground;}
        public override PrefabBase GetPrefab()=>prefab;
        public override bool TrySetPrefab(PrefabBase p){if(!(p is EdgeToolPrefab))return false;if(prefab!=p){CancelStyleDrag();World.GetExistingSystemManaged<StyleSystem>()?.EndGuideEdit();World.GetExistingSystemManaged<StyleSystem>()?.EndLineEdit();}prefab=p;return true;}
        public override void InitializeRaycast(){base.InitializeRaycast();m_ToolRaycastSystem.typeMask=TypeMask.Net;m_ToolRaycastSystem.netLayerMask=Game.Net.Layer.Road;m_ToolRaycastSystem.collisionMask=requireUnderground?CollisionMask.Underground:CollisionMask.OnGround|CollisionMask.Overground;m_ToolRaycastSystem.raycastFlags|=RaycastFlags.SubElements;if(IsColorGuideMode){m_ToolRaycastSystem.raycastFlags|=RaycastFlags.Markers;m_ToolRaycastSystem.netLayerMask|=Game.Net.Layer.PublicTransportRoad;}}
        Game.Input.IProxyAction NativeAction(string name)
        {
            var property=typeof(Game.Input.InputManager).GetProperty("toolActionCollection",System.Reflection.BindingFlags.Instance|System.Reflection.BindingFlags.NonPublic);
            var collection=property?.GetValue(Game.Input.InputManager.instance) as Game.Input.UIInputActionCollection;
            if(collection==null){return null;}
            foreach(var alias in collection.m_InputActions)if(alias.aliasName==name)return alias.GetState(GetType().Name,(source,action,mask,transform)=>new Game.Input.DisplayNameOverride(source,action,IsSideRoadMode?(name=="Upgrade Net Edge"?"RSIK Add side road":"RSIK Remove side road"):IsUTurnMode?(name=="Upgrade Net Edge"?"RSIK Add U-turn zone":"RSIK Remove U-turn zone"):IsCenterMode?(name=="Upgrade Net Edge"?"RSIK Apply center":"RSIK Reset center"):IsPavingMode?ToolText.PavingAction(name=="Upgrade Net Edge"):ToolText.Action(CrosswalkMode,name=="Upgrade Net Edge"),alias.displayPriority,Game.Input.InputManager.DeviceType.None,transform));
            return null;
        }
        internal void CycleSelection(){var system=World.GetExistingSystemManaged<StyleSystem>();if(system==null)return;if(IsRedPavingMode)system.CycleRedPaving();else if(IsColorGuideMode)system.CycleColorGuideBrush();else if(IsDrawLineMode)system.CycleLineBrush();else if(IsCrossingDistanceMode)return;else if(IsSideRoadMode)system.AdjustSideRoadWidth(1,true);else if(DividerGroup>0)system.CycleDivider(DividerGroup);else if(IsGutterMode)system.CycleGutterBrush();else if(ManualDecalKind==8)system.CycleHump();else if((ManualDecalKind==5||ManualDecalKind==6)&&!system.SignSearchFocused)system.CycleRoadSign(ManualDecalKind);else if(ManualDecalKind==2)system.CycleManualArrow();else if(ManualDecalKind>0||IsRemovePaintMode||IsCurbMode||IsGuideMode||IsGuideAutoMode||IsUTurnMode||IsApproachMode||IsLocalStylesMode||IsSideRoadMode){}else if(IsCenterMode)system.CycleCenterBrush();else if(IsPavingMode)system.CyclePavingBrush();else if(CrosswalkMode)system.CycleCrosswalkBrush();else system.CycleEdgeBrush();}
        void RefreshActions(){actionGuideAutoMode=IsGuideAutoMode;actionGuideMode=IsGuideMode;actionUTurnMode=IsUTurnMode;actionCenterMode=IsCenterMode;actionCrosswalkMode=CrosswalkMode;actionPavingMode=IsPavingMode;actionSideRoadMode=IsSideRoadMode;applyActionOverride=NativeAction("Upgrade Net Edge");secondaryApplyActionOverride=NativeAction("Downgrade Net Edge");applyAction.shouldBeEnabled=true;secondaryApplyAction.shouldBeEnabled=true;cancelAction.shouldBeEnabled=false;}
        protected override void OnStartRunning(){base.OnStartRunning();RefreshActions();}
        protected override JobHandle OnUpdate(JobHandle deps)
        {
            var uiInput=Game.Input.InputManager.instance;
            if(World.GetExistingSystemManaged<StyleSystem>()?.MenuSearchFocused==true||!Application.isFocused||!uiInput.controlOverWorld||uiInput.hasInputFieldFocus||uiInput.overlayActive){
                deps.Complete();CancelStyleDrag();World.GetExistingSystemManaged<StyleSystem>()?.CancelLineDrag();Hover(Entity.Null);return deps;
            }
            deps.Complete();var system=World.GetExistingSystemManaged<StyleSystem>();if(system!=null)system.ToolResetHandled=false;
            if(system!=null&&IsPavingMode)system.PavingHint="RSIK.Paving.NoTarget";
            if(actionGuideAutoMode!=IsGuideAutoMode||actionGuideMode!=IsGuideMode||actionUTurnMode!=IsUTurnMode||actionCenterMode!=IsCenterMode||actionCrosswalkMode!=CrosswalkMode||actionPavingMode!=IsPavingMode||actionSideRoadMode!=IsSideRoadMode)RefreshActions();
            if(system!=null&&(applyAction.WasPressedThisFrame()||secondaryApplyAction.WasPressedThisFrame()))system.PrioritizeUserEdit();
            var keyboard=UnityEngine.InputSystem.Keyboard.current;
            if(system!=null&&Application.isFocused&&!Game.Input.InputManager.instance.hasInputFieldFocus&&keyboard!=null&&keyboard.rKey.wasPressedThisFrame&&(keyboard.leftShiftKey.isPressed||keyboard.rightShiftKey.isPressed)){CycleSelection();}
            if(system!=null&&IsSideRoadMode&&Application.isFocused&&!Game.Input.InputManager.instance.hasInputFieldFocus&&keyboard!=null){
             if(keyboard.numpadPlusKey.wasPressedThisFrame||keyboard.equalsKey.wasPressedThisFrame)system.AdjustSideRoadWidth(1);
             if(keyboard.numpadMinusKey.wasPressedThisFrame||keyboard.minusKey.wasPressedThisFrame)system.AdjustSideRoadWidth(-1);
            }
            int zoneDelta=0;
            if(system!=null&&IsUTurnMode&&Application.isFocused&&!Game.Input.InputManager.instance.hasInputFieldFocus&&keyboard!=null){
             if(keyboard.numpadPlusKey.wasPressedThisFrame||keyboard.equalsKey.wasPressedThisFrame)zoneDelta++;
             if(keyboard.numpadMinusKey.wasPressedThisFrame||keyboard.minusKey.wasPressedThisFrame)zoneDelta--;
            }
            if(system!=null&&UpdateStyleDrag(system))return deps;
            if(system==null||!GetRaycastResult(out Entity owner,out RaycastHit hit)){if(zoneDelta!=0)system?.AdjustUTurnLength(Entity.Null,zoneDelta);system?.CancelLineDrag();if(IsRedPavingMode&&UnityEngine.InputSystem.Mouse.current?.leftButton.wasPressedThisFrame==true)system?.CancelRedPavingClick();Hover(Entity.Null);ExitOnReset();return deps;}
            if(IsDrawLineMode||IsColorGuideMode||IsRedPavingMode){
 Hover(Entity.Null);var target=system.LineTarget(hit.m_HitEntity,owner,IsColorGuideMode);
 var lineOverlay=World.GetOrCreateSystemManaged<Game.Rendering.OverlayRenderSystem>();var lineBuffer=lineOverlay.GetBuffer(out var lineWriter);lineWriter.Complete();
 var mouse=UnityEngine.InputSystem.Mouse.current;
 if(mouse!=null&&IsRedPavingMode)system.RedPavingTool(target,hit.m_HitPosition,lineBuffer,mouse.leftButton.wasPressedThisFrame,mouse.rightButton.wasPressedThisFrame,keyboard!=null&&(keyboard.leftShiftKey.isPressed||keyboard.rightShiftKey.isPressed));
 else if(mouse!=null)system.DrawLineTool(target,hit.m_HitPosition,lineBuffer,mouse.leftButton.wasPressedThisFrame,mouse.leftButton.isPressed,mouse.leftButton.wasReleasedThisFrame,mouse.rightButton.wasPressedThisFrame,IsColorGuideMode);
 lineOverlay.AddBufferWriter(default);return deps;
 }
 if(IsCrossingDistanceMode){
 var target=system.CrossingDistanceTarget(hit.m_HitEntity,hit.m_HitPosition);
 if(target==Entity.Null)target=system.CrossingDistanceTarget(owner,hit.m_HitPosition);
 Hover(Entity.Null);system.CrossingDistanceHover=target;
 if(target!=Entity.Null){var distanceOverlay=World.GetOrCreateSystemManaged<Game.Rendering.OverlayRenderSystem>();var distanceBuffer=distanceOverlay.GetBuffer(out var writer);writer.Complete();system.DrawCrossingDistanceTarget(target,distanceBuffer);distanceOverlay.AddBufferWriter(default);}
 var mouse=UnityEngine.InputSystem.Mouse.current;
 if(target!=Entity.Null&&mouse!=null&&mouse.leftButton.wasPressedThisFrame)system.AdjustCrossingDistance(target,keyboard!=null&&(keyboard.leftShiftKey.isPressed||keyboard.rightShiftKey.isPressed)?-1:1);
 ExitOnReset();return deps;
 }
 if(IsRemovePaintMode){var target=hit.m_HitEntity;if(!system.LocalStyleTarget(target))target=owner;Hover(Entity.Null);var removeOverlay=World.GetOrCreateSystemManaged<Game.Rendering.OverlayRenderSystem>();var removeBuffer=removeOverlay.GetBuffer(out var writer);writer.Complete();bool handled=system.RemovePaintTool(target,hit.m_HitPosition,removeBuffer,applyAction.WasPressedThisFrame(),secondaryApplyAction.WasPressedThisFrame());removeOverlay.AddBufferWriter(default);if(secondaryApplyAction.WasPressedThisFrame()&&!handled)ExitOnReset();return deps;} if(IsLocalStylesMode){
                var target=hit.m_HitEntity;if(!system.LocalStyleTarget(target))target=owner;
                if(!system.LocalStyleTarget(target)){Hover(Entity.Null);ExitOnReset();return deps;}
                Hover(target);
                if(secondaryApplyAction.WasPressedThisFrame()){ExitOnReset();return deps;}
                if(applyAction.WasPressedThisFrame())system.ChooseLocalStyles(target,(prefab as EdgeToolPrefab).RemoveAll);
                return deps;
            }
            if(IsGuideMode||IsGuideAutoMode){
                Hover(Entity.Null);var target=hit.m_HitEntity;if(!system.IsRoad(target)&&!EntityManager.HasComponent<Game.Net.Node>(target))target=owner;
                var guideOverlay=World.GetOrCreateSystemManaged<Game.Rendering.OverlayRenderSystem>();var guideBuffer=guideOverlay.GetBuffer(out var job);job.Complete();if(IsGuideAutoMode)system.GuideAutoTool(target,hit.m_HitPosition,guideBuffer,applyAction.WasPressedThisFrame(),secondaryApplyAction.WasPressedThisFrame());else system.GuideTool(target,hit.m_HitPosition,guideBuffer,applyAction.WasPressedThisFrame(),secondaryApplyAction.WasPressedThisFrame());guideOverlay.AddBufferWriter(default);if(!system.ToolResetHandled)ExitOnReset();return deps;}
            var edge=hit.m_HitEntity;if(!system.IsRoad(edge))edge=owner;
            if(CrosswalkMode&&keyboard!=null&&(keyboard.leftCtrlKey.isPressed||keyboard.rightCtrlKey.isPressed)){
                var input=Game.Input.InputManager.instance;
                if(!Application.isFocused||!input.controlOverWorld||input.hasInputFieldFocus||input.overlayActive){Hover(Entity.Null);return deps;}
                var target=hit.m_HitEntity;if(!system.IsRoad(target)&&!EntityManager.HasComponent<Game.Net.Node>(target))target=owner;
                var node=system.CrosswalkJunction(target,hit.m_HitPosition);Hover(node);
                if(node==Entity.Null){ExitOnReset();return deps;}
                var render=World.GetOrCreateSystemManaged<Game.Rendering.OverlayRenderSystem>();var draw=render.GetBuffer(out var job);job.Complete();
                var mouse=UnityEngine.InputSystem.Mouse.current;bool click=mouse!=null&&mouse.leftButton.wasPressedThisFrame;
                system.JunctionCrosswalkTool(node,draw,click,keyboard.leftShiftKey.isPressed||keyboard.rightShiftKey.isPressed);render.AddBufferWriter(default);ExitOnReset();return deps;
            }
            if((IsPavingMode||IsCurbMode||IsGutterMode)&&(EntityManager.HasComponent<Game.Net.Node>(hit.m_HitEntity)||!system.IsRoad(edge))){
                var node=EntityManager.HasComponent<Game.Net.Node>(hit.m_HitEntity)?hit.m_HitEntity:owner;
                if(system.ResolveNodeSurfaceTarget(node,hit.m_HitPosition,IsGutterMode?2:IsCurbMode?1:0,out var nearest,out var surfaceHit)){edge=nearest;hit.m_HitPosition=surfaceHit;}
            }
            if(CrosswalkMode){var crossTarget=hit.m_HitEntity;if(!system.IsRoad(crossTarget)&&!EntityManager.HasComponent<Game.Net.Node>(crossTarget))crossTarget=owner;if(!system.ResolveCrosswalkTarget(crossTarget,hit.m_HitPosition,out edge,out _,out _)){Hover(Entity.Null);ExitOnReset();return deps;}}
            if(!system.IsRoad(edge)||!EntityManager.HasComponent<Game.Net.Curve>(edge)){if(zoneDelta!=0)system.AdjustUTurnLength(Entity.Null,zoneDelta);Hover(Entity.Null);ExitOnReset();return deps;}
            system.PrepareToolTarget(edge);
            if(IsSideRoadMode){
                Hover(Entity.Null);
                // The native Upgrade Net Edge action can reject clicks on a road's
                // sidewalk subelement. The raycast and world-input guards above
                // already establish a valid target for this visual-only tool.
                var mouse=UnityEngine.InputSystem.Mouse.current;
                bool apply=applyAction.WasPressedThisFrame()||mouse!=null&&mouse.leftButton.wasPressedThisFrame;
                bool reset=secondaryApplyAction.WasPressedThisFrame()||mouse!=null&&mouse.rightButton.wasPressedThisFrame;
                if(apply||reset)system.PrioritizeUserEdit();
                var render=World.GetOrCreateSystemManaged<Game.Rendering.OverlayRenderSystem>();var draw=render.GetBuffer(out var job);job.Complete();
                bool handled=system.SideRoadTool(edge,hit.m_HitPosition,draw,apply,reset);
                render.AddBufferWriter(default);
                if(reset&&!handled)ExitOnReset();return deps;
            }
            if(ManualDecalKind==9||ManualDecalKind==10){
                Hover(Entity.Null);var render=World.GetOrCreateSystemManaged<Game.Rendering.OverlayRenderSystem>();var draw=render.GetBuffer(out var job);job.Complete();
                var mouse=UnityEngine.InputSystem.Mouse.current;bool apply=applyAction.WasPressedThisFrame()||mouse!=null&&mouse.leftButton.wasPressedThisFrame;
                bool reset=secondaryApplyAction.WasPressedThisFrame()||mouse!=null&&mouse.rightButton.wasPressedThisFrame;
                system.SidewalkDetailTool(edge,hit.m_HitPosition,ManualDecalKind,draw,apply,reset);render.AddBufferWriter(default);return deps;
            }
            if(IsCurbMode||IsGutterMode){Hover(Entity.Null);var render=World.GetOrCreateSystemManaged<Game.Rendering.OverlayRenderSystem>();var draw=render.GetBuffer(out var job);job.Complete();
                bool handled=IsGutterMode?system.GutterTool(edge,hit.m_HitPosition,draw,applyAction.WasPressedThisFrame(),secondaryApplyAction.WasPressedThisFrame()):system.CurbTool(edge,hit.m_HitPosition,draw,applyAction.WasPressedThisFrame(),secondaryApplyAction.WasPressedThisFrame());render.AddBufferWriter(default);if(secondaryApplyAction.WasPressedThisFrame()&&!handled)ExitOnReset();return deps;}
            if(DividerGroup>0){Hover(Entity.Null);var input=Game.Input.InputManager.instance;if(!input.controlOverWorld||input.hasInputFieldFocus)return deps;var render=World.GetOrCreateSystemManaged<Game.Rendering.OverlayRenderSystem>();var draw=render.GetBuffer(out var job);job.Complete();system.DividerTool(edge,hit.m_HitPosition,DividerGroup,draw,applyAction.WasPressedThisFrame(),secondaryApplyAction.WasPressedThisFrame());render.AddBufferWriter(default);return deps;}
            if(ManualDecalKind>0){
                if(system.SignSearchFocused||!Game.Input.InputManager.instance.controlOverWorld||Game.Input.InputManager.instance.hasInputFieldFocus){Hover(Entity.Null);return deps;}
                Hover(Entity.Null);var render=World.GetOrCreateSystemManaged<Game.Rendering.OverlayRenderSystem>();var draw=render.GetBuffer(out var job);job.Complete();
                bool shift=keyboard!=null&&(keyboard.leftShiftKey.isPressed||keyboard.rightShiftKey.isPressed);
                var input=Game.Input.InputManager.instance;var mouse=UnityEngine.InputSystem.Mouse.current;
                // The native upgrade alias excludes Shift. Only accept the fallback over a valid world hit.
                bool reverseShared=keyboard!=null&&(keyboard.leftAltKey.isPressed||keyboard.rightAltKey.isPressed);
                bool place=applyAction.WasPressedThisFrame()||((shift||reverseShared)&&Application.isFocused&&input.controlOverWorld&&!input.hasInputFieldFocus&&!input.overlayActive&&mouse!=null&&mouse.leftButton.wasPressedThisFrame);
                if(place)system.PrioritizeUserEdit();
                bool handled=system.ManualDecalTool(edge,hit.m_HitPosition,ManualDecalKind,draw,place,secondaryApplyAction.WasPressedThisFrame(),shift,reverseShared);render.AddBufferWriter(default);
                if(secondaryApplyAction.WasPressedThisFrame()&&!handled)ExitOnReset();return deps;
            }
            if(secondaryApplyAction.WasPressedThisFrame()){
                bool affected=IsApproachMode?EntityManager.HasComponent<ApproachChoice>(edge)&&EntityManager.GetComponentData<ApproachChoice>(edge).Enabled:IsUTurnMode?EntityManager.HasComponent<UTurnChoice>(edge):IsCenterMode?EntityManager.HasComponent<CenterChoice>(edge)&&(EntityManager.GetComponentData<CenterChoice>(edge).Left!=0||EntityManager.GetComponentData<CenterChoice>(edge).Right!=0):IsPavingMode?EntityManager.HasBuffer<PavingChoice>(edge)&&EntityManager.GetBuffer<PavingChoice>(edge,true).Length>0:CrosswalkMode?EntityManager.HasComponent<CrosswalkChoice>(edge)&&(EntityManager.GetComponentData<CrosswalkChoice>(edge).Start!=0||EntityManager.GetComponentData<CrosswalkChoice>(edge).End!=0):EntityManager.HasComponent<EdgeChoice>(edge)&&(EntityManager.GetComponentData<EdgeChoice>(edge).Left!=0||EntityManager.GetComponentData<EdgeChoice>(edge).Right!=0);
                if(!affected){ExitOnReset();return deps;}
            }
            if(IsApproachMode){
                Hover(edge);
                if(applyAction.WasPressedThisFrame())system.QueueApproach(edge,false);
                else if(secondaryApplyAction.WasPressedThisFrame())system.QueueApproach(edge,true);
                return deps;
            }
            if(IsUTurnMode){
                if(zoneDelta!=0)system.AdjustUTurnLength(edge,zoneDelta);
                Hover(Entity.Null);var render=World.GetOrCreateSystemManaged<Game.Rendering.OverlayRenderSystem>();var draw=render.GetBuffer(out var job);job.Complete();system.UTurnTool(edge,hit.m_HitPosition,draw,applyAction.WasPressedThisFrame(),secondaryApplyAction.WasPressedThisFrame());render.AddBufferWriter(default);return deps;}
            if(IsCenterMode)
            {
                Hover(Entity.Null);var centerOverlay=World.GetOrCreateSystemManaged<Game.Rendering.OverlayRenderSystem>();
                var centerBuffer=centerOverlay.GetBuffer(out var centerDeps);centerDeps.Complete();
                float selected=system.DrawCenterPreview(edge,hit.m_HitPosition,centerBuffer);
                centerOverlay.AddBufferWriter(default);
                if(applyAction.WasPressedThisFrame())system.ChooseCenter(edge,selected,false);
                else if(secondaryApplyAction.WasPressedThisFrame()){system.ChooseCenter(edge,selected,true);if(!system.ToolResetHandled)ExitOnReset();}
                return deps;
            }
            if(IsPavingMode)
            {
                Hover(Entity.Null);
                var region=system.PickPaving(edge,hit.m_HitPosition);
                if(region==null){ExitOnReset();return deps;}
                if(region!=null)
                {
                    var pavingOverlay=World.GetOrCreateSystemManaged<Game.Rendering.OverlayRenderSystem>();
                    var pavingBuffer=pavingOverlay.GetBuffer(out var pavingDeps);pavingDeps.Complete();
                    bool canApply=Mod.Options!=null&&Mod.Options.Enabled&&Mod.Options.Paving;
                    var color=canApply?new Color(.1f,.85f,1,.35f):new Color(1,.2f,.2f,.35f);
                    foreach(var part in system.PavingGroup(edge,region)){pavingBuffer.DrawCurve(color,part.First,math.max(0,part.PaintMax-part.PaintMin));pavingBuffer.DrawCurve(color,part.Second,math.max(0,part.PaintMax-part.PaintMin));}
                    pavingOverlay.AddBufferWriter(default);
                    if(applyAction.WasPressedThisFrame())system.ChoosePaving(edge,region,false);
                    else if(secondaryApplyAction.WasPressedThisFrame()){system.ChoosePaving(edge,region,true);if(!system.ToolResetHandled)ExitOnReset();}
                }
                return deps;
            }
            Hover(Entity.Null);
            var c=EntityManager.GetComponentData<Game.Net.Curve>(edge).m_Bezier;if(hoverArc==null||!Geometry.SameCurve(hoverArc.Curve,c))hoverArc=new Geometry.Arc(c);float t=hoverArc.Nearest(hit.m_HitPosition);
            var position=Geometry.Position(c,t);bool left=math.dot(hit.m_HitPosition-position,Geometry.Left(Geometry.Tangent(c,t)))>=0;
            var overlay=World.GetOrCreateSystemManaged<Game.Rendering.OverlayRenderSystem>();var buffer=overlay.GetBuffer(out var drawDeps);drawDeps.Complete();
            bool enabled=Mod.Options!=null&&Mod.Options.Enabled;
            system.DrawMarkingPreview(edge,hit.m_HitPosition,CrosswalkMode,left,buffer,
                enabled?(CrosswalkMode?new Color(.1f,.85f,1,.65f):BrushColor(Mod.Options.Brush,.65f)):new Color(1,.2f,.2f,.65f));
            if(!CrosswalkMode)buffer.DrawCircle(BrushColor(Mod.Options.Brush,.95f),hit.m_HitPosition+new float3(0,.03f,0),.45f);
            overlay.AddBufferWriter(default);
            bool crossShift=keyboard!=null&&(keyboard.leftShiftKey.isPressed||keyboard.rightShiftKey.isPressed);
            bool crossApply=applyAction.WasPressedThisFrame()||CrosswalkMode&&crossShift&&Application.isFocused&&!Game.Input.InputManager.instance.hasInputFieldFocus&&UnityEngine.InputSystem.Mouse.current!=null&&UnityEngine.InputSystem.Mouse.current.leftButton.wasPressedThisFrame;
            if(crossApply){if(CrosswalkMode)system.ChooseCrosswalk(edge,hit.m_HitPosition,false,crossShift);else system.ChooseEdge(edge,left,false);}
            else if(secondaryApplyAction.WasPressedThisFrame()){if(CrosswalkMode)system.ChooseCrosswalk(edge,hit.m_HitPosition,true);else system.ChooseEdge(edge,left,true);if(!system.ToolResetHandled)ExitOnReset();}
            return deps;
        }
    }
    public partial class EdgeMenuSystem:GameSystemBase {
        internal readonly System.Collections.Generic.Dictionary<string,EdgeToolPrefab> Tools=new System.Collections.Generic.Dictionary<string,EdgeToolPrefab>();
        protected override void OnUpdate(){
            if(Tools.Count==30)return;
            var ps=World.GetExistingSystemManaged<PrefabSystem>();if(ps==null)return;
            string location=typeof(Mod).Assembly.Location;
            if(Game.SceneFlow.GameManager.instance.modManager.TryGetExecutableAsset(typeof(Mod).Assembly,out var asset))location=asset.path;
            Game.SceneFlow.GameManager.instance.userInterface.view.uiSystem.AddHostLocation("rsik",System.IO.Path.GetDirectoryName(location),false);
            foreach(var name in new[]{"RedPaving","ColorGuide","DrawLine","CrossingDistance","RoadEdge","Center","Crosswalk","Paving","Curb","Gutter","SideRoad","SpeedHump","Tactile","CyclePath","UTurn","Guide","GuideAuto","Approach","ManualStop","ManualArrow","ManualUTurn","ManualDiamond","ManualSpeed","ManualText","MergeDivider","BusDivider","AddAll","RemoveAll","RemovePaint"}){
                if(Tools.ContainsKey(name))continue;
                var p=ScriptableObject.CreateInstance<EdgeToolPrefab>();p.name="RSIK"+name+"Tool";
                p.RedPaving=name=="RedPaving";p.ColorGuide=name=="ColorGuide";p.DrawLine=name=="DrawLine";p.CrossingDistance=name=="CrossingDistance";p.Divider=name=="MergeDivider"?1:name=="BusDivider"?2:0;
                p.ManualDecal=name=="Tactile"?9:name=="CyclePath"?10:name=="SpeedHump"?8:name=="ManualStop"?1:name=="ManualArrow"?2:name=="ManualUTurn"?3:name=="ManualDiamond"?4:name=="ManualSpeed"?5:name=="ManualText"?6:0; p.AddAll=name=="AddAll";p.RemoveAll=name=="RemoveAll";p.RemovePaint=name=="RemovePaint";p.Approach=name=="Approach";p.GuideAuto=name=="GuideAuto";p.Guide=name=="Guide";p.Center=name=="Center";p.Crosswalk=name=="Crosswalk";p.Paving=name=="Paving";p.Curb=name=="Curb";p.Gutter=name=="Gutter";p.SideRoad=name=="SideRoad";p.UTurn=name=="UTurn";
                var ui=p.AddComponent<UIObject>();ui.m_Icon="coui://rsik/"+(name=="Center"?"CenterLine":name)+".svg";
                // No maintenance group: tools are activated from our GameTopRight menu.
                if(ps.AddPrefab(p))Tools.Add(name,p);else UnityEngine.Object.Destroy(p);
            }
        }
    }
}
