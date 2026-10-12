using System.Collections.Generic;
namespace RoadStyleIntoKorea
{
    internal static class ToolText
    {
        internal static string PavingAction(bool apply)=>apply?"RSIK Apply paving":"RSIK Reset paving";
        internal static string Action(bool crossing,bool apply)=>crossing?(apply?"RSIK Apply crosswalk":"RSIK Reset crosswalk"):(apply?"RSIK Apply edge style":"RSIK Reset side");
        internal static int Next(bool crossing,int current)=>crossing?CrosswalkStylePolicy.Next(current):MarkingPolicy.NextEdgeBrush(current);
        internal static string SelectionID(bool crossing,int value,bool next)=>"RSIK.Tool."+(crossing?"Crosswalk.":"Edge.")+(next?"Next.":"Current.")+value;
        internal static Dictionary<string,string> Entries(bool ko)
        {
            var d=new Dictionary<string,string>();
            d["Assets.NAME[RSIKRedPavingTool]"]=ko?"적색 포장":"Red pavement";
            d["RSIK.RedPaving.0"]=ko?"연속 포장":"Continuous";
            d["RSIK.RedPaving.1"]=ko?"반복 띠 · 3m / 공백 6m":"Stripes · 3m / gap 6m";
            d["RSIK.RedPaving.Left"]=ko?"시작점 · 끝점":"Start · end";
            d["RSIK.RedPaving.Right"]=ko?"묶음 삭제 · 취소":"Delete group · cancel";
            d["RSIK.RedPaving.Help"]=ko?"시작점·끝점 선택. Shift: 같은 방향 전체 차로. Shift+R: 연속 / 반복 띠. 다른 곳 클릭: 취소.":"Select start and end. Shift: same-direction lanes. Shift+R: continuous / stripes. Invalid click cancels.";
            d["RSIK.RedPaving.SameLane"]=ko?"같은 방향으로 이어지는 차로를 선택하세요":"Select a continuation of the same lane";
            d["RSIK.RedPaving.Unavailable"]=ko?"내장 버스차로 재질을 찾지 못했습니다":"Native bus lane material unavailable";
            d["Assets.NAME[RSIKTactileTool]"]=ko?"점자블록":"Tactile paving";
            d["Assets.NAME[RSIKCyclePathTool]"]=ko?"보도 자전거도로":"Sidewalk cycle strip";
            d["Assets.NAME[RSIKMergeDividerTool]"]=ko?"합류 구분선":"Merge divider";
            d["Assets.NAME[RSIKBusDividerTool]"]=ko?"버스 구분선":"Bus divider";
            d["Assets.NAME[RSIKManualSpeedTool]"]=ko?"속도 표지":"Speed marking";
            d["Assets.NAME[RSIKManualTextTool]"]=ko?"글자 표지":"Text marking";
            d["Assets.NAME[RSIKAddAllTool]"]=ko?"모두 추가":"Add all";
            d["Assets.NAME[RSIKRemoveAllTool]"]=ko?"모두 제거":"Remove all";
            d["RSIK.Capture.0"]=ko?"진단 범위: 구간 + 양끝 노드":"Capture: segment + end nodes";
            d["RSIK.Capture.1"]=ko?"진단 범위: 가까운 교차로 + 연결 구간":"Capture: nearest junction + connected segments";
            d["RSIK.Capture.2"]=ko?"진단 범위: 연결 도로 3단계 (최대 64개)":"Capture: connected roads, 3 hops (max 64 owners)";
            d["RSIK.Capture.Help"]=ko?"Shift+R 범위 변경 · 드래그 후 놓아 저장 · 옵션 상태에 파일 경로 표시":"Shift+R changes scope; click to save. File path shown in settings status.";
            d["Common.ACTION["+Action(false,true)+"]"]=ko?"선택한 가장자리선 적용":"Apply selected edge line";
            d["Common.ACTION["+Action(false,false)+"]"]=ko?"가장자리 개별 선택 해제":"Reset edge override";
            d["Common.ACTION["+Action(true,true)+"]"]=ko?"선택한 횡단보도 크기 적용":"Apply selected crosswalk size";
            d["Common.ACTION["+Action(true,false)+"]"]=ko?"횡단보도 기본 크기로 복원":"Restore default crosswalk size";
            d["Assets.NAME[RSIKCenterTool]"]=ko?"한국식 중앙선":"Korean center lines";
            d["Assets.DESCRIPTION[RSIKCenterTool]"]=ko?"클릭: 중앙선 적용. Shift+R: 황색 이중선·황색 실선·흰색 실선·표시 없음. 우클릭: 원래 중앙선 복원. 중앙분리대 양쪽은 개별 적용하며 옵션에서 동시 적용 가능.":"Click to paint. Shift+R cycles double yellow, yellow, white and none. Right-click restores original. Median sides are independent; both sides optional in settings.";
            d["Common.ACTION[RSIK Apply center]"]=ko?"강조된 중앙선 적용":"Apply highlighted center line";
            d["Common.ACTION[RSIK Reset center]"]=ko?"강조된 중앙선 원래대로 복원":"Restore highlighted original center line";
            d["RSIK.Center.One"]=ko?"중앙분리대: 클릭한 방향만 적용":"Median: selected carriageway only";
            d["RSIK.Center.Both"]=ko?"중앙분리대: 양쪽 방향 동시 적용":"Median: both carriageways";
            var names=ko?new[]{"원래 표지 유지","표시 없음","흰색 실선","황색 실선","황색 이중선","황색 지그재그선"}:new[]{"Original markings","No markings","White solid line","Yellow solid line","Double yellow line","Yellow zigzag line"};
            foreach(bool next in new[]{false,true})
            {
                string prefix=next?(ko?"Shift+R → 다음: ":"Shift+R → Next: "):(ko?"현재 선택: ":"Selected: ");
                for(int i=0;i<names.Length;i++){d[SelectionID(false,i,next)]=prefix+names[i];d["RSIK.Tool.Center."+(next?"Next.":"Current.")+i]=prefix+names[i];}
                foreach(int i in new[]{2,3,4,13})d[SelectionID(true,i,next)]=prefix+CrosswalkStylePolicy.Label(i,ko);
            }
            d["Common.ACTION["+PavingAction(true)+"]"]=ko?"강조된 보도·갓길에 보도블록 적용":"Apply paving to highlighted sidewalk/shoulder";
            d["Common.ACTION["+PavingAction(false)+"]"]=ko?"강조된 부분의 기본 표면 복원":"Restore highlighted original surface";
            d["RSIK.Paving.NoTarget"]=ko?"보도 또는 빈 갓길 위에 마우스를 올리세요":"Hover over a sidewalk or empty shoulder";
            d["RSIK.Paving.Sidewalk"]=ko?"적용 부위: 강조된 보도":"Target: highlighted sidewalk";
            d["RSIK.Paving.Shoulder"]=ko?"적용 부위: 강조된 빈 갓길":"Target: highlighted empty shoulder";
            d["RSIK.Paving.Disabled"]=ko?"설정에서 이번 도시에서 사용·보도블록 사용을 켜세요":"Enable this city session and paving in settings";
            foreach(bool next in new[]{false,true})for(int i=1;i<=2;i++)
                d["RSIK.Tool.Paving."+(next?"Next.":"Current.")+i]=(next?(ko?"Shift+R → 다음: ":"Shift+R → Next: "):(ko?"현재 선택: ":"Selected: "))+(ko?"보도블록 ":"Brick paving ")+i.ToString("00");
            d["Assets.NAME[RSIKApproachTool]"]=ko?"데칼 자동 생성":"Auto decals";
            d["Assets.NAME[RSIKSpeedHumpTool]"]=ko?"방지턱":"Speed hump";
            d["Assets.DESCRIPTION[RSIKSpeedHumpTool]"]=ko?"클릭: 같은 방향 전체 차로. Shift+클릭: 반대 방향 포함. Shift+R: 깔끔한 도색/헤진 도색. 우클릭: 제거.":"Click: all lanes in the same direction. Shift+click: include opposing lanes. Shift+R: clean/worn. Right-click: remove.";
            d["Assets.NAME[RSIKManualStopTool]"]=ko?"정지선":"Stop line";
            d["Assets.NAME[RSIKManualArrowTool]"]=ko?"화살표 표지":"Arrow marking";
            d["Assets.NAME[RSIKManualUTurnTool]"]=ko?"유턴 표지":"U-turn marking";
            d["Assets.NAME[RSIKManualDiamondTool]"]=ko?"다이아몬드 표지":"Diamond marking";
            d["Assets.NAME[RSIKRemovePaintTool]"]=ko?"하나씩 표시 제거":"Remove marking";d["Assets.NAME[RSIKGutterTool]"]=ko?"배수로":"Gutter"; d["Assets.NAME[RSIKCurbTool]"]=ko?"경계석":"Curb";
            return d;
        }
    }
}




