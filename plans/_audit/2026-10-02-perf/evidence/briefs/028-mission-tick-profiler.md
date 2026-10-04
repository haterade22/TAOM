Plan 028: attribute mission frame time and main-thread allocation to each mission behaviour, time the
engine phases managed code can see, and log a breakdown of every hitch frame. A new default-off
instrument, the measurement every other plan in this run is judged by.

WHY. TAOM's only in-mission record is `[MissionPerf]` (Main/Features/MissionPerf/: FrameStats.cs,
MissionPerfLine.cs, Hooks/MissionPerfHeartbeatBehavior.cs, registered at Main/SubModule.cs:2112): whole
frames only (fps, avg/p95/max ms, agents, GC counts per 5 s). It cannot say how many ms per second any
behaviour costs, and the recurring 0.6 to 1.1 s single-frame hitches in the 2026-10-02 logs (gc2=0, with
and without trolls) are unattributed. yotthani's DualWield measured its own share with a per-subsystem
Stopwatch table (yotthani/bannerlord HoN/DualWield/Core/DwPerf.cs, comparison only, no licence); TAOM
needs the same, engine-wide, with no code taken from it.

VERIFIED ENGINE FACTS (v1.5.3; the managed decompile is
E:/Decompiled_Bannerlord/_categories_v1.5.3/MountAndBlade/TaleWorlds.MountAndBlade/TaleWorlds.MountAndBlade/Mission.cs,
re-check every line with `pwsh tools/taom-src.ps1 path TaleWorlds.MountAndBlade.Mission`):
- `public void OnTick(float dt, float realDt, bool updateCamera, bool doAsyncAITick)` (line ~3652):
  reverse loop `MissionBehaviors[num].OnPreDisplayMissionTick(dt)` (~3748-3751); camera;
  `tickCompleted = false`; reverse loop `MissionBehaviors[num2].OnMissionTick(dt)` (~3757-3760); dynamic
  entities; HandleSpawnedItems; then `TickAgentsAndTeamsAsync(dt)` (posts a native job,
  IMBMission.TickAgentsAndTeamsAsync, RVA 0x6F6290) or `TickAgentsAndTeamsImp(dt, false)` (~3784-3791).
  So every ms spent in OnMissionTick delays the start of the parallel agent tick.
- `[MBCallback] internal void OnPreTick(float dt)` (~3546): `WaitTickCompletion()` then reverse loop
  `OnPreMissionTick(dt)` then TickDebugAgents. `private void WaitTickCompletion()` spins
  `while (!tickCompleted) Thread.Sleep(1);` (~3591-3597): the main thread waiting for the previous
  frame's async agent tick.
- `public void TickAgentsAndTeamsImp(float dt, bool tickPaused)` (~3617): `TWParallel.For` over
  AllAgents running `Agent.TickParallel` on workers, then a serial `Agent.Tick` loop, a serial `Team.Tick`
  loop, `tickCompleted = true`, then each submodule's `AfterAsyncTickTick(dt)`. It runs on the async AI
  thread (harmony-patches.md thread table). Patch91 already brackets it and the main-thread
  `MissionState.TickMissionAux` with a prefix plus finalizer
  (Main/Features/BattleLoadDiagnostics/Hooks/Patch91_MissionTickStallProbes.cs, category
  Patch91_MissionTickStall): reuse those brackets or add timing beside them, do not duplicate them.
- The native engine calls timeBeginPeriod(1) once at start-up (TaleWorlds.Native.dll RVA 0x27DAC, in the
  init function called by WotsMain*), so Sleep(1) in WaitTickCompletion has about 1 to 2 ms granularity.
- Runtime: the game runs on the .NET Framework desktop CLR (Bannerlord.exe is a managed exe; TAOM's VS
  launch profile uses the managed-framework debug engine; `[MissionDiag]` logs CLR 4.0.30319.42000; the
  `[MissionPerf]` gc1>0 with gc2=0 pattern is three-generation). This machine's .NET Framework 4.8.1
  mscorlib has `GC.GetAllocatedBytesForCurrentThread()`; TAOM targets net472 reference assemblies, so
  bind it once by reflection into a Func<long> and fall back to "na" when the method is absent.

DESIGN (decided by the orchestrator; the writer refines details against the code):
1. Per-behaviour timing: a Harmony transpiler on Mission.OnTick and Mission.OnPreTick that replaces each
   `callvirt MissionBehavior::OnPreDisplayMissionTick / OnMissionTick / OnPreMissionTick` with a call to a
   static helper taking (MissionBehavior, float). The helper times the call with Stopwatch.GetTimestamp
   and the allocation counter and accumulates per behaviour System.Type into preallocated structures (no
   allocation per call after a type's first sighting). The rewrite itself is a pure function over
   IEnumerable<CodeInstruction>, unit-tested with synthetic instruction lists; a BindingVerification test
   asserts the installed Mission.OnTick and OnPreTick contain exactly the expected call sites, so an engine
   update that moves them fails the binding gate instead of silently profiling nothing.
   If MissionScreen's view tick loop (OnMissionScreenTick, TaleWorlds.MountAndBlade.View) is the same
   simple loop shape, cover it the same way; otherwise leave it out and say so.
2. Phase timing per frame: OnPreDisplayMissionTick total, OnMissionTick total, OnPreMissionTick total,
   WaitTickCompletion wait (prefix and finalizer on the private method via AccessTools), and
   TickAgentsAndTeamsImp wall time (async thread: single writer per frame, published with
   Volatile/Interlocked and read by the main thread at the window boundary). Frame wall time minus the
   measured managed phases is reported as otherMs (render, native scene, physics, streaming, shader
   compiles).
3. Lines, every 5 s of wall clock aligned with [MissionPerf] (share its window or its clock), and per
   hitch frame. Contract shared with plan 029's parser, pinned by literal tests on both sides:
   `[TickProfile] t=+<s>s frames=<n> wallMs=<x> preDisplayMs=<x> missionTickMs=<x> preTickMs=<x> waitTickMs=<x> agentTickMs=<x> otherMs=<x> allocKB=<x|na> top=<Type>:<ms>/<calls>/<maxMs>/<KB>,<Type>:...`
   `[Hitch] t=+<s>s frameMs=<x> preDisplayMs=<x> missionTickMs=<x> preTickMs=<x> waitTickMs=<x> agentTickMs=<x> otherMs=<x> gc0=<n> gc1=<n> gc2=<n> allocKB=<x|na> top=<Type>:<ms>,<Type>:<ms>,<Type>:<ms>`
   `[PerfContext] build=<Debug|Release> jitOptimized=<true|false> clr=<version> serverGC=<bool> latency=<GCLatencyMode> missionInProcess=<n> scene=<id> agents=<n> textureQuality=<n|na> shadowQuality=<n|na> particleDetail=<n|na> ragdolls=<n|na> memLoad=<pct|na> availPhysMB=<n|na> tickProfiler=<on|off> diag=<comma list or none>`
   Numbers invariant culture, ms with 2 decimals, KB integers; Type is the short type name. top= lists the
   top N behaviours by total ms in the window (N from MCM, default 8). [PerfContext] is written once per
   mission at the first tick, also when the profiler is off (it is cheap and every A/B needs it). Build
   config: `DebuggableAttribute.IsJITOptimizerDisabled` of TAOM's own assembly. Graphics options: reuse
   an existing adapter (the #701 BattleCorpses work added graphics-option reads and a `[BattleSettings]`
   line; find and reuse, do not duplicate); memLoad and availPhysMB: reuse the existing memory reader
   behind [MemSample] (Main/Features/BattleLoadDiagnostics/MemorySampleReader.cs).
4. Settings: new MCM entries in the Battle Load Diagnostics page (Main/Features/BattleLoadDiagnostics/
   BattleLoadDiagnosticsSettings.cs): EnableTickProfiler (default false; it installs patches, so it takes
   effect at the next game start and says so), TickProfilerTopN (default 8), HitchThresholdMs (default
   250). New names, so the persisted-MCM-default trap (orientation.md) does not apply. Instrumentation
   only: exclude them from the co-op settings fingerprint like EnableMissionPerfHeartbeat (find how).
5. Cost discipline: when the profiler is off its patches are not applied at all (players pay nothing).
   When on: two timestamps and two counter reads per behaviour call, no allocation after warm-up, no
   locks on the main thread, no logging per frame except [Hitch].
6. Architecture: pure window, accumulator, hitch and line logic in Main/Features/MissionPerf/ (unit
   tested without the engine); hooks thin under Main/Features/MissionPerf/Hooks/ (ADR-002, under 150
   lines each); no #if DEBUG (ADR-005); nothing takes a sealed TaleWorlds type into a service (ADR-007).
   Read docs/reviews/lessons/harmony-il.md and .claude/rules/harmony-patches.md before writing the
   patches (mandatory per that rule), and the registry entry for Patch91.
7. Registration: Main/SubModule.cs and Main/IoC.cs are single-owner. The plan lists the exact edits: where
   the new category is applied (it must apply when MCM's settings instance is readable; check how other
   diagnostics read their toggles at apply time, for example Patch89 and Patch61, and pick the apply point
   that sees the persisted toggle), and how the behaviour is added (next to MissionPerfHeartbeatBehavior at
   SubModule.cs:2112).
8. Docs: extend docs/features/mission-perf-heartbeat.md (or a new docs/features/mission-tick-profiler.md
   with a feature-map row; the writer chooses and says why), add a docs/reference/harmony-patch-registry.md
   section for the new category, and fix the stale line mission-perf-heartbeat.md:39 ("Read once per
   tick": the code reads the toggle once per second, MissionPerfHeartbeatBehavior.cs:24-26, :54-58).
9. Not testable offline: the live patch application and the in-game numbers. The maintainer runs the
   in-game check (FOR-MIKE).

STOP conditions to include: the call sites in Mission.OnTick or OnPreTick differ from the facts above;
the transpiler would need to change control flow (anything beyond replacing a callvirt with a call);
a TAOM-wide registration change beyond the listed SubModule/IoC edits; MCM settings not readable at any
apply point that precedes the first mission.
