# Plan 028: Profile mission frame time and allocation per behaviour and log every hitch

> **Executor instructions**: Follow this plan step by step. Run every verification command and
> confirm the expected result before moving on. If anything in "STOP conditions" occurs, stop and
> report; do not improvise. Work in the worktree and on the branch you were given. The orchestrator
> keeps `plans/README.md`; do not edit it.
>
> **Drift check (run first)**: from your worktree,
> `git diff --stat dffdf879..HEAD -- Main/SubModule.cs Main/Features/MissionPerf Main/Features/BattleLoadDiagnostics/BattleLoadDiagnosticsSettings.cs Main/Features/BattleLoadDiagnostics/IBattleLoadDiagnosticsSettingsProvider.cs Main/Features/BattleLoadDiagnostics/BattleLoadDiagnosticsSettingsProvider.cs Main/Features/BattleLoadDiagnostics/Hooks/Patch91_MissionTickStallProbes.cs Main/Features/BattleLoadDiagnostics/BattleLoadDiagnosticsIoC.cs Main/Features/BattleLoadDiagnostics/MemorySampleReader.cs Main/Features/BattleLoadDiagnostics/Domain/MemorySample.cs Main/Features/BattleCorpses/BattleCorpsesModule.cs Main/Features/CoopInterop/CoopSettingsRelevance.cs Main/Adapters/IGraphicsOptionsAdapter.cs Main/Adapters/GraphicsOptionsAdapter.cs Dependencies/Foundation/PatchShieldPolicy.cs TAOM.Tests/Features/MissionPerf TAOM.Tests/Features/BattleLoadDiagnostics/BattleLoadDiagnosticsSettingsProviderTests.cs TAOM.Tests/Features/CoopInterop/SettingsFingerprintTests.cs TAOM.Tests/Features/Mcm/SettingRequireRestartPostureTests.cs docs/features/mission-perf-heartbeat.md docs/reference/harmony-patch-registry.md docs/reference/feature-map.md docs/features/coop-interop.md docs/features/bannerlord-together-compat.md docs/features/mcm.md`.
> If an in-scope file changed since this plan was written, compare the "Current state" excerpts with
> the live code; a mismatch is a STOP condition. Plans 029 (adds a `MissionPerfLine` test in
> `TAOM.Tests/Features/MissionPerf/`), 030 (deletes the `Patch35_Mission_OnTick` postfix) and 034
> (PatchShield) run in the same programme; a change from them in the files above is expected drift:
> re-check only the excerpts this plan relies on.
>
> **Decided 2026-10-03 (D13):** the maintainer took `Mission.OnTick` and `Mission.OnPreTick` off
> `ExcludedTargetMethods`; only `Mission.TickAgentsAndTeamsImp` stays excluded. The "PatchShield trade-off"
> paragraph below, and the three entries its steps add, describe the first build.

## Status

- **Priority**: P1
- **Effort**: L
- **Risk**: MED (a transpiler on `Mission.OnTick` and `Mission.OnPreTick`, the hottest managed
  methods of a battle; off by default, so a player who never turns it on runs vanilla IL)
- **Depends on**: none
- **Category**: perf
- **Planned at**: commit `dffdf879`, 2026-10-02
- **Baseline at that commit**: dotnet `Failed! - Failed: 1, Passed: 12345, Skipped: 2, Total: 12348`
  (net472), failing: `EveryLanguage_DeclaresARowForEveryEnglishKey` (English keys without rows in
  the other languages; the paid translator run waits on the maintainer). Skipped:
  `WargAttack_FastWarg_InvokesRunningAttack`, `WargAttack_SlowWarg_InvokesStandingAttack`. Python
  suite: not recorded (this plan touches no `tools/` file).
- **Issue**: filed by the orchestrator before execution

## Why this matters

TAOM's only in-mission performance record is the `[MissionPerf]` heartbeat: whole frames only (fps,
average, p95 and max frame ms, agents, GC counts per 5 s). It cannot say how many milliseconds per
second any mission behaviour costs, and the 0.6 to 1.1 s single-frame hitches seen in the
2026-10-02 battle logs (with `gc2=0`, with and without trolls) are unattributed. Every other plan
in this performance programme is judged by a number, and this instrument produces it: per-behaviour
tick time and main-thread allocation, the engine phases managed code can see (the wait for the
previous frame's agent tick, the agent tick itself, everything else), and one line per hitch frame
naming where that frame went. It is off by default and installs no patch unless the maintainer turns
it on; with it off, a player pays one `[PerfContext]` line per mission, one status line per process
and a static null check in Patch91's agent-tick bracket (see "Cost discipline" for the PatchShield
trade-off).

## Current state

### Files and their roles (all read at `dffdf879`)

- `Main/Features/MissionPerf/FrameStats.cs`: pure 5 s wall-clock window (`ShouldEmit(nowSeconds)`,
  `Emit(nowSeconds)`, `Reset()`). The first `ShouldEmit` call opens the window and returns false.
- `Main/Features/MissionPerf/MissionPerfLine.cs`: the `[MissionPerf]` line, `t` formatted `{0:0}`
  with `CultureInfo.InvariantCulture`.
- `Main/Features/MissionPerf/Hooks/MissionPerfHeartbeatBehavior.cs`: the heartbeat `MissionLogic`.
  Its clock (lines 44, 52-53, 107-118):
  ```csharp
  public override void OnCreated() => Reset();
  ...
  var now = Stopwatch.GetTimestamp();
  var nowSeconds = (now - _missionStart) / (double)Stopwatch.Frequency;
  ...
  private void Reset() { _stats.Reset(); _missionStart = Stopwatch.GetTimestamp(); ... }
  ```
  It reads its toggle at most once a second (lines 24-26 and 54-58):
  ```csharp
  // The MCM instance lookup is a scan over every registered settings container; once a second
  // is plenty for a toggle, and the doctrine logic beside this reads its own toggle even less.
  private const double ToggleRefreshSeconds = 1.0;
  ...
  if (nowSeconds >= _nextToggleCheck)
  {
      _nextToggleCheck = nowSeconds + ToggleRefreshSeconds;
      _enabled = BattleLoadDiagnosticsSettings.Instance?.EnableMissionPerfHeartbeat ?? true;
  }
  ```
- `Main/Features/BattleLoadDiagnostics/Hooks/Patch91_MissionTickStallProbes.cs`: Patch91, applied
  unconditionally once per process. Lines 15-24, the agent-tick bracket this plan reuses:
  ```csharp
  [HarmonyPatch(typeof(Mission), nameof(Mission.TickAgentsAndTeamsImp), new[] { typeof(float), typeof(bool) })]
  [HarmonyPatchCategory("Patch91_MissionTickStall")]
  public static class Mission_TickAgentsAndTeamsImp_StallProbe_Patch
  {
      [HarmonyPrefix]
      public static void Prefix() => MissionTickStallProbe.AsyncAgentTick.Enter();

      [HarmonyFinalizer]
      public static void Finalizer() => MissionTickStallProbe.AsyncAgentTick.Exit();
  }
  ```
  `MissionTickStallProbe` stamps `DateTime.UtcNow` (about 1 ms resolution at best), too coarse for
  millisecond-with-two-decimals timing, so the profiler takes its own `Stopwatch` timestamps from
  the same bracket instead of duplicating the patch.
- `Main/Features/BattleLoadDiagnostics/BattleLoadDiagnosticsSettings.cs`: the MCM page
  (`AttributeGlobalSettings<BattleLoadDiagnosticsSettings>`, `FormatType => "json2"`). Its last
  group, lines 58-61:
  ```csharp
  [SettingPropertyGroup("Mission Performance")]
  [SettingPropertyBool("Enable Mission Frame-Time Heartbeat", Order = 0, RequireRestart = false,
      HintText = "Writes a [MissionPerf] line ...")]
  public bool EnableMissionPerfHeartbeat { get; set; } = true;
  ```
  Integer settings use the positional form, line 34:
  `[SettingPropertyInteger("Stall Threshold (seconds)", 10, 600, Order = 2, RequireRestart = false, HintText = "...")]`.
- `Main/Features/BattleLoadDiagnostics/IBattleLoadDiagnosticsSettingsProvider.cs` (31 lines) and
  `BattleLoadDiagnosticsSettingsProvider.cs` (61 lines): the provider over the MCM static, registered
  `Reuse.Singleton` in `BattleLoadDiagnosticsIoC.cs:10` (called from `Main/IoC.cs:178`, so no IoC
  edit is needed). Validation pattern, provider lines 54-60 (comment 54-55, method 56-60):
  ```csharp
  // Pure seam (StallWatchdogSeconds pattern, testable without the MCM static). ...
  internal static double ValidateSampleIntervalSeconds(double raw) =>
      FiniteFloatValidator.IsFiniteInRange(
          raw, MemoryPressureSampler.MinSampleIntervalSeconds, MemoryPressureSampler.MaxSampleIntervalSeconds)
          ? raw
          : MemoryPressureSampler.DefaultSampleIntervalSeconds;
  ```
  `TAOM.Tests/Features/BattleLoadDiagnostics/BattleLoadDiagnosticsSettingsProviderTests.cs` pins the
  no-MCM defaults (MCM is not loaded in tests, so `Instance` is null there).
- `Main/Features/CoopInterop/CoopSettingsRelevance.cs`: decides which MCM properties the co-op
  settings fingerprint hashes. Lines 70-72, the heartbeat's exclusion:
  ```csharp
  // The doctrine status line and the [MissionPerf] heartbeat; EnableCultureDoctrine itself
  // changes which tactics an AI team can pick and stays relevant.
  "CultureDoctrineDebug", "EnableMissionPerfHeartbeat",
  ```
- `TAOM.Tests/Features/CoopInterop/SettingsFingerprintTests.cs:211`:
  `AssertSplit(typeof(BattleLoadDiagnosticsSettings), reflected: 9, covered: 0);`. Its comment
  (lines 205-209) says the pinned count is the guard that forces a new setting to be classified, so
  moving this number together with the classification is the designed procedure, not a loosened
  gate. `EveryDocQuotingTheSettingsCounts_AgreesWithReflection` (lines 217-252) requires each of
  `docs/features/coop-interop.md` and `docs/features/bannerlord-together-compat.md` to contain
  either `**<total>**` or ` <total> MCM settings` (lines 242-245), and to quote the
  simulation-relevant count (217, unchanged by this plan; lines 246-249). Today: coop-interop.md:310 `TAOM ships **337** settings`,
  :312 `9 in BattleLoadDiagnosticsSettings`, :316 `The 120 excluded (67 counted 2026-09-22, ...,
  and the thirteen Menus & Loading Screens settings added 2026-10-01, #704)`;
  bannerlord-together-compat.md:291 `TAOM's 337 MCM settings`.
- `TAOM.Tests/Features/Mcm/SettingRequireRestartPostureTests.cs`: fails on any value setting with
  `RequireRestart = true` unless `RestartAllowlist` (lines 38-41) names it, keyed `Class.Property`,
  with a reason. Its summary (lines 23-27) says "Every TAOM setting is read live through
  `TaomSettings.Instance` (no Harmony category is gated on a setting at apply time)" and "The
  allowlist holds settings whose consumer is parked (commented out in SubModule.cs)"; both become
  false with this plan's entry. `docs/features/mcm.md:93-96` says "One is allowlisted by
  `Class.Property` with a reason: `TaomSettings.EnableNativeSkinFixes` (parked; ...)", also false
  after this plan. `docs/features/mcm.md:100-102` states the rule this plan follows: "A new setting
  whose consumer really does bind at process start goes on that list with its reason, not on a flag
  alone".
- `Main/Adapters/IGraphicsOptionsAdapter.cs` and `GraphicsOptionsAdapter.cs` (#701, BattleCorpses):
  the existing reader of the player's graphics options, registered by
  `Main/Features/BattleCorpses/BattleCorpsesModule.cs:30`. Adapter lines 17-23:
  ```csharp
  private const int MaxOptionIndex = 5;
  public int RagdollOption => ToOptionIndex(NativeOptions.GetConfig(NativeOptions.NativeOptionsType.NumberOfRagDolls), MaxOptionIndex);
  public int CorpseOption => InRange(BannerlordConfig.NumberOfCorpses, MaxOptionIndex);
  public int BattleSizeOption => BannerlordConfig.BattleSize;
  ```
  `internal static int ToOptionIndex(float raw, int max)` (line 38) returns -1 for a non-finite or
  out-of-range value. `BattleCorpseMissionBehavior` reads these from `OnMissionTick`, so the reads
  are safe in a mission. The `[BattleSettings]` line (`BattleCorpsePolicy.cs:63`) is that feature's
  own; this plan reuses the adapter, not the line.
- `Main/Features/BattleLoadDiagnostics/MemorySampleReader.cs`: `internal static class MemorySampleReader`
  (line 12) with `public static bool TryRead(out MemorySample sample)` (line 64; about 84 us per
  call, never throws); `Domain/MemorySample.cs`: `MemLoadPercent` (int, line 42) and `AvailPhysMb`
  (long, line 36).
- `Dependencies/Foundation/PatchShieldPolicy.cs:134-148`, `ExcludedTargetMethods`:
  ```csharp
  public static readonly IReadOnlyList<string> ExcludedTargetMethods = new[]
  {
      "TaleWorlds.MountAndBlade.Formation.get_UnitDiameter",
      ...
      // Patch93_CreatureBanditNoRout: CommonAIComponent.OnTickParallel asks it for every AI agent, horses
      // included, every 0.5 to 0.6 s on the TWParallel workers.
      "TaleWorlds.MountAndBlade.Mission.CanAgentRout",
  };
  ```
  `IsExcludedTargetMethod(declaringType, name)` compares `"<FullTypeName>.<MethodName>"` ordinally.
  PatchShield (`TAOM.Dependencies`) attaches a finalizer that binds `__originalMethod` (a reflection
  lookup on every call) to every method patched when a pass runs. Pass 2 runs in
  `Dependencies/SubModule.cs:293` on EVERY `OnGameInitializationFinished`, not once, so a category
  applied at TAOM's first game init is shielded at the latest by the second game init of the process.
  `Mission.OnTick` is already a TAOM target today (`Patch35_Mission_OnTick`, a postfix in category
  `Patch35_CompanionTactics` (`Patch35_Mission_OnTick.cs:16`), applied by
  `TryPatchCategory("Patch35_CompanionTactics");` at `SubModule.cs:1816`).
- `Main/SubModule.cs` (single-owner; the two exact edits are in Step 9). The once-per-process game
  init block starts at line 1565 (`if (_gameInitPatchesApplied) return; _gameInitPatchesApplied = true;`).
  Lines 1916-1923:
  ```csharp
  // Patch91 battle-freeze probes (#634): ...
  // idempotent regardless.
  TryPatchCategory("Patch91_MissionTickStall");
  IoC.Resolve<Features.BattleLoadDiagnostics.MissionTickStallWatchdog>().Start();
  ```
  Lines 2110-2113:
  ```csharp
  // [MissionPerf] frame-time heartbeat every 5 s; the measurement the doctrine A/B and any
  // later battle-AI change is judged against. Self-gates on its BattleLoadDiagnostics toggle.
  AddTaomBehavior(new Features.MissionPerf.Hooks.MissionPerfHeartbeatBehavior(IoC.Resolve<IModLogger>()));
  AddTaomBehavior(new Features.CompanionTactics.BattleActionBar.Hooks.BattleActionBarMissionView());
  ```
  `private bool TryPatchCategory(string category) => _patches.TryApply(category);` (line 907)
  contains a failing category and logs a `[PatchApply]` line. `SubModule.cs:1991` (inside the
  `SettingsFingerprintLog.WriteAcross(...)` call at lines 1988-1993) already reads
  `BattleLoadDiagnosticsSettings.Instance` inside `OnGameInitializationFinished` (the co-op
  fingerprint), and `SettingRequireRestartPostureTests` (lines 27-30) records that
  `GlobalSettings<T>.Instance` is null only "until MCM's own OnBeforeInitialModuleScreenSetAsRoot",
  which runs before any game init.
- Highest patch category number in use: `Patch96_TournamentRewards`
  (`Main/Features/TournamentRewards/TournamentRewardsModule.cs`). This plan takes
  `Patch97_MissionTickProfiler` (`git grep -n "Patch97"` returned nothing at `dffdf879`).

### Engine facts (v1.5.3; `pwsh tools/taom-src.ps1 path TaleWorlds.MountAndBlade.Mission` and the IL of the installed `TaleWorlds.MountAndBlade.dll`)

- `[UsedImplicitly] [MBCallback(null, false)] internal void OnPreTick(float dt)` (decompile line
  3546), called by native from `Mission.Tick`, on the main thread:
  ```csharp
  WaitTickCompletion();
  for (int num = MissionBehaviors.Count - 1; num >= 0; num--)
      MissionBehaviors[num].OnPreMissionTick(dt);
  TickDebugAgents();
  ```
  Its IL (55 bytes) contains exactly one `call instance void TaleWorlds.MountAndBlade.Mission::WaitTickCompletion()`
  (IL_0001, right after `ldarg.0`) and exactly one
  `callvirt instance void TaleWorlds.MountAndBlade.MissionBehavior::OnPreMissionTick(float32)` (IL_0023),
  and no call to `OnMissionTick` or `OnPreDisplayMissionTick`.
- `private void WaitTickCompletion()` (line 3601): `while (!tickCompleted) Thread.Sleep(1);`. 17 bytes
  of IL with a loop: small enough that whether the JIT inlines it into a caller is UNVERIFIED. A
  prefix and finalizer on it would be bypassed by an inlined copy, so this plan times it at its one
  call site instead (the rewritten `OnPreTick` calls a TAOM helper that invokes it through an open
  delegate, `docs/reviews/lessons/harmony-il.md` "Call private engine methods from hot-path patches
  via a cached open delegate"). The native engine calls `timeBeginPeriod(1)` at start-up, so
  `Sleep(1)` has about 1 to 2 ms granularity (verified by the orchestrator in `TaleWorlds.Native.dll`).
- `public void OnTick(float dt, float realDt, bool updateCamera, bool doAsyncAITick)` (line 3652;
  IL 1282 bytes): after the tick actions, `CheckMissionEnd` and an early `return` when
  `CurrentState != State.Continuing`, it runs (lines 3748-3791):
  ```csharp
  for (int num = MissionBehaviors.Count - 1; num >= 0; num--)
      MissionBehaviors[num].OnPreDisplayMissionTick(dt);
  if (!GameNetwork.IsDedicatedServer && updateCamera) _missionState.Handler.UpdateCamera(this, realDt);
  tickCompleted = false;
  for (int num2 = MissionBehaviors.Count - 1; num2 >= 0; num2--)
      MissionBehaviors[num2].OnMissionTick(dt);
  ... dynamic entities, HandleSpawnedItems, leave-mission input ...
  if (doAsyncAITick) TickAgentsAndTeamsAsync(dt); else TickAgentsAndTeamsImp(dt, tickPaused: false);
  ```
  Its IL contains exactly one `callvirt instance void TaleWorlds.MountAndBlade.MissionBehavior::OnPreDisplayMissionTick(float32)`
  (IL_03e1) and exactly one `callvirt instance void TaleWorlds.MountAndBlade.MissionBehavior::OnMissionTick(float32)`
  (IL_0435), each preceded by `callvirt ... List`1<MissionBehavior>::get_Item(int32)` and `ldarg.1`,
  and no `OnPreMissionTick` or `WaitTickCompletion`. So every millisecond in `OnMissionTick` delays
  the start of the parallel agent tick.
- `MissionBehavior` (`TaleWorlds.MountAndBlade.MissionBehavior.cs`, `public abstract class`):
  `public virtual void OnPreMissionTick(float dt)` (line 142), `public virtual void OnPreDisplayMissionTick(float dt)`
  (146), `public virtual void OnMissionTick(float dt)` (150). `public List<MissionBehavior> MissionBehaviors { get; }`
  on `Mission` (line 1338).
- `public void TickAgentsAndTeamsImp(float dt, bool tickPaused)` (line 3617): `TWParallel.For` over
  `AllAgents` (`Agent.TickParallel` on workers), a serial `Agent.Tick` loop, a serial `Team.Tick`
  loop, `tickCompleted = true;`, then every submodule's `AfterAsyncTickTick(dt)`. It runs on the
  asynchronous AI thread when `doAsyncAITick` is true and inline on the main thread at the end of
  `OnTick` otherwise. Because `tickCompleted` is set before `AfterAsyncTickTick`, the next frame's
  wait can end while the bracket is still open: the measured agent tick can overlap the next frame.
- `MissionState.TickMission` / `TickMissionAux` (`TaleWorlds.MountAndBlade.MissionState.cs` lines
  133-219): while paused, `dt` is 0 but `TickMissionAux` still runs every frame (so `OnPreTick`
  keeps firing). In fast-forward the engine calls `TickMissionAux` several times per rendered frame
  with `asyncAITick: false` (the agent tick runs inline on the main thread). `TickMissionAux` runs
  `CurrentMission.Tick(dt)` (native, which raises `OnPreTick`) and then, once `_missionTickCount > 2`,
  `CurrentMission.OnTick(...)`. So one profiler "frame" is one mission tick.
- `MissionScreen.OnFrameTick` (`TaleWorlds.MountAndBlade.View.Screens.MissionScreen.cs` lines
  567-604) ticks views through `_missionViewsContainer.ForEach(delegate (MissionView missionView) { missionView.OnMissionScreenTick(dt); })`:
  the call lives in a compiler-generated closure, not a simple loop, so it is NOT the same shape and
  this plan leaves it out (its time lands in `otherMs`). `Mission.OnFixedTick` (line 3536) ticks
  `OnFixedMissionTick` and is also left out.
- `public string SceneName => InitializerRecord.SceneName;` (line 1074);
  `public AgentReadOnlyList AllAgents` (line 1392).
- `TaleWorlds.Engine.Options.NativeOptions`: `public static float GetConfig(NativeOptionsType type)`
  (line 274); `NativeOptionsType` has `TextureQuality`, `ShadowmapResolution`, `ParticleDetail`,
  `NumberOfRagDolls`.
- Runtime: the game runs on the .NET Framework desktop CLR (`[MissionDiag]` logs CLR
  4.0.30319.42000). On this machine's CLR, `System.GC` has
  `public static long GetAllocatedBytesForCurrentThread()` (checked 2026-10-02 from Windows PowerShell
  on CLR 4.0.30319.42000). TAOM compiles against net472 reference assemblies, so bind it by
  reflection into a `Func<long>` and report `na` when it is absent.

### Conventions that bind this change

- **ADR-002** (`docs/adrs/002-thin-entry-points.md`): Harmony classes and mission behaviours are thin
  entry points under 150 lines; logic lives in pure classes.
- **ADR-005**: no `#if DEBUG` (the build flavour is read at run time from `DebuggableAttribute`).
- **ADR-007**: nothing that takes a sealed TaleWorlds type goes into a service. The pure classes in
  this plan (`BehaviourTickTable`, `MissionTickProfiler`, `TickProfileLines`, `TickProfilerTranspiler`)
  take `System.Type`, numbers, strings and `MethodInfo` only; engine reads stay in `Hooks/` and the
  adapter.
- **ADR-008**: pure classes fully unit-tested; hooks at 80%+ through `RequiresGame` tests; the
  Harmony classes themselves are covered by the binding gate.
- **ADR-003, ADR-004**: no `#region`, no `[Obsolete]`.
- `.claude/rules/harmony-patches.md`: read `docs/reviews/lessons/harmony-il.md` before writing the
  patch (mandatory), and the registry entry of Patch91 before editing it
  (`docs/reference/harmony-patch-registry.md`, section `Patch91_MissionTickStall`). Patches go in
  `Main/Features/<Feature>/Hooks/`, with `[HarmonyPatch]`, `[HarmonyPatchCategory]` and a
  `TryPatchCategory` call (all three or the patch is dead). Apply once per process. Transpilers
  soft-fail: on a missing anchor, log a warning and return the unmodified stream, never throw. A
  stack-identical swap (instance `callvirt` to a static `call` taking the instance first) keeps
  labels by mutating the existing `CodeInstruction`. Thread table: `Mission.OnTick` and
  `OnPreTick` run on the main thread; `TickAgentsAndTeamsImp` on the async AI thread or inline.
- `.claude/rules/csharp-architecture.md`: MCM values pass through a validating provider
  (`FiniteFloatValidator` for doubles, NaN fails); no `Agent.Index` keys; no locks on the main thread.
- `.claude/rules/tests.md`: MSTest plus NSubstitute, `Method_Condition_Result` names; a test that
  executes engine code is tagged `[TestCategory("RequiresGame")]`; a `BindingVerification` test that
  reads vanilla IL carries `[TestCategory("RequiresGameIL")]` on the method.
- `docs/reviews/lessons/harmony-il.md`, the entries that apply: "Make IL-mutating transpilers
  soft-fail", "Pin a single-occurrence transpiler swap" (exact count, bail on mismatch), "Call private
  engine methods ... via a cached open delegate", "PatchShield wraps TAOM's own patch on an engine
  method: exclude a hot target in the same change" and "The PatchShield exclusion covers every
  per-agent target" (per-frame targets go on `ExcludedTargetMethods` with a binding test), "Apply
  Harmony patches exactly once per process".
- Exemplars: `Main/Features/BannerColorPersistence/BannerColorTranspiler.cs` (a pure `Rewrite` over
  `IEnumerable<CodeInstruction>` with a warning on a missing site);
  `TAOM.Tests/Features/PartyIconScale/PartyIconScaleTranspilerTests.cs` (synthetic instruction
  lists); `TAOM.Tests/Migration/TranspilerSiteBindingTests.cs` (real engine IL through
  `PatchProcessor.GetOriginalInstructions`, `[TestCategory("RequiresGameIL")]` plus
  `[TestCategory("BindingVerification")]`); `TAOM.Tests/Features/CreatureBandits/CreatureBanditsWiringTests.cs:47-54`
  (`TargetOf(patch)`) and `:469-487` (`HotCreatureTargets_AreOnPatchShieldsExclusionList`);
  `TAOM.Tests/Composition/FeatureModuleHooksTests.cs` (a `RequiresGame` class with a
  `ProbeMissionBehavior : MissionLogic`); `TAOM.Tests/Features/Animalia/AnimaliaWiringTests.cs`
  (source pins on `Main/SubModule.cs`).

### Blast radius (`python tools/graphify_taom.py affected "<Type>" --depth 2`, graph refreshed at `dffdf879`)

- `Mission_TickAgentsAndTeamsImp_StallProbe_Patch`: No affected nodes found.
- `BattleLoadDiagnosticsSettings`: No affected nodes found (it is read through the provider and by
  `MissionPerfHeartbeatBehavior`, `SubModule.cs:1991` and the settings tests).
- `IBattleLoadDiagnosticsSettingsProvider`: `BattleLoadDiagnosticsService`, `BattleLoadStallWatchdog`,
  `ExitStallSampler`, `MemoryPressureSampler`, `MemoryStationSampler`, `MissionTickStallWatchdog`,
  their tests (`AgentBuildDiagnosticsTests`, `BattleLoadDiagnosticsServiceTests`, `ExitStallDisarmTests`,
  `MemoryPressureSamplerTests`, `MemoryStationSamplerTests`, `MissionTickStallWatchdogTests`) and the
  implementer `BattleLoadDiagnosticsSettingsProvider`. All fake it with NSubstitute or use the real
  provider, so new members break nothing.
- `BattleLoadDiagnosticsSettingsProvider`: the six `ValidateSampleIntervalSeconds_*` tests.
- `IGraphicsOptionsAdapter`: `BattleSettingsAdvisor`, `BattleCorpseMissionBehavior`,
  `BattleSettingsAdviceNotifier`, `BattleSettingsAdvisorTests` (NSubstitute), the implementer
  `GraphicsOptionsAdapter`.
- `GraphicsOptionsAdapter`: the two `ToOptionIndex_*` tests.
- `CoopSettingsRelevance`: `SettingsFingerprintTests.AssertSplit`, `NoExcludedName_IsDead`,
  `EverySettingsClass_HasItsSplitPinned`.
- `PatchShieldPolicy`: `PatchShield.IsExcludedTarget`, `PatchShield.Install`,
  `PatchShield.TryUnpatchOffendingPatches`, `PatchShieldPolicyTests`,
  `CreatureBanditsWiringTests.HotCreatureTargets_AreOnPatchShieldsExclusionList`,
  `Patch92BindingTests.EveryPatch92Target_IsOnPatchShieldsHotMethodList`. Adding entries only
  widens an exclusion.

## Design (decided; the executor implements it as written)

**Install point and toggle.** `EnableTickProfiler` (default false) is read once per process at
TAOM's first `OnGameInitializationFinished`, inside the once-per-process block, right after Patch91.
Game init precedes every mission of the process (campaign and Custom Battle alike), MCM's instance
exists by then (see Current state), and nothing that calls `Mission.OnPreTick` has run yet, so no
earlier-compiled caller can hold an inlined copy that bypasses the patch. When the toggle is off,
Patch97 is never applied (what a player still pays is under "Cost discipline"). Because the
consumer binds once per process, the setting carries `RequireRestart = true` and an allowlist entry in `SettingRequireRestartPostureTests`
with that reason (the procedure `docs/features/mcm.md:100-102` prescribes). `TickProfilerTopN` and
`HitchThresholdMs` are read at each mission start and keep `RequireRestart = false`. Measuring in a
mission requires both "installed" and the toggle read at that mission's start.

**Per-behaviour timing.** A transpiler swaps, in `Mission.OnTick`, the one
`callvirt MissionBehavior::OnPreDisplayMissionTick(float32)` and the one
`callvirt MissionBehavior::OnMissionTick(float32)` for `call` to static helpers
`MissionTickProfilerHooks.TimedPreDisplay(MissionBehavior, float)` and
`TimedMissionTick(MissionBehavior, float)`; in `Mission.OnPreTick` it swaps the one
`call Mission::WaitTickCompletion()` for `call MissionTickProfilerHooks.TimedWaitTickCompletion(Mission)`
and the one `callvirt MissionBehavior::OnPreMissionTick(float32)` for
`TimedPreMissionTick(MissionBehavior, float)`. Each swap is stack-identical and changes no control
flow. Every swap must match exactly once in its method or the whole method is left unmodified with
one warning. A helper, when not measuring, calls the virtual and returns; when measuring, it takes
the slot for `behavior.GetType()`, reads the allocation counter and `Stopwatch.GetTimestamp()`
before and after the call (inside `try`/`finally`, so a throwing behaviour still propagates its own
exception unchanged and is still recorded), and records into preallocated per-type arrays (no
allocation after a type's first sighting, no locks).

**Frames and phases.** A prefix on `Mission.OnPreTick` is the frame boundary: it closes the previous
frame (main thread). Per frame: `preDisplayMs`, `missionTickMs`, `preTickMs` (sums of the timed
behaviour calls), `waitTickMs` (the timed `WaitTickCompletion`), `agentTickMs` (wall time of every
`TickAgentsAndTeamsImp` that finished since the previous boundary, from Patch91's bracket, published
with `Interlocked` from whatever thread ran it), `frameMs` (boundary to boundary), and
`otherMs = frameMs - preDisplay - missionTick - preTick - wait - (agent tick time that ran ON the main
thread)`, clamped at 0. An agent tick on the async thread overlaps main-thread work, so it is not
subtracted; an inline one (fast-forward, synchronous AI tick) is. `otherMs` is everything managed
code does not see from these hooks: the native `Mission.Tick` (physics, scene), views and UI
(`OnMissionScreenTick`), rendering, streaming, shader compiles, plus the profiler's own bookkeeping.
A frame whose `frameMs >= HitchThresholdMs` writes one `[Hitch]` line from the boundary hook, with
its GC collection deltas, its main-thread allocation and its three slowest behaviours. Behaviour
calls are recorded into the open frame and folded into the window only when that frame closes,
exactly like the phase sums, so every window total (phases, `wallMs`, `allocKB` and `top=`) covers
the same set of closed frames; the calls of the frame still open when the window is taken land in
the next window. The first boundary of a mission only stamps the clock: what ran before it is
discarded, not counted.

**Window.** A new `MissionTickProfilerBehavior : MissionLogic`, added right after the heartbeat,
copies the heartbeat's clock exactly: `_missionStart = Stopwatch.GetTimestamp()` in `OnCreated`, a
`FrameStats` instance used only as the 5 s clock (`ShouldEmit`/`Emit`; `Record` is never called),
`t` formatted `{0:0}`. Both behaviours are created in consecutive `AddTaomBehavior` calls and tick in
the same `OnMissionTick` loop, so `[TickProfile]` and `[MissionPerf]` windows almost always close in
the same frame with the same `t`; because each behaviour stamps its own clock at a slightly
different moment, a 5 s boundary can occasionally fall between them and the two lines then differ by
one frame. At its first `OnMissionTick` it writes `[PerfContext]` (also when the profiler is off: one
line per mission). Window totals cover the frames closed since the previous line; `wallMs` is the
sum of those frames' `frameMs`; `top=` lists the top N behaviours by total ms in the window (N from
MCM, default 8).

**Mission generation.** The profiler is one static per process. `BeginMission` increments a
generation number and returns it; the behaviour keeps it and passes it to `EndMission(generation)`,
which stops measuring only when the generation is still current. So if a new mission's `OnCreated`
ever ran before the old mission's `OnEndMission` (UNVERIFIED either way), the old mission's end
cannot switch off the new one.

**The line contract (shared with plan 029's parser; pinned by literal tests on both sides).** Numbers
use `CultureInfo.InvariantCulture`; ms have two decimals (`0.00`); KB are integers (bytes / 1024,
floor); `t` is `{0:0}` seconds since `OnCreated`; booleans are lowercase `true`/`false`; Type is
`Type.Name`. `wallMs` is the sum of `frameMs` over the window's closed frames. When the allocation
counter is unavailable every KB field is `na`. When no behaviour recorded a call in the window or
frame, `top=none`. Field order is exactly:

```
[TickProfile] t=+<s>s frames=<n> wallMs=<x> preDisplayMs=<x> missionTickMs=<x> preTickMs=<x> waitTickMs=<x> agentTickMs=<x> otherMs=<x> allocKB=<x|na> top=<Type>:<ms>/<calls>/<maxMs>/<KB>,<Type>:...
[Hitch] t=+<s>s frameMs=<x> preDisplayMs=<x> missionTickMs=<x> preTickMs=<x> waitTickMs=<x> agentTickMs=<x> otherMs=<x> gc0=<n> gc1=<n> gc2=<n> allocKB=<x|na> top=<Type>:<ms>,<Type>:<ms>,<Type>:<ms>
[PerfContext] build=<Debug|Release> jitOptimized=<true|false> clr=<version> serverGC=<bool> latency=<GCLatencyMode> missionInProcess=<n> scene=<id> agents=<n> textureQuality=<n|na> shadowQuality=<n|na> particleDetail=<n|na> ragdolls=<n|na> memLoad=<pct|na> availPhysMB=<n|na> tickProfiler=<on|off> diag=<comma list or none>
```

The three literal pins (use these exact strings in `TickProfileLinesTests`; plan 029's parser tests
hold the same three strings as their `PINNED_TICK_PROFILE`, `PINNED_HITCH` and `PINNED_PERF_CONTEXT`,
and its Step 2 compares them with this plan's test once this plan has landed):

```
[TickProfile] t=+65s frames=300 wallMs=5000.00 preDisplayMs=12.50 missionTickMs=812.40 preTickMs=40.10 waitTickMs=95.00 agentTickMs=1500.00 otherMs=4040.00 allocKB=2048 top=BehaviorTreeMissionLogic:410.20/300/3.10/512,AdvancedCombatBehavior:120.00/900/1.50/64
[Hitch] t=+72s frameMs=812.35 preDisplayMs=0.40 missionTickMs=5.20 preTickMs=0.30 waitTickMs=790.00 agentTickMs=795.10 otherMs=16.45 gc0=1 gc1=1 gc2=0 allocKB=96 top=BehaviorTreeMissionLogic:2.10,AdvancedCombatBehavior:1.30,MissionPerfHeartbeatBehavior:0.05
[PerfContext] build=Debug jitOptimized=false clr=4.0.30319.42000 serverGC=false latency=Interactive missionInProcess=1 scene=battle_terrain_029 agents=0 textureQuality=1 shadowQuality=2 particleDetail=1 ragdolls=3 memLoad=61 availPhysMB=12034 tickProfiler=on diag=battleLoad,stallWatchdog,stallBundle,exitSampler,freezeSampler,memSampler,missionPerf
```

Inputs for those pins: TickProfile `t=65.2`, 300 frames, wall 5000, preDisplay 12.5, missionTick 812.4,
preTick 40.1, wait 95, agent 1500, other 4040, alloc 2,097,152 bytes, top
(`BehaviorTreeMissionLogic`, 410.2 ms, 300 calls, max 3.1 ms, 524,288 bytes) and
(`AdvancedCombatBehavior`, 120 ms, 900, 1.5, 65,536). Hitch `t=72.4`, frame 812.35, 0.4, 5.2, 0.3,
790, 795.1, other 16.45, gc 1/1/0, alloc 98,304 bytes, top (`BehaviorTreeMissionLogic`, 2.1),
(`AdvancedCombatBehavior`, 1.3), (`MissionPerfHeartbeatBehavior`, 0.05). PerfContext as printed,
`ragdolls=3` from the adapter's `RagdollOption`, `shadowQuality` from `ShadowmapResolution`.

`[PerfContext]` sources: `build=Debug` when `DebuggableAttribute.IsJITOptimizerDisabled` is true on
TAOM's own assembly (`typeof(MissionTickProfilerBehavior).Assembly`), else `Release`;
`jitOptimized` is its negation (attribute absent: `Release`, `true`); `clr` is
`Environment.Version`; `serverGC` is `GCSettings.IsServerGC`; `latency` is `GCSettings.LatencyMode`;
`missionInProcess` counts `MissionTickProfilerBehavior.OnCreated` calls this process (starts at 1);
`scene` is `Mission.SceneName` (`unknown` when null or empty); `agents` is `Mission.AllAgents.Count`
at the first tick; the three quality fields from the extended `IGraphicsOptionsAdapter` (-1 prints
`na`); `ragdolls` from `RagdollOption` (-1 prints `na`); `memLoad` and `availPhysMB` from
`MemorySampleReader.TryRead` (`na` when it returns false); `tickProfiler=on` when this mission is
measuring; `diag` lists, in this order, the tokens whose toggle is on: `battleLoad`
(`IsEnabled`), `stallWatchdog` (`StallWatchdogEnabled`), `stallBundle`
(`StallWatchdogBundleEnabled`), `exitSampler` (`ExitStallSamplerEnabled`), `freezeSampler`
(`MissionTickStallSamplerEnabled`), `memSampler` (`MemorySamplerEnabled`), `missionPerf`
(`BattleLoadDiagnosticsSettings.Instance?.EnableMissionPerfHeartbeat ?? true`, the heartbeat's own
read); `none` when no token is on.

**Status lines use a different tag: `[TickProfiler]`.** Plan 029's log segmenter sends every line
that contains the substring `[TickProfile]` to its data parser and counts a line it cannot parse as
malformed, so no status, warning or error line may contain `[TickProfile]`, `[Hitch]`,
`[PerfContext]` or `[MissionPerf]`. `[TickProfiler]` does not contain `[TickProfile]` (the `r`
comes before the `]`). Every status text is built in `TickProfileLines` (Step 6) and nowhere else;
the exact texts, with `<...>` filled by the caller:

```
[TickProfiler] off: 'Enable Tick Profiler' is off at game start (or MCM was not ready); no patches installed
[TickProfiler] install: category <applied|failed>, Mission.OnTick sites <n>/2, Mission.OnPreTick sites <n>/<expected>, allocation counter <available|na>
[TickProfiler] WaitTickCompletion could not be bound; waitTickMs will read 0
[TickProfiler] <method>: <target> matched <count> times, expected 1; left vanilla, the profiler records nothing for this method
[TickProfiler] <method>: helper <helper> does not fit <target>; left vanilla
[TickProfiler] on in MCM but its patches are not installed; see the [TickProfiler] install line and [PatchApply]
[TickProfiler] <where> failed, measuring stopped: <ExceptionType>: <message>
```

The pinned install literal (all sites found): `[TickProfiler] install: category applied, Mission.OnTick sites 2/2, Mission.OnPreTick sites 2/2, allocation counter available`.
`<expected>` is 2 when the `WaitTickCompletion` delegate is bound and 1 otherwise. `<where>` is one
of `install`, `frame boundary`, `mission behaviour`.

**Cost discipline.** Off: no Patch97 patch exists. Every player still pays, per process, the one
`[TickProfiler] off:` line; per mission, the `[PerfContext]` line (one `MemorySampleReader.TryRead`,
about 84 us, four engine option reads and a few `GCSettings` reads, once); and per agent tick, one
static null check in Patch91's bracket. On: two timestamps and two allocation-counter reads per
behaviour call, one dictionary lookup keyed by `Type`, no allocation after warm-up, no locks on the
main thread (the agent-tick fields use `Interlocked`), no logging per frame except `[Hitch]`.

**PatchShield trade-off.** `Mission.OnTick`, `Mission.OnPreTick` and `Mission.TickAgentsAndTeamsImp`
go on `ExcludedTargetMethods` unconditionally, so PatchShield never wraps them, whether or not the
profiler is installed. That keeps PatchShield's per-call finalizer (a reflection lookup per call)
out of the measurement and out of every frame, which is the house rule for per-frame targets
(`docs/reviews/lessons/harmony-il.md` line 683, under "PatchShield wraps TAOM's own patch on an
engine method: exclude a hot target in the same change": "per unit, per agent or per frame").
The cost: an exception thrown by ANY owner's patch on those three methods, Patch35's existing
postfix on `Mission.OnTick` and Patch91's bracket included, is no longer rescued by PatchShield for
any player. Name this trade-off in the commit body.

## Commands you will need

| Purpose | Command | Expected on success |
|---|---|---|
| Build | `dotnet build Main/TAOM.csproj -p:DisableModuleCopy=true -p:ModuleId=` | exit 0, 0 errors |
| Tests | `dotnet test TAOM.Tests -p:DisableModuleCopy=true -p:ModuleId=` | the baseline's totals plus the new tests; the only failure `EveryLanguage_DeclaresARowForEveryEnglishKey` |
| One test class | `dotnet test TAOM.Tests -p:DisableModuleCopy=true -p:ModuleId= --filter "FullyQualifiedName~<ClassName>"` | the named tests run; a filter matching nothing proves nothing |
| Binding gate | `dotnet test TAOM.Tests/TAOM.Tests.csproj -p:DisableModuleCopy=true -p:ModuleId= --settings TAOM.Tests/binding-gate.runsettings --filter "TestCategory=BindingVerification"` | all pass, none inconclusive |
| RefAsm build (hosted CI's first step, `.ai/verification.md`) | `env -u BANNERLORD_GAME_DIR -u BANNERLORD_OVERRIDE_DIR dotnet build TAOM.Tests -p:TaomGameRefs=RefAsm -p:DisableModuleCopy=true -p:ModuleId=` | exit 0 |
| RefAsm unit step (CI's second step; run right after the RefAsm build) | `env -u BANNERLORD_GAME_DIR -u BANNERLORD_OVERRIDE_DIR dotnet test TAOM.Tests --no-build -p:TaomGameRefs=RefAsm -p:DisableModuleCopy=true -p:ModuleId= --filter "TestCategory!=RequiresGame&TestCategory!=LiveInstall&TestCategory!=BindingVerification"` | no failure in any `TAOM.Tests/Features/MissionPerf` class; quote the totals line and every failing name |
| Data | `python tools/validate_moduledata.py` | 0 ERRORs (this plan touches no ModuleData; run it once at the end) |
| Docs | `python tools/lint_docs.py --fail-on-drift` | exit 0 |
| Size check | `wc -l Main/Features/MissionPerf/Hooks/*.cs` | every file under 150 lines |

Both MSBuild flags go on build AND test; prefix every dotnet command with the `TEMP="<tmp>" TMP="<tmp>"`
your dispatch rules give. Never `./build.ps1`: it deploys into the game install.

## Scope

**In scope** (the only files you create or modify):

New, `Main/Features/MissionPerf/`:
- `AllocationCounter.cs`, `BehaviourTickTable.cs`, `MissionTickProfiler.cs`, `TickProfileLines.cs`,
  `TickProfilerTranspiler.cs`

New, `Main/Features/MissionPerf/Hooks/`:
- `MissionTickProfilerHooks.cs`, `MissionTickProfilerInstaller.cs`, `Patch97_MissionTickProfiler.cs`,
  `MissionTickProfilerBehavior.cs`, `PerfContextReader.cs`

New, `TAOM.Tests/Features/MissionPerf/`:
- `AllocationCounterTests.cs`, `BehaviourTickTableTests.cs`, `MissionTickProfilerTests.cs`,
  `TickProfileLinesTests.cs`, `TickProfilerTranspilerTests.cs`, `MissionTickProfilerHooksTests.cs`,
  `MissionTickProfilerBindingTests.cs`, `MissionTickProfilerWiringTests.cs`

Modified:
- `Main/Features/BattleLoadDiagnostics/BattleLoadDiagnosticsSettings.cs` (three properties)
- `Main/Features/BattleLoadDiagnostics/IBattleLoadDiagnosticsSettingsProvider.cs`,
  `BattleLoadDiagnosticsSettingsProvider.cs` (three getters, two validators)
- `Main/Features/BattleLoadDiagnostics/Hooks/Patch91_MissionTickStallProbes.cs` (two calls added to
  the agent-tick prefix and finalizer; nothing else)
- `Main/Features/CoopInterop/CoopSettingsRelevance.cs` (three names on `Instrumentation`)
- `Main/Adapters/IGraphicsOptionsAdapter.cs`, `Main/Adapters/GraphicsOptionsAdapter.cs` (three
  read-only properties)
- `Dependencies/Foundation/PatchShieldPolicy.cs` (three entries on `ExcludedTargetMethods`)
- `Main/SubModule.cs`: single-owner, exactly the two edits in Step 9 and nothing else
- `TAOM.Tests/Features/BattleLoadDiagnostics/BattleLoadDiagnosticsSettingsProviderTests.cs` (new
  test methods)
- `TAOM.Tests/Features/CoopInterop/SettingsFingerprintTests.cs` (line 211: 9 becomes 12; nothing
  else)
- `TAOM.Tests/Features/Mcm/SettingRequireRestartPostureTests.cs` (one allowlist entry with its reason,
  and the summary at lines 23-27 amended; nothing else)
- `docs/features/mission-perf-heartbeat.md`, `docs/reference/harmony-patch-registry.md`,
  `docs/reference/feature-map.md` (the MissionPerf row), `docs/features/coop-interop.md`,
  `docs/features/bannerlord-together-compat.md`, `docs/features/mcm.md`

**Out of scope** (do NOT touch, even though they look related):
- `Main/IoC.cs`: single-owner, and nothing here needs it (the provider is registered in
  `BattleLoadDiagnosticsIoC.cs`, the adapter in `BattleCorpsesModule.cs`). If you find you need it,
  STOP and report the exact line.
- `Main/TAOM.csproj` (SDK globbing picks up new files), `Directory.Build.props`, `.claude/settings*.json`,
  `docs/adrs/*.md` (protected).
- `Main/Composition/*` and `FeatureModules.All`: the profiler is wired by hand beside the heartbeat,
  as decided; do not migrate it into a feature module.
- `MissionPerfHeartbeatBehavior.cs`, `FrameStats.cs`, `MissionPerfLine.cs`: reuse, do not change
  (plan 029 edits `MissionPerfLine.cs`'s comment).
- `Main/Features/CompanionTactics/FormationPresets/Hooks/Patch35_Mission_OnTick.cs` (plan 030 owns it).
- `MissionTickStallProbe.cs`, `MissionTickStallWatchdog.cs`, the Patch91 `TickMissionAux` class.
- Timing `OnMissionScreenTick` (view ticks, a closure-based loop) or `OnFixedMissionTick`.
- Any rate limit, sampling or per-frame log line beyond `[Hitch]`.
- `CHANGELOG.md`, `plans/README.md`, `docs/reference/taleworlds-api-snapshot/patch-targets.md` (the
  orchestrator refreshes it through `/verify-bindings`).
- The gates themselves. Never turn a gate green by editing it: deleting or `[Ignore]`-ing a test,
  loosening an assertion, or adding an allowlist entry without the reason that allowlist requires.
  The two gate edits this plan makes are the designed procedures quoted in Current state: the
  `AssertSplit` count moves with the classification, and the restart allowlist entry carries its
  reason. Anything else: STOP and report.

## Git workflow

- Commit on the branch you were given; never push or open a PR.
- One commit at the end of Step 11. Subject
  `feat(mission-perf): <version> - per-behaviour tick profiler and hitch log`, where `<version>` is
  the `<Version value=...>` in `Main/_Module/SubModule.xml` when you commit (`v2.0.32` at
  `dffdf879`, giving 71 characters; a hook refuses any other version). At most 72 characters.
- The body is the changelog entry, wrapped at 72, for a reader of the release note: what the
  profiler measures, that it is off by default and installs no patch unless turned on (a restart
  applies it), what it still costs when off (one `[PerfContext]` line per mission, one status line
  per process), the three data lines it writes, and the PatchShield exclusions with their
  trade-off (PatchShield no longer rescues a throwing patch on `Mission.OnTick`, `Mission.OnPreTick`
  or `Mission.TickAgentsAndTeamsImp`, for any player). Trailers:
  `Not-tested: live patch application, the in-game numbers, MissionTickProfilerBehavior and the [PerfContext] engine reads (maintainer's in-game check)`
  and `Save-compat: none (no saved state)`. No AI attribution trailer. Never edit `CHANGELOG.md`.
- Stage explicit paths only (every in-scope path you changed); write the message to a file and run
  `git commit -F "<file>"`. Never `--no-verify`.

## Steps

### Step 1: record the base

Record your start commit: `git rev-parse --short HEAD` (call it `<start>`; Step 11 diffs against
it). Run the full suite before any edit:
`dotnet test TAOM.Tests -p:DisableModuleCopy=true -p:ModuleId=`.

**Verify**: the totals line reads `Failed! - Failed: 1, Passed: 12345, Skipped: 2, Total: 12348`
and the one failure is `EveryLanguage_DeclaresARowForEveryEnglishKey`, or the difference is
explained by drift you report. Also run the binding gate once and record its totals.

### Step 2: the three settings, their provider and their co-op classification (TDD)

RED first:
1. In `BattleLoadDiagnosticsSettingsProviderTests.cs` add:
   `TickProfilerEnabled_NoMcmInstance_DefaultsFalse`, `TickProfilerTopN_NoMcmInstance_Defaults8`,
   `HitchThresholdMs_NoMcmInstance_Defaults250`, `ValidateTickProfilerTopN_OutOfRange_Returns8`
   (inputs 0, 21, -5), `ValidateTickProfilerTopN_RangeEdges_ReturnRaw` (1, 20),
   `ValidateHitchThresholdMs_NaN_Returns250`, `ValidateHitchThresholdMs_Infinity_Returns250`
   (both signs), `ValidateHitchThresholdMs_OutOfRange_Returns250` (49, 2001),
   `ValidateHitchThresholdMs_RangeEdges_ReturnRaw` (50, 2000). Each is ONE `[TestMethod]` with one
   assert per listed input, no `[DataRow]` (the existing `ValidateSampleIntervalSeconds_RangeEdges_ReturnRaw`
   pattern), so these add exactly 9 results to the totals.
2. In `SettingsFingerprintTests.cs:211` change `reflected: 9` to `reflected: 12`.
3. In `SettingRequireRestartPostureTests.cs` add to `RestartAllowlist`:
   `[$"{nameof(BattleLoadDiagnosticsSettings)}.{nameof(BattleLoadDiagnosticsSettings.EnableTickProfiler)}"] = "Read once per process at the first game init, where Patch97 installs or is skipped; a change needs a restart"`.

Build the test project (the filtered test command builds it). **Verify RED**: the build fails with
missing-member errors naming `TickProfilerEnabled`, `TickProfilerTopN`, `HitchThresholdMs`,
`ValidateTickProfilerTopN`, `ValidateHitchThresholdMs` and `EnableTickProfiler` (CS1061/CS0117
class diagnostics). Record the error list.

GREEN:
- `BattleLoadDiagnosticsSettings.cs`, after `EnableMissionPerfHeartbeat`, in group
  `"Mission Performance"`:
  - `[SettingPropertyBool("Enable Tick Profiler", Order = 1, RequireRestart = true, HintText = "...")] public bool EnableTickProfiler { get; set; } = false;`
  - `[SettingPropertyInteger("Tick Profiler Top Behaviours", 1, 20, Order = 2, RequireRestart = false, HintText = "...")] public int TickProfilerTopN { get; set; } = 8;`
  - `[SettingPropertyInteger("Hitch Threshold (ms)", 50, 2000, Order = 3, RequireRestart = false, HintText = "...")] public int HitchThresholdMs { get; set; } = 250;`
  Hint texts (no em or en dash): the toggle says it is off by default, times every mission
  behaviour's tick and the engine phases TAOM can see, writes a `[TickProfile]` line every 5 seconds
  and a `[Hitch]` line for each frame slower than the hitch threshold, and that a change takes effect
  after a restart because the profiler is installed once at game start; the top-N hint says how many
  behaviours each `[TickProfile]` line lists (default 8, read at each mission start); the threshold
  hint says a frame at or above it writes one `[Hitch]` line (default 250, read at each mission start).
- Interface: add `bool TickProfilerEnabled { get; }` (doc: fail-CLOSED to false when MCM is not
  ready, unlike its siblings, because it installs patches), `int TickProfilerTopN { get; }` (1-20,
  default 8), `double HitchThresholdMs { get; }` (50-2000, default 250).
- Provider: `TickProfilerEnabled => BattleLoadDiagnosticsSettings.Instance?.EnableTickProfiler ?? false;`;
  `TickProfilerTopN => ValidateTickProfilerTopN(BattleLoadDiagnosticsSettings.Instance?.TickProfilerTopN ?? 8);`;
  `HitchThresholdMs => ValidateHitchThresholdMs(BattleLoadDiagnosticsSettings.Instance?.HitchThresholdMs ?? 250);`;
  `internal static int ValidateTickProfilerTopN(int raw) => raw >= 1 && raw <= 20 ? raw : 8;`;
  `internal static double ValidateHitchThresholdMs(double raw) => FiniteFloatValidator.IsFiniteInRange(raw, 50d, 2000d) ? raw : 250d;`
  (constants named, beside the existing ones).
- `CoopSettingsRelevance.cs`: append to `Instrumentation`, after the heartbeat line, with a comment
  "The Patch97 tick profiler and its two knobs (2026-10-02): log lines, never a computation":
  `"EnableTickProfiler", "TickProfilerTopN", "HitchThresholdMs",`.
- `docs/features/coop-interop.md`: `**337**` becomes `**340**`; `9 in BattleLoadDiagnosticsSettings`
  becomes `12 in BattleLoadDiagnosticsSettings`; `The 120 excluded (` becomes `The 123 excluded (`
  and the parenthetical gains `, and the three tick profiler settings added 2026-10-02` before its
  closing `)`. `docs/features/bannerlord-together-compat.md:291`: `TAOM's 337 MCM settings` becomes
  `TAOM's 340 MCM settings`. Check `340` against reflection: 320 + 12 + 1 + 7.
- `SettingRequireRestartPostureTests.cs`, the summary: replace lines 23-28 (from
  `/// Every TAOM setting is read live` through the line ending
  `<c>GlobalSettings&lt;T&gt;.Instance</c> is null`) with exactly these lines, and leave lines
  29-30 (`/// until MCM's own OnBeforeInitialModuleScreenSetAsRoot, ...`) unchanged:
  ```csharp
  /// Every TAOM setting but one is read live through its settings instance, so the honest posture
  /// is <c>RequireRestart = false</c> everywhere, and a new setting that omits the flag is a bug this
  /// test catches. The allowlist holds the exceptions, each with its reason: a setting whose consumer
  /// is parked (commented out in SubModule.cs), where a restart does not help either but flipping
  /// the flag would promise an effect that does not exist; and <c>EnableTickProfiler</c>, the one
  /// setting a Harmony category is gated on at apply time (read once per process for Patch97).
  /// Note that no MCM setting can gate anything in OnSubModuleLoad:
  /// <c>GlobalSettings&lt;T&gt;.Instance</c> is null
  ```

**Verify**: `dotnet test TAOM.Tests -p:DisableModuleCopy=true -p:ModuleId= --filter "FullyQualifiedName~BattleLoadDiagnosticsSettingsProviderTests|FullyQualifiedName~SettingsFingerprintTests|FullyQualifiedName~SettingRequireRestartPostureTests"`
passes, with the new provider tests among the executed names. `git grep -n -e "(no Harmony category is" -e "holds settings whose consumer is parked" -- TAOM.Tests`
returns nothing.

### Step 3: `AllocationCounter` (TDD)

RED: `TAOM.Tests/Features/MissionPerf/AllocationCounterTests.cs` (no category: it runs no engine
code) with `Bind_InstalledFramework_FindsTheCounter` (`AllocationCounter.Available` is true on the
net472 test host), `ReadOrZero_AfterAllocating_GrowsByAtLeastTheAllocation` (read, allocate a
`new byte[1_000_000]` kept alive with `GC.KeepAlive`, read; delta at least 1,000,000),
`Bind_MissingMethod_ReturnsNull` (`AllocationCounter.Bind(typeof(GC), "NoSuchMethod")` is null).
Verify the build fails on the missing type.

GREEN: `Main/Features/MissionPerf/AllocationCounter.cs`, `internal static class AllocationCounter`:
`internal static Func<long>? Bind(Type owner, string methodName)` (public static, no parameters,
returns `long`, else null; `Delegate.CreateDelegate(typeof(Func<long>), method)` inside try/catch
returning null), a `private static readonly Func<long>? Reader = Bind(typeof(GC), "GetAllocatedBytesForCurrentThread");`,
`public static bool Available => Reader != null;`, `public static long ReadOrZero() => Reader != null ? Reader() : 0L;`.
Doc comment: the counter is per thread, so it measures main-thread allocation only.

**Verify**: `--filter "FullyQualifiedName~AllocationCounterTests"`: 3 passed. If
`Bind_InstalledFramework_FindsTheCounter` fails on the host, STOP (see STOP conditions).

### Step 4: `BehaviourTickTable` (TDD)

RED: `BehaviourTickTableTests.cs` (no category; keys are ordinary `System.Type`s):
`SlotFor_SameType_ReturnsSameSlot`, `SlotFor_DistinctTypes_ReturnDistinctSlots`,
`SlotFor_BeyondInitialCapacity_KeepsEarlierTotals` (300 distinct types from
`typeof(object).Assembly.GetTypes()`), `FoldFrame_AccumulatesMsCallsMaxAndAllocIntoTheWindow`
(two frames: record, fold, record, fold; window ms, calls and alloc are the sums, max the larger
single call), `Record_ZeroTickCalls_CountsEachCallOnce` (two `Record(slot, 0, 0)` in one frame,
then fold: calls is 2 and the slot appears once in the window),
`TakeTop_BeforeFoldFrame_SeesNothingFromTheOpenFrame`,
`TakeTop_OrdersByTotalMsDescending_ThenByName_AndTruncatesToN`, `TakeTop_NoCalls_ReturnsEmpty`,
`ResetWindow_ClearsTotals_KeepsSlotsAndNames`, `FrameTop_IsThisFramesOnly_AfterResetFrame`.

GREEN: `Main/Features/MissionPerf/BehaviourTickTable.cs`, `public sealed class BehaviourTickTable`
(pure, main thread only, documented as not thread-safe):
- `Dictionary<Type, int>` slot map; per-slot arrays `string[] names` (cached `Type.Name` at first
  sighting), frame arrays `long[] frameTicks`, `int[] frameCalls`, `long[] frameMaxTicks`,
  `long[] frameAlloc`, window arrays `long[] windowTicks`, `int[] windowCalls`, `long[] windowMaxTicks`,
  `long[] windowAlloc`, plus `int[] touched` and a touched count for the frame. Initial capacity 128,
  doubling (allocation only on growth or first sighting).
- `public int SlotFor(Type type)`;
  `public void Record(int slot, long elapsedTicks, long allocBytes)` updates the FRAME arrays only,
  and marks the slot touched when its `frameCalls` was 0 (calls, not ticks: a call can measure 0
  ticks);
  `public void FoldFrame()` adds every touched slot's frame values into the window (ticks, calls and
  alloc summed, `windowMaxTicks = Math.Max(windowMaxTicks, frameMaxTicks)`), then clears those
  frame values and the touched count;
  `public IReadOnlyList<BehaviourTotal> TakeTop(int n, long ticksPerSecond)` (window top by ticks,
  ties by ordinal name, only slots with window calls > 0; allocates its result once per window);
  `public IReadOnlyList<BehaviourTotal> FrameTop(int n, long ticksPerSecond)` (frame ticks of the
  open frame, used only on hitch frames, called before `FoldFrame`); `public void ResetWindow()`;
  `public void ResetFrame()` (clears the touched slots' frame values without folding them).
- `public sealed class BehaviourTotal` (same file): `Name`, `Ms`, `Calls`, `MaxMs`, `AllocBytes`,
  public constructor (the line tests build these directly).

**Verify**: `--filter "FullyQualifiedName~BehaviourTickTableTests"`: 10 passed.

### Step 5: `MissionTickProfiler` (TDD)

RED: `MissionTickProfilerTests.cs` (no category). Construct with `ticksPerSecond: 1000` so a tick
is a millisecond. Cases:
`CloseFrame_FirstBoundaryOfMission_ReturnsNullAndCountsNoFrame` (a `Record` made before the first
boundary is discarded: it appears in no window);
`CloseFrame_SumsPhasesIntoTheWindow`, with this oracle: `BeginMission(0, ...)`, `CloseFrame(100, ...)`
(first boundary); then `Record(PreDisplay, s, 2, 0)`, `Record(MissionTick, s, 10, 0)`,
`Record(PreTick, s, 3, 0)`, `AddWait(5)`, `AddAgentTick(30, onMainThread: false)`, `CloseFrame(150, ...)`;
then `Record(MissionTick, s, 20, 0)`, `AddAgentTick(15, onMainThread: true)`, `CloseFrame(250, ...)`;
`TakeWindow(8)` gives frames 2, wallMs 150, preDisplayMs 2, missionTickMs 30, preTickMs 3,
waitTickMs 5, agentTickMs 45, otherMs 95 (30 + 65); check also that wallMs equals the phase sums
plus otherMs plus the 15 ms main-thread agent tick;
`CloseFrame_OtherMs_IsFrameMinusMainThreadPhases` (an off-main agent tick is not subtracted);
`CloseFrame_AgentTickOnMainThread_IsSubtractedFromOther`;
`CloseFrame_OtherMs_NeverNegative`;
`CloseFrame_AtThreshold_ReturnsHitch` and `CloseFrame_BelowThreshold_ReturnsNull`;
`Hitch_TopThree_AreTheFramesSlowestBehaviours`;
`Hitch_GcAndAllocDeltas_AreThisFramesOnly`;
`TakeWindow_ResetsForTheNextWindow`;
`TakeWindow_TopN_ComesFromTheBehaviourTable`;
`TakeWindow_OpenFrameRecords_LandInTheNextWindow` (record, take the window before the boundary:
`top=` is empty; close the frame and take again: the call is there);
`AddAgentTick_FromAnotherThread_LandsInTheNextClosedFrame` (start a `Thread` that calls
`AddAgentTick(40, onMainThread: false)`, join it, then close a frame);
`BeginMission_ResetsFramesWindowAndBehaviourTotals`;
`EndMission_CurrentGeneration_StopsMeasuring`;
`EndMission_StaleGeneration_KeepsMeasuring` (`g1 = BeginMission(...)`, `BeginMission(...)` again,
`EndMission(g1)`: still measuring).

GREEN: `Main/Features/MissionPerf/MissionTickProfiler.cs`:
- `public enum TickPhase { PreDisplay, MissionTick, PreTick }`.
- `public sealed class MissionTickProfiler` with constructor `(long ticksPerSecond)`, properties
  `BehaviourTickTable Behaviours`, `bool Measuring` (backed by a `volatile bool`), `int MainThreadId`,
  `long MissionStartTicks`, `int Generation`.
- `public int BeginMission(long nowTicks, int mainThreadId, bool measuring, double hitchThresholdMs)`:
  resets every accumulator, the behaviour table's window and frame, the "have a boundary" flag
  (use a `bool`, not a 0 sentinel, per the sentinel-collision rule in `harmony-patches.md`), the
  last GC counts and the last allocation reading, increments `Generation`, sets `Measuring`, and
  returns the new `Generation`.
- `public void EndMission(int generation)`: `Measuring = false` only when `generation == Generation`
  (see "Mission generation" in Design).
- `public void Record(TickPhase phase, int slot, long elapsedTicks, long allocBytes)`: adds to the
  frame's phase sum and to `Behaviours.Record`.
- `public void AddWait(long elapsedTicks)`.
- `public void AddAgentTick(long elapsedTicks, bool onMainThread)`: `Interlocked.Add` on a frame
  total and, when `onMainThread`, on a frame main-thread total. The only member callable off the
  main thread.
- `public HitchFrame? CloseFrame(long nowTicks, long allocBytesNow, int gc0, int gc1, int gc2)`: on
  the first call of a mission it records the boundary (time, allocation, GC counts), discards the
  partial frame (phase sums zeroed, agent totals taken with `Interlocked.Exchange(ref x, 0)` and
  dropped, `Behaviours.ResetFrame()`) and returns null. Otherwise it computes the frame (agent
  totals taken with `Interlocked.Exchange(ref x, 0)`), builds a `HitchFrame` with
  `Behaviours.FrameTop(3, ...)` only when `frameMs >= hitchThresholdMs`, THEN calls
  `Behaviours.FoldFrame()`, adds the phase sums, `frameMs` (into `wallMs`) and the frame's
  allocation into the window, increments the frame count, zeroes the frame's phase sums and returns
  the hitch (null otherwise, with no allocation).
- `public TickWindow TakeWindow(int topN)`: returns the window's frame count, `wallMs`, phase totals,
  main-thread allocation and `Behaviours.TakeTop(topN, ...)`, then resets the window and
  `Behaviours.ResetWindow()`. It never touches the open frame.
- `public sealed class TickWindow` and `public sealed class HitchFrame` (same file), each with a
  public constructor taking every field the line needs, in line order, plus
  `IReadOnlyList<BehaviourTotal> Top` and `long AllocBytes`.

**Verify**: `--filter "FullyQualifiedName~MissionTickProfilerTests"`: 16 passed.

### Step 6: `TickProfileLines` (TDD, the contract)

RED: `TickProfileLinesTests.cs` (no category):
`BuildTickProfile_SampleWindow_MatchesThePinnedLiteral`,
`BuildHitch_SampleFrame_MatchesThePinnedLiteral`,
`BuildPerfContext_SampleContext_MatchesThePinnedLiteral` (the three literals in Design, exactly);
`BuildTickProfile_NoAllocationCounter_WritesNaForEveryKb`;
`BuildTickProfile_NoBehaviours_WritesTopNone`; `BuildHitch_NoBehaviours_WritesTopNone`;
`Build_CommaDecimalCulture_StillWritesInvariantPoints` (set `CultureInfo.CurrentCulture` to `de-DE`
in a `try`/`finally` and restore it);
`BuildPerfContext_UnreadableValues_WriteNa` (quality, ragdoll, memLoad and availPhysMB at -1);
`BuildPerfContext_NoDiagnostics_WritesNone`;
`DiagTokens_AllOn_ListsSevenInContractOrder` and `DiagTokens_SomeOn_ListsOnlyThoseInOrder`;
`BuildInstallLine_AllSitesFound_MatchesThePinnedLiteral` (the install literal under "Status lines"
in Design);
`StatusLines_NeverContainADataTag` (every status constant, and every status builder called with
sample arguments, starts with `[TickProfiler] ` and contains none of `[TickProfile]`, `[Hitch]`,
`[PerfContext]`, `[MissionPerf]`).

GREEN: `Main/Features/MissionPerf/TickProfileLines.cs`, `public static class TickProfileLines`:
`BuildTickProfile(double tSeconds, TickWindow window, bool allocAvailable)`,
`BuildHitch(double tSeconds, HitchFrame frame, bool allocAvailable)`,
`BuildPerfContext(PerfContext context)`,
`IReadOnlyList<string> DiagTokens(bool battleLoad, bool stallWatchdog, bool stallBundle, bool exitSampler, bool freezeSampler, bool memSampler, bool missionPerf)`.
`public sealed class PerfContext` (same file) holds the 16 values in line order (ints use -1 for
"unreadable", `availPhysMb` a `long` with -1, `IReadOnlyList<string> Diag`). Use
`string.Format(CultureInfo.InvariantCulture, ...)` and a `StringBuilder` for `top=`; booleans via
`b ? "true" : "false"`.
The status lines, texts exactly as listed under "Status lines" in Design:
`public const string StatusTag = "[TickProfiler]";`, `public const string OffLine`,
`public const string WaitUnboundLine`, `public const string NotInstalledLine`,
`public static string BuildInstallLine(bool applied, int onTickSites, int onPreTickSites, int expectedPreTickSites, bool allocAvailable)`,
`public static string BuildSiteCountWarning(string method, string target, int count)`,
`public static string BuildHelperMismatchWarning(string method, string helper, string target)`,
`public static string BuildFault(string where, Exception ex)` (`ex.GetType().Name` and
`ex.Message`). Every other file logs status text only through these members.

**Verify**: `--filter "FullyQualifiedName~TickProfileLinesTests"`: 13 passed. Copy the three
data literals into your report so the orchestrator can confirm plan 029 holds the same strings.

### Step 7: `TickProfilerTranspiler` (TDD, synthetic streams)

RED: `TickProfilerTranspilerTests.cs` (no category; `MethodInfo`s of `MissionBehavior` and `Mission`
are metadata reads, which work on the reference assemblies; nothing is invoked). Build streams shaped
like the real IL (`ldarg.0`, `call get_MissionBehaviors`, `ldloc`, `callvirt get_Item`, `ldarg.1`,
`callvirt MissionBehavior::OnMissionTick`), using `typeof(MissionBehavior).GetMethod("OnMissionTick")`
and friends (and `typeof(Mission).GetMethod("WaitTickCompletion", BindingFlags.NonPublic | BindingFlags.Instance)`)
as operands. As replacements use private static stub methods declared in the test class with the
helper shapes, `static void StubBehaviourTick(MissionBehavior behavior, float dt) { }` and
`static void StubWait(Mission mission) { }` (the `PartyIconScaleTranspilerTests` stub pattern); the
real helpers are exercised against real IL by the Step 8 binding tests. Cases:
`Rewrite_OnTickShape_SwapsBothBehaviourCalls_ToStaticCalls`;
`Rewrite_SwappedInstruction_KeepsItsLabels`;
`Rewrite_LeavesEveryOtherInstructionUntouched` (same count, same opcodes elsewhere);
`Rewrite_SiteMissing_LeavesStreamUnmodifiedAndWarnsOnce` (the one warning equals
`TickProfileLines.BuildSiteCountWarning(...)` for that site, so it starts `[TickProfiler] `);
`Rewrite_SiteTwice_LeavesStreamUnmodifiedAndWarnsOnce`;
`Rewrite_HelperShapeMismatch_LeavesStreamUnmodifiedAndWarns` (a helper that is not static, or whose
parameters are not `(declaring type, target parameters...)`, or whose return type differs);
`Rewrite_RunTwiceOnItsOwnOutput_NeverThrows_AndLeavesItUnmodified` (the re-application lesson);
`Rewrite_ReportsSwappedCount`.

GREEN: `Main/Features/MissionPerf/TickProfilerTranspiler.cs`:
- `internal sealed class CallSwap` (`MethodInfo Target`, `MethodInfo Helper`).
- `internal static List<CodeInstruction> Rewrite(IEnumerable<CodeInstruction> instructions, IReadOnlyList<CallSwap> swaps, string methodLabel, IModLogger? logger, out int swapped)`.
  Copy to a list. Validate each swap (non-null, helper static, helper parameters equal the target's
  declaring type followed by the target's parameter types, same return type); on failure log
  `TickProfileLines.BuildHelperMismatchWarning(...)` with `LogWarning` and return the copy unmodified
  with `swapped = 0`. Count matches per swap: an instruction whose opcode is `Call` or `Callvirt` and
  whose operand is a `MethodInfo` with the target's `DeclaringType`, `Name` and parameter count. If
  any count is not exactly 1, log one `TickProfileLines.BuildSiteCountWarning(methodLabel, target, count)`
  with `LogWarning`, return unmodified, `swapped = 0`. Otherwise set each match's `opcode = OpCodes.Call` and `operand = swap.Helper` in
  place (labels and blocks stay) and return with `swapped = swaps.Count`. Never throw.

**Verify**: `--filter "FullyQualifiedName~TickProfilerTranspilerTests"`: 8 passed.

### Step 8: the hooks, the patch, the installer and the PatchShield exclusions (TDD)

Read `docs/reviews/lessons/harmony-il.md` and the `Patch91_MissionTickStall` registry section before
this step (mandatory per `.claude/rules/harmony-patches.md`).

RED:
1. `MissionTickProfilerHooksTests.cs`, class-tagged `[TestCategory("RequiresGame")]` (it constructs
   a `MissionLogic` subclass; model on `FeatureModuleHooksTests`' `ProbeMissionBehavior`). The probe
   counts calls per virtual and can be told to throw. `[TestInitialize]` installs a fresh
   `MissionTickProfiler(Stopwatch.Frequency)` (the hooks take real `Stopwatch` timestamps) and an
   NSubstitute `IModLogger` into the hooks' statics and calls `BeginMission`; `[TestCleanup]` clears the
   hooks' statics. Cases: `TimedMissionTick_Measuring_CallsOnMissionTickOnceAndRecordsOneCall`;
   `TimedMissionTick_NotMeasuring_CallsThroughAndRecordsNothing`;
   `TimedMissionTick_BehaviourThrows_PropagatesTheSameExceptionAndRecordsTheCall`;
   `TimedPreDisplay_CallsOnPreDisplayMissionTick`; `TimedPreMissionTick_CallsOnPreMissionTick`;
   `OnFrameBoundary_FrameAboveThreshold_LogsOneHitchLine` (threshold 1 ms, `Thread.Sleep(5)` between
   two boundaries, an NSubstitute `IModLogger`; assert one `LogInfo` starting `[Hitch] t=+`);
   `OnFrameBoundary_NotMeasuring_LogsNothing`;
   `AgentTick_EnterExitOnAnotherThread_IsCountedAsOffMain` (observed through the hitch line, since
   the hooks expose no on-main/off-main split: threshold 1 ms; `OnFrameBoundary()` on the test
   thread; a `Thread` that calls `OnAgentTickEnter()`, `Thread.Sleep(20)`, `OnAgentTickExit()`,
   joined; `OnFrameBoundary()` again; parse the one `[Hitch]` `LogInfo` argument: `agentTickMs` is
   at least 15, and the `otherMs` value string equals the `frameMs` value string, because no phase
   was recorded and an off-main agent tick is not subtracted);
   `AgentTick_ExitWithoutEnter_RecordsNothing`.
2. `MissionTickProfilerBindingTests.cs` (model on `TranspilerSiteBindingTests` and
   `Patch92BindingTests`; `[ClassInitialize]` sets `_gameLoaded = GameAssemblies.EnsureLoaded()`,
   each test starts with `if (!_gameLoaded) Assert.Inconclusive(...)`):
   - `[TestCategory("BindingVerification")] [TestCategory("RequiresGameIL")]`
     `OnTickRewrite_FindsBothBehaviourCalls_InInstalledEngine`: feed
     `PatchProcessor.GetOriginalInstructions(AccessTools.Method(typeof(Mission), nameof(Mission.OnTick), new[] { typeof(float), typeof(float), typeof(bool), typeof(bool) }))`
     through `TickProfilerTranspiler.Rewrite` with `MissionTickProfilerInstaller.OnTickSwaps()`;
     assert no `LogWarning`, `swapped == 2`, and exactly 2 instructions whose operand is declared on
     `MissionTickProfilerHooks`.
   - same categories, `OnPreTickRewrite_FindsWaitAndPreMissionTick_InInstalledEngine`: call
     `MissionTickProfilerInstaller.BindWaitDelegate()` first and assert it returned true, then the
     same with `AccessTools.Method(typeof(Mission), "OnPreTick", new[] { typeof(float) })` and
     `OnPreTickSwaps()`; assert `swapped == 2`.
   - `[TestCategory("BindingVerification")]` only, `ProfiledTargets_AreOnPatchShieldsExclusionList`:
     walk the targets of `Mission_OnTick_TickProfiler_Patch`, `Mission_OnPreTick_TickProfiler_Patch`
     and `Mission_TickAgentsAndTeamsImp_StallProbe_Patch` (copy the `TargetOf` helper from
     `CreatureBanditsWiringTests.cs:47-54`) and assert
     `PatchShieldPolicy.IsExcludedTargetMethod(target.DeclaringType?.FullName, target.Name)` for each.

Build. **Verify RED**: the build fails on the missing types (`MissionTickProfilerHooks` members,
`MissionTickProfilerInstaller`, the two patch classes); after Step 8's code compiles but before the
`PatchShieldPolicy` edit, `ProfiledTargets_AreOnPatchShieldsExclusionList` fails naming
`TaleWorlds.MountAndBlade.Mission.OnTick`. Run it once in that state and quote the failure.

GREEN:
- `Main/Features/MissionPerf/Hooks/MissionTickProfilerHooks.cs`, `public static class`:
  `internal static MissionTickProfiler? Profiler`, `internal static IModLogger? Logger`,
  `internal static Action<Mission>? WaitTickCompletionCall`, `internal static bool Installed`,
  `internal static int OnTickSites`, `internal static int OnPreTickSites`;
  `public static void TimedPreDisplay(MissionBehavior behavior, float dt)`,
  `public static void TimedMissionTick(MissionBehavior behavior, float dt)`,
  `public static void TimedPreMissionTick(MissionBehavior behavior, float dt)` (each one line into a
  private `Timed(TickPhase, MissionBehavior, float)` whose shape is: read `Profiler`; if null or not
  measuring, call the virtual and return; else `slot = profiler.Behaviours.SlotFor(behavior.GetType())`,
  `a0 = AllocationCounter.ReadOrZero()`, `t0 = Stopwatch.GetTimestamp()`, `try { call } finally { profiler.Record(phase, slot, Stopwatch.GetTimestamp() - t0, AllocationCounter.ReadOrZero() - a0); }`);
  `public static void TimedWaitTickCompletion(Mission mission)` (same shape around
  `WaitTickCompletionCall(mission)`, recording with `AddWait`);
  `internal static void OnFrameBoundary()` (if measuring: `CloseFrame(Stopwatch.GetTimestamp(), AllocationCounter.ReadOrZero(), GC.CollectionCount(0), GC.CollectionCount(1), GC.CollectionCount(2))`;
  on a non-null result log `TickProfileLines.BuildHitch(...)` through `Logger.LogInfo`; the whole
  body in try/catch: on an exception call `Profiler.EndMission(Profiler.Generation)` and log
  `TickProfileLines.BuildFault("frame boundary", ex)` with `LogError`, so a fault ends measuring
  instead of repeating);
  `internal static void OnAgentTickEnter()` / `OnAgentTickExit()` (a `static long` start stamp
  written and read with `Volatile`; exit computes the elapsed time, calls
  `AddAgentTick(elapsed, Environment.CurrentManagedThreadId == profiler.MainThreadId)` and clears the
  stamp; both return at once when not measuring and never throw: wrap in try/catch);
  `internal static int BeginMission(long missionStartTicks, bool measuring, double hitchThresholdMs)`
  (returns the profiler's new generation) and `internal static void EndMission(int generation)`,
  delegating to the profiler (no-ops when `Profiler` is null; `BeginMission` then returns 0).
- `Main/Features/MissionPerf/Hooks/Patch97_MissionTickProfiler.cs`, two classes, both
  `[HarmonyPatchCategory("Patch97_MissionTickProfiler")]`:
  - `[HarmonyPatch(typeof(Mission), nameof(Mission.OnTick), new[] { typeof(float), typeof(float), typeof(bool), typeof(bool) })] public static class Mission_OnTick_TickProfiler_Patch`
    with `[HarmonyTranspiler] public static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)`
    calling `TickProfilerTranspiler.Rewrite(instructions, MissionTickProfilerInstaller.OnTickSwaps(), "Mission.OnTick", MissionTickProfilerHooks.Logger, out var swapped)`
    and storing `swapped` in `MissionTickProfilerHooks.OnTickSites`.
  - `[HarmonyPatch(typeof(Mission), "OnPreTick", new[] { typeof(float) })] public static class Mission_OnPreTick_TickProfiler_Patch`
    with `[HarmonyPrefix] public static void Prefix() => MissionTickProfilerHooks.OnFrameBoundary();`
    and the same transpiler shape with `OnPreTickSwaps()`, storing `OnPreTickSites`.
  - A header comment quoting the two engine signatures and the call-site facts from Current state.
- `Main/Features/MissionPerf/Hooks/MissionTickProfilerInstaller.cs`, `internal static class`:
  `internal const string Category = "Patch97_MissionTickProfiler";` a `private static bool _attempted`;
  `internal static bool BindWaitDelegate()` (`AccessTools.Method(typeof(Mission), "WaitTickCompletion")`,
  then `(Action<Mission>)Delegate.CreateDelegate(typeof(Action<Mission>), method)` in try/catch; sets
  `MissionTickProfilerHooks.WaitTickCompletionCall`; false on failure);
  `internal static IReadOnlyList<CallSwap> OnTickSwaps()` (the two `MissionBehavior` virtuals to
  `TimedPreDisplay` and `TimedMissionTick`);
  `internal static IReadOnlyList<CallSwap> OnPreTickSwaps()` (the `WaitTickCompletion` swap only
  when `WaitTickCompletionCall` is bound, plus `OnPreMissionTick` to `TimedPreMissionTick`);
  `internal static void InstallIfEnabled(IBattleLoadDiagnosticsSettingsProvider settings, IModLogger logger, Func<string, bool> tryPatchCategory)`:
  returns at once when `_attempted`; sets `_attempted = true` BEFORE anything else (a failing category
  is never retried); when `settings.TickProfilerEnabled` is false logs `TickProfileLines.OffLine`
  with `logger.LogInfo` and returns; otherwise sets `Logger`, creates
  `Profiler = new MissionTickProfiler(Stopwatch.Frequency)`, calls `BindWaitDelegate()` (when false,
  `LogWarning(TickProfileLines.WaitUnboundLine)`), calls `applied = tryPatchCategory(Category)`, sets
  `Installed = applied && OnTickSites == 2`, and logs
  `TickProfileLines.BuildInstallLine(applied, OnTickSites, OnPreTickSites, WaitTickCompletionCall != null ? 2 : 1, AllocationCounter.Available)`
  with `LogInfo` (all found, it reads exactly the pinned install literal in Design). Everything inside
  try/catch: on an exception log `TickProfileLines.BuildFault("install", ex)` with `LogError` and
  leave `Installed` false.
- `Dependencies/Foundation/PatchShieldPolicy.cs`: append to `ExcludedTargetMethods`, with a comment
  "Patch97_MissionTickProfiler (2026-10-02): the two per-frame methods it rewrites, and the agent tick
  whose Patch91 bracket it reads; a per-call finalizer would sit inside the measurement.
  MissionTickProfilerBindingTests.ProfiledTargets_AreOnPatchShieldsExclusionList walks the real targets.":
  `"TaleWorlds.MountAndBlade.Mission.OnTick"`, `"TaleWorlds.MountAndBlade.Mission.OnPreTick"`,
  `"TaleWorlds.MountAndBlade.Mission.TickAgentsAndTeamsImp"`.
- `Patch91_MissionTickStallProbes.cs`, the agent-tick class only: the prefix body becomes
  `{ MissionTickStallProbe.AsyncAgentTick.Enter(); MissionTickProfilerHooks.OnAgentTickEnter(); }`
  and the finalizer body `{ MissionTickStallProbe.AsyncAgentTick.Exit(); MissionTickProfilerHooks.OnAgentTickExit(); }`
  (stall probe first, so its behaviour is unchanged; the finalizer stays `void`), plus
  `using TAOM.Features.MissionPerf.Hooks;` and one comment line saying the profiler reads the same
  bracket. Do not touch the `TickMissionAux` class.

**Verify**: `--filter "FullyQualifiedName~MissionTickProfilerHooksTests"`: 9 passed (they need the
game assemblies; on this machine they run). Then run the binding gate command: all pass and none
inconclusive, including the three `MissionTickProfilerBindingTests`. `wc -l Main/Features/MissionPerf/Hooks/*.cs`:
every file under 150 lines.

### Step 9: the mission behaviour, the context reader, the adapter reads and the two SubModule edits (TDD for the wiring)

RED: `MissionTickProfilerWiringTests.cs` (no category; source pins like `AnimaliaWiringTests`, read
`Main/SubModule.cs` with `RepoPaths.ReadSource("Main/SubModule.cs", stripComments: true)`):
`SubModule_InstallsTheProfiler_InsideTheOncePerProcessGameInitBlock_AfterPatch91` (the installer
call appears exactly once, after `TryPatchCategory("Patch91_MissionTickStall");` and after
`_gameInitPatchesApplied = true;`, and before `public override void OnMissionBehaviorInitialize`);
`SubModule_AddsTheProfilerBehavior_RightAfterTheHeartbeat` (the `MissionTickProfilerBehavior`
`AddTaomBehavior` call appears exactly once, after the `MissionPerfHeartbeatBehavior` one and before
`BattleActionBarMissionView`). Verify both fail. (That `SubModule.cs` never names the category
itself is a Done-criteria grep, not a test: it would pass before the change.)

GREEN:
- `IGraphicsOptionsAdapter.cs`: add `int TextureQualityOption { get; }`, `int ShadowmapResolutionOption { get; }`,
  `int ParticleDetailOption { get; }`, each documented "the raw option value the engine stores,
  rounded; -1 when unreadable". `GraphicsOptionsAdapter.cs`: implement each as
  `ToOptionIndex(NativeOptions.GetConfig(NativeOptions.NativeOptionsType.<TextureQuality|ShadowmapResolution|ParticleDetail>), MaxRawOptionIndex)`
  with `private const int MaxRawOptionIndex = 16;` commented as a sanity bound that rejects garbage,
  not the length of the options list. Update the interface summary to say it also reports the three
  quality options for `[PerfContext]`. No unit test: the engine read cannot run offline, and the
  `-1` path is covered by `BuildPerfContext_UnreadableValues_WriteNa` (ADR-008: adapters are covered
  through their consumers).
- `Main/Features/MissionPerf/Hooks/PerfContextReader.cs`, `internal static class`:
  `internal static PerfContext Read(Mission mission, IGraphicsOptionsAdapter options, IBattleLoadDiagnosticsSettingsProvider settings, int missionInProcess, bool measuring)`
  filling every field as listed in Design; each engine read in its own try/catch falling back to -1
  or `unknown`.
- `Main/Features/MissionPerf/Hooks/MissionTickProfilerBehavior.cs`, `public sealed class MissionTickProfilerBehavior : MissionLogic`,
  constructor `(IBattleLoadDiagnosticsSettingsProvider settings, IGraphicsOptionsAdapter options, IModLogger logger)`.
  Override only `OnCreated`, `OnMissionTick` and `protected override void OnEndMission()` (never
  `OnBehaviorInitialize`: it does not fire for a TAOM behaviour, `MissionBehaviorLifecycleTests`).
  `OnCreated`: increment a `private static int` mission counter, stamp `_missionStart`, reset the
  `FrameStats` clock and the first-tick flag, read `TickProfilerTopN` and `HitchThresholdMs`, and
  store `_generation = MissionTickProfilerHooks.BeginMission(_missionStart, MissionTickProfilerHooks.Installed && settings.TickProfilerEnabled, hitchMs)`
  (the profiler stamps the main-thread id from this call). `OnMissionTick`: on the first tick write
  `TickProfileLines.BuildPerfContext(PerfContextReader.Read(...))` with `LogInfo`; when the toggle is
  on but `Installed` is false, also `LogWarning(TickProfileLines.NotInstalledLine)` once per mission;
  then, only when measuring, compute `nowSeconds` exactly like the heartbeat and, when
  `_clock.ShouldEmit(nowSeconds)`, call `_clock.Emit(nowSeconds)` and log
  `TickProfileLines.BuildTickProfile(nowSeconds, profiler.TakeWindow(_topN), AllocationCounter.Available)`.
  Wrap the body like the heartbeat: on an exception set `_disabled`, call
  `MissionTickProfilerHooks.EndMission(_generation)` and log
  `TickProfileLines.BuildFault("mission behaviour", ex)` with `LogError`. `OnEndMission`:
  `MissionTickProfilerHooks.EndMission(_generation)`. This class has no unit test (it needs a live
  `Mission`); it is named in the commit's `Not-tested:` trailer, and its pure parts (lines, window,
  generation) are tested in Steps 4 to 6.
- `Main/SubModule.cs`, edit 1: immediately after line 1923
  (`IoC.Resolve<Features.BattleLoadDiagnostics.MissionTickStallWatchdog>().Start();`), insert a blank
  line and:
  ```csharp
  // Patch97 tick profiler (default off): times every mission behaviour's tick and the engine
  // phases managed code sees, for [TickProfile] and [Hitch]. Installed here, once per process and
  // only when its MCM toggle is on: game init precedes every mission, so nothing that calls
  // Mission.OnPreTick has run yet. The installer contains its own failures.
  Features.MissionPerf.Hooks.MissionTickProfilerInstaller.InstallIfEnabled(
      IoC.Resolve<Features.BattleLoadDiagnostics.IBattleLoadDiagnosticsSettingsProvider>(),
      IoC.Resolve<IModLogger>(),
      TryPatchCategory);
  ```
- `Main/SubModule.cs`, edit 2: immediately after line 2112 (the `MissionPerfHeartbeatBehavior`
  `AddTaomBehavior` line), insert:
  ```csharp
  // [PerfContext] once per mission, and, when the Patch97 profiler is installed and on, a
  // [TickProfile] window on the same 5 s wall clock as [MissionPerf].
  AddTaomBehavior(new Features.MissionPerf.Hooks.MissionTickProfilerBehavior(
      IoC.Resolve<Features.BattleLoadDiagnostics.IBattleLoadDiagnosticsSettingsProvider>(),
      IoC.Resolve<IGraphicsOptionsAdapter>(),
      IoC.Resolve<IModLogger>()));
  ```
  (`using TAOM.Adapters;` is already at `SubModule.cs:22`.) No other `SubModule.cs` change.

**Verify**: the build command exits 0; `--filter "FullyQualifiedName~MissionTickProfilerWiringTests|FullyQualifiedName~MissionBehaviorLifecycleTests|FullyQualifiedName~FeatureModulesTests"`
passes; `wc -l Main/Features/MissionPerf/Hooks/*.cs` shows every file under 150 lines.

### Step 10: docs

- `docs/features/mission-perf-heartbeat.md`: keep it as the one doc for `Main/Features/MissionPerf/`
  (reason: one folder, one MCM group, and `[TickProfile]` shares `[MissionPerf]`'s clock, so a reader
  comparing an A/B needs both on one page; the feature-map row is per folder). Fix line 38: "Read once
  per tick from the MCM instance" becomes "Read from the MCM instance at most once a second
  (`ToggleRefreshSeconds`)". Add a `## Tick profiler (Patch97)` section covering: what it measures
  (the three phases by behaviour type, the wait, the agent tick, `otherMs` and what lands in it); the
  three settings, their defaults and that the toggle needs a restart; the three line formats with the
  pinned samples, and that status lines carry `[TickProfiler]` (never the data tags, because plan
  029's parser reads those); that `wallMs` is the sum of the closed frames' `frameMs` and that a
  window counts only closed frames; that a frame is one mission tick (fast-forward runs several per rendered frame);
  that `agentTickMs` includes `AfterAsyncTickTick` and can overlap the next frame, so it is
  subtracted from `otherMs` only when it ran on the main thread; that allocation is main-thread only;
  what is not covered (view ticks `OnMissionScreenTick`, `OnFixedMissionTick`, agent-tick
  allocation); the cost when off and when on; the PatchShield exclusions. Extend Key Files and Tests
  with the new files.
- `docs/reference/harmony-patch-registry.md`: add `## Patch97_MissionTickProfiler` after the
  `Patch96_TournamentRewards` section and before the `<!-- backlinks-start` marker: targets with
  signatures, the four call-site swaps and the prefix, why the install point is the once-per-process
  game init block (toggle read there; nothing that calls `OnPreTick` has run, so no inlined copy),
  why `WaitTickCompletion` is timed at its call site, the soft-fail rule, thread notes, the PatchShield
  entries, MCM toggle default off, status ACTIVE (off by default), and a pointer to
  `docs/features/mission-perf-heartbeat.md`. In the `Patch91_MissionTickStall` section, add one
  sentence: the agent-tick prefix and finalizer also call the Patch97 profiler's
  `OnAgentTickEnter`/`OnAgentTickExit`, a static null check when the profiler is not installed.
- `docs/reference/feature-map.md:72`, the MissionPerf row: add to its description "and, off by
  default, the Patch97 tick profiler (`[TickProfile]` per-behaviour ms and allocation every 5 s,
  `[Hitch]` per slow frame, `[PerfContext]` once per mission)".
- `docs/features/mcm.md`, two edits. Lines 94-96: replace "One is allowlisted by `Class.Property`
  with a reason: `TaomSettings.EnableNativeSkinFixes` (parked; its consumer is commented out, so no
  value of the flag is honest)." with "Two are allowlisted by `Class.Property`, each with a reason:
  `TaomSettings.EnableNativeSkinFixes` (parked; its consumer is commented out, so no value of the
  flag is honest) and `BattleLoadDiagnosticsSettings.EnableTickProfiler` (see below)." Then after the
  sentence ending "and no MCM setting can gate anything in `OnSubModuleLoad`." (line 102) add:
  "`BattleLoadDiagnosticsSettings.EnableTickProfiler` is the one such setting today: it is read once
  per process at the first game init, where Patch97 installs or is skipped, so it carries
  `RequireRestart = true` and an allowlist entry."
- Re-read every sentence you wrote against the code it describes.

**Verify**: `python tools/lint_docs.py --fail-on-drift` exits 0; each of these returns nothing:
`git grep -n "Read once per tick" -- docs`,
`git grep -n "One is allowlisted" -- docs`,
`git grep -n -e "(no Harmony category is" -e "holds settings whose consumer is parked" -- TAOM.Tests docs`.

### Step 11: full verification and commit

1. The RefAsm build, then the RefAsm unit step (Commands table): quote the unit totals line; no
   `TAOM.Tests/Features/MissionPerf` test may fail (`TickProfilerTranspilerTests` reflects
   `Mission`'s private `WaitTickCompletion`, which hosted CI resolves from BUTR's reference
   assemblies). Name every failing test; a failure outside this plan's classes that the RefAsm run
   also shows at `<start>` is not yours, but say so with the evidence. Run this first: the normal
   build in item 2 then rebuilds against the installed game.
2. `dotnet build Main/TAOM.csproj -p:DisableModuleCopy=true -p:ModuleId=`: exit 0.
3. `dotnet test TAOM.Tests -p:DisableModuleCopy=true -p:ModuleId=`: quote the totals line. Expected
   `Failed! - Failed: 1` with only `EveryLanguage_DeclaresARowForEveryEnglishKey` failing, Skipped 2,
   and 73 new test methods (Test plan), so Passed 12418 and Total 12421. If a new test is reported
   skipped instead, name it and explain why.
4. The binding gate command: all pass, none inconclusive; quote its totals.
5. `python tools/validate_moduledata.py`: 0 ERRORs.
6. `python tools/lint_docs.py --fail-on-drift`: exit 0.
7. `git status --porcelain`: only in-scope paths.
8. `git diff <start> -- Main/SubModule.cs` (`<start>` from Step 1, not `dffdf879`: plan 030 may
   have moved this file before you started): exactly the two insertions of Step 9.
9. Commit as in "Git workflow".

**Verify**: `git log -1 --format=%s` shows the subject; `git status --porcelain` is empty for
in-scope paths.

## Test plan

- New test classes, all under `TAOM.Tests/Features/MissionPerf/`: `AllocationCounterTests` (3),
  `BehaviourTickTableTests` (10), `MissionTickProfilerTests` (16), `TickProfileLinesTests` (13),
  `TickProfilerTranspilerTests` (8), `MissionTickProfilerHooksTests` (9, `RequiresGame`),
  `MissionTickProfilerBindingTests` (3, `BindingVerification`; two also `RequiresGameIL`),
  `MissionTickProfilerWiringTests` (2). Plus 9 provider tests in
  `BattleLoadDiagnosticsSettingsProviderTests` (one method each, no `[DataRow]`). Total: 73 new
  test methods, each one result in the totals line.
- Changed pins: `SettingsFingerprintTests` (9 to 12), `SettingRequireRestartPostureTests` (one
  allowlist entry with its reason).
- Patterns: `PartyIconScaleTranspilerTests` (synthetic IL), `TranspilerSiteBindingTests` (real IL),
  `CreatureBanditsWiringTests` (PatchShield walk), `FeatureModuleHooksTests` (`MissionLogic` probe),
  `AnimaliaWiringTests` (source pins), `FrameStatsTests` (pure window maths).
- Cannot be tested offline (the commit's `Not-tested:` trailer): Harmony actually applying Patch97
  in the game, the in-game numbers, `MissionTickProfilerBehavior` (it needs a live `Mission`; its
  line building, window and generation logic are tested in the pure classes), and the engine reads
  in `PerfContextReader` (graphics options, scene name, agent count, memory status). Everything
  else has a test that failed first.

## Done criteria

Machine-checkable. ALL must hold:

- [ ] `dotnet build Main/TAOM.csproj -p:DisableModuleCopy=true -p:ModuleId=` exits 0
- [ ] The full test command reports Failed 1 (`EveryLanguage_DeclaresARowForEveryEnglishKey` only),
      Skipped 2, Passed 12418 (12345 + the 73 new test methods), every new test class among the
      executed
- [ ] The RefAsm unit step shows no failing test in `TAOM.Tests/Features/MissionPerf`
- [ ] The binding gate passes with none inconclusive, `MissionTickProfilerBindingTests` included
- [ ] `git grep -n -E 'Log(Info|Warning|Error)\(\$?"' -- Main/Features/MissionPerf ':!Main/Features/MissionPerf/Hooks/MissionPerfHeartbeatBehavior.cs'`
      returns nothing (every new log text comes from `TickProfileLines`, whose test keeps status
      lines off the data tags)
- [ ] `git grep -n "Patch97_MissionTickProfiler" -- Main/SubModule.cs` returns nothing (the category
      is applied only through the installer), and
      `git grep -n "## Patch97_MissionTickProfiler" -- docs/reference/harmony-patch-registry.md`
      returns one line
- [ ] `git grep -n "Read once per tick" -- docs` returns nothing
- [ ] `git grep -n -e "Mission.OnTick\"" -e "Mission.OnPreTick\"" -e "Mission.TickAgentsAndTeamsImp\"" -- Dependencies/Foundation/PatchShieldPolicy.cs`
      lists the three new entries
- [ ] `wc -l Main/Features/MissionPerf/Hooks/*.cs` shows every file under 150 lines
- [ ] `git grep -n -P "[\x{2013}\x{2014}]" -- Main/Features/MissionPerf docs/features/mission-perf-heartbeat.md`
      returns nothing (no em or en dash in new prose)
- [ ] `python tools/lint_docs.py --fail-on-drift` exits 0
- [ ] `git status --porcelain` lists only in-scope files; one commit on your branch
- [ ] Every comment, doc line and test oracle this plan supplied was re-checked against the code it
      describes (the literals in Design included)

## STOP conditions

Stop and report (do not improvise) if:

- The IL of the installed `Mission.OnTick` or `Mission.OnPreTick` does not hold exactly the call
  sites in Current state (the two binding tests fail, or a swap count is not 1), or a signature in
  Current state differs. Report the mismatch; do not adjust the counts.
- The transpiler would need anything beyond swapping a `call`/`callvirt` operand for a static
  `call` of the same stack shape (an inserted instruction, a new branch, a removed instruction).
- The wiring needs a registration change beyond the two `SubModule.cs` edits of Step 9 (an
  `IoC.cs` line, a `FeatureModules` entry, a change to `ModuleRunner` or `PatchCategoryDecl`).
- The code shows `BattleLoadDiagnosticsSettings.Instance` is not readable in
  `OnGameInitializationFinished` (for example a comment or code path stating MCM builds its settings
  later), so no apply point before the first mission sees the persisted toggle.
- `AllocationCounterTests.Bind_InstalledFramework_FindsTheCounter` fails on the test host (the CLR
  fact this design relies on is false there).
- `Patch97_` is already taken when you start (`git grep -n "Patch97_"`): report it; do not renumber
  on your own.
- After Step 2 the reflected settings total is not 340, or `BattleLoadDiagnosticsSettings` does not
  reflect 12, or the simulation-relevant count moves from 217.
- A test outside this plan's new classes fails that did not fail in Step 1.
- A step's verification fails twice after a reasonable fix.

## Orchestrator steps (not the executor's)

- Issue: file it before dispatch (title along the lines of "Mission tick profiler: per-behaviour
  frame time, allocation and hitch log"), and name it in the commit body if it exists by then.
- No `/localize`: the Battle Load Diagnostics MCM page is not localised (its existing strings carry
  no `{=KEY}`), and the log lines are not player-facing text.
- Confirm that plan 029's `PINNED_TICK_PROFILE`, `PINNED_HITCH` and `PINNED_PERF_CONTEXT` equal the
  three literals in `TickProfileLinesTests` character for character (029's Step 2 makes the same
  check when 028 lands first), and that 029's segmenter needs no change for the `[TickProfiler]`
  status lines (it matches the substring `[TickProfile]`, which they do not contain).
- After review, `/verify-bindings` to refresh `docs/reference/taleworlds-api-snapshot/patch-targets.md`
  (two new patch classes).
- The feature doc and its feature-map row are done by the executor in Step 10; check them.

## After merge: the maintainer's actions

- Pull and build. To use the profiler: MCM, Battle Load Diagnostics, Mission Performance, "Enable Tick
  Profiler" on (MCM asks to restart), then a battle. In-game check (FOR-MIKE): one Custom Battle and
  one campaign field battle with the profiler on: at game start the line
  `[TickProfiler] install: category applied, Mission.OnTick sites 2/2, Mission.OnPreTick sites 2/2, allocation counter available`;
  one `[PerfContext]` per mission; a `[TickProfile]` line every 5 s whose `t` matches the
  `[MissionPerf]` line beside it (give or take one frame, so occasionally 1 s apart);
  `[Hitch]` lines on slow frames; then the same battle with the profiler off: one
  `[TickProfiler] off:` line at game start, `[PerfContext]` says `tickProfiler=off`, no
  `[TickProfile]` lines, and `[MissionPerf]` `avgMs` on versus off within
  run-to-run noise (the profiler's own overhead). Close the issue with `triage-needs-ingame` until
  that check is done.

## Maintenance notes

- The `[TickProfile]`, `[Hitch]` and `[PerfContext]` formats are a contract with plan 029's parser;
  change them only together with its tests. Status lines stay on `[TickProfiler]`: a status line
  that contains a data tag counts as a malformed line in every log 029 reads.
- Plan 030 deletes `Patch35_Mission_OnTick` (a postfix on the same method); Harmony composes the two,
  so the order of merging does not matter. Plan 034 changes PatchShield; if it rewrites
  `ExcludedTargetMethods`, keep `Mission.TickAgentsAndTeamsImp` on it and `Mission.OnTick` and `Mission.OnPreTick` off
  it (decision D13, 2026-10-03: those two are not excluded), and the binding tests that walk both directions
  (`AgentTickTarget_IsOnPatchShieldsExclusionList`, `TickAndPreTickTargets_AreNotOnPatchShieldsExclusionList`).
- Plans that add MCM settings also move `SettingsFingerprintTests` and the two co-op docs' counts;
  the second to merge recomputes.
- An engine bump that moves the call sites fails `MissionTickProfilerBindingTests` (the binding gate)
  instead of silently profiling nothing; at run time the transpiler leaves vanilla IL and the
  `[TickProfiler] install:` line reports `sites 0/2` for the method it skipped.
- Review probes: the `try`/`finally` in the helpers (a behaviour's exception must propagate
  unchanged); the agent-tick `Interlocked` fields and the main-thread-id comparison; `otherMs`
  clamping; that nothing in `Timed` allocates after warm-up; the `_attempted` flag set before the
  apply; that `OnFrameBoundary` stops measuring on its own fault; the generation check in
  `EndMission`; that behaviour totals fold into the window only at `CloseFrame`; that the profiler
  behaviour never overrides `OnBehaviorInitialize`.
- Deferred, with reasons: timing view ticks (`OnMissionScreenTick` runs through a closure, not a
  simple loop) and `OnFixedMissionTick`; per-behaviour timing of agent components (on the async and
  worker threads, needs a different design); a rate limit on `[Hitch]` (the threshold is the
  control; a 3 fps battle writes about 4 lines a second, which is harmless); adding Patch91's
  `MissionState.TickMissionAux` to `ExcludedTargetMethods` (per frame, but outside this profiler's
  measured path; a candidate for plan 034). UNVERIFIED: whether native's `Mission.IdleTick` raises
  `OnPreTick`; if it does not, returning from an overlay state could show one long frame made of
  `otherMs` only.

## Amendment (orchestrator, 2026-10-03; binding)

The maintainer's instruction of 2026-10-02 (plans/_audit/2026-10-02-perf/DECISIONS.md D6): taom_debug.log
is a critical, comprehensive log. This plan's line contract stays exactly as written and reviewed; one
line is added:

- **A mission-end summary**, written once from `OnEndMission` when the mission measured anything:
  `[TickSummary] frames=<n> wallMs=<x> preDisplayMs=<x> missionTickMs=<x> preTickMs=<x> waitTickMs=<x> agentTickMs=<x> otherMs=<x> allocKB=<x|na> hitches=<n> worstHitchMs=<x> worstHitchT=+<s>s top=<Type>:<ms>/<calls>/<maxMs>/<KB>,...`
  covering every closed frame of the mission (the sums of the windows; `top=` the top N behaviours over
  the whole mission, N from MCM). Same number rules as the contract above. Its tag must not contain
  `[TickProfile]`, `[Hitch]`, `[PerfContext]` or `[MissionPerf]` (plan 029 parses unknown tags
  generically, so `[TickSummary]` needs no parser change). Build it in `TickProfileLines` and pin it with
  one more literal test (inputs of your choosing, written into the test and the feature doc).
  When the profiler is off, no `[TickSummary]` is written (the `[TickProfiler] off:` line already says
  why).

Out of scope here, so do not build them: spawn-work timing, script-component timing, the clip-loading
flag, and an on-by-default hitch probe. They are plan 041, built on this plan's branch tip after its
review.
