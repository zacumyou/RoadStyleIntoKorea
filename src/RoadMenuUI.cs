using Colossal.UI.Binding;
using Game;
using Game.Tools;
namespace RoadStyleIntoKorea {
public partial class RoadMenuUI:Game.UI.UISystemBase {
 ValueBinding<bool> open;ValueBinding<string> selected;ValueBinding<int> brush,brushNext;
 bool opened;ValueBinding<string> signHover,menuQuery,menuPlacement;
 ValueBinding<bool> guideReady;ValueBinding<int> guideStep;
 public override GameMode gameMode=>GameMode.Game;
 protected override void OnCreate(){base.OnCreate();
  AddBinding(menuPlacement=new ValueBinding<string>("RoadStyleIntoKorea","menuPlacement",Mod.Options?.MenuPlacement??""));
  AddBinding(new TriggerBinding<string>("RoadStyleIntoKorea","saveMenuPlacement",v=>{
   var options=Mod.Options;if(options==null||v==null||v.Length>2048||options.MenuPlacement==v)return;
   var previous=options.MenuPlacement;
   try{options.MenuPlacement=v;options.ApplyAndSave();menuPlacement.Update(v);}
   catch(System.Exception ){options.MenuPlacement=previous;}
  }));
  AddBinding(menuQuery=new ValueBinding<string>("RoadStyleIntoKorea","menuQuery",""));
  AddBinding(new TriggerBinding<string>("RoadStyleIntoKorea","menuSearch",v=>menuQuery.Update(v??"")));
  AddBinding(new TriggerBinding<bool>("RoadStyleIntoKorea","menuSearchFocus",v=>{var system=World.GetExistingSystemManaged<StyleSystem>();if(system!=null)system.MenuSearchFocused=v;}));
  AddBinding(open=new ValueBinding<bool>("RoadStyleIntoKorea","open",false));
  AddBinding(guideReady=new ValueBinding<bool>("RoadStyleIntoKorea","guideReady",false));
  AddBinding(guideStep=new ValueBinding<int>("RoadStyleIntoKorea","guideStep",0));
  AddBinding(selected=new ValueBinding<string>("RoadStyleIntoKorea","selected",""));
  AddBinding(brush=new ValueBinding<int>("RoadStyleIntoKorea","brush",2));
  AddBinding(brushNext=new ValueBinding<int>("RoadStyleIntoKorea","brushNext",3));
  AddBinding(signHover=new ValueBinding<string>("RoadStyleIntoKorea","signHover",""));
  AddBinding(new TriggerBinding<int>("RoadStyleIntoKorea","speedBrush",v=>World.GetExistingSystemManaged<StyleSystem>()?.SelectRoadSign(5,v)));
  AddBinding(new TriggerBinding<int>("RoadStyleIntoKorea","textBrush",v=>World.GetExistingSystemManaged<StyleSystem>()?.SelectRoadSign(6,v)));
  AddBinding(new TriggerBinding<bool>("RoadStyleIntoKorea","signFocus",v=>{var s=World.GetExistingSystemManaged<StyleSystem>();if(s!=null)s.SignSearchFocused=v;}));
  AddBinding(new TriggerBinding<string>("RoadStyleIntoKorea","command",Command));
 }
 void Command(string command){
  
{
  var tools=World.GetOrCreateSystemManaged<ToolSystem>();
  if(command=="exit"){if(tools.activeTool is EdgeStyleTool)tools.activeTool=World.GetOrCreateSystemManaged<DefaultToolSystem>();return;}
  if(command=="cycle"||command=="wider"||command=="narrower"){
   var selectedTool=tools.activeTool as EdgeStyleTool;if(selectedTool==null)return;
   if(command=="cycle")selectedTool.CycleSelection();
   else {var system=World.GetExistingSystemManaged<StyleSystem>();int delta=command=="wider"?1:-1;if(selectedTool.IsSideRoadMode)system?.AdjustSideRoadWidth(delta);else if(selectedTool.IsUTurnMode)system?.AdjustUTurnLength(Unity.Entities.Entity.Null,delta);}
   return;
  }
  if(command=="menu"){opened=!opened;if(!opened){var system=World.GetExistingSystemManaged<StyleSystem>();if(system!=null)system.MenuSearchFocused=false;}if(!opened&&tools.activeTool is EdgeStyleTool)tools.activeTool=World.GetOrCreateSystemManaged<DefaultToolSystem>();return;}
  var menu=World.GetExistingSystemManaged<EdgeMenuSystem>();
  if(menu!=null&&menu.Tools.TryGetValue(command,out var prefab)){
   if(tools.activeTool is EdgeStyleTool active&&active.GetPrefab()==prefab)tools.activeTool=World.GetOrCreateSystemManaged<DefaultToolSystem>();
   else tools.ActivatePrefabTool(prefab);
  }
 }
 }
 protected override void OnUpdate(){base.OnUpdate();open.Update(opened);menuPlacement.Update(Mod.Options?.MenuPlacement??"");
  var tool=World.GetExistingSystemManaged<ToolSystem>()?.activeTool as EdgeStyleTool;
  selected.Update(tool?.GetPrefab()?.name??"");
  var system=World.GetExistingSystemManaged<StyleSystem>();guideReady.Update(tool?.IsGuideMode==true&&system?.GuideReady==true);guideStep.Update(system?.GuideStep??0);
  if(system!=null){if(tool==null||(tool.ManualDecalKind!=5&&tool.ManualDecalKind!=6))system.SignSearchFocused=false;signHover.Update(tool?.IsRedPavingMode==true?system.RedPavingHint:(tool?.IsDrawLineMode==true||tool?.IsColorGuideMode==true)?system.LineAssetStatus:tool!=null&&tool.ManualDecalKind>=5?system.SignHover:"");}
  if(tool!=null&&system!=null&&Mod.Options!=null){bool crossing=tool.IsCrosswalkMode;int value=tool.IsRedPavingMode?system.RedPavingBrush:tool.IsColorGuideMode?system.ColorGuideBrush:tool.IsDrawLineMode?system.LineBrush:tool.IsCrossingDistanceMode?system.CrossingDistanceValue(system.CrossingDistanceHover):tool.ManualDecalKind==8?system.HumpBrush:tool.IsSideRoadMode?system.SideRoadWidth:tool.DividerGroup==1?system.MergeDividerBrush:tool.DividerGroup==2?system.BusDividerBrush:tool.ManualDecalKind==5?system.SpeedBrush:tool.ManualDecalKind==6?system.TextBrush:tool.IsGutterMode?system.GutterBrush:tool.ManualDecalKind==2?system.ManualArrowBrush:tool.IsCenterMode?(int)Mod.Options.CenterBrush:tool.IsPavingMode?(int)Mod.Options.PavingBrush:tool.IsUTurnMode?Mod.Options.UTurnZoneLength:tool.IsDiagnosticMode?system.CaptureScope:crossing?(int)Mod.Options.CrosswalkBrush:(int)Mod.Options.Brush;brush.Update(value);brushNext.Update(tool.IsRedPavingMode?1-value:tool.IsColorGuideMode?(value+1)%DrawnLinePolicy.ColorCount:tool.IsDrawLineMode?(value+1)%DrawnLinePolicy.LineCount:tool.ManualDecalKind==8?1-value:tool.IsSideRoadMode?(value>=12?3:value+1):tool.DividerGroup>0?DividerStylePolicy.Next(tool.DividerGroup,value):tool.ManualDecalKind>=5?value/(tool.ManualDecalKind==5?13:11)*(tool.ManualDecalKind==5?13:11)+(value%(tool.ManualDecalKind==5?13:11)+1)%(tool.ManualDecalKind==5?13:11):tool.IsGutterMode?3-value:tool.ManualDecalKind==2?(value+1)%StyleSystem.ManualArrowSymbols.Length:tool.IsCenterMode?CenterLinePolicy.Next(value):tool.IsPavingMode?PavingPolicy.NextStyle(value):tool.IsDiagnosticMode?(value+1)%3:ToolText.Next(crossing,value));}
 }
 protected override void OnGamePreload(Colossal.Serialization.Entities.Purpose purpose,GameMode mode){opened=false;menuQuery.Update("");var system=World.GetExistingSystemManaged<StyleSystem>();if(system!=null)system.MenuSearchFocused=false;base.OnGamePreload(purpose,mode);}
}}
