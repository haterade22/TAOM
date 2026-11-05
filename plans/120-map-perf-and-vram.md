# Plan 120: Stop the cheat-mode inventory spike, advise 16 GB players on textures, cull hidden settlement nameplates and release the covered map view's render targets

> **Executor instructions**: Follow this plan step by step. Run every verification command and
> confirm the expected result before moving to the next step. If anything in "STOP conditions"
> occurs, stop and report; do not improvise. Do not edit `plans/README.md`: every WP branch shares
> it, so the integrator records this plan's status and test count when merging. Put the numbers the
> hand-off step lists in your final message instead. The plan has four Parts (A, B, C, D) in a fixed
> order; each Part ends at a green full suite and can be handed off on its own.
>
> **Drift check (run first, Step 0.2)**:
> `git diff --stat 1b00d1acd..HEAD -- Main/Composition/FeatureModules.cs Main/Features/BattleLoadDiagnostics/BattleLoadDiagnosticsSettings.cs Main/Features/CoopInterop/CoopSettingsRelevance.cs Main/_Module/ModuleData/taom_module_strings.xml Main/_Module/ModuleData/Languages Main/_Module/THIRD-PARTY-LICENSES.txt TAOM.Tests/Features/CoopInterop TAOM.Tests/Migration/ReflectionSiteBindingTests.cs docs/features/coop-interop.md docs/features/bannerlord-together-compat.md docs/features/battle-load-diagnostics.md docs/features/dev-console.md docs/reference/harmony-patch-registry.md docs/reference/provenance-register.md docs/reference/taleworlds-api-snapshot`
> Earlier WPs of this programme change most of these (WP105 and WP109: `FeatureModules.cs`,
> the license file, the provenance register, the registry, the snapshot; WP106 and WP113:
> `BattleLoadDiagnosticsSettings.cs`, `CoopSettingsRelevance.cs`, the fingerprint test and the two
> co-op docs; WP111: strings, `CoopSettingsRelevance.cs`). Compare the live files with "Current
> state" and STOP only on a real mismatch: an anchor this plan names is missing or appears twice.

## Status

- **Priority**: P1 (Parts A and B are P1; Part C is P2; Part D is P3)
- **Effort**: L (four Parts, four commits, about 224 new test results)
- **Risk**: MED. Parts A and C patch engine methods (two soft-failing transpilers; a bool prefix on a
  per-frame map update that falls back to vanilla on any doubt). Part D calls a vanilla GPU release
  every 20th map cover. Part B shows one popup.
- **Depends on**: WP109 merged (it brings WP104 to WP107 with it) in `bannerlord-1.4.5`
- **Category**: perf (memory, map frame time, VRAM)
- **Wave / lane**: wave 2, lane D
- **Executor**: laptop
- **Decisions applied**: D1 (no `CHANGELOG.md` staging; the commit body is the entry), D2 (the two
  ported features arrive as their 1.5.x `*Module.cs` plus a `FeatureModules.All` line; the two new
  1.4.x features are written as modules too, so no single-owner file is edited), D7 (no co-op work;
  `6d7e67f3f` and the `Client/` doc cites are skipped), D8 (subjects carry `v2.0.28`), D9 (every gate
  runs locally), D10 (an ordinary session executes this plan), D19 (git only through the Bash tool),
  D20 (a ModuleData commit needs the refreshed laptop Armory), D22 (NameplateCull ports with the
  v1.4.8 hideout range and re-pinned call lists)
- **Planned at**: 1.4.x `origin/bannerlord-1.4.5` `1b00d1acd` and 1.5.x `origin/bannerlord-1.5.x`
  `e45f6df2c`, 2026-11-04 (every 1.5.x source below is named by its own commit, so a moved tip does
  not matter)
- **Issue**: Part C is the port of #777, Part D the port of #778. Parts A (MEMORY-01) and B
  (MEMORY-04) are new work: the orchestrator creates their issues before implementation lands and
  gives you the numbers; if you were given none, write `to be created by the orchestrator (plan 120
  Part A)` (or Part B) in the feature doc and say so in the hand-off.

## Why this matters

On a 16 GB machine TAOM 1.4.8 sits at 9 to 10 GB on the campaign map, so memory spikes page the
whole session. With vanilla cheat mode on, opening your own inventory lists every item in the game:
one such open in a 2026-11-01 player log (taom_debug 2026-11-01_20-16-56, lines 2755 to 2782; cheat
mode inferred, not logged) grew the managed heap from 489 to 2,841 MB in 29 s, took private bytes to
13,965 MB at 96% memory load with 608 MB free, and the main thread wrote nothing for 31 s. TAOM steers
players into cheat mode: every `taom.*` console command refuses to run without it, including the
rescue commands. Texture Quality is the largest memory lever a player has (highest to lowest freed
2.3 GB on the map in the maintainer's 2026-09-12 measurement, live), and nothing tells 16 GB players.
On the map, vanilla updates every settlement nameplate every frame, hidden ones included (988
settlements on the laptop's TAOM_Map v2.0.27); the 1.5.x line's cull, measured there by yotthani,
took that update from 5.5 ms to 2.3 ms per fast-forward frame. Every screen opened over the map may
also strand the map view's render targets in GPU memory until the next battle (yotthani, 1.5.x,
vanilla too; not measured on v1.4.8). This plan stops the spike, tells the player once, and ports the
two map guards with the v1.4.8 changes they need.

## Current state

### Executor environment

- Laptop, repo `C:\Users\mikew\source\repos\TAOM`; Bannerlord v1.4.8 (build 119303) at
  `C:\Program Files (x86)\Steam\steamapps\common\Mount & Blade II Bannerlord` (env
  `BANNERLORD_GAME_DIR`). Never write into the game install.
- Worktree `C:/Users/mikew/source/repos/taom-port/120`, branch `port148/120-map-perf-and-vram`, from
  local `bannerlord-1.4.5`. Logs and commit message files go to `C:/Users/mikew/source/repos/taom-port`
  (outside git). Every Bash call sets its own variables. Long runs need a tool timeout of 600000 ms.

```
WT=C:/Users/mikew/source/repos/taom-port/120
AUX=C:/Users/mikew/source/repos/taom-port
```

### What the earlier WPs give you (Step 0.3 checks each)

| From | What | Used by |
|---|---|---|
| WP105 | `Main/Composition/` (`TaomFeatureModule` with `Id`, `ParkedReason`, `RegisterServices(IRegistrator)`, `InitializeStatics(IResolver)`, `PatchCategories`, `OnPhase(ApplyPhase, IResolver)`; `ApplyPhase` `ProcessLoad`/`MainMenu`/`GameInit`/`FirstMission`; `PatchCategoryDecl(category, phase)`; `FeatureModules.All`). `ModuleRunner.RunPhase` applies a module's categories for the phase, then calls its `OnPhase` (`1efdf2985:Main/Composition/ModuleRunner.cs:50-59`). `MainMenu` is the first `OnBeforeInitialModuleScreenSetAsRoot`, `GameInit` the first `OnGameInitializationFinished`, once per process. Porting rules: `docs/features/composition-root.md` | all four Parts |
| WP107 | `PatchShieldPolicy.IsExcludedTargetMethod(string?, string?)` and `IsExcludedTargetNamespace(string?)` | `NameplateCullBindingTests.PatchShield_StillWrapsTheUpdate_ByNameAndByNamespace` |
| WP109 | `Main/Features/MissionPerf/TickProfilerTranspiler.cs` (`CallSwap`, the soft-failing `TickProfilerTranspiler.Rewrite`); `MissionStartGuardModule()` and `SkeletonBufferModule()` lines in `FeatureModules.All`; the `VANILLATUNING PORTIONS` block in `Main/_Module/THIRD-PARTY-LICENSES.txt`; the `Yotthani VanillaTuning` row and `### Yotthani VanillaTuning` section in `docs/reference/provenance-register.md`; `## Patch103_MissionStartGuard` in the registry | Part A transpilers; Parts C and D license, provenance and registry edits |

The composition API did not change between the plan 018 merge `1efdf2985` and `ecaa67d26^`
(`git diff --stat 1efdf2985 ecaa67d26^ -- Main/Composition/` touches only `FeatureModules.cs`), so the
two 1.5.x modules compile against WP105's runner unchanged.

### 1.4.x files and roles (excerpts from `origin/bannerlord-1.4.5`)

- `Main/Features/BattleLoadDiagnostics/BattleLoadDiagnosticsSettings.cs`: MCM page `TAOM.BattleLoadDiagnostics`,
  `json2`, 7 settings on origin. The last property is (`:48-51`):

```csharp
    [SettingPropertyGroup("Memory Sampler")]
    [SettingPropertyInteger("Sample Interval (seconds)", 10, 120, Order = 1, RequireRestart = false,
        HintText = "Seconds between [MemSample] lines. ...")]
    public int MemorySampleIntervalSeconds { get; set; } = 30;
}
```

  No `Map Performance` group on origin. WP117 (same wave) may add `EnableMapProfiler` there at
  `Order = 0`; this plan uses Orders 1 to 4, as the 1.5.x line does for 1 to 3
  (`e45f6df2c:Main/Features/BattleLoadDiagnostics/BattleLoadDiagnosticsSettings.cs:94-112`).
- `Main/Features/CoopInterop/CoopSettingsRelevance.cs`: `PlayerLocal` set (`:86-98`, last entry
  `"OfferLeaveOnArrival",`), `Presentation` set (`:101-113`; holds `"EnableNativeSkinFixes",`, then a
  comment line starting `// Already suppressed under co-op` above
  `"FastForwardMultiplier", "ExtraFastForwardMultiplier", "CtrlSpaceMultiplier",`).
  `SettingsFingerprintTests` fails if a name here stops matching a real property.
- `TAOM.Tests/Features/CoopInterop/SettingsFingerprintTests.cs:211`:
  `AssertSplit(typeof(BattleLoadDiagnosticsSettings), reflected: 7, covered: 0);` (WP106 makes it 8,
  WP113 may make it 9). `EveryDocQuotingTheSettingsCounts_AgreesWithReflection` (`:216-254`) requires
  `docs/features/coop-interop.md` to contain `**<total>**` and `docs/features/bannerlord-together-compat.md`
  to contain ` <total> MCM settings` (origin: 243 total, 180 simulation-relevant;
  `coop-interop.md:310-318` also names the per-class split and an "excluded" figure).
- `TAOM.Tests/Features/CoopInterop/CoopVetoClassificationTests.cs`: every `static bool Prefix(` must be
  classified in `Registry`; the `// --- Parked ---` section starts at `:270`.
- `TAOM.Tests/Migration/ReflectionSiteBindingTests.cs`: 5-column `[DataRow]`s above
  `public void ReflectionSite_ResolvesAgainstInstalledEngine(` (`:106` on origin; WP109 adds six rows).
  The fifth column is a label only.
- `Main/Features/BattleLoadDiagnostics/MemorySampleReader.cs:64`: `public static bool TryRead(out MemorySample sample)`
  (P/Invoke `GlobalMemoryStatusEx`, never throws); `MemorySample.TotalPhysMb` (`Domain/MemorySample.cs:39`).
- `Main/Adapters/IInquiryAdapter.cs`: `void ShowMessage(string key, string fallback, string variableName, string variableValue, string secondVariableName = null, string secondVariableValue = null)`
  (a chat line built as `"{=" + key + "}" + fallback`); registered by `Main/Features/Enlistment/EnlistmentIoC.cs:45`
  (`container.Register<TAOM.Adapters.IInquiryAdapter, TAOM.Adapters.InquiryAdapter>(Reuse.Singleton);`).
- `Main/Features/BattleLoadDiagnostics/StallReportNotifier.cs:25-34`: the precedent for a popup at the
  main menu: `new InquiryData(titleText: ..., text: ..., isAffirmativeOptionShown: true, ...)` then
  `InformationManager.ShowInquiry(inquiry, pauseGameActiveState: false);`, called from
  `OnBeforeInitialModuleScreenSetAsRoot` (`Main/SubModule.cs:558-560`).
- `Main/Features/DevConsole/DevConsoleGuard.cs:59-70` and `TaomConsole.cs:41`: every `taom.*` command
  requires `Game.CheatMode`.
- `docs/reference/harmony-patch-registry.md`: numbered sections, then `## Patch_MissionTime_SetMovementOrder`
  (`:1004`), `## Late_ActionSetOverride` (`:1010`). Non-numbered categories already exist.
- `TAOM.Tests/Infrastructure/Localization/UnregisteredLocalizationKeyBaselineTests.cs`: a literal
  `{=taom_*}` in C# with no row in a non-`Languages` ModuleData XML fails the build. Rows go above
  `</strings>` in `Main/_Module/ModuleData/taom_module_strings.xml`, tab-indented:
  `	<string id="taom_x" text="{=taom_x}..." />`.
- `TAOM.Tests/Infrastructure/Localization/LanguageFileCoverageTests.cs` (`EveryLanguage_DeclaresARowForEveryEnglishKey`,
  no baseline or exemption on 1.4.x): every key in a non-`Languages` ModuleData XML needs a row with the same
  id in each of the 12 `Languages/<LANG>/std_taom_module_strings_<code>.xml` files, so Parts A and B seed
  English rows with the helper in "Commands you will need" (WP135 translates them). Those files use raw
  CR LF rows, four-space indent and a closing `  </strings>` line. Parts C and D add no string key.
- `TAOM.Tests/Migration/IlCallScanner.cs`: `FindCallers(Assembly, Func<MethodBase,bool>, out List<string>, out int)`
  and `ExtractCalledMethods(MethodBase, byte[])` exist on 1.4.x (both used by ported tests).
- `TAOM.Tests` uses MSTest 3.1.1 and NSubstitute 5.1.0, no test parallelization; `InternalsVisibleTo`
  `TAOM.Tests` is set in `Main/TAOM.csproj`.
- Absent on origin (checked with `git grep` and `git cat-file`, all empty): `NameplateCull`,
  `MapViewRelease`, `CheatScreenGuard`, `TextureAdvice`, `Patch104`, `CullHiddenNameplates`,
  `ReleaseMapViewMemory`, `MapViewReleaseInterval`, `Map Performance`, `CampaignHotPathSettingsProvidersTests.cs`,
  `HotPathSettingsProvidersTests.cs`, `docs/features/map-perf-profiler.md`. No TAOM code patches
  `SettlementNameplatesVM`, `InventoryScreenHelper.OpenInventoryPresentation` or
  `PartyScreenHelper.OpenScreenAsNormal`, and none adds a `SceneLayer` (`git grep "new SceneLayer" -- Main`
  is empty). TAOM's Harmony id is `com.taom.mod` (`Main/SubModule.cs:161`).

### Engine facts (installed v1.4.8; taom-src paths under `C:\Users\mikew\.taom-src\v1.4.8\`, read this session)

**Cheat screens (Part A).**
- `Helpers.InventoryScreenHelper` is `public static` (`:15`). `private static void OpenInventoryPresentation(TextObject leftRosterName, Action doneLogicExtrasDelegate = null)`
  (`:174-196`): `if (Game.Current.CheatMode)` (`:177`) then, unless a test harness runs, one
  `itemRoster.AddToCounts(item, 10)` per `ItemObject` (`:182-187`). `public static void OpenScreenAsInventory(Action doneLogicExtrasDelegate = null)`
  (`:271-274`) is its only caller. No overload of `OpenInventoryPresentation` exists.
- `Helpers.PartyScreenHelper` is `public static` (`:18`). `public static void OpenScreenAsNormal()`
  (`:185-195`): `if (Game.Current.CheatMode) OpenScreenAsCheat(); else OpenPartyScreen();`.
  `OpenScreenAsCheat` (`:116`) builds every troop's roster; `private static void OpenPartyScreen(bool isDonating = false)` (`:43`).
- `TaleWorlds.Core.Game` is `public sealed class` (`:14`); `public bool CheatMode => GameManager.CheatMode;`
  (`:67`); `GameManagerBase.CheatMode` is abstract (`:43`); `MBGameManager.CheatMode => NativeConfig.CheatMode` (`:22`);
  `NativeConfig.CheatMode` is the engine config's flag (`:7`, set at `:45`).
- Installed IL (`ilspycmd 9.1 --ilcode` on `TaleWorlds.CampaignSystem.dll`): exactly one
  `callvirt instance bool [TaleWorlds.Core]TaleWorlds.Core.Game::get_CheatMode()` in
  `OpenInventoryPresentation` (IL_000b) and exactly one in `OpenScreenAsNormal` (IL_0005).
- Vanilla callers (v1.4.8 dump `C:\Decompiled_Bannerlord\_modules_build\SandBox__SandBox.View.cs`):
  `OpenScreenAsInventory` at `:5104` (mission I key), `:16036` (map), `:22497`, `:22504` (navigation);
  `OpenScreenAsNormal` at `:5126`, `:16025`.

**Texture advice (Part B).**
- `TaleWorlds.Engine.Options.NativeOptions.GetConfig(NativeOptionsType type)` (`:274`) returns `float`;
  `NativeOptionsType.TextureQuality` exists (`:53`).
- `NativeSelectionOptionData.GetOptionsLimit(NativeOptions.NativeOptionsType optionType)` is
  `public static int` (`:29`) and returns 3 for `TextureQuality` (`:39`). Native's
  `ModuleData/global_strings.xml:877-885` names the levels 0 Low, 1 Medium, 2 High. The option sits in
  Options, Performance, group "Graphics", beside Foliage Quality and Terrain Quality (dump
  `TaleWorlds.MountAndBlade.Options/OptionsProvider.cs:53-80`).
- `InquiryData(string titleText, string text, bool isAffirmativeOptionShown, bool isNegativeOptionShown, string affirmativeText, string negativeText, Action affirmativeAction, Action negativeAction, ...)`
  (`TaleWorlds.Library.InquiryData.cs:33`); `InformationManager.ShowInquiry(InquiryData data, bool pauseGameActiveState = false, bool prioritize = false)` (`:91`).
- The maintainer's 2026-09-12 run (`docs/investigations/native-commit-audit-2026-08.md:636-666`):
  texture 2 to 0 freed 2,318 MB, foliage 4 to 0 freed 769 MB, "all freed on a live switch with no
  restart" (before the 2026-09-13 map downsizing; one step down was never measured). Never put a
  number in the player-facing text.

**Nameplate cull (Part C).**
- `SandBox.ViewModelCollection.Nameplate.SettlementNameplatesVM`: `private readonly Camera _mapCamera;` (`:18`),
  `private Vec3 _cachedCameraPosition;` (`:20`), `private readonly ParallelForAuxPredicate UpdateNameplateAuxMTPredicate;` (`:22`),
  `public MBReadOnlyList<SettlementNameplateVM> AllNameplates` (`:42`). `public void Update()` (`:238-248`):
  `_cachedCameraPosition = _mapCamera.Position;`, `TWParallel.For(0, count, UpdateNameplateAuxMTPredicate, 16);`,
  then `RefreshBindValues()` for every plate. Its caller is `GauntletMapSettlementNameplateView.OnMapScreenUpdate`
  (v1.4.8 dump `SandBox__SandBox.GauntletUI.cs:10297-10300`).
- `SettlementNameplateVM`: `_worldPos` (`:72`), `_bindIsVisibleOnMap` (`:108`), `public Settlement Settlement { get; }` (`:148`),
  `IsTracked` (`:296`), `IsInRange` whose setter alone registers the three listeners (`:332-358`),
  `RefreshDynamicProperties(bool forceUpdate)` (`:474-532`; hideout range at `:500-509`:
  `SettlementComponent is ISpottable && IsSpotted`, otherwise `IsInspected`), `RefreshPosition` parks a
  hidden plate at `new Vec2(-1000f, -1000f)` (`:570`), `UpdateNameplateMT(Vec3)` (`:681-688`),
  `RefreshBindValues()` (`:744-770`), `IsVisible` with 400 / 200 / +100 (`:772-793`).
- `NameplateVM`: `IsVisibleOnMap` (`:65`), `IsTargetedByTutorial` (`:81`), `Position` (`:99`).
- `TWParallel.ParallelForAuxPredicate(int, int)` is nested (`:11`); `For(int, int, ParallelForAuxPredicate, int grainSize = 16)` (`:36`).
- `Settlement` (sealed, `ITrackableBase`, `:27`): `Party` (`:97`), `IsVisible` (`:130-144`), `IsInspected` (`:147`),
  `SettlementComponent` (`:270`), `HasPort` (`:291`), `IsTown` (`:370`), `IsFortification` (`:394`),
  `IsHideout` (`:408`). `ISpottable { bool IsSpotted { get; set; } }`; `Hideout : SettlementComponent, ISpottable`
  (`:13`) with its own `_isSpotted` field (`:49-59`). `PartyBase.IsVisualDirty` (`:454`);
  `VisualTrackerManager.CheckTracked(ITrackableBase)` (`:84`). The dump's `IncidentEffect.cs:699-700`
  sets `hideout.IsSpotted = true; hideout.Settlement.IsVisible = false;`, so the two disagree in vanilla
  play: that is decision D22.
- Installed IL of `SandBox.ViewModelCollection.dll`, read this session: `Update` calls distinct
  `Camera.get_Position`, ``List`1.get_Count``, `TWParallel.For` (once), ``List`1.get_Item``,
  `SettlementNameplateVM.RefreshBindValues` (once); `UpdateNameplateMT` calls exactly
  `CalculatePosition`, `DetermineIsInsideWindow`, `DetermineIsVisibleOnMap`, `NameplateVM.RefreshPosition`,
  `NameplateVM.RefreshDynamicProperties`; `RefreshPosition` holds two `ldc.r4 -1000`; `IsVisible` holds
  `0`, `400`, `200`, `100`; only `set_IsInRange` calls the three `RegisterEvents`; `_cachedCameraPosition`
  is read only in `UpdateNameplateAuxMT` and written only in `Update`. `RefreshDynamicProperties` has
  44 calls and differs from the 1.5.x pin in two places (see Step C1); `RefreshBindValues` has 23 calls,
  the 1.5.x pin minus `SettlementNameplateVM.set_HasFerry`. `Settlement.FerryTarget` does not exist on v1.4.8.

**Map view release (Part D).**
- `ScreenLayer`: `IsActive` (`:25`), `public static event Action<ScreenLayer> OnLayerActiveStateChanged` (`:39`),
  raised in `HandleActivate` after `IsActive = true; OnActivate();` (`:110-118`) and in `HandleDeactivate`
  after `OnDeactivate(); IsActive = false; ScreenManager.TryLoseFocus(this);` (`:120-129`). In
  `TaleWorlds.ScreenSystem.dll` only `HandleDeactivate` calls `ScreenLayer.OnDeactivate` (installed IL).
- `SceneLayer(bool clearSceneOnFinalize = true, bool autoToggleSceneView = true)` (`:17`); `OnDeactivate`
  disables the view only when `AutoToggleSceneView` (`:37-44`); `public void ClearRuntimeGPUMemory(bool remove_terrain)`
  is `_sceneView.ClearAll(clearScene: false, remove_terrain)` (`:145-148`; IL: `ldc.i4.0`, one `SceneView::ClearAll(bool, bool)`).
- `MapScreen`: `public static MapScreen Instance` (`:257`), `public SceneLayer SceneLayer` (`:294`),
  `ClearGPUMemory()` calls `SceneLayer.ClearRuntimeGPUMemory(true)` (`:507-511`), the map builds
  `new SceneLayer(true, false)` (`:651`), `OnPause` runs `HandleIfSceneIsReady` (`:453-461`), `OnActivate`
  too (`:468-480`), `OnResume` has none (`:423`). `HandleIfBlockerStatesDisabled` (`:833-860`) raises the
  global loading window while the view is not ready and lowers it after three ready frames.
- `ScreenBase.HandleActivate` activates layers (`:85`) before `OnActivate` (`:88`); `HandleResume`
  (`:122` before `:126`); `HandlePause` deactivates layers (`:141`) before `OnPause` (`:145`).
- Vanilla's own release: `MissionCampaignView.OnMissionScreenPreLoad` calls `MapScreen.ClearGPUMemory()`
  when `Mission.NeedsMemoryCleanup` (dump `SandBox__SandBox.View.cs:2270-2278`); `MissionState.OpenNew`
  defaults `needsMemoryCleanup = true` (dump `MissionState.cs:302`).

**PatchShield on 1.4.x (origin).** It shields every Harmony-patched method not declared in a TAOM
assembly, outside three UI namespace prefixes. After a missing-API swallow it unpatches each owner's
prefixes, postfixes and transpilers unless `PatchShieldPolicy.IsProtectedOwner` matches; that is a
case-insensitive prefix match against a list that starts with `"TAOM"` (`Dependencies/Foundation/PatchShieldPolicy.cs:23-25`, `:84-96`),
and `com.taom.mod` does not start with it. So a swallow on a method TAOM patches strips TAOM's patch
there and vanilla runs. Re-read both files after WP107 before writing this into a doc.

### Conventions that bind this change

- ADR-007 adapters: services never touch sealed TaleWorlds types. Engine calls live in
  `Main/Adapters/*`; hooks (patch classes, the swap helpers) may read `Game` and `__instance` and hand plain
  values to services.
- ADR-002 thin entry points (under 150 lines): patch classes and helpers only delegate.
- ADR-003 no `#region`; ADR-004 no `[Obsolete]`; ADR-005 no `#if DEBUG` outside IoC registration.
- TDD: each Part writes or copies its tests first and shows the RED build before the code.
- MCM `json2`: a persisted setting keeps its value, so rename a key to change its default, never flip
  it. The three ported keys keep their 1.5.x names and defaults: both lines use the same settings Id
  and folder, so a machine that ran a 1.5.x build may already hold values under those names.
- `.claude/rules/harmony-patches.md`: `MovementOrder` postfixes go through the shared deferred category
  (none here). A bool prefix needs a `CoopVetoClassificationTests` entry; transpilers do not.
- D2 and `docs/features/composition-root.md`: copy each 1.5.x `*Module.cs` as is, append it to the END
  of `FeatureModules.All`, never apply its category or add it by hand in `SubModule.cs` or `IoC.cs`. Keep
  the full type names `TAOM.Features.NameplateCull.NameplateCullModule` and
  `TAOM.Features.MapViewRelease.MapViewReleaseModule`: VanillaTuning v0.1.1 stands its own copies down on
  those names (1.5.x line, `7eac30153`).
- Per-port procedure: never let `cherry-pick` create a commit; stage explicit paths; `git commit -F` in
  the NEXT sequential Bash call; drop bookkeeping (`CHANGELOG.md`, `CLAUDE.md`, `AGENTS.md`,
  `docs/reviews/**`, `docs/INDEX.md`, `docs/reference/feature-map.md`, `tools/README.md`). This plan edits
  no `.claude/` file.

## Commands you will need

| Purpose | Command | Expected |
|---|---|---|
| Restore | `cd $WT && dotnet restore TAOM.sln` | exit 0 |
| Build | `cd $WT && dotnet build TAOM.sln -p:DisableModuleCopy=true -p:ModuleId= > $AUX/120-build.log 2>&1; echo "exit=$?"; grep -E "[0-9]+ Error\(s\)" $AUX/120-build.log \| tail -1` | `exit=0`, `0 Error(s)` (the BHA0001 Patch88 warning is known) |
| Full tests | `cd $WT && dotnet test TAOM.Tests --no-build -p:DisableModuleCopy=true -p:ModuleId= > $AUX/120-test.log 2>&1; grep -E "Passed!\|Failed!" $AUX/120-test.log \| tail -1` | `Failed: 0`; totals as each step says |
| Filtered | `cd $WT && dotnet test TAOM.Tests --no-build -p:DisableModuleCopy=true -p:ModuleId= --filter "<filter>" 2>&1 \| grep -E "Passed!\|Failed!"` | as each step says |
| Binding gate | `cd $WT && dotnet test TAOM.Tests --no-build -p:DisableModuleCopy=true -p:ModuleId= --filter "TestCategory=BindingVerification" 2>&1 \| grep -E "Passed!\|Failed!"` | `Failed: 0`; record Skipped |
| Data | `cd $WT && python tools/validate_moduledata.py > $AUX/120-data-<step>.txt 2>&1; tail -15 $AUX/120-data-<step>.txt` | no ERROR the Step 0.5 baseline lacks |
| Docs | `cd $WT && python tools/lint_docs.py --summary --dash-base $BASE` | no counter above the Step 0.6 record |
| Snapshot check | `cd $WT && pwsh tools/snapshot_api_surface.ps1 -Check; echo "exit=$?"` | as Step 0.7 records |

Both `-p:` flags are mandatory on every build and test (on 1.4.x `Main/TAOM.csproj` does not gate
CopyModule on `DisableModuleCopy`, so a build without `-p:ModuleId=` deploys into the game). Never
`./build.ps1`. Never two builds at once in one worktree. A filtered run needs a Build first.

Message check (run before every commit; set `p` to the message file):

```
python - <<'PY'
import re
p = r"C:/Users/mikew/source/repos/taom-port/120-msg-X.txt"
t = open(p, encoding="utf-8").read()
subj = t.splitlines()[0]
ok = bool(re.match(r"^[a-z][a-z0-9-]*(?:\([^)]+\))?!?: v2\.0\.28 - \S", subj)) and len(subj) <= 72
bad = any(c in t for c in "\u2013\u2014") or "Co-Authored-By" in t or "Generated with" in t
longest = max(len(l) for l in t.splitlines())
print("subject ok" if ok else "SUBJECT BAD", "| dash or attribution:", bad, "| longest line:", longest)
PY
```

Expected: `subject ok | dash or attribution: False | longest line:` and a number of at most 72.

Language rows helper (A2.6 and B2.2 run it from `$WT` with their `ROWS`; no provider call, no cost). It
stops on an assertion if a key is already present or a file lacks exactly one closing `  </strings>`;
that is a STOP. Expected output `ok 12`:

```
python -X utf8 - <<'EOF'
import glob, pathlib, xml.etree.ElementTree as ET
ROWS = [("<key>", "<English text>")]
files = sorted(glob.glob("Main/_Module/ModuleData/Languages/*/std_taom_module_strings_*.xml"))
assert len(files) == 12, files
for f in files:
    p = pathlib.Path(f)
    data = p.read_bytes()
    assert data.count(b"\n  </strings>") == 1, f
    for key, _ in ROWS:
        assert ('id="%s"' % key).encode() not in data, (f, key)
    rows = b"".join(('    <string id="%s" text="%s" />' % (k, t)).encode("utf-8") + b"\r\n" for k, t in ROWS)
    new = data.replace(b"\n  </strings>", b"\n" + rows + b"  </strings>", 1)
    ET.fromstring(new)
    p.write_bytes(new)
print("ok", len(files))
EOF
```

## Scope

**In scope (create)**
- `Main/Features/CheatScreenGuard/**` (11 files, Part A), `Main/Features/TextureAdvice/**` (5 files, Part B),
  `Main/Features/NameplateCull/**` (11 files from `ecaa67d26`, Part C), `Main/Features/MapViewRelease/**`
  (9 files from `ecaa67d26`, Part D)
- `Main/Adapters/{I,}TextureAdviceAdapter.cs`, `Main/Adapters/{I,}NameplateCullAdapter.cs`,
  `Main/Adapters/{I,}MapViewReleaseAdapter.cs`
- `TAOM.Tests/Features/CheatScreenGuard/**` (6), `TAOM.Tests/Features/TextureAdvice/**` (5),
  `TAOM.Tests/Features/NameplateCull/**` (7 from `ecaa67d26` plus `NameplateCullSettingsProviderTests.cs`),
  `TAOM.Tests/Features/MapViewRelease/**` (7 from `ecaa67d26`)
- `docs/features/cheat-screen-guard.md`, `docs/features/texture-advice.md`, `docs/features/nameplate-cull.md`,
  `docs/features/map-view-release.md`

**In scope (edit)**
- `Main/Composition/FeatureModules.cs` (four appended lines)
- `Main/Features/BattleLoadDiagnostics/BattleLoadDiagnosticsSettings.cs` (four settings)
- `Main/Features/CoopInterop/CoopSettingsRelevance.cs`, `Main/_Module/ModuleData/taom_module_strings.xml`,
  `Main/_Module/THIRD-PARTY-LICENSES.txt`, the 12 `Main/_Module/ModuleData/Languages/*/std_taom_module_strings_*.xml` (English rows only)
- `TAOM.Tests/Features/CoopInterop/SettingsFingerprintTests.cs`, `TAOM.Tests/Features/CoopInterop/CoopVetoClassificationTests.cs`,
  `TAOM.Tests/Migration/ReflectionSiteBindingTests.cs`
- `docs/features/{coop-interop,bannerlord-together-compat,battle-load-diagnostics,dev-console}.md`,
  `docs/reference/{harmony-patch-registry,provenance-register}.md`,
  `docs/reference/taleworlds-api-snapshot/{patch-targets,reflection-sites}.md`,
  `docs/features/map-perf-profiler.md` only if it exists (WP117 creates it)

**Out of scope (do NOT touch)**
- `Main/SubModule.cs`, `Main/IoC.cs` (the runner wires all four features), `Main/TAOM.csproj`,
  `TAOM.Tests/TAOM.Tests.csproj` (SDK globbing picks up new files), `Directory.Build.props`,
  `.claude/**`, `.ai/**`, `docs/adrs/**`, `Dependencies/**`.
- `Main/Adapters/{I,}GraphicsOptionsAdapter.cs` (WP111 creates it; WP117 adds `TextureQualityOption`).
- `Main/Features/MissionPerf/**`, `Main/Features/MissionStartGuard/**`, `Main/Features/SkeletonBuffer/**` (WP109's).
- `CampaignHotPathSettingsProvidersTests.cs`, `HotPathSettingsProvidersTests.cs` (absent; WP123 brings them).
- `docs/reference/engine/**` (WP117, WP144), `CHANGELOG.md`, `docs/reviews/**`, `docs/INDEX.md`,
  `docs/reference/feature-map.md`, `tools/README.md`, `plans/**` (including `plans/README.md`).

## Git workflow

- Work only in `$WT` on branch `port148/120-map-perf-and-vram`. Never push, open a PR, tag or merge into
  `bannerlord-1.4.5`. Git through the Bash tool only (D19).
- Copy files from a commit with `git -C $WT checkout ecaa67d26 <path> [<path>...]` and NO `--`
  separator (the `--` form trips the dangerous-git hook). This form also stages the files. Use a
  directory path only for a directory Step 0.3 proved absent.
- Stage with explicit `git add <paths>`; never `git add -A`, `-u` or `.`. Then `git commit -F <msgfile>`
  in the NEXT sequential Bash call. Never `--no-verify`. If a hook blocks, STOP and report.
- Each step gives the commit subject and a body you may use as is: wrapped at 72, written for 1.4.x
  readers, no em or en dash, no `Co-Authored-By` or `Generated with` line.

## Steps

### Step 0: worktree, prerequisites, baseline

0.1 Create the worktree and record the base:

```
cd /c/Users/mikew/source/repos/TAOM && mkdir -p /c/Users/mikew/source/repos/taom-port && git worktree add C:/Users/mikew/source/repos/taom-port/120 -b port148/120-map-perf-and-vram bannerlord-1.4.5 && git -C C:/Users/mikew/source/repos/taom-port/120 log -1 --format='BASE=%H %s'
```

Write the printed SHA into `$AUX/120-base.txt`; later steps call it `$BASE`. Confirm in the main
checkout's `plans/README.md` that WP109 is merged; if not, STOP.

0.2 Run the drift check from the header; read the diff of each listed path against "Current state".

0.3 Prerequisite and collision checks (one Bash call):

```
WT=C:/Users/mikew/source/repos/taom-port/120; cd $WT
grep -c "internal static readonly TaomFeatureModule\[\] All" Main/Composition/FeatureModules.cs
grep -c "MainMenu," Main/Composition/TaomFeatureModule.cs
grep -cE "new Features\.(MissionStartGuard\.MissionStartGuardModule|SkeletonBuffer\.SkeletonBufferModule)\(\)," Main/Composition/FeatureModules.cs
grep -c "internal sealed class CallSwap" Main/Features/MissionPerf/TickProfilerTranspiler.cs
grep -cE "public static bool IsExcludedTarget(Method|Namespace)\(" Dependencies/Foundation/PatchShieldPolicy.cs
grep -c "VANILLATUNING PORTIONS" Main/_Module/THIRD-PARTY-LICENSES.txt
grep -c "SkeletonBuffer/, and their adapters in Main/Adapters/, in the TAOM repository)." Main/_Module/THIRD-PARTY-LICENSES.txt
grep -c "Yotthani VanillaTuning" docs/reference/provenance-register.md
grep -cE "^## (Patch103_MissionStartGuard|Patch_MissionTime_SetMovementOrder|Late_ActionSetOverride)$" docs/reference/harmony-patch-registry.md
grep -c "public static bool TryRead(out MemorySample sample)" Main/Features/BattleLoadDiagnostics/MemorySampleReader.cs
grep -c "void ShowMessage(" Main/Adapters/IInquiryAdapter.cs
grep -c "IInquiryAdapter, TAOM.Adapters.InquiryAdapter" Main/Features/Enlistment/EnlistmentIoC.cs
grep -c 'SettingPropertyGroup("Map Performance")' Main/Features/BattleLoadDiagnostics/BattleLoadDiagnosticsSettings.cs
for d in Main/Features/CheatScreenGuard Main/Features/TextureAdvice Main/Features/NameplateCull Main/Features/MapViewRelease TAOM.Tests/Features/CheatScreenGuard TAOM.Tests/Features/TextureAdvice TAOM.Tests/Features/NameplateCull TAOM.Tests/Features/MapViewRelease docs/features/cheat-screen-guard.md docs/features/texture-advice.md docs/features/nameplate-cull.md docs/features/map-view-release.md; do test -e $d && echo "EXISTS $d"; done
git grep -nE 'Patch104_|Patch_CheatScreenGuard|CullHiddenNameplates|CheatModeListsAllItemsAndTroops|taom_texture_advice' -- Main TAOM.Tests; echo "names grep done"
test -f docs/features/map-perf-profiler.md && echo "MAP PROFILER DOC PRESENT" || echo "map profiler doc absent"
```

Expected, in order: `1`; `1`; `2`; `1`; `2`; `1`; `1`; `2`; `3`; `1`; `1`; `1`; then `0` (or `1` when
WP117 merged first: record it); no `EXISTS` line; `names grep done` with no hit above it; one of the two
map-profiler lines (record which). Any other result: STOP.

0.4 Baseline: Restore, Build, Full tests. Record T0 = (Total, Passed, Skipped) in `$AUX/120-base.txt`.
Expected `Failed: 0` and the count the integrator recorded for the merged base in `plans/README.md`
(a different count or any failure: STOP). Run the Binding gate and record its Skipped number.

0.5 D20: the main checkout's `plans/README.md` (read only) must show WP101 done with the laptop
`LOTRLOME_Armory` updated and a validator baseline recorded (Parts A and B commit ModuleData); if not,
STOP. Then `python tools/validate_moduledata.py > $AUX/120-data-before.txt 2>&1`; keep the summary.

0.6 `python tools/lint_docs.py --summary --dash-base $BASE > $AUX/120-lint-before.txt`; keep it.

0.7 Snapshot check: `pwsh tools/snapshot_api_surface.ps1 -Check; echo "exit=$?"`. Record SNAP0 = the
exit code. With SNAP0 = 0 the committed snapshot reproduces, and each Part regenerates it (A4, C5, D3).
With SNAP0 other than 0, each Part hand-edits `patch-targets.md` instead (rows given in the steps) and
the hand-off names the pre-existing drift.

## Part A: cheat-screen guard (MEMORY-01, RUNTIME-PERF-14; new work)

Design (decided by the orchestrator under the maintainer's delegation): with cheat mode on, both
screens open as they do without it, unless the new MCM setting asks for the cheat lists (default off).
Method: two transpilers reuse WP109's `TickProfilerTranspiler.Rewrite` to swap the one
`Game.get_CheatMode` read in each opener for a static TAOM helper with the same stack shape
(`bool (Game)`). Any other site count leaves the method vanilla. Rejected: a prefix that rebuilds
`OpenInventoryPresentation` (it would copy a private market helper and an 11-argument `Initialize`);
a numbered category (the 1.5.x line used numbers up to 132 at `e45f6df2c` and up to 141 at its
2026-11-04 tip, so this 1.4.x-only feature takes the name `Patch_CheatScreenGuard`, like
`Patch_MissionTime_SetMovementOrder`).

### Step A1 (RED): the tests

Create these six files under `TAOM.Tests/Features/CheatScreenGuard/` (namespace
`TAOM.Tests.Features.CheatScreenGuard`). Model the game-loading pattern (`[ClassInitialize]` with
`GameAssemblies.EnsureLoaded()`, `RequireGame()` returning `Assert.Inconclusive`) on
`ecaa67d26:TAOM.Tests/Features/NameplateCull/NameplateCullBindingTests.cs:38-46`, and the real-IL
rewrite tests on `ecaa67d26:TAOM.Tests/Features/MissionStartGuard/MissionStartGuardBindingTests.cs:105-184`.

1. `CheatScreenGuardCallsTests.cs` (5 `[TestMethod]`s, no game; each `[TestCleanup]` calls
   `CheatScreenGuardCalls.Initialize(null)`), over `CheatScreenGuardCalls.Decide(bool cheatMode, CheatScreen screen)`:
   cheat mode off returns false and never calls the service; no service returns true (vanilla); service
   true returns true; service false returns false; a service that throws returns true.
2. `CheatScreenGuardServiceTests.cs` (8, no game; NSubstitute `ICheatScreenGuardSettingsProvider`,
   `IInquiryAdapter`, `IModLogger`):
   `ShowCheatList_ToggleOn_ReturnsTrueAndSaysNothing`;
   `ShowCheatList_ToggleOff_ReturnsFalse`;
   `ShowCheatList_FirstHiddenList_LogsOneInfoLineAndShowsOneNotice` (the notice is
   `ShowMessage("taom_cheat_screen_guard_notice", <fallback>, null, null)`);
   `ShowCheatList_LaterHiddenLists_SayNothingMore` (inventory then party screen: still one line, one notice);
   `ShowCheatList_SettingsThrow_HidesTheList` (returns false, the shipped default);
   `ShowCheatList_LoggerAndNoticeThrow_StillReturnsFalse`;
   `LogInstall_BothSitesGated_WritesOneOnInfoLine`;
   `LogInstall_ASiteNotGated_WritesOneWarningNamingIt` (inventory 0, party 1: one `LogWarning` whose text
   contains `inventory gate off`).
3. `CheatScreenGuardSettingsProviderTests.cs` (2): `NoMcm_CheatListsAreOff`
   (`new CheatScreenGuardSettingsProvider().CheatListsEnabled` is false) and
   `ReadsThroughTheCachedSettings` (internal ctor over `new BattleLoadDiagnosticsSettings()`, set
   `CheatModeListsAllItemsAndTroops = true`, the provider reads true).
4. `CheatScreenGuardLinesTests.cs` (3): the ON line starts with `[CheatScreenGuard] ON:`; the partial
   line names both gates (`on`, `off (the call site was not found exactly once)`, `off (the patch did not apply)`
   for 1, 0 and -1); the hidden line names the screen (`inventory` or `party screen`) and the MCM
   setting name.
5. `CheatScreenGuardModuleTests.cs` (6, no game; real DryIoc `Container`, a substitute `IModLogger` and
   `IInquiryAdapter` registered first): the service resolves as a singleton; the provider resolves;
   `InitializeStatics` hands the service to `CheatScreenGuardCalls.Service`; `OnPhase(GameInit)` with
   both `CheatScreenGuardSwaps.Last*Swapped` set to 1 writes exactly one `LogInfo` starting
   `[CheatScreenGuard] ON` (restore both to -1 in cleanup); the other three phases log nothing;
   `FeatureModules.All.OfType<CheatScreenGuardModule>()` has exactly one entry with Id `CheatScreenGuard`,
   `ParkedReason` null and one `PatchCategoryDecl` `("Patch_CheatScreenGuard", ApplyPhase.GameInit)`.
6. `CheatScreenGuardBindingTests.cs` (8; class `[TestCategory("RequiresGame")]`; each method
   `[TestCategory("BindingVerification")]`, the IL ones also `[TestCategory("RequiresGameIL")]`):
   - `Targets_HaveTheirV148Shapes`: `AccessTools.Method(typeof(InventoryScreenHelper), "OpenInventoryPresentation")`
     is private, static, void, parameters `(TextObject, Action)`; `PartyScreenHelper.OpenScreenAsNormal`
     is public, static, void, no parameters.
   - `GameCheatMode_IsAnInstanceBoolGetterOnTheSealedGame`: `AccessTools.PropertyGetter(typeof(Game), nameof(Game.CheatMode))`
     is non-null, non-static, returns bool, declared on `Game`.
   - `EachTarget_ReadsCheatModeExactlyOnce_InTheInstalledEngine`: one `ci.Calls(getter)` in each
     `PatchProcessor.GetOriginalInstructions(target)`.
   - `Rewrite_OnTheInventoryOpener_SwapsOneSiteWithNoWarning` and
     `Rewrite_OnThePartyOpener_SwapsOneSiteWithNoWarning`: `TickProfilerTranspiler.Rewrite(original, CheatScreenGuardSwaps.BuildInventory() (or BuildParty()), "<label>", logger, out var swapped)`;
     no `LogWarning`; `swapped == 1`; same instruction count; no remaining call of the getter; one
     `call` whose operand's declaring type is `CheatScreenGuardCalls`.
   - `PatchClasses_AppliedToTheInstalledEngine_BuildReplacementsTheJitAccepts`: a `Harmony` with id
     `taom.tests.cheatscreenguard.jit` runs `CreateClassProcessor(...).Patch()` for both patch classes;
     both `Last*Swapped` read 1; `RuntimeHelpers.PrepareMethod` on both targets; `UnpatchAll(id)` and
     reset both counters to -1 in `finally`.
   - `PatchClasses_AreOneTranspilerAndOneCleanupEach_InTheLiteralCategory`: each class carries
     `[HarmonyPatchCategory("Patch_CheatScreenGuard")]`, one `[HarmonyTranspiler]`, one
     `[HarmonyCleanup]`, no prefix, postfix or finalizer.
   - `Setting_CheatModeListsAllItemsAndTroops_IsAMapPerformanceBoolOffByDefault`: DisplayName
     `Cheat Mode Lists All Items And Troops`, group `Map Performance`, `RequireRestart` false, non-empty
     hint, `new BattleLoadDiagnosticsSettings().CheatModeListsAllItemsAndTroops` is false.

Build: expected FAILS with `CS0246`/`CS0234` naming `TAOM.Features.CheatScreenGuard` types.

### Step A2 (GREEN): the code

Create these files. All production files start with no license header (TAOM's own work).

`Main/Features/CheatScreenGuard/Models/CheatScreen.cs`: `public enum CheatScreen { Inventory, PartyScreen }`
in namespace `TAOM.Features.CheatScreenGuard.Models`.

`Main/Features/CheatScreenGuard/ICheatScreenGuardSettingsProvider.cs` and `CheatScreenGuardSettingsProvider.cs`:
copy the shape of `ecaa67d26:Main/Features/NameplateCull/NameplateCullSettingsProvider.cs` (public
parameterless ctor, internal ctor taking the settings, lazy `Settings => _settings ??= BattleLoadDiagnosticsSettings.Instance`)
with one member: `public bool CheatListsEnabled => Settings?.CheatModeListsAllItemsAndTroops ?? false;`.

`Main/Features/CheatScreenGuard/ICheatScreenGuardService.cs`:

```csharp
using TAOM.Features.CheatScreenGuard.Models;

namespace TAOM.Features.CheatScreenGuard;

/// <summary>What the two swapped-in cheat-mode reads ask (docs/features/cheat-screen-guard.md). Neither member throws.</summary>
public interface ICheatScreenGuardService
{
    /// <summary>Asked only while cheat mode is on. True opens the vanilla cheat list; false opens the screen as without cheat mode.</summary>
    bool ShowCheatList(CheatScreen screen);

    /// <summary>The install line from the two swap counts: 1 gated, 0 left vanilla, -1 the transpiler never ran.</summary>
    void LogInstall(int inventorySwapped, int partySwapped);
}
```

`Main/Features/CheatScreenGuard/CheatScreenGuardService.cs`: `public sealed class`, ctor
`(ICheatScreenGuardSettingsProvider settings, TAOM.Adapters.IInquiryAdapter notice, IModLogger logger)`.
`ShowCheatList`: read `CheatListsEnabled` in a try (a throw counts as false); true returns true; else, the
first time this launch only (one `bool _noticed` field), log
`CheatScreenGuardLines.BuildHiddenLine(screen)` with `LogInfo` and call
`_notice.ShowMessage(NoticeKey, NoticeFallback, null, null)`, each in its own try with an empty catch;
return false. `LogInstall`: both counts 1 writes `BuildInstallLine()` with `LogInfo`, anything else
`BuildPartialInstallLine(...)` with `LogWarning`, inside a try with an empty catch. Constants:

```csharp
    internal const string NoticeKey = "taom_cheat_screen_guard_notice";
    internal const string NoticeFallback = "TAOM: cheat mode is on. Its every-item inventory and every-troop party lists take gigabytes of memory, so TAOM opened the normal screens. To get the cheat lists, turn on Cheat Mode Lists All Items And Troops in MCM, TAOM Battle Load Diagnostics, Map Performance.";
```

`Main/Features/CheatScreenGuard/CheatScreenGuardLines.cs`:

```csharp
using TAOM.Features.CheatScreenGuard.Models;

namespace TAOM.Features.CheatScreenGuard;

/// <summary>The guard's log lines, built in one place so the tests pin their wording.</summary>
internal static class CheatScreenGuardLines
{
    internal const string Tag = "[CheatScreenGuard]";

    internal static string BuildInstallLine() =>
        $"{Tag} ON: with cheat mode on, the own inventory and the party screen open without the every-item and every-troop lists unless MCM 'Cheat Mode Lists All Items And Troops' is on";

    internal static string BuildPartialInstallLine(int inventorySwapped, int partySwapped) =>
        $"{Tag} not fully installed: inventory gate {Describe(inventorySwapped)}, party screen gate {Describe(partySwapped)}; an ungated screen keeps the vanilla cheat list";

    internal static string BuildHiddenLine(CheatScreen screen) =>
        $"{Tag} cheat mode is on: the {(screen == CheatScreen.Inventory ? "inventory" : "party screen")} opened without the cheat list (MCM 'Cheat Mode Lists All Items And Troops' is off); later opens this launch say nothing more";

    private static string Describe(int swapped) => swapped switch
    {
        1 => "on",
        0 => "off (the call site was not found exactly once)",
        _ => "off (the patch did not apply)",
    };
}
```

`Main/Features/CheatScreenGuard/Hooks/CheatScreenGuardCalls.cs`:

```csharp
using TAOM.Features.CheatScreenGuard.Models;
using TaleWorlds.Core;

namespace TAOM.Features.CheatScreenGuard.Hooks;

/// <summary>
/// The helpers the guard's transpilers swap in for the one <c>Game.get_CheatMode</c> read in each screen opener. Each
/// takes the <see cref="Game"/> the engine pushed, reads the real flag, and asks the service only while it is on. With no
/// service, or when the service throws, the answer is the real flag, so the screen behaves as in vanilla. Main thread.
/// </summary>
internal static class CheatScreenGuardCalls
{
    internal static ICheatScreenGuardService? Service { get; private set; }

    internal static void Initialize(ICheatScreenGuardService? service) => Service = service;

    // v1.4.8 InventoryScreenHelper.cs:177, inside OpenInventoryPresentation.
    internal static bool InventoryCheatMode(Game game) => Decide(game.CheatMode, CheatScreen.Inventory);

    // v1.4.8 PartyScreenHelper.cs:187, inside OpenScreenAsNormal.
    internal static bool PartyCheatMode(Game game) => Decide(game.CheatMode, CheatScreen.PartyScreen);

    internal static bool Decide(bool cheatMode, CheatScreen screen)
    {
        if (!cheatMode) return false;
        var service = Service;
        if (service == null) return true;
        try { return service.ShowCheatList(screen); }
        catch { return true; }
    }
}
```

`Main/Features/CheatScreenGuard/Hooks/CheatScreenGuardSwaps.cs`:

```csharp
using System.Collections.Generic;
using System.Reflection;
using HarmonyLib;
using TAOM.Features.MissionPerf;
using TaleWorlds.Core;

namespace TAOM.Features.CheatScreenGuard.Hooks;

/// <summary>The one call site each transpiler swaps (CheatScreenGuardBindingTests reads the real IL). A missing getter makes the swap misfit, so the method stays vanilla.</summary>
internal static class CheatScreenGuardSwaps
{
    /// <summary>Sites the inventory transpiler swapped on its last run; -1 until it ran or after its apply failed.</summary>
    internal static int LastInventorySwapped { get; set; } = -1;

    /// <summary>The same for the party screen transpiler.</summary>
    internal static int LastPartySwapped { get; set; } = -1;

    private static MethodInfo CheatModeGetter => AccessTools.PropertyGetter(typeof(Game), nameof(Game.CheatMode));

    internal static IReadOnlyList<CallSwap> BuildInventory() => new[]
    {
        new CallSwap(CheatModeGetter, AccessTools.Method(typeof(CheatScreenGuardCalls), nameof(CheatScreenGuardCalls.InventoryCheatMode))),
    };

    internal static IReadOnlyList<CallSwap> BuildParty() => new[]
    {
        new CallSwap(CheatModeGetter, AccessTools.Method(typeof(CheatScreenGuardCalls), nameof(CheatScreenGuardCalls.PartyCheatMode))),
    };
}
```

`Main/Features/CheatScreenGuard/Hooks/InventoryScreenHelper_OpenInventoryPresentation_CheatScreenGuard_Patch.cs`
(the party twin is `PartyScreenHelper_OpenScreenAsNormal_CheatScreenGuard_Patch.cs` with
`[HarmonyPatch(typeof(PartyScreenHelper), nameof(PartyScreenHelper.OpenScreenAsNormal))]`,
`BuildParty()`, label `"PartyScreenHelper.OpenScreenAsNormal"` and `LastPartySwapped`):

```csharp
using System;
using System.Collections.Generic;
using HarmonyLib;
using Helpers;
using TAOM.Features.MissionPerf;

namespace TAOM.Features.CheatScreenGuard.Hooks;

/// <summary>
/// Patch_CheatScreenGuard (docs/reference/harmony-patch-registry.md). One transpiler swaps the single Game.get_CheatMode
/// read in the private InventoryScreenHelper.OpenInventoryPresentation (v1.4.8 :174-196) for
/// <see cref="CheatScreenGuardCalls.InventoryCheatMode"/>. Soft-failing: any other site count leaves the method vanilla,
/// and the install line names it. No logger is passed; the module's GameInit step writes the line.
/// </summary>
[HarmonyPatch(typeof(InventoryScreenHelper), "OpenInventoryPresentation")]
[HarmonyPatchCategory(CheatScreenGuardModule.PatchCategory)]
public static class InventoryScreenHelper_OpenInventoryPresentation_CheatScreenGuard_Patch
{
    [HarmonyTranspiler]
    public static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
    {
        var result = TickProfilerTranspiler.Rewrite(instructions, CheatScreenGuardSwaps.BuildInventory(),
            "InventoryScreenHelper.OpenInventoryPresentation", null, out var swapped);
        CheatScreenGuardSwaps.LastInventorySwapped = swapped;
        return result;
    }

    // An apply that failed after the transpiler ran leaves the method unpatched, so the count must not say gated.
    [HarmonyCleanup]
    public static void Cleanup(Exception ex)
    {
        if (ex != null) CheatScreenGuardSwaps.LastInventorySwapped = -1;
    }
}
```

`Main/Features/CheatScreenGuard/CheatScreenGuardModule.cs`:

```csharp
using System.Collections.Generic;
using DryIoc;
using TAOM.Composition;
using TAOM.Features.CheatScreenGuard.Hooks;

namespace TAOM.Features.CheatScreenGuard;

/// <summary>
/// Cheat-screen guard (docs/features/cheat-screen-guard.md): with vanilla cheat mode on, the own inventory lists every item
/// and the party screen every troop (gigabytes of memory with TAOM's content). Two transpilers gate the one cheat-mode
/// read in each opener behind an MCM setting, default off. Applied at GameInit, before any campaign screen can open; the
/// step after the category writes the install line from both swap counts.
/// </summary>
internal sealed class CheatScreenGuardModule : TaomFeatureModule
{
    // No number: a 1.4.x-only feature, so it takes no number the 1.5.x line could also use.
    internal const string PatchCategory = "Patch_CheatScreenGuard";

    private static readonly PatchCategoryDecl[] Categories = { new(PatchCategory, ApplyPhase.GameInit) };

    public override string Id => "CheatScreenGuard";

    public override void RegisterServices(IRegistrator registrator)
    {
        registrator.Register<ICheatScreenGuardSettingsProvider, CheatScreenGuardSettingsProvider>(Reuse.Singleton);
        registrator.Register<ICheatScreenGuardService, CheatScreenGuardService>(Reuse.Singleton);
    }

    public override void InitializeStatics(IResolver resolver) =>
        CheatScreenGuardCalls.Initialize(resolver.Resolve<ICheatScreenGuardService>());

    public override IReadOnlyList<PatchCategoryDecl> PatchCategories => Categories;

    public override void OnPhase(ApplyPhase phase, IResolver resolver)
    {
        if (phase == ApplyPhase.GameInit)
            resolver.Resolve<ICheatScreenGuardService>().LogInstall(
                CheatScreenGuardSwaps.LastInventorySwapped, CheatScreenGuardSwaps.LastPartySwapped);
    }
}
```

Then these edits:

1. `Main/Composition/FeatureModules.cs`: append as the LAST entry of `All`:
   `        new Features.CheatScreenGuard.CheatScreenGuardModule(),`
2. `BattleLoadDiagnosticsSettings.cs`: directly above the closing `}` of the class, after the last
   property (a blank line first), add:

```csharp
    [SettingPropertyGroup("Map Performance")]
    [SettingPropertyBool("Cheat Mode Lists All Items And Troops", Order = 4, RequireRestart = false,
        HintText = "Vanilla cheat mode (cheat_mode = 1 in engine_config.txt, which TAOM's taom.* console commands need) makes your own inventory list every item in the game and the party screen every troop. With TAOM's thousands of items that list takes gigabytes of memory and can freeze the game for many seconds on a 16 GB machine. Off: with cheat mode on, both screens open as they do without it. On: the vanilla cheat lists. Takes effect at the next screen open. Default OFF.")]
    public bool CheatModeListsAllItemsAndTroops { get; set; } = false;
```

3. `CoopSettingsRelevance.cs`, `PlayerLocal` set: after the line `"OfferLeaveOnArrival",` add:

```csharp
        // Whether YOUR cheat-mode inventory and party screens list every item and troop (CheatScreenGuard). Cheat mode
        // is per install and the player acts on the list; the co-op layer replicates what was taken, not the screen.
        "CheatModeListsAllItemsAndTroops",
```

4. `SettingsFingerprintTests.cs`: raise the `reflected:` number of
   `AssertSplit(typeof(BattleLoadDiagnosticsSettings), ...)` by 1 (`covered: 0` stays).
5. `docs/features/coop-interop.md`, paragraph `**No MCM settings parity: reported, not yet exchanged.**`:
   raise `**<total>**` by 1, the `BattleLoadDiagnosticsSettings` split number by 1, and set the
   "excluded" figure (the number before ` excluded are`) to total minus the simulation-relevant count.
   `docs/features/bannerlord-together-compat.md`: raise `TAOM's <total> MCM settings` by 1.
6. `Main/_Module/ModuleData/taom_module_strings.xml`, directly above `</strings>` after the last
   `<string .../>` line (tab indent):

```xml
	<!-- CheatScreenGuardService -->
	<string id="taom_cheat_screen_guard_notice" text="{=taom_cheat_screen_guard_notice}TAOM: cheat mode is on. Its every-item inventory and every-troop party lists take gigabytes of memory, so TAOM opened the normal screens. To get the cheat lists, turn on Cheat Mode Lists All Items And Troops in MCM, TAOM Battle Load Diagnostics, Map Performance." />
```

   Then run the Language rows helper with
   `ROWS = [("taom_cheat_screen_guard_notice", "TAOM: cheat mode is on. Its every-item inventory and every-troop party lists take gigabytes of memory, so TAOM opened the normal screens. To get the cheat lists, turn on Cheat Mode Lists All Items And Troops in MCM, TAOM Battle Load Diagnostics, Map Performance.")]`.

**Verify A2**: Build `0 Error(s)`. Filtered `FullyQualifiedName~TAOM.Tests.Features.CheatScreenGuard`:
`Failed: 0`, `Total: 32`, `Skipped: 0` (a skip means the game assemblies did not load: report, do not
proceed). Filtered `FullyQualifiedName~SettingsFingerprintTests|FullyQualifiedName~UnregisteredLocalizationKey|FullyQualifiedName~LanguageFileCoverageTests|FullyQualifiedName~LanguageDataXmlTests|FullyQualifiedName~TAOM.Tests.Composition|FullyQualifiedName~HarmonyPatchBindingTests|FullyQualifiedName~CoopVetoClassificationTests`:
`Failed: 0`.

### Step A3: docs

1. Create `docs/features/cheat-screen-guard.md` with exactly the content between the `~~~~` fences
   (the fences are not part of the file; replace `<today>` and the issue placeholder):

~~~~markdown
# Cheat-screen guard

## Overview

With vanilla cheat mode on, opening your own inventory lists every item in the game and the party screen lists every
troop. With TAOM's content that is about 5,166 items, and the list costs gigabytes of memory per open. The guard opens
both screens as they open without cheat mode, unless MCM "Cheat Mode Lists All Items And Troops" (TAOM Battle Load
Diagnostics page, group "Map Performance", default off, no restart) asks for the cheat lists. TAOM's own work, 1.4.x line
only (MEMORY-01 of the 2026-11-04 port audit).

## Why This Exists

- **Vanilla behavior:** `InventoryScreenHelper.OpenInventoryPresentation` (v1.4.8, taom-src `:174-196`) adds every
  `ItemObject` to the left roster, ten of each, when `Game.CheatMode` is on; `PartyScreenHelper.OpenScreenAsNormal`
  (`:185-195`) opens `OpenScreenAsCheat`, every troop. `Game.CheatMode` is `NativeConfig.CheatMode`, the `cheat_mode`
  line of `engine_config.txt`. Loot, trade, stash and warehouse screens never read it.
- **TAOM requirement:** every `taom.*` console command needs cheat mode (`DevConsoleGuard.CheckCheatMode`), including
  the rescue commands, so TAOM steers players into it. On a 16 GB machine one inventory open in a 2026-11-01 player log
  (cheat mode inferred) grew the managed heap from 489 to 2,841 MB, reached 96% memory load and silenced the main thread
  for 31 s.
- **Without this feature:** every I or P key press in cheat mode pays that cost again.

## Architecture

### Design Challenge

The cheat read is inside a private engine method that also builds the screen's logic with private helpers, so a prefix
would have to copy them. A transpiler can change the one read and nothing else.

### Solution Approach

Two transpilers (`Patch_CheatScreenGuard`, applied at `ApplyPhase.GameInit` by `CheatScreenGuardModule`) reuse the
soft-failing call-site swap `TickProfilerTranspiler.Rewrite`: the single `Game.get_CheatMode` read in each opener
becomes `CheatScreenGuardCalls.InventoryCheatMode(Game)` or `PartyCheatMode(Game)`. Each reads the real flag and,
only while it is on, asks `CheatScreenGuardService.ShowCheatList`, which returns the MCM setting. Any other site count
leaves the method vanilla; with no service, or when the service throws, the answer is the real flag. The first hidden
list per launch writes one log line and one chat line.

### Component Diagram

```
CheatScreenGuardModule (GameInit: Patch_CheatScreenGuard, then the install line)
        |
two transpilers -> CheatScreenGuardCalls (bool (Game) helpers, real flag first)
        |
ICheatScreenGuardService (CheatScreenGuardService) --- ICheatScreenGuardSettingsProvider, IInquiryAdapter, IModLogger
```

## Configuration

MCM, TAOM Battle Load Diagnostics page, group "Map Performance": `Cheat Mode Lists All Items And Troops`, default off,
`RequireRestart = false`, read at each screen open through a cached settings reference (off while MCM has no instance).
Player-local in `CoopSettingsRelevance`.

## Key Files

| File | Purpose |
|------|---------|
| `Main/Features/CheatScreenGuard/CheatScreenGuardModule.cs` | Registrations, the category at GameInit, the install line |
| `Main/Features/CheatScreenGuard/Hooks/*_CheatScreenGuard_Patch.cs` | The two transpilers |
| `Main/Features/CheatScreenGuard/Hooks/CheatScreenGuardCalls.cs`, `CheatScreenGuardSwaps.cs` | The swapped-in helpers and the swap table |
| `Main/Features/CheatScreenGuard/CheatScreenGuardService.cs` | The decision, the once-per-launch line and notice |
| `Main/Features/BattleLoadDiagnostics/BattleLoadDiagnosticsSettings.cs` | The MCM setting |

## Logging

All lines carry `[CheatScreenGuard]`: one install line at game init (`ON: ...`, or a warning naming each gate that is
off), and one line the first time this launch a cheat list is hidden.

## Tests

`CheatScreenGuardCallsTests` (5), `CheatScreenGuardServiceTests` (8), `CheatScreenGuardSettingsProviderTests` (2),
`CheatScreenGuardLinesTests` (3), `CheatScreenGuardModuleTests` (6) and `CheatScreenGuardBindingTests` (8: the targets
and the one cheat read each in the installed IL, the swap applied to the real IL and through real Harmony, the setting).

## How to check it in game

Set `cheat_mode = 1` in `engine_config.txt`, load a campaign, press I: your own items only, no Discard list of
thousands; the `[MemStation]` enter and exit lines of `GauntletInventoryScreen` differ by well under 100 MB heap; one
`[CheatScreenGuard] cheat mode is on` line and one chat line. Press P: the normal party screen. Turn the setting on and
press I: the vanilla list (and its memory cost) returns.

## Changelog

- <today>: added on the 1.4.x line by plan 120.

## GitHub Issue

- **Issue:** <the number the orchestrator gave you>.
- **Status:** built and unit-tested; not yet checked in game.
~~~~

2. `docs/reference/harmony-patch-registry.md`: directly above `## Late_ActionSetOverride` insert:

```markdown
## Patch_CheatScreenGuard

**Targets:** `private static void Helpers.InventoryScreenHelper.OpenInventoryPresentation(TextObject leftRosterName, Action doneLogicExtrasDelegate = null)` (v1.4.8, taom-src `InventoryScreenHelper.cs:174-196`) and `public static void Helpers.PartyScreenHelper.OpenScreenAsNormal()` (`PartyScreenHelper.cs:185-195`). Two classes in `Main/Features/CheatScreenGuard/Hooks/`, category `Patch_CheatScreenGuard` (no number: a 1.4.x-only feature), declared by `CheatScreenGuardModule` and applied at `ApplyPhase.GameInit`: one transpiler and one void `[HarmonyCleanup]` each.

Cheat-screen guard (1.4.x only, MEMORY-01). With `Game.CheatMode` on (`Game.cs:67`, through `MBGameManager` to `NativeConfig.CheatMode`), the inventory opener adds every `ItemObject` (`:177-187`) and the party opener routes to `OpenScreenAsCheat`. Each reads `Game.get_CheatMode` exactly once in the installed IL. The transpiler (`TickProfilerTranspiler.Rewrite`, soft-failing) swaps that read for `CheatScreenGuardCalls.InventoryCheatMode(Game)` or `PartyCheatMode(Game)`, which return the real flag when it is off and otherwise the MCM setting "Cheat Mode Lists All Items And Troops" (default off). Any other site count leaves the method vanilla and the install line says which gate is off. Callers: the map buttons, the navigation bar and the mission I and P keys (v1.4.8 dump `SandBox__SandBox.View.cs:5104`, `:5126`, `:16025`, `:16036`, `:22497`, `:22504`).

**Co-op.** Transpilers only, no bool prefix, so `CoopVetoClassificationTests` needs no entry; the setting is player-local in `CoopSettingsRelevance`.

Threads: main thread (screen opens). Tests: `CheatScreenGuardBindingTests`, `CheatScreenGuardCallsTests`, `CheatScreenGuardServiceTests`, `CheatScreenGuardModuleTests`. Feature: [cheat-screen-guard.md](../features/cheat-screen-guard.md). **ACTIVE**, not yet checked in game.
```

3. `docs/features/dev-console.md`: after the paragraph that starts `Cheat mode is a per-install flag in`
   and ends `irreversible mutation of persisted state.`, add a blank line and this paragraph:

```markdown
Cheat mode also changes two vanilla screens: the player's own inventory lists every item in the game and the party screen every troop, which with TAOM's item count costs gigabytes of memory per open. Since plan 120 the cheat-screen guard opens both normally unless MCM's Cheat Mode Lists All Items And Troops is on ([cheat-screen-guard.md](cheat-screen-guard.md)). A player who turns cheat mode on only for a `taom.*` command should turn it off afterwards.
```
4. `docs/features/battle-load-diagnostics.md`, table under `## Configuration` (header
   `| Setting | Default | Effect |`): after its last row add
   ``| `CheatModeListsAllItemsAndTroops` | `false` | Map Performance. With cheat mode on, show vanilla's every-item inventory and every-troop party lists (gigabytes of memory); off keeps the normal screens ([cheat-screen-guard.md](cheat-screen-guard.md)). Live. |``
5. `docs/features/battle-load-diagnostics.md`: directly above the heading
   `### What `taom.print_memory` reports since 2026-09-12` insert this paragraph and a blank line
   (`af990ca76`'s correction, re-cited to v1.4.8; its 1.5.x trace file does not exist on this line):

```markdown
**Correction (v1.4.8 code).** The `[MemStation] enter` line is written from `ScreenManager.OnPushScreen`, which `GameStateManager.OnPushState` (`GameStateManager.cs:257`) reaches through its listeners (`:277-279`) *before* its own `Common.MemoryCleanupGC()` (`:285`). So the 2,64x MB re-entry readings followed one collection, the pop's (`:313`), not two. The size comes from cheat mode: vanilla's `InventoryScreenHelper.OpenInventoryPresentation` (`:177-187`) puts every item in the game, ten of each, into the inventory's left list (about 5,166 rows with the Armory), and the party screen lists every troop; since plan 120 both lists stay out unless MCM asks for them ([cheat-screen-guard.md](cheat-screen-guard.md)). A 1.5.x reading, not re-traced on v1.4.8: that collection runs inside the closing tick while the closing screen and its layer are still on the stack, and those keep their movie and widget tree after release, so the graph is held only until the next collection; no type on that graph has a finalizer that would delay it; each row costs about 0.5 MB; a release one frame after the pop, not in the same tick, is what would return the memory at close.
```

   **Verify**: `grep -c "GameStateManager.cs:257" docs/features/battle-load-diagnostics.md` gives 1 and
   `grep -n "Correction (v1.4.8 code)" docs/features/battle-load-diagnostics.md` prints one line, above the
   `What `taom.print_memory` reports` heading.

### Step A4: snapshot and commit

1. Snapshot: with SNAP0 = 0 run `pwsh tools/snapshot_api_surface.ps1`, then
   `git -C $WT diff --stat docs/reference/taleworlds-api-snapshot` must list only `patch-targets.md`,
   and its diff must add exactly the two rows below and raise `Patches: N.` by 2. With SNAP0 other
   than 0, hand-edit `patch-targets.md` the same way, keeping the table sorted by the first column:
   ``| `TAOM.Features.CheatScreenGuard.Hooks.InventoryScreenHelper_OpenInventoryPresentation_CheatScreenGuard_Patch` | `Helpers.InventoryScreenHelper.Void OpenInventoryPresentation(TextObject leftRosterName, Action doneLogicExtrasDelegate)` |``
   ``| `TAOM.Features.CheatScreenGuard.Hooks.PartyScreenHelper_OpenScreenAsNormal_CheatScreenGuard_Patch` | `Helpers.PartyScreenHelper.Void OpenScreenAsNormal()` |``
   (if the regenerated rows differ in wording from these, keep the regenerated ones and say so in the hand-off).
2. **Verify A4**: Data command (strings changed): no new ERROR. Docs command: no counter above Step 0.6.
   Full tests: T0 + 32, `Failed: 0`, Skipped = T0's. Binding gate `Failed: 0`. Snapshot check exit 0
   (only when SNAP0 = 0).
3. Commit A. `git add` the new folders `Main/Features/CheatScreenGuard` and
   `TAOM.Tests/Features/CheatScreenGuard`, `docs/features/cheat-screen-guard.md`, and the edited
   `FeatureModules.cs`, `BattleLoadDiagnosticsSettings.cs`, `CoopSettingsRelevance.cs`,
   `SettingsFingerprintTests.cs`, `taom_module_strings.xml`, the 12 `Main/_Module/ModuleData/Languages/*/std_taom_module_strings_*.xml`, `coop-interop.md`,
   `bannerlord-together-compat.md`, `dev-console.md`, `battle-load-diagnostics.md`,
   `harmony-patch-registry.md`, `patch-targets.md`. `git status --porcelain` shows nothing else.
   Message `$AUX/120-msg-A.txt`:

```
perf(cheat-screens): v2.0.28 - cheat mode no longer lists every item

With vanilla cheat mode on, opening your own inventory listed every
item in the game (5,166 with TAOM's content) and the party screen
listed every troop. A 2026-11-01 player log shows one such open
(cheat mode inferred) growing the managed heap from 489 to 2,841 MB
and silencing the game for 31 s on a 16 GB machine. TAOM's taom.*
console commands need cheat mode, so a player who turns it on for a
rescue command paid this at every open. Both screens now open as
they do without cheat mode unless MCM, TAOM Battle Load Diagnostics,
Map Performance, "Cheat Mode Lists All Items And Troops" is on
(default off). One chat line per launch says so. Loot, trade and
stash screens are unchanged. The [MemStation] note in the feature doc
is corrected too: a re-entry reading follows one collection, not two.

Research: taom-src v1.4.8 InventoryScreenHelper.cs:174-196 and
PartyScreenHelper.cs:185-195, one Game.get_CheatMode read each in
the installed IL; Game.CheatMode reads NativeConfig.CheatMode
Not-tested: both screens in game with cheat_mode = 1
Save-compat: no saved state; one new MCM key, default off
Ported-from: af990ca769da52f408b8e804d127c446b515a379
```

**Part A done**: commit A exists; Full tests T0 + 32 with `Failed: 0`.

## Part B: one-time texture advice (MEMORY-04; new work)

Design (decided): at the first main menu (`ApplyPhase.MainMenu`, once per process), read installed
memory and Texture Quality once. When memory is at most 17,408 MB (17 GiB: every machine sold as
16 GB or less; the 2026-11-01 player reports 16,321 MB) and Texture Quality is at its highest level,
show one popup that suggests one step lower, then Foliage Quality, with no number. A marker file in
the game's `Logs` folder (the folder `BattleLoadStallMarker` and `FileLogger` use) means it never shows
again on that install. Every launch writes one `[TextureAdvice]` line with the memory size and the
setting, so future player logs carry both. No MCM setting: the popup is dismissed by its one button.
Not reused: `IGraphicsOptionsAdapter` (WP111 creates it without a texture member; WP117 adds one, same
wave, not a dependency).

### Step B1 (RED): the tests

Create under `TAOM.Tests/Features/TextureAdvice/` (namespace `TAOM.Tests.Features.TextureAdvice`):

1. `TextureAdviceRuleTests.cs` (9, no game), over `TextureAdviceRule.ShouldAdvise(totalPhysicalMb, textureQuality, topTextureQuality, shownBefore)`:
   true for `(16321, 2, 2, false)`; true at exactly `(17408, 2, 2, false)`; false at `(17409, 2, 2, false)`;
   false at `(32768, 2, 2, false)`; false for Medium `(16321, 1, 2, false)`; false when shown before;
   false for unreadable memory `(0, 2, 2, false)`; false for an unknown top `(16321, 2, -1, false)`; and
   `RoundedGigabytes(16321) == 16`.
2. `TextureAdviceServiceTests.cs` (6, no game; NSubstitute `ITextureAdviceAdapter` and `IModLogger`):
   advice due shows `ShowAdvice(16)` once and then `RememberAdviceShown()` once; no advice (memory 32,768)
   shows and remembers nothing but writes one decision line containing `no advice`; a second
   `AdviseOnce()` in the same launch reads nothing (adapter received each member once); an adapter whose
   `TextureQuality` throws writes one warning and does not throw; a `RememberAdviceShown` that throws
   writes one warning naming the next launch and the popup was still shown; a logger that throws
   escapes nothing.
3. `TextureAdviceAdapterTests.cs` (2, no game; the internal ctor with a path under
   `Path.Combine(Path.GetTempPath(), "taom-tests-" + Guid.NewGuid().ToString("N"), "sub", "marker")`,
   deleted in cleanup): `WasAdviceShown_NoMarker_IsFalse`; `RememberAdviceShown_CreatesTheFolderAndTheMarker`
   (then `WasAdviceShown` is true).
4. `TextureAdviceModuleTests.cs` (5, no game): the service resolves as a singleton from a real container
   (with a substitute `IModLogger`); `OnPhase(MainMenu)` with a substitute service registered over the
   real one calls `AdviseOnce()` once; `ProcessLoad`, `GameInit` and `FirstMission` call nothing; the
   module is listed once in `FeatureModules.All` with Id `TextureAdvice`, `ParkedReason` null and no
   patch categories; `RegisterServices` registers the real adapter (`IsRegistered<ITextureAdviceAdapter>()`
   on a fresh container).
5. `TextureAdviceBindingTests.cs` (3; `RequiresGame` class, `BindingVerification` methods; no engine
   method is called): `NativeOptions.GetConfig(NativeOptionsType)` is public static and returns float;
   `NativeOptions.NativeOptionsType.TextureQuality` is defined; `NativeSelectionOptionData.GetOptionsLimit(NativeOptionsType)`
   is public static and returns int.

Build: expected FAILS naming `TAOM.Features.TextureAdvice` types.

### Step B2 (GREEN): the code

`Main/Features/TextureAdvice/TextureAdviceRule.cs`:

```csharp
using System;

namespace TAOM.Features.TextureAdvice;

/// <summary>When the one-time memory advice shows (docs/features/texture-advice.md). Pure.</summary>
internal static class TextureAdviceRule
{
    /// <summary>17 GiB: a machine sold as 16 GB reports a little under 16,384 MB, so this takes every 16 GB machine and smaller and leaves out 24 GB and more.</summary>
    internal const long AdviseAtOrBelowMb = 17408;

    internal static bool ShouldAdvise(long totalPhysicalMb, int textureQuality, int topTextureQuality, bool shownBefore) =>
        !shownBefore
        && totalPhysicalMb > 0 && totalPhysicalMb <= AdviseAtOrBelowMb
        && topTextureQuality > 0 && textureQuality >= topTextureQuality;

    internal static int RoundedGigabytes(long totalPhysicalMb) =>
        (int)Math.Round(totalPhysicalMb / 1024d, MidpointRounding.AwayFromZero);
}
```

`Main/Features/TextureAdvice/TextureAdviceLines.cs` (internal static, `Tag = "[TextureAdvice]"`):
`BuildDecisionLine(long totalPhysMb, int quality, int top, bool shownBefore, bool advise)` returning
`[TextureAdvice] totalPhysMB=<n> textureQuality=<q>/<top> shownBefore=<true|false>: <advice shown (lower Texture Quality one step)|no advice>`
(invariant culture); `BuildFailedLine(Exception ex)` returning `[TextureAdvice] no advice: <Type>: <message>`;
`BuildNotRememberedLine(Exception ex)` returning
`[TextureAdvice] the advice was shown but could not be recorded (<Type>: <message>); it shows again at the next launch`.

`Main/Features/TextureAdvice/ITextureAdviceService.cs` (`void AdviseOnce();`, "decides once per launch,
writes one log line, shows the advice when the rule says so; never throws") and
`TextureAdviceService.cs` (`public sealed class`, ctor `(ITextureAdviceAdapter adapter, IModLogger logger)`,
a `bool _ran` latch): in one try read `TotalPhysicalMemoryMb`, `TextureQuality`, `TopTextureQuality`
and `WasAdviceShown()`; decide with the rule; log the decision line (`LogInfo`, own try); when due call
`ShowAdvice(TextureAdviceRule.RoundedGigabytes(ram))`. A throw anywhere in that try logs
`BuildFailedLine` as a warning and returns. After a shown popup, call `RememberAdviceShown()` in its own
try; a throw there logs `BuildNotRememberedLine` as a warning. Every log call sits in its own try with
an empty catch.

`Main/Adapters/ITextureAdviceAdapter.cs`:

```csharp
namespace TAOM.Adapters;

/// <summary>The engine and file side of the one-time texture advice (docs/features/texture-advice.md). Main thread.</summary>
public interface ITextureAdviceAdapter
{
    /// <summary>Texture Quality as the engine stores it (0 Low, 1 Medium, 2 High on v1.4.8); -1 when unreadable.</summary>
    int TextureQuality { get; }

    /// <summary>The highest Texture Quality level this game build offers (2 on v1.4.8).</summary>
    int TopTextureQuality { get; }

    /// <summary>Installed physical memory in MB (GlobalMemoryStatusEx); 0 when unreadable.</summary>
    long TotalPhysicalMemoryMb { get; }

    bool WasAdviceShown();

    void RememberAdviceShown();

    /// <summary>One popup with one button; <paramref name="totalGigabytes"/> fills the text's {RAM}.</summary>
    void ShowAdvice(int totalGigabytes);
}
```

`Main/Adapters/TextureAdviceAdapter.cs`:

```csharp
using System;
using System.Globalization;
using System.IO;
using TaleWorlds.Engine.Options;
using TaleWorlds.Library;
using TaleWorlds.Localization;
using TAOM.Core.Validation;
using TAOM.Features.BattleLoadDiagnostics;

namespace TAOM.Adapters;

/// <summary>
/// Engine side of the texture advice: the Texture Quality option (NativeOptions.GetConfig, v1.4.8 :274; three levels per
/// NativeSelectionOptionData.GetOptionsLimit :39), installed memory through TAOM's own GlobalMemoryStatusEx reader, a
/// marker file beside the TAOM log ("Logs", as BattleLoadStallMarker), and the popup (StallReportNotifier's shape).
/// </summary>
public sealed class TextureAdviceAdapter : ITextureAdviceAdapter
{
    private const string MarkerFileName = "taom-texture-advice.shown";

    // A sanity bound that rejects garbage from the engine store, not the length of the options list.
    private const float MaxRawOption = 16f;

    private readonly string _markerPath;

    public TextureAdviceAdapter() : this(Path.Combine("Logs", MarkerFileName)) { }

    // Test seam; internal so DryIoc sees one public ctor.
    internal TextureAdviceAdapter(string markerPath) => _markerPath = markerPath;

    public int TextureQuality
    {
        get
        {
            var raw = NativeOptions.GetConfig(NativeOptions.NativeOptionsType.TextureQuality);
            return FiniteFloatValidator.IsFiniteInRange(raw, 0f, MaxRawOption) ? (int)Math.Round(raw) : -1;
        }
    }

    public int TopTextureQuality => NativeSelectionOptionData.GetOptionsLimit(NativeOptions.NativeOptionsType.TextureQuality) - 1;

    public long TotalPhysicalMemoryMb => MemorySampleReader.TryRead(out var sample) ? sample.TotalPhysMb : 0;

    public bool WasAdviceShown() => File.Exists(_markerPath);

    public void RememberAdviceShown()
    {
        var dir = Path.GetDirectoryName(_markerPath);
        if (!string.IsNullOrEmpty(dir)) Directory.CreateDirectory(dir);
        File.WriteAllText(_markerPath, DateTime.UtcNow.ToString("o", CultureInfo.InvariantCulture));
    }

    public void ShowAdvice(int totalGigabytes)
    {
        var body = new TextObject("{=taom_texture_advice_body}This computer has about {RAM} GB of memory and Texture Quality is at its highest setting. TAOM's campaign map and items fill a lot of that memory, and on a machine like this the game can stutter or run out of memory. Lowering Texture Quality by one step (Options, Performance, Graphics) is the largest memory saving the game offers, and Foliage Quality is the next. Both take effect at once, with no restart. This message will not show again.");
        body.SetTextVariable("RAM", totalGigabytes.ToString(CultureInfo.InvariantCulture));
        InformationManager.ShowInquiry(new InquiryData(
            titleText: new TextObject("{=taom_texture_advice_title}TAOM: memory advice").ToString(),
            text: body.ToString(),
            isAffirmativeOptionShown: true,
            isNegativeOptionShown: false,
            affirmativeText: new TextObject("{=taom_texture_advice_ok}Understood").ToString(),
            negativeText: string.Empty,
            affirmativeAction: () => { },
            negativeAction: () => { }),
            pauseGameActiveState: false);
    }
}
```

`Main/Features/TextureAdvice/TextureAdviceModule.cs`: `internal sealed class TextureAdviceModule : TaomFeatureModule`
with `Id => "TextureAdvice"`, `RegisterServices` registering `ITextureAdviceAdapter`/`TextureAdviceAdapter`
and `ITextureAdviceService`/`TextureAdviceService` as `Reuse.Singleton`, no categories, and
`OnPhase(phase, resolver)` calling `resolver.Resolve<ITextureAdviceService>().AdviseOnce()` when
`phase == ApplyPhase.MainMenu`. Summary comment: what the advice is, that `MainMenu` runs once per
process at the first main menu (where `StallReportNotifier` already shows a popup), and that no engine
type is touched before that step.

Then:
1. `FeatureModules.cs`: append `        new Features.TextureAdvice.TextureAdviceModule(),` as the LAST entry.
2. `taom_module_strings.xml`, after the Part A row (above `</strings>`):

```xml
	<!-- TextureAdviceAdapter -->
	<string id="taom_texture_advice_title" text="{=taom_texture_advice_title}TAOM: memory advice" />
	<string id="taom_texture_advice_body" text="{=taom_texture_advice_body}This computer has about {RAM} GB of memory and Texture Quality is at its highest setting. TAOM's campaign map and items fill a lot of that memory, and on a machine like this the game can stutter or run out of memory. Lowering Texture Quality by one step (Options, Performance, Graphics) is the largest memory saving the game offers, and Foliage Quality is the next. Both take effect at once, with no restart. This message will not show again." />
	<string id="taom_texture_advice_ok" text="{=taom_texture_advice_ok}Understood" />
```

   Then run the Language rows helper with
   `ROWS = [("taom_texture_advice_title", "TAOM: memory advice"), ("taom_texture_advice_body", "This computer has about {RAM} GB of memory and Texture Quality is at its highest setting. TAOM's campaign map and items fill a lot of that memory, and on a machine like this the game can stutter or run out of memory. Lowering Texture Quality by one step (Options, Performance, Graphics) is the largest memory saving the game offers, and Foliage Quality is the next. Both take effect at once, with no restart. This message will not show again."), ("taom_texture_advice_ok", "Understood")]`.

**Verify B2**: Build `0 Error(s)`. Filtered `FullyQualifiedName~TAOM.Tests.Features.TextureAdvice`:
`Failed: 0`, `Total: 25`, `Skipped: 0`. Filtered
`FullyQualifiedName~UnregisteredLocalizationKey|FullyQualifiedName~LanguageFileCoverageTests|FullyQualifiedName~LanguageDataXmlTests|FullyQualifiedName~TAOM.Tests.Composition`: `Failed: 0`.

### Step B3: docs and commit

1. Create `docs/features/texture-advice.md` from `docs/features/TEMPLATE.md` with these facts and no
   others: Overview (one popup at the first main menu on machines with about 16 GB or less at the
   highest Texture Quality; never again on that install; one `[TextureAdvice]` line every launch);
   Why This Exists (vanilla: no advice; Texture Quality is the largest lever, highest to lowest freed
   2.3 GB on the map in the maintainer's 2026-09-12 run, live, before the 2026-09-13 map downsizing;
   foliage 0.77 GB; one step down never measured, so the popup carries no number); Solution Approach
   (`TextureAdviceModule` at `ApplyPhase.MainMenu`, the pure `TextureAdviceRule` with the 17,408 MB
   threshold, the adapter's four reads and the popup, the marker `Logs/taom-texture-advice.shown`
   relative to the game's working directory, the order show then remember); Configuration (none; delete
   the marker to see the popup again); Key Files; Logging (the three lines); Tests (the five classes and
   counts); How to check it in game (Part B of "In-game checks owed" below); Changelog
   (`- <today>: added on the 1.4.x line by plan 120.`); GitHub Issue (the number you were given, or the
   placeholder). No long dashes.
2. `docs/features/battle-load-diagnostics.md`, section `## Related`: add a bullet
   `- [texture-advice.md](texture-advice.md): the one-time memory advice at the first main menu; it reads the same GlobalMemoryStatusEx total as the [MemSample] session line.`
3. **Verify B3**: Data command: no new ERROR. Docs command: no counter above Step 0.6. Full tests:
   previous total + 25, `Failed: 0`, Skipped = T0's.
4. Commit B: `git add Main/Features/TextureAdvice Main/Adapters/ITextureAdviceAdapter.cs Main/Adapters/TextureAdviceAdapter.cs TAOM.Tests/Features/TextureAdvice docs/features/texture-advice.md Main/Composition/FeatureModules.cs Main/_Module/ModuleData/taom_module_strings.xml Main/_Module/ModuleData/Languages/*/std_taom_module_strings_*.xml docs/features/battle-load-diagnostics.md`;
   `git status --porcelain` shows nothing else. Message `$AUX/120-msg-B.txt`:

```
feat(texture-advice): v2.0.28 - advise 16 GB players on texture quality

Texture Quality is the largest memory lever a player has: on TAOM's
campaign map, highest to lowest freed 2.3 GB in a 2026-09-12
measurement on the maintainer's desktop, live, with no restart, and
nothing told players. At the first main menu TAOM now reads the
installed memory and the Texture Quality option once. On a machine
with about 16 GB or less at the highest setting it shows one popup
that suggests one step lower, then Foliage Quality. It never shows
again on that install. Every launch writes one [TextureAdvice] line
with the memory size and the setting, so a player log says both.

Research: taom-src v1.4.8 NativeOptions.GetConfig (:274),
NativeSelectionOptionData.GetOptionsLimit (TextureQuality has 3
levels, :39; level 2 is High in Native global_strings.xml),
InquiryData, InformationManager.ShowInquiry
Not-tested: the popup in game on a 16 GB machine
Save-compat: no saved state; a marker file in the game's Logs folder
```

**Part B done**: commit B exists; Full tests T0 + 57 with `Failed: 0`.

## Part C: settlement nameplate cull (Patch104; port of `ecaa67d26`, RUNTIME-PERF-11 part B)

Source `ecaa67d26` (feat(stability): v2.0.34 - mission start, freeze and map guards), its NameplateCull
part only; WP109 took the mission-start and skeleton-buffer parts. Method: path-limited checkout plus
hand edits. Simulation re-run this session
(`git merge-tree --write-tree --name-only --no-messages --merge-base=ecaa67d26^ origin/bannerlord-1.4.5 ecaa67d26`):
exit 1, 38 conflicted paths; those this Part resolves by hand: `FeatureModules.cs` (absent on origin;
WP105 adds it), `BattleLoadDiagnosticsSettings.cs`, `CoopSettingsRelevance.cs`, `THIRD-PARTY-LICENSES.txt`,
`SettingsFingerprintTests.cs`, `CampaignHotPathSettingsProvidersTests.cs` (absent: replaced by a new
file), `ReflectionSiteBindingTests.cs`, `coop-interop.md`, `bannerlord-together-compat.md`,
`harmony-patch-registry.md`, `provenance-register.md`, `patch-targets.md`, `reflection-sites.md`,
`map-perf-profiler.md` (conditional). `CoopVetoClassificationTests.cs` merges cleanly in the
simulation but is added by hand here. Later 1.5.x edits to these folders: only `6d7e67f3f`
(`[ClientOnly]` on the patch class; skipped, D7) and doc commits (Disposition).

### Step C1 (RED): the tests, re-pinned to v1.4.8

1. `git -C $WT checkout ecaa67d26 TAOM.Tests/Features/NameplateCull`
2. Edit `TAOM.Tests/Features/NameplateCull/NameplateCullBindingTests.cs`:
   - Class summary: `(v1.5.4 at the time of writing)` becomes `(v1.4.8 at the time of the 1.4.x port)`.
   - In `PublicMembers_TheCullCallsOrReads_ExistWithTheirTypes`, replace
     `foreach (var name in new[] { "IsTown", "IsFortification", "IsHideout", "IsVisible", "IsInspected" })`
     with the same line without `"IsVisible", `, and directly after that loop's `AssertProperty` line add:

```csharp
        // v1.4.8 (decision D22): a hideout's range is its component's ISpottable.IsSpotted (SettlementNameplateVM.cs:500-509),
        // a field of its own (Hideout.cs:49-59), not Settlement.IsVisible.
        AssertProperty(typeof(Settlement), "SettlementComponent", typeof(SettlementComponent));
        AssertProperty(typeof(ISpottable), "IsSpotted", typeof(bool));
        Assert.IsTrue(typeof(ISpottable).IsAssignableFrom(typeof(Hideout)), "a hideout's component is spottable");
```

   - In `VanillaDynamicProperties_CallExactlyTheSettlementFactsTheRuleMirrors`, replace the element
     `"Settlement.get_IsVisible"` with `"Settlement.get_SettlementComponent", "ISpottable.get_IsSpotted"`,
     and delete the last two elements `"SettlementNameplateVM.get_Settlement", "Settlement.get_FerryTarget",`
     (the list must end with `"Building.get_CurrentLevel", "List`1.get_Count",`; 44 elements, read this
     session from the installed IL).
   - In `RefreshBindValues_PushesTheBoundValuesAndTicksNotificationsAndEventsOnlyWhenTheirEventsAreRegistered`,
     delete the element `"SettlementNameplateVM.set_HasFerry", ` (23 elements remain).
   - Add this test after `IsInRangeSetter_IsTheOnlyPlaceAPlateRegistersItsEvents_InTheNameplateAssembly`:

```csharp
    [TestMethod]
    [TestCategory("BindingVerification")]
    [TestCategory("RequiresGameIL")]
    public void Adapter_ReadsAHideoutsRangeFromItsSpottableComponent_AsTheV148PlateUpdateDoes()
    {
        RequireGame();

        // D22: v1.4.8's RefreshDynamicProperties takes a hideout's range from ISpottable.IsSpotted, and
        // IncidentEffect sets IsSpotted while Settlement.IsVisible stays false, so the skip rule must read
        // the same member or it culls a plate vanilla would bring into range.
        var calls = Calls(typeof(NameplateCullAdapter).GetMethod("MaySkip", AllInstance)!);

        CollectionAssert.Contains(calls, "Settlement.get_SettlementComponent");
        CollectionAssert.Contains(calls, "ISpottable.get_IsSpotted");
        CollectionAssert.DoesNotContain(calls, "Settlement.get_IsVisible");
    }
```

3. Create `TAOM.Tests/Features/NameplateCull/NameplateCullSettingsProviderTests.cs` (the two tests of
   the `ecaa67d26` `CampaignHotPathSettingsProvidersTests.cs` hunk, which file 1.4.x lacks):

```csharp
using Microsoft.VisualStudio.TestTools.UnitTesting;
using TAOM.Features.BattleLoadDiagnostics;
using TAOM.Features.NameplateCull;

namespace TAOM.Tests.Features.NameplateCull;

/// <summary>
/// The cull's toggle is read every campaign-map frame. On the 1.5.x line these pins live in
/// CampaignHotPathSettingsProvidersTests (plan 037), which 1.4.x does not have yet (WP123 Step C2 moves them there).
/// </summary>
[TestClass]
public class NameplateCullSettingsProviderTests
{
    [TestMethod]
    public void NameplateCull_ReadsThroughTheCachedSettings()
    {
        var settings = new BattleLoadDiagnosticsSettings();
        var sut = new NameplateCullSettingsProvider(settings);
        Assert.IsTrue(sut.CullEnabled);

        settings.CullHiddenNameplates = false;

        Assert.IsFalse(sut.CullEnabled);
    }

    // No-MCM pin: BattleLoadDiagnosticsSettings.Instance is null in the test host (MCM is never initialised).
    [TestMethod]
    public void NameplateCull_NoMcm_CullDefaultsOn()
        => Assert.IsTrue(new NameplateCullSettingsProvider().CullEnabled);
}
```

Build: expected FAILS naming `TAOM.Features.NameplateCull` types.

### Step C2 (GREEN for all but the D22 test)

1. `git -C $WT checkout ecaa67d26 Main/Features/NameplateCull Main/Adapters/INameplateCullAdapter.cs Main/Adapters/NameplateCullAdapter.cs`
2. `FeatureModules.cs`: append `        new Features.NameplateCull.NameplateCullModule(),` as the LAST entry.
3. `BattleLoadDiagnosticsSettings.cs`: directly ABOVE the `[SettingPropertyGroup("Map Performance")]`
   line that precedes `[SettingPropertyBool("Cheat Mode Lists All Items And Troops"` (Part A's property;
   so Orders read 1 to 4 top to bottom), add:

```csharp
    [SettingPropertyGroup("Map Performance")]
    [SettingPropertyBool("Cull Hidden Settlement Nameplates", Order = 1, RequireRestart = false,
        HintText = "The campaign map updates every settlement nameplate every frame (about a thousand in TAOM), hidden ones included. When on, a nameplate that is hidden and cannot become visible this frame (out of height range, not tracked, not in range, nothing about its settlement changed) is left alone, which saves frame time on the map, most in fast forward. Everything else updates exactly as in the vanilla game. Takes effect on the next frame. Default ON.")]
    public bool CullHiddenNameplates { get; set; } = true;

```

   (the 1.5.x hint minus its map-profiler sentence; the profiler is WP117's). If Part A has not landed in
   this branch, add it directly above the closing `}` of the class instead.
4. `CoopSettingsRelevance.cs`, `Presentation` set: directly above the comment line that starts
   `// Already suppressed under co-op`, add:

```csharp
        // Campaign map performance (Battle Load Diagnostics page, Map Performance): whether hidden settlement
        // nameplates are skipped each frame (Patch104). It changes what this client draws, never the campaign.
        "CullHiddenNameplates",
```

5. `CoopVetoClassificationTests.cs`: directly above the comment line that starts `// --- Parked` (it
   opens the Parked section of `Registry`), add the `ecaa67d26` entry verbatim:

```csharp
        ["SettlementNameplatesVM_Update_NameplateCull_Patch"] = new(CoopVeto.ReviewedSafe,
            "Patch104: client-side UI. The prefix replaces SettlementNameplatesVM.Update, the campaign map's per-frame " +
            "nameplate refresh, with the same update over the plates that are hidden-and-staying-hidden left out. The " +
            "view model feeds widgets on this client only: it reads settlement state, the camera and the visual " +
            "tracker, and writes nothing to campaign state, so there is no replicated mutation to skip and nothing for " +
            "two peers to disagree about. Every other outcome (toggle off, cull unavailable, any error) returns true " +
            "and runs vanilla whole. The toggle is presentation in CoopSettingsRelevance."),

```

   (on origin that comment line is `:270`; leave the blank line before it.)
6. `SettingsFingerprintTests.cs`: `BattleLoadDiagnosticsSettings` `reflected:` +1. The two co-op docs:
   total, split and excluded figure +1, as in Step A2 item 5.
7. `ReflectionSiteBindingTests.cs`: directly above `public void ReflectionSite_ResolvesAgainstInstalledEngine(`,
   after the last `[DataRow(...)]`, add:

```csharp
    // --- NameplateCull (Patch104): the three private fields the adapter binds with AccessTools.FieldRefAccess.
    // A miss makes Initialize return a reason, the cull stays off and the vanilla nameplate update runs (one WARNING line).
    [DataRow("SandBox.ViewModelCollection.Nameplate.SettlementNameplatesVM", "SettlementNameplatesVM", "_mapCamera", "Field", "NameplateCullAdapter.cs:49")]
    [DataRow("SandBox.ViewModelCollection.Nameplate.SettlementNameplateVM", "SettlementNameplateVM", "_bindIsVisibleOnMap", "Field", "NameplateCullAdapter.cs:50")]
    [DataRow("SandBox.ViewModelCollection.Nameplate.SettlementNameplateVM", "SettlementNameplateVM", "_worldPos", "Field", "NameplateCullAdapter.cs:51")]
```

   Confirm the three cites with `grep -n "FieldRefAccess" Main/Adapters/NameplateCullAdapter.cs` (lines
   49, 50, 51); write what it shows if they differ.

**Verify C2**: Build `0 Error(s)`. Filtered `FullyQualifiedName~TAOM.Tests.Features.NameplateCull`:
`Total: 85`, `Failed: 1`, `Skipped: 0`, and the one failure is
`Adapter_ReadsAHideoutsRangeFromItsSpottableComponent_AsTheV148PlateUpdateDoes` (the ported adapter
still reads `Settlement.get_IsVisible`). Any other failure, especially in the re-pinned call lists:
STOP and report the actual list the test printed (do not hand-edit it to match).

### Step C3 (GREEN): the v1.4.8 hideout range (D22) and comment re-cites

1. `Main/Adapters/NameplateCullAdapter.cs`, in `MaySkip`, replace
   `            SettlementInRange = settlement.IsHideout ? settlement.IsVisible : settlement.IsInspected,` with:

```csharp
            // v1.4.8 RefreshDynamicProperties (SettlementNameplateVM.cs:500-509) takes a hideout's range from its component's
            // ISpottable.IsSpotted, which can differ from Settlement.IsVisible (IncidentEffect sets one without the other).
            SettlementInRange = settlement.IsHideout
                ? settlement.SettlementComponent is ISpottable spottable && spottable.IsSpotted
                : settlement.IsInspected,
```

2. Comment re-cites (same line count each): `NameplateCullAdapter.cs` summary `(v1.5.4)` becomes
   `(v1.4.8)`; `// RefreshPosition parks a hidden plate at (-1000, -1000) in v1.5.4;` becomes `... in v1.4.8;`;
   `NameplateCullRule.cs` `SettlementNameplateVM.IsVisible (v1.5.4)` becomes `(v1.4.8)`;
   `Models/NameplateFacts.cs` summary of `SettlementInRange`: `<c>IsVisible</c> for a hideout` becomes
   `<c>ISpottable.IsSpotted</c> for a hideout`. `NameplateCullRuleTests.cs` comment `(v1.5.4)` becomes `(v1.4.8)`.
3. `grep -n "v1\.5" Main/Features/NameplateCull Main/Adapters/NameplateCullAdapter.cs -r`: no hit.

**Verify C3**: Build; Filtered `FullyQualifiedName~TAOM.Tests.Features.NameplateCull`: `Failed: 0`,
`Total: 85`, `Skipped: 0`. Filtered `FullyQualifiedName~ReflectionSiteBindingTests|FullyQualifiedName~CoopVetoClassificationTests|FullyQualifiedName~SettingsFingerprintTests|FullyQualifiedName~TAOM.Tests.Composition`:
`Failed: 0` (ReflectionSite total = its previous total + 3).

### Step C4: docs

1. `git -C $WT checkout ecaa67d26 docs/features/nameplate-cull.md`, then:
   - Overview: replace from `TAOM_Map defines 1,040` through `once it is visible)` with
     `The laptop's installed TAOM_Map (v2.0.27) defines 988 settlements (a hideout gets a plate only once it is visible)`.
   - Replace the link `[adopt-yotthani-2026-10-08.md](../reviews/adopt-yotthani-2026-10-08.md)` (twice) with
     `` `docs/reviews/adopt-yotthani-2026-10-08.md` on the 1.5.x line (not ported) ``.
   - "Vanilla behavior": `(v1.5.4)` becomes `(v1.4.8, taom-src `SettlementNameplatesVM.cs:238-248`)`; after
     `GauntletMapSettlementNameplateView.OnMapScreenUpdate` add ` (v1.4.8 dump `SandBox__SandBox.GauntletUI.cs:10297-10300`)`.
   - "TAOM requirement": end the bullet with ` That measurement is the 1.5.x line's; nothing was measured on v1.4.8.`
   - Condition table, row `settlement range false`: the Why cell becomes
     `` `ISpottable.IsSpotted` for a hideout, `IsInspected` otherwise, as v1.4.8's `RefreshDynamicProperties` computes it (`SettlementNameplateVM.cs:500-509`). The 1.5.x line read `Settlement.IsVisible`, a separate field on v1.4.8 that `IncidentEffect` leaves false while setting `IsSpotted` (decision D22) ``.
   - Section on the toggle (the paragraph that starts `The toggle is read every map frame`): replace its
     second clause, from `the provider is in the hot-path settings gate` through `lookup fails a test.` (it wraps
     over two lines), with `` `NameplateCullSettingsProviderTests` pins that it reads through that reference (1.4.x has no hot-path IL gate until WP123 brings one). ``
   - Tests section: replace the other `` `CampaignHotPathSettingsProvidersTests` `` with `` `NameplateCullSettingsProviderTests` ``.
   - After the paragraph about another mod's bool prefix, add: `On the 1.5.x line yotthani's VanillaTuning v0.1.1 switches its own `nameplate-cull` off when `TAOM.Features.NameplateCull.NameplateCullModule` is loaded (code read there, game 1.5.5); whether VanillaTuning runs on v1.4.8 is not known. Keep the module's full type name.`
   - "How to A/B it in game": replace steps 1 to 3 with: `1. If WP117's map profiler is in this build, turn "Enable Map Profiler" on (it needs a restart) and compare the `[MapProfile]` lines of the two stretches; otherwise use a frame-time overlay (for example Steam's FPS counter). 2. On one save and one camera position, fast-forward on the map for two minutes with "Cull Hidden Settlement Nameplates" on, then untick it (it applies on the next frame) and repeat. 3. After 18,000 culled frames a `[NameplateCull]` line says how many plate updates were skipped.` Replace step 4 with: `4. The default is on, as on the 1.5.x line, and both lines share the MCM key; to ship it off, rename the key (`docs/features/mcm.md`), never flip its default.`
   - Changelog: add `- <today>: ported to the 1.4.x line by plan 120 (v1.4.8 hideout range, re-pinned call lists, the settings-provider pins in their own class).`
     GitHub Issue: add `- **1.4.x port:** plans/120-map-perf-and-vram.md.`
   - `grep -n "v1\.5\|adopt-yotthani\](" docs/features/nameplate-cull.md`: every hit must be an
     attribution to the 1.5.x line.
2. `docs/reference/harmony-patch-registry.md`: directly above `## Patch_MissionTime_SetMovementOrder`
   insert (first confirm the PatchShield sentence against the live `Dependencies/Foundation/PatchShield.cs`
   and `PatchShieldPolicy.cs`; if either changed after WP107, write what they show):

```markdown
## Patch104_NameplateCull

**Target:** `public void SandBox.ViewModelCollection.Nameplate.SettlementNameplatesVM.Update()` (v1.4.8, taom-src `SettlementNameplatesVM.cs:238-248`; non-virtual, no parameters), one class `SettlementNameplatesVM_Update_NameplateCull_Patch` in `Main/Features/NameplateCull/Hooks/`, category `Patch104_NameplateCull`, declared by `NameplateCullModule` and applied at `ApplyPhase.GameInit` by the feature-module runner: one bool prefix (`SettlementNameplatesVM __instance`), no postfix, no finalizer.

Settlement nameplate cull (ported from the 1.5.x line, #777; adapted from yotthani's VanillaTuning `nameplate-cull`, MIT). Vanilla `Update` stores the map camera in `_cachedCameraPosition` (`:242`), runs `UpdateNameplateMT(camera)` for every plate in a `TWParallel.For` with grain 16 (`:243`), then `RefreshBindValues()` for every plate (`:244-247`); its caller is `GauntletMapSettlementNameplateView.OnMapScreenUpdate`, once per map frame (v1.4.8 dump `SandBox__SandBox.GauntletUI.cs:10297-10300`). The prefix hands the frame to `NameplateCullService.TryUpdate`; `NameplateCullAdapter.UpdateCulled` runs the same three steps over the plates `NameplateCullRule.MaySkip` does not clear, and the prefix returns false. Every other outcome (toggle off, cull unavailable, any error) returns true and vanilla runs whole. A plate is skipped only when it is hidden, already parked at (-1000, -1000) (`SettlementNameplateVM.cs:570`), untracked, out of range, not a tutorial target, provably invisible by the height-and-distance part of `IsVisible` (400, 200, distance below z + 100; `:772-793`), its settlement's range is false, its party is not `IsVisualDirty`, and `VisualTrackerManager.CheckTracked` does not hold it.

**v1.4.8 adaptation (decision D22).** `RefreshDynamicProperties` (`SettlementNameplateVM.cs:500-509`) takes a hideout's range from `SettlementComponent is ISpottable && IsSpotted`; on v1.4.8 that is a field of its own (`Hideout.cs:49-59`), separate from `Settlement.IsVisible` (`Settlement.cs:130-144`), and `IncidentEffect` sets one without the other (dump `IncidentEffect.cs:699-700`). The adapter reads the same member. The 1.5.x pins named `Settlement.get_FerryTarget` and `set_HasFerry`, absent on v1.4.8; `NameplateCullBindingTests` carries the v1.4.8 call lists.

**Event registration.** A plate registers its notification, party-marker and event listeners only in the `IsInRange` setter (`:332-358`), and `RefreshBindValues` ticks them only when registered (`:762-768`), so skipping an out-of-range plate skips no live tick.

**PatchShield and co-op.** `Update` is on no PatchShield exclusion list, so the shield's finalizer attaches from a process's second game start. TAOM's Harmony id `com.taom.mod` does not match the protected owner prefix `TAOM` (`PatchShieldPolicy.IsProtectedOwner` is a prefix match), so after a missing-API swallow on `Update` the shield strips this prefix and vanilla runs: the safe outcome. A bool prefix, so `CoopVetoClassificationTests` carries a ReviewedSafe entry (client-side UI, no campaign state); the toggle is presentation in `CoopSettingsRelevance`.

Threads: the prefix, `UpdateCulled` and `RefreshBindValues` on the main thread; `UpdateNameplateMT` on the `TWParallel` pool as in vanilla. Tests: `NameplateCullBindingTests`, `NameplateCullRuleTests`, `NameplateCullServiceTests`, `NameplateCullLinesTests`, `NameplateCullHookTests`, `NameplateCullPrefixOrderTests`, `NameplateCullModuleTests`, `NameplateCullSettingsProviderTests`. Feature: [nameplate-cull.md](../features/nameplate-cull.md). **ACTIVE**, not yet measured or checked in game on either line.
```

3. `docs/reference/taleworlds-api-snapshot/reflection-sites.md`, Category B table: after its last row add
   the three rows of `ecaa67d26` with the 1.4.x cites:
   ``| `SandBox.ViewModelCollection.Nameplate.SettlementNameplatesVM` | `_mapCamera` | field (private) | `NameplateCullAdapter.cs:49` | NameplateCull (Patch104). Read through `AccessTools.FieldRefAccess` to get the map camera the vanilla `Update` reads. Missing: `Initialize` returns a reason, the cull stays off and the vanilla nameplate update runs |``
   ``| `SandBox.ViewModelCollection.Nameplate.SettlementNameplateVM` | `_bindIsVisibleOnMap` | field (private) | `NameplateCullAdapter.cs:50` | NameplateCull. The bound visibility a plate must agree with before it may be skipped. Missing: as `_mapCamera` |``
   ``| `SandBox.ViewModelCollection.Nameplate.SettlementNameplateVM` | `_worldPos` | field (private) | `NameplateCullAdapter.cs:51` | NameplateCull. The plate's world position, for the distance half of the visibility test. Missing: as `_mapCamera` |``
   Then above the existing `Status (...)` lines add
   `Status (<today>): NameplateCull (Patch104) adds three private field rows; they resolve against installed v1.4.8 (ReflectionSiteBindingTests <N>/<N>).`
   with the numbers from your C3 run.
4. `Main/_Module/THIRD-PARTY-LICENSES.txt`: replace these three lines (WP109's text)

```
the mission-start guard and the skeleton-buffer guard and watch, whose
first-pool machine code block is his (sources: Main/Features/MissionStartGuard/ and
SkeletonBuffer/, and their adapters in Main/Adapters/, in the TAOM repository).
```

   with

```
the mission-start guard, the skeleton-buffer guard and watch (whose first-pool
machine code block is his) and the settlement nameplate cull (sources:
Main/Features/MissionStartGuard/, SkeletonBuffer/ and NameplateCull/, and their
adapters in Main/Adapters/, in the TAOM repository).
```

5. `docs/reference/provenance-register.md`: in the row starting `| Yotthani VanillaTuning |`, directly
   before ` | cleared |` append
   ``; `Main/Features/NameplateCull/**`, its adapter `Main/Adapters/{I,}NameplateCullAdapter.cs`, its tests `TAOM.Tests/Features/NameplateCull/**` ``.
   At the end of the `### Yotthani VanillaTuning` section add a paragraph:
   `` `Main/Features/NameplateCull/**` adapts its `nameplate-cull`: his skip rule and replacement update loop. TAOM's code is its own in structure (service, adapter, settings) and adds two skip conditions (a plate not yet parked off screen, a plate the tutorial targets). The 1.4.x port reads a hideout's range from `ISpottable.IsSpotted`, as v1.4.8's own plate update does ([nameplate-cull.md](../features/nameplate-cull.md)). ``
6. `docs/features/battle-load-diagnostics.md`, Configuration table: after its last row add
   ``| `CullHiddenNameplates` | `true` | Map Performance. Skips hidden settlement nameplates that cannot become visible this frame ([nameplate-cull.md](nameplate-cull.md)). Live. |``
7. Only if Step 0.3 printed `MAP PROFILER DOC PRESENT`: in `docs/features/map-perf-profiler.md`'s
   settings table, after the `Tick Profiler Top Behaviours` row, add
   `| Cull Hidden Settlement Nameplates | on | live; [nameplate-cull.md](nameplate-cull.md) |`. Otherwise
   skip and list "map-perf-profiler.md nameplate row owed" in the hand-off.

### Step C5: snapshot and commit

1. Snapshot as in Step A4 (here the diff also lists `reflection-sites.md`, your Step C4 edit; the tool
   does not write that file), adding exactly one row to `patch-targets.md` (Patches +1):
   ``| `TAOM.Features.NameplateCull.Hooks.SettlementNameplatesVM_Update_NameplateCull_Patch` | `SandBox.ViewModelCollection.Nameplate.SettlementNameplatesVM.Void Update()` |``
2. **Verify C5**: `grep -c "Permission is hereby granted" Main/_Module/THIRD-PARTY-LICENSES.txt` gives 1;
   `grep -c "NameplateCull/" Main/_Module/THIRD-PARTY-LICENSES.txt` gives 1; `grep -c "^## Patch104_NameplateCull$" docs/reference/harmony-patch-registry.md`
   gives 1. Docs command: no counter above Step 0.6. Full tests: previous total + 88, `Failed: 0`,
   Skipped = T0's. Binding gate `Failed: 0`. Snapshot check exit 0 (only when SNAP0 = 0).
3. Commit C: `git add` the checked-out and new paths (`Main/Features/NameplateCull`,
   `Main/Adapters/INameplateCullAdapter.cs`, `Main/Adapters/NameplateCullAdapter.cs`,
   `TAOM.Tests/Features/NameplateCull`, `docs/features/nameplate-cull.md`) and the edited
   `FeatureModules.cs`, `BattleLoadDiagnosticsSettings.cs`, `CoopSettingsRelevance.cs`,
   `CoopVetoClassificationTests.cs`, `SettingsFingerprintTests.cs`, `ReflectionSiteBindingTests.cs`,
   `coop-interop.md`, `bannerlord-together-compat.md`, `battle-load-diagnostics.md`,
   `harmony-patch-registry.md`, `reflection-sites.md`, `patch-targets.md`, `THIRD-PARTY-LICENSES.txt`,
   `provenance-register.md` (and `map-perf-profiler.md` if edited). `git status --porcelain` shows nothing
   else. Message `$AUX/120-msg-C.txt`:

```
feat(stability): v2.0.28 - skip hidden settlement nameplates

The campaign map updated every settlement nameplate every frame,
hidden ones included (988 settlements in TAOM_Map v2.0.27). A prefix
on SettlementNameplatesVM.Update now runs the same update over only
the plates that are not hidden and staying hidden; everything else
updates as in vanilla, and any doubt or error runs vanilla whole.
MCM: TAOM Battle Load Diagnostics, Map Performance, "Cull Hidden
Settlement Nameplates", on by default. On v1.4.8 a hideout's range
comes from ISpottable.IsSpotted, so the port reads that and not the
1.5.x line's Settlement.IsVisible. Adapted from yotthani's
VanillaTuning (MIT); the notice is in THIRD-PARTY-LICENSES.txt. Not
measured on v1.4.8 yet; on the 1.5.x line yotthani measured 5.5 ms
down to 2.3 ms per fast-forward map frame.

Research: taom-src v1.4.8 SettlementNameplatesVM.cs:238-248,
SettlementNameplateVM.cs:332-358, :474-532, :744-770; installed IL
call lists re-pinned in NameplateCullBindingTests
Not-tested: the cull in game; the frame-time A/B
Save-compat: no saved state; one new MCM key, default on
Ported-from: ecaa67d26463f7652c8235ebff54a7fe78e030a7
Ported-from: 7eac30153353ae401ef37c0955a29e92bb5d4b25
```

**Part C done**: commit C exists; Full tests T0 + 145 with `Failed: 0`.

## Part D: map-view release (port of `ecaa67d26`, MEMORY-07)

Source `ecaa67d26`, its MapViewRelease part only. Method: path-limited checkout plus hand edits (same
simulation as Part C). No engine member this code binds drifted on v1.4.8 (verification report V03,
re-read this session: see "Engine facts"). The VRAM saving is yotthani's 1.5.x measurement; nothing
in the commit or the docs may claim a saving measured on v1.4.8.

### Step D1 (RED, then GREEN)

1. RED: `git -C $WT checkout ecaa67d26 TAOM.Tests/Features/MapViewRelease`; in
   `MapViewReleaseBindingTests.cs` change `(v1.5.4 at the time of writing)` to
   `(v1.4.8 at the time of the 1.4.x port)`. Build: expected FAILS naming `TAOM.Features.MapViewRelease` types.
2. GREEN: `git -C $WT checkout ecaa67d26 Main/Features/MapViewRelease Main/Adapters/IMapViewReleaseAdapter.cs Main/Adapters/MapViewReleaseAdapter.cs`
3. `FeatureModules.cs`: append `        new Features.MapViewRelease.MapViewReleaseModule(),` as the LAST entry.
4. `BattleLoadDiagnosticsSettings.cs`: directly after the `CullHiddenNameplates` property (or, without
   Part C, directly above the Part A property; without both, above the class's closing `}`), add the
   two `ecaa67d26` properties verbatim:

```csharp
    [SettingPropertyGroup("Map Performance")]
    [SettingPropertyBool("Release Map View Memory", Order = 2, RequireRestart = false,
        HintText = "In the measurement this feature is adapted from, every screen or menu that covered the campaign map left three colour targets and one depth target of the map view's old set behind (tens of megabytes of graphics memory per closed screen, more at a high resolution). That memory stays until a battle, or a town, village or arena scene, starts. When on, every Nth cover (see Map View Release Interval) has the map view release its graphics memory as you return to the map. The map then shows a short loading screen until its view is ready again, on one return in N (about 0.4 s instead of 0.08 s in that measurement). Takes effect at the next cover. Default ON.")]
    public bool ReleaseMapViewMemory { get; set; } = true;

    [SettingPropertyGroup("Map Performance")]
    [SettingPropertyInteger("Map View Release Interval", 1, 1000, Order = 3, RequireRestart = false,
        HintText = "Release the map view's memory on every Nth cover of the map. Default 20. A lower number gives memory back sooner but shows the short loading screen on more returns to the map (about 0.4 s in the measurement this feature is adapted from); 1 releases on every cover. Takes effect at the next cover.")]
    public int MapViewReleaseInterval { get; set; } = 20;

```

5. `CoopSettingsRelevance.cs`, `Presentation` set: directly after the `"CullHiddenNameplates",` line
   (or, without Part C, directly above the comment line that starts `// Already suppressed under co-op`), add:

```csharp
        // Whether a covered campaign map releases its render targets and how often (MapViewRelease, a layer-event
        // handler, no patch): this client's graphics memory, never the campaign.
        "ReleaseMapViewMemory", "MapViewReleaseInterval",
```

6. `SettingsFingerprintTests.cs`: `BattleLoadDiagnosticsSettings` `reflected:` +2. Co-op docs: total,
   split and excluded figure +2.

**Verify D1**: Build `0 Error(s)`. Filtered `FullyQualifiedName~TAOM.Tests.Features.MapViewRelease`:
`Failed: 0`, `Total: 79`, `Skipped: 0`. Filtered
`FullyQualifiedName~SettingsFingerprintTests|FullyQualifiedName~TAOM.Tests.Composition`: `Failed: 0`.
`grep -rn "v1\.5" Main/Features/MapViewRelease Main/Adapters/MapViewReleaseAdapter.cs Main/Adapters/IMapViewReleaseAdapter.cs`: no hit.

### Step D2: docs

1. `git -C $WT checkout ecaa67d26 docs/features/map-view-release.md`, then:
   - Replace the link `[adopt-yotthani-2026-10-08.md](../reviews/adopt-yotthani-2026-10-08.md)` (twice:
     Overview and "GitHub Issue") with `` `docs/reviews/adopt-yotthani-2026-10-08.md` on the 1.5.x line (not ported) ``,
     and the sentence `TAOM's own audit lists the leak as item M3a.` with
     `The 1.4.x port audit lists the leak as MEMORY-07.`
   - "Vanilla behavior": `(v1.5.4)` becomes `(v1.4.8, taom-src `SceneLayer.cs:37-44`)`; `MapScreen.cs:648`
     becomes `MapScreen.cs:651`; `MapScreen.cs:504` becomes `MapScreen.cs:507`; after
     `MissionCampaignView.OnMissionScreenPreLoad` add ` (v1.4.8 dump `SandBox__SandBox.View.cs:2270-2278`)`;
     after `and only conversation and meeting missions pass false` add ` (1.5.x reading)`.
   - After the "Measurement" bullet add:
     `- **More from yotthani (1.5.x line, hooking `ID3D11Device::CreateTexture2D` on 1.5.3 to 1.5.5):** 42 of 42 colour targets and 14 of 14 depth targets were still alive after 14 screens, about 40 MB a screen at 1920x1080, and 2.29 GB over 35 openings with only the official modules, so the leak is vanilla's. TAOM has not measured it on v1.4.8 (see "How to check it in game").`
   - "Design Challenge": after the native parenthesis that ends `at `0x3491EB` and `0x34925F`)` add
     ` (a native reading on v1.5.4 by the 1.5.x line, not repeated on v1.4.8)`; after
     `MapScreen.HandleIfBlockerStatesDisabled` add ` (v1.4.8 taom-src `MapScreen.cs:833-860`)`.
   - "Solution Approach": `(v1.5.4, `ScreenLayer.cs:120-129`)` becomes `(v1.4.8, `ScreenLayer.cs:120-129`)`.
     After `MapNavigationHelper.SwitchToANewScreen` add ` (1.5.x reading)`.
   - Replace the sentence that starts `No existing TAOM adapter exposes the map's scene layer` with:
     `No existing 1.4.x TAOM adapter exposes the map's scene layer, so `IMapViewReleaseAdapter` is new.`
   - After `## Dependencies`'s last bullet add:
     `- On the 1.5.x line yotthani's VanillaTuning v0.1.1 switches its own `map-view-release` off when `TAOM.Features.MapViewRelease.MapViewReleaseModule` is loaded (code read there, game 1.5.5); whether VanillaTuning runs on v1.4.8 is not known. Keep the module's full type name.`
   - Directly above `## Performance` insert:

```markdown
## A second scene view on the map scene (1.5.x line, yotthani's 1.5.3 study)

On the 1.5.x line yotthani's RealmUI feasibility study (game 1.5.3, vanilla modules, one run each) saw a second enabled
scene view on the campaign map scene leave the map without terrain, and switching that second view off and then calling
`ClearAll(clearScene: false, removeTerrain: false)` on it hang the game on entering the map. The release here makes the
same `ClearAll` call through `ClearRuntimeGPUMemory(remove_terrain: false)` (`SceneLayer.cs:145-148`) on the map's own
view, as VanillaTuning's own release does every 20th cover in yotthani's game. TAOM on 1.4.x adds no scene view or scene
layer anywhere (`git grep "new SceneLayer" -- Main` is empty), so the second-view case does not arise; the in-game check
still watches for a hang or a missing terrain on returning to the map.
```

   - "How to check it in game", step 6: replace `the Realm Borders parchment, the camps and the supply routes`
     with `the camps, the supply routes and the War of the Ring momentum indicator` (Realm Borders is not on 1.4.x).
   - Changelog: add `- <today>: ported to the 1.4.x line by plan 120; not yet measured on v1.4.8.`
     GitHub Issue: add `- **1.4.x port:** plans/120-map-perf-and-vram.md.`
   - `grep -n "v1\.5\|adopt-yotthani\](" docs/features/map-view-release.md`: every hit must be an
     attribution to the 1.5.x line.
2. `THIRD-PARTY-LICENSES.txt`: replace the four lines Part C wrote

```
the mission-start guard, the skeleton-buffer guard and watch (whose first-pool
machine code block is his) and the settlement nameplate cull (sources:
Main/Features/MissionStartGuard/, SkeletonBuffer/ and NameplateCull/, and their
adapters in Main/Adapters/, in the TAOM repository).
```

   with

```
the mission-start guard, the skeleton-buffer guard and watch (whose first-pool
machine code block is his), the settlement nameplate cull and the map-view release
(sources: Main/Features/MissionStartGuard/, SkeletonBuffer/, NameplateCull/ and
MapViewRelease/, and their adapters in Main/Adapters/, in the TAOM repository).
```

   Without Part C, replace WP109's three lines (Step C4 item 4) with a version that names the map-view
   release instead of the nameplate cull.
3. `provenance-register.md`: in the `| Yotthani VanillaTuning |` row, directly before ` | cleared |`
   append ``; `Main/Features/MapViewRelease/**`, its adapter `Main/Adapters/{I,}MapViewReleaseAdapter.cs`, its tests `TAOM.Tests/Features/MapViewRelease/**` ``.
   At the end of `### Yotthani VanillaTuning` add:
   `` `Main/Features/MapViewRelease/**` adapts its `map-view-release`: his release of the map's scene view every Nth cover. TAOM listens to the engine's `ScreenLayer.OnLayerActiveStateChanged` where he patched `SceneLayer.OnDeactivate`, releases on the map's return instead of the cover, and checks for a running campaign before `MapScreen` is touched ([map-view-release.md](../features/map-view-release.md)). ``
4. `battle-load-diagnostics.md`, Configuration table: after its last row add
   ``| `ReleaseMapViewMemory` | `true` | Map Performance. Every Nth cover of the campaign map releases the map view's render targets on its return ([map-view-release.md](map-view-release.md)). Live. |``
   and ``| `MapViewReleaseInterval` | `20` | Map Performance. N for the row above, 1 to 1000. Live. |``
5. Only if `MAP PROFILER DOC PRESENT`: add to `map-perf-profiler.md`'s settings table
   `| Release Map View Memory | on | live; [map-view-release.md](map-view-release.md) |` and
   `| Map View Release Interval | 20 | live, 1 to 1000; [map-view-release.md](map-view-release.md) |`;
   otherwise list "map-perf-profiler.md map-view rows owed" in the hand-off.

### Step D3: verify and commit

1. Snapshot: no patch class is added, so with SNAP0 = 0 the Snapshot check must still exit 0 and
   `patch-targets.md` stays unchanged.
2. **Verify D3**: `grep -c "MapViewRelease/" Main/_Module/THIRD-PARTY-LICENSES.txt` gives 1;
   `grep -c "Permission is hereby granted" Main/_Module/THIRD-PARTY-LICENSES.txt` gives 1. Docs command:
   no counter above Step 0.6. Full tests: previous total + 79, `Failed: 0`, Skipped = T0's. Binding gate
   `Failed: 0`.
3. Commit D: `git add Main/Features/MapViewRelease Main/Adapters/IMapViewReleaseAdapter.cs Main/Adapters/MapViewReleaseAdapter.cs TAOM.Tests/Features/MapViewRelease docs/features/map-view-release.md`
   plus the edited `FeatureModules.cs`, `BattleLoadDiagnosticsSettings.cs`, `CoopSettingsRelevance.cs`,
   `SettingsFingerprintTests.cs`, `coop-interop.md`, `bannerlord-together-compat.md`,
   `battle-load-diagnostics.md`, `THIRD-PARTY-LICENSES.txt`, `provenance-register.md` (and
   `map-perf-profiler.md` if edited); `git status --porcelain` shows nothing else. Message
   `$AUX/120-msg-D.txt`:

```
feat(stability): v2.0.28 - release the covered map view's GPU targets

In yotthani's measurement on the 1.5.x line, every screen opened over
the campaign map left the map view's render targets in GPU memory
until the next battle or town, with only the official modules too.
TAOM now counts the covers of the map's own scene layer and, every
20th time, releases that view's GPU resources through vanilla's
SceneLayer.ClearRuntimeGPUMemory(false) when the map comes back; that
return shows a short loading screen. No Harmony patch: one handler on
ScreenLayer.OnLayerActiveStateChanged. MCM: TAOM Battle Load
Diagnostics, Map Performance, "Release Map View Memory" (on) and "Map
View Release Interval" (1 to 1000, default 20). Adapted from
yotthani's VanillaTuning (MIT). The saving is not measured on v1.4.8.

Research: taom-src v1.4.8 ScreenLayer.cs:110-129, SceneLayer.cs:145-148,
MapScreen.cs:257, :294, :507, :651, :833-860, ScreenBase.cs:71-145
Not-tested: dedicated GPU memory over 20 covers; the release in game
Save-compat: no saved state; two new MCM keys, defaults on and 20
Ported-from: ecaa67d26463f7652c8235ebff54a7fe78e030a7
Ported-from: 7eac30153353ae401ef37c0955a29e92bb5d4b25
Ported-from: 8dd76ddb376425da6a167415186f1f4a40781355
Ported-from: 2ac85fbe777a3cdc1513ba76402b8563fcea3770
```

**Part D done**: commit D exists; Full tests T0 + 224 with `Failed: 0`.

### Step E: review gate and hand-off

1. Run every Done criterion below.
2. Review gate: run `/deep-review` on `git diff $BASE..HEAD` (C# across many files) and ask it to probe
   the "Maintenance notes" review spots. Act only on findings that change runtime behaviour or break
   this plan; land each fix as one extra commit `fix(stability): v2.0.28 - plan 120 review follow-ups`
   (tests first, same commit procedure) and re-run the Done criteria. Never rewrite earlier commits. If
   your session cannot invoke skills, say so in the hand-off. Never run `/review-codex` or `/codex-verify`
   (paid; maintainer only).
3. Hand-off (your final message; no old-plan row needs updating): status (which Parts are finished);
   branch; each commit's short SHA and subject; T0 and the final
   (Total, Passed, Skipped); the Binding gate Skipped number; SNAP0 and whether you regenerated or
   hand-edited the snapshot; the map-profiler rows owed, if any; the `/deep-review` outcome; the issue
   numbers used for Parts A and B; the "In-game checks owed" list.

## Disposition

Simulations re-run this session onto `origin/bannerlord-1.4.5` with
`git merge-tree --write-tree --name-only --no-messages --merge-base=<sha>^ origin/bannerlord-1.4.5 <sha>`.

| Commit or finding | Subject | Simulation | Disposition |
|---|---|---|---|
| `ecaa67d26` | feat(stability): v2.0.34 - mission start, freeze and map guards | exit 1, 38 paths | **Owned by WP109** (group `yotthani-stability-guards`). **Transplanted here**: NameplateCull (Part C) and MapViewRelease (Part D): both folders, four adapters, 14 test files, two module lines, three settings, the `CoopSettingsRelevance` keys, the CoopVeto entry, the fingerprint and doc counts, three ReflectionSite rows, two feature docs, the Patch104 registry section, one `patch-targets.md` row and three `reflection-sites.md` rows, the license and provenance extension. **Adapted**: the v1.4.8 hideout range and the two re-pinned call lists (D22), the NameplateCull hint (no map-profiler sentence), the two `CampaignHotPathSettingsProvidersTests` pins moved to `NameplateCullSettingsProviderTests`. **Not taken**: the `HotPathSettingsProvidersTests` DataRow (file absent; WP123 brings it), the `map-perf-profiler.md` rows unless WP117 merged first, engine doc section 10 and the lifecycle doc (WP117, WP144). Everything else belongs to WP109 or is bookkeeping |
| `83ad1eab5` | docs(ai): v2.0.34 - pair Harmony prefix and finalizer via __state | exit 1 (`harmony-patches.md`) | **Not here**: WP109 Step B4 |
| `6d7e67f3f` | feat(coop): v2.0.35 - server leaves client-only patch classes out | not run | **Skipped**: D7; Part C takes the `ecaa67d26` patch file without `[ClientOnly]` (V03) |
| `7eac30153` | docs(engine): v2.0.34 - 1.5.5 perf, native and coexistence findings | exit 1 (both docs absent on 1.4.x) | **Adapted**: one sentence each in `nameplate-cull.md` and `map-view-release.md` (VanillaTuning v0.1.1 stands aside on the module type names), labelled as a 1.5.x reading |
| `0bb1f3cae` | docs(engine): v2.0.34 - doc review fixes for the yotthani batch | not run | **Skipped**: a line-number fix inside `7eac30153`'s sentence, which is rewritten here |
| `8dd76ddb3` | docs(reviews): v2.0.34 - adopt yotthani 2026-10-16, RCA and lessons | exit 1 (`map-view-release.md`) | **Adapted**: the second-scene-view caveat, condensed in `map-view-release.md`; the review records are skipped |
| `2ac85fbe7` | docs(yotthani): v2.0.36 - engine and campaign findings, Oct 29 | exit 1 (`map-view-release.md`) | **Adapted**: the 1.5.x measurement bullet (42 of 42 targets, 2.29 GB over 35 openings) |
| `a15563a3c`, `7f3636b2e` | feat(coop) ... TAOM.Client; fix(coop) ... acting co-op player | not run | **Skipped**: D7 (their only hunk here is a `Client/` line cite) |
| `e339df075`, `cd1fbdb6f` | feat(cultures) ... Arnor realms; docs: ... backlinks footers | not run | **Skipped**: an auto-generated backlinks footer in a content commit and a footer regeneration |
| `1325c36ea` | perf(campaign): v2.0.34 - switch off ButterLib's Distance Matrix | not run | **Not here**: RUNTIME-PERF-11 part A; WP118 owns group `issue-740-butterlib-distance-matrix` |
| `af990ca76` | docs(perf): v2.0.32 - engine performance programme records and plans | not run | **Adapted**: only its `docs/features/battle-load-diagnostics.md` correction (Step A3 item 5), re-cited to v1.4.8 `GameStateManager.cs:257`, `:277-279`, `:285`, `:313`, the unverified parts labelled as a 1.5.x reading, the 1.5.x trace path dropped. **Not here**: the engine page, toolchain row and provenance row (WP117); `rca-memory-instruments-2026-09-12.md` and `native-commit-audit-2026-08.md` (WP142) |
| MEMORY-01, RUNTIME-PERF-14 | cheat-mode inventory transient | | **New work**: Part A (also gates the party screen twin, the same mechanism) |
| MEMORY-04 | texture quality advisor | | **New work**: Part B |
| RUNTIME-PERF-11 part B | settlement nameplates every frame | | **Ported**: Part C |
| MEMORY-07 | covered map view keeps its render targets | | **Ported**: Part D (VRAM check owed) |

## Test plan

- New results per Part: A 32 (`CheatScreenGuardCallsTests` 5, `...ServiceTests` 8,
  `...SettingsProviderTests` 2, `...LinesTests` 3, `...ModuleTests` 6, `...BindingTests` 8); B 25
  (`TextureAdviceRuleTests` 9, `...ServiceTests` 6, `...AdapterTests` 2, `...ModuleTests` 5,
  `...BindingTests` 3); C 88 (82 ported from `ecaa67d26`: `NameplateCullBindingTests` 19,
  `...HookTests` 5, `...LinesTests` 8, `...ModuleTests` 7, `...PrefixOrderTests` 2, `...RuleTests` 20,
  `...ServiceTests` 21; plus the new D22 test, `NameplateCullSettingsProviderTests` 2 and 3
  ReflectionSite rows); D 79 (`MapViewReleaseBindingTests` 12, `...CounterTests` 12, `...HookTests` 8,
  `...LinesTests` 5, `...ModuleTests` 8, `...ServiceTests` 32 with its two DataRows,
  `...SettingsProviderTests` 2). Total 224.
- Edited tests: `NameplateCullBindingTests` (v1.4.8 pins), `SettingsFingerprintTests` (counts),
  `CoopVetoClassificationTests` (one entry), `ReflectionSiteBindingTests` (three rows).
- Patterns: `ecaa67d26:TAOM.Tests/Features/NameplateCull/NameplateCullModuleTests.cs` (module wiring),
  `ecaa67d26:TAOM.Tests/Features/MissionStartGuard/MissionStartGuardBindingTests.cs:105-184` (real-IL
  rewrite and JIT), `ecaa67d26:TAOM.Tests/Features/NameplateCull/NameplateCullServiceTests.cs` (services
  over NSubstitute).
- Structurally untestable (for `Not-tested:`): both cheat screens in a live campaign; the popup's look;
  `NativeOptions.GetConfig` and `GlobalMemoryStatusEx` on a player's machine; the cull and the release
  in a live map; frame time and dedicated GPU memory.

## Done criteria

All must hold at the end of each Part (later Parts include the earlier criteria):

- [ ] Build exits 0 with `0 Error(s)`.
- [ ] Full tests `Failed: 0`; Total = T0 + 32 (Part A), + 57 (after B), + 145 (after C), + 224 (after D);
      Skipped = T0's.
- [ ] Binding gate `Failed: 0`.
- [ ] `git -C $WT log --format=%s $BASE..HEAD` lists exactly the commits of the finished Parts (plus any
      review follow-up), each matching `v2.0.28 - ` and at most 72 characters;
      `git -C $WT diff --stat $BASE..HEAD -- CHANGELOG.md Main/IoC.cs Main/SubModule.cs Main/TAOM.csproj Dependencies .claude plans`
      is empty.
- [ ] `git -C $WT status --porcelain` is empty.
- [ ] `grep -cE "new Features\.(CheatScreenGuard\.CheatScreenGuardModule|TextureAdvice\.TextureAdviceModule|NameplateCull\.NameplateCullModule|MapViewRelease\.MapViewReleaseModule)\(\)," Main/Composition/FeatureModules.cs`
      gives the number of finished Parts (4 at the end).
- [ ] `git grep -n "Patch_CheatScreenGuard\|Patch104_NameplateCull" -- Main/SubModule.cs Main/IoC.cs` is empty.
- [ ] Part A: `grep -l 'id="taom_cheat_screen_guard_notice"' Main/_Module/ModuleData/Languages/*/std_taom_module_strings_*.xml | wc -l` gives 12. Part B: the same
      count gives 12 for each of `taom_texture_advice_title`, `taom_texture_advice_body`, `taom_texture_advice_ok`.
- [ ] Part C: `grep -c '"Settlement.get_FerryTarget"\|"SettlementNameplateVM.set_HasFerry"' TAOM.Tests/Features/NameplateCull/NameplateCullBindingTests.cs`
      gives 0; `grep -c "spottable.IsSpotted" Main/Adapters/NameplateCullAdapter.cs` gives 1.
- [ ] Data command: no ERROR beyond Step 0.5. Docs command: no counter above Step 0.6 (in particular
      `ai_dashes` and the orphan count do not rise).
- [ ] Snapshot check exits 0 when SNAP0 was 0.

## STOP conditions

Stop and report (do not improvise) if:

- Any Step 0.3 result differs: a WP105 runner type, a WP109 module line, `TickProfilerTranspiler`, a WP107
  `PatchShieldPolicy` method, the WP109 license, provenance or registry text, `MemorySampleReader.TryRead`,
  `IInquiryAdapter.ShowMessage` or its registration is missing; a target folder or doc already exists; a
  WP120 name already appears in `Main` or `TAOM.Tests`.
- `plans/README.md` does not show WP109 merged, or the merged-base test count differs from Step 0.4, or
  D20 is unmet (no recorded WP101 laptop Armory update and validator baseline).
- `CheatScreenGuardBindingTests` reports a `Game.get_CheatMode` count other than 1 in either opener, or a
  swap count other than 1, or the JIT test throws.
- In Step C2 any NameplateCull test fails other than the one D22 test, in particular a re-pinned call list
  (`VanillaDynamicProperties_...`, `RefreshBindValues_...`, `VanillaPlateUpdate_...`,
  `VanillaUpdate_...`): the engine differs from this session's IL reading. Report the printed list; do
  not edit the pin to match.
- `NameplateCullBindingTests` or `MapViewReleaseBindingTests` skips (the game assemblies did not load).
- A ported test fails for any reason other than the RED states this plan predicts, including a missing
  composition member (`FeatureModules.All`, `ApplyPhase`, `PatchCategoryDecl`) or a missing MCM type at
  runtime (`FileNotFoundException` for `MCMv5`).
- An anchor this plan quotes (`"OfferLeaveOnArrival",`, the `// Already suppressed under co-op` comment,
  the `// --- Parked` line, `public void ReflectionSite_ResolvesAgainstInstalledEngine(`, WP109's license
  lines, `| Yotthani VanillaTuning |`, `## Patch_MissionTime_SetMovementOrder`, `## Late_ActionSetOverride`)
  is missing or appears twice.
- The Language rows helper stops on an assertion (a key already present, a file without exactly one
  `  </strings>`, a file that no longer parses, not exactly 12 files), or `LanguageFileCoverageTests` or
  `LanguageDataXmlTests` still fail after the rows are in.
- `FeatureModulesTests` fails after any Part (double wiring or a kernel anchor).
- A commit gate blocks a commit (subject, the ModuleData validator under D20 for Parts A and B,
  files-tracked); never bypass.
- The fix for a review finding needs an out-of-scope file, especially `Main/SubModule.cs`, `Main/IoC.cs`,
  a csproj, `Dependencies/` or `IGraphicsOptionsAdapter`.
- A step's verification fails twice after a reasonable fix attempt.

## In-game checks owed (maintainer; read `taom_debug_*.log` unless named)

Part A (cheat-screen guard):
1. With `cheat_mode = 1` in `engine_config.txt`, load a campaign: one `[CheatScreenGuard] ON:` line at
   game init (a `not fully installed` warning means a gate is off: report it).
2. Press I on the map: only your own items, no Discard list of thousands; the
   `[MemStation] enter/exit screen='GauntletInventoryScreen'` heapMB values differ by well under 100 MB;
   one `[CheatScreenGuard] cheat mode is on: the inventory opened without the cheat list` line; record
   whether the chat notice was visible (on the screen or after closing it).
3. Press P: the normal party screen. Open a loot screen after a battle and a town trade screen: unchanged.
4. Tick "Cheat Mode Lists All Items And Troops", press I: the vanilla list returns (with its memory cost:
   expected). Untick it.
5. With `cheat_mode = 0`: no `cheat mode is on` line in a whole session.

Part B (texture advice):
1. On a machine with 16 GB or less and Texture Quality High, first launch: one popup at the main menu;
   `[TextureAdvice] totalPhysMB=... textureQuality=2/2 shownBefore=false: advice shown ...`; the file
   `taom-texture-advice.shown` appears in the `Logs` folder beside the TAOM log.
2. Next launch: no popup; the line ends `shownBefore=true: no advice`.
3. On the desktop (more than 17 GB): no popup; the line ends `no advice`. Lowering Texture Quality and
   deleting the marker on a 16 GB machine: no popup either.

Part C (nameplate cull):
1. Game init: `[NameplateCull] ON: the campaign map skips hidden settlement nameplates (toggle on)`.
2. Look at the map at each zoom level: plates appear and fade as in vanilla; a tracked settlement keeps
   its marker; walking up to a settlement shows its plate at once; siege and raid events still show on plates.
3. A hideout spotted by an incident shows its plate in range (the D22 case).
4. Fast-forward five minutes: one `[NameplateCull] 18000 culled map frames: ... skipped (N percent)` line.
5. The frame-time A/B in `nameplate-cull.md`, "How to A/B it in game". Before the v2.4.0 release: if it
   shows no gain or any visual fault, ship the cull off by renaming its key (Maintenance notes).

Part D (map-view release):
1. Game init: `[MapViewRelease] ON: the campaign map's view releases its render targets once per 20 covers (toggle on)`.
2. Exit to the main menu after a minute on the map and open Load: the preview's characters stand.
3. Task Manager, Performance, GPU, "Dedicated GPU memory": open and close the inventory, party and clan
   screens 20 to 30 times from the map with the setting on, then off. Record both curves: this is the
   VRAM measurement owed before any saving is claimed.
4. One return in twenty shows a short loading screen, and `[MapViewRelease] released 1 times, map covered 20 times` appears.
5. Interval 1: Options, Save, Load and the fief hub (F6) from the map show no loading screen over them;
   every return to the map renders terrain and does not hang. Interval back to 20.
6. After a release, zoom fully out: camps, supply routes and the War of the Ring indicator still show.
7. Talk to 20 parties on the map without opening a screen and record whether GPU memory climbs (map
   conversations are not counted as covers).

## Maintenance notes

- **Shared files**: `FeatureModules.All` gets four appended lines here; WP111 and WP117 append too, so
  the integrator keeps every line when merging. `BattleLoadDiagnosticsSettings.cs`'s "Map Performance"
  group is shared with WP117 (`EnableMapProfiler`, Order 0). `CoopSettingsRelevance.cs`,
  `SettingsFingerprintTests.cs` and the two co-op doc counts move with many WPs: the integrator recounts
  on each merge. WP117 has no step for the three `map-perf-profiler.md` rows in Steps C4 and D2: after both
  WP117 and WP120 merge, in either order, the integrator applies whichever of them this plan's hand-off lists as owed.
- **Defaults and keys**: the three ported keys keep their 1.5.x names and defaults because both lines
  share the MCM file. To ship one off on 1.4.x, rename the property (for example
  `CullHiddenNameplatesV2` with default false), update its provider, binding test and the co-op lists,
  never flip the default. `CheatModeListsAllItemsAndTroops` is 1.4.x-only, default off.
- **Type names are a contract**: VanillaTuning v0.1.1 stands aside on `TAOM.Features.NameplateCull.NameplateCullModule`
  and `TAOM.Features.MapViewRelease.MapViewReleaseModule` (1.5.x reading). Never rename them.
- **Later WPs**: WP123 (plans 003, 031, 037) brings `CampaignHotPathSettingsProvidersTests` and
  `HotPathSettingsProvidersTests`; its Step C2 takes the `NameplateCullSettingsProvider` DataRows and the
  `BattleLoadDiagnosticsSettings` row from `ecaa67d26` and folds `NameplateCullSettingsProviderTests` in.
  WP117 adds `TextureQualityOption` to `IGraphicsOptionsAdapter`; `TextureAdviceAdapter` may delegate to
  it later (optional). If WP122 or a later WP ports the 1.5.x `PatchCategoryNumberTests`, both new
  categories already have registry headings.
- **PatchShield**: `com.taom.mod` is not a protected owner on 1.4.x (origin), so a missing-API swallow on
  any method TAOM patches strips TAOM's patch there. For the cull and the guard that means vanilla runs;
  it is a wider question for WP107's owner, not this plan.
- **Review spots for `/deep-review`**: the D22 line and the re-pinned lists; the guard's fallbacks (no
  service or a throwing service gives vanilla; a throwing setting read hides the list); the transpilers'
  soft-fail and the `[HarmonyCleanup]` counts; the advice's show-then-remember order and the marker path;
  that no category or behaviour is hand-wired in `SubModule.cs`; the license block (scoped ownership
  sentence); that no doc claims a v1.4.8 measurement for the cull or the release.
- **Deferred on purpose**: translations of the four new strings (WP135, `/localize`, paid; the 12 language files carry English rows until then); a frame-time
  and a VRAM measurement on v1.4.8 (in-game checks above); banner atlas and UI sheet compression
  (MEMORY-02, MEMORY-03: WP121); a `cheatMode=` field in the mission diagnostic snapshot (the guard's own
  line and the `[TextureAdvice]` line already put cheat mode and texture quality in player logs). The
  optional `[InvOpen]` line (MEMORY-09's proposal; WP117's maintenance note points here) is not taken for
  the same reason: the guard's hidden-list line and the existing `[MemStation]` lines answer MEMORY-01.
- **Release**: TAOM.dll only; no `Dependencies/` change, so no #371 pairing duty from this plan.
