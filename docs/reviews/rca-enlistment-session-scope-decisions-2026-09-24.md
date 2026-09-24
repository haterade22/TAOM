# RCA: plan 014 maintainer decisions, Enlistment session scope (2026-09-24)

**Summary.** The commit applying Mike's six decisions for plan 014 (`41754a03..a67792c4`, #656) was
reviewed by six deep-review lenses and a Codex gpt-6-astra (ultra) pass. Five of the six decisions
held up in code. Decision 6 ("the offer latch clears when the stop ends") was implemented on only
one of the ways a stop ends: the exit sweep that walks the player out. A shore-leave pass, which the
offer itself grants, suspends that sweep, so the player walks out through vanilla's Leave option
and the latch never cleared. Four lenses and Codex found it independently. The rest was text and
coverage: a heap-release claim the code does not establish, a comment that reversed the engine's
teardown order, a source-level test that passed on a commented-out call, stale "a load or a new
campaign" docs, and three untested edges. All sixteen confirmed findings in the changed code are
fixed in the review follow-up commit; two more (the ADR-002 split issue and the #656 body) need
Mike. Report:
[deep-review-014-enlistment-session-scope-decisions-2026-09-24.md](deep-review-014-enlistment-session-scope-decisions-2026-09-24.md).

## Findings

| # | Sev | Bug | Category | Why missed | Preventive action |
|---|---|---|---|---|---|
| 1 | LOW | The offer's settlement latch cleared only in `ExitSettlementForService`; on an accepted pass `Assess` returns Attached (`ServiceAttachmentService.cs:120`, `:144-145`), the player leaves by vanilla's menu, and the next stop in that town stayed silent | Stale state / lifecycle | "The stop ends" was implemented as "the exit sweep walks the player out", one of several exits. The stale `TownLeavePolicy` class doc ("dies the moment he leaves") hid that a pass outlives the commander's departure, and the new tests called `OnStopEnded` directly, so none exercised a real route | `EnlistmentMaintenanceBehavior` ends the stop on the commander's settlement-left edge; `EnlistmentStopEndTests` (RED seen); lesson in `lessons/state-lifecycle-save.md` |
| 2 | LOW | CHANGELOG, feature doc, a service summary and a test name said the game-end reset releases the finished campaign; `_lastSessionStarter` on singleton behaviors and `CommanderLordAdapter._lastSeenMapEvent` still reference it | Overclaim (REPEAT) | Nulling two handles was described as the outcome it was meant to have, with no trace of the other roots | Text narrowed to "handles dropped, heap effect unmeasured"; lesson in `lessons/state-lifecycle-save.md` |
| 3 | NIT | `ServiceMaintenanceService` said the game-end reset runs "after the Game is gone"; `Game.Destroy` calls `GameManager.OnGameEnd` before `GameType.OnDestroy` and `Current = null` | Assumed API order (REPEAT) | Written without opening `Game.Destroy`; the existing lesson on `MBSubModuleBase` virtuals says to open the caller | Corrected; folded into the second lesson |
| 4 | NIT | `GameEnd_ReachesTheSessionReset_SoTheDeadCampaignsObjectsAreReleased` passed on a commented-out call and its name claimed release | Test oracle | A substring search over source; the name was written from the intent | Renamed `GameEnd_CallsTheEnlistmentSessionReset`, comment lines filtered; the new wiring pin filters them too and was mutation-checked |
| 5 | LOW | The `ColumnLeftSettlement` subscription had no test | Test gap | Judged untestable because `RegisterEvents` reads `CampaignEvents.Instance` | Source pin `BothStopEndEdges_AreWiredToTheHooks` |
| 6 | LOW | The co-op client load of a save with data (the usual co-op load) was the untested cell of the two new `OnGameLoaded` guards | Test gap (skip-guard exhaustion) | Three of four guard combinations were tested | `GameLoad_AfterALoadingSyncData_OnACoopClient_KeepsTheLoadedRecord` |
| 7 | LOW | A throwing `ColumnLeftSettlement` subscriber was untested | Test gap | The sibling `ColumnEnteredSettlement` catch has no test either, and the new one was copied | `Exit_AThrowingColumnLeftSubscriber_IsSwallowed_AndTheReParkStillRuns` |
| 8 | LOW | `FromRepoRoot` re-implemented `RepoPaths.RepoPath` | Reuse | Copied from `ExitStallDisarmTests` instead of searching `TAOM.Tests/Infrastructure` | Uses `RepoPath` |
| 9 | LOW | `EnlistmentContainerWiringTests` said `OnGameLoaded` cannot run in a test, which this change's own tests disprove | Correction not propagated (REPEAT) | The grep for stale claims covered `Main` and the changed test files only | Comment points at `EnlistmentSessionResetTests` |
| 10 | NIT | Four reset docs still said "a load or a new campaign" after decision 5 added game end | Correction not propagated (REPEAT) | Same | Fixed |
| 11 | NIT | `_justLoadedFromSave` comment named only `OnSessionLaunched` | Correction not propagated | Same | Fixed |
| 12 | NIT | `enlistment.md` "a later stop in the same town was never offered"; it was, once another town moved the latch | Overclaim | Written from the symptom, not the latch rule | "not offered until then" |
| 13 | NIT | `ServiceMaintenanceService` comment on the reconciler reset named only the stale-battle anchor | Missing comment | Decision 3 added a second field to the same reset | One line added |
| 14 | NIT | CHANGELOG not-smoked list left out the two new player-visible behaviours | Completeness | The list was carried over from the first commit | Two smokes added |
| 15 | NIT | `EnlistmentReconciler` summary "once per commander" | Correction not propagated | Decision 3 changed the latch to once per episode | "once per loss episode" |
| 16 | NIT | Test comment "OnGameEnd needs a live Game"; the blocker is the static `IoC` container | Wrong mechanism | Same as 3 | Test comment corrected |
| 17 | LOW | `EnlistmentMenuBehavior` grew from 160 to 162 lines, over the ADR-002 ceiling, with no split issue | Standards | The earlier review listed the split as a follow-up but nobody filed it | NEEDS MIKE: file the split issue |
| 18 | LOW | The #656 body predates the decisions commit | Process | Issue text is not re-read at each commit | NEEDS MIKE (public write) |

## Root-cause pattern

Findings 1, 2 and 12 share one cause: **an outcome was written down as done because the mechanism
meant to produce it was written.** Decision 6 asked for "clears when the stop ends"; the code
cleared on one exit and the doc said "literal". Decision 5 asked for the reset at game end; the code
nulled two handles and the CHANGELOG said "released". In both, the check that would have caught it
is the same: list every way the state can reach the outcome (every exit from a stop, every root to
the campaign) and mark each covered or not before writing the claim. Findings 9 to 11 and 15 are
the propagation lesson from the first plan 014 review, recurring one commit later, because the
stale-claim grep was again narrower than the repo.

## Why each agent missed these

The implementation is the miss; the review caught everything. Per lens:

- **Agent 1 (Standards):** caught 1, 3, 4, 8, 10 to 12 and 17. It does not trace lifecycle edges
  beyond the rule text, so finding 2 came only as the N4 wording nit.
- **Agent 2 (Engine):** caught 2, 3 and 16 with decompile evidence. Finding 1 is TAOM control flow,
  not an engine API, so it was outside its checklist.
- **Agent 3 (Efficiency):** caught 2 and traced the retention roots. Finding 1 is not a cost issue.
- **Agent 4 (Completeness):** caught 1, 4 to 10, 14, 15, 17 and 18. It did not look at engine
  teardown order (3).
- **Agent 5 (Data flow):** caught 1, 2, 3, 8, 10 and 13. It reported the missing wiring test only
  indirectly; tests are Agent 4's lens.
- **Agent 6 (Design):** caught 1 and 8, and proposed the fix used. It does not audit prose.
- **Codex:** caught 1, 3 and 4. It missed 2 beyond the test name, and 5 to 18.

## Feedback memories to codify

Two lessons, appended to `docs/reviews/lessons/state-lifecycle-save.md`:

- A state that "ends when X ends" is cleared on X's own edge, not on one of the ways the player
  leaves.
- A "released" claim is a reachability claim; so is a claim about when a teardown hook runs.
