# Map-load diagnostics

**Status:** shipped 2026-08-20, and it did its job on the first run.
**Code:** [`Main/Features/MapLoadDiagnostics/`](../../Main/Features/MapLoadDiagnostics/)
**Category:** `Patch66_MapLoadDiagnostics` (+ `_Lifecycle`, `_MapScreen`, `_SceneReady`)
**Related:** [`v1.5.0-impact.md`](../migration/v1.5.0-impact.md)

## Why it exists

On v1.5.0 a new campaign reached the map screen and then sat on the loading screen forever. Every
offline gate was green, no error was logged anywhere, and the same build had worked on v1.4.8. Seven
hypotheses were tested and discarded over several hours, each costing a full game launch: the
compressed shader sack, the settlement distance cache, per-frame nameplate cost, party scale,
runaway spawning, `Patch58_SkipCampaignIntro`, and `Patch38_SettlementNameplateFade`. Every one was
wrong, and single-point logging could only ever kill one at a time.

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
state and map screen seams bracketed ENTER/EXIT; the first completed map frame; and every raise and
lower of the global loading window **with its managed caller chain**.

The caller chain is what solved it. It is affordable because those transitions fire a handful of
times, unlike the per-frame work around them.

## What it found

```
LOADING-WINDOW raised :: callers: LoadingWindow.EnableGlobalLoadingWindow
  < MapScreen.HandleIfBlockerStatesDisabled < MapScreen.HandleIfSceneIsReady
  < MapScreen.OnActivate < ScreenBase.HandleActivate < ScreenManager.CleanAndPushScreen
```

Vanilla's own gate, behaving exactly as written. `MapScreen.HandleIfBlockerStatesDisabled()` runs
every frame and lowers the window only when
`SceneView.ReadyToRender() && SceneView.CheckSceneReadyToRender()` has held for three consecutive
frames. After the map screen raised it there were **2 raises and 0 lowers**, so the scene never
reported ready. Everything else was healthy: 68 fps, `MapScreen.OnInitialize` complete in 646 ms,
first frame ticked, party count flat, clock correctly paused, one clean state on the stack.

Not a TAOM defect. See the impact doc's blocker section.

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
