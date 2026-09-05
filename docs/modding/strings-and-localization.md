# Strings and localization

## What this file is

A strings XML is a flat list of `<string id="..." text="..."/>` rows, and it is the registry that gives every player-facing name and sentence in TAOM a stable key. Every other data file points at that key with the `{=KEY}English fallback` form written straight into an attribute, so the key and the English words travel together in one place. The twelve per-language files under `Languages/` then swap the words out for everyone who is not playing in English.

If you only remember one rule: **the English text a player sees is the literal after `{=KEY}`, never the registry row.** `MBTextManager.GetLocalizedText` returns the inline default and stops before it ever looks a key up when the active language is English (`MBTextManager.cs:264-267`), and `LocalizedTextManager.LoadLanguage` skips the `<strings>` block of every language file when the language is English (`LocalizedTextManager.cs:235,247`). Registering a key buys you the other eleven languages and nothing else.

## Where it lives and how it is registered

There are three registration kinds and they are not interchangeable. Putting a string in the wrong one fails in a different way each time.

<!-- engine-ref type="TaleWorlds.Core.GameTextManager" file="Core/TaleWorlds.Core/TaleWorlds.Core/GameTextManager.cs" lines="107-165" -->

| Kind | Where the file goes | How the engine finds it | Which manager ends up holding it |
|---|---|---|---|
| **`GameText` XmlName** | anywhere under `Main/_Module/ModuleData/`, named in `Main/_Module/SubModule.xml` | `MBObjectManager.GetMergedXmlForManaged("GameText", ...)` merges every module's rows (`GameTextManager.cs:117`) | the per-`Game` `GameTextManager`, built when a campaign initialises |
| **`global_strings.xml` literal path** | `Main/_Module/ModuleData/global_strings.xml`, that exact name at the ModuleData root | `LoadDefaultTexts` walks every installed module and opens the literal path (`GameTextManager.cs:127-140`); it never reads `SubModule.xml` | `Module.CurrentModule.GlobalTextManager`, which is what the Options keybinding screen reads |
| **`Languages/` auto-discovery** | `Main/_Module/ModuleData/Languages/<LANG>/`, listed in that folder's `language_data.xml` | `LoadLocalizationXmls` recurses every module's `ModuleData/Languages` looking for files called `language_data.xml` (`LocalizedTextManager.cs:86-118`) | the flat, active-language `_gameTextDictionary` |

TAOM's `SubModule.xml` carries 15 `<XmlName id="GameText">` rows. Twelve name a TAOM-owned `.xml`; the other three (`module_strings`, `action_strings`, `comment_strings`) have no `.xml` in this repo at all, because they are XSLT overlays on the vanilla files of the same name. <!-- measured: rg -n 'GameText' Main/_Module/SubModule.xml and ls Main/_Module/ModuleData/*.xslt 2026-09-05 --> A GameText row looks like this.

<!-- excerpt file="Main/_Module/SubModule.xml" -->

```xml
    <XmlNode>
      <XmlName id="GameText" path="taom_module_strings"/>
      <IncludedGameTypes>
        <GameType value="Campaign"/>
        <GameType value="CampaignStoryMode"/>
      </IncludedGameTypes>
    </XmlNode>
```

The `path` has no `.xml` and no leading `ModuleData/`, the same convention the rest of [submodule-and-registration](submodule-and-registration.md) describes.

**Which file owns which prefix.** A key prefix is an ownership claim: grep every strings XML for it before you take one, because two features writing the same prefix produced 70 colliding rows in the 2026-08-23 camps port (`docs/reviews/lessons/localization-ui.md:440-450`).

<!-- measured: python ElementTree scan counting <string> rows and their {=KEY} prefixes per file under Main/_Module/ModuleData outside Languages/ 2026-09-05 -->

| Source file | Rows | Dominant prefixes |
|---|---|---|
| `taom_module_strings.xml` | 2618 | `taom_faction_` 744, `taom_career_` 459, `taom_str_` 374 |
| `taom_career_strings.xml` | 2050 | `taom_career_` 100, `taom_ability_` 100, `taom_buc_` 61 |
| `taom_xslt_strings.xml` | 1449 | `aom_lord_` 389, `aom_harad_` 120, plus 576 keys with no prefix pattern |
| `taom_wanderer_strings.xml` | 1337 | `aom_backstory_` 680, `aom_response_` 340 |
| `taom_cc_strings.xml` | 966 | `taom_cc_` 966 |
| `taom_lotr_issue_strings.xml` | 308 | `taom_lotr_` 308 |
| `taom_enlistment_strings.xml` | 252 | `taom_enlist_` 225, `taom_fc_` 27 |
| `named_companions/named_companion_strings.xml` | 119 | `nc_backstory_` 68, `nc_response_` 34 |
| `taom_messenger_strings.xml` | 29 | `taom_messenger_` 28 |
| `taom_wotr_strings.xml` | 24 | `taom_wotr_` 24 |
| `taom_emissary_strings.xml` | 22 | `taom_emissary_` 22 |
| `taom_player_switcher_strings.xml` | 12 | `taom_ps_` 12 |
| `global_strings.xml` | 8 | `taom_key_` 8 |

Root element of every one of them is `<strings>`; the per-entry element is `<string>`; the engine class is `TaleWorlds.Core.GameTextManager`. The per-language twins live at `Main/_Module/ModuleData/Languages/<LANG>/std_taom_*.xml`, use the root element `<base type="string">` and are read by `TaleWorlds.Localization.LocalizedTextManager`.

## Attributes

<!-- engine-table type="TaleWorlds.Core.GameTextManager" file="Core/TaleWorlds.Core/TaleWorlds.Core/GameTextManager.cs" method="LoadFromXML" -->

| Attribute | Type | Required | Default when absent | What it does | Read at (file:line) |
|---|---|---|---|---|---|
| `id` | string | yes | none, the row throws and is swallowed | The registry key. Split on the first `.`: `taom_str_faction_official.empire` registers game text `taom_str_faction_official` with the variation `empire`. Duplicate ids across the merged set shadow each other with no error. | `GameTextManager.cs:190` |
| `text` | string | yes | none, the row throws and is swallowed | The English text. In TAOM it is written `{=KEY}English words` so that the row also declares the translation key, and for `taom_wotr_strings.xml` it is written bare, in which case the `id` is the key. | `GameTextManager.cs:198` |

A `<string>` may also carry a `<tags>` block whose `<tag>` children take `weight` and `tag_name` and feed the engine's weighted-variation picker (`GameTextManager.cs:213-217`). No TAOM strings file uses it.

The per-language files are parsed by a different reader with a different, smaller vocabulary.

<!-- engine-table type="TaleWorlds.Localization.LocalizedTextManager" file="Core/TaleWorlds.Localization/TaleWorlds.Localization/LocalizedTextManager.cs" method="LoadLanguage" -->

| Attribute | Type | Required | Default when absent | What it does | Read at (file:line) |
|---|---|---|---|---|---|
| `id` | string | yes | throws, and the throw kills the whole file | The key this row translates. Must match the `{=KEY}` of the English row, not the English row's outer `id`, when the two differ. | `LocalizedTextManager.cs:281` |
| `text` | string | yes | throws, and the throw kills the whole file | The translated text. Never write `{=...}` here. | `LocalizedTextManager.cs:282` |
| `functionName` | string | on `<function>` only | throws | Names a text-processing function for the language. Native uses this for the Russian and Turkish processors; no TAOM file declares one. | `LocalizedTextManager.cs:264` |
| `functionBody` | string | on `<function>` only | throws | The function body that goes with it. | `LocalizedTextManager.cs:265` |

And the folder manifest, `Languages/<LANG>/language_data.xml`.

<!-- engine-table type="TaleWorlds.Localization.LanguageData" file="Core/TaleWorlds.Localization/TaleWorlds.Localization/LanguageData.cs" method="LoadFromXml" -->

| Attribute | Type | Required | Default when absent | What it does | Read at (file:line) |
|---|---|---|---|---|---|
| `id` | string | yes | the node is skipped entirely | The language's own display name (`Русский`, not `RU`). Modules with the same `id` merge into one `LanguageData`, which is how TAOM's twelve one-line manifests attach to the definitions `Native` already declared. | `LanguageData.cs:156` |

<!-- engine-table type="TaleWorlds.Localization.LanguageData" file="Core/TaleWorlds.Localization/TaleWorlds.Localization/LanguageData.cs" method="Deserialize" -->

| Attribute | Type | Required | Default when absent | What it does | Read at (file:line) |
|---|---|---|---|---|---|
| `name` | string | no | keeps whatever another module set | The title shown in the Options language dropdown. | `LanguageData.cs:91` |
| `subtitle_extension` | string | no | keeps whatever another module set | Suffix used to find subtitle files. | `LanguageData.cs:96` |
| `supported_iso` | comma list | no | union stays as-is | ISO codes that select this language. `IsValid` is `SupportedIsoCodes.Length != 0`, so at least one module must supply them. TAOM's do not; `Native` does. | `LanguageData.cs:101` |
| `text_processor` | string | no | keeps whatever another module set | Assembly-qualified text processor. Read but only assigned when `name` is non-empty, so a file that sets `text_processor` without `name` sets nothing. | `LanguageData.cs:107` |
| `under_development` | bool | no | false | Hides the language from the normal dropdown. | `LanguageData.cs:112` |
| `xml_path` | string | yes on a child | the child is ignored | Module-relative path of one translation file. | `LanguageData.cs:127,135` |

## Child elements

<!-- engine-table type="TaleWorlds.Core.GameTextManager" file="Core/TaleWorlds.Core/TaleWorlds.Core/GameTextManager.cs" method="LoadFromXML" -->

| Element | Type | Required | Default when absent | What it does | Read at (file:line) |
|---|---|---|---|---|---|
| `<strings>` | root | yes | nothing loads, silently | Must be the document's own top-level element. The reader walks `doc.ChildNodes` looking for it, so a wrapper around it hides everything inside. | `GameTextManager.cs:172` |
| `<string>` | entry | yes | nothing loads | One key. Anything else at that level is skipped. | `GameTextManager.cs:182` |
| `<tags>` | child of `<string>` | no | one unweighted variation | Weighted variation selection. Unused in TAOM. | `GameTextManager.cs:202` |

<!-- engine-table type="TaleWorlds.Localization.LocalizedTextManager" file="Core/TaleWorlds.Localization/TaleWorlds.Localization/LocalizedTextManager.cs" method="LoadLanguage" -->

| Element | Type | Required | Default when absent | What it does | Read at (file:line) |
|---|---|---|---|---|---|
| `<strings>` | child of `<base>` | yes | the file contributes nothing | Holds the translated rows. Skipped outright when the active language is English. | `LocalizedTextManager.cs:245,235` |
| `<string>` | entry | yes | nothing loads | One translated row. | `LocalizedTextManager.cs:251` |
| `<functions>` | child of `<base>` | no | no processors registered | Language-level text functions. | `LocalizedTextManager.cs:258` |
| `<function>` | entry | no | none | One function. | `LocalizedTextManager.cs:262` |

The `<base>` wrapper is not decoration. `LoadLanguage` starts at `xmlDocument.ChildNodes[1].FirstChild` (`LocalizedTextManager.cs:243`), which is the second top-level node: the XML declaration is node 0 and `<base>` is node 1. Drop the declaration, or promote `<strings>` to the root, and the file loads as nothing.

<!-- engine-table type="TaleWorlds.Localization.LanguageData" file="Core/TaleWorlds.Localization/TaleWorlds.Localization/LanguageData.cs" method="LoadFromXml" -->

| Element | Type | Required | Default when absent | What it does | Read at (file:line) |
|---|---|---|---|---|---|
| `<LanguageData>` | root | yes | the file contributes nothing | The only top-level element the reader looks for. Anything else at that level is skipped without comment. | `LanguageData.cs:154` |

<!-- engine-table type="TaleWorlds.Localization.LanguageData" file="Core/TaleWorlds.Localization/TaleWorlds.Localization/LanguageData.cs" method="Deserialize" -->

| Element | Type | Required | Default when absent | What it does | Read at (file:line) |
|---|---|---|---|---|---|
| `<LanguageFile>` | child of `<LanguageData>` | yes | the translation file is never opened | Adds one file to this language's load list. A file present on disk but missing here is dead weight. | `LanguageData.cs:125` |
| `<VoiceFile>` | child of `<LanguageData>` | no | no voice data | Voice-over manifest. TAOM ships none. | `LanguageData.cs:133` |

## Worked example

One name, three files. This is the whole shape of a translated string in TAOM.

The reference site, an inline `{=KEY}Fallback` on a data entry:

<!-- example file="Main/_Module/ModuleData/characters/lords.xml" id="lord_1_1_10" -->

```xml
    <NPCCharacter id="lord_1_1_10" name="{=aom_lord_1_1_10_name}Haldis Redmist" default_group="Cavalry" age="21" voice="curt" is_hero="true" is_female="true" culture="Culture.empire" occupation="Lord" face_mesh_cache="true" skill_template="SkillSet.taom_dunland_young_lady_skills">
```

The registry row, in a `GameText`-registered strings file:

<!-- example file="Main/_Module/ModuleData/taom_xslt_strings.xml" id="aom_lord_1_1_10_name" -->

```xml
	<string id="aom_lord_1_1_10_name" text="{=aom_lord_1_1_10_name}Haldis Redmist" />
```

And one of the twelve translated twins:

<!-- example file="Main/_Module/ModuleData/Languages/RU/std_taom_xslt_strings_rus-RU.xml" id="aom_lord_1_1_10_name" -->

```xml
    <string id="aom_lord_1_1_10_name" text="Халдис Алый Туман" />
```

What you change first:

1. **The literal after `{=...}` in `lords.xml`.** That string, not the registry row, is what an English player reads. Change it here or English does not change.
2. **The identical literal in the registry row's `text`.** It is the translator's source text. Leave it stale and the next translator run measures against the old English.
3. **The `text` of all twelve twins.** They are what the other eleven languages render, and nothing propagates a reworded English string into them for you.

The outer `id` on the registry row and the `{=KEY}` inside its own `text` are the same string here, which is the convention every TAOM strings file follows except `taom_wotr_strings.xml`. The key that matters is the one inside `{=...}`; that is what both the translator and `LanguageFileCoverageTests` address a row by.

## Recipes: Add / Modify / Delete

### Add

A new player-facing name or sentence, from nothing to twelve languages.

1. Pick a key. Prefix it with the feature that owns it, and grep first: `rg 'id="taom_myfeature_greeting"|\{=taom_myfeature_greeting\}' Main/_Module/ModuleData/`. Expect zero hits.
2. Write the reference with the key inline: `name="{=taom_myfeature_greeting}Good morning"` in the data XML, or `new TextObject("{=taom_myfeature_greeting}Good morning")` in C#.
3. Add the registry row to the strings file that owns the prefix, from the table above: `<string id="taom_myfeature_greeting" text="{=taom_myfeature_greeting}Good morning" />`. For keys declared as C# literals you can skip the hand edit and let `python tools/harvest_literal_loc_keys.py --dry-run` find them, then `--apply`; it scans `Main/**/*.cs` and only `taom_*` keys, so an `aom_*` key or a key inside an XML attribute has to be added by hand.
4. Seed the row into all twelve language files, then translate: `python tools/translate_with_claude.py --lang RU --module TAOM --sync-ids --dry-run`, read the count, then re-run with `--apply`. `--sync-ids` must run before the translation, because `write_back` substitutes by id and has nowhere to put a key the target file does not already declare (`docs/reviews/lessons/localization-ui.md:353-380`).
5. Never reach for `python tools/generate_translation_template.py --all --apply` to do step 4. It overwrites each per-language file with a fresh English template and discards every translation in it.

Check: `python tools/validate_moduledata.py` for the XML, then `dotnet test TAOM.Tests --filter LanguageFileCoverage` and `--filter LanguageDataXml`
Takes effect: full game restart
Code: No code changes needed for an XML-side key. Code changes required in the owning `Main/Features/<Name>/` file if the string is a C# `TextObject`.

### Modify

Editing the English of a key that already has translations, or renaming what a lord or settlement is called. This is a three-store operation, not a one-file edit, and every gate in the repo stays green if you do only part of it.

1. Change the English text in the source XML under `Main/_Module/ModuleData/`, or in the C# literal if the string is built there.
2. Change the same words in the inline `{=KEY}Fallback` at every reference site. For a rename this is the step that is easy to miss, and it is the only one an English player can see. Thirteen TAOM lords drifted this way (`docs/reviews/lessons/localization-ui.md:105-136`).
3. Update all twelve per-language rows. If only a numeral moved, substitute per row and keep each language's own phrasing; the percent sign is written four different ways across the twelve, and even one language is not internally consistent (`docs/localization/TRANSLATOR_GUIDE.md:292-303`).
4. Evict the key from all twelve `tools/translation_cache/<lang>.json`. The cache is keyed on the string id alone, so a reworded string resolves from cache at zero cost and a later `--apply` writes the old wording straight back over your fix.
5. Confirm the eviction worked: `python tools/translate_with_claude.py --lang DE --module TAOM --dry-run` must report the key as needing the LLM. A dry run that still reports it resolved from cache means step 4 did not take.

Check: `python tools/translate_with_claude.py --lang DE --module TAOM --dry-run`, then `bash tools/translation_status.sh`
Takes effect: full game restart
Code: No code changes needed, unless the English lives in a C# literal, in which case code changes required in that feature file.

Changing the SHAPE of a string, rather than its wording, needs a **new key id**. The eleven translated rows win for their languages, so reusing the id renders the old sentence and silently drops any new `{TOKEN}` the new template introduced, while English looks correct throughout (`docs/reviews/lessons/localization-ui.md:230-266`).

### Delete

1. Remove every reference first: the inline `{=KEY}...` in XML or C#. A key with a row but no reader is a dead key, and the only thing that finds one is reading the code (`docs/reviews/lessons/localization-ui.md:461-467`).
2. Remove the `<string>` row from the source file under `Main/_Module/ModuleData/`.
3. Remove the row from all twelve `Languages/<LANG>/std_taom_*.xml`. Leaving it is harmless at runtime but makes the next coverage diff noisy.
4. Delete the key from all twelve `tools/translation_cache/<lang>.json` and from `tools/translation_overrides/<lang>.json` if it is parked there.
5. If you removed the last key of an entire file, also drop its `<XmlName id="GameText">` row from `Main/_Module/SubModule.xml`, its `<LanguageFile>` row from all twelve `language_data.xml`, and lower the count in `LanguageDataXmlTests.AllLanguageDirs_HaveExactlyThirteenLanguageFiles`.

Check: `dotnet test TAOM.Tests --filter LanguageDataXml`
Takes effect: full game restart
Code: No code changes needed, unless a C# `TextObject` referenced the key.

### Add a name that lives in another module

Settlement names and equipment names are not TAOM's to register. They belong to `TAOM_Map` and `LOTRLOME_Armory`, whose ModuleData lives in the game install, not the repo; a module reinstall reverts hand edits, so land a repo-side validator gate with any fix.

1. Settlement names: the key is written into `TAOM_Map/ModuleData/settlements.xml` as `name="{=Settlements.Settlement.name.<settlement id>}English Name"`. See [settlements](settlements.md) for the entry itself.
2. Translate it by adding a matching `<string id="Settlements.Settlement.name.<settlement id>" text="..."/>` row to each of the twelve `TAOM_Map/ModuleData/Languages/<LANG>/loc_settlements.xml`. That is thirteen files for one name: the source plus twelve twins.
3. Equipment names work the same way against `LOTRLOME_Armory/ModuleData/Languages/<LANG>/loc_<culture>.xml`, one file per culture rather than one file for everything.
4. Because neither module is tracked here, run the in-repo ratchet afterwards so a reinstall that drops the English files back over your work is visible: `python tools/check_external_loc_coverage.py`.

Check: `python tools/check_external_loc_coverage.py`
Takes effect: full game restart
Code: No code changes needed.

## Gotchas: what fails silently and what crashes

- **Registering a key is not the same as translating it, and neither is the same as it being read.** Three independent failures with one appearance: perfect English, green suite, no warning. 317 keys were never registered and 96 were registered but never propagated before #434 swept them (`docs/reviews/lessons/localization-ui.md:353-380`).
- **No TAOM tool sees a `{=key}` written into an XML attribute.** `harvest_literal_loc_keys.py` scans `Main/**/*.cs` for `taom_*` literals (`tools/harvest_literal_loc_keys.py:38,87`), and `LanguageFileCoverageTests.LoadEnglishKeys` collects `<string>` rows only (`TAOM.Tests/Infrastructure/Localization/LanguageFileCoverageTests.cs:82-109`). So all 836 distinct `{=key}` values across the sixteen troop files are unregistered, and every troop name in TAOM ships English in all twelve languages. <!-- measured: python regex scan of Main/_Module/ModuleData/troops/*.xml against every <string id> in the strings XMLs 2026-09-05 --> TAOM has never localized a troop name and there is no tool for it; the route is the [Add](#add) recipe by hand, one `<string>` row per troop, and [troops](troops.md) is where the names themselves live.
- **A duplicate `<string id>` shadows silently.** The merged GameText set has no uniqueness check, so one row wins at load with no build error and no test failure, and a dialog can render the wrong text with unset variables (`docs/reviews/lessons/localization-ui.md:35-40`).
- **A malformed per-language file loses the whole file, not the row.** `LoadXmlFile` swallows the parse exception and returns null, and the caller just moves on (`LocalizedTextManager.cs:202-219`). One unescaped `&` costs a language every string in that file.
- **The active-language dictionary is flat and last-wins across every module.** `_gameTextDictionary[value] = value2` (`LocalizedTextManager.cs:283`), so a key another module also declares overwrites yours with no diagnostic.
- **Keybinding labels must be in `global_strings.xml` at the ModuleData root, and nowhere else.** A `GameText` XmlName feeds a different manager entirely, and the wrong placement renders as `ERROR: Text with id str_key_name doesn't exist!` (`docs/features/time-acceleration.md:86-107`).
- **Line terminators are not uniform, so scripts must detect them.** Ten of the thirteen `std_taom_*` types end their lines with a doubled CR: `std_taom_xslt_strings_rus-RU.xml` holds 1,459 `\r\r\n` terminators and 26 plain `\r\n`, while `std_taom_enlistment_strings_rus-RU.xml` holds 0 and 467. <!-- measured: python byte count of b'\r\r\n' and b'\r\n' in the two files 2026-09-05 --> A `\r?\n` regex matches nothing on the doubled ones and a text-mode round trip rewrites every line (`docs/localization/TRANSLATOR_GUIDE.md:65-98`).
- **Nothing here is BOM-tolerant by policy, but the repo is inconsistent.** `taom_xslt_strings.xml` has a UTF-8 BOM and `taom_module_strings.xml` does not; every file under `Languages/` has none. Match the file you are editing rather than normalising it.
- **A non-zero exit from the translator is not automatically your problem.** It exits 1 if any entry failed validation, and four vanilla-derived strings with nested gender conditionals fail on every uncached language: `uc4M4bhG`, `qa4FlTWS`, `uiY3ds0Z`, `V097rA1v` (`docs/localization/TRANSLATOR_GUIDE.md:504-508`).
- **`tools/translation_overrides/<lang>.json` outranks everything.** A key parked there is pinned no matter what the cache or the API says. Only four languages have an overrides file today: `cns`, `ko`, `ru`, `tr`. <!-- measured: ls tools/translation_overrides/ 2026-09-05 -->
- **To test a change without a full restart, use the console.** `localization.change_language <name>` switches language (`LocalizedTextManager.cs:286`) and `localization.reload_texts` re-reads the active language's files in place (`LocalizedTextManager.cs:315-320`). Neither helps in English, because the English text is the inline literal that was read when the object loaded. `localization.check_for_errors` writes `faulty_translation_lines.txt` (`LocalizedTextManager.cs:323`).

## Numbers in this chapter

Every count below was produced on 2026-09-05 by the command beside it, run from the repo root.

| Number | Command |
|---|---|
| 13 English key-bearing XML files outside `Languages/`, holding 9,134 distinct translation keys | a python ElementTree walk of `Main/_Module/ModuleData`, skipping `Languages/`, keying each `<string>` by its `{=KEY}` prefix or its `id` |
| Per-file row counts and prefixes in the ownership table (2618, 2050, 1449, 1337, 966, 308, 252, 119, 29, 24, 22, 12, 8) | the same walk, counting rows and prefix groups per file |
| 15 `<XmlName id="GameText">` rows in `SubModule.xml`; 8 XSLT files at the ModuleData root | `rg -n 'GameText' Main/_Module/SubModule.xml` and `ls Main/_Module/ModuleData/*.xslt` |
| 12 language directories, 14 files in each (one `language_data.xml` plus 13 `std_taom_*`), and 13 `<LanguageFile>` rows in every `language_data.xml` | `ls -d Main/_Module/ModuleData/Languages/*/` and `grep -c '<LanguageFile' <dir>/language_data.xml` |
| 836 distinct `{=key}` values across the 16 troop files, 0 of them registered in any strings XML | a python regex scan of `Main/_Module/ModuleData/troops/*.xml` for `\{=([^}]+)\}`, intersected with every `<string id="...">` in the strings XMLs |
| 1,459 `\r\r\n` and 26 plain `\r\n` in `std_taom_xslt_strings_rus-RU.xml`; 0 and 467 in `std_taom_enlistment_strings_rus-RU.xml` | a python byte count of `b'\r\r\n'` and `b'\r\n'` in each file |
| 4 override files (`cns`, `ko`, `ru`, `tr`); 12 cache files totalling 18 MB | `ls tools/translation_overrides/` and `du -sh tools/translation_cache` |
| 988 `<Settlement>` entries in the live `TAOM_Map` settlements file, and 1,227 rows in its Russian `loc_settlements.xml` | `grep -c '<Settlement '` and `grep -o '<string ' \| wc -l` against the two files in the game install |

## Read next

- [docs/reference/localization-map.md](../reference/localization-map.md) for the full component map, the tools and the cache
- [docs/localization/TRANSLATOR_GUIDE.md](../localization/TRANSLATOR_GUIDE.md) for the file format, the line-ending rules, the stale-English mechanism and the order of steps when adding a whole new strings file
- [docs/features/localization.md](../features/localization.md) for how the engine loads translations and what is not translatable through this system
- [docs/features/localization-override.md](../features/localization-override.md) for overriding a vanilla string by putting its id in a `{=...}` prefix
- [docs/features/time-acceleration.md](../features/time-acceleration.md) for the `global_strings.xml` keybinding rule and why the other two registration kinds do not work there
- [docs/reviews/lessons/localization-ui.md](../reviews/lessons/localization-ui.md) for the accumulated failure modes
- [.claude/skills/localize/SKILL.md](../../.claude/skills/localize/SKILL.md) for the guided workflow
- [tools/README.md](../../tools/README.md) for the rest of the pipeline scripts

---

<!-- backlinks-start auto-generated; edit lint_docs.py / build_backlinks.py to change -->

## Referenced by

- [docs/INDEX.md](../INDEX.md)
- [docs/modding/clans.md](./clans.md)
- [docs/modding/configs-factions-and-world.md](./configs-factions-and-world.md)
- [docs/modding/cultures.md](./cultures.md)
- [docs/modding/file-catalogue.md](./file-catalogue.md)
- [docs/modding/items-armor.md](./items-armor.md)
- [docs/modding/items-shields.md](./items-shields.md)
- [docs/modding/items-weapons-and-crafting.md](./items-weapons-and-crafting.md)
- [docs/modding/kingdoms.md](./kingdoms.md)
- [docs/modding/lords-and-heroes.md](./lords-and-heroes.md)
- [docs/modding/module-map.md](./module-map.md)
- [docs/modding/module-taom.md](./module-taom.md)
- [docs/modding/modules-overview.md](./modules-overview.md)
- [docs/modding/README.md](./README.md)
- [docs/modding/recipe-add-a-culture.md](./recipe-add-a-culture.md)
- [docs/modding/recipe-add-a-kingdom.md](./recipe-add-a-kingdom.md)
- [docs/modding/recipe-add-a-race-or-creature.md](./recipe-add-a-race-or-creature.md)
- [docs/modding/recipe-new-mod-from-zero.md](./recipe-new-mod-from-zero.md)
- [docs/modding/submodule-and-registration.md](./submodule-and-registration.md)
- [docs/modding/troops.md](./troops.md)

<!-- backlinks-end -->
