# Map-load diagnostics

**Status:** ported to the 1.4.x line on 2026-11-04 (plans/118-campaign-load-time.md) from the 1.5.x line's Patch89
(`502f7cde9`) together with plan 012's real-lower gate (`8028bf3df`, `e6c03411d`, `15e5475a4`, `a4492fa89`),
so the per-frame lower trace never shipped here. Not yet run in game on v1.4.8.
**Code:** [`Main/Features/MapLoadDiagnostics/`](../../Main/Features/MapLoadDiagnostics/)
**Category:** `Patch89_MapLoadDiagnostics` (+ `_Lifecycle`, `_MapScreen`, `_SceneReady`)
**Related:** [load-time-stamps.md](load-time-stamps.md) for the [Lifecycle] dispatch lines inside the same window
(once plan 118 Part A is merged).

## Why it exists

On v1.4.8 the stretch from TAOM's `GameInitializationFinished` stamp to the map screen has no line in either log: 9.4 s
of one player's load and about 17 s on another machine, with private memory rising about 2 GB in it (player logs of
2026-10-28 and 2026-11-01). `Main/SubModule.cs` records a 2026-09-12 live run with 95 s and +2.7 GB between
AllBehaviorDataLoaded and the map, and #509 saw the window stall for 10 minutes. The feature was written on the
archived v1.5.0 line for the same blind spot.

This feature replaced that with a timeline. It answered the question on its first run.

## What it records

**A heartbeat every 5 seconds** from a postfix on `Campaign.RealTick`:

```
[MapLoad] t=+30s frames=342 fps=68.4 tickMs=7.5 parties=2033(+0)
[lord=63 villager=0 caravan=302 bandit=541 militia=828 garrison=221 other=78]
heroes=4770(+0) clans=235 settlements=988 campaignTime=2185857.000(+0.000)
loadingWindow=True timeControl=Stop topScreen=MapScreen activeState=MapState stack=[MapState]
```

Each field exists to kill one candidate. `fps` and `tickMs` separate a slow load from a stopped one
and put the cost inside or outside the simulation. The per-type party census turns a bare count into
an accusation: 988 villagers against 988 settlements is expected, a climbing bandit count is not.
`campaignTime` distinguishes a paused simulation from a frozen one. `stack` shows the WHOLE game
state stack, because a state left pushed above `MapState` would hold the overlay while the map ran
underneath, and `activeState` alone would read `MapState` and look healthy.

**A lifecycle trace**, each line carrying a sequence number and a millisecond offset so the log reads
as a timeline: every game-state push, pop, clean and initialize with the resulting stack; the map
state and map screen seams bracketed ENTER/EXIT; the first completed map frame; and every raise of
the global loading window, and every lower that actually took it down, **with its managed caller
chain**.

The caller chain is what solved it. It is affordable because real transitions fire a handful of
times. The engine also calls `LoadingWindow.DisableGlobalLoadingWindow()` on every frame of the main
menu and several campaign screens (party, inventory, clan, kingdom, quests, character and crafting
among them), and of scene screens such as character creation, the barber, the face generator and
the banner editor once their scene is ready, whether or not the window is
up, so the Disable patch captures `IsLoadingWindowActive` in a Prefix and traces only a
true-to-false change (`LoadingWindowTraceGate`). Raise lines are still traced per call, not per
transition: a healthy new-campaign load can log two raises (the map screen, then character creation
finalizing) and one lower, so do not pair each raise with its own lower. On the 1.5.x line (its v2.0.29 and v2.0.30 builds),
before this guard, those no-op lowers wrote one line per rendered frame (up to about 360 a second):
84 MB in a 35-minute session, 1.16 GB with the main menu left open for three hours.

## What it found on the 1.5.x line (v1.5.0)

```
LOADING-WINDOW raised :: callers: LoadingWindow.EnableGlobalLoadingWindow
  < MapScreen.HandleIfBlockerStatesDisabled < MapScreen.HandleIfSceneIsReady
  < MapScreen.OnActivate < ScreenBase.HandleActivate < ScreenManager.CleanAndPushScreen
```

The vanilla gate is the same on v1.4.8 (`SandBox.View` `MapScreen.HandleIfBlockerStatesDisabled`).

Vanilla's own gate, behaving exactly as written. `MapScreen.HandleIfBlockerStatesDisabled()` runs
every frame and lowers the window only when
`SceneView.ReadyToRender() && SceneView.CheckSceneReadyToRender()` has held while its ready-frame
counter climbs to 3; the comparison runs before the increment, so the fourth consecutive ready
frame lowers it. After the map screen raised it there were **2 raises and 0 lowers**, so the scene never
reported ready. Everything else was healthy: 68 fps, `MapScreen.OnInitialize` complete in 646 ms,
first frame ticked, party count flat, clock correctly paused, one clean state on the stack.

Not a TAOM defect: a terrain baked by pre-1.5 tools never satisfied the check. The v1.5.2 Modding Kit is
what re-bakes it, which is why the v1.5.x line waited for it. Re-baked on 2026-09-14, the map loaded
that evening, `ReadyToRender` flipping to True 231 s into the session and the loading window
lowering within 15 s of `MapState`.

## Design notes worth keeping

**The census runs only on emit frames.** It walks every mobile party, which is the cost under
investigation, so a per-frame census would have added to the very number it was measuring.

**Categories are split four ways** so a drifted engine binding cannot take the working heartbeat with
it. Harmony aborts a category at its first failing class, which is the `Patch61` precedent.

**A base-class binding was caught by the snapshot, not by the compiler.** `MapState` does not
override `OnInitialize`, so `[HarmonyPatch(typeof(MapState), "OnInitialize")]` silently bound
`GameState.OnInitialize` and would have logged "MapState.OnInitialize" for unrelated states. The
regenerated `patch-targets.md` showed the resolved target and the label was corrected to report the
real instance type. A patch that compiles is not a patch that binds where you think.

## The lesson

The expected sequence was established by decompiling `HandleIfBlockerStatesDisabled`, which took ten
minutes and defined the entire problem. That should have come first. Hours of symptom sampling
preceded it, and every hypothesis formed before it was wrong.

**Establish how the mechanism is supposed to work before instrumenting what it is doing.**
