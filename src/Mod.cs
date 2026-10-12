using Colossal.IO.AssetDatabase;
using Game;
using Game.Modding;
using Game.Prefabs;
using Game.SceneFlow;
using Game.Serialization;
using Unity.Entities;

namespace RoadStyleIntoKorea
{
    public sealed class Mod : IMod
    {
        internal static Settings Options;
        private Locale en, ko;
        public void OnLoad(UpdateSystem updates)
        {
            Options = new Settings(this);
            AssetDatabase.global.LoadSettings("RoadStyleIntoKorea", Options, new Settings(this));
            if(Options.PlacementVersion<5){Options.StopGap=System.Math.Max(3,Options.StopGap);Options.PlacementVersion=5;}
            if(Options.PlacementVersion<6){Options.DefaultCrosswalk=Options.CrosswalkBrush=CrosswalkSize.Three;if(Options.GuideDash==2&&Options.GuideGap==2){Options.GuideDash=1;Options.GuideGap=.5f;}Options.PlacementVersion=6;}
            if(Options.PlacementVersion<7){Options.GuideDash=System.Math.Max(.1f,Options.GuideDash>=.5f?Options.GuideDash*.4f:Options.GuideDash);Options.CrosswalkClearance=System.Math.Max(14,Options.CrosswalkClearance);Options.AutoCrosswalkSize=true;Options.PlacementVersion=7;}
            Options.Enabled = true;
            if(Options.PlacementVersion<8){if(System.Math.Abs(Options.EdgeInset-.15f)<.001f)Options.EdgeInset=0;Options.PlacementVersion=8;}
            if(Options.PlacementVersion<9){Options.EdgeLaneGap=0;Options.PlacementVersion=9;}
            if(Options.PlacementVersion<10){Options.UTurnZones=false;Options.PlacementVersion=10;}
            en = new Locale(Options, false); ko = new Locale(Options, true);
            GameManager.instance.localizationManager.AddSource("en-US", en);
            GameManager.instance.localizationManager.AddSource("ko-KR", ko);
            Options.RegisterInOptionsUI();
            updates.UpdateBefore<LegacyStyleCacheCleanupSystem,PrimaryPrefabReferencesSystem>(SystemUpdatePhase.PrefabReferences);
            // Create/retire visuals before BOTH object and lane spatial indexes,
            // then let native rendering and end-of-frame cleanup consume tags.
            updates.UpdateBefore<StyleSystem,Game.Objects.NetObjectInitializeSystem>(SystemUpdatePhase.Modification5);
            updates.UpdateBefore<PaintProtectionSystem,Game.Net.SecondaryLaneSystem>(SystemUpdatePhase.Modification4B);
            updates.UpdateAt<EdtAssetMenuSystem>(SystemUpdatePhase.Modification5);
            updates.UpdateAt<NativeCrossingSystem>(SystemUpdatePhase.Modification1);
            updates.UpdateBefore<UTurnRoutingSystem,Game.Net.LaneReferencesSystem>(SystemUpdatePhase.Modification4B);
            updates.UpdateBefore<TrafficGeometrySystem,Game.Net.LaneOverlapSystem>(SystemUpdatePhase.Modification4B);
            updates.UpdateBefore<VisibilitySystem, Game.Rendering.RequiredBatchesSystem>(SystemUpdatePhase.ModificationEnd);
            updates.UpdateBefore<BeforeSaveSystem, BeginPrefabSerializationSystem>(SystemUpdatePhase.Serialize);
            updates.UpdateAfter<AfterSaveSystem, EndPrefabSerializationSystem>(SystemUpdatePhase.Serialize);
            updates.UpdateAt<EdgeStyleTool>(SystemUpdatePhase.ToolUpdate);
            updates.UpdateAt<RoadMenuUI>(SystemUpdatePhase.UIUpdate);
            updates.UpdateAt<EdgeTooltipSystem>(SystemUpdatePhase.UITooltip);
            updates.UpdateBefore<EdgeMenuSystem, PrefabInitializeSystem>(SystemUpdatePhase.PrefabUpdate);
        }
        internal static StyleSystem Runtime => World.DefaultGameObjectInjectionWorld?.GetExistingSystemManaged<StyleSystem>();
        public void OnDispose()
        {
            var world = World.DefaultGameObjectInjectionWorld;
            if (world != null && world.IsCreated)
            {
                var tools = world.GetExistingSystemManaged<Game.Tools.ToolSystem>();
                if (tools?.activeTool is EdgeStyleTool) tools.activeTool = world.GetOrCreateSystemManaged<Game.Tools.DefaultToolSystem>();
                Runtime?.Clear();
            }
            Options?.UnregisterInOptionsUI(); Options = null;            if (en != null) GameManager.instance.localizationManager.RemoveSource("en-US", en);
            if (ko != null) GameManager.instance.localizationManager.RemoveSource("ko-KR", ko);
        }
    }
    public partial class VisibilitySystem : GameSystemBase
    { protected override void OnUpdate() => World.GetExistingSystemManaged<StyleSystem>()?.EnforceAllVisibility(); }
    public partial class BeforeSaveSystem : GameSystemBase
    { protected override void OnUpdate() => World.GetExistingSystemManaged<StyleSystem>()?.BeforeSave(); }
    public partial class AfterSaveSystem : GameSystemBase
    { protected override void OnUpdate() => World.GetExistingSystemManaged<StyleSystem>()?.AfterSave(); }
}

































