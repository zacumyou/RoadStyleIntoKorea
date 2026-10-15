using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
namespace RoadStyleIntoKorea {
 internal static class StatusText {
  internal static bool Korean=>(Game.SceneFlow.GameManager.instance?.localizationManager?.activeLocaleId??"").StartsWith("ko",StringComparison.OrdinalIgnoreCase);
  static readonly Dictionary<string,string> Keys=new Dictionary<string,string>{
["진단 기록 중입니다 / Capture already running"]="RSIK.Status.CaptureAlreadyRunning",
["도로 진단 기록 시작 / Capture started: "]="RSIK.Status.CaptureStarted",
["진단 용량 제한 / Capture truncated: "]="RSIK.Status.CaptureTruncated",
["진단 저장 실패 / Capture failed: "]="RSIK.Status.CaptureFailed",
["진단 저장 완료 / Capture saved: "]="RSIK.Status.CaptureSaved",
["진단 실패 / Capture failed: "]="RSIK.Status.CaptureFailed2",
["게임이 횡단보도를 생성하지 못했습니다. 보행 연결·도로 구성을 확인하세요 / No native crossing generated; check pedestrian connections and road configuration"]="RSIK.Status.NoNativeCrossingGeneratedCheckPedestrianConnectionsAndRoadConfiguration",
["유턴 점선: 왕복 3차로 이상, 중앙분리대 없는 도로 / Requires opposing traffic, 3+ lanes, no median"]="RSIK.Status.RequiresOpposingTraffic3LanesNoMedian",
["이 도로 템플릿은 횡단보도 추가를 지원하지 않습니다 / Road template does not support adding crossings"]="RSIK.Status.RoadTemplateDoesNotSupportAddingCrossings",
["구분선 에셋 또는 자동 대상 없음 / Divider asset or automatic target unavailable"]="RSIK.Status.DividerAssetOrAutomaticTargetUnavailable",
["원본 보도 메시를 읽지 못했습니다 / Native sidewalk mesh unavailable"]="RSIK.Status.NativeSidewalkMeshUnavailable",
["해당 분야 사용 옵션을 먼저 켜세요 / Enable this category first"]="RSIK.Status.EnableThisCategoryFirst",
["선택 범위 모두 복원 대기 / Restore local styles queued"]="RSIK.Status.RestoreLocalStylesQueued",
["보도 재질 준비 실패 / Paving material unavailable: "]="RSIK.Status.PavingMaterialUnavailable",
["선택 범위 모두 추가 대기 / Apply local styles queued"]="RSIK.Status.ApplyLocalStylesQueued",
["분야별 교체 대기 / Category replacement queued: "]="RSIK.Status.CategoryReplacementQueued",
["정지선·데칼 적용 완료 / Approach markings updated"]="RSIK.Status.ApproachMarkingsUpdated",
["선택 구간 처리 대기 / Selected segments queued: "]="RSIK.Status.SelectedSegmentsQueued",
["유턴 점선 에셋 누락 / Missing U-turn zone asset"]="RSIK.Status.MissingUTurnZoneAsset",
["필요 에셋 확인 완료 / Required assets available"]="RSIK.Status.RequiredAssetsAvailable",
["중앙선 에셋 누락 / Center line asset missing"]="RSIK.Status.CenterLineAssetMissing",
["중앙선 변경 대기 / Center line change queued"]="RSIK.Status.CenterLineChangeQueued",
["중앙선 대상이 없습니다 / No center line target"]="RSIK.Status.NoCenterLineTarget",
["횡단보도 변경 대기 / Crosswalk change queued"]="RSIK.Status.CrosswalkChangeQueued",
["보도블록 변경 대기 / Paving change queued"]="RSIK.Status.PavingChangeQueued",
["유턴 점선 변경 대기 / U-turn zone queued"]="RSIK.Status.UTurnZoneQueued",
["도시를 먼저 불러오세요 / Load a city first"]="RSIK.Status.LoadACityFirst",
["원래 표지 복원 완료 / Originals restored"]="RSIK.Status.OriginalsRestored",
[" — 도로 segment / road segments: "]="RSIK.Status.RoadSegments",
["가장자리 변경 대기 / Edge change queued"]="RSIK.Status.EdgeChangeQueued",
["유도선 적용 완료 / Guides applied: "]="RSIK.Status.GuidesApplied",
["진단 기록 중 / Owners remaining: "]="RSIK.Status.OwnersRemaining",
["보도블록 NetLane 누락 / Missing: "]="RSIK.Status.Missing",
["개별 표시 복원 / Marking restored"]="RSIK.Status.MarkingRestored",
["하나씩 표시 제거 / Marking removed"]="RSIK.Status.MarkingRemoved",
["유턴 점선 길이 / U-turn length: "]="RSIK.Status.UTurnLength",
["배수로 제거 / Gutter removed"]="RSIK.Status.GutterRemoved",
["배수로 적용 / Gutter applied"]="RSIK.Status.GutterApplied",
["가장자리 브러시 / Edge brush: "]="RSIK.Status.EdgeBrush",
["도시를 불러오세요 / Load a city"]="RSIK.Status.LoadACity",
[", 처리 중 / in progress: "]="RSIK.Status.InProgress",
[" · 에셋 없음 / Unavailable"]="RSIK.Status.Unavailable",
["중앙선 에셋 누락 / Missing: "]="RSIK.Status.Missing2",
["배수로 에셋 누락 / Missing: "]="RSIK.Status.Missing3",
["횡단보도 / Crosswalk: "]="RSIK.Status.Crosswalk",
["복원 중 / Restoring: "]="RSIK.Status.Restoring",
["에셋 누락 / Missing: "]="RSIK.Status.Missing4",
[", 제외 / skipped: "]="RSIK.Status.Skipped",
["교차로 마무리 / Finishing junctions"]="RSIK.Status.FinishingJunctions",
["교체 중 / Replacing"]="RSIK.Status.Replacing",
[", 실패 / failed: "]="RSIK.Status.Failed",
["취소됨 / Cancelled"]="RSIK.Status.Cancelled",
["누락 / Missing: "]="RSIK.Status.Missing5",
["완료 / Finished"]="RSIK.Status.Finished",
["대기 / Ready"]="RSIK.Status.Ready",
["방지턱 NetLane 없음 / Speed hump NetLane unavailable"]="RSIK.Status.SpeedHumpNetlaneUnavailable",
["도로 아스팔트 재질을 찾지 못했습니다 / Native road asphalt unavailable"]="RSIK.Status.NativeRoadAsphaltUnavailable",
["도로 아스팔트 준비 실패 / Asphalt material failed"]="RSIK.Status.AsphaltMaterialFailed",
["샛길 입구 제거 / Side road entrance removed"]="RSIK.Status.SideRoadEntranceRemoved",
["샛길 입구 적용 / Side road entrance applied"]="RSIK.Status.SideRoadEntranceApplied",
["보도 폭 부족 / Sidewalk too narrow"]="RSIK.Status.SidewalkTooNarrow",
["에셋 없음 / Missing: "]="RSIK.Status.Missing6",
["교체 처리 완료 / Finished: "]="RSIK.Status.Finished2",
[", 미지원 화살표 / unsupported arrows: "]="RSIK.Status.UnsupportedArrows",
["교체 중 / Applying: "]="RSIK.Status.Applying",
[", 남음 / remaining: "]="RSIK.Status.Remaining",
["한 줄 "]="RSIK.Status.SingleRow",
["두 줄 "]="RSIK.Status.DoubleRow",
["YellowDouble"]="RSIK.Status.YellowDouble",
["YellowZigzag"]="RSIK.Status.YellowZigzag",
["YellowSolid"]="RSIK.Status.YellowSolid",
["WhiteSolid"]="RSIK.Status.WhiteSolid"
  };
  // Match source fragments once; never feed translated text into subsequent replacements.
  static readonly Regex Phrases=new Regex(string.Join("|",Keys.Keys.OrderByDescending(s=>s.Length).Select(Regex.Escape)));
  internal static string Get(string key){
   var dictionary=Game.SceneFlow.GameManager.instance?.localizationManager?.activeDictionary;
   if(dictionary!=null&&dictionary.TryGetValue(key,out var value))return value;
   var fallback=Korean?UiCatalog.Korean:UiCatalog.English;
   return fallback.TryGetValue(key,out var text)?text:key;
  }
  internal static string Display(string text)=>string.IsNullOrEmpty(text)?text:Phrases.Replace(text,m=>Get(Keys[m.Value]));
 }
}
