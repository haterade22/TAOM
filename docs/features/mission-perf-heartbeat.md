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
from the co-op settings fingerprint.

## Log line

```
[MissionPerf] t=+65s frames=300 fps=60.0 avgMs=16.67 p95Ms=25.50 maxMs=40.3 agents=812 active=640 formations=9 gc0=12 gc1=3 gc2=1
```

`t` is seconds since the mission was created; the first line lands at +5 s.

## Tick profiler (Patch97)

Off by default. When on, it says where a battle's frame time goes: milliseconds and main-thread
allocation per mission behaviour type, the engine phases managed code can see, and one line per
slow frame naming where that frame went. It is the number the rest of the 2026-10 performance
plans are judged by.

### What it measures

A frame is one mission tick, from one `Mission.OnPreTick` to the next (in fast-forward the engine
runs several mission ticks per rendered frame, so several profiler frames). Per frame:

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
finalizers on `Mission.OnTick`, `Mission.OnPreTick`, `Mission.Tick`, `MissionState.TickMissionAux`
and `MissionState.OnTick` (see PatchShield below),
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
| `Enable Tick Profiler` (`EnableTickProfiler`) | off | At the first game init, where Patch97 installs or is skipped, and again at each mission start. Turning it on needs a restart (`RequireRestart = true`, allowlisted in `SettingRequireRestartPostureTests`); turning it off stops measuring from the next mission, while the patches stay installed and call through until a restart |
| `Tick Profiler Top Behaviours` (`TickProfilerTopN`) | 8 (1 to 20) | At each mission start |
| `Hitch Threshold (ms)` (`HitchThresholdMs`) | 250 (50 to 2000) | At each mission start |

The provider validates both numbers (an out-of-range or non-finite value falls back to the
default, and a measured mission names each replaced value in one warning) and fails CLOSED to off when MCM is not ready, unlike its siblings, because the toggle
installs patches. All three are instrumentation and excluded from the co-op settings fingerprint.
A mission measures only when the profiler is installed, the toggle reads on at that mission's start
AND every hook the profiler needs is still in place (see Hook health).

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

**`[TickSummary]`**, INFO, once at the end of a measured mission that closed any frame (never when
the profiler is off; a measured mission that closed none writes a `[TickProfiler]` line saying so). Every closed frame of the mission (the sum of its windows, including the
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
| `[TickProfiler] off: 'Enable Tick Profiler' is off at game start (or MCM was not ready); no patches installed` | INFO | Once per process, toggle off |
| `[TickProfiler] install: category applied, Mission.OnTick sites 2/2, Mission.OnPreTick sites 2/2, allocation counter available` | INFO | Once per process, toggle on: `applied` or `failed`, the swaps made per method (the `OnPreTick` denominator is 1 when the wait delegate did not bind), `available` or `na` |
| `[TickProfiler] WaitTickCompletion could not be bound; waitTickMs reads 0 and the wait lands in otherMs` | WARNING | At install, when the private method did not bind |
| `[TickProfiler] Mission.OnTick: MissionBehavior.OnMissionTick matched 0 times, expected 1; Mission.OnTick left vanilla, so no mission is measured` | WARNING | At install, or whenever a later patch on the method reruns the transpilers (another mod's patch, an earlier transpiler that took the anchor): a call site not found exactly once (an engine bump). For `Mission.OnTick` the next mission start does not measure, and a mission already running stops on its next tick (see Hook health). For `Mission.OnPreTick` the consequence reads `so waitTickMs and preTickMs read 0 and that time lands in otherMs`: the frame boundary is a prefix, so measuring continues |
| `[TickProfiler] Mission.OnTick: helper MissionTickProfilerHooks.TimedMissionTick does not fit MissionBehavior.OnMissionTick; Mission.OnTick left vanilla, so no mission is measured` | WARNING | At install, a helper whose shape does not match its target (same consequences) |
| `[TickProfiler] Mission.OnTick rewrite failed: InvalidOperationException: ...; Mission.OnTick left vanilla, so no mission is measured` | WARNING | At install, a transpiler that threw (same consequences) |
| `[TickProfiler] on in MCM but it was off at game start, so no patches are installed and nothing is measured; restart the game to measure` | WARNING | Once per mission, toggle on but never installed (switched on after game start) |
| `[TickProfiler] on in MCM but the install at game start failed, so nothing is measured; see the [TickProfiler] install line and [PatchApply]` | WARNING | Once per mission, toggle on but the install failed |
| `[TickProfiler] mission 3: not measuring, 'Enable Tick Profiler' is off in MCM; its patches stay installed and only call through until a restart` | INFO | Once per mission, installed but switched off since game start |
| `[TickProfiler] mission 3: not measuring, required hooks missing: Mission.OnTick call sites 0/2, Mission.OnPreTick frame-boundary prefix (Patch97); another mod's transpiler, a PatchShield strip or a failed patch apply left them out, and the next mission checks again` | WARNING | Once per mission, installed and toggled on, when a hook the profiler needs is not in place at the mission's start. The names are `Mission.OnTick call sites n/2` (a rewrite found fewer anchors), `Mission.OnTick transpiler (Patch97)`, `Mission.OnPreTick frame-boundary prefix (Patch97)` and `Mission.TickAgentsAndTeamsImp prefix (Patch91)` or `finalizer (Patch91)`, all the missing ones in one line; a name followed by `(not resolved)` or `(patch info unreadable: <exception type>)` could not be checked and counts as missing |
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
`callvirt MissionBehavior::OnPreMissionTick`; a prefix on `Mission.OnPreTick` is the frame
boundary. Each swap is stack-identical (the helper takes the instance first) and must match
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
`Mission.OnTick` transpiler (behaviour time), its `Mission.OnPreTick` frame-boundary prefix (no frame closes without it)
and Patch91's agent-tick prefix and finalizer on `Mission.TickAgentsAndTeamsImp` (`agentTickMs`). The `Mission.OnPreTick`
transpiler is not: without it `waitTickMs` and `preTickMs` read 0 and that time lands in `otherMs`, which the mission
header's `Mission.OnPreTick n/m` already says. A problem at the start means the mission does not measure and writes one
aggregated warning; the next mission checks again, so a hook that came back measures again. A hook that cannot be
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
- **A PatchShield strip of a required patch during a mission is found at the next mission start**, not before. Until
  then the mission keeps measuring with the hook gone: a stripped `Mission.OnTick` transpiler zeroes `preDisplayMs` and
  `missionTickMs`, and those two phases leave the per-behaviour list; a stripped frame-boundary prefix leaves empty
  windows.
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

`Mission.TickAgentsAndTeamsImp` is on `PatchShieldPolicy.ExcludedTargetMethods` for every player,
profiler on or off; `Mission.OnTick` and `Mission.OnPreTick` are not. PatchShield attaches a
finalizer to every patched method it does not exclude. It swallows a MissingMethod, MissingField or
TypeLoad exception that escapes the method and strips patches; every other exception is rethrown.

**`Mission.TickAgentsAndTeamsImp` is excluded so that a swallow cannot skip `tickCompleted = true`, and that is all the
exclusion buys.** It carries Patch91's bracket for every player. With a finalizer on it, a swallowed exception from the
agent or team ticks would skip `tickCompleted = true` (`Mission.cs:3629`), and the next `WaitTickCompletion` would spin
forever. Excluded, the exception leaves the method instead. On the asynchronous call it reaches the native job thread:
the generated shim `Mission_TickAgentsAndTeams` has no catch (v1.5.3 `ManagedCallbacks.CoreCallbacksGenerated.cs:936-940`),
and what native does with the exception is UNVERIFIED. On the inline call, which fast-forward makes
(`MissionState.TickMission` passes `asyncAITick: false`, `MissionState.cs:178-196`), it reaches `Mission.OnTick`, which
carries the hazard below. What is given up, for any owner's patch on it: the swallow of a missing-API exception and the
strip of the offending patch. PatchShield writes one diag.log line per patched method it skips this way, naming the
method, its patch owners and what is given up.

**`Mission.OnTick` and `Mission.OnPreTick` are not on the list** (decision D13, 2026-10-03). Plan 028 first excluded them
under the house rule for per-frame targets, and the maintainer took them off. `Mission.OnTick` is patched for every
player (Patch35's postfix) and by the profiler's transpiler; TAOM patches `Mission.OnPreTick` only through Patch97, and
any patch on it attaches the shield. Each runs once per frame on the main thread, so the finalizer's per-call
cost is two calls a frame, and plan 034 takes the per-call method lookup off the shield's no-exception path. Excluded, a
foreign mod's load-time patch on `Mission.OnTick` is never rescued. In a process's first game, where no shield sits
higher up, its missing-API throw unwinds the whole application tick (that frame skips every module's
`OnApplicationTick`, `JobManager.OnTick`, the avatar services and `Game.OnTick`'s game handlers), and a postfix that
throws after `Mission.OnTick` has ended the mission keeps `MissionState.OnTick` from popping it, because that pops a
finished mission only after `TickMission` returns, so the battle never closes. Shielded, pass 2 sees that patch, and the
finalizer swallows the throw and strips every unprotected owner's prefixes, postfixes and transpilers on that method, not
only the offending patch's.

**The hazard no list entry fixes (older than the profiler, not fixed here).** `Mission.OnTick` clears `tickCompleted`
(`Mission.cs:3756`) before its `OnMissionTick` loop, and only `TickAgentsAndTeamsImp` sets it again (`:3629`), after the
loop and the dynamic-entity and spawned-item code. An exception that escapes `OnTick` between the two and is swallowed by
a finalizer above it leaves `tickCompleted` false, so the next `OnPreTick` spins in `WaitTickCompletion` (`:3601-3606`)
for good and the battle freezes with no exception. What can throw there: any module's behaviour in its `OnMissionTick`
(no patch is needed), a patch or transpiled call in that stretch, and the inline agent tick. Who swallows: PatchShield's
finalizer on `Mission.OnTick` (a missing-API exception only) and Patch37's crash capture on `Module.OnApplicationTick`
(any exception, while capture is on: `CrashReportPatchHelper.HandleAndSwallow`). A throw before the clear (a prefix) or
after the agent tick is launched (a postfix) does not leave the flag false. So the list does not decide this: excluded or
shielded, an interrupted tick leaves the flag false. A throw before the clear still costs the frame: one that no patch
made (a behaviour's `OnPreDisplayMissionTick`, `Mission.cs:3750`) and that a finalizer swallows skips the rest of
`OnTick`, the agent tick launch included, on every frame it recurs. PatchShield writes a diag.log line for each
missing-API swallow (the line is not deduplicated); Patch37's capture logs a recurring throw at occurrences 1, 2, 10, 100
and so on (`CrashBundleThrottle.IsLoggedOccurrence`), not every frame. Re-shielding `Mission.OnTick` (D13) leaves the
hazard where trunk already had it, because trunk shields `Mission.OnTick` too (Patch35's postfix gives PatchShield
something to attach to, from a process's second game start). The PatchShield follow-up plan (the culprit-only strip,
FOLLOW-UP L1, plus a completion-aware recovery) takes it, with regression tests for an exception before the clear,
between the clear and the agent-tick launch, and inside the inline agent tick (Codex review, 2026-10-03, finding 1). It
takes a second, related hazard too (convergence round 3, finding R3-1): a swallowed foreign prefix throw on
`Mission.OnPreTick` skips that method's whole body, whose first call is `WaitTickCompletion` (`Mission.cs:3548`), so that
frame's `OnTick` can run while the previous agent tick still runs (consequence UNVERIFIED). Trunk shields a foreign patch
on it the same way, so that is older than the profiler as well.

When the finalizer is there: pass 1 runs at module load and pass 2 at the end of every game
initialisation, and a pass attaches only to what is patched by then and was not seen before. Patch35
and Patch97 apply after pass 2 of a process's first game start, so TAOM's own patches put the
finalizer on `Mission.OnTick` (and, with the profiler on, `Mission.OnPreTick`) from a process's
second game start; in a first game it is there only if another mod patched the method before a pass
ran. The finalizer's cost lands in `otherMs`, with the finalizers on `Mission.Tick` (Patch37, from
the first game start), `MissionState.TickMissionAux` (Patch91) and `MissionState.OnTick` (Patch43,
both from a second game start).

**What keeping the shield on `Mission.OnTick` risks for TAOM's own patches.** The strip is not
culprit-only: PatchShield cannot tell which owner's patch threw, so it removes the prefixes,
postfixes and transpilers of every owner on the rescued method that is not on its protected list,
and TAOM's owner `com.taom.mod` is not on it (FOLLOW-UP L1 in the review record). If the shield ever
strips patches on `Mission.OnTick`, TAOM's own patches there (Patch35's postfix, Patch97's
transpiler) are stripped too, and a strip on `Mission.OnPreTick` takes Patch97's prefix and
transpiler, until the planned culprit-only fix. diag.log shows the swallow, then one
`unpatched owner` line per stripped owner.

`MissionTickProfilerBindingTests` walks the real targets in both directions
(`AgentTickTarget_IsOnPatchShieldsExclusionList` and
`TickAndPreTickTargets_AreNotOnPatchShieldsExclusionList`), and `PatchShieldPolicyTests` pins the two
tick methods off the list without a game. The analysis behind the first exclusion decision is in
`docs/reviews/deep-review-028-mission-tick-profiler-2026-10-02.md`, convergence round 2.

## Key Files

| File | Purpose |
|---|---|
| `Main/Features/MissionPerf/FrameStats.cs` | Pure window: record, should-emit, emit |
| `Main/Features/MissionPerf/MissionPerfLine.cs` | The line format |
| `Main/Features/MissionPerf/Hooks/MissionPerfHeartbeatBehavior.cs` | The `MissionLogic` |
| `Main/Features/BattleLoadDiagnostics/BattleLoadDiagnosticsSettings.cs` | The heartbeat toggle and the three tick profiler settings |
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
| `Dependencies/Foundation/PatchShieldPolicy.cs` | The agent-tick PatchShield exclusion and the diag.log skip line |

## Tests

`TAOM.Tests/Features/MissionPerf/FrameStatsTests.cs`: cadence, average, nearest-rank p95,
single sample, empty window (zeros, not NaN), window reset, bounded sample set, clock reset,
line format.

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
PatchShield walks: the agent tick excluded, the two tick methods not), `MissionTickProfilerWiringTests` (the
`SubModule.cs` pins: the behaviour beside the heartbeat, and the install behind the once-per-process early return, with
mutation tests showing the pin fails without it).
`BattleLoadDiagnosticsSettingsProviderTests` pins the three settings' defaults and validation.

## Reading an A/B

Take the lines from 30 s after both sides are AI-controlled (F6) to the first rout, compare the
median `avgMs` and `p95Ms` between the two runs; `gc2` should not climb faster with the feature
on. Three runs per cell is the floor, AI battles vary.
