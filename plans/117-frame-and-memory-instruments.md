# Plan 117: Measure frame time, hitches, map frames and clip memory on the 1.4.8 line

> **Executor instructions**: Follow this plan step by step. Run every verification command and
> confirm the expected result before moving on. If anything in "STOP conditions" occurs, stop and
> report; do not improvise. Do not edit `plans/README.md`: every WP branch shares it, so the
> integrator records this plan's status and test count when merging; put the hand-off numbers in
> your final message. Two Parts (A, B) in a fixed order; each ends at a green full suite and can be
> handed off on its own (Part B needs Part A on the same branch).
>
> **Drift check (run first, Step A0.2)**:
> `git diff --stat 1b00d1acd..HEAD -- Main/SubModule.cs Main/Features/BattleLoadDiagnostics Main/Features/CoopInterop/CoopSettingsRelevance.cs Main/Features/TimeAcceleration Main/Features/TaomSettings.cs Main/Features/MissionPerf Main/Features/MapPerf Main/Features/LostFooting Main/Adapters Main/Composition/FeatureModules.cs Main/Core/Diagnostics Dependencies/Foundation/PatchShieldPolicy.cs TAOM.Tests/Features/MissionPerf TAOM.Tests/Features/MapPerf TAOM.Tests/Features/LostFooting TAOM.Tests/Features/BattleLoadDiagnostics TAOM.Tests/Features/CoopInterop/SettingsFingerprintTests.cs TAOM.Tests/Features/Mcm/SettingRequireRestartPostureTests.cs TAOM.Tests/Features/TimeAcceleration TAOM.Tests/Infrastructure/Dependencies/PatchShieldPolicyTests.cs TAOM.Tests/Migration/ReflectionSiteBindingTests.cs tools/perf_runs.py tools/audit_battle_equipment_memory.py tools/audit_map_scene_memory.py tools/tests docs/features docs/reference/engine docs/reference/harmony-patch-registry.md docs/reference/provenance-register.md docs/reference/bannerlord-engine-and-toolchain.md docs/reference/taleworlds-api-snapshot/reflection-sites.md`
> Earlier WPs of this programme are expected to have changed many of these (WP104 and WP105:
> `SubModule.cs`, `Composition`; WP106: Patch91, the settings page, `CoopSettingsRelevance.cs`;
> WP107: `PatchShieldPolicy.cs`; WP109: the first `MissionPerf` files; WP111: the graphics adapter,
> `FeatureModules.cs`, `TaomSettings.cs`; WP113: the settings page). Compare the live files with
> "Current state" and STOP only on a real mismatch: an anchor this plan names is missing or appears twice.

## Status

- **Priority**: P1
- **Effort**: L (two Parts, eight commits; about 60 production and 50 test files transplanted from
  the 1.5.x tree, about 25 hand edits to shared files, three docs adapted)
- **Risk**: MED. Two instruments ship on by default: the hitch probe puts Harmony brackets on six
  per-frame mission methods, and the clip probe reads native memory once a second. Both self-disable
  on a failed check, the code is the 1.5.x line's reviewed tree, and the two v1.4.8 differences found
  in research (the `Mission.SpawnAgent` signature, the map view list) are pinned by binding tests.
- **Depends on**: WP106, WP107, WP109, WP111 merged, on top of wave 0 (WP101 to WP105) in `bannerlord-1.4.5`
- **Category**: perf (diagnostics and tools)
- **Wave / lane**: wave 2, lane A
- **Executor**: laptop
- **Decisions applied**: D1 (never stage `CHANGELOG.md`), D2 (the clip probe and the census arrive as
  their 1.5.x `*Module.cs` plus a `FeatureModules.All` line; the profilers, which the 1.5.x line wires
  by hand in `SubModule.cs`, stay hand-wired), D7 (co-op commits `6d7e67f3f`, `c91380274`, `a15563a3c`,
  `d021426b0` not taken), D8 (`v2.0.28` subjects), D9 (gates run locally), D10, D17 (WP106's
  recovery is cited as the remedy for the OnTick hang), D19 (git only through the Bash tool). This
  plan decides, under the orchestrator's WP117 note: the hitch probe ships ON (section "Decisions
  taken in this plan", measured in Step A2.7); the clip probe ON; the Lost Footing census OFF under a
  renamed key.
- **Planned at**: 1.4.x `origin/bannerlord-1.4.5` `1b00d1acd` and 1.5.x `origin/bannerlord-1.5.x`
  `e45f6df2c`, 2026-11-04. Transplant source for MissionPerf and MapPerf: `685c6a1e0` (the last commit
  of this pack that touches them).
- **Issue**: port of #608 (heartbeat), #710 and #724 (perf programme, tick profiler), #711 (perf runs),
  #712, #716, #722 (integration fixups), #718 (clip probe), #720 (equipment audit), #721 (map
  profiler), #723 (hitch probe), #806 and #809 (camera timing), #894 (Lost Footing)

## Why this matters

The 1.4.x line cannot measure frame time (deep-dive RUNTIME-PERF-13): the player logs E1 to E4 carry
`[MemSample]` and `[BattleLoad]` lines but nothing per frame, so the "game freezes, combat sounds keep
playing" reports and the Harad first-battle freeze (E3: two `WaitingForRender` samples, then silence)
cannot be told apart. Report V02 found E3 undecidable from its log, and the #612 lesson shows that a
falling then frozen shader count can be a wedge, not a compile. After this plan every 1.4.8 log gets
a `[MissionPerf]` line every 5 s (it stops when the mission tick stops), a `[Hitch]`/`[HitchDetail]`
pair naming where each frame of 250 ms or more went (agent-tick wait, mission tick, camera and render
preparation, scene scripts, spawns, clip loading), and a `[PerfContext]` line per mission with the
frame limiter, vsync, texture, shadow and particle options, memory load and the CLR version (which
rules the CoreCLR host in or out, an open question of report V01). Opt-in: a per-behaviour tick
profiler, a campaign map frame profiler and a census of stranded soldiers. On by default: an
`[AnimMem]` clip-memory line, whose v1.4.8 native pattern report V03 verified. Two offline tools
come with it: `perf_runs.py` turns logs into per-mission rows and A/B comparisons, and
`audit_battle_equipment_memory.py` ranks the assets a battle preloads from the cooked packs this
laptop has. Every magnitude the runtime-perf lens left UNVERIFIED (waves 3 and 4 rank their work by
it) needs these numbers.

## Current state

### Executor environment

Laptop; repo `C:\Users\mikew\source\repos\TAOM`; Bannerlord v1.4.8 (build 119303) at
`C:\Program Files (x86)\Steam\steamapps\common\Mount & Blade II Bannerlord` (env `BANNERLORD_GAME_DIR`);
never write into the game install. Python: `python` (else `/c/Python314/python.exe`). Every Bash
call sets its own variables, and long runs need a tool timeout of 600000 ms. After Step A0.1 every
call starts with:

```
WT=/c/Users/mikew/source/repos/taom-port/117; AUX=/c/Users/mikew/source/repos/taom-port/117-tmp; BASE=$(cat $AUX/base.txt)
```

Commit messages are written with the Write tool to `C:\Users\mikew\source\repos\taom-port\117-tmp\msg-<id>.txt`
(outside the worktree, never staged) and passed to git as `C:/Users/mikew/source/repos/taom-port/117-tmp/msg-<id>.txt`.

### Prerequisites from earlier WPs (Step A0.3 checks each)

| From | What this plan uses |
|---|---|
| WP104 | `private bool TryPatchCategory(string category)` in `Main/SubModule.cs` (the installers take it as `Func<string, bool>`); `TAOM.Tests/Infrastructure/RepoPaths.cs` with `ReadSource(path, stripComments)`, `RepoPath(...)`, `StripComments(...)` |
| WP105 | `Main/Composition/*`: `TaomFeatureModule`, `MissionBehaviorDecl.Of<T>(Func<Mission, IResolver, T>)` with `BehaviorType`, `FeatureModules.All`, and `FeatureModuleHooks.AddMissionBehaviors(mission, AddTaomBehavior);` in `SubModule.cs`. The API is unchanged between WP105's source `1efdf2985` and `685c6a1e0` (`git diff 1efdf2985 685c6a1e0 -- Main/Composition/` touches only `FeatureModules.cs` and the 1.5.x-only `MainMenuStep.cs`) |
| WP106 | `Main/Features/BattleLoadDiagnostics/Hooks/Patch91_MissionTickStallProbes.cs` (class `Mission_TickAgentsAndTeamsImp_StallProbe_Patch`, category `Patch91_MissionTickStall`, expression-bodied `Prefix` and `Finalizer`); `IBattleLoadDiagnosticsSettingsProvider.MissionTickStallSamplerEnabled`; in `SubModule.cs` `TryPatchCategory("Patch91_MissionTickStall");` then `IoC.Resolve<Features.BattleLoadDiagnostics.MissionTickStallWatchdog>().Start();` after `_gameInitPatchesApplied = true;`; its Part B adds `TryPatchCategory("Patch91b_MissionTickRecovery");` and a `MissionTickRecoveryWiring.VerifyAndArm(...)` line right after, and `docs/features/mission-tick-guard.md` |
| WP107 | `PatchShieldPolicy.ExcludedTargetMethods` holding exactly `"TaleWorlds.MountAndBlade.Mission.TickAgentsAndTeamsImp",`; `IsExcludedTargetMethod`, `IsExcludedTargetNamespace`, `FormatHotMethodSkip`; `PatchShield.LogHotMethodSkip` (plan 107's Commit E took `6eb2e1b90`'s entry, the diag line and their two tests) |
| WP109 | `Main/Features/MissionPerf/TickProfilerTranspiler.cs` (with `CallSwap`), a trimmed `TickProfileLines.cs` whose summary names WP117, `AnimMemory/ClipBudgetSignature.cs`, `AnimMemory/PeSectionTable.cs`; tests `TickProfilerTranspilerTests`, `TickProfilerTranspilerOccurrenceTests`, `AnimMemory/{SyntheticNativeImage,ClipBudgetSignatureTests,PeSectionTableTests,ClipBudgetSignatureInstalledBinaryTests}.cs` (the last with the v1.4.8 row `0x23AADF`), `TAOM.Tests/Migration/NativePinGateTests.cs`. Those files are byte-identical at `685c6a1e0` and the tip (blob check, this session); do not touch them, except that A2 replaces the trimmed `TickProfileLines.cs` with the full one, as plan 109 says. The six members the trimmed file holds are unchanged between `ecaa67d26` and `685c6a1e0`, and no MissionStartGuard or SkeletonBuffer file names `TickProfileLines` |
| WP111 | `Main/Adapters/IGraphicsOptionsAdapter.cs` and `GraphicsOptionsAdapter.cs` in the `c17541f2c` shape (plan 111 changes only the line-11 comment), registered by `BattleCorpsesModule` (`Register<IGraphicsOptionsAdapter, GraphicsOptionsAdapter>(Reuse.Singleton)`), listed in `FeatureModules.All` |

### 1.4.x anchors (`origin/bannerlord-1.4.5` line numbers; earlier WPs move them)

- `Main/SubModule.cs` (single-owner; this plan's edits are listed per step): `using TAOM.Core.Logging;` `:18`,
  `using TAOM.Adapters;` `:22`; `public override void OnGameEnd(Game game)` `:668` (ends with two
  try/catch blocks, the last one `catch { /* teardown is best-effort, never break OnGameEnd */ }`);
  `protected override void OnGameStart(Game game, IGameStarter gameStarterObject)` `:697`;
  `OnGameInitializationFinished(Game game)` `:1309`; `StampSaveLoadPhase(Features.SaveLoadDiagnostics.Domain.SaveLoadPhase.GameInitializationFinished);`
  `:1315`; the guard comment `// Harmony patches are process-global (applied to methods, persist across games). Apply this`
  `:1317`; `if (_gameInitPatchesApplied) return;` `_gameInitPatchesApplied = true;` `:1324-1325`;
  Patch43 `:1672`; `BattleLoadStallWatchdog>().Start();` `:1677`;
  `public override void OnMissionBehaviorInitialize(Mission mission)` `:1757`;
  `AddTaomBehavior(new Features.CompanionTactics.BattleActionBar.Hooks.BattleActionBarMissionView());` `:1853`;
  `protected override void OnApplicationTick(float dt)` `:1913` (a Patch101 target).
- `Main/Features/BattleLoadDiagnostics/BattleLoadDiagnosticsSettings.cs` (52 lines on origin; id
  `TAOM.BattleLoadDiagnostics`, `json2`): the class ends with `MemorySampleIntervalSeconds` (`:51`) and `}`.
- `BattleLoadDiagnosticsSettingsProvider.cs`: `using TAOM.Core.Validation;` `:1`;
  `private const double MaxWatchdogSeconds = 600d;` `:13`; ends with `ValidateSampleIntervalSeconds`.
- `IBattleLoadDiagnosticsSettingsProvider.cs`: ends with `double MemorySampleIntervalSeconds { get; }`.
- `Main/Features/CoopInterop/CoopSettingsRelevance.cs`: the excluded (instrumentation) name set ends
  with `"EnableEnlistmentDiagnostics", "EnableFieldCommissionDiagnostics",` then `};` (`:68-69`).
- `TAOM.Tests/Features/CoopInterop/SettingsFingerprintTests.cs:210-211`:
  `AssertSplit(typeof(TaomSettings), reflected: 229, covered: 180);` and
  `AssertSplit(typeof(BattleLoadDiagnosticsSettings), reflected: 7, covered: 0);` (earlier WPs raise both).
  `EveryDocQuotingTheSettingsCounts_AgreesWithReflection` needs the total as `**<n>**` in
  `docs/features/coop-interop.md` (`:310`, "TAOM ships **243** settings") and as ` <n> MCM settings`
  in `docs/features/bannerlord-together-compat.md` (`:291`); the same coop-interop paragraph quotes
  the per-class split (`7 in \`BattleLoadDiagnosticsSettings\``, `229 in \`TaomSettings\``) and "The 59 excluded".
- `TAOM.Tests/Features/Mcm/SettingRequireRestartPostureTests.cs:37-42`: `RestartAllowlist`, keyed
  `"Class.Property"`, first entry `TaomSettings.EnableNativeSkinFixes`.
- `TAOM.Tests/Migration/ReflectionSiteBindingTests.cs:55`: the `"RegisterBlow", "Method"` row; helpers
  resolve nested generic names (`Assembly.GetType`) and fields with all binding flags.
- `Main/Features/TimeAcceleration/ITimeControlAdapter.cs` and `TimeControlAdapter.cs`: identical to the
  pre-`f6bf0e588` 1.5.x shape plus one v1.4.8 comment; `ITimeAccelerationSettingsProvider` identical to
  `685c6a1e0` (empty diff). `TimeAccelerationIoC` registers `ITimeControlAdapter` and
  `ITimeAccelerationSettingsProvider` as singletons; `BattleLoadDiagnosticsIoC` registers
  `IBattleLoadDiagnosticsSettingsProvider`.
- Absent on origin (this plan creates them): `Main/Features/MapPerf`, `Main/Features/LostFooting`,
  `Main/Core/Diagnostics/IStampClock.cs` (WP118 may add it first, see B4), the adapters
  `IAnimationLoadingAdapter`, `INativeModuleMemoryAdapter`, `ILostFootingAdapter`, every
  `"Patch97_"`, `"Patch98_"`, `"Patch101_"` string, `tools/perf_runs.py`,
  `tools/audit_battle_equipment_memory.py`, `docs/features/{mission-perf-heartbeat,map-perf-profiler,lost-footing}.md`,
  `docs/reference/engine/mission-frame-threads-and-native-costs.md`. `tools/audit_map_scene_memory.py`
  equals `3dac7ab5b^` (empty diff). No ported file declares a `ResetForUnload`, calls `.PatchCategory(`
  or `new Harmony(`, or names a 1.5.x-only feature, except where "v1.4.8 differences" says so.

### What the 14 commits carry, and the source this plan takes

The perf batch landed on 1.5.x as a linear series (`af990ca76`, `6eb2e1b90`, `f6bf0e588`, `daba6f0a0`,
`a9f10688f`, then `346f3e093`, `a27c6b338` of WP118, `67eff55cf`, `5ee49d05f` of WP107, others, `3dac7ab5b`,
`43ca92ee9`), then the camera trio and `685c6a1e0` reworked the same files. Every one of the 14 commits
conflicts in this session's simulation (table in "Disposition"). The batch notes direct one package
against the final MissionPerf tree. So the method is an **end-state transplant**: wholly new files come
from `685c6a1e0` by a path-limited `git checkout 685c6a1e0 <files>` (MapPerf code is the same at
`43ca92ee9` and `685c6a1e0`), and every shared file gets the exact hand edit shown in its step. Later
1.5.x edits to these files belong elsewhere: `6d7e67f3f` `[ClientOnly]` (D7), `c91380274`, `a15563a3c`,
`d021426b0` (D7), `d96f349f1` (WP130), `943ab74c0` (WP125, a third map profiler category), `ad3967222` and
`4407fbc05` (WP138, `perf_runs.py`), the engine-pin rows of `ClipBudgetSignatureInstalledBinaryTests`
(WP109 holds the v1.4.8 row).

### Engine facts the ported code binds (installed v1.4.8 via `pwsh -NoProfile -File tools/taom-src.ps1 path <Type>`, this session)

| Member | v1.4.8 | Where |
|---|---|---|
| `private bool tickCompleted = true;` | set true only in `TickAgentsAndTeamsImp`, cleared in `OnTick` | `TaleWorlds.MountAndBlade.Mission.cs:1040`, `:3613`, `:3740` |
| `[MBCallback(null, false)] internal void OnPreTick(float dt)` | `WaitTickCompletion();` (`:3532`), one `OnPreMissionTick` loop (`:3535`), `TickDebugAgents()` | `:3528-3538` |
| `private void WaitTickCompletion()` | `while (!tickCompleted) Thread.Sleep(1);` | `:3585-3591` |
| `public void TickAgentsAndTeamsImp(float dt, bool tickPaused)` | `TWParallel.For` over agents, agent and team ticks, `tickCompleted = true;`, then each submodule's `AfterAsyncTickTick` | `:3601-3618` |
| `public void OnTick(float dt, float realDt, bool updateCamera, bool doAsyncAITick)` | one `OnPreDisplayMissionTick` (`:3734`), one `_missionState.Handler.UpdateCamera(this, realDt)` under `!GameNetwork.IsDedicatedServer && updateCamera` (`:3738`), `tickCompleted = false;` (`:3740`), one `OnMissionTick` (`:3743`), `OnEndMissionRequest()` (`:3761`), `TickAgentsAndTeamsAsync(dt)` or `TickAgentsAndTeamsImp(dt, tickPaused: false)` (`:3768-3775`); ends `:3776` | `:3636-3776` |
| `public Agent SpawnAgent(AgentBuildData agentBuildData, bool spawnFromAgentVisuals = false)` | **the only overload; two parameters** (the 1.5.x line's has four). Two `OnAgentBuild(Agent, Banner)` calls: mount `:4346`, rider `:4360` | `:4086` |
| `MissionBehavior` virtuals | `OnAgentBuild(Agent, Banner)` `:57`, `OnPreMissionTick(float)` `:138`, `OnPreDisplayMissionTick(float)` `:142`, `OnMissionTick(float)` `:146`, `OnEndMissionInternal()` calls `OnEndMission()` `:121-124` | `TaleWorlds.MountAndBlade.MissionBehavior.cs` |
| `[EngineCallback(null, false)] internal void ManagedScriptHolder.TickComponents(float dt)` | three `TWParallel.For(int, int, float, ParallelForWithDtAuxPredicate, int)`, one `scriptComponent.OnTick(dt)` loop, one occasional `For`: 4 + 1 sites | `TaleWorlds.Engine.ManagedScriptHolder.cs:240-267` |
| `protected internal virtual void ScriptComponentBehavior.OnTick(float dt)` | | `TaleWorlds.Engine.ScriptComponentBehavior.cs:215` |
| `TWParallel.For(int fromInclusive, int toExclusive, float deltaTime, ParallelForWithDtAuxPredicate body, int grainSize = 16)` | | `TaleWorlds.Library.TWParallel.cs:72` |
| `void IMissionSystemHandler.UpdateCamera(Mission mission, float realDt)` on `MissionScreen` | `CameraTick(mission, realDt)`, then `MBWindowManager.PreDisplay()` only while `CurrentState == 2 && !MissionEnded`; interface in namespace `TaleWorlds.MountAndBlade` | `TaleWorlds.MountAndBlade.View.Screens.MissionScreen.cs:4092-4101`; `TaleWorlds.MountAndBlade.IMissionSystemHandler.cs:15` |
| `public static bool MBAnimation.IsAnyAnimationLoadingFromDisk()` | vanilla polls it on the main thread while the mission loading screen waits (`_modules_build\Native__TaleWorlds.MountAndBlade.View.cs:16483`, v1.4.8 dump) | `TaleWorlds.MountAndBlade.MBAnimation.cs:143` |
| `Utilities.GetMainFps()`, `GetRendererFps()` | public static float | `TaleWorlds.Engine.Utilities.cs:447`, `:452` |
| `NativeOptions.NativeOptionsType` | `FrameLimiter` `:47`, `VSync` `:48`, `TextureQuality` `:53`, `ShadowmapResolution` `:54`, `ParticleDetail` `:57`; `public static float GetConfig(NativeOptionsType)` `:274` | `TaleWorlds.Engine.Options.NativeOptions.cs` |
| `Mission` members | `TeamCollection Teams` (`: List<Team>`, `:686`, `:1285`), `AgentReadOnlyList AllAgents` `:1383`, `Agents` `:1385`, `SceneName` `:1065`, `MissionEnded` `:1237`, `IsFieldBattle`/`IsSiegeBattle`/`IsSallyOutBattle` `:1373-1377`, `GetWaterLevelAtPosition(Vec2, bool)` `:2440`; `Team.FormationsIncludingEmpty` (`MBList<Formation>`, `Team.cs:34`); `Formation.CountOfUnits` `:190`, `GetOrderPositionOfUnit(Agent)` `:1239` | `Mission.cs`, `Team.cs`, `Formation.cs` |
| Campaign map targets | `internal void Campaign.RealTick(float realDt)` `:885`, `internal void Campaign.Tick()` `:954`, `public CampaignTimeControlMode GetSimplifiedTimeControlMode()` `:807`; `public override void CampaignEvents.Tick(float dt) { Instance._tickEvent.Invoke(dt); }` `:2063` (`CampaignEvents : CampaignEventReceiver`); `protected override void MapState.OnTick(float dt)` `:144` (its `OnMapModeTick` calls `RealTick` `:186` and `Tick()` `:189`); `protected override void MapScreen.OnFrameTick(float dt)` `:1332`; TAOM's `SubModule.OnApplicationTick(float)` | `Campaign.cs`, `CampaignEvents.cs`, `MapState.cs`, `SandBox.View.Map.MapScreen.cs` |
| `MbEvent<T>` internals | `private EventHandlerRec<T> _nonSerializedListenerList` `:22`; nested `internal class EventHandlerRec<TS>` with field `Next`, auto-properties `Action`, `Owner`; `public void Invoke(T t)` `:32` calls `InvokeList` (`:37-44`: no handler, the call, then `Next`) | ``TaleWorlds.CampaignSystem.MbEvent`1.cs`` |
| `CampaignTimeControlMode` | Stop 0 to FastForwardStop 6, same order as the 1.5.x comment | v1.4.8 dump `CampaignTimeControlMode.cs:3-12` |
| Lost Footing reads | `Scene.GetWaterLevel()` `:265`, `IsPositionOnADynamicNavMesh(Vec3)` `:1656`, `FindClosestExitPositionForPositionOnABoundaryFace(Vec3, UIntPtr)` `:1731`; `WorldPosition.GetNavMesh()` `:124-128` (calls `ValidateZ`, a no-op when the position is already marked valid), `GetNearestNavMesh()` `:136`, `Invalid` `:33`; `Agent.GetAIMoveDestination()` `:2032`, `GetCurrentVelocity()` (returns `Vec2`) `:2808`, `GetWorldPosition()` `:2858`, `GetAgentFlags()` `:2888`, `IsOnLand()` `:3314-3317`, `MovementMode` `:682`, `IsRunningAway` `:784`, `IsUsingGameObject` `:668`, `GetTargetPosition()` `:2619`; `AgentFlag.UnreachableViaNavMesh = 0x8000000u` (namespace `TaleWorlds.Core`); `AgentMovementMode` Land 1, WaterSurface 2, WaterDiving 3 | `Scene.cs`, `WorldPosition.cs`, `Agent.cs`, dump `Core\...\AgentFlag.cs:35`, `AgentMovementMode.cs:6-14` |
| AnimMem comment lines | `Mission.AfterStart` `:3799`: submodules' `OnMissionBehaviorInitialize` `:3815`, behaviours' `AfterStart` `:3825`; `EndMissionInternal` `:4573` calls `OnEndMissionInternal()` `:4585`; `RemoveMissionBehavior` `:4643` calls `OnRemoveBehavior()` `:4645`; `OnMissionStateFinalize` `:2207` removes every behaviour `:2219`; `MissionState.FinishMissionLoading` two 0.001 s ticks `:338-341`, `AfterStart` `:345`; `MissionState.OnFinalize` `:52` | `Mission.cs`, `MissionState.cs` |
| Managed tick entries | `Mission_TickAgentsAndTeams` shim `:936-940` (no catch); `CoreManaged.IManagedComponent.OnApplicationTick` `:120-123`; `Managed.ApplicationTick` `:289-300`, `ApplicationTickLight` `:303-313` | `ManagedCallbacks.CoreCallbacksGenerated.cs`, `TaleWorlds.MountAndBlade.CoreManaged.cs`, dump `TaleWorlds.DotNet\Managed.cs` |

Native (report V03, read-only scan of the installed `TaleWorlds.Native.dll`, 14,185,944 bytes): the
clip-budget pattern matches once; load site `0x23AADF`, budget site `0x23AB04`, counter `0xDA59B0` in
`.data`, budget float `0xB2BC9C` in `.rdata` reading `12582912.0`.

### v1.4.8 and 1.4.x differences the port must handle

| Difference | Handled in |
|---|---|
| `Mission.SpawnAgent` has two parameters on v1.4.8. Patch97's attribution class and Patch98's spawn bracket name four (`AgentBuildData, bool, Equipment, ItemObject`), so `AccessTools.Method` returns null, `HitchProbeBindingTests` fails, and in game the whole `Patch98_HitchProbe` category would throw at apply | A2.2 (RED by the binding tests), A2.3 (the fix) |
| 1.4.x has two TAOM map views with `OnMapScreenUpdate` (`FieldCampMapView`, `MomentumIndicatorMapView`); `RealmBordersMapView` is a 1.5.x feature | B1 (binding test list, comment) |
| 1.4.x has no armour gating (`ApplyGating(`), the anchor `MapFrameProfilerWiringTests` uses | B1 (the `[SaveLoad]` stamp becomes the anchor) |
| Patch89 (MapLoadDiagnostics, `[MapLoad]`) arrives with WP118 (same wave, not a dependency) | A4.1 and B1 write their "with WP118" sentences only when `docs/features/map-load-diagnostics.md` is absent from this plan's base (WP118 Part C or the integrator removes them later, see Maintenance notes); B1 then also converts a link |
| Lost Footing gates on the 1.5.x-only Fell Foes gate, excludes the 1.5.x-only Race Abilities tree, uses `IStampClock` from `346f3e093` (WP118), and its tests read `HotPathSettingsProvidersTests` (absent on 1.4.x) | B4 |
| WP106's recovery (Patch91b) is the 1.4.x remedy the 1.5.x docs leave to "the PatchShield follow-up plan" | A4 doc section |

### Conventions that bind this change

- **ADR-007 adapters**: services never touch sealed TaleWorlds types. The ported code keeps engine reads
  in adapters (`IGraphicsOptionsAdapter`, `IAnimationLoadingAdapter`, `INativeModuleMemoryAdapter`,
  `ITimeControlAdapter`, `ILostFootingAdapter`) and in `Hooks/` entry points.
- **ADR-002 thin entry points** (under 150 lines), **ADR-003** no `#region`, **ADR-004** no `[Obsolete]`,
  **ADR-005** no `#if DEBUG` outside IoC registration. The transplanted files already comply.
- **TDD**: each step adds the tests first (build or test RED), then the code (GREEN).
- **MCM json2 rule**: rename a persisted setting, never flip its default. Both lines use the settings
  ids `TAOM.BattleLoadDiagnostics` and the TaomSettings page, so a machine that ran a 1.5.x build has
  values under the 1.5.x names (report V03 section 6.3). Every key keeps its 1.5.x name and default,
  except the census, whose 1.4.x default differs and so gets a new name (`LostFootingCensusEnabled`).
- **`.claude/rules/harmony-patches.md`**: no `MovementOrder` postfix here. Every new category applies
  through `TryPatchCategory` (plan 009's source gate forbids `.PatchCategory(` in `Main`), from the
  installers, inside the once-per-process game-init block (the map profiler: before it, applying once).
- **Composition (D2)**: modules only for features that have one on 1.5.x (`AnimMemoryProbeModule`,
  `LostFootingModule`), appended at the end of `FeatureModules.All`; neither owns save data, declares a
  game model or a campaign object, or touches co-op client code. Never `IoC.Resolver` in a module.
- **Release pairing (#371)**: this plan edits `Dependencies/Foundation/PatchShieldPolicy.cs`, so a release
  containing it must ship TAOM and TAOM.Dependencies from one build.

## Commands you will need

| Purpose | Command (start every call with the variable line, then `cd $WT &&`) | Expected |
|---|---|---|
| Restore | `dotnet restore TAOM.sln` | exit 0 |
| Build | `dotnet build TAOM.sln -p:DisableModuleCopy=true -p:ModuleId= 2>&1 \| tail -3` | `0 Error(s)` (a BHA0001 Patch88 warning may print; ignore it) |
| Test | `dotnet test TAOM.Tests --no-build -p:DisableModuleCopy=true -p:ModuleId= 2>&1 \| grep -E "Passed!\|Failed!"` | `Failed: 0` |
| Filtered | `dotnet test TAOM.Tests --no-build -p:DisableModuleCopy=true -p:ModuleId= --filter "$FILTER" 2>&1 \| grep -E "Passed!\|Failed!"` | as each step states |
| Tool test | `python -m unittest discover -s tools/tests -t . -p "<file>"` | `OK` |
| Tool suite | `python -m unittest discover -s tools/tests -t . 2>&1 \| tail -3` | no new failure vs A0.6 |
| Docs | `python tools/lint_docs.py --summary --dash-base $BASE` | see Done criteria |
| Data | `python tools/validate_moduledata.py` | no new ERROR vs A0.6 (this plan changes no ModuleData) |

Both `-p:` flags are mandatory on every build and test: `Main/TAOM.csproj:133-136` does not gate the
CopyModule target on `DisableModuleCopy`, so a build without `-p:ModuleId=` deploys into the game.
Never `./build.ps1`; never two builds at once in this worktree; always build before a `--no-build` run.
Dotnet test counts an `Assert.Inconclusive` as Skipped.

## Scope

**In scope.** Part A: the `Main/Features/MissionPerf` files and tests listed in A1 and A2 (not
`AnimMemory/**`); `Main/Adapters/{IAnimationLoadingAdapter,AnimationLoadingAdapter,IGraphicsOptionsAdapter,GraphicsOptionsAdapter}.cs`;
`Main/Features/BattleLoadDiagnostics/{BattleLoadDiagnosticsSettings,BattleLoadDiagnosticsSettingsProvider,IBattleLoadDiagnosticsSettingsProvider}.cs`;
`Main/Features/BattleLoadDiagnostics/Hooks/Patch91_MissionTickStallProbes.cs` (one hunk);
`Dependencies/Foundation/PatchShieldPolicy.cs` (entries only); `Main/Features/CoopInterop/CoopSettingsRelevance.cs`;
the tests `BattleLoadDiagnosticsSettingsProviderTests.cs`, `PatchShieldPolicyTests.cs`,
`SettingsFingerprintTests.cs`, `SettingRequireRestartPostureTests.cs`, `ReflectionSiteBindingTests.cs`;
`tools/perf_runs.py`, `tools/tests/test_perf_runs.py`; docs `docs/features/{mission-perf-heartbeat,coop-interop,bannerlord-together-compat,mcm}.md`,
`docs/reference/engine/mission-frame-threads-and-native-costs.md`, `docs/reference/{harmony-patch-registry,provenance-register,bannerlord-engine-and-toolchain}.md`,
`docs/reference/taleworlds-api-snapshot/reflection-sites.md`. Part B adds: `Main/Features/MapPerf/**`,
`Main/Features/MissionPerf/AnimMemory/**` (not WP109's two files), `Main/Features/LostFooting/**`,
`Main/Adapters/{INativeModuleMemoryAdapter,NativeModuleMemoryAdapter,ILostFootingAdapter,LostFootingAdapter}.cs`,
`Main/Features/TimeAcceleration/{ITimeControlAdapter,TimeControlAdapter}.cs`, `Main/Features/TaomSettings.cs`
(one block), `Main/Composition/FeatureModules.cs` (two list lines), `Main/Core/Diagnostics/{IStampClock,StopwatchStampClock}.cs`
(only if absent), the matching tests, `tools/audit_battle_equipment_memory.py`, `tools/audit_map_scene_memory.py`,
`tools/tests/test_audit_battle_equipment_memory.py`, docs `docs/features/{map-perf-profiler,lost-footing,time-acceleration}.md`.

**Single-owner files.** `Main/SubModule.cs`: this WP edits only these anchors (the exact insertions are
in A1, A2 and B1); WP118 and WP119 edit the same file in parallel wave 2 lanes, and the integrator
resolves any merge overlap. WP119 also inserts into `OnGameEnd`: both `OnGameEnd` lines must survive
(see Maintenance notes). `Main/IoC.cs`, `Main/TAOM.csproj`, `TAOM.Tests/TAOM.Tests.csproj`: not
touched (SDK globbing picks up new files; nothing needs a registration in `IoC.cs`).

**Out of scope.** `CHANGELOG.md` (D1); `.claude/**`, `CLAUDE.md`, `AGENTS.md`, `docs/reviews/**`,
`docs/INDEX.md`, `docs/reference/feature-map.md`, `tools/README.md` (WPs 142 to 144); `docs/adrs/**`;
`plans/**`; `docs/reference/taleworlds-api-snapshot/{patch-targets,gamemodel-bases}.md` (its `-Check`
cannot pass on 1.4.x until WP137, plan 112 note); WP109's helper files and pin tables; every
CultureDoctrine, FellFoes, RaceAbilities, XmlMerge, LoadTimeStamps, MapLoadDiagnostics, NameplateCull and
co-op file; `docs/features/battle-load-diagnostics.md` and the other inventory-memory corrections of
`af990ca76` (see Disposition); the 1.5.x review records, RCAs, Codex prompts, `plans/0xx` and `plans/_audit/2026-10-02-perf`.

## Git workflow

- Worktree from the main checkout (A0.1); work and commit only there, on
  `port148/117-frame-and-memory-instruments`. Never push, open a PR, tag or merge. Git only through the
  Bash tool (D19). Never `--no-verify`, `--amend`, `git commit -C` or `-c`, or a cherry-pick that commits.
- Transplants use `git checkout <sha> <explicit file paths>` (never a directory: that would overwrite
  WP109's files with their 1.5.x pin rows). The checkout stages the files.
- Stage the rest with explicit `git add <paths>` (never `-A`, `-u`, `.`), check `git status --porcelain`,
  write the message file, then run `git commit -F C:/Users/mikew/source/repos/taom-port/117-tmp/msg-<id>.txt`
  in the NEXT sequential Bash call. A hook that denies a commit is a STOP: report its message verbatim.
- Message rules: subject `<type>(<scope>): v2.0.28 - <description>` at most 72 characters; body for
  1.4.x readers wrapped at 72, no long dashes, no `Co-Authored-By` or "Generated with" line; trailers
  `Research:`, `Not-tested:`, `Save-compat:` when relevant, then one `Ported-from: <40-hex>` per included
  1.5.x commit. The exact messages are given per step.

**Count procedure (P-COUNT), used whenever a step adds MCM settings (all are diagnostics, so the
simulation-relevant count never moves).** Read the current values:
`grep -o 'ships \*\*[0-9]*\*\*' docs/features/coop-interop.md; grep -o "TAOM's [0-9]* MCM settings" docs/features/bannerlord-together-compat.md; grep -oE '[0-9]+ in .(BattleLoadDiagnosticsSettings|TaomSettings).' docs/features/coop-interop.md; grep -oE 'The [0-9]+ excluded' docs/features/coop-interop.md; grep -n 'AssertSplit(typeof(BattleLoadDiagnosticsSettings)\|AssertSplit(typeof(TaomSettings)' TAOM.Tests/Features/CoopInterop/SettingsFingerprintTests.cs`.
Raise, with the Edit tool, by the number of settings the step adds: the `**<n>**` total and the
`<n> MCM settings` total, the class's split number, the `The <n> excluded` number, and the class's
`reflected:` value in `AssertSplit` (`covered:` stays). Verify with
`FILTER="FullyQualifiedName~SettingsFingerprintTests"`: `Failed: 0`.

## Decisions taken in this plan

1. **The hitch probe ships ON** ("Enable Hitch Probe", the 1.5.x key and default, so json2 needs no
   rename). Cost: the 1.5.x line measured 0.70 to 0.75 us per simulated frame for the six managed
   brackets (`HitchProbeOverheadBenchmarkTests`, desktop, 2026-10-15, recorded in the 1.5.x feature doc's
   "Cost") against its own 50 us budget (0.5% of a 10 ms frame); the native clip-loading sample runs only
   when its median over 32 calls at the first measured mission is within 20 us. A2.7 re-measures on this
   laptop and STOPs at 50 us or more. Lines: a `[Hitch]` pair only for frames of 250 ms or more (the first
   100 per mission), plus per-window lines while measuring (the classifier estimated 2 to 4 INFO lines every
   5 s; in-game check 3 counts them). Value: the only per-player record of where a slow battle frame went.
   Not covered: a frame that never ends writes nothing; WP106's `[MissionStall]` watchdog covers that.
   Accepted risk: the clip-loading sample walks the engine's clip list from the main thread with no lock
   while a worker may load a clip; its safety is UNVERIFIED on both lines (vanilla polls the same function
   on the main thread during the loading screen). In-game check 2 watches for it.
2. **The clip memory probe ships ON** (1.5.x key and default): read-only, once a second, from addresses
   checked against the module's own section table; it turns itself off on any mismatch, and V03 verified
   the v1.4.8 pattern and row.
3. **The Lost Footing census ships OFF** under the new key `LostFootingCensusEnabled`: it reads up to 64
   agents per frame in every field battle, maps to no 1.4.x player symptom, and should not add noise while
   the battle freezes are investigated. The 1.5.x key `LostFootingCensus` defaults on, so reusing it with
   a flipped default would break the json2 rule.

## Part A: mission-frame instruments

### Step A0: worktree, prerequisites, baseline

**A0.1** `cd /c/Users/mikew/source/repos/TAOM && git worktree add /c/Users/mikew/source/repos/taom-port/117 -b port148/117-frame-and-memory-instruments bannerlord-1.4.5 && mkdir -p /c/Users/mikew/source/repos/taom-port/117-tmp && git -C /c/Users/mikew/source/repos/taom-port/117 rev-parse HEAD > /c/Users/mikew/source/repos/taom-port/117-tmp/base.txt`
**Verify**: `Preparing worktree (new branch ...)`. An existing branch or folder: STOP.

**A0.2** Drift check (header). Read every change; let A0.3 and A0.4 decide.

**A0.3** Prerequisites:

```bash
WT=/c/Users/mikew/source/repos/taom-port/117; cd $WT && \
for f in Main/Composition/FeatureModules.cs TAOM.Tests/Infrastructure/RepoPaths.cs Main/Features/BattleLoadDiagnostics/Hooks/Patch91_MissionTickStallProbes.cs Main/Features/MissionPerf/TickProfilerTranspiler.cs Main/Features/MissionPerf/TickProfileLines.cs Main/Features/MissionPerf/AnimMemory/ClipBudgetSignature.cs Main/Features/MissionPerf/AnimMemory/PeSectionTable.cs TAOM.Tests/Features/MissionPerf/TickProfilerTranspilerTests.cs TAOM.Tests/Features/MissionPerf/TickProfilerTranspilerOccurrenceTests.cs TAOM.Tests/Features/MissionPerf/AnimMemory/SyntheticNativeImage.cs TAOM.Tests/Features/MissionPerf/AnimMemory/ClipBudgetSignatureInstalledBinaryTests.cs TAOM.Tests/Migration/NativePinGateTests.cs Main/Adapters/IGraphicsOptionsAdapter.cs Main/Adapters/GraphicsOptionsAdapter.cs; do test -f $f && echo "ok $f" || echo "MISSING $f"; done; \
grep -cF 'private bool TryPatchCategory(string category)' Main/SubModule.cs; \
grep -cF 'FeatureModuleHooks.AddMissionBehaviors(mission, AddTaomBehavior);' Main/SubModule.cs; \
grep -cF 'TryPatchCategory("Patch91_MissionTickStall");' Main/SubModule.cs; \
grep -cF 'IoC.Resolve<Features.BattleLoadDiagnostics.MissionTickStallWatchdog>().Start();' Main/SubModule.cs; \
grep -cF 'public static void Prefix() => MissionTickStallProbe.AsyncAgentTick.Enter();' Main/Features/BattleLoadDiagnostics/Hooks/Patch91_MissionTickStallProbes.cs; \
grep -cF 'public static void Finalizer() => MissionTickStallProbe.AsyncAgentTick.Exit();' Main/Features/BattleLoadDiagnostics/Hooks/Patch91_MissionTickStallProbes.cs; \
grep -c 'bool MissionTickStallSamplerEnabled' Main/Features/BattleLoadDiagnostics/IBattleLoadDiagnosticsSettingsProvider.cs; \
grep -cF '"TaleWorlds.MountAndBlade.Mission.TickAgentsAndTeamsImp",' Dependencies/Foundation/PatchShieldPolicy.cs; \
grep -cE 'public static (bool IsExcludedTargetMethod|bool IsExcludedTargetNamespace|string FormatHotMethodSkip)' Dependencies/Foundation/PatchShieldPolicy.cs; \
grep -c 'WP117' Main/Features/MissionPerf/TickProfileLines.cs; \
grep -c '0x23AADF' TAOM.Tests/Features/MissionPerf/AnimMemory/ClipBudgetSignatureInstalledBinaryTests.cs; \
grep -cE 'int BattleSizeOption \{ get; \}|FrameLimiterFps' Main/Adapters/IGraphicsOptionsAdapter.cs; \
grep -cF 'Register<IGraphicsOptionsAdapter, GraphicsOptionsAdapter>(Reuse.Singleton);' Main/Features/BattleCorpses/BattleCorpsesModule.cs; \
grep -cF 'new Features.BattleCorpses.BattleCorpsesModule(),' Main/Composition/FeatureModules.cs; \
grep -c 'MissionTickRecoveryWiring.VerifyAndArm' Main/SubModule.cs; \
test -f docs/features/mission-tick-guard.md && echo guard-doc-ok || echo guard-doc-absent; \
ls -d Main/Features/MapPerf Main/Features/LostFooting Main/Features/MissionPerf/FrameStats.cs docs/features/mission-perf-heartbeat.md 2>&1 | grep -c "No such"; \
git grep -nE '"Patch9[78]_|"Patch101_' -- Main | wc -l
```

**Verify**: 14 `ok` lines; then `1`, `1`, `1`, `1`, `1`, `1`, `1`, `1`, `3`, `1`, `1`, `1` (the
`BattleSizeOption` line only), `1`, `1`; then `1` (or `0` if WP106 Part B is not merged: then A2 anchors on
the watchdog line), `guard-doc-ok` (or `guard-doc-absent`: A4 says what changes); then `4`; then `0`.
Any `MISSING`, any other count: STOP and report the output.

**A0.4** Anchors, each exactly once in `Main/SubModule.cs`, in this order:

```bash
WT=/c/Users/mikew/source/repos/taom-port/117; cd $WT && for a in 'public override void OnGameEnd(Game game)' 'protected override void OnGameStart(Game game, IGameStarter gameStarterObject)' 'StampSaveLoadPhase(Features.SaveLoadDiagnostics.Domain.SaveLoadPhase.GameInitializationFinished);' '// Harmony patches are process-global (applied to methods, persist across games). Apply this' 'if (_gameInitPatchesApplied) return;' '_gameInitPatchesApplied = true;' 'TryPatchCategory("Patch91_MissionTickStall");' 'public override void OnMissionBehaviorInitialize(Mission mission)' 'AddTaomBehavior(new Features.CompanionTactics.BattleActionBar.Hooks.BattleActionBarMissionView());' 'protected override void OnApplicationTick(float dt)' 'using TAOM.Adapters;'; do printf '%s:%s  %s\n' "$(grep -cF "$a" Main/SubModule.cs)" "$(grep -nF "$a" Main/SubModule.cs | head -1 | cut -d: -f1)" "$a"; done
```

**Verify**: every line starts `1:`, and the line numbers rise in the listed order from `StampSaveLoadPhase`
to `BattleActionBarMissionView` (`OnGameEnd` before `OnGameStart`). Otherwise STOP.

**A0.5** Baseline: Restore, Build, Test. Record **T0** (Total, Passed, Skipped). Any failure: STOP.

**A0.6** `python tools/lint_docs.py --summary --dash-base $BASE > $AUX/lint0.txt; cat $AUX/lint0.txt`;
`python -m unittest discover -s tools/tests -t . > $AUX/tools0.txt 2>&1; tail -3 $AUX/tools0.txt`;
`python tools/validate_moduledata.py > $AUX/data0.txt 2>&1; tail -5 $AUX/data0.txt`. Keep the three files.

### Step A1: the `[MissionPerf]` heartbeat

Sources: `5dfa8f4cb` (heartbeat extract only; CultureDoctrine is OUT), with `67eff55cf`'s two comment hunks
and `685c6a1e0`'s busy-time hunks already in the `685c6a1e0` copies. Method: transplant plus hand edits.

**A1.1 RED.** `cd $WT && git checkout 685c6a1e0 TAOM.Tests/Features/MissionPerf/FrameStatsTests.cs TAOM.Tests/Features/MissionPerf/MissionPerfHeartbeatBehaviorTests.cs`.
**RED check** (used by every RED step below):
`cd $WT && dotnet build TAOM.sln -p:DisableModuleCopy=true -p:ModuleId= > $AUX/build.log 2>&1; echo exit=$?; grep -c "error CS" $AUX/build.log; grep "error CS" $AUX/build.log | grep -vc 'TAOM.Tests'`
prints `exit=1`, a count above 0, then `0` (every error sits in the tests just added: here `CS0246`/`CS0234`
for the heartbeat types or the `TAOM.Features.MissionPerf.Hooks` namespace). Anything else: STOP.

**A1.2 GREEN.**

1. `cd $WT && git checkout 685c6a1e0 Main/Features/MissionPerf/FrameStats.cs Main/Features/MissionPerf/MissionPerfLine.cs Main/Features/MissionPerf/Hooks/MissionPerfHeartbeatBehavior.cs`
2. `BattleLoadDiagnosticsSettings.cs`: insert before the class's closing `}` (the file's last line):

```csharp

    [SettingPropertyGroup("Mission Performance")]
    [SettingPropertyBool("Enable Mission Frame-Time Heartbeat", Order = 0, RequireRestart = false,
        HintText = "Writes a [MissionPerf] line to the TAOM debug log every 5 seconds while a mission runs: frames, fps, average / p95 / max frame time in ms, agent and formation counts, garbage collections, and the engine's main-thread and render-thread busy times (mainMs, renderMs). This is the in-mission counterpart of [MemSample] and the measurement an AI or content change is judged against. Cost is one timestamp and one GetMainFps read per frame. Default ON.")]
    public bool EnableMissionPerfHeartbeat { get; set; } = true;
```

3. `CoopSettingsRelevance.cs`: directly before the `};` that closes the set holding
   `"EnableFieldCommissionDiagnostics",` (later WPs may have appended lines; insert at the set's end):

```csharp
        // The [MissionPerf] frame-time heartbeat (plan 117): a log line, never a computation.
        "EnableMissionPerfHeartbeat",
```

4. P-COUNT with one setting (`BattleLoadDiagnosticsSettings`).
5. `Main/SubModule.cs`: directly before
   `        AddTaomBehavior(new Features.CompanionTactics.BattleActionBar.Hooks.BattleActionBarMissionView());` insert:

```csharp
        // [MissionPerf] frame-time heartbeat every 5 s (plan 117): the in-mission frame-time record the
        // battle freeze and stutter reports lack. Self-gates on its Battle Load Diagnostics toggle.
        AddTaomBehavior(new Features.MissionPerf.Hooks.MissionPerfHeartbeatBehavior(IoC.Resolve<IModLogger>()));
```

**Verify**: Build `0 Error(s)`; Filtered
`FILTER="FullyQualifiedName~FrameStatsTests|FullyQualifiedName~MissionPerfHeartbeatBehaviorTests|FullyQualifiedName~SettingsFingerprintTests|FullyQualifiedName~SettingRequireRestartPostureTests|FullyQualifiedName~TAOM.Tests.Composition"`:
`Failed: 0`; Test: Total T0 + 14, `Failed: 0`, Skipped as T0. (`FrameStatsTests` has 13 methods,
`MissionPerfHeartbeatBehaviorTests` 1; if the total differs, report the filtered counts before going on.)

**A1.3 Commit A1.** `git add Main/Features/MissionPerf/FrameStats.cs Main/Features/MissionPerf/MissionPerfLine.cs Main/Features/MissionPerf/Hooks/MissionPerfHeartbeatBehavior.cs TAOM.Tests/Features/MissionPerf/FrameStatsTests.cs TAOM.Tests/Features/MissionPerf/MissionPerfHeartbeatBehaviorTests.cs Main/Features/BattleLoadDiagnostics/BattleLoadDiagnosticsSettings.cs Main/Features/CoopInterop/CoopSettingsRelevance.cs TAOM.Tests/Features/CoopInterop/SettingsFingerprintTests.cs docs/features/coop-interop.md docs/features/bannerlord-together-compat.md Main/SubModule.cs`;
`git status --porcelain` lists exactly these 11. `msg-A1.txt`:

```text
feat(mission-perf): v2.0.28 - [MissionPerf] frame-time heartbeat

Every mission now writes one [MissionPerf] line to the TAOM debug log
every five seconds: frames, fps, average, p95 and max frame time,
agent and formation counts, garbage collections, and the engine's
main and render busy times. The 1.4.8 line had no in-mission frame
record, so a battle that stutters or freezes left no number behind;
the line also stops when the mission tick stops. MCM: Battle Load
Diagnostics, Mission Performance, "Enable Mission Frame-Time
Heartbeat", on, the same key and default as the 1.5.x line.

Research: taom-src v1.4.8 Utilities.GetMainFps and GetRendererFps
(Utilities.cs:447-454), Mission.AllAgents, Agents, Teams,
Team.FormationsIncludingEmpty, Formation.CountOfUnits
Not-tested: the [MissionPerf] line in a live battle
Save-compat: no saved state; one new MCM key, default on
Ported-from: 5dfa8f4cb0a9e86118ed41b0c6bdd3d9a977990b
Ported-from: 67eff55cf9cace45727b2324a79555d05ec927b2
Ported-from: 685c6a1e03f799567d5c5c5e93eae052900f767b
```

### Step A2: tick profiler, hitch probe, attribution, camera timing, `[PerfContext]`

Sources: `6eb2e1b90`, `daba6f0a0`, the MissionPerf half of `43ca92ee9`, `0273e2ea4`, `c76125773`, the Patch98
comment of `fdee18f02`, the profiler half of `685c6a1e0`. Method: transplant from `685c6a1e0` plus hand edits.

**A2.1 RED (tests).**

1. Transplant 26 test files:

```bash
cd $WT && git checkout 685c6a1e0 TAOM.Tests/Features/MissionPerf/{AllocationCounterTests,AnimLoadingSamplerTests,BehaviorTickTableTests,HitchProbeBindingTests,HitchProbeHooksTests,HitchProbeInstallerTests,HitchProbeLinesTests,HitchProbeMissionFlowTests,HitchProbeOverheadBenchmarkTests,HitchProbePrefixGuardTests,HitchProbeSimulatedFrameTests,HitchProbeWiringTests,HookHealthTests,MissionAttributionHooksTests,MissionTickProfilerBehaviorProbeTests,MissionTickProfilerBehaviorTests,MissionTickProfilerBindingTests,MissionTickProfilerHooksTests,MissionTickProfilerInstallerTests,MissionTickProfilerProbeTests,MissionTickProfilerTests,MissionTickProfilerWiringTests,OpenDelegateDispatchTests,ProbeCostMeterTests,ProbeWindowWriterTests,TickProfileLinesTests}.cs && git status --porcelain | grep -c '^A '
```

   Expected `26`.
2. `TAOM.Tests/Features/BattleLoadDiagnostics/BattleLoadDiagnosticsSettingsProviderTests.cs`: insert before
   the class's closing `}` (11 tests, from `6eb2e1b90` and `daba6f0a0`):

```csharp

    // The tick profiler installs Harmony patches, so its toggle fails CLOSED when MCM is not ready,
    // unlike every sibling above.
    [TestMethod]
    public void TickProfilerEnabled_NoMcmInstance_DefaultsFalse()
    {
        var sut = new BattleLoadDiagnosticsSettingsProvider();
        Assert.IsFalse(sut.TickProfilerEnabled);
    }

    // The hitch probe installs Patch98, so it fails CLOSED when MCM is not ready, like the tick profiler,
    // although its compiled MCM default is on.
    [TestMethod]
    public void HitchProbeEnabled_NoMcmInstance_DefaultsFalse()
    {
        var sut = new BattleLoadDiagnosticsSettingsProvider();
        Assert.IsFalse(sut.HitchProbeEnabled);
    }

    [TestMethod]
    public void EnableHitchProbe_CompiledDefault_IsTrue()
    {
        Assert.IsTrue(new BattleLoadDiagnosticsSettings().EnableHitchProbe);
    }

    [TestMethod]
    public void TickProfilerTopN_NoMcmInstance_Defaults8()
    {
        var sut = new BattleLoadDiagnosticsSettingsProvider();
        Assert.AreEqual(8, sut.TickProfilerTopN);
    }

    [TestMethod]
    public void HitchThresholdMs_NoMcmInstance_Defaults250()
    {
        var sut = new BattleLoadDiagnosticsSettingsProvider();
        Assert.AreEqual(250d, sut.HitchThresholdMs);
    }

    [TestMethod]
    public void ValidateTickProfilerTopN_OutOfRange_Returns8()
    {
        Assert.AreEqual(8, BattleLoadDiagnosticsSettingsProvider.ValidateTickProfilerTopN(0));
        Assert.AreEqual(8, BattleLoadDiagnosticsSettingsProvider.ValidateTickProfilerTopN(21));
        Assert.AreEqual(8, BattleLoadDiagnosticsSettingsProvider.ValidateTickProfilerTopN(-5));
    }

    [TestMethod]
    public void ValidateTickProfilerTopN_RangeEdges_ReturnRaw()
    {
        Assert.AreEqual(1, BattleLoadDiagnosticsSettingsProvider.ValidateTickProfilerTopN(1));
        Assert.AreEqual(20, BattleLoadDiagnosticsSettingsProvider.ValidateTickProfilerTopN(20));
    }

    [TestMethod]
    public void ValidateHitchThresholdMs_NaN_Returns250()
        => Assert.AreEqual(250d, BattleLoadDiagnosticsSettingsProvider.ValidateHitchThresholdMs(double.NaN));

    [TestMethod]
    public void ValidateHitchThresholdMs_Infinity_Returns250()
    {
        Assert.AreEqual(250d, BattleLoadDiagnosticsSettingsProvider.ValidateHitchThresholdMs(double.PositiveInfinity));
        Assert.AreEqual(250d, BattleLoadDiagnosticsSettingsProvider.ValidateHitchThresholdMs(double.NegativeInfinity));
    }

    [TestMethod]
    public void ValidateHitchThresholdMs_OutOfRange_Returns250()
    {
        Assert.AreEqual(250d, BattleLoadDiagnosticsSettingsProvider.ValidateHitchThresholdMs(49d));
        Assert.AreEqual(250d, BattleLoadDiagnosticsSettingsProvider.ValidateHitchThresholdMs(2001d));
    }

    [TestMethod]
    public void ValidateHitchThresholdMs_RangeEdges_ReturnRaw()
    {
        Assert.AreEqual(50d, BattleLoadDiagnosticsSettingsProvider.ValidateHitchThresholdMs(50d));
        Assert.AreEqual(2000d, BattleLoadDiagnosticsSettingsProvider.ValidateHitchThresholdMs(2000d));
    }
```

3. `TAOM.Tests/Infrastructure/Dependencies/PatchShieldPolicyTests.cs`: first
   `grep -c 'MissionTickTargetsKeptOffTheShield\|MissionOnTickOnPreTickAndSpawnAgent' TAOM.Tests/Infrastructure/Dependencies/PatchShieldPolicyTests.cs`
   must print `0` (else skip this item and say so in the hand-off). Insert before the class's closing `}`
   (`daba6f0a0`'s two tests with `43ca92ee9`'s comment, re-worded for 1.4.x):

```csharp

    // --- Mission tick targets: which stay off the shield (decision D13 of the 1.5.x line, 2026-10-03) -----
    // Since plan 034 (WP107) the shield's finalizer is cheap, so cost alone no longer keeps a target off it:
    // Mission.OnTick, Mission.OnPreTick and Mission.SpawnAgent stay shielded. The three that stay off would
    // break the frame if a swallow skipped their body (or, for the script tick, stay as built); the first two
    // stop a swallow only at their own method, since their callers are shielded. The binding tests
    // (MissionTickProfilerBindingTests, HitchProbeBindingTests) check the same split against the installed
    // engine's real targets; these two need no game.

    [TestMethod]
    public void IsExcludedTargetMethod_MissionTickTargetsKeptOffTheShield_ReturnTrue()
    {
        Assert.IsTrue(PatchShieldPolicy.IsExcludedTargetMethod("TaleWorlds.MountAndBlade.Mission", "TickAgentsAndTeamsImp"));
        Assert.IsTrue(PatchShieldPolicy.IsExcludedTargetMethod("TaleWorlds.MountAndBlade.Mission", "WaitTickCompletion"));
        Assert.IsTrue(PatchShieldPolicy.IsExcludedTargetMethod("TaleWorlds.Engine.ManagedScriptHolder", "TickComponents"));
    }

    [TestMethod]
    public void IsExcludedTargetMethod_MissionOnTickOnPreTickAndSpawnAgent_ReturnFalse()
    {
        foreach (var name in new[] { "OnTick", "OnPreTick", "SpawnAgent" })
        {
            Assert.IsFalse(PatchShieldPolicy.IsExcludedTargetMethod("TaleWorlds.MountAndBlade.Mission", name),
                $"Mission.{name} was excluded for cost alone and is shielded again (D13)");
        }
    }
```

4. `TAOM.Tests/Migration/ReflectionSiteBindingTests.cs`: directly after the `[DataRow(...)]` line that
   contains `"RegisterBlow", "Method"` insert:

```csharp
    // --- MissionPerf tick profiler: the private wait it times at its call site (MissionTickProfilerInstaller.cs) ---
    [DataRow("TaleWorlds.MountAndBlade.Mission", "Mission", "WaitTickCompletion", "Method", "MissionTickProfilerInstaller.cs:31,54")]
    // --- MissionPerf attribution: the protected internal script tick it binds and swaps (MissionAttributionInstaller.cs) ---
    [DataRow("TaleWorlds.Engine.ScriptComponentBehavior", "ScriptComponentBehavior", "OnTick", "Method", "MissionAttributionInstaller.cs:35,66")]
```

5. `TAOM.Tests/Features/Mcm/SettingRequireRestartPostureTests.cs`: in `RestartAllowlist`, directly after the
   `EnableNativeSkinFixes` entry, insert:

```csharp
        [$"{nameof(BattleLoadDiagnosticsSettings)}.{nameof(BattleLoadDiagnosticsSettings.EnableTickProfiler)}"] = "Read at the first game init, where Patch97 installs or is skipped: turning it on needs a restart (turning it off applies from the next mission)",
        [$"{nameof(BattleLoadDiagnosticsSettings)}.{nameof(BattleLoadDiagnosticsSettings.EnableHitchProbe)}"] = "Read at the first game init, where Patch98 installs or is skipped: turning it on needs a restart (turning it off applies from the next mission while the tick profiler is off)",
```

**Verify (RED)**: the RED check of A1.1 (errors name MissionPerf types and the provider members of A2.2).

**A2.2 GREEN (code).**

1. Check the file A2 replaces: `grep -c 'WP117' Main/Features/MissionPerf/TickProfileLines.cs` prints `1`
   (WP109's trimmed file). Then transplant 31 files (29 MissionPerf files, overwriting that one, plus two adapters):

```bash
cd $WT && git checkout 685c6a1e0 Main/Features/MissionPerf/{AllocationCounter,AnimLoadingSampler,BehaviorTickTable,HitchProbeLines,HookHealth,MissionTickProfiler,ProbeBracket,ProbeCostMeter,ProbeDelegates,ProbeLineData,ProbeWindowWriter,TickProfileLines}.cs Main/Features/MissionPerf/Hooks/{HitchProbeHooks.Camera,HitchProbeHooks.Diagnostics,HitchProbeHooks.Script,HitchProbeHooks.Spawn,HitchProbeHooks,HitchProbeInstaller,MissionAttributionHooks,MissionAttributionInstaller,MissionTickProfilerBehavior,MissionTickProfilerHealth,MissionTickProfilerHooks.Probe,MissionTickProfilerHooks,MissionTickProfilerInstaller,Patch97_MissionAttribution,Patch97_MissionTickProfiler,Patch98_HitchProbe,PerfContextReader}.cs Main/Adapters/{AnimationLoadingAdapter,IAnimationLoadingAdapter}.cs && git diff --cached --stat | tail -1
```

   Expected: `57 files changed` (26 tests from A2.1 plus these 31; A2.1's hand edits are not staged yet).

2. `Main/Adapters/IGraphicsOptionsAdapter.cs` (`6eb2e1b90` and `685c6a1e0`; leave WP111's lines): replace the
   summary line `/// passed through raw.` with the two lines
   `/// passed through raw. It also reports the frame limiter, VSync, texture, shadow-map and particle` and
   `/// quality options for the tick profiler's <c>[PerfContext]</c> line.`; after `    int BattleSizeOption { get; }` insert:

```csharp

    /// <summary>The in-game frame limiter's fps (UI range 30 to 360); -1 when unreadable.</summary>
    int FrameLimiterFps { get; }

    /// <summary>VSync: 0 None, 1 Full, 2 Half; -1 when unreadable.</summary>
    int VSyncOption { get; }

    /// <summary>Texture Quality: the raw option value the engine stores, rounded; -1 when unreadable.</summary>
    int TextureQualityOption { get; }

    /// <summary>Shadow-map Resolution: the raw option value the engine stores, rounded; -1 when unreadable.</summary>
    int ShadowmapResolutionOption { get; }

    /// <summary>Particle Detail: the raw option value the engine stores, rounded; -1 when unreadable.</summary>
    int ParticleDetailOption { get; }
```

3. `Main/Adapters/GraphicsOptionsAdapter.cs`: after `    private const int MaxOptionIndex = 5;` insert the
   first block, after `    public int BattleSizeOption => BannerlordConfig.BattleSize;` the second:

```csharp

    // A sanity bound that rejects garbage from the engine store, not the length of any options list.
    private const int MaxRawOptionIndex = 16;

    private const int MaxFrameLimiterFps = 1000;
```

```csharp

    public int FrameLimiterFps => ToOptionIndex(NativeOptions.GetConfig(NativeOptions.NativeOptionsType.FrameLimiter), MaxFrameLimiterFps);

    public int VSyncOption => ToOptionIndex(NativeOptions.GetConfig(NativeOptions.NativeOptionsType.VSync), MaxRawOptionIndex);

    public int TextureQualityOption => ToOptionIndex(NativeOptions.GetConfig(NativeOptions.NativeOptionsType.TextureQuality), MaxRawOptionIndex);

    public int ShadowmapResolutionOption => ToOptionIndex(NativeOptions.GetConfig(NativeOptions.NativeOptionsType.ShadowmapResolution), MaxRawOptionIndex);

    public int ParticleDetailOption => ToOptionIndex(NativeOptions.GetConfig(NativeOptions.NativeOptionsType.ParticleDetail), MaxRawOptionIndex);
```

4. `BattleLoadDiagnosticsSettings.cs`: directly after the `EnableMissionPerfHeartbeat` property (A1) insert
   (final 1.5.x texts, except the Top Behaviours hint, which B1 widens when the map profiler lands):

```csharp

    [SettingPropertyGroup("Mission Performance")]
    [SettingPropertyBool("Enable Tick Profiler", Order = 1, RequireRestart = true,
        HintText = "Off by default. Adds per-type attribution to the hitch probe's lines: times every mission behaviour's tick, every behaviour's spawn callback and every scene script component by type, and lists the slowest. Installed once at game start: turning it on takes effect after a restart; turning it off stops it from the next mission.")]
    public bool EnableTickProfiler { get; set; } = false;

    [SettingPropertyGroup("Mission Performance")]
    [SettingPropertyInteger("Tick Profiler Top Behaviours", 1, 20, Order = 2, RequireRestart = false,
        HintText = "How many mission behaviours each [TickProfile] and [TickSummary] line lists, slowest first. Default 8. Read at each mission start.")]
    public int TickProfilerTopN { get; set; } = 8;

    [SettingPropertyGroup("Mission Performance")]
    [SettingPropertyInteger("Hitch Threshold (ms)", 50, 2000, Order = 3, RequireRestart = false,
        HintText = "A frame that takes this many milliseconds or more writes one [Hitch] line naming where the frame went, for the first 100 such frames of a mission; later ones are only counted. Below the battle's usual frame time, nearly every frame is a hitch. Default 250. Read at each mission start.")]
    public int HitchThresholdMs { get; set; } = 250;

    [SettingPropertyGroup("Mission Performance")]
    [SettingPropertyBool("Enable Hitch Probe", Order = 4, RequireRestart = true,
        HintText = "On by default. Times the parts of every mission frame TAOM can see (the wait for the agent tick, the mission tick, the camera and render preparation, scene scripts, agent spawns) and checks whether an animation clip is loading from disk, so each frame slower than the hitch threshold writes a hitch line and a detail line saying where the time went. Its measured cost is written to the TAOM debug log. Installed once at game start: turning it on takes effect after a restart; turning it off stops measuring from the next mission while 'Enable Tick Profiler' is off (the profiler measures through the probe's patches).")]
    public bool EnableHitchProbe { get; set; } = true;
```

5. `IBattleLoadDiagnosticsSettingsProvider.cs`: insert before the interface's closing `}`:

```csharp

    /// <summary>The Patch97 tick profiler's toggle. Unlike its siblings it fails CLOSED to false when
    /// MCM is not ready, because it installs Harmony patches on the two hottest mission methods. Read at
    /// the first game init, where Patch97 installs or is skipped, and again at each mission start: turning
    /// it on needs a restart, turning it off stops behaviour timing from the next mission (the patches stay,
    /// and the hitch probe, when installed and on, still measures).</summary>
    bool TickProfilerEnabled { get; }

    /// <summary>How many behaviours a [TickProfile] or [TickSummary] line lists (validated 1-20,
    /// default 8); read at each mission start.</summary>
    int TickProfilerTopN { get; }

    /// <summary>The frame time at or above which the profiler writes a [Hitch] line (validated
    /// 50-2000 ms, default 250); read at each mission start.</summary>
    double HitchThresholdMs { get; }

    /// <summary>The Patch98 hitch probe's toggle. Fails CLOSED to false when MCM is not ready, because it
    /// installs Harmony patches; the compiled MCM default is true. Read once per process at the first game
    /// init, where Patch98 installs or is skipped, and at each mission start.</summary>
    bool HitchProbeEnabled { get; }
```

6. `BattleLoadDiagnosticsSettingsProvider.cs`: after `    private const double MaxWatchdogSeconds = 600d;` insert
   the constants; before the class's closing `}` insert the members:

```csharp
    private const int DefaultTickProfilerTopN = 8;
    private const int MinTickProfilerTopN = 1;
    private const int MaxTickProfilerTopN = 20;
    private const double DefaultHitchThresholdMs = 250d;
    private const double MinHitchThresholdMs = 50d;
    private const double MaxHitchThresholdMs = 2000d;
```

```csharp

    // Fail-CLOSED, unlike the getters above: this toggle installs Patch97 on Mission.OnTick and
    // Mission.OnPreTick, so "MCM not ready" must never install it.
    public bool TickProfilerEnabled =>
        BattleLoadDiagnosticsSettings.Instance?.EnableTickProfiler ?? false;

    // Fail-CLOSED for the same reason: it installs Patch98 (the hitch probe).
    public bool HitchProbeEnabled => BattleLoadDiagnosticsSettings.Instance?.EnableHitchProbe ?? false;

    public int TickProfilerTopN =>
        ValidateTickProfilerTopN(BattleLoadDiagnosticsSettings.Instance?.TickProfilerTopN ?? DefaultTickProfilerTopN);

    public double HitchThresholdMs =>
        ValidateHitchThresholdMs(BattleLoadDiagnosticsSettings.Instance?.HitchThresholdMs ?? (int)DefaultHitchThresholdMs);

    internal static int ValidateTickProfilerTopN(int raw) =>
        raw >= MinTickProfilerTopN && raw <= MaxTickProfilerTopN ? raw : DefaultTickProfilerTopN;

    internal static double ValidateHitchThresholdMs(double raw) =>
        FiniteFloatValidator.IsFiniteInRange(raw, MinHitchThresholdMs, MaxHitchThresholdMs)
            ? raw
            : DefaultHitchThresholdMs;
```

7. `Patch91_MissionTickStallProbes.cs` (`6eb2e1b90`'s hunk): add `using TAOM.Features.MissionPerf.Hooks;` after
   `using HarmonyLib;`; in `Mission_TickAgentsAndTeamsImp_StallProbe_Patch` replace
   `    public static void Prefix() => MissionTickStallProbe.AsyncAgentTick.Enter();` with
   `    public static void Prefix() { MissionTickStallProbe.AsyncAgentTick.Enter(); MissionTickProfilerHooks.OnAgentTickEnter(); }`
   and `    public static void Finalizer() => MissionTickStallProbe.AsyncAgentTick.Exit();` with
   `    public static void Finalizer() { MissionTickStallProbe.AsyncAgentTick.Exit(); MissionTickProfilerHooks.OnAgentTickExit(); }`,
   and add above `[HarmonyPrefix]` of that class the comment
   `    // The Patch97 tick profiler reads the same bracket for agentTickMs (a static null check when it is not installed).`
   Keep every other attribute WP106 put there (priority, ordering) unchanged.
8. `Dependencies/Foundation/PatchShieldPolicy.cs`: directly after
   `        "TaleWorlds.MountAndBlade.Mission.TickAgentsAndTeamsImp",` insert (WP107's comment above stays):

```csharp
        // The hitch probe's two kept targets (the 1.5.x line's decision D13, 2026-10-03; ported by plan 117). Since
        // plan 034's finalizer change the shield costs little, so cost alone keeps no target off it: Mission.OnTick,
        // Mission.OnPreTick and Mission.SpawnAgent stay shielded. These two stay off, for any owner's patch on them
        // (the missing-API swallow and the strip of the offending patch are given up):
        //  - Mission.WaitTickCompletion (v1.4.8 Mission.cs:3585-3591) runs once per frame on the main thread, and a
        //    swallow there would skip its wait loop, so OnPreMissionTick would overlap the running agent tick.
        //  - ManagedScriptHolder.TickComponents (ManagedScriptHolder.cs:240-267) runs once per ticking scene per
        //    frame, on a thread native picks, and carries Patch97's attribution transpiler when the profiler is on;
        //    it stays as built.
        // Both stop a swallow only at their own method; the callers (OnPreTick for the wait) are shielded.
        // HitchProbeBindingTests walks the real targets; PatchShieldPolicyTests pins the split without the game.
        "TaleWorlds.MountAndBlade.Mission.WaitTickCompletion",
        "TaleWorlds.Engine.ManagedScriptHolder.TickComponents",
```

9. `Main/SubModule.cs`, game init: directly after the line
   `        Features.MissionTickGuard.Hooks.MissionTickRecoveryWiring.VerifyAndArm(IoC.Resolve<IModLogger>());`
   (A0.3 printed `1`), or, if A0.3 printed `0` for it, directly after
   `        IoC.Resolve<Features.BattleLoadDiagnostics.MissionTickStallWatchdog>().Start();`, insert:

```csharp

        // Patch98 hitch probe (default on) and Patch97 tick profiler (default off), plan 117: the probe brackets
        // the frame's phases, scene scripts and spawns for [Hitch] and [HitchDetail]; the profiler adds per-type
        // attribution. Installed here, once per process, by their MCM toggles: game init precedes every mission,
        // so nothing that calls Mission.OnPreTick has run yet. The installers contain their own failures.
        Features.MissionPerf.Hooks.MissionTickProfilerInstaller.InstallIfEnabled(
            IoC.Resolve<Features.BattleLoadDiagnostics.IBattleLoadDiagnosticsSettingsProvider>(),
            IoC.Resolve<IModLogger>(),
            TryPatchCategory);
```

10. `Main/SubModule.cs`, mission start: directly after the heartbeat line from A1 insert:

```csharp
        // [PerfContext] once per mission; while the hitch probe or the tick profiler measures, a [TickProfile]
        // window (with script, spawn and clip-loading lines) on the same 5 s clock as [MissionPerf].
        AddTaomBehavior(new Features.MissionPerf.Hooks.MissionTickProfilerBehavior(
            IoC.Resolve<Features.BattleLoadDiagnostics.IBattleLoadDiagnosticsSettingsProvider>(),
            IoC.Resolve<IGraphicsOptionsAdapter>(),
            IoC.Resolve<IModLogger>()));
```

11. `CoopSettingsRelevance.cs`: after the A1 line `"EnableMissionPerfHeartbeat",` insert:

```csharp
        // The Patch97 tick profiler and its two knobs, and the Patch98 hitch probe: log lines, never a computation.
        "EnableTickProfiler", "TickProfilerTopN", "HitchThresholdMs", "EnableHitchProbe",
```

12. P-COUNT with four settings (`BattleLoadDiagnosticsSettings`).
13. Comment re-cites (procedure step 3; use the Edit tool, each old string occurs once):

| File | Old | New |
|---|---|---|
| `Main/Adapters/AnimationLoadingAdapter.cs` | `(v1.5.3, static)` | `(v1.4.8: MBAnimation.cs:143, static)` |
| `Main/Features/MissionPerf/Hooks/MissionAttributionInstaller.cs` | `Verified on the installed v1.5.3:` | `Verified on the installed v1.5.3 and re-read on v1.4.8 (Mission.cs:4346, :4360; ManagedScriptHolder.cs:240-267):` |
| `Main/Features/MissionPerf/Hooks/Patch97_MissionTickProfiler.cs` | `SubModule.cs). Verified on the installed v1.5.3:` | `SubModule.cs). Verified on the installed v1.5.3 and re-read on v1.4.8 (Mission.cs:3530-3538, :3636-3776):` |
| `Main/Features/MissionPerf/Hooks/Patch98_HitchProbe.cs` | `// verified on v1.5.5):` | `// verified on v1.5.5; v1.4.8 MissionScreen.cs:4092-4101, Mission.cs re-read for the port):` |

**Verify (GREEN for the build)**: Build `0 Error(s)`. Then
`FILTER="FullyQualifiedName~HitchProbeBindingTests"`: expected `Failed: 2`, exactly
`ProbeTargets_ResolveInInstalledEngine` and
`ProbeAndAttributionTargets_OnlyTheWaitAndTheScriptTickAreOnPatchShieldsExclusionList`, each saying
`Mission_SpawnAgent_..._Patch has no resolvable target.` That is the RED of A2.3. Any other failure, or these
two passing: STOP (the v1.4.8 `SpawnAgent` fact in "Engine facts" is wrong for this build).

**A2.3 GREEN (the v1.4.8 `SpawnAgent` signature).** In both files replace
`new[] { typeof(AgentBuildData), typeof(bool), typeof(Equipment), typeof(ItemObject) }` with
`new[] { typeof(AgentBuildData), typeof(bool) }`:
`Main/Features/MissionPerf/Hooks/Patch98_HitchProbe.cs` and `Main/Features/MissionPerf/Hooks/Patch97_MissionAttribution.cs`.
In the comments replace `public Agent Mission.SpawnAgent(AgentBuildData, bool, Equipment, ItemObject): the only overload; the`
(Patch98) with `public Agent Mission.SpawnAgent(AgentBuildData, bool) (v1.4.8 Mission.cs:4086): the only overload; the`,
and `public Agent Mission.SpawnAgent(AgentBuildData, bool, Equipment, ItemObject): main thread; the probe's`
(Patch97 attribution) with `public Agent Mission.SpawnAgent(AgentBuildData, bool) (v1.4.8 Mission.cs:4086): main thread; the probe's`.
Leave the `using TaleWorlds.Core;` lines.

**Verify**: `grep -c 'typeof(Equipment)' Main/Features/MissionPerf/Hooks/*.cs | grep -v ':0' | wc -l` prints `0`;
Build; `FILTER="FullyQualifiedName~TAOM.Tests.Features.MissionPerf|FullyQualifiedName~BattleLoadDiagnosticsSettingsProviderTests|FullyQualifiedName~PatchShieldPolicyTests|FullyQualifiedName~ReflectionSiteBindingTests|FullyQualifiedName~SettingRequireRestartPostureTests|FullyQualifiedName~SettingsFingerprintTests|FullyQualifiedName~Patch91|FullyQualifiedName~TAOM.Tests.Composition|FullyQualifiedName~PatchCategoryApplierTests"`:
`Failed: 0`, and Skipped is exactly 1 (`HitchProbe_SimulatedFrame_CostsUnderHalfAPercentOfTenMilliseconds`,
opt-in). Then the binding gate: `FILTER="TestCategory=BindingVerification"` with
`--logger "console;verbosity=normal"`: `Failed: 0`; record Skipped and confirm no skipped test name
contains `HitchProbe`, `MissionTickProfiler` or `ReflectionSite`.

**A2.4 Site counts.** In the A2.3 detailed output (re-run the two binding classes with
`--logger "console;verbosity=normal"` if needed) every MissionPerf binding test passes: that pins the
`Mission.OnTick` rewrite (2 sites), `OnPreTick` (2), `SpawnAgent` (2 `OnAgentBuild`), `TickComponents` (5),
the camera target's interface slot and the single `UpdateCamera` call. A failure here is a STOP, never a
test edit.

**A2.5 Full suite.** Test: `Failed: 0`; Total = (A1 total) + the A2 tests (about 281 results in the 26
files by a source count, plus 11 provider tests, 2 policy tests and 2 reflection rows; the exact number is
what the run reports: record it); Skipped = T0's + 1.

**A2.6 Wiring greps**: `grep -cF 'MissionTickProfilerInstaller.InstallIfEnabled(' Main/SubModule.cs` `1`;
`grep -cF 'new Features.MissionPerf.Hooks.MissionPerfHeartbeatBehavior(' Main/SubModule.cs` `1`;
`grep -cF 'new Features.MissionPerf.Hooks.MissionTickProfilerBehavior(' Main/SubModule.cs` `1`;
`grep -rn '\.PatchCategory(' Main/Features/MissionPerf` prints nothing (the installers call the
`tryPatchCategory` delegate).

**A2.7 Measure the hitch probe's cost (the orchestrator's note: decide the default with a measurement).**

```bash
cd $WT && TAOM_RUN_BENCHMARKS=1 dotnet test TAOM.Tests --no-build -p:DisableModuleCopy=true -p:ModuleId= --filter "FullyQualifiedName~HitchProbeOverheadBenchmarkTests" --logger "console;verbosity=detailed" 2>&1 | grep -E "HitchProbe overhead|Passed!|Failed!"
```

**Verify**: `Passed!` and a line `HitchProbe overhead: <x> us per simulated frame ...`. Run it three times;
record the three `<x>`. All under 50: keep the default ON and put the three values in commit A2's
`Research:` trailer. Any run at 50 or more, or a failure: STOP (turning the probe off by default would need a
renamed key; that is the maintainer's call).

**A2.8 Commit A2.** `git add` every path of A2.1 and A2.2 (the 31 checked-out code files and 26 test
files are already staged; add `Main/Adapters/IGraphicsOptionsAdapter.cs Main/Adapters/GraphicsOptionsAdapter.cs Main/Features/BattleLoadDiagnostics/BattleLoadDiagnosticsSettings.cs Main/Features/BattleLoadDiagnostics/BattleLoadDiagnosticsSettingsProvider.cs Main/Features/BattleLoadDiagnostics/IBattleLoadDiagnosticsSettingsProvider.cs Main/Features/BattleLoadDiagnostics/Hooks/Patch91_MissionTickStallProbes.cs Dependencies/Foundation/PatchShieldPolicy.cs Main/SubModule.cs Main/Features/CoopInterop/CoopSettingsRelevance.cs TAOM.Tests/Features/BattleLoadDiagnostics/BattleLoadDiagnosticsSettingsProviderTests.cs TAOM.Tests/Infrastructure/Dependencies/PatchShieldPolicyTests.cs TAOM.Tests/Migration/ReflectionSiteBindingTests.cs TAOM.Tests/Features/Mcm/SettingRequireRestartPostureTests.cs TAOM.Tests/Features/CoopInterop/SettingsFingerprintTests.cs docs/features/coop-interop.md docs/features/bannerlord-together-compat.md`
and the five re-cited or adapted code files `Main/Adapters/AnimationLoadingAdapter.cs Main/Features/MissionPerf/Hooks/MissionAttributionInstaller.cs Main/Features/MissionPerf/Hooks/Patch97_MissionTickProfiler.cs Main/Features/MissionPerf/Hooks/Patch97_MissionAttribution.cs Main/Features/MissionPerf/Hooks/Patch98_HitchProbe.cs`).
`git status --porcelain` shows nothing unstaged or untracked.
`msg-A2.txt` (fill the three values):

```text
feat(mission-perf): v2.0.28 - hitch probe and opt-in tick profiler

Slow battle frames now explain themselves. The hitch probe (on by
default) times each mission frame's parts TAOM can see: the wait for
the agent tick, the mission tick, the camera and render preparation,
scene scripts, spawns and clip loading. Every frame of 250 ms or more
writes a [Hitch] and a [HitchDetail] line, and each mission writes one
[PerfContext] line with the frame limiter, vsync, quality options,
memory load and CLR version. The tick profiler (off by default, needs
a restart) adds per-behaviour attribution. Both apply once at game
start through TryPatchCategory.

v1.4.8 differs from the 1.5.x line in one target: Mission.SpawnAgent
takes two parameters, so the spawn bracket and the spawn attribution
transpiler name those two. PatchShield keeps WaitTickCompletion and
ManagedScriptHolder.TickComponents off its shield, as on 1.5.x.

Research: taom-src v1.4.8 Mission.OnTick, OnPreTick,
WaitTickCompletion, TickAgentsAndTeamsImp, SpawnAgent (two
parameters, Mission.cs:4086), ManagedScriptHolder.TickComponents,
MissionScreen IMissionSystemHandler.UpdateCamera, NativeOptions;
HitchProbeOverheadBenchmarkTests on the laptop: <x1>, <x2>, <x3> us
per simulated frame (budget 50)
Not-tested: Patch97 and Patch98 in a live battle; the clip-loading
sample against the engine's own loads
Save-compat: no saved state; four new MCM keys, 1.5.x defaults
Ported-from: 6eb2e1b90c2fe42d6cda851434c0ff6ec1fdcd1c
Ported-from: daba6f0a065bb53424c312119c1063aa78f9f2db
Ported-from: 43ca92ee99c064467113e603c19c58a5901b9093
Ported-from: 0273e2ea48f9bf590eb80b72a3c207778279f40c
Ported-from: c7612577321b057a47639d95cd657c962af26ba8
Ported-from: fdee18f02132a259c6ea52c6c43be5e82f5ca93f
Ported-from: 685c6a1e03f799567d5c5c5e93eae052900f767b
```

### Step A3: `tools/perf_runs.py`

Sources: `67eff55cf`, with the tool hunks of `0273e2ea4`, `c76125773`, `fdee18f02`, `685c6a1e0` (end state at
`685c6a1e0`; `ad3967222` and `4407fbc05` are WP138's). Its two C# comment hunks arrived in A1. The tool reads
`[BattleLoad]` lines through `triage_battle_load.parse_battle_load_log` and `classify_phase_timings`, which have
the same signatures on origin and at `1581d4d0a` (WP113's end state), and its tests write synthetic logs to a
temp dir.

RED: `cd $WT && git checkout 685c6a1e0 tools/tests/test_perf_runs.py && python -m unittest discover -s tools/tests -t . -p "test_perf_runs.py" 2>&1 | tail -3`:
an import error for `perf_runs`. GREEN: `git checkout 685c6a1e0 tools/perf_runs.py`; the same command
prints `OK`. Then the Tool suite: no failure beyond `$AUX/tools0.txt`.

Commit A3 (both files staged by the checkouts). `msg-A3.txt`:

```text
feat(perf-runs): v2.0.28 - per-mission rows and A/B compare from logs

Adds tools/perf_runs.py: it turns taom_debug logs into one row per
mission (load phases through triage_battle_load.py, frame time from
[MissionPerf], hitches and the camera share from the probe's lines,
GC counts, the [PerfContext] options) and compares two groups of runs,
flagging confounders such as memory pressure. Read-only on the logs.

Not-tested: a run over a real 1.4.8 log with the new lines
Ported-from: 67eff55cf9cace45727b2324a79555d05ec927b2
Ported-from: 0273e2ea48f9bf590eb80b72a3c207778279f40c
Ported-from: c7612577321b057a47639d95cd657c962af26ba8
Ported-from: fdee18f02132a259c6ea52c6c43be5e82f5ca93f
Ported-from: 685c6a1e03f799567d5c5c5e93eae052900f767b
```

### Step A4: the feature doc, the engine page, the registry

Docs travel with the feature (procedure step 2). Use the Edit tool; each old string below occurs exactly once.

**A4.1 `docs/features/mission-perf-heartbeat.md`.** `cd $WT && git show 685c6a1e0:docs/features/mission-perf-heartbeat.md > docs/features/mission-perf-heartbeat.md`
(1,144 lines, no long dashes). Then read WP118's state on this base:
`cd $WT && test -f docs/features/map-load-diagnostics.md && echo maploaddoc-present || echo maploaddoc-absent`.
**Verify**: it prints exactly one of the two words; keep it for this step and the hand-off. Then:

| Old | New |
|---|---|
| `doctrine A/B (#608) and kept for every mission.` | `doctrine A/B (#608) on the 1.5.x line (that feature is not on this line) and kept for every mission;`<br>`ported to the 1.4.x line by plan 117 for the battle freeze and stutter reports.` (two lines) |
| ``frame counter; `[MapLoad]` reports fps on the campaign map only.`` | ``frame counter; on the 1.5.x line `[MapLoad]` reports fps on the campaign map (here it arrives with WP118).`` |
| `` `Mission.SpawnAgent(AgentBuildData, bool, Equipment, ItemObject)` `` (line 474, a table cell) | `` `Mission.SpawnAgent(AgentBuildData, bool)` `` |
| ``(v1.5.3 `Mission.cs:3756`)`` | ``(v1.4.8 `Mission.cs:3740`; v1.5.3 `:3756`)`` |

If the probe printed `maploaddoc-present`, skip the `[MapLoad]` row: WP118 is on this base, so the old sentence is true here.

After the heading line `## Animation clip memory probe` insert a blank line and this one line (B2 deletes it):

```markdown
> **1.4.x:** this probe arrives with Part B of plan 117; until that commit lands nothing on this line writes `[AnimMem]`.
```

Then insert this section directly before `## Why This Exists`:

```markdown
## On the 1.4.x line

Ported by plan 117 of the 2026-11-04 port programme from the 1.5.x tree at `685c6a1e0`. The rest of
this page is the 1.5.x text: a line number or native address with a v1.5.x tag is the 1.5.x line's fact.
For v1.4.8 (build 119303, re-read for the port):

| 1.5.x citation on this page | v1.4.8 |
|---|---|
| `Mission.cs:3548` (the wait, first in `OnPreTick`) | `Mission.cs:3532` (`OnPreTick` 3530-3538) |
| `Mission.cs:3629-3633` (`tickCompleted = true`, then `AfterAsyncTickTick`) | `Mission.cs:3613-3617` |
| `Mission.cs:3756` (`tickCompleted = false`) | `Mission.cs:3740` |
| `:3750`, `:3777`, `:3786` (`OnPreDisplayMissionTick`, `OnEndMissionRequest`, the agent tick launch) | `:3734`, `:3761`, `:3770` |
| `Mission.SpawnAgent(AgentBuildData, bool, Equipment, ItemObject)` | `SpawnAgent(AgentBuildData agentBuildData, bool spawnFromAgentVisuals = false)`, the only overload (`Mission.cs:4086`); Patch97's and Patch98's spawn classes name these two parameters |
| `MissionScreen`'s `IMissionSystemHandler.UpdateCamera` | `MissionScreen.cs:4092-4101`; called once, from `Mission.cs:3738` |
| `CoreCallbacksGenerated.cs:936-940`, `CoreManaged.cs:120-123`, `Managed.cs:303-313` | the same lines |

- **The OnTick hang has a remedy here.** Where the "PatchShield" sections leave an exception that escapes
  `Mission.OnTick` between the two `tickCompleted` writes to a PatchShield follow-up plan, the 1.4.x line
  has WP106's launch-aware recovery (Patch91b, [mission-tick-guard.md](mission-tick-guard.md)).
- **Busy times are a v1.5.5 reading.** `mainMs` and `renderMs` are `1000 / Utilities.GetMainFps()` and
  `1000 / Utilities.GetRendererFps()` (v1.4.8 `Utilities.cs:447-454`); that they are busy times was read
  from the v1.5.5 native binary and is UNVERIFIED on v1.4.8.
- **The clip-loading sample** calls `MBAnimation.IsAnyAnimationLoadingFromDisk()` (v1.4.8 `MBAnimation.cs:143`),
  which vanilla's mission screen also polls on the main thread while the loading screen waits; whether the
  walk is safe against a clip loading on a worker is UNVERIFIED on both lines.
- **Defaults**: the same MCM keys and defaults as the 1.5.x line (heartbeat and hitch probe on, tick profiler
  off). The hitch probe's cost was re-measured on the laptop for the port (plan 117, Step A2.7).
- **Not on this line**: the culture doctrine feature, co-op's client-only patch attribute, and, until WP118,
  `[MapLoad]`, `[XmlMerge]` and the load-time stamps this page mentions.
```

If A0.3 printed `guard-doc-absent`, write the first bullet without the link: `... has WP106's launch-aware recovery (Patch91b).`
If A4.1's probe printed `maploaddoc-present`, write the last bullet as
`- **Not on this line**: the culture doctrine feature and co-op's client-only patch attribute.`

**A4.2 `docs/reference/engine/mission-frame-threads-and-native-costs.md`** (from `af990ca76` with its later
edits, re-verified on v1.4.8 as far as the tools on this line allow).
`cd $WT && mkdir -p docs/reference/engine && git show 685c6a1e0:docs/reference/engine/mission-frame-threads-and-native-costs.md > docs/reference/engine/mission-frame-threads-and-native-costs.md`. Then:

| Old | New |
|---|---|
| `# Mission frame, threads and native costs (v1.5.3)` | `# Mission frame, threads and native costs (v1.4.8 managed facts, v1.5.x native findings)` |
| ``[TAOM-verified, v1.5.3 `Mission.cs`]`` | ``[TAOM-verified, v1.5.3 `Mission.cs`; the line numbers are v1.4.8's, re-read for the 1.4.x port]`` |
| `~3546` | `3530` |
| `~3652` | `3636` |
| `~3784-3791` | `3768-3775` |
| `~3617` | `3601` |
| `([scripted-melee-strikes.md](../scripted-melee-strikes.md) section 9)` | ``(1.5.x line: `docs/reference/scripted-melee-strikes.md` section 9)`` |
| `([bannerlord-animation-system-map.md](../bannerlord-animation-system-map.md))` | ``(1.5.x line: `docs/reference/bannerlord-animation-system-map.md`)`` |

Replace everything from the line `## 10. The per-frame skeleton buffer (v1.5.4)` up to (not including) the
line `## How to re-verify` with:

```markdown
## 10. The per-frame skeleton buffer

On the 1.4.x line the v1.4.8 layout, the pattern addresses and the guard's caves are in
[skeleton-buffer-guard.md](../../features/skeleton-buffer-guard.md), "Native layout (v1.4.8)" (plan 109).
The 1.5.x line's v1.5.4 analysis is not repeated here.

```

Insert after the paragraph that ends `([provenance](../provenance-register.md), comparison only).`:

```markdown

**On the 1.4.x line** (plan 117, re-checked 2026-11-04 on the installed v1.4.8, build 119303):

- Section 1's managed steps are re-read on v1.4.8 (`Mission.cs`: `OnPreTick` 3530, `WaitTickCompletion`
  3585-3591, `TickAgentsAndTeamsImp` 3601 with `tickCompleted = true` at 3613, `OnTick` 3636 with the camera
  call at 3738, `tickCompleted = false` at 3740 and the agent tick launch at 3768-3775).
- Section 6's clip-budget sites have v1.4.8 values: load site `0x23AADF`, budget `subss` `0x23AB04`, counter
  `0xDA59B0`, budget float `0xB2BC9C` reading 12582912 (`ClipBudgetSignatureInstalledBinaryTests`).
  `MBAnimation.IsAnyAnimationLoadingFromDisk()` is `MBAnimation.cs:143`; vanilla's mission screen polls it
  on the main thread while the loading screen waits.
- Section 2: v1.4.8 ships three hosts: the managed `Bannerlord.exe` and launcher on the .NET Framework,
  `Launcher.Native.exe` on the bundled .NET 6 CoreCLR, and `WotsMainNative` on Mono. A player on the public
  1.4.8 build logged `CLR: 4.0.30319.42000` (`[MissionDiag] OS:`, 2026-11-01), which rules out CoreCLR for
  that player; whether that version string also separates the .NET Framework from Mono is UNVERIFIED.
- Every other native address here (an RVA, a `FUN_` or `DAT_` name) comes from the v1.5.3 or v1.5.5 client
  and is **not valid for v1.4.8**; treat it as a lead. `tools/native_decompile.py`, which re-derives them,
  reaches this line with WP137.
```

**A4.3 `docs/reference/bannerlord-engine-and-toolchain.md`** (`af990ca76`'s correction, rewritten for v1.4.8):
replace the whole table row that starts ``| **`mono-2.0-sgen.dll`** |`` with:

```markdown
| **`mono-2.0-sgen.dll`** | The native engine can host **Mono** (sgen GC) through its `WotsMainNative` entry; the import proves only that Mono is linked. v1.4.8 ships three hosts: `Bannerlord.exe` and the launcher are managed .NET Framework executables, `Launcher.Native.exe` starts the bundled .NET 6 CoreCLR, and `WotsMainNative` starts Mono. The tick profiler's `[PerfContext]` line logs `clr=` (6.0.x means CoreCLR); see [mission-frame-threads-and-native-costs.md](engine/mission-frame-threads-and-native-costs.md), section 2. |
```

**A4.4 `docs/reference/provenance-register.md`**: as the last table row before `<!-- provenance-register-end -->` add:

```markdown
| Yotthani MithrilForge and `bannerlord` repository (engine notes) | `MithrilForge` `yotthani/bannerlord` | MIT (MithrilForge's own licence; `yotthani/bannerlord` by the maintainer's statement of 2026-10-09, no root `LICENSE` yet) | comparison-only | (none; restated facts tagged [yotthani] in `docs/reference/engine/mission-frame-threads-and-native-costs.md`, carried from the 1.5.x line by plan 117) | cleared |
```

**A4.5 `docs/reference/harmony-patch-registry.md`**: extract the two 1.5.x sections and insert them
directly before the line `## Patch_MissionTime_SetMovementOrder`:

```bash
cd $WT && git show 685c6a1e0:docs/reference/harmony-patch-registry.md | sed -n '1111,1128p' > $AUX/reg-A.md && head -1 $AUX/reg-A.md && sed -n '9p' $AUX/reg-A.md && tail -1 $AUX/reg-A.md | cut -c1-60
```

Expected: `## Patch97_MissionTickProfiler`, `## Patch98_HitchProbe`, and an empty last line (line 1128 of
the source is blank, so the extract ends with its own separator). Insert the file's lines directly before
the heading (Edit tool, or `sed -i "/^## Patch_MissionTime_SetMovementOrder$/e cat $AUX/reg-A.md" docs/reference/harmony-patch-registry.md`).
Then in the inserted text: `Mission.SpawnAgent(AgentBuildData, bool, Equipment, ItemObject)` to
`Mission.SpawnAgent(AgentBuildData, bool)`;
`public Agent Mission.SpawnAgent(AgentBuildData agentBuildData, bool spawnFromAgentVisuals, Equipment agentSpawnEquipment, ItemObject formationBannerItem)`
to `public Agent Mission.SpawnAgent(AgentBuildData agentBuildData, bool spawnFromAgentVisuals = false)`;
``(`Mission.cs:3756`)`` to ``(v1.4.8 `Mission.cs:3740`)``; ``(`:3629`)`` to ``(`:3613`)``;
`read from the v1.5.3 decompile` to `read from the v1.4.8 decompile`. Append to the end of the Patch97
paragraph that ends `**ACTIVE** (off by default), not yet smoked in game.` the sentence: `On the 1.4.x line (plan 117) the remedy for the OnTick hang is WP106's launch-aware recovery (Patch91b).`

**A4.6 `docs/features/mcm.md`**: insert this paragraph and a blank line directly before the line that starts
`**A moved compiled default reaches fresh`:

```markdown
`BattleLoadDiagnosticsSettings.EnableTickProfiler` and `EnableHitchProbe` are on that list too (plan 117): each
is read at the first game init, where its Harmony category (Patch97, Patch98) installs or is skipped, so
turning it on needs a restart; turning it off applies from the next mission
([mission-perf-heartbeat.md](mission-perf-heartbeat.md)).
```

**A4.7 `docs/reference/taleworlds-api-snapshot/reflection-sites.md`**: after the row starting
``| `TaleWorlds.MountAndBlade.Mission` | `RegisterBlow` |`` insert lines 48 and 49 of
`git show 685c6a1e0:docs/reference/taleworlds-api-snapshot/reflection-sites.md` (the `WaitTickCompletion`
and `ScriptComponentBehavior` `OnTick` rows).

**Verify**: Docs command: `dead_links` and `orphan_features` not above `$AUX/lint0.txt`, `missing_features`
one lower than `$AUX/lint0.txt` (MissionPerf now has its doc), `ai_dashes: 0`;
`grep -c '^## On the 1.4.x line' docs/features/mission-perf-heartbeat.md` prints `1`;
`grep -c '^\*\*On the 1.4.x line\*\*' docs/reference/engine/mission-frame-threads-and-native-costs.md` prints `1`
(its section 10 replacement holds a second line that starts with those words); `grep -c '^## Patch9[78]_' docs/reference/harmony-patch-registry.md` prints `2`;
`grep -c 'Yotthani MithrilForge and' docs/reference/provenance-register.md` prints `1`; Test still `Failed: 0`.

Commit A4: `git add docs/features/mission-perf-heartbeat.md docs/reference/engine/mission-frame-threads-and-native-costs.md docs/reference/bannerlord-engine-and-toolchain.md docs/reference/provenance-register.md docs/reference/harmony-patch-registry.md docs/features/mcm.md docs/reference/taleworlds-api-snapshot/reflection-sites.md`. `msg-A4.txt`:

```text
docs(mission-perf): v2.0.28 - frame instruments doc and engine page

Adds the feature doc for the [MissionPerf] heartbeat, the hitch probe
and the tick profiler, and the engine page on the battle frame, its
threads and native costs, both carried from the 1.5.x line with a
section that maps their engine citations to v1.4.8. Registry entries
for Patch97 and Patch98, the MCM restart note, the reflection rows,
and a provenance row for the yotthani engine notes the page restates.
The toolchain doc no longer says the game runs on Mono: v1.4.8 ships
three runtime hosts, and the [PerfContext] line names the CLR.

Research: v1.4.8 Mission.cs lines re-read for the frame table;
installed bin folder hosts per the port's runtime check; player log
CLR line (taom_debug 2026-11-01)
Ported-from: af990ca769da52f408b8e804d127c446b515a379
Ported-from: 6eb2e1b90c2fe42d6cda851434c0ff6ec1fdcd1c
Ported-from: daba6f0a065bb53424c312119c1063aa78f9f2db
Ported-from: 43ca92ee99c064467113e603c19c58a5901b9093
Ported-from: 685c6a1e03f799567d5c5c5e93eae052900f767b
```

### Part A done criteria

- [ ] Build `0 Error(s)`; Test `Failed: 0`, Total = T0 + A1 + A2 deltas as recorded, Skipped = T0's + 1.
- [ ] `git diff --stat 685c6a1e0 HEAD -- Main/Features/MissionPerf Main/Adapters/AnimationLoadingAdapter.cs Main/Adapters/IAnimationLoadingAdapter.cs TAOM.Tests/Features/MissionPerf ':!Main/Features/MissionPerf/AnimMemory' ':!TAOM.Tests/Features/MissionPerf/AnimMemory'`
      lists exactly `AnimationLoadingAdapter.cs`, `MissionAttributionInstaller.cs`, `Patch97_MissionAttribution.cs`,
      `Patch97_MissionTickProfiler.cs` and `Patch98_HitchProbe.cs` (the re-cites and the `SpawnAgent` fix).
- [ ] `python -m unittest discover -s tools/tests -t . -p "test_perf_runs.py"` `OK`; Tool suite no new failure.
- [ ] Docs as A4's Verify. `git log --format=%s $BASE..HEAD | awk 'length($0) > 72 || $0 !~ / v2\.0\.28 - /'`
      prints nothing (four subjects). `git diff --name-only $BASE..HEAD | grep -c CHANGELOG` prints `0`.
- [ ] `git status --porcelain` empty.

## Part B: map and memory instruments

### Step B0: Part B checks

```bash
WT=/c/Users/mikew/source/repos/taom-port/117; cd $WT && git log --format=%s -4 | grep -c ' v2.0.28 - '; \
stat -c %s "/c/Program Files (x86)/Steam/steamapps/common/Mount & Blade II Bannerlord/bin/Win64_Shipping_Client/TaleWorlds.Native.dll"; \
test -f Main/Core/Diagnostics/IStampClock.cs && echo stamp-present || echo stamp-absent; \
grep -c 'GroupOrder = 64' Main/Features/TaomSettings.cs; \
test -f docs/features/map-load-diagnostics.md && echo maploaddoc-present || echo maploaddoc-absent; \
grep -cF 'catch { /* teardown is best-effort, never break OnGameEnd */ }' Main/SubModule.cs
```

**Verify**: `4`; `14185944` (any other size: STOP and recommend `/engine-bump`; the clip row and the
pattern facts are for that file); `stamp-absent` or `stamp-present` (if present, `git diff --quiet 346f3e093 HEAD -- Main/Core/Diagnostics/IStampClock.cs Main/Core/Diagnostics/StopwatchStampClock.cs && echo same`
must print `same`, else STOP); `0` (otherwise B4 uses the highest `GroupOrder` below 100 plus one and says so);
either doc state (B1 says what changes); a count of at least `1`. Record **TB0** with a full Test run.

### Step B1: the campaign map frame profiler (Patch101, off by default)

Sources: `f6bf0e588`, `daba6f0a0`'s `TickEventSwapTests` edit, `43ca92ee9`'s doc edits. MapPerf code is the
same at `43ca92ee9` and `685c6a1e0`; the doc comes from `43ca92ee9` (later hunks are WP120, WP125, D7, WP144).

**B1.1 RED.**

1. `cd $WT && git checkout 685c6a1e0 TAOM.Tests/Features/MapPerf/{MapFrameProfilerBindingTests,MapFrameProfilerHooksTests,MapFrameProfilerInstallerTests,MapFrameProfilerTests,MapFrameProfilerWiringTests,MapProfileLinesTests,MapSpeedTests,TickEventListenerWalkerTests,TickEventSwapTests}.cs TAOM.Tests/Features/TimeAcceleration/TimeControlAdapterBindingTests.cs`
2. 1.4.x adaptations of two tests (they would fail on 1.4.x data, not on the port):
   - `MapFrameProfilerWiringTests.cs`: replace `        var gating = src.IndexOf("ApplyGating(", initFinished);` with
     `        // 1.4.x has no armour gating (a 1.5.x feature); the every-game [SaveLoad] stamp precedes the guard.` and
     `        var gating = src.IndexOf("SaveLoadPhase.GameInitializationFinished", initFinished);` (two lines), and
     `"The installer follows the armour gating."` with `"The installer follows the [SaveLoad] stamp."`.
   - `MapFrameProfilerBindingTests.cs`: replace
     `new[] { "FieldCampMapView", "MomentumIndicatorMapView", "RealmBordersMapView" }` with
     `new[] { "FieldCampMapView", "MomentumIndicatorMapView" }` (1.4.x has no Realm Borders).
3. `BattleLoadDiagnosticsSettingsProviderTests.cs`: before the class's closing `}` add:

```csharp

    // The map profiler installs Harmony patches on the campaign map's per-frame methods, so its
    // toggle fails CLOSED too.
    [TestMethod]
    public void MapProfilerEnabled_NoMcmInstance_DefaultsFalse()
    {
        Assert.IsFalse(new BattleLoadDiagnosticsSettingsProvider().MapProfilerEnabled);
    }
```

4. `ReflectionSiteBindingTests.cs`: after the A2 `ScriptComponentBehavior` row insert:

```csharp
    // --- MapPerf map profiler: the TickEvent listener walk (TickEventListenerWalker.cs) ---
    [DataRow("TaleWorlds.CampaignSystem.MbEvent`1", "MbEvent`1", "_nonSerializedListenerList", "Field", "TickEventListenerWalker.cs:47")]
    [DataRow("TaleWorlds.CampaignSystem.MbEvent`1+EventHandlerRec`1", "EventHandlerRec`1", "Next", "Field", "TickEventListenerWalker.cs:51")]
    [DataRow("TaleWorlds.CampaignSystem.MbEvent`1+EventHandlerRec`1", "EventHandlerRec`1", "<Action>k__BackingField", "Field", "TickEventListenerWalker.cs:54")]
    [DataRow("TaleWorlds.CampaignSystem.MbEvent`1+EventHandlerRec`1", "EventHandlerRec`1", "<Owner>k__BackingField", "Field", "TickEventListenerWalker.cs:57")]
```

5. `SettingRequireRestartPostureTests.cs`: after the A2 `EnableHitchProbe` entry insert:

```csharp
        [$"{nameof(BattleLoadDiagnosticsSettings)}.{nameof(BattleLoadDiagnosticsSettings.EnableMapProfiler)}"] = "Read at the first game init, where Patch101 installs or is skipped: turning it on needs a restart (turning it off applies from the next campaign session)",
```

**Verify (RED)**: the RED check of A1.1 (errors name MapPerf types, `MapProfilerEnabled`, `EnableMapProfiler`
or `SimplifiedTimeControlMode`).

**B1.2 GREEN.**

1. `cd $WT && git checkout 685c6a1e0 Main/Features/MapPerf/Hooks/{MapFrameBoundary,MapFrameProfilerHooks,MapFrameProfilerInstaller,MapProfilerTargets,MapSessionHooks,Patch101_MapFrameProfiler,Patch101_MapFrameProfilerViews,TickEventListenerWalker}.cs Main/Features/MapPerf/{MapFrameProfiler,MapProfileLines,MapSpeed}.cs`
2. `ITimeControlAdapter.cs`: after `    int TimeControlMode { get; set; }` insert:

```csharp

    /// <summary>
    /// The engine's own simplification of the mode (<c>Campaign.GetSimplifiedTimeControlMode</c>), read-only: a
    /// Stoppable mode reads Stop while the main party is waiting, because <c>TickMapTime</c> then gives the frame no
    /// campaign time; the party-wait fast-forward reads as the plain unstoppable one and FastForwardStop as Stop.
    /// Stop when no campaign is running.
    /// </summary>
    int SimplifiedTimeControlMode { get; }
```

   `TimeControlAdapter.cs`: after the closing `}` of the `TimeControlMode` property insert:

```csharp

    public int SimplifiedTimeControlMode =>
        (int)(Campaign.Current?.GetSimplifiedTimeControlMode() ?? CampaignTimeControlMode.Stop);
```

3. `BattleLoadDiagnosticsSettings.cs`: in the `TickProfilerTopN` attribute replace its HintText with
   `"How many entries each [TickProfile] and [TickSummary] line lists (mission behaviours) and each [MapProfile] and [MapProfileSummary] line lists (map tick listeners and TAOM map views), slowest first. Default 8. Read at each mission start and each campaign session start."`;
   before the class's closing `}` insert:

```csharp

    [SettingPropertyGroup("Map Performance")]
    [SettingPropertyBool("Enable Map Profiler", Order = 0, RequireRestart = true,
        HintText = "Off by default. Times the campaign map's frame: the campaign tick, every per-frame tick-event listener by its owner, the map screen and TAOM's map views, and TAOM's application tick. Writes a [MapProfile] line to the TAOM debug log every 5 seconds while the map runs and a [MapProfileSummary] line when the campaign ends. Turning it on takes effect after a restart (the profiler is installed once, at game start); turning it off stops measuring from the next campaign session.")]
    public bool EnableMapProfiler { get; set; } = false;
```

4. `IBattleLoadDiagnosticsSettingsProvider.cs`: replace the `TickProfilerTopN` summary's
   `/// default 8); read at each mission start.</summary>` with
   `/// default 8); read at each mission start. It also sizes the [MapProfile] and [MapProfileSummary]` and
   `/// lists (map tick listeners and TAOM map views), read at each campaign session start.</summary>`;
   before the closing `}` insert:

```csharp

    /// <summary>The Patch101 map profiler's toggle. Like the tick profiler's it fails CLOSED to false
    /// when MCM is not ready, because it installs Harmony patches on the campaign map's per-frame methods.
    /// Read at every game init (the first installs or skips Patch101, later ones only report) and at each
    /// campaign session start: turning it on needs a restart, turning it off stops measuring from the next
    /// campaign session (the patches stay).</summary>
    bool MapProfilerEnabled { get; }
```

   `BattleLoadDiagnosticsSettingsProvider.cs`: after the `HitchProbeEnabled` line insert:

```csharp

    // Fail-CLOSED for the same reason: this toggle installs Patch101 on the campaign map's per-frame methods.
    public bool MapProfilerEnabled => BattleLoadDiagnosticsSettings.Instance?.EnableMapProfiler ?? false;
```

5. `PatchShieldPolicy.cs`: directly after `        "TaleWorlds.Engine.ManagedScriptHolder.TickComponents",` insert:

```csharp
        // Per-frame campaign-map targets that only the Patch101 map profiler patches (plan 117, from the 1.5.x
        // line's decision D13 option B, 2026-10-03). With the profiler off no TAOM patch is on them, but another
        // mod's patch on them loses the shield all the same. Given up, for any owner: the swallow of a missing-API
        // exception thrown anywhere inside them (the tick-event listeners, the periodic and hourly events of every
        // mod's campaign behaviours); it unwinds to MapState.OnTick instead (docs/features/map-perf-profiler.md,
        // "PatchShield"). MapState.OnTick, Campaign.RealTick and MapScreen.OnFrameTick keep the shield.
        // MapFrameProfilerBindingTests walks the real targets in both directions.
        "TaleWorlds.CampaignSystem.Campaign.Tick",
        "TaleWorlds.CampaignSystem.CampaignEvents.Tick",
```

6. `Main/SubModule.cs`, game init: directly before the line
   `        // Harmony patches are process-global (applied to methods, persist across games). Apply this` insert:

```csharp
        // Patch101 map frame profiler (default off; docs/features/map-perf-profiler.md), plan 117: every game
        // init, before the once-per-process guard, because a later game init only reports (restart needed, or
        // the install failed). The installer applies its categories at most once per process and contains its
        // own failures; the four services it takes are registered singletons.
        Features.MapPerf.Hooks.MapFrameProfilerInstaller.OnGameInitialized(
            IoC.Resolve<Features.BattleLoadDiagnostics.IBattleLoadDiagnosticsSettingsProvider>(),
            IoC.Resolve<Features.TimeAcceleration.ITimeControlAdapter>(),
            IoC.Resolve<Features.TimeAcceleration.ITimeAccelerationSettingsProvider>(),
            IoC.Resolve<IModLogger>(),
            TryPatchCategory);

```

   `OnGameEnd`: as the last statement of the method (directly before the `    }` that precedes
   `    protected override void OnGameStart(Game game, IGameStarter gameStarterObject)`) insert:

```csharp

        // [MapProfileSummary] for the campaign session that just ended (Patch101, plan 117; a no-op when the
        // map profiler is not installed). An in-campaign load can skip OnGameEnd, so the profiler also closes a
        // session when it sees a new campaign.
        try { Features.MapPerf.Hooks.MapSessionHooks.EndSession("gameEnd"); }
        catch { /* diagnostic is best-effort, never break OnGameEnd */ }
```

7. `CoopSettingsRelevance.cs`: after the A2 key line insert
   `        // The Patch101 campaign map profiler's toggle: log lines, never a computation.` and
   `        "EnableMapProfiler",`.
8. P-COUNT with one setting (`BattleLoadDiagnosticsSettings`).
9. Comment re-cites:

| File | Old | New |
|---|---|---|
| `Main/Features/MapPerf/Hooks/Patch101_MapFrameProfiler.cs` | `SubModule.cs). Verified on the installed v1.5.3, every target on the main thread:` | `SubModule.cs). Verified on the installed v1.5.3 and re-read on v1.4.8 (Campaign.cs:885, :954; CampaignEvents.cs:2063; MapState.cs:144; MapScreen.cs:1332), every target on the main thread:` |
| `Main/Features/MapPerf/Hooks/Patch101_MapFrameProfilerViews.cs` | `by reflection; at v1.5.3 that is the three OnMapScreenUpdate overrides. MapScreen ticks the views through` | `by reflection; on the 1.4.x line the two OnMapScreenUpdate overrides (FieldCampMapView, MomentumIndicatorMapView). MapScreen ticks the views through` |
| `Main/Features/MapPerf/Hooks/TickEventListenerWalker.cs` | `<c>MbEvent&lt;T&gt;.InvokeList</c> (v1.5.3)` | `<c>MbEvent&lt;T&gt;.InvokeList</c> (v1.5.3; the same on v1.4.8, MbEvent`1.cs:37-44)` |
| `Main/Features/MapPerf/MapSpeed.cs` | `/// v1.5.3: Stop 0,` | `/// v1.5.3 (the same values on v1.4.8): Stop 0,` |

10. Docs. `cd $WT && git show 43ca92ee9:docs/features/map-perf-profiler.md > docs/features/map-perf-profiler.md`, then
    insert directly before its `## Why This Exists`:

```markdown
## On the 1.4.x line

Ported by plan 117 from the 1.5.x line (the doc as of `43ca92ee9`). Engine targets re-read on v1.4.8:
`Campaign.RealTick` (`Campaign.cs:885`), `Campaign.Tick` (`:954`), `CampaignEvents.Tick` (`CampaignEvents.cs:2063`,
one `Invoke` of the tick event), `MapState.OnTick` (`MapState.cs:144`, whose `OnMapModeTick` calls `RealTick` at
`:186` and `Tick` at `:189`), `MapScreen.OnFrameTick` (`MapScreen.cs:1332`) and `MbEvent<T>.Invoke` /
`InvokeList` (``MbEvent`1.cs:32-44``). Two TAOM map views have a per-frame override here
(`FieldCampMapView`, `MomentumIndicatorMapView`). Patch89 and `[MapLoad]` (MapLoadDiagnostics) arrive with
WP118: until then the Patch89 sentences below do not apply, `Campaign.RealTick` carries no other TAOM patch,
and only Patch36 patches `MapScreen.OnFrameTick` besides this profiler. Other v1.5.x tags are the 1.5.x line's facts.
```

    If B0 printed `maploaddoc-present`, delete from `Patch89 and` through `besides this profiler.` in the block
    above: WP118 is on this base, so the sentences below already apply.
    If B0 printed `maploaddoc-absent`, replace `([map-load-diagnostics.md](map-load-diagnostics.md))` with
    ``(`docs/features/map-load-diagnostics.md`, with WP118)``.
    `docs/features/time-acceleration.md` (`f6bf0e588`'s hunks): replace
    ``| `Main/Features/TimeAcceleration/ITimeControlAdapter.cs` | Time control abstraction (no TaleWorlds types) |`` with
    ``| `Main/Features/TimeAcceleration/ITimeControlAdapter.cs` | Time control abstraction (no TaleWorlds types); its read-only `SimplifiedTimeControlMode` is read by the map profiler ([map-perf-profiler.md](map-perf-profiler.md)), not by this feature |``
    and ``| `Main/Features/TimeAcceleration/TimeControlAdapter.cs` | Wraps `Campaign.Current` speed/mode/lock and the wait-menu flag |`` with
    ``| `Main/Features/TimeAcceleration/TimeControlAdapter.cs` | Wraps `Campaign.Current` speed/mode/lock, `GetSimplifiedTimeControlMode()` and the wait-menu flag |``;
    after the line ending ``null-`KeyboardKey` premise the adapter's guard relies on`` insert the four lines
    `- \`TAOM.Tests/Features/TimeAcceleration/TimeControlAdapterBindingTests.cs\` carries 1 \`BindingVerification\``,
    `  test: the adapter's \`SimplifiedTimeControlMode\` getter calls \`Campaign.GetSimplifiedTimeControlMode\` and not`,
    `  the raw \`TimeControlMode\`. The map profiler's speed tests fake the adapter, so this IL pin is the only check`,
    `  on that line ([map-perf-profiler.md](map-perf-profiler.md))` (without the backslashes).
    `docs/features/mcm.md`: after the A4.6 paragraph add the paragraph
    `` `BattleLoadDiagnosticsSettings.EnableMapProfiler` is on that list for the same reason (Patch101; turning it off applies from the next campaign session; [map-perf-profiler.md](map-perf-profiler.md)). `` and a blank line.
    `docs/features/mission-perf-heartbeat.md`: at the end of the "On the 1.4.x line" section add the bullet
    `- **The campaign map** has its own profiler: [map-perf-profiler.md](map-perf-profiler.md).`
    Registry: `git show 685c6a1e0:docs/reference/harmony-patch-registry.md | sed -n '1151,1158p' > $AUX/reg-B.md`
    (first line `## Patch101_MapFrameProfiler`, last line blank, so it brings its own separator); insert it directly before
    `## Patch_MissionTime_SetMovementOrder`; in it replace
    ``the three `OnMapScreenUpdate` overrides of `FieldCampMapView`, `MomentumIndicatorMapView` and `RealmBordersMapView` at v1.5.3``
    with ``the two `OnMapScreenUpdate` overrides of `FieldCampMapView` and `MomentumIndicatorMapView` on the 1.4.x line``,
    and, only if B0 printed `maploaddoc-absent`, append to the section's last paragraph `On the 1.4.x line Patch89 arrives with WP118; until then no other TAOM patch is on Campaign.RealTick.`
    `reflection-sites.md`: after the A4.7 rows insert lines 50 to 53 of the `685c6a1e0` file (the four `MbEvent` rows).

**Verify**: Build; `FILTER="FullyQualifiedName~TAOM.Tests.Features.MapPerf|FullyQualifiedName~TimeAcceleration|FullyQualifiedName~BattleLoadDiagnosticsSettingsProviderTests|FullyQualifiedName~ReflectionSiteBindingTests|FullyQualifiedName~SettingRequireRestartPostureTests|FullyQualifiedName~SettingsFingerprintTests|FullyQualifiedName~PatchShieldPolicyTests|FullyQualifiedName~PatchCategoryApplierTests|FullyQualifiedName~TAOM.Tests.Composition|FullyQualifiedName~MissionTickProfilerWiringTests"`:
`Failed: 0`, Skipped `0` (in particular `MapFrameProfilerBindingTests` passes: six core targets resolve,
the `CampaignEvents.Tick` rewrite finds 1 site, the views are the two named, the two exclusions hold both
ways). Test: Total = TB0 + the MapPerf files (about 113 results by a source count) + 1 + 1 + 4, `Failed: 0`.
Docs: no counter above A4's, `ai_dashes: 0`.

**B1.3 Commit B1.** Stage the checked-out files and
`Main/Features/TimeAcceleration/ITimeControlAdapter.cs Main/Features/TimeAcceleration/TimeControlAdapter.cs Main/Features/BattleLoadDiagnostics/BattleLoadDiagnosticsSettings.cs Main/Features/BattleLoadDiagnostics/BattleLoadDiagnosticsSettingsProvider.cs Main/Features/BattleLoadDiagnostics/IBattleLoadDiagnosticsSettingsProvider.cs Dependencies/Foundation/PatchShieldPolicy.cs Main/SubModule.cs Main/Features/CoopInterop/CoopSettingsRelevance.cs TAOM.Tests/Features/MapPerf/MapFrameProfilerWiringTests.cs TAOM.Tests/Features/MapPerf/MapFrameProfilerBindingTests.cs TAOM.Tests/Features/BattleLoadDiagnostics/BattleLoadDiagnosticsSettingsProviderTests.cs TAOM.Tests/Migration/ReflectionSiteBindingTests.cs TAOM.Tests/Features/Mcm/SettingRequireRestartPostureTests.cs TAOM.Tests/Features/CoopInterop/SettingsFingerprintTests.cs docs/features/coop-interop.md docs/features/bannerlord-together-compat.md docs/features/map-perf-profiler.md docs/features/time-acceleration.md docs/features/mcm.md docs/features/mission-perf-heartbeat.md docs/reference/harmony-patch-registry.md docs/reference/taleworlds-api-snapshot/reflection-sites.md`
and the four re-cited MapPerf files. `msg-B1.txt`:

```text
feat(map-perf): v2.0.28 - campaign map frame profiler, off by default

With "Enable Map Profiler" on (Battle Load Diagnostics, Map
Performance; needs a restart) the campaign map's frame is split into
the campaign tick, every per-frame tick-event listener by its owner,
the map screen with TAOM's map views, TAOM's application tick and the
rest, written as [MapProfile] every 5 s and [MapProfileSummary] per
campaign session. Off, nothing is patched. PatchShield keeps
Campaign.Tick and CampaignEvents.Tick off its shield, as on 1.5.x.
The time adapter gains the engine's simplified time mode, which the
profiler's speed class reads.

Research: taom-src v1.4.8 Campaign.RealTick, Campaign.Tick,
GetSimplifiedTimeControlMode, CampaignEvents.Tick, MapState.OnTick,
MapScreen.OnFrameTick, MbEvent<T>.Invoke and InvokeList; two TAOM
map views with OnMapScreenUpdate on this line
Not-tested: Patch101 on a live campaign map
Save-compat: no saved state; one new MCM key, default off
Ported-from: f6bf0e58880c077f4859d2d1b0dc0b24e168fc2f
Ported-from: daba6f0a065bb53424c312119c1063aa78f9f2db
Ported-from: 43ca92ee99c064467113e603c19c58a5901b9093
```

### Step B2: the clip memory probe (`[AnimMem]`, on by default)

Source: `a9f10688f` (end state `685c6a1e0`), minus `ClipBudgetSignature.cs`, `PeSectionTable.cs` and their
tests (WP109). D2: `AnimMemoryProbeModule` plus one `FeatureModules.All` line; the module registers
`INativeModuleMemoryAdapter`, `IAnimationLoadingAdapter` (nothing else registers it; the hitch probe news its
own) and the probe singleton.

**B2.1 RED.** `cd $WT && git checkout 685c6a1e0 TAOM.Tests/Features/MissionPerf/AnimMemory/{AnimMemLineTests,AnimMemoryProbeMissionBehaviorTests,AnimMemoryProbeTests,AnimMemoryProbeWiringTests,AnimMemorySessionTests}.cs`;
the RED check of A1.1 (errors name AnimMemory types, `INativeModuleMemoryAdapter`, `AnimMemoryProbeModule`
or `EnableAnimMemoryProbe`).

**B2.2 GREEN.**

1. `cd $WT && git checkout 685c6a1e0 Main/Features/MissionPerf/AnimMemory/{AnimMemLine,AnimMemoryProbe,AnimMemoryProbeModule,AnimMemorySession,IAnimClipMemoryProbe}.cs Main/Features/MissionPerf/AnimMemory/Hooks/AnimMemoryProbeMissionBehavior.cs Main/Adapters/{INativeModuleMemoryAdapter,NativeModuleMemoryAdapter}.cs`
2. `Main/Composition/FeatureModules.cs`: append `        new Features.MissionPerf.AnimMemory.AnimMemoryProbeModule(),`
   as the last entry of `All`.
3. `BattleLoadDiagnosticsSettings.cs`: directly after the `EnableHitchProbe` property insert:

```csharp

    [SettingPropertyGroup("Mission Performance")]
    [SettingPropertyBool("Enable Animation Clip Memory Probe", Order = 10, RequireRestart = false,
        HintText = "Writes an [AnimMem] line to the TAOM debug log every 5 seconds while a mission runs: how much on-demand animation clip data the engine holds against its 12 MiB budget, whether a clip is loading from disk at the moment of a sample, and how often the total fell between one-second samples (a sign that clips were evicted), plus a summary when the mission ends. Read-only: it finds the two engine values once per game session by a signature check, then once a second reads the clip byte total and asks the engine whether a clip is loading; if the check fails on this game version it turns itself off and says why in the log. Takes effect at the next mission start. Default ON.")]
    public bool EnableAnimMemoryProbe { get; set; } = true;
```

4. `CoopSettingsRelevance.cs`: after `"EnableMapProfiler",` insert
   `        // The [AnimMem] clip memory probe; it reads, never changes, the engine.` and `        "EnableAnimMemoryProbe",`.
5. P-COUNT with one setting.
6. Re-cite the comments of `AnimMemoryProbeMissionBehavior.cs` to v1.4.8 (old to new, each once):
   `(Mission.cs:3831, where AddTaomBehavior adds this behavior), then` to `(Mission.cs:3815 on v1.4.8, where AddTaomBehavior adds this behavior), then`;
   ``(:3841); MissionState.cs:345 calls it`` to ``(:3825); MissionState.cs:345 calls it``;
   `(Mission.cs:3759), main thread.` to `(Mission.cs:3743 on v1.4.8), main thread.`;
   `(Mission.cs:4656), which calls OnEndMission (MissionBehavior.cs:127).` to `(Mission.cs:4585 on v1.4.8), which calls OnEndMission (MissionBehavior.cs:123).`;
   `(Mission.cs:4716),` to `(Mission.cs:4645 on v1.4.8),`; `(:2228);` to `(:2219);`; and every
   `// v1.5.3 caller:` to `// v1.5.3 caller (v1.4.8 lines in brackets where they differ):`.
   (`MissionState.cs:338-341`, `:345` and `:52` are the same on v1.4.8.)
7. Docs `docs/features/mission-perf-heartbeat.md`: delete the line
   ``> **1.4.x:** this probe arrives with Part B of plan 117; until that commit lands nothing on this line writes `[AnimMem]`.``
   and the blank line after it; replace
   ``On the v1.5.3 client the site is at RVA `0x21E00F`, the`` through ``(still `12582912`).`` (one sentence over
   three lines) with ``On the v1.4.8 client (build 119303, 14,185,944 bytes) the site is at RVA `0x23AADF`, the budget `subss` at `0x23AB04`, the counter at `0xDA59B0` and the budget float at `0xB2BC9C` (reading `12582912`); `ClipBudgetSignatureInstalledBinaryTests` pins that row.``;
   replace `text=0x1000+0xA240CC loadSite=0x21E00F budgetSite=0x21E034 counter=0xDABE40 budget=0xB2E2DC` with
   `text=0x1000+0xA2B5DC loadSite=0x23AADF budgetSite=0x23AB04 counter=0xDA59B0 budget=0xB2BC9C`;
   replace ``(the loader's compare at `0x591327`;`` with ``(the loader's compare at `0x591327` on v1.5.3, not re-derived for v1.4.8;``.

**Verify**: Build; `FILTER="FullyQualifiedName~TAOM.Tests.Features.MissionPerf.AnimMemory|FullyQualifiedName~TAOM.Tests.Composition|FullyQualifiedName~SettingsFingerprintTests|FullyQualifiedName~SettingRequireRestartPostureTests|FullyQualifiedName~NativePinGateTests"`:
`Failed: 0`, Skipped `0` (WP109's `ClipBudgetSignatureInstalledBinaryTests` reads the v1.4.8 row); Test:
Total = (B1 total) + about 59, `Failed: 0`; `grep -c 'AnimMemoryProbeModule()' Main/Composition/FeatureModules.cs` `1`;
`git diff --stat 685c6a1e0 -- Main/Features/MissionPerf/AnimMemory TAOM.Tests/Features/MissionPerf/AnimMemory Main/Adapters/INativeModuleMemoryAdapter.cs Main/Adapters/NativeModuleMemoryAdapter.cs`
(no `HEAD`: B2 is not committed yet, so this compares against the worktree) lists only
`AnimMemoryProbeMissionBehavior.cs` and `ClipBudgetSignatureInstalledBinaryTests.cs` (WP109's row).

**B2.3 Commit B2.** Stage the checked-out files and `Main/Composition/FeatureModules.cs Main/Features/BattleLoadDiagnostics/BattleLoadDiagnosticsSettings.cs Main/Features/CoopInterop/CoopSettingsRelevance.cs TAOM.Tests/Features/CoopInterop/SettingsFingerprintTests.cs docs/features/coop-interop.md docs/features/bannerlord-together-compat.md docs/features/mission-perf-heartbeat.md`
and the re-cited behaviour file. `msg-B2.txt`:

```text
feat(mission-perf): v2.0.28 - log clip memory against the 12 MiB budget

Each mission writes an [AnimMem] line every five seconds: how much
on-demand animation clip data the engine holds against its 12 MiB
eviction budget, whether a clip is loading at a sample, and how often
the total fell, a sign of evictions. The probe finds the counter once
per process by a signature scan of TaleWorlds.Native.dll, checks every
address against the module's own section table before reading it,
and turns itself off on any mismatch. It never writes engine memory.
On by default, the same key as the 1.5.x line.

Research: read-only scan of the installed v1.4.8 TaleWorlds.Native.dll
(port report V03): one pattern match, load 0x23AADF, budget float
0xB2BC9C reads 12582912; taom-src v1.4.8 Mission.AfterStart order
Not-tested: the armed line and [AnimMem] samples in a live battle
Save-compat: no saved state; one new MCM key, default on
Ported-from: a9f10688fbbb2e540b36c5fb593dda71811a881a
```

### Step B3: the battle equipment memory audit tool

Source: `3dac7ab5b`, whose simulation conflicts only in bookkeeping (REVIEW-LOG, lessons, `plans/038`,
`tools/README.md`); the transplant takes its code files (the result equals a cherry-pick with every
bookkeeping hunk dropped), plus the test file at `569f6750d`, whose only change is a comment that matches
1.4.x data (the Uruk-hai skirt sits in the Body slot, `troops_isengard.xml:1341-1342`).

RED: `cd $WT && git checkout 569f6750d tools/tests/test_audit_battle_equipment_memory.py && python -m unittest discover -s tools/tests -t . -p "test_audit_battle_equipment_memory.py" 2>&1 | tail -3`:
an import error. GREEN: `git checkout 3dac7ab5b tools/audit_battle_equipment_memory.py tools/audit_map_scene_memory.py`;
the same command prints `OK`; then
`python -m unittest discover -s tools/tests -t . -p "test_audit_map_scene_memory.py" 2>&1 | tail -2` (`OK`: the
existing map audit tests still pass with the new option), and the Tool suite with no new failure.

Commit B3 (three files staged). `msg-B3.txt`:

```text
feat(tools): v2.0.28 - rank the equipment assets a battle preloads

Adds tools/audit_battle_equipment_memory.py: an offline ranking, from
the installed packs, of the items, meshes, textures and collision
bodies a battle preloads, per troop, culture and side. It reuses the
map audit's pack index, which gains a loose-assets option. Read-only
against the packs; it attributes pack bytes, not process memory, and
each run deletes the previous report in its output folder, so point
it at a scratch folder. Its test file comes from 569f6750d, a later
commit that fixed one comment in it; that commit's troop changes are
not part of this one.

Not-tested: a run against this laptop's cooked TAOM_Map and Armory
Ported-from: 3dac7ab5b03944b0fdfd44b15043941b382d70ec
Ported-from: 569f6750d79e41b5045624ff05c9ef25d529fd4b
```

### Step B4: the Lost Footing census (opt-in)

Source: `639df895b`, adapted. Not taken: `FellFoesMissionGate.cs` and `FellFoesWiringTests.cs` (Fell Foes is a
1.5.x feature), the `MountDespawnMissionGate.cs` comment, the `HotPathSettingsProvidersTests.cs` rows (file
absent on 1.4.x), the 1.5.x settings count.

**B4.1 RED.**

1. If B0 printed `stamp-absent`: `cd $WT && git checkout 346f3e093 Main/Core/Diagnostics/IStampClock.cs Main/Core/Diagnostics/StopwatchStampClock.cs`
   (byte-identical to WP118's future add, so the integrator's merge sees the same blob twice).
2. `cd $WT && git checkout 639df895b TAOM.Tests/Features/LostFooting/{FakeLostFootingAdapter,LostFootingCensusLineTests,LostFootingMissionLogicTests,LostFootingRulesTests,LostFootingServiceTests,LostFootingSettingsProviderTests,LostFootingTunablesTests,LostFootingWiringTests}.cs`
3. `LostFootingSettingsProviderTests.cs` (renamed key, default off), in this order:
   `sed -i 's/LostFootingCensus\b/LostFootingCensusEnabled/g; s/_WhichIsOn()/_WhichIsOff()/; s/_AndDefaultsOn()/_AndDefaultsOff()/; s/StringAssert.Contains(hint, "Default: on.");/StringAssert.Contains(hint, "Default: off.");/; s/Assert.IsTrue(new TaomSettings().LostFootingCensusEnabled);/Assert.IsFalse(new TaomSettings().LostFootingCensusEnabled);/' TAOM.Tests/Features/LostFooting/LostFootingSettingsProviderTests.cs && sed -i '0,/Assert.IsTrue(provider.CensusEnabled);/s//Assert.IsFalse(provider.CensusEnabled);/' TAOM.Tests/Features/LostFooting/LostFootingSettingsProviderTests.cs`.
   **Verify**: `grep -c 'LostFootingCensusEnabled' ...SettingsProviderTests.cs` at least `5`;
   `grep -c 'IsFalse(provider.CensusEnabled)' ...` `2` (the fallback test and the pass-through test).
4. `LostFootingWiringTests.cs`: `sed -i 's/Assert.AreEqual("LostFootingCensus", mine\[0\].Name);/Assert.AreEqual("LostFootingCensusEnabled", mine[0].Name);/' TAOM.Tests/Features/LostFooting/LostFootingWiringTests.cs`;
   then replace three methods (from the `[TestMethod]` line to the closing brace) with the Edit tool:
   `Module_TheMissionGate_IsTheSharedFellFoesGate_WiredIntoTheClosure` and
   `NoOwnGateFileExists_SoThereIsOnePlaceToChangeEligibility` with the first two below,
   `Adapter_ARaceAbilityTree_IsNotACreature` with the third:

```csharp
    [TestMethod]
    public void Module_TheMissionGate_IsTheLostFootingGate_WiredIntoTheClosure()
    {
        var src = RepoPaths.ReadSource(ModulePath, stripComments: true);

        Assert.IsTrue(Regex.IsMatch(src, @"\(\)\s*=>\s*LostFootingMissionGate\.IsEligible\(mission,\s*coop\)"),
            "the census reads its eligibility through LostFootingMissionGate, passed into the service as a closure");
    }

    [TestMethod]
    public void TheGate_IsTheMountDespawnAllowlist_WithTheCoopStandDownAndAnEndedMission()
    {
        // On the 1.5.x line the census shares Fell Foes' gate; Fell Foes is not on the 1.4.x line, so the census
        // owns the same expression here. A change to MountDespawnMissionGate moves the census too.
        var src = RepoPaths.ReadSource("Main/Features/LostFooting/Hooks/LostFootingMissionGate.cs", stripComments: true);

        StringAssert.Contains(src, "coop.IsSessionActive");
        StringAssert.Contains(src, "coop.ShouldDeferToHost");
        StringAssert.Contains(src, "MountDespawnMissionGate.IsEligible(mission)");
        StringAssert.Contains(src, "!mission.MissionEnded");
    }

    [TestMethod]
    public void Adapter_AnyBuiltTree_MarksACreature()
    {
        // The 1.5.x line also excludes its Race Abilities tree; that feature is not on the 1.4.x line, so any built
        // tree marks one of TAOM's creatures here (a failed build leaves Tree null).
        var src = RepoPaths.ReadSource(AdapterPath, stripComments: true);

        Assert.IsTrue(Regex.IsMatch(src, @"GetComponent<BehaviorTreeAgentComponent>\(\) is \{ \} (?<t>\w+) && \k<t>\.Tree != null"));
        Assert.IsFalse(src.Contains("RaceAbility"), "Race Abilities is not on the 1.4.x line");
    }
```

**Verify (RED)**: the RED check of A1.1 (errors name LostFooting types, `LostFootingMissionGate` or
`LostFootingCensusEnabled`; with B4.1 item 1 done, none names `IStampClock`).

**B4.2 GREEN.**

1. `cd $WT && git checkout 639df895b Main/Features/LostFooting/{CensusLine,ILostFootingSettingsProvider,LostFootingModule,LostFootingRules,LostFootingService,LostFootingSettingsProvider}.cs Main/Features/LostFooting/Domain/{AgentProbe,LostFootingMissionState,LostFootingTunables,SceneFacts}.cs Main/Features/LostFooting/Hooks/LostFootingMissionLogic.cs Main/Adapters/{ILostFootingAdapter,LostFootingAdapter}.cs docs/features/lost-footing.md`
2. Create `Main/Features/LostFooting/Hooks/LostFootingMissionGate.cs`:

```csharp
using TaleWorlds.MountAndBlade;
using TAOM.Features.CoopInterop;
using TAOM.Features.MountDespawn.Hooks;

namespace TAOM.Features.LostFooting.Hooks;

/// <summary>
/// Which missions the Lost Footing census reads: the field battles, sieges and sally-outs of
/// <see cref="MountDespawnMissionGate"/>'s allowlist, never in a co-op session or where a co-op host owns the
/// battle, and never once the mission has ended. On the 1.5.x line the census shares Fell Foes' gate, which is
/// this same expression; Fell Foes is not on the 1.4.x line, so the census owns it here (plan 117).
/// </summary>
public static class LostFootingMissionGate
{
    public static bool IsEligible(Mission mission, ICoopSessionProvider coop) =>
        !(coop.IsSessionActive || coop.ShouldDeferToHost) && MountDespawnMissionGate.IsEligible(mission) && !mission.MissionEnded;
}
```

3. `LostFootingModule.cs`: `sed -i '/^using TAOM.Features.FellFoes.Hooks;$/d; s/() => FellFoesMissionGate.IsEligible(mission, coop)/() => LostFootingMissionGate.IsEligible(mission, coop)/' Main/Features/LostFooting/LostFootingModule.cs`.
4. `LostFootingAdapter.cs`: delete the line `using TAOM.Features.RaceAbilities;`; replace
   `IsCreature = agent.GetComponent<BehaviorTreeAgentComponent>() is { } tree && tree.Tree != null && !(tree.Tree is RaceAbilityBehaviorTree),`
   with `IsCreature = agent.GetComponent<BehaviorTreeAgentComponent>() is { } tree && tree.Tree != null,`, the comment line
   above it with `            // A built behavior tree marks one of TAOM's creatures (a failed build leaves Tree null).`, and
   `Engine members (v1.5.5):` with `Engine members (v1.5.5; re-read on v1.4.8 by plan 117):`.
5. `LostFootingSettingsProvider.cs`: `Settings?.LostFootingCensus ?? true;` to `Settings?.LostFootingCensusEnabled ?? false;`.
6. `Main/Features/TaomSettings.cs`: before the class's closing `}` (the file's last line) insert (use the
   GroupOrder B0 settled):

```csharp

    // --- Battle Tactics / Lost Footing (#894, phase 1: the census only; plan 117) ---
    // The first property carries the GroupOrder, because MCM ignores one on any later property; no other group may
    // share it (LostFootingSettingsProviderTests pins that). Off by default on the 1.4.x line, under a key of its own:
    // the 1.5.x line's LostFootingCensus defaults on, and a persisted value must never meet a flipped default.
    // The scan numbers are compiled defaults in LostFootingTunables, not settings.

    [SettingPropertyGroup("Battle Tactics/Lost Footing", GroupOrder = 64)]
    [SettingPropertyBool("Lost Footing Census", Order = 0, RequireRestart = false,
        HintText = "Counts soldiers, mounts and creatures that are stuck in a battle, standing off the walkable ground or wading in water without moving, and writes what it finds to the log with a [LostFooting] tag, with a summary when the battle ends. It only watches: it never moves or changes anything in the battle. Field battles, sieges and sally-outs. Default: off.")]
    public bool LostFootingCensusEnabled { get; set; } = false;
```

7. `Main/Composition/FeatureModules.cs`: append `        new Features.LostFooting.LostFootingModule(),` as the last entry.
8. `CoopSettingsRelevance.cs`: after `"EnableAnimMemoryProbe",` insert
   `        // Lost Footing (docs/features/lost-footing.md): the census writes [LostFooting] lines and reads the battle, never changes it.`
   and `        "LostFootingCensusEnabled",`.
9. P-COUNT with one setting, class `TaomSettings` (split `<n> in \`TaomSettings\``).
10. `docs/features/lost-footing.md` edits:

| Old | New |
|---|---|
| ``**The gate** is `FellFoesMissionGate.IsEligible` unchanged, passed in by the module:`` | ``**The gate** is `LostFootingMissionGate.IsEligible` (the 1.5.x line's Fell Foes gate expression), passed in by the module:`` |
| ``creature` (carries a behavior tree that is not a race-ability tree; Race Abilities attaches one to`` (line end) plus the next line's start `every profiled soldier, so those are soldiers)` | ``creature` (carries a built behavior tree)`` |
| `## Engine facts (v1.5.5)` | `## Engine facts (v1.5.5; v1.4.8 lines in "On the 1.4.x line")` |
| ``one switch, **Lost Footing Census**, default on.`` | ``one switch, **Lost Footing Census** (`LostFootingCensusEnabled`), default off on the 1.4.x line.`` |
| ``- `ICoopSessionProvider`, `FellFoesMissionGate`, `MountDespawnMissionGate`: the gate.`` | ``- `ICoopSessionProvider`, `LostFootingMissionGate`, `MountDespawnMissionGate`: the gate.`` |
| ``- `BehaviorTreeAgentComponent`, `RaceAbilityBehaviorTree`: the creature test.`` | ``- `BehaviorTreeAgentComponent`: the creature test.`` |
| `the Fell Foes gate wired in |` | `the census gate wired in |` |
| `In game, with the Lost Footing census on (the default), send` | `In game, with the Lost Footing census switched on (off by default on the 1.4.x line), send` |

    Then insert before `## Why This Exists`:

```markdown
## On the 1.4.x line

Ported by plan 117 from the 1.5.x line (`639df895b`), with three changes. The switch is
`LostFootingCensusEnabled`, **off by default** (the 1.5.x key `LostFootingCensus` defaults on, and a persisted
value must not meet a flipped default). The gate is `LostFootingMissionGate`, the same expression as the 1.5.x
line's Fell Foes gate (Fell Foes is not on this line). The creature kind counts any built behavior tree (no
Race Abilities on this line). Engine reads re-read on v1.4.8: `Agent.IsOnLand` `Agent.cs:3314-3317`
(`MovementMode` `:682`, the same mode values), `GetAIMoveDestination` `:2032`, `GetTargetPosition` `:2619`,
`Mission.GetWaterLevelAtPosition` `Mission.cs:2440`, `Scene.GetWaterLevel` `Scene.cs:265`,
`WorldPosition.GetNavMesh` `WorldPosition.cs:124-128` (it calls `ValidateZ`, a no-op for a position the
native side already marked valid; that the native side does so on v1.4.8 is UNVERIFIED), and
`AgentFlag.UnreachableViaNavMesh`. The `MovementOrder.cs` lines below are v1.5.5's.
```

    `docs/features/mission-perf-heartbeat.md`: add to its "On the 1.4.x line" bullets
    `- **Stranded soldiers** have an opt-in census: [lost-footing.md](lost-footing.md).`

**Verify**: Build; `FILTER="FullyQualifiedName~TAOM.Tests.Features.LostFooting|FullyQualifiedName~TAOM.Tests.Composition|FullyQualifiedName~SettingsFingerprintTests|FullyQualifiedName~SettingRequireRestartPostureTests|FullyQualifiedName~ResetForUnloadSweepTests"`:
`Failed: 0`; Test: Total = (B3 total) + the LostFooting files (about 155 results by a source count), `Failed: 0`;
`grep -rn 'FellFoes\|RaceAbilit' Main/Features/LostFooting Main/Adapters/LostFootingAdapter.cs TAOM.Tests/Features/LostFooting | grep -v '1.5.x'`
prints nothing; `grep -c 'LostFootingModule()' Main/Composition/FeatureModules.cs` `1`; Docs: no counter above B1's.

**B4.3 Commit B4.** Stage every path of B4.1 and B4.2 (checked-out files are staged; add the gate,
`TaomSettings.cs`, `FeatureModules.cs`, `CoopSettingsRelevance.cs`, `SettingsFingerprintTests.cs`, the two
count docs, `lost-footing.md`, `mission-perf-heartbeat.md` and the edited LostFooting files). `msg-B4.txt`:

```text
feat(lost-footing): v2.0.28 - opt-in census of stranded soldiers

Adds the Lost Footing census, off by default (Battle Tactics, Lost
Footing, "Lost Footing Census"). Switched on, it reads up to 64
agents a frame in field battles, sieges and sally-outs and logs
soldiers, mounts and creatures stranded off the walkable ground or in
water, with a summary per battle. It only reads; it never moves or
changes anything. The 1.5.x key defaults on, so this line uses its
own key; its gate is the mount-despawn allowlist with the co-op
stand-down and ended missions excluded, as on 1.5.x. IStampClock and
StopwatchStampClock are copied from 346f3e093 because this tree lacks
them; nothing else of that commit comes with them.

Research: taom-src v1.4.8 Agent.IsOnLand, GetAIMoveDestination,
GetAgentFlags, Mission.GetWaterLevelAtPosition, Scene.GetWaterLevel,
IsPositionOnADynamicNavMesh, WorldPosition.GetNavMesh, Formation.
GetOrderPositionOfUnit
Not-tested: the census in a live battle
Save-compat: no saved state; one new MCM key, default off
Ported-from: 639df895becd1966fbe1dc15e566b6275372e75e
Ported-from: 346f3e093ccc2c146107a3b7e005c0c06963cd85
```

(If B0 printed `stamp-present`, drop the last `Ported-from` line and the body sentence that names `346f3e093`.)

### Step B5: review gate and hand-off

1. Run every Done criterion of both Parts.
2. Run `/deep-review` on `git diff $BASE..HEAD` and ask it to probe the "Maintenance notes" spots. Act only on
   findings that change runtime behaviour or break this plan; land each as one extra commit
   `fix(mission-perf): v2.0.28 - review follow-ups` (tests first, same procedure). Never rewrite earlier
   commits. If the session cannot invoke skills, say so; the integrator runs it. Never run `/review-codex`
   or `/codex-verify` (paid; maintainer only). Recommend `/verify-bindings` to the integrator.
3. Hand-off report (the integrator records it in `plans/README.md`; never edit that file here): status of each
   Part (done, or stopped and why); branch; each commit's short SHA and subject; T0, TB0 and final Total/Passed/Skipped; the
   three benchmark values; the binding-gate skip list; the B0 outputs (DLL size, stamp state, GroupOrder, `maploaddoc` state);
   lint counters before and after; the `/deep-review` outcome; "In-game checks owed"; a note that
   `Dependencies/Foundation/PatchShieldPolicy.cs` changed, so the release must pair TAOM.Dependencies (#371); a
   note that `af990ca76`'s three inventory-memory corrections (Disposition) are not landed here:
   `docs/features/battle-load-diagnostics.md` goes to WP120 Part A3,
   `docs/reviews/rca-memory-instruments-2026-09-12.md` and `docs/investigations/native-commit-audit-2026-08.md` go to WP142.

### Part B done criteria

- [ ] Build `0 Error(s)`; Test `Failed: 0`, Total = TB0 + the B1, B2, B4 deltas as recorded, Skipped = TB0's.
- [ ] `git diff --stat 685c6a1e0 HEAD -- Main/Features/MapPerf TAOM.Tests/Features/MapPerf` lists exactly
      `Patch101_MapFrameProfiler.cs`, `Patch101_MapFrameProfilerViews.cs`, `TickEventListenerWalker.cs`, `MapSpeed.cs`,
      `MapFrameProfilerWiringTests.cs`, `MapFrameProfilerBindingTests.cs`.
- [ ] `grep -cE '"TaleWorlds\.(MountAndBlade\.Mission\.WaitTickCompletion|Engine\.ManagedScriptHolder\.TickComponents|CampaignSystem\.Campaign\.Tick|CampaignSystem\.CampaignEvents\.Tick)",' Dependencies/Foundation/PatchShieldPolicy.cs`
      prints `4`.
- [ ] `grep -c 'Module()' Main/Composition/FeatureModules.cs` is two higher than
      `git show $BASE:Main/Composition/FeatureModules.cs | grep -c 'Module()'`;
      `grep -cE 'MapFrameProfilerInstaller.OnGameInitialized\(|MapSessionHooks.EndSession\("gameEnd"\)' Main/SubModule.cs` prints `2`.
- [ ] `python -m unittest discover -s tools/tests -t . -p "test_audit_battle_equipment_memory.py"` `OK`; Tool suite
      no new failure; Data command no new ERROR vs `$AUX/data0.txt`.
- [ ] Docs: `dead_links` and `orphan_features` not above `$AUX/lint0.txt`, `missing_features` one lower
      (MissionPerf; MapPerf and LostFooting arrive with their docs), `ai_dashes: 0`.
- [ ] Eight subjects (nine with a review commit) pass the A-part `awk` check; no `CHANGELOG.md`; `git status --porcelain` empty.

## Disposition

Simulation re-run this session: `git merge-tree --write-tree --name-only --no-messages --merge-base=<sha>^ origin/bannerlord-1.4.5 <sha>`; exit 1 (conflict) for all 14.

| 1.5.x commit | Subject | Conflicted paths (beyond CHANGELOG and lessons) | Disposition |
|---|---|---|---|
| `5dfa8f4cb` | feat(combat): v2.0.29 - culture doctrines for field-battle AI (#608) | `Main/IoC.cs`, `Main/SubModule.cs`, 12 language files, `SettingsFingerprintTests.cs`, coop docs | **Extracted** (A1): the three heartbeat files and `FrameStatsTests` (at `685c6a1e0`), the setting, the relevance key, one `SubModule.cs` line; doc in A4. CultureDoctrine, `culture_doctrines.json`, its strings, the IoC line: OUT (1.5.x feature). The `formations-and-team-ai.md` tactic table: WP144 (with `578770e03`) |
| `af990ca76` | docs(perf): v2.0.32 - engine performance programme records and plans | `provenance-register.md`, `lessons/state-lifecycle-save.md`, `plans/README.md` | **Adapted** (A4): the engine page with a v1.4.8 section; the toolchain row rewritten for v1.4.8's three hosts (the 1.5.x "runs on .NET Framework" claim is not established for 1.4.8 players); a new provenance row. **Not landed here, owners named** (B5's hand-off says so): the inventory corrections to `docs/features/battle-load-diagnostics.md` go to WP120 Part A3, those to `docs/reviews/rca-memory-instruments-2026-09-12.md` and `docs/investigations/native-commit-audit-2026-08.md` go to WP142 (they cite v1.5.3 lines; this session re-read v1.4.8 `GameStateManager.OnPushState` `:257`, listeners `:277-279` before `Common.MemoryCleanupGC()` `:285`). Skipped: `plans/028-042`, `plans/README.md`, `plans/_audit/2026-10-02-perf/**`, the MithrilForge adoption record (1.5.x records); lessons and INDEX (WP142) |
| `6eb2e1b90` | feat(mission-perf): v2.0.32 - per-behaviour tick profiler and hitch log | PatchShieldPolicy, graphics adapter, settings, Patch91, relevance, SubModule, posture and policy tests | **Transplanted** (A2) at the end state; adapter members, settings, provider, Patch91 hunk, wiring by hand. PatchShield mechanism, the agent-tick entry, `FormatHotMethodSkip`, `LogHotMethodSkip` and their tests: already WP107. Review record, RCA, lessons, plan text: skipped |
| `f6bf0e588` | feat(map-perf): v2.0.32 - campaign map frame profiler, off by default | PatchShieldPolicy, settings, provider, relevance, SubModule, provider, posture, profiler-binding and reflection tests | **Transplanted** (B1) with the v1.4.8 view list and the 1.4.x wiring anchor; time adapter, settings, exclusions, wiring by hand; doc from `43ca92ee9` |
| `daba6f0a0` | feat(mission-perf): v2.0.32 - hitch probe; spawn, script, clip timing | PatchShieldPolicy, settings, provider, relevance, 8 MissionPerf files, SubModule, 9 tests | **Transplanted** (A2) plus the v1.4.8 `SpawnAgent` signature (A2.3); the two exclusions and two policy tests by hand; its `TickEventSwapTests` edit arrives in B1's `685c6a1e0` copy |
| `a9f10688f` | feat(mission-perf): v2.0.32 - log clip memory against the 12 MiB budget | animation adapter, `FeatureModules.cs`, settings, relevance | **Transplanted** (B2) minus WP109's two helpers and four tests; module through `FeatureModules.All` (D2) |
| `67eff55cf` | feat(perf-runs): v2.0.32 - per-mission rows and A/B compare from logs | `MissionPerfLine.cs`, `FrameStatsTests.cs`, `tools/README.md` | **Transplanted** (A3); its two comment hunks are in A1's copies; README row: WP142; review, RCA, lessons skipped |
| `3dac7ab5b` | feat(tools): v2.0.32 - rank the equipment assets a battle preloads | bookkeeping only | **Transplanted** (B3), test at `569f6750d`; README row: WP142; review, RCA, plan skipped |
| `43ca92ee9` | fix(perf): v2.0.32 - integration fixups and review for the perf plans | PatchShieldPolicy, 5 MissionPerf files, XmlMerge patch, 6 tests | **Transplanted file by file** (A2 MissionPerf code and tests; its policy comment consolidation becomes the 1.4.x comment in A2.2 item 8; its removal of a `6eb2e1b90` policy test is moot, that test never came). XmlMerge comment: WP118. Docs in A4 and B1. API snapshot rows: WP137. `plans/_audit`: skipped |
| `0273e2ea4` | feat(mission-perf): v2.0.34 - time the camera and PreDisplay per frame | settings hint, 10 MissionPerf files, 13 tests, `perf_runs.py` | **Transplanted** (A2, A3); docs A4; engine-page row comes with the page |
| `c76125773` | fix(mission-perf): v2.0.34 - cameraMs review fixes in tools and docs | Patch98, camera hooks, 6 tests, `perf_runs.py` | **Transplanted** (A2, A3); its v1.5.5 PreDisplay address line is a v1.5.x lead under the page's 1.4.x note |
| `fdee18f02` | docs(mission-perf): v2.0.34 - shield timing covers all four targets | Patch98, `NativePinGateTests.cs`, `perf_runs.py` | **Transplanted** (Patch98 comment in A2, tool comment in A3, doc lines in A4). `NativePinGateTests` summary: WP109 (tip copy). Codex prompt and RCA: skipped |
| `685c6a1e0` | perf(missionperf): v2.0.34 - log busy time, frame limiter and vsync | graphics adapter, settings hint, 5 MissionPerf files, 3 tests, `perf_runs.py` | **Transplanted** (A1 heartbeat, A2 profiler and adapter members, A3 tool); docs A4. `culture-doctrine.md` line: OUT; feature-map line: WP142 |
| `639df895b` | feat(lost-footing): v2.0.35 - census of soldiers stranded in battle | `FeatureModules.cs`, relevance, FellFoes gate, MountDespawn gate, `TaomSettings.cs`, FellFoes and hot-path tests | **Adapted** (B4): module through `FeatureModules.All` (D2); a local gate (no Fell Foes on 1.4.x); Race Abilities clause removed; renamed key, default off; `IStampClock` copied from `346f3e093` if absent. Skipped: FellFoes files and test, the MountDespawn comment, hot-path test rows, INDEX and feature-map lines |

Related commits that touch these files but are not taken here: `6d7e67f3f`, `c91380274`, `a15563a3c`,
`d021426b0` (co-op client split, D7); `d96f349f1` (WP130); `943ab74c0` (WP125: a menu category for the
map profiler); `ad3967222`, `4407fbc05` (WP138: `perf_runs.py`); `9c310bc23`, `4f9985852`, `15e52d7af`,
`12b010626`, `f39b399b4` (1.5.x engine pin rows; WP109 holds the 1.4.x row); `ecaa67d26`'s
`map-perf-profiler.md` lines (WP120 when its base has the doc; otherwise the integrator applies the three
rows after both WP117 and WP120 merge, see Maintenance notes); `2ac85fbe7`, `cd1fbdb6f` (WP144, bookkeeping).

## Test plan

- New test classes (transplanted unchanged except where named): A1 `FrameStatsTests` (13),
  `MissionPerfHeartbeatBehaviorTests` (1); A2 the 26 MissionPerf classes (about 281 results by a source count;
  `HitchProbeOverheadBenchmarkTests` is opt-in and reads Skipped by default), 11 provider tests, 2 policy
  tests, 2 reflection rows; B1 the nine MapPerf classes (about 113), `TimeControlAdapterBindingTests` (1),
  1 provider test, 4 reflection rows (two MapPerf tests adapted); B2 five AnimMemory classes (about 59);
  B4 eight LostFooting files (about 155; three wiring tests replaced 3-for-3, the settings tests re-pointed
  to the renamed key). The exact numbers are what each run reports; record them.
- Gates that see the new code and must stay green: `SettingsFingerprintTests` (counts and docs),
  `SettingRequireRestartPostureTests` (three restart-required toggles allowlisted), `TAOM.Tests.Composition`
  (two modules, no double wiring), `PatchCategoryApplierTests` (no `.PatchCategory(`),
  `ResetForUnloadSweepTests`, `NativePinGateTests`, the binding gate.
- Structural patterns: `MissionTickProfilerWiringTests` and `MapFrameProfilerWiringTests` pin the
  `SubModule.cs` placements by source text; the binding tests feed the installed v1.4.8 IL through the
  production rewrites.
- Not unit-testable (for `Not-tested:`): Patch97, Patch98 and Patch101 in a live game; the clip-loading
  sample against the engine's own loads; the armed `[AnimMem]` line on the real module; the census in battle.

## STOP conditions

Stop and report (do not improvise) if:

- A0.3 or A0.4 fails: a prerequisite file, member or anchor is missing or duplicated (a WP in "Depends on"
  is not merged), or a `MapPerf`/`LostFooting`/`FrameStats` file or a `Patch97`/`Patch98`/`Patch101` string exists.
- WP109's `TickProfileLines.cs` is not the trimmed file (A2.2 item 1 prints `0`) and differs from `685c6a1e0`'s.
- A RED step does not fail as described, or a GREEN step leaves any failure other than the ones named
  (in particular A2.2's two `SpawnAgent` failures must appear before A2.3 and vanish after it).
- A binding test reports a site count other than OnTick 2, OnPreTick 2, SpawnAgent 2, TickComponents 5,
  `CampaignEvents.Tick` 1, or any target unresolved after A2.3. Never edit a binding test to pass.
- A2.7: a benchmark run at 50 us per frame or more, or a failing run.
- B0: `TaleWorlds.Native.dll` is not 14,185,944 bytes, or `IStampClock.cs` exists and differs from `346f3e093`'s.
- The work needs `Main/IoC.cs`, the csproj files, `Directory.Build.props`, a `.claude/` file or `CHANGELOG.md`.
- A module would need `OwnsSaveData`, a game model or `IoC.Resolver` (D2).
- The full suite fails at the end of a step, or the Skipped count rises by more than A2's one benchmark.
- A hook denies a commit (report its message verbatim; never bypass it).
- A step's verification fails twice after a reasonable fix attempt.

## In-game checks owed (maintainer; `Logs\taom_debug_<timestamp>.log` unless named)

On the merged branch, in a build that ships TAOM and TAOM.Dependencies from one build:

1. **First game start**: `[TickProfiler] probe install: category applied, enabled by hitch probe, targets Mission.OnPreTick,...`
   with the bookkeeping cost, no `[PatchApply] ... Patch98 ... FAILED`, no `camera bracket not installed`.
2. **A field battle of a few minutes**: a `[MissionPerf]` line every 5 s; one `[PerfContext]` line (note
   `clr=`: 6.0.x would mean the CoreCLR host); an `anim-loading sample` median line at the first measured
   mission; `[TickProfile]` windows. The game must not crash in the first battles with the probe on: a native
   fault in the clip-loading sample would crash with no probe line first.
3. **A large siege**: `[Hitch]` and `[HitchDetail]` pairs for slow frames; count the INFO lines per minute
   (the classifier's estimate was 2 to 4 per 5 s besides the heartbeat).
4. **A/B of the probe's cost**: three battles each with "Enable Hitch Probe" off and on (restart between),
   comparing `[MissionPerf]` `avgMs` (method in the feature doc, "Reading an A/B"); `tools/perf_runs.py` reads the logs.
5. **Tick profiler**: switch it on, restart: `attribution install: Mission.SpawnAgent sites 2/2, ManagedScriptHolder.TickComponents sites 5/5`,
   and `[TickProfile] top=` names behaviours.
6. **Clip probe**: `[AnimMem] armed: TaleWorlds.Native.dll ... loadSite=0x23AADF budgetSite=0x23AB04 counter=0xDA59B0 budget=0xB2BC9C budgetBytes=12582912`,
   periodic `[AnimMem]` lines and a summary at mission end; no `[AnimMem] disabled` on build 119303.
7. **Map profiler**: switch it on, restart, two minutes on the map with fast forward: `[MapProfile]` every 5 s,
   an install line naming 6 core targets and 2 views, a `[MapProfileSummary]` at quit to menu.
8. **Lost Footing**: switch it on, a field battle: one start line, census lines while someone is stranded, one summary.
9. **A frozen battle, if one happens**: the `[MissionPerf]` lines stop; read WP106's `[MissionStall]` stacks;
   no `[Hitch]` line is written for a frame that never ends.
10. **Tools**: `python tools/perf_runs.py <log>` on a new log; `python tools/audit_battle_equipment_memory.py`
    against the installed packs into a scratch folder (it deletes its previous report there).

## Maintenance notes

- **Later WPs extend these files**: WP125 (`943ab74c0`) adds the map profiler's menu category; WP130
  (`d96f349f1`) re-anchors the composition kernel test to the `MissionTickProfilerBehavior(` line in
  `SubModule.cs` (keep that line directly before `BattleActionBarMissionView`); WP138 (`ad3967222`,
  `4407fbc05`) extends `perf_runs.py`; WP118 brings Patch89, the load-time stamps (`IStampClock`, the same
  bytes) and `43ca92ee9`'s XmlMerge comment, and its Part C removes the "with WP118" sentences when this plan's
  docs are on its base; WP120 owns the NameplateCull hint that names `[MapProfile]`; `af990ca76`'s inventory
  corrections go to WP120 Part A3 (`docs/features/battle-load-diagnostics.md`) and WP142
  (`docs/reviews/rca-memory-instruments-2026-09-12.md`, `docs/investigations/native-commit-audit-2026-08.md`), each
  re-cited with v1.4.8 `GameStateManager.cs:257`, `:277-279`, `:285`; WP137 brings `native_decompile.py`
  (re-derive the engine page's RVAs) and the snapshot fix (regenerate `patch-targets.md` for Patch97, Patch98,
  Patch101); WPs 142 to 144 sync lessons, REVIEW-LOG, feature-map and README rows, CLAUDE.md's category count
  and the engine docs.
- **Integrator, wave 2 merges** (lanes cut their branches in parallel, so each `test -f` here reads the
  executor's base, not the merge order). (1) `Main/SubModule.cs`, `OnGameEnd`: keep both additions, this plan's
  `MapSessionHooks.EndSession("gameEnd")` try/catch (B1) and WP119's `ResetSessionCaches()` lines in the teardown
  `try`. (2) If this plan's hand-off says `maploaddoc-absent` and WP118's Part C did not see this plan's docs, then
  after both merge remove the sentences: in `docs/features/mission-perf-heartbeat.md` restore
  ``frame counter; `[MapLoad]` reports fps on the campaign map only.`` and shorten the "Not on this line" bullet
  to the culture doctrine feature and co-op's client-only patch attribute; in `docs/features/map-perf-profiler.md` delete from `Patch89 and`
  through `besides this profiler.` and restore `([map-load-diagnostics.md](map-load-diagnostics.md))`; in
  `docs/reference/harmony-patch-registry.md` delete the `On the 1.4.x line Patch89 arrives with WP118` sentence.
  (3) After both WP117 and WP120 merge, apply to `docs/features/map-perf-profiler.md`'s settings table, after the
  `Tick Profiler Top Behaviours` row, whichever of plan 120's Steps C4 and D2 rows WP120's base could not take:
  `| Cull Hidden Settlement Nameplates | on | live; [nameplate-cull.md](nameplate-cull.md) |`,
  `| Release Map View Memory | on | live; [map-view-release.md](map-view-release.md) |`,
  `| Map View Release Interval | 20 | live, 1 to 1000; [map-view-release.md](map-view-release.md) |`.
- **Two per-frame stamps on purpose**: WP113's `MainThreadHeartbeat` beats in `OnApplicationTick` for liveness
  in every state; the `[MissionPerf]` heartbeat stamps inside the mission tick for frame statistics. Merging
  them would change both features' measurements; leave both.
- **What a reviewer should probe**: (1) the two-parameter `SpawnAgent` in both spawn classes; (2) the
  installer inside the once-per-process block after Patch91 (and Patch91b), the map installer before the
  guard; (3) the four exclusion entries and that `OnTick`, `OnPreTick`, `SpawnAgent`, `MapState.OnTick`,
  `Campaign.RealTick`, `MapScreen.OnFrameTick` stay shielded; (4) the clip probe reads only checked addresses;
  (5) the census gate equals the 1.5.x Fell Foes expression; (6) the renamed census key; (7) mission start
  resolves `IGraphicsOptionsAdapter`, registered by WP111's module: if that module ever faults at registration
  the resolve throws inside `OnMissionBehaviorInitialize`, where WP109's start guard contains it.
- **json2**: every key except the census keeps its 1.5.x name and default; a future 1.4.x default change needs
  a new key.
- **Partial `Ported-from` trailers**: B3's `569f6750d` covers only the test file's comment (WP136's pack owns
  the rest of that commit) and B4's `346f3e093` only the two stamp-clock files (WP118, wave 2 lane B, ports
  the rest; identical files added on both branches merge cleanly). A ledger built from trailers must not
  mark either commit fully ported.
- **Deferred**: `[InvOpen]` (MEMORY-09's proposed new instrument) belongs to WP120 with MEMORY-01; a v1.4.8
  check that the clip-list walk is safe while a clip loads (native, needs WP137's tools).
