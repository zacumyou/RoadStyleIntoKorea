using System.Collections.Generic;
using Colossal;
using Colossal.IO.AssetDatabase;
using Game.Modding;
using Game.Settings;
namespace RoadStyleIntoKorea
{
    public enum PavingStyle { Brick01=1, Brick02=2, Brick03=3, Brick04=4, Brick05=5 }
    public enum EdgeStyle { Original, None, WhiteSolid, YellowSolid, YellowDouble, YellowZigzag }
    public enum CrosswalkSize { Two=2, Three=3, Four=4, SingleThree=13 }
    public enum ToolSide { ClickedSide, BothSides }
    [FileLocation("RoadStyleIntoKorea")]
    [SettingsUITabOrder("Apply", "Crosswalk", "Edges", "Directions", "Surface", "Performance")]
    [SettingsUIGroupOrder("Actions", "Crossings", "Approach", "EdgeLines", "Arrows", "Guides", "Wear", "Budget")]
    [SettingsUIShowGroupName("Actions", "Crossings", "Approach", "EdgeLines", "Arrows", "Guides", "Wear", "Budget")]
    public sealed class Settings : ModSetting
    {
        public Settings(IMod mod) : base(mod) { }
        [SettingsUIHidden] public string MenuPlacement { get; set; } = "";
        [SettingsUIHidden]
        public bool Enabled { get => true; set { } }
        [SettingsUISection("Apply", "Actions"), SettingsUIButton, SettingsUIConfirmation("RSIK.ReplaceAll.Confirm", "주의하세요!\n이것은 실험 기능이며, 세이브 파일이 망가지거나 심각한 성능 저하를 불러일으킬 수 있습니다"), SettingsUIDisableByCondition(typeof(Settings), nameof(CannotApply))]
        public bool ReplaceAll { set { if (value) Mod.Runtime?.RequestApply(); } }
        [SettingsUISection("Apply", "Actions"), SettingsUIButton, SettingsUIConfirmation("RSIK.ReplaceAll.Confirm", "Apply road styles?"), SettingsUIDisableByCondition(typeof(Settings), nameof(CannotApply))]
        public bool ReplaceOrdinary { set { if(value)Mod.Runtime?.RequestApply(1); } }
        [SettingsUISection("Apply", "Actions"), SettingsUIButton, SettingsUIConfirmation("RSIK.ReplaceAll.Confirm", "Apply road styles?"), SettingsUIDisableByCondition(typeof(Settings), nameof(CannotApply))]
        public bool ReplaceHighways { set { if(value)Mod.Runtime?.RequestApply(2); } }

        [SettingsUISection("Apply", "Actions"), SettingsUIButton]
        public bool RestoreAll { set { if (value) Mod.Runtime?.RequestRestore(); } }
        [SettingsUISection("Apply", "Actions"), SettingsUIButton]
        public bool InspectAssets { set { if (value) Mod.Runtime?.Inspect(); } }
        [SettingsUISection("Apply", "Actions"), SettingsUIMultilineText, SettingsUIDisplayName(typeof(Settings),nameof(StatusText))]
        public string Status => RoadStyleIntoKorea.StatusText.Display(Mod.Runtime?.Status ?? "도시를 불러오세요 / Load a city");
        [SettingsUISection("Apply", "Actions"), SettingsUIMultilineText, SettingsUIDisplayName(typeof(Settings),nameof(AllRoadProgressText))]
        public string AllRoadProgress => RoadStyleIntoKorea.StatusText.Display(Mod.Runtime?.AllRoadProgress ?? "도시를 불러오세요 / Load a city");
        public Game.UI.Localization.LocalizedString StatusText()=>Game.UI.Localization.LocalizedString.Value(Status);
        public Game.UI.Localization.LocalizedString AllRoadProgressText()=>Game.UI.Localization.LocalizedString.Value(RoadStyleIntoKorea.StatusText.Get("RSIK.Status.AllRoadStyles")+AllRoadProgress);
        [SettingsUISection("Apply", "Actions")] public bool AutoStyleNewRoads { get; set; } = true;
        public bool CannotApply() => Mod.Runtime==null||Mod.Runtime.CannotReplaceAll;
        [SettingsUISection("Apply", "Actions")] public ReplaceCategory BulkCategory { get; set; }
        [SettingsUISection("Apply", "Actions")] public CenterStyle BulkCenter { get; set; }=CenterStyle.YellowDouble;
        [SettingsUISection("Apply", "Actions")] public EdgeStyle BulkEdge { get; set; }=EdgeStyle.YellowSolid;
        [SettingsUISection("Apply", "Actions")] public CrosswalkSize BulkCrosswalk { get; set; }=CrosswalkSize.Two;
        [SettingsUISection("Apply", "Actions")] public PavingStyle BulkPaving { get; set; }=PavingStyle.Brick01;
        [SettingsUISection("Apply", "Actions"),SettingsUIButton,SettingsUIDisableByCondition(typeof(Settings),nameof(CannotApply))]
        public bool ReplaceCategoryAll {set {if(value)Mod.Runtime?.RequestBulk();}}
        [SettingsUISection("Crosswalk", "Crossings")] public bool SyncTraffic { get; set; } = true;
        [SettingsUISection("Crosswalk", "Crossings")] public bool Crosswalks { get; set; } = true;
        [SettingsUISection("Crosswalk", "Crossings")] public bool AutoCrosswalkSize { get; set; } = true;
        [SettingsUISection("Crosswalk", "Crossings")] public CrosswalkSize DefaultCrosswalk { get; set; } = CrosswalkSize.Three;
        [SettingsUIHidden] public CrosswalkSize CrosswalkBrush { get; set; } = CrosswalkSize.Three;
        [SettingsUISection("Crosswalk", "Crossings"), SettingsUISlider(min=.5f,max=5,step=.5f,unit="floatSingleFraction")]
        public float PaintClearance { get; set; } = 1;
        [SettingsUISection("Crosswalk", "Crossings")] public bool StopLines { get; set; } = true;
        [SettingsUIHidden] public int PlacementVersion { get; set; }
        [SettingsUISection("Crosswalk", "Crossings"), SettingsUISlider(min=0.5f,max=8f,step=0.5f,unit="floatSingleFraction")]
        public float StopGap { get; set; } = 3;
        [SettingsUIHidden]
        public int CrosswalkClearance { get; set; } = 14;
        [SettingsUISection("Crosswalk", "Approach")] public bool SolidApproach { get; set; } = true;
        [SettingsUIHidden] public bool UTurnZones { get; set; } = false;
        [SettingsUISection("Crosswalk", "Approach"), SettingsUISlider(min=4,max=24,step=1,unit="integer")]
        public int UTurnZoneLength { get; set; } = 8;
        [SettingsUISection("Crosswalk", "Approach"), SettingsUISlider(min=25,max=50,step=1,unit="percentage")]
        public int SolidPercent { get; set; } = 25;
        [SettingsUISection("Crosswalk", "Approach"), SettingsUISlider(min=5,max=50,step=1,unit="integer")]
        public int SolidMinimum { get; set; } = 10;
        [SettingsUISection("Crosswalk", "Approach"), SettingsUISlider(min=15,max=100,step=1,unit="integer")]
        public int SolidMaximum { get; set; } = 50;
        [SettingsUISection("Crosswalk", "Approach")] public bool Diamonds { get; set; } = true;
        [SettingsUISection("Crosswalk", "Approach")] public bool SpeedSigns { get; set; } = true;
        [SettingsUISection("Crosswalk", "Approach"), SettingsUISlider(min=5,max=80,step=1,unit="integer")]
        public int DiamondDistance { get; set; } = 20;
        [SettingsUISection("Crosswalk", "Approach"), SettingsUISlider(min=1,max=3,step=1,unit="integer")]
        public int DiamondCount { get; set; } = 1;
        [SettingsUISection("Crosswalk", "Approach"), SettingsUISlider(min=5,max=50,step=1,unit="integer")]
        public int DiamondSpacing { get; set; } = 15;
        [SettingsUISection("Edges", "EdgeLines")] public bool EdgeLines { get; set; } = true;
        [SettingsUISection("Edges", "EdgeLines")] public EdgeStyle DefaultEdge { get; set; } = EdgeStyle.Original;
        [SettingsUISection("Edges", "EdgeLines")] public CenterStyle DefaultCenter { get; set; } = CenterStyle.YellowDouble;
        [SettingsUIHidden] public EdgeStyle Brush { get; set; } = EdgeStyle.YellowSolid;
        [SettingsUISection("Edges", "EdgeLines")] public ToolSide Side { get; set; } = ToolSide.ClickedSide;
        [SettingsUISection("Edges", "EdgeLines"), SettingsUISlider(min=0,max=1,step=0.05f,unit="floatTwoFractions")]
        public float EdgeLaneGap { get; set; } = 0;
        [SettingsUIHidden] public EdgeStyle CenterBrush { get; set; } = EdgeStyle.YellowDouble;
        [SettingsUISection("Edges", "EdgeLines")] public ToolSide CenterSide { get; set; } = ToolSide.ClickedSide;
        [SettingsUIHidden] public float EdgeInset { get; set; }
        [SettingsUISection("Directions", "Arrows"), SettingsUISlider(min=4,max=15,step=.5f,unit="floatSingleFraction")]
        public float ArrowStopDistance { get; set; } = 5;
        [SettingsUISection("Directions", "Arrows")] public bool Arrows { get; set; } = true;
        [SettingsUIHidden] public bool LeftGuides { get; set; } = false;
        [SettingsUIHidden] public bool HideJunctionLines { get; set; } = false;
        [SettingsUISection("Directions", "Guides"), SettingsUISlider(min=45,max=135,step=5,unit="integer")]
        public int TurnAngle { get; set; } = 45;
        [SettingsUISection("Directions", "Guides"), SettingsUISlider(min=0.5f,max=3,step=0.1f,unit="floatSingleFraction")]
        public float GuideLeftOffset { get; set; } = 1.5f;
        [SettingsUISection("Directions", "Guides"), SettingsUISlider(min=0.1f,max=5,step=0.1f,unit="floatSingleFraction")]
        public float GuideDash { get; set; } = .4f;
        [SettingsUISection("Directions", "Guides"), SettingsUISlider(min=0.5f,max=5,step=0.5f,unit="floatSingleFraction")]
        public float GuideGap { get; set; } = .5f;
        [SettingsUISection("Surface", "Wear")] public bool HideTransportColor { get; set; } = true;
        [SettingsUISection("Surface", "Wear")] public bool HideTransportText { get; set; } = true;
        [SettingsUISection("Surface", "Wear")] public bool Paving { get; set; } = true;
        [SettingsUISection("Surface", "Wear")] public bool HighwayPaving { get; set; } = false;
        [SettingsUISection("Surface", "Wear")] public bool HighwayCurbs { get; set; } = false;
        [SettingsUIHidden] public bool HighwayGutters { get; set; } = false;
        [SettingsUISection("Surface", "Wear")] public PavingStyle DefaultPaving { get; set; } = PavingStyle.Brick01;
        [SettingsUIHidden] public PavingStyle PavingBrush { get; set; } = PavingStyle.Brick01;
        [SettingsUISection("Surface", "Wear")] public bool LaneBoundaryCracks { get; set; }
        [SettingsUISection("Performance", "Budget"), SettingsUISlider(min=1,max=8,step=1,unit="integer")]
        public int Milliseconds { get; set; } = 2;
        [SettingsUISection("Performance", "Budget"), SettingsUISlider(min=1,max=64,step=1,unit="integer")]
        public int RoadsPerFrame { get; set; } = 8;
        [SettingsUISection("Performance", "Budget")] public bool UpdateEditedRoads { get; set; } = true;
        public override void SetDefaults()
        {
            BulkCategory=ReplaceCategory.Center;BulkCenter=CenterStyle.YellowDouble;BulkEdge=EdgeStyle.YellowSolid;BulkCrosswalk=CrosswalkSize.Two;BulkPaving=PavingStyle.Brick01;
            DefaultCenter=CenterStyle.YellowDouble;DefaultPaving=PavingStyle.Brick01;
            HighwayPaving=HighwayCurbs=HighwayGutters=false;
            CenterBrush=EdgeStyle.YellowDouble;CenterSide=ToolSide.ClickedSide;
            HideTransportColor=HideTransportText=true;
            Paving=true;PavingBrush=PavingStyle.Brick01;LaneBoundaryCracks=false;
            UTurnZones=false;UTurnZoneLength=8;
            AutoStyleNewRoads=true;Enabled=true;SpeedSigns=true;SyncTraffic=true;AutoCrosswalkSize=true;ArrowStopDistance=5; Crosswalks=StopLines=SolidApproach=Diamonds=EdgeLines=Arrows=LeftGuides=UpdateEditedRoads=true;
            StopGap=3;CrosswalkClearance=14;HideJunctionLines=false;PlacementVersion=10;DefaultCrosswalk=CrosswalkBrush=CrosswalkSize.Three;PaintClearance=1; SolidPercent=25; SolidMinimum=10; SolidMaximum=50; DiamondDistance=20; DiamondCount=1; DiamondSpacing=15;
            DefaultEdge=EdgeStyle.Original; Brush=EdgeStyle.YellowSolid; Side=ToolSide.ClickedSide; EdgeLaneGap=0; EdgeInset=0;
            TurnAngle=45; GuideLeftOffset=1.5f; GuideDash=.4f;GuideGap=.5f; Milliseconds=2; RoadsPerFrame=8;
        }
    }
    internal sealed class Locale : IDictionarySource
    {
        readonly Settings s; readonly bool ko;
        internal Locale(Settings settings, bool korean) { s=settings; ko=korean; }
        public IEnumerable<KeyValuePair<string,string>> ReadEntries(IList<IDictionaryEntryError> errors, Dictionary<string,int> indexCounts)
        {
            var d=new Dictionary<string,string> { [s.GetSettingsLocaleID()]="ROAD STYLE INTO KOREA" };
            Add(d,"HideTransportColor","버스·택시 전용차로 빨간 표시 제거","Hide red bus/taxi lane surface","빨간 시각 효과만 제거합니다. 전용차로 통행 제한은 유지됩니다.","Hides the red surface only; reserved-lane traffic rules remain unchanged.");
            Add(d,"HideTransportText","버스·택시 문자 제거","Hide BUS TAXI markings","도로의 버스·택시 문자 데칼만 제거합니다. 끄면 원래 문자를 복원합니다.","Hides road BUS TAXI decals; disabling restores the original markings.");
            string[] tabs={"Apply","적용","Apply","Crosswalk","횡단보도·접근부","Crosswalks & approaches","Edges","가장자리","Road edges","Directions","방향·유도선","Arrows & guides","Performance","처리 속도","Performance"};
            for(int i=0;i<tabs.Length;i+=3)d[s.GetOptionTabLocaleID(tabs[i])]=tabs[i+(ko?1:2)];
            d[s.GetOptionTabLocaleID("Surface")]=ko?"보도·갓길·균열":"Paving & cracks";
            d[s.GetOptionGroupLocaleID("Wear")]=ko?"도로 표면 장식":"Road surface details";
            Add(d,"LaneBoundaryCracks","차로 경계 균열","Lane boundary cracks","양쪽에 주행 차로가 있는 기존 차로 구분선을 따라 8m 반복 균열을 표시합니다. 횡단보도와 교차로 내부는 제외합니다. 기본 꺼짐. 변경 후 모두 교체를 누르세요.","Adds 8 m repeating cracks along existing dividers with driving lanes on both sides. Excludes crosswalks and junction interiors. Off by default. Press Replace all after changes.");
            string[] groups={"Actions","적용 및 복원","Apply and restore","Crossings","횡단보도와 정지선","Crosswalks and stop lines","Approach","접근부 표지","Approach markings","EdgeLines","도로 한쪽씩 선택","Choose each side","Arrows","진행 방향","Direction arrows","Guides","좌회전 유도선","Left-turn guides","Budget","프레임별 처리량","Work per frame"};
            for(int i=0;i<groups.Length;i+=3)d[s.GetOptionGroupLocaleID(groups[i])]=groups[i+(ko?1:2)];
            Add(d,"Enabled","이번 도시에서 사용","Enable for this city session","도시를 불러올 때마다 꺼집니다. 켠 뒤 모두 교체를 누르세요. 끄면 원래 표지로 복원합니다.","Off after each city load. Enable, then press Replace all. Disable to restore original markings.");
            Add(d,"AutoStyleNewRoads","신규 도로에 스타일 자동 적용","Automatically style new roads","새로 건설한 도로에 기본 스타일을 자동 적용합니다. 끄면 이후 건설한 도로는 자동 적용하지 않습니다. 기존 도로 편집 시 스타일 유지와 수동·전체 적용에는 영향을 주지 않습니다.","Automatically applies default styles to newly built roads. Disable to leave future roads unstyled. Existing style preservation during road edits, manual tools and Apply All are unaffected.");
            Add(d,"ReplaceOrdinary","일반도로만 전체 적용","Apply to ordinary roads","고속도로 속성이 없는 도로에만 전체 적용합니다.","Applies all styles only to roads without the highway flag.");
            Add(d,"ReplaceHighways","고속도로만 전체 적용","Apply to highways","고속도로 속성이 있는 도로에만 전체 적용합니다. 고속도로 포장 등 별도 설정은 유지합니다.","Applies all styles only to roads with the highway flag. Highway paving and other highway options are respected.");
            Add(d,"ReplaceAll","전체 적용","Apply to all roads","실험 기능. 현재 옵션의 기본 중앙선·가장자리·횡단보도·보도 스타일과 접근부 설정을 모든 도로에 적용합니다. 개별 선택과 수동 접근부·유도선은 기본 설정으로 교체됩니다. 경계석·배수로의 개별 선택은 원래 상태로 복원합니다. 카메라 중심에 가까운 도로부터 처리합니다.","Experimental. Applies the configured default center, edge, crossing, paving and approach styles to every road, replacing individual selections, manual approach decals and guides. Resets individual curb/gutter overrides to native. Starts nearest the camera center.");
            d["Options.WARNING[RSIK.ReplaceAll.Confirm]"]=ko?"주의하세요!\n이것은 실험 기능이며, 세이브 파일이 망가지거나 심각한 성능 저하를 불러일으킬 수 있습니다":"Caution!\nThis is an experimental feature and may corrupt your save file or cause severe performance degradation.";
            Add(d,"AllRoadProgress","모든 도로 교체 진행","All road replacement progress","도로 구간 기준 완료·처리 중·실패·제외 수입니다. 도로 구간 처리 후 교차로를 모아서 마무리합니다.","Completed, active, failed and skipped road segments. Junction work is grouped into a final phase after road segments.");
            Add(d,"DefaultCenter","기본 중앙선 스타일","Default center line style","모든 도로 스타일 교체에 사용할 중앙선입니다.","Center style used by Replace all road styles.");
            Add(d,"DefaultPaving","기본 보도블록 스타일","Default paving style","모든 도로 스타일 교체에 사용할 보도블록입니다.","Paving style used by Replace all road styles.");
            Add(d,"HighwayPaving","고속도로에도 보도블록 일괄 추가","Include highway paving","모두 추가·새 도로 자동 적용·전체 교체에서 고속도로 보도블록을 포함합니다. 수동 배치는 제한하지 않습니다.","Include highway paving in bulk and automatic styles. Manual placement remains available.");
            Add(d,"HighwayCurbs","고속도로에도 경계석 일괄 추가","Include highway curbs","모두 추가·새 도로 자동 적용·전체 교체에서 고속도로 경계석을 포함합니다.","Include highway curbs in bulk and automatic styles.");
            Add(d,"RestoreAll","원래 표지로 모두 복원","Restore original markings","작업을 취소하고 원래 표시를 복원합니다. 도로별 가장자리 선택은 보존됩니다.","Cancels work and restores original visuals. Per-road edge choices are retained.");
            Add(d,"InspectAssets","필요 에셋 확인","Check required assets","현재 로드된 에셋을 검사합니다. 누락이 있으면 모두 교체를 시작하지 않습니다.","Checks loaded prefabs. Replace all will not start if required assets are missing.");
            Add(d,"Status","처리 상태","Status","완료 수와 누락된 에셋을 표시합니다.","Shows progress and missing assets.");
            Add(d,"SyncTraffic","실제 정지·횡단 위치 맞추기","Synchronize traffic paths","차량 차로 연결점과 연결된 보행 경로를 도색 위치에 맞춥니다. 신호·우선권은 유지합니다. 공유 연결점이 충돌하는 횡단보도는 원래 위치를 유지합니다.","Moves connected car and pedestrian endpoints to the paint. Keeps signals and priorities. Crossings with conflicting shared endpoints retain their original location.");
            Add(d,"Crosswalks","한국식 횡단보도","Korean crosswalks","한국식 횡단보도 표시를 적용합니다. 실제 이동 경로는 위치 맞추기 옵션을 따릅니다.","Applies Korean crosswalk paint. Path relocation follows the traffic synchronization option.");
            Add(d,"AutoCrosswalkSize","공간 부족 시 작은 횡단보도 자동 선택","Automatically use a smaller crosswalk","두 줄 선택은 공간이 부족하면 4→3→2m 순서로 확인합니다. 한 줄 3m 선택은 유지합니다. 2m도 이동 공간이 부족하면 원래 위치를 유지합니다.","Double-row selections check smaller sizes down to 2 m. Single-row 3 m stays single-row. Keeps the original position if setback cannot fit.");
            Add(d,"BulkCategory","일괄 교체 분야","Replace category","선택한 분야만 일괄 변경합니다.","Replaces the selected category.");
            Add(d,"BulkCenter","중앙선 교체 종류","Center line style","중앙선 일괄 교체에 사용할 종류입니다.","Style for center line replacement.");
            Add(d,"BulkEdge","가장자리 교체 종류","Edge line style","가장자리 일괄 교체에 사용할 종류입니다.","Style for edge replacement.");
            Add(d,"BulkCrosswalk","횡단보도 교체 크기","Crosswalk size","횡단보도 일괄 교체 크기입니다.","Size for all crossings.");
            Add(d,"BulkPaving","보도블록 교체 종류","Paving style","보도블록 일괄 교체 종류입니다.","Style for all sidewalks.");
            Add(d,"ReplaceCategoryAll","선택한 분야 모두 교체","Replace selected category","위에서 선택한 분야와 종류를 도시 전체에 적용합니다. 횡단보도는 정지선 위치도 재계산합니다.","Apply this category and style across the city. Crosswalk changes also recalculate stop placement.");
            Add(d,"ArrowStopDistance","정지선에서 화살표 중심까지 (m)","Stop line to arrow center (m)","진입 차로의 첫 화살표 중심을 같은 거리로 맞춥니다. 공간이 부족하면 기존 표지를 유지합니다.","Aligns the first arrow center to this distance on each incoming lane. Keeps originals if space is insufficient.");
            Add(d,"DefaultCrosswalk","기본 횡단보도 띠 길이","Default crosswalk bar length","좁은 도로 기본은 한 줄 3m입니다. 일반 도로 기본은 두 줄 3m. 두 줄 전체 폭은 2m형 4.2m, 3m형 6.2m, 4m형 8.2m입니다. 변경 후 모두 교체를 누르세요.","Narrow roads default to single-row 3 m. Other roads default to double-row 3 m. Double-row widths: 4.2 / 6.2 / 8.2 m. Press Replace all after changes.");
            Add(d,"CrosswalkBrush","횡단보도 도구 크기","Crosswalk tool size","도로 메뉴의 한국식 횡단보도 도구로 원하는 횡단보도 근처를 클릭하세요. Shift+R로 두 줄 2·3·4m와 한 줄 3m 순환, 우클릭은 기본값 복원.","Use Korean crosswalks in Roads and click near a crossing. Shift+R cycles double-row 2/3/4 m and single-row 3 m; right-click resets to default.");
            Add(d,"PaintClearance","횡단보도와 가장자리·유도선 간격 (m)","Crosswalk clearance for edge lines and guides (m)","횡단보도 도색 가장자리에서 측정합니다.","Measured from the painted crosswalk boundary.");
            Add(d,"StopLines","횡단보도 앞 정지선","Stop lines before crosswalks","신호·우선권과 관계없이 진입 차로에 표시합니다. 교통 규칙 자체는 변경하지 않습니다.","Paints incoming lanes regardless of signal or priority. Does not change traffic rules.");
            Add(d,"StopGap","횡단보도와 정지선 간격 (m)","Crosswalk-to-stop gap (m)","기본 3m. 짧은 접근부에서 공간이 부족하면 기존 1m 간격으로 줄입니다.","Default 3 m. Falls back to a 1 m gap when the approach is too short.");
            Add(d,"CrosswalkClearance","교차로 중심에서 횡단보도까지 (m)","Crosswalk clearance from junction center (m)","가까운 도색 가장자리 기준, 기본 14m. 가까운 교차로·급한 곡선에서는 이동을 생략합니다. 0은 이동 끄기. 위치 맞추기를 켜면 연결된 보행 경로도 이동합니다. 신호등 물체 위치는 유지합니다.","Measured to the near painted edge; default 14 m. Skips relocation on short or sharply curved approaches. Set 0 to disable. Connected pedestrian paths move when synchronization is enabled; signal objects remain in place.");
            Add(d,"SolidApproach","정지선 앞 차선 실선화","Solid approach dividers","같은 방향의 차로 구분선만 바꿉니다. 중앙선은 보존합니다.","Replaces same-direction dividers only. Preserves center lines.");
            Add(d,"UTurnZones","중앙선 유턴 구역","U-turn zones in center lines","유턴 연결이 있는 왕복 3차로 이상 도로에서 정지선 뒤 1m는 기존 중앙선을 유지하고 그 뒤를 유턴 점선으로 바꿉니다. 구역 뒤에도 기존 중앙선이 남습니다. 실제 유턴 허용 규칙은 바꾸지 않습니다. 변경 후 모두 교체를 누르세요.","On two-way roads with at least 3 total lanes and a U-turn connection, keeps 1 m of original center line upstream of the stop, then adds dashed U-turn paint followed by the original line. Does not change turn permissions. Press Replace all after changes.");
            Add(d,"UTurnZoneLength","유턴 점선 기본 길이 (m)","Default U-turn zone length (m)","기본 8m, 4–24m. 유턴 도구에서 +/−로 1m씩 조절합니다. 배치한 구간은 길이를 개별 저장합니다.","Default 8m, range 4–24m. Use +/− in the U-turn tool; placed zones retain their own length.");
            Add(d,"SolidPercent","접근 구간 비율 (%)","Approach length (%)","노드 사이 차로 길이의 비율입니다. 최소·최대 길이와 실제 사용 가능한 길이로 제한합니다.","Fraction of the lane between nodes, bounded by minimum, maximum, and available approach length.");
            Add(d,"SolidMinimum","실선 최소 길이 (m)","Minimum solid length (m)","짧은 도로는 실제 남은 길이까지만 표시합니다.","Short roads use only available length.");
            Add(d,"SolidMaximum","실선 최대 길이 (m)","Maximum solid length (m)","최소 길이보다 작은 값을 선택하면 최소 길이를 우선합니다.","If lower than minimum, minimum takes priority.");
            Add(d,"SpeedSigns","속도 노면 표지 자동 생성","Automatic speed markings","도로 속도에 맞는 표지를 생성합니다. 시속 10~120킬로미터의 10 단위 표지만 지원하며 공간이 부족하면 생략합니다. 글자 표지는 수동 전용입니다.","Uses road speed; supports 10–120 km/h in steps of 10. Skips unsupported speeds or insufficient space. Text markings are manual only.");
            Add(d,"Diamonds","횡단보도 예고 마름모","Crosswalk warning diamonds","진입 차로마다 표시합니다. 짧은 접근부에서 도로를 벗어나는 표지는 생략합니다.","Paints each incoming lane; skips markings that do not fit the approach.");
            Add(d,"DiamondDistance","첫 마름모 거리 (m)","First diamond distance (m)","정지선에서 상류 방향으로 측정합니다.","Measured upstream from the stop line.");
            Add(d,"DiamondCount","마름모 개수","Diamond count","차로 하나당 표시 개수입니다.","Number per incoming lane.");
            Add(d,"DiamondSpacing","마름모 사이 간격 (m)","Diamond spacing (m)","마름모 중심 사이 거리입니다.","Distance between diamond centers.");
            Add(d,"EdgeLines","가장자리선 변경 사용","Enable edge styles","도로 메뉴의 한국식 가장자리 도구로 원하는 쪽을 클릭하세요. 우클릭은 해당 쪽을 기본 선택으로 되돌립니다.","Use the Korean edge tool in Roads and click the desired side. Right-click resets that side to the default choice.");
            Add(d,"DefaultEdge","전체 도로 기본 가장자리","Default edge style","개별 선택이 없는 도로에만 사용합니다. 원래 표지 유지가 기본입니다.","Used on roads without a side override. Default is Original.");
            Add(d,"Brush","도구로 칠할 가장자리선","Edge tool brush","도구 사용 중 Shift+R: 흰색 → 황색 → 황색 이중 → 황색 지그재그 순환. 클릭하면 적용됩니다.","While the tool is active, Shift+R cycles white, yellow, double yellow and yellow zigzag. Click to apply.");
            Add(d,"Side","도구 적용 범위","Tool side selection","클릭한 쪽만 또는 양쪽을 선택합니다. 기준은 도로 중심선입니다.","Choose the clicked side or both sides, relative to the road centerline.");
            Add(d,"EdgeLaneGap","마지막 차로와 가장자리선 간격 (m)","Gap outside the last driving lane (m)","마지막 주행 차로의 바깥 경계와 선의 안쪽 끝 사이 거리입니다. 0이면 바로 붙이며 값을 올리면 바깥으로 이동합니다. 보도 침범 및 정류장 확장·축소 구간은 잘라냅니다.","Distance from the outer driving-lane boundary to the inner edge of the marking. Zero touches the lane; larger values move outward. Sidewalk overlaps and bus-bay tapers are omitted.");
            Add(d,"Arrows","한국식 방향 화살표","Korean direction arrows","실제 허용 방향으로 정지선 앞 화살표를 우선 표시하고, 기존 화살표가 없어도 보충합니다. 직진 없이 좌회전·유턴만 허용하면 좌회전+유턴을 사용합니다. 유턴 예고는 마름모보다 교차로에서 먼 쪽에 3m 이상 띄우며, 공간이 없으면 유턴 예고부터 생략합니다.","Prioritizes stop-approach direction arrows from actual permissions, adding missing native arrows. Uses left+U-turn only without straight/right permissions. Advance U-turn paint goes upstream of all diamonds with at least 3 m clearance; omits the U-turn warning first when space is insufficient.");
            Add(d,"LeftGuides","좌회전 유도선","Left-turn guides","차량 경로의 왼쪽에 점선을 그립니다. 교통 경로 자체는 변경하지 않습니다.","Draws dashed guides on the vehicle's left without changing routing.");
            Add(d,"HideJunctionLines","교차로 안 기존 차선 숨기기","Hide original junction lane markings","좌회전 유도선 사용 시 교차로 안의 기존 차선·합류선·중앙선 도색만 숨깁니다. 끄거나 복원하면 다시 표시됩니다.","With left guides enabled, hides original divider, merge and center paint inside junctions. Restoring or disabling returns the original paint.");
            Add(d,"TurnAngle","좌회전 최소 각도 (도)","Minimum left-turn angle (degrees)","45도 이상을 선택할 수 있습니다. 유턴 경로에는 유도선을 표시하지 않습니다.","At least 45 degrees. U-turn paths do not receive guides.");
            Add(d,"GuideLeftOffset","차량 경로 왼쪽 거리 (m)","Offset left of vehicle path (m)","차로 반폭보다 크면 차로 반폭으로 제한합니다.","Clamped to half the lane width.");
            Add(d,"GuideDash","유도선 한 점선 길이 (m)","Guide dash length (m)","곡선을 따라 측정합니다.","Measured along the curve.");
            Add(d,"GuideGap","유도선 빈 간격 (m)","Guide gap length (m)","곡선을 따라 측정합니다.","Measured along the curve.");
            Add(d,"Milliseconds","프레임당 작업 시간 목표 (ms)","Work time target per frame (ms)","도로 하나를 처리한 뒤 시간을 확인합니다. 복잡한 교차로 하나는 목표 시간을 넘을 수 있습니다.","Checked after each road. A complex junction can exceed this soft time budget.");
            Add(d,"RoadsPerFrame","프레임당 최대 도로 수","Maximum roads per frame","작게 설정하면 교체가 느려지는 대신 순간 부하가 줄어듭니다.","Lower values spread application over more frames.");
            Add(d,"UpdateEditedRoads","적용 후 편집된 도로 갱신","Refresh edited roads after application","이미 스타일이 적용된 도로를 편집하면 해당 스타일을 다시 맞춥니다. 새로 건설한 도로에는 이 옵션과 관계없이 기본 스타일을 자동 적용합니다.","Refreshes styles on edited, styled roads. Newly built roads always receive the configured defaults.");
            foreach(CrosswalkSize v in System.Enum.GetValues(typeof(CrosswalkSize)))d[s.GetEnumValueLocaleID(v)]=CrosswalkStylePolicy.Label((int)v,ko);
            d["Assets.NAME[RSIKCrosswalkTool]"]=ko?"한국식 횡단보도 크기":"Korean crosswalk sizes";
            d["Assets.DESCRIPTION[RSIKCrosswalkTool]"]=ko?"횡단보도 근처 클릭: 선택한 크기 적용. Shift+R: 두 줄 2·3·4m / 한 줄 3m 순환. 우클릭: 기본 크기 복원.":"Click near a crossing to apply size. Shift+R: cycle double-row 2/3/4 m and single-row 3 m. Right-click: reset to default.";
            foreach(EdgeStyle v in System.Enum.GetValues(typeof(EdgeStyle)))d[s.GetEnumValueLocaleID(v)]=ko?new[]{"원래 표지 유지","표시 없음","흰색 실선","황색 실선","황색 이중선","황색 지그재그선"}[(int)v]:v.ToString();
            d[s.GetEnumValueLocaleID(ToolSide.ClickedSide)]=ko?"클릭한 쪽만":"Clicked side only";
            d[s.GetEnumValueLocaleID(ToolSide.BothSides)]=ko?"양쪽":"Both sides";
            d["Assets.NAME[RSIKRoadEdgeTool]"]=ko?"한국식 도로 가장자리":"Korean road edges";
            d["Assets.DESCRIPTION[RSIKRoadEdgeTool]"]=ko?"적용 허용 → 강조된 도로 한쪽 클릭. Shift+R: 선 종류 순환. 우클릭: 개별 선택 해제.":"Enable, then click a highlighted road side. Shift+R: cycle styles. Right-click: clear override.";
            Add(d,"Paving","보도·갓길 보도블록 사용","Enable sidewalk/shoulder paving","도로 메뉴의 보도블록 도구로 강조된 부분을 클릭하세요. Shift+R: 01–05 전환, 우클릭: 해당 부분 복원. 설정을 끈 뒤 모두 교체하면 보도블록만 제거합니다.","Click the highlighted region with the paving tool in Roads. Shift+R cycles 01–05; right-click restores that region. Disable and Replace all to remove paving.");
            Add(d,"PavingBrush","보도블록 종류","Paving brush","선택한 종류를 클릭한 구간에만 적용합니다.","Applies the selected style only to the clicked region.");
            foreach(PavingStyle v in System.Enum.GetValues(typeof(PavingStyle)))d[s.GetEnumValueLocaleID(v)]=(ko?"보도블록 ":"Brick paving ")+((int)v).ToString("00");
            d["Assets.NAME[RSIKPavingTool]"]=ko?"한국식 보도·갓길 보도블록":"Korean sidewalk / shoulder paving";
            d["Assets.DESCRIPTION[RSIKPavingTool]"]=ko?"보도 또는 빈 갓길에 마우스를 올리고 클릭하세요. Shift+R: 01–05 전환. 우클릭: 원래 표면 복원.":"Hover and click a sidewalk or empty shoulder. Shift+R cycles 01–05. Right-click restores the original surface.";
            Add(d,"CenterSide","중앙선 적용 방향","Center line application sides","중앙분리대가 있는 도로는 클릭한 쪽만 적용하거나 양쪽을 동시에 적용합니다. 기본은 클릭한 쪽만입니다.","Paint the clicked carriageway or both sides of a median. Default: clicked side only.");
            foreach(var entry in ToolText.Entries(ko))d[entry.Key]=entry.Value;
            foreach(var entry in UiCatalog.Entries(ko))d[entry.Key]=entry.Value;
            foreach(ReplaceCategory v in System.Enum.GetValues(typeof(ReplaceCategory)))d[s.GetEnumValueLocaleID(v)]=ko?new[]{"중앙선","가장자리","횡단보도","보도블록"}[(int)v]:v.ToString();
            foreach(CenterStyle v in System.Enum.GetValues(typeof(CenterStyle)))d[s.GetEnumValueLocaleID(v)]=ko?new[]{"원래 표지 유지","표시 없음","흰색 실선","황색 실선","황색 이중선"}[(int)v]:v.ToString();
            d["RSIK.Menu.Language"]=ko?"ko":"en";
            d["RSIK.UTurn.Help"]=ko?"유턴 점선 표시만 변경 · 우클릭: 제거":"Paint only; native traffic routes unchanged · Right-click: remove";
            d["Assets.NAME[RSIKUTurnTool]"]=ko?"유턴 점선":"U-turn zone";
            return d;
        }
        void Add(Dictionary<string,string>d,string key,string kl,string el,string kd,string ed){d[s.GetOptionLabelLocaleID(key)]=ko?kl.Replace("(ms)","(밀리초)").Replace("(m)","(미터)"):el;d[s.GetOptionDescLocaleID(key)]=ko?kd:ed;}
        public void Unload() { }
    }
}
