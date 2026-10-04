# Mission Perf Heartbeat

## Overview

One `[MissionPerf]` line in the TAOM debug log every five seconds of wall clock while a mission
ticks: frames, fps, average / p95 / max frame time in milliseconds, agent and formation counts,
garbage collections since the last line. It is the in-mission counterpart of `[MemSample]`
and the measurement a battle-AI or content change is judged against. Written for the culture
doctrine A/B (#608) and kept for every mission.

## Why This Exists

- **Vanilla behavior:** no frame-time record. `[MemSample]` (Battle Load Diagnostics) has no
  frame counter; `[MapLoad]` reports fps on the campaign map only.
- **TAOM requirement:** an A/B of a team-AI change needs frame times from the same battle with
  the feature on and off, in a form a script can compare.
- **Without this feature:** "it felt fine" is the only evidence a perf change leaves.

## Architecture

`MissionPerfHeartbeatBehavior : MissionLogic` measures wall clock between consecutive
`OnMissionTick` calls (`Stopwatch.GetTimestamp`, no allocation), feeds `FrameStats`, and every
five seconds logs `MissionPerfLine.Build(...)` with `Mission.AllAgents.Count`,
`Mission.Agents.Count` (active), the count of formations with units, and the `GC.CollectionCount`
deltas. Wall clock, not mission time: mission time slows under time scaling and stops in the
order menu, and the line is about what the player feels.

`FrameStats` is pure: count, average and max cover every frame; the percentile set is bounded
to the newest 4,096 samples (nearest-rank p95 over a sorted copy once per window). It resets
its clock at `OnCreated` and `OnEndMission`. The behavior self-disables for the mission after a
single exception.

Cost: one timestamp per frame, one sort of at most 4,096 doubles per five seconds.

## Configuration

Battle Load Diagnostics MCM page, `Mission Performance` group: `EnableMissionPerfHeartbeat`,
default on. Read from the MCM instance at most once a second (`ToggleRefreshSeconds`);
instrumentation only, so it is excluded
from the co-op settings fingerprint. The same group holds `EnableAnimMemoryProbe` for the clip memory
probe below, also default on and read at each mission start.

## Log line

```
[MissionPerf] t=+65s frames=300 fps=60.0 avgMs=16.67 p95Ms=25.50 maxMs=40.3 agents=812 active=640 formations=9 gc0=12 gc1=3 gc2=1
```

`t` is wall seconds since the behavior's `OnCreated`. The first window opens at the first
`OnMissionTick`, so the first line lands one interval after that tick: `t=+6s` in the custom and
campaign battle logs of 2026-09-29 to 2026-10-02, `t=+7s` in a tournament.

## Tick profiler (Patch97)

Off by default. When on, it says where a battle's frame time goes: milliseconds and main-thread
allocation per mission behaviour type, the engine phases managed code can see, and one line per
slow frame naming where that frame went. It is the number the rest of the 2026-10 performance
plans are judged by.

### What it measures

A frame is one mission tick, from one `Mission.OnPreTick` to the next (in fast-forward the engine
runs several mission ticks per rendered frame, so several profiler frames). Per frame (these are the
`full`-mode definitions; in the default `probe` mode the columns come from the Patch98 brackets, see
"Hitch probe and attribution (Patch98)" below):

| Field | What it is |
|---|---|
| `preDisplayMs` | Sum of every behaviour's `OnPreDisplayMissionTick` (timed at its call site in `Mission.OnTick`) |
| `missionTickMs` | Sum of every behaviour's `OnMissionTick` (same loop shape, also in `Mission.OnTick`); every millisecond here delays the start of the parallel agent tick |
| `preTickMs` | Sum of every behaviour's `OnPreMissionTick` (in `Mission.OnPreTick`) |
| `waitTickMs` | `Mission.WaitTickCompletion()`: the main thread waiting for the previous frame's agent tick (`Thread.Sleep(1)` loop, about 1 to 2 ms granularity) |
| `agentTickMs` | Wall time of every `Mission.TickAgentsAndTeamsImp` that finished since the previous boundary, read from Patch91's bracket. It includes `AfterAsyncTickTick`, which runs after `tickCompleted` is set, so it can overlap the next frame |
| `otherMs` | `frameMs` minus the three behaviour sums, the wait, and the agent tick only when it ran ON the main thread (fast-forward or a synchronous AI tick); clamped at 0. An agent tick on the asynchronous thread overlaps main-thread work and is not subtracted |
| `allocKB` | Main-thread allocation between boundaries (`GC.GetAllocatedBytesForCurrentThread`, bound by reflection; `na` when absent). The counter is per thread, so agent-tick allocation on the AI thread and the workers is not in it |

`otherMs` is everything managed code does not see from these hooks: the native `Mission.Tick`
(physics, scene) and the managed callbacks it raises on the main thread (agent hit, missile, agent
removed, and every behaviour's handlers for them, TAOM's included), the parts of `Mission.OnTick`
outside the two behaviour loops (tick actions, the mission-end check, the camera, spawned items),
views and UI (`OnMissionScreenTick`), rendering, streaming, shader compiles, the PatchShield
finalizers on the frame's shielded methods (see PatchShield below),
plus the profiler's own bookkeeping, including the write of the previous frame's `[Hitch]` line. Not covered: view ticks (`MissionScreen.OnFrameTick` ticks
them through a closure, not a simple loop), `OnFixedMissionTick`, per-behaviour time inside the
agent tick, and agent-tick allocation.

Behaviour calls are recorded into the open frame and folded into the window only when the frame
closes, so every window total covers the same closed frames: `wallMs` is the sum of those frames'
`frameMs`, and the calls of the frame still open when a window is taken land in the next window.
The first boundary of a mission only stamps the clock; what ran before it is discarded.

The profiler's first window opens at that first boundary, three mission ticks before the
heartbeat's 5 s clock starts (the engine first calls `OnMissionTick` on the fourth tick), so the
first `[TickProfile]` line carries a little more than 5 s and the slow first frames after loading.

A frame spans any time the mission is not ticked. An inventory, party or encyclopedia screen opened
over a town mission stops `OnPreTick` (the state stack ticks only the active state, and the
encyclopedia's disable request routes the mission to the native idle tick, which most likely raises
no `OnPreTick`), so closing it produces one `[Hitch]` made almost entirely of `otherMs`, and
`[TickSummary]`'s worst hitch may be that menu visit. Read a settlement mission's worst hitch with
that in mind.

### Settings

Battle Load Diagnostics MCM page, `Mission Performance` group:

| Setting | Default | Read |
|---|---|---|
| `Enable Tick Profiler` (`EnableTickProfiler`) | off | At the first game init, where Patch97 installs or is skipped, and again at each mission start. Turning it on needs a restart (`RequireRestart = true`, allowlisted in `SettingRequireRestartPostureTests`); turning it off stops behaviour timing from the next mission, while the patches stay installed and call through until a restart |
| `Tick Profiler Top Behaviours` (`TickProfilerTopN`) | 8 (1 to 20) | At each mission start |
| `Hitch Threshold (ms)` (`HitchThresholdMs`) | 250 (50 to 2000) | At each mission start |

The provider validates both numbers (an out-of-range or non-finite value falls back to the
default, and a measured mission names each replaced value in one warning) and fails CLOSED to off when MCM is not ready, unlike its siblings, because the toggle
installs patches. All three are instrumentation and excluded from the co-op settings fingerprint.
Behaviour timing needs the profiler installed, its toggle on at that mission's start AND every hook the profiler
needs still in place (see Hook health); with it off, a mission still measures in `probe` mode when the hitch probe
is on (see Patch98 below).

### Log lines

Data lines are a contract with plan 029's log parser (pinned literally in
`TickProfileLinesTests`): invariant culture, ms with two decimals, KB as whole bytes / 1024, `t`
as whole seconds since the mission was created (the same clock as `[MissionPerf]`, so the two
lines of a window usually share `t`; each behaviour stamps its own clock, so a boundary can
occasionally fall between them and the two then differ by a frame), booleans lowercase, `Type` is
`Type.Name`, `na` for an unavailable allocation counter, `top=none` when no behaviour ran.

**`[TickProfile]`**, INFO, every 5 s while measuring. Window totals, then the top N behaviours by
total ms as `<Type>:<ms>/<calls>/<maxMs>/<KB>`:

```
[TickProfile] t=+65s frames=300 wallMs=5000.00 preDisplayMs=12.50 missionTickMs=812.40 preTickMs=40.10 waitTickMs=95.00 agentTickMs=1500.00 otherMs=4040.00 allocKB=2048 top=BehaviorTreeMissionLogic:410.20/300/3.10/512,AdvancedCombatBehavior:120.00/900/1.50/64
```

**`[Hitch]`**, INFO, one per frame at or above the hitch threshold, from the frame boundary, for the
first 100 such frames of a mission (`MissionTickProfiler.MaxHitchLinesPerMission`). Past that, a
battle that stays slow would turn it into a per-frame INFO flush, so later hitch frames are only
counted: `[TickSummary]`'s `hitches=` counts every one and `worstHitchMs` keeps the worst, and one
`[TickProfiler] hitch line cap reached` line marks where the full lines stop. The frame's phases,
its GC collection deltas, its main-thread allocation and its three slowest behaviours as
`<Type>:<ms>`:

```
[Hitch] t=+72s frameMs=812.35 preDisplayMs=0.40 missionTickMs=5.20 preTickMs=0.30 waitTickMs=790.00 agentTickMs=795.10 otherMs=16.45 gc0=1 gc1=1 gc2=0 allocKB=96 top=BehaviorTreeMissionLogic:2.10,AdvancedCombatBehavior:1.30,MissionPerfHeartbeatBehavior:0.05
```

**`[PerfContext]`**, INFO, once per mission at its first tick, profiler on or off. `build` is
`Debug` when TAOM's own `DebuggableAttribute.IsJITOptimizerDisabled` is true; `missionInProcess`
counts missions this process; `scene` is `unknown` when unreadable and `agents` -1; the quality
fields are the raw engine option values (`na` when unreadable); `memLoad` and `availPhysMB` come
from `GlobalMemoryStatusEx`; `diag` lists, in this order, `battleLoad`, `stallWatchdog`,
`stallBundle`, `exitSampler`, `freezeSampler`, `memSampler`, `missionPerf` for each diagnostic that
will run, or `none`. `stallWatchdog` and `exitSampler` also need the master Battle Load Diagnostics
toggle, and `stallBundle` needs the watchdog, as their consumers do; the other three stand alone:

```
[PerfContext] build=Debug jitOptimized=false clr=4.0.30319.42000 serverGC=false latency=Interactive missionInProcess=1 scene=battle_terrain_029 agents=0 textureQuality=1 shadowQuality=2 particleDetail=1 ragdolls=3 memLoad=61 availPhysMB=12034 tickProfiler=on diag=battleLoad,stallWatchdog,stallBundle,exitSampler,freezeSampler,memSampler,missionPerf
```

**`[TickSummary]`**, INFO, once at the end of a measured mission that closed any frame, in `probe` or
`full` mode (never when nothing measures; a measured mission that closed none writes a `[TickProfiler]`
line saying so). Every closed frame of the mission (the sum of its windows, including the
last partial one), the hitch count, the worst hitch and its `t` (`na` with no hitch), and the top N
behaviours over the whole mission:

```
[TickSummary] frames=4500 wallMs=75000.00 preDisplayMs=187.50 missionTickMs=12186.00 preTickMs=601.50 waitTickMs=1425.00 agentTickMs=22500.00 otherMs=60600.00 allocKB=30720 hitches=3 worstHitchMs=1104.20 worstHitchT=+72s top=BehaviorTreeMissionLogic:6153.00/4500/3.10/7680,AdvancedCombatBehavior:1800.00/13500/1.50/960
```

The literal is pinned by `TickProfileLinesTests.BuildTickSummary_SampleMission_MatchesThePinnedLiteral`;
its inputs are that class's `SampleSummary` (4,500 frames, 3 hitches, the worst 1104.2 ms at 72.4 s,
allocation 31,457,280 bytes, two behaviours).

**Status lines** carry `[TickProfiler]`, which does not contain the substring `[TickProfile]`;
no status line holds a data tag (an exception message is quoted with its square brackets turned
into parentheses), so plan 029's parser never counts one as a malformed data line:

| Line | Level | When |
|---|---|---|
| `[TickProfiler] off: 'Enable Tick Profiler' is off at game start (or MCM was not ready); no per-behaviour transpilers installed` | INFO | Once per process, toggle off. Since plan 041 it names what is not installed (Patch97's transpilers); whether Patch98 installed is the `probe install` or `probe off` line right after it |
| `[TickProfiler] install: category applied, Mission.OnTick sites 2/2, Mission.OnPreTick sites 2/2, allocation counter available` | INFO | Once per process, toggle on: `applied` or `failed`, the swaps made per method (the `OnPreTick` denominator is 1 when the wait delegate did not bind), `available` or `na` |
| `[TickProfiler] WaitTickCompletion could not be bound; waitTickMs reads 0 and the wait lands in otherMs` | WARNING | At install, when the private method did not bind |
| `[TickProfiler] Mission.OnTick: MissionBehavior.OnMissionTick matched 0 times, expected 1; Mission.OnTick left vanilla, so no mission times behaviours by type (with 'Enable Hitch Probe' on, missions still measure in probe mode)` | WARNING | At install, or whenever a later patch on the method reruns the transpilers (another mod's patch, an earlier transpiler that took the anchor): a call site not found as many times as its swap has helpers (an engine bump). For `Mission.OnTick` the next mission start does not time behaviours, and a mission already running stops on its next tick (see Hook health). For `Mission.OnPreTick` the consequence reads `so preTickMs reads 0 and that time lands in otherMs (the hitch probe still times the wait)`: Patch98's prefix holds the frame boundary, so measuring continues; for the attribution rewrites (`Mission.SpawnAgent`, `ManagedScriptHolder.TickComponents`) it reads `so per-type attribution records nothing for it (the hitch probe's totals stay)` |
| `[TickProfiler] Mission.OnTick: helper MissionTickProfilerHooks.TimedMissionTick does not fit MissionBehavior.OnMissionTick; Mission.OnTick left vanilla, so no mission times behaviours by type (with 'Enable Hitch Probe' on, missions still measure in probe mode)` | WARNING | At install, a helper whose shape does not match its target (same consequences) |
| `[TickProfiler] Mission.OnTick rewrite failed: InvalidOperationException: ...; Mission.OnTick left vanilla, so no mission times behaviours by type (with 'Enable Hitch Probe' on, missions still measure in probe mode)` | WARNING | At install, a transpiler that threw (same consequences) |
| `[TickProfiler] on in MCM but it was off at game start, so its patches are not installed and nothing is measured; restart the game to measure` | WARNING | Once per mission, toggle on but never installed (switched on after game start), and the hitch probe not measuring the mission |
| `[TickProfiler] on in MCM but it was off at game start, so per-type timing is not installed; the hitch probe still measures this mission; restart the game for per-type timing` | WARNING | The same, while the hitch probe measures the mission (the default) |
| `[TickProfiler] on in MCM but the install at game start failed, so nothing is measured; see the [TickProfiler] install line and [PatchApply]` | WARNING | Once per mission, toggle on but the install failed, and the hitch probe not measuring the mission |
| `[TickProfiler] on in MCM but its install at game start failed, so per-type timing is off; the hitch probe still measures this mission; see the [TickProfiler] install line and [PatchApply]` | WARNING | The same, while the hitch probe measures the mission |
| `[TickProfiler] mission 3: not measuring, 'Enable Tick Profiler' is off in MCM; its patches stay installed and only call through until a restart` | INFO | Once per mission, installed but switched off since game start, and the hitch probe not measuring the mission |
| `[TickProfiler] mission 3: no per-type timing, 'Enable Tick Profiler' is off in MCM; the hitch probe still measures this mission, and the profiler's patches only call through until a restart` | INFO | The same, while the hitch probe measures the mission |
| `[TickProfiler] mission 3: not measuring, required hooks missing: Mission.OnTick call sites 0/2, Mission.OnPreTick frame-boundary prefix (Patch98); another mod's transpiler, a PatchShield strip or a failed patch apply left them out, and the next mission checks again` | WARNING | Once per mission, installed and toggled on, when a hook the profiler needs is not in place at the mission's start, and the hitch probe not measuring the mission: it is off, or the missing hooks include Patch98's frame boundary, which closes the probe's frames too. The names are `Mission.OnTick call sites n/2` (a rewrite found fewer anchors), `Mission.OnTick transpiler (Patch97)`, `Mission.OnPreTick frame-boundary prefix (Patch98)` and `Mission.TickAgentsAndTeamsImp prefix (Patch91)` or `finalizer (Patch91)`, all the missing ones in one line; a name followed by `(not resolved)` or `(patch info unreadable: <exception type>)` could not be checked and counts as missing |
| `[TickProfiler] mission 3: no per-type timing, required hooks missing: Mission.OnTick call sites 0/2; the hitch probe still measures this mission; another mod's transpiler, a PatchShield strip or a failed patch apply left them out, and the next mission checks again` | WARNING | The same, while the hitch probe measures the mission (the default) |
| `[TickProfiler] mission 3: measuring stopped at t=+65s, required hooks missing: Mission.OnTick call sites 0/2; they were in place at this mission's start, so a later patch on the method took them out, and the next mission checks again` | WARNING | Once per mission, when a rerun of the transpilers lowers the `Mission.OnTick` site count while the mission measures. Found on the next tick; measuring stops for the rest of the mission, and the `[TickSummary]` at its end still covers every frame closed before the stop |
| `[TickProfiler] mission 2: measuring, top 8 behaviours per line, hitch threshold 250 ms, first 100 hitch frames written in full, sites Mission.OnTick 2/2 Mission.OnPreTick 2/2` | INFO | Once per measured mission, when the mission is created (before any `[Hitch]` and before `[PerfContext]`): the knobs this mission read, the hitch-line cap, and the call sites swapped right now |
| `[TickProfiler] MCM 'Hitch Threshold (ms)' reads 10, out of range, so 250 ms is used` | WARNING | Right after the header, one per knob whose raw MCM value the provider replaced with its default (a hand-edited settings file; the MCM page clamps its own input). `Tick Profiler Top Behaviours` reads `... so 8 is used` |
| `[TickProfiler] hitch line cap reached at t=+412s: the first 100 slow frames of this mission were written in full; later ones are counted only in the mission summary's hitches= and worstHitchMs=` | INFO | Once per mission, on the first hitch frame past the cap |
| `[TickProfiler] mission end for generation 4: no frame closed while measuring, so no mission summary` | INFO | A measured mission that ended before closing a frame |
| `[TickProfiler] context read of scene failed, that field falls back to na, -1 or unknown: NullReferenceException: ...` | INFO | Once per mission, the first `[PerfContext]` read that threw |
| `[TickProfiler] memory status read failed, memLoad and availPhysMB fall back to na` | INFO | Once per mission, when the memory read returned false and no earlier `[PerfContext]` read failed (one context fault line per mission) |
| `[TickProfiler] mission end for generation 4 ignored: generation 5 is current and keeps measuring` | INFO | An older mission's end arriving after a newer mission began |
| `[TickProfiler] frame boundary failed, measuring stopped: InvalidOperationException: ...` | ERROR | A fault in `install`, `frame boundary`, `agent tick`, `mission behaviour` or `mission summary`; measuring stops for the mission, so it is logged once |

### Mechanism

`Patch97_MissionTickProfiler` (registry entry in `docs/reference/harmony-patch-registry.md`) swaps,
in `Mission.OnTick`, the one `callvirt MissionBehavior::OnPreDisplayMissionTick` and the one
`callvirt MissionBehavior::OnMissionTick` for static calls of `MissionTickProfilerHooks` helpers,
and in `Mission.OnPreTick` the one `call Mission::WaitTickCompletion()` and the one
`callvirt MissionBehavior::OnPreMissionTick`. The frame boundary is Patch98's prefix on
`Mission.OnPreTick` (it moved there in plan 041, so both modes share it). Each swap is stack-identical (the helper takes the instance first) and must match
exactly once, or its whole method stays vanilla with one warning. A helper, when not measuring,
calls the virtual and returns; measuring, it takes two timestamps and two allocation reads around
the call inside `try`/`finally`, so a throwing behaviour's exception propagates unchanged and is
still recorded. A per-process generation number stops an older mission's end from switching off a
newer mission. With the profiler installed, an exception thrown by a behaviour's tick carries a
`MissionTickProfilerHooks` frame between the behaviour and `Mission.OnTick`, so stack-based attribution
of the "involved module" can name TAOM; the behaviour's own frame is still the top one.

**Hook health.** The install is a startup fact (`MissionTickProfilerHooks.Installed`: the category applied and both
`Mission.OnTick` calls swapped); Harmony patches can change afterwards. Another mod's transpiler that runs earlier can
take an anchor, after which Patch97's rewrite finds none, returns its input and lowers `OnTickSites` (its warning says
no mission is measured). PatchShield strips every unprotected owner's prefixes, postfixes and transpilers on a method it
rescues, TAOM's own included; the strip removes Patch97's transpiler, so nothing rewrites `OnTickSites` afterwards and
the counter stays stale. A mission therefore asks
`MissionTickProfilerHealth.Problems()` at its start: `OnTickSites` is 2, and `Harmony.GetPatchInfo` lists the patch
methods the profiler needs on their targets (`HookHealth`). Required: Patch97's
`Mission.OnTick` transpiler (behaviour time), Patch98's `Mission.OnPreTick` frame-boundary prefix (no frame closes without it)
and Patch91's agent-tick prefix and finalizer on `Mission.TickAgentsAndTeamsImp` (`agentTickMs`). The `Mission.OnPreTick`
transpiler is not: without it `waitTickMs` and `preTickMs` read 0 and that time lands in `otherMs`, which the mission
header's `Mission.OnPreTick n/m` already says. A problem at the start means the profiler times no behaviours for that mission and
writes one aggregated warning; with "Enable Hitch Probe" on (the default) the mission still measures in probe mode,
unless the missing hooks include Patch98's frame boundary, without which no frame closes in either mode. The next
mission checks again, so a hook that came back measures again. A hook that cannot be
checked (an unresolved target, patch info that throws) counts as missing, never as healthy. While a mission measures,
every tick re-reads only the site count (a field): a rewrite is the one loss that announces itself, because the
transpiler's own warning says no mission is measured, and when the count drops measuring stops at once with one
warning. A strip is not re-read mid-mission, because reading patch info deserializes it on every call and resolves each
patch's method by scanning the loaded assemblies (0.01 ms per `GetPatchInfo` and 0.02 ms per patch method in a
30-assembly test host; the game loads hundreds and the scan grows with them, unmeasured there): cheap at a mission start,
not something to do on the clock of the measurement being taken.

### Cost

**Off:** no Patch97 patch exists. Every player pays the one `[TickProfiler] off:` line per process,
the `[PerfContext]` line per mission (one `GlobalMemoryStatusEx` read, about 84 us, four engine
option reads and a few `GCSettings` reads, once), and two static null checks (enter and exit) per
agent tick in Patch91's bracket, about one agent tick per mission frame. **Installed, then switched
off in MCM:** the patches stay until a restart, so every behaviour call pays one static read and one
volatile read before calling through, and the frame boundary the same. **On:** two timestamps and two allocation-counter reads per behaviour call, one
dictionary lookup keyed by `Type`, no allocation after a type's first sighting, no locks on the
main thread (the agent-tick totals use `Interlocked`), no per-frame line except `[Hitch]`. The hook
health check is three `Harmony.GetPatchInfo` reads and a short list at each mission start, plus one static int read
per tick while measuring.

### Known limits

- **Hook health reads what Harmony registered, not the final IL.** A transpiler that runs after Patch97 and rewrites its
  helper calls is not detected: the site count is taken before it, and `GetPatchInfo` lists patches, not the generated
  wrapper.
- **The profiler's health check finds a PatchShield strip of a required patch at the next mission start**, not before.
  Until then the mission keeps measuring with the hook gone: a stripped `Mission.OnTick` transpiler zeroes `preDisplayMs`
  and `missionTickMs`, and those two phases leave the per-behaviour list; a stripped frame-boundary prefix leaves empty
  windows, which Patch98's finalizer reports from the next frame (its missing-prefix warning, once per process, and the
  count at each measured mission's end).
- **The agent-tick bracket has one open slot.** Patch91's prefix writes one start stamp and its finalizer consumes it
  (`OnAgentTickEnter`, `OnAgentTickExit`), so the profiler assumes one agent tick is open at a time.
  `Mission.TickAgentsAndTeamsImp` sets `tickCompleted` before it calls every submodule's `AfterAsyncTickTick`
  (`Mission.cs:3629-3633`) and the finalizer runs after that tail, so the tail of tick N can still run while the main
  thread is past `WaitTickCompletion`. Tick N+1 cannot start before the main thread has left `WaitTickCompletion` in
  the next `OnPreTick`, run the `OnTick` behaviour loops and launched it (`Mission.cs:3548`, `:3786`), so an overlap
  needs an `AfterAsyncTickTick` subscriber slower than that (TAOM overrides none); whether native can start the next
  tick sooner is UNVERIFIED. If two brackets did overlap, the earlier
  exit would consume the later stamp and the later exit would record nothing, so `agentTickMs` would read low for that
  tick. A stale exit after a new mission began could do the same, but `BeginMission` clears the stamp and the old
  mission's tail would have to outlast a whole scene load. A per-call token (Harmony `__state`) would remove the
  assumption, at the price of changing Patch91 for every player, so it waits for a measured overlap (Codex review,
  2026-10-03, observation 3).

### PatchShield

Six mission targets carry patches the profiler or the hitch probe relies on. Three are shielded, and three stay on
`PatchShieldPolicy.ExcludedTargetMethods` for every player, profiler on or off:

| Target | TAOM patches on it | PatchShield | A missing-API swallow there |
|---|---|---|---|
| `Mission.OnPreTick` | Patch97's transpiler (profiler on), Patch98's bracket (either toggle on) | shielded (D13) | skips the rest of the method, all of it when a prefix threw, the wait included; strips Patch97's transpiler and Patch98's prefix |
| `Mission.WaitTickCompletion` | Patch98's bracket | excluded: a swallow would skip the wait loop | none there; the exception reaches `OnPreTick`, which is shielded |
| `Mission.OnTick` | Patch97's transpiler (profiler on), Patch98's bracket | shielded (D13) | skips the rest of the method, and a throw between the `tickCompleted` clear and the agent tick that sets it leaves the next frame waiting forever (below); strips Patch97's transpiler and Patch98's prefix |
| `Mission.TickAgentsAndTeamsImp` | Patch91's bracket (every player) | excluded: its body sets `tickCompleted` | none there; from the asynchronous agent tick the exception reaches native's job thread, from the inline one (fast-forward) it reaches `OnTick` |
| `Mission.SpawnAgent` | Patch23's colour prefix (every player), Patch97's attribution transpiler (profiler on), Patch98's bracket | shielded (D13) | the spawn returns null in place of its agent; strips Patch23's prefix, Patch97's transpiler and Patch98's prefix |
| `ManagedScriptHolder.TickComponents` | Patch97's attribution transpiler (profiler on), Patch98's bracket | excluded, as built | none there; native reaches it through a `ManagedCallbacks` shim, which PatchShield also excludes |

Finalizers are never stripped, so Patch91's and Patch98's stay. On an excluded target, for any owner's patch on it,
PatchShield's swallow of a MissingMethod, MissingField or TypeLoad exception (every other exception is rethrown) and its
strip of the offending patch are given up. An exception escaping the asynchronous agent tick reaches the native job
thread (the generated shim `Mission_TickAgentsAndTeams` has no catch, `ManagedCallbacks.CoreCallbacksGenerated.cs:936-940`,
and what native does with the exception is UNVERIFIED). PatchShield writes one diag.log line per patched method it skips
this way, naming the method, its patch owners and what is given up. The hitch probe part below covers the probe's side.

`Mission.OnTick` and `Mission.OnPreTick` were on the list too (plan 028), for cost alone. Maintainer
decision D13 (2026-10-03) took them off once plan 034 had made PatchShield's finalizer cheap (the figures are in `PatchShieldPolicy`'s `ExcludedTargetNamespacePrefixes` comment): two calls per frame and one per spawn were small even before that, and do not pay for the
rescue the exclusion gave up. They are shielded like any other patched method again, from the pass that sees them. A foreign mod's load-time patch on either is covered from
a process's first game (pass 1, or pass 2 for a patch applied after it); TAOM's own patches there
(Patch97, Patch98) apply in the first game's late batch, after both passes, so they are
covered from the second game start, when pass 2 attaches what was patched since the last pass. The
finalizer then runs on both once per frame, alongside `Mission.Tick` (Patch37), `MissionState.TickMissionAux`
(Patch91) and `MissionState.OnTick` (Patch43), which already carried it.

The rescue itself: PatchShield classifies by exception type only. Any MissingMethod, MissingField or
TypeLoad exception that escapes the shielded method is swallowed, whatever threw it: a patch on the
method, but equally a mission behaviour the method calls (a foreign `OnPreDisplayMissionTick` or
`OnMissionTick` raised from `Mission.OnTick`, a foreign `OnPreMissionTick` raised from
`Mission.OnPreTick`, an `OnAgentBuild` handler raised from `Mission.SpawnAgent`). The shield then strips
the prefixes, postfixes and transpilers of every owner on that method that is not protected, without
finding out which patch, if any, threw. TAOM's owner `com.taom.mod` is not a protected prefix
(FOLLOW-UP L1 in the review record), so a swallow on `Mission.OnTick` or `Mission.OnPreTick` costs
TAOM's own Patch97 transpilers and Patch98 prefix on that method as well. Finalizers
are never stripped, so Patch98's stay; see "A finalizer without its prefix" under "What the probe
brackets" for what they do alone.

What the two kept exclusions do not cover. `TickAgentsAndTeamsImp` and `WaitTickCompletion` stay off the
shield so that no swallow happens at them, but the same exception then reaches their caller, and both
callers are shielded: `WaitTickCompletion` is called only from `Mission.OnPreTick`, and
`TickAgentsAndTeamsImp` runs synchronously inside `Mission.OnTick` whenever the frame passes
`doAsyncAITick: false` (every fast-forward tick; at normal speed native runs it asynchronously, through
the `TickAgentsAndTeams` callback). A missing-API exception from a prefix on the wait, or one escaping
the synchronous agent tick, is therefore swallowed one level up, with the rest of that caller skipped.
Re-shielding `Mission.OnTick` also does not prevent the frame-completion hang, and no list entry does:
`OnTick` clears `tickCompleted` before it runs the behaviours' `OnMissionTick` (v1.5.3 `Mission.cs:3756`)
and the flag is set again only at the end of the agent tick (`:3629`), so an exception that escapes
`OnTick` between the two and is swallowed above it leaves the next `WaitTickCompletion` spinning forever.
What can throw there needs no patch: any module's behaviour in its `OnMissionTick`, the spawned-item handling,
`OnEndMissionRequest` (`:3777`), a patch or transpiled call in that stretch, and the inline agent tick before it sets
the flag (a subscriber's `AfterAsyncTickTick` runs after `:3629`, so a throw there does not). A throw before the clear (a prefix, or a behaviour's
`OnPreDisplayMissionTick`, `:3750`) or after the agent tick is launched (a postfix) does not leave the flag
false, though one before the clear that a finalizer swallows still skips the rest of `OnTick`, the agent
tick launch included, on every frame it recurs. Finalizers at three levels can swallow the throw, and no
vanilla frame between `Mission.OnTick` and the engine catches it (`MissionState`, `GameStateManager.OnTick`,
`GameManagerBase.OnTick`, `Module.OnApplicationTick`, its caller `CoreManaged`'s `IManagedComponent.OnApplicationTick`
(v1.5.3 `CoreManaged.cs:120-123`) and that method's caller `Managed.ApplicationTick` have no `try`, and
`Game.OnTick` calls `GameStateManager.OnTick` outside its only `try`, which wraps the game handlers):

- PatchShield's finalizer on `Mission.OnTick`, and above it on `MissionState.TickMissionAux` (Patch91) and
  `MissionState.OnTick` (Patch43): a missing-API exception only, on the timing described above for
  `Mission.OnTick`, and from a process's second game start for the two `MissionState` methods.
- Patch37's crash capture on `Module.OnApplicationTick`, and again two frames up on `Managed.ApplicationTick`
  (`CrashReportPatchHelper.HandleAndSwallow`): any exception, in any game, while crash capture is on (the default).
  The outer one gets its own try at what the inner one hands back. Native's other tick entry,
  `Managed.ApplicationTickLight` (v1.5.3 `Managed.cs:303-313`), calls the same component, and neither Patch37 nor
  PatchShield wraps it, so a frame ticked that way has no TAOM catcher above `Module.OnApplicationTick` (whether
  native ever ticks a mission that way is UNVERIFIED).
- PatchShield's own finalizer on both, for what Patch37 hands back (capture off, re-entry, the crash service
  unresolved or throwing): a missing-API exception only, attached by the first game start's pass 2.

So an ordinary exception in that stretch, a null reference from any module's `OnMissionTick` for one, freezes
the next frame in any game while crash capture is on, with no shield involved. That path is not new with
D13: from a process's second game start the shielded `MissionState` methods already swallowed a missing-API
exception one level higher, and in a process's first game, before D13, TAOM's own patches on the callers
above `OnTick` had no shield yet, so the exception unwound the application tick instead, up to
`Module.OnApplicationTick`'s finalizers (every module's `OnApplicationTick`, `JobManager.OnTick`, the avatar
services, and `Game.OnTick`'s game handlers, `AfterTick` and save-completion handoff skipped for that frame, and,
for a postfix that threw on every frame after `Mission.OnTick` had ended the mission, `MissionState.OnTick` never
popping it, so the battle never closed; the 2026-10-02 lesson in `harmony-il.md`).

It is UNVERIFIED in game (read from the v1.5.3 decompile, and no test exercises it) and not fixed here: it
belongs to the PatchShield follow-up plan. A strip of only the owner whose patch threw (FOLLOW-UP L1 in the
plan 028 review record) changes none of the catchers above; the remedy is a completion-aware recovery on
`Mission.OnTick`, for example a TAOM finalizer that completes the tick when the body unwinds after `:3756`,
tested for a throw before the clear, between the clear and the agent tick launch, inside the inline agent
tick, in a postfix after the asynchronous launch (where the flag is false only because the job still runs, so a
recovery that set it then would let the next frame overlap the agent tick) and with crash capture off. A recovery
finalizer ordered after PatchShield's sees no exception once PatchShield has swallowed it. Decision D13 gives the
remedy to the PatchShield follow-up plan. A second hazard of the same kind: a
swallowed foreign prefix throw on `Mission.OnPreTick` skips that method's whole body, whose first call is
`WaitTickCompletion` (`Mission.cs:3548`), so that frame's `OnTick` can run while the previous agent tick
still runs (consequence UNVERIFIED; older than the profiler, since a foreign patch on it was shielded the
same way before plan 028).

PatchShield writes a swallow's diag.log line for its first occurrence and counts the repeats (decision D16);
Patch37's capture logs a recurring throw at occurrences 1, 2, 10, 100 and so on
(`CrashBundleThrottle.IsLoggedOccurrence`), so a throw that recurs every frame leaves few lines.

`MissionTickProfilerBindingTests` walks the real targets in both directions
(`AgentTickTarget_IsOnPatchShieldsExclusionList` and `TickAndPreTickTargets_AreNotOnPatchShieldsExclusionList`)
and pins the split.

## Hitch probe and attribution (Patch98)

On by default for every player (plan 041). Battles show single frames of 0.6 to 1.1 s every 30 to 70 s,
the worst in the spawn wave; the probe makes every player's own `taom_debug.log` say where each such
frame went, with no setting changed. It brackets five whole engine methods (no transpiler), so it
cannot name a behaviour or a component; the per-type attribution stays behind `Enable Tick Profiler`,
because it needs transpilers.

### Modes

Decided once per process at the first game init, by the same installer call as Patch97 (no new
`SubModule.cs` call):

| `Enable Hitch Probe` | `Enable Tick Profiler` | Installed | Mode |
|---|---|---|---|
| off | off | nothing | `off` |
| on (default) | off (default) | `Patch98_HitchProbe` | `probe` |
| any | on | `Patch97_MissionTickProfiler` (plan 028's transpilers plus the two attribution transpilers), then `Patch98_HitchProbe` | `full` |

Patch98 holds the frame boundary for both modes: the `Mission.OnPreTick` prefix moved out of Patch97,
so the profiler needs Patch98 to measure anything: a failed Patch98 leaves Patch97's transpilers applied
but inert (`Installed` reads false, so no mission times behaviours; Harmony does not roll back).
A mission measures when Patch98 is installed and either toggle reads on at that mission's start (the
profiler's toggle also needs Patch97 installed). Behaviour timing by type (`full`) needs the profiler
installed and its toggle on at the mission's start. `[PerfContext]`'s `tickProfiler=on` still means
behaviour timing, so a default install is not flagged as profiled.

### What the probe brackets

Each bracket is a `void` prefix at `Priority.First` and a `void` finalizer at `Priority.Last`, each
taking only Harmony's `__state` (`out` on the prefix, `ref` on the finalizer), so it encloses every
other patch on the method (Patch23 on `SpawnAgent`, Patch97 on `OnTick`) and still closes when the
method throws:

| Target | Thread | Feeds |
|---|---|---|
| `Mission.WaitTickCompletion()` | main | `waitTickMs` in `probe` mode |
| `Mission.OnPreTick(float)` | main | the frame boundary, the clip-loading sample, `preTickAllMs` |
| `Mission.OnTick(float, float, bool, bool)` | main | `onTickMs` |
| `ManagedScriptHolder.TickComponents(float)` | native's choice, logged | `scriptTickMs`, `calls` |
| `Mission.SpawnAgent(AgentBuildData, bool, Equipment, ItemObject)` | main, checked | `spawnMs`, `spawns` |

The agent tick keeps Patch91's bracket. Every hook body is in `try`/`catch`: a fault turns that part
off for the process with one ERROR line and never throws into the engine.

**A finalizer without its prefix.** PatchShield's rescue strips prefixes, postfixes and transpilers
and never finalizers, and a prefix before ours that throws keeps ours from running, so a finalizer can
run alone. (A prefix before ours that only returns `false` does not: ours is `void` and takes nothing
but `__state`, which Harmony does not count as affecting the original, so it still runs;
`HitchProbePrefixGuardTests` runs both cases through real Harmony.) Each call therefore carries its
own state from its prefix to its finalizer in `__state`. Harmony starts it at 0 in every call of the
patched method; each enter hook sets it to Entered as its first statement, before it looks at the
profiler (the only code ahead of one is the frame boundary at the top of the `OnPreTick` prefix, which
catches its own faults and never throws), so a prefix that returned early (not measuring, a part turned
off, the wait swap live, an off-main spawn) still pairs with its finalizer and stays silent; the
finalizer that closes the call sets it to Closed. A finalizer that finds 0 adds no duration, records
nothing and is counted, and the first one per method per
process writes one WARNING (the status lines below). A finalizer that finds Closed does nothing:
Harmony reruns every finalizer of a call when a later finalizer throws on the normal path, and the
rerun must neither record the call again nor read as a missing prefix. Because the state belongs to
the call, not to a thread or a nesting level, a nested spawn that lost its prefix cannot close the
outer spawn that kept its own: PatchShield strips from inside the call that threw, so the outer call
keeps running its old replacement while a later nested call in it runs one with no prefix. Each
measured mission's end writes one WARNING with the number of such calls per method since the last such
line, the first one included, so a reader can tell every call since a strip from a single call. While
the prefix stays gone the matching columns read 0, and those counts are the only record of the calls
they missed.

**Phase columns per mode.** `[TickProfile]`, `[Hitch]` and `[TickSummary]` keep plan 028's format
byte for byte. In `full` mode their phase columns are the behaviour sums above. In `probe` mode no
behaviour call is timed, so they come from the brackets: `preDisplayMs` is 0, `missionTickMs` is the
whole `Mission.OnTick` minus the agent tick that ran on the main thread (both behaviour loops, the
camera, dynamic entities, spawned items), `preTickMs` is the whole `Mission.OnPreTick` minus the wait
(the `OnPreMissionTick` loop, since `TickDebugAgents` is empty), and `otherMs` follows plan 028's
formula; `top=none`. `[HitchDetail]` carries `mode=` and the raw brackets (`onTickMs`,
`preTickAllMs`), so a reader always knows which definition a line used. Spawn and script time are
parts of these columns, not additions: a spawn made from a behaviour's `OnMissionTick` is inside
`missionTickMs`, and `TickComponents` on the main thread is inside `otherMs`.

**The wait.** In `full` mode plan 028's call-site swap times `WaitTickCompletion` (when its delegate
bound and both `OnPreTick` sites swapped); the probe's wait bracket still fires (the swapped helper
calls the patched method through its delegate) but records nothing, so the wait is counted once. In
`probe` mode the bracket on `WaitTickCompletion` itself times it. The method is 17 bytes with a loop,
so the JIT could inline it into the patched `OnPreTick`, and then the bracket would never run. The
probe checks: if the wait bracket did not run inside any of the process's first 30 measured
pre-ticks, it writes one WARNING line, and from then on `waitTickMs` reads 0 and `preTickMs` includes
the wait.

**Scene scripts.** `TickComponents` is reached from native for every scene with script components
(the mission scene, the campaign map, any other scene ticking), and which thread calls it is not
recorded in the repo, so the bracket keeps a `[ThreadStatic]` open flag and start stamp and adds its
time with `Interlocked`. The first measured call of the process names its thread once: INFO on the
main thread, WARNING elsewhere, in which case per-component attribution is off for the process and
the totals stay. `calls=` on `[ScriptProfile]` counts every call, so more calls than frames means
more than one scene ticked.

**Spawns.** `SpawnAgent` brackets nest (an `OnAgentBuild` handler may spawn): a main-thread depth
counter times only the outermost call into `spawnMs`, and every call counts in `spawns`. A call off
the main thread is counted in `offMainSpawns`, not timed, with one WARNING line per process. Spawns
before the mission's first frame boundary (missions that spawn in `AfterStart`) land in the mission's
`preFrameSpawns` and `preFrameSpawnMs`, never dropped; everything else from that partial frame is
discarded as plan 028 does.

**Clip loading.** At the first tick of the first measured mission of the process, the sampler calls
`MBAnimation.IsAnyAnimationLoadingFromDisk()` 32 times and takes the median per-call cost; it samples
every frame only when the median is within 20 us (0.2% of a 10 ms frame), and logs one line either
way. While on, the pre-tick prefix samples once per frame, right after the frame boundary, and marks
the frame it opens: the main thread waits for the agent tick right after that point, so a clip load
blocking a worker of the agent tick shows up in that frame's `waitTickMs`, and that frame's `[Hitch]`
and `[HitchDetail]` carry the flag. An exception from the native call turns sampling off for the
process with one WARNING line.

**Attribution (`full` mode only).** Two transpilers in Patch97's category: in `Mission.SpawnAgent`
both `callvirt MissionBehavior::OnAgentBuild(Agent, Banner)` become `MissionAttributionHooks.TimedAgentBuild`,
recorded per behaviour type (`[SpawnProfile]` `top=`); in `ManagedScriptHolder.TickComponents` the one
`callvirt ScriptComponentBehavior::OnTick(float)` becomes `TimedScriptTick` (through a cached open
delegate, since the method is `protected internal virtual`; left out when the delegate does not bind)
and the four `call TWParallel::For` become, in order, three `TimedParallelBlock` and one
`TimedOccasionalBlock` (`scriptParallelMs`, `occasionalMs`). Every count must match exactly, or that
method stays vanilla with one warning; the `attribution install` line reports the counts found.

### Cost

Target: the always-on part costs under 0.5% of a 10 ms frame (50 us). `HitchProbeOverheadBenchmarkTests`
patches five dummy methods with the real Patch98 prefixes and finalizers and times a simulated frame
(the four per-frame brackets with the wait inside the pre-tick, the agent-tick pair, two spawns, the
frame boundary, clip sampling on through a stub adapter) against the unpatched dummies with no
profiler, as a default player had before the probe (so the agent-tick pair's measuring counts): 0.50
us per frame on the desktop on 2026-10-03 (three runs, 0.492, 0.496 and 0.506 us, with the per-call
state and the missing-prefix counts; the counts-only guard before it read 0.512, 0.514 and 0.514 us,
and 0.476, 0.479 and 0.488 us before any guard), about 0.005% of a 10 ms frame. That is the managed
part only: a claim about the managed brackets, not about the native call, which is a stub there. Two
things about the native clip-loading call are not established:

- **The cost gate is a one-time eligibility check, not a continuing budget.** The median of 32
  back-to-back calls at the first measured mission of the process decides whether sampling is on
  (within 20 us), and that decision is kept for the process: later missions do not measure again, a
  later sample is not timed, and a sample that is slower later does not turn sampling off (only an
  exception from the native call, or a fault in the pre-tick bracket, does). The native function returns at the first record in the loading
  state, so a batch taken while a clip happens to be loading returns early on every call and
  under-states a frame with nothing loading, which walks the whole record list. The per-frame cost of
  the sample in a battle is therefore UNVERIFIED; the `anim-loading sample` line's median is the cost
  of that batch at that moment. When sampling is on, the call runs after the frame boundary, so each
  frame's share lands in that frame's `otherMs`.
- **Whether the walk is safe against the engine's own writers is UNVERIFIED.** The native function
  (RVA `0x6EAAE0` in the v1.5.3 client) reads the clip record list with no lock, and the sample runs
  before the wait that joins the previous frame's agent tick, so a clip may still be loading on a
  worker. A load only changes a record's state field, one aligned 4-byte value that the walk reads
  old or new, and vanilla itself polls the same function every frame while a mission's loading screen
  waits for it to return false (`MissionScreen`, v1.5.3).
  What is not established is the list's lifecycle. The list is a member of a larger engine object and
  records are appended through that object's methods; only its initializer and its teardown at process
  exit write the list's pointers by absolute address, and the append path was not traced to a thread
  or a moment. A later review reported clip registration appending to the registry and a teardown that
  frees records and clears it (addresses not re-derived here). Whether any of that can overlap a
  battle's sample is not known. Evicting a clip frees its data and sets its record's state back to 0
  (the engine reference, section 6), so eviction alone does not free a record the walk reads. A native
  fault in the sample cannot be caught, so a failure there would be a crash with no probe line first.
  The first battles with the probe on that end without one are the first evidence. With both toggles
  off nothing is installed and no sample is taken (a restart applies a changed toggle).

It is opt-in (`TAOM_RUN_BENCHMARKS=1`), so the default run reports it skipped. At game start the
`probe install` line reports `ProbeCostMeter`'s measurement of the profiler bookkeeping alone (about
2,000 simulated frames on a scratch profiler, no Harmony), and the first measured mission's
`anim-loading sample` line reports the native call's median. With both toggles off nothing is
installed and the cost is the one `probe off:` line per process.

### Settings

| Setting | Default | Read |
|---|---|---|
| `Enable Hitch Probe` (`EnableHitchProbe`) | on | Once per process at the first game init, where Patch98 installs or is skipped (`RequireRestart = true`, allowlisted in `SettingRequireRestartPostureTests`), and at each mission start. The provider fails CLOSED to off when MCM is not ready, because the toggle installs patches |

`Hitch Threshold (ms)` applies to both modes. The toggle is instrumentation and excluded from the
co-op settings fingerprint.

### Log lines

Number rules as plan 028's data lines (invariant culture, ms with two decimals, `t` in whole seconds
on the same clock as `[TickProfile]`, `na` for not measured, `none` for an empty list, `Type` is
`Type.Name`). No new tag contains another data tag as a substring (`[HitchDetail]` is not `[Hitch]`
followed by more: `D` follows `h`; `[TickSummaryExtra]` likewise). `HitchProbeLinesTests` pins each
literal below.

**`[SpawnProfile]`**, INFO, in a measuring mission's 5 s window right after `[TickProfile]`, only when
the window had spawns. `top=` lists the slowest `OnAgentBuild` handlers as `<Type>:<ms>/<calls>` in
`full` mode with both `SpawnAgent` sites swapped, else `none`:

```
[SpawnProfile] t=+10s spawns=412 spawnMs=286.40 top=AdvancedCombatBehavior:40.20/412,WargMissionBehavior:12.75/412
```

**`[ScriptProfile]`**, INFO, every window after `[SpawnProfile]`. `calls` is the number of
`TickComponents` calls in the window; `scriptParallelMs` and `occasionalMs` are the parallel and
occasional blocks (`na` unless the `TickComponents` rewrite found all its sites); `top=` lists
main-thread components as `<Type>:<ms>/<calls>/<maxMs>`, or `none`:

```
[ScriptProfile] t=+65s calls=300 scriptTickMs=310.50 scriptParallelMs=120.25 occasionalMs=8.00 top=TaomHowdahMachine:95.10/1200/2.40,SiegeTower:40.00/300/0.90
[ScriptProfile] t=+65s calls=300 scriptTickMs=310.50 scriptParallelMs=na occasionalMs=na top=none
```

**`[AnimLoad]`**, INFO, every window after `[ScriptProfile]` while the clip-loading sampler is on: the
window's frames that began with a clip loading from disk, and its frames:

```
[AnimLoad] t=+65s loadingFrames=3 frames=300
```

**`[HitchDetail]`**, INFO, right after each `[Hitch]`, same `t`, so it follows `[Hitch]`'s cap of the
first 100 slow frames per mission. `spawnMs`, `scriptTickMs`, `scriptParallelMs` (`na` as above),
`animLoading` (1 or 0, `na` while the sampler is off), `mode` (`probe` or `full`), `spawns`,
`occasionalMs`, and the raw `onTickMs` and `preTickAllMs` brackets:

```
[HitchDetail] t=+72s spawnMs=0.00 scriptTickMs=4.10 scriptParallelMs=1.25 animLoading=1 mode=full spawns=0 occasionalMs=0.30 onTickMs=6.30 preTickAllMs=790.60
[HitchDetail] t=+72s spawnMs=12.00 scriptTickMs=4.10 scriptParallelMs=na animLoading=na mode=probe spawns=3 occasionalMs=na onTickMs=20.50 preTickAllMs=790.60
```

**`[TickSummaryExtra]`**, INFO, at the end of a measured mission that closed a frame, right after
`[TickSummary]` (never after the `no mission summary` line): the
mission's spawn and script totals, `animLoadingFrames` and `hitchesWithAnimLoading` (`na` when the
sampler was never on in the mission; the hitch count includes hitches past the line cap), the mode,
the frames, every spawn count (`preFrameSpawns` and `preFrameSpawnMs` before the first boundary,
`offMainSpawns` counted but not timed), the block totals, the raw bracket totals and the mission-wide
tops:

```
[TickSummaryExtra] spawnMs=1843.20 scriptTickMs=6020.75 animLoadingFrames=41 hitchesWithAnimLoading=3 mode=full frames=18000 spawns=1313 preFrameSpawns=2 preFrameSpawnMs=3.50 offMainSpawns=0 scriptParallelMs=2400.50 occasionalMs=160.00 onTickMs=41000.25 preTickAllMs=9800.00 spawnTop=AdvancedCombatBehavior:210.40/1313 scriptTop=TaomHowdahMachine:1900.20/72000/4.80
[TickSummaryExtra] spawnMs=1843.20 scriptTickMs=6020.75 animLoadingFrames=na hitchesWithAnimLoading=na mode=probe frames=18000 spawns=1313 preFrameSpawns=0 preFrameSpawnMs=0.00 offMainSpawns=0 scriptParallelMs=na occasionalMs=na onTickMs=41000.25 preTickAllMs=9800.00 spawnTop=none scriptTop=none
```

**Status lines** (all `[TickProfiler] `, none holds a data tag):

| Line | Level | When |
|---|---|---|
| `[TickProfiler] probe off: 'Enable Hitch Probe' and 'Enable Tick Profiler' are off at game start (or MCM was not ready); no probe patches installed, no hitch lines this session` | INFO | Once per process, both toggles off |
| `[TickProfiler] probe install: category applied, enabled by hitch probe, targets Mission.OnPreTick,Mission.WaitTickCompletion,Mission.OnTick,ManagedScriptHolder.TickComponents,Mission.SpawnAgent, bookkeeping 1.25 us per frame (0.01% of a 10 ms frame, target 0.50%)` | INFO | Once per process, either toggle on: `applied` or `failed`, `hitch probe`, `tick profiler` or `both`, the measured bookkeeping |
| `[TickProfiler] attribution install: Mission.SpawnAgent sites 2/2, ManagedScriptHolder.TickComponents sites 5/5, script tick delegate bound` | INFO | Once per process, profiler on: the sites the two attribution rewrites swapped (the `TickComponents` denominator is 4 when the delegate did not bind) |
| `[TickProfiler] ScriptComponentBehavior.OnTick could not be bound; per-component script attribution is off, the script totals stay` | WARNING | At install, profiler on, the delegate did not bind |
| `[TickProfiler] probe on in MCM but its patches are not installed: it was off at game start (restart the game to measure) or its install failed (see the probe install line and [PatchApply])` | WARNING | Once per mission, probe toggle on but Patch98 not installed |
| `[TickProfiler] mission 3: not measuring, 'Enable Hitch Probe' and 'Enable Tick Profiler' are off in MCM; the probe's patches stay installed and only call through until a restart` | INFO | Once per mission at its first tick, Patch98 installed (the profiler not) but both toggles switched off since game start |
| `[TickProfiler] mission 1: mode probe, hitch threshold 250 ms, spawn attribution off, script attribution off, anim-loading sample on` | INFO | Once per mission at its first tick, unless both toggles are off; mode `off` when a toggle is on but nothing is installed |
| `[TickProfiler] probe: Mission.WaitTickCompletion's bracket did not run inside Mission.OnPreTick in the first 30 frames (inlined by the JIT, or skipped by another patch); waitTickMs reads 0 and preTickMs includes the wait` | WARNING | Once per process, the wait self-check failed |
| `[TickProfiler] Mission.OnTick: the hitch probe bracket's prefix did not run before its finalizer (it was removed, or an earlier prefix threw): that call is not measured, and while the prefix stays gone onTickMs reads 0 and, in probe mode, so does missionTickMs; this line is written once per process, each measured mission's end counts these calls` | WARNING | Once per method per process, at the first finalizer that ran with no prefix before it in its own call (PatchShield strips prefixes, never finalizers; a prefix before ours that throws keeps ours from running). One line per bracket, each saying what it reads while the prefix stays gone: `Mission.OnPreTick` (no frame boundary runs, no frame closes and no hitch is detected), `Mission.WaitTickCompletion` (in probe mode `waitTickMs` reads 0 and `preTickMs` includes the wait), `ManagedScriptHolder.TickComponents` (`scriptTickMs` and `calls` read 0), `Mission.SpawnAgent` (`spawns` and `spawnMs` read 0) |
| `[TickProfiler] mission end for generation 4: hitch probe finalizers ran without their prefix since the last such line (or game start), so these calls were not measured: Mission.OnTick 1204, Mission.SpawnAgent 17` | WARNING | At a measured mission's end, after `[TickSummary]` and `[TickSummaryExtra]` (or the no-summary line), only when some finalizer ran with no prefix since the last such line: every such call per method, the one that wrote the once-per-process line included, in the order `Mission.OnPreTick`, `Mission.WaitTickCompletion`, `Mission.OnTick`, `ManagedScriptHolder.TickComponents`, `Mission.SpawnAgent`. An older mission's end writes none and takes none |
| `[TickProfiler] script tick runs on the main thread (managed thread 1, main 1)` | INFO | Once per process, the first measured script tick |
| `[TickProfiler] script tick runs on another thread (managed thread 7, main 1); per-component script attribution is off for this process, the totals stay` | WARNING | Instead of the line above, when that call was off the main thread |
| `[TickProfiler] Mission.SpawnAgent ran off the main thread (managed thread 7); off-main spawns are counted in the mission summary but not timed` | WARNING | Once per process, the first off-main spawn |
| `[TickProfiler] anim-loading sample: MBAnimation.IsAnyAnimationLoadingFromDisk median 3.20 us over 32 calls (budget 20.00 us); sampling every frame` | INFO | Once per process, the first measured mission; over budget it ends `over budget, sampling is off for this process` |
| `[TickProfiler] anim-loading sample failed, sampling is off for this process: InvalidOperationException: ...` | WARNING | Once, the native call threw |
| `[TickProfiler] spawn hook failed, its timing is off for this process: InvalidOperationException: ...` | ERROR | Once per part, a fault in `pre-tick (with the anim-loading sample, which samples there)`, `wait`, `on-tick`, `script`, `spawn` or `probe install`. In `probe` mode a pre-tick or on-tick fault moves that time into `otherMs` and a wait fault moves it into `preTickMs`; a pre-tick fault also turns the clip sample off, so `animLoading` reads `na` |
| `[TickProfiler] agent build hook failed, all per-type attribution (spawn callbacks, script components, script blocks) is off for this process: InvalidOperationException: ...` | ERROR | Once per process, a fault in an attribution helper's own bookkeeping (`agent build`, `script attribution` or `parallel block`); later missions configure no attribution, so their tops read `none` and the block columns `na` |

### PatchShield on the probe's targets

Two of the five targets stay on `PatchShieldPolicy.ExcludedTargetMethods` for every player.
`Mission.WaitTickCompletion` runs once per frame on the main thread, and a swallow there would skip the
wait loop, letting `OnPreMissionTick` overlap the running agent tick. `ManagedScriptHolder.TickComponents`
runs once per ticking scene per frame, on the thread native picks, and stays as built (decision D13). For
any owner's patch on those two, PatchShield's swallow of a MissingMethod, MissingField or TypeLoad
exception and its strip of the offending patch are given up.

`Mission.OnPreTick`, `Mission.OnTick` and `Mission.SpawnAgent` are shielded like any other patched
method: plan 028 had excluded the first two and plan 041 the third, for cost, and maintainer decision
D13 (2026-10-03) took them off once plan 034 had made PatchShield's finalizer cheap (the figures are in `PatchShieldPolicy`'s `ExcludedTargetNamespacePrefixes` comment), at
two calls per frame and one per spawn. A shield
finalizer runs inside those three brackets like every other patch on the method (the bracket's prefix is
`Priority.First` and its finalizer `Priority.Last`).

What that gives, and what it costs. A MissingMethod, MissingField or TypeLoad exception that escapes
`SpawnAgent` is swallowed, whether it came from Patch23, another mod's patch or a foreign behaviour's
`OnAgentBuild`; the same holds for `OnPreTick` and `OnTick`. The rescue then strips the prefixes,
postfixes and transpilers of every unprotected owner on the method, and `com.taom.mod` is unprotected,
so on `SpawnAgent` it also removes Patch23's banner colour prefix and Patch97's spawn
attribution transpiler (profiler on), and battlefield armour colours revert to vanilla's choice for the
rest of the process; the spawn that threw returns null in place of its agent. Patch98's own prefix goes with them; its finalizer stays, which is the case the
missing-prefix line above covers. The rescue is not free of loss for TAOM's own patches, and it does
not remove the frame-completion hazard described under "PatchShield" in the tick profiler part above:
the kept exclusions on `TickAgentsAndTeamsImp` and `WaitTickCompletion` stop a swallow at their own
method only, and their callers are shielded (`Mission.OnTick` runs the agent tick synchronously,
`Mission.OnPreTick` calls the wait). `ManagedScriptHolder.TickComponents`, the third kept exclusion, has
no such caller: native reaches it through a shim in the `ManagedCallbacks` namespace, which PatchShield
excludes.
`HitchProbeBindingTests.ProbeAndAttributionTargets_OnlyTheWaitAndTheScriptTickAreOnPatchShieldsExclusionList`
walks the real targets of the five Patch98 classes and the two attribution transpilers and pins the split.

### Not covered

View ticks (`OnMissionScreenTick`), fixed ticks (`OnFixedMissionTick`, `FixedTickComponents`),
filtering `TickComponents` to the mission scene's holder (no managed handle from the scene to its
holder was found; `calls=` shows when other scenes tick), and per-type attribution of the parallel
script blocks (they run on workers; the block totals are kept). Unverified until the first player
log: whether the JIT inlines `WaitTickCompletion` (the self-check line), which thread ticks script
components (the thread line), and the native cost of the clip-loading call (the sample line).

## Animation clip memory probe

Bannerlord keeps animation clips marked Loading Type 1 or 2 "on demand": a worker that samples an
unloaded clip blocks until it loads, which delays the parallel agent tick and shows up as a frame
spike. The engine's eviction pass works against a **12 MiB** budget: it measures the excess over
12 MiB once, when it starts, and evicts idle loaded clips until it has freed that much. Clips in use,
and loads that finish during the pass, can leave the total above 12 MiB. A pass is scheduled once a
load takes the total past 15 MiB (the loader's compare at `0x591327`; that the compare schedules the
pass is inferred, not traced). The `[AnimMem]`
probe logs how many bytes of on-demand clip data the engine holds against that budget, whether a clip
is loading at the instant of a sample, and how often the total fell between samples. A troll-heavy
battle and a large vanilla battle then show whether the budget is under pressure, which is the
evidence for raising it. They do not decide the other lever, making TAOM's hot clips resident: the
first load of a clip blocks the worker that samples it whatever the total, and the probe cannot see
that wait (see "Reading it" and the
[engine notes, section 6](../reference/engine/mission-frame-threads-and-native-costs.md)).

**How it finds the values.** No fixed offset: once per process, in the first mission's `AfterStart`
(under the loading screen), `AnimMemoryProbe` reads the PE headers of the loaded
`TaleWorlds.Native.dll`, requires `.text`, `.rdata` and `.data`, copies `.text` and scans it for a
50-byte signature of the clip eviction pass (its `mov eax` load of the loaded-bytes counter and its
`subss` of the budget float, with the four rip displacements wildcarded). The match must be unique;
the counter target must be an aligned 4-byte address inside `.data` and the budget target one inside
`.rdata`, both proven before either is read; the budget float must read exactly `12582912`; and the
first counter read must not be negative. On the v1.5.3 client the site is at RVA `0x21E00F`, the
budget `subss` at `0x21E034`, the counter at `0xDABE40` and the budget float at `0xB2E2DC`. Any failed
check turns the probe off for the rest of the process with one `[AnimMem] disabled` line naming the
reason; `[MissionPerf]` is unaffected. The probe never writes engine memory.

**What is checked and what is trusted.** Before the first read of the counter or the budget float, the
probe checks that each target is an aligned 4-byte address inside `.data` or `.rdata`, by the section
table it parsed from the module's own headers. Read on trust before that: the 4 KiB header page at the
module base, and the `.text` copy, whose range comes from the same table. Also trusted: that
`TaleWorlds.Native.dll` stays mapped for the whole process (`GetModuleHandleW` takes no reference, and
the counter address is cached once armed). Outside every check is the engine's own
`IsAnyAnimationLoadingFromDisk()`: it takes no lock, and what can grow the clip record list or free a
record while a mission callback walks it was not traced (**UNVERIFIED**; see the engine notes). A
managed `catch` cannot help there: an access violation is a corrupted-state exception on .NET Framework.

**Cost.** One scan of about 10 MB once per process, inside the first mission's loading screen
(`scanMs` in the header), plus one read of the budget float. Then, per mission, one aligned 32-bit read
of the counter and one `MBAnimation.IsAnyAnimationLoadingFromDisk()` call per second (a native walk
over every clip record, not only the on-demand ones, stopping at the first one loading; it takes no
lock), one log line every 5 s, and one or two lines at mission end. Each INFO line is written and
flushed on the calling thread, which is the main thread here (`FileLogger.LogInfo` is durable).
**Not yet measured in a battle:** what one `IsAnyAnimationLoadingFromDisk()` call costs against the
full clip table, and what each synchronous flush costs a frame. The scan reports its own cost
(`scanMs`); nothing else is timed.

**Measuring that cost.** The heartbeat's A/B ("Reading an A/B": the toggle off against on, three runs
per cell) cannot resolve either one. At 60 fps the sample lands on one frame in sixty and the 5 s line
on one in three hundred, so a cost of X ms moves `avgMs` by X/60 (0.03 ms for a 2 ms stall), which the
run-to-run variation that section warns of would hide. The five sampled frames in a window can move
`p95Ms` by at most five places along the sorted frames, and `maxMs` only if one of them becomes the
window's slowest. The A/B therefore only bounds the aggregate effect on frame time; compare `maxMs` as
well, because a single long stall can show there. To measure the two costs directly, use plan 041's
`[TickProfiler] anim-loading sample` line (the median of 32 timed calls of this same native call, once
that plan merges; it does not time a flush), or time one battle with a `Stopwatch` around a sample and
around its INFO write.

**Toggle.** Battle Load Diagnostics MCM page, `Mission Performance` group,
`EnableAnimMemoryProbe` ("Enable Animation Clip Memory Probe"), default on, read at each mission start.
Instrumentation only, so it is excluded from the co-op settings fingerprint.

**Reading it.** `pctOfBudget` above 100 is expected: the total can reach about 125% (15 MiB) before
an eviction pass is scheduled. A pass that finds enough idle clips frees the excess it measured at
its start; with clips in use, or loads landing during the pass, it can end above 12 MiB, and the next
pass waits for a load past 15 MiB. `samplesAtOrAbove90Pct` counts
against 12 MiB, so in a busy battle most samples count. `drops` counts one-second samples lower than
the one before (across a window boundary too). Among the engine's direct references to the counter
only the eviction pass lowers it (engine notes, section 6), so a drop means a pass ran since the
previous sample. It is not a count of evictions: one drop can be many clips, and a pass followed by a
reload of the same bytes inside one second leaves none.

**What the numbers can and cannot decide.** A total sitting at or above 100% with frequent drops is
evidence of budget pressure, which supports a budget raise; resident clips would also stop those clips
reloading. A low total with no drops is only the absence of that evidence: it shows no sustained
pressure in the sampled seconds, and a one-second snapshot cannot exclude churn that returns to the
same total within a second. It does not rule out making hot clips resident. A type 2 clip starts
unloaded and loads on its first sample, and again after an eviction, and that load blocks the worker
that asked for it whatever the total. The probe cannot see that wait either: `loadingNow` and
`loadingSamples` are point observations taken in `OnMissionTick`, after the frame's
`WaitTickCompletion` has already held the main thread until the previous parallel agent tick
finished, and before the next one starts, so the load that delayed a frame has already ended by the
time the probe looks. `loadingSamples=0` therefore does not show that no frame waited on a clip load.
To reject either lever, use timing or event evidence correlated with the hitches themselves, not these
logs alone: plan 028's `[Hitch]` lines give each hitch's wait and agent-tick timing, and plan 041's
clip-loading sample, taken in a prefix on `Mission.OnPreTick` before its `WaitTickCompletion`, ties a
hitch to a clip load (engine notes, section 6).

### Log lines

Every line is INFO except `stopped`, which is ERROR; none is per frame. KB is bytes / 1024 (floor) and
a percentage is floored. The formats are pinned literally by `AnimMemLineTests`.

| Line | When | Fields | Example |
|---|---|---|---|
| armed | once per process, when the scan succeeds | module base address; `.text` RVA and size; the RVAs of the load site, the budget site, the counter and the budget float; the budget in bytes; the scan's cost in ms | `[AnimMem] armed: TaleWorlds.Native.dll base=0x7FFB12340000 text=0x1000+0xA240CC loadSite=0x21E00F budgetSite=0x21E034 counter=0xDABE40 budget=0xB2E2DC budgetBytes=12582912 scanMs=23.4` |
| disabled | once per process, when a check fails or the counter later reads negative | the reason | `[AnimMem] disabled for this process: TaleWorlds.Native.dll is not loaded in this process. No further [AnimMem] samples will be taken; [MissionPerf] is unaffected.` |
| off | each mission start with the toggle off | none | `[AnimMem] off for this mission: 'Enable Animation Clip Memory Probe' is off (Battle Load Diagnostics page).` |
| mission start | each mission start with the probe armed | first sample in KB, budget in KB, percent of budget, whether a clip is loading (0 or 1) | `[AnimMem] mission start: sample every 1 s, line every 5 s, startKB=10240 budgetKB=12288 pctOfBudget=83 loadingNow=0` |
| periodic | every 5 s of wall clock | `t` (seconds since mission start); the last sample in KB and as a percent of the budget; `loadingNow` of the last sample; over the window: `drops`, `minKB`, `maxKB`, and samples with a clip loading out of all samples | `[AnimMem] t=+5s loadedKB=12288 budgetKB=12288 pctOfBudget=100 loadingNow=0 drops=2 minKB=9216 maxKB=12288 loadingSamples=0/6` |
| periodic, over budget | as above | as above | `[AnimMem] t=+10s loadedKB=15360 budgetKB=12288 pctOfBudget=125 loadingNow=1 drops=0 minKB=15360 maxKB=15360 loadingSamples=1/5` |
| periodic, tail | mission end, before the summary, when samples were taken since the last 5 s line | as periodic, over that partial window | `[AnimMem] t=+7s loadedKB=10240 budgetKB=12288 pctOfBudget=83 loadingNow=0 drops=1 minKB=8192 maxKB=10240 loadingSamples=0/2` |
| summary | mission end (`OnEndMission`, or `OnRemoveBehavior` when the mission is torn down without it), when at least one sample was taken | `t`; over the whole mission: samples, first and last sample in KB, peak in KB and percent, samples at or above 90% of the budget, drops, samples with a clip loading, and whether the session stopped early (0 or 1) | `[AnimMem] summary: t=+7s samples=6 startKB=10240 endKB=12288 peakKB=12288 peakPct=100 samplesAtOrAbove90Pct=4 drops=2 loadingSamples=0 stopped=0` |
| stopped (ERROR) | once per mission, on a caught exception | exception type and message | `[AnimMem] stopped for this mission after InvalidOperationException: boom` |

The summary counts samples at or above 90% rather than seconds because samples are one second apart
only while frames are shorter than a second.

## Key Files

| File | Purpose |
|---|---|
| `Main/Features/MissionPerf/FrameStats.cs` | Pure window: record, should-emit, emit |
| `Main/Features/MissionPerf/MissionPerfLine.cs` | The line format |
| `Main/Features/MissionPerf/Hooks/MissionPerfHeartbeatBehavior.cs` | The `MissionLogic` |
| `Main/Features/BattleLoadDiagnostics/BattleLoadDiagnosticsSettings.cs` | The heartbeat toggle, the three tick profiler settings, the hitch probe toggle and `EnableAnimMemoryProbe` |
| `Main/Features/MissionPerf/AllocationCounter.cs` | The per-thread allocation counter, bound by reflection |
| `Main/Features/MissionPerf/BehaviorTickTable.cs` | Per-behaviour-type frame, window and mission accumulators |
| `Main/Features/MissionPerf/MissionTickProfiler.cs` | Frame, window, hitch and mission-summary arithmetic; generation |
| `Main/Features/MissionPerf/TickProfileLines.cs` | Every tick profiler line, data and status |
| `Main/Features/MissionPerf/TickProfilerTranspiler.cs` | The pure call-site swap, soft-fail |
| `Main/Features/MissionPerf/HookHealth.cs` | The pure hook-health rule: which required patch methods Harmony still lists on their targets |
| `Main/Features/MissionPerf/Hooks/MissionTickProfilerHealth.cs` | The profiler's required hooks, the check at each mission start, and the site count on every tick |
| `Main/Features/MissionPerf/Hooks/Patch97_MissionTickProfiler.cs` | The two patch classes on `Mission.OnTick` and `Mission.OnPreTick` |
| `Main/Features/MissionPerf/Hooks/MissionTickProfilerHooks.cs` | The timed helpers, frame boundary, agent-tick bracket, mission start and end |
| `Main/Features/MissionPerf/Hooks/MissionTickProfilerInstaller.cs` | The once-per-process install, the swaps, the wait delegate |
| `Main/Features/MissionPerf/Hooks/MissionTickProfilerBehavior.cs` | `[PerfContext]`, the `[TickProfile]` window, `[TickSummary]` |
| `Main/Features/MissionPerf/Hooks/PerfContextReader.cs` | The `[PerfContext]` reads |
| `Main/Features/BattleLoadDiagnostics/Hooks/Patch91_MissionTickStallProbes.cs` | The agent-tick bracket the profiler reads |
| `Main/Adapters/GraphicsOptionsAdapter.cs` | The ragdoll and three quality option reads |
| `Dependencies/Foundation/PatchShieldPolicy.cs` | The three MissionPerf PatchShield exclusions (agent tick, wait, script tick) and the diag.log skip line |
| `Main/Features/MissionPerf/ProbeLineData.cs` | `HitchDetailFrame`, `ExtrasWindow`, `MissionExtras` |
| `Main/Features/MissionPerf/ProbeDelegates.cs` | `ProbeDelegates` (the open-instance delegate binder) |
| `Main/Features/MissionPerf/HitchProbeLines.cs` | Every hitch probe line, data and status |
| `Main/Features/MissionPerf/AnimLoadingSampler.cs` | The clip-loading sampler and its cost gate |
| `Main/Features/MissionPerf/ProbeCostMeter.cs` | The start-up bookkeeping measurement |
| `Main/Features/MissionPerf/ProbeWindowWriter.cs` | The window, header and mission-end lines |
| `Main/Features/MissionPerf/Hooks/Patch98_HitchProbe.cs` | The five bracket classes |
| `Main/Features/MissionPerf/Hooks/HitchProbeHooks.cs` (`.Spawn.cs`, `.Script.cs`, `.Diagnostics.cs`) | The brackets, the wait self-check, the thread lines, the fault line, the per-call missing-prefix guard and its counts |
| `Main/Features/MissionPerf/ProbeBracket.cs` | `ProbeState` (the per-call state Harmony carries from a prefix to its finalizer) and `ProbeBracket` |
| `Main/Features/MissionPerf/Hooks/HitchProbeInstaller.cs` | The once-per-process Patch98 install |
| `Main/Features/MissionPerf/Hooks/Patch97_MissionAttribution.cs` | The two attribution transpilers |
| `Main/Features/MissionPerf/Hooks/MissionAttributionHooks.cs` | The attribution helpers |
| `Main/Features/MissionPerf/Hooks/MissionAttributionInstaller.cs` | The attribution swap lists, the script tick delegate, the site counts |
| `Main/Features/MissionPerf/Hooks/MissionTickProfilerHooks.Probe.cs` | `[HitchDetail]`, the per-mission probe flags, the profiler's status lines at a mission's creation (`OnMissionCreated`), the first-tick header, `[TickSummaryExtra]` |
| `Main/Adapters/IAnimationLoadingAdapter.cs`, `AnimationLoadingAdapter.cs` | `MBAnimation.IsAnyAnimationLoadingFromDisk()` |
| `Main/Features/MissionPerf/AnimMemory/ClipBudgetSignature.cs` | The eviction-pass signature: parse, scan, rip targets, resolve |
| `Main/Features/MissionPerf/AnimMemory/PeSectionTable.cs` | PE32+ section table parser and bounds check |
| `Main/Features/MissionPerf/AnimMemory/AnimMemLine.cs` | Every `[AnimMem]` line format |
| `Main/Features/MissionPerf/AnimMemory/IAnimClipMemoryProbe.cs` | The probe interface the session uses |
| `Main/Features/MissionPerf/AnimMemory/AnimMemoryProbe.cs` | Once-per-process arming, every disable reason, the counter read |
| `Main/Features/MissionPerf/AnimMemory/AnimMemorySession.cs` | One mission: 1 s samples, 5 s line, summary |
| `Main/Features/MissionPerf/AnimMemory/AnimMemoryProbeModule.cs` | Feature module: registrations and the mission behavior |
| `Main/Features/MissionPerf/AnimMemory/Hooks/AnimMemoryProbeMissionBehavior.cs` | The `MissionLogic`: toggle, arm in `AfterStart`, tick, end |
| `Main/Adapters/INativeModuleMemoryAdapter.cs` | Read-only module memory: base, copy, aligned 32-bit read |
| `Main/Adapters/NativeModuleMemoryAdapter.cs` | `GetModuleHandleW`, `Marshal.Copy`, `Marshal.ReadInt32` |

## Tests

`TAOM.Tests/Features/MissionPerf/FrameStatsTests.cs`: cadence, average, nearest-rank p95,
single sample, empty window (zeros, not NaN), window reset, bounded sample set, clock reset,
line format.
The line-format test is a twin pin with `tools/tests/test_perf_runs.py` (`PINNED_MISSION_PERF`),
which parses the same literal: change both or neither.

Tick profiler, all in `TAOM.Tests/Features/MissionPerf/`: `AllocationCounterTests` (the reflection
binding), `BehaviorTickTableTests` (slots, folding, ordering, open frame, mission totals),
`MissionTickProfilerTests` (the phase oracle, `otherMs`, hitches and the hitch-line cap, windows,
the off-thread agent tick, generations, the mission summary), `TickProfileLinesTests` (the four
data literals, every status line literally, `na`, `top=none`, the diagnostics gating, invariant
culture, status lines free of data tags), `TickProfilerTranspilerTests` (synthetic IL: swaps,
labels, bail paths, rerun), `MissionTickProfilerHooksTests` (`RequiresGame`: a real `MissionLogic`
probe, exception propagation, the timed wait, hitch, cap and agent-tick lines, a fault, summary,
no-frame and stale end), `MissionTickProfilerInstallerTests` (`RequiresGame`: off, installed, a
failed category, a missing site, a throwing apply), `MissionTickProfilerBehaviorTests`
(`RequiresGame`: every mission's status line, `[PerfContext]` once with its fallbacks, the summary
at mission end, and the install followed by a change: a zero-site rewrite, each required hook removed, all of them at
once, the hook back at the next mission, a site count lowered mid-mission, a strip mid-mission found at the next start),
`HookHealthTests` (real Harmony on test targets with the removals PatchShield makes, a foreign owner, a patch method
seen through another reflected type, a patch that cannot resolve its method, unresolved and throwing patch info),
`MissionTickProfilerBindingTests`
(`BindingVerification`: the swaps against the installed engine IL, the hook list walked to its real targets, the
frame-boundary hook pinned to the one method that calls `OnFrameBoundary`, the PatchShield walks: the agent tick
excluded, the two tick methods not), `MissionTickProfilerWiringTests` (the
`SubModule.cs` pins: the behaviour beside the heartbeat, and the install behind the once-per-process early return, with
mutation tests showing the pin fails without it).
`PatchShieldPolicyTests` pins the Mission tick split of the exclusion list without the game.
`BattleLoadDiagnosticsSettingsProviderTests` pins the settings' defaults and validation.

Hitch probe and attribution, same folder: `MissionTickProfilerProbeTests` (the probe-mode column
oracle, the hitch detail, the window and mission extras, pre-frame and off-thread totals),
`HitchProbeLinesTests` (the eight data literals and every status line, invariant culture, no tag
inside another), `TickProfilerTranspilerOccurrenceTests` (occurrence swaps, static targets),
`OpenDelegateDispatchTests` (the open delegate calls the override), `AnimLoadingSamplerTests` (the
cost gate and the fault line), `ProbeCostMeterTests`, `ProbeWindowWriterTests` (line order and
level), `HitchProbeHooksTests` (every bracket, nesting, threads, the wait self-check, a finalizer
without its prefix by a direct exit and through a real Harmony strip of the prefix, the counts of such
calls, a lone exit on one thread beside an open call on another),
`HitchProbePrefixGuardTests` (the real Patch98 pairs on dummy methods under real Harmony: a prefix
stripped while an outer spawn is in flight, finalizers rerun for a call and for a nested call, and a
prefix before ours that throws or returns false), `HitchProbeSimulatedFrameTests` (one frame through
all five real Patch98 classes on dummy methods: each bracket's own column, exact counts, no warning),
`HitchProbeInstallerTests` (the three modes through `InstallIfEnabled`), `HitchProbeMissionFlowTests` (the per-mission
decisions without a mission: attribution flags, the first-tick header and reason lines, `[HitchDetail]` beside
`[Hitch]`, the mission-end extras, and every branch of the profiler's status at a mission's creation,
`OnMissionCreated`), `MissionAttributionHooksTests`
(`RequiresGame`: the helpers call through once and propagate), `HitchProbeBindingTests`
(`BindingVerification`: the targets, the two rewrites against the installed IL, the PatchShield split),
`HitchProbeWiringTests` (the frame boundary's move, the priorities, the `__state` signatures), and
`HitchProbeOverheadBenchmarkTests` (`Benchmark`, opt-in: the per-frame cost).

`TAOM.Tests/Features/MissionPerf/AnimMemory/`: `ClipBudgetSignatureTests` (parse, scan, rip targets
of the two real encodings, resolve on the real 50 bytes), `PeSectionTableTests` (parse and every
reject, bounds), `ClipBudgetSignatureInstalledBinaryTests` (LiveInstall: the signature on the
installed `TaleWorlds.Native.dll`, and the v1.5.3 RVAs), `AnimMemLineTests` (every line literally),
`AnimMemoryProbeTests` (header, scan once, every disable reason, NaN budget, negative counter at
arming and after it, exception, disable latch, read before arming, reads), `AnimMemorySessionTests`
(start line, cadence, 5 s line, drops in and across windows, loading count, the 90% boundary, the
tail line, summary, negative read at start and later, exception, empty mission),
`AnimMemoryProbeWiringTests` (module listed once, behavior declared, the source shape of the two
teardown overrides, singleton probe, default on, instrumentation, no native write and no
`Marshal.Copy` into native memory), `AnimMemoryProbeMissionBehaviorTests` (RequiresGame, so not on
hosted CI: the end and removal callbacks against a real session write the summary once whichever
comes first, write it from removal alone, repeat nothing, sample nothing afterwards, and write nothing
when the probe never armed). `SyntheticNativeImage` builds the fake module.

## Reading an A/B

`tools/perf_runs.py` does the arithmetic. Run the same scene several times with the change off and
several times with it on, keep each log, then:

```
python tools/perf_runs.py <logs...>
python tools/perf_runs.py compare --a <logs with it off...> --b <logs with it on...> --scene <scene id>
```

The first command prints one row per mission (a log may hold several). Each row reports the first
window on its own as the spawn window, because `BattlePlayable` fires with `agents=0` and the spawn
burst lands in that window, and steady-state numbers over the later windows from `t=+30s` with
active agents: median fps, `avgMs` and `p95Ms`, the worst `maxMs`, and `gc0`, `gc1`, `gc2` per
minute. The spawn window is never steady, even when a long render wait before the first tick puts
it past `t=+30s`. `compare` prints the median of each metric per group, the delta and the
percentage, with N per group, and refuses groups whose `[PerfContext]` build (Debug or Release) or
texture quality differ unless given `--allow-mixed`. It counts the rows it could not check (no
`[PerfContext]`, or `na` for either setting) under the table. Press F6 on the first frame so both
sides are AI-controlled, and end each run at the same point: steady windows run to the end of the
mission, routs included. Three runs per cell is the floor; AI battles vary. `gc2` should not climb
faster with the change on.

The report names what it could not read. It opens with one line per log read (path, size in bytes,
line count, missions found, and how many lines it could not parse: a `[MissionPerf]`,
`[TickProfile]`, `[Hitch]` or `[PerfContext]` line that does not read, or a `[TickSummary]` whose
`hitches=` is missing or not a whole number, a line cut before its first key included), followed by
the first five of those lines verbatim.
Those four tags count only where a line's tag sits, right after the `[ts] [LEVEL]` prefix: a line
that merely names one, as `[AnimMem]`'s `... [MissionPerf] is unaffected.`, is not that tag's line.
Under each row, every flag prints its evidence, and every other `[Tag] key=value` line between the
mission's start and the next mission's start or the next game's initialization (a later instrument
such as `[TickSummary]`, and the campaign map's lines after a battle) is counted per tag; `--json`
carries those lines in full as
`extra_tags`, with the flag evidence as `flag_evidence`. A value runs to the next space-led `key=`
outside brackets, so `[Doctrine]`'s `registered=[ShieldWall*1.00, Charge*0.30]` stays one value.
A bracketed group after a space joins the value before it (`[MapLoad]`'s per-kind counts
`[lord=64 ... other=78]` are part of `parties=`), so does a bare word or prose after a value, and a
bracket left open runs its value to the end of the line. A line without the logger's `[ts] [LEVEL]`
prefix (a multi-line entry's later lines) takes the timestamp of the newest prefixed line above it.
Prose lines, such as the tick profiler's `[TickProfiler]` status lines, are neither counted nor
unparsed. For example (lines cut to `...`):

```
log: taom_debug_2026-10-02_11-38-06.log size=372870B lines=3021 missions=4 unparsed=0
  tag [Engine]: 1 line(s) before the first mission
  tag [SaveLoad]: 2 line(s) before the first mission
  tag [MountSpawn]: 4 line(s) before the first mission
  tag [MapLoad]: 4 line(s) before the first mission
taom_debug_2026-10-02_11-38-06.log #2 CustomBattle battle_terrain_biome_148 windows=14/9 fps=117.0 ... flags=FRAME_CAP,MEMORY_PRESSURE,DIRTY_BUILD,BUILD_PAIR_MISMATCH
  FRAME_CAP: 6 of 9 steady windows within 1.0 fps of 117.1 fps; windows: t=+31s fps=117.1 active=83, ...
  MEMORY_PRESSURE: [2026-10-02 11:44:21] [INFO] [MemSample] privMB=10092 ... memLoad=84%
  DIRTY_BUILD: [BuildStamp] TAOM=v2.0.0.0 build.20261002-163736Z+bc39f6e4....dirty
  BUILD_PAIR_MISMATCH: [BuildStamp] TAOM and TAOM.Dependencies built 1d 02h 55m apart
  tag [MountSpawn]: 14 line(s)
  ...
```

A game initializes before its first mission, and what that writes (the `[LoadPhase]` steps, and
`[LoadXml]` and `[XmlMerge]` lines per module XML type, each with a per-game `summary`) belongs to
no mission. Every `[Tag] key=value` line before a game's first mission goes on the log's header, not
on a row: one `tag [LoadXml]: 27 line(s) before the first mission` line per tag in the text report
(in a log with several games, the lines before each game's first mission), and `extra_tags` on the
log in `--json`. The first of three kinds of line read while a mission is open is taken as the
start of the next game: the lifecycle trace's `[MapLoad] #N t=Nms STATE initialized: InitialState`
(the main menu, which the engine initializes only once the game before it is gone) or
`... STATE initialized: GameLoadingState` (what `MBGameManager.StartNewGame` pushes for a new
campaign, a saved game and a custom battle alike), a saved game's
`[SaveLoad] ... phase=LoadRequested` line, which the Load Game click writes before the load reads
anything, or a `[LoadPhase]`, `[LoadXml]` or `[XmlMerge]` line. The trace is
`Patch89_MapLoadDiagnostics_Lifecycle` (`Main/Features/MapLoadDiagnostics`): a build that applies it
writes both state lines, and no setting turns it off. It first shipped in v2.0.29, and the 1.4.5
branch has none. The three load tags are written only while a game initializes, but each line
follows the step it times (a `[LoadXml]` line comes when its XML type has finished loading, seconds
after the load began); the state lines and the request come earlier. A save's own phases
(`SaveBegin`, `SaveCompleted`) and the later phases of a load do not start a game. That
mission's row stops there (the campaign map after a battle counted toward it up to that line, and a
`[MemSample]` of the next game's load does not), and every line up to the next mission's start goes
on the header. The request is written before the load can fail, so a load that never completes ends
the row too: a Cancel at the module-mismatch question (`SandBoxSaveHelper.TryLoadSave` asks it when
a module other than the official ones differs between the save and the install, a TAOM version
change included) or a save that does not read leaves the player where the Load menu was opened. The
lines written from there up to the next mission go on the header, as `before the first mission`
lines, and the `[MemSample]` lines among them on no row. The Load item is on the campaign map's
escape menu and a mission's has none, so the click comes after the mission it ends, and the row
loses only the map's lines after it. A new campaign or a custom battle writes no load request, but
it starts from the main menu, so the row of the game before it stops at the menu's `InitialState`
line: the time in the menu goes on the header, the `[MemSample]` lines of the new game's load on no
row (the header holds no `[MemSample]` lines), and the row keeps what its game wrote before that
line, the teardown included. A log with none of these
lines has no boundary: each mission keeps every line after its start, as before.

A summary line, `[Tag] summary key=value ...` or `[Tag] summary: key=value ...` (`[AnimMem]`,
`[LoadXml]` and `[XmlMerge]` write them), is kept without its leading word, so its fields are the
line's own keys. A key named `summary`, or one that only starts with the word (`summaryMs=`), is not
skipped. A row's `hitches:` count adds up what its `[TickSummary]` lines report, each for the
`[Hitch]` lines above it back to the previous summary, because the profiler writes only the first
100 `[Hitch]` lines of a mission in full and counts every slow frame in the summary. Lines that no
readable summary covers count as themselves: those of a summary whose `hitches=` is missing or not a
whole number, those below the row's last summary, and all of them in a row with no summary. A row
with a readable summary and an unreadable one counts the readable one's `hitches=` plus the lines
the unreadable one would have covered, whichever comes first.
The phase breakdown and the worst frame always come from the `[Hitch]` lines that parsed, and the
clause in parentheses says how many that is whenever it differs from the count:
`hitches: 137 (100 parsed [Hitch] lines: agentTickMs x100)` has 37 slow frames without a line,
`hitches: 5 (no [Hitch] line parsed)` has none, and a row whose summary counts 0 but whose log holds
a line prints `hitches: 0 (1 parsed [Hitch] line: agentTickMs x1)`. No clause means every counted
hitch has its line. A `[Hitch]` line that did not parse is counted as unparsed in the header, not
as parsed. So is a `[TickSummary]` whose `hitches=` is missing or not a whole number (a line cut
before its first key, `[TickSummary] frames` or the tag alone, included: the profiler writes its
status text under `[TickProfiler]`, so a `[TickSummary]` line is always data), and the `[Hitch]`
lines it would have covered count as themselves.

Read the flags before the numbers:

| Flag | Meaning |
|---|---|
| `FRAME_CAP` | With four or more steady windows: at least half of them sit within 1 fps of one window's fps, or the windows with the most and the fewest active agents, 30% or more apart, ran within 1 fps of each other. A frame limiter (in game or in the driver) may have set the frame time; lift it and rerun |
| `MEMORY_PRESSURE` | The mission's `[PerfContext]`, or a `[MemSample]` line written between the mission's start and the next mission's start or the next game's initialization (so the campaign map after a battle counts toward that battle), read `memLoad` of 80% or more |
| `DIAG_ON` | `[PerfContext]` shows cost beyond the default diagnostics: `tickProfiler=on`, or a `diag=` token other than the seven that default on (`battleLoad`, `stallWatchdog`, `stallBundle`, `exitSampler`, `freezeSampler`, `memSampler`, `missionPerf`) |
| `DIRTY_BUILD` | The `[BuildStamp]` TAOM stamp ends in `.dirty`: the build held uncommitted edits, so say what was measured |
| `BUILD_PAIR_MISMATCH` | The `[BuildStamp]` line says `MISMATCH`: TAOM and TAOM.Dependencies were built more than 12 hours apart |

Exit codes: 0 when rows were found, 1 when no mission was found, 2 for a usage error, an unreadable
file or a refused compare.
