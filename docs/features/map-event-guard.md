# Map Event Guard

## Overview

A single Harmony prefix that restores one engine invariant before the campaign's battle simulation
relies on it: a `MapEvent` carrying a `BattleObserver` must also carry a `TroopUpgradeTracker`. When
that pairing is broken, the next simulation tick for that battle crashes the game to desktop with a
`NullReferenceException` and a stack containing no TAOM frame at all.

This is a backstop, not a feature. It changes nothing a player can see, and on a healthy campaign its
prefix returns on its first comparison.

## Why This Exists

- **Vanilla behavior:** `MapEventSide.AllocateTroops` (installed v1.4.8, :552) calls
  `_mapEvent.TroopUpgradeTracker.AddTrackedTroop(...)` with no null check, gated only on
  `BattleObserver != null`. `AllocateTroop` :590 does the same, and
  `ApplySimulatedHitRewardToSelectedTroop` :1050 and :1056 do it behind an early
  `if (BattleObserver == null) return;` at :1040. Four unguarded dereferences across three methods,
  resting on one pairing that the engine never re-checks.
- **TAOM requirement:** the pairing is breakable, and TAOM broke it. Anything that removes the MAIN
  party from a live map event nulls the tracker outright (`MapEvent.RemoveInvolvedPartyInternal`
  :855-858), permanently, unless the main party rejoins (:636) or the save is reloaded (:530). The
  observer's only writer is the `BattleSimulation` constructor, which attaches it and only then
  indexes `SelectedTroops[(int)_mapEvent.PlayerSide]`; `BattleSideEnum.None` is `-1`, so a main party
  with no `MapEventSide` makes that line throw with the observer already attached, and the one
  clearer (`PlayerEncounter.LeaveBattle` :1990) never runs on that path.
- **Without this feature:** crash bundle `31942985` (issue #551). The reported chain ran through
  enlistment, which is fixed at source in [enlistment.md](enlistment.md), but nothing about the
  engine hazard is specific to that feature.

## Architecture

### Design Challenge

The crash is invisible at its own site. Removing the main party from a map event does two things at
once, and the CTD needs both:

| Consequence | Engine site |
|---|---|
| `TroopUpgradeTracker` is nulled for good | `MapEvent.RemoveInvolvedPartyInternal` :855-858 |
| The event becomes engine-tickable again. `MapEventManager.Tick`'s condition is `IsRaid \|\| _mapEvents[i] != MobileParty.MainParty.MapEvent`, so a non-raid event is skipped exactly while it IS the player's | `MapEventManager.Tick` :59 |

So the detach happens in TAOM code, and minutes later the engine's own simulation timer walks into
the null on a stack with no TAOM frame on it. No amount of care inside the detaching feature makes
that stack readable for the next person; the guard has to sit at the read.

### Solution Approach

A prefix on `MapEvent.SimulateBattleSetup`. That target is chosen over `AllocateTroops` for three
reasons: it is public, it runs once per simulated tick rather than once per side, and it sits above
every one of the four unguarded reads.

**The repair clears the observer rather than rebuilding the tracker.** The observer is a
`BattleSimulation` whose constructor threw, so `PlayerEncounter.Current.BattleSimulation` was never
assigned and nothing else holds it: there is no scoreboard left to feed. Rebuilding the tracker would
instead invent state for a battle the player is provably not in, since the tracker is null precisely
because the main party was removed.

**Fail-quiet by construction.** `Initialize` resolves the internal `BattleObserver` property once and
sets `IsReady`; when it fails the prefix does nothing at all, so an engine rename degrades to "the
guard is not installed" rather than throwing inside every simulated battle in the world. The body is
wrapped anyway, because it runs ahead of vanilla simulation for every live map event.

**No throttle is needed.** The repair makes its own condition false, so the warning fires once unless
something attaches a second dangling observer, which is worth hearing about.

### Component Diagram

```
MapEvent.SimulateBattleSetup  (engine, per simulation tick)
        |
  Patch82 prefix  — TroopUpgradeTracker != null ?  -> return (the common case)
        |            BattleObserver == null ?      -> return
        |
   clear BattleObserver + LogWarning
        |
  vanilla MakeReadyForSimulation -> AllocateTroops  (now takes the observer-less path)
```

## Configuration

None. There is nothing to tune: the guard either sees a broken invariant or it does not.

## Key Files

| File | Purpose |
|------|---------|
| `Main/Features/MapEventGuard/Hooks/Patch82_MapEventObserverInvariant.cs` | The whole feature: cached binding, prefix, repair |
| `Main/SubModule.cs` | `Initialize(...)` plus `PatchCategory("Patch82_MapEventObserverInvariant")` in the `OnGameInitializationFinished` batch, and `ResetForUnload` |

No service, no adapter, no IoC registration. The logic is a null comparison and a property write; a
service layer here would be indirection with nothing to hold (`simplicity-criterion.md`).

## Dependencies

- `IModLogger` (Core/Logging): passed in at `Initialize`, never resolved in the prefix.

## Tests

- `TAOM.Tests/Features/MapEventGuard/Patch82MapEventObserverInvariantBindingTests.cs` holds 5 tests. The
  target method and its arity plus overload count; both accessors of the internal `BattleObserver`;
  the public, reference-typed `TroopUpgradeTracker`; the continued existence of
  `MapEventSide.AllocateTroops`, the reader this protects; and all three registrations, including the
  `PatchCategory` call in `SubModule.cs`.

**The behaviour itself is not unit-testable.** `MapEvent` has no public constructor and the broken
state needs a live campaign, so these tests pin what the patch BINDS to and the repair is verified in
game. That split is deliberate: every fact the patch stands on is an engine detail it cannot see
change, and a rename in any of them would make the guard silently inert.

## How to tell whether it ever fired

Search the TAOM debug log for `[MapEventGuard]`. Two lines exist:

- `cleared a dangling BattleObserver ...` means the guard caught a real break. Something removed the main
  party from a live map event while its battle UI stayed attached. Find that, because the guard is
  the floor, not the fix.
- `MapEvent.BattleObserver did not resolve ...` means the binding failed on this engine build and the
  guard is inert. Re-derive it against the new shape; the binding tests should have caught this first.

## Performance

The prefix runs for every live map event on its own simulation timer, which is many times a second at
accelerated campaign speed, so ordering matters: `TroopUpgradeTracker != null` is tested first and is
true for every ordinary AI battle, so the common case is one property read and a return. Reflection is
resolved once in `Initialize`, never in the prefix (`.claude/rules/harmony-patches.md`).

## Changelog

- 2026-09-06: created with `Patch82_MapEventObserverInvariant` (#551).

## GitHub Issue

- **Issue:** #551, crash: enlisted player CTDs in MapEventSide.AllocateTroops when an unrelated battle ends during the join
- **Status:** Open

---

<!-- backlinks-start auto-generated; edit lint_docs.py / build_backlinks.py to change -->

## Referenced by

- [docs/features/enlistment.md](./enlistment.md)
- [docs/INDEX.md](../INDEX.md)
- [docs/reference/doc-lookup.md](../reference/doc-lookup.md)
- [docs/reference/feature-map.md](../reference/feature-map.md)

<!-- backlinks-end -->
