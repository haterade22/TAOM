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

A third guard is a campaign behavior, not a patch: the stuck AI battle sweep (see
[Stuck AI battles](#stuck-ai-battles)) ends map battles the engine can never finish on its own.

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

## Stuck AI battles

### The report

A player found an AI battle on the campaign map frozen at 8000 against 0 in two campaigns (Gundabad
once, Goblins once). Lords of the winning faction kept joining it, so the whole faction stood still,
and the player could join neither side. They traced the engine cause themselves and sent a workaround
module, Stuck Battle Guard v1.0.0 (see the
[provenance register](../reference/provenance-register.md#stuck-battle-guard-uncleared)). TAOM's guard
is written from that diagnosis, re-checked here against the installed v1.5.4.

### Why the engine never ends it

Read from the v1.5.4 decompile (`taom-src path TaleWorlds.CampaignSystem.MapEvents.MapEvent`):

- `MapEvent.Update` runs a simulation round only while
  `DefenderSide.TroopCount > 0 && AttackerSide.TroopCount > 0`, or before its first update.
  `TroopCount` is `RecalculateMemberCountOfSide()`, the sum of `NumberOfHealthyMembers`; wounded troops
  do not count.
- A winner is set only inside a round (`CalculateWinner` from `SimulateBattleRound`), and `Update`
  finishes the event only once `BattleState` is not `None`.
- So a side that reaches 0 healthy troops between rounds leaves the event open for good: no round
  runs, so no winner is ever set.
- `CanPartyJoinBattle` requires every party on both sides to be `IsActive`. A party destroyed while
  attached stays in the event inactive, and every joiner on both sides is then refused.
- The engine's only other exits are `DiplomaticallyFinished` (the two factions stop being at war) and
  a side losing its last party (`MapEventSide.RemovePartyInternal` calls `FinalizeEvent`). During the
  War of the Ring, `MakePeaceAction_ApplyInternal_Patch` (Patch12) blocks peace between Hostile-tier
  kingdoms, so the console `declare_peace` workaround does nothing there.

What drops a side to 0 healthy between rounds is not known yet. Vanilla's own roster changes outside a
battle skip a party that sits in one (desertion, `DesertionCampaignBehavior`; healing and starvation
wounds, `PartyHealCampaignBehavior`), so wounded troops never heal back and nothing vanilla empties a
side there. TAOM's daily alignment desertion has no such check and is the one known TAOM producer
(#750). The sweep ends the battle whatever the trigger was.

A destroyed party left attached is rarer than it sounds: vanilla's `DestroyPartyAction` detaches a party
it destroys, except a party a quest is using. Any other route that removes a party without detaching it
leaves the second lock.

### What the sweep does

`StuckBattleCampaignBehavior` calls `StuckBattleService.Resolve` once per in-game hour. For each live map
event, `Decide` returns one verdict, and the sweep makes at most one write per battle per pass:

| State | Verdict |
|---|---|
| A co-op session is live, or a co-op mod TAOM cannot probe is loaded (`IsSessionActive`, `ShouldDeferToHost`) | No sweep at all |
| The player's battle (in it, or standing in its encounter), or an outcome already pending | None |
| Younger than 12 in-game hours (NaN and infinite ages fail this gate) | None |
| A destroyed mobile party still attached, other than the player's and a quest's | Detach it (`party.MapEventSide = null`, the write `DestroyPartyAction` makes); the next pass judges the event afresh |
| A raid, forced supplies or forced volunteers | None: the raid component drives a raid, and a village has no healthy defenders by design |
| Attackers at 0 healthy, defenders at 0 or more | `SetOverrideWinner(Defender)` |
| Defenders at 0 healthy, attackers healthy | `SetOverrideWinner(Attacker)` |
| Both sides healthy | None |
| Any battle holding the enlisted player's commander party | None (asked last, only for a battle the sweep would act on) |

`SetOverrideWinner` is public. Its `BattleState` setter calls `OnBattleWon`, which computes and commits
the results of an AI event, and the next `Update`, in the same campaign frame (`Campaign.Tick` fires the
hourly event, then `MapEventManager.Tick`), finishes it. The healthy side wins with the usual prisoners
and loot, which is what the missed round would have produced; no prisoners are taken while a side is
retreating, as in any battle. Mike chose this over the player module's approach (end every stuck battle
with no winner) on 2026-10-06.

**Both sides at 0: the defenders hold.** That is the engine's own rule for judging sides outside a round
(`MapEvent.CheckIfOneSideHasLost`). The no-winner exit (`DiplomaticallyFinished`) was rejected: with no
winner, the engine destroys every party on both sides that has no healthy troops and makes its leader a
fugitive (Mike, 2026-10-06).

**Sieges.** A siege assault ends the way a natural win does: on finalize, `SiegeAssaultEventComponent`
fires `SiegeCompleted` with `isWin: true` for an attacker victory (the event a natural assault win fires),
and clears the besieger camp for a defender victory. The sally-out and outside-battle components turn a
winner into their own siege results the same way. Whatever a natural win at an empty garrison does to
the settlement, a swept one does too.

**After a detach** the winner, if one is owed, lands on the next pass, one in-game hour later. In that
hour a party the detach unblocked can join and let a normal round decide. A detach that empties a side
finalizes the event inside the engine.

**The enlisted commander's battle** is left alone because `EnlistmentBehavior`'s hourly tick runs after
this one and would join the player into a decided, unfinished event. Known limitation: a commander stuck
in such a battle stays stuck while the player is enlisted.

**Co-op** is not supported yet. BannerlordCoop's `MapEvent.Update` prefix stops vanilla finishing a
battle that holds a remote player's party, and the player-battle test sees only the local player, so an
award there would commit and never finish. Teaching the sweep to recognise remote players needs Coop's
party-control API (follow-up in #748).

### Console

`taom.print_stuck_battles` (tier A, cheat-gated): every live map event, its healthy counts, age, attached
destroyed parties and verdict. Changes nothing; the hourly sweep does the work. To check a fix in game,
print, let an in-game hour pass, and print again.

## Key Files

| File | Purpose |
|------|---------|
| `Main/Features/MapEventGuard/StuckBattleService.cs` | The co-op stand-down, the verdict, the grace window and the one-write-per-pass sweep |
| `Main/Features/MapEventGuard/StuckBattleSnapshot.cs`, `StuckBattleVerdict.cs` | The engine-free read of one event, and the four verdicts |
| `Main/Features/MapEventGuard/Hooks/StuckBattleCampaignBehavior.cs` | The hourly tick |
| `Main/Features/MapEventGuard/Cheats/StuckBattleCheats.cs` | `taom.print_stuck_battles` |
| `Main/Adapters/IStuckBattleAdapter.cs`, `StuckBattleAdapter.cs` | The live map events, one per-event adapter each |
| `Main/Adapters/IStuckBattleEventAdapter.cs`, `StuckBattleEventAdapter.cs` | One `MapEvent`: the snapshot read, the label, the detach and the award |
| `Main/Features/MapEventGuard/StuckBattleModule.cs`, `Main/Composition/FeatureModules.cs` | Feature-module wiring (no patch, no saved data) |
| `Main/Features/MapEventGuard/Hooks/Patch82_MapEventObserverInvariant.cs` | Cached binding, prefix, repair for the observer/tracker pairing |
| `Main/Features/MapEventGuard/Hooks/Patch84_SiegeAftermathMenuGuard.cs` | Shared binding plus `Decide`, and the two menu prefix classes |
| `Main/Features/Enlistment/Hooks/EnlistmentBattleBehavior.cs` | `LogSiegeAftermathContext`, the siege-only diagnostic that will close #557's open question |
| `Main/SubModule.cs` | `Initialize(...)` plus `PatchCategory(...)` for both categories in the `OnGameInitializationFinished` batch, and `ResetForUnload` |

The two patches have no service, no adapter and no IoC registration. Their logic is a null comparison
and a property write; a service layer there would be indirection with nothing to hold
(`simplicity-criterion.md`). The stuck-battle sweep makes a real decision, so it has the full stack.

## Dependencies

- `IModLogger` (Core/Logging): passed in at `Initialize`, never resolved in the prefix.
- Stuck battles: `IEnlistmentStateQuery` (skip the enlisted commander's battle) and
  `ICoopSessionProvider` (no sweep while a co-op session is live or a co-op mod cannot be probed).

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

- `TAOM.Tests/Features/MapEventGuard/StuckBattleServiceTests.cs` covers `Decide`: each empty side, both
  empty (the defenders hold), both healthy, the grace edge either side of 12 hours, NaN and infinite ages,
  the player, pending-outcome and commander skips (and a commander elsewhere still resolving), the
  commander lookup never asked for a battle left alone, raids, and destroyed parties.
- `TAOM.Tests/Features/MapEventGuard/StuckBattleSweepTests.cs` covers `Resolve` and `Describe` over
  substituted per-event adapters: one write per pass, the label read before the write and never for a
  battle left alone, only the stuck one of two battles written, one throwing battle not stopping the
  rest, and the co-op stand-down for a live session and for an unprobeable co-op mod.
- `TAOM.Tests/Features/MapEventGuard/StuckBattleWiringTests.cs` pins the module: listed once, one
  behavior, no patch or saved data, and a container that builds the service.

The per-event adapter itself is not unit-testable (`MapEvent`'s constructor is internal); it is verified
in game with `taom.print_stuck_battles`.

**The patches' behaviour itself is not unit-testable.** `MapEvent` has no public constructor, `MenuContext` and
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
- `[StuckBattle] <attacker> vs <defender>: ...` (INFO) means the sweep ended or unblocked a battle; the
  line says which: `removed N wrecked parties that blocked joiners`, `the attackers win`, `the defenders
  win` or `the defenders hold`.
- `[StuckBattle] could not read a map event` (WARNING) means a battle threw while being read and was
  left alone this pass; the sweep tries it again next hour, so it repeats hourly while the state lasts.
- `[StuckBattle] resolving a map event failed` (WARNING) means the write itself threw, which can follow
  a partial write: an award sets the winner before the engine commits the results, and a detach can
  throw after earlier parties have left. Read the battle with `taom.print_stuck_battles`.
- `[StuckBattle] hourly sweep failed` means the whole pass threw.

## Performance

The prefix runs for every live map event on its own simulation timer, which is many times a second at
accelerated campaign speed, so ordering matters: `TroopUpgradeTracker != null` is tested first and is
true for every ordinary AI battle, so the common case is one property read and a return. Reflection is
resolved once in `Initialize`, never in the prefix (`.claude/rules/harmony-patches.md`).

The stuck-battle sweep runs once per in-game hour, which at 16x campaign speed is several times a real
second. Each pass reads each live event's parties once and renders no names unless it writes a line.
The enlisted-commander lookup runs only while the player is enlisted, and only for a battle the sweep
would act on.

## Changelog

- 2026-09-06: created with `Patch82_MapEventObserverInvariant` (#551).
- 2026-09-07: added `Patch84_SiegeAftermathMenuGuard`, two prefixes over the siege aftermath menus,
  plus the siege-only diagnostic in `EnlistmentBattleBehavior` (#557, crash bundle `d7d9f7d3`).
- 2026-10-06: added the stuck AI battle sweep, from a player's report and diagnosis (#748). Not yet
  verified in game.

## GitHub Issue

- **Issue:** #551, crash: enlisted player CTDs in MapEventSide.AllocateTroops when an unrelated battle ends during the join
- **Issue:** #557, crash: siege aftermath menu NREs on a null `_besiegerParty` when the player is not in the capturing army
- **Issue:** #748, AI map battles that can never end (a side at 0 healthy troops, or a destroyed party left attached)
- **Status:** Open

---

<!-- backlinks-start auto-generated; edit lint_docs.py / build_backlinks.py to change -->

## Referenced by

- [docs/features/enlistment.md](./enlistment.md)
- [docs/INDEX.md](../INDEX.md)
- [docs/reference/doc-lookup.md](../reference/doc-lookup.md)
- [docs/reference/feature-map.md](../reference/feature-map.md)

<!-- backlinks-end -->
