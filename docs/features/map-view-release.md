# Map-view release

## Overview

Every time a screen covers the campaign map, the map's scene view leaves render targets behind (yotthani measured three
colour targets and one depth target per cover; TAOM has not read that in the native code). Vanilla gives them back only
when a mission with memory cleanup starts: a battle, or a town, village or arena scene. A handler on the engine's own
event `ScreenLayer.OnLayerActiveStateChanged` counts the covers of the map's own scene layer and, on every Nth (default
20), releases the view's GPU resources through vanilla's `SceneLayer.ClearRuntimeGPUMemory(false)` when that layer
becomes active again, so the map, the top screen again, shows and lowers its own short loading screen. No Harmony patch.
Two MCM settings on the Battle Load Diagnostics page, group "Map Performance": "Release Map View Memory" (default on) and
"Map View Release Interval" (1 to 1000, default 20), both live, no restart.

Adapted from yotthani's VanillaTuning `map-view-release` (MIT, (c) 2026 yotthani). Provenance:
[provenance-register.md](../reference/provenance-register.md), adoption record
[adopt-yotthani-2026-10-08.md](../reviews/adopt-yotthani-2026-10-08.md). TAOM's own audit lists the leak as item M3a.

## Why This Exists

- **Vanilla behavior:** `SceneLayer.OnDeactivate` (v1.5.4) only disables the view when `AutoToggleSceneView` is set, and
  the map's layer is built `new SceneLayer(true, false)` (`MapScreen.cs:648`), so the map manages its own view. The
  render targets of a covered map are not released; `MapScreen.ClearGPUMemory` (`MapScreen.cs:504`) does it when a mission
  with memory cleanup starts (`MissionCampaignView.OnMissionScreenPreLoad`; `MissionState.OpenNew` defaults
  `needsMemoryCleanup` to true, and only conversation and meeting missions pass false).
- **Measurement (yotthani, not reproduced by TAOM):** about 94 MB of graphics memory per closed screen at 5120x2160 with
  DLSS. Calling `ClearAll` on every cover left nothing behind but made each close take about 0.4 s instead of 0.08 s,
  because the map's textures reload, hence the interval. Switching the view to render on demand had no effect.
- **Without this feature:** dedicated GPU memory grows with every screen opened over the map until the next battle or
  town visit.

## Architecture

### Design Challenge

The event fires for every screen layer in the game, activations included, so the handler has to cost nothing for any
layer but the map's, and it must not touch `MapScreen` (its static `Instance`) before a campaign runs. Upstream's first
install at game start broke the load-game preview; why is not known to TAOM.

The release must also run when the map can absorb its effect. Native `ClearAll` zeroes the view's ready word
(`mov word ptr [rbx+0x8A0], 0` at RVA `0x34B769` in v1.5.4; `ReadyToRender` and `CheckSceneReadyToRender` both return
that byte, `0x50AB20`; only one function sets it back, the one that names itself `rglScene_view::render` in a profiler
string, at `0x3491EB` and `0x34925F`), and `MapScreen.HandleIfBlockerStatesDisabled`
raises the global loading window while the view is not ready, lowering it only while the map is the top screen. On a
cover, `ScreenBase.HandlePause` deactivates the layers first and then calls `MapScreen.OnPause`, which runs that check:
a release on the cover raised the loading window over the screen being opened, and nothing lowered it while that screen
was up (Options, Save/Load and TAOM's fief hub, career and supply-order screens never do). Upstream released on the cover;
the 2026-10-08 review found this, and the release moved to the map's return.

### Solution Approach

Upstream patched `SceneLayer.OnDeactivate` with a Harmony postfix. The engine already raises a public static event at the
same point: `ScreenLayer.HandleDeactivate` (v1.5.4, `ScreenLayer.cs:120-129`) runs `OnDeactivate()`, sets
`IsActive = false`, drops the focus and raises `OnLayerActiveStateChanged`, and `HandleActivate` (`:110-118`) raises it
after `IsActive = true` and `OnActivate()`. `MapViewReleaseModule` subscribes `MapViewReleaseLayerEvents.OnLayerActiveStateChanged`
once, in `OnPhase(ApplyPhase.GameInit)`, and never removes it (the engine's own screen manager keeps its subscription for
the whole run too). So TAOM patches no engine method here: no registry entry, no PatchShield wrapper, nothing to strip.
The handler returns at once unless the layer is a `SceneLayer`; it hands a deactivation to
`MapViewReleaseService.OnSceneLayerDeactivated` and an activation to `OnSceneLayerActivated`.

The cover (deactivation) checks, cheapest first:

1. the error latch (the release switched itself off after an exception);
2. the MCM toggle;
3. `IMapViewReleaseAdapter.IsCampaignRunning` (`Campaign.Current != null`);
4. `IsMapSceneLayer(layer)`: the adapter compares the layer by reference with `MapScreen.Instance.SceneLayer`;
5. `MapViewReleaseCounter.Cover(interval)`: counts the cover and says whether this is the Nth since the last release;
   the interval is read on every cover, clamped to 1 to 1000, so a live edit applies at the next cover.

When the counter says this cover releases, the service only marks the layer pending. The return (activation) of that same
layer clears the mark and, if the toggle is still on, a campaign runs and the layer is still the map's, calls
`ReleaseSceneView(layer)`; any other activation returns at once while nothing is pending. `ScreenBase.HandleActivate` and
`HandleResume` activate the layers before the screen's own `OnActivate` and `OnResume` (`MapViewReleaseBindingTests`
reads the IL), so the release lands just before the map's own ready check: the map, now the top screen, sees its view not
ready, shows the global loading window and lowers it three ready frames later. The nav-bar screen switch
(`MapNavigationHelper.SwitchToANewScreen`) pops back to the map and pushes the next screen in the same call, so on a due
return the map raises the window and is covered again before its first frame; every screen that switch opens (inventory,
party, clan, kingdom, character, quests) lowers the loading window itself each frame. A pending mark is dropped when the
current map's own layer activates without being the pending one (a save was loaded, so the old map screen is gone), or
when another
layer activates and no campaign runs any more, so an ended campaign keeps its old layer alive only until then.

`ReleaseSceneView(layer)` calls `SceneLayer.ClearRuntimeGPUMemory(remove_terrain: false)`, which is exactly
`SceneView.ClearAll(clearScene: false, removeTerrain: false)` (`SceneLayer.cs:145-148`), the path vanilla's own
`MapScreen.ClearGPUMemory` takes. The native call keeps the scene and the terrain, releases 14 reference-counted members
of the view plus one deferred, runs the map scene's own GPU clear (all of it except the terrain block and one call gated
on clearing the scene), resets the
scene's load state, zeroes the view's ready word and writes `Scene_view::clear_all` to the engine log (decompiled
`ISceneView.ClearAll`, 2026-10-08 reviews). Vanilla's mission-start release (`ClearRuntimeGPUMemory(true)`) runs the same
native work and more, so anything that still draws after a battle or a town visit survives this release too. `MapScreen`
is named in exactly one method, `MapViewReleaseAdapter.MapSceneLayerOrNull`, marked `NoInlining`, which only
`IsMapSceneLayer` calls, so compiling the handler, the service or the rest of the adapter resolves no map type
(`MapViewReleaseBindingTests` reads the IL).

The cover count runs for the whole game launch, across missions and campaigns. Vanilla's own release at a mission start
does not reset it, and entering a battle or a town is itself a cover of the map, so a release can come soon after
vanilla's own; at most one short loading screen per N covers either way (the simplicity criterion rejected a reset on
mission entry, 2026-10-08 review). A map conversation switches the map's view off and on without a layer event, so it is
not counted (whether it leaks is in the in-game check below).

No existing TAOM adapter exposes the map's scene layer (`IMapScreenInputAdapter` is the F6 key only; `RefugeVisualService`
and the FieldCamp query read `MapScreen.Instance` inline), so `IMapViewReleaseAdapter` is new rather than a copy.

### Component Diagram

```
MapViewReleaseModule (GameInit: the install line, then subscribe once)
        |
ScreenLayer.OnLayerActiveStateChanged -> MapViewReleaseLayerEvents (scene layers only, both edges, catches everything)
        |
IMapViewReleaseService (MapViewReleaseService) --- IModLogger, IMapViewReleaseSettingsProvider
   cover: count, mark pending      return: release the pending layer
        |                  |
MapViewReleaseCounter     IMapViewReleaseAdapter (MapViewReleaseAdapter)
 (pure: which cover)       IsCampaignRunning / IsMapSceneLayer / ReleaseSceneView
```

## Configuration

MCM, Battle Load Diagnostics page, group "Map Performance":

| Setting | Default | Notes |
|---|---|---|
| Release Map View Memory | on | `RequireRestart = false`; read at every cover of the map and again at the return that releases |
| Map View Release Interval | 20 | integer 1 to 1000, `RequireRestart = false`; clamped again in `MapViewReleaseSettingsProvider` |

Both read through a cached settings reference (lazy, read through), falling back to on and 20 while MCM has no instance.
Both are classified as presentation in `CoopSettingsRelevance`. They are new settings, so a persisted default does not
apply.

## Key Files

| File | Purpose |
|------|---------|
| `Main/Features/MapViewRelease/MapViewReleaseModule.cs` | Registrations, the install line and the subscription at GameInit |
| `Main/Features/MapViewRelease/Hooks/MapViewReleaseLayerEvents.cs` | The event handler |
| `Main/Features/MapViewRelease/MapViewReleaseService.cs` | The decisions (the cover marks, the return releases), the log, the error latch |
| `Main/Features/MapViewRelease/MapViewReleaseCounter.cs` | Interval clamp and which cover releases |
| `Main/Features/MapViewRelease/MapViewReleaseLines.cs` | Log wording |
| `Main/Features/MapViewRelease/MapViewReleaseCalls.cs` | The service the event handler reaches, set by the module's static initialisation |
| `Main/Features/MapViewRelease/MapViewReleaseSettingsProvider.cs` | Reads the two settings (falls back to on, every 20th cover) and clamps the interval |
| `Main/Features/MapViewRelease/IMapViewReleaseService.cs`, `IMapViewReleaseSettingsProvider.cs` | The seams the hook and service tests fake |
| `Main/Adapters/IMapViewReleaseAdapter.cs`, `MapViewReleaseAdapter.cs` | The engine calls and the one `NoInlining` map access |
| `Main/Features/BattleLoadDiagnostics/BattleLoadDiagnosticsSettings.cs` | The two MCM settings |

## Dependencies

- `IMapViewReleaseAdapter`: `Campaign.Current`, `MapScreen.Instance.SceneLayer`, `SceneLayer.ClearRuntimeGPUMemory`.
- The engine event `ScreenLayer.OnLayerActiveStateChanged` (`TaleWorlds.ScreenSystem`).
- `IMapViewReleaseSettingsProvider`: the toggle and the interval.
- `IModLogger`: `taom_debug.log`.

## Logging

All lines carry `[MapViewRelease]`: one install line at GameInit (`ON: ... once per 20 covers (toggle on)`, or
`installed, toggle off`); `released N times, map covered M times` at the first release and on every 20th release,
counting releases that ran; and one ERROR line with the exception when the release switches itself off for the rest of
this game launch after an error (screens then leave the render targets behind, as in the vanilla game).

## Tests

- `MapViewReleaseCounterTests` (12): the 1 to 1000 clamp, every Nth cover, a live interval change either way, and that
  only a release that ran counts as one.
- `MapViewReleaseServiceTests` (32): toggle off touches no engine member, no campaign never asks about the map screen,
  another layer counts nothing; a due cover releases and logs nothing; the return of that layer releases once and logs;
  a return with nothing due makes no adapter call; another layer's activation keeps the release due while a campaign
  runs and drops it after the campaign ended, and a new map's own layer drops an old map's due release; a toggle switched off between cover and return releases nothing and is not
  counted; the log cadence; a failing logger; the error paths on both edges.
- `MapViewReleaseSettingsProviderTests` (2, no game): the fallback to on and every 20th cover, the read-through and the
  interval clamp.
- `MapViewReleaseLinesTests` (5), `MapViewReleaseModuleTests` (8, `RequiresGame`: it touches `ScreenLayer`),
  `MapViewReleaseHookTests` (8): wording, container wiring, and the handler (an activated scene layer goes to the
  return path, a deactivated one to the cover path, a non-scene layer and null to neither, a throwing service does not
  escape).
- `MapViewReleaseBindingTests` (12): the event is a public static `Action<ScreenLayer>` raised in `HandleDeactivate`
  after `OnDeactivate`, and in `HandleActivate` after `IsActive` is set; `ScreenBase` activates its layers before
  `OnActivate` and `OnResume` and deactivates them before
  `OnPause` (why the release cannot run on the cover); `SceneLayer.ClearRuntimeGPUMemory(bool)` and its parameter name;
  `MapScreen.Instance` and `MapScreen.SceneLayer`; which methods name `MapScreen`; the adapter in a test host
  (`RequiresGameIL`); the MCM attributes.

## How to check it in game

1. The subscription exists from the first game start on, so check the screen upstream's first install broke after it:
   load a save and play a minute on the map, exit to the main menu, open the load-game screen (its preview must show
   the characters standing), and load a save again.
2. Open Task Manager, Performance, GPU, and watch "Dedicated GPU memory". Open and close the inventory, the party screen
   and the clan screen 20 to 30 times from the map. With the setting on, the memory stays flat after each round of 20; with
   it off it climbs with every close until a battle or a town visit.
3. On one return in twenty the map shows a short loading screen (about 0.4 s against 0.08 s in upstream's measurement):
   the release marks its view not ready until it renders again (read in the native code, 2026-10-08). `taom_debug.log`
   holds `[MapViewRelease] released 1 times, map covered 20 times` after the twentieth map cover of this launch (entering
   a town or a battle counts as a cover, and the count runs across campaigns).
4. Set the interval to 1. From the map open the escape menu's Options, Save As and Load, and the fief hub (F6): no loading
   screen may cover any of them (before the 2026-10-08 fix one did), and the map's short loading screen comes only after
   each closes. Then set the interval back to 20.
5. With the interval at 1, open the inventory from the map and press P, then C: no loading screen may stay over the
   party or the clan screen.
6. After a release, zoom fully out: the Realm Borders parchment, the camps and the supply routes must still show. A battle
   or a town visit runs the same native release and more, so this confirms what the code already implies.
7. With the setting on, talk to 20 or more parties on the map without opening a screen, and watch Dedicated GPU memory.
   A map conversation switches the map's view off and on without a layer event, so the feature does not count it. If the
   memory climbs with each conversation, record it here: conversations then leak too and are not covered.

## Performance

The handler adds a type test to each layer activation change (screen changes, layers added or removed); a deactivated
scene layer costs a few reads (a latch, two settings, `Campaign.Current`, one reference compare), and an activated one a
null check while nothing is pending. Nothing is allocated on either path (a lambda that captured a local allocated a
closure on every call until the 2026-10-08 review). The release itself is one native call every 20th cover.

## Changelog

- 2026-10-08: added, adapted from yotthani's VanillaTuning `map-view-release`; the review replaced the Harmony postfix
  (planned as Patch105) with the engine's event and `ClearAll` with `ClearRuntimeGPUMemory(false)`. The final review the
  same day moved the release from the cover to the map layer's return: on the cover it raised the global loading window
  over the screen being opened.

## GitHub Issue

- **Issue:** #778 (draft item 4 in [adopt-yotthani-2026-10-08.md](../reviews/adopt-yotthani-2026-10-08.md)).
- **Status:** built and unit-tested; not yet checked in game.
