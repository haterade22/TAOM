# Settlement nameplate cull

## Overview

The campaign map updates every settlement nameplate every frame, hidden ones included. TAOM_Map defines 1,040
settlements (live `settlements.xml`, 2026-10-08: 82 towns, 150 castles, 649 villages, 158 hideouts and one custom
settlement; a hideout gets a plate only once it is visible), so that is a large share of a fast-forward frame spent on
plates nobody can see. One Harmony bool prefix on
`SettlementNameplatesVM.Update` replaces the engine's loop with the same loop over only the plates that are not hidden
and staying hidden; everything else updates exactly as in vanilla. The MCM toggle "Cull Hidden Settlement Nameplates"
(Battle Load Diagnostics page, group "Map Performance", default on, no restart) turns it off again, live.

Adapted from yotthani's VanillaTuning `nameplate-cull` (MIT, (c) 2026 yotthani). Provenance:
[provenance-register.md](../reference/provenance-register.md), adoption record
[adopt-yotthani-2026-10-08.md](../reviews/adopt-yotthani-2026-10-08.md).

## Why This Exists

- **Vanilla behavior:** `SettlementNameplatesVM.Update()` (v1.5.4) sets `_cachedCameraPosition` from the map camera, runs
  `SettlementNameplateVM.UpdateNameplateMT(camera)` for every plate in a `TWParallel.For`, then
  `RefreshBindValues()` for every plate. It is called once per map frame by
  `GauntletMapSettlementNameplateView.OnMapScreenUpdate`.
- **TAOM requirement:** yotthani measured the nameplate update at 4 ms of a 14 ms campaign-map frame in fast forward on
  TAOM's map (1,002 settlements in his count), and 5.5 ms down to 2.3 ms per frame with the cull. TAOM has not measured it yet (the A/B
  below does).
- **Without this feature:** the map pays for every hidden plate every frame.

## Architecture

### Design Challenge

A skipped plate must be one whose update would change nothing a widget can show, and the cull replaces an engine method
that runs every frame, so it must add no garbage, read no MCM setting the slow way, and fall back to the whole vanilla
method on any doubt.

### Solution Approach

`SettlementNameplatesVM_Update_NameplateCull_Patch` (Patch104, category `Patch104_NameplateCull`, applied at
`ApplyPhase.GameInit`) is a bool prefix: `NameplateCullService.TryUpdate` returns true when the culled update ran
(the prefix returns false, skipping vanilla) and false for every other outcome (the prefix returns true, vanilla runs).
`NameplateCullAdapter.UpdateCulled` does the work in vanilla's order: read the camera, fill a reused list with the plates
`NameplateCullRule.MaySkip` does not clear,
`TWParallel.For` over that list calling `UpdateNameplateMT`, then `RefreshBindValues` over it. The decision is made on the
main thread before the loop, without locks; the first upstream version decided per plate on the worker threads through a
`ConditionalWeakTable`, and the lock per lookup cost 7.5 ms of CPU per frame.

A plate is skipped only when all of these hold (`NameplateFacts`, `NameplateCullRule.MaySkip`):

| Condition | Why |
|---|---|
| pushed `IsVisibleOnMap` false, private `_bindIsVisibleOnMap` false | the widget shows nothing and the last update decided hidden |
| pushed `Position` is already (-1000, -1000) | TAOM addition: a new plate sits at (0, 0) until its first update parks it |
| `IsTracked`, `IsInRange` false | tracked and in-range plates always update (in-range plates also tick events) |
| `IsTargetedByTutorial` false | TAOM addition: the pushed property is what the widget reads; a skipped plate never pushes it |
| `CanBeVisible` false | the distance half of vanilla `IsVisible`: camera z above 400 shows towns only, above 200 towns and castles, else distance below z + 100. A non-finite input answers "can be visible" |
| settlement range false | `IsVisible` for a hideout, `IsInspected` otherwise, what `RefreshDynamicProperties` would push |
| party not `IsVisualDirty` | the name is about to be re-read |
| `VisualTrackerManager.CheckTracked` false | a tracked settlement always updates |

`TryUpdate` returns false, and the cull stays off, when the toggle is off (read live each frame through a cached MCM
reference), when `Install` could not bind an engine member, and for the rest of this game launch after the culled
update threw once. After a throw vanilla re-runs the whole update that frame, at most once per launch. The replay is
best effort, not exact: values are recomputed from the camera and the settlements, but a plate that had already pushed
its bindings ticks its notifications a second time (one tick of notification merging), and an engine-side fault need not
repeat in vanilla's run if a setter stored its value before its change callback threw.

Vanilla's `Update` writes `_cachedCameraPosition` and only `UpdateNameplateAuxMT` reads it, which only `Update` reaches,
so the culled update passes the camera straight to its loop and does not touch that field
(`NameplateCullBindingTests` pins both facts in the IL). A plate that turns visible after skipped frames decides its
visibility from the `WPos` it pushed on its last unskipped frame, where vanilla uses the previous frame's: within one
frame of vanilla, and the widget's alpha eases toward its target anyway.

When another mod's bool prefix on `Update` runs first and replaces the update, Harmony 2.4.2 (the version TAOM.Dependencies
ships) skips TAOM's prefix, and the other way round: its wrapper emits a `runOriginal` check before every bool prefix
(`NameplateCullPrefixOrderTests` pins this), so the two never both run a culled update.

The toggle is read every map frame through `NameplateCullSettingsProvider`, which caches the MCM settings reference;
the provider is in the hot-path settings gate (`CampaignHotPathSettingsProvidersTests`), so a regression to a per-frame
`Instance` lookup fails a test.

### Component Diagram

```
NameplateCullModule (GameInit: Patch104, then Install)
        |
SettlementNameplatesVM_Update_NameplateCull_Patch   (bool Prefix, catches everything)
        |
INameplateCullService (NameplateCullService)  --- IModLogger, INameplateCullSettingsProvider
        |
INameplateCullAdapter (NameplateCullAdapter)  --- FieldRef delegates, reused list, TWParallel.For
        |
NameplateCullRule.MaySkip(NameplateFacts)      (pure)
```

## Configuration

MCM, Battle Load Diagnostics page, group "Map Performance": `Cull Hidden Settlement Nameplates`, default on,
`RequireRestart = false`. Read through `NameplateCullSettingsProvider`, which caches the settings reference lazily and
reads through it (plan 031's pattern), falling back to on while MCM has no instance. Classified as presentation in
`CoopSettingsRelevance`.

## Key Files

| File | Purpose |
|------|---------|
| `Main/Features/NameplateCull/NameplateCullModule.cs` | Registrations, Patch104 at GameInit, `Install` |
| `Main/Features/NameplateCull/Hooks/SettlementNameplatesVM_Update_NameplateCull_Patch.cs` | The bool prefix |
| `Main/Features/NameplateCull/NameplateCullService.cs` | Toggle, error latch, the 18,000-frame window line |
| `Main/Features/NameplateCull/NameplateCullRule.cs` | `CanBeVisible` and `MaySkip` |
| `Main/Features/NameplateCull/Models/NameplateFacts.cs`, `CullCounts.cs` | The decision's inputs, the frame's result |
| `Main/Features/NameplateCull/NameplateCullLines.cs` | Log wording |
| `Main/Adapters/NameplateCullAdapter.cs` | Field refs, the reused list, the culled update |
| `Main/Features/BattleLoadDiagnostics/BattleLoadDiagnosticsSettings.cs` | The MCM toggle |

## Dependencies

- `IModLogger`: `taom_debug.log`.
- `INameplateCullAdapter`: the engine members, listed in `NameplateCullBindingTests`.

## Logging

All lines carry `[NameplateCull]`: one install line at GameInit (`ON ... (toggle on)`, `installed, toggle off`, or a
warning `OFF: <reason>; the vanilla nameplate update runs`, for example when `Harmony.GetPatchInfo` shows Patch104 is
not attached); one line per 18,000 culled map frames (about five minutes at 60 fps; frames the cull handled, so
toggled-off frames, battles and screens over the map add nothing, while a game menu, a map conversation or the paused
map keep the map updating and count) with the plate updates run and skipped and the percent skipped; and one ERROR line with the
exception when the cull switches itself off for the rest of this game launch.

## Tests

- `NameplateCullRuleTests`: every condition of `MaySkip` flipped alone, `CanBeVisible` at both height boundaries and the
  near band, non-finite inputs.
- `NameplateCullServiceTests`: the toggle live, not installed, an unbound member, the error path (one line, off for the
  rest of the launch), the window line counting culled frames only and its reset, failing logger and settings, the
  install lines.
- `NameplateCullPrefixOrderTests` (no game): Harmony skips a second bool prefix once the first returned false.
- `NameplateCullLinesTests`, `NameplateCullModuleTests`, `NameplateCullHookTests`: the wording, the container wiring, the
  prefix's four outcomes, and the real class applied to the real `Update` and JIT-compiled.
- `NameplateCullBindingTests`: the target, the three private fields, every public member the adapter calls, the full
  ordered call lists of the vanilla `Update`, `UpdateNameplateMT`, `RefreshBindValues` and `RefreshDynamicProperties`
  (so a new engine step fails a test), the constants of `IsVisible` and the park point of `RefreshPosition`, that only
  the `IsInRange` setter registers a plate's events, that `Initialize` reports a cull whose Patch104 is not attached,
  PatchShield's reach and the MCM attribute. The two settings-provider pins live in the untagged
  `CampaignHotPathSettingsProvidersTests`, so a no-game run executes them.
- Mutations (2026-10-08, run with the skeleton buffer's after the review found a RED step skipped): a dropped `!` in
  the prefix and a renamed `__instance` parameter each failed the expected tests.

## How to A/B it in game

1. Turn "Enable Map Profiler" on (Battle Load Diagnostics page, "Map Performance"; it needs a restart) and start a
   campaign.
2. Fast-forward on the map for a few minutes with "Cull Hidden Settlement Nameplates" on, then untick it (it applies on
   the next frame) and fast-forward for the same time.
3. Compare the `[MapProfile]` lines of the two stretches in `taom_debug.log` (the `mapScreen` part and the wall time per
   frame). After 18,000 culled frames a `[NameplateCull]` line also says how many plate updates were skipped.
4. The default is on (Mike, 2026-10-08), and this A/B runs before the next release. If it shows no gain, the default
   flips before that release: until a release ships the setting, only the maintainer's machine holds a saved value
   (after one, a persisted MCM default cannot be flipped without renaming the setting, `docs/features/mcm.md`).

Also look at the map once at each zoom level: plates appear and fade as in vanilla, a tracked settlement keeps its marker,
and a settlement you walk up to shows its plate at once.

## Performance

Per frame the cull reads about thirteen values per plate on the main thread (no locks, no allocation: a reused list, field
references built at install, one delegate built at install) and then runs `UpdateNameplateMT` and `RefreshBindValues` for
the plates left. Not measured in TAOM yet.

## Changelog

- 2026-10-08: added, adapted from yotthani's VanillaTuning `nameplate-cull`; two conditions added to the upstream rule.

## GitHub Issue

- **Issue:** #777 (draft item 3 in [adopt-yotthani-2026-10-08.md](../reviews/adopt-yotthani-2026-10-08.md)).
- **Status:** built and unit-tested; not yet checked in game.
