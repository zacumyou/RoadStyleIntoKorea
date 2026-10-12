# I18NEveryWhere translation support (0.1.214)

RSIK uses the game localization dictionary. I18NEveryWhere is optional: Korean and English remain built in, and English is the fallback for other languages. No I18NEveryWhere assembly reference or required-mod dependency was added.

## Contributing a translation

1. Copy `lang/en-US.json` as `<localeId>.json` (for example `de-DE.json`). Translate values only. Keep keys, numbers/units where appropriate, and whitespace in status fragments. `RSIK.Menu.Language` is an internal fallback marker: retain `en` for a new language; it does not prevent translated keys from being used.
2. For a local test, place the file in `Mods/RoadStyleIntoKorea/lang/` next to the supplied `en-US.json` and `ko-KR.json`. Select the same game locale and reload translations using I18NEveryWhere or restart the game. Missing entries fall back to English.
3. For a separate community translation mod, follow I18NEveryWhere's language-pack format. Its centralized files live under `Localization/<localeId>/*.json`. Do not edit the subscribed RSIK cache: an update can replace it.
4. To replace an existing native Korean/English entry, enable I18NEveryWhere's **Overwrite** setting. Its default hook preserves entries native to the active language when this is off. This differs from missing entries inherited from the fallback language.

The catalog includes settings, tool names, brush choices, hints, text-marking labels, search controls and status fragments. Searches accept translated tool/sign labels as well as existing Korean/English names. The road markings themselves intentionally retain Korean glyphs; these JSON files change UI text, not meshes/textures. Asset-pack 163071 localization remains in that separate asset pack.

## Maintaining the source

- Edit `localization/ui.json` for UI/status values and retain existing keys. `tools/generate-localization.cjs` generates the C# native source and UI fallback catalog.
- Existing settings/tool strings remain in `src/Settings.cs` and `src/ToolText.cs`.
- `build.ps1` runs the generator, builds using the official SDK, then runs `tools/LocaleExport`. The exporter invokes the compiled `Locale.ReadEntries` and the installed game's actual ModSetting key methods, so exported IDs and values match native entries rather than guessed key patterns. It creates a Settings object for key generation only and does not load the mod or game systems.
- The build verifies matching Korean/English keys and UI references, and includes `lang` recursively. The installer preserves and copies subdirectories.
- UI override tests exercise a third-language tool label, search/category and brush instructions. These are component tests, not a live game/I18NEveryWhere test.

## Contract checked 2026-10-13

- https://github.com/baka-gourd/I18NEveryWhere
- https://raw.githubusercontent.com/baka-gourd/I18NEveryWhere/main/I18NEverywhere/Mod.cs
- https://raw.githubusercontent.com/baka-gourd/I18NEveryWhere/main/I18NEverywhere/HookLocalizationDictionary.cs

The current implementation discovers per-mod `lang/<localeId>.json` flat string dictionaries. Embedded files have lower priority than centralized translations and language packs. No custom handshake/API is necessary. Future upstream changes may require another check.
