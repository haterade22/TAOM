# Return to Army

## Overview

One Harmony prefix, `Patch87_ReturnToArmy`, on vanilla's "Return to Army" settlement option. When
the player is a member of an army that is NOT physically here (the party is in the army but not
merged into it), the option now leaves the town or castle the way vanilla's own "Leave" does.
Every other case runs vanilla untouched: a member merged into the army keeps waiting with it, a
village keeps vanilla's own leave, and the army's leader never sees the option at all.

Issue #566. Reported 2026-09-12 as "I click Return to Army in Orthanc and it waits there".

## Why This Exists

- **Vanilla behavior:** `PlayerTownVisitCampaignBehavior.game_menu_return_to_army_on_consequence`
  (installed v1.4.8, :368-376) switches to the `army_wait_at_settlement` wait menu and calls
  `PlayerEncounter.LeaveSettlement()` + `Finish()` ONLY when the current settlement is a village.
  One method serves `town_return_to_army` (:63), `castle_return_to_army` (:142) and
  `village_return_to_army` (:158). Separately, `game_menu_town_town_leave_on_condition`
  (:994-1006) hides "Leave" for every army member who is not the leader. So inside a town or
  castle, an army member has exactly one exit, and it does not exit.
- **What the wait menu does next:** its tick (`PlayerArmyWaitBehavior.cs:181-196`) asks
  `DefaultEncounterGameMenuModel.GetGenericStateMenu()` (:211-330) where the player should be.
  For a party with `Army != null` and `AttachedTo == null`:
  - in a fortification of a DIFFERENT faction it answers `town_wait_menus` (:295-298, because the
    wait menu's init set `IsPlayerWaiting = true`). That is "You are waiting in Orthanc". Its only
    button, "Stop waiting", returns to the town menu. Loop.
  - in a fortification of the player's OWN faction it answers `army_wait_at_settlement` (:291-294),
    so the wait never ends: `Army.Tick` (`Army.cs:379-398`) merges a member only while that party
    is marching at the leader (`ShortTermTargetParty == LeaderParty`), which a party parked inside
    a settlement never is. "Leave Army" is on this menu, so this variant at least has an exit; the
    foreign-faction one does not, because the bounce happens on the first tick.
- **The case vanilla designed for:** a member merged into the army (`AttachedTo != null`). The army
  rests in the settlement, the player enters it from the wait menu, and when the leader moves,
  `LeaveSettlementAction.ApplyForParty(leader)` (`LeaveSettlementAction.cs:13-26`) finishes the
  player's encounter for him. That is correct and the patch leaves it alone.
- **How a player becomes an unattached member:** two ways.
  1. A [Player Switcher](player-switcher.md) takeover inherits the lord's `Army` untouched
     (`HeroSwitchService`, `PlayerIdentityAdapter.ApplyPlayerCharacter`; neither reads `Army` or
     `AttachedTo`). A lord marching to join an army is exactly `Army != null, AttachedTo == null`.
     This is how #566 happened.
  2. Vanilla's own "Sure. We will wait other parties around X for a while, then follow us"
     (`LordConversationsCampaignBehavior.cs:3510-3514`) sets `MainParty.Army` and nothing else.
  From either, `PlayerEncounter.Init` (:715-718) enters any non-hostile settlement immediately, so
  the town menu is reachable on foot.
- **Without this feature:** the player is stuck in the town with no menu exit. The only ways out
  are the army dispersing on its own, or the dev console.

TAOM code was ruled out first: no patch on `PlayerArmyWaitBehavior`, `PlayerTownVisitCampaignBehavior`,
`GameMenu` or `EncounterGameMenuModel`, no `EncounterGameMenuModel` override, and the
[enlistment](enlistment.md) menu redirect (`EnlistmentMenuService.cs:49-50`) returns before it
consults its table unless the player is `EnlistedAttached`. The live log of the reporting session
had zero `[Enlistment]` lines.

## Architecture

### Design Challenge

The option is one vanilla method with three registrations, and the correct behaviour depends on
state the method never reads: whether the player's party is merged into the army. Replacing the
whole method would inherit the wait-menu switch and the village leave; replacing nothing leaves
the trap. So the prefix decides, and runs vanilla for every row except the one that traps.

### Solution Approach

`ReturnToArmyRules.Decide(inArmy, isArmyLeader, attachedToArmy, inVillage)` is pure and carries the
whole policy:

| Row | Verdict | Why |
|---|---|---|
| not in an army | run vanilla | unreachable: the option's own condition hides it |
| the army's leader | run vanilla | unreachable: same condition, and the leader keeps "Leave" |
| merged into the army (`AttachedTo != null`) | run vanilla | the army is here; wait with it, leave with it |
| unattached, in a village | run vanilla | vanilla already leaves; doing it twice would `Finish` a dead encounter |
| unattached, in a town or castle | **leave the settlement** | #566 |

The leave replicates vanilla's own `game_menu_settlement_leave_on_consequence` (:1054-1068), all
public API and in vanilla's order: `MainParty.Position = CurrentSettlement.GatePosition`,
`PlayerEncounter.LeaveSettlement()`, `PlayerEncounter.Finish()`, `MainParty.SetMoveModeHold()`,
`Campaign.Current.SaveHandler.SignalAutoSave()`. Its `AttachedParties` loop is consciously
dropped: parties attach to the LEADER, and the leader never reaches this option. Nothing writes
`Army`, so the player stays a member and is free on the map; vanilla's "follow us" already leaves
the march back to him.

**Error paths, and why they differ.** Up to and including the gate-position write, a throw defers
to vanilla: nothing vanilla reads has changed (the party is still inside, so its map position is
inert), the old wait menu is a dead end but not a crash, and the error is logged. From
`LeaveSettlement` on, vanilla is NOT a safe default: it has nulled `CurrentSettlement`, which
vanilla's :371 dereferences unguarded. So a throw past that point skips vanilla and is logged as an
error (the "fall through to vanilla on error is only safe when vanilla is safe at THAT call site"
lesson in [harmony-il.md](../reviews/lessons/harmony-il.md)). The review's first draft drew that
line one statement too early, at the top of the block; the RCA records why.

**Why the map is paused afterwards.** `PlayerEncounter.Finish` forces `TimeControlMode = Stop`
only for a party with no army (:1015), so on its own it would leave time as it was for an army
member. It does not act on its own: with the menu still up it calls `GameMenu.ExitToLast`
(:1019-1021), and that writes Stop unconditionally (`GameMenu.cs:376-378`). The player lands on the
map paused, exactly as after vanilla's own Leave, and the time-control lock is not involved (it is
set only around the character portrait popup, `CampaignEvents.cs:2230-2242`). Single-player only:
the installed CoopNightly build patches the game-menu time writes (`ExitToLast`, `SwitchToMenu`,
`ActivateGameMenu`, `StartWait`, `EndWait`) into a no-op, so under that mod the outcome is whatever
the co-op mod decides, for vanilla Leave and for this patch alike (Codex review 98).

### Component Diagram

```
town / castle / village menu: "Return to Army"
        |
  Patch87 prefix
        |  reads MainParty.Army, .AttachedTo, .CurrentSettlement.IsVillage
        |
  ReturnToArmyRules.Decide  -> RunVanilla       -> vanilla: wait menu (+ leave, villages only)
                            -> LeaveSettlement  -> gate position, LeaveSettlement, Finish,
                                                   SetMoveModeHold, autosave; skip vanilla
```

## Configuration

None. There is nothing to tune: the party is either merged into its army or it is not.

## Key Files

| File | Purpose |
|------|---------|
| `Main/Features/ReturnToArmy/ReturnToArmyRules.cs` | The pure decision |
| `Main/Features/ReturnToArmy/Hooks/Patch87_ReturnToArmy.cs` | The prefix, `Initialize(IModLogger)`, `ResetForUnload()` |
| `Main/SubModule.cs` | `Initialize` + `PatchCategory("Patch87_ReturnToArmy")` in the standard `OnGameInitializationFinished` batch, and `ResetForUnload` |

No service, no adapter, no IoC registration. The logic is four boolean reads and five public engine
calls; a service layer here would be indirection with nothing to hold (`simplicity-criterion.md`).

## Dependencies

- `IModLogger` (Core/Logging): passed in at `Initialize`, never resolved in the prefix.

## Tests

`TAOM.Tests/Features/ReturnToArmy/Patch87ReturnToArmyTests.cs`, 12 tests:

- Six on the pure `Decide`: one per row of the table, plus a loop over all sixteen input
  combinations asserting that exactly one may skip vanilla.
- Six binding drift-guards (`BindingVerification`): the target resolves as a private instance
  method with one `MenuCallbackArgs` parameter and one overload; its condition still exists; the
  vanilla body still reaches `SwitchToMenu`, `LeaveSettlement`, `Finish` and `get_IsVillage` (call
  presence only, not branch shape); every engine member the leave sequence reads or calls resolves
  with the expected shape (the target is private and non-static, `Finish(bool)` keeps its default
  and that default is still `true`, `Position` and `GatePosition` share a type,
  `Campaign.SaveHandler` is still a `SaveHandler`); all three registration sites; and an IL scan
  proving the prefix still calls `Decide` and the full leave sequence.

`TAOM.Tests/Features/CoopInterop/CoopVetoClassificationTests.cs` classifies the prefix as
`ReviewedSafe`: what it skips is a menu switch (per-peer presentation), and what it runs instead is
the player's own vanilla "Leave" on the local main party, which reaches the co-op mod's existing
`LeaveSettlementAction` interception exactly as vanilla Leave does. That is a same-integration
argument, not a claim that two peers hold identical snapshots (replication is asynchronous); a
two-peer run is unverified.

**What the drift guards cannot see.** The IL tests pin call PRESENCE. An engine refactor that keeps
the same calls but changes which branch leaves (say, castles too) passes them; Codex review 98
demonstrated this with a synthetic body. The consequence would not be a double leave (the prefix
returns false and vanilla never runs on that row) but a silently suppressed new vanilla behaviour.
So at every engine bump, re-read `game_menu_return_to_army_on_consequence` by hand and re-derive
the decision table; the registry entry says the same.

**The leave itself is not unit-testable.** It needs a live `PlayerEncounter` and a captured
settlement, so it is verified in game (below). The tests pin what the patch BINDS to, so an engine
rename fails a test instead of silently restoring #566.

## How to tell whether it fired

Search the TAOM debug log for `[ReturnToArmy]`:

- `left <settlement> as an unattached member of <leader>'s army` is the fix doing its job. One line
  per click, never per tick.
- `could not read the party's army state, deferring to vanilla's wait menu` means the prefix threw
  before touching anything. The player is back in the #566 trap; the exception text says why.
- `the leave sequence threw after mutating` is an ERROR, not a fallback. Something between the gate
  move and the autosave signal failed; vanilla was skipped on purpose because its body would now
  dereference a null `CurrentSettlement`.

## Interactions

- **Enlistment:** an enlisted soldier never sees this option. Enlistment keeps `MainParty.Army`
  null outside a battle (#443), so vanilla's condition hides it. The redirect list in
  `IEnlistmentConfigProvider` does carry `army_wait_at_settlement`, but the redirect is gated on
  `EnlistedAttached` before the list is read.
- **Player Switcher:** a takeover inherits the lord's army membership, attached or not. Attached
  means the player starts inside the army wait menu and moves with the army, exactly as a player
  who joined normally. Unattached means the player is free and this patch is what keeps a town from
  becoming a cage. Whether a takeover should clear `Army` instead is an open design question, left
  open on purpose: vanilla supports both states for a player.
- **Co-op:** `ReviewedSafe`, see Tests.

## What was deliberately not done

- **Marching the player back to the leader.** `MobileParty.SetMoveEngageParty(leader, NavigationType)`
  (`MobileParty.cs:3918`) is the one-liner, and it is what a map click on the army does. Declined
  by the reporting user in favour of a plain leave; the player steers.
- **Restoring "Leave" for army members.** It would change vanilla's UX for the attached case too,
  where hiding it is correct.
- **Changing the option text.** It still reads "Return to Army"; in the unattached case it now
  means "go back out and return to it".

## In-game verification (owed)

1. Repro state: take over a lord who is marching to join an army, or accept "follow us" from a
   gathering leader. Enter a town of a faction you are not at war with. "Return to Army": expect the
   party outside the gate on the map, still in the army (Kingdom > Armies), time stopped, no menu.
   Before the fix: "You are waiting in <town>".
2. Same in a town of your own faction, and in a castle.
3. Attached case: the army rests in a town, "Enter the Town", then "Return to Army": expect vanilla's
   "You are following X's army" wait, and the player leaves when the leader does.
4. Village: unchanged.
5. Not in an army: option absent, "Leave" present.

## Changelog

- 2026-09-12: created with `Patch87_ReturnToArmy` (#566).

## Reviews

- Deep review (five agents) and the Codex pass (review 98, GPT-6-Astra at ultra, no P1 or P2):
  [rca-return-to-army-2026-09-12.md](../reviews/rca-return-to-army-2026-09-12.md). Prompt:
  [codex-adversarial-return-to-army-2026-09-12.prompt.md](../reviews/codex-adversarial-return-to-army-2026-09-12.prompt.md).
  Entry 98 in [REVIEW-LOG.md](../reviews/REVIEW-LOG.md).

## GitHub Issue

- **Issue:** #566, "Return to Army" strands an unattached army member inside a town or castle
- **Status:** Open, pending the in-game smokes above

---

<!-- backlinks-start auto-generated; edit lint_docs.py / build_backlinks.py to change -->

## Referenced by

- [docs/features/player-switcher.md](./player-switcher.md)
- [docs/INDEX.md](../INDEX.md)
- [docs/reference/doc-lookup.md](../reference/doc-lookup.md)
- [docs/reference/feature-map.md](../reference/feature-map.md)
- [docs/reviews/REVIEW-LOG.md](../reviews/REVIEW-LOG.md)

<!-- backlinks-end -->
