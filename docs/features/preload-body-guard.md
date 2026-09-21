# Preload Body Guard

## Overview

A Harmony prefix on `TaleWorlds.MountAndBlade.View.PreloadHelper.WaitForMeshesToBeLoaded` that
keeps a missing collision body from freezing the game. It drains the body names that cannot
resolve out of the preload set, with a bounded wait, logs each one at ERROR as `[PreloadGuard]`,
and lets vanilla's loop run unchanged. Category `Patch90_PreloadBodyGuard`, issue #601.

## Why This Exists

Vanilla's wait (installed v1.5.3, same shape as v1.4.8):

```
do {
    num = 0;
    foreach (var m in _uniqueMetaMeshes) num += m.Item1.CheckResources();
    foreach (string name in _uniqueDynamicPhysicsShapeName)
        num += (PhysicsShape.GetFromResource(name, true) == null) ? 1 : 0;
    Thread.Sleep(1);
} while (num != 0);
```

`GetFromResource(name, mayReturnNull: true)` returns null for "not loaded yet" and for "no such
body" alike, and the loop has no exit for the second case. One `body_name` that no loaded tpac
ships pins the game-loop thread on the first frame after the loading window drops: no error, no
crash, `Process.Responding` still true because the window pump is another thread. Two typos did
it in #352 (2026-07-16); the 2026-09-11 Armory art drop that renamed the elven bows did it again
in #599, and every elf start hung on the Rivendell tournament for two days.

#599 built the desk-side gates (`/armory-audit`, `MISSING_COLLISION_BODY` in
`validate_moduledata.py`, the `ARMORY ART DRIFT` startup line). None of them reaches a player
whose Armory copy already carries a bad pair, and the artists will keep combining, renaming and
deleting meshes. This guard is the only protection at the point of failure.

**#633 (2026-09-21) is the case the desk-side gates cannot see.** Three Rhun longbows had no
collision body of their own and borrowed the elven bow's. Every desk-side gate passed,
`/armory-audit` included (CLEAN, 2026-09-20), because the borrowed name resolves. A player found the
hang by hand and narrowed it to four item ids before anyone here knew it existed. Why their build
hung is still open: the shipped patreon tree carries that body in its `pack0`, so the borrow
resolved there too, and the leading hypothesis is a build whose packs predate the 2026-09-11 art
drop while its XML carries the post-#599 name, the plain #599 class. If that is right, this guard
would have logged a `[PreloadGuard]` line naming the body, on a build that had it (v2.0.30,
`bannerlord-1.5.x` only; the player's version is not recorded). If the stall is mesh-side instead
(`MetaMesh.CheckResources` is in the same loop and this guard drains body names only), it would
have logged nothing. The desk-side half is now `COLLISION_BODY_BORROWED`; see
`docs/reviews/rca-rhun-longbow-collision-body-2026-09-21.md`.

Six callers, all on the main thread: `MissionPreloadView.OnSceneRenderingStarted` (campaign
battles and sieges), `ArenaPreloadView.OnSceneRenderingStarted` (tournaments and arena practice;
`FightTournamentGame.GetParticipantCharacters` puts the player character first, so the player's
own kit is always in that set), `MissionCustomBattlePreloadView`, `MissionMultiplayerPreloadView`,
`NavalShipsPreloadView`, and `GauntletEducationScreen.OnFrameTick`.

## Architecture

### Design Challenge

"Cannot resolve" and "not loaded yet" look identical from managed code. The physics preload is
queued by `PhysicsShape.AddPreloadQueueWithName` and `ProcessPreloadQueue()` inside
`PreloadHelper.PreloadMeshesAndPhysics`, and a healthy load resolves every name within the load
itself (the `[BattleLoad]` diagnostics record `WaitingForRender waitedMs=0..1`), but nothing
proves the native side is synchronous, so a single-pass drop could take a real weapon's
collision shape away on a slow disk.

### Solution Approach

- **Repair the precondition, never reimplement the method** (lesson in
  `docs/reviews/lessons/harmony-il.md`). The prefix returns `true`; vanilla's loop, the mesh half
  included, runs exactly as shipped against a set that can now empty. `_uniqueMetaMeshes` is
  never touched, so nothing of the `HashSet<(MetaMesh, bool, bool)>` has to be reflected into.
- **A bounded poll, not a single pass.** The service polls only the body names, sleeping 1 ms per
  failed pass like vanilla, for a 5 s budget (`BudgetSeconds`, a constant: a guard against a
  frozen game has no reason to be configurable off). Only what is still null after the budget is
  dropped. A healthy load pays one pass of resolved lookups and no sleep; a broken one pays five
  seconds once and then loads.
- **The decision is pure.** `PreloadBodyGuardService.DrainUnresolvable(names, isResolved,
  elapsedSeconds, sleepOnce, budgetSeconds)` takes the resolver, the clock and the sleep as
  parameters, so the budget is tested against an injected clock rather than wall time, and a
  resolver that throws counts as unresolved for that pass and never escapes.
- **Field by injection, not reflection.** The prefix parameter is
  `HashSet<string> ____uniqueDynamicPhysicsShapeName` (four underscores: Harmony's three plus the
  engine field's own). A binding test pins the field's name and type on the installed engine, so
  a rename on the next bump is a red test and not a category that quietly fails to apply.
- **Everything wrapped.** On any failure in the guard itself it logs once and returns `true`; a
  guard that throws inside the load it protects is worse than the hang.
- **Applied in `OnSubModuleLoad`** beside Patch89, own try/catch, because the education screen
  and the custom-battle preload call the wait before any campaign initialises.

### Component Diagram

```
PreloadHelper.WaitForMeshesToBeLoaded()          (engine, 6 callers, main thread)
        ^ Harmony prefix (Patch90_PreloadBodyGuard)
PreloadHelper_WaitForMeshesToBeLoaded_Patch      thin: resolver = GetFromResource(name, true) != null,
        |                                        clock = Stopwatch, sleep = Thread.Sleep(1), budget = 5 s
        v
IPreloadBodyGuardService.DrainUnresolvable(...)   pure: poll, budget, remove, return dropped
        |
        v
IModLogger  "[PreloadGuard] dropped N unresolvable collision body name(s) after X.Xs ...: <names>"
```

## Configuration

None. `BudgetSeconds = 5.0` is a public constant on the patch class.

## Key Files

| File | Role |
|------|------|
| `Main/Features/PreloadBodyGuard/IPreloadBodyGuardService.cs` | the contract |
| `Main/Features/PreloadBodyGuard/PreloadBodyGuardService.cs` | the bounded drain, pure |
| `Main/Features/PreloadBodyGuard/PreloadBodyGuardIoC.cs` | `Reuse.Singleton` registration |
| `Main/Features/PreloadBodyGuard/Hooks/PreloadHelper_WaitForMeshesToBeLoaded_Patch.cs` | the prefix |
| `Main/IoC.cs`, `Main/SubModule.cs` | registration; category applied in `OnSubModuleLoad` |
| `TAOM.Tests/Features/PreloadBodyGuard/PreloadBodyGuardServiceTests.cs` | 7 tests on the contract |
| `TAOM.Tests/Features/PreloadBodyGuard/PreloadBodyGuardBindingTests.cs` | the field and target on the installed engine |

## Dependencies

`TaleWorlds.MountAndBlade.View` (the target), `TaleWorlds.Engine` (`PhysicsShape`), `IModLogger`.

## Tests

- Service (7): all names resolve on the first pass (nothing dropped, no sleep); a name that never
  resolves is dropped after the budget and the others kept; a name resolving on the third pass is
  kept; an empty set returns at once; the budget is measured against the injected clock; a
  throwing resolver counts as unresolved and never escapes; a non-positive budget drops after one
  pass without sleeping.
- Binding (1, `BindingVerification`): `PreloadHelper` keeps a private instance field
  `_uniqueDynamicPhysicsShapeName` of `HashSet<string>` and a parameterless public
  `WaitForMeshesToBeLoaded`. `HarmonyPatchBindingTests` covers the target automatically.
- In game (owed): a probe item with `body_name="bo_this_body_does_not_exist"` on the player's
  kit loads a tournament after about five seconds with one `[PreloadGuard]` line; a normal load
  logs nothing.

### Coverage follows the caller

The guard runs whenever the wait runs, and only then. The five mission preload views call the wait
once per mission from `OnSceneRenderingStarted`. `GauntletEducationScreen.OnFrameTick` calls it once
per screen instance, on the first render-ready frame, behind its own `_startedRendering` latch; the
character sets that later `OnOptionSelect` picks preload into the same helper are never waited on by
vanilla, so they are neither hung against nor guarded or logged here. That is vanilla's coverage, not
a gap the guard opens (deep-review data-flow trace, 2026-09-15).

## How to read a `[PreloadGuard]` line

`dropped N unresolvable collision body name(s) after X.Xs so the mission can load: <names>`. Each
name is a `body_name`, `holster_body_name` or collision body in the item XML that no loaded tpac
ships. Run `python tools/audit_armory_refs.py`: its first table names the item, the file and every
troop carrying it. Repoint the ref to the art that ships; never restore a tpac beside its
replacement. Until then those items have no collision shape in missions (hits may not register).

## Performance

Healthy load: one pass over the body names (tens to a few hundred native lookups, the same calls
vanilla makes on its first pass) and no sleep. Broken load: the 5 s budget once. Nothing per frame.

## Changelog

- 2026-09-15: created (#601), after #599's data repair and desk-side gates.

## GitHub Issue

#601. Mechanism: #352, #599.
