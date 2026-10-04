Plan 041: extend plan 028's mission profiler with the three missing attributions (spawn work, scene
script components, on-demand clip loading) and make the cheap hitch probe ON by default for every player,
so a player's own taom_debug.log already explains a battle hitch. Built on the tip of plan 028's branch
(perf/028-mission-tick-profiler) after its review; read that plan and its feature doc first.

WHY. Plan 028 times mission-behaviour ticks and the engine phases managed code sees, but three costs it
does not attribute matter most for the unexplained hitches (REPORT.md "The battle hitches"):
1. **Spawn work.** The worst recorded spikes (maxMs up to 1,663 ms at 1,313 agents) sit in the spawn
   wave. v1.5.3 `Mission.SpawnAgent` dispatches each behaviour's `OnAgentBuild` in two `foreach` loops
   (`Mission.cs` about lines 4383 and 4397: `missionBehavior2.OnAgentBuild(agent2, null)` and
   `missionBehavior3.OnAgentBuild(agent, agentBuildData.AgentBanner ?? agentBuildData.AgentTeam?.Banner)`),
   on the main thread. About 13 TAOM behaviours override it.
2. **Scene script components.** TAOM's howdah, seat and war-tower components (`TaomHowdahMachine`,
   `TaomHowdahStandingPoint`, `TaomMumakilPlatform`, `TaomMumakilStandingPoint`) and vanilla's siege
   machines tick in `TaleWorlds.Engine.ManagedScriptHolder.TickComponents(float dt)`, an
   `[EngineCallback]` outside `Mission.OnTick`: three `TWParallel.For` blocks (`OnTickParallel`,
   `OnTickParallel2`, `OnTickParallel3`), a serial
   `foreach (ScriptComponentBehavior scriptComponent in _toTick.ScriptComponents) scriptComponent.OnTick(dt);`,
   then a `TWParallel.For` over a tenth of the occasional-tick components (v1.5.3 `ManagedScriptHolder.cs`).
   Plan 028 counts all of it in `otherMs`.
3. **On-demand clip loading.** TAOM verified natively (Ghidra, 2026-10-02; the engine reference page
   section 6) that clips at Loading Type 1 or 2 block a sampling worker of the parallel agent tick on first
   use and after eviction past a 12 MiB budget. `MBAnimation.IsAnyAnimationLoadingFromDisk()` (native
   `0x6EAAE0`) is true while any on-demand clip is loading.
WHAT:
- Time each behaviour's `OnAgentBuild` with plan 028's call-site rewrite (per type, main thread); time the
  serial script `OnTick` calls per component type with the same rewrite on
  `ManagedScriptHolder.TickComponents`, and the parallel and occasional blocks as totals (verify the
  calling thread; accumulate thread-safely if it is not the main thread); sample
  `IsAnyAnimationLoadingFromDisk()` once per frame through an adapter (ADR-007) and measure its cost once.
- New lines, so plan 028's pinned formats stay untouched: `[SpawnProfile] t=+<s>s spawns=<n> spawnMs=<x>
  top=<Type>:<ms>/<calls>,...` per window when spawns happened; `[ScriptProfile] t=+<s>s scriptTickMs=<x>
  scriptParallelMs=<x> occasionalMs=<x> top=<Type>:<ms>/<calls>/<maxMs>,...` per window; and a
  `[HitchDetail] t=+<s>s spawnMs=<x> scriptTickMs=<x> scriptParallelMs=<x> animLoading=<0|1>` line written
  right after each `[Hitch]`; and the clip-loading count on its own line,
  `[AnimLoad] t=+<s>s loadingFrames=<n> frames=<n>`, per window. None of these tags may contain
  `[TickProfile]`, `[Hitch]`, `[PerfContext]` or `[MissionPerf]` as a substring (`[HitchDetail]` does not
  contain `[Hitch]` because `D` follows `h`; check each with the same test 028 uses). Mission-end: extend
  plan 028's `[TickSummary]` with a second line `[TickSummaryExtra] spawnMs=<x> scriptTickMs=<x>
  animLoadingFrames=<n> hitchesWithAnimLoading=<n>`. Literal pins for each.
- **On by default, split by risk (DECISIONS D7).** A new MCM setting `EnableHitchProbe` (default true):
  whole-method prefix and finalizer pairs only, no transpilers, on `Mission.OnTick`, `Mission.OnPreTick`
  (with plan 028's `WaitTickCompletion` split), `Mission.TickAgentsAndTeamsImp` (beside Patch91),
  `ManagedScriptHolder.TickComponents` and `Mission.SpawnAgent`, plus the clip-loading sample: they feed
  the phase totals, `[Hitch]` (phase columns; behaviour `top=` reads `none` when the transpilers are not
  installed) and `[HitchDetail]`. The per-behaviour, per-spawn-behaviour and per-component attribution
  stays behind `EnableTickProfiler` (default false, restart). Measure the always-on part's overhead with a
  unit benchmark (target under 0.5% of a 10 ms frame), log it in the `[TickProfiler] install` line, and
  default `EnableHitchProbe` false instead if the target cannot be met, saying why.
LOGGING (D6): configuration header lines for each part, reason lines on every skip or failure, mission-end
summaries, a feature-doc log section listing every line.
TESTS: as plan 028's patterns: the rewrite helpers on synthetic IL, binding tests for the new call sites,
accumulators, line pins, the always-on overhead benchmark (category excluded from the default run).
STOP conditions to include: `TickComponents` or `SpawnAgent` call sites differ from the shapes above; the
thread calling `TickComponents` is not determinable; the always-on part cannot avoid a transpiler.
