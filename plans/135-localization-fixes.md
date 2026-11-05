# Plan 135: Fix the localization pipeline and the English overrides, then translate every wave-4 string once

> **Executor instructions**: Follow this plan step by step, in the worktree named under "Git workflow".
> It has four Parts. Part A, Part B and the free half of Part C are executor work, one session each;
> the paid translation in Part C is run by the maintainer; Part D is optional. Run every verification
> command and confirm the expected result before moving on. If anything in "STOP conditions" occurs,
> stop and report; do not improvise. Never edit `plans/README.md` on this branch: it belongs to the
> integrator. At the end of each Part, report status, commit hashes and measured counts in your hand-off,
> naming the plan 135 row as the one the integrator should update (no older plan's row is affected).
>
> **Drift check (run first, inside A0)**:
> `git diff --stat 1b00d1acd..HEAD -- Main/Features/LocalizationOverride TAOM.Tests/Features/LocalizationOverride TAOM.Tests/Infrastructure/Localization Main/_Module/ModuleData/Languages Main/_Module/SubModule.xml Main/_Module/ModuleData/taom_module_strings.xml Main/_Module/ModuleData/taom_lotr_issue_strings.xml Main/_Module/ModuleData/taom_career_strings.xml Main/_Module/ModuleData/taom_xslt_strings.xml Main/_Module/ModuleData/lords.xslt Main/_Module/ModuleData/characters/clans.xml Main/Features/LotrIssues/Templates Main/Features/Messengers/MessengerCampaignBehavior.cs tools/translate_with_claude.py tools/generate_translation_template.py tools/rebuild_translation_files.py tools/check_external_loc_coverage.py tools/harvest_literal_loc_keys.py tools/translation_overrides docs/features/localization.md docs/features/localization-override.md docs/localization/TRANSLATOR_GUIDE.md`
> Earlier WPs of this programme are expected to have changed some of these: WPs 128 to 134 add
> English rows to the English sources and English copies to the 12 language files; WP132 changes two
> regexes in `translate_with_claude.py` and adds `tools/tests/test_translate_placeholders.py`; WP123 may
> have rekeyed the override patch on `IdSlice`; WP131 may have edited `MessengerCampaignBehavior.cs`;
> many WPs edit `SubModule.xml`. A0 lists the exact checks; STOP only when one of them fails or an
> excerpt below no longer matches its file.

## Status

- **Priority**: P1. #706 and #1026 change what every non-English player reads on the public build
  today; the rest is P2 and P3.
- **Effort**: L. Part A one session, Part B one session, Part C one session plus a maintainer-run paid
  translation, Part D optional.
- **Risk**: MED. It rewrites rows in all 12 language files and the 12 translation caches, and the
  Patch25 prefix changes behaviour in every language. Two commands could spend money or write into the
  game install; both are fenced by STOP conditions.
- **Depends on**: WP128, WP129, WP130, WP131, WP132, WP133 and WP134 merged; this WP runs last in wave 4.
  Through them: WP104 (`TAOM.Tests/Infrastructure/RepoPaths.cs`), WP105, WP123, and WP101's laptop
  Armory refresh (D20).
- **Category**: bug, with tooling and test gates.
- **Wave / lane**: wave 4, lane G, last in the wave.
- **Executor**: laptop.
- **Decisions applied**: D1 (no `CHANGELOG.md`; the commit body is the entry), D2 (LocalizationOverride is
  a static 1.4.x feature with no module: `Main/IoC.cs` and `Main/SubModule.cs` stay untouched), D7 (the
  co-op and ServerSettings rows of `7abce5db9` are not ported), D8 (subjects carry `v2.0.28`), D9 (A1 drops
  the `RequiresGame` tag from the test it ports), D10, D13
  (each data fix below is verified on 1.4.x data), D19 (git only through the Bash tool), D20 (this WP
  commits ModuleData; the laptop Armory must be the refreshed one).
- **Planned at**: 1.4.x `origin/bannerlord-1.4.5` `1b00d1acd` and 1.5.x `origin/bannerlord-1.5.x`
  `e45f6df2c`, 2026-11-04. The 1.5.x ref has since moved to `c075c92f0`; `git log e45f6df2c..c075c92f0`
  over every localization tool and test path is empty (checked). Later still, `44d4dba46` touched the
  translator and `rebuild_translation_files.py`; it is not ported here (see Maintenance notes), and every
  1.5.x source below stays pinned to the commit named.
- **Issue**: port of #706 (`407b1e767`), #1026 (`41aa2779b`), #572 (`303bf2a7d`), #649 (`4ceae903b`),
  #919 with #781 and #922 (`337228885`), #1007 (`df375d99e`), #900 (`7abce5db9`), #1023 (`fd1cee10b`),
  #1018 (`616cf12f1`) and #769 (`c2c8afe42`); `e74ef3931`, `9e2a39f40` and `8a4027110` carry no issue.
  The cache seeding and the wave-4 translation pass are new work: the orchestrator creates the issue
  before implementation lands.

## Why this matters

- **313 strings show English in every language.** The public build logs
  `[LocalizationOverride] Registered 313 English string overrides` (E3 and E4, line 6). The Patch25 prefix
  answers from that table in every language, although vanilla skips its dictionary only for English
  (v1.4.8 `MBTextManager.cs:264`). 90 `aom_*` and 42 `TAOM_*` of them are TAOM names and descriptions
  whose translated rows exist in all 12 language files and are never shown.
- **Grammar processors are switched off.** v1.4.8 `LanguageData.Deserialize` assigns `TextProcessor`
  whenever `name` is non-empty, even when `text_processor` is absent, and a null processor becomes
  `DefaultTextProcessor`, whose `ProcessToken` does nothing. On this laptop every `LOTRLOME_Armory`
  `language_data.xml` (English included) and TAOM_Map's seven non-English processor languages carry
  `name` with no processor, so possessives and case endings print nothing ("Aragorn Party") in English,
  German, French, Italian, Polish, Russian, Spanish and Turkish. TAOM loads after both modules in the
  players' mod list (E1/E2 command line), so TAOM's own files declaring name and processor restore them.
- **2,020 troop, lord, clan and kingdom names have no translation path** (measured: clan 115, kingdom 60,
  lord 988, troop 857), and the six Umbar clans share one name key, so once names translate every
  language would give six clans one name.
- **181 English-only rewordings of vanilla texts replace the game's curated translations.** TAOM ships a
  machine translation of each in all 12 languages (181 rows per language); they load after SandBox's rows
  and win, losing Turkish suffixes, Russian cases and German articles. 1,803 of the 2,172 rows go;
  369 are kept on purpose.
- **The translator is unsafe for the paid run this WP needs**: `--max-entries 0` means "no cap"
  (`translate_with_claude.py:930`), seeding writes second copies of keys another file owns and rewrites
  line terminators, and there is no free seeding mode.
- **Damaged and drifted text**: about 133 language rows and 151 cache values carry words from another
  writing system; the Combat and Deliver Personnel quest fallbacks show the Deliver Goods text in every
  other language; two messenger lines and two data texts differ from their registered English.
- **Wave-4 WPs 128 to 134 add English rows only.** This WP translates them once. Most come from 1.5.x
  commits with identical English, so a 1.5.x cache entry can fill them free (1,989 of the 2,020 name keys
  alone have one), and the paid run is small.

## Current state

### Files and their roles (`origin/bannerlord-1.4.5`; every tool file below equals the fork point `a4c6d7c30`)

| File | Role |
|---|---|
| `Main/Features/LocalizationOverride/Hooks/MBTextManager_GetLocalizedText_Patch.cs` | Patch25 prefix; string-keyed table, no language gate (:21-62) |
| `Main/Features/LocalizationOverride/LocalizationOverrideLoader.cs` | Registers every `{=KEY}` of `taom_module_strings.xml` not starting `taom_` (:41) |
| `Main/SubModule.cs:226-239` | Applies Patch25 and runs the loader (unchanged by this WP) |
| `Main/_Module/ModuleData/Languages/language_data.xml`, `Languages/<L>/language_data.xml` | Manifests: `id` only, no `name` or `text_processor`; 13 `LanguageFile` rows per language |
| `Main/_Module/SubModule.xml:885-891` | GameText node of `taom_module_strings`: Campaign and CampaignStoryMode only |
| `tools/translate_with_claude.py` | The translator (1,042 lines) |
| `tools/generate_translation_template.py` | Template writer. Its `--apply` OVERWRITES every templated language file with English: this WP never runs it |
| `tools/rebuild_translation_files.py`, `tools/check_external_loc_coverage.py`, `tools/harvest_literal_loc_keys.py` | Rebuild from cache; TAOM_Map/Armory coverage check; literal-key harvest |
| `TAOM.Tests/Infrastructure/Localization/*.cs` | 7 test classes; `LanguageDataXmlTests` pins 13 `LanguageFile` rows (:142-152) |
| `Main/Features/LotrIssues/Templates/CombatLotrIssue.cs:63-65, :285`, `DeliverPersonnelLotrIssue.cs:64-65, :310` | Reuse Deliver Goods' fallback keys with their own English |
| `Main/Features/Messengers/MessengerCampaignBehavior.cs:504, :636` | Inline defaults that differ from `taom_messenger_strings.xml:36, :17` |
| `Main/_Module/ModuleData/characters/clans.xml:1084-1149` | Six Umbar clans named `{=aom_clan_umbar_1_name}` |
| `lords.xslt:21144`, `taom_xslt_strings.xml:2103`, `taom_career_strings.xml:88` | Registrations that drifted from `characters/lords.xml:13000` and `taom_careers.xml` |
| `tools/translation_cache/<l>.json`, `tools/translation_overrides/<l>.json` | Translator cache (179,215 values) and hand overrides (cns, ko, ru, tr) |

### Excerpts (`origin/bannerlord-1.4.5`)

```csharp
// Main/Features/LocalizationOverride/Hooks/MBTextManager_GetLocalizedText_Patch.cs
23    private static readonly Dictionary<string, string> _overrides = new();
29    public static bool Prefix(string text, ref string __result)
38        int idLength = end - 2;
39        if (idLength == 1 && (text[2] == '!' || text[2] == '*'))
40            return true;
42        string id = text.Substring(2, idLength);
44        if (_overrides.TryGetValue(id, out string overrideText))
```

```xml
<!-- Languages/DE/language_data.xml:2 (every language: id only); Languages/language_data.xml:2 -->
<LanguageData id="Deutsch">
<LanguageData id="English">
<!-- Main/_Module/SubModule.xml:885-891 -->
    <XmlNode>
      <XmlName id="GameText" path="taom_module_strings"/>
      <IncludedGameTypes>
        <GameType value="Campaign"/>
        <GameType value="CampaignStoryMode"/>
      </IncludedGameTypes>
    </XmlNode>
```

```python
# tools/translate_with_claude.py
234 def english_source_files(module: str) -> list[tuple[Path, str]]:      # a hand-kept 13-row list
725 def sync_missing_ids(source_path: Path, target_path: Path, dry_run: bool = False) -> list[str]:
841 def main():
864     args = p.parse_args()
930     if args.max_entries and len(need_llm) > args.max_entries:          # 0 means "no cap"
```

### Engine facts (v1.4.8, read this session with `pwsh -NoProfile -File tools/taom-src.ps1 path <Type>`)

- `TaleWorlds.Localization.MBTextManager`: `private static string _activeTextLanguageId = "English";` :36;
  `public static string ActiveTextLanguage => _activeTextLanguageId;` :52;
  `internal static string GetLocalizedText(string text)` :232; English returns the inline text at
  `if (_activeTextLanguageId == "English")` :264; other languages call
  `LocalizedTextManager.GetTranslatedText(_activeTextLanguageId, ...)` :275.
- `TaleWorlds.Localization.LanguageData.Deserialize` :85-143: `name` read at :91; :107-111 reads
  `text_processor` but tests the NAME: `if (!string.IsNullOrEmpty(text)) { TextProcessor = textProcessor; }`.
- `TaleWorlds.Localization.LocalizedTextManager`: `CreateTextProcessorForLanguage` :53-67 returns
  `DefaultTextProcessor` when `TextProcessor` is null; `LoadLanguage` :231-273 reads `<strings>` only when
  `stringId != "English"` (:235); `LoadXmlFile` :202-219 reads through `new StreamReader(path)` and returns
  null on any parse error (a malformed language file is skipped silently). Dump
  `LocalizedTextManager.cs:86-115` (`LoadLocalizationXmls`) walks the modules in load order
  (`Module.cs:294-301`), so the last module's `language_data.xml` sets the processor.
- `DefaultTextProcessor.ProcessToken` has an empty body (dump `DefaultTextProcessor.cs`). The eight
  processors exist in the installed DLLs: `TaleWorlds.Localization.TextProcessor.LanguageProcessors.`
  `EnglishTextProcessor`, `GermanTextProcessor`, `FrenchTextProcessor`, `ItalianTextProcessor`,
  `PolishTextProcessor`, `RussianTextProcessor`, `SpanishTextProcessor`, `TurkishTextProcessor`. There is no
  Portuguese processor in v1.4.8.
- Installed Native `language_data.xml`: processors for English, Deutsch, Français, Italiano, Polski,
  Русский, Español (LA), Türkçe; none for Português (BR), 简体中文, 繁體中文, 日本語, 한국어. TAOM's ids equal
  Native's for all 13.
- `TaleWorlds.Core.GameTextManager.LoadGameTexts` :107 merges the GameText XmlNodes for
  `current.GameType.GetType().Name` (:117); `LoadFromXML` reads `<strings><string id text>`.
- `TaleWorlds.CampaignSystem.Clan`: `[SaveableProperty(51)] public TextObject Name` :101-102. A clan name is
  saved, so an existing campaign keeps the key it saved.
- Dump `EventManager.cs:880` (`CollectEnableWidgetsAt`) skips disabled children, as the screen checklist
  ported in A10 says.

### Data facts (measured this session on `1b00d1acd`, the #697 troop tree and the laptop install, read only)

- `taom_module_strings.xml`: 2,657 rows; 313 non-`taom_` keys = 181 eight-character vanilla ids + 90 `aom_*`
  + 42 `TAOM_*`. v1.4.8 vanilla declares all 181, including `bjEN2OzZ` and `d6KbdIWg`
  (`SandBox/ModuleData/Languages/std_TaleWorlds_CampaignSystem.xml`), which 1.5.x deleted as gone: KEEP them.
- Language files: 110,904 rows in `std_taom_*.xml`; exactly 181 rows per language carry a vanilla id, all of
  them the 181 rewordings. `8a4027110`'s `kept-vanilla-key-rows.txt` has 369 lines (BR 171, DE 48, FR 25,
  IT 108, JP 2, KO 2, PL 9, RU 4); every line names a pair that exists here. Deleting the rest removes
  1,803 rows and 1,800 cache entries. Overrides holding a rewording key: `cns.json` `uc4M4bhG`, `ko.json`
  `qa4FlTWS`, `tr.json` `uiY3ds0Z` (none kept).
- English sources: the 13 files listed in A5. `taom_module_strings.xml` and `taom_xslt_strings.xml` share
  42 keys; `global_strings.xml` (8 keys) shares none.
- Name sources on the merged base: 19 files, 2,251 keys, 2,020 not yet registered; 1,989 have identical
  English in the 1.5.x name masters and an entry in the `e45f6df2c` de, ru and br caches. None is a
  vanilla key. One key has two English defaults: `aom_clan_umbar_1_name` (six clans). The Lindon key split
  of `4ceae903b` is already in the #697 tree.
- `LocalizationKeyConsistencyTests` gates, emulated: 819 C# sites; 9 mismatches (CombatLotrIssue :63,
  :64, :65, :285; DeliverPersonnelLotrIssue :64, :65, :310; Messenger :504, :636), all fixed by `4ceae903b`'s
  hunks; about 4,600 data sites (3,156 XML, 1,443 XSLT), below `4ceae903b`'s floor of 6,300; 2 data
  mismatches (cave troll master description; Duinhir's title); 0 conflicting rows in 12 languages.
- Writing-system rule (`4ceae903b` plus `9e2a39f40`'s "Other" class), emulated: 133 language rows (RU 56,
  TR 53, KO 15, JP 5, CNs 2, DE 1, SP 1) after skipping rows equal to their English; 151 cache values.
- The 52 rows `616cf12f1` retired on 1.5.x all exist in `taom_module_strings.xml` here. The four
  `taom_precompile_*` rows it allowlists are read here (`Main/SubModule.cs:645-661`); the parked Far Harad
  half-troll career rows exist (`taom_career_strings.xml:43-44, :146`).
- Laptop install today: `LOTRLOME_Armory` `v2.0.26` (WP101 replaces it), `TAOM_Map` `v2.0.27`; 25 live
  `language_data.xml` files carry `name` and no processor, 15 of them for languages where Native declares
  a processor (the list in step M2). BetterTime is not installed here.

### What earlier WPs must have landed (A0 checks each)

- WP104: `TAOM.Tests/Infrastructure/RepoPaths.cs` with `public static string RepoPath(params string[] parts)`.
- WP132: `113d3483b`'s `VAR_PATTERN`/`CONDITIONAL_OPEN` and `tools/tests/test_translate_placeholders.py`. If
  absent, A9 applies them first as an adopted hunk.
- WP123: maybe `Main/Features/LocalizationOverride/IdSlice.cs` (`74e9172d4`). A1 handles either key form.
- WP134: its Part C (decided under D13) lands `9dd2f594f`'s rows for 40 wanderers: 320 English rows per file in
  `taom_wanderer_strings.xml` and the 12 `std_taom_wanderer_strings_*.xml` (280 of them the required rows it
  reports missing), and this WP translates them in C1. That commit's `RegisteredEnglish`/skip-English hunks target code this WP creates, so WP134 never takes them and
  A8 does.
- WP128: maybe `Main/Features/Enlistment/Content/Domain/DutyText.cs` (`a20b4d154`); B3 reads the answer.

### Not on 1.4.x yet (`git grep` and `git ls-tree` on `origin/bannerlord-1.4.5`, this session)

`ITextLocalizerAdapter`, `OverrideLanguageGate`, `IdSlice`, `tools/_loc_sources.py`,
`generate_name_localization_strings.py`, `check_orphan_loc_rows.py`, `loc_length_report.py`,
`validate_xml_schemas.py`, `name_tags.py`, `LanguageProcessorTests`, `LocalizationKeyConsistencyTests`,
`VanillaKeyOverrideTests`, `VanillaLocalizationKeys`, `kept-vanilla-key-rows.txt`,
`test_translate_caps_and_seed.py`, `test_harvest_literal_loc_keys.py`. `VanillaKeyOverrideTests` and `LanguageProcessorTests` arrive with `[TestCategory("LiveInstall")]`: delete those lines as for
`RequiresGame` (D9); `git grep -c LiveInstall` over both files prints nothing after A2. The suite runs such tests; the laptop has the install.

### Conventions that bind this change

- ADR-007 adapters: services never touch sealed TaleWorlds types; `OverrideLanguageGate` reads the language
  through `ITextLocalizerAdapter`, and only `TextLocalizerAdapter` touches `MBTextManager`.
- ADR-002 thin entry points (under 150 lines): the Harmony prefix only asks the gate and the table.
- ADR-003 no `#region`; ADR-004 no `[Obsolete]`; ADR-005 no `#if DEBUG` outside the IoC registration.
- TDD: a failing test before each change (each step says how RED is shown).
- MCM json2 rule (rename a persisted setting, never flip its default): no setting changes here.
- `.claude/rules/harmony-patches.md`: no MovementOrder postfix here; Patch25 keeps its category name.
- Localization: `tools/translation_overrides/<lang>.json` always wins over the model; every translator run
  in this WP uses `--module TAOM` (any other module writes the live game install).

## Commands you will need

| Purpose | Command (run in the worktree) | Expected |
|---|---|---|
| Restore | `dotnet restore TAOM.sln` | exit 0 |
| Build | `dotnet build TAOM.sln -p:DisableModuleCopy=true -p:ModuleId=` | exit 0, 0 errors (BHA0001 on Patch88 is a known false positive) |
| Test | `dotnet test TAOM.Tests --no-build -p:DisableModuleCopy=true -p:ModuleId=` | `Failed: 0` |
| Filtered | `dotnet test TAOM.Tests --no-build -p:DisableModuleCopy=true -p:ModuleId= --filter FullyQualifiedName~<Name>` | `Failed: 0` |
| One tool test file | `python -m unittest discover -s tools/tests -t . -p "<file>.py"` | `OK` |
| Tool tests | `python -m unittest discover -s tools/tests -t .` | no new failure vs A0 |
| Data | `python tools/validate_moduledata.py` | no new ERROR vs the WP101 baseline |
| Docs | `python tools/lint_docs.py` | no new finding in touched files |
| Free seeding (after A7) | `python tools/translate_with_claude.py --lang <L> --module TAOM --sync-ids --seed-only --apply` | `Ids seeded: <n>`, no provider call |

Both `-p:` flags are mandatory on every build and test (`Main/TAOM.csproj:133-136` copies into the game
install otherwise). Never `./build.ps1`. `python` is `C:\Python314\python.exe`.

## Scope

**In scope**:
- Part A: `Main/Features/LocalizationOverride/Hooks/MBTextManager_GetLocalizedText_Patch.cs`,
  `Main/Features/LocalizationOverride/OverrideLanguageGate.cs` (new), `Main/Adapters/ITextLocalizerAdapter.cs`
  and `TextLocalizerAdapter.cs` (new), `TAOM.Tests/Features/LocalizationOverride/*`, the 13
  `language_data.xml` files, `Main/_Module/SubModule.xml` (two GameType lines),
  `TAOM.Tests/Infrastructure/Localization/{LanguageDataXmlTests,LanguageProcessorTests,LocalizationKeyConsistencyTests,LanguageTextIntegrityTests,LanguageFileCoverageTests}.cs`,
  `tools/{translate_with_claude,generate_translation_template,check_external_loc_coverage,harvest_literal_loc_keys,loc_length_report,_loc_sources}.py`,
  their tests under `tools/tests/`, `CombatLotrIssue.cs`, `DeliverPersonnelLotrIssue.cs`,
  `MessengerCampaignBehavior.cs`, `taom_lotr_issue_strings.xml`, `characters/clans.xml`, `lords.xslt`,
  `taom_career_strings.xml`, `taom_xslt_strings.xml`, rows in `Main/_Module/ModuleData/Languages/<L>/*.xml`,
  `tools/translation_cache/*.json`, `.claude/rules/gui-ui.md` (one mechanism line),
  `docs/reference/gauntlet-screen-checklist.md` (new), the two RCA files the picks add, and the docs named in
  each step (`docs/features/localization.md`, `docs/features/localization-override.md`,
  `docs/localization/TRANSLATOR_GUIDE.md`, `docs/reference/harmony-patch-registry.md`,
  `docs/reference/localization-map.md`, `docs/modding/strings-and-localization.md`).
- Part B: `tools/generate_name_localization_strings.py` and the four masters it writes,
  `tools/{check_orphan_loc_rows.py,loc_composed_families.json,loc_orphan_allowlist.json,rebuild_translation_files.py}`,
  `TAOM.Tests/Infrastructure/Localization/{VanillaKeyOverrideTests,VanillaLocalizationKeys}.cs`,
  `kept-vanilla-key-rows.txt`, `TAOM.Tests/Features/LocalizationOverride/LocalizationOverrideLoaderTests.cs`,
  48 new `std_taom_{troop,lord,clan,kingdom}_name_strings_<locale>.xml`, `tools/translation_overrides/{cns,ko,tr}.json`,
  `taom_module_strings.xml` (orphan rows), tests under `tools/tests/`.
- Part C: rows in the language files and the caches only. Part D: see D1.

**Single-owner files**: `Main/IoC.cs`, `Main/SubModule.cs`, `Main/TAOM.csproj` are NOT touched (the override
feature is static-only; the new adapter needs no registration).

**Out of scope**: the game install (read only, always); `generate_translation_template.py --apply`;
any translator run with `--module` other than `TAOM`; the bookkeeping files (`CHANGELOG.md`, `CLAUDE.md`,
`AGENTS.md`, `docs/INDEX.md`, `docs/reference/feature-map.md`, `tools/README.md`, `docs/reviews/lessons/*`,
`.ai/*`, `.claude/skills/*`; WPs 142 and 143 sync them, see Maintenance notes); the five 1.5.x data-text
name families (culture text, hero text, career data, character names, battle scenes); BanditVoices,
TournamentRewards, TrollBruteForce, hill troll content; Portuguese name tags.

## Git workflow

- Worktree (create once; reuse it if it exists):
  `git -C /c/Users/mikew/source/repos/TAOM worktree add /c/Users/mikew/source/repos/taom-port/135 -b port148/135-localization-fixes bannerlord-1.4.5`
  Work only there (`WT=/c/Users/mikew/source/repos/taom-port/135` below). At most four executor worktrees
  at once; never two builds in one worktree.
- Git only through the Bash tool (D19). Stage explicit paths; never `git add -A`, `git add .`,
  `git commit -a`. Never stash, `reset --hard`, pull, push, tag, open a PR or merge into `bannerlord-1.4.5`.
- Per-port procedure (binding): a pick is `git cherry-pick -n <sha>`; never `--continue`, `-C`, `-c` or
  `--amend --no-edit` (`GIT_EDITOR=true` here would commit the 1.5.x message). A path-limited port is
  `git diff <sha>^ <sha> -- <paths> | git apply --3way`; it stages what applies and leaves conflict
  markers to resolve. A bookkeeping path the pick changed or conflicted goes back with
  `git checkout HEAD <path>`; a bookkeeping file the pick added goes with `git rm -q -f <path>`. If the
  `block-dangerous-git` hook asks about these, approve: they discard only the pick's own output in this
  worktree. After a commit, if `git status` still reports a cherry-pick, run `git cherry-pick --quit`.
- Commit: write the message with the Write tool to `C:\Users\mikew\source\repos\taom-port\msg-135-<n>.txt`,
  stage with explicit `git add <paths>`, then run `git commit -F C:/Users/mikew/source/repos/taom-port/msg-135-<n>.txt`
  in the NEXT sequential Bash call, never the same command, never PowerShell.
- Message template: subject `<type>(<scope>): v2.0.28 - <description>` (at most 72 characters); body wrapped
  at 72, written for 1.4.x readers, no em or en dash, no Co-Authored-By or "Generated with" line; trailers
  `Research:`, `Not-tested:`, optional `Save-compat:`/`Rejected:`, and one `Ported-from: <40-hex>` line per
  included 1.5.x commit. Each step gives its subject, body points and trailers.
- Build and filtered tests after each commit; the full suite and the tool suite at the end of each Part.
- Shell variables do not survive between Bash calls: write `$WT`, `$H` and `$BASE` (recorded in A0) out as
  literal values in every command. Do not set `MSYS_NO_PATHCONV`: with it, Windows Python cannot open a
  `/c/Users/...` path (no command in this plan needs it).

## The helper script (create it once in A0; never commit it)

Some steps edit hundreds of rows by rule. Create this file with the **Write tool** (a Bash heredoc may
collapse doubled backslashes) at `C:\Users\mikew\source\repos\taom-port\h135\h135.py`, and run it from the
worktree root as `python /c/Users/mikew/source/repos/taom-port/h135/h135.py <command>` (`$H` below). It
refuses to write any XML that does not parse, keeps each line's own terminator, and writes caches in
`translate_with_claude.save_cache`'s format.

```python
"""WP135 helper (not committed). Run from the worktree root: python h135.py <command>.
stubs          create the empty troop, lord, clan and kingdom name files in every language folder
ws [KEY ...]   reset rows that mix writing systems (LanguageTextIntegrityTests rule) and every row of each
               KEY to their registered English; drop their cache entries and every cache value that mixes
vanilla        drop language rows and cache entries of the English-only vanilla rewordings (8-character
               keys of taom_module_strings.xml) except the pairs in kept-vanilla-key-rows.txt
orphans FILE   FILE holds check_orphan_loc_rows.py output: UNREAD rows leave the English files, the 12
               language files and the caches; a SHADOWED row leaves the English files only
cacheseed      add a 1.5.x cache entry for each key whose English is identical on both lines (never overwrites)
"""
import html
import json
import re
import subprocess
import sys
import unicodedata
import xml.etree.ElementTree as ET
from pathlib import Path

ROOT = Path.cwd()
MD = ROOT / "Main" / "_Module" / "ModuleData"
LANG = MD / "Languages"
sys.path.insert(0, str(ROOT / "tools"))
from _loc_sources import TAOM_SOURCES  # noqa: E402

CODES = {"BR": "br", "CNs": "cns", "CNt": "cnt", "DE": "de", "FR": "fr", "IT": "it",
         "JP": "jp", "KO": "ko", "PL": "pl", "RU": "ru", "SP": "sp", "TR": "tr"}
KEY = re.compile(r"^\{=([^}]+)\}")
ROW_ID = re.compile(r'<string\s+id="([^"]+)"')
TEXT = re.compile(r'(\stext=")([^"]*)(")')

def read_lines(path):
    raw = path.read_bytes().decode("utf-8")
    parts = [p for p in re.findall(r"[^\r\n]*(?:\r*\n|$)", raw) if p]
    if "".join(parts) != raw:
        sys.exit(f"{path}: a line ending this helper does not model; nothing written")
    return parts

def write_lines(path, parts):
    text = "".join(parts)
    ET.fromstring(text.lstrip("\ufeff").encode("utf-8"))
    path.write_bytes(text.encode("utf-8"))

def row_id(line):
    m = ROW_ID.search(line)
    return m.group(1) if m else None

def esc(s):
    return s.replace("&", "&amp;").replace('"', "&quot;").replace("<", "&lt;").replace(">", "&gt;")

def english_of(data):
    out = {}
    for el in ET.fromstring(data).iter("string"):
        t = el.get("text", "") or ""
        m = KEY.match(t)
        if m and m.group(1) not in ("!", "*"):
            out.setdefault(m.group(1), t[m.end():])
    return out

def english():
    out = {}
    for src, _ in TAOM_SOURCES:
        if (MD / src).exists():
            for k, v in english_of((MD / src).read_bytes()).items():
                out.setdefault(k, v)
    return out

def cache_file(code):
    return ROOT / "tools" / "translation_cache" / f"{code}.json"

def load_cache(code):
    return json.loads(cache_file(code).read_text(encoding="utf-8"))

def save_cache(code, cache):
    with open(cache_file(code), "w", encoding="utf-8") as fh:
        json.dump(cache, fh, ensure_ascii=False, indent=2, sort_keys=True)

def lang_files(lang):
    return sorted((LANG / lang).glob("std_taom_*.xml"))

def drop_rows(lang, ids):
    removed = 0
    for f in lang_files(lang):
        parts = read_lines(f)
        keep = [p for p in parts if row_id(p) not in ids]
        if len(keep) != len(parts):
            write_lines(f, keep)
            removed += len(parts) - len(keep)
    return removed

def drop_cache(code, keys, also=lambda k, v: False):
    cache = load_cache(code)
    gone = [k for k, v in cache.items() if k in keys or also(k, v)]
    for k in gone:
        del cache[k]
    save_cache(code, cache)
    return len(gone)

LATIN_LANGS = {"BR", "DE", "FR", "IT", "PL", "SP", "TR"}
FOREIGN = {"RU": ["Han", "Kana", "Hangul", "Other"], "KO": ["Kana", "Cyrillic", "Other"],
           "JP": ["Hangul", "Cyrillic", "Other"], "CNs": ["Hangul", "Kana", "Cyrillic", "Other"],
           "CNt": ["Hangul", "Kana", "Cyrillic", "Other"]}
LATIN_ALLOWED = ["Ctrl", "Shift", "Alt", "TAOM", "Bannerlord", "NativeSkinFixes", "covers_head", "Discord",
                 "Steam", "MCM", "Harmony", "Patreon", "Nexus", "Workshop", "Modding", "Kit"]
MARKUP = re.compile(r"\{[^}]*\}|<[^>]*>|\[[a-z]{2}:[^\]]*\]|^\[[^\]]*\]")
WORD = re.compile(r"[\w-]+")
LATIN_RUN = re.compile("[A-Za-z\u00c0-\u024f]+")

def script(c):
    o = ord(c)
    if 0x0400 <= o <= 0x04FF:
        return "Cyrillic"
    if 0xAC00 <= o <= 0xD7AF or 0x1100 <= o <= 0x11FF or 0x3130 <= o <= 0x318F:
        return "Hangul"
    if 0x3040 <= o <= 0x30FF or 0x31F0 <= o <= 0x31FF:
        return "Kana"
    if 0x4E00 <= o <= 0x9FFF or 0x3400 <= o <= 0x4DBF or 0xF900 <= o <= 0xFAFF:
        return "Han"
    if c.isalpha() and (o < 0x0250 or 0x1E00 <= o <= 0x1EFF):
        return "Latin"
    if c.isalpha() and not 0xFF00 <= o <= 0xFFEF and unicodedata.category(c) != "Lm":
        return "Other"
    return None

def scripts(s):
    return {x for x in map(script, s) if x}

def mixes(lang, text):
    body = MARKUP.sub(" ", text or "").replace("\u30fb", " ")
    found = scripts(body)
    if lang in LATIN_LANGS:
        return bool(found - {"Latin"})
    if lang not in FOREIGN:
        return False
    if any(s in found for s in FOREIGN[lang]):
        return True
    if "Latin" not in found:
        return False
    for token in WORD.findall(body):
        if any(a in token for a in LATIN_ALLOWED):
            continue
        ts = scripts(token)
        english_word = ts == {"Latin"} and len(token) >= 4 and token == token.lower()
        if lang == "RU":
            if ("Latin" in ts and "Cyrillic" in ts) or english_word:
                return True
            continue
        split = len(ts) > 1 and any(
            len(scripts(part)) > 1 and any(len(r) >= 2 and r[0].islower() for r in LATIN_RUN.findall(part))
            for part in token.split("-"))
        if split or english_word:
            return True
    return False

def cmd_stubs():
    for lang in CODES:
        folder = LANG / lang
        sibling = next(folder.glob("std_taom_player_switcher_strings_*.xml"))
        locale = sibling.stem.rsplit("_", 1)[1]
        head = []
        for line in read_lines(sibling):
            head.append(line)
            if "<strings>" in line:
                break
        nl = head[-1][len(head[-1].rstrip("\r\n")):]
        indent = re.match(r"\s*", head[-1]).group(0)
        for kind in ("troop", "lord", "clan", "kingdom"):
            out = folder / f"std_taom_{kind}_name_strings_{locale}.xml"
            if out.exists():
                print(f"{lang}: {out.name} exists, left alone")
                continue
            write_lines(out, head + [f"{indent}</strings>{nl}", f"</base>{nl}"])
            print(f"{lang}: created {out.name}")

def cmd_ws(extra):
    eng = english()
    total_rows = total_cache = 0
    for lang, code in CODES.items():
        reset = set()
        for f in lang_files(lang):
            parts, changed = read_lines(f), False
            for i, line in enumerate(parts):
                rid = row_id(line)
                m = TEXT.search(line) if rid else None
                if not m:
                    continue
                text = html.unescape(m.group(2))
                if rid not in extra and (eng.get(rid) == text or not mixes(lang, text)):
                    continue
                if rid not in eng:
                    print(f"NO ENGLISH, fix by hand: {lang}/{f.name} [{rid}]")
                    continue
                parts[i] = line[:m.start(2)] + esc(eng[rid]) + line[m.end(2):]
                reset.add(rid)
                changed = True
            if changed:
                write_lines(f, parts)
        n = drop_cache(code, reset, lambda k, v, lang=lang: mixes(lang, v))
        total_rows, total_cache = total_rows + len(reset), total_cache + n
        print(f"{lang}: {len(reset)} keys reset, {n} cache entries dropped")
    print(f"total: {total_rows} keys reset, {total_cache} cache entries dropped")

def cmd_vanilla():
    shape = re.compile("[0-9A-Za-z]{8}")
    rewords = {k for k in english_of((MD / "taom_module_strings.xml").read_bytes()) if shape.fullmatch(k)}
    kept = set()
    kept_file = ROOT / "TAOM.Tests" / "Infrastructure" / "Localization" / "kept-vanilla-key-rows.txt"
    for line in kept_file.read_text(encoding="utf-8").splitlines():
        if line.strip() and not line.startswith("#"):
            lang, key, _reason = line.split()
            kept.add((lang, key))
    rows = entries = 0
    for lang, code in CODES.items():
        drop = {k for k in rewords if (lang, k) not in kept}
        n, c = drop_rows(lang, drop), drop_cache(code, drop)
        rows, entries = rows + n, entries + c
        print(f"{lang}: {n} rows, {c} cache entries removed")
    print(f"total: {rows} rows, {entries} cache entries removed; rewordings: {len(rewords)}")

def cmd_orphans(path):
    unread, shadowed = set(), set()
    for line in Path(path).read_text(encoding="utf-8").splitlines():
        m = re.match(r"UNREAD (\S+) \(", line)
        if m:
            unread.add(m.group(1))
        m = re.match(r"SHADOWED (\S+):", line)
        if m:
            shadowed.add(m.group(1))
    for src, _ in TAOM_SOURCES:
        p = MD / src
        if not p.exists():
            continue
        parts = read_lines(p)
        at = {}
        for i, x in enumerate(parts):
            if row_id(x) in shadowed:
                at.setdefault(row_id(x), []).append(i)
        drop_at = {i for idx in at.values() for i in (idx if len(idx) == 1 else idx[:-1])}
        keep = [x for i, x in enumerate(parts) if row_id(x) not in unread and i not in drop_at]
        if len(keep) != len(parts):
            write_lines(p, keep)
            print(f"{src}: {len(parts) - len(keep)} rows removed")
    for lang, code in CODES.items():
        print(f"{lang}: {drop_rows(lang, unread)} rows, {drop_cache(code, unread)} cache entries removed")

def git_bytes(ref, path):
    r = subprocess.run(["git", "show", f"{ref}:{path}"], capture_output=True)
    return r.stdout if r.returncode == 0 else None

def english_at(ref):
    table = git_bytes(ref, "tools/_loc_sources.py").decode("utf-8")
    out = {}
    for src in re.findall(r'^\s*\("([^"]+\.xml)",', table, re.M):
        data = git_bytes(ref, f"Main/_Module/ModuleData/{src}")
        if data:
            for k, v in english_of(data).items():
                out.setdefault(k, v)
    return out

def cmd_cacheseed():
    here, there, total = english(), {}, 0
    refs = {"BR": "41aa2779b", "IT": "41aa2779b"}
    for lang, code in CODES.items():
        ref = refs.get(lang, "e45f6df2c")
        if ref not in there:
            there[ref] = english_at(ref)
        theirs = json.loads(git_bytes(ref, f"tools/translation_cache/{code}.json").decode("utf-8"))
        cache = load_cache(code)
        added = [k for k, v in here.items() if k not in cache and k in theirs and there[ref].get(k) == v]
        for k in added:
            cache[k] = theirs[k]
        save_cache(code, cache)
        total += len(added)
        print(f"{lang} (from {ref}): {len(added)} entries added")
    print(f"total: {total} entries added")

if __name__ == "__main__":
    cmd, args = sys.argv[1], sys.argv[2:]
    {"stubs": cmd_stubs, "ws": lambda: cmd_ws(set(args)), "vanilla": cmd_vanilla,
     "orphans": lambda: cmd_orphans(args[0]), "cacheseed": cmd_cacheseed}[cmd]()
```

`41aa2779b` is the parent of `fd1cee10b`, the last 1.5.x state before the Portuguese and Italian name
articles were edited in the caches; the other ten languages read the `e45f6df2c` caches.

## Steps

### Part A: code and tool fixes (no paid call; one session)

#### A0: Preflight

1. Create the worktree (Git workflow), then
   `cd $WT && git status --porcelain | wc -l && git rev-parse HEAD && echo "$BANNERLORD_GAME_DIR"`.
   Expected `0`, a sha (record it as `BASE`), the game path. An empty game path is an environment failure:
   STOP and report; do not set it yourself.
2. Preconditions (one Bash call, in `$WT`):
   ```bash
   grep -c 'public static string RepoPath(params string\[\] parts)' TAOM.Tests/Infrastructure/RepoPaths.cs; \
   test -f Main/Features/LocalizationOverride/IdSlice.cs && echo KEY=IDSLICE || echo KEY=STRING; \
   grep -cF 'CONDITIONAL_OPEN = re.compile(r"\{\?[^{}?]+\}")' tools/translate_with_claude.py; \
   test -f tools/tests/test_translate_placeholders.py && echo PLACEHOLDER-TESTS || echo NO-PLACEHOLDER-TESTS; \
   test -f Main/Features/Enlistment/Content/Domain/DutyText.cs && echo DUTYTEXT || echo NO-DUTYTEXT; \
   grep -o '<Version value="[^"]*"' "$BANNERLORD_GAME_DIR/Modules/LOTRLOME_Armory/SubModule.xml"; \
   ls Main/Features/LocalizationOverride/OverrideLanguageGate.cs Main/Adapters/ITextLocalizerAdapter.cs tools/_loc_sources.py \
      tools/generate_name_localization_strings.py tools/check_orphan_loc_rows.py tools/loc_length_report.py \
      TAOM.Tests/Infrastructure/Localization/LocalizationKeyConsistencyTests.cs \
      TAOM.Tests/Infrastructure/Localization/VanillaKeyOverrideTests.cs tools/tests/test_translate_caps_and_seed.py 2>&1 | grep -c "No such file"; \
   grep -c "RegisteredEnglish" TAOM.Tests/Infrastructure/Localization/LanguageTextIntegrityTests.cs; \
   grep -c 'name=' Main/_Module/ModuleData/Languages/language_data.xml; \
   grep -c 'backstory_a.spc_wanderer_goblin_0"' Main/_Module/ModuleData/taom_wanderer_strings.xml
   ```
   Expected in order: `1`; `KEY=IDSLICE` or `KEY=STRING` (write it down for A1); `1` or `0` and
   `PLACEHOLDER-TESTS` or `NO-PLACEHOLDER-TESTS` (write both down for A9); `DUTYTEXT` or `NO-DUTYTEXT` (for
   B3); an Armory version that is NOT `v2.0.26`; `9`; `0`; `0`; `1`. A missing RepoPaths, the `v2.0.26` Armory,
   fewer than 9 absent files, a non-zero count in the two after them, or `0` in the last (WP134 Part C has not
   landed) is a STOP.
3. Run the drift check (top of the file) and compare with the excerpts. STOP on a real mismatch.
4. Baseline: Restore, Build, Test. Record `Total`, `Passed`, `Skipped` (expected: the count the previous
   merged WP recorded in the main checkout's `C:\Users\mikew\source\repos\TAOM\plans\README.md`, read only,
   0 failed). Run the tool suite and record its failures, if any.
5. Create `C:\Users\mikew\source\repos\taom-port\h135\h135.py` with the Write tool (content above), then
   `python -m py_compile /c/Users/mikew/source/repos/taom-port/h135/h135.py && echo HELPER-OK` -> `HELPER-OK`.
   It imports `tools/_loc_sources.py`, which A5 creates, so it is first run in A8.

#### A1: English overrides only while the text language is English (`407b1e767`, pick, patch resolved by hand)

1. `cd $WT && git cherry-pick -n 407b1e767; git status --porcelain`. Expected conflicts on `1b00d1acd`:
   the patch file, its test file, `docs/features/localization-override.md`, `docs/features/localization.md`,
   `docs/localization/TRANSLATOR_GUIDE.md`. Clean: `OverrideLanguageGate.cs` and `OverrideLanguageGateTests.cs`
   (new), `docs/reference/harmony-patch-registry.md`, `docs/INDEX.md`. Run `git checkout HEAD docs/INDEX.md`.
2. Resolve `MBTextManager_GetLocalizedText_Patch.cs` to this shape (keep any other line the live file has;
   with `KEY=IDSLICE` the pick may apply this file and its tests cleanly: then check they already match):
   - usings: add `using TAOM.Adapters;` and `using TAOM.Core.Logging;` (with `KEY=IDSLICE` also keep
     `using System;`).
   - summary: replace "making the ~120 "The" fixes in taom_module_strings.xml actually take effect." with
     the 1.5.x sentence "making the overrides in taom_module_strings.xml actually take effect. It does that
     only while the text language is English (#706): for any other language vanilla reads that language's
     own row, so the prefix falls through and the player's translation shows."
   - after the `_overrides` field add:
     ```csharp
         // Decides English or not, and writes the once-per-language-change log line. Replaced only by tests.
         private static OverrideLanguageGate _languageGate = NewGameGate();
     ```
   - in `Prefix`, directly after the `if (idLength == 1 && (text[2] == '!' || text[2] == '*')) return true;`
     block and BEFORE the id is built (`KEY=STRING`: before `string id = text.Substring(2, idLength);`;
     `KEY=IDSLICE`: before the `TryGetValue(new IdSlice(...))` line):
     ```csharp
             // Only English skips vanilla's dictionary; every other language has a row of its own to show.
             if (!_languageGate.AllowsOverrides())
                 return true;
     ```
   - keep `RegisterOverride` and `ClearOverrides` exactly as the live file has them (string key or IdSlice),
     and append before the class's closing brace:
     ```csharp
         // This feature is static-only and has no IoC registration, so the gate is built here. The logger is
         // resolved inside the lambda, on the rare path that writes a line, never per lookup.
         private static OverrideLanguageGate NewGameGate()
             => new(new TextLocalizerAdapter(), static () => IoC.Resolve<IModLogger>());

         // Test seam: installs a gate over a fake language; null puts back the one the game uses.
         internal static void UseLanguageGate(OverrideLanguageGate? gate)
         {
             _languageGate = gate ?? NewGameGate();
         }
     ```
3. Resolve `MBTextManager_GetLocalizedText_PatchTests.cs`: keep every test the live file has; add from
   `git show 407b1e767 -- TAOM.Tests/Features/LocalizationOverride/MBTextManager_GetLocalizedText_PatchTests.cs`
   the usings `NSubstitute`, `TAOM.Adapters`, `TAOM.Core.Logging`, `TAOM.Features.LocalizationOverride`, the
   `_localizer` field, the three Setup lines, the Cleanup method and the four `#706` tests. On
   `Prefix_GameGate_EngineLanguageIsEnglish_ReturnsTheOverride` delete the line
   `[TestCategory("RequiresGame")]` (D9), and in its comment change "(v1.5.3 MBTextManager.cs:36). Needs the
   engine, so not on hosted CI." to "(v1.4.8 MBTextManager.cs:36). Needs the engine DLLs.". In
   `OverrideLanguageGate.cs` change "v1.5.3 <c>MBTextManager.cs:264</c>" to "v1.4.8 <c>MBTextManager.cs:264</c>".
4. RED: Build. Expected: errors CS0246 naming `ITextLocalizerAdapter` and `TextLocalizerAdapter`.
5. Create the adapter pair (1.5.x has a second member, `Localize`, used only by a 1.5.x feature; it is left
   out):
   ```csharp
   // Main/Adapters/ITextLocalizerAdapter.cs
   namespace TAOM.Adapters;

   /// <summary>The game's text language, read through an adapter so no service touches <c>MBTextManager</c> (ADR-007).</summary>
   public interface ITextLocalizerAdapter
   {
       /// <summary>The game's text language now; it can change while the game runs.</summary>
       string ActiveLanguage { get; }
   }
   ```
   ```csharp
   // Main/Adapters/TextLocalizerAdapter.cs
   using TaleWorlds.Localization;

   namespace TAOM.Adapters;

   /// <summary><see cref="ITextLocalizerAdapter"/> over <see cref="MBTextManager"/>.</summary>
   public sealed class TextLocalizerAdapter : ITextLocalizerAdapter
   {
       public string ActiveLanguage => MBTextManager.ActiveTextLanguage;
   }
   ```
6. GREEN: Build, then the filtered tests `MBTextManager_GetLocalizedText_PatchTests` and
   `OverrideLanguageGateTests`: `Failed: 0` (4 more tests than before in the first class;
   `Prefix_GameGate_EngineLanguageIsEnglish_ReturnsTheOverride` runs here because the laptop has the DLLs; if it
   throws `TypeInitializationException`, STOP).
7. Docs: in `localization-override.md` take the `407b1e767` side of each conflict, then correct for 1.4.x:
   the counts are right here (313 = 181 + 90 + 42, measured this session); drop every `IdSlice` mention
   unless `KEY=IDSLICE`; replace `TryPatchCategory(...)`/`SubModule.cs:275-289` with what the live
   `Main/SubModule.cs` has (`grep -n Patch25_LocalizationOverride Main/SubModule.cs`); the test counts are
   the live counts (`grep -c "\[TestMethod\]"` per file); "(the real adapter, `RequiresGame`)" becomes "(the real
   adapter)" (D9). In `localization.md` and `TRANSLATOR_GUIDE.md` keep
   HEAD and add the one changed sentence of the `407b1e767` hunk ("applies ... only while the game language is
   English"). Keep the clean registry line.
8. Commit (`msg-135-1`): `fix(localization): v2.0.28 - English overrides only for English`. Body: the override
   table answered in every language, so non-English players saw English for 313 strings that have rows in all
   twelve languages; it now applies only while the text language is English; one INFO line per switch away
   from and back to English in taom_debug.log; the adapter is new. Trailers:
   `Research: taom-src MBTextManager.GetLocalizedText (:232, English branch :264), ActiveTextLanguage (:52)`,
   `Not-tested: in game, German shows an aom_ notable's German row; switching to English logs the line`,
   `Ported-from: 407b1e767546ae7f8b35436ffd59c4bba59447c4`.

#### A2: Grammar processors run again (`41aa2779b`, pick, one import adapted, one test left out)

1. RED, before the pick: `git show 41aa2779b:TAOM.Tests/Infrastructure/Localization/LanguageProcessorTests.cs > TAOM.Tests/Infrastructure/Localization/LanguageProcessorTests.cs`,
   delete its third test method (`LiveTaomMapAndArmoryLanguageData_InstalledNative_CarryTheSameProcessor`,
   the `[TestMethod]` at :113 through its closing brace at :138), Build, run the filter `LanguageProcessorTests`.
   Expected: `TaomLanguageDataFiles_PinnedNativeTable_DeclareTheSameProcessor` and
   `TaomLanguageDataFiles_InstalledNative_DeclareTheSameNameAndProcessor` fail on `Languages`. Then
   `rm TAOM.Tests/Infrastructure/Localization/LanguageProcessorTests.cs`.
2. `git cherry-pick -n 41aa2779b`. Expected conflicts (bookkeeping): `.claude/skills/release/SKILL.md`,
   `docs/INDEX.md`, `docs/reference/lotrlome-armory-snapshot/README.md`, `docs/reference/release-process.md`,
   `docs/reviews/lessons/build-tooling-workflow.md`, `docs/reviews/lessons/localization-ui.md`,
   `tools/README.md`: `git checkout HEAD <each>`. Conflict `docs/features/localization.md`: keep HEAD and
   add the `41aa2779b` paragraph about the name and processor attributes. Clean and kept: the 8
   `language_data.xml`, `LanguageProcessorTests.cs`, `tools/check_external_loc_coverage.py`,
   `tools/tests/test_check_external_loc_coverage.py`, `docs/modding/strings-and-localization.md`,
   `docs/reviews/rca-text-processors-2026-10-26.md`.
3. Delete the same third test method again from the picked `LanguageProcessorTests.cs` (it reads the live
   TAOM_Map and Armory files, which only the maintainer may fix; until then it would fail on every machine;
   the tool's `--processors-only` covers them, see In-game checks).
4. In `tools/check_external_loc_coverage.py` replace the line
   `from validate_xml_schemas import _parse_like_engine   # noqa: E402` (that tool is not on 1.4.x) with this
   local copy (verbatim from `41aa2779b:tools/validate_xml_schemas.py:165-194`, comments shortened):
   ```python
   # The reader of LocalizedTextManager.LoadXmlFile (v1.4.8 :202-219): a StreamReader, so a byte order mark picks the
   # encoding, anything else is UTF-8, and the declared encoding is ignored. Copied from 1.5.x validate_xml_schemas.py.
   _BOMS = ((b"\xff\xfe\x00\x00", "utf-32-le"), (b"\x00\x00\xfe\xff", "utf-32-be"),
            (b"\xef\xbb\xbf", "utf-8"), (b"\xff\xfe", "utf-16-le"), (b"\xfe\xff", "utf-16-be"))


   def _read_like_engine(path) -> str:
       data = Path(path).read_bytes()
       for bom, codec in _BOMS:
           if data.startswith(bom):
               return data[len(bom):].decode(codec, errors="replace")
       return data.decode("utf-8", errors="replace")


   def _parse_like_engine(path):
       text = _read_like_engine(path)
       if "\x00" in text:
           raise ET.ParseError("the text contains a NUL character (a BOM-less UTF-16 file reads as UTF-8 "
                               "with a NUL after every character)")
       return ET.fromstring(text)
   ```
5. GREEN: `python -m unittest discover -s tools/tests -t . -p "test_check_external_loc_coverage.py"` -> `OK`;
   Build; filter `LanguageProcessorTests` -> 2 passed; filter `LanguageDataXmlTests` -> `Failed: 0`;
   `grep -c 'text_processor=' Main/_Module/ModuleData/Languages/language_data.xml Main/_Module/ModuleData/Languages/*/language_data.xml | grep -c ':1$'` -> `8`.
   Do NOT run `check_external_loc_coverage.py` itself here: it reads the live modules and exits 1 until the
   maintainer's edit (expected, not this step's gate).
6. Commit (`msg-135-2`): `fix(localization): v2.0.28 - grammar processors run again`. Body: a language file
   that names a language but declares no processor resets it to one that prints no grammar; TAOM's files now
   carry Native's name and processor for the eight processor languages and restore them when TAOM loads after
   TAOM_Map and the Armory; the coverage tool gains the processor check and `--processors-only` for a release
   channel; the live TAOM_Map and Armory files still need the maintainer's edit. Trailers:
   `Research: taom-src LanguageData.Deserialize (:107-111), LocalizedTextManager.CreateTextProcessorForLanguage (:53-67); dump LocalizedTextManager.cs:86-115, Module.cs:294-301`,
   `Not-tested: in game, English party names read "<Name>'s Party"; German and Turkish lines keep their article and suffix`,
   `Ported-from: 41aa2779bf2b9f6c715775b303fc8894f9538a91`.

#### A3: TAOM's module strings load in Custom Battle and the editor (`e74ef3931`, partial, by hand)

The doctrine half and `DoctrinePopupStringsTests` are 1.5.x-only, so this is a hand port of two lines.
1. RED: append to `TAOM.Tests/Infrastructure/Localization/LanguageDataXmlTests.cs` (inside the class):
   ```csharp
       [TestMethod]
       public void ModuleStringsGameText_IsLoadedInCustomBattleAndTheEditorToo()
       {
           // GameTextManager.LoadGameTexts (v1.4.8 GameTextManager.cs:107-117) merges the GameText files of the running
           // game type only, so a file registered for Campaign alone is absent in Custom Battle and the editor.
           var subModule = XDocument.Load(Path.Combine(ModuleDataPath, "..", "SubModule.xml"));
           var node = subModule.Descendants("XmlNode").Single(n =>
               (string?)n.Element("XmlName")?.Attribute("id") == "GameText"
               && (string?)n.Element("XmlName")?.Attribute("path") == "taom_module_strings");
           var types = node.Element("IncludedGameTypes")?.Elements("GameType")
               .Select(g => ((string?)g.Attribute("value") ?? "").Trim()).ToList() ?? new List<string>();
           foreach (var required in new[] { "Campaign", "CampaignStoryMode", "CustomGame", "EditorGame" })
               CollectionAssert.Contains(types, required, "taom_module_strings.xml is not loaded under " + required);
       }
   ```
   Build; filter `ModuleStringsGameText_IsLoadedInCustomBattleAndTheEditorToo` -> 1 failed (CustomGame).
2. In `Main/_Module/SubModule.xml`, inside the `taom_module_strings` GameText node (excerpt above), add
   `<GameType value="CustomGame"/>` and `<GameType value="EditorGame"/>` after `CampaignStoryMode`, same indent.
3. GREEN: the same filter -> passed; filter `LanguageDataXmlTests` -> `Failed: 0`.
4. Commit (`msg-135-3`): `fix(localization): v2.0.28 - module strings load in Custom Battle`. Body: the
   GameText file was registered for Campaign and CampaignStoryMode only, so its 2,657 rows (the str_ culture
   texts among them) were absent in Custom Battle and the editor; no current FindText id is affected, so this
   is groundwork. Trailers: `Research: dump GameTextManager.cs:107-117`,
   `Not-tested: Custom Battle shows no "Text with id" error for a TAOM culture text`,
   `Ported-from: e74ef39315b7ae787bb847b03924a7f83f0a89cb`.

#### A4: The string harvest stops adding blank lines (`337228885`, pick)

1. RED: `git show 337228885:tools/tests/test_harvest_literal_loc_keys.py > tools/tests/test_harvest_literal_loc_keys.py`;
   run that file -> failures (the 1.4.x tool equals the fix's parent); `rm tools/tests/test_harvest_literal_loc_keys.py`.
2. `git cherry-pick -n 337228885`. Expected conflict: `docs/reviews/lessons/build-tooling-workflow.md` ->
   `git checkout HEAD docs/reviews/lessons/build-tooling-workflow.md`. Kept: the tool, the test, the RCA.
3. GREEN: the test file -> `OK`.
4. Commit (`msg-135-4`): `fix(tools): v2.0.28 - string harvest stops adding blank lines`. Body: each harvest
   that wrote rows left one more blank line before `</strings>`; the block now replaces the blank lines.
   Trailers: `Not-tested: none (tool only)`, `Ported-from: 337228885b0725f1a2b0c657e5106c7e0090fe6d`.

#### A5: One table of English sources (`9e2a39f40`, partial; template crash fix of `303bf2a7d`)

1. RED: create `tools/tests/test_loc_sources.py` from `git show 9e2a39f40:tools/tests/test_loc_sources.py`
   WITHOUT the line `import generate_name_localization_strings as gen  # noqa: E402` and WITHOUT the test
   `test_EveryGeneratorOutput_IsATableRow` (both return in B1); in `test_Table_ModuleStringsAheadOfGlobalStrings`
   change the comment to "key_owners gives a shared key to the first source". Run -> error, no `_loc_sources`.
2. Create `tools/_loc_sources.py`: the module docstring of `9e2a39f40:tools/_loc_sources.py` with its ORDER
   paragraph rewritten for 1.4.x ("translate_with_claude.key_owners gives a key two sources declare to the FIRST
   one; taom_module_strings.xml and taom_xslt_strings.xml share 42 keys."), and `TAOM_SOURCES` holding the 13
   rows of the live `english_source_files` list in its order (module, global/keybind, wanderer,
   named_companions/named_companion, cc, career, messenger, xslt, wotr, lotr_issue, emissary, enlistment,
   player_switcher). If an earlier WP added a `*_strings.xml` file, `test_loc_sources` names it: add its row
   with its language-file template.
3. `tools/translate_with_claude.py`: add `from _loc_sources import TAOM_SOURCES` below the `_gamedir` import;
   in `english_source_files` replace the literal list with
   `# The ordered table lives in tools/_loc_sources.py (order decides key_owners).` and
   `for src_name, tgt_template in TAOM_SOURCES:` (body unchanged).
4. `tools/generate_translation_template.py`: add `import sys`, the UTF-8 stdout block of `303bf2a7d` (the
   `for _stream_name in ("stdout", "stderr"):` loop and its comment), `from _loc_sources import TAOM_SOURCES`,
   and replace `SOURCES = [...]` with `9e2a39f40`'s `NOT_TEMPLATED` set (without `taom_bandit_voice_strings.xml`)
   and the comprehension `SOURCES = [(TAOM_BASE / src, tpl, Path(src).stem) for src, tpl in TAOM_SOURCES if src not in NOT_TEMPLATED]`.
   The result must name the same six sources as before: `python -c "import sys; sys.path.insert(0,'tools'); import generate_translation_template as g; print([p.name for p,_,_ in g.SOURCES])"`
   prints `['taom_module_strings.xml', 'taom_wanderer_strings.xml', 'named_companion_strings.xml', 'taom_cc_strings.xml', 'taom_career_strings.xml', 'taom_enlistment_strings.xml']`.
5. GREEN: `test_loc_sources.py`, `test_translate_batching.py`, `test_translate_gamedir.py`,
   `test_translate_providers.py` -> `OK`.
6. Commit (`msg-135-5`): `refactor(tools): v2.0.28 - one table of English loc sources`. Body: the translator
   and the template writer read one ordered table; the template writer no longer crashes printing a non-Latin
   language tag. Trailers: `Not-tested: none (tool only)`,
   `Ported-from: 9e2a39f4092dd32c7bd58e193e24ee210594f26f`, `Ported-from: 303bf2a7d9db038474eca90af2a6bced8047868c`.

#### A6: The translator respects key owners and line terminators (`4ceae903b`, translator part)

1. RED: `git diff 4ceae903b^ 4ceae903b -- tools/tests/test_translate_batching.py | git apply --3way` (clean:
   the 1.4.x file equals its parent); run it -> errors (`key_owners`, `skip_ids_for`, `skip_ids`).
2. `git diff 4ceae903b^ 4ceae903b -- tools/translate_with_claude.py | git apply --3way`. It merges cleanly on
   `1b00d1acd` (checked with `git merge-tree`). If WP132's regex lines make a hunk conflict, keep both sides.
3. GREEN: `test_translate_batching.py` -> `OK` (includes `ShippedKeyCoverageTests` over the real tree; on
   `1b00d1acd` no key is hidden in DE, IT, RU or BR, checked).
4. Commit (`msg-135-6`): `fix(tools): v2.0.28 - translator keeps key owners and line endings`. Body: a key two
   English sources declare is seeded and translated only into its first source's file; a key another file of
   the language carries is not seeded twice; every line keeps its own terminator and the result is parsed before
   it is written. Trailers: `Not-tested: none (tool only)`,
   `Ported-from: 4ceae903bf1f52e1587506883a434439e8f85379`.

#### A7: Free English seeding and a real cap of zero (`c2c8afe42` at its 1.5.x end state)

`c2c8afe42`'s own `--seed-only` was replaced in the `b11b6d142` merge by trunk's form (`a9f2d7eda`, owned by
WP141) plus two guards. This step ports that end state, with `a9f2d7eda`'s and `71fea53c8`'s cap fix, because
Part C's free fill needs `--max-entries 0` to mean zero. WP141 must skip these hunks (Maintenance notes).
1. RED: `git show e45f6df2c:tools/tests/test_translate_caps_and_seed.py > tools/tests/test_translate_caps_and_seed.py`;
   run it -> failures (no `argv` parameter, no `--seed-only`).
2. Edit `main` in `tools/translate_with_claude.py`:
   - `def main():` -> `def main(argv=None):`; `args = p.parse_args()` -> `args = p.parse_args(argv)`.
   - the `--max-entries` argument becomes
     `p.add_argument("--max-entries", type=int, default=None, help="Cap API entries for testing; 0 is a cap of zero (no provider call)")`.
   - after the `--sync-ids` argument add
     `p.add_argument("--seed-only", action="store_true", help="With --sync-ids: seed the missing ids and stop, before discovering or translating anything (free; translation is a separate paid run)")`.
   - after `if not args.dry_run and not args.apply: p.error(...)` add:
     ```python
         if args.seed_only and not args.sync_ids:
             p.error("--seed-only is only valid with --sync-ids")
         if args.seed_only and args.module == "TAOM_Map":
             p.error("--seed-only has no TAOM_Map pass; use --module TAOM, Armory or all")
         if args.sync_ids and args.module in ("Armory", "all"):
             # Before the TAOM rows are written: a sync that reaches the Armory needs the install, and failing later
             # would leave the TAOM files seeded by a run that reported an error.
             ensure_exists(game_modules_root(), "the Bannerlord Modules folder")
         if args.max_entries is not None and args.max_entries < 0:
             p.error("--max-entries must be 0 or more")  # a negative slice would send all but one entry to the provider
     ```
   - inside `if args.sync_ids:`, after the line that prints the seeded count, add `if args.seed_only: return 0`.
   - `if args.max_entries and len(need_llm) > args.max_entries:` ->
     `if args.max_entries is not None and len(need_llm) > args.max_entries:`.
3. `LanguageFileCoverageTests.cs`: apply `git diff a9f2d7eda^ a9f2d7eda -- TAOM.Tests/Infrastructure/Localization/LanguageFileCoverageTests.cs | git apply --3way`
   (the "Fix (free, seeds the English rows only)" message).
4. GREEN: `test_translate_caps_and_seed.py`, `test_translate_batching.py` -> `OK`; Build; filter
   `LanguageFileCoverageTests` -> `Failed: 0`.
5. Commit (`msg-135-7`): `fix(tools): v2.0.28 - free English seeding, a real cap of zero`. Body:
   `--sync-ids --seed-only --apply` writes the English rows and stops before any provider is touched;
   `--max-entries 0` is a cap of zero (it meant no cap) and a negative cap is refused; a sync that reaches the
   Armory checks the install before writing. Trailers: `Not-tested: none (tool only)`,
   `Ported-from: c2c8afe420257616acf61991ce3fe2f1e0c58b1c`, `Ported-from: a9f2d7edab4096dee8cf2524949686d0be0bb560`,
   `Ported-from: 71fea53c83a8db1a9ce33a3ac20ff5be6e06ac42`, `Ported-from: b11b6d142e99774f462f7b8e365fb29d6bb0c009`.

#### A8: One text per key, and damaged rows reset (`4ceae903b` tests and data, `9e2a39f40` "Other", `9dd2f594f`)

1. RED, tests first:
   - `git show 4ceae903b:TAOM.Tests/Infrastructure/Localization/LocalizationKeyConsistencyTests.cs > TAOM.Tests/Infrastructure/Localization/LocalizationKeyConsistencyTests.cs`;
     change `private static Dictionary<string, string> RegisteredEnglish()` to `internal static` (`9dd2f594f`).
     Keep its own `Main/*.cs` walk and the bare `{=key}` exemption (1.5.x dropped it with NativeSkinFixes,
     which 1.4.x still has).
   - `git diff 4ceae903b^ 4ceae903b -- TAOM.Tests/Infrastructure/Localization/LanguageTextIntegrityTests.cs | git apply --3way`,
     then the same for `9e2a39f40` and for `9dd2f594f` (A0 proved `RegisteredEnglish` absent there).
     Resolve any conflict by keeping both sides (WP134 may have edited the file). Do not apply `9c310bc23`.
   - Build; run the filters `LocalizationKeyConsistencyTests` and `LanguageTextIntegrityTests`. Expected
     failures: `EveryCSharpDefault_...` (9 sites), `EveryDataDefault_...` (on its floor: the message prints
     the measured count, about 4,600; its 2 mismatches would show once the floor is set), `EveryNameKey_...`
     (`aom_clan_umbar_1_name`),
     `NoTranslatedString_MixesWritingSystems` (about 133 rows), `NoCachedTranslation_MixesWritingSystems`
     (about 151 values). Record the data-site count the message prints.
2. Data and code fixes, path-limited:
   `git diff 4ceae903b^ 4ceae903b -- Main/Features/LotrIssues/Templates/CombatLotrIssue.cs Main/Features/LotrIssues/Templates/DeliverPersonnelLotrIssue.cs Main/Features/Messengers/MessengerCampaignBehavior.cs Main/_Module/ModuleData/taom_lotr_issue_strings.xml Main/_Module/ModuleData/characters/clans.xml Main/_Module/ModuleData/lords.xslt Main/_Module/ModuleData/taom_career_strings.xml Main/_Module/ModuleData/taom_xslt_strings.xml | git apply --3way`.
   All eight merge cleanly on `1b00d1acd`. If WP131 moved the messenger lines, resolve by keeping the live file
   and setting the two inline texts to the registered English in `taom_messenger_strings.xml` (:36 and :17).
   The DeliverPersonnel `taom_lotr_issue_common_working` line now copies the registered text verbatim (it
   contains a long dash character; that is the registered game text, not prose).
3. Set the data floor in `EveryDataDefault_...`: `floor(0.9 x measured / 100) x 100` (about 4,100), and its
   comment to "<measured> on <date> on the 1.4.x line". Leave the other floors (819 C# sites, 156 files,
   19 sources and 2,251 keys clear them).
4. Seed the five new quest keys (free): `for L in BR CNs CNt DE FR IT JP KO PL RU SP TR; do python tools/translate_with_claude.py --lang $L --module TAOM --sync-ids --seed-only --apply || break; done`.
   Expected per language `+   5 ids seeded into std_taom_lotr_issue_strings_<locale>.xml` (plus any row an
   earlier WP left unseeded) and `Ids seeded:`.
5. Reset damaged rows and the two keys whose English this step changed:
   `python $H ws taom_career_cave_troll_master_desc aom_lord_WE9_l_name`. Expected: roughly 150 to 170 keys
   reset in total (about 133 damaged rows plus the two changed keys in 12 languages) and roughly 150 to 320
   cache entries dropped; no `NO ENGLISH` line (fix any by hand: delete that row).
6. GREEN: Build; filters `LocalizationKeyConsistencyTests`, `LanguageTextIntegrityTests`,
   `LanguageFileCoverageTests`, `LanguageDataXmlTests`, `RegisteredDefaultRoundTripTests`,
   `UnregisteredLocalizationKeyBaselineTests`, `LotrIssue`, `Messenger` -> `Failed: 0`. If the writing-system
   tests still list rows the helper missed, apply its rule by hand (row to registered English, cache key
   deleted); STOP if more than 20 remain.
7. Docs: add to `docs/features/localization.md` a "Consistency gates" paragraph (the three gates, the
   writing-system scan over files and caches) and to `TRANSLATOR_GUIDE.md` the key-ownership rule; 1.4.x facts only.
8. Commit (`msg-135-8`): `fix(localization): v2.0.28 - one text per key, damaged rows reset`. Body: new gates
   keep one English text per key, inline defaults equal to their registration, one name per name key, and no
   word from another writing system; the Combat and Deliver Personnel quests get their own fallback keys (they
   showed the Deliver Goods text in other languages); two messenger lines and two data texts match their
   registration; the six Umbar clans get their own name keys; about 133 damaged rows go back to English and
   their cache entries go, for the next translation run. Trailers:
   `Save-compat: clan names are saved (Clan.Name is a SaveableProperty), so an existing campaign keeps the shared Umbar key; new campaigns get six`,
   `Not-tested: in game, a German Combat issue without its own texts reads "a foe" text`,
   `Ported-from: 4ceae903bf1f52e1587506883a434439e8f85379`, `Ported-from: 9e2a39f4092dd32c7bd58e193e24ee210594f26f`,
   `Ported-from: 9dd2f594fd887f792a5cd336781b6832ff6ff753`.

#### A9: The translator keeps conversation animation tags (`7abce5db9`, partial)

1. If A0 printed `0` or `NO-PLACEHOLDER-TESTS`, first apply
   `git diff 113d3483b^ 113d3483b -- tools/translate_with_claude.py tools/tests/test_translate_placeholders.py | git apply --3way`
   and add `Ported-from: 113d3483bb1c6b303546e96c63e369eb2d920d5d` to this commit.
2. RED: `git diff 7abce5db9^ 7abce5db9 -- tools/tests/test_translate_placeholders.py | git apply --3way`; run
   it -> `FAILED (failures=2, errors=1)`: `test_dropped_expression_tag_is_rejected` and
   `test_changed_expression_tag_is_rejected` fail, `test_culture_tag_is_not_an_expression_tag` errors.
3. `git diff 7abce5db9^ 7abce5db9 -- tools/translate_with_claude.py | git apply --3way` (EXPRESSION_TAG, its
   count in `extract_placeholders`, prompt rules 3 and 7). Do not take its `_loc_sources.py` or template rows.
4. GREEN: `test_translate_placeholders.py`, `test_translate_batching.py` -> `OK`.
5. Commit (`msg-135-9`): `fix(tools): v2.0.28 - translator keeps conversation animation tags`. Body: a
   translation that drops or changes an [ib:], [if:], [rb:] or [rf:] tag is rejected, and the prompt says to copy
   them; the language files carry four such rows per language today. Trailers: `Not-tested: none (tool only)`,
   `Ported-from: 7abce5db992d52f9fd881f0214b01b52cf417740`.

#### A10: Translation fit report and Gauntlet screen checklist (`df375d99e`, pick)

1. `git cherry-pick -n df375d99e`. Expected conflicts: `docs/ai-includes/external-repo-adoption.md`,
   `tools/README.md` -> `git checkout HEAD <each>`. Clean but dropped: `docs/INDEX.md` and
   `.claude/skills/localize/SKILL.md` (`git checkout HEAD <each>`; the skill line goes to WP143), and the
   review record (`git rm -q -f docs/reviews/adopt-impeccable-2026-10-26.md`). Kept: `tools/loc_length_report.py`,
   `tools/tests/test_loc_length_report.py`, `docs/reference/gauntlet-screen-checklist.md`, and the
   `.claude/rules/gui-ui.md` line that points to the checklist (a rule line documenting the mechanism ported).
2. GREEN: `test_loc_length_report.py` -> `OK`; `python tools/loc_length_report.py --since $BASE` exits 0.
3. Commit (`msg-135-10`): `feat(tools): v2.0.28 - translation fit report and screen checklist`. Body: the
   report lists translations much longer than their English for a check in game; the checklist lists what a new
   screen must handle. Trailers: `Research: dump EventManager.cs:880 (disabled widgets are never hovered)`,
   `Not-tested: none (report only)`, `Ported-from: df375d99e6312ad583fa48ba9a6482eb2c988b6c`.

**Part A done**: see Done criteria. Run the full suite and the tool suite; put the results in your hand-off.

### Part B: data re-derived on 1.4.x (no paid call; one session)

#### B1: Troop, lord, clan and kingdom names enter the pipeline (`303bf2a7d`, adapted)

The generator is taken at its `e45f6df2c` end state (missing-source refusal, `--check`, one owner per key,
CRLF bytes) with only the four name categories of `303bf2a7d`. No GameText node is added for the four files:
nothing reads `{=KEY}` rows through GameText (1.5.x `rca-full-translation-run-2026-10-02.md` finding 10).
1. RED: `git show e45f6df2c:tools/tests/test_generate_name_localization_strings.py > tools/tests/test_generate_name_localization_strings.py`;
   delete `test_CharacterNameSources_ExcludeFilesOtherCategoriesOwn` (the character_name category is not ported)
   and keep `test_CategorySources_NeverIncludeUnloadedSettlementFiles`. Run -> error, no generator.
2. `git show e45f6df2c:tools/generate_name_localization_strings.py > tools/generate_name_localization_strings.py`.
   Delete from `CATEGORIES` the comment block that starts `# The data-text families (2026-10-01).` and the five
   entries `culture_text`, `hero_text`, `career_data`, `character_name`, `battle_scene`. Rewrite the docstring's
   "WHY THIS EXISTS" paragraph: "Troop, lord, clan and kingdom names carry {=KEY}default inline in their own data
   XML. The translator reads only registered English sources (tools/_loc_sources.py), so an unregistered key shows
   its English default in every language, with no error anywhere (#572)."
3. Append to `TAOM_SOURCES` in `tools/_loc_sources.py`, after the player switcher row:
   `# Generated by tools/generate_name_localization_strings.py (its CATEGORIES outputs):` and the four rows
   `("taom_troop_name_strings.xml", "std_taom_troop_name_strings_{locale}.xml")`, then lord, clan, kingdom.
4. In `tools/tests/test_loc_sources.py` put back the `gen` import and `test_EveryGeneratorOutput_IsATableRow`.
5. `python tools/generate_name_localization_strings.py --dry-run`. Expected about: clan 120 (115 plus the five
   new Umbar keys), kingdom 60, lord 988, troop 857. Then `--apply`, then `--check` -> `OK: 4 generated file(s) match their sources`.
6. Each of the 12 `Languages/<L>/language_data.xml`: after the last `LanguageFile` line add four lines in the
   order troop, lord, clan, kingdom, e.g. for DE
   `  <LanguageFile xml_path="DE/std_taom_troop_name_strings_deu-DE.xml" />` (locale suffix of that folder).
7. `python $H stubs` -> 48 `created` lines. Then seed (free):
   `for L in BR CNs CNt DE FR IT JP KO PL RU SP TR; do python tools/translate_with_claude.py --lang $L --module TAOM --sync-ids --seed-only --apply || break; done`
   -> per language four `+ N ids seeded into std_taom_<kind>_name_strings_<locale>.xml` lines with the dry-run sizes.
8. `LanguageDataXmlTests.cs`: raise the count test by 4 (13 -> 17 gives `303bf2a7d`'s
   `AllLanguageDirs_HaveExactlySeventeenLanguageFiles` name and message; another count keeps the pattern) and add
   `303bf2a7d`'s four `AllLanguageDirs_Have<Kind>NameStringsFile` tests (`git show 303bf2a7d -- TAOM.Tests/Infrastructure/Localization/LanguageDataXmlTests.cs`).
9. GREEN: `test_generate_name_localization_strings.py`, `test_loc_sources.py`, `test_translate_batching.py` ->
   `OK`; Build; filter `TAOM.Tests.Infrastructure.Localization` -> `Failed: 0`.
10. Docs: `docs/features/localization.md` (the name pipeline, its four files, the generator's `--check`),
    `TRANSLATOR_GUIDE.md` (re-run the generator after adding a named troop, lord, clan or kingdom),
    `docs/reference/localization-map.md` (the four files and two tools); measured 1.4.x counts only.
11. Commit (`msg-135-11`): `feat(localization): v2.0.28 - troop, lord, clan and kingdom names`. Body: those
    names carried {=KEY} defaults that no English source registered, so every language showed English; the
    generator registers them in four English files (counts from step 5) and every language file holds an English
    row until the translation run. Trailers: `Not-tested: in game, names show translated after Part C`,
    `Ported-from: 303bf2a7d9db038474eca90af2a6bced8047868c`, `Ported-from: 9e2a39f4092dd32c7bd58e193e24ee210594f26f`.

#### B2: Vanilla translations come back for 181 English-only rewordings (`8a4027110`, re-derived on v1.4.8)

1. RED: write `TAOM.Tests/Infrastructure/Localization/VanillaLocalizationKeys.cs`, `VanillaKeyOverrideTests.cs`
   and `kept-vanilla-key-rows.txt` from `git show 8a4027110:<path>` (the `8a4027110` versions; `9e2a39f40`'s
   first version of the test is superseded). Build; filter `VanillaKeyOverrideTests`. Expected:
   `TaomLanguageRows_VanillaKey_OnlyWhenKept` fails (1,803 pairs, 40 shown); the other two pass. An
   Inconclusive result means the install was not found: STOP.
2. Tools, path-limited from `8a4027110`: `tools/translate_with_claude.py`, `tools/generate_translation_template.py`,
   `tools/rebuild_translation_files.py`, `tools/tests/test_translate_batching.py`, new
   `tools/tests/test_english_only_writers.py` (on `1b00d1acd` three of them conflict because A5 and A6 had not
   landed; after them they should merge; keep both sides of any conflict). Then move the two constants the translator hunk adds
   (`OVERRIDE_SOURCE`, `VANILLA_ID_SHAPE` and their comment) into `tools/_loc_sources.py` (with `import re`; this is
   `616cf12f1`'s end state) and make the translator import read
   `from _loc_sources import OVERRIDE_SOURCE, TAOM_SOURCES, VANILLA_ID_SHAPE  # noqa: F401`.
3. Tests, path-limited from `8a4027110`: `LanguageFileCoverageTests.cs` (English-only keys need no row) and
   `LocalizationOverrideLoaderTests.cs` (the shipped-file test; change "179 ship" to "181 ship" and `>= 179` to
   `>= 181`; its pins `0CsbbwtN`, `LjUhEJxz`, `l4pRw7pO` exist here). Do NOT take its two
   `taom_module_strings.xml` deletions: v1.4.8 still declares `bjEN2OzZ` and `d6KbdIWg`.
4. `python $H vanilla`. Expected: `total: 1803 rows, 1800 cache entries removed; rewordings: 181`.
5. Overrides, by hand: in `tools/translation_overrides/cns.json` delete the `uc4M4bhG` entry, in `ko.json`
   `qa4FlTWS`, in `tr.json` `uiY3ds0Z` (leave no trailing comma), and end each `_comment` with " The
   vanilla-derived ids that once lived here (uc4M4bhG, qa4FlTWS, uiY3ds0Z) are English-only overrides now."
6. GREEN: `test_english_only_writers.py`, `test_translate_batching.py` -> `OK`; Build; filters
   `VanillaKeyOverrideTests` (3 passed, none Inconclusive), `LanguageFileCoverageTests`,
   `LocalizationOverrideLoaderTests`, `LanguageTextIntegrityTests` -> `Failed: 0`.
7. Docs: the English-only rule in `localization-override.md` and `TRANSLATOR_GUIDE.md` (1.4.x counts: 181, 369
   kept, 1,803 removed).
8. Commit (`msg-135-12`): `fix(localization): v2.0.28 - vanilla translations for 181 rewordings`. Body: TAOM
   rewords 181 game texts in English only, but shipped a machine translation of each that replaced the game's
   curated rows; 1,803 rows go, 369 stay where the game's row would read wrong with TAOM's names; the tools now
   leave these keys out. Trailers: `Research: installed vanilla ids (VanillaLocalizationKeys over Native, SandBoxCore, SandBox, StoryMode, CustomBattle)`,
   `Not-tested: in game, German "Call ... to war" reads the game's own German`,
   `Ported-from: 8a40271107ff75703db4ccfa5b3ed99963cd210f`.

#### B3: A gate for text rows that nothing reads (`616cf12f1`, tool ported, removals re-derived)

1. Take from `616cf12f1`: `tools/check_orphan_loc_rows.py`, `tools/loc_composed_families.json`,
   `tools/loc_orphan_allowlist.json`, `tools/tests/test_check_orphan_loc_rows.py` (`git show 616cf12f1:<path> > <path>`).
   Its `_loc_sources.py` constants are already in place (B2). Run the test file -> `OK` (synthetic trees).
2. Adapt the declarations: with `NO-DUTYTEXT`, set the `enlist_duty` family's `site` to the 1.4.x file that
   builds the key (`git grep -n '"taom_enlist_duty_"' -- Main`). Run `python tools/check_orphan_loc_rows.py > /c/Users/mikew/source/repos/taom-port/h135/orphans.txt; echo exit $?`.
   Drop every `STALE ALLOWLIST` entry it names (expected: the four `taom_precompile_*`, read by
   `Main/SubModule.cs:645-661`) and fix every `STALE FAMILY`; re-run until only `UNREAD` and `SHADOWED` lines remain.
3. For each `UNREAD` id, check the live modules do not read it:
   `grep -rl "<id>" "$BANNERLORD_GAME_DIR/Modules/TAOM_Map/ModuleData" "$BANNERLORD_GAME_DIR/Modules/LOTRLOME_Armory/ModuleData"`
   (read only). An id they read goes into `loc_orphan_allowlist.json` with the reason "read by the live TAOM_Map"
   (or Armory) and is not removed. Re-run the gate into `orphans.txt`.
4. `python $H orphans /c/Users/mikew/source/repos/taom-port/h135/orphans.txt`. Expected about 52 English rows
   (the dead goblin and Misty Mountain bandit texts and the shadowed duplicates) and the matching language rows.
   Re-run the gate -> `exit 0`.
5. GREEN: Build; Test (full) -> `Failed: 0`; tool suite no new failure.
6. Docs: a `TRANSLATOR_GUIDE.md` paragraph on the gate (check the live modules before retiring a row).
7. Commit (`msg-135-13`): `feat(tools): v2.0.28 - a gate for text rows that nothing reads`. Body: the gate lists
   registered TAOM rows that no code, data, composed family or reasoned allowlist entry reads; its first run here
   removed <n> rows from the English file and the matching rows and cache entries; a shadowed row leaves the
   English file only. Trailers: `Not-tested: none (rows nothing reads)`,
   `Ported-from: 616cf12f16d2942e0235d793afc375509a8c204c`.

**Part B done**: see Done criteria.

### Part C: one translation pass for every wave-4 row and the names

#### C1: Seed and fill from caches (free)

1. Seed: the 12-language `--sync-ids --seed-only --apply` loop (as in B1). Expected `Ids seeded: 0` or the rows
   an earlier WP left unseeded.
2. `python $H cacheseed`. Expected per-language counts near 2,300: 1,989 name keys had an identical-English
   entry in the `e45f6df2c` de, ru and br caches, and WP134 Part C's 320 wanderer rows have one in all 12 caches
   (`e45f6df2c`, and `41aa2779b` for BR and IT; checked, so they need no paid call). BR and IT read `41aa2779b`.
3. Free fill: `for L in BR CNs CNt DE FR IT JP KO PL RU SP TR; do python tools/translate_with_claude.py --lang $L --module TAOM --apply --max-entries 0 || break; done`.
   Each run prints `--max-entries cap applied: 0/<n>` when rows remain and writes only override and cache fills;
   no API key is needed.
4. GREEN: Build; Test (full) -> `Failed: 0` (the writing-system tests now read the filled rows).
5. Commit (`msg-135-14`): `chore(localization): v2.0.28 - fill wave-4 and name rows from cache`. Body: rows
   with identical English on the 1.5.x line take its translation, the 320 wanderer rows per language from
   WP134 Part C among them; the rest stay English until the paid run.
   Trailers: `Not-tested: in game, see the plan's in-game list`. No `Ported-from`.

#### C2: Cost report for the maintainer

`for L in BR CNs CNt DE FR IT JP KO PL RU SP TR; do echo "== $L"; python tools/translate_with_claude.py --lang $L --module TAOM --dry-run | grep -E "needs LLM|Estimated API cost"; done`
Report the 12 `needs LLM` counts and costs to the maintainer, then stop: the paid run is not executor work.

#### C3: Paid translation (MAINTAINER ONLY; spends money)

In `$WT`, for each language: `python tools/translate_with_claude.py --lang <L> --module TAOM --apply` (or add
`--batch` for the Batches API at half price, up to 24 h). Never another `--module`. Keep the console output.

#### C4: Validate and commit the paid rows (executor, after C3)

1. Build; Test (full) -> `Failed: 0`. If the writing-system tests fail: `python $H ws`, ask the maintainer to
   re-run C3 for the affected languages once, and leave anything still failing as English (rows equal to their
   English pass).
2. `python tools/loc_length_report.py --since $BASE` (advisory). List flagged keys shown in fixed-width widgets
   under `Not-tested:`.
3. Commit (`msg-135-15`): `chore(localization): v2.0.28 - translate the wave-4 and name rows`. Body: the counts per
   language from C3. Trailers: `Not-tested: <flagged keys>`.

### Part D (optional, P3): Italian name grammar tags (`fd1cee10b`, Italian half)

Merge Parts A to C without D if this Part stops. v1.4.8's `ItalianTextProcessor` reads the `.MS/.MP/.FS/.FP/.MN/.FN`
tags; there is no Portuguese processor, so BR stays untagged.
1. Take from `fd1cee10b`: `tools/name_tags.py`, `tools/generate_name_tags.py`, `tools/name_tag_inventory.py`,
   `tools/translation_name_tags.json`, the tests `test_name_tags.py`, `test_generate_name_tags.py`,
   `test_rebuild_name_tags.py`, `test_translate_grammar_tags.py`, `test_check_external_name_tags.py`, and the
   `_gamedir.py` `DEFAULT_GAME_ROOT` constant (then drop the translator's own `DEFAULT_GAME_ROOT` line).
2. Restrict to Italian: `TAGGED_LANGUAGES = ("IT",)` in `name_tags.py`; delete the `"BR"` block of the table;
   set the IT `"tags"` to `{}` and run `python tools/generate_name_tags.py --write`, then `--list-undecided`.
3. Path-limited hunks of `fd1cee10b` for `translate_with_claude.py`, `rebuild_translation_files.py` and
   `check_external_loc_coverage.py`; resolve import-block conflicts by keeping both lines.
4. `python tools/name_tags.py --lang IT --apply` (repo files only; never `--target live-map`).
5. Delete only the BR-specific test methods; STOP if a test needs more than that. Tool suite and full suite green.
6. Commit: `feat(localization): v2.0.28 - Italian name grammar tags`, `Ported-from: fd1cee10b227a2f2b760db1258b45889b796dba9`.
Not taken: the Portuguese rows and article removals, the map writers (`Apply-MapVillageNames.py`,
`add_map_villages.py`, `rename_map_settlements.py`; WP141 owns their rewrite), the `NameGrammarTags` C# report
and `taom.print_kept_pairs` command (a new feature folder), the live TAOM_Map settlement edit.

## Disposition

| 1.5.x commit | Group | Disposition | Where |
|---|---|---|---|
| `407b1e767` English overrides only for English | issue-706 | ported (pick; patch hand-resolved for either key form; adapter trimmed to `ActiveLanguage`) | A1 |
| `41aa2779b` grammar processors run again | issue-1026 | ported (pick); adapted: local engine-like reader replaces the `validate_xml_schemas` import; the live-module C# test is left out (the tool covers it); release skill line to WP143 | A2 |
| `e74ef3931` popup strings in Custom Battle; archers vs horse | localization-gametext-gametypes | partial by hand: two GameType rows and a standalone pin test; the archer and doctrine half is 1.5.x-only | A3 |
| `337228885` string harvest stops adding blank lines | issue-919 | ported (pick); lessons hunk dropped | A4 |
| `9e2a39f40` localization and mesh weight transfer tools | localization-full-translation-run | partial: `_loc_sources.py` and its wiring (A5), "Other" script class and 4 rows (A8), VanillaKeyOverrideTests at `8a4027110`'s version (B2); skipped: TournamentRewards (feature), Arena, custom battle (already `55d779012`), regenerated files, xslt revert, caches, mesh tool, RCA | A5, A8, B2 |
| `4ceae903b` hill troll troop, 200 HP trolls, loc sweep | loc-translator-key-ownership | partial: translator key owners and terminators (A6); consistency gates, writing-system scan, quest fallback keys, messenger defaults, Umbar keys (required once names register), two registrations (A8); skipped: hill troll, TrollBruteForce, 200 HP, troop and party data, SettlementGuard, caches and 106 language files (re-derived), translation override polish, `test_ci_runner_compat.py` (D9), Lindon keys (already in the #697 tree) | A6, A8 |
| `c2c8afe42` career forks generator, free English seeding | localization-translate-seed-only | ported at its 1.5.x end state (`b11b6d142` kept trunk's `a9f2d7eda` form plus two guards); its `test_translate_seed_only.py` is absent at `e45f6df2c`, so `test_translate_caps_and_seed.py` covers the mode; generator, rules, docs, RCA skipped (feature) | A7 |
| `7abce5db9` bandit clans speak in their own voices | loc-translator-expression-tags | partial: tag check, two prompt exceptions, test cases; BanditVoices and its rows, ServerSettings and CoopSettingsRelevance rows (D7) skipped | A9 |
| `df375d99e` translation fit report and screen checklist | issue-1007 | ported (pick); gui-ui.md line kept; localize skill line to WP143; review record dropped | A10 |
| `303bf2a7d` troop/lord/clan/kingdom names in pipeline | issue-572 | adapted: generator at `e45f6df2c` restricted to four categories, re-run on 1.4.x data; no GameText nodes (inert); template UTF-8 fix (A5); generated files, caches and the enlistment reformat not copied | B1, A5 |
| `10539c29b` ASO description rows restored | issue-572 | skipped: 1.5.x-only (ASO); its lesson is moot because this WP never runs `generate_translation_template.py --apply` | - |
| `8a4027110` vanilla translations come back for 177 texts | loc-english-only-vanilla-overrides | re-derived on v1.4.8: kept list carried (all 369 lines apply), 1,803 rows and 1,800 cache entries removed, writer exclusions and tests; `bjEN2OzZ`/`d6KbdIWg` rows kept (v1.4.8 declares them); skill lines to WP143 | B2 |
| `616cf12f1` a gate for text rows that nothing reads | issue-1018 | tool, declarations and tests ported; removals from the gate's own 1.4.x run; `.ai` lines to WP143 | B3 |
| `fd1cee10b` Portuguese and Italian name grammar | issue-1023 | optional Part D, Italian half only; Portuguese half skipped (no v1.4.8 processor); NameGrammarTags report skipped (new feature folder) | D |
| adopted: `a9f2d7eda` and `71fea53c8` (WP141) | tools-artist-port-safety | translator cap and seed-only hunks, `test_translate_caps_and_seed.py`, coverage message taken here; WP141 skips them | A7 |
| adopted: `b11b6d142` (merge) | - | its two `--seed-only` guards | A7 |
| adopted: `9dd2f594f` (WP134) | issue-757 | `RegisteredEnglish` internal and the skip-English rule, only if absent (their target code arrives here) | A8 |
| adopted: `113d3483b` (WP132) | battlebalance-enlisted-survival-roll | translator regexes and placeholder tests, only if WP132 did not land them | A9 |

## Test plan

- C# new: `OverrideLanguageGateTests` (15 methods, 19 cases, from `407b1e767`), 4 tests in
  `MBTextManager_GetLocalizedText_PatchTests`, 2 in `LanguageProcessorTests`, `LocalizationKeyConsistencyTests`
  (11 methods), the writing-system tests in `LanguageTextIntegrityTests`, 1 + 4 in `LanguageDataXmlTests`,
  3 in `VanillaKeyOverrideTests`, 1 in `LocalizationOverrideLoaderTests`.
- Python new: `test_check_external_loc_coverage.py`, `test_harvest_literal_loc_keys.py`, `test_loc_sources.py`,
  `test_translate_caps_and_seed.py`, `test_loc_length_report.py`, `test_generate_name_localization_strings.py`,
  `test_english_only_writers.py`, `test_check_orphan_loc_rows.py`, plus the added cases in
  `test_translate_batching.py` and `test_translate_placeholders.py`.
- Structural pattern: `TAOM.Tests/Infrastructure/Localization/LanguageFileCoverageTests.cs` (repo-file scans).
- Untestable here (for `Not-tested:`): what the engine renders in each language, the processor reset by load
  order, Custom Battle text loading, save behaviour of clan names.
- Verification: the full suite `Failed: 0` with `Total` = A0's total plus the new cases; the tool suite no new
  failure.

## Done criteria

Part A (all must hold):
- [ ] Build exit 0; Test `Failed: 0`; tool suite no new failure vs A0.
- [ ] `grep -c "AllowsOverrides" Main/Features/LocalizationOverride/Hooks/MBTextManager_GetLocalizedText_Patch.cs` -> `1`
- [ ] `grep -l 'text_processor=' Main/_Module/ModuleData/Languages/language_data.xml Main/_Module/ModuleData/Languages/*/language_data.xml | wc -l` -> `8`
- [ ] `grep -c 'aom_clan_umbar_1_name' Main/_Module/ModuleData/characters/clans.xml` -> `1`
- [ ] `python -c "import sys; sys.path.insert(0,'tools'); import translate_with_claude as t; print(hasattr(t,'key_owners'), hasattr(t,'EXPRESSION_TAG'))"` -> `True True`
- [ ] `git log --oneline $BASE..HEAD | wc -l` -> `10`; every subject matches `v2.0.28 - `; every body has its `Ported-from:` lines.
- [ ] `git status --porcelain` empty (the helper and message files live outside the worktree).
Part B:
- [ ] `python tools/generate_name_localization_strings.py --check` exit 0; `python tools/check_orphan_loc_rows.py` exit 0.
- [ ] `ls Main/_Module/ModuleData/Languages/*/std_taom_*_name_strings_*.xml | wc -l` -> `48`
- [ ] Filter `VanillaKeyOverrideTests`: 3 passed, 0 Inconclusive; full suite `Failed: 0`; `python tools/validate_moduledata.py` no new ERROR.
- [ ] 3 more commits; `git status --porcelain` empty.
Part C:
- [ ] C1 committed; C2 report delivered; after C3, C4 committed with the suite at `Failed: 0`.
Part D (if run): its tests green and one commit.
Every Part: `git diff --name-only $BASE..HEAD -- plans/README.md` prints nothing; the hand-off carries the
status, commit hashes and measured counts.

## STOP conditions

- A0 finds RepoPaths missing, the laptop Armory still `v2.0.26` (D20), any of this WP's new files already
  present, `RegisteredEnglish`/`name=` already in the files it checks, or WP134 Part C's wanderer rows absent.
- Any command would write the game install: a translator run with `--module` other than `TAOM`,
  `name_tags.py --target live-map`, any `Apply-MapVillageNames.py` or `add_map_villages.py` run. Never.
- Any translator run that could call a provider: everything except `--dry-run`, `--sync-ids --seed-only`, and
  `--apply --max-entries 0` (after A7). Before A7, `--max-entries 0` means no cap: never use it.
- `generate_translation_template.py --apply` would be needed: it overwrites every templated language file. Never.
- A 3-way apply conflicts in a C# or tool file in a way this plan does not describe.
- `VanillaKeyOverrideTests` is Inconclusive (install not found), `KeptRows_EachStillHasItsRow` fails, or
  `ModuleStrings_VanillaShape_MatchesTheInstall` reports anything: do not delete rows to make it pass.
- The helper's `vanilla` totals differ from 1,803 / 1,800 by more than the rows earlier WPs added, or `ws`
  prints `NO ENGLISH` for more than 5 rows, or more than 20 writing-system offenders remain after it.
- The name generator reports a missing source, `EveryNameKey_...` names a key other than
  `aom_clan_umbar_1_name`, or the dry-run sizes differ from the expected ones by more than 10 percent.
- The orphan gate names a row that the live TAOM_Map or Armory reads and you cannot allowlist it with a reason.
- `LanguageProcessorTests.TaomLanguageDataFiles_InstalledNative_...` fails after A2 (an id differs from Native's),
  or `Prefix_GameGate_EngineLanguageIsEnglish_ReturnsTheOverride` throws.
- A step's verification fails twice after a reasonable fix attempt.

## In-game checks owed (maintainer, on a build containing this WP; read `Logs/taom_debug_<timestamp>.log` under the game's `bin/Win64_Shipping_Client/` and `rgl_log`)

1. #706, German: an `aom_*` notable or a `TAOM_*` minor faction shows its German row; switching the text language
   to English logs `[LocalizationOverride] Text language is English again: the English string overrides apply`,
   back to German logs `Text language 'Deutsch' is not English`; a session that starts in English logs neither.
2. #1026, with TAOM after TAOM_Map and the Armory (the players' order): English party names read
   "<Name>'s Party"; a German or Turkish line with a lord's name keeps its article or suffix. With TAOM_Map loaded
   after TAOM the seven languages lose it again until the live files are fixed (step M2).
3. Names (after Part C): in German, the party screen troop names, encyclopedia lord names, clan and kingdom names
   are German; in a NEW campaign the six Umbar clans show six names (an existing campaign keeps its saved one).
4. Vanilla rewordings (B2): German "Call ... to war" decisions read the game's own German; a kept BR row (for
   example `0cdXddA9`) still shows TAOM's row.
5. Custom Battle: no "Text with id" error for a TAOM culture text (A3).
6. A translated conversation line with an animation tag animates and shows no `[ib:...]` text (A9).
7. Keys `loc_length_report.py` flagged in fixed-width widgets fit in that language (C4).

Maintainer steps:
- M1: authorize and run C3 (spends money).
- M2: fix the 15 live `language_data.xml` files (Armory English, DE, FR, IT, PL, RU, SP, TR; TAOM_Map DE, FR,
  IT, PL, RU, SP, TR): keep `name`, add Native's `text_processor` (table under Engine facts). Then, before every
  release, run `BANNERLORD_GAME_MODULES="<channel>/Modules" python tools/check_external_loc_coverage.py --processors-only`;
  any exit other than 0 blocks the release. Ship TAOM_Map and Armory packages that carry the fix.
- M3: create the tracking issue(s) before the integrator merges.
- M4: decide whether Part D runs. If a player runs BetterTime (E1/E2 load it last), check whether its
  `language_data.xml` carries `name` without `text_processor` (UNVERIFIED).

## Maintenance notes

- **Handoffs.** WP141: `a9f2d7eda`'s and `71fea53c8`'s `translate_with_claude.py` hunks and
  `test_translate_caps_and_seed.py` landed here; port only their other files. Its `244c15d27` port may keep
  `carry_tag` only if Part D landed `name_tags.py`. WP136: `check_external_loc_coverage.py` carries a local copy
  of `_parse_like_engine`; when `validate_xml_schemas.py` lands, switch back to the import. `f1675c058` makes
  `--module` required; every test added here already passes `--module`. WP138: `check_stale_loc_rows.py` runs on
  these files next. WP143: take the skill and `.ai` lines this WP dropped: `41aa2779b`'s release item 6a,
  `df375d99e`'s and `71fea53c8`'s `/localize` lines (`loc_length_report.py --since`, `--sync-ids --seed-only --apply`),
  `8a4027110`'s two `/localize` lines (English-only overrides), `616cf12f1`'s `loc-orphans` rows in
  `.ai/routing.json` and `.ai/verification.md`. WP142: `tools/README.md` rows for the new tools and the
  `release-process.md` processor step.
- **Follow-ups deliberately left out**: the five data-text name families of 1.5.x (one `CATEGORIES` entry and one
  `_loc_sources` row each, plus a manifest line, stubs and a paid run; `--dry-run` measures them); Portuguese
  name tags (needs a v1.4.8 consumer of the tags verified first); the NameGrammarTags kept-pair report; the
  translation override polish in `4ceae903b` (RU and IT); `44d4dba46` (1.5.x, newer than this programme's
  classification): `rebuild_translation_files.py` reads `_loc_sources.py` and the seeder uses a file's usual
  indent and line ending. The integrator decides where it goes.
- **What a reviewer should probe** (`/deep-review`): the gate placement before the id is built (no allocation in
  other languages); the helper's writing-system mirror against the C# rule; the B2 kept list (BR gender-tag rows
  matter more here, since BR names carry no tags); the B3 removals against the live modules; that no translator
  run in the history used `--module` other than `TAOM`.
- **Order rules for later work**: a new English strings file needs a `_loc_sources.py` row (`test_loc_sources`
  fails otherwise), a manifest line in all 12 languages, a stub and a `--seed-only` run; a new named troop, lord,
  clan or kingdom needs `generate_name_localization_strings.py --apply` (its `--check` fails otherwise).
