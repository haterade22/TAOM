# Localization

## Overview

TAOM supports all 12 languages that vanilla Bannerlord ships with. The English text is the authoritative source; all other languages are community-contributed overrides. When a player runs a non-English locale, TAOM looks up translated strings from the language-specific files — if a translation is missing, the English fallback is shown seamlessly.

## Why This Exists

The mod ships 1,773 LOTR-specific strings (faction names, culture terms, wanderer backstories) that vanilla language packs know nothing about. Without this infrastructure, non-English players would see raw English text for TAOM content even if they'd translated everything else in Bannerlord. The `Languages/` directory is how Bannerlord discovers and loads per-language overrides for each active module.

## Architecture

### How Bannerlord Loads Translations

1. On startup, the engine scans `ModuleData/Languages/language_data.xml` in every active module
2. The root file declares `id="English"` — this is the anchor that tells the engine a `Languages/` dir exists
3. Per-language subdirs (`FR/`, `DE/`, etc.) each have their own `language_data.xml` listing translation files
4. When the player's language is set to e.g. Français, the engine loads `FR/std_taom_*.xml` and uses those translations to override the embedded English fallback text

### Key Flow

```
TextObject("{=taom_str_faction_official.empire}a clansman of Dunland")
    │
    ├── English: returns "a clansman of Dunland" (embedded fallback, no dict lookup)
    └── Français: looks up "taom_str_faction_official.empire" in FR translation file
                  → found:   returns French text
                  → missing: returns "a clansman of Dunland" (English fallback)
```

### String ID Convention

TAOM uses two localization key prefixes:

| Prefix | Source file | Count | Example key |
|--------|-------------|-------|-------------|
| `taom_str_*` | `taom_module_strings.xml` | 596 | `taom_str_faction_official.empire` |
| `aom_*` | `taom_wanderer_strings.xml` | 1,177 | `aom_prebackstory.spc_wanderer_gondor_0_text` |

The key is embedded directly in the source XML `text` attribute: `text="{=KEY}English fallback"`.

## Configuration

### Directory Structure

```
Main/_Module/ModuleData/
└── Languages/
    ├── language_data.xml          English anchor (no LanguageFile entries)
    ├── FR/
    │   ├── language_data.xml      id="Français"
    │   ├── std_taom_module_strings_fre-FR.xml
    │   └── std_taom_wanderer_strings_fre-FR.xml
    ├── DE/  ├── RU/  ├── SP/  ├── PL/  ├── IT/
    ├── TR/  ├── BR/  ├── JP/  ├── KO/  ├── CNs/  ├── CNt/
```

Each language dir follows the same 3-file pattern.

### Language ID → Directory Mapping

| Dir | Bannerlord language ID | File suffix |
|-----|------------------------|-------------|
| `FR/` | `Français` | `_fre-FR.xml` |
| `DE/` | `Deutsch` | `_deu-DE.xml` |
| `RU/` | `Русский` | `_rus-RU.xml` |
| `SP/` | `Español (LA)` | `_spa-LA.xml` |
| `PL/` | `Polski` | `_pol-PL.xml` |
| `IT/` | `Italiano` | `_ita-IT.xml` |
| `TR/` | `Türkçe` | `_tur-TR.xml` |
| `BR/` | `Português (BR)` | `_por-BR.xml` |
| `JP/` | `日本語` | `_jpn-JP.xml` |
| `KO/` | `한국어` | `_kor-KO.xml` |
| `CNs/` | `简体中文` | `_zho-CN.xml` |
| `CNt/` | `繁體中文` | `_zho-HK.xml` |

**Important:** The `LanguageData id` must match the string Bannerlord uses internally (from `Native/ModuleData/Languages/{LANG}/language_data.xml`). These are verified against the vanilla files.

## Key Files

| File | Purpose |
|------|---------|
| `Main/_Module/ModuleData/taom_module_strings.xml` | Source of 596 English strings with `taom_str_*` keys |
| `Main/_Module/ModuleData/taom_wanderer_strings.xml` | Source of 1,177 English strings with `aom_*` keys |
| `Main/_Module/ModuleData/Languages/language_data.xml` | English anchor — required for engine discovery |
| `Main/_Module/ModuleData/Languages/{LANG}/language_data.xml` | Per-language manifest listing translation files |
| `Main/_Module/ModuleData/Languages/{LANG}/std_taom_*.xml` | Community translation files (stub templates) |
| `TAOM.Tests/Infrastructure/Localization/LanguageDataXmlTests.cs` | Structural contract tests |

## Dependencies

- No C# code changes required — the localization system is purely data-driven
- `TaleWorlds.Localization.dll` (`LocalizedTextManager`, `MBTextManager`) handles all loading
- Discovery is automatic — no registration in `SubModule.xml` or C# needed for `Languages/` files

## Tests

`TAOM.Tests/Infrastructure/Localization/LanguageDataXmlTests.cs` — 15 structural contract tests:

| Test | What it guards |
|------|----------------|
| `LanguagesDirExists` | Languages/ dir is present |
| `RootLanguageDataFile_Exists` | English anchor file exists |
| `RootLanguageDataFile_HasEnglishId` | Anchor declares `id="English"` |
| `AllSupportedLanguageDirs_Exist` | All 12 language dirs are present |
| `AllLanguageSubdirs_HaveLanguageDataXml` | Every language dir has its manifest |
| `AllLanguageDataXml_AreWellFormedXml` | Manifests parse without error |
| `AllLanguageDataXml_HaveLanguageDataRootElement` | Root element is `<LanguageData>` |
| `AllLanguageDataXml_HaveNonEmptyId` | Every manifest has a non-empty language ID |
| `AllLanguageFilePaths_ReferenceExistingFiles` | Every `xml_path` in manifests resolves to a real file |
| `AllTranslationFiles_AreWellFormedXml` | Translation stubs parse without error |
| `AllTranslationFiles_HaveBaseRootWithTypeString` | Root is `<base type="string">` |
| `AllTranslationFiles_HaveTagsElement` | `<tags><tag language="..."/></tags>` is present |
| `AllTranslationFiles_HaveStringsElement` | `<strings>` container is present |
| `AllTranslationFiles_StringEntries_HaveIdAndTextAttributes` | Any existing entries have both `id` and `text` |

Run with: `dotnet test TAOM.Tests --filter "FullyQualifiedName~LanguageDataXml"`

## How-To

### How to add a translation for an existing language

1. Open the target language file, e.g. `Languages/FR/std_taom_module_strings_fre-FR.xml`
2. Find the English source text in `taom_module_strings.xml`:
   ```xml
   <string id="str_faction_official.empire"
           text="{=taom_str_faction_official.empire}a clansman of Dunland" />
   ```
3. The localization key is `taom_str_faction_official.empire` (the value inside `{=...}`)
4. Add to the `<strings>` block:
   ```xml
   <string id="taom_str_faction_official.empire" text="un homme de clan de Dunland" />
   ```
5. Preserve all variables (`{RULER.NAME}`) and conditionals (`{?RULER.GENDER}..{?}..{\?}`) verbatim
6. Do NOT use `{=...}` syntax in the `text` attribute of a translation file

### How to add a new language not in the current list

Bannerlord must already support the language natively (it must have an entry in `Native/ModuleData/Languages/`). If it does:

1. Create a new directory under `Languages/` matching the vanilla directory name (e.g. `PT/`)
2. Create `Languages/PT/language_data.xml` with the correct language ID from the vanilla file:
   ```xml
   <?xml version="1.0" encoding="utf-8"?>
   <LanguageData id="[id from vanilla]">
     <LanguageFile xml_path="PT/std_taom_module_strings_[locale].xml" />
     <LanguageFile xml_path="PT/std_taom_wanderer_strings_[locale].xml" />
   </LanguageData>
   ```
3. Create the two stub translation files following the pattern in any existing language dir
4. Add the new dir name to `SupportedLanguageDirs` in `LanguageDataXmlTests.cs`
5. Run tests to confirm structure is correct

### What strings are NOT translatable through this system

| Content | Reason | Workaround |
|---------|--------|------------|
| CharacterCreation JSON narratives | Custom C# loader, not TextObject-based | Requires code changes to inject translated content |
| XSLT-modified action/comment strings | Reuse vanilla hash IDs — vanilla language packs handle them | None needed; vanilla translations still apply |
| Tolkien proper nouns (Gondor, Aragorn) | Convention — not translated in official LOTR | None — leave as English |
| Equipment/item names | Owned by LOTRLOME_Armory module | Translate in that module |

## Performance

No performance impact — translation files are loaded once at startup by the engine and cached in a static dictionary. Empty translation files (the current stub state) add zero overhead.

## Changelog

- 2026-05-23 — Added the AI first-draft translation pipeline (`tools/translate_with_claude.py` + `tools/rebuild_translation_files.py`) with a 4-tier fallback chain (overrides → cache → LLM → English) and first-draft coverage across all 11 AI-translated languages.
- 2026-04-29 — Code-side string localization (#96): wrapped Main Menu / CC Narrative / Career System literals with `{=KEY}default`, extracted `taom_cc_strings.xml` + `taom_career_strings.xml`, scaffolded per-language stubs, and bumped `LanguageDataXmlTests` from 3 to 5 LanguageFile entries.
- 2026-04-03 — Localization Infrastructure (#65): added the `Languages/` directory structure (37 XML files — English anchor, 12 manifests, 24 stubs), made 1,773 strings translatable with English fallback, and added 15 structural contract tests in `LanguageDataXmlTests.cs`.

---

<!-- backlinks-start auto-generated; edit lint_docs.py / build_backlinks.py to change -->

## Referenced by

- [docs/features/localization-override.md](./localization-override.md)
- [docs/INDEX.md](../INDEX.md)
- [docs/modding/module-taom.md](../modding/module-taom.md)
- [docs/modding/strings-and-localization.md](../modding/strings-and-localization.md)

<!-- backlinks-end -->
