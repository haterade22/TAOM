# RCA: a stranded PlayerEncounter could latch enlisted service permanently (#538)

**Date:** 2026-09-04 · **Feature:** Enlistment (reconciler, encounter ownership) · **Issue:** [#538](https://github.com/haterade22/TAOM/issues/538)
**Review:** `/deep-review`, 2 agents (standards, cross-system data flow). 1 MEDIUM + 2 HIGH + assorted LOW, all verified against source by the author before acting.
**Suite after fixes:** `./build.ps1 -RunTests` → 8020 passed, 0 failed, 2 skipped. `lint_docs.py` clean.

## Top line

A player reported being stranded after a siege: unable to move, unable to interact, with the
enlistment UI gone. No log existed for it, so the cause was found by reading code.

Two recovery mechanisms, written months apart, had become each other's precondition.
`ServiceMaintenanceService.TryBreakBattleLatch` is the only exit from `EnlistedBattle` when no battle
is running and returns early while `presence.HasPlayerEncounter`. The reconciler's
stranded-encounter sweep is the only thing that closes a stale encounter and required
`State == EnlistedAttached`. `EnlistedBattle` plus a stranded encounter was therefore permanent.

**The more valuable half of this RCA is the fix, not the bug.** The first version would have replaced
a permanent strand with a torn-down loot screen in the same scenario. The deep review caught it.

## Findings

| # | Sev | Bug | Category | Why missed | Preventive action |
|---|---|---|---|---|---|
| B1 | HIGH | The deadlock itself: two self-heals each waiting on what only the other clears | Cross-mechanism lifecycle | Each guard is locally correct; neither file references the other; unit tests covered each in isolation and both passed | Sweep no longer proxies the hazard with a state; `SweepStrandedEncounter` shared by both paths; regression test that ticks the exact shape |
| F1 | HIGH | Deleting the state gate let the sweep run inside the battle loot/aftermath window, tearing down the player's own siege results | Guard removal on a plausible-but-wrong rationale | `MapEventSide.Clear()` nulls `MainParty.MapEvent` BEFORE the encounter closes, so every remaining guard reads "no battle anywhere". Two other places in the feature documented that window in comments the author had read for other reasons | `EncounterOwnershipSnapshot.IsBattleEncounter` + policy R1b, universal across intents. Regression test `Reconcile_BattleAftermathEncounter_IsNeverTornDown` |
| F2 | HIGH | R2c trusted the caller for `!PlayerInsideSettlement`, and the caller read it from a snapshot captured earlier in the tick. The committed test asserted `Finish` for a snapshot built with `playerInsideSettlement: true` | Precondition-by-contract; test encoding the wrong state as correct | The snapshot already carried a freshly-read `PlayerInsideSettlement` that the policy never looked at. The test helper's name (`SettlementVisit`) read as generic while its content was specific | R2c enforces the precondition itself. Test split into `...SettlementShapedButPlayerIsOut_Finish` and `...PlayerActuallyInsideSettlement_SkipsNotOurs` |
| F3 | MED/HIGH | `taom.rescue_service` could leave the party inside a settlement, immobile, which is what it promises to fix | Engine-property trap | `PlayerEncounter.InsideSettlement` short-circuits on `MainParty.IsActive`, which is false for a parked enlisted player, so it returns false for someone who IS inside a settlement | Read `MainParty.CurrentSettlement` directly; call `LeaveSettlement()` explicitly, as `DischargeService` does |
| F4 | MED | The rescue duplicated the commander-fitness predicate and had already drifted (omits `HasParty`) | Re-derived service-owned decision | Four copies of this predicate now exist; `CommanderTickSnapshot.IsFollowable` is the canonical one but lives on the other snapshot type | Recorded; a shared `IsFollowable` on `CommanderSnapshot` is the right convergence and is deliberately not folded into a bug fix |
| F5 | LOW | A successful settlement exit with a failed re-park logged "the player is stuck inside a settlement" (untrue) and skipped the encounter finish | Two outcomes folded into one bool | `ExitSettlementForService` returns `LeaveSettlement() && ParkNear()`, and `ParkNear` fails exactly when the commander is unfindable, which is the grace window | Message corrected; encounter finish attempted regardless, safe because R2c re-reads the settlement itself |
| F6 | LOW | Stale comments: `ShoreLeaveEnd` documented as "the ONLY intent" that inverts R3; R2c documented as caller-owed | Documentation drift introduced by the same change | Written before the review changed both facts | Both corrected in the same commit |
| F7 | LOW | New intent was hand-listed into some universal-rule `DataRow` sets and missed others (no R0 case) | Test enumeration by hand | The universal-rule tests predate the enum having a stable shape | Enum-driven tests over `Enum.GetValues(typeof(EncounterFinishIntent))` for R0, R1 and R1b, self-maintaining for the next intent |

## Root-cause pattern: a guard that proxies its hazard lets two mechanisms disagree about what they protect

B1 and F1 are one shape seen from both ends.

`TryBreakBattleLatch` used "is a `PlayerEncounter` open?" as a proxy for "is a battle in progress?".
The sweep used "is the state `EnlistedAttached`?" as a proxy for the same thing. Both proxies are
reasonable, both are usually right, and they disagree in exactly the window where it matters. The
deadlock is the disagreement in one direction; the aftermath teardown is the disagreement in the
other. Naming the hazard directly (`IsBattleEncounter`) collapses both.

The corollary is the one worth carrying: **when a codebase already contains a predicate over the same
subsystem, diff yours against it term by term.** The sweep's guard set was literally
`noBattleAnywhere` minus its `!HasCurrent` term. That single missing term is the whole of F1, and it
was visible in the same file, eighty lines up, the entire time.

## Why the author missed these

- **B1** was found by reading, not by testing, and would not have been found by testing: the suite
  exercises each mechanism alone and both pass. Nothing enumerates pairs of recovery paths.
- **F1** followed from a rationale that felt principled. "The state is a proxy, the map-event checks
  are the real condition" is true of the deadlock and false of the aftermath, and the author checked
  it against the case in hand rather than against the excluded set.
- **F2** is the ordinary shape of a caller-owed precondition: the contract was documented carefully,
  in the intent's own XML doc, which is exactly the sort of documentation that reads as diligence
  while the field needed to enforce it sat unread in the snapshot the policy already received.
- **F3** is an engine-property trap of the kind `.claude/rules/adapters.md` exists for. The property
  name is a complete description of what the author wanted and an incomplete description of what it
  does.

## Lessons codified

Appended to `docs/reviews/lessons/state-lifecycle-save.md`:

1. Two independent self-heals can become each other's precondition, and neither will ever fire.
2. A guard you delete because "it was never the real guard" may be load-bearing for a case you have
   not named.

## Still owed

The in-game run. Diagnostics are already on (`39b0b3ce`). Enlist, follow the commander into a
settlement, then have him besiege one; watch for the sweep line, and confirm a fought siege still
shows its loot screen. Every claim in this document is from source and the installed v1.4.8
decompile, not from a game that produced the shape.
