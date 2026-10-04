# Map Perf Profiler

## Overview

The Patch101 campaign map profiler splits the campaign map's frame into the parts TAOM can see and
attributes them. It is **off by default** and installs no patch unless "Enable Map Profiler" is on at the
first game start. On, it writes a `[MapProfile]` line to the TAOM debug log every 5 s of wall clock while
the map ticks and a `[MapProfileSummary]` line when the campaign session ends. Each line splits the frame
into the campaign tick, the campaign tick-event dispatch (per listener, TAOM's and vanilla's), the map
screen (with TAOM's map views per type), TAOM's application tick and everything else, with main-thread
allocation. It is the map's counterpart of the Patch97 mission tick profiler
([mission-perf-heartbeat.md](mission-perf-heartbeat.md)) and reuses its allocation counter, per-type
table, call-site transpiler and line conventions.

## Why This Exists

- **Observation:** on the maintainer's desktop a new campaign ran at 7 to 8 fps in fast-forward while
  lords and villagers spawned in, with `Campaign.RealTick` averaging 20 ms per frame
  (`docs/migration/v1.5.2-impact.md`, the "Time running" row of the v1.5.2 evidence table). RealTick
  explains about 20 of each frame's ~130 ms.
- **The gap:** the `[MapLoad]` heartbeat ([map-load-diagnostics.md](map-load-diagnostics.md)) writes
  `fps` and `tickMs` (the mean `Campaign.RealTick` time) every 5 s; it cannot split the rest of the frame
  or say how much of it is TAOM's code: eight `CampaignEvents.TickEvent` listeners, three map views,
  `SubModule.OnApplicationTick`, and Harmony patches on the per-frame map methods.
- **Use:** plan 037's campaign hot-path fixes are judged by these numbers; a slower CPU and the spawn-in
  phase are where the cost shows (the steady map on the desktop runs near 170 fps in fast-forward).

## Architecture

### The frame model

One frame is one application tick in which `MapState.OnTick` ran. `Module.OnApplicationTick` reaches
`GameStateManager.OnTick` (and so `MapState.OnTick`, at most once, only while the map state is active and
not disabled) BEFORE every submodule's `OnApplicationTick`, so between two consecutive `MapState.OnTick`
calls of an unbroken run of map frames TAOM's `OnApplicationTick` runs exactly once. The
`MapState.OnTick` prefix is the frame boundary.

A frame closes into the window and the session only when (1) a previous boundary exists in this session,
(2) at the frame's START boundary the loading window was down (`LoadingWindow.IsLoadingWindowActive`)
and the map screen was the top screen (`ScreenManager.TopScreen is MapScreen`), and (3) TAOM's
`OnApplicationTick` ran exactly once since that boundary. Otherwise the frame is discarded (its phase
sums and entry calls dropped) and counted as `loading`, `notTop` or `gap`. A gap is any break in
consecutive map ticks: the escape menu, a party or inventory screen, a battle. A save from the map is
usually not a gap: `MapState.OnTick` runs `SaveHandler.SaveTick` and returns while saving, and the saving
code raises no loading window, so those frames close with the save work inside `mapStateMs` and no campaign
tick (whether a save's UI takes the top screen is UNVERIFIED).

Per closed frame: `frameMs` (boundary to boundary), the phase sums, main-thread `allocBytes` (boundary
to boundary), `taomMs` and `otherMs = max(0, frameMs - mapStateMs - mapScreenMs - appTickMs)`. With no
clamping, `wallMs = mapStateMs + mapScreenMs + appTickMs + otherMs` for every window. Entry calls and
phase sums reach the window and the session only when their frame closes, so every total covers the same
closed frames.

### Targets and nesting

| Target | Patch | Measures |
|---|---|---|
| `MapState.OnTick(float)` | prefix `Priority.First` (frame boundary, then stamp); postfix `Priority.Last` | the boundary; `mapStateMs` |
| `Campaign.RealTick(float)` (internal) | prefix `First` / postfix `Last` | `realTickMs`, inside `mapStateMs` (Patch89's heartbeat postfix included) |
| `Campaign.Tick()` (internal) | prefix `First` / postfix `Last` | `campaignTickMs`, inside `mapStateMs` |
| `CampaignEvents.Tick(float)` | transpiler: the one `callvirt MbEvent<float>::Invoke(float)` becomes `call MapFrameProfilerHooks.TimedTickEvent` | `tickEventMs`, inside `campaignTickMs`, and one entry per listener owner type |
| `MapScreen.OnFrameTick(float)` | prefix `First` / postfix `Last` | `mapScreenMs` (Patch36's and Patch89's postfixes included) |
| `TAOM.SubModule.OnApplicationTick(float)` | prefix `First` / postfix `Last` | `appTickMs` and the continuity count |
| each TAOM `MapView` subclass's own `OnFrameTick`, `OnMapScreenUpdate`, `OnMenuModeTick` or `OnIdleTick` | prefix `First` / postfix `Last` (views category) | one entry per view type |

Nesting: `realTickMs` and `campaignTickMs` are inside `mapStateMs`; `tickEventMs` is inside
`campaignTickMs`; listener entries are inside `tickEventMs`. View entries depend on the override: an
`OnMapScreenUpdate` entry is inside `mapScreenMs` (TAOM's three views today), an `OnFrameTick` or
`OnMenuModeTick` entry is inside `mapStateMs` (reached through `MapState.OnTick`), and an `OnIdleTick`
entry never lands in a closed frame (the map state is idle, so the frame is a gap). Prefix and postfix,
never a finalizer: a call that throws records nothing for that call, and Patch101's own patches change no
exception path (the two PatchShield exclusions below do, with the profiler on).

`Campaign.TickMapTime` gives Stop and FastForwardStop no campaign time, and gives StoppablePlay and
StoppableFastForward campaign time only while the main party is not waiting. The speed class reads the
engine's simplified mode (see "Speed class"), which turns a waiting Stoppable mode into Stop, so a frame
classed `Play` or `FF` advances campaign time, and a `Stop` frame can be a waiting frame that still ticks
the map.

### The TickEvent listener walk

The brief left open how to attribute the listeners. Per-listener patches would cover TAOM's listeners
only, would target a compiler-generated lambda (`RealmBordersCampaignBehavior`) whose name changes with
any edit, and would miss every listener added later. A transpiler on `MbEvent<float>.InvokeList` would
patch a private method of a generic type shared with `_missionTickEvent`, small enough that an inlined
copy could bypass it. Instead the single, non-generic, virtually dispatched call site in
`CampaignEvents.Tick` is swapped with plan 028's tested `TickProfilerTranspiler.Rewrite` (soft-fail,
exactly one match), and the helper either calls `Invoke` unchanged (not measuring, or the walk not
bound) or walks the same list itself, reproducing `InvokeList` statement for statement: read the head
once; for each record read `Action` and `Owner`, time the call inside `try`/`finally`, then read `Next`
AFTER the call. Order (newest first), exception propagation (nothing catches; the throwing listener's
call is still recorded), self-removal (the Messenger listener's `ClearListeners(this)` continues to its
old successor) and a listener added during the dispatch (it waits for the next one) are identical.
`TickEventListenerWalkerTests` runs vanilla `Invoke` and the walk over identical lists and compares the
call sequences; `MapFrameProfilerBindingTests` pins the IL shape of both `Invoke` (one read of the
non-serialized list, one call of `InvokeList`, no branch, no handler) and `InvokeList`. Each listener is keyed by
its owner's type (the action's declaring type when the owner is null); TAOM-owned means declared in
TAOM's assembly. The four members are bound once at install with Harmony field-reference delegates.

### Sessions and windows

A session is one `Campaign` instance, held only through a `WeakReference`. The boundary compares
`Campaign.Current` with it; on a new campaign it first closes the previous session (reason
`newCampaign`), then opens the new one: it reads the toggle, the shared top-N and the two fast-forward
multipliers, with the toggle on looks again at the six core hooks (see "Hook health"), resets every accumulator
and logs the session line (or the hooks-lost warning). `SubModule.OnGameEnd` closes the session
with reason `gameEnd` (an in-campaign load can skip `OnGameEnd`, which the `newCampaign` path covers; a
process killed from the map writes no summary, its windows are already in the log). A measuring session looks at the
six core hooks before either close, and a lost one turns the summary's reason into `hooksLost`.

A window line is due every 5 s of wall clock from the session's first boundary, checked at boundaries
only, including windows in which every frame was skipped (`frames=0`, which records a long loading
window, for example).

### Hook health

PatchShield strips by owner: after a swallowed MissingMethod, MissingField or TypeLoad throw on a shielded
method it removes every non-protected owner's prefixes, postfixes and transpilers on that method, and the
profiler's owner `com.taom.mod` is not protected, so Patch101's pair on `MapState.OnTick`, `Campaign.RealTick`
or `MapScreen.OnFrameTick` goes with the rest. Nothing reinstalls it. The install check ran once, so without a
second look a stripped `RealTick` pair would leave windows that read `realTickMs=0.00` and look measured.

The installer therefore hands `MapSessionHooks.LostHooks` the same patched check it installed with
(`MapProfilerTargets.UnpatchedCore`: the core targets that do not resolve or carry no `*_MapProfiler_Patch`),
and the session hooks run it at each session start (toggle on), at each window start, right after the window
line is written, and before the summary of a measuring session, never per frame. A required hook found unpatched
stops measuring that campaign session behind
ONE `[MapProfiler] session N: required hooks no longer patched (...)` warning that names every lost hook, and
the frames closed so far follow as a `reason=hooksLost` summary. The window line written just before the
warning is kept: the strip time is unknown, so it can hold up to one window of frames measured after the
strip, which the warning's "no longer sound" covers. At a session end the look comes first: a lost hook gives the
warning for that session and a summary whose reason is `hooksLost` in place of `gameEnd` or `newCampaign` (on a new
campaign the new session's own look at its start follows, and warns again, for the new session, while the hook is
still lost). A session that starts with a hook already lost opens
without measuring, with the warning in place of its start line; the next session looks again and warns again,
because nothing reinstalls the hook, and a restart is the way back.

Not covered: a strip on `MapState.OnTick` takes the frame boundary, the code that runs the window check, so no
window or session start follows and the lines stop. Nothing clears `Measuring`, so the other five hooks and the views
keep paying the full measuring cost every frame, the per-listener walk included, until the session ends; the session
end's look then finds the strip and writes the warning and a `reason=hooksLost` summary of the frames closed before it.
diag.log's `swallowed` and `unpatched owner 'com.taom.mod'` lines are the record. A check from a hook the shield cannot
reach (TAOM's own `SubModule.OnApplicationTick`) would find the strip within a window and stop that cost at once, and
was not built. The views are not checked: they are TAOM's own methods, which PatchShield never shields.

### Speed class

Read at each boundary for the frame it starts, from `ITimeControlAdapter`: the multiplier, and the mode as
`SimplifiedTimeControlMode`, which is `Campaign.GetSimplifiedTimeControlMode()` (decision FOR-MIKE 16r, after the
Codex review). That is the engine's own simplification: StoppablePlay and StoppableFastForward read `Stop` while
the main party is waiting, which is when `TickMapTime` gives the frame no campaign time; the party-wait
fast-forward reads as the plain unstoppable one and FastForwardStop as Stop. Classes: `Stop`, `Play`
(UnstoppablePlay, and StoppablePlay while the party moves), and for the fast-forward modes `FF` when the
multiplier is at most the MCM fast-forward multiplier, `FF2` when at most the extra fast-forward multiplier,
`FF3` above (Ctrl+Space turbo or any larger value). An unknown mode, or a non-finite multiplier in a
fast-forward mode, prints `na`. The waiting flag is the one the previous frame's `TickMapTime` stored, so the
frame in which the party starts or stops waiting is classed one frame late. A window's `speed` is the class with
the most closed frames, ties to the faster class. The two multipliers are read at session start and again at each
window line, never per frame.

### Install

`MapFrameProfilerInstaller.OnGameInitialized` is called on EVERY `OnGameInitializationFinished`, before
SubModule's once-per-process guard. Its first call reads the toggle: off, it logs one line and applies
nothing; on, it binds the listener walk, applies the two categories (`Patch101_MapFrameProfiler` and
`Patch101_MapFrameProfiler_Views`, separate so a failing view cannot stop the core), verifies each of the
six core targets is patched, creates the profiler only when the core category applied and nothing is
missing, and logs one install line. It never applies anything again. A later call (a second campaign in
the process) only reports: toggle on but off at the first game start gives the restart line; toggle on
but the install failed gives the not-installed line. It also hands the session hooks the patched check it
installed with, which they run again at each session start, each window start and each measuring session end (see
"Hook health").

## Configuration

MCM, Battle Load Diagnostics page, group "Map Performance":

| Setting | Default | Effect |
|---|---|---|
| Enable Map Profiler | off | `RequireRestart = true`: read at the first game start, where Patch101 installs or is skipped. Turning it on needs a restart; turning it off stops measuring from the next campaign session while the patches stay until a restart |
| Tick Profiler Top Behaviours (Mission Performance, shared) | 8 | how many entries each `[MapProfile]` and `[MapProfileSummary]` line lists, read at each campaign session start |

The toggle is co-op excluded (instrumentation, `CoopSettingsRelevance.Instrumentation`).

## Log lines

Every line is built in `MapProfileLines`; `MapProfileLinesTests` pins each one literally. Invariant
culture, ms with two decimals, KB as whole bytes / 1024 or `na` when the allocation counter is
unavailable, `t` as whole seconds since the session's first boundary, `top=none` when no entry ran, and
`top=` last because its value holds commas. An entry is `<Type>:<ms>/<calls>/<maxMs>/<KB>`. Nothing is
logged per frame.

### `[MapProfile]` (INFO, every 5 s of wall clock while the map ticks)

```
[MapProfile] t=+65s frames=300 wallMs=5000.00 realTickMs=400.50 mapScreenMs=1200.25 otherMs=2283.75 allocKB=3072 speed=FF parties=2091 mapStateMs=1500.75 campaignTickMs=950.40 tickEventMs=310.20 appTickMs=15.25 taomMs=115.85 maxFrameMs=48.30 skipped=2 gc0=3 gc1=1 gc2=0 top=FieldCommissionBehavior:60.10/300/1.25/256,RealmBordersMapView:40.50/300/0.90/64
```

| Field | Meaning |
|---|---|
| `t` | seconds since the session's first frame boundary |
| `frames` | closed frames in the window |
| `wallMs` | the closed frames' wall time, boundary to boundary |
| `realTickMs` | `Campaign.RealTick`, inside `mapStateMs`; it starts with `WaitAsyncTasks`, the main thread waiting for the previous frame's party AI task (started from `MapScreen.OnPostFrameTick`, outside every bracket) |
| `mapScreenMs` | `MapScreen.OnFrameTick` |
| `otherMs` | per frame `max(0, frameMs - mapStateMs - mapScreenMs - appTickMs)`: rendering, the native engine, UI, the other submodules' application ticks and the profiler's own boundary work. Once per window that includes building and writing the `[MapProfile]` line (a synchronous flushed write) and the hook check that follows it (see "Hook health"), so their time lands in the next window's `otherMs` and `maxFrameMs` and their allocation in the next `allocKB` (and any collection they cause in the next window's `gc` deltas). The looks at a campaign change (the previous session's, before its `newCampaign` summary, and the new session's own) land the same way in the new session's first frame. What one look costs is UNVERIFIED (see "Cost"). The listener walk's per-listener bookkeeping is inside `tickEventMs` (and so `campaignTickMs` and `mapStateMs`), not here |
| `allocKB` | main-thread allocation over the closed frames, or `na` |
| `speed` | the class with the most closed frames (`Stop`, `Play`, `FF`, `FF2`, `FF3`, `na` when none closed), from the engine's simplified time-control mode: a Stoppable mode with the main party waiting reads `Stop`, and such frames still tick the map |
| `parties` | `Campaign.Current.MobileParties.Count` at the window, `na` when unreadable |
| `mapStateMs` | `MapState.OnTick` |
| `campaignTickMs` | `Campaign.Tick`, inside `mapStateMs` |
| `tickEventMs` | the `CampaignEvents.TickEvent` dispatch, inside `campaignTickMs` |
| `appTickMs` | TAOM's `SubModule.OnApplicationTick` |
| `taomMs` | TAOM-owned listener and view entries plus `appTickMs`: a floor on TAOM's share. It leaves out TAOM's patches inside the brackets (Patch89 in `realTickMs` and `mapScreenMs`, Patch36 in `mapScreenMs`, Patch43 in `mapStateMs`), TAOM's hourly and daily listeners inside `campaignTickMs`, and TAOM's model overrides |
| `maxFrameMs` | the slowest closed frame |
| `skipped` | frames discarded in the window (loading, not top, or gap) |
| `gc0`, `gc1`, `gc2` | collection-count deltas since the previous window (the session start for the first) |
| `top` | the slowest entries (tick listeners by owner type, TAOM map views by type), at most the top-N |

### `[MapProfileSummary]` (INFO, once per measuring session with at least one closed frame)

```
[MapProfileSummary] reason=gameEnd session=1 frames=9000 wallMs=150000.00 realTickMs=12000.50 mapScreenMs=36000.25 otherMs=68500.00 allocKB=102400 mapStateMs=45000.75 campaignTickMs=28000.00 tickEventMs=9300.50 appTickMs=499.00 taomMs=3600.25 maxFrameMs=912.40 windows=30 skippedLoading=180 skippedNotTop=12 skippedGap=7 byspeed=Stop:3000/50000.00,FF:6000/100000.00 top=FieldCommissionBehavior:1800.50/9000/2.50/7680,RealmBordersMapView:1300.75/9000/1.75/1024
```

The same phase fields over every closed frame of the session, plus `reason` (`gameEnd` or
`newCampaign`, `fault` for the frames that closed before a fault stopped measuring, or `hooksLost` for the
frames that closed before a core hook was found stripped), `session` (the
session number in the process), `windows` (window lines written),
`skippedLoading`, `skippedNotTop`, `skippedGap` (discarded frames by reason), `byspeed` (closed frames
and wall ms per speed class with frames, in the order Stop, Play, FF, FF2, FF3, na) and `top` over the
whole session.

### `[MapProfiler]` status lines

The status tag contains no data tag, and no status line starts its body with `key=value`, so plan 029's
log tool reads them as prose.

| Line | Level | When |
|---|---|---|
| `[MapProfiler] off: 'Enable Map Profiler' is off at game start (or MCM was not ready); no patches installed` | INFO | first game start, toggle off (every player gets this line once per process; see "Cost" for what else runs with the toggle off) |
| `[MapProfiler] on in MCM but it was off at the first game start, so no patches are installed and nothing is measured; restart the game to measure` | INFO | a later game start after the toggle was turned on |
| `[MapProfiler] on in MCM but the install at the first game start failed, so nothing is measured; see the [MapProfiler] install line and [PatchApply]` | WARNING | right after the install line of a failed install, and at each later game start with the toggle on |
| `[MapProfiler] install: category applied, views category applied, targets 6/6 patched (missing none), MapView overrides 3/3 patched, CampaignEvents.Tick sites 1/1, listener walk bound, allocation counter available` | INFO | the install, everything found; otherwise `failed`, the missing target names (`MapState.OnTick`, `Campaign.RealTick`, `Campaign.Tick`, `CampaignEvents.Tick`, `MapScreen.OnFrameTick`, `SubModule.OnApplicationTick`), `unbound`, `na` |
| `[MapProfiler] TickEvent listener walk not bound (<detail>); tickEventMs still times the whole dispatch, but no listener is attributed and taomMs leaves the TAOM listeners out` | WARNING | a walk member missing or of another type |
| `[MapProfiler] CampaignEvents.Tick left vanilla (sites 0/1); tickEventMs reads 0, no listener is attributed, and the dispatch stays inside campaignTickMs` | WARNING | installed, but the transpiler did not swap the call site |
| `[MapProfiler] 1 of 3 TAOM map view overrides patched; the others run unattributed inside mapScreenMs` | WARNING | installed, but not every view override is patched |
| `[MapProfiler] session 1: measuring, top 8 entries per line, a window line every 5 s of wall clock while the map ticks, speed classes FF up to 4x, FF2 up to 8x, FF3 above` | INFO | each measuring session start |
| `[MapProfiler] session 1: not measuring, 'Enable Map Profiler' is off in MCM; the patches stay installed and only call through until a restart` | INFO | a session start with the toggle turned off mid-process |
| `[MapProfiler] session 1: required hooks no longer patched (Campaign.RealTick, MapScreen.OnFrameTick), most likely stripped by PatchShield after a swallowed exception (see the 'swallowed' and 'unpatched owner' lines in diag.log), so the numbers would no longer be sound: measuring stopped for this campaign session, and nothing reinstalls the hooks until a restart` | WARNING | a session start (in place of its start line), a window start (after the window line, then the `reason=hooksLost` summary) or the end of a measuring session (before its summary, which then reads `reason=hooksLost`) where a core hook is no longer patched, once per session, naming every lost hook |
| `[MapProfiler] MCM 'Tick Profiler Top Behaviours' reads 0, out of range, so 8 is used for the map lines` | WARNING | a measuring session start whose shared top-N was out of range (a hand-edited settings file) |
| `[MapProfiler] session 1 end (gameEnd): no frame closed while measuring, so no summary` | INFO | a measuring session that closed no frame |
| `[MapProfiler] frame boundary failed, measuring stopped for this campaign session: InvalidOperationException: (x)` | ERROR | a fault at the boundary or at `session end`, once per campaign, the message's brackets turned into parentheses; the frames already closed follow as a `reason=fault` summary, unless the fault was in writing a summary (session end, or closing the previous campaign): that summary is lost, because the retry runs the same code over the same state; that campaign is not measured again |
| `[MapProfiler] install failed, nothing is measured in this process: InvalidOperationException: (x)` | ERROR | the install threw; no profiler exists until a restart |

Plan 028's transpiler can also write a `[TickProfiler]`-tagged warning for `CampaignEvents.Tick` (for
example `[TickProfiler] CampaignEvents.Tick: MbEvent`1.Invoke matched 0 times, expected 1; ...`) beside
the `[MapProfiler]` consequence line.

### What is NOT covered

- Vanilla map views individually: `MapScreen` ticks its views through compiler-generated closures, so
  their time is inside `mapScreenMs` (or `mapStateMs` for their `OnFrameTick` and `OnMenuModeTick`).
- Other mods' patches and TAOM's own patches inside the brackets: Patch89's census runs inside
  `realTickMs` on its emit frames; Patch36's and Patch89's `MapScreen.OnFrameTick` postfixes are inside
  `mapScreenMs`.
- The periodic events and hourly ticks inside `Campaign.Tick`, which count only as part of
  `campaignTickMs`.
- Worker-thread allocation: the counter is per thread, read on the main thread.
- Another mod's patch on `MbEvent<float>.Invoke` or `InvokeList`: while measuring with the walk bound, the
  TickEvent dispatch calls neither, so such a patch does not run for `CampaignEvents.TickEvent` (only while
  measuring; whether any mod patches them is UNVERIFIED).
- Crash attribution: once Patch101 is installed, a TickEvent listener's exception, vanilla or another
  mod's, carries `MapFrameProfilerHooks.TimedTickEvent` (and while measuring `InvokeTimed`) on its stack, so
  a crash reporter that blames the first mod frame could blame TAOM (UNVERIFIED how each reporter attributes).

## Cost

- **Off (the default):** no Patch101 patch exists and no per-frame profiler work runs, so no frame pays
  anything. The lifecycle cost remains for every player: one `[MapProfiler] off:` line per process; at each
  game init four singleton resolves, one delegate allocation (`TryPatchCategory`, handed to the installer)
  and at most one toggle read; at each game end one null check. The two `PatchShieldPolicy` exclusions
  (`Campaign.Tick`, `CampaignEvents.Tick`) are unconditional too, so with the profiler off they still take
  the shield off any other mod's patch on those two methods (see "PatchShield"); nothing TAOM ships patches
  either.
- **On and measuring:** per frame two timestamps per phase bracket (five), one boundary (five engine
  reads: `Campaign.Current`, the loading window, the top screen and the two time-control values; one
  allocation read), per listener two timestamps, two allocation reads and one dictionary
  lookup, per TAOM view the same. No allocation after a type's first sighting, no lock, nothing logged
  per frame. Once per window (5 s of wall clock), at each session start and at the end of each measuring
  session, off the per-frame path: six `Harmony.GetPatchInfo` lookups for the hook check, plus, once per
  window, the line's synchronous write. Both run after the boundary's timestamp is taken, so they sit inside a
  measured frame: a window start's write and check in the next window's first frame, and the looks at a campaign
  change in the new session's first frame, each landing in that frame's `otherMs` and `maxFrameMs` and in `allocKB`
  (the look at a `gameEnd` ends the session, so it lands in no closed frame). What one look costs is UNVERIFIED, not
  measured: each `GetPatchInfo` is a `BinaryFormatter` deserialization of the method's patch info, and each patch the
  check inspects is resolved by a scan of the loaded modules (`AccessTools.GetMethodByModuleAndToken`).
- **On but not measuring** (toggle turned off mid-process, or after a fault): every patch calls through
  after a null and a flag check; the boundary reads `Campaign.Current` and its session's
  `WeakReference.Target` and compares them, before any other engine read.

## PatchShield

Option B, chosen by the maintainer on 2026-10-03 (decision D13). The first build took option A, the house rule for
per-frame targets of a TAOM patch (the 2026-09-26 and 2026-09-28 lessons in `docs/reviews/lessons/harmony-il.md`), and
excluded all five engine targets. Option B excludes only the two that no other TAOM patch uses, `Campaign.Tick` and
`CampaignEvents.Tick`; `MapState.OnTick` (Patch43), `Campaign.RealTick` (Patch89) and `MapScreen.OnFrameTick` (Patch36,
Patch89) are patched for every player and keep the shield they had before this feature, so a player with the profiler
off sees no change. `MapFrameProfilerBindingTests.ProfilerOnlyTargets_AreOnPatchShieldsExclusionList` and
`SharedMapTargets_StayUnderPatchShield` walk the real targets. PatchShield writes one `not shielding ...` diag.log line
per excluded method it skips. The per-target analysis of option A is in
`docs/reviews/deep-review-039-campaign-map-frame-profiler-2026-10-02.md`.

PatchShield attaches a finalizer to every method patched at the time of a pass (pass 1 in the Dependencies module's
`OnSubModuleLoad`, pass 2 at the end of every game initialisation). Patch89 applies in `OnSubModuleLoad`, so pass 2 of the
first game start already sees it; Patch43, Patch36's postfix and the profiler's own patches are applied after that pass 2.

| Target | Patched for every player by | PatchShield now | A missing-API exception (MissingMethod, MissingField, TypeLoad) thrown inside it |
|---|---|---|---|
| `Campaign.RealTick` | Patch89 | shielded from the first game start | swallowed there: the rest of `RealTick` is skipped; the views' tick, `Campaign.Tick`, the save tick and the application tick run; the strip removes Patch89's and Patch101's patches |
| `MapState.OnTick` | Patch43 | shielded from a process's second game start, or from the first when another mod patched it at load | swallowed there: the rest of `MapState.OnTick` is skipped and the application tick runs; the strip removes Patch43's and Patch101's patches |
| `MapScreen.OnFrameTick` | Patch36, Patch89 | shielded from the first game start | swallowed there: the rest of `OnFrameTick` is skipped and the map's UI layers still tick; the strip removes Patch36's F6 hub, Patch89's trace and Patch101's pair |
| `Campaign.Tick`, `CampaignEvents.Tick` | none | excluded (without the profiler no patch exists on them) | escapes to `MapState.OnTick`, see "Given up" below |

The strip is the shield's rescue: the first swallow at a method removes every non-protected owner's prefixes, postfixes
and transpilers on it. TAOM's owner id `com.taom.mod` matches no protected prefix, so TAOM's own patches on that method go
too.

**Gained:** with the profiler on, the finalizer the shield would attach to `Campaign.Tick` (it would run after Patch101's
postfix, so inside `mapStateMs`) and to `CampaignEvents.Tick` (inside `campaignTickMs`, never inside `tickEventMs`, which
times only the swapped dispatch) never exists. The profiler's patches on them are applied after the first game start's
pass 2, so without the exclusions both would carry one from a process's second game start. Shielded methods that
`Campaign.Tick` calls still run their finalizers inside `campaignTickMs`, for example Patch65's `SpawnLordParty` on a
clan's daily tick. A player with the profiler off gains nothing and loses nothing.

**Not gained:** the three shared methods keep PatchShield's finalizer, which binds `__originalMethod` (a
`MethodBase.GetMethodFromHandle` and a try/catch per call), once per frame each. Harmony emits finalizers after every
postfix, so a finalizer never runs inside its own method's bracket: `Campaign.RealTick`'s runs inside `mapStateMs` (after
Patch101's postfix on it), and the ones on `MapState.OnTick` and `MapScreen.OnFrameTick` run after their own brackets, in
`otherMs`. Plan 034 owns the finalizer's cost.

**Given up on `Campaign.Tick` and `CampaignEvents.Tick`, profiler on:** a missing-API exception thrown anywhere inside
either, by Patch101 or by code they call (a `CampaignEvents.TickEvent` listener, TAOM's or another mod's, the periodic and
hourly events of every mod's campaign behaviours), is no longer swallowed there. It skips the rest of `Campaign.Tick` (after
a throw in `CampaignEvents.Tick`: the periodic and hourly events, the tick data store, the captivity update and encounters)
and then the rest of `MapState.OnTick`: the map screen's `AfterWaitTick` (`TickNavigationInput`) and
`SaveHandler.CampaignTick` (the autosave attempt). From a process's second game start, or from the first when another mod
patched `MapState.OnTick` at load, the shield on `MapState.OnTick` catches it there: it swallows, writes one `swallowed`
diag.log line per throw and, on the first, strips Patch43's and Patch101's patches on `MapState.OnTick`, so no more frames
close and `[MapProfile]` lines stop for the process. While the cause recurs it is swallowed again each frame, and each
frame skips the same calls. In a process's first game, when nothing shields `MapState.OnTick`, it leaves through
`GameStateManager.OnTick`, `Game.OnTick` and `GameManagerBase.OnTick` (the game handlers' ticks, `AfterTick` and the
save-completion check are skipped) to `Module.OnApplicationTick`, and from there every submodule's `OnApplicationTick`,
TAOM's included, `JobManager.OnTick` and `AvatarServices.UpdateAvatarServices` are skipped, every frame it recurs.

Who catches it at `Module.OnApplicationTick`, verified against the shipped Lib.Harmony 2.4.2: it carries Patch37's
crash-capture finalizer (`[HarmonyPriority(800)]`) and, from the first game start's pass 2, PatchShield's own finalizer (no
priority attribute, which Harmony files as 400). Harmony runs finalizers highest priority first against one shared
exception slot, each value returned becoming the exception the next one sees, so Patch37 runs first. With "Enable Crash
Capture" on (its default) Patch37 writes a crash report (a recurring signature is throttled) and swallows, and PatchShield's
finalizer then sees none. With it off (or on re-entry, or with the crash-report service unresolved) Patch37 hands the same
exception back, and PatchShield's finalizer on `Module.OnApplicationTick` swallows the MissingMethod, MissingField or
TypeLoad exception, writes one `swallowed` diag.log line per throw, and on the first throw strips every non-protected
owner's prefixes, postfixes and transpilers there, TAOM's own `CrashReportApplicationTickTrigger` among them, never the
patch that threw.

Nothing TAOM ships is given up on these two with the profiler off: no patch exists on either. The exclusion is by method
name for every owner, so another mod's patch on either loses the shield too, profiler on or off (whether any mod patches
them is UNVERIFIED).

**Given up by keeping the shield on the other three, profiler on:** a swallow there also strips Patch101's pair on that
method (the strip takes every `com.taom.mod` patch), which ends that measurement for the process: `realTickMs` or
`mapScreenMs` would read 0 from then on, and after a strip on `MapState.OnTick` no more frames close. The profiler
notices the first case (see "Hook health"): at the next window start, session start or measuring session end it finds
the hook unpatched, writes ONE `[MapProfiler]` warning naming every lost hook, writes the frames closed so far as a
`reason=hooksLost` summary and stops measuring; the window line before it can hold up to one window of frames measured
after the strip. A strip on `MapState.OnTick` takes the boundary that runs the window check, so only the session end
looks: until then the lines stop, nothing clears `Measuring`, and the other five hooks and the views keep paying the
full measuring cost every frame, the per-listener walk included. diag.log's `swallowed ...` and
`unpatched owner 'com.taom.mod' on ...` lines are the record of why in both cases.
Before this feature the same swallow already stripped Patch43's, Patch89's and Patch36's patches there.

**Still per frame:** Patch37's targets `Module.OnApplicationTick`, `ScreenManager.Tick` and `ScreenManager.Update`, and any
other patched method the frame calls, keep PatchShield's finalizer once per frame; that cost lands in `otherMs` or in the
bracket enclosing the call (plan 034 owns it). One example inside `mapStateMs`: `MapScreen`'s `BeforeTick` calls
`SceneView.ReadyToRender` and `CheckSceneReadyToRender`, which carry Patch89's `SceneReady` patches and so its finalizer
from the first game start.

## Key Files

| File | Purpose |
|---|---|
| `Main/Features/MapPerf/MapFrameProfiler.cs` | pure accumulator: frames, skips, windows, session, speed split (`MapWindow`, `MapSummary`, `SpeedTotal`) |
| `Main/Features/MapPerf/MapSpeed.cs` | speed class from the (simplified) time-control mode and multiplier |
| `Main/Features/MapPerf/MapProfileLines.cs` | every line the profiler writes |
| `Main/Features/MapPerf/Hooks/MapFrameProfilerHooks.cs` | phase brackets, view probe, the timed TickEvent dispatch |
| `Main/Features/MapPerf/Hooks/MapFrameBoundary.cs` | the frame boundary (the `MapState.OnTick` prefix), its engine reads, and the party count and raw top-N reads |
| `Main/Features/MapPerf/Hooks/MapSessionHooks.cs` | sessions, window lines, the speed read and the hook revalidation |
| `Main/Features/MapPerf/Hooks/TickEventListenerWalker.cs` | the `InvokeList` copy that times each listener |
| `Main/Features/MapPerf/Hooks/MapFrameProfilerInstaller.cs` | the once-per-process install and later-init reports |
| `Main/Features/MapPerf/Hooks/MapProfilerTargets.cs` | the six core targets and the unpatched check, the TAOM map view targets, the call swap |
| `Main/Features/TimeAcceleration/ITimeControlAdapter.cs`, `TimeControlAdapter.cs` | `SimplifiedTimeControlMode`, the engine's simplified mode the speed class reads |
| `Main/Features/MapPerf/Hooks/Patch101_MapFrameProfiler.cs` | the six core patch classes |
| `Main/Features/MapPerf/Hooks/Patch101_MapFrameProfilerViews.cs` | the TAOM map view patch class |
| `Dependencies/Foundation/PatchShieldPolicy.cs` | the two `ExcludedTargetMethods` entries (`Campaign.Tick`, `CampaignEvents.Tick`) |

## Tests

- `MapSpeedTests`, `MapFrameProfilerTests`, `MapProfileLinesTests`, `TickEventSwapTests`,
  `MapFrameProfilerWiringTests`: no category, so hosted CI's unit step runs them too.
- `TickEventListenerWalkerTests` (the differential walk), `MapFrameProfilerHooksTests` (sessions, windows, the
  speed class through a fake time-control adapter, the hook revalidation through a fake `LostHooks`),
  `MapFrameProfilerInstallerTests` (the install decisions, the unpatched check, and a real Harmony strip with
  PatchShield's three `Unpatch` calls to show the check sees it): `RequiresGame`.
- `MapFrameProfilerBindingTests`: `BindingVerification` (the `CampaignEvents.Tick` rewrite and the
  `Invoke` and `InvokeList` shapes against the installed IL, every Patch101 method compiled against the
  installed engine, the core and view targets, the two PatchShield walks).
- `MapFrameProfilerInstallerTests` also patches test methods with a real Patch101 prefix to check the
  installer's production patched check.
- `ReflectionSiteBindingTests`: the four listener-walk members.
- `TimeControlAdapterBindingTests`: `BindingVerification` (the adapter's `SimplifiedTimeControlMode` getter calls
  `Campaign.GetSimplifiedTimeControlMode`, not the raw `TimeControlMode`; every speed test fakes the adapter, so none
  would notice the change back).
- Not testable offline: Harmony applying Patch101 in the game, the in-game numbers, a real PatchShield swallow
  and strip on the three shared targets, and the engine reads in `MapFrameBoundary` (`OnFrameBoundary` and the party
  count).

## Reading the lines

- Compare runs at the same speed class and a similar party count; the summary's `byspeed` says how the
  session's frames split.
- `taomMs` against `wallMs` is a floor on TAOM's share of the closed frames (see the field table); the `top=` entries name the
  listeners and views behind it. `otherMs` is everything the brackets do not enclose, rendering first.
- `frames` should sit near the `[MapLoad]` heartbeat's frame count for the same window; a large `skipped`
  during a load is expected.

## Changelog

- 2026-10-03: Patch101 campaign map profiler, off by default (plan 039).
- 2026-10-03: PatchShield option B (decision D13, the maintainer): `MapState.OnTick`, `Campaign.RealTick` and
  `MapScreen.OnFrameTick` keep the shield; only `Campaign.Tick` and `CampaignEvents.Tick` are excluded.
- 2026-10-03: review follow-ups: the speed class reads `Campaign.GetSimplifiedTimeControlMode()` so a waiting
  Stoppable mode reads `Stop` (FOR-MIKE 16r); the six core hooks are looked at again at each session and window
  start and a lost one stops measuring behind one warning (Codex P2); the "off" cost wording is exact (Codex P3);
  the frame boundary moved to `MapFrameBoundary` to keep the hooks under 150 lines (ADR-002).
- 2026-10-03: convergence follow-ups: a measuring session's end looks at the six core hooks too, before its summary,
  so a strip on `MapState.OnTick` and the frames since the last window start are flagged (`reason=hooksLost`);
  the "Gained" line names the right brackets; the adapter's simplified-mode read is pinned by IL.

## GitHub Issue

- **Issue:** [#721](https://github.com/haterade22/TAOM/issues/721) diagnostics: attribute campaign-map frame time to TAOM's per-frame map code
- **Status:** Open
