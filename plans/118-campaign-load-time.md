# Plan 118: Cut campaign load time: load-time stamps, the one-document XML merge, ButterLib's Distance Matrix off

> **Executor instructions**: Follow this plan part by part, step by step. Run every verification
> command and confirm its expected result before moving on. If anything in "STOP conditions" occurs,
> stop and report; do not improvise. Parts A to E are each independently landable and each has its
> own done criteria; finish a part (all its commits green) before starting the next. Order: A, then B
> (B needs A), then C, D and E in any order. Do not edit `plans/README.md`: the integrator records
> this plan's status and test counts when merging. Put the numbers each part's hand-off asks for in
> your final message instead.
>
> **Drift check (run first, in the worktree, Step 0.2)**:
> `git diff --stat 1b00d1acd..HEAD -- Main/SubModule.cs Main/IoC.cs Main/TAOM.csproj Main/PatchCategoryApplier.cs Main/Composition Main/Core Main/Adapters Main/Features/BattleLoadDiagnostics Main/Features/CoopInterop/CoopSettingsRelevance.cs Main/Features/SupplyLines Main/_Module/ModuleData/lords.xslt Dependencies TAOM.Tests/Infrastructure TAOM.Tests/Migration TAOM.Tests/Features/BattleLoadDiagnostics TAOM.Tests/Features/CoopInterop TAOM.Tests/Features/SupplyLines Stubs/Bannerlord.ButterLib tools/complete_lords_xslt.py tools/lords_inventory.csv tools/tests docs/features docs/reference/harmony-patch-registry.md docs/reference/taleworlds-api-snapshot docs/reference/engine/object-system-mbobjectmanager.md docs/reference/provenance-register.md docs/modding docs/migration/dr3-maintenance.md docs/migration/templates/characters.md docs/ai-includes/lord-skills-authoring.md`
> Changes by earlier WPs of this programme are expected (WP104: `PatchCategoryApplier`, `RepoPaths`,
> `SubModule.cs`; WP105: `Main/Composition`, `IoC.cs`, `SubModule.cs`; WP107: `Dependencies/Foundation`;
> WP112: `Main/Features/SupplyLines`; others: settings counts, `SubModule.cs` blocks). Compare the live
> files against "Current state" and STOP only on a real mismatch (an anchor in Step 0.3 missing,
> duplicated or out of order, or a prerequisite absent).

## Status

- **Priority**: P1 (Parts A, B, D); Parts C and E are P2
- **Effort**: L (five parts, each one executor session or less)
- **Risk**: MED (Part B replaces the engine's module-XML merge loop for every player, guarded by a
  byte-equivalence harness, a stand-aside rule and a self-disable; Parts A and D change
  TAOM.Dependencies, which reaches players only in a release that ships both modules from one build)
- **Depends on**: WP104 (plan 009 `PatchCategoryApplier`/`TryPatchCategory`, `RepoPaths`), WP105
  (plan 018 runner `Main/Composition`), WP107 (`PatchShieldPolicy.ExcludedTargetMethods` and
  `IsExcludedTargetMethod`), and for Step B2 only WP112 (`SupplyCaravanService.AddLandPath`). All are
  in earlier waves.
- **Category**: perf (load time) and diagnostics
- **Wave / lane**: wave 2, lane B
- **Executor**: laptop
- **Decisions applied**: D1 (no `CHANGELOG.md` staging; the commit body is the entry), D2 (the three
  1.5.x feature modules arrive as `*Module.cs` plus one `FeatureModules.All` line each;
  MapLoadDiagnostics stays hand-wired because it has no 1.5.x module), D7 (no co-op or `Client/`
  hunks: `6d7e67f3f`, `[ClientOnly]`), D8 (subjects carry `v2.0.28`), D9 (no
  CI test tags; `binding-gate.runsettings` is not D9: it arrives with WP122, which takes that hunk), D10, D14 (LOAD-TIME-07 dropped), D19 (git only through
  the Bash tool), D20 (Part E's data commit needs the WP101 laptop baseline)
- **Planned at**: 1.4.x `origin/bannerlord-1.4.5` `1b00d1acd` and 1.5.x `origin/bannerlord-1.5.x`
  `e45f6df2c`, 2026-11-04. (The 1.5.x tip moved to `ca1cba35f` while planning; between the two only
  `Dependencies/Foundation/PatchShieldPolicy.cs` changed among this plan's paths, by a 1.5.x-only
  entry for Marshal's Word, not ported.)
- **Issue**: port of #722 (plan 040), #724 (plan 042), #655 (plan 012), #725, #740, #950, #959, and
  the IL tests of #1068. New work (the TAOM.Dependencies pairing guard, Step A1): orchestrator creates
  the issue before implementation lands and writes its number here: **#TBD**.

## Why this matters

Every campaign load on v1.4.8 pays the engine's module-XML merge, which is quadratic: for each file
after the first, `MBObjectManager.MergeTwoXmls` converts the whole document merged so far to
`XDocument` and back (`MBObjectManager.cs:997-1010`), `MergeElements` rebuilds an id dictionary over
every accumulated child (`:833-845`), and `ApplyXslt` compiles a new `XslCompiledTransform` per call
(`:984-995`). One player's load (rgl_log_1744, 2026-10-28) spent 27.2 s of 70.5 s in that window,
15.10 s of it on NPCCharacters and 11.80 s on TAOM's 42 character files; the same player's 12 loads on
2026-11-01 spent 292.9 s in merge windows (22.6 to 29.7 s each after the first). Nothing in
`taom_debug.log` says where a load goes, and the last 9 to 17 s before the map is unattributed. After
this plan: per-type, per-patch-group and per-dispatch load stamps (Part A); a fast path that keeps one
document per type and compiles each XSLT once per process, proven byte-identical on the installed
v1.4.8 module set before it ships (Part B; 1.5.x measured it at 27 to 35 percent of the engine's time
offline, in-game saving unmeasured on any line); a map-load timeline (Part C); ButterLib's unread
Distance Matrix off (about 170 ms per settlement owner change and a 355,000-pair table per load,
measured on 1.5.x), BUTR builds made for 1.4.6 to 1.4.8, and TAOM.Dependencies' startup lines in the
log (Part D); an append-only lord generator and a lords.xslt 360 KiB lighter (Part E).

## Current state

### Executor environment

- Laptop; repo `C:\Users\mikew\source\repos\TAOM`; Bannerlord v1.4.8 (build 119303) at
  `C:\Program Files (x86)\Steam\steamapps\common\Mount & Blade II Bannerlord` (`BANNERLORD_GAME_DIR`).
  Never write into the game install. Python `python` (or `/c/Python314/python.exe`), lxml 6.1.3
  installed; Windows PowerShell 5.1 (`powershell.exe`, .NET Framework) and `pwsh` 7 present.
- Worktree `C:\Users\mikew\source\repos\taom-port\118`, branch `port148/118-campaign-load-time`,
  created from local `bannerlord-1.4.5` (after WP101 plus every merged WP). Start EVERY command with
  `cd /c/Users/mikew/source/repos/taom-port/118 &&` (a `cd` does not persist there).
- Scratch folder for messages, scripts and outputs, never staged:
  `C:\Users\mikew\source\repos\taom-port\118-scratch\` (create it with `mkdir -p`).
- The laptop NuGet cache lacks `Bannerlord.MCM` 5.12.3 and `Bannerlord.UIExtenderEx` 2.13.3 (it has
  5.12.1 and 2.13.2): Step D1's restore needs nuget.org.

### What earlier WPs must have provided (checked in Step 0.3)

| WP | Must exist | Why |
|---|---|---|
| 104 | `Main/PatchCategoryApplier.cs` equal to `998ae7e95:Main/PatchCategoryApplier.cs`; `private bool TryPatchCategory(string category) => _patches.TryApply(category);` in `SubModule.cs`; `TAOM.Tests/Infrastructure/RepoPaths.cs` with `ReadSource(string relativePath, bool stripComments = false)` and `RepoPath(params string[] parts)` | `346f3e093` extends the applier; tests read sources through `RepoPaths` |
| 105 | `Main/Composition/{TaomFeatureModule,FeatureDecls,ModuleRunner,FeatureModuleHooks,FeatureModules}.cs`; in `SubModule.cs` the six `FeatureModuleHooks.` calls (`RunPhase` for `ProcessLoad`, `MainMenu`, `GameInit`, `FirstMission`, `AddGameStartContent`, `AddMissionBehaviors`) | The three modules run through it (D2) |
| 107 | In `Dependencies/Foundation/PatchShieldPolicy.cs`: `public static readonly IReadOnlyList<string> ExcludedTargetMethods` and `public static bool IsExcludedTargetMethod(string? declaringType, string? name)`; `PatchShield.cs` calls `PatchShieldPolicy.IsExcludedTargetMethod(` | The seven exclusions this plan adds; the pairing guard asks it |
| 112 | `internal static void AddLandPath(IMapScene mapScene, PathFaceRecord fromFace, PathFaceRecord toFace,` in `Main/Features/SupplyLines/SupplyCaravanService.cs`; `TAOM.Tests/Features/SupplyLines/SupplyNativeQueryTests.cs` | Step B2's two IL tests pin that code |

### Files and roles (1.4.x `1b00d1acd` unless stated)

| Path | Role here |
|---|---|
| `Main/SubModule.cs` (single owner) | `IoC.Configure();` `:114`; SaveLoad diagnostics block `:296-334` (its catch logs `[SaveLoad] init failed`), then the Patch62 block; `Patch42_CastleRecruitment` `:520` is the last category of `OnSubModuleLoad`; `OnBeforeInitialModuleScreenSetAsRoot` `:525`; `OnGameStart` `:697-733`; `StartupResourcesBehavior` add `:1141`; `OnGameInitializationFinished` `:1309`, `StampSaveLoadPhase(...GameInitializationFinished)` `:1315`, guard `:1324-1325`; `ManualPatchApplicator.ApplyAll(_harmony);` `:1715`; Harmony census try/catch to `:1754`; `OnMissionBehaviorInitialize` `:1757` |
| `Main/IoC.cs` (single owner, Part C) | `using TAOM.Features.BattleLoadDiagnostics;` `:66`; `BattleLoadDiagnosticsIoC.RegisterBattleLoadDiagnosticsFeature(container);` `:158` |
| `Main/TAOM.csproj` (single owner, Part D) | `Bannerlord.MCM` `5.12.1` and `Bannerlord.UIExtenderEx` `2.13.2` compile pins |
| `Dependencies/Foundation/PatchShieldPolicy.cs` | 153 lines at `1b00d1acd`, no method list; WP107 adds it |
| `Dependencies/EarlyLog.cs` (42 lines) | Equal to `d31026b5b^`; nothing in `Main` calls `EarlyLog.DrainTo` |
| `Main/Features/BattleLoadDiagnostics/BattleLoadDiagnosticsSettings.cs`, `...SettingsProvider.cs`, `IBattleLoadDiagnosticsSettingsProvider.cs` | MCM page and provider; one implementer of the interface |
| `Main/Features/CoopInterop/CoopSettingsRelevance.cs` | `Instrumentation` set `:52-69`; every MCM property must be classified |
| `TAOM.Tests/Features/CoopInterop/SettingsFingerprintTests.cs` | `:211` `AssertSplit(typeof(BattleLoadDiagnosticsSettings), reflected: 7, covered: 0);` (earlier WPs may have raised 7); `EveryDocQuotingTheSettingsCounts_AgreesWithReflection` (`:217`) requires `docs/features/coop-interop.md` and `docs/features/bannerlord-together-compat.md` to quote the MCM total (`**243**`, ` 243 MCM settings` at `1b00d1acd`) |
| `TAOM.Tests/Migration/IlCallScanner.cs` (166 lines) | Equal to `a27c6b338^`; no `Fingerprint` |
| `TAOM.Tests/Migration/ReflectionSiteBindingTests.cs` | One `[DataRow]` per string-named engine member; same resolver logic as 1.5.x |
| `TAOM.Tests/Features/CoopInterop/CoopVetoClassificationTests.cs` | Every bool-returning prefix needs a registry entry; `// --- Parked ---` at `:270` |
| `Main/Features/SupplyLines/SupplyRouteVisualService.cs:255-290` | `SyncArrows`: `scene.HasTerrainHeightmap` then `scene.GetTerrainHeightAndNormal(...)`, else `scene.GetHeightAtPoint(position, BodyFlags.Moveable \| BodyFlags.OnlyCollideWithRaycast, ref height)` |
| `Main/_Module/ModuleData/lords.xslt` | Blob `f8c1e3f8597fbdac34827b536938861a33ec8590` on `1b00d1acd`, `c5b84fb41` and the public branch: UTF-8 with BOM, 21,304 lines, 1,493,730 bytes as LF; 82 `<skills>` blocks indented deeper than their `</face>` line (up to 4,960 spaces), 320 lines over 200 leading spaces |
| `tools/complete_lords_xslt.py` (645 lines) | Equal to the fork version `a4c6d7c30`; `tools/tests/test_complete_lords_xslt.py` does not exist on 1.4.x |
| `Dependencies/_Module/bin/Win64_Shipping_Client/` | ButterLib 2.11.0 with implementations 1.4.0 to 1.4.5, MBOptionScreen 5.12.1 v1.4.0 to v1.4.5 |

Nothing from this plan exists on 1.4.x: `git grep` for `Patch89`, `Patch99`, `Patch100`,
`LoadTimeStamps`, `XmlMerge`, `MapLoadDiagnostics`, `ButterLibDistance`, `EarlyLogBridge`,
`IStampClock` under `Main`, `TAOM.Tests` and `Dependencies` finds nothing.

### 1.5.x sources (verified this session)

| Feature | Source | End state |
|---|---|---|
| Load-time stamps | `346f3e093` (61 files, 37 added under `Main`/`TAOM.Tests` plus its doc) | Code and tests unchanged up to `e45f6df2c` (only a docs footer later) |
| XML merge fast path | `a27c6b338` (28 files added) | Code and tests equal `e45f6df2c` except the patch class comment from `43ca92ee9` (lines 37-38); take code and tests from `e45f6df2c`, the doc from `a27c6b338` |
| Map-load timeline | `502f7cde9` + plan 012 (`8028bf3df`, `e6c03411d`, `bce3c27f4`, `15e5475a4`, `a4492fa89`) | Take files from `a4492fa89`; later 1.5.x edits are CI tags (`b8c000452`, `e9b28e2ad`, D9) and `[ClientOnly]` (`6d7e67f3f`, D7) |
| Distance Matrix | `1325c36ea` (9 files added) | Unchanged up to `e45f6df2c` |
| EarlyLog flush | `d31026b5b` | Unchanged up to `e45f6df2c` |
| BUTR refresh | `6027ecfae` (binary DLLs, two csproj pins, stub, licences, dr3 note) | Take whole minus the two 1.5.1 builds |
| Lord generator | `9165a6727` | `tools/complete_lords_xslt.py` 428 lines, unchanged up to `e45f6df2c` |
| lords.xslt dedent | `5e45d2782` (whitespace only) | Re-derived for 1.4.x (rule below) |
| IL tests | `ab78564e0:TAOM.Tests/Features/SupplyLines/SupplyNativeQueryTests.cs` (two of its tests) | WP112 ports the rest |

### Engine facts verified on the installed v1.4.8 (`pwsh -NoProfile -File tools/taom-src.ps1 path <Type>`)

| Member | Verified |
|---|---|
| `public void LoadXML(string id, bool isDevelopment, string gameType, bool skipXmlFilterForEditor = false)`; `GetMergedXmlForManaged` at `:789` sits outside the `try` of `:790-796` | `TaleWorlds.ObjectSystem.MBObjectManager.cs:786-797` |
| `public static bool MergeElementAttributes(XElement, XElement)` `:799`; `public static void MergeElements(XElement element1, XElement element2, string xsdPath)` `:820-875` | same file |
| `public static XmlDocument GetMergedXmlForManaged(string id, bool skipValidation, bool ignoreGameTypeInclusionCheck = true, string gameType = "")` `:877-918`; `GetMergedXmlForNative` `:920-947`; `private static bool HandleXsltList(...)` `:949-964` | same file |
| `public static XmlDocument CreateMergedXmlFile(List<Tuple<string, string>> toBeMerged, List<string> xsltList, bool skipValidation)` `:966-982`; `public static XmlDocument ApplyXslt(string xsltPath, XmlDocument baseDocument)` `:984-995` (new `XslCompiledTransform` per call); `MergeTwoXmls(XmlDocument, XmlDocument, string xsdPath, bool keepDuplicates)` `:997-1010`; `ToXDocument` `:1012-1025`; `ToXmlDocument` `:1027-1035`; `private static void LoadXmlWithValidation(...)` `:1061-1129`; `private static XmlDocument CreateDocumentFromXmlFile(string xmlPath, string xsdPath, bool forceSkipValidation = false)` `:1343-1359` | same file |
| `XmlResource.GetXmlListAndApply` `:149-189`, `XsdElementDictionary` `:32`, `ReadXsdFileAndExtractInformation` `:36`, `GetFullXPathOfElement` `:75`; `ModuleHelper.GetXmlPath` `:232`, `GetXsltPath` `:237`, `GetXsdPathForModules` `:242`, `GetXsdPath` `:247` | `TaleWorlds.ObjectSystem.XmlResource.cs`, `TaleWorlds.ModuleManager.ModuleHelper.cs` |
| `CampaignEventDispatcher`: `public override void OnSessionStart(CampaignGameStarter)` `:1053`, `OnAfterSessionStart` `:1062`, `OnNewGameCreated` `:1071`, `OnGameEarlyLoaded` `:1080`, `OnGameLoaded` `:1089`; receivers `{ CampaignEvents, IssueManager, QuestManager }` built at `Campaign.cs:1370` | `TaleWorlds.CampaignSystem.CampaignEventDispatcher.cs`, `Campaign.cs` |
| `CampaignEvents`: `OnSessionLaunchedEvent` `:849`, `OnAfterSessionLaunchedEvent` `:851`, `OnNewGameCreatedEvent` `:853`, `IMbEvent<CampaignGameStarter, int> OnNewGameCreatedPartialFollowUpEvent` `:855`, `...EndEvent` `:857`, `OnGameEarlyLoadedEvent` `:859`, `OnGameLoadedEvent` `:861`; the receiver overrides that invoke them `:2068-2096` | `TaleWorlds.CampaignSystem.CampaignEvents.cs` |
| ``MbEvent`1``/``MbEvent`2``: `internal class EventHandlerRec<..>` `:7` with `public ... Next` `:9`, `internal Action<..> Action { get; private set; }` `:11`, `internal object Owner { get; private set; }` `:13`; `private EventHandlerRec<..> _nonSerializedListenerList` `:22`; `AddNonSerializedListener` `:24-30`; `ClearListeners`/`ClearListenerOfList` `:46-79` | ``TaleWorlds.CampaignSystem.MbEvent`1.cs``, `` `2.cs`` |
| `Campaign`: `internal void RealTick(float realDt)` `:885`; `MobileParties` `:475`; `Clans` `:501`; `public static float CurrentTime` `:467`; `TimeControlModeLock` `:351`; `TimeControlMode` `:353`; `GameManager.OnGameStart(...)` `:1391`; `InitializeDefaultCampaignObjects()` `:1398` (body `:1466`, `LoadBasicFiles` `:1470`); `GameManager.OnGameInitializationFinished(...)` `:1452` | `TaleWorlds.CampaignSystem.Campaign.cs` |
| `GameStateManager`: `GameStates` `:72`, `ActiveState` `:76`, `PushState(GameState, int level = 0)` `:221`, `PopState(int level = 0)` `:232`, `CleanAndPushState(GameState, int level = 0)` `:243`, `CleanStates(int level = 0)` `:250`; `MapState.OnActivate` protected override `:237`; `GameState.OnInitialize` protected virtual `:85` | `TaleWorlds.Core.*`, `TaleWorlds.CampaignSystem.GameState.MapState.cs` |
| `LoadingWindow.DisableGlobalLoadingWindow()` `:31-44` sets `IsLoadingWindowActive = false` whatever its prior value; `MBInitialScreenBase.cs:80` calls it from the initial screen; 13 call sites in `_modules_build\SandBox__SandBox.GauntletUI.cs`; `SceneView.CheckSceneReadyToRender()` `:45`, `ReadyToRender()` `:120`; `MapScreen.OnInitialize` and `OnFrameTick` (`SandBox__SandBox.View.cs:14058`, `:14756`), `HandleIfBlockerStatesDisabled` lowers the window on the fourth consecutive ready frame (`:14257-14285`) | `TaleWorlds.Engine.LoadingWindow.cs`, `SceneView.cs`, dump |
| `MobileParty.NavigationType.Default = 1`; `BodyFlags.Moveable = 0x10`, `OnlyCollideWithRaycast = 0x80` (so the arrows' mask is 144); `Scene.HasTerrainHeightmap` `:26`, `GetTerrainHeightAndNormal(Vec2, out float, out Vec3)` `:485`, `GetHeightAtPoint(Vec2, BodyFlags, ref float)` `:988` | `MobileParty.cs:37-43`, `BodyFlags.cs`, `Scene.cs` |
| ButterLib 2.11.0 `Implementation.1.4.5` (`_modules_build`): `internal class DistanceMatrixSubSystem` in `Bannerlord.ButterLib.Implementation.DistanceMatrix` with `public static DistanceMatrixSubSystem? Instance`, `public bool IsEnabled { get; private set; }`, `public void Disable()` (ignored once `GameInitialized`); `GeopoliticsBehavior` is added at game start only if `IsEnabled`; its `SyncData` is empty | `TAOM.Dependencies__Bannerlord.ButterLib.Implementation.1.4.5.cs:2488-2540`, `:2541` (behavior), `:540-550` |
| Both BUTR loaders (ButterLib 2.11.0 core, MBOptionScreen 5.12.1 module loader) load the implementation that matches the running engine exactly, and when none matches, the LATEST build they find (`ImplementationLatest`, `SelectLatestImplementation`). This is how v1.4.8 ran on the 1.4.5 builds | `TAOM.Dependencies__Bannerlord.ButterLib.cs:1594-1660`, `TAOM.Dependencies__Bannerlord.ModuleLoader.Bannerlord.MBOptionScreen.cs:145-230` |

### Conventions that bind this work

- **TDD**: failing test first (RED), then the code (GREEN). New work: Steps A1, A2 (guard test), B1
  (guard test), B2 (arrows test rewrite), E2.
- **ADR-007**: services never touch sealed TaleWorlds types; the ported services already go through
  `ICampaignListenerAdapter`, `IXmlMergeEngineAdapter`, `IButterLibDistanceMatrixAdapter`.
  `ShieldExclusionCheck` touches no engine type.
- **ADR-002** thin entry points: every `SubModule.cs` edit is a one-line call or a small guarded block.
- **ADR-003** no `#region`; **ADR-004** no `[Obsolete]`; **ADR-005** no `#if DEBUG`.
- **D2 and `docs/features/composition-root.md`** (WP105): copy a 1.5.x module, append one
  `FeatureModules.All` line, never read `IoC.Resolver`, never call `.PatchCategory(` in a module, STOP
  on `OwnsSaveData => true` (none here). This plan adapts two module files for the pairing guard and
  says so in their doc comments.
- **MCM json2 rule**: never flip a persisted default; `EnableLoadTimeStamps` is new (default false,
  `RequireRestart = false`, which `SettingRequireRestartPostureTests` requires), classified as
  instrumentation, counted in the fingerprint census and the two docs.
- **`.claude/rules/harmony-patches.md`**: no `MovementOrder` postfix here. Keep the 1.5.x patch
  numbers (Patch89, Patch99, Patch100) so crash triage matches across lines.
- **#371 pairing**: `Dependencies/` changes reach players only when TAOM and TAOM.Dependencies ship
  from one build; the release bumps `Dependencies/_Module/SubModule.xml` `<Version>` and Main's
  `DependedModuleMetadata` pin together (maintainer, `/release`). This plan bumps neither.
- No long dashes in any prose you write (commit bodies, docs).

## Commands you will need

Start every command with `cd /c/Users/mikew/source/repos/taom-port/118 &&`.

| Purpose | Command | Expected |
|---|---|---|
| Restore | `dotnet restore TAOM.sln` | exit 0 |
| Build | `dotnet build TAOM.sln -p:DisableModuleCopy=true -p:ModuleId=` | exit 0, `0 Error(s)` |
| Test | `dotnet test TAOM.Tests --no-build -p:DisableModuleCopy=true -p:ModuleId=` (timeout 600000 ms) | `Failed: 0` |
| Filtered | `dotnet test TAOM.Tests --no-build -p:DisableModuleCopy=true -p:ModuleId= --filter "FullyQualifiedName~<A>\|FullyQualifiedName~<B>"` | as each step states |
| Binding gate | `dotnet test TAOM.Tests --no-build -p:DisableModuleCopy=true -p:ModuleId= --filter "TestCategory=BindingVerification"` | `Failed: 0` |
| Live merge harness | `TAOM_RUN_BENCHMARKS=1 dotnet test TAOM.Tests --no-build -p:DisableModuleCopy=true -p:ModuleId= --filter "FullyQualifiedName~XmlMergeLiveEquivalenceTests" --logger "console;verbosity=detailed"` | `Passed: 2` |
| Data | `python tools/validate_moduledata.py` | no new ERROR vs the WP101 baseline |
| Tool tests | `python -m unittest discover -s tools/tests -t .` | no new failure vs Step 0.4 |
| Docs | `python tools/lint_docs.py --summary --dash-base $(git merge-base HEAD bannerlord-1.4.5)` | `dead_links` and `orphan_features` equal Step 0.4's; `ai_dashes: 0` |

In the Filtered row `\|` is Markdown table escaping: type a plain `|` inside the quotes. Where a step lists
classes as `A|B|C`, expand them to `FullyQualifiedName~A|FullyQualifiedName~B|FullyQualifiedName~C`.

Both `-p:` flags are mandatory on every build and test: `Main/TAOM.csproj:133-136` does not gate
CopyModule on `DisableModuleCopy`, so without `-p:ModuleId=` a build deploys into the game install.
Never `./build.ps1`; never two builds at once; always Build before a `--no-build` run.

## Scope

**In scope** (create or modify only these):

- Part A: create `Main/Core/Diagnostics/ShieldExclusionCheck.cs`,
  `TAOM.Tests/Core/Diagnostics/ShieldExclusionCheckTests.cs`, the 37 files `346f3e093` adds under
  `Main`, `TAOM.Tests` and `docs/features/load-time-stamps.md`; modify
  `Dependencies/Foundation/PatchShieldPolicy.cs`, `Main/Composition/FeatureModules.cs`,
  `Main/PatchCategoryApplier.cs`, the three BattleLoadDiagnostics settings files,
  `Main/Features/CoopInterop/CoopSettingsRelevance.cs`,
  `TAOM.Tests/Infrastructure/PatchCategoryApplierTests.cs`,
  `TAOM.Tests/Features/BattleLoadDiagnostics/BattleLoadDiagnosticsSettingsProviderTests.cs`,
  `TAOM.Tests/Features/CoopInterop/SettingsFingerprintTests.cs`,
  `TAOM.Tests/Migration/ReflectionSiteBindingTests.cs`, `docs/features/battle-load-diagnostics.md`,
  `docs/features/coop-interop.md`, `docs/features/bannerlord-together-compat.md`.
- Part B: the 28 files `a27c6b338` adds; modify `TAOM.Tests/Migration/IlCallScanner.cs`,
  `TAOM.Tests/Features/CoopInterop/CoopVetoClassificationTests.cs`, `ReflectionSiteBindingTests.cs`,
  `ShieldExclusionCheckTests.cs`, `FeatureModules.cs`, `docs/reference/engine/object-system-mbobjectmanager.md`,
  `TAOM.Tests/Features/SupplyLines/SupplyNativeQueryTests.cs`.
- Part C: the 16 files under `Main/Features/MapLoadDiagnostics` and
  `TAOM.Tests/Features/MapLoadDiagnostics` at `a4492fa89`, `docs/features/map-load-diagnostics.md`;
  modify `docs/features/save-load-diagnostics.md`; Step C1.4 only: `docs/features/mission-perf-heartbeat.md`,
  `docs/features/map-perf-profiler.md`, `docs/features/time-acceleration.md` (the last only if C1.4's check names it;
  plan 117 writes no such sentence there as it stands).
- Part D: the vendored DLLs and text files of `6027ecfae` (minus the two 1.5.1 builds), the 9 files
  `1325c36ea` adds, `Dependencies/EarlyLog.cs`, `Main/Core/Logging/EarlyLogBridge.cs`,
  `TAOM.Tests/Core/Logging/EarlyLogBridgeTests.cs`, `docs/migration/dr3-maintenance.md`,
  `docs/modding/module-dependencies.md`, `docs/reference/provenance-register.md`, `FeatureModules.cs`.
- Part E: `tools/complete_lords_xslt.py`, `tools/tests/test_complete_lords_xslt.py`,
  `tools/lords_inventory.csv`, `tools/tests/test_lords_xslt_indentation.py`,
  `Main/_Module/ModuleData/lords.xslt`, `docs/ai-includes/lord-skills-authoring.md`,
  `docs/features/lord-identity-reconciliation.md`, `docs/migration/templates/characters.md`,
  `docs/modding/lords-and-heroes.md`.
- Several parts: `docs/reference/harmony-patch-registry.md`,
  `docs/reference/taleworlds-api-snapshot/reflection-sites.md` (hand-kept). Not `patch-targets.md` or
  `gamemodel-bases.md`: `snapshot_api_surface.ps1 -Check` cannot pass on 1.4.x (the committed file carries a
  backlinks footer the generator never writes) until WP137 ports `4ab005dd1` and regenerates them.
- **Single-owner files** (this WP edits them only at the anchors named): `Main/SubModule.cs` (Parts
  A, B, C, D), `Main/IoC.cs` (Part C), `Main/TAOM.csproj` (Step D1).

**Out of scope** (do not touch): `CHANGELOG.md` (decision D1); `.claude/**`, `CLAUDE.md`, `AGENTS.md`,
`docs/reviews/**`, `docs/INDEX.md`, `docs/reference/feature-map.md`, `tools/README.md` (WPs 142 to 144
sync them); `plans/README.md`; `Directory.Build.props`, `docs/adrs/**` (maintainer only);
`Dependencies/_Module/SubModule.xml` and `Main/_Module/SubModule.xml` (release bump);
`TAOM.Tests/binding-gate.runsettings`, `BindingGateRunSettingsTests` (they arrive with WP122, which takes that
hunk of `a27c6b338`); `Client/`
and `[ClientOnly]` (D7); ButterLib's Save System subsystem (SAVE-TIME-09); a generated NPCCharacters
file (LOAD-TIME-07, D14); running `tools/complete_lords_xslt.py --apply`; anything nearby that this
plan does not name.

## Git workflow

- Worktree and branch as above; commit only there. Never push, open a PR, tag, or merge into
  `bannerlord-1.4.5`. Run git only through the Bash tool (D19).
- Per-port procedure (binding): a cherry-pick is always `git cherry-pick -n <sha>` (never let git
  create the commit; `GIT_EDITOR=true` would commit the 1.5.x message). End-state transplants write
  files with `git show <sha>:<path> > <path>` (LF; `core.autocrlf=true` normalises on `git add`).
  Bookkeeping never travels: a conflicted bookkeeping path is `git checkout --ours <path>` then
  `git add <path>`; a cleanly applied one that exists in HEAD is `git checkout HEAD <path>`; an
  added one is `git rm -q -f -- <path>`.
- Stage with explicit `git add <paths>` (never `-A`, `-u` or `.`), then run
  `git commit -F <msgfile>` in the NEXT sequential Bash call. Write each message with the Write tool
  to `C:\Users\mikew\source\repos\taom-port\118-msg-<step>.txt` and pass it as
  `C:/Users/mikew/source/repos/taom-port/118-msg-<step>.txt`. Never `--no-verify`, `--amend`, `-C`,
  `-c`. A hook that denies a commit is a STOP (report its message verbatim).
- Message template: subject `<type>(<scope>): v2.0.28 - <description>` (at most 72 characters; the
  subjects below are counted); a body for 1.4.x readers wrapped at 72 with no long dashes; trailers
  `Research:`, `Not-tested:`, `Save-compat:` when relevant, then one `Ported-from: <40-hex sha>` per
  included 1.5.x commit. No `Co-Authored-By`, no "Generated with" line. Replace `<DATE>` with
  `date +%F` and `#NNN` with the issue number from the Status block.

## Steps

### Step 0: Worktree, drift, prerequisites, baselines (every session)

**0.1** First session only:
`cd /c/Users/mikew/source/repos/TAOM && git worktree add /c/Users/mikew/source/repos/taom-port/118 -b port148/118-campaign-load-time bannerlord-1.4.5 && mkdir -p /c/Users/mikew/source/repos/taom-port/118-scratch`
Expected: `Preparing worktree (new branch ...)`. A later session reuses the worktree; run
`git log --oneline bannerlord-1.4.5..HEAD` to see which parts are committed.

**0.2** Run the drift check from the header. Read every change it lists; 0.3 decides.

**0.3** Prerequisites and anchors:

```bash
cd /c/Users/mikew/source/repos/taom-port/118 && \
git diff --quiet 998ae7e95 HEAD -- Main/PatchCategoryApplier.cs && echo applier-same; \
for p in 'private bool TryPatchCategory(string category) => _patches.TryApply(category);' \
 'FeatureModuleHooks.RunPhase(ApplyPhase.ProcessLoad, TryPatchCategory);' \
 'FeatureModuleHooks.RunPhase(ApplyPhase.MainMenu, TryPatchCategory);' \
 'FeatureModuleHooks.RunPhase(ApplyPhase.GameInit, TryPatchCategory);' \
 'FeatureModuleHooks.RunPhase(ApplyPhase.FirstMission, TryPatchCategory);' \
 'FeatureModuleHooks.AddGameStartContent(gameStarterObject);' 'IoC.Configure();' \
 'base.OnGameStart(game, gameStarterObject);' 'base.OnGameInitializationFinished(game);' \
 'StampSaveLoadPhase(Features.SaveLoadDiagnostics.Domain.SaveLoadPhase.GameInitializationFinished);' \
 'if (_gameInitPatchesApplied) return;' '_gameInitPatchesApplied = true;' \
 'ReportPatchFailures(new TextObject("{=taom_patch_apply_phase_game_init}game initialization"));' \
 'ManualPatchApplicator.ApplyAll(_harmony);' '[SaveLoad] init failed' \
 'campaignStarter.AddBehavior(new StartupResourcesBehavior('; do \
 printf '%s:%s  %s\n' "$(grep -cF "$p" Main/SubModule.cs)" "$(grep -nF "$p" Main/SubModule.cs | head -1 | cut -d: -f1)" "$p"; done; \
grep -cF 'public static string ReadSource(string relativePath, bool stripComments = false)' TAOM.Tests/Infrastructure/RepoPaths.cs; \
grep -cF 'public static string RepoPath(params string[] parts)' TAOM.Tests/Infrastructure/RepoPaths.cs; \
grep -cF 'public static readonly IReadOnlyList<string> ExcludedTargetMethods' Dependencies/Foundation/PatchShieldPolicy.cs; \
grep -cF 'public static bool IsExcludedTargetMethod(string? declaringType, string? name)' Dependencies/Foundation/PatchShieldPolicy.cs; \
grep -cF 'PatchShieldPolicy.IsExcludedTargetMethod(' Dependencies/Foundation/PatchShield.cs; \
grep -cF 'internal static void AddLandPath(IMapScene mapScene, PathFaceRecord fromFace, PathFaceRecord toFace,' Main/Features/SupplyLines/SupplyCaravanService.cs; \
ls TAOM.Tests/Features/SupplyLines/SupplyNativeQueryTests.cs; grep -c 'Fingerprint(' TAOM.Tests/Migration/IlCallScanner.cs; \
ls -d Main/Features/LoadTimeStamps Main/Features/XmlMerge Main/Features/MapLoadDiagnostics Main/Features/ButterLibDistanceMatrix Main/Core/Logging/EarlyLogBridge.cs 2>&1 | grep -c 'No such file'; \
git rev-parse HEAD:Main/_Module/ModuleData/lords.xslt
```

Expected: `applier-same` (only before Part A, which edits the applier; in a later session its absence is normal); every SubModule line starts `1:` and the line numbers rise in this order:
`IoC.Configure();` < `[SaveLoad] init failed` < `RunPhase(ApplyPhase.ProcessLoad` < `RunPhase(ApplyPhase.MainMenu` <
`base.OnGameStart` < `AddGameStartContent` < `StartupResourcesBehavior` < `base.OnGameInitializationFinished` <
`StampSaveLoadPhase` < `if (_gameInitPatchesApplied)` < `_gameInitPatchesApplied = true;` < `RunPhase(ApplyPhase.GameInit` <
game-init `ReportPatchFailures` < `ManualPatchApplicator` < `RunPhase(ApplyPhase.FirstMission`. Then `1`, `1`, `1`, `1`, at
least `1`, `1` (WP112), the `SupplyNativeQueryTests.cs` path, `0` (no Fingerprint yet), `5`, and
`f8c1e3f8597fbdac34827b536938861a33ec8590`. In a later session the last four values reflect the parts already
committed (Fingerprint present after B; one fewer missing path per landed part: LoadTimeStamps after A, XmlMerge
after B, MapLoadDiagnostics after C, ButterLibDistanceMatrix and EarlyLogBridge.cs after D; the dedented blob
`f2f180d05ddc816635087b27e070210502d9cb92` after E). A WP104/105/107 line that fails: STOP. A WP112 line that
fails: Step B2 only is blocked (report it; do the rest). A non-zero `Fingerprint(` count before Part B: another WP
brought it; skip that hunk in B1 and confirm `git diff a27c6b338 HEAD -- TAOM.Tests/Migration/IlCallScanner.cs`
shows no change inside `Fingerprint`/`FingerprintToken`. Before Part E, any lords.xslt blob other than `f8c1e3f8...`:
STOP before E2 (its numbers no longer hold).

**0.4** Baselines: Restore, Build, Test. In the first session, before any part is committed, record **T0** (Total,
Passed, Skipped); every later count in this plan is relative to that T0, so a later session takes it from the earlier
hand-off and does not re-measure it. Run the Docs command and record `dead_links`, `orphan_features`. Before Part E also run the Tool tests and record the
failures (names), and run the Data command and save its summary to `118-scratch/validate-before.txt`.

---

## Part A: the pairing guard and the load-time stamps (`346f3e093`)

### Step A1 (RED then GREEN): ShieldExclusionCheck

New work (issue #NNN from the Status block; if it still reads `#TBD`, STOP and ask for the number).
Create `TAOM.Tests/Core/Diagnostics/ShieldExclusionCheckTests.cs`:

```csharp
using System;
using System.Collections.Generic;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using TAOM.Core.Diagnostics;

namespace TAOM.Tests.Core.Diagnostics;

/// <summary>
/// ShieldExclusionCheck answers "does the loaded TAOM.Dependencies.dll keep PatchShield off these engine
/// methods?" without ever throwing, including against a Dependencies DLL too old to have the question (#371).
/// </summary>
[TestClass]
public class ShieldExclusionCheckTests
{
    private static readonly IReadOnlyList<(string Type, string Method)> Targets = new[]
    {
        ("Engine.TypeA", "First"),
        ("Engine.TypeB", "Second"),
    };

    [TestMethod]
    public void Problem_EveryTargetExcluded_IsNull()
    {
        Assert.IsNull(ShieldExclusionCheck.Problem(Targets, (_, _) => true));
    }

    [TestMethod]
    public void Problem_ATargetNotExcluded_NamesItAndThePairingRule()
    {
        var problem = ShieldExclusionCheck.Problem(Targets, (type, _) => type != "Engine.TypeB");

        StringAssert.Contains(problem, "Engine.TypeB.Second");
        StringAssert.Contains(problem, "#371");
    }

    [TestMethod]
    public void Problem_AnOlderDependenciesWithoutTheMethod_SaysSoInsteadOfThrowing()
    {
        var problem = ShieldExclusionCheck.Problem(Targets,
            (_, _) => throw new MissingMethodException("PatchShieldPolicy", "IsExcludedTargetMethod"));

        StringAssert.Contains(problem, "IsExcludedTargetMethod");
        StringAssert.Contains(problem, "#371");
    }

    [TestMethod]
    public void Problem_AnyOtherFault_IsReportedNotThrown()
    {
        var problem = ShieldExclusionCheck.Problem(Targets, (_, _) => throw new InvalidOperationException("boom"));

        StringAssert.Contains(problem, "InvalidOperationException");
    }
}
```

**Verify (RED)**: Build. Expected: exit 1; every `error CS` names `ShieldExclusionCheck` or
`TAOM.Core.Diagnostics` in `ShieldExclusionCheckTests.cs`
(`grep "error CS" <log> | grep -vc ShieldExclusionCheckTests` prints `0`; write the log to `118-scratch/`).

Create `Main/Core/Diagnostics/ShieldExclusionCheck.cs`:

```csharp
using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;

namespace TAOM.Core.Diagnostics;

/// <summary>
/// Asks the loaded TAOM.Dependencies.dll whether PatchShield leaves each engine method of a list alone
/// (<c>PatchShieldPolicy.IsExcludedTargetMethod</c>). The load-time stamps and the XML merge fast path patch
/// MBObjectManager and CampaignEventDispatcher methods that must never run under PatchShield's finalizer: it would
/// swallow a missing-API exception into an empty XML type or a half-run campaign dispatch, then strip TAOM's
/// patches from the method for the session. Those exclusions live in TAOM.Dependencies, and a player can pair a
/// newer TAOM.dll with an older TAOM.Dependencies.dll (#371; the public 1.4.8 build logs such a MISMATCH), so the
/// two modules ask before they declare their patch categories. Never throws.
/// </summary>
internal static class ShieldExclusionCheck
{
    /// <summary>Null when the loaded TAOM.Dependencies excludes every target; otherwise why it does not.</summary>
    internal static string? Problem(IReadOnlyList<(string Type, string Method)> targets) =>
        Problem(targets, LoadedPolicyExcludes);

    /// <summary>The same, with the policy question as a parameter so tests can stand in for an older DLL.</summary>
    internal static string? Problem(IReadOnlyList<(string Type, string Method)> targets, Func<string, string, bool> isExcluded)
    {
        foreach (var (type, method) in targets)
        {
            try
            {
                if (!isExcluded(type, method))
                    return "PatchShield in the loaded TAOM.Dependencies.dll does not exclude " + type + "." + method +
                           " (ship TAOM and TAOM.Dependencies from one build, #371)";
            }
            catch (MissingMemberException)
            {
                return "the loaded TAOM.Dependencies.dll has no PatchShieldPolicy.IsExcludedTargetMethod, so it is " +
                       "older than this TAOM.dll (ship TAOM and TAOM.Dependencies from one build, #371)";
            }
            catch (Exception ex)
            {
                return "asking the loaded TAOM.Dependencies.dll about " + type + "." + method + " threw " +
                       ex.GetType().Name + ": " + ex.Message;
            }
        }
        return null;
    }

    // Its own method, never inlined: against an older TAOM.Dependencies.dll the call below fails when this method
    // is compiled, which happens on its first call, inside Problem's try.
    [MethodImpl(MethodImplOptions.NoInlining)]
    private static bool LoadedPolicyExcludes(string type, string method) =>
        TAOM.Dependencies.Foundation.PatchShieldPolicy.IsExcludedTargetMethod(type, method);
}
```

**Verify (GREEN)**: Build `0 Error(s)`; Filtered `ShieldExclusionCheckTests`: `Total: 4`, `Failed: 0`.

**Commit A1**: `git add Main/Core/Diagnostics/ShieldExclusionCheck.cs TAOM.Tests/Core/Diagnostics/ShieldExclusionCheckTests.cs`; message:

```text
feat(diagnostics): v2.0.28 - ask Dependencies which targets it shields

Adds ShieldExclusionCheck, which asks the loaded TAOM.Dependencies.dll
whether PatchShield leaves a list of engine methods alone
(PatchShieldPolicy.IsExcludedTargetMethod). It never throws: a
Dependencies DLL too old to have that method, or one that does not
exclude a target, comes back as a reason naming the gap.

The load-time stamps and the XML merge fast path that follow patch
MBObjectManager and CampaignEventDispatcher methods that must not run
under PatchShield's finalizer: it would swallow a missing-API
exception into an empty XML type or a half-run campaign dispatch and
then unpatch TAOM for the session. The exclusions live in
TAOM.Dependencies, and the public 1.4.8 build already pairs a newer
TAOM.dll with an older TAOM.Dependencies.dll (#371), so the modules
ask before they declare their patch categories.

Refs #NNN
Not-tested: against a real older TAOM.Dependencies.dll (a probe that
 throws MissingMethodException stands in for it)
```

### Step A2: Load-time stamps (`346f3e093`, end-state transplant plus 1.4.x adaptations)

Method: transplant, not cherry-pick: the merge-tree simulation onto `1b00d1acd` (re-run this session)
conflicts in 19 paths, and on this base the shared files differ again; every code file equals its
`e45f6df2c` state. Bookkeeping left behind: `docs/reviews/**`, `plans/040-load-time-stamps.md`,
`docs/reference/feature-map.md`.

**A2.1 Transplant the added files**:

```bash
cd /c/Users/mikew/source/repos/taom-port/118 && S=346f3e093 && \
for f in $(git show $S --name-only --diff-filter=A --format= -- Main TAOM.Tests docs/features/load-time-stamps.md); do \
  mkdir -p "$(dirname "$f")" && git show "$S:$f" > "$f" || echo "FAILED $f"; done; \
git status --porcelain | grep -c '^??'
```

Expected: no `FAILED`; untracked entries for `Main/Adapters/CampaignListenerAdapter.cs`,
`ICampaignListenerAdapter.cs`, `Main/Core/Diagnostics/IStampClock.cs`, `StopwatchStampClock.cs`, the
folders `Main/Features/LoadTimeStamps/` and `TAOM.Tests/Features/LoadTimeStamps/`, and the doc
(`git show 346f3e093 --name-only --diff-filter=A --format= -- Main TAOM.Tests docs/features/load-time-stamps.md | wc -l` is `37`).
If WP117 merged first it already brought `IStampClock.cs` and `StopwatchStampClock.cs` from this same commit: those two
then show no change, which is correct.

**A2.2 PatchCategoryApplier** (`Main/PatchCategoryApplier.cs` equals `346f3e093^` after WP104; WP104's test file lacks three 1.5.x `[TestCategory("RequiresGame")]` lines, outside these hunks, so both apply; checked against `998ae7e95`):
`git show 346f3e093 -- Main/PatchCategoryApplier.cs TAOM.Tests/Infrastructure/PatchCategoryApplierTests.cs | git apply`
Expected: silent, exit 0. If it fails: STOP.

**A2.3 Shared-file edits** (Edit tool; the 1.5.x text is quoted, keep it exact):

1. `Dependencies/Foundation/PatchShieldPolicy.cs`: in the doc comment of `ExcludedTargetMethods`, at the
   end of its first paragraph, append the sentence `It also lists targets that are not hot but where a
   shield finalizer would change behaviour an observe-only TAOM patch promises to leave alone (the
   load-time stamps).` (as `///` lines, before `</summary>`). Then insert directly before the closing `};` of `ExcludedTargetMethods`:

```csharp
        // The load-time stamps (docs/features/load-time-stamps.md): observe-only TAOM patches that run
        // once per XML type, or once per lifecycle event, per load, so they are not hot. A shield
        // finalizer here would swallow a missing-API exception that propagates today (ending a
        // dispatch early, or returning a null merged document) and add one Harmony.Patch per target
        // to every player's first game start. LoadTimeStampsBindingTests walks the real targets.
        "TaleWorlds.ObjectSystem.MBObjectManager.LoadXML",
        "TaleWorlds.ObjectSystem.MBObjectManager.CreateMergedXmlFile",
        // The load-time stamps' campaign-handler timing: one call per lifecycle event per load. A shield
        // swallow here would end the dispatch early and skip the remaining listeners and receivers.
        "TaleWorlds.CampaignSystem.CampaignEventDispatcher.OnNewGameCreated",
        "TaleWorlds.CampaignSystem.CampaignEventDispatcher.OnGameEarlyLoaded",
        "TaleWorlds.CampaignSystem.CampaignEventDispatcher.OnGameLoaded",
        "TaleWorlds.CampaignSystem.CampaignEventDispatcher.OnSessionStart",
        "TaleWorlds.CampaignSystem.CampaignEventDispatcher.OnAfterSessionStart",
```

2. `Main/Composition/FeatureModules.cs`: append `        new Features.LoadTimeStamps.LoadTimeStampsModule(),`
   as the last entry of `All`.
3. `BattleLoadDiagnosticsSettings.cs`: after the last property, before the class's closing brace, add
   one blank line and the four lines of
   `git show 346f3e093:Main/Features/BattleLoadDiagnostics/BattleLoadDiagnosticsSettings.cs | sed -n '93,96p'`
   (the `[SettingPropertyGroup("Load-Time Stamps")]` group, the `SettingPropertyBool` with its
   `HintText`, and `public bool EnableLoadTimeStamps { get; set; } = false;`).
4. `IBattleLoadDiagnosticsSettingsProvider.cs`: as the last member add a blank line, then
   `    /// <summary>The detailed load-time stamps (default false); read at game start, game initialization and each campaign dispatch.</summary>`
   and `    bool LoadTimeStampsEnabled { get; }`.
5. `BattleLoadDiagnosticsSettingsProvider.cs`: after the `MemorySamplerEnabled` property add a blank line,
   `    public bool LoadTimeStampsEnabled =>` and `        BattleLoadDiagnosticsSettings.Instance?.EnableLoadTimeStamps ?? false;`.
6. `CoopSettingsRelevance.cs`: in the `Instrumentation` set, after the line holding
   `"EnableEnlistmentDiagnostics", "EnableFieldCommissionDiagnostics",` add
   `        // The load-time stamps' detail toggle: log lines, never a computation.` and
   `        "EnableLoadTimeStamps",`.
7. `SettingsFingerprintTests.cs`: raise the `reflected:` number of
   `AssertSplit(typeof(BattleLoadDiagnosticsSettings), ...)` by exactly 1 from its live value
   (`covered` unchanged).
8. `docs/features/coop-interop.md` ("What is NOT done"): raise by 1 the bold total (`**243**` at
   `1b00d1acd`), the BattleLoadDiagnosticsSettings count in the split, and the excluded count (`The 59
   excluded`), each from its live value. `docs/features/bannerlord-together-compat.md`: raise the total in
   `TAOM's 243 MCM settings` by 1 from its live value.
9. `BattleLoadDiagnosticsSettingsProviderTests.cs`: add the test from
   `git show 346f3e093 -- TAOM.Tests/Features/BattleLoadDiagnostics/BattleLoadDiagnosticsSettingsProviderTests.cs`
   (`LoadTimeStampsEnabled_NoMcmInstance_DefaultsFalse`) after the class's first test.
10. `ReflectionSiteBindingTests.cs`: insert the two comment lines and ten `[DataRow]`s that
    `git show 346f3e093 -- TAOM.Tests/Migration/ReflectionSiteBindingTests.cs` adds directly before the line
    `    public void ReflectionSite_ResolvesAgainstInstalledEngine(`.
11. `docs/features/battle-load-diagnostics.md`: append as the last row of the settings table that holds the
    `EnableBattleLoadDiagnostics` row:
    `` | `EnableLoadTimeStamps` | `false` | The detailed load-time lines ([load-time-stamps.md](load-time-stamps.md)). Independent of the master toggle. Local-only for co-op (`CoopSettingsRelevance`). | ``

**A2.4 Main/SubModule.cs** (insert exactly; nothing else changes):

| After the line | Insert |
|---|---|
| `        FeatureModuleHooks.RunPhase(ApplyPhase.ProcessLoad, TryPatchCategory);` | `        // Load-time stamps (docs/features/load-time-stamps.md): phase total now, per-category lines at game init.` and `        _patches.EndPhase("OnSubModuleLoad");` |
| `            FeatureModuleHooks.RunPhase(ApplyPhase.MainMenu, TryPatchCategory);` | `            _patches.EndPhase("MainMenu");` |
| `        base.OnGameStart(game, gameStarterObject);` | `        // Load-time stamps (docs/features/load-time-stamps.md): null unless "Enable Load-Time Stamps" is on.` and `        var hookStamp = Features.LoadTimeStamps.LoadTimeStampsHooks.StartHook("OnGameStart", game?.GameType?.GetType().Name);` |
| the session-snapshot `catch { /* diagnostic is best-effort, never break OnGameStart */ }` | `        hookStamp?.Mark("session_snapshot");` |
| the closing brace of `if (gameStarterObject is CampaignGameStarter campaignStarter) { ... }` | `        hookStamp?.Mark("hand_wired");` |
| `        FeatureModuleHooks.AddGameStartContent(gameStarterObject);` | `        hookStamp?.Mark("feature_modules");` and `        hookStamp?.End();` |
| `        base.OnGameInitializationFinished(game);` | `        // Load-time stamps (docs/features/load-time-stamps.md): the every-game part, before the guard.` and `        var hookStamp = Features.LoadTimeStamps.LoadTimeStampsHooks.StartHook("OnGameInitializationFinished", game?.GameType?.GetType().Name);` |
| `        StampSaveLoadPhase(Features.SaveLoadDiagnostics.Domain.SaveLoadPhase.GameInitializationFinished);` | `        hookStamp?.Mark("save_load_stamp");`, `        Features.LoadTimeStamps.LoadTimeStampsHooks.LogLoadXmlSummary();`, `        hookStamp?.End();` |
| `        _gameInitPatchesApplied = true;` | `        var onceStamp = Features.LoadTimeStamps.LoadTimeStampsHooks.StartHook("GameInitOnce", null);` |
| `        FeatureModuleHooks.RunPhase(ApplyPhase.GameInit, TryPatchCategory);` | `        _patches.EndPhase("GameInit");` and `        _patches.WriteHeldCategoryLines(Features.LoadTimeStamps.LoadTimeStampsHooks.DetailEnabled);` |
| the game-init `ReportPatchFailures(...)` line | `        onceStamp?.Mark("patch_categories");` |
| `        ManualPatchApplicator.ApplyAll(_harmony);` | `        onceStamp?.Mark("manual_patches");` |
| the closing brace of the Harmony census `catch`, the method's last block | `        onceStamp?.End();` (must be the method's last statement) |
| `            FeatureModuleHooks.RunPhase(ApplyPhase.FirstMission, TryPatchCategory);` | `            _patches.EndPhase("Mission");` and `            _patches.WriteHeldCategoryLines(Features.LoadTimeStamps.LoadTimeStampsHooks.DetailEnabled);` |

**A2.5 1.4.x adaptations of transplanted files**:

1. `TAOM.Tests/Features/LoadTimeStamps/LoadTimeStampsWiringTests.cs`, test
   `SubModule_OnGameInitializationFinished_LogsTheLoadXmlSummaryBeforeTheGuard`: replace
   `IndexOf(body, "hookStamp?.Mark(\"armour_gate\");") < summary, "the summary must follow the armour gate step"`
   with `IndexOf(body, "hookStamp?.Mark(\"save_load_stamp\");") < summary, "the summary must follow the save/load stamp step"`
   (1.4.x has no MonsterSize or ArmourAcquisition step).
2. Re-cite to v1.4.8: in `Main/Adapters/CampaignListenerAdapter.cs` replace both `v1.5.3 MbEvent` with
   `v1.4.8 MbEvent` (lines 15 and 175, same line numbers on v1.4.8); in
   `Main/Features/LoadTimeStamps/LifecycleTimingService.cs` replace `(v1.5.3 CampaignEvents.cs:2088-2116)` with
   `(v1.4.8 CampaignEvents.cs:2068-2096)`; in `TAOM.Tests/Features/LoadTimeStamps/CampaignListenerAdapterTests.cs`
   replace `(v1.5.3 MbEvent`1.cs:46-79)` with `(v1.4.8 MbEvent`1.cs:46-79)`.
3. TDD order: first add A2.6's `LoadTimeStampsModuleTests` test and build (RED: `CS1729`, no
   `LoadTimeStampsModule` constructor takes 1 argument), then make this edit (GREEN).
   `Main/Features/LoadTimeStamps/LoadTimeStampsModule.cs` (the pairing guard; keep the rest of the 1.5.x file):
   add `using System;` and `using TAOM.Core.Diagnostics;`; before the class doc comment's closing `/// </summary>`
   add `/// <para>1.4.x (plans/118-campaign-load-time.md): both categories apply only when the loaded
   TAOM.Dependencies.dll keeps PatchShield off their seven targets (<see cref="ShieldExclusionCheck"/>, #371);
   otherwise one warning replaces the two ready lines and the engine methods stay unpatched.</para>` (as `///`
   lines); insert block 1 after the `LifecycleCategory` constant and block 2 after the `Categories` array; replace
   `    public override IReadOnlyList<PatchCategoryDecl> PatchCategories => Categories;` with block 3; in
   `InitializeStatics` replace the last two lines (`logger.LogInfo(LoadTimeStampLines.LoadXmlReady());` and the
   `LifecycleReady` line) with block 4.

```csharp
    // block 1
    /// <summary>The engine methods the two categories patch, named as PatchShieldPolicy names them.</summary>
    internal static readonly IReadOnlyList<(string Type, string Method)> ShieldTargets = new[]
    {
        ("TaleWorlds.ObjectSystem.MBObjectManager", "LoadXML"),
        ("TaleWorlds.ObjectSystem.MBObjectManager", "CreateMergedXmlFile"),
        ("TaleWorlds.CampaignSystem.CampaignEventDispatcher", "OnNewGameCreated"),
        ("TaleWorlds.CampaignSystem.CampaignEventDispatcher", "OnGameEarlyLoaded"),
        ("TaleWorlds.CampaignSystem.CampaignEventDispatcher", "OnGameLoaded"),
        ("TaleWorlds.CampaignSystem.CampaignEventDispatcher", "OnSessionStart"),
        ("TaleWorlds.CampaignSystem.CampaignEventDispatcher", "OnAfterSessionStart"),
    };

    // block 2
    private readonly Func<IReadOnlyList<(string Type, string Method)>, string?> _shieldCheck;
    private string? _shieldProblem;

    public LoadTimeStampsModule() : this(ShieldExclusionCheck.Problem) { }

    internal LoadTimeStampsModule(Func<IReadOnlyList<(string Type, string Method)>, string?> shieldCheck) =>
        _shieldCheck = shieldCheck ?? throw new ArgumentNullException(nameof(shieldCheck));

    // block 3
    public override IReadOnlyList<PatchCategoryDecl> PatchCategories =>
        _shieldProblem == null ? Categories : Array.Empty<PatchCategoryDecl>();

    // block 4 (inside InitializeStatics, after logger.LogInfo(LoadTimeStampLines.Ready());)
        _shieldProblem = _shieldCheck(ShieldTargets);
        if (_shieldProblem != null)
        {
            logger.LogWarning("[LoadXml] [Lifecycle] stamps off for this session: " + _shieldProblem);
            return;
        }
        logger.LogInfo(LoadTimeStampLines.LoadXmlReady());
        logger.LogInfo(LoadTimeStampLines.LifecycleReady(resolver.Resolve<ICampaignListenerAdapter>().BindingProblem));
```

   Do not copy the `// block N` marker lines into the file.

**A2.6 The guard's tests (write them, then confirm the RED one)**:

- `TAOM.Tests/Features/LoadTimeStamps/LoadTimeStampsModuleTests.cs` (its usings already cover this) add:

```csharp
    // 1.4.x pairing guard (plans/118): with a TAOM.Dependencies.dll that shields the targets, neither patch group
    // applies and one warning says why.
    [TestMethod]
    public void InitializeStatics_WhenTheLoadedDependenciesShieldTheTargets_DeclaresNoCategory_AndWarns()
    {
        var logger = Substitute.For<IModLogger>();
        var container = new Container();
        container.RegisterInstance(logger);
        container.RegisterInstance(Substitute.For<IBattleLoadDiagnosticsSettingsProvider>());
        var module = new LoadTimeStampsModule(_ => "stale Dependencies");
        module.RegisterServices(container);
        container.RegisterInstance<ICampaignListenerAdapter>(new FakeCampaignListenerAdapter(), IfAlreadyRegistered.Replace);
        try
        {
            module.InitializeStatics(container);

            Assert.AreEqual(0, module.PatchCategories.Count);
            logger.Received(1).LogWarning(Arg.Is<string>(s => s.Contains("stamps off for this session: stale Dependencies")));
            logger.DidNotReceive().LogInfo(LoadTimeStampLines.LoadXmlReady());
        }
        finally
        {
            LoadTimeStampsHooks.Initialize(null, null);
            LoadTimeStampsHooks.InitializeLoadXml(null);
            LoadTimeStampsHooks.InitializeLifecycle(null);
        }
    }
```

- `TAOM.Tests/Core/Diagnostics/ShieldExclusionCheckTests.cs`: add `using TAOM.Features.LoadTimeStamps;` and

```csharp
    // The pairing pin: the TAOM.Dependencies this repo builds keeps PatchShield off every load-time stamp target.
    [TestMethod]
    public void Problem_AgainstTheBuiltDependencies_TheLoadTimeStampTargetsAreExcluded()
    {
        Assert.IsNull(ShieldExclusionCheck.Problem(LoadTimeStampsModule.ShieldTargets));
    }
```

**Verify (RED for the pin)**: temporarily comment out the two `"TaleWorlds.ObjectSystem.MBObjectManager...`
entries you added to `PatchShieldPolicy.cs`, Build, Filtered `ShieldExclusionCheckTests`: `Failed: 1` (the pin,
naming `MBObjectManager.LoadXML`). Restore the two entries exactly (`git diff -- Dependencies` shows the full block
of A2.3 step 1).

**A2.7 Registry, reflection catalogue, doc**:

1. `docs/reference/harmony-patch-registry.md`: take the two sections
   `MSYS_NO_PATHCONV=1 git show e45f6df2c:docs/reference/harmony-patch-registry.md | sed -n '/^## Patch100_LoadTimeStamps_LoadXml/,/^## Patch101_/p' | sed '$d'`
   and insert them, keeping numeric order, before the first `## PatchNN_` heading whose number is above 100,
   or else before `## Patch_MissionTime_SetMovementOrder`. In the inserted text replace `(v1.5.3 \`Campaign.cs:1471\`)`
   with `(v1.4.8 \`Campaign.cs:1452\`)` and `the \`QuestManager\` receiver in v1.5.3` with
   `the \`IssueManager\` and \`QuestManager\` receivers in v1.4.8 (\`Campaign.cs:1370\`)`.
2. `docs/reference/taleworlds-api-snapshot/reflection-sites.md`: append the ten table rows that
   `git show 346f3e093 -- docs/reference/taleworlds-api-snapshot/reflection-sites.md` adds to the end of the
   Category B table, and below the table one line:
   `Status (<DATE>, 1.4.x): LoadTimeStamps adds ten rows (the MbEvent listener records); all resolve against installed v1.4.8 (ReflectionSiteBindingTests).`
3. `docs/features/load-time-stamps.md` (Edit tool):
   - after the title line insert a blank line and:
     `> **On the 1.4.x line** (ported <DATE>, plans/118-campaign-load-time.md, from \`346f3e093\`, #722): the same code,`
     `> wired through the feature-module runner. Both patch groups apply only when the loaded TAOM.Dependencies.dll`
     `> keeps PatchShield off their seven targets (\`ShieldExclusionCheck\`); otherwise one warning,`
     `> \`[LoadXml] [Lifecycle] stamps off for this session: <reason>\`, replaces the two ready lines. Engine lines are`
     `> re-cited to v1.4.8. Timings quoted from the maintainer's desktop are 1.5.x-line figures; no 1.4.8 run exists yet.`
   - replace `` `save_load_stamp`, `monster_size`, `armour_gate` `` with `` `save_load_stamp` ``;
   - replace `` own `ObjectManagerAdapter` and `MonsterSizeCatalogAdapter`) `` with `` own `ObjectManagerAdapter`) ``;
   - replace `` (in v1.5.3 the `QuestManager`, plus any receiver a mod adds) `` with
     `` (in v1.4.8 the `IssueManager` and the `QuestManager`, `Campaign.cs:1370`, plus any receiver a mod adds) ``;
   - replace `(v1.5.3 decompile)` with ``(v1.4.8 `MbEvent`1.cs:46-79`)``;
   - replace `` (v1.5.3 `Campaign.cs:1410`) before its first `LoadXML` `` and the following
     `` (`Game.LoadBasicFiles`, reached at :1417 or :1426) `` with
     `` (v1.4.8 `Campaign.cs:1391`) before its first `LoadXML` `` and
     `` (`Game.LoadBasicFiles`, reached through `InitializeDefaultCampaignObjects` at :1398 or :1524, body :1470) ``;
   - in "Changelog" add `- <DATE>: ported to the 1.4.x line (plans/118-campaign-load-time.md), with the
     TAOM.Dependencies pairing guard.`; in "GitHub Issue" add `1.4.x: port of #722; pairing guard #NNN.`

**Verify**: Build `0 Error(s)`. Filtered
`TAOM.Tests.Features.LoadTimeStamps|ShieldExclusionCheckTests|PatchCategoryApplierTests|BattleLoadDiagnosticsSettingsProviderTests|SettingsFingerprintTests|ReflectionSiteBindingTests|TAOM.Tests.Composition|SettingRequireRestartPostureTests`:
`Failed: 0`. The LoadTimeStamps classes total 139 (planner's count from the 1.5.x files plus the guard test);
`ShieldExclusionCheckTests` 5. Then the Binding gate: `Failed: 0`. Then the full Test: `Total` = T0 + 165, `Failed: 0`, `Skipped` = T0's. Docs
command: unchanged `dead_links` and `orphan_features`, `ai_dashes: 0`.

**Commit A2**: stage the 37 added files, `Main/Features/LoadTimeStamps/LoadTimeStampsModule.cs` (already among
them), and every file A2.2 to A2.7 touched, by explicit path (`git status --porcelain` must then list nothing
unstaged or untracked). Message:

```text
feat(load-stamps): v2.0.28 - time module XML, patch groups and handlers

taom_debug.log now says where a campaign load's time goes. Always on:
one [LoadXml] line per MBObjectManager.LoadXML call (one per XML type)
with its files, XSLTs and time split into the merge and the object
creation, a [LoadXml] summary at every game initialization, a
"[PatchApply] phase=... scope=total" line for each of the four patch
apply phases, and one "[Lifecycle] dispatch=..." line for each
campaign dispatch of a new game, a loaded save and the session start.
With "Enable Load-Time Stamps" on (MCM, Battle Load Diagnostics,
default off) it also writes one [PatchApply] line per patch group,
[LoadPhase] steps of TAOM's game start and game initialization, and a
[Lifecycle] line for every campaign handler of 10 ms or more.

The stamps only observe. PatchShield in this build leaves the seven
patched methods alone (MBObjectManager.LoadXML and CreateMergedXmlFile
and five CampaignEventDispatcher lifecycle methods), so a missing-API
exception there behaves as in the unpatched game. With an older
TAOM.Dependencies.dll the module applies neither patch group and logs
one warning instead. On the 1.4.8 line one player's 12 loads spent
292.9 s in the XML merge window and nothing in taom_debug.log said so.

Research: taom-src v1.4.8 MBObjectManager, CampaignEventDispatcher,
CampaignEvents, MbEvent`1, MbEvent`2, Campaign
Not-tested: in game (ready, per-type and dispatch lines; the detail
 lines after a restart with the toggle on)
Save-compat: no save data
Ported-from: 346f3e093ccc2c146107a3b7e005c0c06963cd85
```

**Part A done criteria**: build exit 0; full suite `Failed: 0`, `Total` = T0 + 165;
`grep -c '_patches.EndPhase(' Main/SubModule.cs` prints `4`; `grep -c 'LoadTimeStampsModule()' Main/Composition/FeatureModules.cs`
prints `1`; `grep -c 'CampaignEventDispatcher.On' Dependencies/Foundation/PatchShieldPolicy.cs` prints `5`;
`git diff --stat 346f3e093 HEAD -- Main/Features/LoadTimeStamps Main/Adapters/CampaignListenerAdapter.cs Main/Adapters/ICampaignListenerAdapter.cs Main/Core/Diagnostics/IStampClock.cs Main/Core/Diagnostics/StopwatchStampClock.cs TAOM.Tests/Features/LoadTimeStamps`
lists only `LoadTimeStampsModule.cs`, `LifecycleTimingService.cs`, `CampaignListenerAdapter.cs`,
`CampaignListenerAdapterTests.cs`, `LoadTimeStampsModuleTests.cs`, `LoadTimeStampsWiringTests.cs`; two commits since
`bannerlord-1.4.5`; `git status --porcelain` empty. Hand-off: SHAs, T0 and final counts.

---

## Part B: the XML merge fast path (`a27c6b338`, with `43ca92ee9`'s patch comment) and two IL tests

Requires Part A committed (`grep -c 'LoadTimeStampsModule()' Main/Composition/FeatureModules.cs` is `1`).

### Step B1: XmlMerge (end-state transplant from `e45f6df2c`, doc from `a27c6b338`)

Simulation onto `1b00d1acd` conflicts in `FeatureModules.cs`, `CoopVetoClassificationTests.cs`,
`BindingGateRunSettingsTests.cs`, `ReflectionSiteBindingTests.cs`, `binding-gate.runsettings` and bookkeeping.
Not taken: `binding-gate.runsettings`, `BindingGateRunSettingsTests.cs` (they arrive with WP122, which takes that hunk), the `.claude/skills/engine-bump/SKILL.md`
hunk (harness, WP143), review records and lessons.

**B1.1 Transplant**:

```bash
cd /c/Users/mikew/source/repos/taom-port/118 && \
for f in $(git show a27c6b338 --name-only --diff-filter=A --format= -- Main TAOM.Tests); do \
  mkdir -p "$(dirname "$f")" && git show "e45f6df2c:$f" > "$f" || echo "FAILED $f"; done; \
git show a27c6b338:docs/features/xml-merge-fast-path.md > docs/features/xml-merge-fast-path.md; \
git show a27c6b338 -- TAOM.Tests/Migration/IlCallScanner.cs docs/reference/engine/object-system-mbobjectmanager.md | git apply && echo applied
```

Expected: no `FAILED`, `applied` (skip `IlCallScanner.cs` in the `git apply` if Step 0.3 found `Fingerprint`
already present). 27 code and test files plus the doc are new.

**B1.2 Guard adaptation** (TDD: add the two tests below first and build: RED with `CS1729` and `CS0117`; then
edit) of `Main/Features/XmlMerge/XmlMergeModule.cs` (keep the rest of the 1.5.x file): add
`using System;`, `using TAOM.Core.Diagnostics;` and `using TAOM.Core.Logging;`; before the class doc comment's
closing `/// </summary>` add `/// <para>1.4.x (plans/118-campaign-load-time.md): Patch99 applies only when the
loaded TAOM.Dependencies.dll keeps PatchShield off CreateMergedXmlFile (<see cref="ShieldExclusionCheck"/>, #371);
otherwise the start line reads "fast path off" with the reason and every merge runs the engine's own code.</para>`
(as `///` lines); insert block 1 after the `PatchCategory` constant and block 2 after the `Categories` array;
replace the body of `InitializeStatics` with block 3; replace
`    public override IReadOnlyList<PatchCategoryDecl> PatchCategories => Categories;` with block 4.

```csharp
    // block 1
    /// <summary>The engine method Patch99 patches, named as PatchShieldPolicy names it.</summary>
    internal static readonly IReadOnlyList<(string Type, string Method)> ShieldTargets = new[]
    {
        ("TaleWorlds.ObjectSystem.MBObjectManager", "CreateMergedXmlFile"),
    };

    // block 2
    private readonly Func<IReadOnlyList<(string Type, string Method)>, string?> _shieldCheck;
    private string? _shieldProblem;

    public XmlMergeModule() : this(ShieldExclusionCheck.Problem) { }

    internal XmlMergeModule(Func<IReadOnlyList<(string Type, string Method)>, string?> shieldCheck) =>
        _shieldCheck = shieldCheck ?? throw new ArgumentNullException(nameof(shieldCheck));

    // block 3 (the whole body of InitializeStatics)
        _shieldProblem = _shieldCheck(ShieldTargets);
        if (_shieldProblem != null)
        {
            resolver.Resolve<IModLogger>().LogWarning(XmlMergeLines.Off(_shieldProblem));
            return;
        }
        var service = resolver.Resolve<XmlMergeService>();
        MBObjectManager_CreateMergedXmlFile_Patch.Initialize(service);
        service.LogConfigurationHeader();

    // block 4
    public override IReadOnlyList<PatchCategoryDecl> PatchCategories =>
        _shieldProblem == null ? Categories : Array.Empty<PatchCategoryDecl>();
```

Do not copy the `// block N` marker lines into the file.

Tests (write before building): in `TAOM.Tests/Features/XmlMerge/XmlMergeWiringTests.cs` add

```csharp
    // 1.4.x pairing guard (plans/118): under a TAOM.Dependencies.dll that shields CreateMergedXmlFile, Patch99 is
    // never declared, the start line says why, and the patch class is never handed the service.
    [TestMethod]
    public void InitializeStatics_WhenTheLoadedDependenciesShieldTheTarget_DeclaresNoCategory_AndSaysFastPathOff()
    {
        var logger = Substitute.For<IModLogger>();
        var container = new Container();
        container.RegisterInstance(logger);
        var module = new XmlMergeModule(_ => "stale Dependencies");

        module.InitializeStatics(container);

        Assert.AreEqual(0, module.PatchCategories.Count);
        logger.Received(1).LogWarning(Arg.Is<string>(s => s.Contains("fast path off: stale Dependencies")));
    }
```

and in `ShieldExclusionCheckTests.cs` add `using TAOM.Features.XmlMerge;` and

```csharp
    [TestMethod]
    public void Problem_AgainstTheBuiltDependencies_TheXmlMergeTargetIsExcluded()
    {
        Assert.IsNull(ShieldExclusionCheck.Problem(XmlMergeModule.ShieldTargets));
    }
```

**B1.3 Re-cites and the players' module order** (Edit tool, exact substrings):

| File | Replace | With |
|---|---|---|
| `Main/Adapters/XmlMergeEngineAdapter.cs` | `<c>MBObjectManager</c> (v1.5.3)` / `(MBObjectManager.cs:1339)` | `<c>MBObjectManager</c> (v1.4.8)` / `(MBObjectManager.cs:1343)` |
| `Main/Features/XmlMerge/Hooks/MBObjectManager_CreateMergedXmlFile_Patch.cs` | `v1.5.3 MBObjectManager.cs:786-797` | `v1.4.8 MBObjectManager.cs:786-797` |
| `Main/Features/XmlMerge/XmlMergeService.cs` | `(MBObjectManager.cs:962-978)` | `(MBObjectManager.cs:966-982)` |
| `Main/Features/XmlMerge/XsltTransformCache.cs` | `(v1.5.3, MBObjectManager.cs:980-991)` | `(v1.4.8, MBObjectManager.cs:984-995)` |
| `TAOM.Tests/Features/XmlMerge/XmlMergeBindingTests.cs` | `// v1.5.3 MBObjectManager.cs:962-978:` | `// v1.4.8 MBObjectManager.cs:966-982:` |
| `TAOM.Tests/Features/XmlMerge/RecordingXmlMergeEngine.cs` | `(MBObjectManager.cs:1008-1031)` | `(MBObjectManager.cs:1012-1035)` |
| `TAOM.Tests/Features/XmlMerge/LiveMergeListBuilder.cs` | `(v1.5.3, MBObjectManager.cs:873-914;` / `(XmlResource.cs:258-326)` / `(MBObjectManager.cs:945-960)` / `order's optional FastMode.` | `(v1.4.8, MBObjectManager.cs:877-918;` / `(XmlResource.cs:149-189)` / `(MBObjectManager.cs:949-964)` / `order's optional BetterTime.` |
| same file | the `DefaultModuleOrder` value `"TAOM.Dependencies;Native;SandBoxCore;CustomBattle;SandBox;StoryMode;BirthAndDeath;FastMode;LOTRLOME_Armory;TAOM_Map;TAOM"` | `"TAOM.Dependencies;Native;SandBoxCore;CustomBattle;SandBox;StoryMode;LOTRLOME_Armory;TAOM_Map;TAOM;BetterTime"` (the public 1.4.8 players' list) |
| same file | `/// <summary>Default-order modules the gate lets be absent: the v1.5.3 install has no FastMode folder.</summary>` and `new[] { "FastMode" }` | `/// <summary>Default-order modules the gate lets be absent: BetterTime, a third-party time mod the players load last, which ships no object XML and which the laptop's v1.4.8 install lacks.</summary>` and `new[] { "BetterTime" }` |
| `TAOM.Tests/Features/XmlMerge/LiveGateRules.cs` | `except FastMode in the default order (the v1.5.3` / `install has no FastMode folder)` | `except BetterTime in the default order (the v1.4.8` / `laptop install has no BetterTime folder)` |
| `TAOM.Tests/Features/XmlMerge/LiveGateRulesTests.cs` | every `FastMode`; `// Arrange: the v1.5.3 install has no FastMode folder.` | `BetterTime`; `// Arrange: the v1.4.8 laptop install has no BetterTime folder.` |
| `TAOM.Tests/Features/XmlMerge/XmlMergeLiveEquivalenceTests.cs` | `(FastMode may be absent from the default order only)` | `(BetterTime may be absent from the default order only)` |

Check: `grep -rn "FastMode\|BirthAndDeath\|v1\.5\.3" Main/Features/XmlMerge Main/Adapters/XmlMergeEngineAdapter.cs TAOM.Tests/Features/XmlMerge`
prints nothing.

**B1.4 Shared files**:

1. `FeatureModules.cs`: append `        new Features.XmlMerge.XmlMergeModule(),` as the last entry.
2. `Main/SubModule.cs`: directly after the `        hookStamp?.End();` that Part A put after `LogLoadXmlSummary()`
   in `OnGameInitializationFinished` (before `if (_gameInitPatchesApplied) return;`), insert a blank line and:

```csharp
        // [XmlMerge] summary (docs/features/xml-merge-fast-path.md): one line per game for the module-XML merges
        // since the previous one. Every game init, before the once-per-process guard: Campaign.OnInitialize and
        // CustomGame.OnInitialize call this after their XML loads. The hook never throws.
        Features.XmlMerge.Hooks.MBObjectManager_CreateMergedXmlFile_Patch.LogWindowSummary(game?.GameType?.GameTypeStringId);
```

3. `CoopVetoClassificationTests.cs`: insert the five-line `["MBObjectManager_CreateMergedXmlFile_Patch"] = new(CoopVeto.ReviewedSafe, ...)`
   entry from `git show a27c6b338 -- TAOM.Tests/Features/CoopInterop/CoopVetoClassificationTests.cs` directly before
   `        // --- Parked ---`.
4. `ReflectionSiteBindingTests.cs`: insert its two comment lines and the `CreateDocumentFromXmlFile` `[DataRow]`
   (same `git show`) before `    public void ReflectionSite_ResolvesAgainstInstalledEngine(`.
5. `docs/reference/engine/object-system-mbobjectmanager.md`: in the paragraph B1.1 applied, replace
   `**The merge loop (v1.5.3).**` with `**The merge loop (v1.4.8; the same code as v1.5.3).**`.
6. Registry: insert `MSYS_NO_PATHCONV=1 git show e45f6df2c:docs/reference/harmony-patch-registry.md | sed -n '/^## Patch99_XmlMergeFastPath/,/^## Patch100_/p' | sed '$d'`
   directly before `## Patch100_LoadTimeStamps_LoadXml`.
7. `reflection-sites.md`: append the `CreateDocumentFromXmlFile` row `a27c6b338` adds to the Category B table and
   the line `Status (<DATE>, 1.4.x): XmlMerge adds one row (MBObjectManager.CreateDocumentFromXmlFile); it resolves against installed v1.4.8.`

**B1.5 Build and gates**: Build `0 Error(s)`. Filtered
`TAOM.Tests.Features.XmlMerge|IlFingerprintTests|ShieldExclusionCheckTests|CoopVetoClassificationTests|ReflectionSiteBindingTests|TAOM.Tests.Composition|PatchCategoryApplierTests`:
`Failed: 0`; XmlMerge plus IlFingerprint classes total 128 (planner's count, 2 of them `Skipped`: the harness without
its variable); `ShieldExclusionCheckTests` 6. `MirroredEngineBodies_CallExactlyThePinnedSequence` must pass unchanged
(the verification pass matched the five v1.4.8 bodies to the pins by IL; never re-pin to make it pass). Binding gate:
`Failed: 0`.

**B1.6 Live harness (the gate for shipping Patch99)**: run the Live merge harness command; save the console output
to `118-scratch/xmlmerge-harness.txt`. Expected `Passed: 2` (Campaign and CustomGame), every type identical, the
four heavy types' fast total at most 50 percent of the engine's, and only BetterTime allowed missing. Record
`Modules/TAOM/SubModule.xml`'s `<Version>` on the install (the harness reads installed module XML). A failure of
any kind: STOP; do not commit B1, do not loosen a rule or the bar, report the message and the table. (Part A stays
landed; Parts C to E continue.)

**B1.7 Doc** (`docs/features/xml-merge-fast-path.md`, Edit tool):

1. After the title insert a blank line and an "On the 1.4.x line" block: ported <DATE> (plan 118, `a27c6b338`,
   #724); registered through `XmlMergeModule` and the runner; Patch99 applies only when the loaded
   TAOM.Dependencies.dll excludes `CreateMergedXmlFile` (`ShieldExclusionCheck`, else
   `[XmlMerge] fast path off: <reason>`); the v1.4.8 engine bodies equal v1.5.3's by text and IL except
   `MergeElements` (four lines longer on v1.4.8, `:820-875`), which the fast path calls rather than replaces;
   1.4.x has no `binding-gate.runsettings` (plan 122 brings it and rewrites this sentence), so wherever this page says `/verify-bindings` runs the harness,
   on 1.4.x that holds only with `TAOM_RUN_BENCHMARKS=1` set; figures below dated 2026-10-03 are the 1.5.x desktop's.
2. Replace ``(v1.5.3, `MBObjectManager.cs:962-978`)`` with ``(v1.4.8, `MBObjectManager.cs:966-982`)``.
3. Replace the whole section `### Shipping order: plan 040 first` (heading to the line before `### Known limits`)
   with `### Shipping order and the pairing guard` and a paragraph saying: PatchShield's second pass would attach its
   value-returning finalizer to `CreateMergedXmlFile` unless `PatchShieldPolicy.ExcludedTargetMethods` lists it;
   with it, a `MissingMethodException`, `MissingFieldException` or `TypeLoadException` from the engine's own merge
   is swallowed, the method returns null, and `LoadXML` (v1.4.8 `MBObjectManager.cs:786-797`, the merge outside its
   `try`, `LoadXml(doc)` inside a catch-all) loads that type empty; on 1.4.x the entry lands with the load-time
   stamps (Part A of plan 118) in the same TAOM.Dependencies build, and `XmlMergeModule` declares Patch99 only when
   the loaded TAOM.Dependencies.dll confirms it (`ShieldExclusionCheck`), because players have run a newer TAOM.dll
   with an older TAOM.Dependencies.dll (#371).
4. In "How to run the harness", replace the sentence that starts `The module order defaults to` through
   `install without it).` (the sentence is wrapped over five lines) with: the default order is the public 1.4.8 players' list
   `TAOM.Dependencies;Native;SandBoxCore;CustomBattle;SandBox;StoryMode;LOTRLOME_Armory;TAOM_Map;TAOM;BetterTime`;
   only `BetterTime` may be absent (it ships no object XML); `TAOM_XMLMERGE_MODULES` sets another order, every module
   of which must exist.
5. Directly under `## Performance` add `### v1.4.8, laptop harness (<DATE>)` with the installed TAOM version and the
   harness table from `118-scratch/xmlmerge-harness.txt` (per heavy type engine ms and fast ms, the heavy share),
   and label the existing table `On the 1.5.x line (desktop, v1.5.3):`.
6. "Changelog": add `- <DATE>: ported to the 1.4.x line (plan 118) with the pairing guard and the players' module order.`;
   "GitHub Issue": add `1.4.x: port of #724.`

**Verify**: full Test `Total` = T0 + 295, `Failed: 0`, `Skipped` = T0's + 2; Docs command unchanged counts,
`ai_dashes: 0`.

**Commit B1**: stage the 28 new files and every file B1.2 to B1.7 touched. Message:

```text
perf(xml-merge): v2.0.28 - merge module XML in one document per load

Every campaign load and custom battle merges each module's ModuleData
XML into one document per type. On v1.4.8 the engine converts the
whole document merged so far to XDocument and back for every file and
compiles every XSLT on every call (MBObjectManager.cs:966-1010), so
the cost grows with the square of the merged size: one player's load
spent 27.2 s of 70.5 s there, 15.1 s on NPCCharacters alone.

The fast path (Patch99, XmlMergeModule) runs the same loop with the
engine's own loader, merge and converters in the engine's order, but
keeps one document across the files and compiles each XSLT once per
game process. Only validated merges take it. It stands aside while
another mod patches a method it bypasses, re-runs the engine's merge
on any exception, and turns itself off for the session if the engine
succeeds where it failed. It applies only when the loaded
TAOM.Dependencies.dll keeps PatchShield off CreateMergedXmlFile.

The live harness merged every type of the installed v1.4.8 module set
both ways, in the players' module order, for a campaign and a custom
battle: identical output, heavy types at <SHARE> percent of the
engine's time on the laptop. In-game saving not yet measured.

Research: taom-src v1.4.8 MBObjectManager merge bodies and XmlResource;
IL fingerprints of CreateMergedXmlFile, MergeTwoXmls, ApplyXslt,
ToXDocument, ToXmlDocument
Not-tested: in game (the [XmlMerge] start, per-type and summary lines;
 a saved-campaign load against the rgl merge window)
Save-compat: no save data
Ported-from: a27c6b33817f6375f793b37c3892f897450e4bfe
Ported-from: 43ca92ee99c064467113e603c19c58a5901b9093
```

Replace `<SHARE>` with the harness's heavy-type percentage (one decimal).

### Step B2: The supply route IL tests (`ab78564e0`, two tests; needs WP112)

Skip with a hand-off note if Step 0.3's WP112 lines failed.

1. Read `Main/Features/SupplyLines/SupplyRouteVisualService.cs` `SyncArrows`. If its fallback calls
   `IMapScene.GetHeightAtPoint` with a `CampaignVec2` (WP112 took the 1.5.x RCA's design D), take both 1.5.x tests
   verbatim. Otherwise (the 1.4.x fallback `scene.GetHeightAtPoint(position, BodyFlags.Moveable | BodyFlags.OnlyCollideWithRaycast, ref height)`)
   take the ComputeNavPathPoints test verbatim and the arrows test as rewritten below.
2. Into `TAOM.Tests/Features/SupplyLines/SupplyNativeQueryTests.cs` add, if absent, `using System.Linq;`,
   `using System.Reflection;`, `using TAOM.Tests.Migration;`, then the helpers `FingerprintOf` and `IndexOf` and the
   test `ComputeNavPathPoints_ReadsTheActiveModelsLandRules_ForAddLandPath`, copied from
   `git show ab78564e0:TAOM.Tests/Features/SupplyLines/SupplyNativeQueryTests.cs | sed -n '86,116p'` (it starts
   with `private static string[] FingerprintOf(` and ends with that test's closing brace). The 1.4.x arrows test:

```csharp
    // Scene is sealed and native, so this pins the shape of the 1.4.x fallback: the heightmap read decides a branch
    // before the one terrain query, and the fallback asks the same scene for its collision height with
    // SandBox.MapScene.GetHeightAtPoint's land mask (BodyFlags.Moveable | BodyFlags.OnlyCollideWithRaycast = 144).
    [TestMethod]
    public void SyncArrows_BranchesOnTheHeightmap_BeforeTheTerrainQuery_AndFallsBackToTheSceneCollisionHeight()
    {
        var tokens = FingerprintOf(typeof(SupplyRouteVisualService), "SyncArrows", BindingFlags.NonPublic | BindingFlags.Instance);

        int check = IndexOf(tokens, "Scene::get_HasTerrainHeightmap()");
        int terrain = IndexOf(tokens, "Scene::GetTerrainHeightAndNormal(");
        int fallback = IndexOf(tokens, "Scene::GetHeightAtPoint(");

        Assert.IsTrue(check < terrain && terrain < fallback, "check, terrain query, then the fallback");
        Assert.IsTrue(Enumerable.Range(check + 1, terrain - check - 1).Any(i => tokens[i] == "brtrue" || tokens[i] == "brfalse"),
            "a conditional branch between the heightmap read and the terrain query");
        Assert.AreEqual("ldc.i4 144", tokens[fallback - 1], "the fallback passes the land mask Moveable | OnlyCollideWithRaycast");
        Assert.IsFalse(tokens.Any(t => t.Contains("IMapScene::GetTerrainHeightAndNormal(")),
            "the unguarded map-wrapper terrain query is not called");
        Assert.AreEqual(1, tokens.Count(t => t.Contains("GetTerrainHeightAndNormal(")), "one terrain query, behind the check");
    }
```

**Verify**: Build; Filtered `SupplyNativeQueryTests`: `Failed: 0`, two more tests than before. A failure: STOP and
report its message (never edit `Main/` source to make a pin pass). Full Test: `Total` = T0 + 297.

**Commit B2**: `git add TAOM.Tests/Features/SupplyLines/SupplyNativeQueryTests.cs`. Message:

```text
test(supply): v2.0.28 - pin the path query and the arrow fallback by IL

Two IL-fingerprint tests from the 1.5.x port of PR #1064, which needed
IlCallScanner.Fingerprint (now on this line with the XML merge fast
path). ComputeNavPathPoints must read the navigation model's terrain
exclusions and the distance model's two region costs before it calls
AddLandPath, and must never call the native path query itself.
SyncArrows must check the scene's terrain heightmap before the one
terrain query and fall back to the scene's collision height with the
land mask SandBox uses (Moveable | OnlyCollideWithRaycast): the 1.4.x
fallback, rewritten from the 1.5.x test, which pins a map-wrapper call.

Research: taom-src v1.4.8 Scene.GetHeightAtPoint, BodyFlags,
MobileParty.NavigationType
Ported-from: ab78564e064e173bef9f8d23d22d46bf86e3b3aa
```

**Part B done criteria**: build exit 0; full suite `Failed: 0`, `Total` = T0 + 297 (T0 + 295 if B2 was skipped),
`Skipped` = T0's + 2; `118-scratch/xmlmerge-harness.txt` shows `Passed: 2`; `grep -c 'XmlMergeModule()' Main/Composition/FeatureModules.cs`
prints `1`; `grep -c 'LogWindowSummary(' Main/SubModule.cs` prints `1`;
`git diff --stat e45f6df2c HEAD -- Main/Features/XmlMerge Main/Adapters/IXmlMergeEngineAdapter.cs Main/Adapters/XmlMergeEngineAdapter.cs TAOM.Tests/Features/XmlMerge TAOM.Tests/Migration/IlFingerprintTests.cs`
lists only `XmlMergeModule.cs`, `XmlMergeEngineAdapter.cs`, `MBObjectManager_CreateMergedXmlFile_Patch.cs`,
`XmlMergeService.cs`, `XsltTransformCache.cs`, `LiveGateRules.cs`, `LiveGateRulesTests.cs`, `LiveMergeListBuilder.cs`,
`RecordingXmlMergeEngine.cs`, `XmlMergeBindingTests.cs`, `XmlMergeLiveEquivalenceTests.cs`, `XmlMergeWiringTests.cs`;
`git status --porcelain` empty. Hand-off adds the harness table.

---

## Part C: the map-load timeline with plan 012's real-lower gate (one squashed commit)

### Step C1: MapLoadDiagnostics (end-state transplant from `a4492fa89`, hand wiring as on the 1.5.x tip)

Why squashed: as first shipped, `502f7cde9`'s loading-window postfix walked 12 stack frames and wrote a line on
every rendered frame of the main menu (84 MB per 35 minutes on 1.5.x); plan 012 fixes it, so the two land together.
The feature has no 1.5.x module, so it stays hand-wired (decision D2). Simulation: `502f7cde9` conflicts in `SubModule.cs`
(and bookkeeping); the plan 012 commits conflict only because the files are absent here.

**C1.1 Transplant**:

```bash
cd /c/Users/mikew/source/repos/taom-port/118 && \
for f in $(git ls-tree -r --name-only a4492fa89 -- Main/Features/MapLoadDiagnostics TAOM.Tests/Features/MapLoadDiagnostics); do \
  mkdir -p "$(dirname "$f")" && git show "a4492fa89:$f" > "$f" || echo "FAILED $f"; done; \
git show a4492fa89:docs/features/map-load-diagnostics.md > docs/features/map-load-diagnostics.md; \
grep -rc "ClientOnly" Main/Features/MapLoadDiagnostics | grep -v ':0' | wc -l
```

Expected: no `FAILED`, 16 code and test files plus the doc, last line `0`.

**C1.2 Wiring** (single-owner files):

1. `Main/IoC.cs`: after `using TAOM.Features.BattleLoadDiagnostics;` add `using TAOM.Features.MapLoadDiagnostics;`;
   after `        BattleLoadDiagnosticsIoC.RegisterBattleLoadDiagnosticsFeature(container);` add
   `        MapLoadDiagnosticsIoC.RegisterMapLoadDiagnosticsFeature(container);`.
2. `Main/SubModule.cs`, `OnSubModuleLoad`: directly after the closing brace of the `catch` that logs
   `[SaveLoad] init failed`, insert a blank line and the block from
   `MSYS_NO_PATHCONV=1 git show e45f6df2c:Main/SubModule.cs | sed -n '403,434p'` (the Patch89 comment, the `try`
   that initializes the heartbeat patch and `MapLoadTracer`, `TryPatchCategory("Patch89_MapLoadDiagnostics")`, the
   loop over the three `_Lifecycle`, `_MapScreen` and `_SceneReady` categories, and the `catch` logging
   `[MapLoad] init failed`). Check the pasted lines start with `        // Patch89_MapLoadDiagnostics:` and end with
   the `catch` block's closing brace.
3. `Main/SubModule.cs`, `RegisterCampaignLifeBehaviors`: directly after
   `        campaignStarter.AddBehavior(new StartupResourcesBehavior(goldService, influenceService, startupLogger));`
   insert `        campaignStarter.AddBehavior(new Features.MapLoadDiagnostics.MapLoadDiagnosticsBehavior(` and
   `            IoC.Resolve<Features.MapLoadDiagnostics.IMapLoadHeartbeatService>()));`.

**C1.3 Docs**:

1. `docs/features/map-load-diagnostics.md`: replace the first paragraph (from `**Status:**` to the line
   `evidence table is in \`docs/migration/v1.5.2-impact.md\`.`) with
   `**Status:** ported to the 1.4.x line on <DATE> (plans/118-campaign-load-time.md) from the 1.5.x line's Patch89`
   `(\`502f7cde9\`) together with plan 012's real-lower gate (\`8028bf3df\`, \`e6c03411d\`, \`15e5475a4\`, \`a4492fa89\`),`
   `so the per-frame lower trace never shipped here. Not yet run in game on v1.4.8.`
   Replace the two `**Related:**` lines (they end `(tag \`archive/bannerlord-1.5.0-port\`).`) with
   `**Related:** [load-time-stamps.md](load-time-stamps.md) for the [Lifecycle] dispatch lines inside the same window`
   `(once plan 118 Part A is merged).` Replace the "Why it exists" section's
   first paragraph with: on v1.4.8 the stretch from TAOM's `GameInitializationFinished` stamp to the map screen has no
   line in either log: 9.4 s of one player's load and about 17 s on another machine, with private memory rising about
   2 GB in it (player logs of 2026-10-28 and 2026-11-01); `Main/SubModule.cs` records a 2026-09-12 live run with 95 s
   and +2.7 GB between AllBehaviorDataLoaded and the map, and #509 saw the window stall for 10 minutes; the feature was
   written on the archived v1.5.0 line for the same blind spot (keep the rest of that section as the 1.5.x history).
   At the end of the line that ends `In v2.0.29 and v2.0.30,` (the next line starts `before this guard,`), replace
   `In v2.0.29 and v2.0.30,` with `On the 1.5.x line (its v2.0.29 and v2.0.30 builds),`. Rename `## What it found` to
   `## What it found on the 1.5.x line (v1.5.0)`; after its first
   code block add: `The vanilla gate is the same on v1.4.8 (\`SandBox.View\` \`MapScreen.HandleIfBlockerStatesDisabled\`).`
2. `docs/features/save-load-diagnostics.md`: insert immediately before the heading line that starts
   `## Offline inspector` a paragraph and a blank line:
   `From \`GameInitializationFinished\` to the map screen the \`[MapLoad]\` heartbeat and lifecycle trace take over:`
   `[map-load-diagnostics.md](map-load-diagnostics.md).` (This link keeps the new doc from being an orphan.)
3. Registry: insert `MSYS_NO_PATHCONV=1 git show e45f6df2c:docs/reference/harmony-patch-registry.md | sed -n '/^## Patch89_MapLoadDiagnostics/,/^## Patch90_/p' | sed '$d'`
   before the first `## PatchNN_` heading numbered above 89, or else before `## Patch_MissionTime_SetMovementOrder`.

**C1.4 WP117's "until WP118" sentences**: plan 117 writes them into its docs and leaves their removal to this WP. WP117 is
lane A of this wave with no dependency edge, so they are in this worktree only if WP117 merged before it was cut. Parts A and
B are committed by now, so the features the sentences wait for are here. Edit tool, for each file that exists; an old string
that is not there was not written, so skip it and let the check below decide.

1. `docs/features/mission-perf-heartbeat.md`: replace
   ``frame counter; on the 1.5.x line `[MapLoad]` reports fps on the campaign map (here it arrives with WP118).`` with
   ``frame counter; `[MapLoad]` reports fps on the campaign map only.`` Replace the two lines
   `- **Not on this line**: the culture doctrine feature, co-op's client-only patch attribute, and, until WP118,` and
   `` `[MapLoad]`, `[XmlMerge]` and the load-time stamps this page mentions.`` with
   `- **Not on this line**: the culture doctrine feature and co-op's client-only patch attribute.`
2. `docs/features/map-perf-profiler.md`, section `## On the 1.4.x line`: delete
   ``Patch89 and `[MapLoad]` (MapLoadDiagnostics) arrive with WP118: until then the Patch89 sentences below do not apply, `Campaign.RealTick` carries no other TAOM patch, and only Patch36 patches `MapScreen.OnFrameTick` besides this profiler. ``
   (three lines in that file; keep the sentence before it and `Other v1.5.x tags are the 1.5.x line's facts.`). Convert
   ``(`docs/features/map-load-diagnostics.md`, with WP118)`` to `([map-load-diagnostics.md](map-load-diagnostics.md))`.
3. `docs/reference/harmony-patch-registry.md`, section `## Patch101_MapFrameProfiler`: delete the sentence
   `On the 1.4.x line Patch89 arrives with WP118; until then no other TAOM patch is on Campaign.RealTick.` and the space before it.

**Verify C1.4**:

```bash
cd /c/Users/mikew/source/repos/taom-port/118 && \
ls docs/features/mission-perf-heartbeat.md docs/features/map-perf-profiler.md 2>/dev/null | wc -l; \
grep -n 'WP118' docs/features/mission-perf-heartbeat.md docs/features/map-perf-profiler.md docs/features/time-acceleration.md docs/reference/harmony-patch-registry.md 2>/dev/null | wc -l
```

Expected: second number `0` (a hit is another such sentence: delete it the same way). If the first number is below `2`, WP117
is not fully in this base, so put this line in the hand-off: `Integrator: once WP117 and WP118 are both merged, run C1.4's
check from plans/118-campaign-load-time.md on the merged tree; any hit gets C1.4's edits as one commit, docs(perf): v2.0.28 -
drop WP117's until WP118 notes.`

**Verify**: Build; Filtered `TAOM.Tests.Features.MapLoadDiagnostics|HarmonyPatchBindingTests|IoCRegistrationDisciplineTests|TAOM.Tests.Composition|PatchCategoryApplierTests`:
`Failed: 0`, MapLoadDiagnostics classes 24; Binding gate `Failed: 0`; full Test `Total` = previous total + 24, `Failed: 0`;
Docs command unchanged `dead_links` and `orphan_features`, `ai_dashes: 0`.

**Commit C1**: stage the 16 files, the doc, `Main/IoC.cs`, `Main/SubModule.cs`, `save-load-diagnostics.md`, the
registry and whichever of `mission-perf-heartbeat.md` and `map-perf-profiler.md` C1.4 changed. Message:

```text
feat(diagnostics): v2.0.28 - map-load timeline, renumbered Patch89

The last 9 to 17 s of a v1.4.8 campaign load, from game init to the
map screen, wrote nothing to either log, and a 2026-09-12 run spent
95 s and 2.7 GB there unexplained. The map-load timeline adds a
[MapLoad] heartbeat every 5 seconds from a Campaign.RealTick postfix
(frames, fps, tick time, a party census on emit frames only, campaign
time, loading-window state and the whole game-state stack) and a
numbered lifecycle trace: state pushes and pops, the map state and
screen seams, the first map frame, and each raise and real lower of
the global loading window with its caller chain. A lower is traced
only when the window actually drops: the engine lowers it on every
frame of the main menu and many screens, which on the 1.5.x line
wrote a line per frame before this gate. Four categories, each
applied on its own; the timeline resets on every session launch.

Research: taom-src v1.4.8 Campaign.RealTick, GameStateManager,
LoadingWindow, SceneView, MapState; SandBox.View MapScreen (dump)
Not-tested: in game (heartbeat and trace on a v1.4.8 load; no lower
 lines while the main menu stays open)
Save-compat: the behavior's SyncData is empty
Ported-from: 502f7cde946f767bf2e1ce6f100bef9179e036d8
Ported-from: 8028bf3df1b1651cd1178c2d2f990bdf5fd79edb
Ported-from: e6c03411dc0cca984d6aac7cfd01c64fccaa9d03
Ported-from: bce3c27f42f29ae7a047dbce1230561dc7a1ed76
Ported-from: 15e5475a45c9f601ddce87b66abd503cfa694bc6
Ported-from: a4492fa8900dbfe74c1b974d215b6cc0b1c8fa99
```

**Part C done criteria**: build exit 0; full suite `Failed: 0` (+24); `git diff --stat a4492fa89 HEAD -- Main/Features/MapLoadDiagnostics TAOM.Tests/Features/MapLoadDiagnostics`
prints nothing; `grep -c 'TryPatchCategory("Patch89_MapLoadDiagnostics")' Main/SubModule.cs` prints `1`;
`grep -c 'MapLoadDiagnosticsBehavior(' Main/SubModule.cs` prints `1`; `grep -c 'RegisterMapLoadDiagnosticsFeature' Main/IoC.cs`
prints `1`; C1.4's check prints `0` for its second number, and the hand-off carries the integrator line if its first was below `2`;
`git status --porcelain` empty.

---

## Part D: BUTR refresh, ButterLib's Distance Matrix off, TAOM.Dependencies startup lines

Steps D1 and D3 change what TAOM.Dependencies ships (Step D2 is TAOM-only): they reach players only in a release built from one tree
(#371; maintainer step).

### Step D1: BUTR refresh (`git cherry-pick -n 6027ecfae`, minus the 1.5.1 builds)

Simulation onto `1b00d1acd`: conflicts only in `CHANGELOG.md`.

```bash
cd /c/Users/mikew/source/repos/taom-port/118 && git cherry-pick -n 6027ecfae; git status --porcelain | head -40
```

Then: `git checkout --ours CHANGELOG.md && git add CHANGELOG.md` if it is listed as conflicted (if it shows as
modified instead: `git checkout HEAD CHANGELOG.md`), and
`git rm -q -f -- Dependencies/_Module/bin/Win64_Shipping_Client/Bannerlord.ButterLib.Implementation.1.5.1.dll Dependencies/_Module/bin/Win64_Shipping_Client/Bannerlord.MBOptionScreen.v1.5.1.dll`.
Why drop them: both BUTR loaders take an exact engine match and otherwise the LATEST build present (Current state),
so a 1.5.1 build would become the fallback on any engine without its own build; this line runs only v1.4.x.
Edit `Dependencies/_Module/THIRD-PARTY-LICENSES.txt`: replace
`Bannerlord.ButterLib.dll + Bannerlord.ButterLib.Implementation.1.4.*.dll + Bannerlord.ButterLib.Implementation.1.5.1.dll  --  ButterLib 2.12.0`
with `Bannerlord.ButterLib.dll + Bannerlord.ButterLib.Implementation.1.4.*.dll  --  ButterLib 2.12.0`, and the two
lines `Bannerlord.MBOptionScreen.v1.4.*.dll, Bannerlord.MBOptionScreen.v1.5.1.dll,` / `Bannerlord.ModuleLoader.Bannerlord.MBOptionScreen.dll,`
with the single line `Bannerlord.MBOptionScreen.v1.4.*.dll, Bannerlord.ModuleLoader.Bannerlord.MBOptionScreen.dll,`.
In `docs/migration/dr3-maintenance.md` replace the blockquote paragraph the pick added (it starts
`> **Refresh 2026-09-14 (the v1.5.2 engine bump).**`) with:

```text
> **Refresh <DATE> (1.4.x, plan 118, from the 1.5.x line's 6027ecfae).** ButterLib 2.11.0 to 2.12.0 (`Implementation.1.4.6`, `1.4.7` and `1.4.8` added beside the six 1.4.0 to 1.4.5 builds, all re-copied at 2.12.0), MBOptionScreen 5.12.1 to 5.12.3 (`v1.4.6` to `v1.4.8` added) with `MCM.UI.Adapter.MCMv5.dll` at 5.12.3, NuGet pins `Bannerlord.MCM` 5.12.3 and `Bannerlord.UIExtenderEx` 2.13.3 in both csproj files, the ButterLib stub at `v2.12.99.0`. The 1.5.x line's `1.5.1` builds are left out: both loaders load the build that matches the running engine and otherwise the latest build they find, so a build for an engine this line never runs would become that fallback. On v1.4.8 both now find an exact match; before this refresh none matched and each fell back to 1.4.5.
```

**Verify**: Restore exit 0 (needs nuget.org; a fetch failure for 5.12.3 or 2.13.3 is an environment STOP); Build;
Filtered `BundledDependencyManifestTests|DependenciesPairingTests`: `Failed: 0`;
`ls Dependencies/_Module/bin/Win64_Shipping_Client | grep -cE 'Implementation\.1\.4\.[678]\.dll|MBOptionScreen\.v1\.4\.[678]\.dll'`
prints `6`; `ls Dependencies/_Module/bin/Win64_Shipping_Client | grep -c '1\.5\.1'` prints `0`; full Test `Failed: 0`.

**Commit D1**: stage `Dependencies/TAOM.Dependencies.csproj Main/TAOM.csproj Dependencies/_Module/THIRD-PARTY-LICENSES.txt Stubs/Bannerlord.ButterLib/_Module/SubModule.xml docs/migration/dr3-maintenance.md`
plus the DLL paths `git status --porcelain` lists (the pick staged them). Message:

```text
fix(deps): v2.0.28 - BUTR stack refreshed with 1.4.6 to 1.4.8 builds

TAOM.Dependencies shipped ButterLib 2.11.0 and MBOptionScreen 5.12.1
with per-engine builds only up to game 1.4.5. Both loaders take the
build that matches the running engine and otherwise the latest they
find, so v1.4.8 has run the 1.4.5 builds. This refresh brings
ButterLib 2.12.0 (implementations 1.4.6, 1.4.7 and 1.4.8 added, the
six older ones re-copied), MBOptionScreen and MCM.UI.Adapter 5.12.3,
NuGet Bannerlord.MCM 5.12.3 and Bannerlord.UIExtenderEx 2.13.3 in both
csproj files, the ButterLib stub at v2.12.99.0, the licence notice and
the maintenance record. The 1.5.x line's 1.5.1 builds are left out:
they would only ever be a wrong fallback here.

Research: decompiled ButterLib 2.11.0 and MBOptionScreen 5.12.1
implementation loaders; BundledDependencyManifestTests
Not-tested: in game (MCM opens, the session snapshot lists
 Implementation.1.4.8 and MBOptionScreen.v1.4.8, a dev-trigger crash
 writes a bundle)
Ported-from: 6027ecfae1f2be5bc57a1c8eeb33730f825cd81b
```

### Step D2: Switch off ButterLib's Distance Matrix (`1325c36ea`, module transplant)

Simulation: conflicts in `FeatureModules.cs`, `feature-map.md`, `provenance-register.md` and a lesson. Do Step D1 first:
the binding test loads the newest implementation DLL in `Dependencies/_Module/bin/Win64_Shipping_Client`, which
must be the 1.4.8 build v1.4.8 actually loads.

1. `for f in $(git show 1325c36ea --name-only --diff-filter=A --format= -- Main TAOM.Tests docs/features/butter-lib-distance-matrix.md); do mkdir -p "$(dirname "$f")" && git show "1325c36ea:$f" > "$f" || echo "FAILED $f"; done`
   (9 files, no `FAILED`).
2. `FeatureModules.cs`: append `        new Features.ButterLibDistanceMatrix.ButterLibDistanceMatrixModule(),`.
3. `docs/modding/module-dependencies.md`: `grep -cF -e '--filter DependenciesPairing' docs/modding/module-dependencies.md`
   prints `1` (the `Check:` line of the pairing section, `:772` at `1b00d1acd`; WP103 rewrote it with the flags, so
   anchor on that substring, not on the whole line). Replace that whole line with
   `` Check: `dotnet test TAOM.Tests -p:DisableModuleCopy=true -p:ModuleId= --filter DependenciesPairing`, and for a ButterLib update also `` and
   `` `dotnet test TAOM.Tests -p:DisableModuleCopy=true -p:ModuleId= --filter FullyQualifiedName~ButterLibDistanceMatrixBindingTests` (TAOM switches a ButterLib subsystem off by name, #740; [butter-lib-distance-matrix.md](../features/butter-lib-distance-matrix.md)) ``.
4. `docs/reference/provenance-register.md`: replace the `| BUTR stack |` row with the `1325c36ea` version
   (`git show 1325c36ea:docs/reference/provenance-register.md | grep '^| BUTR stack'`). Leave `1325c36ea`'s Yotthani
   hunks out (the row and the `Fourth pass` paragraph, absent at `1b00d1acd`): WP144's provenance step carries that note.
5. `reflection-sites.md`: the 1.4.x file lacks the `Native2ManagedTargets.cs` paragraph the 1.5.x one follows, so
   insert the paragraph `1325c36ea` adds (`git show 1325c36ea:docs/reference/taleworlds-api-snapshot/reflection-sites.md | grep '^A third-party site gated'`)
   at the end of the Category A section, directly before the `---` line above `## Category B`, with a blank line
   before it, and change its opening words `A third-party site gated the same way:` to
   `A third-party reflection site with its own gate:`.
6. `docs/features/butter-lib-distance-matrix.md`: the sentence spans two lines (line 12 ends
   `On Bannerlord v1.5.4 it loads`, line 13 starts with two spaces and
   `` `Bannerlord.ButterLib.Implementation.1.5.1.dll`, the newest it has. ``); replace those words (one Edit with a
   two-line old string) with
   `` On Bannerlord v1.4.8 it loads `Bannerlord.ButterLib.Implementation.1.4.8.dll` (before plan 118's refresh, `1.4.5`). ``;
   after the title add `> **On the 1.4.x line** since <DATE> (plan 118): same code; the 170 ms figure and the`
   `> 355,000-pair count are yotthani's v1.5.3 measurements on TAOM's map; the v1.4.8 stall is not re-measured.`;
   "Changelog" add a 1.4.x line; "GitHub Issue" add `1.4.x: port of #740.`

**Verify**: Build; Filtered `TAOM.Tests.Features.ButterLibDistanceMatrix|TAOM.Tests.Composition`: `Failed: 0`,
ButterLibDistanceMatrix classes 18; `SubSystem_HasTheMembersTheAdapterCalls` passes against the 2.12.0 1.4.8 build;
full Test `Failed: 0`, total +18; Docs unchanged counts; `python -m unittest discover -s tools/tests -t . -p "test_dotnet_commands_flagged.py"`
ends `OK` (WP103's gate scans `docs/modding`).

**Commit D2**: stage the 9 files and the four edited files. Message:

```text
perf(campaign): v2.0.28 - switch off ButterLib's Distance Matrix

ButterLib, which TAOM ships, keeps a Distance Matrix of every
settlement, clan and kingdom. Its GeopoliticsBehavior builds the full
settlement table at every load and new game (about 355,000 pairs on
TAOM's map) and rebuilds the clan and kingdom tables whenever a
settlement passes to another clan: about 170 ms, up to 281 ms in one
frame, per siege end (yotthani, v1.5.3). Nothing installed reads them.

TAOM now calls ButterLib's own Disable() on that subsystem at the
first main menu, after the TAOM.Dependencies submodules (ButterLib,
MCM) have run their own main-menu hooks, so ButterLib never adds the
behavior. One [ButterLibDistance] line says whether it was switched
off, was already off, or could not be.

Research: decompiled ButterLib 2.11.0 Implementation.1.4.5
DistanceMatrixSubSystem and its game-start gate; binding test against
the vendored 2.12.0 Implementation.1.4.8
Not-tested: in game (the log line; no stall at a siege end)
Save-compat: GeopoliticsBehavior's SyncData is empty
Ported-from: 1325c36ea8d14bcba9dc9d0a3f6f5087c63833a9
```

### Step D3: Flush TAOM.Dependencies startup lines (`git cherry-pick -n d31026b5b`)

Simulation: conflicts only in `docs/migration/dr3-maintenance.md`.

```bash
cd /c/Users/mikew/source/repos/taom-port/118 && git cherry-pick -n d31026b5b; git status --porcelain
```

Resolve `docs/migration/dr3-maintenance.md`: `git checkout --ours docs/migration/dr3-maintenance.md`, then apply
by hand the three changes of `git show d31026b5b -- docs/migration/dr3-maintenance.md`: the `diag.log` row text, the
new `taom_debug_*.log` row after it (write `Since <DATE> on the 1.4.x line (#725)` for `Since 2026-10-03 (#725)`),
and the sample line `[INFO  ] [Dependencies]               OnSubModuleLoad: complete` (1.4.x `Dependencies/SubModule.cs:263`
writes that DiagLog line). If `Main/SubModule.cs` is conflicted too: `git checkout --ours Main/SubModule.cs` and
insert after `        IoC.Configure();` a blank line and the three lines the commit adds (the `#725` comment, the
`try { Core.Logging.EarlyLogBridge.Connect(IoC.Resolve<IModLogger>()); }` line and its `catch` line).
`git add` both files.

**Verify**: `grep -c 'EarlyLogBridge.Connect' Main/SubModule.cs` prints `1`; Build; Filtered `EarlyLogBridgeTests`:
`Failed: 0`, 8 tests; full Test `Failed: 0`, total +8.

**Commit D3**: stage `Dependencies/EarlyLog.cs Main/Core/Logging/EarlyLogBridge.cs Main/SubModule.cs TAOM.Tests/Core/Logging/EarlyLogBridgeTests.cs docs/migration/dr3-maintenance.md`. Message:

```text
fix(logging): v2.0.28 - flush TAOM.Dependencies startup lines

TAOM.Dependencies loads before TAOM and holds its startup lines in
EarlyLog until TAOM hands it a logger. Nothing ever did, so its static
init, the Harmony fork version, every AssemblyResolve redirect, the
UnpatchAll guard, UIExtenderEx's start-up, a second 0Harmony.dll and
any shim install failure were dropped on every launch: the Harad
player's 2026-11-01 taom_debug log has no [TAOM.Dependencies] line.

TAOM now connects EarlyLog in OnSubModuleLoad right after the
container is configured. Buffered lines come out in order at their own
level, each starting with "[buffered HH:mm:ss]"; later lines go
straight to the log. A line logged on another thread during the flush
queues behind the older ones, and a failing logger costs only its line.

Not-tested: in game (the [TAOM.Dependencies] lines in taom_debug.log)
Ported-from: d31026b5becadfa49e4baf57dd867611ce41cbf5
```

**Part D done criteria**: build exit 0; full suite `Failed: 0` (+26 over Part D's start); Step D1's `ls` checks hold;
`grep -c 'ButterLibDistanceMatrixModule()' Main/Composition/FeatureModules.cs` prints `1`;
`git diff --stat 1325c36ea HEAD -- Main/Features/ButterLibDistanceMatrix Main/Adapters/ButterLibDistanceMatrixAdapter.cs Main/Adapters/IButterLibDistanceMatrixAdapter.cs TAOM.Tests/Features/ButterLibDistanceMatrix`
prints nothing; `git diff --stat d31026b5b HEAD -- Dependencies/EarlyLog.cs Main/Core/Logging/EarlyLogBridge.cs TAOM.Tests/Core/Logging/EarlyLogBridgeTests.cs`
prints nothing; `git status --porcelain` empty.

---

## Part E: the append-only lord generator and the lords.xslt dedent

Precondition (D20): `grep -c 'decision D20' docs/reference/development-machines.md` prints at least `1` (WP101's laptop
data baseline). Otherwise STOP before Step E2. Also confirm `ls "$BANNERLORD_GAME_DIR/Modules/SandBox/ModuleData/lords.xml" "$BANNERLORD_GAME_DIR/Modules/TAOM_Map/ModuleData/spnpccharacters.xml"`
lists both (they are the proof inputs).

### Step E1: Lord generator only adds (`9165a6727`, end-state transplant)

Simulation: conflicts in the tool, its test (absent on 1.4.x: modify/delete), `tools/README.md`, two 1.5.x-only
feature docs, `file-catalogue.md` and two lessons. Transplant instead:

```bash
cd /c/Users/mikew/source/repos/taom-port/118 && \
git show 9165a6727:tools/complete_lords_xslt.py > tools/complete_lords_xslt.py && \
git show 9165a6727:tools/tests/test_complete_lords_xslt.py > tools/tests/test_complete_lords_xslt.py && \
git show 9165a6727 -- docs/ai-includes/lord-skills-authoring.md docs/features/lord-identity-reconciliation.md docs/migration/templates/characters.md | git apply && \
git show 9165a6727 -- docs/modding/lords-and-heroes.md | git apply -C1 && echo docs-applied && \
python -c "import re,pathlib; p=pathlib.Path('tools/tests/test_complete_lords_xslt.py'); r=re.subn(rb'    def test_the_skill_gate_finds_no_drift_in_a_generated_templated_lord.*?(?=    def test_a_new_lords_template_carries)', b'', p.read_bytes(), flags=re.S); p.write_bytes(r[0]); print('removed', r[1])" && \
grep -c sync_lord_inline_skills tools/tests/test_complete_lords_xslt.py; \
wc -l tools/complete_lords_xslt.py && python tools/complete_lords_xslt.py --dry-run | tail -3
```

Why the two adaptations: `-C1` because the hunk's trailing context in `lords-and-heroes.md` is a 1.5.x paragraph (1.4.x
has `**The two localization tiers do not interoperate.**` there; the replaced bullet itself is identical). The removed
test imports `tools/sync_lord_inline_skills.py`, which is #626's tool and was not ported: on v1.4.8 an inline `<skills>`
block beside a `skill_template` is still discarded (`BasicCharacterObject.cs:337-357`), so the tool does not exist here.

Expected: `docs-applied`; `removed 1`; then `0`; `428 tools/complete_lords_xslt.py`; the dry run ends with
`--apply would add 0 template(s); bytes already in the file are never rewritten.` Any other count: STOP (never run
`--apply`; adding lords is content). Then regenerate the inventory from 1.4.x data:
`python tools/complete_lords_xslt.py --export-csv` and check `head -1 tools/lords_inventory.csv` contains `,race,`
and `wc -l tools/lords_inventory.csv` is close to 397 (one row per lord plus the header; a count off by more than a
handful: STOP).

**Verify**: `python -m unittest discover -s tools/tests -t . -p "test_complete_lords_xslt.py" -v`: 32 tests, 0
failures (the `ShippedFileTests` run, because the laptop has SandBox's `lords.xml`). A failing
`test_the_shipped_file_needs_no_lord`: STOP and report the dry-run output. Then the Tool tests: no new failure vs
Step 0.4.

**Commit E1**: stage `tools/complete_lords_xslt.py tools/tests/test_complete_lords_xslt.py tools/lords_inventory.csv`
and the four docs. Message:

```text
fix(tools): v2.0.28 - lord generator only adds, never rewrites

complete_lords_xslt.py --apply rebuilt lords.xslt from scratch and
lost everything it did not generate itself: lord equipment, the race
attributes, hand layout, explicit genders and the byte-order mark.
Nothing ran it automatically, so no shipped data was hurt.

The tool now never rewrites a byte already in lords.xslt. It only
inserts a template for each vanilla lord the file lacks, in that
lord's faction section, and keeps the BOM and line endings; on the
1.4.x file it adds nothing. --dry-run says how many lords --apply
would add and lists vanilla attributes no template carries; an empty
vanilla lords file stops it. lords_inventory.csv is regenerated from
1.4.x data and gains a race column. The 1.5.x test that needs
sync_lord_inline_skills.py stays behind: that tool is not on this line.

Not-tested: an --apply run that adds a lord (the tests cover it on
 copies)
Ported-from: 9165a672781e9996bb109cc1fc3ce99e30f4f32c
```

### Step E2 (RED then GREEN): lords.xslt sheds its runaway indentation (`5e45d2782`, re-derived)

Not a cherry-pick: the files differ between the lines. The rule, which reproduces `5e45d2782` byte for byte on its
1.5.x parent (82 blocks) and was run in memory on the 1.4.x blob this session: for each line whose text is
`<skills>`, take the nearest earlier non-blank line; if it is `</face>` and `<skills>` is indented deeper, set
`<skills>` and its `</skills>` to the `</face>` indentation and every non-blank line between to that plus 4.

1. RED: create `tools/tests/test_lords_xslt_indentation.py`:

```python
"""lords.xslt must not regrow the runaway skill-block indentation removed for #950 (5e45d2782 on the
1.5.x line, re-derived for 1.4.x by plans/118-campaign-load-time.md): no <skills> block sits deeper than the
</face> line before it, and no line carries more than 200 leading spaces."""
import unittest
from pathlib import Path

LORDS_XSLT = Path(__file__).resolve().parents[2] / "Main" / "_Module" / "ModuleData" / "lords.xslt"
MAX_LEADING_SPACES = 200


def leading_spaces(line):
    return len(line) - len(line.lstrip(" "))


class LordsXsltIndentationTests(unittest.TestCase):
    @classmethod
    def setUpClass(cls):
        cls.lines = LORDS_XSLT.read_text(encoding="utf-8-sig").replace("\r\n", "\n").split("\n")

    def test_no_skills_block_sits_deeper_than_its_face_line(self):
        deeper = []
        for i, line in enumerate(self.lines):
            if line.strip() != "<skills>":
                continue
            j = i - 1
            while j >= 0 and not self.lines[j].strip():
                j -= 1
            if j >= 0 and self.lines[j].strip() == "</face>" and leading_spaces(line) > leading_spaces(self.lines[j]):
                deeper.append(i + 1)
        self.assertEqual([], deeper[:10], f"{len(deeper)} <skills> blocks sit deeper than their </face> line")

    def test_no_line_has_runaway_indentation(self):
        runaway = [i + 1 for i, line in enumerate(self.lines) if leading_spaces(line) > MAX_LEADING_SPACES]
        self.assertEqual([], runaway[:10], f"{len(runaway)} lines carry more than {MAX_LEADING_SPACES} leading spaces")


if __name__ == "__main__":
    unittest.main()
```

   **Verify (RED)**: `python -m unittest discover -s tools/tests -t . -p "test_lords_xslt_indentation.py"`: 2
   failures, messages `82 <skills> blocks sit deeper ...` and `320 lines carry more than 200 ...`.
2. GREEN: write `C:\Users\mikew\source\repos\taom-port\118-scratch\dedent_lords_xslt.py`:

```python
import sys
from pathlib import Path

path = Path(sys.argv[1])
lines = path.read_bytes().decode("utf-8").split("\n")   # keeps the BOM; a CRLF file keeps "\r" at line ends
out = list(lines)
ind = lambda s: len(s) - len(s.lstrip(" "))
blocks = 0
i = 0
while i < len(lines):
    if lines[i].strip() == "<skills>":
        j = i - 1
        while j >= 0 and not lines[j].strip():
            j -= 1
        if j >= 0 and lines[j].strip() == "</face>" and ind(lines[i]) > ind(lines[j]):
            base = ind(lines[j])
            blocks += 1
            out[i] = " " * base + lines[i].lstrip(" ")
            k = i + 1
            while lines[k].strip() != "</skills>":
                if lines[k].strip():
                    out[k] = " " * (base + 4) + lines[k].lstrip(" ")
                k += 1
            out[k] = " " * base + lines[k].lstrip(" ")
            i = k
    i += 1
path.write_bytes("\n".join(out).encode("utf-8"))
print("blocks re-indented:", blocks)
```

   Run `python C:/Users/mikew/source/repos/taom-port/118-scratch/dedent_lords_xslt.py Main/_Module/ModuleData/lords.xslt`.
   **Verify**: prints `blocks re-indented: 82`; `git hash-object Main/_Module/ModuleData/lords.xslt` prints
   `f2f180d05ddc816635087b27e070210502d9cb92`; `git diff --minimal --shortstat -- Main/_Module/ModuleData/lords.xslt` shows
   `1640 insertions(+), 1640 deletions(-)` (plain `git diff --stat` says 1655: its default aligns lines less tightly); `git diff -w --stat -- Main/_Module/ModuleData/lords.xslt` prints nothing;
   the indentation tests pass.
3. Proof that the transform output is unchanged. `git show HEAD:Main/_Module/ModuleData/lords.xslt > ../118-scratch/lords_before.xslt`.
   Write `118-scratch/xslt_lxml.py` (prints the SHA-256 of `bytes(etree.XSLT(etree.parse(argv[1]))(etree.parse(argv[2])))`)
   and `118-scratch/xslt_net.ps1` (`param([string]$Xslt,[string]$InputXml,[string]$OutFile)`; a new
   `System.Xml.Xsl.XslCompiledTransform`, `.Load($Xslt)`, `.Transform($InputXml, $OutFile)`, then
   `(Get-FileHash -Algorithm SHA256 $OutFile).Hash`). For each input (SandBox `lords.xml`, TAOM_Map
   `spnpccharacters.xml` under the game install) run both scripts against `lords_before.xslt` and against the
   worktree `lords.xslt` (lxml: `python ...`; .NET Framework: `powershell.exe -NoProfile -ExecutionPolicy Bypass -File ...`,
   outputs into `118-scratch/`). **Verify**: for each input, before and after hashes are equal under lxml and equal
   under .NET (four equal pairs). Any unequal pair: STOP and `git checkout HEAD Main/_Module/ModuleData/lords.xslt`.
4. Data: run the Data command into `118-scratch/validate-after.txt`;
   `diff <(grep -E 'ERROR|WARN' ../118-scratch/validate-before.txt | sort) <(grep -E 'ERROR|WARN' ../118-scratch/validate-after.txt | sort)`
   prints nothing. Tool tests: no new failure vs Step 0.4.

**Commit E2**: `git add Main/_Module/ModuleData/lords.xslt tools/tests/test_lords_xslt_indentation.py`. Message:

```text
perf(lords): v2.0.28 - lords.xslt sheds 360 KB of runaway indentation

82 skill blocks in lords.xslt had grown thousands of spaces of
indentation (up to 4,960 on one line; 320 lines over 200), about a
quarter of the file. Each block now sits at its </face> line's level,
taking the file from 1,493,730 to 1,125,090 bytes. The change is
whitespace only: the stylesheet gives byte-identical output under lxml
and under the .NET Framework transform the game uses, checked on
SandBox's lords.xml and TAOM_Map's spnpccharacters.xml. A new tools
test fails if any block regrows. The game parses the stylesheet once
per load (once per process with the XML merge fast path), so the load
gain is small; the lord tools that parse the file get faster.

Ported-from: 5e45d2782c6acc0a591bd4da9454977071c0345c
```

**Part E done criteria**: tool tests no new failure, both new test files pass; `git hash-object Main/_Module/ModuleData/lords.xslt`
prints `f2f180d05ddc816635087b27e070210502d9cb92`; the four XSLT hash pairs equal (in the hand-off); validator
before/after diff empty; `git status --porcelain` empty.

---

### Final step (after the last part you land): review gate and hand-off

1. Whole-WP checks: `git diff --name-only $(git merge-base HEAD bannerlord-1.4.5)..HEAD | grep -c 'CHANGELOG.md\|^\.claude/\|feature-map.md\|tools/README.md\|docs/reviews/'`
   prints `0`; `git log --format=%s $(git merge-base HEAD bannerlord-1.4.5)..HEAD | awk 'length($0) > 72 || $0 !~ / v2\.0\.28 - /'`
   prints nothing; every changed path is in Scope.
2. Run `/deep-review` on `git diff $(git merge-base HEAD bannerlord-1.4.5)..HEAD` (C# in many files and both
   single-owner files) and point it at "Maintenance notes". Act only on findings that change runtime behaviour; each
   fix lands as one extra commit `fix(load-time): v2.0.28 - review follow-ups for plan 118` (tests first), then
   re-run the done criteria. If your session cannot invoke skills, say so; the integrator runs it. Never run
   `/review-codex` or `/codex-verify` (paid).
3. Hand-off: branch; each commit's short SHA and subject; T0 and every part's final counts; the harness table and
   the installed TAOM version; the XSLT hash pairs; C1.4's integrator line if it applies; any skipped step and why; the
   review outcome; the in-game checks owed. No old-plan row in `plans/README.md` closes with this WP (plans 012, 040
   and 042 have rows only on the 1.5.x ref); the integrator updates this plan's own row from the hand-off.

## Disposition (every commit in the pack, plus the shared hunks)

Simulation re-run this session: `git merge-tree --write-tree --name-only --no-messages --merge-base=<sha>^ origin/bannerlord-1.4.5 <sha>`.

| 1.5.x commit | Subject | Simulation onto `1b00d1acd` | Disposition |
|---|---|---|---|
| `346f3e093` | feat(load-stamps): v2.0.32 - time module XML, patch groups and handlers | 1: `PatchShieldPolicy.cs`, `FeatureModules.cs`, two settings files, `CoopSettingsRelevance.cs`, `PatchCategoryApplier.cs`, `SubModule.cs`, four test files, docs, bookkeeping | **Transplanted** (A2) with the pairing guard, the 1.4.x hook steps and v1.4.8 cites; review records, RCA, REVIEW-LOG, lessons, plan 040 text dropped |
| `a27c6b338` | perf(xml-merge): v2.0.32 - merge module XML in one document per load | 1: `FeatureModules.cs`, `CoopVetoClassificationTests.cs`, `BindingGateRunSettingsTests.cs`, `ReflectionSiteBindingTests.cs`, `binding-gate.runsettings`, docs, bookkeeping | **Transplanted** (B1) from `e45f6df2c` with the guard, the players' module order, v1.4.8 cites, a v1.4.8 harness run; runsettings and its test left to WP122, which takes that hunk; engine-bump `SKILL.md` hunk left to WP143 |
| `43ca92ee9` | fix(perf): v2.0.32 - integration fixups and review for the perf plans | (owned by WP117) | **Shared hunk only**: its two-line Patch99 comment arrives with B1's `e45f6df2c` file; WP117 ports the rest |
| `ab78564e0` | fix(campaign): v2.0.36 - port PR #1064 supply and desertion fixes | 1 (owned by WP112) | **Shared hunk only**: its two IL-fingerprint tests (B2; the arrows test rewritten for the 1.4.x fallback); WP112 ports the rest |
| `502f7cde9` | feat(diagnostics): v2.0.28 - map-load timeline, renumbered Patch89 | 1: `CHANGELOG.md`, `SubModule.cs`, `patch-targets.md` | **Transplanted** in C1 (squashed with plan 012), hand-wired as on the 1.5.x tip |
| `8028bf3df` | perf(map-load): v2.0.30 - add the loading-window real-lower gate | 0 (clean) | **Transplanted** in C1 |
| `e6c03411d` | perf(map-load): v2.0.30 - log a loading-window lower only when it drops | 1: the patch file (absent here) | **Transplanted** in C1 |
| `bce3c27f4` | docs(map-load): v2.0.30 - loading-window lowers trace on real drops | 1: two docs | **Transplanted** in C1 (doc and registry text, restated for 1.4.x) |
| `15e5475a4` | fix(map-load): v2.0.30 - review follow-ups for plan 012 | 1: four code and test files, docs, bookkeeping | **Transplanted** in C1; review record, RCA and lessons dropped |
| `a4492fa89` | fix(map-load): v2.0.30 - convergence fixes for plan 012 | 1: code, test, docs, bookkeeping | **Transplanted** (source of C1's end state); bookkeeping dropped |
| `6f7ddd394` | docs(changelog): v2.0.30 - plan 012 loading-window trace entry | 1: `CHANGELOG.md` | **Skipped**: CHANGELOG only (decision D1) |
| `6027ecfae` | fix(deps): v2.0.28 - BUTR stack refreshed for the 1.5.x line | 1: `CHANGELOG.md` | **Ported** (Step D1, cherry-pick) minus the two 1.5.1 builds (wrong fallback on this line), licence and dr3 text adapted |
| `1325c36ea` | perf(campaign): v2.0.34 - switch off ButterLib's Distance Matrix | 1: `FeatureModules.cs`, `feature-map.md`, `provenance-register.md`, a lesson | **Transplanted** (Step D2) as a module (decision D2); runsettings mention adapted; Yotthani register note not here (WP144's provenance step) |
| `d31026b5b` | fix(logging): v2.0.32 - flush TAOM.Dependencies startup lines | 1: `dr3-maintenance.md` | **Ported** (Step D3, cherry-pick); dr3 hunks re-applied by hand |
| `9165a6727` | fix(tools): v2.0.35 - lord generator only adds, never rewrites | 1: tool, test, `tools/README.md`, two 1.5.x feature docs, `file-catalogue.md`, lessons | **Transplanted** (E1): tool, test (minus the one that needs #626's `sync_lord_inline_skills.py`), four generic docs; CSV regenerated from 1.4.x data; `tools/README.md` and lessons left to WP142, 1.5.x-only docs and RCA dropped |
| `5e45d2782` | perf(lords): v2.0.35 - lords.xslt sheds 360 KB of runaway indentation | 1: `lords.xslt` | **Re-implemented** (E2) by the same rule, proven byte-identical in output, plus a regrowth test |

Not taken, with reason: `30abf550b` (method-exclusion mechanism: WP107); `45bcf80bd` and `9ed3ee308` (plans 009 and
018: WPs 104 and 105); `70517552c` (1.5.x map evidence); `6d7e67f3f` and `[ClientOnly]` (D7); `b8c000452`,
`e9b28e2ad` (CI test tags, D9); the 1.5.x-only `PatchShieldPolicy` entries (Patch92, Patch93, map profiler, Marshal's
Word).

Findings: LOAD-TIME-01 (B1), LOAD-TIME-05 (A2), LOAD-TIME-07 **dropped** (D14: after plan 042 it saves well under
0.5 s per load), LOAD-TIME-08 (Step D3; boot is mostly native, nothing else to port), LOAD-TIME-09 (C1, with A2's
`[Lifecycle]` lines), LOAD-TIME-10 (E2, and B1's XSLT cache, the real lever), RELEASE-INTEGRITY-10 (Step D1),
RUNTIME-PERF-11 part A (Step D2), SAVE-TIME-09 **no change**: no 1.5.x fix exists, and its own recommendation is to
measure before touching and never switch ButterLib's Save System subsystem off blind (saves written with it on may
not load with it off); WP114 owns save-time measurement. Step D2 here switches off only the Distance Matrix, whose
behavior's `SyncData` is empty.

## Test plan

- New C#: `ShieldExclusionCheckTests` (6), the module guard tests in `LoadTimeStampsModuleTests` and
  `XmlMergeWiringTests` (1 each), the 1.4.x arrows IL test (B2). Transplanted: LoadTimeStamps 138, XmlMerge 119 plus
  `IlFingerprintTests` 8 (2 harness tests skip without `TAOM_RUN_BENCHMARKS=1`), MapLoadDiagnostics 24,
  ButterLibDistanceMatrix 18, `EarlyLogBridgeTests` 8, `PatchCategoryApplierTests` +10, settings provider +1,
  `ReflectionSiteBindingTests` +11 rows, `SupplyNativeQueryTests` +2. Planner's counts from the 1.5.x files; a filtered
  run that lists the same classes with `Failed: 0` but a slightly different count is reported, not a STOP.
- New Python: `test_lords_xslt_indentation.py` (2); transplanted `test_complete_lords_xslt.py` (32 of 1.5.x's 33).
- Gates that must stay green: `SettingsFingerprintTests` (census and the two docs),
  `SettingRequireRestartPostureTests`, `CoopVetoClassificationTests`, `HarmonyPatchBindingTests`, the composition
  kernel tests, `PatchCategoryApplierTests`' source gate, `BundledDependencyManifestTests`.
- Live gate: the merge harness on the installed v1.4.8 module set (B1.6), equivalence plus the 50 percent bar.
- Structurally untestable (commit trailers say so): every in-game behaviour below.

## Done criteria (whole WP; each part also has its own)

- [ ] `dotnet build TAOM.sln -p:DisableModuleCopy=true -p:ModuleId=` exits 0 with 0 errors.
- [ ] `dotnet test TAOM.Tests --no-build -p:DisableModuleCopy=true -p:ModuleId=` reports `Failed: 0`, `Total` = T0 +
      347 for all five parts (A 165, B 132, C 24, D 26), `Skipped` = T0's + 2.
- [ ] The live harness output shows `Passed: 2` and is quoted in `docs/features/xml-merge-fast-path.md`.
- [ ] `grep -cE 'LoadTimeStampsModule\(\)|XmlMergeModule\(\)|ButterLibDistanceMatrixModule\(\)' Main/Composition/FeatureModules.cs` prints `3`.
- [ ] `python -m unittest discover -s tools/tests -t .` shows no failure that Step 0.4 did not show.
- [ ] `git hash-object Main/_Module/ModuleData/lords.xslt` prints `f2f180d05ddc816635087b27e070210502d9cb92`.
- [ ] The Docs command: `dead_links` and `orphan_features` equal Step 0.4's; `ai_dashes: 0`.
- [ ] Final-step checks 1 hold (no bookkeeping paths, every subject carries `v2.0.28` within 72 characters).
- [ ] `git status --porcelain` prints nothing.
- [ ] Any release containing Part A (and so Part B) or Part D ships TAOM and TAOM.Dependencies from one build (maintainer).

## STOP conditions

Stop and report (do not improvise) if:

- Any WP104, WP105 or WP107 prerequisite in Step 0.3 is missing, or a Step 0.3 anchor is missing, duplicated or out
  of order; or `Main/Features/LoadTimeStamps`, `XmlMerge`, `MapLoadDiagnostics` or `ButterLibDistanceMatrix` already exists before the part that creates it.
- Step A1's issue number is still `#TBD`.
- `git apply` of the `PatchCategoryApplier` hunks or of a doc hunk fails (as written, including E1's `-C1`).
- `MirroredEngineBodies_CallExactlyThePinnedSequence`, `Adapter_OnInstalledEngine_HasNoBindingProblem`,
  `LoadTimeStampsBindingTests` or `ButterLibDistanceMatrixBindingTests` fails (an engine or library shape differs:
  never re-pin or loosen).
- The live harness fails in any way (B1.6): do not commit B1.
- A ShieldExclusionCheck pin fails after the seven entries are in place.
- Restore cannot fetch `Bannerlord.MCM` 5.12.3 or `Bannerlord.UIExtenderEx` 2.13.3 (environment failure: report it).
- `--dry-run` would add a lord, or `test_the_shipped_file_needs_no_lord` fails; never run `--apply`.
- `HEAD:Main/_Module/ModuleData/lords.xslt` is not `f8c1e3f8597fbdac34827b536938861a33ec8590` before E2, the dedent
  prints a block count other than 82, the result hash differs, an XSLT output pair differs, or the validator shows a
  new ERROR.
- The D20 record is absent before E2, or the ModuleData validation hook denies E2's commit.
- Any module would need `OwnsSaveData => true`, `GameModels` or `RegisterCampaignObjects`.
- The work needs a file outside Scope, or any `IoC.cs`/`SubModule.cs`/`TAOM.csproj` edit this plan does not list.
- A hook denies a commit, or still demands `CHANGELOG.md` (never stage it).
- A RED step does not fail as described, or a step's verification fails twice after a reasonable fix attempt.

## In-game checks owed (maintainer)

Executors cannot run the game. On the merged branch, after a build that deploys both modules (`./build.ps1`, game
closed), with a save from before this WP. Log: `taom_debug_<timestamp>.log` under the game's
`bin\Win64_Shipping_Client\Logs`; engine log `rgl_log_<pid>.txt`.

1. Boot to the main menu. taom_debug: `[LoadStamps] ready`, `[LoadXml] ready`, `[Lifecycle] ready: listener binding ok`,
   `[XmlMerge] fast path ready`, `[ButterLibDistance] switched off ButterLib's Distance Matrix`, `[buffered HH:mm:ss]
   [TAOM.Dependencies]` lines near the top, and `[PatchApply] phase=OnSubModuleLoad scope=total` and `phase=MainMenu`.
   No `stamps off` or `fast path off` warning (those mean a mismatched TAOM.Dependencies.dll). The session snapshot
   lists `Bannerlord.ButterLib.Implementation.1.4.8 v2.12.0.0` and `Bannerlord.MBOptionScreen.v1.4.8 v5.12.3.0`.
2. Load the old save. rgl: the window from `Initializing saved game end` to `Main_map/atmosphere.xml` against the
   pre-port 22.6 to 29.7 s, and the gap between TAOM character file `opening` lines (0.24 to 0.28 s before).
   taom_debug: `[XmlMerge] type=NPCCharacters ... path=fast`, `[XmlMerge] summary game=Campaign ... fast_path=on`,
   `[LoadXml]` lines with `merge_ms`, the `[LoadXml] summary`, `[Lifecycle] dispatch=OnGameLoaded`, `[MapLoad]`
   heartbeats and a `LOADING-WINDOW lowered` trace before the map. Any `stands aside`, `fast path failed` or
   `disabled for this session` line is a finding; so is any NPC, item or settlement missing in game.
   `Modules/TAOM.Dependencies/diag.log` names `MBObjectManager.LoadXML`, `CreateMergedXmlFile` and the five
   `CampaignEventDispatcher` methods as not shielded (WP107's skip line), never as shielded.
3. Return to the main menu and load the same save again: the summary's `xslt_cache_hits` above 0 and a shorter
   merge window than step 2.
4. Start a new campaign through character creation: `[Lifecycle] dispatch=OnNewGameCreated`, map loads normally.
5. Custom battle: `[XmlMerge] summary game=CustomGame`, no `[Lifecycle]` line, battle plays.
6. Mod Options: MCM opens with TAOM's pages; Battle Load Diagnostics shows "Enable Load-Time Stamps" off. Turn it on,
   restart, load: per-group `[PatchApply]`, `[LoadPhase]` and per-handler `[Lifecycle]` lines appear.
7. Crash reporter: the Crash Report "throw on next application tick" dev setting writes a bundle.
8. Capture a castle: no quarter-second stall at the siege end (with WP117's map profiler, no ~170 ms frame).
9. Leave the main menu open five minutes: taom_debug gains no stream of loading-window lines.

## Maintainer steps

1. Create the GitHub issue for the pairing guard (Step A1) and write its number into the Status block before Part A.
2. Release: Parts A and D change TAOM.Dependencies (Part B relies on Part A's exclusions). Ship only builds that carry both modules from one tree, and
   at that release bump `Dependencies/_Module/SubModule.xml` `<Version>` and Main's `DependedModuleMetadata`
   `TAOM.Dependencies` pin together (`/release`, #371). Until then the guard keeps a mismatched pair safe but slow.
3. The in-game checks above; optionally the live harness on the desktop as a second module set.
4. `/deep-review` if the executor session could not run it.

## Maintenance notes

- **Creates for later WPs**: `IStampClock`/`StopwatchStampClock` (WP125's screen timer, WP139's OutcomeLog);
  `IlCallScanner.Fingerprint` (WP129, WP139; WP139 also lifts `92839bc52`'s `ExtractOpCodes` on top);
  `PatchCategoryApplier.EndPhase`/`WriteHeldCategoryLines`; `ShieldExclusionCheck`; the seven
  `ExcludedTargetMethods` entries; `LoadTimeStampsHooks.StartHook`.
- **WP117 overlap**: both WPs edit `BattleLoadDiagnosticsSettings.cs`, `IBattleLoadDiagnosticsSettingsProvider.cs`,
  `CoopSettingsRelevance.cs`, `SettingsFingerprintTests.cs` (the census number), the two co-op docs' totals,
  `PatchShieldPolicy.cs` comments and `SubModule.cs`; the second to merge re-applies its own one-line edits and
  recounts. WP117 should skip `43ca92ee9`'s XmlMerge comment hunk (already here). Step C1.4 removes WP117's "until WP118"
  sentences, or names the integrator action when WP117 is not in this base.
- **API snapshot**: this WP does not regenerate `patch-targets.md` (its `-Check` cannot pass on 1.4.x before
  `4ab005dd1`); WP137 regenerates it and must then list the Patch89, Patch99 and Patch100 classes.
- **Re-run the live harness** after any engine update and whenever the module set changes shape: the IL fingerprint
  pins the five mirrored bodies, not every input (the doc's "How to run the harness").
- **The guard is conservative**: it also blocks Patch99 and the stamps when PatchShield would not install at all
  (co-op active or disabled by flag), which costs only speed. If PatchShield ever stops shielding by default, revisit.
- **lords.xslt can regrow**: the generator that grew the indentation is not identified (candidates
  `tools/apply_culture_skills_traits.py`, `tools/rebalance_lords.py`, UNVERIFIED); the new test now fails if it does.
  The 1.4.x `tools/README.md` row for `complete_lords_xslt.py` describes the old rebuild until WP142 syncs it.
- **Deferred**: LOAD-TIME-07 (D14); ButterLib's Save System subsystem (SAVE-TIME-09, WP114); the in-game load
  measurement on v1.4.8 (owed above); the Yotthani provenance note (WP144's provenance step).
- **What a reviewer should probe**: (1) both modules compute the guard in `InitializeStatics`, which runs in
  `IoC.Configure` before the ProcessLoad phase reads `PatchCategories`; (2) the seven exclusion strings match
  `ShieldTargets` exactly; (3) the stamps sit on the right side of the once-per-process guard and `onceStamp?.End()` is
  last; (4) the `[XmlMerge]` summary runs every game init; (5) the harness default order equals the players' list;
  (6) every v1.4.8 re-cite against the Current state table; (7) MapLoad's per-frame cost (a timestamp pair and a
  compare on non-emit frames); (8) Step D1 drops both 1.5.1 builds and the binding test then loads the 1.4.8 build;
  (9) EarlyLog's lock is never held while a logger runs.
