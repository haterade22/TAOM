# Plan 114: Name what an in-game reload adds to every later save, with probes and a repro matrix, before any fix

> **Executor instructions**: Follow this plan step by step. Run every verification command and confirm
> the expected result before moving on. If anything in "STOP conditions" occurs, stop and report; do
> not improvise. Three Parts (A, B, C) each land on their own; finish a Part completely (its done
> criteria included) before starting the next. Part C needs Part B. When the WP is done, report status,
> commit hashes and measured counts in your hand-off (Step C6); never edit `plans/README.md`, which
> belongs to the integrator. Every code block starts at column 0: copy it as shown.
>
> **Drift check (run first, inside the worktree)**:
> `git diff --stat 1b00d1acd..HEAD -- Main/SubModule.cs Main/Features/SaveLoadDiagnostics Main/Features/RaceAge/Models/TaomPregnancyModel.cs Main/_Module/ModuleData/configs/initial_child_generation.json Main/_Module/ModuleData/raceage/race_age_config.json TAOM.Tests/Features/SaveLoadDiagnostics TAOM.Tests/Features/RaceAge docs/features/save-load-diagnostics.md docs/features/race-age-system.md docs/features/initial-child-generation.md docs/features/dev-console.md docs/modding/configs-balance.md docs/reviews/rca-wotr-phase-ordering-2026-07-30.md docs/reviews/lessons docs/reference/engine docs/reference/harmony-patch-registry.md docs/reference/bannerlord-engine-and-toolchain.md tools/README.md`
> Earlier WPs of this programme are expected to have changed `Main/SubModule.cs` (WP104 converted
> `_harmony.PatchCategory` to `TryPatchCategory`) and two lessons files (WP101 appended entries).
> Compare every other changed path against the excerpts below; STOP only on a real mismatch.

## Status

- **Priority**: P1
- **Effort**: L (Part A: S, Part B: M, Part C: M)
- **Risk**: MED (always-on observe-only hooks on every save and load; one gameplay data fix, #628)
- **Depends on**: WP104 (`plans/104-guarded-patch-categories.md`: `TryPatchCategory` in `Main/SubModule.cs`
  and `TAOM.Tests/Infrastructure/RepoPaths.cs`). Wave 0 (WP101 to WP105) merges before wave 1 starts.
- **Category**: perf (save-time diagnostics), plus one shared-feature bug port (#628) and engine docs
- **Wave / lane**: wave 1, lane E
- **Executor**: laptop
- **Decisions applied**: D1 (no `CHANGELOG.md`; the commit body is the entry), D2 (SaveLoadDiagnostics
  has no 1.5.x module, so it stays hand-wired in `Main/SubModule.cs`; nothing here owns save data), D8
  (subjects carry `v2.0.28`), D10 (an ordinary session executes this), D13 (#628 data fix: defect
  verified byte-identical on 1.4.x data, no OUT content), D18 (lessons are hand-appended; no
  `merge=union`), D19 (git only through the Bash tool), D20 (no ModuleData commit before WP101's
  laptop Armory refresh)
- **Planned at**: 1.4.x `origin/bannerlord-1.4.5` `1b00d1acd` and 1.5.x `origin/bannerlord-1.5.x`
  `e45f6df2c`, 2026-11-04
- **Issue**: Port of #628 (`83bdad853`, `925db38d1`) and of the #792/#793 engine-doc corrections. The
  probe work (SAVE-TIME-01/07, MEMORY-05, LOAD-TIME-03 part 1) has no issue: orchestrator creates the
  issue before Part B lands.

## Why this matters

One player's saves on the public 1.4.8 build went from 91.5 s (17:44) to 1,460.8 s (the 21:18
autosave) within one 4.5-hour session (E2, `rgl_log_22112`). The verification report V01 (`plans/_audit/2026-11-04-port148/verify/V01-save-growth.md`)
re-derived 12 loads and 15 saves from that log: object and container counts stay flat across about
80 campaign days of play and step up only across in-game loads, by about 71,500 containers (85,000
to 90,000 objects) per reload, 12 increments by 21:18. The 21:18 autosave held 2,343,646 objects and
1,884,752 containers against 1,302,023 and 1,026,584 in the file the session started from. A first
load in a fresh process adds nothing (E1, and E2 load #1). Not proven: that the growth is permanent
in the file, and that it is exactly one step per load. The "stale XML object" candidate is refuted
for v1.4.8; the holder is unknown. Two live hypotheses: SaveShield swallowing a load-callback fault on
a second load, or an in-game load behaving differently from a main-menu load.

The 1.4.x `[SaveLoad]` probe times saves but cannot say what grew: it logs no counts, no memory at
save, no loads-per-process and no load origin. This WP adds exactly those probes, a type census that
names the carrier, a tool that diffs two censuses, and a repro matrix the maintainer runs in game.
It fixes nothing in the save path: a follow-up plan is written once the census names the carrier.
It also ports #628 (orc and goblin clans breed toward twice their cap on 1.4.x: identical code and
config to the 1.5.x pre-fix state, and more heroes means more saved containers), corrects the
engine docs that wrongly say a load re-aligns the daily tick (on v1.4.8 a load can repeat the save's
day), and drops a per-call reflection cost from two always-on load finalizers (LOAD-TIME-03 part 1).

## Current state

### Files and roles (all on `origin/bannerlord-1.4.5`)

- `Main/Features/SaveLoadDiagnostics/SaveLoadDiagnosticsService.cs`: the `[SaveLoad]` line writer.
  `BeginLoadAttempt(string)` `:36`, `BeginSaveAttempt(string)` `:38`, `BeginAttempt` `:80-91` (resets
  seq/faults/dedup, stamps `name='...'`), `Stamp` `:93-99`. Interface `ISaveLoadDiagnosticsService.cs:9-33`
  (six members; "every member may be called concurrently from TWParallel load workers and the async
  save-writer thread"). `Domain/SaveLoadPhase.cs`: phase names, a log contract (add, never rename).
- `Hooks/SandBoxSaveHelper_TryLoadSave_Patch.cs:23-31`: Prefix, `svc.BeginLoadAttempt(saveInfo?.Name ?? "<null>")`.
  Every campaign load reaches it (v1.4.8 dump `_modules_build/SandBox__SandBox.View.cs:542` preload,
  `:1291` `ContinueCampaign`, `SandBox__SandBox.ViewModelCollection.cs:6056` save/load screen).
- `Hooks/SaveOutput_PrintStatus_Patch.cs:36-63`: Postfix, `LogPhase(SaveCompleted, "result=Success")` at
  `:46`. `Hooks/SaveManager_Save_Patch.cs:17-22` (SaveBegin) is not changed here.
- `Hooks/ContainerLoadData_Fill_Patch.cs:63-78`, `Hooks/HeaderLoadData_Readers_Patch.cs:32-52`: finalizers
  taking `MethodBase __originalMethod` on every call (four per container, two per header, mostly on
  parallel load workers; LOAD-TIME-03). `Hooks/CampaignBehaviorDataStore_LoadBehaviorData_Patch.cs:33-35`:
  `ResolveStoreType()`, reused by Step B3.
- `Main/Features/BattleLoadDiagnostics/ProcessMemoryTokens.cs:14-29`: `internal static string Format()`
  -> `gc=a/b/c heapMB=N privMB=N wsMB=N`.
- `Main/SubModule.cs` (single-owner): Patch61 block `:280-334` (origin: `_harmony.PatchCategory(...)` at
  `:313`, sub-category loop `:314-329`; after WP104 both use `TryPatchCategory(...)`, as 1.5.x does at
  `origin/bannerlord-1.5.x:Main/SubModule.cs:387-396`); `OnGameEnd` `:668-695`; `StampSaveLoadPhase`
  `:751-758`, which passes `ProcessMemoryTokens.Format()`.
- `Main/Features/RaceAge/Models/TaomPregnancyModel.cs:76-99`: `ComputeBaseChance`; `:91-93` the clan
  brake, `:97` `baseChance *= raceFertilityModifier;` (the bonus multiplies after the brake, #628).
  `configs/initial_child_generation.json` excludes only `mordor`, `isengard`, `gundabad`, `dolguldur`,
  while `charactercreation/cultures.json` also has `goblin`, `mistymountainorcs`, `bluecraig` with an
  orc-kin default race; `raceage/race_age_config.json` has human `fertilityEnd` 195, orc-kin 2.0 to 3.0x.
- `docs/reviews/rca-wotr-phase-ordering-2026-07-30.md:16-19` says `CreateCampaignEvents` "re-aligns the
  daily periodic event ... on both new-game and load" (wrong for load on v1.4.8).

### Planning-time facts the executor relies on

- These 1.4.x blobs equal the 1.5.x parent `83bdad853^` (checked with `git rev-parse`):
  `TaomPregnancyModel.cs`, `TaomPregnancyModelTests.cs`, `initial_child_generation.json`,
  `race_age_config.json`, `docs/features/race-age-system.md`, `docs/features/initial-child-generation.md`.
  At the 1.5.x tip the first four equal `83bdad853`'s version, so no later fix exists. The six
  production classes `ShippedFertilityConfigTests` uses (`IPathService`, `CultureCreationDataProvider`,
  `CultureCreationData`, `InitialChildGenerationConfigProvider`, `RaceAgeConfigProvider`, `RaceAgeConfig`)
  are byte-identical on 1.4.x and at `925db38d1`.
- Cherry-pick simulation this session (`git merge-tree --write-tree --merge-base=<sha>^ origin/bannerlord-1.4.5 <sha>`):
  all six owned commits exit 1. `83bdad853` conflicts in 22 paths (howdah, skill-template, coop
  settings, ranged-rebalance docs and bookkeeping), none of them a #628 path. `925db38d1` conflicts in 32; its #628 paths
  (`ShippedFertilityConfigTests.cs`, `race-age-system.md`, `configs-balance.md`, the lessons) conflict
  only because they build on `83bdad853`. `573dc155f` conflicts in three War Chronicle docs (absent on
  1.4.x; its RCA hunk applies cleanly); `a66b5754a` in 11 paths (War Chronicle code, tests and docs,
  its ledger tool, `tools/README.md`, and the RCA paragraph it refines); `e8504a62b` in four lessons
  files; `ae1a6b553` in `.claude/rules/adapters.md` and `docs/ai-includes/orientation.md` (absent on
  1.4.x). Hence: no cherry-pick in this WP. Code arrives by path checkout where the blobs match, docs
  by hand with exact text.
- E3/E4 already log `[MissionDiag] OS: ... CLR: 4.0.30319.42000` (E3 line 32). That rules out the
  CoreCLR host (6.0.x) for that player; whether Mono reports the same value is UNVERIFIED, which is
  why Step B1 logs `RuntimeInformation.FrameworkDescription`. Server GC is NOT to be recommended
  anywhere (V01: three runtime hosts ship with v1.4.8 and the players' host is unknown).

### Engine members verified on the installed v1.4.8 DLLs (`pwsh -NoProfile -File tools/taom-src.ps1 path <Type>`)

| Member | Installed file:line | What it is |
|---|---|---|
| `TaleWorlds.SaveSystem.Save.SaveContext` | `SaveContext.cs:11` | `public class SaveContext : ISaveContext` |
| `private List<object> _childObjects;` / `private List<object> _childContainers;` / `private List<string> _strings;` | `:75` / `:79` / `:83` | the save's object, container and string lists |
| `private void CollectObjects()` | `:175` | BFS from `RootObject`; an overload `private void CollectObjects(object parent)` exists at `:225`, so bind with `Type.EmptyTypes` |
| `public bool Save(object target, MetaData metaData, out string errorMessage)` | `:328` | calls `CollectObjects()` at `:342`, then `CollectSaveDatas()` at `:346` |
| `SaveStringTo` / `GetStringSizeInBytes(string text) => 4 + Encoding.UTF8.GetByteCount(text)` | `:308-316` / `:318-321` | writes `(short)stringSizeInBytes`, so one string over 32,763 UTF-8 bytes wraps |
| `TaleWorlds.SaveSystem.Load.LoadContext` | `LoadContext.cs:9` | `public class LoadContext` |
| `private int _objectCount;` / `_stringCount` / `_containerCount` | `:11` / `:13` / `:15` | read from the save header at `:67-69` |
| `public bool Load(LoadData loadData, bool loadAsLateInitialize)` | `:54` | the only `Load` overload; `GC.Collect()` at `:135`, `:146`, `:168`, `:235` |
| `TaleWorlds.SaveSystem.LoadData` | `LoadData.cs:3` | `public class LoadData` (namespace `TaleWorlds.SaveSystem`) |
| `TaleWorlds.CampaignSystem.CampaignBehaviorDataStore` | `CampaignBehaviorDataStore.cs:8` | `internal class` |
| nested `internal class BehaviorSaveData : IDataStore` with `private Dictionary<string, object> _records` | `:10`, `:13` | one per behavior per save |
| `private readonly Dictionary<string, BehaviorSaveData> _behaviorDict;` | `:58` | keyed by `CampaignBehaviorBase.StringId` |
| `internal void SaveBehaviorData(CampaignBehaviorBase campaignBehavior)` | `:65-79` | new `BehaviorSaveData`, `SyncData`, then stored under `campaignBehavior.StringId` (`public readonly string`, `CampaignBehaviorBase.cs:5`) |
| `TaleWorlds.ObjectSystem.MBObjectBase` | `MBObjectBase.cs:9` | `public MBGUID Id` `:15`; `internal bool IsRegistered { get; private set; }` `:25`; `GetHashCode() => Id.GetHashCode()` `:79-82` |
| `TaleWorlds.ObjectSystem.MBGUID.InternalValue` | `MBGUID.cs:17` | `public uint InternalValue` |
| `TaleWorlds.Core.Game` | `Game.cs:77`, `:199-212`, `:216-224`, `:246-263` | `Current`; `SaveAux` (behaviors' `OnBeforeSave`, then `SaveManager.Save` at `:205`); `OnSaveCompleted` (`PrintStatus` `:218`, then `Common.MemoryCleanupGC()` `:223`); `Destroy` (`GameManager.OnGameEnd(this)` `:253`, `Common.MemoryCleanupGC()` `:263`) |
| `Campaign.CreateCampaignEvents()` | `Campaign.cs:1002-1026` | daily wait `:1004-1010`, hourly `:1012-1018`, quarter-hourly `:1019-1025`; called from `OnDataLoadFinished` (`:697`) at `:724`, which `DoLoadingForGameType` runs at `:1666` and `:1685`; a new campaign starts its clock at `CampaignStartTime` (`:1402`) |
| `MBCampaignEvent(CampaignTime triggerPeriod, CampaignTime initialWait)` | `MBCampaignEvent.cs:28-34` | `NextTriggerTime = CampaignTime.Now + InitialWait;` at `:32` |
| `DefaultCampaignTimeModel.CampaignStartTime` | `DefaultCampaignTimeModel.cs:7` | `Years(1084) + Weeks(WeeksInSeason) + Hours(9)`: elapsed days start at 09:00 |
| `ChangeKingdomAction.ApplyInternal` | `ChangeKingdomAction.cs:50-65`, `:76`, `:95-98`, `:161` | join/defection/create set `clan.Kingdom` at `:65`; every leave detail sets it null at `:76`; only `LeaveKingdom` moves fiefs (`:95-98`); `OnClanChangedKingdom` at `:161` |

v1.4.8 dump (behaviour, not signatures): `LoadCallbackInitializator.InitializeObjects` ends with
`GC.Collect()` (`Core/TaleWorlds.SaveSystem/TaleWorlds.SaveSystem.Load/LoadCallbackInitializator.cs:61`)
and `AfterInitializeObjects` too (`:100`); `Common.MemoryCleanupGC` is `GC.Collect()`
(`Core/TaleWorlds.Library/TaleWorlds.Library/Common.cs:242-246`). So between an old game's `OnGameEnd`
and the next game's `GameLoaded` at least two full collections run while the old game is no longer on
the stack, on both the in-game and the main-menu load path.

### Conventions that bind this work

- ADR-007 adapters: services never touch sealed TaleWorlds types. Hooks (entry points) read engine
  objects and hand plain values (counts, strings, a `Type`, a `uint` id) to the service and to pure
  `Domain` classes; the service holds an engine object only as `object` inside a `WeakReference`.
- ADR-002 thin entry points (every hook file under 150 lines; logic in `Domain`); ADR-003 no
  `#region`; ADR-004 no `[Obsolete]`; ADR-005 no `#if DEBUG`.
- TDD: failing test first (a compile error for a missing member counts), then implement; never commit red.
- `.claude/rules/harmony-patches.md`: read `docs/reviews/lessons/harmony-il.md` first; hooks live in
  `Main/Features/SaveLoadDiagnostics/Hooks/`; cache reflection at `Initialize`/`TargetMethod` time,
  never per call; every new target here runs on the main thread. MovementOrder rule: not applicable.
- Patch61 rules: every hook body is wrapped in try/catch and never throws; finalizers are void with
  `[HarmonyPriority(Priority.First)]`; a hook bound by name to an engine member gets its OWN category
  (Harmony stops a category at its first failing class).
- MCM json2 rule (rename a persisted setting, never flip its default): no MCM setting is added here.
- Markdown: no U+2014 or U+2013 on any line you add or change (`tools/lint_docs.py` counts a changed
  line as added); replace an existing dash on such a line with a comma, colon or parentheses, or
  `none` for an empty table cell.

### What other WPs bring or own

- WP104 (dependency): `Main/PatchCategoryApplier.cs`, `private bool TryPatchCategory(string category)`
  in `Main/SubModule.cs`, and `TAOM.Tests/Infrastructure/RepoPaths.cs` with `RepoPath(params string[])`
  and `ReadSource(string relativePath, bool stripComments = false)`.
- WP102 (D1): the hand-edited `CHANGELOG.md` is retired; no plan stages it.
- WP110 (same wave): ports `16a34027d` (`SyncDataChunker` replaces `MomentumSyncChunker`, SAVE-TIME-06)
  and the #764 code, but drops their lesson hunks as bookkeeping, so it edits no lessons file.
  `9ab3952a7`, which it also ports, touches only `.claude/rules/csharp-architecture.md`.
- WP112: the issue-792 `PartyBase.MainParty` series, including the `.claude/rules/adapters.md`
  paragraph. Its scope excludes `docs/reviews/lessons/*`, so `e8504a62b`'s MainParty lesson goes to
  WP142 (see Maintenance notes).
- WP142: syncs lessons last, including those WP110 dropped (the `16a34027d` hunk on the first lesson of
  `gamemodels-services.md` and the #764 lesson in `state-lifecycle-save.md`).
- WP107 (`5ee49d05f`, `2a3f71b12`, PatchShield's own per-call cost), WP118 (`1325c36ea`, ButterLib
  Distance Matrix), WP119 (`e99d705a3`, `a67792c40`, `4263535a1`, MEMORY-05 correctness fixes),
  WP139 (`cb4eeaa3d`, `[Session]` summary in `SaveManager_Save_Patch`), WP144 (the 1.5.x
  `save-system.md` "what a save costs" section).

## Commands you will need

Run from the worktree root. Use the Bash tool for every git command (D19). Python on the laptop is
`C:\Python314\python.exe` if `python` is not on PATH.

| Purpose | Command | Expected |
|---|---|---|
| Restore | `dotnet restore TAOM.sln` | exit 0 |
| Build | `dotnet build TAOM.sln -p:DisableModuleCopy=true -p:ModuleId=` | exit 0, 0 errors |
| Test | `dotnet test TAOM.Tests --no-build -p:DisableModuleCopy=true -p:ModuleId=` | 0 failed |
| Filtered | `dotnet test TAOM.Tests --no-build -p:DisableModuleCopy=true -p:ModuleId= --filter "FullyQualifiedName~<Class>"` | 0 failed |
| Data | `python tools/validate_moduledata.py` | no new ERROR vs the WP101 baseline in the main checkout's `plans/README.md` |
| Tool tests | `python -m unittest discover -s tools/tests -t .` | no new failure |
| Docs | `python tools/lint_docs.py` | no new finding in touched files |

Both `-p:` flags are mandatory on every build and test: on 1.4.x `Main/TAOM.csproj:133-136` does not
gate the CopyModule target, so a build without `-p:ModuleId=` deploys into the game install. Never
`./build.ps1`. Test-count baseline: the count the previous merged WP recorded in the main checkout's
`plans/README.md` (`C:/Users/mikew/source/repos/TAOM/plans/README.md`; read it, never edit it), plus the tests this plan adds (Part A 13, Part B 31, Part C 11; names in "Test plan").

## Scope

**In scope** (the only files you may modify):

- Part A: `Main/Features/RaceAge/Models/TaomPregnancyModel.cs`; `Main/_Module/ModuleData/configs/initial_child_generation.json`;
  `Main/_Module/ModuleData/raceage/race_age_config.json`; `TAOM.Tests/Features/RaceAge/TaomPregnancyModelTests.cs`;
  `TAOM.Tests/Features/RaceAge/ShippedFertilityConfigTests.cs` (new); `docs/features/race-age-system.md`;
  `docs/features/initial-child-generation.md`; `docs/modding/configs-balance.md`;
  `docs/reviews/rca-race-fertility-2026-09-19.md` (new); `docs/reviews/rca-wotr-phase-ordering-2026-07-30.md`;
  `docs/reference/engine/campaign-tick-time-and-party-ai.md`; `docs/reference/engine/campaignevents-and-campaignbehavior.md`;
  `docs/reference/engine/save-system.md`; `docs/reviews/lessons/{campaign-mechanics,data-content-cultures,state-lifecycle-save,gamemodels-services}.md`.
- Part B: `Main/SubModule.cs` (single-owner: ONLY the edits Steps B1, B2 and B3 name);
  `Main/Features/SaveLoadDiagnostics/ISaveLoadDiagnosticsService.cs`; `SaveLoadDiagnosticsService.cs`;
  `Domain/SaveLoadPhase.cs`; `Domain/SaveLoadTokens.cs` (new); `Domain/BehaviorRecordStats.cs` (new);
  `Hooks/SandBoxSaveHelper_TryLoadSave_Patch.cs`; `Hooks/SaveOutput_PrintStatus_Patch.cs`;
  `Hooks/SaveContext_CollectObjects_Patch.cs` (new); `Hooks/LoadContext_Load_Patch.cs` (new);
  `Hooks/CampaignBehaviorDataStore_SaveBehaviorData_Size_Patch.cs` (new); `Hooks/ContainerLoadData_Fill_Patch.cs`;
  `Hooks/HeaderLoadData_Readers_Patch.cs`; in `TAOM.Tests/Features/SaveLoadDiagnostics/`:
  `SaveLoadDiagnosticsServiceTests.cs`, `SaveLoadDiagnosticsBindingTests.cs`, and the new
  `SaveLoadTokensTests.cs`, `BehaviorRecordStatsTests.cs`, `SaveGrowthProbeWiringTests.cs`, `SaveLoadHotFinalizerTests.cs`.
- Part C: `Main/Features/SaveLoadDiagnostics/Domain/SaveGraphCensus.cs` (new); `Cheats/SaveCensusCheats.cs`
  (new); the Part B service, interface, phase and `SaveContext_CollectObjects_Patch.cs` files;
  `TAOM.Tests/Features/SaveLoadDiagnostics/SaveGraphCensusTests.cs` (new) plus the Part B test files;
  `tools/diff_save_census.py` (new); `tools/tests/test_diff_save_census.py` (new); `tools/README.md` (one row);
  `docs/investigations/save-growth-2026-11.md` (new); `docs/features/save-load-diagnostics.md`;
  `docs/features/dev-console.md`; `docs/reference/harmony-patch-registry.md`;
  `docs/reference/bannerlord-engine-and-toolchain.md` (one table row).

**Out of scope** (do not touch, even though they look related):

- `Main/IoC.cs`: nothing needs registering (the service is already a singleton). If a registration
  seems needed, STOP.
- `Dependencies/**` (SaveShield, PatchShield), every MCM settings file (`TaomSettings.cs`,
  `CoopSettingsRelevance.cs`, `SettingsFingerprintTests.cs`), any `SyncData` or save-format change.
- Any SAVE-TIME-01 fix: no dedupe, no object removal, no change to what a save holds (binding note).
- Patches on `ObjectSaveData.GetChildObjects` or `ContainerSaveData.GetChildObjects` (the "referrer"
  probe): deferred, see Maintenance notes.
- `CHANGELOG.md`, `CLAUDE.md`, `AGENTS.md`, `.claude/**`, `docs/INDEX.md`, `docs/reference/feature-map.md`,
  `docs/reviews/LESSONS-LEARNED.md`, `docs/reviews/REVIEW-LOG.md`.
- The howdah (#627), career-kit (#629) and skill-template (#626) parts of `83bdad853`/`925db38d1`; every
  War Chronicle file; `MarriageClanPoolCache` (an unowned lead, see Maintenance notes).
- Server GC: never recommend it, in code comments, docs or player advice.

## Git workflow

- Worktree from the main checkout (Bash):
  `git -C C:/Users/mikew/source/repos/TAOM worktree add C:/Users/mikew/source/repos/taom-port/114 -b port148/114-save-growth-investigation bannerlord-1.4.5`
  Work only in `C:/Users/mikew/source/repos/taom-port/114`. Never push, open a PR, tag or merge.
- No cherry-pick in this WP (see "Planning-time facts"). Code arrives by `git checkout <sha> <path>`
  only where the blob check of Step A1 passes; docs and lessons are written by hand from the exact
  text below. `git checkout <sha> <path>` also stages the file.
- Stage with explicit `git add <paths>`; never `git add -A`. Run `git status --porcelain` before every
  commit and confirm only the step's paths are staged.
- Commit with `git commit -F <msgfile>` in the NEXT sequential Bash call after staging, never in the
  same command, never as a parallel call, never through PowerShell. Put the message file outside the
  worktree.
- Message shape (CLAUDE.md "Commits", decisions brief): subject `<type>(<scope>): v2.0.28 - <description>`,
  at most 72 characters; body for 1.4.x players, wrapped at 72, no em or en dash, no Co-Authored-By
  or "Generated with" line; trailers `Research:`, `Not-tested:`, `Save-compat:` when relevant, and one
  `Ported-from: <40-hex sha>` line per included 1.5.x commit. If a hook rejects `v2.0.28`, read the
  `<Version value=...>` in `Main/_Module/SubModule.xml`, use it, and report the mismatch.
- Build and test after each commit; full suite at the end of each Part.

## Part A: port #628 and the daily-tick-on-load docs

### Step A1: prerequisite checks (read-only)

```
git rev-parse --abbrev-ref HEAD
grep -c 'Baseline after WP101' C:/Users/mikew/source/repos/TAOM/plans/README.md
grep -c 'public 2026-09-29' C:/Users/mikew/source/repos/TAOM/plans/README.md
grep -c 'public static string RepoPath(params string\[\] parts)' TAOM.Tests/Infrastructure/RepoPaths.cs
grep -c 'public static string ReadSource(string relativePath, bool stripComments = false)' TAOM.Tests/Infrastructure/RepoPaths.cs
grep -c 'private bool TryPatchCategory(string category)' Main/SubModule.cs
ls Main/Features/SaveLoadDiagnostics/*Module.cs 2>/dev/null | wc -l
for p in Main/Features/RaceAge/Models/TaomPregnancyModel.cs TAOM.Tests/Features/RaceAge/TaomPregnancyModelTests.cs Main/_Module/ModuleData/configs/initial_child_generation.json Main/_Module/ModuleData/raceage/race_age_config.json docs/features/race-age-system.md docs/features/initial-child-generation.md; do if [ "$(git rev-parse HEAD:$p)" = "$(git rev-parse 83bdad853^:$p)" ]; then echo "same $p"; else echo "DRIFT $p"; fi; done
test -e TAOM.Tests/Features/RaceAge/ShippedFertilityConfigTests.cs && echo EXISTS || echo absent
```

**Verify**: `port148/114-save-growth-investigation`; `1`; at least `1` (D20: WP101 recorded the public
2026-09-29 Armory); `1`; `1`; `1`; `0`; six `same` lines; `absent`. Any other result is a STOP.

### Step A2: #628, tests first (RED)

```
git checkout 83bdad853 TAOM.Tests/Features/RaceAge/TaomPregnancyModelTests.cs
git checkout 925db38d1 TAOM.Tests/Features/RaceAge/ShippedFertilityConfigTests.cs
dotnet restore TAOM.sln && dotnet build TAOM.sln -p:DisableModuleCopy=true -p:ModuleId=
dotnet test TAOM.Tests --no-build -p:DisableModuleCopy=true -p:ModuleId= --filter "FullyQualifiedName~TaomPregnancyModelTests|FullyQualifiedName~ShippedFertilityConfigTests"
```

`ShippedFertilityConfigTests` is taken at `925db38d1` because that commit corrected it (the "is a real
culture" check reads every culture in `cultures.json`).

**Verify**: build exit 0; exactly these 8 tests fail, everything else in the two classes passes:
`ComputeBaseChance_NpcClanPastCap_RaceBonusDropped`, `ComputeBaseChance_NpcClanPastCap_BonusRaceNeverBeatsHumanRate`,
`ComputeBaseChance_NpcClanOneLordPastCap_RaceBonusDropped`, `ComputeBaseChance_PlayerOrSpouseInvolved_RaceBonusClamped`,
`ShippedRaceAgeConfig_EveryRace_FertilityModAtMostCeiling`, `ShippedRaceAgeConfig_OrcFamilyRaces_FertileNoLaterThanVanillaEnd`,
`ShippedRaceAgeConfig_Human_FertileNoLaterThanSixty`, `ShippedInitialChildGeneration_EveryOrcCulture_Excluded`.
(Planning-time reading of the tests against the 1.4.x code and data; a different failing set is a STOP.)

### Step A3: #628 code, data and docs (GREEN), then commit

1. Transplant the files whose 1.4.x blob matched in Step A1, plus the two new docs:

```
git checkout 83bdad853 Main/Features/RaceAge/Models/TaomPregnancyModel.cs Main/_Module/ModuleData/configs/initial_child_generation.json Main/_Module/ModuleData/raceage/race_age_config.json
git checkout 925db38d1 docs/features/race-age-system.md docs/features/initial-child-generation.md docs/reviews/rca-race-fertility-2026-09-19.md
```

2. `docs/modding/configs-balance.md` diverged on 1.4.x elsewhere, so edit two lines by hand. Replace the
   line that starts `The shipped file holds **15 race entries**.` (1.4.x line 183) with:

```
The shipped file holds **15 race entries**. <!-- measured: python json key count over races in race_age_config.json 2026-09-05 --> The "Current Race Values" table in [race-age-system](../features/race-age-system.md) was refreshed from the file on 2026-09-19 (#628); if the two disagree, the file wins. The shipped `human` row is `maxAge` 200, `becomeOld` 170, `middleAge` 100, `fertilityEnd` 60, and **every one of the 15 races ships `comesOfAge` 18**. No race may ship a `fertilityMod` above 1.5 (`ShippedFertilityConfigTests`), and a bonus above 1.0 applies only while a clan is at or under its population cap (`TaomPregnancyModelTests`). <!-- measured: python json dump of races in race_age_config.json 2026-09-19 -->
```

   and the `excluded_cultures` table row (1.4.x line 195) with:

```
| `excluded_cultures` | string[] | no | empty | Culture ids skipped entirely. Ships with `mordor`, `isengard`, `gundabad`, `dolguldur`, `goblin`, `mistymountainorcs`, `bluecraig` (every orc culture; #628) | file contents |
```

3. In `docs/reviews/rca-race-fertility-2026-09-19.md`, insert after line 1 (the title) and a blank line,
   with `<date>` = the output of `date +%F`:

```
> **1.4.x line:** ported on <date> to Bannerlord v1.4.8 from 1.5.x `83bdad853` and `925db38d1`. The
> code, data and tests are unchanged from 1.5.x. The fix binds no engine member, so the 1.5.3 engine
> checks below needed no re-run. The seven causes are listed in the port commit's body (this line no
> longer keeps a hand-edited CHANGELOG).
```

4. In `docs/features/race-age-system.md`, in `## Changelog`, insert as the first list item:

```
- <date>: the #628 fix below reached the 1.4.x line (Bannerlord v1.4.8) unchanged from 1.5.x.
```

5. Append the two #628 lessons (D18: plain appends, no `merge=union`). At the END of
   `docs/reviews/lessons/campaign-mechanics.md` add a blank line and:

```
### A multiplier applied after a vanilla brake cancels the brake; decide which one wins
`TaomPregnancyModel.ComputeBaseChance` reproduced vanilla's pregnancy chance, clan population brake included (`min(1, (2*cap - aliveLords)/cap)`), then multiplied by the race `fertilityMod`. At x2 an orc clan at 1.5x its cap (brake 0.5) bred at exactly vanilla's unbraked rate, so orc clans ran to the `2*cap` ceiling and stayed there (#628). Nothing errored; the numbers only looked generous.
- **Why missed:** "vanilla formula, then times the race factor" reads as a neutral composition. A brake that works by scaling toward zero is undone by any factor above 1 over half its range, and nobody asked what the multiplier did to it.
- **Prevent:** when a TAOM factor multiplies a vanilla result that already contains a limiter (a cap, a population brake, a clamp), decide which one wins and encode it. Here a bonus above 1.0 applies only when an NPC clan is at or under its cap (`aliveLords <= clanCap`); past it the modifier is clamped to 1, and a penalty below 1.0 applies everywhere. Pin the property as a test ("a bonus race never out-breeds the human rate at the same fill"), not only point values.
- **Source:** #628, `docs/reviews/rca-race-fertility-2026-09-19.md` item A.
```

   In `docs/reviews/lessons/data-content-cultures.md`, directly after the line
   ``- **Source:** the 2026-08-29 goblin tree merge, caught by `generate_clan_heraldry.py`'s own drift gate refusing to regenerate.``
   insert:

```
- **Second instance (#628, 2026-09-19):** `initial_child_generation.json` `excluded_cultures` listed the four orc cultures that existed when it was written; `goblin`, `mistymountainorcs` and `bluecraig` came later and were never added, so every one of their clans started the campaign topped up with children. Now gated both ways by `ShippedFertilityConfigTests`: the orc cultures are derived from `cultures.json` and must all be excluded, and every excluded id must name a real culture. `docs/reviews/rca-race-fertility-2026-09-19.md` item B.
```

6. Build, run the two classes, the full suite and the data validator:

```
dotnet build TAOM.sln -p:DisableModuleCopy=true -p:ModuleId=
dotnet test TAOM.Tests --no-build -p:DisableModuleCopy=true -p:ModuleId= --filter "FullyQualifiedName~TaomPregnancyModelTests|FullyQualifiedName~ShippedFertilityConfigTests"
dotnet test TAOM.Tests --no-build -p:DisableModuleCopy=true -p:ModuleId=
python tools/validate_moduledata.py
python tools/lint_docs.py
```

**Verify**: 0 errors; the two classes pass (6 new `ComputeBaseChance_*` tests plus 7
`ShippedFertilityConfigTests`); full suite 0 failed, total = baseline + 13; validator shows no new
ERROR vs the WP101 baseline; lint reports nothing new in the touched files.

7. Stage and commit (two sequential Bash calls):

```
git add Main/Features/RaceAge/Models/TaomPregnancyModel.cs Main/_Module/ModuleData/configs/initial_child_generation.json Main/_Module/ModuleData/raceage/race_age_config.json TAOM.Tests/Features/RaceAge/TaomPregnancyModelTests.cs TAOM.Tests/Features/RaceAge/ShippedFertilityConfigTests.cs docs/features/race-age-system.md docs/features/initial-child-generation.md docs/modding/configs-balance.md docs/reviews/rca-race-fertility-2026-09-19.md docs/reviews/lessons/campaign-mechanics.md docs/reviews/lessons/data-content-cultures.md && git status --porcelain
```

Commit message:

```
fix(race-age): v2.0.28 - orcs and goblins overbreed (#628)

Orc and goblin clans no longer fill to twice their population cap.
The race fertility bonus now applies only while a clan is at or under
its cap and never to a marriage that involves the player; a penalty
(elves, dwarves, trolls) still applies everywhere. Orc-kin fertility
drops from 2.0-3.0x to 1.3-1.5x, orcs and berserkers are fertile to 45
(was 50) and humans to 60 (was 195). Goblin, Misty Mountain and Blue
Craig clans no longer start a new campaign topped up with children,
like the other orc cultures. Existing saves keep the children they
have; the new rates apply after a full restart of the game.

Research: the code, config and tests on this line were byte-identical
to the 1.5.x parent of 83bdad853; the fix binds no engine member.
Not-tested: in game (clan sizes need a long campaign to show).
Save-compat: no save format change.
Ported-from: 83bdad8537b9760deb6cd849c7ced8943a5f5a37
Ported-from: 925db38d14a55e8851b7aeabd7e1a3627fe9dc75
```

**Verify**: `git log -1 --format=%s` prints `fix(race-age): v2.0.28 - orcs and goblins overbreed (#628)`.

### Step A4: correct the daily-tick-on-load docs for v1.4.8, then commit

No code. Every citation below was read on the installed v1.4.8 DLLs at planning time.

1. `docs/reviews/rca-wotr-phase-ordering-2026-07-30.md`: after the line `risk that Day 2/14 did not.`
   and the blank line that follows it, insert this paragraph and one blank line:

```
**Correction (found on the 1.5.x line 2026-10-14; re-checked on v1.4.8 2026-11-04):** the load half
of that claim is wrong. On a load, `Campaign.CreateCampaignEvents` (v1.4.8 `Campaign.cs:1002-1026`,
run from `OnDataLoadFinished` at `:724`) sets the daily event's initial wait to the elapsed part of the
current day, not to the rest of it (`:1004-1010`), and `MBCampaignEvent` sets `NextTriggerTime` to now
plus that wait (`MBCampaignEvent.cs:32`). A save made at fraction f of elapsed day S next ticks at
S + 2f, so the daily event leaves the elapsed-day boundary after a load part way through a day, and a
save made in the first half of elapsed day S ticks day S a second time. Elapsed days start at
`CampaignStartTime` (09:00 on vanilla's calendar clock, `DefaultCampaignTimeModel.cs:7`), not at
midnight. The new-game half holds: a new campaign starts its clock at `CampaignStartTime`
(`Campaign.cs:1402`) and waits one whole day. The 1.4.x War of the Ring daily handlers
(`WarOfTheRingBehavior.cs:38`, `WarOfTheRingMomentumBehavior.cs:83`) have no once-per-day guard;
whether each is safe to run twice for one day is not audited yet (#793).
```

2. `docs/reference/engine/campaign-tick-time-and-party-ai.md`: in the line
   `rate or fast-forward), so behavior is deterministic in game-time, not wall-time. Staggering party-AI ticks`
   change `rate or fast-forward)` to `rate or fast-forward; a load can repeat a day, see the gotchas below)`.
   Then replace the two-line bullet that starts `- **Periodic events fire on game-time boundaries, not real-time**`
   (it ends `day even at FastForward; don't assume frame cadence.`) with:

```
- **Periodic events fire on game-time boundaries, not real-time**: a `DailyTickEvent` handler runs once per elapsed
  campaign day even at FastForward, but not across a load. On every load `Campaign.CreateCampaignEvents` (v1.4.8
  `Campaign.cs:1002-1026`, run from `OnDataLoadFinished` at `:724`) gives the daily event an initial wait equal to
  the part of the day already elapsed (`:1004-1010`), and `MBCampaignEvent` schedules it at `Now + InitialWait`
  (`MBCampaignEvent.cs:32`). A save made at fraction f of elapsed day S therefore next ticks at S + 2f. Day S runs
  again when f is under a half (09:00 to 21:00 on the game clock, since elapsed days count from `CampaignStartTime`,
  09:00, `DefaultCampaignTimeModel.cs:7`), later ticks keep that offset, and repeated reloads can skip a day. Only
  the hourly event follows that S + 2f shape, on the hour (`:1012-1018`). The quarter-hourly event (`:1019-1025`)
  takes the elapsed fraction g of the current quarter but counts it in hours, so its first wait is 4g quarters, up
  to four periods: it next fires at Q + 5g quarters, runs quarter Q again only when g is under a fifth, and can skip
  up to four ticks. With no remainder (a save exactly on a boundary) the hourly and quarter-hourly first waits are
  half a period (0.5 and 0.125 hours), while the daily wait is a full day. Key once-a-day work on a saved last-run
  day: store the last elapsed day the handler ran with its state and skip a repeat. TAOM's daily listeners are not
  audited for this yet (#793). Read from the managed source; the in-game check is owed (#793).
```

3. `docs/reference/engine/campaignevents-and-campaignbehavior.md`: replace the line
   ``(e.g. `DailyTickEvent` fired once per campaign day).`` with:

```
(e.g. `DailyTickEvent`, once per campaign day, except that a load can repeat the save's day:
[campaign-tick-time-and-party-ai.md](campaign-tick-time-and-party-ai.md)).
```

4. `docs/reference/engine/save-system.md`: directly after the bullet that ends
   ``Module.Initialize**, not a soft failure.`` insert:

```
- **One `SyncData` string over 32,763 UTF-8 bytes corrupts the save at write time.** v1.4.8
  `SaveContext.SaveStringTo` writes the entry length as `(short)(4 + UTF-8 bytes)` (`SaveContext.cs:308-321`,
  `GetStringSizeInBytes`), so a longer string wraps and the save no longer loads (the v2.0.9 momentum incident,
  `docs/reviews/rca-momentum-save-corruption-2026-07-07.md`). Split any payload that can grow into chunks of at
  most 10,000 characters, as `WarOfTheRingMomentumBehavior.SyncData` does. Lesson:
  `docs/reviews/lessons/gamemodels-services.md`.
```

   (Name the behavior, not its helper class: WP110 replaces `MomentumSyncChunker` with `SyncDataChunker`.)

5. Lessons (D18: hand edits, no `merge=union`).
   a. At the END of `docs/reviews/lessons/state-lifecycle-save.md` add a blank line and:

```
### After a load, the daily tick can repeat the save's own day: guard once-a-day work by day (#793)
On every load, v1.4.8 `Campaign.CreateCampaignEvents` (`Campaign.cs:1004-1010`, run from `OnDataLoadFinished` at
`:724`) sets the daily event's first wait to the part of the current day already elapsed, and `MBCampaignEvent`
schedules it at now plus that wait (`MBCampaignEvent.cs:32`). A save made a fraction f into elapsed day S next ticks
at S + 2f. So a save in the first half of an elapsed day (09:00 to 21:00 on vanilla's calendar clock, since elapsed
days start at `CampaignStartTime`) runs day S's daily handlers twice, and repeated reloads can skip a day.
- **Why missed:** `rca-wotr-phase-ordering-2026-07-30.md`, checked against v1.4.7, said the event re-aligns on
  load (corrected on this line 2026-11-04).
- **Prevent:** a daily handler that must not run twice records the last elapsed day it ran, saves it with its
  state, and skips a repeat. Describe the boundary in elapsed days, never in calendar hours. TAOM's daily
  listeners are not audited yet (#793).
- **Source:** #793; found on the 1.5.x line (`e8504a62b`), re-checked on the installed v1.4.8 DLLs.
```

   b. Conditional: run `grep -n 'reset in the last lesson of this file\|See the constructor reset in the' docs/reviews/lessons/state-lifecycle-save.md`.
   If it prints nothing (the expected case: WP110 drops the #764 lesson and WP142 brings it later), skip
   this item. If it prints lines (the #764 lesson is present, and your new lesson now follows it), replace in each printed line the words `the last lesson of this file`
   with `"<the #764 lesson's heading, copied from the file>" (#764) near the end of this file` (on
   1.5.x that heading reads "Reset a per-campaign singleton in the constructor of a behavior built per
   campaign, not in a campaign event"). For the two-line form ending `See the constructor reset in the`
   / `last lesson of this file.`, join it into one line. Report whether this item ran.
   c. At the END of `docs/reviews/lessons/campaign-mechanics.md` (after the #628 lesson from Step A3)
   add a blank line and:

```
### A clan that changes kingdom moves its fiefs with no owner-change event
`ChangeKingdomAction` join, defection (`JoinKingdomByDefection`) and create set `clan.Kingdom` (v1.4.8
`ChangeKingdomAction.cs:65`), and every leave detail, rebellion (`LeaveWithRebellion`) included, sets it to null
(`:76`). The clan keeps its settlements, so each fief's map faction changes without
`OnSettlementOwnerChangedEvent`. Only a plain `LeaveKingdom` hands the fiefs over through
`ChangeOwnerOfSettlementAction` (`:95-98`); every detail raises `OnClanChangedKingdom` (`:161`).
- **Why missed:** a design that takes one engine event as the ownership signal misses these moves (found by the
  1.5.x War Chronicle review, finding C02).
- **Prevent:** treat the owner event as a fast path, and re-check the owner of every watched settlement in the
  daily step, or also listen to `OnClanChangedKingdom`.
- **Source:** 1.5.x `e8504a62b` (fixed there in `7e8451555`); re-checked on the installed v1.4.8 DLLs.
```

   d. `docs/reviews/lessons/gamemodels-services.md`, first lesson only (32,767 is wrong; the limit is
   32,763 because the length field stores 4 plus the UTF-8 bytes as a signed short):
   - Replace the heading line with
     `### A single SyncData string must stay at or under 32,763 UTF-8 bytes: chunk any unbounded or growing string payload`
   - In the paragraph line under it: change `exceeds 32,767 bytes` to
     ``exceeds 32,763 bytes (the entry stores 4 plus the UTF-8 bytes as a signed short; v1.4.8 `SaveContext.SaveStringTo`, `GetStringSizeInBytes`, `SaveContext.cs:308-321`)``.
     That line holds long dashes (U+2014 or U+2013) at three places and lint flags a changed line, so also:
     put `a signed int16 truncation` in parentheses in place of the dash pair around it; write the
     range before `65,535` as `32,768 to 65,535`; put the clause that starts `up to 100 events/type`
     and ends `full localized description` in parentheses in place of its dash pair. Leave the chunker
     class name on that line as it is.
   - In the `- **Prevent:**` line: replace the parenthesis that starts
     `(the engine handles many small entries fine` and ends `is fatal)` with
     `(the engine handles many small entries fine; only a *single* string over 32,763 B is fatal)`.

6. Check, then commit:

```
python tools/lint_docs.py
git diff -U0 -- docs/reviews/rca-wotr-phase-ordering-2026-07-30.md docs/reference/engine docs/reviews/lessons/state-lifecycle-save.md docs/reviews/lessons/campaign-mechanics.md | grep '^+' | grep -E 'v1\.5\.|WarChronicle|SyncDataChunker'
grep -c '32,763' docs/reviews/lessons/gamemodels-services.md; grep -c '32,767' docs/reviews/lessons/gamemodels-services.md
git add docs/reviews/rca-wotr-phase-ordering-2026-07-30.md docs/reference/engine/campaign-tick-time-and-party-ai.md docs/reference/engine/campaignevents-and-campaignbehavior.md docs/reference/engine/save-system.md docs/reviews/lessons/state-lifecycle-save.md docs/reviews/lessons/campaign-mechanics.md docs/reviews/lessons/gamemodels-services.md && git status --porcelain
```

**Verify**: lint reports nothing new in the touched files (it also catches any long dash on an added
or changed line); the first grep prints nothing; the counts are `3` (heading, paragraph and
`Prevent` lines) and `0`; `git status` shows only those seven paths staged.

Commit message:

```
docs(engine): v2.0.28 - a load can repeat the save's day (#793)

Corrects the engine notes for Bannerlord v1.4.8: after a load the
daily tick waits only the part of the day already played, so a save
made in the first half of a day runs that day's daily events twice.
The War of the Ring phase-ordering RCA said the opposite and now
carries a correction. The notes also record that one SyncData string
over 32,763 UTF-8 bytes corrupts a save (the lesson said 32,767), and
that a clan changing kingdom moves its fiefs without an owner-change
event.

Research: taom-src v1.4.8 Campaign.CreateCampaignEvents (:1002-1026),
MBCampaignEvent ctor (:28-34), DefaultCampaignTimeModel (:7),
SaveContext.SaveStringTo and GetStringSizeInBytes (:308-321),
ChangeKingdomAction.ApplyInternal (:65, :76, :95-98, :161).
Not-tested: the repeated daily tick in game (#793).
Ported-from: 573dc155fb5f6e5fe1c39e8d8d46c5cf0770ec3e
Ported-from: a66b5754a69db17062a7987834a95338daa00aa7
Ported-from: e8504a62b63be99a3843670b546e4f1faafd7954
Ported-from: ae1a6b55327d8777725fddcda91263eeab4b434b
```

### Part A done criteria

- [ ] `git log --format=%s bannerlord-1.4.5..HEAD` lists exactly the two Part A subjects.
- [ ] Build exit 0; full suite 0 failed, total = baseline + 13.
- [ ] `grep -c '"goblin"\|"mistymountainorcs"\|"bluecraig"' Main/_Module/ModuleData/configs/initial_child_generation.json` prints `3`.
- [ ] `grep -c 'Correction (found on the 1.5.x line' docs/reviews/rca-wotr-phase-ordering-2026-07-30.md` prints `1`.
- [ ] `python tools/validate_moduledata.py`: no new ERROR vs the WP101 baseline.
- [ ] `git status --porcelain` is empty.

## Part B: always-on probes (new work, TDD)

All lines go through `ISaveLoadDiagnosticsService` so `seq=`/`t=` stay coherent. New `SaveLoadPhase`
values are appended at the END of the enum under one comment:

```
    // Save-growth probes (plan 114): Runtime is one line at module load naming the .NET runtime and
    // GC mode; LoadCounts is what a load read (LoadContext.Load); SaveCollected is what a save is about
    // to write, right after the engine's object walk; SaveBehaviorData is the per-behavior SyncData
    // size summary from the save's OnBeforeSave pass; SaveCensus is the per-type census.
    Runtime,
```

(Step B2 appends `LoadCounts,` and `SaveCollected,`; Step B3 `SaveBehaviorData,`; Step C1 `SaveCensus,`.)

### Step B1: process context on load and save lines (counts, memory, previous campaign, runtime)

**RED.** Create the tests first (names exact; full list in "Test plan"):

1. `TAOM.Tests/Features/SaveLoadDiagnostics/SaveLoadTokensTests.cs` (5 tests) for a new pure class
   `TAOM.Features.SaveLoadDiagnostics.Domain.SaveLoadTokens`:
   - `LoadRequested("S", inGame: true, loads: 2, inGameLoads: 1, memory: "gc=1/2/3")` returns
     `name='S' inGame=true loadsThisProcess=2 inGameLoads=1 gc=1/2/3`.
   - `ProcessContext(3, 2, PreviousCampaignState.None, 0, "gc=x")` returns
     `loadsThisProcess=3 inGameLoads=2 prevCampaign=none gc=x` (no `gen2Since` token).
   - `ProcessContext(3, 2, PreviousCampaignState.Collected, 4, "gc=x")` returns
     `loadsThisProcess=3 inGameLoads=2 prevCampaign=collected gen2Since=4 gc=x`; `Alive` prints `prevCampaign=alive`.
   - `RuntimeHost("Mono 6.12", "?", null, "Interactive", null)` returns
     `framework='Mono 6.12' clr=? serverGC=? latency=Interactive is64Bit=?` (unknown values print `?`,
     never `0` or `false`).
2. In `SaveLoadDiagnosticsServiceTests.cs` first update every existing call mechanically:

```
sed -i -E 's/BeginLoadAttempt\("([^"]*)"\)/BeginLoadAttempt("\1", inGame: false)/g' TAOM.Tests/Features/SaveLoadDiagnostics/SaveLoadDiagnosticsServiceTests.cs
grep -n 'BeginLoadAttempt(' TAOM.Tests/Features/SaveLoadDiagnostics/SaveLoadDiagnosticsServiceTests.cs | grep -v 'inGame'
```

   (the grep must print nothing), then add 5 tests using the test seam
   `new SaveLoadDiagnosticsService(logger, () => "gc=1/2/3", () => 7)`:
   `BeginLoadAttempt_InGame_CountsLoadAndInGameLoad` (two calls, the second `inGame: true`: line
   carries `loadsThisProcess=2 inGameLoads=1`, `LoadsThisProcess == 2`, `InGameLoadsThisProcess == 1`);
   `BeginLoadAttempt_FromMenu_CountsLoadOnly`; `BeginSaveAttempt_StampsProcessContextAfterTheName`
   (`phase=SaveBegin name='x' loadsThisProcess=`); `NoteGameEnded_ObjectCollected_ProcessContextSaysCollected`
   (call `NoteGameEnded(new object())` from a `[MethodImpl(MethodImplOptions.NoInlining)]` helper, then
   `GC.Collect(); GC.WaitForPendingFinalizers(); GC.Collect();`, expect `prevCampaign=collected gen2Since=0`);
   `NoteGameEnded_ObjectStillReferenced_ProcessContextSaysAlive` (hold the object, `GC.KeepAlive` it at the end).
3. `TAOM.Tests/Features/SaveLoadDiagnostics/SaveGrowthProbeWiringTests.cs` (3 tests) reading
   `RepoPaths.ReadSource("Main/SubModule.cs", stripComments: true)` (`using TAOM.Tests.Infrastructure;`):
   `SubModule_OnGameEnd_NotesTheEndedCampaign` (contains `.NoteGameEnded(endedCampaign)`),
   `SubModule_StampSaveLoadPhase_UsesProcessContext` (contains `saveLoad.LogPhase(phase, saveLoad.ProcessContext());`),
   `SubModule_Patch61Block_LogsTheRuntimeLine` (contains `SaveLoadPhase.Runtime` and `SaveLoadTokens.RuntimeHost()`).

**Verify RED**: build fails on the missing `SaveLoadTokens`, `PreviousCampaignState`, the two-argument
`BeginLoadAttempt`, `LoadsThisProcess`, `InGameLoadsThisProcess`, `ProcessContext`, `NoteGameEnded`
and the 3-argument constructor. No other compile error.

**GREEN.**

1. `Domain/SaveLoadTokens.cs`: `internal enum PreviousCampaignState { None, Collected, Alive }` and
   `internal static class SaveLoadTokens` with `LoadRequested(string saveName, bool inGame, int loads, int inGameLoads, string memory)`,
   `ProcessContext(int loads, int inGameLoads, PreviousCampaignState prev, int gen2Since, string memory)`,
   `RuntimeHost(string framework, string clr, bool? serverGc, string latency, bool? is64Bit)` (formats
   exactly as the tests say, lower-case `true`/`false`, `?` for null; trim trailing spaces when
   `memory` is empty) and a parameterless `RuntimeHost()` that reads, each in its own try/catch,
   `System.Runtime.InteropServices.RuntimeInformation.FrameworkDescription`, `Environment.Version`,
   `System.Runtime.GCSettings.IsServerGC`, `GCSettings.LatencyMode`, `Environment.Is64BitProcess`
   (all compile on 1.4.x: see `Main/Features/CrashReport/Collectors/ProcessEnvironmentCollector.cs:72`
   and `Main/Features/EditorCacheRebuild/RuntimeCacheRebuildService.cs:211`).
2. `ISaveLoadDiagnosticsService`: change `BeginLoadAttempt(string saveName)` to
   `BeginLoadAttempt(string saveName, bool inGame)` and add, each with a one-line why-comment:
   `int LoadsThisProcess { get; }`, `int InGameLoadsThisProcess { get; }`, `string ProcessContext();`,
   `void NoteGameEnded(object endedCampaign);`.
3. `SaveLoadDiagnosticsService`, target shape:

```
    private readonly Func<string> _memoryTokens;
    private readonly Func<int> _gen2Count;
    private int _loadsThisProcess;
    private int _inGameLoadsThisProcess;
    private WeakReference? _endedCampaign;
    private int _gen2AtGameEnd;

    public SaveLoadDiagnosticsService(IModLogger logger)
        : this(logger, BattleLoadDiagnostics.ProcessMemoryTokens.Format, () => GC.CollectionCount(2)) { }

    // Test seam. Internal so DryIoc sees a single public constructor (MemoryStationSampler precedent).
    internal SaveLoadDiagnosticsService(IModLogger logger, Func<string> memoryTokens, Func<int> gen2Count)
    { _logger = logger; _memoryTokens = memoryTokens; _gen2Count = gen2Count; }

    public int LoadsThisProcess => Volatile.Read(ref _loadsThisProcess);
    public int InGameLoadsThisProcess => Volatile.Read(ref _inGameLoadsThisProcess);

    public void BeginLoadAttempt(string saveName, bool inGame)
    {
        int loads = Interlocked.Increment(ref _loadsThisProcess);
        int inGameLoads = inGame ? Interlocked.Increment(ref _inGameLoadsThisProcess) : Volatile.Read(ref _inGameLoadsThisProcess);
        BeginAttempt(SaveLoadPhase.LoadRequested, SaveLoadTokens.LoadRequested(saveName, inGame, loads, inGameLoads, SafeMemory()));
    }

    public void BeginSaveAttempt(string saveName) =>
        BeginAttempt(SaveLoadPhase.SaveBegin, $"name='{saveName}' {ProcessContext()}");

    public string ProcessContext()
    {
        try
        {
            var handle = Volatile.Read(ref _endedCampaign);
            var state = handle == null ? PreviousCampaignState.None
                : handle.IsAlive ? PreviousCampaignState.Alive : PreviousCampaignState.Collected;
            int gen2Since = handle == null ? 0 : _gen2Count() - Volatile.Read(ref _gen2AtGameEnd);
            return SaveLoadTokens.ProcessContext(LoadsThisProcess, InGameLoadsThisProcess, state, gen2Since, SafeMemory());
        }
        catch { return string.Empty; }
    }

    public void NoteGameEnded(object endedCampaign)
    {
        try
        {
            if (endedCampaign == null) return;
            Volatile.Write(ref _gen2AtGameEnd, _gen2Count());
            Volatile.Write(ref _endedCampaign, new WeakReference(endedCampaign));
        }
        catch { /* the diagnostic must never propagate */ }
    }

    private string SafeMemory() { try { return _memoryTokens(); } catch { return string.Empty; } }
```

   Change the private `BeginAttempt(SaveLoadPhase phase, string saveName)` to take the full detail
   (`string detail`) and log `Stamp(phase, detail)`; its resets stay as they are.
4. `Hooks/SandBoxSaveHelper_TryLoadSave_Patch.cs`: call `svc.BeginLoadAttempt(saveInfo?.Name ?? "<null>", Game.Current != null);`
   (`using TaleWorlds.Core;`). A non-null `Game.Current` means the player loaded from inside a running
   game; `Game.Destroy` sets it null (`Game.cs:246-263`), so the main menu reads null.
5. `Hooks/SaveOutput_PrintStatus_Patch.cs`: the success stamp becomes
   `svc.LogPhase(SaveLoadPhase.SaveCompleted, "result=Success " + svc.ProcessContext());` and the
   failure detail gets ` {svc.ProcessContext()}` appended. (This runs before the engine's own
   `Common.MemoryCleanupGC()` in `Game.OnSaveCompleted`, `Game.cs:218-223`.)
6. Append `Runtime,` to `SaveLoadPhase` (block shown above Step B1).
7. `Main/SubModule.cs`, three edits, nothing else:
   - In the Patch61 block, directly after `var saveLoadLogger = IoC.Resolve<IModLogger>();` insert:

```
            saveLoadDiagnostics.LogPhase(Features.SaveLoadDiagnostics.Domain.SaveLoadPhase.Runtime,
                Features.SaveLoadDiagnostics.Domain.SaveLoadTokens.RuntimeHost());
```

   - At the end of `OnGameEnd`, after the ArmyTargeting try/catch, insert:

```
        // Plan 114 (MEMORY-05): remember, weakly, the campaign that just ended, so the next load's
        // GameLoaded/GameInitializationFinished stamps and later saves can say whether it was freed.
        try
        {
            if (game?.GameType is Campaign endedCampaign)
                IoC.Resolve<Features.SaveLoadDiagnostics.ISaveLoadDiagnosticsService>()?.NoteGameEnded(endedCampaign);
        }
        catch { /* diagnostic is best-effort, never break OnGameEnd */ }
```

   - In `StampSaveLoadPhase`, replace the two-line `IoC.Resolve<...>().LogPhase(phase, Features.BattleLoadDiagnostics.ProcessMemoryTokens.Format());` with:

```
            var saveLoad = IoC.Resolve<Features.SaveLoadDiagnostics.ISaveLoadDiagnosticsService>();
            saveLoad.LogPhase(phase, saveLoad.ProcessContext());
```

**Verify GREEN**: build 0 errors; `--filter "FullyQualifiedName~TAOM.Tests.Features.SaveLoadDiagnostics"`
0 failed (all old tests still pass); full suite 0 failed.

Commit `feat(diagnostics): v2.0.28 - saves and loads name the process context`, staging the interface,
service, `Domain/SaveLoadPhase.cs`, `Domain/SaveLoadTokens.cs`, the two hooks, `Main/SubModule.cs` and
the three test files. Body, player-readable: save and load lines in taom_debug now say how many saved
games this session has loaded, whether a load started inside a running game, the process memory at the
start and end of every save, and whether the campaign from before the last load has been freed; the
game's start logs which .NET runtime and GC mode it runs on; nothing changes in play. Trailers:
`Research: taom-src v1.4.8 Game (Current :77, Destroy :246-263, OnSaveCompleted :216-224)` and
`Not-tested: the new tokens in a live save and load`.

### Step B2: object and container counts at save and load

**RED.**
- `SaveLoadTokensTests`: `SaveCollected_FormatsCountsAndCollectMs` (`SaveCollected(10, 20, 30, 4601)` ->
  `objects=10 containers=20 strings=30 collectMs=4601`), `SaveCollected_UnreadCount_IsOmittedNotZero`
  (`SaveCollected(null, 20, null, 5)` -> `containers=20 collectMs=5`), `LoadCounts_FormatsCounts`
  (`LoadCounts(1, 2, 3)` -> `objects=1 containers=2 strings=3`; all null -> `counts=<unavailable>`).
- `SaveLoadDiagnosticsBindingTests` (`[TestCategory("BindingVerification")]`, same Inconclusive guard as
  the existing tests, types via `AccessTools.TypeByName`):
  `SaveContext_CollectObjectsAndCountFields_ResolveAgainstInstalledEngine`: on
  `TaleWorlds.SaveSystem.Save.SaveContext`, `AccessTools.Method(type, "CollectObjects", Type.EmptyTypes)`
  is not null, and fields `_childObjects`/`_childContainers` have `FieldType == typeof(List<object>)`,
  `_strings` `typeof(List<string>)`.
  `LoadContext_LoadAndCountFields_ResolveAgainstInstalledEngine`: `LoadContext.Load` with parameter types
  `TaleWorlds.SaveSystem.LoadData` and `bool` resolves; `_objectCount`, `_stringCount`, `_containerCount`
  are `int` fields.
- `SaveGrowthProbeWiringTests`: `SubModule_AppliesAndInitializesTheCountHooks` (contains
  `"Patch61_SaveLoadDiagnostics_SaveGraph",`, `"Patch61_SaveLoadDiagnostics_LoadCounts",`,
  `SaveContext_CollectObjects_Patch.Initialize(` and `LoadContext_Load_Patch.Initialize(`) and
  `CountHookCategoryAttributes_MatchTheStringsSubModuleApplies` (each new hook's `[HarmonyPatchCategory]`
  value equals the literal the first test looks for in `SubModule.cs`; a typo on either side is a dead
  patch with no error).

**Verify RED**: build fails on the missing `SaveLoadTokens.SaveCollected`, `SaveLoadTokens.LoadCounts`,
`SaveContext_CollectObjects_Patch` and `LoadContext_Load_Patch`. No other compile error.

**GREEN.**
1. `SaveLoadTokens.SaveCollected(int? objects, int? containers, int? strings, long collectMs)` and
   `SaveLoadTokens.LoadCounts(int? objects, int? containers, int? strings)` (omit a null token; never
   print a made-up 0). Append `LoadCounts,` and `SaveCollected,` to `SaveLoadPhase`.
2. `Hooks/SaveContext_CollectObjects_Patch.cs` (new, under 150 lines):

```
[HarmonyPatchCategory(Category)]
public static class SaveContext_CollectObjects_Patch
{
    internal const string Category = "Patch61_SaveLoadDiagnostics_SaveGraph";

    private static ISaveLoadDiagnosticsService? _service;
    private static IModLogger? _logger;
    private static AccessTools.FieldRef<SaveContext, List<object>>? _childObjects;
    private static AccessTools.FieldRef<SaveContext, List<object>>? _childContainers;
    private static AccessTools.FieldRef<SaveContext, List<string>>? _strings;

    // Binds the three private lists once (installed v1.4.8 SaveContext.cs:75, :79, :83). A field
    // that no longer exists is left null and its count is omitted from the line (never a fake 0).
    public static void Initialize(ISaveLoadDiagnosticsService service, IModLogger logger) { ... }

    // The no-argument overload: CollectObjects(object) also exists (SaveContext.cs:225).
    internal static MethodBase? ResolveTarget() =>
        AccessTools.Method(typeof(SaveContext), "CollectObjects", Type.EmptyTypes);

    [HarmonyTargetMethod]
    public static MethodBase? TargetMethod() { /* ResolveTarget(); warn once when null */ }

    [HarmonyPrefix]
    public static void Prefix(out long __state) => __state = Stopwatch.GetTimestamp();

    // Runs on the main thread inside SaveContext.Save, after the object walk and before
    // CollectSaveDatas (SaveContext.cs:342-346), so the lists are complete and stable here.
    [HarmonyPostfix]
    public static void Postfix(SaveContext __instance, long __state) { /* try { counts; LogPhase(SaveCollected, SaveLoadTokens.SaveCollected(...)); } catch { } */ }
}
```

   Bind each field with its own `try { _childObjects = AccessTools.FieldRefAccess<SaveContext, List<object>>("_childObjects"); } catch { warn }`.
   `collectMs = (Stopwatch.GetTimestamp() - __state) * 1000 / Stopwatch.Frequency`.
3. `Hooks/LoadContext_Load_Patch.cs` (new): `[HarmonyPatch(typeof(LoadContext), nameof(LoadContext.Load), new[] { typeof(LoadData), typeof(bool) })]`,
   `[HarmonyPatchCategory(Category)]` with `internal const string Category = "Patch61_SaveLoadDiagnostics_LoadCounts";`,
   `Initialize(ISaveLoadDiagnosticsService, IModLogger)` binding three `AccessTools.FieldRef<LoadContext, int>`
   for `_objectCount`, `_stringCount`, `_containerCount` (each in its own try/catch), and a Postfix
   `(LoadContext __instance)` that logs `LogPhase(SaveLoadPhase.LoadCounts, SaveLoadTokens.LoadCounts(...))`
   inside try/catch. (`using TaleWorlds.SaveSystem;` for `LoadData`, `using TaleWorlds.SaveSystem.Load;`.)
4. `Main/SubModule.cs`: in the Patch61 block, after
   `Features.SaveLoadDiagnostics.Hooks.ArchiveDeserializer_LoadFrom_Patch.Initialize(saveLoadDiagnostics, saveLoadLogger);` insert:

```
            // Plan 114 save-growth probes, each in its own category so a drifted member costs only itself.
            Features.SaveLoadDiagnostics.Hooks.SaveContext_CollectObjects_Patch.Initialize(saveLoadDiagnostics, saveLoadLogger);
            Features.SaveLoadDiagnostics.Hooks.LoadContext_Load_Patch.Initialize(saveLoadDiagnostics, saveLoadLogger);
```

   and in the sub-category array, after `"Patch61_SaveLoadDiagnostics_ArchiveParse",` add
   `"Patch61_SaveLoadDiagnostics_SaveGraph",` and `"Patch61_SaveLoadDiagnostics_LoadCounts",` (one per
   line, same indentation). The loop already applies each through `TryPatchCategory`.

**Verify GREEN**: build 0 errors; the SaveLoadDiagnostics filter and
`--filter "FullyQualifiedName~HarmonyPatchBindingTests"` 0 failed (the two new patch classes bind on
v1.4.8); binding tests report Passed, not Inconclusive; full suite 0 failed.

Commit `feat(diagnostics): v2.0.28 - log a save's object and container counts` (body: taom_debug now
logs how many objects and containers each save writes and each load reads, and how long the save's
object walk took; `Research:` taom-src v1.4.8 SaveContext :75-83, :175, :225, :328-346 and LoadContext
:11-15, :54; `Not-tested:` the counts against the rgl SAVE/LOAD STATISTICS in game).

### Step B3: per-behavior SyncData sizes at save

The engine builds each behavior's save record in `CampaignBehaviorDataStore.SaveBehaviorData` during
`Game.SaveAux`'s `OnBeforeSave` pass, BEFORE `SaveManager.Save` (`Game.cs:199-205`), i.e. before
`SaveBegin` resets the attempt. So the samples are stored by behavior id and written (then cleared)
right after the `SaveBegin` line.

**RED.**
- `BehaviorRecordStatsTests.cs` (4): `Measure_NullRecords_IsEmpty`; `Measure_CountsRecordsAndCollectionElements`
  (`{"a": new List<int>{1,2,3}, "b": new Dictionary<string,int>{{"x",1}}, "c": 5}` -> Records 3, Elements 4);
  `Measure_FindsTheLongestUtf8StringInValuesKeysAndElements` (a 3-byte character counts 3; the max is a
  list element of record `"names"`, so `MaxStringKey == "names"`); `FormatSummary_SummaryLineThenOnlyBehaviorsOverAThreshold`
  (samples with 12,000 elements, a small one, a 31,000-byte string, 300 ms: one summary line, then rows
  for the three flagged behaviors only, sorted by elements descending then id).
- `SaveLoadDiagnosticsServiceTests` (2): `RecordBehaviorSave_IsWrittenAfterSaveBegin_ThenCleared` (two
  records, then `BeginSaveAttempt`: the SaveBegin line, then `phase=SaveBehaviorData behaviors=2`; a
  second `BeginSaveAttempt` writes none) and `RecordBehaviorSave_SameBehaviorTwice_KeepsTheLatest`.
- `SaveLoadDiagnosticsBindingTests`: `CampaignBehaviorDataStore_RecordFields_ResolveAgainstInstalledEngine`
  (`_behaviorDict` exists; `AccessTools.Inner(store, "BehaviorSaveData")` is not null; its `_records`
  field type is `Dictionary<string, object>`).
- `SaveGrowthProbeWiringTests`: `SubModule_AppliesAndInitializesTheBehaviorSizeHook` (contains
  `"Patch61_SaveLoadDiagnostics_BehaviorSize",` and `CampaignBehaviorDataStore_SaveBehaviorData_Size_Patch.Initialize(`).

**Verify RED**: build fails on the missing `BehaviorRecordStats`, `BehaviorSaveSample`, `BehaviorSaveSummary`
and `RecordBehaviorSave`. No other compile error.

**GREEN.**
1. `Domain/BehaviorRecordStats.cs`: `public readonly struct BehaviorRecordStats` (`int Records`,
   `long Elements`, `int MaxStringBytes`, `string? MaxStringKey`) with
   `public static BehaviorRecordStats Measure(IDictionary<string, object>? records)`: count records;
   for each value: `string` -> UTF-8 byte count; `IDictionary` -> `Elements += Count` and scan its
   string keys and values; other `ICollection` -> `Elements += Count` and scan string elements; stop
   scanning one record after 200,000 elements (counts stay exact). Plus `internal readonly struct BehaviorSaveSample`
   (`Id`, `Type`, `Stats`, `SyncMs`) and `internal static class BehaviorSaveSummary` with
   `ElementsThreshold = 10_000`, `StringBytesThreshold = 30_000` (the entry limit is 32,763 bytes),
   `SyncMsThreshold = 250`, and `IReadOnlyList<string> Format(IReadOnlyCollection<BehaviorSaveSample> samples)`:
   line 1 `behaviors=N records=R elements=E syncMs=T maxStringBytes=B maxStringBehavior='id:key' slowest='id' slowestMs=S`
   (InvariantCulture, one decimal for ms; omit `maxStringBehavior` when B is 0), then one row per
   flagged sample `behavior='id' type='T' records=R elements=E maxStringBytes=B syncMs=S`.
2. Interface: `void RecordBehaviorSave(string behaviorId, string behaviorType, BehaviorRecordStats stats, double syncMs);`
   Service: a `Dictionary<string, BehaviorSaveSample>` under a `lock`; `BeginSaveAttempt` writes the
   SaveBegin stamp, then takes and clears the samples and logs each `BehaviorSaveSummary.Format` line as
   `LogPhase(SaveLoadPhase.SaveBehaviorData, line)`. Append `SaveBehaviorData,` to `SaveLoadPhase`.
3. `Hooks/CampaignBehaviorDataStore_SaveBehaviorData_Size_Patch.cs` (new): category
   `Patch61_SaveLoadDiagnostics_BehaviorSize` (own category, `internal const string Category`);
   `Initialize(ISaveLoadDiagnosticsService, IModLogger)`; `TargetMethod()` resolves the store type with
   `CampaignBehaviorDataStore_LoadBehaviorData_Patch.ResolveStoreType()`, caches
   `AccessTools.Field(type, "_behaviorDict")` and `AccessTools.Field(AccessTools.Inner(type, "BehaviorSaveData"), "_records")`
   (warn once if either is missing; the hook then records nothing), and returns
   `AccessTools.Method(type, "SaveBehaviorData")`; `Prefix(out long __state)` stamps the time;
   `Postfix(object __instance, CampaignBehaviorBase campaignBehavior, long __state)` reads
   `(_behaviorDictField.GetValue(__instance) as IDictionary)?[campaignBehavior.StringId]`, then that
   record's `_records` as `IDictionary<string, object>`, and calls
   `RecordBehaviorSave(campaignBehavior.StringId, campaignBehavior.GetType().FullName, BehaviorRecordStats.Measure(records), ms)`,
   all inside try/catch. Do NOT add these to the existing `CampaignBehaviorDataStore_LoadBehaviorData_Patch`
   class or its category.
4. `Main/SubModule.cs`: after the two Step B2 `Initialize` lines add
   `            Features.SaveLoadDiagnostics.Hooks.CampaignBehaviorDataStore_SaveBehaviorData_Size_Patch.Initialize(saveLoadDiagnostics, saveLoadLogger);`
   and add `"Patch61_SaveLoadDiagnostics_BehaviorSize",` to the sub-category array after the two Step B2 entries.

**Verify GREEN**: build 0 errors; SaveLoadDiagnostics filter and `HarmonyPatchBindingTests` 0 failed;
full suite 0 failed.

Commit `feat(diagnostics): v2.0.28 - per-behavior SyncData sizes at save` (body: at each save
taom_debug now writes one line summing every campaign behavior's saved data, and names any behavior
whose data is very large or slow to collect; `Research:` taom-src v1.4.8 CampaignBehaviorDataStore
:8-79, Game.SaveAux :199-205; `Not-tested:` in game).

### Step B4: the two hot load finalizers stop binding `__originalMethod` (LOAD-TIME-03 part 1)

A finalizer parameter `MethodBase __originalMethod` makes Harmony's wrapper call
`MethodBase.GetMethodFromHandle` on every call (measured on 1.5.x for PatchShield in `5ee49d05f`: about
64 ns and 241 bytes per call, about 1.1 microseconds with 8 threads). These two finalizers run on every
container and header of every load. Replace each multi-target class with one tiny patch class per
target carrying a constant step name. The log lines stay byte-identical.

**RED.** `TAOM.Tests/Features/SaveLoadDiagnostics/SaveLoadHotFinalizerTests.cs` (3 tests):
- `HotLoadFinalizers_DoNotBindOriginalMethod`: collect every method with `[HarmonyFinalizer]` declared on
  `typeof(ContainerLoadData_Fill_Patch)`, `typeof(HeaderLoadData_Readers_Patch)` and their nested types
  (`GetNestedTypes(BindingFlags.Public | BindingFlags.NonPublic)`); assert exactly 7 (4 container sites,
  3 header sites) and that none has a parameter named `__originalMethod`.
- `ContainerFillSites_EachReportItsOwnStep`: for each nested type of `ContainerLoadData_Fill_Patch` with a
  finalizer, call `ContainerLoadData_Fill_Patch.Initialize(svc, logger)` (NSubstitute), invoke the
  finalizer by reflection with `(new InvalidOperationException("x"), new object())`, and assert
  `svc.Received().LogFault(SaveLoadPhase.GraphFault, Arg.Is<string>(d => d.StartsWith("kind=container step=" + step + " ")), Arg.Any<Exception>())`
  where `step` is the nested type's `internal const string Step`; also assert the set of `Step` values
  equals `ContainerLoadData_Fill_Patch.PatchedMethodNames`. Restore the static state in a `finally`
  (`Initialize(null!, null!)`), as for the header test below.
- `HeaderReaderSites_EachReportItsOwnStep`: the same for `HeaderLoadData_Readers_Patch`, whose `Initialize`
  takes only the service (`Initialize(svc)`, restored with `Initialize(null!)`; an `object`
  instance gives `kind=header step=<Step>`); steps are `InitialieReaders`, `InitialieReaders`, `CreateObject`.

**Verify RED**: the first test fails (2 finalizers found, both binding `__originalMethod`); the other
two fail to find nested sites.

**GREEN.** Target shape for `ContainerLoadData_Fill_Patch.cs` (keep the file name, the holder class,
`Initialize`, `ResolveContainerLoadDataType()`, `PatchedMethodNames`, the existing warning texts and the
GraphFault detail text):

```
public static class ContainerLoadData_Fill_Patch
{
    internal const string Category = "Patch61_SaveLoadDiagnostics_ContainerFill";
    // _service, _logger, _headerProperty, Initialize, ResolveContainerLoadDataType, PatchedMethodNames as today.

    // Resolves one target by name; logs the missing type, method or header property once.
    internal static MethodBase? Resolve(string name) { ... }

    // The old Finalizer body, with the step passed in instead of read from __originalMethod. Read the
    // header only when _headerProperty.DeclaringType.IsInstanceOfType(instance); otherwise use null
    // (tests pass a plain object, and GetValue on the wrong type would throw and lose the stamp).
    internal static void Report(Exception? exception, object? instance, string step) { ... }

    [HarmonyPatchCategory(Category)]
    internal static class FillObjectSite
    {
        internal const string Step = "FillObject";
        [HarmonyPrepare] private static bool Prepare() => Resolve(Step) != null;
        [HarmonyTargetMethod] private static MethodBase? TargetMethod() => Resolve(Step);
        [HarmonyFinalizer, HarmonyPriority(Priority.First)]
        private static void Finalizer(Exception? __exception, object __instance) => Report(__exception, __instance, Step);
    }
    // InitializeReadersSite, FillCreatedObjectSite, ReadSite: the same shape.
}
```

`Prepare` returning false skips only that site, which keeps today's behaviour (a missing method costs
that site, not its siblings). `HeaderLoadData_Readers_Patch.cs` gets the same split into
`ObjectHeaderReadersSite` (`AccessTools.Method(typeof(ObjectHeaderLoadData), nameof(ObjectHeaderLoadData.InitialieReaders))`),
`ContainerHeaderReadersSite` and `ContainerHeaderCreateObjectSite`, all in the main
`Patch61_SaveLoadDiagnostics` category (public types, `nameof` targets: a rename fails the build, so no
`Prepare`), with a shared `Report(Exception?, object?, string step)` keeping the `__instance switch`
detail text. `Main/SubModule.cs` needs no change (the `Initialize` calls are unchanged).

**Verify GREEN**: build 0 errors; `SaveLoadHotFinalizerTests`, `SaveLoadDiagnosticsBindingTests`
(the existing `ContainerLoadData_TypeMethodsAndHeaderProperty_ResolveAgainstInstalledEngine` too) and
`HarmonyPatchBindingTests` 0 failed; full suite 0 failed;
`grep -n 'MethodBase __originalMethod' Main/Features/SaveLoadDiagnostics/Hooks/ContainerLoadData_Fill_Patch.cs Main/Features/SaveLoadDiagnostics/Hooks/HeaderLoadData_Readers_Patch.cs` prints nothing.

Commit `perf(diagnostics): v2.0.28 - load finalizers stop binding the method` (body: two always-on
load hooks no longer ask the game which method they patched on every call; each now knows its own
name; their log lines are unchanged and every load does less work per saved object; `Research:`
1.5.x `5ee49d05f` measurement of the same binding; `Not-tested:` load time in game).

### Part B done criteria

- [ ] Four Part B commits with the subjects above; build exit 0; full suite 0 failed, total = Part A
  total + 31.
- [ ] `grep -c 'Patch61_SaveLoadDiagnostics_SaveGraph\|Patch61_SaveLoadDiagnostics_LoadCounts\|Patch61_SaveLoadDiagnostics_BehaviorSize' Main/SubModule.cs` prints `3`.
- [ ] `grep -c 'NoteGameEnded(endedCampaign)' Main/SubModule.cs` prints `1`.
- [ ] Every new `BindingVerification` test reports Passed (not Inconclusive).
- [ ] `git status --porcelain` is empty; `git diff --name-only bannerlord-1.4.5..HEAD` lists only in-scope paths.

## Part C: type census, console arming, diff tool, repro matrix

### Step C1: the census (TDD)

The census walks the two lists the engine just collected and counts saved objects per type, and
containers per type with summed element counts. For engine objects (`MBObjectBase`) it also counts
instances with `IsRegistered == false` and instances whose `Id` repeats an id already seen in this
save: a stale copy of a registered object shares its original's id, and the save's identity
dictionary keeps both because `MBObjectBase` overrides `GetHashCode` but not `Equals`
(`MBObjectBase.cs:79-82`). It runs automatically on the first save after any load that is not the
first in the process (`LoadsThisProcess >= 2`), or on the next save after `taom.arm_save_census`.

**RED.**
- `TAOM.Tests/Features/SaveLoadDiagnostics/SaveGraphCensusTests.cs` (7 tests, plain .NET types only;
  use the test constructor `new SaveGraphCensus(minRowCount: 1, maxRowsPerKind: 3)`):
  `AddObject_CountsPerType`; `AddMbObject_CountsUnregisteredAndDuplicateIds`;
  `AddMbObject_ZeroIdIsNeverADuplicate`; `AddContainer_SumsElementCounts_IgnoringUnknown` (an element
  count of -1 adds 0); `FormatTypeLines_ThresholdCapAndFlaggedRows` (rows below `minRowCount` and past
  the cap are dropped, but a type with `unregistered > 0` or `dupIds > 0` is always listed; objects
  first, each kind sorted by n descending then name);
  `FriendlyName_NestedGenericsUseFullArgumentNames` (`typeof(Dictionary<string, List<int>>)` ->
  `System.Collections.Generic.Dictionary<System.String,System.Collections.Generic.List<System.Int32>>`);
  `Walk_UsesTheDescribeDelegateForEngineObjects` (a fake `describe` returns ids for one test type).
- `SaveLoadDiagnosticsServiceTests` (3): `TakeSaveCensusRequest_NothingArmed_ReturnsFalse`;
  `TakeSaveCensusRequest_AfterSecondLoad_ReturnsAfterReloadOnce` (two `BeginLoadAttempt` calls: the
  first take returns true with reason `afterReload`, the second false);
  `ArmSaveCensus_ReturnsPreviousState_AndTakeReturnsConsoleOnce`.
- `SaveLoadDiagnosticsBindingTests`: `MBObjectBase_IsRegisteredGetter_ResolvesAgainstInstalledEngine`
  (`AccessTools.PropertyGetter(typeof(MBObjectBase), "IsRegistered")` is not null).

**Verify RED**: build fails on the missing `SaveGraphCensus`, `ArmSaveCensus` and `TakeSaveCensusRequest`.
No other compile error.

**GREEN.**
1. `Domain/SaveGraphCensus.cs`: `internal sealed class SaveGraphCensus` with constants
   `MinRowCount = 1000`, `MaxRowsPerKind = 200` (public parameterless constructor uses them; an internal
   `(int minRowCount, int maxRowsPerKind)` one serves tests); `AddObject(Type)`,
   `AddMbObject(Type, uint id, bool? isRegistered)` (one `HashSet<uint>` per census of the non-zero
   ids seen; a repeat adds to that type's `dupIds`), `AddContainer(Type, int elementCount)`;
   `static SaveGraphCensus Walk(IReadOnlyList<object> objects, IReadOnlyList<object> containers, Func<object, (uint Id, bool? IsRegistered)?> describe)`
   (containers: `Array.Length`, else `ICollection.Count`, else -1);
   `string FormatSummary(string reason, int loadsThisProcess, long passMs)` ->
   `reason=R loadsThisProcess=N objects=O containers=C objectTypes=OT containerTypes=CT unregistered=U duplicateIds=D passMs=P`;
   `IEnumerable<string> FormatTypeLines()` -> `obj n=N type='T'` (with ` unregistered=U dupIds=D`
   before `type=` for types that had an engine identity) and `ctr n=N elements=E type='T'`, `type=`
   always last; `internal static string FriendlyName(Type)`.
2. Interface and service: `bool ArmSaveCensus();` (returns the previous armed state) and
   `bool TakeSaveCensusRequest(out string reason);` backed by two `int` flags with `Interlocked.Exchange`:
   console-armed wins (`reason = "console"`, clears both), else the after-reload flag
   (`reason = "afterReload"`); `BeginLoadAttempt` sets the after-reload flag when `loads >= 2`.
   Append `SaveCensus,` to `SaveLoadPhase`.
3. `SaveContext_CollectObjects_Patch`: in `Initialize` also cache
   `AccessTools.MethodDelegate<Func<MBObjectBase, bool>>(AccessTools.PropertyGetter(typeof(MBObjectBase), "IsRegistered"))`
   (try/catch; null leaves `isRegistered` unknown). In `Postfix`, after the SaveCollected line and only
   when both lists were read: `if (svc.TakeSaveCensusRequest(out var reason))` time
   `SaveGraphCensus.Walk(objects, containers, Describe)`, then log `FormatSummary(reason, svc.LoadsThisProcess, passMs)`
   and every `FormatTypeLines()` line as `LogPhase(SaveLoadPhase.SaveCensus, ...)`. `Describe(object o)`
   returns null unless `o is MBObjectBase mb`, else `(mb.Id.InternalValue, registered)`. Keep the file
   under 150 lines; drop the census object before returning.
4. `Cheats/SaveCensusCheats.cs` (new, `namespace TAOM.Features.SaveLoadDiagnostics.Cheats`), shaped like
   `Main/Features/BattleLoadDiagnostics/Cheats/MemoryProbeCheats.cs`: `public static class`, no static
   field initializers beyond `const string`, one method
   `[CommandLineFunctionality.CommandLineArgumentFunction("arm_save_census", "taom")] public static string ArmSaveCensus(List<string> strings) => TaomConsole.RunInCampaign(strings, Usage, _ => { ... });`
   whose body resolves `IoC.Resolve<ISaveLoadDiagnosticsService>()`, calls `ArmSaveCensus()` and returns
   `Save census armed (was armed|was idle). Save the game, then read [SaveLoad] phase=SaveCensus in taom_debug.`
   `ConsoleCommandBindingTests` already pins the shape, the `taom` group, the name and the no-campaign gate.

**Verify GREEN**: build 0 errors; filters `SaveGraphCensusTests`, `SaveLoadDiagnosticsServiceTests`,
`SaveLoadDiagnosticsBindingTests`, `ConsoleCommandBindingTests`, `HarmonyPatchBindingTests` 0 failed;
full suite 0 failed.

Commit `feat(diagnostics): v2.0.28 - census of save types after a reload` (body: after any load that
is not the first in a session, the next save writes a count of every saved type to taom_debug, so a
log from a reload session shows what grows; `taom.arm_save_census` asks for the same count on the next
save (console, cheat mode); `Research:` taom-src v1.4.8 MBObjectBase :15, :25, :79-82, MBGUID :17;
`Not-tested:` the census pass time on a 2 M object save).

### Step C2: `tools/diff_save_census.py` (TDD)

**RED.** `tools/tests/test_diff_save_census.py` (stdlib `unittest`; import like
`tools/tests/test_add_map_villages.py` does with `sys.path.insert(0, <tools dir>)`), 4 tests on synthetic
log text: `test_parses_blocks_by_summary_line`, `test_diff_reports_largest_deltas_first`,
`test_type_missing_from_one_block_counts_as_zero`, `test_cli_with_fewer_than_two_blocks_exits_2`.
**Verify RED**: `python -m unittest tools.tests.test_diff_save_census` errors (module missing).

**GREEN.** `tools/diff_save_census.py`, stdlib only: `parse_blocks(lines)` keeps the text after
`phase=SaveCensus ` on each line; a part containing ` reason=` (or starting `reason=`) opens a block;
rows match `^(obj|ctr) n=(\d+)(?: elements=(\d+))?.*type='(.*)'$`. `diff_blocks(a, b, top)` returns the
summary deltas (objects, containers) and the `top` rows with the largest absolute change in `n`,
keyed by `(kind, type)`, a type absent from one block counting as 0. `main(argv)`:
`python tools/diff_save_census.py <taom_debug.log> [--from N] [--to M] [--top K]` (blocks numbered
from 1 in file order; default the last two; `--top` default 40); prints a plain table; exit 2 with a
message when the log has fewer than two blocks. Read the log with `encoding="utf-8", errors="replace"`.
Add one row to `tools/README.md`, section "Save-game diagnostics & recovery", after the
`repair_sav_strings.ps1` row:

```
| `diff_save_census.py` | Compare two `[SaveLoad] phase=SaveCensus` blocks from one `taom_debug` log: per-type object and container count deltas, largest first. Names what a save gained between two reloads (`docs/investigations/save-growth-2026-11.md`). | `<taom_debug.log>`, `--from N`, `--to M`, `--top K` |
```

**Verify GREEN**: `python -m unittest tools.tests.test_diff_save_census` OK (4 tests);
`python -m unittest discover -s tools/tests -t .` no new failure. Commit
`feat(tools): v2.0.28 - diff two save censuses from a taom_debug log`.

### Step C3: the investigation doc with the repro matrix

Create `docs/investigations/save-growth-2026-11.md` (style of `docs/investigations/native-commit-audit-2026-08.md`;
no long dashes). Required content, in this order:

1. Title `# Save growth after in-game reloads (SAVE-TIME-01): probes and repro matrix`, then a quote
   block: hand-off line ("Read this and run the next unrun cell"), trigger (E2 `rgl_log_22112`, public
   v1.4.8 build), and that no fix exists yet.
2. "What is known": the V01 facts from "Why this matters" above, with this table:

| Save (E2) | Loads in the process | Objects | Containers | Whole save s |
|---|---|---|---|---|
| 17:56 | 1 | 1,301,837 | 1,027,153 | 137.3 |
| 18:10 | 2 | 1,386,205 | 1,099,176 | 131.8 |
| 18:16 | 3 | 1,475,675 | 1,171,631 | 220.2 |
| 19:40 | 11 | 2,181,266 | 1,742,139 | 1092.4 |
| 21:18 | 12 | 2,343,646 | 1,884,752 | 1460.8 |

   and the open points: permanence unproven, one-step-per-load unproven, stale XML objects refuted
   (they save only `StringId`, `Id`, `IsRegistered`), hypotheses H1 (SaveShield swallows a load-callback
   fault on a later load; the existing `phase=LoadFault` stamp and `Modules/TAOM.Dependencies/diag.log`
   show it) and H2 (in-game load versus main-menu load), plus "vanilla engine" and "third-party mod"
   as the cells' alternatives. Source: `plans/_audit/2026-11-04-port148/verify/V01-save-growth.md`.
3. "The probes": a table with one row per line (`phase=Runtime`, `LoadRequested`, `LoadCounts`,
   `GameLoaded`/`GameInitializationFinished` with `prevCampaign=`, `SaveBegin`, `SaveBehaviorData`,
   `SaveCollected`, `SaveCensus`, `SaveCompleted`), each with where it comes from and what to read it
   for. Must say: `objects=`/`containers=` equal the rgl `[GameData.Write] ObjectData Length` /
   `ContainerData Length` (save) and `[GameData.Read] Object Length` / `Container Length` (load);
   `strings=` is a count with no rgl twin (rgl's string figure is a byte size); `collectMs` sits near
   the rgl `SaveContext::CollectObjects completed in N seconds`; `loadsThisProcess` counts load
   attempts, a load cancelled at the module prompt included; read `prevCampaign=` at
   `GameLoaded`/`GameInitializationFinished` (at least two full collections ran after the old game
   left the stack) and treat `alive` with `gen2Since=0` as no evidence; `phase=Runtime` names the
   runtime host and is never a reason to advise server GC.
4. "Repro matrix" (laptop or desktop, v1.4.8, any TAOM campaign save S; BetterTime is not installed on
   the laptop, so every TAOM cell there is also a no-BetterTime cell). Copy this table:

| Cell | Steps (fresh process = start the game from the launcher) | Read | Result means |
|---|---|---|---|
| 1 in-game reload | Fresh process. Main menu, load S. Save to new slot A. Open the save/load screen over the map, load S, save to slot B. Load S again the same way, save to slot C. | `LoadRequested inGame=true loadsThisProcess=`; `SaveCollected` for A, B, C; `SaveCensus` blocks at B and C (reason=afterReload); then `python tools/diff_save_census.py <taom_debug log>` | B and C each above the save before by tens of thousands of containers: reproduced, and the diff names the growing types |
| 2 main-menu reload | Fresh process. Load S, save A. Exit to the main menu. Load S, save B. | second `LoadRequested` reads `inGame=false loadsThisProcess=2`; `SaveCollected` A vs B | B about one increment above A: any second load in a process grows saves. B about equal to A: only in-game loads do |
| 3 does a restart repair? | Quit to the desktop. Fresh process. Load slot C from cell 1, save at once to slot D. | `LoadCounts` (C as read) vs `SaveCollected` (D) | D about C: the growth is in the file, permanent. D about A: process state re-attached it, and a restart repairs a save |
| 4 vanilla control | Only Native, SandBoxCore, Sandbox, StoryMode, CustomBattle; a vanilla save; cell 1 steps. | rgl `[GameData.Write] ObjectData Length` / `ContainerData Length` per save | Growth here: TaleWorlds behaviour, mitigation only. None: a mod carries it |
| 5 BetterTime | Only if cells 1 to 4 do not reproduce: cell 1 with BetterTime added (the player's list) | as cell 1 | Growth only with BetterTime: third-party carrier |
| 6 ButterLib Save System (optional, SAVE-TIME-09) | On a throwaway copy of a save only (saves written with the subsystem on may not load with it off), compare `collectMs` with ButterLib's Save System subsystem on and off. Where ButterLib exposes the toggle on 1.4.8 is UNVERIFIED. | `SaveCollected collectMs` | a large difference: ButterLib's `IsContainer` prefix is a measurable save cost (secondary) |

   In every TAOM cell also: `grep -n 'phase=LoadFault' <taom_debug log>` and look for SaveShield
   swallow lines in `Modules/TAOM.Dependencies/diag.log` (H1). The console baseline is optional:
   `taom.arm_save_census` needs cheat mode; do not open the inventory with cheat mode on during a
   memory reading (one cheat-mode open added about 2.4 GB in E4, MEMORY-01).
5. "Decision table" (copy):

| Observation | Conclusion | Next (a follow-up plan, never this WP) |
|---|---|---|
| Census rows for engine types with `dupIds` or `unregistered` above 0 that grow by about one increment per reload | stale copies of registered objects | name the holder (referrer probe, see `plans/114-save-growth-investigation.md` Maintenance notes), then fix; consider a load-time dedupe only if the copies are unreachable from live play |
| `phase=LoadFault` on a second load, or SaveShield swallow lines in diag.log | H1: a load callback fails on later loads and SaveShield hides it | fix the throwing callback, re-run cell 1 |
| Cell 1 grows, cell 2 does not | in-game load path | research `SandBoxSaveHelper` in-game load path on v1.4.8 |
| Cell 4 grows | engine behaviour | player advice plus a TAOM mitigation plan; report to TaleWorlds |
| Cell 3: D about A | a restart repairs a save | tell players to restart, load and save once |
| Growing rows are containers of a TAOM type | TAOM carrier | plan scoped to that feature |
| `prevCampaign=alive` at GameInitializationFinished | the previous campaign is still reachable (MEMORY-05) | name the static holder with a heap snapshot; if it reads `collected` and the menu `heapMB` stays above 300, look at process caches, not TAOM singletons (V07) |

6. "Player advice that is safe today" (V01 section 7): do not reload inside a running session; to
   retry, exit to the desktop, start the game, then load (quitting only to the main menu is untested);
   do not count on a large save shrinking; older saves of the campaign are smaller but lose progress;
   lengthen the autosave interval and save by hand at quiet moments; send `rgl_log_*.txt` and
   `taom_debug_*.log` with any report. Not advised: server GC or any `.exe.config` edit, removing
   BetterTime from a running campaign, editing save files.
7. "Engine notes": a short paragraph pointing at `docs/reference/engine/save-system.md` and stating
   that the v1.4.8 save-cost mechanics (Containers phase, per-element allocations) are documented by
   the 1.5.x section WP144 ports, re-checked in `plans/_audit/2026-11-04-port148/deepdive/save-time.md`
   (SAVE-TIME-02) with V01's correction that equal-content `TextObject`s do not collapse in the save.

### Step C4: feature, registry, console and toolchain docs

1. `docs/features/save-load-diagnostics.md`: add the three categories to the Architecture table
   (`Patch61_SaveLoadDiagnostics_SaveGraph` `SaveContext_CollectObjects_Patch`;
   `Patch61_SaveLoadDiagnostics_LoadCounts` `LoadContext_Load_Patch`;
   `Patch61_SaveLoadDiagnostics_BehaviorSize` `CampaignBehaviorDataStore_SaveBehaviorData_Size_Patch`;
   "why split": own category per name-bound engine member); note under the table that
   `ContainerLoadData_Fill_Patch` and `HeaderLoadData_Readers_Patch` now hold one site class per target
   with a constant step name (no `__originalMethod`, plan 114); add Log contract rows for `Runtime`,
   `LoadCounts`, `SaveCollected`, `SaveBehaviorData`, `SaveCensus`, and extend the `LoadRequested`,
   `SaveBegin`, `SaveCompleted` and `GameLoaded`/`GameInitializationFinished` rows with their new tokens;
   add the new files to Key Files and the new test classes to Tests (recount with grep, do not copy a
   number); add a link to `../investigations/save-growth-2026-11.md`. Insert above the
   `<!-- backlinks-start` marker, never inside it.
2. `docs/features/dev-console.md`: add this Command reference row (after `taom.print_memory`) and,
   at the top of its changelog list, the bullet below it (`<date>` from `date +%F`):

```
| `taom.arm_save_census` | B | campaign | Nothing: a save's per-type census needed a debugger. Arms the census for the next save; it also runs by itself after any reload in a session. See [save-load-diagnostics.md](save-load-diagnostics.md) |
- **<date>**: Added `taom.arm_save_census` (Tier B, campaign gate) for the save-growth investigation (plan 114).
```
3. `docs/reference/harmony-patch-registry.md`: three new sections after
   `## Patch61_SaveLoadDiagnostics_ContainerFill`, each `**Target:**` plus two sentences:
   `SaveContext.CollectObjects()` (private, no-argument overload, main thread, Prefix+Postfix; counts and
   census), `LoadContext.Load(LoadData, bool)` (Postfix; counts), and
   `CampaignBehaviorDataStore.SaveBehaviorData` (internal type, Prefix+Postfix, its own category beside
   the BehaviorData finalizer; per-behavior sizes). Change the ContainerFill `**Target:**` line to say
   "one site class per method with `Prepare()` + `TargetMethod()`; void Finalizer, `Priority.First`".
4. `docs/reference/bannerlord-engine-and-toolchain.md`: replace the second cell of the
   `mono-2.0-sgen.dll` row (it claims managed code runs on Mono, which the import does not prove;
   v1.4.8 ships three possible runtime hosts per V01) with:

```
Mono is linked into the engine (sgen GC), so the engine can host managed code on Mono. The import alone does not show which runtime players' managed code runs on: TAOM's `[SaveLoad] phase=Runtime` line (`framework=`) reports it per session.
```
5. `python tools/lint_docs.py`: no new finding in touched files.

Commit Steps C3 and C4 together: `docs(diagnostics): v2.0.28 - save-growth repro matrix and probe docs`.

### Step C5: Part C verification

```
dotnet build TAOM.sln -p:DisableModuleCopy=true -p:ModuleId=
dotnet test TAOM.Tests --no-build -p:DisableModuleCopy=true -p:ModuleId=
python -m unittest discover -s tools/tests -t .
python tools/lint_docs.py
git status --porcelain
```

**Verify**: 0 errors; 0 failed, total = Part B total + 11; no new tool-test failure; no new lint
finding in touched files; clean tree.

### Step C6: hand-off report

Do not edit `plans/README.md`: the integrator owns it and records this WP after the merge. Report in
your hand-off: status (DONE, or the Parts landed), each commit's hash and subject, the measured test
total and pass/skip counts, the validator result against the WP101 baseline, and whether Step A4.5b
ran. Planning found no old-plan row that this WP closes; name one if you find it.

### Part C done criteria

- [ ] Three Part C commits (C1, C2, C3+C4); the Step C6 hand-off report is written.
- [ ] `grep -rn 'arm_save_census' Main docs/features/dev-console.md` matches the cheat and the doc row.
- [ ] `test -f docs/investigations/save-growth-2026-11.md` and `grep -c 'save-growth-2026-11.md' docs/features/save-load-diagnostics.md` prints at least `1`.
- [ ] `python -m unittest tools.tests.test_diff_save_census` OK.

## Disposition table

Every commit in the pack (three groups, six commits; no shared groups):

| Commit | Hunk or file group | Disposition | Where, or why |
|---|---|---|---|
| `83bdad853` | `TaomPregnancyModel.cs`, both JSON configs, `TaomPregnancyModelTests.cs` | Transplanted (path checkout) | Step A3; 1.4.x blobs equal the 1.5.x parent |
| `83bdad853` | `ShippedFertilityConfigTests.cs` (new) | Transplanted at `925db38d1`'s version | Step A2 |
| `83bdad853` | `race-age-system.md`, `initial-child-generation.md` | Transplanted at `925db38d1`'s version | Step A3 |
| `83bdad853` | `configs-balance.md` (two lines) | Adapted by hand | Step A3; the file diverged elsewhere on 1.4.x |
| `83bdad853` | howdah platform and diagnostics: `Main/Features/Elephant/*`, `EnableHowdahDiagnostics` in `TaomSettings.cs`, `CoopSettingsRelevance.cs`, `SettingsFingerprintTests.cs`, howdah prefabs, Elephant tests and docs, `rca-howdah-prefab-review` | Skipped | #627, 1.5.x howdah work (classifier: leave) |
| `83bdad853` | career starting-equipment XMLs, `StarterKitCoverageTests.cs`, `generate_starter_kit.py`, `wire_starter_kit_rosters.py` and their tests, starting-equipment docs | Skipped | #629 career kits, content |
| `83bdad853` | `SKILL_TEMPLATE_MISMATCH` in `validate_moduledata.py`, `sync_lord_inline_skills.py`, `taom_schema.py`, `test_skill_template_mismatch.py`, `test_sync_lord_inline_skills.py`, `TroopUpgradeSkillMonotonicityTests.cs`, `test_upgrade_*` edits, `check-moduledata-validation.sh`, `moduledata-validation.md`, lord-skills skill and docs, the #626 edits to `ranged-ladders.md`, `troop-skill-balance.md` and `rca-ranged-rebalance-second-review` | Skipped | #626: on v1.4.8 `BasicCharacterObject.cs:337-357` still discards inline skills under a template, so the 1.4.x rule is right (batch 3 note 4) |
| `83bdad853` | `CHANGELOG.md`, `CLAUDE.md`, `.serena/project.yml`, `.gitattributes` (howdah snapshot eol), `LICENSE-CONTENT.md`, unrelated lessons, review records, armory snapshot and other docs | Skipped | bookkeeping (D1, per-port procedure) or #626/#627/#629 |
| `925db38d1` | `docs/reviews/rca-race-fertility-2026-09-19.md` (new) | Transplanted plus a 1.4.x note | Step A3 |
| `925db38d1` | `ShippedFertilityConfigTests` delta, `race-age-system.md` lines, `configs-balance.md` wording | Transplanted or adapted | Steps A2, A3 |
| `925db38d1` | the two #628 lesson entries (campaign-mechanics "multiplier after a brake"; data-content-cultures "Second instance (#628)") | Adapted (hand-appended) | Step A3; the RCA names them and no docs WP owns `925db38d1` |
| `925db38d1` | `generate_career_kits.py` and tests, kit data, `troops_harad.xml`, starter-kit tools, career docs, `rca-career-kits`, career-kit lessons | Skipped | #629, content |
| `925db38d1` | `rca-skill-template-parity`, `validate_moduledata.py` SKILL_TEMPLATE hunk, `test_skill_template_mismatch.py` | Skipped | #626, v1.5.2 engine semantics |
| `925db38d1` | `HowdahCrewSpawner`, `HowdahCrewAgentOrigin`, `HowdahHarness`, Elephant config and behaviour, Elephant tests and docs | Skipped | #627, 1.5.x crew feature |
| `925db38d1` | `CHANGELOG.md`, `CLAUDE.md`, `LESSONS-LEARNED.md`, `REVIEW-LOG.md`, `tools/README.md`, `feature-map.md`, `release-process.md`, `open-questions.md`, armory audit, catalogue and snapshot | Skipped | bookkeeping or #629 |
| `573dc155f` | `rca-wotr-phase-ordering` correction paragraph | Adapted (final wording with `a66b5754a`, v1.4.8 cites) | Step A4.1 |
| `573dc155f` | `war-chronicle.md`, `rca-war-chronicle-batch-a`, `rca-war-chronicle-events` | Skipped | War Chronicle is 1.5.x-only; files absent on 1.4.x |
| `a66b5754a` | `rca-wotr-phase-ordering` hunk | Adapted (folded into the paragraph) | Step A4.1 |
| `a66b5754a` | `ChronicleService.cs`, War Chronicle tests, `analyze_war_ledger.py` and its test and README row, `war-chronicle.md`, War Chronicle RCAs, Codex prompt | Skipped | 1.5.x-only feature |
| `e8504a62b` | `state-lifecycle-save.md` daily-tick lesson | Adapted | Step A4.5a |
| `e8504a62b` | `state-lifecycle-save.md` four #764 pointer rewordings | Conditional | Step A4.5b runs them only when the #764 lesson is present; otherwise WP142 applies them when it brings that lesson (see Maintenance notes) |
| `e8504a62b` | `campaign-mechanics.md` ChangeKingdomAction lesson | Adapted | Step A4.5c |
| `e8504a62b` | `gamemodels-services.md` 32,767 to 32,763 correction | Adapted | Step A4.5d (same paragraph as `16a34027d`'s lesson hunk, which WP142 takes; see Maintenance notes) |
| `e8504a62b` | "rebuild owned effects on every refresh" lesson | Skipped | optional, and its only source is the 1.5.x War Chronicle rally |
| `e8504a62b` | `adapters-taleworlds-api.md` MainParty lesson; `testing-qa.md` #792 recurrence line | Deferred to WP142 | belongs with the issue-792 series, but WP112's scope excludes lessons; WP142 takes it (see Maintenance notes) |
| `e8504a62b` | `gamemodels-services.md` #792 F1 and #765 C18 recurrence lines; `data-content-cultures.md` Eye of Sauron recurrence | Skipped | War Chronicle and Eye of Sauron are 1.5.x-only |
| `ae1a6b553` | `campaign-tick-time-and-party-ai.md` overview clause and gotcha | Adapted (v1.4.8 cites, generic prescription) | Step A4.2 |
| `ae1a6b553` | `campaignevents-and-campaignbehavior.md` pointer | Ported as is | Step A4.3 |
| `ae1a6b553` | `save-system.md` SyncData bullet | Adapted (v1.4.8 cite; names `WarOfTheRingMomentumBehavior.SyncData`) | Step A4.4 |
| `ae1a6b553` | `.claude/rules/adapters.md` MainParty sentence | Deferred to WP112 | issue-792 series |
| `ae1a6b553` | `.claude/rules/gamemodels.md` War Chronicle rows; `docs/ai-includes/orientation.md` trap row | Skipped | 1.5.x-only feature; file absent on 1.4.x |

Findings this WP owns (no commit of their own):

| Finding | Disposition |
|---|---|
| SAVE-TIME-01 | Parts B and C build the probes and the repro matrix; no fix (binding note) |
| SAVE-TIME-02 | shapes the census (containers counted with their element totals); the v1.4.8 engine-doc text belongs to WP144's port of the 1.5.x save-cost section (re-cite v1.4.8; V01 section 8 `TextObject` correction) |
| SAVE-TIME-03 | memory tokens at SaveBegin and SaveCompleted (Step B1) |
| SAVE-TIME-04 | census rows (heroes, log entries, rosters) and the decision table |
| SAVE-TIME-05 | per-behavior SyncData sizes (Step B3) |
| SAVE-TIME-07 | Steps B1 to B3 and C1 |
| SAVE-TIME-08, LOAD-TIME-11 | Step A4 |
| SAVE-TIME-09 | repro cell 6 (measure only); `1325c36ea` is WP118's |
| LOAD-TIME-02 | `LoadCounts` (Step B2) |
| LOAD-TIME-03 part 1 | Step B4 (part 2, `5ee49d05f`, is WP107's) |
| MEMORY-05 (shared probe) | `prevCampaign=` tokens (Step B1); the correctness fixes are WP119's |

Commits named by those findings, owned elsewhere (owner checked in `data/wp_map.json`): `cb4eeaa3d`
WP139; `1325c36ea` WP118; `5ee49d05f` and `2a3f71b12` WP107; `e99d705a3`, `a67792c40`, `4263535a1`
WP119; `16a34027d` (SAVE-TIME-06) and `9ab3952a7` WP110. `5479109bf` (war ledger load marker) is
SKIP-1.5-ONLY-FIX (War Chronicle).

## Test plan

| File | New tests | Part |
|---|---|---|
| `TAOM.Tests/Features/RaceAge/TaomPregnancyModelTests.cs` | 6 (`ComputeBaseChance_NpcClanPastCap_RaceBonusDropped`, `..._NpcClanPastCap_BonusRaceNeverBeatsHumanRate`, `..._NpcClanAtCap_RaceBonusStillApplies`, `..._NpcClanOneLordPastCap_RaceBonusDropped`, `..._NpcClanPastCap_LowFertilityRaceKeepsPenalty`, `..._PlayerOrSpouseInvolved_RaceBonusClamped`) | A |
| `TAOM.Tests/Features/RaceAge/ShippedFertilityConfigTests.cs` | 7 (as shipped in `925db38d1`) | A |
| `SaveLoadTokensTests.cs` | 8: `LoadRequested_CarriesInGameAndCountsAfterTheName`, `ProcessContext_NoEndedCampaign_SaysNoneWithoutGen2Token`, `ProcessContext_EndedCampaignCollected_SaysCollectedAndGen2Delta`, `ProcessContext_EndedCampaignAlive_SaysAlive`, `RuntimeHost_UnknownValues_PrintQuestionMarksNotZeros`, `SaveCollected_FormatsCountsAndCollectMs`, `SaveCollected_UnreadCount_IsOmittedNotZero`, `LoadCounts_FormatsCounts` | B |
| `SaveLoadDiagnosticsServiceTests.cs` | 7 in Part B (5 in B1, 2 in B3, names in those steps), 3 in Part C | B, C |
| `SaveGrowthProbeWiringTests.cs` | 6: three in B1, two in B2, one in B3 | B |
| `SaveLoadDiagnosticsBindingTests.cs` | 3 in Part B (two in B2, one in B3), 1 in Part C; all `BindingVerification` | B, C |
| `BehaviorRecordStatsTests.cs` | 4 (Step B3) | B |
| `SaveLoadHotFinalizerTests.cs` | 3 (Step B4) | B |
| `SaveGraphCensusTests.cs` | 7 (Step C1) | C |
| `tools/tests/test_diff_save_census.py` | 4 (Python, separate runner) | C |

Totals: Part A 13, Part B 31, Part C 11 C# tests, plus 4 Python tests. Pattern files: existing
`SaveLoadDiagnosticsServiceTests.cs` (NSubstitute `IModLogger`, `Received().LogInfo(Arg.Is<string>(...))`),
`SaveLoadDiagnosticsBindingTests.cs` (`GameAssemblies.EnsureLoaded()` guard), `HeroRaceWiringTests.cs`
(wiring by source text; use `RepoPaths.ReadSource` instead of its private reader). Structurally
untestable, for `Not-tested:` trailers: the hooks firing inside a live save or load, the census pass
time on a 2 M object save, the console command in game, and the in-game daily-tick repeat.

## Done criteria (whole WP)

- [ ] Part A, Part B and Part C done criteria all hold.
- [ ] `dotnet build TAOM.sln -p:DisableModuleCopy=true -p:ModuleId=` exits 0.
- [ ] `dotnet test TAOM.Tests --no-build -p:DisableModuleCopy=true -p:ModuleId=` 0 failed; total =
  baseline + 55.
- [ ] `git diff --name-only bannerlord-1.4.5..HEAD` lists only in-scope paths; `CHANGELOG.md` and
  `Main/IoC.cs` are not among them.
- [ ] `git log --format=%s bannerlord-1.4.5..HEAD | grep -vc ': v2.0.28 - '` prints `0`.
- [ ] `grep -rln 'ObjectSaveData\|ContainerSaveData' Main/Features/SaveLoadDiagnostics` prints nothing
  (no referrer patch).
- [ ] `git status --porcelain` is empty.

## STOP conditions

Stop and report (do not improvise) if:

- Step A1 shows a missing WP104 artifact (`RepoPath`, `ReadSource`, `TryPatchCategory`), a
  `*Module.cs` in `Main/Features/SaveLoadDiagnostics/` (this plan assumes hand wiring), any `DRIFT`
  line, an existing `ShippedFertilityConfigTests.cs`, or no WP101 Armory record (D20).
- The RED run of Step A2 fails a different set than the 8 named tests.
- `python tools/validate_moduledata.py` reports a new ERROR after Step A3.
- A hook rejects a commit for lacking a `CHANGELOG.md` change (WP102 not merged). Never stage it.
- taom-src on the installed DLLs shows a member different from "Engine members verified" (a renamed
  field, a second `LoadContext.Load` overload, `CollectObjects()` gone, `IsRegistered` public): report
  it; do not decompile and improvise.
- A probe seems to need a non-void finalizer, a Prefix that returns `false`, a change to what a save
  writes, or a patch on `ObjectSaveData.GetChildObjects` / `ContainerSaveData.GetChildObjects`.
- Anything seems to need `Main/IoC.cs`, `Dependencies/**`, an MCM setting or a save-format change.
- The census or any probe turns into a SAVE-TIME-01 fix (removing or deduplicating objects).
- A build or test failure survives two reasonable fix attempts in one step.
- `Main/SubModule.cs` no longer has the Patch61 block, `OnGameEnd` or `StampSaveLoadPhase` in the
  shape described (ignoring WP104's `TryPatchCategory` conversion).

## In-game checks owed (maintainer)

Logs: `Logs/taom_debug_*.log` (TAOM) and the engine's `rgl_log_*.txt`; SaveShield's
`Modules/TAOM.Dependencies/diag.log`. Use a deployed build of the merged branch.

1. Startup: `[SaveLoad] ... phase=Runtime framework='...' clr=... serverGC=... latency=...` appears once.
2. One fresh-process load and save: `phase=LoadRequested ... inGame=false loadsThisProcess=1`;
   `phase=LoadCounts objects=X containers=Y` equal to the rgl `[GameData.Read] Object Length` /
   `Container Length`; `phase=SaveBegin ... gc=... privMB=...`; `phase=SaveBehaviorData behaviors=N ...`;
   `phase=SaveCollected objects=X containers=Y ... collectMs=Z` with X, Y equal to the rgl
   `[GameData.Write] ObjectData Length` / `ContainerData Length` and Z near
   `SaveContext::CollectObjects completed in`; `phase=SaveCompleted result=Success loadsThisProcess=1 ...`.
3. An in-game reload, then a save: `inGame=true loadsThisProcess=2`; `GameInitializationFinished ...
   prevCampaign=collected|alive gen2Since=N`; one `phase=SaveCensus reason=afterReload` block.
4. With cheat mode: `taom.arm_save_census` echoes the armed state; the next save writes
   `phase=SaveCensus reason=console`.
5. Load time: one load of the same save before and after Step B4 (LoadContext phase times in rgl);
   expected a small drop, UNVERIFIED size.
6. The repro matrix cells 1 to 3 in `docs/investigations/save-growth-2026-11.md` (cells 4 to 6 as the
   decision table directs), then `python tools/diff_save_census.py <log>`; hand the logs and the diff
   to the orchestrator for the follow-up plan.
7. #628: start a new campaign; the `InitialChildGeneration: created N children across M clans (K excluded)`
   line shows the goblin, Misty Mountain and Blue Craig clans among the excluded (K above a pre-port
   log); over a long campaign orc clans stop sitting at twice their cap.
8. #793 (owed on both lines): save in the first half of a day, reload, and check whether a daily
   handler with a visible effect runs twice for that day.

## Maintenance notes

- **Follow-up plan.** Once a census diff names the carrier, the orchestrator writes the fix plan from
  the decision table. If the carrier's holder field is still unknown, that plan adds the referrer
  probe deferred here: postfixes on `ObjectSaveData.GetChildObjects(ISaveContext, TypeDefinition, object, List<object>)`
  (installed `ObjectSaveData.cs:361`, `internal class` at `:9`) and
  `ContainerSaveData.GetChildObjects(ISaveContext, ContainerDefinition, ContainerType, object, List<object>)`
  (`ContainerSaveData.cs:499`, `internal class` at `:9`), counting parent types of children of the
  named type. They run once per saved object (millions per save) and PatchShield (TAOM.Dependencies)
  adds its own finalizer to every TaleWorlds method TAOM patches, so they must never be patched
  permanently: exclude them from PatchShield or patch them only for one armed save. Recommend
  `/research` on both before writing it.
- **Hand-offs to WP142 (lessons).** (1) `16a34027d`'s lesson hunk renames the chunker on the paragraph
  line this plan edits in `docs/reviews/lessons/gamemodels-services.md`: take its chunker wording and
  keep this plan's 32,763 heading, parenthetical and `Prevent` clause. (2) When WP142 appends the #764
  lesson to `state-lifecycle-save.md`, the four "constructor reset in the last lesson of this file"
  pointers become wrong if this WP's daily-tick lesson follows it: apply Step A4.5b's rewording then.
  (3) Take `e8504a62b`'s MainParty lesson (`adapters-taleworlds-api.md`; re-cite v1.4.8
  `PartyBase.cs:208-218`, keep only the messenger example `91f369984`, repoint Source links to RCAs
  absent on 1.4.x) and its `testing-qa.md` #792 recurrence line; WP112 ports the code but not lessons.
- **Census trigger.** The input pack says "opt-in"; V01 section 5, the report that specifies the probe,
  also runs it on the first save after a second load in a process, and this plan follows V01 so a
  player's log can carry a census without the console. That run ships on by default, is bounded to one
  pass per reload and is unmeasured on a 2 M object save. To veto at merge, drop the after-reload flag
  (`TakeSaveCensusRequest` then only honours `taom.arm_save_census`) and arm before each matrix save.
- **WP139** later adds a `[Session]` summary to `SaveManager_Save_Patch`; this WP leaves that file
  unchanged. **WP144** ports the 1.5.x `save-system.md` save-cost and GC bullets: it must use the v1.4.8
  re-checks and must not recommend server GC (V01). **WP143** recounts the category total in
  `CLAUDE.md` ("Harmony Patch Categories") for the three new categories.
- **Unowned lead.** V01 row 3 reports that `MarriageClanPoolCache` (Patch81) can serve the previous
  game's `Clan` objects after a same-day reload with an unchanged clan count (V01 cites
  `MarriageClanPoolCache.cs:25` and `MarriageClanPoolStamp.cs:46-59`; not re-read for this plan).
  Not a save-growth carrier; whether a stale clan passes vanilla's marriage checks is UNVERIFIED.
  Orchestrator: create an issue (`/issue`); not fixed here.
- **#628 balance values.** The pack asked to confirm the fertility numbers with the maintainer; under
  the 2026-11-04 delegation this plan ports them as the maintainer committed them on 1.5.x in
  `83bdad853` (the RCA calls it "Mike's joint commit"), because D13 applies (defect verified on
  identical 1.4.x data, no OUT content). To veto at merge, revert only the numbers in
  `race_age_config.json` and the three ceilings in `ShippedFertilityConfigTests`; keep the brake fix
  and the exclusions. Clans already above their cap keep their heroes; the brake only stops new growth.
- **Reviewer focus** (`/deep-review` on Parts B and C, C# in several files): every hook body is
  wrapped and cannot throw into the engine; no hook runs reflection lookups per call; the census runs
  only when requested and drops its data before the save continues; the `Prepare` gating in
  `ContainerLoadData_Fill_Patch` keeps one missing method from costing its siblings; the
  `WeakReference` cannot keep a campaign alive; `BeginSaveAttempt` writes the behavior summary after
  `SaveBegin` and clears it; the SubModule edits are only the listed ones.
