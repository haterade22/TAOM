# Plan 041: Turn the hitch probe on by default and attribute spawn, script and clip-loading time

> **Executor instructions**: Follow this plan step by step. Run every verification command and
> confirm the expected result before moving on. If anything in "STOP conditions" occurs, stop and
> report; do not improvise. Work in the worktree and on the branch you were given. The orchestrator
> keeps `plans/README.md`; do not edit it.
>
> **This plan builds on plan 028's code.** Your branch starts at the tip of plan 028's reviewed
> branch, so most `Main/Features/MissionPerf/` files this plan edits were written by plan 028 and do
> not exist at the planned-at commit. Step 1 re-anchors: it checks that every plan 028 member this plan
> relies on exists with the shape quoted under "Plan 028's code (what this plan builds on)". A
> missing member, or a signature this plan's edits cannot map one to one, is a STOP condition.
>
> **Drift check (run first)**: `git log --oneline 0d1e91f0..HEAD` must list plan 028's commits: its
> feature commit (subject containing `per-behaviour tick profiler and hitch log`) and any review-fix
> commits on its branch, whose subjects may differ. Then
> `git diff --stat 0d1e91f0..HEAD -- Main/SubModule.cs Main/Adapters/IAnimationLoadingAdapter.cs Main/Adapters/AnimationLoadingAdapter.cs Main/Features/BattleLoadDiagnostics/BattleLoadDiagnosticsSettings.cs Main/Features/BattleLoadDiagnostics/IBattleLoadDiagnosticsSettingsProvider.cs Main/Features/BattleLoadDiagnostics/BattleLoadDiagnosticsSettingsProvider.cs Main/Features/BattleLoadDiagnostics/Hooks/Patch91_MissionTickStallProbes.cs Main/Features/CoopInterop/CoopSettingsRelevance.cs Main/Features/BannerColorPersistence/Hooks/Mission_SpawnAgent_Patch.cs Dependencies/Foundation/PatchShieldPolicy.cs Main/Features/MissionPerf TAOM.Tests/Features/MissionPerf TAOM.Tests/Features/BattleLoadDiagnostics/BattleLoadDiagnosticsSettingsProviderTests.cs TAOM.Tests/Features/CoopInterop/SettingsFingerprintTests.cs TAOM.Tests/Features/Mcm/SettingRequireRestartPostureTests.cs docs/features/mission-perf-heartbeat.md docs/reference/harmony-patch-registry.md docs/reference/feature-map.md docs/features/coop-interop.md docs/features/bannerlord-together-compat.md docs/features/mcm.md`.
> Every change it lists must come from plan 028's commits (expected) or from another plan the
> orchestrator names in your dispatch. For any other change, compare the "Current state" excerpts
> with the live code; a mismatch is a STOP condition. Plan 036 (anim memory probe) may land first and
> create `Main/Adapters/IAnimationLoadingAdapter.cs` and `AnimationLoadingAdapter.cs`: that is
> expected, and Step 7 says what to do.

## Orchestrator re-anchor on plan 028's reviewed tip (2026-10-03)

Binding: where this section differs from the text below, this section wins. Plan 028's review
(`docs/reviews/deep-review-028-mission-tick-profiler-2026-10-02.md`; commits `e64529b3`, `7ca09cc2`
and the docs-only `9d52bb86`) changed these members of the code this plan builds on. The orchestrator
read each one at the tip before dispatch.

| This plan's text | At plan 028's reviewed tip | What changes here |
|---|---|---|
| `BehaviourTickTable`, `MissionTickProfiler.Behaviours`, `BehaviourTotal` | `BehaviorTickTable` (`Main/Features/MissionPerf/BehaviorTickTable.cs`), `MissionTickProfiler.Behaviors`, `BehaviorTotal` | Use the American spelling wherever this plan names them, Step 1's graphify queries included |
| `BehaviourTickTable.TakeTop(int n, long ticksPerSecond)` | `WindowTop(int n, long ticksPerSecond)`; also new `MissionTop(int n, long ticksPerSecond)` and `ResetMission()` | Read `TakeTop` as `WindowTop` |
| `MissionTickProfiler.EndMission(int generation)` returns nothing | returns `bool` | Callers may ignore the result |
| `CloseFrame` builds a `HitchFrame` for every frame at or over the threshold | builds one only for a mission's first `MissionTickProfiler.MaxHitchLinesPerMission` (100) slow frames; later ones are counted for `[TickSummary]` (`hitches=`, the worst kept), and `HitchCapReachedThisFrame` is true on the 101st, when `OnFrameBoundary` writes the one cap line (`TickProfileLines.BuildHitchCapLine`) | Step 3: build the `HitchDetailFrame` only where `CloseFrame` builds the `HitchFrame` (inside its `_hitches <= MaxHitchLinesPerMission` branch), so `[HitchDetail]` follows `[Hitch]`'s cap. Fold the new frame fields into the window and mission totals for every frame, as Step 3 says |
| `MissionTickProfilerInstaller` has `private static bool _attempted`, set first in `InstallIfEnabled` | no such field: the once-per-process guard is `Main/SubModule.cs`'s guarded game-init block, which calls `InstallIfEnabled` once (`MissionTickProfilerWiringTests` pins the placement) | Step 1 item 2: drop `-e "_attempted"` from the installer grep, and check instead that `git grep -n "MissionTickProfilerInstaller.InstallIfEnabled" -- Main/SubModule.cs` prints exactly one line. Step 10: add no `MissionTickProfilerInstaller.ResetForTests()` (it would have nothing to reset) and leave it out of the `[TestCleanup]` order; drop the case `Install_SecondCall_DoesNothing` (five cases, not six), since `HitchProbeInstaller.Install` is reached only through `InstallIfEnabled`; "keep `_attempted` first" does not apply. STOP conditions: read "or the installer has no `_attempted` guard" as "or `Main/SubModule.cs` calls `InstallIfEnabled` outside its once-per-process game-init block" |
| `MissionTickProfilerHooks` | still no reset member; new `WriteSummary(int generation, int topN)`, through which the behaviour's `OnEndMission` writes `[TickSummary]` when the mission measured (its `_measuring` flag) | Step 12's summary condition is already the mission's measuring flag |
| `TickProfileLines` | also has `BuildTickSummary`, `BuildContextReadFault`, `MemoryReadFailedLine`, `SettingFallbackLines`, `RestartNeededLine`, `BuildMissionStartLine`, `BuildMissionOffLine`, `BuildRewriteFault`, `BuildHitchCapLine`, `BuildNoFramesLine`, `BuildStaleEndLine` | Nothing: this plan's lines keep their own tags |

**Base totals.** At the tip the full suite reads `Failed! - Failed: 1, Passed: 12453, Skipped: 2,
Total: 12456` (the orchestrator ran it at `7ca09cc2`; `9d52bb86` changes docs only), not "about
`Passed: 12419`". Record your own run as `<base>`.

**ADR-002.** `Main/Features/MissionPerf/Hooks/MissionTickProfilerHooks.cs` is 149 lines at the tip, and
this plan adds to it. Keep every `Main/` file you touch at 150 lines or fewer: if a step would take this
file over, first extract a helper that changes no behaviour, in a commit of its own (for example, move
the body of `OnFrameBoundary` into a new `internal static class` in the same folder and leave
`OnFrameBoundary` as its one-line caller, so every reference and plan 028's tests stay as they are),
then add this plan's code. Say so in your report.

**PatchShield.** This plan's three new `ExcludedTargetMethods` entries follow
`.claude/rules/harmony-patches.md` as written. Whether a target that runs once per frame on the main
thread (as opposed to per agent) belongs on that list is open with the maintainer (the 028 record's
"Convergence round 2"): implement the entries as planned, and name in your report which of your
targets run per frame on the main thread and which per agent.

**Plan 036** is on its own branch and not in your base: you create the two animation adapters
yourself (Step 7's "Otherwise" case); the orchestrator resolves the add/add overlap at merge.

## Status

- **Priority**: P1
- **Effort**: L
- **Risk**: MED (on by default for every player: whole-method prefix and finalizer pairs on five
  engine methods, two of them per frame on the main thread and one per spawned agent; no transpiler is
  installed unless the maintainer turns the tick profiler on; every hook contains its own faults)
- **Depends on**: `plans/028-mission-tick-profiler.md`, built on the tip of its branch after its
  review (the orchestrator re-anchors this plan before dispatch)
- **Category**: perf
- **Planned at**: commit `0d1e91f0`, 2026-10-02 (engine facts re-read from the installed v1.5.3
  client that day; `.claude/pinned-game-version.txt` reads `v1.5.3`)
- **Baseline at that commit**: dotnet `Failed! - Failed: 1, Passed: 12345, Skipped: 2, Total: 12348`
  (net472), measured at `dffdf879` and unchanged at `0d1e91f0` (docs and plans only since). Failing:
  `EveryLanguage_DeclaresARowForEveryEnglishKey` (English keys without rows in the other languages;
  the paid translator run waits on the maintainer). Skipped: `WargAttack_FastWarg_InvokesRunningAttack`,
  `WargAttack_SlowWarg_InvokesStandingAttack`. At plan 028's tip expect about 74 more passing tests
  (its 73 plus its `[TickSummary]` pin); Step 1 records the real numbers. Python suite: not needed
  (this plan touches no `tools/` file).
- **Issue**: filed by the orchestrator before execution

## Why this matters

Battles show single frames of 0.6 to 1.1 s every 30 to 70 s, and the worst spikes (up to 1,663 ms at
1,313 agents) sit in the spawn wave. Plan 028 builds a per-behaviour profiler, but it is off by default
and it does not see three of the strongest suspects: the work done while spawning each agent (every
behaviour's `OnAgentBuild`), the scene script components (TAOM's howdah, mumakil and seat components,
vanilla siege machines), which tick outside `Mission.OnTick`, and on-demand animation clip loads, which
block a worker of the parallel agent tick. After this plan, every player's own `taom_debug.log` explains
each slow frame by default: a `[Hitch]` line with the frame's phases and a `[HitchDetail]` line with the
spawn, script and clip-loading share, plus per-window and per-mission totals, at a measured cost under
0.5% of a 10 ms frame. The per-type attribution (which behaviour or component) stays behind plan 028's
opt-in toggle, because it needs transpilers.

## Current state

### What plan 028 delivers (and what it leaves out)

Plan 028 (`plans/028-mission-tick-profiler.md`) adds, behind the MCM toggle `EnableTickProfiler`
(default off, `RequireRestart = true`, read once per process at the first game init): Harmony category
`Patch97_MissionTickProfiler`, whose transpilers swap the behaviour calls in `Mission.OnTick` and
`Mission.OnPreTick` (and the `WaitTickCompletion` call) for timed static helpers, and whose prefix on
`Mission.OnPreTick` is the frame boundary; per-window `[TickProfile]` lines, one `[Hitch]` line per
frame at or over `HitchThresholdMs`, one `[PerfContext]` line per mission, and (its 2026-10-03
amendment) one `[TickSummary]` line at mission end. Its amendment says, verbatim: "Out of scope here,
so do not build them: spawn-work timing, script-component timing, the clip-loading flag, and an
on-by-default hitch probe. They are plan 041".

### Plan 028's code (what this plan builds on)

These are the members plan 028's text specifies (its Steps 3 to 9 and its amendment). Step 1 checks
they exist. Quoted from the plan, since the code does not exist at `0d1e91f0`:

- `Main/Features/MissionPerf/MissionTickProfiler.cs`: `public enum TickPhase { PreDisplay, MissionTick, PreTick }`;
  `public sealed class MissionTickProfiler` with `(long ticksPerSecond)`, `BehaviourTickTable Behaviours`,
  `bool Measuring` (volatile), `int MainThreadId`, `long MissionStartTicks`, `int Generation`;
  `public int BeginMission(long nowTicks, int mainThreadId, bool measuring, double hitchThresholdMs)`;
  `public void EndMission(int generation)`;
  `public void Record(TickPhase phase, int slot, long elapsedTicks, long allocBytes)`;
  `public void AddWait(long elapsedTicks)`; `public void AddAgentTick(long elapsedTicks, bool onMainThread)`
  (`Interlocked`, the only member callable off the main thread);
  `public HitchFrame? CloseFrame(long nowTicks, long allocBytesNow, int gc0, int gc1, int gc2)` (the
  first call of a mission only records the boundary and discards the partial frame; otherwise it
  computes `frameMs` and `otherMs = frameMs - preDisplay - missionTick - preTick - wait - (agent tick
  time that ran on the main thread)`, clamped at 0, builds a `HitchFrame` with `Behaviours.FrameTop(3, ...)`
  when `frameMs >= hitchThresholdMs`, then folds the frame into the window);
  `public TickWindow TakeWindow(int topN)`; classes `TickWindow`, `HitchFrame`.
- `Main/Features/MissionPerf/BehaviourTickTable.cs`: `SlotFor(Type)`, `Record(int slot, long elapsedTicks, long allocBytes)`
  (frame arrays only), `FoldFrame()`, `TakeTop(int n, long ticksPerSecond)`, `FrameTop(int n, long ticksPerSecond)`,
  `ResetWindow()`, `ResetFrame()`; `public sealed class BehaviourTotal` (`Name`, `Ms`, `Calls`, `MaxMs`,
  `AllocBytes`). Main thread only, not thread-safe.
- `Main/Features/MissionPerf/TickProfileLines.cs`: `StatusTag = "[TickProfiler]"`, `OffLine`,
  `WaitUnboundLine`, `NotInstalledLine`, `BuildInstallLine(...)`,
  `BuildSiteCountWarning(string method, string target, int count)` (text
  `[TickProfiler] <method>: <target> matched <count> times, expected 1; left vanilla, the profiler records nothing for this method`),
  `BuildHelperMismatchWarning(...)`, `BuildFault(string where, Exception ex)`, `BuildTickProfile`,
  `BuildHitch`, `BuildPerfContext`, `DiagTokens`, and the `[TickSummary]` builder from its amendment.
  `OffLine` is `[TickProfiler] off: 'Enable Tick Profiler' is off at game start (or MCM was not ready); no patches installed`.
- `Main/Features/MissionPerf/TickProfilerTranspiler.cs`: `internal sealed class CallSwap` (`MethodInfo Target`,
  `MethodInfo Helper`); `internal static List<CodeInstruction> Rewrite(IEnumerable<CodeInstruction> instructions, IReadOnlyList<CallSwap> swaps, string methodLabel, IModLogger? logger, out int swapped)`:
  validates each helper (static; parameters are the target's declaring type followed by the target's
  parameters; same return type), counts `call`/`callvirt` matches by declaring type, name and parameter
  count, requires exactly 1 per swap, else leaves the stream unmodified with one warning; swaps in place
  (`opcode = OpCodes.Call`, `operand = helper`), labels kept; never throws.
- `Main/Features/MissionPerf/Hooks/MissionTickProfilerHooks.cs`: statics `Profiler`, `Logger`,
  `WaitTickCompletionCall` (`Action<Mission>`), `Installed`, `OnTickSites`, `OnPreTickSites`; helpers
  `TimedPreDisplay`, `TimedMissionTick`, `TimedPreMissionTick` (each through a private
  `Timed(TickPhase, MissionBehavior, float)` that calls straight through when `Profiler` is null or not
  measuring), `TimedWaitTickCompletion(Mission)` (records with `AddWait`);
  `internal static void OnFrameBoundary()` (closes the frame, logs `[Hitch]` with `LogInfo` on a
  non-null result, ends measuring and logs `BuildFault("frame boundary", ex)` on its own exception);
  `OnAgentTickEnter()` / `OnAgentTickExit()` (called from Patch91's agent-tick prefix and finalizer);
  `BeginMission(long missionStartTicks, bool measuring, double hitchThresholdMs)` and
  `EndMission(int generation)`. No reset member: plan 028's `MissionTickProfilerHooksTests` clear the
  statics from the test side. This plan adds `ResetForTests()` (Step 9).
- `Main/Features/MissionPerf/Hooks/Patch97_MissionTickProfiler.cs`: `Mission_OnTick_TickProfiler_Patch`
  (transpiler) and `Mission_OnPreTick_TickProfiler_Patch` (`[HarmonyPrefix] public static void Prefix() => MissionTickProfilerHooks.OnFrameBoundary();`
  plus a transpiler), both `[HarmonyPatchCategory("Patch97_MissionTickProfiler")]`.
- `Main/Features/MissionPerf/Hooks/MissionTickProfilerInstaller.cs`: `Category`, a
  `private static bool _attempted` (`InstallIfEnabled` returns at once when it is set, and sets it before
  anything else; there is no reset of any kind, so this plan adds `ResetForTests()` in Step 10),
  `BindWaitDelegate()`, `OnTickSwaps()`, `OnPreTickSwaps()`,
  `InstallIfEnabled(IBattleLoadDiagnosticsSettingsProvider settings, IModLogger logger, Func<string, bool> tryPatchCategory)`:
  when `settings.TickProfilerEnabled` is false it logs `OffLine` and returns; otherwise it creates the
  profiler, binds the wait delegate, applies the category, sets
  `Installed = applied && OnTickSites == 2` and logs the install line.
- `Main/Features/MissionPerf/Hooks/MissionTickProfilerBehavior.cs` (`MissionLogic`): `OnCreated`
  stores `_generation = MissionTickProfilerHooks.BeginMission(_missionStart, MissionTickProfilerHooks.Installed && settings.TickProfilerEnabled, hitchMs)`;
  `OnMissionTick` writes `[PerfContext]` on the first tick (its `tickProfiler=on` when the mission is
  measuring), then a `[TickProfile]` window every 5 s while measuring; `OnEndMission` ends the mission
  and writes `[TickSummary]` "when the mission measured anything" (028's amendment; the same amendment
  also says "When the profiler is off, no `[TickSummary]` is written", so check which condition the
  code uses: Step 12 needs it to be the mission's measuring flag).
- Settings (plan 028 Step 2), group `"Mission Performance"` of `BattleLoadDiagnosticsSettings`:
  `EnableMissionPerfHeartbeat` (Order 0), `EnableTickProfiler` (Order 1, `RequireRestart = true`),
  `TickProfilerTopN` (Order 2, default 8), `HitchThresholdMs` (Order 3, default 250). Provider members
  `TickProfilerEnabled` (fail-closed `?? false`), `TickProfilerTopN`, `HitchThresholdMs`.
  `SettingsFingerprintTests` pins `BattleLoadDiagnosticsSettings` at `reflected: 12`;
  `docs/features/coop-interop.md` says `**340**`, `12 in BattleLoadDiagnosticsSettings`, `The 123 excluded (`;
  `docs/features/bannerlord-together-compat.md` says `TAOM's 340 MCM settings`.
- `Dependencies/Foundation/PatchShieldPolicy.cs` `ExcludedTargetMethods` gains
  `"TaleWorlds.MountAndBlade.Mission.OnTick"`, `"...Mission.OnPreTick"`, `"...Mission.TickAgentsAndTeamsImp"`.
- `Main/SubModule.cs` (single-owner): the installer call after
  `IoC.Resolve<Features.BattleLoadDiagnostics.MissionTickStallWatchdog>().Start();`, preceded by the
  comment `// Patch97 tick profiler (default off): ... Installed here, once per process and only when its MCM toggle is on: ...`,
  and the `MissionTickProfilerBehavior` `AddTaomBehavior` call after the heartbeat's, preceded by
  `// [PerfContext] once per mission, and, when the Patch97 profiler is installed and on, a // [TickProfile] window on the same 5 s wall clock as [MissionPerf].`

### Files read at `0d1e91f0` that this plan touches

- `Main/Features/BannerColorPersistence/Hooks/Mission_SpawnAgent_Patch.cs:9-10`: an existing TAOM patch
  on the same target this plan brackets:
  ```csharp
  [HarmonyPatch(typeof(Mission), nameof(Mission.SpawnAgent))]
  [HarmonyPatchCategory("Patch23_BannerColorPersistence")]
  ```
  with a `[HarmonyPrefix]` (line 54) and a `[HarmonyPostfix]` (line 68), default priority. Not edited.
- `Dependencies/Foundation/PatchShieldPolicy.cs:134-148`, `ExcludedTargetMethods` (before plan 028):
  `Formation.get_UnitDiameter`, the two `Formation` layout methods, three `Agent` weapon-state getters,
  `Mission.CanAgentRout`. `Mission.SpawnAgent`, `Mission.WaitTickCompletion` and
  `ManagedScriptHolder.TickComponents` are not on it. The namespace list excludes `ManagedCallbacks`
  (the native callback shims), not `TaleWorlds.Engine`.
- `Main/Features/CrashReport/Hooks/Native2ManagedTargets.cs:33-34`: CrashReport attaches a finalizer to
  the native shim `ManagedCallbacks.EngineCallbacksGenerated.ManagedScriptHolder_TickComponents`, which
  calls `ManagedScriptHolder.TickComponents`; `docs/reviews/rca-patchshield-skip-callback-shims-2026-09-24.md:33`
  records that ButterLib puts a `BlankTranspiler` on that shim. Both sit on the shim, a different method
  from this plan's target. Not edited.
- `Main/Core/Logging/FileLogger.cs:8-12`: INFO, WARNING and ERROR "drain to disk synchronously on the
  calling thread; DEBUG (the bulk of the volume) stays async"; the queue is a `ConcurrentQueue` and the
  file is written under a lock, so a call from any thread is safe.
- TAOM mission behaviours that override `OnAgentBuild`: 14 files (`git grep -l "override void OnAgentBuild" -- Main`),
  13 active plus the parked `Main/Features/ShaderPrecompilation/ShaderPrecompilePlayerAgentGuard.cs`.
- TAOM scene script components: `Main/Features/Elephant/TaomHowdahMachine.cs:28` (`: UsableMachine`),
  `TaomHowdahStandingPoint.cs:30` (`: StandingPoint`), `Main/Features/Mumakil/TaomMumakilPlatform.cs:29`
  (`: UsableMachine`), `TaomMumakilStandingPoint.cs:36` (`: StandingPoint`).
- Plan 036 (`plans/036-anim-memory-probe.md`, its Step 7) creates, on its own branch, the adapter this
  plan also needs. Its whole specification, verbatim: "`Main/Adapters/IAnimationLoadingAdapter.cs`
  (public): `bool IsAnyAnimationLoadingFromDisk();`. `Main/Adapters/AnimationLoadingAdapter.cs`
  (`public sealed`): returns `MBAnimation.IsAnyAnimationLoadingFromDisk()` (`using TaleWorlds.MountAndBlade;`)."
  It gives no XML summary. This plan creates the same two files from the same specification (Step 7),
  with no summary either. Two executors will not produce byte-identical files, so if both branches add
  them, the second merge hits an add/add conflict on these two paths; the orchestrator resolves it by
  keeping the first-merged branch's files (the members are the same).

### Engine facts (v1.5.3; `pwsh tools/taom-src.ps1 path <Type>` plus the IL of the installed DLLs)

Signatures from the decompiled sources:

- `TaleWorlds.Engine.ManagedScriptHolder` (`public sealed class ... : DotNetObject`), decompile lines 240-267:
  ```csharp
  [EngineCallback(null, false)]
  internal void TickComponents(float dt)
  {
      _toParallelTick.TickRec();
      TWParallel.For(0, _toParallelTick.ScriptComponents.Count, dt, TickComponentsParallelAuxMTPredicate, 1);
      _toParallelTick2.TickRec();
      TWParallel.For(0, _toParallelTick2.ScriptComponents.Count, dt, TickComponentsParallel2AuxMTPredicate, 8);
      _toParallelTick3.TickRec();
      TWParallel.For(0, _toParallelTick3.ScriptComponents.Count, dt, TickComponentsParallel3AuxMTPredicate, 8);
      _toTick.TickRec();
      foreach (ScriptComponentBehavior scriptComponent in _toTick.ScriptComponents)
      {
          scriptComponent.OnTick(dt);
      }
      ... // occasional bookkeeping
      if (_nextIndexToTickOccasionally < num2)
      {
          TWParallel.For(_nextIndexToTickOccasionally, num2, dt, TickComponentsOccasionallyParallelAuxMTPredicate, 8);
          ...
  ```
  It is reached from native through the shim above for every scene that has script components: the
  mission scene, but also the campaign map scene and any other scene ticking at the time. Which native
  thread calls it is not recorded anywhere in the repo (the thread table in `.claude/rules/harmony-patches.md`
  has no row for it), so this plan detects it at run time.
- `TaleWorlds.Engine.ScriptComponentBehavior.cs:215`: `protected internal virtual void OnTick(float dt)`.
  TAOM cannot call it directly (protected internal in another assembly); a cached open delegate
  `Action<ScriptComponentBehavior, float>` built from its `MethodInfo` performs the virtual call
  (Step 6 proves the dispatch with a test before relying on it).
- `TaleWorlds.Library.TWParallel.cs:13`: `public delegate void ParallelForWithDtAuxPredicate(int localStartIndex, int localEndIndex, float dt);`
  and lines 72-82:
  ```csharp
  public static void For(int fromInclusive, int toExclusive, float deltaTime, ParallelForWithDtAuxPredicate body, int grainSize = 16)
  {
      if (toExclusive - fromInclusive < grainSize) { body(fromInclusive, toExclusive, deltaTime); }
      else { _parallelDriver.For(fromInclusive, toExclusive, deltaTime, body, grainSize); }
  }
  ```
  The calling thread waits for the whole block, so a wall-clock bracket around the call is the block's
  cost to that thread.
- `TaleWorlds.MountAndBlade.Mission.cs:4174`:
  `public Agent SpawnAgent(AgentBuildData agentBuildData, bool spawnFromAgentVisuals = false, Equipment agentSpawnEquipment = null, ItemObject formationBannerItem = null)`
  (the only overload). The mount is built inline (lines 4378-4385), then the rider (4386-4398):
  ```csharp
  foreach (MissionBehavior missionBehavior2 in MissionBehaviors)
  {
      missionBehavior2.OnAgentBuild(agent2, null);
  }
  ...
  foreach (MissionBehavior missionBehavior3 in MissionBehaviors)
  {
      missionBehavior3.OnAgentBuild(agent, agentBuildData.AgentBanner ?? agentBuildData.AgentTeam?.Banner);
  }
  ```
  `TaleWorlds.MountAndBlade.MissionBehavior.cs:61`: `public virtual void OnAgentBuild(Agent agent, Banner banner)`.
  The thread table in `.claude/rules/harmony-patches.md` lists `Mission.SpawnAgent -> OnAgentBuild` under
  "Main thread"; this plan still checks the thread at run time, because a row there "is a claim until a
  log line proves it".
- `Mission.cs:3546` `internal void OnPreTick(float dt)` (body: `WaitTickCompletion();`, the reverse
  `OnPreMissionTick` loop, `TickDebugAgents();`), `:3601` `private void WaitTickCompletion()`
  (`while (!tickCompleted) Thread.Sleep(1);`), `:6742` `private void TickDebugAgents() { }` (empty, so
  `OnPreTick` minus the wait is the `OnPreMissionTick` loop), `:3617`
  `public void TickAgentsAndTeamsImp(float dt, bool tickPaused)`, `:3652`
  `public void OnTick(float dt, float realDt, bool updateCamera, bool doAsyncAITick)`. None is virtual.
- `TaleWorlds.MountAndBlade.MBAnimation.cs:143-146`:
  `public static bool IsAnyAnimationLoadingFromDisk() { return MBAPI.IMBAnimation.IsAnyAnimationLoadingFromDisk(); }`.
  Natively (`docs/reference/engine/mission-frame-threads-and-native-costs.md` section 6, TAOM-verified in
  Ghidra 2026-10-02): `IMBAnimation.IsAnyAnimationLoadingFromDisk` (`0x6EAAE0`) "walks the on-demand clip
  records and returns true while any is in state 1 (loading)"; a clip at Loading Type 1 or 2 "blocks
  that worker until the clip has loaded", so "a load shows up as a frame spike". Its per-call cost is
  unknown (it walks every on-demand record), so this plan measures it once at run time.

IL of the installed client (read on 2026-10-02 by walking `MethodBody.GetILAsByteArray()` from Windows
PowerShell and resolving each `call`/`callvirt` token; the Step 9 binding tests check the same facts
through `PatchProcessor.GetOriginalInstructions`):

| Method | IL bytes | Call sites this plan relies on |
|---|---|---|
| `ManagedScriptHolder.TickComponents(float32)` | 351 | `call TWParallel::For(int32,int32,float32,ParallelForWithDtAuxPredicate,int32)` exactly 4 times (IL_0024, IL_004d, IL_0076, IL_0134, in that order: three parallel blocks, then the occasional block); `callvirt ScriptComponentBehavior::OnTick(float32)` exactly once (IL_00a1) |
| `Mission.SpawnAgent(...)` | 2209 | `callvirt MissionBehavior::OnAgentBuild(Agent,Banner)` exactly twice (IL_078d mount, IL_080e rider) |
| `Mission.OnPreTick(float32)` | 55 | `call Mission::WaitTickCompletion()` at IL_0001; `callvirt MissionBehavior::OnPreMissionTick(float32)` at IL_0023 |
| `Mission.WaitTickCompletion()` | 17 | (a loop; whether the JIT inlines it into a caller is UNVERIFIED, see Design "The wait") |

Reflection flags: `ScriptComponentBehavior.OnTick` is `IsFamilyOrAssembly`, virtual;
`ManagedScriptHolder.TickComponents` is `IsAssembly`, not virtual; `Mission.SpawnAgent` has one overload;
`MBAnimation.IsAnyAnimationLoadingFromDisk` is static and returns `bool`.

### Conventions that bind this change

- **ADR-002** (`docs/adrs/002-thin-entry-points.md`): Harmony classes, hooks and mission behaviours are
  thin entry points under 150 lines; logic lives in pure classes. Plan 028 checks it with
  `wc -l Main/Features/MissionPerf/Hooks/*.cs`; so does this plan.
- **ADR-007** (`docs/adrs/007-adapter-pattern.md`): nothing that takes a TaleWorlds type goes into a pure
  class. `MBAnimation` sits behind `IAnimationLoadingAdapter`; the pure classes (`MissionTickProfiler`,
  `HitchProbeLines`, `AnimLoadingSampler`, `ProbeCostMeter`, `ProbeWindowWriter`) take numbers, strings,
  `System.Type` and interfaces only. Engine types appear only in `Hooks/` and `Main/Adapters/`.
- **ADR-008** (`docs/adrs/008-testability-requirements.md`): pure classes fully unit-tested; hooks 80%+ through
  `RequiresGame` tests where they need engine objects; Harmony classes covered by the binding gate.
- **ADR-003, ADR-004, ADR-005**: no `#region`, no `[Obsolete]`, no `#if DEBUG`.
- `.claude/rules/harmony-patches.md` (read it, and `docs/reviews/lessons/harmony-il.md`, before Step 9;
  mandatory): patches live in `Main/Features/<Feature>/Hooks/` with `[HarmonyPatch]`,
  `[HarmonyPatchCategory]` and an applied category (all three or the patch is dead); applied once per
  process; transpilers soft-fail (log and return the unmodified stream, never throw); per-frame or
  per-agent targets go on `PatchShieldPolicy.ExcludedTargetMethods` in the same change with a
  `BindingVerification` test walking the real targets (lesson "PatchShield wraps TAOM's own patch on an
  engine method: exclude a hot target in the same change"); a finalizer on a virtual never runs for an
  override (every target here is non-virtual); never use 0 as a sentinel where 0 is a valid value (use a
  `bool`); "Which thread runs your target" is mandatory before the first line of a patch.
- `.claude/rules/csharp-architecture.md`: MCM values go through a validating provider; no `Agent.Index`
  keys; no locks on the main thread.
- `.claude/rules/tests.md`: MSTest plus NSubstitute, `Method_Condition_Result` names; a test that runs
  engine code is `[TestCategory("RequiresGame")]`; a binding test that reads vanilla IL carries
  `[TestCategory("RequiresGameIL")]` on the method.
- **Logging (the maintainer's instruction of 2026-10-02, DECISIONS D6, binding).** `taom_debug.log` is
  TAOM's critical record. Every instrument logs a one-line configuration header when it starts (what is
  on, thresholds, the binding or signature it found, or why it found none); periodic lines carry every
  measured field; a mission-end summary gives totals, maxima and counts; anything that disables itself,
  skips work or falls back logs one line naming the reason and the consequence, once, never per frame;
  a cost reduction keeps every piece of information (aggregate or sample, never drop silently).
  `FileLogger` semantics: INFO, WARNING and ERROR flush synchronously on the calling thread. (The level
  rules that follow are this plan's, not D6's text.) Periodic lines (at most one per few seconds),
  summaries and configuration headers are INFO; a reason line uses the level pinned for it under Design
  "Status lines" (WARNING when a part disables itself or falls back, ERROR for a hook fault); nothing per
  agent or per frame is logged except plan 028's `[Hitch]` and this plan's `[HitchDetail]`, one each per
  slow frame, at INFO.
  The feature doc lists every new line with its fields and an example, and the tests pin each format
  literally.
- Exemplars: plan 028's own classes (above) for every pattern; `TAOM.Tests/Features/PartyIconScale/PartyIconScaleTranspilerTests.cs`
  (synthetic IL with stub helpers); `TAOM.Tests/Migration/TranspilerSiteBindingTests.cs` (real IL);
  `TAOM.Tests/Features/CreatureBandits/CreatureBanditsWiringTests.cs:47-54` (`TargetOf`) and `:469-487`
  (PatchShield walk); `TAOM.Tests/Composition/FeatureModuleHooksTests.cs` (`RequiresGame` with a
  `ProbeMissionBehavior : MissionLogic`); `TAOM.Tests/Infrastructure/PatchCategoryApplierTests.cs`
  (`new Harmony(...)` in a test); `TAOM.Tests/Features/AiPartySize/AiPartySizeServiceTests.cs:539`
  (`new TaomSettings()` reads a compiled MCM default in a test).

### Blast radius (`python tools/graphify_taom.py affected "<Type>" --depth 2`, graph refreshed at `5b7f5b1b`, which differs from `0d1e91f0` only in docs and `plans/_audit/`)

- `PatchShieldPolicy`: `PatchShield.Install` (`Dependencies/Foundation/PatchShield.cs:227`),
  `PatchShield.TryUnpatchOffendingPatches` (:366), `PatchShield.ShouldSwallow` (:305),
  `Dependencies/SubModule.cs` `OnSubModuleLoad` (:234) and `OnGameInitializationFinished` (:293), and
  `PatchShieldPolicyTests` (26 test methods). Adding entries only widens the exclusion.
- `BattleLoadDiagnosticsSettings`: no affected nodes found.
- `IBattleLoadDiagnosticsSettingsProvider`: `BattleLoadDiagnosticsService`, `BattleLoadStallWatchdog`,
  `ExitStallSampler`, `MemoryPressureSampler`, `MemoryStationSampler`, `MissionTickStallWatchdog`, their six
  test classes, and the implementer `BattleLoadDiagnosticsSettingsProvider`. All fake it with NSubstitute
  or use the real provider, so a new member breaks nothing.
- `BattleLoadDiagnosticsSettingsProvider`: the six `ValidateSampleIntervalSeconds_*` tests.
- `CoopSettingsRelevance`: `SettingsFingerprintTests.AssertSplit`, `NoExcludedName_IsDead`,
  `EverySettingsClass_HasItsSplitPinned`.
- Plan 028's types (`MissionTickProfiler`, `BehaviourTickTable`, `TickProfileLines`,
  `TickProfilerTranspiler`, `MissionTickProfilerHooks`, `MissionTickProfilerInstaller`,
  `MissionTickProfilerBehavior`) do not exist at `0d1e91f0`. Step 1 runs the command for each on your
  tree and you quote the output in your report.

## Design (decided; implement it as written)

**Modes.** Decided once per process at the first game init, by plan 028's installer call (no new
`SubModule.cs` call):

| `EnableHitchProbe` | `EnableTickProfiler` | Installed | Mode |
|---|---|---|---|
| off | off | nothing | `off` |
| on (default) | off (default) | `Patch98_HitchProbe` | `probe` |
| any | on | `Patch97_MissionTickProfiler` (plan 028's transpilers plus this plan's two), then `Patch98_HitchProbe` | `full` |

The probe category is the frame boundary for both modes: plan 028's `OnPreTick` prefix moves out of
Patch97 into Patch98 (Step 9), so the profiler needs Patch98 to measure anything. A mission measures
frames when Patch98 is installed and either toggle is on at that mission's start; it times behaviours,
spawn callbacks and script components by type ("behaviour timing") only in `full` mode. `[PerfContext]`
keeps its meaning: `tickProfiler=on` only when behaviour timing is on, so a default install is not
flagged as profiled by plan 029's parser.

**The always-on part: whole-method brackets only.** Category `Patch98_HitchProbe` holds five patch
classes, each a parameterless `[HarmonyPrefix]` with `[HarmonyPriority(Priority.First)]` and a
parameterless `[HarmonyFinalizer]` with `[HarmonyPriority(Priority.Last)]`, so the bracket encloses every
other patch on the method (Patch23's on `SpawnAgent`, Patch35's postfix on `OnTick`), and a finalizer
runs when the method throws:

| Target | Prefix | Finalizer | Feeds |
|---|---|---|---|
| `Mission.WaitTickCompletion()` (class first in the file) | `HitchProbeHooks.OnWaitEnter()` | `OnWaitExit()` | `waitTickMs` in `probe` mode |
| `Mission.OnPreTick(float)` | `MissionTickProfilerHooks.OnFrameBoundary()` then `HitchProbeHooks.OnPreTickEnter()` | `OnPreTickExit()` | frame boundary, clip-loading sample, `preTickAllMs` |
| `Mission.OnTick(float, float, bool, bool)` | `OnTickEnter()` | `OnTickExit()` | `onTickMs` |
| `ManagedScriptHolder.TickComponents(float)` | `OnScriptTickEnter()` | `OnScriptTickExit()` | `scriptTickMs`, script calls |
| `Mission.SpawnAgent(AgentBuildData, bool, Equipment, ItemObject)` | `OnSpawnEnter()` | `OnSpawnExit()` | `spawnMs`, `spawns` |

The agent tick keeps plan 028's Patch91 bracket. Every hook body is wrapped in `try`/`catch`: a hook
that throws turns its own part off for the process and logs one reason line; it never throws into the
engine.

**Phase columns per mode.** Plan 028's `[TickProfile]`, `[Hitch]` and `[TickSummary]` formats stay
byte-for-byte. In `full` mode their phase columns are plan 028's behaviour sums. In `probe` mode no
behaviour call is timed, so `MissionTickProfiler.CloseFrame` derives them from the brackets:
`preDisplayMs = 0`, `missionTickMs = max(0, onTick - agent tick time that ran on the main thread)` (all
of `Mission.OnTick`'s main-thread work: both behaviour loops, the camera, dynamic entities, spawned
items), `preTickMs = max(0, preTickAll - wait)` (the `OnPreMissionTick` loop, since `TickDebugAgents` is
empty), `waitTickMs` from the wait bracket, and `otherMs` by plan 028's formula, unchanged. `top=none`.
`[HitchDetail]` carries `mode=` and the raw brackets, so a reader always knows which definition a line
used. Spawn and script time are parts of these columns, not additions: a spawn made from a behaviour's
`OnMissionTick` is inside `missionTickMs`; `TickComponents` on the main thread is inside `otherMs`.

**The wait.** In `full` mode plan 028's call-site swap times `WaitTickCompletion` (when its delegate
bound); the probe's wait bracket then still fires (the swapped helper calls the patched method through
its delegate) but records nothing (`HitchProbeHooks.WaitSwapActive`). `WaitSwapActive` is true exactly
when the swap is live in the patched IL: Patch97 applied, its wait delegate bound and
`OnPreTickSites == 2`. It does not depend on plan 028's `Installed` (which also needs
`OnTickSites == 2`), because an engine bump can leave the `OnPreTick` swap live while the `OnTick` swap
fails; plan 028's `TimedWaitTickCompletion` stays ungated by behaviour timing, so while the swap is
live it is the one wait recorder in every measuring mission, and the wait is counted once. In `probe`
mode the bracket on `WaitTickCompletion` itself times it. The method is 17 bytes with a loop: if the
JIT inlined it into the patched `OnPreTick`, the bracket would never run. The probe checks this at run time: `OnWaitEnter` sets a
flag that `OnPreTickEnter` clears; if the flag is still clear in `OnPreTickExit` for all of the first 30
measured frames of the process, it logs one reason line, and `preTickMs` then includes the wait (which
the line says).

**Script components.** `TickComponents` is ticked by native, possibly off the main thread. The bracket
uses a `[ThreadStatic]` open flag and start stamp and adds its elapsed time with `Interlocked` into the
open frame, from any thread. On the first measured call of the process it logs which thread it runs on.
Per-component attribution (`full` mode) records into a `BehaviourTickTable` that only the main thread may
touch, so the attribution helper records only on the main thread; if the first measured call is off the
main thread, per-component attribution is off for the process (reason line) and the totals stay. Every
`TickComponents` call while measuring counts, the mission scene's and any other scene's; `calls=` on
`[ScriptProfile]` shows how many ran per window (more calls than frames means more than one scene).

**Spawns.** `SpawnAgent` brackets nest (an `OnAgentBuild` handler may spawn): a main-thread depth
counter times only the outermost call into `spawnMs`; every call counts in `spawns`. A call off the main
thread is counted in `offMainSpawns` and not timed, with one reason line. Spawns while measuring but
before the mission's first frame boundary (missions that spawn in `AfterStart`) land in the mission's
`preFrameSpawns` and `preFrameSpawnMs`, never silently dropped; everything else from that partial frame
is discarded as plan 028 does.

**Clip loading.** `AnimLoadingSampler` wraps `IAnimationLoadingAdapter`. At the first tick of the first
measured mission of the process it calls the adapter 32 times, takes the median per-call cost, and turns
sampling on when the median is at most 20 us (0.2% of a 10 ms frame), off otherwise; either way it logs
one line. While on, `OnPreTickEnter` samples once per frame, right after the frame boundary, and marks
the frame it opens. Reason: the main thread waits for the agent tick right after that point, so a clip
load that is blocking the agent tick shows up in the frame being opened (its `waitTickMs`), and that is
the frame whose `[Hitch]` and `[HitchDetail]` carry the flag. An adapter exception turns sampling off for
the process with one reason line.

**Attribution (full mode only; transpilers in `Patch97_MissionTickProfiler`).** Two new transpiler
classes, applied with plan 028's category:
- `Mission.SpawnAgent`: both `callvirt MissionBehavior::OnAgentBuild(Agent, Banner)` become
  `call MissionAttributionHooks.TimedAgentBuild(MissionBehavior, Agent, Banner)`, recorded per behaviour
  type into a second `BehaviourTickTable`, `SpawnBuilds`.
- `ManagedScriptHolder.TickComponents`: the one `callvirt ScriptComponentBehavior::OnTick(float32)`
  becomes `call MissionAttributionHooks.TimedScriptTick(ScriptComponentBehavior, float)` (it calls
  through the cached open delegate; omitted from the swap list when the delegate cannot be bound), and
  the four `call TWParallel::For(...)` become, by occurrence, `TimedParallelBlock` three times and
  `TimedOccasionalBlock` once (static helpers with exactly `For`'s parameters).
This needs plan 028's transpiler to swap a target that occurs N times with N helpers in order, and to
validate a static target's helper (its parameters equal the target's, with no declaring type first).
Every count must match exactly or the method is left vanilla with one warning, as in plan 028.

**Cost and its measurement.** Target: the always-on part costs under 0.5% of a 10 ms frame, 50 us. A
unit benchmark (Step 13) patches dummy methods with the real Patch98 prefix and finalizer methods and
measures a simulated frame (the four per-frame brackets, the agent-tick pair, two spawns, a frame
boundary) against the unpatched dummies. At run time, the probe install line reports `ProbeCostMeter`'s
measurement of the bookkeeping alone (no Harmony; about 2,000 simulated frames on a scratch profiler at
start-up), and the clip-sample line reports the native call's median. If the benchmark misses the
target, `EnableHitchProbe` defaults to false and every surface says why (Step 13).

**Line contract.** Number rules exactly as plan 028: `CultureInfo.InvariantCulture`; ms with two
decimals (`0.00`); `t` is `{0:0}` seconds since the profiler behaviour's `OnCreated`; `na` means not
measured; `none` means an empty list; Type is `Type.Name`. No new tag contains `[TickProfile]`,
`[Hitch]`, `[PerfContext]`, `[MissionPerf]` or `[TickSummary]` as a substring (`[HitchDetail]` does not
contain `[Hitch]`: `D` follows `h`; `[TickSummaryExtra]` does not contain `[TickSummary]`: `E` follows
`y`). Field order is exactly:

```
[SpawnProfile] t=+<s>s spawns=<n> spawnMs=<x> top=<Type>:<ms>/<calls>,...
[ScriptProfile] t=+<s>s calls=<n> scriptTickMs=<x> scriptParallelMs=<x|na> occasionalMs=<x|na> top=<Type>:<ms>/<calls>/<maxMs>,...
[AnimLoad] t=+<s>s loadingFrames=<n> frames=<n>
[HitchDetail] t=+<s>s spawnMs=<x> scriptTickMs=<x> scriptParallelMs=<x|na> animLoading=<0|1|na> mode=<probe|full> spawns=<n> occasionalMs=<x|na> onTickMs=<x> preTickAllMs=<x>
[TickSummaryExtra] spawnMs=<x> scriptTickMs=<x> animLoadingFrames=<n|na> hitchesWithAnimLoading=<n|na> mode=<probe|full> frames=<n> spawns=<n> preFrameSpawns=<n> preFrameSpawnMs=<x> offMainSpawns=<n> scriptParallelMs=<x|na> occasionalMs=<x|na> onTickMs=<x> preTickAllMs=<x> spawnTop=<Type>:<ms>/<calls>,...|none scriptTop=<Type>:<ms>/<calls>/<maxMs>,...|none
```

The brief's fields come first in each line; the fields after them were added by this plan because
D6 requires them: `calls` (more than one scene ticking), `mode` (which column definition applies),
`spawns`, `occasionalMs`, `onTickMs`, `preTickAllMs` (the raw brackets behind the probe-mode columns),
the pre-frame and off-main spawn counts (nothing dropped silently) and the mission-wide tops.

When each line is written:
- `[SpawnProfile]`, `[ScriptProfile]`, `[AnimLoad]`: in every 5 s window of a measuring mission, right
  after plan 028's `[TickProfile]`, in that order. `[SpawnProfile]` only when the window had spawns;
  `[AnimLoad]` only while the sampler is on. Their `top=` is `none` unless behaviour timing is on and
  that rewrite found all its sites; `scriptParallelMs` and `occasionalMs` are `na` on the same terms.
- `[HitchDetail]`: right after each `[Hitch]`, same `t`. `animLoading=na` while the sampler is off.
- `[TickSummaryExtra]`: at mission end, right after `[TickSummary]`, when the mission measured
  anything. `animLoadingFrames` and `hitchesWithAnimLoading` are `na` when the sampler was never on in
  the mission.

Pinned literals (the tests use these exact strings; inputs follow each):

```
[SpawnProfile] t=+10s spawns=412 spawnMs=286.40 top=AdvancedCombatBehavior:40.20/412,WargMissionBehavior:12.75/412
[ScriptProfile] t=+65s calls=300 scriptTickMs=310.50 scriptParallelMs=120.25 occasionalMs=8.00 top=TaomHowdahMachine:95.10/1200/2.40,SiegeTower:40.00/300/0.90
[ScriptProfile] t=+65s calls=300 scriptTickMs=310.50 scriptParallelMs=na occasionalMs=na top=none
[AnimLoad] t=+65s loadingFrames=3 frames=300
[HitchDetail] t=+72s spawnMs=0.00 scriptTickMs=4.10 scriptParallelMs=1.25 animLoading=1 mode=full spawns=0 occasionalMs=0.30 onTickMs=6.30 preTickAllMs=790.60
[HitchDetail] t=+72s spawnMs=12.00 scriptTickMs=4.10 scriptParallelMs=na animLoading=na mode=probe spawns=3 occasionalMs=na onTickMs=20.50 preTickAllMs=790.60
[TickSummaryExtra] spawnMs=1843.20 scriptTickMs=6020.75 animLoadingFrames=41 hitchesWithAnimLoading=3 mode=full frames=18000 spawns=1313 preFrameSpawns=2 preFrameSpawnMs=3.50 offMainSpawns=0 scriptParallelMs=2400.50 occasionalMs=160.00 onTickMs=41000.25 preTickAllMs=9800.00 spawnTop=AdvancedCombatBehavior:210.40/1313 scriptTop=TaomHowdahMachine:1900.20/72000/4.80
[TickSummaryExtra] spawnMs=1843.20 scriptTickMs=6020.75 animLoadingFrames=na hitchesWithAnimLoading=na mode=probe frames=18000 spawns=1313 preFrameSpawns=0 preFrameSpawnMs=0.00 offMainSpawns=0 scriptParallelMs=na occasionalMs=na onTickMs=41000.25 preTickAllMs=9800.00 spawnTop=none scriptTop=none
```

Inputs: SpawnProfile `t=10.4`, 412 spawns, 286.4 ms, top (`AdvancedCombatBehavior`, 40.2 ms, 412 calls),
(`WargMissionBehavior`, 12.75, 412). ScriptProfile `t=65.2`, 300 calls, 310.5, 120.25, 8, top
(`TaomHowdahMachine`, 95.1, 1200, max 2.4), (`SiegeTower`, 40, 300, 0.9); the second literal is the same
window with parallel and occasional not measured and no attribution. AnimLoad `t=65.2`, 3, 300.
HitchDetail full `t=72.4`, spawn 0, script 4.1, parallel 1.25, loading true, spawns 0, occasional 0.3,
onTick 6.3, preTickAll 790.6; probe `t=72.4`, spawn 12, script 4.1, sampler off, spawns 3, onTick 20.5,
preTickAll 790.6. TickSummaryExtra full: spawn 1843.2, script 6020.75, 41 loading frames, 3 hitches
with loading, 18000 frames, 1313 spawns, 2 pre-frame spawns taking 3.5 ms, 0 off-main, parallel 2400.5,
occasional 160, onTick 41000.25, preTickAll 9800, spawn top (`AdvancedCombatBehavior`, 210.4, 1313),
script top (`TaomHowdahMachine`, 1900.2, 72000, 4.8); the probe literal: sampler never on, no pre-frame
spawns, no attribution.

**Status lines** (all `[TickProfiler] ` plus text, built only in `HitchProbeLines`; none contains any
data tag above or plan 028's; `<...>` filled by the caller):

```
[TickProfiler] probe off: 'Enable Hitch Probe' and 'Enable Tick Profiler' are off at game start (or MCM was not ready); no probe patches installed, no hitch lines this session
[TickProfiler] probe install: category <applied|failed>, enabled by <hitch probe|tick profiler|both>, targets Mission.OnPreTick,Mission.WaitTickCompletion,Mission.OnTick,ManagedScriptHolder.TickComponents,Mission.SpawnAgent, bookkeeping <x> us per frame (<p>% of a 10 ms frame, target 0.50%)
[TickProfiler] attribution install: Mission.SpawnAgent sites <n>/2, ManagedScriptHolder.TickComponents sites <n>/<expected>, script tick delegate <bound|unbound>
[TickProfiler] ScriptComponentBehavior.OnTick could not be bound; per-component script attribution is off, the script totals stay
[TickProfiler] probe on in MCM but its patches are not installed; see the probe install line and [PatchApply]
[TickProfiler] mission <n>: mode <off|probe|full>, hitch threshold <x> ms, spawn attribution <on|off>, script attribution <on|off>, anim-loading sample <on|off>
[TickProfiler] probe: Mission.WaitTickCompletion's bracket did not run inside Mission.OnPreTick in the first 30 frames (inlined by the JIT, or skipped by another patch); waitTickMs reads 0 and preTickMs includes the wait
[TickProfiler] script tick runs on the main thread (managed thread <id>, main <id>)
[TickProfiler] script tick runs on another thread (managed thread <id>, main <id>); per-component script attribution is off for this process, the totals stay
[TickProfiler] Mission.SpawnAgent ran off the main thread (managed thread <id>); off-main spawns are counted in the mission summary but not timed
[TickProfiler] anim-loading sample: MBAnimation.IsAnyAnimationLoadingFromDisk median <x> us over <n> calls (budget 20.00 us); <sampling every frame|over budget, sampling is off for this process>
[TickProfiler] anim-loading sample failed, sampling is off for this process: <ExceptionType>: <message>
[TickProfiler] <part> hook failed, its timing is off for this process: <ExceptionType>: <message>
```

`<part>` is one of `pre-tick`, `wait`, `on-tick`, `script`, `spawn`, `agent build`, `script attribution`,
`parallel block`, `probe install`. `<expected>` is 5 when the script tick delegate is bound and 4
otherwise. Mode `off` appears in the mission line only when a toggle is on but nothing is installed.

Log level per status line (the tests assert these):

| Line (in the order above) | Level |
|---|---|
| `probe off:`, `probe install:`, `attribution install:`, `mission <n>:`, `anim-loading sample:` (both outcomes), `script tick runs on the main thread` | `LogInfo` |
| `ScriptComponentBehavior.OnTick could not be bound`, `probe on in MCM but its patches are not installed`, `probe: Mission.WaitTickCompletion's bracket did not run`, `script tick runs on another thread`, `Mission.SpawnAgent ran off the main thread`, `anim-loading sample failed` | `LogWarning` |
| `<part> hook failed` | `LogError` |

Plan 028's `OffLine` stays `LogInfo`. Pinned status literals:

```
[TickProfiler] probe install: category applied, enabled by hitch probe, targets Mission.OnPreTick,Mission.WaitTickCompletion,Mission.OnTick,ManagedScriptHolder.TickComponents,Mission.SpawnAgent, bookkeeping 1.25 us per frame (0.01% of a 10 ms frame, target 0.50%)
[TickProfiler] attribution install: Mission.SpawnAgent sites 2/2, ManagedScriptHolder.TickComponents sites 5/5, script tick delegate bound
[TickProfiler] mission 1: mode probe, hitch threshold 250 ms, spawn attribution off, script attribution off, anim-loading sample on
[TickProfiler] anim-loading sample: MBAnimation.IsAnyAnimationLoadingFromDisk median 3.20 us over 32 calls (budget 20.00 us); sampling every frame
```

(`bookkeeping` and the median use `0.00`; the percentage is `us / 10000 * 100` with `0.00`; the
threshold uses `{0:0}`.)

Plan 028's `OffLine` changes, since probe patches may now be installed with the profiler off:
`[TickProfiler] off: 'Enable Tick Profiler' is off at game start (or MCM was not ready); no per-behaviour transpilers installed`.
`BuildSiteCountWarning` gains an `int expected = 1` parameter, so its text says `expected <expected>`.

## Step 0: the maintainer's edit

None. This plan edits no protected file (`.claude/settings.json`, `.claude/settings.local.json`,
`Directory.Build.props`, `docs/adrs/*.md`). The executor's first check is Step 1.

## Commands you will need

| Purpose | Command | Expected on success |
|---|---|---|
| Build | `dotnet build Main/TAOM.csproj -p:DisableModuleCopy=true -p:ModuleId=` | exit 0, 0 errors |
| Tests | `dotnet test TAOM.Tests -p:DisableModuleCopy=true -p:ModuleId=` | Step 1's totals plus the new tests (Test plan); the failures a subset of {`EveryLanguage_DeclaresARowForEveryEnglishKey`} (it passes once the translation run lands) |
| One test class | `dotnet test TAOM.Tests -p:DisableModuleCopy=true -p:ModuleId= --filter "FullyQualifiedName~<ClassName>"` | the named tests run; a filter matching nothing proves nothing |
| Binding gate | `dotnet test TAOM.Tests/TAOM.Tests.csproj -p:DisableModuleCopy=true -p:ModuleId= --settings TAOM.Tests/binding-gate.runsettings --filter "TestCategory=BindingVerification"` | all pass, none inconclusive |
| Benchmark | `TAOM_RUN_BENCHMARKS=1 dotnet test TAOM.Tests -p:DisableModuleCopy=true -p:ModuleId= --filter "TestCategory=Benchmark"` | 1 passed; the measured us per frame printed in the test output |
| RefAsm build (hosted CI's first step, `.ai/verification.md`) | `env -u BANNERLORD_GAME_DIR -u BANNERLORD_OVERRIDE_DIR dotnet build TAOM.Tests -p:TaomGameRefs=RefAsm -p:DisableModuleCopy=true -p:ModuleId=` | exit 0. It restores BUTR's reference assemblies from NuGet; if the restore cannot download them (no network), report "RefAsm not run (environment)" with the error, which is not a failure of this plan |
| RefAsm unit step (run right after the RefAsm build) | `env -u BANNERLORD_GAME_DIR -u BANNERLORD_OVERRIDE_DIR dotnet test TAOM.Tests --no-build -p:TaomGameRefs=RefAsm -p:DisableModuleCopy=true -p:ModuleId= --filter "TestCategory!=RequiresGame&TestCategory!=LiveInstall&TestCategory!=BindingVerification"` | no failure in any `TAOM.Tests/Features/MissionPerf` class; quote the totals line and every failing name |
| Data | `python tools/validate_moduledata.py` | 0 ERRORs (no ModuleData change; run once at the end) |
| Docs | `python tools/lint_docs.py --fail-on-drift` | exit 0 |
| Size check | `wc -l Main/Features/MissionPerf/Hooks/*.cs` | every file under 150 lines |

Both MSBuild flags go on build AND test; prefix every dotnet command with the `TEMP="<tmp>" TMP="<tmp>"`
your dispatch rules give. Never `./build.ps1`: it deploys into the game install.

## Scope

**In scope** (the only files you create or modify):

New, `Main/Features/MissionPerf/`:
- `ProbeTotals.cs` (`HitchDetailFrame`, `ExtrasWindow`, `MissionExtras`), `HitchProbeLines.cs`,
  `AnimLoadingSampler.cs`, `ProbeCostMeter.cs`, `ProbeWindowWriter.cs`

New, `Main/Features/MissionPerf/Hooks/`:
- `HitchProbeHooks.cs`, `MissionAttributionHooks.cs`, `Patch98_HitchProbe.cs`,
  `Patch97_MissionAttribution.cs`, `HitchProbeInstaller.cs`
- Only when a file would reach 150 lines (ADR-002), these split files and no others:
  `HitchProbeHooks.Spawn.cs` and `HitchProbeHooks.Script.cs` (the spawn and script brackets of a
  `public static partial class HitchProbeHooks`; Step 9); `MissionAttributionInstaller.cs` (Step 11);
  `MissionTickProfilerHooks.Probe.cs` (plan 028's class made `partial`, holding this plan's additions
  to it; Steps 9 and 12)

New, `Main/Adapters/` (plan 036's specification; if they already exist in your tree, leave them as they are):
- `IAnimationLoadingAdapter.cs`, `AnimationLoadingAdapter.cs`

New, `TAOM.Tests/Features/MissionPerf/`:
- `HitchProbeLinesTests.cs`, `MissionTickProfilerProbeTests.cs`, `TickProfilerTranspilerOccurrenceTests.cs`,
  `OpenDelegateDispatchTests.cs`, `AnimLoadingSamplerTests.cs`, `ProbeCostMeterTests.cs`,
  `HitchProbeHooksTests.cs`, `MissionAttributionHooksTests.cs`, `HitchProbeBindingTests.cs`,
  `HitchProbeInstallerTests.cs`, `ProbeWindowWriterTests.cs`, `HitchProbeWiringTests.cs`,
  `HitchProbeOverheadBenchmarkTests.cs`, and `BehaviourTickTableMissionTests.cs` only if Step 3 adds the
  mission level

Modified, plan 028's files:
- `Main/Features/MissionPerf/MissionTickProfiler.cs`, `BehaviourTickTable.cs` (mission level, only if
  absent), `TickProfilerTranspiler.cs`, `TickProfileLines.cs` (`OffLine` text, `BuildSiteCountWarning`
  parameter, and nothing else)
- `Main/Features/MissionPerf/Hooks/MissionTickProfilerHooks.cs` (`ResetForTests`, the `BeginMission`
  parameter, the `[HitchDetail]` log, the `Timed` gate), `MissionTickProfilerInstaller.cs`
  (`ResetForTests`, the probe call), `Patch97_MissionTickProfiler.cs` (the prefix moves out),
  `MissionTickProfilerBehavior.cs`

Modified, others:
- `Main/Features/BattleLoadDiagnostics/BattleLoadDiagnosticsSettings.cs` (one property, one hint amended)
- `Main/Features/BattleLoadDiagnostics/IBattleLoadDiagnosticsSettingsProvider.cs`,
  `BattleLoadDiagnosticsSettingsProvider.cs` (one getter)
- `Main/Features/CoopInterop/CoopSettingsRelevance.cs` (one name on `Instrumentation`)
- `Dependencies/Foundation/PatchShieldPolicy.cs` (three entries on `ExcludedTargetMethods`)
- `Main/SubModule.cs`: single-owner, exactly the two comment replacements in Step 12 and nothing else
- `TAOM.Tests/Features/BattleLoadDiagnostics/BattleLoadDiagnosticsSettingsProviderTests.cs` (two methods)
- `TAOM.Tests/Features/CoopInterop/SettingsFingerprintTests.cs` (the `BattleLoadDiagnosticsSettings`
  `reflected:` count plus 1; nothing else)
- `TAOM.Tests/Features/Mcm/SettingRequireRestartPostureTests.cs` (one allowlist entry with its reason;
  the summary sentence naming `EnableTickProfiler` as the one gated setting amended; nothing else)
- `docs/features/mission-perf-heartbeat.md`, `docs/reference/harmony-patch-registry.md`,
  `docs/reference/feature-map.md` (the MissionPerf row), `docs/features/coop-interop.md`,
  `docs/features/bannerlord-together-compat.md`, `docs/features/mcm.md`

**Out of scope** (do NOT touch, even though they look related):
- `Main/IoC.cs` and `Main/Composition/*`: nothing here needs a registration (the installer constructs
  the stateless adapter at the Harmony boundary, as Step 10 says). If you find you need one, STOP and
  report the exact line.
- `Main/TAOM.csproj` (SDK globbing picks up new files), `Directory.Build.props`, `.claude/settings*.json`,
  `docs/adrs/*.md` (protected).
- `Main/Features/BattleLoadDiagnostics/Hooks/Patch91_MissionTickStallProbes.cs`: plan 028 already
  routes the agent tick into the profiler; do not change it.
- `Main/Features/BannerColorPersistence/Hooks/Mission_SpawnAgent_Patch.cs` (Patch23), the CrashReport
  shims, `MissionPerfHeartbeatBehavior.cs`, `FrameStats.cs`, `MissionPerfLine.cs`.
- Plan 028's pinned data formats (`[TickProfile]`, `[Hitch]`, `[PerfContext]`, `[TickSummary]`) and its
  three data literals in `TickProfileLinesTests`, and `DiagTokens` (adding a `diag=` token would flag
  every default run in plan 029's parser).
- `tools/perf_runs.py` and plan 029's files (the generic-tag amendment of plan 029 already collects new
  `key=value` tags).
- Timing `OnMissionScreenTick`, `OnFixedMissionTick` or `FixedTickComponents`; filtering
  `TickComponents` to the mission scene's holder.
- `CHANGELOG.md`, `plans/README.md`, `docs/reference/taleworlds-api-snapshot/patch-targets.md` (the
  orchestrator refreshes it through `/verify-bindings`).
- The gates themselves. Never turn a gate green by editing it: deleting or `[Ignore]`-ing a test,
  loosening an assertion, or adding an allowlist entry without the reason that allowlist requires. The
  gate edits this plan makes are the designed procedures: the `AssertSplit` count moves with the
  classification, and the restart allowlist entry carries its reason. Anything else: STOP and report.

## Git workflow

- Commit on the branch you were given; never push or open a PR.
- One commit at the end of Step 14. Subject
  `feat(mission-perf): <version> - hitch probe; spawn, script, clip timing`, where `<version>` is the
  `<Version value=...>` in `Main/_Module/SubModule.xml` when you commit (`v2.0.32` at `0d1e91f0`, giving
  69 characters; a hook refuses any other version). At most 72 characters.
- The body is the changelog entry, wrapped at 72, for a reader of the release note: that a hitch probe
  is now on by default and writes a hitch line and a hitch detail line for each slow frame (where the
  frame went: the wait for the agent tick, the mission tick, spawning, scene scripts, and whether an
  animation clip was loading from disk), plus per-window script, spawn and clip-loading lines and a
  mission summary; its measured cost (quote the benchmark's us per frame and the target); that the
  per-type attribution stays behind "Enable Tick Profiler"; that the probe is installed at game start, so
  turning it off needs a restart; the three new PatchShield exclusions and their trade-off (PatchShield
  no longer rescues a throwing patch on `Mission.SpawnAgent`, `Mission.WaitTickCompletion` or
  `ManagedScriptHolder.TickComponents` for any player, Patch23's banner colour patch on `SpawnAgent`
  included); and how the changed `[TickProfiler] off:` line still tells the reader the same thing (D6
  rule 4). Trailers:
  `Not-tested: live patch application, the in-game numbers, MissionTickProfilerBehavior, AnimationLoadingAdapter, MissionAttributionHooks.TimedScriptTick against a live component (maintainer's in-game check)`
  and `Save-compat: none (no saved state)`. No AI attribution trailer. Never edit `CHANGELOG.md`.
- Stage explicit paths only (every in-scope path you changed); write the message to a file and run
  `git commit -F "<file>"`. Never `--no-verify`.

## Steps

### Step 1: re-anchor on plan 028 and record the base

1. `git log --oneline 0d1e91f0..HEAD` lists plan 028's commits (its feature commit, subject containing
   `per-behaviour tick profiler and hitch log`, and any review fixes). Record
   `<start> = git rev-parse --short HEAD`.
2. Each of these prints at least one line (run each; quote the output):
   `git grep -n "public int BeginMission(long nowTicks, int mainThreadId, bool measuring, double hitchThresholdMs" -- Main/Features/MissionPerf/MissionTickProfiler.cs`;
   `git grep -n "public HitchFrame? CloseFrame(long nowTicks, long allocBytesNow, int gc0, int gc1, int gc2)" -- Main/Features/MissionPerf/MissionTickProfiler.cs`;
   `git grep -n -e "public TickWindow TakeWindow(int topN)" -e "public void AddAgentTick(long elapsedTicks, bool onMainThread)" -- Main/Features/MissionPerf/MissionTickProfiler.cs`;
   `git grep -n -e "class CallSwap" -e "internal static List<CodeInstruction> Rewrite(" -- Main/Features/MissionPerf/TickProfilerTranspiler.cs`;
   `git grep -n -e "OnFrameBoundary" -e "OnAgentTickEnter" -e "WaitTickCompletionCall" -e "OnPreTickSites" -- Main/Features/MissionPerf/Hooks/MissionTickProfilerHooks.cs`;
   `git grep -n -e "InstallIfEnabled" -e "BindWaitDelegate" -e "_attempted" -- Main/Features/MissionPerf/Hooks/MissionTickProfilerInstaller.cs`;
   `git grep -n "OnFrameBoundary" -- Main/Features/MissionPerf/Hooks/Patch97_MissionTickProfiler.cs`;
   `git grep -n -e "\[TickSummary\]" -e "no patches installed" -e "BuildSiteCountWarning" -- Main/Features/MissionPerf/TickProfileLines.cs`;
   `git grep -n -e "EnableTickProfiler" -e "HitchThresholdMs" -- Main/Features/BattleLoadDiagnostics/BattleLoadDiagnosticsSettings.cs`;
   `git grep -n "Mission.TickAgentsAndTeamsImp" -- Dependencies/Foundation/PatchShieldPolicy.cs`.
   A pattern with no hit is a STOP (report which). A member that exists with a different but
   equivalent shape (a renamed parameter, an extra optional parameter) is fine: note it and map this
   plan's edits onto it. Anything that changes what a member does is a STOP.
   Also run, for information only (no hit is the expected state):
   `git grep -n "ResetForTests" -- Main/Features/MissionPerf`. If plan 028's review added a reset to
   `MissionTickProfilerHooks` or `MissionTickProfilerInstaller`, Steps 9 and 10 extend that member
   instead of adding a second one; say so in your report.
3. `git grep -n -e "Patch98_" -e "Patch99_" -- Main Dependencies TAOM.Tests` prints nothing (this plan
   takes `Patch98_HitchProbe`). A hit is a STOP: report it, do not renumber.
4. Run `python tools/graphify_taom.py refresh --if-stale`, then
   `python tools/graphify_taom.py affected "<Type>" --depth 2` for `MissionTickProfiler`,
   `BehaviourTickTable`, `TickProfileLines`, `TickProfilerTranspiler`, `MissionTickProfilerHooks`,
   `MissionTickProfilerInstaller` and `MissionTickProfilerBehavior`; quote each output in your report.
5. Read `plans/028-mission-tick-profiler.md` "Design" and its amendment once, and the code of the files
   in step 2, before any edit.
6. Run the full suite once: `dotnet test TAOM.Tests -p:DisableModuleCopy=true -p:ModuleId=`, and the
   binding gate once.
7. Run `wc -l Main/Features/MissionPerf/Hooks/*.cs` and record each of plan 028's files' line counts.

**Verify**: every grep in item 2 has a hit; item 3 prints nothing; the full suite's failures are a
subset of {`EveryLanguage_DeclaresARowForEveryEnglishKey`} (zero failures is fine: the paid
translation run may have landed), and Skipped 2. Record its totals line as `<base>` (about
`Passed: 12419` if plan 028 landed as planned and the translation test still fails); every later count
is `<base>` plus this plan's new tests.

### Step 2: the setting, its provider and its classification (TDD)

RED first:
1. In `BattleLoadDiagnosticsSettingsProviderTests.cs` add `HitchProbeEnabled_NoMcmInstance_DefaultsFalse`
   (MCM is not loaded in tests, so `new BattleLoadDiagnosticsSettingsProvider().HitchProbeEnabled` is
   false: fail-closed, like `TickProfilerEnabled`, because it installs patches) and
   `EnableHitchProbe_CompiledDefault_IsTrue` (`Assert.IsTrue(new BattleLoadDiagnosticsSettings().EnableHitchProbe)`;
   the `new TaomSettings()` pattern of `AiPartySizeServiceTests.cs:539`).
2. In `SettingsFingerprintTests.cs`, the `AssertSplit(typeof(BattleLoadDiagnosticsSettings), reflected: N, covered: 0);`
   line: N becomes N + 1 (12 to 13 if plan 028 landed as planned).
3. In `SettingRequireRestartPostureTests.cs` add to `RestartAllowlist`:
   `[$"{nameof(BattleLoadDiagnosticsSettings)}.{nameof(BattleLoadDiagnosticsSettings.EnableHitchProbe)}"] = "Read once per process at the first game init, where Patch98 installs or is skipped; a change needs a restart"`.

Build the test project (the filtered test command builds it). **Verify RED**: the build fails with
missing-member diagnostics naming `HitchProbeEnabled` and `EnableHitchProbe`. Record the error list.

GREEN:
- `BattleLoadDiagnosticsSettings.cs`, group `"Mission Performance"`, after `HitchThresholdMs`:
  `[SettingPropertyBool("Enable Hitch Probe", Order = 4, RequireRestart = true, HintText = "...")] public bool EnableHitchProbe { get; set; } = true;`
  Hint (no em or en dash): "On by default. Times the parts of every battle frame TAOM can see (the wait
  for the agent tick, the mission tick, scene scripts, agent spawns) and checks whether an animation clip
  is loading from disk, so each frame slower than the hitch threshold writes a hitch line and a detail
  line saying where the time went. It costs a few microseconds per frame. Installed once at game start:
  a change takes effect after a restart."
- Amend `EnableTickProfiler`'s hint (plan 028's text) to: "Off by default. Adds per-type attribution to
  the hitch probe's lines: times every mission behaviour's tick, every behaviour's spawn callback and every
  scene script component by type, and lists the slowest. Installed once at game start: a change takes
  effect after a restart." Leave its other attribute arguments unchanged.
- Interface: `bool HitchProbeEnabled { get; }` (doc: fail-CLOSED to false when MCM is not ready, because
  it installs patches; the compiled default is true). Provider:
  `public bool HitchProbeEnabled => BattleLoadDiagnosticsSettings.Instance?.EnableHitchProbe ?? false;`.
- `CoopSettingsRelevance.cs`: append `"EnableHitchProbe",` to `Instrumentation` after plan 028's three
  names, with the comment `// The Patch98 hitch probe (2026-10-02): log lines, never a computation`.
- `docs/features/coop-interop.md`: the settings total (`**340**` if 028 landed as planned) plus 1; the
  `BattleLoadDiagnosticsSettings` count plus 1; the excluded count (`The 123 excluded (`) plus 1, and the
  parenthetical gains `, and the hitch probe toggle added 2026-10-02` before its closing `)`.
  `docs/features/bannerlord-together-compat.md`: `TAOM's <total> MCM settings` plus 1. The
  simulation-relevant count (217) does not move.
- `SettingRequireRestartPostureTests.cs` summary. Before editing, run
  `git grep -n -e "setting a Harmony category is gated on at apply time (read once per process for Patch97)" -e "Every TAOM setting but one" -- TAOM.Tests`:
  it must print two lines (plan 028 wraps "the one" and "setting a Harmony category" onto two `///`
  lines, so these one-line fragments are what a grep can see). If it prints fewer, plan 028's text
  differs: STOP. Then replace plan 028's sentence part (in the file it spans the end of one `///` line
  and the whole next one)
  `and <c>EnableTickProfiler</c>, the one setting a Harmony category is gated on at apply time (read once per process for Patch97).`
  with `and the two settings a Harmony category is gated on at apply time, <c>EnableTickProfiler</c> (Patch97) and <c>EnableHitchProbe</c> (Patch98), each read once per process.`
  and the start of that summary, `Every TAOM setting but one is read live`, becomes
  `Every TAOM setting but two is read live` (re-read the result as a sentence; keep the line wrap at
  about 100 characters).

**Verify**: `dotnet test TAOM.Tests -p:DisableModuleCopy=true -p:ModuleId= --filter "FullyQualifiedName~BattleLoadDiagnosticsSettingsProviderTests|FullyQualifiedName~SettingsFingerprintTests|FullyQualifiedName~SettingRequireRestartPostureTests"`
passes, with the two new provider tests among the executed names. The same two-fragment `git grep`
as before the edit now returns nothing.

### Step 3: the profiler's probe accumulators (TDD)

RED: `TAOM.Tests/Features/MissionPerf/MissionTickProfilerProbeTests.cs` (no category). Construct with
`ticksPerSecond: 1000` so a tick is a millisecond. Fourteen cases:
1. `CloseFrame_ProbeMode_DerivesPhasesFromTheWholeMethodBrackets`, this oracle:
   `BeginMission(0, Environment.CurrentManagedThreadId, measuring: true, hitchThresholdMs: 1000, behaviourTiming: false)`;
   `CloseFrame(100, 0, 0, 0, 0)` (first boundary); `AddPreTickAll(40)`, `AddWait(30)`, `AddOnTick(60)`,
   `AddAgentTick(10, onMainThread: true)`, `AddAgentTick(25, onMainThread: false)`; `CloseFrame(300, 0, 0, 0, 0)`;
   `TakeWindow(8)`: frames 1, wallMs 200, preDisplayMs 0, missionTickMs 50 (60 - 10), preTickMs 10 (40 - 30),
   waitTickMs 30, agentTickMs 35, otherMs 100 (200 - 50 - 10 - 30 - 10), top empty.
2. `CloseFrame_FullMode_KeepsBehaviourSums_AndReportsBracketsInTheDetail`: the same calls plus
   `Record(TickPhase.MissionTick, slot, 20, 0)` with `behaviourTiming: true` and threshold 150:
   window missionTickMs 20, preTickMs 0, otherMs 140; the returned hitch's `Detail` has
   `OnTickMs` 60 and `PreTickAllMs` 40, `Mode` "full".
3. `CloseFrame_ProbeMode_NeverRecordsBehaviourTop`.
4. `CloseFrame_Hitch_CarriesItsDetail` (spawn time 12 with 3 spawns, script 4, parallel 1, occasional 0.5,
   anim loading true: each appears in `Detail`).
5. `CloseFrame_BelowThreshold_ReturnsNullWithoutDetail`.
6. `MarkAnimLoading_AppliesToTheFrameItOpens` (first boundary; `MarkAnimLoading(true)`; close: the hitch
   detail and the window count it; the next frame, unmarked, does not).
7. `TakeExtrasWindow_CountsLoadingFramesAndFrames`.
8. `TakeExtrasWindow_OpenFrameValues_LandInTheNextWindow` (a spawn recorded after the last boundary is
   absent from the window taken now, present in the next one after a boundary).
9. `AddScriptTick_FromAnotherThread_LandsInTheNextClosedFrame` (a `Thread` calls `AddScriptTick(7)`,
   `AddScriptParallel(3)` and `AddOccasional(1)`, joined; close; the window shows 7, 3, 1 and calls 1).
10. `SpawnsBeforeTheFirstBoundary_LandInPreFrameTotals_NotInAWindow` (`CountSpawn()` and
    `AddSpawnTime(5)` before the first `CloseFrame`: the mission extras show `PreFrameSpawns` 1 and
    `PreFrameSpawnMs` 5; the first window shows 0 spawns).
11. `TakeMissionExtras_SumsEveryClosedFrame_AndCountsHitchesWithAnimLoading`.
12. `TakeMissionExtras_TopsComeFromTheSpawnAndScriptTables`.
13. `BeginMission_ResetsEveryExtra`.
14. `BeginMission_WithoutBehaviourTiming_DefaultsToFullMode` (plan 028's four-argument call keeps its
    semantics: `BehaviourTiming` is true).

If `BehaviourTickTable` has no mission-level totals in your tree, also create
`BehaviourTickTableMissionTests.cs` (no category) with `FoldFrame_AlsoAccumulatesTheMissionLevel`,
`ResetWindow_KeepsTheMissionLevel`, `ResetMission_ClearsTheMissionLevel`. If plan 028's `[TickSummary]`
work already gave it a mission level, reuse that, skip these three and say so in your report.

**Verify RED**: the test build fails on the missing members (`AddPreTickAll`, `AddOnTick`,
`AddScriptTick`, `MarkAnimLoading`, `TakeExtrasWindow`, `TakeMissionExtras`, the fifth `BeginMission`
parameter, `HitchFrame.Detail`).

GREEN:
- `Main/Features/MissionPerf/ProbeTotals.cs`: `public sealed class HitchDetailFrame` (fields in
  `[HitchDetail]` order: `SpawnMs`, `ScriptTickMs`, `ScriptParallelMs` (double, `NaN` meaning not
  measured), `AnimLoading` (int: 0, 1, or -1 for not sampled), `Mode` (string), `Spawns`, `OccasionalMs`
  (NaN when not measured), `OnTickMs`, `PreTickAllMs`); `public sealed class ExtrasWindow` (`Spawns`,
  `SpawnMs`, `SpawnTop` (`IReadOnlyList<BehaviourTotal>`), `ScriptCalls`, `ScriptTickMs`,
  `ScriptParallelMs`, `OccasionalMs` (NaN when not measured), `ScriptTop`, `Frames`, `LoadingFrames`,
  `AnimSampling` (bool)); `public sealed class MissionExtras` (every `[TickSummaryExtra]` field in order,
  NaN or -1 for `na`, tops as lists). Each with a public constructor in line order (the line tests build
  them directly).
- `MissionTickProfiler.cs`:
  - `BeginMission(long nowTicks, int mainThreadId, bool measuring, double hitchThresholdMs, bool behaviourTiming = true)`;
    `public bool BehaviourTiming { get; private set; }`; settable properties `SpawnAttribution`,
    `ScriptAttribution`, `AnimSampling` (main thread, set at mission start, default false; they decide
    `none` and `na`).
  - `public BehaviourTickTable SpawnBuilds { get; }`, `public BehaviourTickTable ScriptComponents { get; }`
    (constructed beside `Behaviours`, reset with it).
  - Main-thread adders into the open frame: `AddOnTick(long)`, `AddPreTickAll(long)`, `CountSpawn()`,
    `AddSpawnTime(long)`, `MarkAnimLoading(bool)`. Any-thread adders (`Interlocked.Add` and
    `Interlocked.Increment` on frame fields, taken with `Interlocked.Exchange(ref x, 0)` at
    `CloseFrame`, exactly like plan 028's agent tick): `AddScriptTick(long)` (adds time and one call),
    `AddScriptParallel(long)`, `AddOccasional(long)`, `CountOffMainSpawn()`.
  - `CloseFrame`: on the first boundary of a mission, move the frame's spawn count and spawn time into
    the mission's pre-frame totals, then discard as plan 028 does (all new frame fields zeroed or
    exchanged to 0). Otherwise, when `BehaviourTiming` is false, compute the three behaviour columns as
    in Design ("Phase columns per mode") before plan 028's `otherMs` formula; build `HitchFrame.Detail`
    (a `HitchDetailFrame`) only when the frame is a hitch; fold the new frame fields into the window and
    the mission (frames, spawns, spawn ms, script calls and ms, parallel, occasional, onTick, preTickAll,
    loading frames, hitches with loading), fold `SpawnBuilds` and `ScriptComponents` like `Behaviours`,
    then zero the frame fields. No allocation on a non-hitch frame.
  - `public ExtrasWindow TakeExtrasWindow(int topN)` (then resets the extras window and both tables'
    windows) and `public MissionExtras TakeMissionExtras(int topN)` (the mission-level tops from both
    tables). Call `TakeExtrasWindow` right after `TakeWindow`, so both cover the same closed frames.
  - `HitchFrame` gains `public HitchDetailFrame? Detail { get; }` (set by `CloseFrame`; plan 028's
    constructor keeps working: add the property with an internal setter or an optional constructor
    argument at the end).
- `BehaviourTickTable.cs`, only if it has no mission level: mission arrays beside the window arrays,
  folded in `FoldFrame`, `TakeMissionTop(int n, long ticksPerSecond)`, `ResetMission()`; `ResetWindow`
  leaves them alone.

**Verify**: `--filter "FullyQualifiedName~MissionTickProfilerProbeTests|FullyQualifiedName~BehaviourTickTable|FullyQualifiedName~MissionTickProfilerTests"`:
the 14 new cases (17 with the mission-level file) pass, and plan 028's `MissionTickProfilerTests` and
`BehaviourTickTableTests` still pass unchanged.

### Step 4: the line builders (TDD, the contract)

RED: `HitchProbeLinesTests.cs` (no category), sixteen cases:
`BuildSpawnProfile_SampleWindow_MatchesThePinnedLiteral`, `BuildSpawnProfile_NoAttribution_WritesTopNone`,
`BuildScriptProfile_FullWindow_MatchesThePinnedLiteral`, `BuildScriptProfile_ProbeWindow_MatchesThePinnedLiteral`,
`BuildAnimLoad_SampleWindow_MatchesThePinnedLiteral`, `BuildHitchDetail_FullFrame_MatchesThePinnedLiteral`,
`BuildHitchDetail_ProbeFrameWithoutSampler_MatchesThePinnedLiteral`,
`BuildTickSummaryExtra_FullMission_MatchesThePinnedLiteral`,
`BuildTickSummaryExtra_ProbeMissionWithoutSampler_MatchesThePinnedLiteral`,
`BuildProbeInstallLine_Applied_MatchesThePinnedLiteral`, `BuildAttributionInstallLine_AllSitesFound_MatchesThePinnedLiteral`,
`BuildMissionHeader_Probe_MatchesThePinnedLiteral`, `BuildAnimCostLine_UnderBudget_MatchesThePinnedLiteral`
(the literals and inputs in Design, exactly),
`Build_CommaDecimalCulture_StillWritesInvariantPoints` (`de-DE` in a `try`/`finally`),
`StatusLines_NeverContainADataTag` (every status constant and builder, called with sample arguments,
starts with `[TickProfiler] ` and contains none of `[TickProfile]`, `[Hitch]`, `[PerfContext]`,
`[MissionPerf]`, `[TickSummary]`, `[SpawnProfile]`, `[ScriptProfile]`, `[AnimLoad]`, `[HitchDetail]`,
`[TickSummaryExtra]`), `DataTags_NeverContainAnotherDataTag` (each of the five new tags against the
other nine strings, with `string.Contains`).
Also update plan 028's `TickProfileLinesTests` only if a test there asserts the old `OffLine` text
(`git grep -n "no patches installed" -- TAOM.Tests`): change that expectation to the new text and say so
in your report; touch nothing else in that file.

**Verify RED**: the build fails on the missing `HitchProbeLines`.

GREEN: `Main/Features/MissionPerf/HitchProbeLines.cs`, `public static class HitchProbeLines`:
`BuildSpawnProfile(double tSeconds, ExtrasWindow w, bool attribution)`,
`BuildScriptProfile(double tSeconds, ExtrasWindow w, bool attribution)`,
`BuildAnimLoad(double tSeconds, ExtrasWindow w)`, `BuildHitchDetail(double tSeconds, HitchDetailFrame d)`,
`BuildTickSummaryExtra(MissionExtras m)`, and the status members: `ProbeOffLine`,
`ScriptDelegateUnboundLine`, `ProbeNotInstalledLine`, `WaitUnseenLine` (constants),
`BuildProbeInstallLine(bool applied, string enabledBy, double bookkeepingUs)`,
`BuildAttributionInstallLine(int spawnSites, int scriptSites, int scriptExpected, bool scriptDelegateBound)`,
`BuildMissionHeader(int missionInProcess, string mode, double hitchThresholdMs, bool spawnAttribution, bool scriptAttribution, bool animSampling)`,
`BuildScriptThreadLine(bool onMain, int threadId, int mainThreadId)`, `BuildSpawnOffMainLine(int threadId)`,
`BuildAnimCostLine(double medianUs, int calls, double budgetUs, bool on)`, `BuildAnimFault(Exception ex)`,
`BuildHookFault(string part, Exception ex)`. Texts exactly as under "Status lines" in Design. Numbers
through `string.Format(CultureInfo.InvariantCulture, ...)`; `top=` through a `StringBuilder`; `NaN`
prints `na`, a negative count prints `na`, an empty list prints `none`.
In `TickProfileLines.cs`: change `OffLine` to the new text in Design, and add `int expected = 1` to
`BuildSiteCountWarning`, so the text reads `expected <expected>`. Nothing else in that file.

**Verify**: `--filter "FullyQualifiedName~HitchProbeLinesTests|FullyQualifiedName~TickProfileLinesTests"`:
16 new pass, plan 028's pass (its three data literals untouched). Copy the eight data literals into
your report.

### Step 5: occurrence swaps in the transpiler (TDD, synthetic streams)

RED: `TickProfilerTranspilerOccurrenceTests.cs` (no category; `MethodInfo`s are metadata reads that work
on the reference assemblies; private static stub helpers in the test class with the helper shapes, the
`PartyIconScaleTranspilerTests` pattern):
`Rewrite_FourOccurrencesWithFourHelpers_SwapsEachInOrder` (a stream with four
`call TWParallel::For(int,int,float,ParallelForWithDtAuxPredicate,int)`; helpers A, A, A, B; the fourth
instruction's operand is B);
`Rewrite_OccurrenceCountDiffersFromHelperCount_LeavesStreamUnmodifiedAndWarnsOnce` (three occurrences,
four helpers: unmodified, `swapped == 0`, one warning that equals
`TickProfileLines.BuildSiteCountWarning(label, target, 3, 4)`);
`Rewrite_StaticTarget_HelperWithTheSameParameters_Validates`;
`Rewrite_StaticTarget_HelperWithAnExtraLeadingParameter_IsRejected`;
`Rewrite_InstanceAndStaticSwapsTogether_SwapsAll` (the `TickComponents` shape: four `For` plus one
`callvirt ScriptComponentBehavior::OnTick`, `swapped == 5`);
`Rewrite_SingleHelperSwap_StillExpectsExactlyOne` (plan 028's behaviour preserved: two occurrences of a
single-helper swap leave the stream unmodified).

**Verify RED**: the build fails on the missing `CallSwap(MethodInfo, IReadOnlyList<MethodInfo>)`
constructor.

GREEN, `TickProfilerTranspiler.cs`: `CallSwap` gains `IReadOnlyList<MethodInfo> Helpers` and a second
constructor `CallSwap(MethodInfo target, IReadOnlyList<MethodInfo> helpersByOccurrence)`; the existing
one-helper constructor sets `Helpers = new[] { helper }` (and `Helper` stays as plan 028 wrote it).
`Rewrite`: validate every helper (for an instance target, plan 028's rule; for a static target, the
helper's parameters equal the target's exactly); a swap matches when its count equals `Helpers.Count`;
occurrence `k` (in stream order) gets `Helpers[k]`; `swapped` is the total number of instructions
swapped. Any mismatch: one `BuildSiteCountWarning(methodLabel, target, count, Helpers.Count)`, stream
unmodified, `swapped = 0`. Never throw.

**Verify**: `--filter "FullyQualifiedName~TickProfilerTranspiler"`: the 6 new and plan 028's
`TickProfilerTranspilerTests` all pass.

### Step 6: prove open-delegate virtual dispatch (TDD)

`MissionAttributionHooks.TimedScriptTick` will call `protected internal virtual OnTick` through an open
delegate. Prove the mechanism first, with no engine type.

RED: `OpenDelegateDispatchTests.cs` (no category). In the test file declare
`public class DispatchBase { protected internal virtual void Tick(float dt) { Calls = "base"; } public string Calls = ""; }`
and `public sealed class DispatchDerived : DispatchBase { protected internal override void Tick(float dt) { Calls = "derived"; } }`,
plus a `ThrowingDerived` whose override throws `InvalidOperationException`. Cases:
`OpenDelegate_ProtectedInternalVirtual_CallsTheOverride` (bind
`(Action<DispatchBase, float>)Delegate.CreateDelegate(typeof(Action<DispatchBase, float>), typeof(DispatchBase).GetMethod("Tick", BindingFlags.Instance | BindingFlags.NonPublic))`,
invoke with a `DispatchDerived`: `Calls == "derived"`) and
`OpenDelegate_ThrowingOverride_PropagatesTheException`. Write the tests to use a helper
`ProbeDelegates.BindOpenInstance<TTarget>(MethodInfo)` that does not exist yet.

**Verify RED**: the build fails on the missing `ProbeDelegates`.

GREEN: `Main/Features/MissionPerf/Hooks/MissionAttributionHooks.cs` will need it, but the helper is pure:
add `internal static class ProbeDelegates` to `Main/Features/MissionPerf/ProbeTotals.cs` with
`internal static Action<T, float>? BindOpenInstance<T>(MethodInfo? method)` (null method or any
exception: null). Both tests pass. If `OpenDelegate_ProtectedInternalVirtual_CallsTheOverride` fails
(the delegate calls the base), STOP: the script attribution design depends on it.

**Verify**: `--filter "FullyQualifiedName~OpenDelegateDispatchTests"`: 2 passed.

### Step 7: the adapter and the clip-loading sampler (TDD)

RED: `AnimLoadingSamplerTests.cs` (no category; `ticksPerSecond` 1,000,000 so a tick is a
microsecond). The fake clock is a `long now` field read by `Func<long> clock = () => now`, and it moves
only inside the adapter: `Substitute.For<IAnimationLoadingAdapter>()` whose
`IsAnyAnimationLoadingFromDisk()` is set up with `.Returns(_ => { now += costs[i++]; return false; })`
(or throws, per case). The sampler reads the clock once immediately before and once immediately after
each adapter call, so each call's measured cost is exactly its scripted cost. Cases:
`MeasureCost_UnderBudget_EnablesSampling_AndReportsTheMedian` (32 calls costing 1 to 32 us in shuffled
order: median (16 + 17) / 2 = 16.5, the returned line equals
`HitchProbeLines.BuildAnimCostLine(16.5, 32, 20, true)`),
`MeasureCost_OverBudget_DisablesSampling` (32 calls of 25 us: `Enabled` false, the line ends
`over budget, sampling is off for this process`),
`MeasureCost_AdapterThrows_DisablesSampling_AndQueuesTheFaultLine` (returns null; `TakePendingFault()`
returns `HitchProbeLines.BuildAnimFault(ex)` once, then null),
`Sample_BeforeMeasureCost_ReturnsFalseWithoutCallingTheAdapter`,
`Sample_AdapterThrows_DisablesSampling_AndReportsTheFaultOnce` (the fault line is returned by
`TakePendingFault()` once, then null), `MeasureCost_SecondCall_DoesNothingAndReturnsNull`.

**Verify RED**: the build fails on the missing `IAnimationLoadingAdapter` and `AnimLoadingSampler`.

GREEN:
- If `Main/Adapters/IAnimationLoadingAdapter.cs` and `AnimationLoadingAdapter.cs` exist (plan 036
  landed), use them unchanged. Otherwise create them from plan 036's Step 7 specification (quoted in
  Current state), adding nothing to it, no XML summary included:
  `namespace TAOM.Adapters;` `public interface IAnimationLoadingAdapter { bool IsAnyAnimationLoadingFromDisk(); }`
  and `public sealed class AnimationLoadingAdapter : IAnimationLoadingAdapter` whose method returns
  `MBAnimation.IsAnyAnimationLoadingFromDisk()` (`using TaleWorlds.MountAndBlade;`).
- `Main/Features/MissionPerf/AnimLoadingSampler.cs`, `public sealed class AnimLoadingSampler`:
  constructor `(IAnimationLoadingAdapter adapter, Func<long> clock, long ticksPerSecond)`;
  `public const int CostCalls = 32; public const double BudgetUs = 20.0;`; `public bool Enabled`
  (false until `MeasureCost` turns it on); `public string? MeasureCost()` (once per instance: times
  `CostCalls` calls, each between two clock reads, takes the median, sets `Enabled`, returns the cost
  line; on an adapter exception leaves `Enabled` false, stores the fault line for `TakePendingFault()`
  and returns null; a second call returns null); `public bool Sample()` (false when not enabled; on an
  exception sets `Enabled` false, stores the fault line for `TakePendingFault()`, returns false);
  `public string? TakePendingFault()` (returns the stored line once, then null). The caller logs the
  cost line with `LogInfo` and a fault line with `LogWarning`.

**Verify**: `--filter "FullyQualifiedName~AnimLoadingSamplerTests"`: 6 passed.

### Step 8: the bookkeeping cost meter and the window writer (TDD)

RED:
- `ProbeCostMeterTests.cs` (no category): `MeasureBookkeeping_ReturnsAFinitePositiveCost` (2,000 frames)
  and `MeasureBookkeeping_LeavesTheStaticProfilerUntouched` (`MissionTickProfilerHooks.Profiler` is the
  same object, and its window empty, before and after).
- `ProbeWindowWriterTests.cs` (no category; NSubstitute `IModLogger`, a real `MissionTickProfiler` driven
  through two frames): `WriteExtras_ProbeMode_WritesScriptProfileThenAnimLoad`,
  `WriteExtras_WithSpawns_WritesSpawnProfileFirst`, `WriteExtras_SamplerOff_WritesNoAnimLoad`,
  `WriteMissionEnd_WritesOneTickSummaryExtraLine`, `WriteMissionHeader_WritesTheHeaderLine` (each
  asserts the `LogInfo` arguments in order with `Received.InOrder`, and that each starts with its tag).

**Verify RED**: the build fails on the missing `ProbeCostMeter` and `ProbeWindowWriter`.

GREEN:
- `Main/Features/MissionPerf/ProbeCostMeter.cs`, `public static class`:
  `public static double MeasureBookkeepingMicroseconds(int frames)`: a scratch
  `new MissionTickProfiler(Stopwatch.Frequency)`, `BeginMission(..., measuring: true, hitchThresholdMs: double.MaxValue, behaviourTiming: false)`,
  then per frame the calls the hooks make on a frame, each bracket read with two
  `Stopwatch.GetTimestamp()` calls: `CloseFrame(...)` with `AllocationCounter.ReadOrZero()` and the three
  `GC.CollectionCount`, `MarkAnimLoading(false)`, `AddWait`, `AddPreTickAll`, `AddOnTick`,
  `AddAgentTick(x, false)`, `AddScriptTick`, two `CountSpawn()` and one `AddSpawnTime`; returns the
  elapsed microseconds divided by `frames`. 200 warm-up frames first, not counted.
- `Main/Features/MissionPerf/ProbeWindowWriter.cs`, `public static class`:
  `WriteExtras(IModLogger logger, double tSeconds, ExtrasWindow w, MissionTickProfiler p)` (`[SpawnProfile]`
  when `w.Spawns > 0`, then `[ScriptProfile]`, then `[AnimLoad]` when `w.AnimSampling`),
  `WriteMissionEnd(IModLogger logger, MissionExtras m)`, `WriteMissionHeader(IModLogger logger, string line)`.
  All `LogInfo`.

**Verify**: `--filter "FullyQualifiedName~ProbeCostMeterTests|FullyQualifiedName~ProbeWindowWriterTests"`:
7 passed.

### Step 9: the probe hooks, the Patch98 classes and the PatchShield entries (TDD)

Read `.claude/rules/harmony-patches.md` and `docs/reviews/lessons/harmony-il.md` first (mandatory).

RED:
1. `HitchProbeHooksTests.cs` (no category: the probe hooks touch no engine object). `[TestInitialize]`
   sets `MissionTickProfilerHooks.Profiler = new MissionTickProfiler(Stopwatch.Frequency)` and
   `MissionTickProfilerHooks.Logger` to an NSubstitute `IModLogger`, then begins a probe-mode mission
   on the profiler directly:
   `MissionTickProfilerHooks.Profiler.BeginMission(Stopwatch.GetTimestamp(), Environment.CurrentManagedThreadId, measuring: true, hitchThresholdMs: 1000, behaviourTiming: false)`
   (Step 3 added that parameter; the hooks' own `BeginMission` gains it only in Step 12).
   `[TestCleanup]` calls `HitchProbeHooks.ResetForTests()` and `MissionTickProfilerHooks.ResetForTests()`.
   Twelve cases: `PreTickBracket_Measuring_AddsPreTickAllToTheOpenFrame`;
   `PreTickEnter_SamplerOn_MarksTheOpenFrame` (a sampler over a substitute adapter returning true, cost
   measured with a fake clock); `WaitBracket_ProbeMode_AddsWait`; `WaitBracket_WaitSwapActive_AddsNothing`;
   `WaitNeverSeen_ThirtyFrames_LogsTheReasonLineOnce` (31 pre-tick brackets without a wait bracket:
   exactly one `LogWarning` equal to `HitchProbeLines.WaitUnseenLine`);
   `SpawnBracket_Nested_TimesOnlyTheOutermost_AndCountsBoth`;
   `SpawnBracket_OffMainThread_CountsWithoutTiming_AndLogsOnce` (two spawns from a second `Thread`, one
   `LogWarning`); `SpawnExit_WithoutEnter_RecordsNothing`;
   `ScriptBracket_OffMainThread_AddsTotals_AndLogsTheThreadLineOnce` (two brackets from a second
   `Thread`: exactly one `LogWarning` equal to `BuildScriptThreadLine(false, ...)`, and
   `HitchProbeHooks.ScriptOffMain` reads true afterwards); `ScriptBracket_MainThread_LogsTheThreadLineOnce`
   (two brackets on the test thread: exactly one `LogInfo` equal to `BuildScriptThreadLine(true, ...)`);
   `OnTickBracket_Measuring_AddsOnTick`; `Brackets_NotMeasuring_RecordNothing`.
2. `HitchProbeBindingTests.cs` (`[ClassInitialize]` sets `_gameLoaded = GameAssemblies.EnsureLoaded()`;
   each test starts with `if (!_gameLoaded) Assert.Inconclusive(...)`; model on plan 028's
   `MissionTickProfilerBindingTests`), all `[TestCategory("BindingVerification")]`:
   `ProbeTargets_ResolveInInstalledEngine` (for each `Patch98_HitchProbe` class, `TargetOf(patch)` is not
   null and not virtual; copy `TargetOf` from `CreatureBanditsWiringTests.cs:47-54`) and
   `ProbeAndAttributionTargets_AreOnPatchShieldsExclusionList` (every target of the Patch98 classes and of
   the two Step 11 attribution classes passes `PatchShieldPolicy.IsExcludedTargetMethod(target.DeclaringType?.FullName, target.Name)`;
   until Step 11 exists, walk the Patch98 classes only, then extend the walk in Step 11).

**Verify RED**: the build fails on the missing `HitchProbeHooks`, Patch98 classes and
`MissionTickProfilerHooks.ResetForTests`. Once they compile
and before the `PatchShieldPolicy` edit, `ProbeAndAttributionTargets_AreOnPatchShieldsExclusionList`
fails naming `TaleWorlds.MountAndBlade.Mission.WaitTickCompletion` or `Mission.SpawnAgent` or
`TaleWorlds.Engine.ManagedScriptHolder.TickComponents`. Run it once in that state and quote the failure.

GREEN:
- `Main/Features/MissionPerf/Hooks/HitchProbeHooks.cs`, `public static class HitchProbeHooks`:
  `internal static AnimLoadingSampler? Sampler`, `internal static bool WaitSwapActive`, part-disabled
  flags, and the brackets in Design's table: `OnPreTickEnter/Exit`, `OnWaitEnter/Exit`, `OnTickEnter/Exit`,
  `OnScriptTickEnter/Exit` (`[ThreadStatic]` open flag and start stamp; `AddScriptTick` from any thread;
  the thread line once per process through an `Interlocked.CompareExchange` guard; when off the main
  thread, set `internal static volatile bool ScriptOffMain`), `OnSpawnEnter/Exit` (main-thread depth
  counter; off-main: `CountOffMainSpawn()` and the line once). Each reads
  `MissionTickProfilerHooks.Profiler` once into a local and returns at once when it is null or not
  measuring; uses a `bool` for "open", never a 0 stamp; wraps its body in `try`/`catch`, and on an
  exception sets that part's disabled flag and logs `HitchProbeLines.BuildHookFault(part, ex)` with
  `LogError` once. `OnPreTickEnter` samples with `Sampler` when enabled and calls
  `profiler.MarkAnimLoading(...)`, then logs a pending sampler fault (`TakePendingFault()`) with
  `LogWarning` if there is one. The off-main spawn line and `WaitUnseenLine` are `LogWarning` too (the
  level table in Design). The wait
  self-check counts the first 30 measured pre-tick brackets of the process while `WaitSwapActive` is
  false. `internal static void ResetForTests()` clears every static (the once-per-process line guards
  and the self-check counter included). Model it on `Main/Features/AdvancedCombat/MissionThreadGuard.cs:62`.
  If the file would reach 150 lines, make the class `public static partial class HitchProbeHooks` and
  move the spawn brackets to `HitchProbeHooks.Spawn.cs` and the script brackets to
  `HitchProbeHooks.Script.cs` (Scope names these two and no other split).
- `MissionTickProfilerHooks.cs` (plan 028's): add `internal static void ResetForTests()`, which sets
  `Profiler`, `Logger` and `WaitTickCompletionCall` to null, `Installed` to false, `OnTickSites` and
  `OnPreTickSites` to 0, and resets any other static plan 028 gave the class (to its declared initial
  value). If that would take the file to 150 lines, make the class `partial` and put this plan's
  additions to it (this member and Step 12's) in `MissionTickProfilerHooks.Probe.cs`.
- `Main/Features/MissionPerf/Hooks/Patch98_HitchProbe.cs`: five `public static class`es, all
  `[HarmonyPatchCategory("Patch98_HitchProbe")]`, the `WaitTickCompletion` class first:
  `Mission_WaitTickCompletion_HitchProbe_Patch` (`[HarmonyPatch(typeof(Mission), "WaitTickCompletion")]`),
  `Mission_OnPreTick_HitchProbe_Patch` (`[HarmonyPatch(typeof(Mission), "OnPreTick", new[] { typeof(float) })]`;
  prefix body `{ MissionTickProfilerHooks.OnFrameBoundary(); HitchProbeHooks.OnPreTickEnter(); }`),
  `Mission_OnTick_HitchProbe_Patch` (`[HarmonyPatch(typeof(Mission), nameof(Mission.OnTick), new[] { typeof(float), typeof(float), typeof(bool), typeof(bool) })]`),
  `ManagedScriptHolder_TickComponents_HitchProbe_Patch` (`[HarmonyPatch(typeof(ManagedScriptHolder), "TickComponents", new[] { typeof(float) })]`),
  `Mission_SpawnAgent_HitchProbe_Patch` (`[HarmonyPatch(typeof(Mission), nameof(Mission.SpawnAgent), new[] { typeof(AgentBuildData), typeof(bool), typeof(Equipment), typeof(ItemObject) })]`).
  Each: `[HarmonyPrefix] [HarmonyPriority(Priority.First)] public static void Prefix()` and
  `[HarmonyFinalizer] [HarmonyPriority(Priority.Last)] public static void Finalizer()` (void: Harmony
  rethrows the original's exception). A header comment quotes the engine signatures and the thread facts
  from Current state. The word `OnFrameBoundary` appears in this file exactly once, on the prefix body
  line above; no comment names it (the Done criteria count it with a comment-blind `git grep`).
- `Patch97_MissionTickProfiler.cs` (plan 028's): remove the `[HarmonyPrefix]` from
  `Mission_OnPreTick_TickProfiler_Patch` (its transpiler stays); add a one-line comment saying the frame
  boundary now lives in `Patch98_HitchProbe` so both modes share it. That comment, and every other line
  left in this file, must not contain the word `OnFrameBoundary` (write "the frame boundary").
- `Dependencies/Foundation/PatchShieldPolicy.cs`: append to `ExcludedTargetMethods`, with the comment
  "Patch98_HitchProbe (2026-10-02): the per-frame and per-spawn methods it brackets for every player; a
  per-call finalizer would sit inside the measurement. HitchProbeBindingTests walks the real targets.":
  `"TaleWorlds.MountAndBlade.Mission.WaitTickCompletion"`, `"TaleWorlds.MountAndBlade.Mission.SpawnAgent"`,
  `"TaleWorlds.Engine.ManagedScriptHolder.TickComponents"`.

**Verify**: `--filter "FullyQualifiedName~HitchProbeHooksTests"`: 12 passed. The binding gate: all pass,
none inconclusive, `HitchProbeBindingTests` included. `wc -l Main/Features/MissionPerf/Hooks/*.cs`:
every file under 150 lines (if `HitchProbeHooks.cs` is not, use the two split files named above).

### Step 10: the probe installer and plan 028's installer (TDD)

RED: `HitchProbeInstallerTests.cs` (no category; NSubstitute settings provider and logger; a recording
`Func<string, bool>` fake for `tryPatchCategory` that appends each category name to a list and returns
a per-test result). Every case drives the one public entry point,
`MissionTickProfilerInstaller.InstallIfEnabled(settings, logger, recorder)`, never
`HitchProbeInstaller.Install` directly. `[TestCleanup]` calls, in this order,
`MissionTickProfilerInstaller.ResetForTests()`, `HitchProbeInstaller.ResetForTests()`,
`HitchProbeHooks.ResetForTests()` and `MissionTickProfilerHooks.ResetForTests()`, so plan 028's
`_attempted` flag and every hook static start clear in the next test. Six cases:
`Install_BothTogglesOff_AppliesNothing_AndLogsBothOffLines` (no category applied; `LogInfo` received
plan 028's new `OffLine` and `HitchProbeLines.ProbeOffLine`);
`Install_ProbeOnProfilerOff_AppliesOnlyTheProbeCategory_AndLogsTheInstallLine` (applied
`["Patch98_HitchProbe"]`; one `LogInfo` starting `[TickProfiler] probe install: category applied, enabled by hitch probe,`;
`HitchProbeHooks.WaitSwapActive` false);
`Install_ProfilerOn_AppliesPatch97ThenTheProbe` (order `["Patch97_MissionTickProfiler", "Patch98_HitchProbe"]`,
`enabled by tick profiler` or `both` per the toggles);
`Install_ProbeCategoryFails_LeavesItUninstalled_AndSaysSo` (`category failed`; `ProbeInstalled` false;
plan 028's `Installed` false);
`Install_Patch97WaitSwapLiveButOnTickSitesShort_MarksTheWaitSwapActive` (profiler on; the recorder,
when called with `"Patch97_MissionTickProfiler"`, simulates the transpilers by setting
`MissionTickProfilerHooks.OnPreTickSites = 2`, `OnTickSites = 1` and `WaitTickCompletionCall = _ => { }`,
then returns true: afterwards `MissionTickProfilerHooks.Installed` is false and
`HitchProbeHooks.WaitSwapActive` is true, so the wait is recorded once, by plan 028's swapped helper);
`Install_SecondCall_DoesNothing` (both toggles on; a second `InstallIfEnabled` call adds no category to
the recorder and no log call: this exercises plan 028's `_attempted` guard, the only once-guard;
`HitchProbeInstaller.Install` has none of its own because only `InstallIfEnabled` calls it).

**Verify RED**: the build fails on the missing `HitchProbeInstaller` and the two installers'
`ResetForTests`.

GREEN:
- `Main/Features/MissionPerf/Hooks/HitchProbeInstaller.cs`, `internal static class`:
  `internal const string Category = "Patch98_HitchProbe";` `internal static bool ProbeInstalled`;
  `internal static void Install(IBattleLoadDiagnosticsSettingsProvider settings, IModLogger logger, Func<string, bool> tryPatchCategory, bool profilerRequested, bool waitSwapLive)`:
  when neither `settings.HitchProbeEnabled` nor `profilerRequested`: log `ProbeOffLine` (`LogInfo`) and
  return. Otherwise make sure `MissionTickProfilerHooks.Profiler` and `Logger` are set (create the
  profiler with `Stopwatch.Frequency` if plan 028's path did not), set
  `HitchProbeHooks.Sampler = new AnimLoadingSampler(new AnimationLoadingAdapter(), Stopwatch.GetTimestamp, Stopwatch.Frequency)`
  (the adapter is stateless and the hooks are static, outside the container; plan 028's hooks take their
  logger the same way), measure `ProbeCostMeter.MeasureBookkeepingMicroseconds(2000)`, apply
  `tryPatchCategory(Category)`, set `ProbeInstalled`, set `HitchProbeHooks.WaitSwapActive = waitSwapLive`
  (see Design "The wait": it does not read plan 028's `Installed`),
  then `MissionTickProfilerHooks.Installed = MissionTickProfilerHooks.Installed && ProbeInstalled`, and
  log `BuildProbeInstallLine(...)` with `LogInfo`. Everything in `try`/`catch`: on an exception log
  `HitchProbeLines.BuildHookFault("probe install", ex)` with `LogError` and leave `ProbeInstalled` false.
  `internal static void ResetForTests()` sets `ProbeInstalled` to false.
- `MissionTickProfilerInstaller.cs` (plan 028's): add `internal static void ResetForTests()`, which sets
  `_attempted` to false.
- `MissionTickProfilerInstaller.InstallIfEnabled` (plan 028's): keep `_attempted` first. When
  `settings.TickProfilerEnabled` is false, log the (new) `OffLine` and do NOT return; skip only the
  Patch97 work. When true, before applying Patch97, Step 11 will bind the script tick delegate: for now
  put the comment line `// Step 11: bind the script tick delegate here, before Patch97 applies.` at that
  point (a call to a type that does not exist yet would not compile). Keep plan 028's `applied` result
  in a local, `false` when Patch97 was skipped. After the Patch97 work (or its skip), call
  `HitchProbeInstaller.Install(settings, logger, tryPatchCategory, settings.TickProfilerEnabled, applied && MissionTickProfilerHooks.WaitTickCompletionCall != null && MissionTickProfilerHooks.OnPreTickSites == 2)`.
  No change to its signature, so `Main/SubModule.cs`'s call stays as it is.

**Verify**: `--filter "FullyQualifiedName~HitchProbeInstallerTests"`: 6 passed, and the same filter run a
second time in one process order still passes (no test depends on another's statics); plan 028's tests
still pass (`--filter "FullyQualifiedName~MissionPerf"`).

### Step 11: the attribution transpilers and helpers (TDD)

RED:
1. `MissionAttributionHooksTests.cs`, class-tagged `[TestCategory("RequiresGame")]` (a `MissionLogic`
   probe, the `FeatureModuleHooksTests` pattern; `TWParallel` runs a range smaller than its grain inline,
   but its type initialiser is engine code). Five cases:
   `TimedAgentBuild_Attributing_CallsOnAgentBuildOnce_AndRecordsBySpawnType`;
   `TimedAgentBuild_NotAttributing_CallsThroughAndRecordsNothing`;
   `TimedAgentBuild_BehaviourThrows_PropagatesTheSameException_AndRecordsTheCall`;
   `TimedParallelBlock_RangeBelowGrain_RunsTheBodyOnce_AndAddsParallelTime`;
   `TimedOccasionalBlock_RangeBelowGrain_AddsOccasionalTime`.
2. In `HitchProbeBindingTests.cs` add, `[TestCategory("BindingVerification")] [TestCategory("RequiresGameIL")]`:
   `SpawnAgentRewrite_FindsBothAgentBuildCalls_InInstalledEngine` (feed
   `PatchProcessor.GetOriginalInstructions(AccessTools.Method(typeof(Mission), nameof(Mission.SpawnAgent)))`
   through `TickProfilerTranspiler.Rewrite` with `MissionAttributionInstaller.SpawnAgentSwaps()`: no
   `LogWarning`, `swapped == 2`) and
   `TickComponentsRewrite_FindsFourParallelForsAndOneOnTick_InInstalledEngine` (call
   `MissionAttributionInstaller.BindScriptTickDelegate()` first and assert true; then
   `TickComponentsSwaps()`: `swapped == 5`, and the fourth swapped `For` site's operand is
   `TimedOccasionalBlock`). Extend `ProbeAndAttributionTargets_AreOnPatchShieldsExclusionList` to the two
   new classes.

**Verify RED**: the build fails on the missing `MissionAttributionHooks` and `MissionAttributionInstaller`.

GREEN:
- `Main/Features/MissionPerf/Hooks/MissionAttributionHooks.cs`, `public static class`:
  `internal static Action<ScriptComponentBehavior, float>? ScriptTickCall`;
  `public static void TimedAgentBuild(MissionBehavior behavior, Agent agent, Banner banner)` (plan 028's
  `Timed` shape: when the profiler is null, not measuring, not `BehaviourTiming` or not
  `SpawnAttribution`, or off the main thread, call `behavior.OnAgentBuild(agent, banner)` and return;
  else slot from `profiler.SpawnBuilds.SlotFor(behavior.GetType())`, timestamps around the call in
  `try`/`finally`, `profiler.SpawnBuilds.Record(slot, elapsed, 0)`);
  `public static void TimedScriptTick(ScriptComponentBehavior component, float dt)` (same shape over
  `ScriptTickCall(component, dt)`, recording into `ScriptComponents`, only on the main thread and only
  while `HitchProbeHooks.ScriptOffMain` is false);
  `public static void TimedParallelBlock(int fromInclusive, int toExclusive, float deltaTime, TWParallel.ParallelForWithDtAuxPredicate body, int grainSize)`
  and `TimedOccasionalBlock(...)` (same parameters; call `TWParallel.For(...)` inside `try`/`finally`;
  record with `AddScriptParallel` or `AddOccasional`, any thread). A helper's own bookkeeping fault turns
  attribution off with one `BuildHookFault` line and never swallows the engine call's exception.
- `MissionAttributionInstaller` (add it to `MissionAttributionHooks.cs` if the file stays under 150 lines,
  else its own file `Hooks/MissionAttributionInstaller.cs`): `BindScriptTickDelegate()` (via
  `ProbeDelegates.BindOpenInstance<ScriptComponentBehavior>(AccessTools.Method(typeof(ScriptComponentBehavior), "OnTick", new[] { typeof(float) }))`;
  sets `MissionAttributionHooks.ScriptTickCall`; returns false when the delegate is null, and logs
  nothing itself), `SpawnAgentSwaps()` (one
  `CallSwap` for `MissionBehavior.OnAgentBuild` with helpers `[TimedAgentBuild, TimedAgentBuild]`),
  `TickComponentsSwaps()` (`TWParallel.For(int, int, float, ParallelForWithDtAuxPredicate, int)` with
  helpers `[TimedParallelBlock, TimedParallelBlock, TimedParallelBlock, TimedOccasionalBlock]`, plus the
  `ScriptComponentBehavior.OnTick` swap to `TimedScriptTick` only when the delegate is bound), statics
  `SpawnSites`, `ScriptSites`, and the expected script count (5 bound, 4 not); and
  `internal static void ResetForTests()`, which zeroes `SpawnSites` and `ScriptSites` and nulls
  `MissionAttributionHooks.ScriptTickCall`. Add a call to it at the end of `HitchProbeInstallerTests`'
  `[TestCleanup]`, since `InstallIfEnabled` now binds the delegate when the profiler is on.
- `Main/Features/MissionPerf/Hooks/Patch97_MissionAttribution.cs`: two classes in
  `[HarmonyPatchCategory("Patch97_MissionTickProfiler")]` (plan 028's category, so they install only with
  the profiler): `Mission_SpawnAgent_Attribution_Patch` and `ManagedScriptHolder_TickComponents_Attribution_Patch`,
  each a `[HarmonyTranspiler]` calling `TickProfilerTranspiler.Rewrite(..., "Mission.SpawnAgent" | "ManagedScriptHolder.TickComponents", MissionTickProfilerHooks.Logger, out var swapped)`
  and storing `swapped` in `SpawnSites` or `ScriptSites`.
- `MissionTickProfilerInstaller.InstallIfEnabled`: replace the `// Step 11:` comment line from Step 10
  with `var bound = MissionAttributionInstaller.BindScriptTickDelegate();` (before applying Patch97,
  after plan 028 has set `Logger`), followed by `if (!bound) logger.LogWarning(HitchProbeLines.ScriptDelegateUnboundLine);`;
  after applying Patch97, log
  `HitchProbeLines.BuildAttributionInstallLine(SpawnSites, ScriptSites, expected, bound)` with `LogInfo`.
- `MissionTickProfilerHooks.Timed` (plan 028's): the "call straight through" condition becomes "profiler
  null, or not measuring, or not `BehaviourTiming`", so a probe-mode mission never records a behaviour
  call even with Patch97 installed. Leave `TimedWaitTickCompletion` as plan 028 wrote it (not gated on
  `BehaviourTiming`): while its swap is live it is the only wait recorder (Design "The wait").

**Verify**: `--filter "FullyQualifiedName~MissionAttributionHooksTests"`: 5 passed;
`--filter "FullyQualifiedName~HitchProbeInstallerTests"`: still 6 passed. The binding gate: all pass,
none inconclusive, including the two new rewrite tests.

### Step 12: the mission behaviour, the hitch detail and the two SubModule comments (wiring pins)

Pins, not a RED step: Steps 9 and 10 already made these four tests' subjects true, so they pass when
first run; they guard the wiring against later edits. Write them first anyway and quote their first
run. `HitchProbeWiringTests.cs` (no category; source pins through
`RepoPaths.ReadSource(path, stripComments: true)`, the `AnimaliaWiringTests` pattern, plus reflection):
`Patch98_OnPreTickPrefix_CallsTheFrameBoundaryBeforeTheProbe` (in `Patch98_HitchProbe.cs`,
`MissionTickProfilerHooks.OnFrameBoundary()` appears once and before `HitchProbeHooks.OnPreTickEnter()`);
`Patch97_NoLongerOwnsTheFrameBoundary` (`Patch97_MissionTickProfiler.cs` does not contain
`OnFrameBoundary`); `ProfilerInstaller_CallsTheProbeInstaller_Once` (`MissionTickProfilerInstaller.cs`
contains `HitchProbeInstaller.Install(` exactly once); `ProbeBrackets_HaveFirstPrefixAndLastFinalizer`
(reflection over every `Patch98_HitchProbe` class: the `Prefix` carries `HarmonyPriority` with
`Priority.First`, the `Finalizer` with `Priority.Last`). If any of the four fails on its first run,
fix the code from Steps 9 and 10 that it pins, not the test.

Then:
- `MissionTickProfilerHooks.BeginMission` gains `bool behaviourTiming = true` and forwards it.
  `OnFrameBoundary`: after logging `[Hitch]`, when `hitch.Detail` is not null, log
  `HitchProbeLines.BuildHitchDetail(t, hitch.Detail)` with `LogInfo`, same `t`. (If these additions
  take `MissionTickProfilerHooks.cs` to 150 lines, use `MissionTickProfilerHooks.Probe.cs` as Step 9
  says.)
- `MissionTickProfilerBehavior` (plan 028's): in `OnCreated` compute
  `behaviourTiming = MissionTickProfilerHooks.Installed && settings.TickProfilerEnabled` and
  `probe = HitchProbeInstaller.ProbeInstalled && (settings.HitchProbeEnabled || behaviourTiming)`; call
  `BeginMission(_missionStart, probe, hitchMs, behaviourTiming)`; then set on the profiler
  `SpawnAttribution = behaviourTiming && SpawnSites == 2`,
  `ScriptAttribution = behaviourTiming && ScriptSites == expected && !HitchProbeHooks.ScriptOffMain`,
  `AnimSampling = HitchProbeHooks.Sampler?.Enabled ?? false`. Pass `behaviourTiming` (not `probe`) as the
  `measuring` argument of `PerfContextReader.Read`, so `tickProfiler=` keeps its meaning. On the first
  tick: after `[PerfContext]`, when the sampler exists and has not measured yet, call `MeasureCost()`,
  log a non-null result with `LogInfo`, log a non-null `TakePendingFault()` with `LogWarning`, and
  refresh `AnimSampling`; then, unless both toggles are off (the
  game-start `probe off:` line already says why), write the mission header through
  `ProbeWindowWriter.WriteMissionHeader` (mode `full`, `probe` or, when a toggle is on but nothing is
  installed, `off`); when `settings.HitchProbeEnabled` is on but `ProbeInstalled` is false, also
  `LogWarning(HitchProbeLines.ProbeNotInstalledLine)` once per mission. In each window, right after plan
  028's `[TickProfile]` line, `ProbeWindowWriter.WriteExtras(logger, nowSeconds, profiler.TakeExtrasWindow(_topN), profiler)`.
  In `OnEndMission`, right after `[TickSummary]`,
  `ProbeWindowWriter.WriteMissionEnd(logger, profiler.TakeMissionExtras(_topN))`. Both lines are
  written when the mission measured (`probe` above was true at `OnCreated`), so a default probe-mode
  mission writes `[TickSummary]` and `[TickSummaryExtra]`. If plan 028's code writes `[TickSummary]`
  only when `settings.TickProfilerEnabled` or `MissionTickProfilerHooks.Installed` is true, change that
  condition to the mission's measuring flag and say so in your report. Keep the file under 150 lines
  (move logic into `ProbeWindowWriter` if needed); if it is still at 150 or more after that, STOP and
  report its line count.
- `Main/SubModule.cs` (single-owner), exactly two comment replacements and nothing else:
  1. The comment block directly above the `MissionTickProfilerInstaller.InstallIfEnabled(` call becomes:
     ```csharp
     // Patch98 hitch probe (default on) and Patch97 tick profiler (default off): the probe brackets
     // the frame's phases, scene scripts and spawns for [Hitch] and [HitchDetail]; the profiler adds
     // per-type attribution. Installed here, once per process, by their MCM toggles: game init
     // precedes every mission, so nothing that calls Mission.OnPreTick has run yet. The installers
     // contain their own failures.
     ```
  2. The comment directly above the `MissionTickProfilerBehavior` `AddTaomBehavior(` call becomes:
     ```csharp
     // [PerfContext] once per mission; while the probe or the profiler measures, a [TickProfile]
     // window (with script, spawn and clip-loading lines) on the same 5 s clock as [MissionPerf].
     ```

**Verify**: the build command exits 0; `--filter "FullyQualifiedName~HitchProbeWiringTests|FullyQualifiedName~MissionTickProfilerWiringTests|FullyQualifiedName~MissionBehaviorLifecycleTests"`
passes; `git diff <start> -- Main/SubModule.cs` shows only the two comment replacements;
`wc -l Main/Features/MissionPerf/Hooks/*.cs`: every file under 150 lines.

### Step 13: the overhead benchmark and the default decision

RED/GREEN in one: `HitchProbeOverheadBenchmarkTests.cs`, `[TestCategory("Benchmark")]`, one method
`HitchProbe_SimulatedFrame_CostsUnderHalfAPercentOfTenMilliseconds`. First line:
`if (Environment.GetEnvironmentVariable("TAOM_RUN_BENCHMARKS") != "1") Assert.Inconclusive("Benchmark: set TAOM_RUN_BENCHMARKS=1");`
(so the default run reports it Skipped, and hosted CI never times a shared runner). Body: five private
static `[MethodImpl(MethodImplOptions.NoInlining)]` dummies (`DummyPreTick` calling `DummyWait`,
`DummyOnTick`, `DummyTickComponents`, `DummySpawn`); a simulated frame calls `DummyPreTick`, `DummyOnTick`,
`DummyTickComponents`, `MissionTickProfilerHooks.OnAgentTickEnter()`/`OnAgentTickExit()` and `DummySpawn`
twice. Measure 20,000 frames unpatched (after 2,000 warm-up frames); then
`new Harmony("taom.tests.hitchprobe.bench")` patches each dummy with the matching Patch98 class's real
`Prefix` and `Finalizer` (`new HarmonyMethod(typeof(...).GetMethod("Prefix"))`, priorities as declared);
install a fresh probe-mode profiler and a stub `IAnimationLoadingAdapter` class (not NSubstitute, whose
call cost would swamp the number) with sampling on; measure 20,000 frames again after warm-up; best of
three runs; `UnpatchAll` that id in `finally`. Cost per frame = (patched - unpatched) / frames, in us.
Print it with `Console.WriteLine` and assert `< 50.0`.

Run it with the Benchmark command. **Verify**: 1 passed, and quote the printed us per frame.

Decision:
- **Under 50 us**: nothing changes. Put the number in the feature doc and the commit body.
- **50 us or more** (after one reasonable fix of the hook bodies, never of the assertion): change
  `EnableHitchProbe`'s default to `false`; rename `EnableHitchProbe_CompiledDefault_IsTrue` to
  `EnableHitchProbe_CompiledDefault_IsFalse` and flip its assertion; start its hint with "Off by default:
  the probe measured <x> microseconds per frame against a 50 microsecond target (plan 041)."; mark the
  benchmark test's assertion as the failing measurement in your report (keep the test; it records the
  number), and say why in the feature doc and the commit body. Also flip every "on by default" this
  plan writes: in Step 12's first `SubModule.cs` comment, `Patch98 hitch probe (default on)` becomes
  `Patch98 hitch probe (default off)`; in Step 14, the feature-map row's "and, on by default, the
  Patch98 hitch probe" becomes "and, off by default (its measured cost missed the target), the Patch98
  hitch probe", the registry section says the toggle defaults off, and the feature doc says why. Then
  continue.

Also run the full suite once now: the benchmark shows as Skipped (one more than `<base>`'s Skipped).

### Step 14: docs, full verification and commit

Docs:
- `docs/features/mission-perf-heartbeat.md`: add `## Hitch probe and attribution (Patch98)` after plan
  028's `## Tick profiler (Patch97)` section, covering: the three modes and their toggles (the table in
  Design); what the probe brackets and what each bracket feeds; the probe-mode column definitions and
  why `[HitchDetail]` carries `mode=`; that spawn and script time are parts of other columns; the wait
  bracket and its self-check line; the script thread line and what happens off the main thread; the
  clip-loading sample (when it samples, why the flag belongs to the frame it opens, the 20 us budget);
  the measured cost (the benchmark number and the start-up bookkeeping line) and the 0.5% target; a log
  section listing every new line (the five data lines and every status line) with its fields, when it
  is written and one example (the pinned literals); the PatchShield exclusions and their trade-off;
  what is not covered (view ticks, fixed ticks, filtering to the mission scene). In plan 028's section,
  replace any sentence saying the frame boundary is Patch97's prefix or that turning the profiler off
  installs no patches. Extend Key Files and Tests with the new files.
- `docs/reference/harmony-patch-registry.md`: add `## Patch98_HitchProbe` after the
  `## Patch97_MissionTickProfiler` section and before `<!-- backlinks-start`: the five targets with
  signatures, prefix and finalizer priorities, threads, the install point and modes, the wait self-check,
  the PatchShield entries, MCM toggle default on (or off, per Step 13), status ACTIVE, a pointer to the
  feature doc. In the Patch97 section: the OnPreTick prefix moved to Patch98; the two attribution
  transpilers and their site counts. In the `Patch23_BannerColorPersistence` section, one sentence:
  `Mission.SpawnAgent` is on `PatchShieldPolicy.ExcludedTargetMethods` since plan 041, so PatchShield no
  longer rescues Patch23's prefix or postfix.
- `docs/reference/feature-map.md`, the MissionPerf row: add "and, on by default, the Patch98 hitch probe
  (`[Hitch]` and `[HitchDetail]` per slow frame; `[ScriptProfile]`, `[SpawnProfile]`, `[AnimLoad]` per
  window; `[TickSummaryExtra]` per mission)".
- `docs/features/mcm.md`: before editing, `git grep -n "one such setting" -- docs/features/mcm.md` must
  print one line; if it prints nothing, plan 028 wrapped the phrase across lines: pick a fragment of
  that sentence that sits on one line in your tree, use it here and in the Done criteria in place of
  `one such setting`, and say so in your report. Then the sentence plan 028 added
  ("`BattleLoadDiagnosticsSettings.EnableTickProfiler` is the one such setting today: ...") becomes "`BattleLoadDiagnosticsSettings.EnableTickProfiler` and
  `EnableHitchProbe` are the two such settings today: each is read once per process at the first game
  init, where Patch97 or Patch98 installs or is skipped, so each carries `RequireRestart = true` and an
  allowlist entry."; and the allowlist sentence ("Two are allowlisted by `Class.Property`, ...") names
  three, adding `BattleLoadDiagnosticsSettings.EnableHitchProbe (see below)`.
- Re-read every sentence you wrote against the code it describes.

Then, in order:
1. The RefAsm build, then the RefAsm unit step: quote the unit totals line; no `TAOM.Tests/Features/MissionPerf`
   test may fail. A failure outside this plan's classes that the RefAsm run also shows at `<start>` is
   not yours; say so with the evidence. If the restore cannot download the reference assemblies,
   report "RefAsm not run (environment)" with the error and continue.
2. `dotnet build Main/TAOM.csproj -p:DisableModuleCopy=true -p:ModuleId=`: exit 0.
3. `dotnet test TAOM.Tests -p:DisableModuleCopy=true -p:ModuleId=`: quote the totals line. Expected:
   the same failures as `<base>` (a subset of {`EveryLanguage_DeclaresARowForEveryEnglishKey`}),
   Skipped `<base>` + 1 (the benchmark), Passed `<base>` + 84 (+ 87 with
   `BehaviourTickTableMissionTests`).
4. The binding gate: all pass, none inconclusive; quote its totals.
5. `python tools/validate_moduledata.py`: 0 ERRORs.
6. `python tools/lint_docs.py --fail-on-drift`: exit 0.
7. The Done-criteria greps.
8. `git status --porcelain`: only in-scope paths.
9. Commit as in "Git workflow".

**Verify**: `git log -1 --format=%s` shows the subject; `git status --porcelain` is empty for in-scope
paths.

## Test plan

- New test methods (all under `TAOM.Tests/Features/MissionPerf/` unless named):
  `BattleLoadDiagnosticsSettingsProviderTests` +2; `MissionTickProfilerProbeTests` 14;
  `BehaviourTickTableMissionTests` 3 (only if the mission level is new); `HitchProbeLinesTests` 16;
  `TickProfilerTranspilerOccurrenceTests` 6; `OpenDelegateDispatchTests` 2; `AnimLoadingSamplerTests` 6;
  `ProbeCostMeterTests` 2; `ProbeWindowWriterTests` 5; `HitchProbeHooksTests` 12;
  `HitchProbeBindingTests` 4 (`BindingVerification`; two also `RequiresGameIL`);
  `HitchProbeInstallerTests` 6; `MissionAttributionHooksTests` 5 (`RequiresGame`);
  `HitchProbeWiringTests` 4 (pins); `HitchProbeOverheadBenchmarkTests` 1 (`Benchmark`, Skipped in the
  default run). Total 85 methods (88 with the mission-level file): 84 (87) pass in the default run, 1
  skipped.
- Changed pins: `SettingsFingerprintTests` (`BattleLoadDiagnosticsSettings` count plus 1),
  `SettingRequireRestartPostureTests` (one allowlist entry with its reason), and plan 028's
  `TickProfileLinesTests` only if it asserted the old `OffLine` text.
- Patterns: plan 028's test classes; `PartyIconScaleTranspilerTests` (synthetic IL);
  `TranspilerSiteBindingTests` (real IL); `CreatureBanditsWiringTests` (PatchShield walk);
  `FeatureModuleHooksTests` (`MissionLogic` probe); `AnimaliaWiringTests` (source pins);
  `PatchCategoryApplierTests` (Harmony in a test).
- Cannot be tested offline (the commit's `Not-tested:` trailer, and only these): Harmony applying
  Patch98 and the two attribution transpilers in the game, the in-game numbers,
  `MissionTickProfilerBehavior` (needs a live `Mission`; its window, line and mode logic are tested in
  the pure classes and `ProbeWindowWriter`), `AnimationLoadingAdapter` (one native call; the sampler is
  tested through the interface), and `MissionAttributionHooks.TimedScriptTick` against a live
  `ScriptComponentBehavior` (its dispatch mechanism is proven by `OpenDelegateDispatchTests` and its
  binding by `TickComponentsRewrite_FindsFourParallelForsAndOneOnTick_InInstalledEngine`). Everything
  else has a test that failed first, apart from the four `HitchProbeWiringTests` pins (Step 12), whose
  subjects Steps 9 and 10 built under their own failing tests.

## Done criteria

Machine-checkable. ALL must hold:

- [ ] `dotnet build Main/TAOM.csproj -p:DisableModuleCopy=true -p:ModuleId=` exits 0
- [ ] The full test command reports the same failures as `<base>` (a subset of
      {`EveryLanguage_DeclaresARowForEveryEnglishKey`}), Skipped `<base>` + 1, Passed `<base>` + 84
      (or + 87), every new test class among the executed
- [ ] The Benchmark command reports 1 passed and the commit body quotes its us per frame (or the Step 13
      fallback is applied and explained)
- [ ] The RefAsm unit step shows no failing test in `TAOM.Tests/Features/MissionPerf` (or is reported
      "not run (environment)" with the restore error)
- [ ] The binding gate passes with none inconclusive, `HitchProbeBindingTests` included
- [ ] `git grep -n -E 'Log(Info|Warning|Error)\(\$?"' -- Main/Features/MissionPerf ':!Main/Features/MissionPerf/Hooks/MissionPerfHeartbeatBehavior.cs'`
      returns nothing (every log text comes from `TickProfileLines` or `HitchProbeLines`)
- [ ] `git grep -n "OnFrameBoundary" -- Main/Features/MissionPerf/Hooks/Patch97_MissionTickProfiler.cs`
      returns nothing, and `git grep -n "OnFrameBoundary" -- Main/Features/MissionPerf/Hooks/Patch98_HitchProbe.cs`
      returns one line (the prefix body; no comment in either file names `OnFrameBoundary`)
- [ ] `git grep -n -e "Mission.WaitTickCompletion\"" -e "Mission.SpawnAgent\"" -e "ManagedScriptHolder.TickComponents\"" -- Dependencies/Foundation/PatchShieldPolicy.cs`
      lists the three new entries
- [ ] `git grep -n "## Patch98_HitchProbe" -- docs/reference/harmony-patch-registry.md` returns one line
- [ ] `git grep -n -e "no patches installed" -e "setting a Harmony category is gated on at apply time (read once per process for Patch97)" -e "Every TAOM setting but one" -e "one such setting" -- Main TAOM.Tests docs`
      returns nothing (each fragment sits on one line in plan 028's text, and Steps 2 and 14 saw each
      one hit before the edit, so this check can fail; use the substitute fragment if Step 14 chose one)
- [ ] `git grep -n "Patch98_HitchProbe" -- Main/SubModule.cs` returns nothing (applied only through the
      installer)
- [ ] `git diff <start> -- Main/SubModule.cs` shows only the two comment replacements of Step 12
- [ ] `wc -l Main/Features/MissionPerf/Hooks/*.cs` shows every file under 150 lines (split files only
      under the names Scope lists)
- [ ] `git grep -n -P "[\x{2013}\x{2014}]" -- Main/Features/MissionPerf Main/Adapters/IAnimationLoadingAdapter.cs Main/Adapters/AnimationLoadingAdapter.cs docs/features/mission-perf-heartbeat.md`
      returns nothing
- [ ] `python tools/lint_docs.py --fail-on-drift` exits 0
- [ ] `git status --porcelain` lists only in-scope files; one commit on your branch
- [ ] Every comment, doc line and test oracle this plan supplied was re-checked against the code it
      describes (the literals in Design included)

## STOP conditions

Stop and report (do not improvise) if:

- Plan 028's feature commit is not in `0d1e91f0..HEAD`, or a Step 1 grep finds no hit, or a plan 028 member does
  something other than "Plan 028's code" says (for example `CloseFrame` folds behaviour totals before the
  hitch is built, or the installer has no `_attempted` guard).
- The installed `ManagedScriptHolder.TickComponents` does not hold exactly four
  `call TWParallel::For(int32,int32,float32,ParallelForWithDtAuxPredicate,int32)` and one
  `callvirt ScriptComponentBehavior::OnTick(float32)`, or `Mission.SpawnAgent` does not hold exactly two
  `callvirt MissionBehavior::OnAgentBuild(Agent,Banner)` (the Step 11 binding tests fail, or a count is
  not as stated), or a signature in Current state differs. Report the mismatch; do not adjust the counts.
- The always-on part would need a transpiler, or any patch other than a whole-method prefix and
  finalizer pair, to work.
- `OpenDelegate_ProtectedInternalVirtual_CallsTheOverride` fails (the open delegate calls the base
  method): the script attribution design is wrong.
- The thread that calls `TickComponents` cannot be determined at run time by the design here (for
  example you find it is called for one holder from several threads at once, so a `[ThreadStatic]`
  bracket and `Interlocked` totals cannot describe it).
- The wiring needs an `IoC.cs` line, a `FeatureModules` entry, or a `SubModule.cs` change beyond the two
  comment replacements.
- `Patch98_` or `Patch99_` is already taken when you start: report it; do not renumber.
- After Step 2 the `BattleLoadDiagnosticsSettings` reflected count did not move by exactly one, or the
  simulation-relevant count moved from 217.
- A test outside this plan's new classes fails that did not fail in Step 1 (plan 028's own tests
  included, apart from the `OffLine` expectation named in Step 4).
- The benchmark cannot be made to run (Harmony cannot patch the dummies in the test host). Report it;
  do not ship the default-on toggle without a measured number.
- A step's verification fails twice after a reasonable fix.

## Orchestrator steps (not the executor's)

- Re-anchor this plan on plan 028's reviewed tip before dispatch: compare "Plan 028's code" with the
  merged code and update any member name or signature that changed in review.
- Issue: file it before dispatch (title along the lines of "Hitch probe on by default: spawn, script
  component and clip-loading attribution for slow battle frames"), and name it in the commit body if it
  exists by then.
- No `/localize`: the Battle Load Diagnostics MCM page is not localised (no `{=KEY}` on its strings), and
  log lines are not player-facing text.
- Confirm plan 029's generic-tag collection (its amendment item 1) picks up `[HitchDetail]`,
  `[SpawnProfile]`, `[ScriptProfile]`, `[AnimLoad]` and `[TickSummaryExtra]` without a parser change,
  and that a default run (`tickProfiler=off`, the seven default `diag=` tokens) raises no `DIAG_ON`.
- If plans 036 and 041 both add `Main/Adapters/IAnimationLoadingAdapter.cs` and
  `AnimationLoadingAdapter.cs`, the second merge has an add/add conflict on those two paths: keep the
  first-merged branch's files (same members, from the same specification) and confirm the build.
- After review, `/verify-bindings` to refresh `docs/reference/taleworlds-api-snapshot/patch-targets.md`
  (five Patch98 classes, two Patch97 classes).
- The feature doc and its feature-map row are done by the executor in Step 14; check them.

## After merge: the maintainer's actions

- Pull and build. In-game check (FOR-MIKE), default settings, one Custom Battle with elephants or
  mumakil and one campaign field battle: at game start
  `[TickProfiler] off: 'Enable Tick Profiler' is off at game start (or MCM was not ready); no per-behaviour transpilers installed`
  and a `[TickProfiler] probe install: category applied, enabled by hitch probe, ...` line with a
  bookkeeping figure; per mission `[PerfContext]` with `tickProfiler=off`, the `[TickProfiler] mission <n>: mode probe, ...`
  line, and on the first mission the `anim-loading sample:` line; every 5 s `[TickProfile]` (top=none),
  `[ScriptProfile]`, `[AnimLoad]` and, in the spawn wave, `[SpawnProfile]`; a `[HitchDetail]` right after
  every `[Hitch]`; `[TickSummary]` and `[TickSummaryExtra]` at mission end. Check that the
  `WaitTickCompletion's bracket did not run` line is ABSENT (if present, the default probe cannot time
  the wait; report it) and note which script-thread line appears. Then the same battle with "Enable Tick
  Profiler" on (restart): the `attribution install` line with `sites 2/2` and `5/5`, and `top=` lists
  filled. Then "Enable Hitch Probe" off and the profiler off (restart): the `probe off:` line and no
  `[Hitch]`. Compare `[MissionPerf]` `avgMs` probe on against both off: within run-to-run noise. Close
  the issue with `triage-needs-ingame` until that check is done.

## Maintenance notes

- The five new data formats are read by plan 029's generic-tag collection; any change to them goes with
  a change to their pinned literals and the feature doc. Status lines stay on `[TickProfiler]` and never
  quote a data tag.
- Plan 034 (PatchShield per-call cost) may rewrite `ExcludedTargetMethods`: keep the three entries here
  and the binding test that walks them.
- Plan 030 deletes `Patch35_Mission_OnTick`; the probe's `OnTick` bracket composes with or without it.
- Plans that add MCM settings move `SettingsFingerprintTests` and the two co-op docs' counts; the second
  to merge recomputes.
- An engine bump that moves `TickComponents` or `SpawnAgent` call sites fails `HitchProbeBindingTests`
  (the binding gate); at run time the transpiler leaves vanilla IL and the `attribution install` line
  reports the found counts. The probe brackets do not depend on call sites.
- Review probes: the probe-mode column derivation in `CloseFrame` (oracle in Step 3); that plan 028's
  `Timed` helpers record nothing without `BehaviourTiming`; the `[ThreadStatic]` script bracket and the
  `Interlocked` frame fields; the spawn depth counter when `SpawnAgent` throws (the finalizer must still
  close the bracket); the wait self-check and `WaitSwapActive` (no double counting in `full` mode, nor
  when Patch97 applied with `OnTickSites != 2` but the wait swap live); the `ResetForTests` members
  (test-only; never called from production code); that
  no hook can throw into the engine; that the clip sample happens only after its cost was measured; the
  `HarmonyPriority` on every Patch98 method; that `[HitchDetail]` follows `[Hitch]` with the same `t`.
- Volume: a battle that runs below 4 fps makes every frame a hitch, so two INFO lines per frame; the
  hitch threshold is the control, as in plan 028. If a player log shows this, a per-mission cap with a
  count of suppressed lines (D6 rule 4) is the follow-up, not a lower log level.
- UNVERIFIED, decided at run time by the lines this plan adds: whether the JIT inlines
  `WaitTickCompletion` into the patched `OnPreTick` (the self-check line), which thread ticks script
  components (the thread line), and the native cost of `IsAnyAnimationLoadingFromDisk` (the sample
  line). Also unverified: whether the native clip walk is safe to call while the agent tick's workers
  run (it only reads states; plan 036 makes the same call).
- Deferred, with reasons: filtering `TickComponents` to the mission scene's holder (no managed handle
  from the scene to its holder was found; `calls=` shows when other scenes tick); timing
  `FixedTickComponents` and `OnFixedMissionTick` (not in the hitch path the logs point at); per-type
  attribution of the parallel script blocks (they run on workers; the block totals are kept).
