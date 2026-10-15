# Road Style Into Korea

## 베타 버전 안내

**아직 개발 중인 베타 버전입니다. 사용 전 세이브 파일을 별도로 백업하고, 원본 대신 복사본에서 먼저 테스트해 주세요.** 예상하지 못한 오류나 다른 모드와의 호환성 문제가 발생할 수 있습니다.

한국 도시의 도로 풍경을 Cities: Skylines II에 담아보세요.

한국식 차선과 횡단보도, 노면 표지, 보도블록을 도로에 적용하고 원하는 구간을 직접 다듬을 수 있는 모드입니다. 도로의 기본 스타일을 정하거나, 전용 도구로 필요한 표시만 골라 배치할 수 있습니다.

## 필수 에셋

**이 모드와 함께 전용 도로 에셋 팩을 구독하고 같은 플레이셋에서 활성화해야 합니다.**

- 전용 도로 에셋 팩: **[CS KR Road Netlane and Decal Pack](https://mods.paradoxplaza.com/mods/163071)**
- 모드는 도로 스타일을 적용하고 편집하는 기능을, 에셋 팩은 표시에 필요한 넷레인과 데칼을 제공합니다.

도시를 불러온 뒤 **설정 → ROAD STYLE INTO KOREA → 필요 에셋 확인**에서 로드 상태를 확인할 수 있습니다. 누락된 에셋이 표시되면 에셋 팩의 다운로드와 플레이셋 활성화 상태를 확인해 주세요.

## 주요 기능

- **차선과 횡단보도:** 전용 도구를 통해 한국식 차선·횡단보도·유도선을 적용하고, 횡단보도 규격과 목표 거리를 조절함.
- **노면 표지와 적색 포장:** 자동 생성과 수동 배치를 통해 정지선·화살표·속도 숫자·한글 문구를 추가하고, 차로를 따라 연속·반복 적색 포장을 적용함.
- **보도와 도로 시설:** 보도 편집 도구를 통해 보도블록·경계석·배수로·점자블록·자전거도로 표시·방지턱·샛길 진입부를 꾸밈.
- **구간별 편집과 관리:** 구간 선택을 통해 스타일을 추가·제거하고, 옵션을 통해 새 도로의 기본 스타일 자동 적용을 켜고 끔. 중앙분리대가 여러 개인 도로의 중앙선을 개별 편집함. 메뉴 검색과 패널 배치 변경을 지원함.

## 시작하기

1. 모드와 필수 에셋 팩을 같은 플레이셋에서 활성화한 뒤 도시를 불러옵니다.
2. 화면 오른쪽 위의 **Road Style Into Korea** 버튼을 누릅니다.
3. 원하는 도구를 선택하고 도로 위에서 미리보기를 확인합니다.
4. 화면에 표시되는 도구별 조작 안내에 따라 적용합니다.

스타일 전환을 지원하는 도구는 **Shift + R**로 다음 스타일, **Shift + T**로 이전 스타일을 선택합니다. **모두 추가·모두 제거·데칼 자동 생성**은 도로 위를 드래그해 여러 구간을 선택할 수 있습니다. 횡단보도 거리 도구는 클릭으로 목표 거리를 1m 늘리고, **Shift + 클릭**으로 1m 줄입니다.

우클릭의 기능은 도구에 따라 삭제·복원·취소·종료로 달라집니다. 선택한 도구의 안내를 확인해 주세요.

## 전체 적용

- **적용 범위:** 설정의 전체 적용을 통해 모든 도로·일반도로·고속도로를 선택하여 기본 스타일을 적용함.
- **도로 규모별 스타일:** 왕복 4차로급 이상은 보도블록 01, 그보다 작은 도로는 보도블록 02와 한 줄 횡단보도를 적용함.
- **자동 표시:** 속도 표지는 도로 구간의 약 25%에 적용함. 배수로와 횡단보도에 붙는 보행 화살표는 자동 생성하지 않음.
- **교차로와 유턴:** 교차로의 기존 차선을 정리하고, 유도선은 전용 도구로 추가함. 유턴 표시는 횡단보도와 여유 공간이 있는 곳에 적용함.

전체 적용은 기존 개별 설정을 기본 스타일로 바꿉니다. 실행 전 세이브 파일을 별도로 백업해 주세요.

## 다른 모드와 함께 사용하기

- **ExtraDetailingTools(EDT):** 함께 쓰면 도로표지 카테고리에서 넷레인과 데칼을 찾아 직접 배치하기 좋음. 선택 사항임.

## 알아두세요

- 커스텀 도로의 구성과 연결 방식에 따라 일부 표시가 생략되거나 위치가 달라질 수 있습니다.
- 수동으로 놓은 속도 숫자나 한글 노면 문구 자체가 제한속도나 통행 규칙을 바꾸지는 않습니다.
- **실제 정지·횡단 위치 맞추기** 옵션은 연결된 차량·보행 경로의 끝점을 도색 위치에 맞춥니다. 신호와 우선권은 유지하며, 연결 충돌이 있는 경우 원래 위치를 유지할 수 있습니다.
- 공간이 부족한 접근로에서는 일부 표지가 생략되거나 더 작은 횡단보도가 적용될 수 있습니다.
- **모든 도로 스타일 교체**는 개별 설정을 기본 스타일로 바꾸는 실험 기능입니다. 사용 전 별도 저장본을 만들어 주세요.

한국어와 영어 UI를 지원합니다.

## 문제 제보

문제가 생긴 도로와 사용 중인 도구, 함께 사용하는 도로 관련 모드, 재현 방법을 스크린샷과 함께 알려주세요. 특정 도로에서만 발생한다면 해당 도로의 이름과 구성도 함께 적어주시면 확인에 도움이 됩니다.
---

# Road Style Into Korea — English

## Beta notice

**This mod is still in beta. Back up your save files before using it, and test on a separate copy of your city first.** Unexpected bugs and compatibility issues with other mods may occur.

Bring the look of Korean streets to Cities: Skylines II.

Apply Korean-style lane markings, crosswalks, road decals, and sidewalk paving, then refine individual road sections. Set default road styles or use dedicated tools to place the markings you need.

## Required assets

**Subscribe to the dedicated road asset pack alongside this mod and enable both in the same playset.**

- Dedicated road asset pack: **[CS KR Road Netlane and Decal Pack](https://mods.paradoxplaza.com/mods/163071)**
- The mod provides road styling and editing tools; the asset pack supplies the NetLanes and decals used to display them.

After loading a city, open **Settings → ROAD STYLE INTO KOREA → Check required assets** to check their loading status. If assets are missing, check the pack's download and activation status in your playset.

## Main features

- **Lane markings and crosswalks:** Use dedicated tools to apply Korean-style lane markings, crosswalks, and guidance lines, and adjust crosswalk size and target distance.
- **Road decals and red pavement:** Use automatic generation or manual placement to add stop lines, arrows, speed numbers, and Korean text, and apply continuous or repeating red pavement along lanes.
- **Sidewalks and roadside details:** Use editing tools to add sidewalk paving, curbs, gutters, tactile paving, cycle strip markings, speed humps, and side road entrances.
- **Section editing and management:** Select road sections to add or remove styles, toggle automatic styling of newly built roads, and edit individual center lines on roads with multiple medians. Search tools and customize the panel layout.

## Getting started

1. Enable the mod and required asset pack in the same playset, then load a city.
2. Click the **Road Style Into Korea** button at the top right of the screen.
3. Select a tool and hover over a road to preview the result.
4. Follow the on-screen instructions for the selected tool to apply it.

For tools with multiple styles, press **Shift + R** to select the next style and **Shift + T** to select the previous style. With **Add all, Remove all, and Auto decals**, drag over roads to select multiple sections. In the crosswalk distance tool, click to increase the target distance by 1 m, or **Shift + click** to decrease it by 1 m.

Depending on the tool, right-click deletes, restores, cancels, or exits. Check the selected tool's instructions.

## Apply All

- **Scope:** Apply defaults to all roads, ordinary roads only, or highways only from the settings.
- **Road size:** Four-lane-class roads and wider use paving 01; smaller roads use paving 02 and single-row crosswalks.
- **Automatic markings:** Speed markings appear on approximately 25% of road segments. Gutters and pedestrian arrows beside crosswalks are not added automatically.
- **Junctions and U-turns:** Clear existing junction lane markings, then add guidance lines with the dedicated tools. U-turn markings are added where there is enough room around crosswalks.

Apply All replaces individual road settings with defaults. Back up your save before running it.

## Using other mods

- **ExtraDetailingTools (EDT):** Useful for finding and directly placing NetLanes and decals from its road marking categories. Optional.

## Notes

- Some markings may be omitted or positioned differently depending on custom road layouts and connections.
- Manually placed speed numbers and Korean road text do not themselves change speed limits or traffic rules.
- The **Synchronize traffic paths** option aligns connected vehicle and pedestrian path endpoints with the markings. Signals and priorities remain unchanged; crossings with conflicting connections may retain their original position.
- On approaches with limited space, some markings may be omitted or a smaller crosswalk may be used.
- **Replace all road styles** is an experimental feature that replaces individual settings with defaults. Create a separate save before using it.

Korean and English UI are supported.

## Reporting issues

Please include screenshots, the affected road, the tool you were using, any other road-related mods, and steps to reproduce the issue. If it only occurs on a particular road, include its name and layout as well.

문제 확인을 위해 **도로 진단** 도구로 해당 구간을 기록할 수 있습니다.

Use the **Road diagnostics** tool to capture an affected road for troubleshooting.

## Links

- [GitHub — 소스 코드 / Source code](https://github.com/zacumyou/RoadStyleIntoKorea)
