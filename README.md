# Road Style Into Korea [Beta]

한국식 차선·횡단보도·노면 표지와 보도를 꾸미는 Cities: Skylines II 모드입니다.
A Cities: Skylines II mod for Korean-style lane markings, crosswalks, road decals and sidewalk details.

**Beta: back up your save files before use. / 사용 전 세이브 파일을 백업하세요.**

- [Paradox Mods](https://mods.paradoxplaza.com/mods/163083)
- Required assets: [CS KR Road Netlane and Decal Pack](https://mods.paradoxplaza.com/mods/163071)
- [Feedback and bug reports](https://forum.paradoxplaza.com/forum/threads/road-style-into-korea-beta-feedback-bug-reports-and-suggestions.1945378/)
- [Community translations / 번역 안내](docs/TRANSLATING.md)

## Features

- Korean lane markings, crosswalk sizes and adjustable crossing distance.
- Automatic or manually placed arrows, stop lines, speed markings and Korean road text.
- Red pavement, sidewalk paving, tactile paving, curbs, gutters and cycle strips.
- Section-based editing, searchable tools and configurable panel layout.
- Faster Apply All with separate ordinary-road and highway scopes, road-size defaults and junction marking cleanup.
- Native Korean/English UI and optional I18NEveryWhere community translations.

## Build

This source snapshot corresponds to 0.1.225. The asset pack is maintained separately.

Requires Windows, the official Cities: Skylines II modding toolchain (with its CSII_* environment variables), .NET SDK, .NET Framework 4.8 targeting support, and Node.js 18+.

```powershell
npm --prefix ui install
./build.ps1
```

Output is placed in `package/`. The script does not install or publish it. Game assemblies and the required road asset pack are not included. UI build configuration uses this repository's own `ui/node_modules` instead of the private workspace's shared dependency directory.

The source is available for inspection and translation contributions. No additional reuse license is granted by this initial source publication.
