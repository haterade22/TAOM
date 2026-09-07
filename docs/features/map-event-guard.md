# Map Event Guard

## Overview

Harmony prefixes that hold up engine invariants the campaign relies on but never re-checks. Each one
sits under a pure-vanilla `NullReferenceException` whose stack contains no TAOM frame at all.

| Patch | Invariant it holds up | Crash it stops |
|---|---|---|
| `Patch82_MapEventObserverInvariant` | A `MapEvent` with a `BattleObserver` also has a `TroopUpgradeTracker` | CTD on the next simulation tick, `MapEventSide.AllocateTroops` (#551) |
| `Patch84_SiegeAftermathMenuGuard` | `SiegeAftermathCampaignBehavior._besiegerParty` and its `CurrentSettlement` are non-null when an aftermath menu opens | CTD returning to the map after a siege, `menu_settlement_taken_player_participant_on_init` (#557) |

These are backstops, not features. They change nothing a player can see, and on a healthy campaign
each prefix returns on its first comparison.

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

## Patch84: the siege aftermath menus

### Why This Exists

- **Vanilla behavior:** `menu_settlement_taken_player_participant_on_init` (installed v1.4.8) opens
  with `Settlement currentSettlement = _besiegerParty.CurrentSettlement;` and then reads
  `currentSettlement.GetName()`. Neither is null-guarded, even though the very next line guards the
  character chain as `LordPartyComponent?.Owner?.CharacterObject ?? CharacterObject.PlayerCharacter`.
  `menu_settlement_taken_player_army_member_on_init` is worse: three unguarded dereferences of
  `_besiegerParty`, the first being `.Army` at :325, a line ahead of its own `.CurrentSettlement` at
  :326, and `.CurrentSettlement.Culture` again at :349. The guard takes its verdict before either
  body runs, so it covers whole methods rather than individual lines.
- **How the field goes null:** `_besiegerParty` is a SAVED field assigned in exactly one place,
  inside `OnMapEventEnded`'s `if (mapEvent.GetMapEventSide(Attacker).IsMainPartyAmongParties())`
  block. When the main party is not among the ending event's parties, the whole block is skipped, so
  `_besiegerParty` keeps whatever the save held and `_wasPlayerArmyMember` is never set.
  `menu_settlement_taken_on_init` routes on that unset flag straight into the participant menu.
- **Without this feature:** crash bundle `d7d9f7d3` (issue #557). An enlisted soldier on the winning
  attacker side of the siege of East Osgiliath, on a campaign at renown 1 with no earlier
  player-participating siege, so the saved `_besiegerParty` was null.

### Why the player was no longer in the event

**TAOM's own detach removes him, and the campaign-event listener order is why.** The first write-up
of this feature claimed the opposite and said ordering ruled the detach out. That was wrong. The
correction is recorded in full because the mistake is cheap to repeat.

`CampaignEvents.MapEventEnded` is an `MbEvent<MapEvent>`. Its `AddNonSerializedListener` (installed
v1.4.8, :24-30) HEAD-INSERTS each listener into a singly-linked list, and `Invoke` (:32-35) walks
from the head:

```csharp
public void AddNonSerializedListener(object owner, Action<T> action)
{
    EventHandlerRec<T> eventHandlerRec = new EventHandlerRec<T>(owner, action);
    EventHandlerRec<T> nonSerializedListenerList = _nonSerializedListenerList;
    _nonSerializedListenerList = eventHandlerRec;   // new listener becomes the HEAD
    eventHandlerRec.Next = nonSerializedListenerList;
}
```

Registration is therefore **LIFO: the last listener registered is the first invoked.** TAOM adds its
campaign behaviours in `SubModule.OnGameStart`, deliberately after SandBox has registered its own
(see the comment at `SubModule.cs:721`), so TAOM registers later, sits at the head, and runs **first**.

The chain that follows:

| Step | Site |
|---|---|
| TAOM's listener fires first on the dispatch | `EnlistmentBattleBehavior.OnMapEventEnded` |
| It calls the battle-end service | `ServiceBattleService.OnCommanderBattleEnded` |
| Which detaches unconditionally | `ArmyMembershipAdapter.LeaveArmy` sets `MainParty.AttachedTo = null` |
| The engine answers a cleared `AttachedTo` | `MobileParty.SetAttachedToInternal` :1780-1783 runs `HandleMapEventEndForPartyInternal` then `Party.MapEventSide = null` |
| Whose setter drops the party from the side | `MapEventSide.RemovePartyInternal` removes it from `_battleParties` |
| Vanilla's listener then runs against the emptied list | `IsMainPartyAmongParties()` is false, the assignment block is skipped, `_besiegerParty` stays null |

`MapEventSide.Parties` IS `_battleParties`, the same collection `MapEvent.InvolvedParties` tracks, so
the two views cannot disagree. This is the same seam as #551, landing on a different victim.

Consistent with the bundle: TAOM's own diagnostic logged `mainAmong=True` at `MissionOpenNew`, so the
party was in the attacker side at battle start and gone by the end, which is exactly what a
same-dispatch pre-emptive detach produces. And `_wasPlayerArmyMember` was never set, which is why the
routing landed on the participant menu rather than the army-member one.

**Patch84 remains the right floor, and the ordering is now fixed too.**
`Patch85_EnlistedDetachDeferral` moves the detach out of the dispatch into the one-statement window
between `PlayerEncounter.FinalizeBattle()` :1071 and `FinishEncounterInternal()` :1072, so vanilla
reads an intact party list while the post-defeat escape grant still sees a detached party. That
removes the path TAOM creates. It does not make vanilla's two menus null-safe, and they stay
reachable by any player who leaves a winning siege's party list for any other reason, so both ship.
See the Patch85 section of [`harmony-patch-registry.md`](../reference/harmony-patch-registry.md).

### The repair

`Decide` is pure and carries the whole policy:

| Verdict | When | What the prefix does |
|---|---|---|
| `RunVanilla` | Both of vanilla's dereferences would succeed | Returns true, vanilla runs untouched |
| `RepairWithSettlement` | Vanilla would throw, but `_besiegerParty?.CurrentSettlement ?? Settlement.CurrentSettlement` resolves | Rebuilds the body from vanilla's own key, naming the settlement |
| `RepairWithoutSettlement` | Nothing names the settlement | `TextObject.GetEmpty()`, which is how vanilla itself degrades the army-member menu |

**Guarding only the party would move the crash one line down**, onto `currentSettlement.GetName()`.
Both terms are in the verdict for that reason, and a test pins the case.

**No new player-facing strings, so no `/localize` pass.** The repair reuses the engine's own keys
`{=C2KeQd0a}` and `{=hvQUqRSb}`, copied verbatim from installed v1.4.8 so the shipped translations
keep resolving. Do not reword the defaults: the key carries the other languages. The army-member
aftermath flavour sentence is deliberately not reproduced, because it branches on
`_playerEncounterAftermath` and `_wasPlayerArmyMember` and those were never assigned on this path
either, so reproducing it would invent a decision the game did not make.

## Key Files

| File | Purpose |
|------|---------|
| `Main/Features/MapEventGuard/Hooks/Patch82_MapEventObserverInvariant.cs` | Cached binding, prefix, repair for the observer/tracker pairing |
| `Main/Features/MapEventGuard/Hooks/Patch84_SiegeAftermathMenuGuard.cs` | Shared binding plus `Decide`, and the two menu prefix classes |
| `Main/Features/Enlistment/Hooks/EnlistmentBattleBehavior.cs` | `LogSiegeAftermathContext`, the siege-only diagnostic that will close #557's open question |
| `Main/SubModule.cs` | `Initialize(...)` plus `PatchCategory(...)` for both categories in the `OnGameInitializationFinished` batch, and `ResetForUnload` |

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

- `TAOM.Tests/Features/MapEventGuard/Patch84SiegeAftermathMenuGuardTests.cs` holds 9 tests. Five run
  directly on the pure `Decide`, including the case that would move the crash one line down (besieger
  present, its settlement null) and a loop asserting a null besieger is never handed to vanilla. The
  rest pin the behaviour type, the `_besiegerParty` field and that it is still a `MobileParty`, both
  menu inits with arity and overload counts, the `menu_settlement_taken_on_init` router that reaches
  them, and all three registrations for BOTH patch classes. They share one category, so a missing
  attribute on either silently halves the guard.
- `TAOM.Tests/Features/CoopInterop/CoopVetoClassificationTests.cs` classifies both Patch84 prefixes as
  `ReviewedSafe`: the skipped bodies set menu text variables and a background mesh only, they mutate
  no campaign state at all, and they are skipped solely on the path vanilla cannot survive.

**The behaviour itself is not unit-testable.** `MapEvent` has no public constructor, `MenuContext` and
a captured settlement need a live campaign, so these tests pin what the patches BIND to and the
repairs are verified in game. That split is deliberate: every fact these patches stand on is an
engine detail they cannot see change, and a rename in any of them would make the guard silently
inert.

## How to tell whether it ever fired

Search the TAOM debug log for `[MapEventGuard]`:

- `cleared a dangling BattleObserver ...` means Patch82 caught a real break. Something removed the main
  party from a live map event while its battle UI stayed attached. Find that, because the guard is
  the floor, not the fix.
- `repaired the '<menu>' siege aftermath menu ...` means Patch84 caught the #557 crash. The line names
  which of vanilla's two dereferences was null plus the army and attachment state. A null
  `besiegerParty` means vanilla skipped its `OnMapEventEnded` assignment block, which means the main
  party was not among the ending event's parties; pair it with the `[EnlistDiag] siege map event
  ended:` line from the same battle, which carries `mainPartyInvolved`.
- `... did not resolve ...` (either patch) means the binding failed on this engine build and that
  guard is inert. Re-derive it against the new shape; the binding tests should have caught this first.
- `the '<menu>' siege aftermath guard threw and is deferring to vanilla` means Patch84's own body
  failed. Vanilla is about to crash exactly as it did in `d7d9f7d3`. This is an ERROR, not a
  fallback.

## Performance

The prefix runs for every live map event on its own simulation timer, which is many times a second at
accelerated campaign speed, so ordering matters: `TroopUpgradeTracker != null` is tested first and is
true for every ordinary AI battle, so the common case is one property read and a return. Reflection is
resolved once in `Initialize`, never in the prefix (`.claude/rules/harmony-patches.md`).

## Changelog

- 2026-09-06: created with `Patch82_MapEventObserverInvariant` (#551).
- 2026-09-07: added `Patch84_SiegeAftermathMenuGuard`, two prefixes over the siege aftermath menus,
  plus the siege-only diagnostic in `EnlistmentBattleBehavior` (#557, crash bundle `d7d9f7d3`).

## GitHub Issue

- **Issue:** #551, crash: enlisted player CTDs in MapEventSide.AllocateTroops when an unrelated battle ends during the join
- **Issue:** #557, crash: siege aftermath menu NREs on a null `_besiegerParty` when the player is not in the capturing army
- **Status:** Open

---

<!-- backlinks-start auto-generated; edit lint_docs.py / build_backlinks.py to change -->

## Referenced by

- [docs/features/enlistment.md](./enlistment.md)
- [docs/INDEX.md](../INDEX.md)
- [docs/reference/doc-lookup.md](../reference/doc-lookup.md)
- [docs/reference/feature-map.md](../reference/feature-map.md)

<!-- backlinks-end -->
