# Map-load diagnostics

**Status:** shipped 2026-08-20 on the archived v1.5.0 line, where it did its job on the first run; re-landed
on the v1.5.x line on 2026-09-14 as Patch89 (Patch66 belongs to Enlistment on trunk). On v1.5.2 with
the re-baked `TAOM_Map` it recorded the healthy sequence the same evening (2026-09-14 20:17): scene
ready flipped to True, `MapScreen.OnInitialize` 632 ms, the loading window lowered between the +10 s
and +15 s heartbeats, 54 fps at Stop, time running from +90 s. The v1.5.0 hang is closed; the
evidence table is in `docs/migration/v1.5.2-impact.md`.
**Code:** [`Main/Features/MapLoadDiagnostics/`](../../Main/Features/MapLoadDiagnostics/)
**Category:** `Patch89_MapLoadDiagnostics` (+ `_Lifecycle`, `_MapScreen`, `_SceneReady`)
**Related:** the v1.5.2 entries in `CHANGELOG.md`; the v1.5.0 impact write-up lives on the archived branch
(tag `archive/bannerlord-1.5.0-port`).

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
state and map screen seams bracketed ENTER/EXIT; the first completed map frame; and every raise of
the global loading window, and every lower that actually took it down, **with its managed caller
chain**.

The caller chain is what solved it. It is affordable because real transitions fire a handful of
times. The engine also calls `LoadingWindow.DisableGlobalLoadingWindow()` on every frame of the main
menu and most campaign screens (party, inventory, clan, kingdom, quests, character, crafting), and
of character creation and the banner editor once their scene is ready, whether or not the window is
up, so the Disable patch captures `IsLoadingWindowActive` in a Prefix and traces only a
true-to-false change (`LoadingWindowTraceGate`). Raise lines are still traced per call, not per
transition: a healthy new-campaign load can log two raises (the map screen, then character creation
finalizing) and one lower, so do not pair each raise with its own lower. In v2.0.29 and v2.0.30,
before this guard, those no-op lowers wrote one line per rendered frame (up to about 360 a second):
84 MB in a 35-minute session, 1.16 GB with the main menu left open for three hours.

## What it found

```
LOADING-WINDOW raised :: callers: LoadingWindow.EnableGlobalLoadingWindow
  < MapScreen.HandleIfBlockerStatesDisabled < MapScreen.HandleIfSceneIsReady
  < MapScreen.OnActivate < ScreenBase.HandleActivate < ScreenManager.CleanAndPushScreen
```

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
