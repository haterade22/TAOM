# Deep review: plan 014 maintainer decisions (2026-09-24)

```
DEEP REVIEW REPORT
===================
Feature: plan 014 maintainer decisions, Enlistment session scope (#656)
Branch:  improve/014-enlistment-session-scope, worktree E:\repos\taom-improve\wt-014
Diff:    41754a03..a67792c4 (fix(enlistment): v2.0.30 - apply maintainer decisions for plan 014)
Date:    2026-09-24

Scope:   C# (Main/Features/Enlistment, Main/SubModule.cs one call), tests, CHANGELOG, feature doc
Waves:   one wave: Agent 1 Standards, Agent 2 Engine, Agent 3 Efficiency, Agent 4 Completeness,
         Agent 5 Data flow, Agent 6 Design. Codex gpt-6-astra (ultra) adversarial pass, complete.

STANDARDS:     FAIL: 1 violation (ADR-002 ceiling, EnlistmentMenuBehavior 160 to 162 lines), 3 LOW, 6 NIT
COMPATIBILITY: PASS: 0 incompatible, 2 unverified (co-op client load pipeline, heap effect); 2 WRONG text claims
EFFICIENCY:    PASS on hot paths: 3 issues (0 high, 1 medium FOLLOW-UP, 2 low)
COMPLETENESS:  INCOMPLETE: decision 6 missed the shore-leave route; wiring, co-op-with-data and
               throwing-subscriber tests missing; #656 body stale
DATA FLOW:     PASS: 0 gaps, 2 inconsistencies (stop end on the pass route; heap-release claim)
DESIGN:        4 KEEP proposals (3 apply, 1 follow-up)
XML:           NOT IN SCOPE (no ModuleData or XSLT touched)
TOOLING:       NOT IN SCOPE (no scripts or harness files touched)
```

## Details

Every finding below was re-read against the worktree at `a67792c4` before it was classified.
Engine claims were checked in the installed v1.5.3 decompile cache
(`C:\Users\mikew\.taom-src\v1.5.3\`), including `LeaveSettlementAction.ApplyForParty`, which
dispatches `OnSettlementLeft` for the leaving party and recurses into attached army parties in the
same settlement, and `Campaign.Current` (a plain static property, so `CampaignTime.Now` throws a
`NullReferenceException` outside a campaign).

### Agent 1, Standards

| # | Sev | Finding | Verdict | Action |
|---|---|---|---|---|
| S1 | LOW | `EnlistmentMenuBehavior.cs` went from 160 to 162 lines, over the ADR-002 ceiling of 150; no split issue exists | CONFIRMED (`wc -l`: 162) | Not fixed here: the real fix is the split, and filing its issue is a public write. NEEDS MIKE |
| S2 | LOW | Decision 6 misses the stop that ends while the player holds a pass | CONFIRMED, see Data flow F1 | Fixed (code) |
| S3 | LOW | `FromRepoRoot` re-implements `RepoPaths.RepoPath` | CONFIRMED (`RepoPaths.cs:14`) | Fixed |
| S4 | LOW | `ServiceMaintenanceService` said the game-end reset runs "after the Game is gone" | CONFIRMED (`Game.Destroy` calls `GameManager.OnGameEnd` before `GameType.OnDestroy` and `Current = null`) | Fixed |
| N1 | NIT | Reset docs still said "a load or a new campaign" | CONFIRMED in `IServiceAttachmentService`, the presenter, `ArmyRhythmSnapshotService`, `EnlistmentReconciler` | Fixed. The `enlistment.md` stale-battle paragraph is history and is followed by the bold paragraph naming game end, so it is left |
| N2 | NIT | `_justLoadedFromSave` comment named only `OnSessionLaunched` | CONFIRMED | Fixed |
| N3 | NIT | `enlistment.md` "was never offered" | CONFIRMED (it was offered again once another town moved the latch) | Fixed: "not offered until then" |
| N4 | NIT | "not kept reachable at the main menu" | CONFIRMED, see Agent 2 T1 | Fixed |
| N5 | NIT | The Enlistment reset shares ArmyTargeting's `try` | CONFIRMED as a fact | Not applied: decision 5's record chose this placement deliberately (last, so it cannot skip the ArmyTargeting resets); a separate `try` changes the failure path in a single-owner file. NEEDS MIKE (low) |
| N6 | NIT | The source pin matches a commented-out call; its name claims release | CONFIRMED | Fixed: renamed `GameEnd_CallsTheEnlistmentSessionReset`, comment lines ignored |

### Agent 2, Engine compatibility

16 API usages verified, 0 incompatible. Text findings:

| # | Sev | Finding | Verdict | Action |
|---|---|---|---|---|
| T1 | LOW | "Released at the main menu" is not established: `EnlistmentBehavior._lastSessionStarter` (and `EnlistmentContentBehavior`'s) holds the finished `CampaignGameStarter`, and `CommanderLordAdapter._lastSeenMapEvent` holds a finished `MapEvent` | CONFIRMED: both fields are on singletons and neither is cleared at game end (read at `EnlistmentBehavior.cs:29`, `CommanderLordAdapter.cs:74`). Whether the campaign stays collectable is UNVERIFIED (no heap dump) | Fixed text in CHANGELOG, `enlistment.md`, `ServiceMaintenanceService` summary and the test name |
| T2 | NIT | Teardown order reversed | Same as S4 | Fixed |
| T3 | NIT | "OnGameEnd needs a live Game" | CONFIRMED: `MBSubModuleBase.OnGameEnd` is empty; the blocker is the static `IoC` container | Fixed in the test comment. The same phrase in the earlier report (`deep-review-014-enlistment-session-scope-2026-09-24.md`) is left as the historical record and corrected here |
| F1 | follow-up | Clear `_lastSessionStarter` and `_lastSeenMapEvent` at game end for a real release | Pre-existing fields | FOLLOW-UP, NEEDS MIKE |
| F2 | follow-up | `EnlistmentBehavior.cs:145-146` says the starter "is reused on the 2nd load"; v1.5.3 builds a fresh one every time (`Campaign.cs:1402`) | Plausible, pre-existing line | FOLLOW-UP |

### Agent 3, Efficiency

No hot-path cost in the changed hunks (all lifecycle edges and field clears).

| # | Sev | Finding | Scope | Action |
|---|---|---|---|---|
| 1 | LOW | Heap-release claim not established | APPLY, PRESERVING | Applied (same as T1) |
| 2 | MEDIUM | Two singleton behaviors keep the finished `CampaignGameStarter` through the main menu and the next load | FOLLOW-UP (pre-existing lines) | Listed, NEEDS MIKE with a heap snapshot before any release claim |
| 3 | LOW | `CommanderLordAdapter._lastSeenMapEvent` survives teardown | FOLLOW-UP (adapters out of plan scope) | Listed |

### Agent 4, Completeness

| # | Sev | Finding | Verdict | Action |
|---|---|---|---|---|
| F1 | LOW | Decision 6 not done on the shore-leave path | CONFIRMED | Fixed (code), see below |
| F2 | LOW | `ColumnLeftSettlement` wiring in `EnlistmentMenuBehavior` untested | CONFIRMED | Fixed: `EnlistmentStopEndTests.BothStopEndEdges_AreWiredToTheHooks`, a source pin that also covers the new maintenance-hook route; mutation-checked (commenting the menu line out fails the test) |
| F3 | LOW | Co-op client load of a save with data untested | CONFIRMED | Fixed: `GameLoad_AfterALoadingSyncData_OnACoopClient_KeepsTheLoadedRecord` (characterisation, green first run) |
| F4 | LOW | Throwing `ColumnLeftSettlement` subscriber untested | CONFIRMED | Fixed: `Exit_AThrowingColumnLeftSubscriber_IsSwallowed_AndTheReParkStillRuns` |
| F5 | LOW | `EnlistmentContainerWiringTests` comment said `OnGameLoaded` cannot run in a test | CONFIRMED | Fixed |
| F6 | LOW | `FromRepoRoot` duplicate | Same as S3 | Fixed |
| F7 | NIT | Reset docs omit game end | Same as N1 | Fixed |
| F8 | NIT | Test name claims release | Same as N6 | Fixed |
| F9 | LOW | #656 body predates `a67792c4` | CONFIRMED by the lens's `gh` read; not re-read here | NEEDS MIKE (public write) |
| F10 | NIT | Not-smoked list omits the loss popup and the returning-town offer | CONFIRMED | Fixed in CHANGELOG |
| follow-up | NIT | `EnlistmentReconciler.cs:205-206` "once per commander" | CONFIRMED; the file is in the diff and decision 3 changed the latch's meaning | Fixed: "once per loss episode" |

### Agent 5, Data flow

Eight flows traced, all connected. F1 (stop end on the pass route) and F2 (heap-release claim) are
the two inconsistencies; both CONFIRMED and fixed as above. NITs (the reconciler reset comment in
`ServiceMaintenanceService` naming only the stale-battle anchor; teardown order; reset docs;
`FromRepoRoot`) are fixed. Follow-ups: `TownLeavePolicy.cs:23-26` still says the pass dies when the
commander leaves, which contradicts `Assess` and is likely how the pass route was missed; the offer
latch and dwell anchor still survive a discharge.

### Agent 6, Design and elegance

| # | Proposal | Behaviour | Disposition |
|---|---|---|---|
| 1 | End the stop on the commander's settlement-left edge; delete `ColumnLeftSettlement` | CHANGING | The first half is the defect fix for decision 6 and is applied (below). Deleting the event is NOT APPLIED: in normal play the commander's edge precedes the exit sweep (it is what triggers the "settlement left" reconcile), so the event is now mostly redundant, but I could not prove that every sweep run follows a commander edge (for example a commander whose party is removed while inside the town), so parity is unproven. NEEDS MIKE |
| 2 | Delete `_lossAnnouncedFor` (the state machine alone prevents an hourly repeat) | PRESERVING per the lens | NOT APPLIED: it reverses the letter of Mike's decision 3 ("clear"), and I did not re-verify every transition. NEEDS MIKE |
| 3 | Use `RepoPaths.RepoPath` in the game-end test | PRESERVING | APPLIED |
| 4 | Drop the redundant store clear in `OnSessionLaunched` | PRESERVING on both engine paths | FOLLOW-UP (pre-existing lines; co-op client flow UNVERIFIED) |

### The decision 6 fix

`EnlistmentMaintenanceBehavior` already routes the commander's `OnSettlementLeft` edge (filtered to
the commander, host-only). It now takes `IEnlistmentWaitMenuPresenter` and calls `OnStopEnded`
before the re-attach pass, through an internal seam `OnPartyLeftSettlement(string leaderHeroId)`
(`Hooks/EnlistmentMaintenanceBehavior.cs:91-98`), because a `MobileParty` cannot be built in a
unit test. The commander filter moved into `IsCommander` (`:112`) with its existing comment;
`OnPartyLeftArmy` keeps the same filter and reconcile. The file is 120 lines.

RED first: `EnlistmentStopEndTests.CommanderLeavesTheSettlement_EndsTheStop_BeforeTheReconcile`
failed on the seam without the clear ("Threw exception NullReferenceException, but exception
ReachedTheReconcile was expected"), which also proves the commander filter passed and reached the
engine read. Green after the one-line clear. Two more tests pin that another party's edge and a
co-op client's edge do not end the stop, and `MaintenanceBehavior_Resolvable_StopEndPresenterDependencySatisfied`
pins the new constructor dependency in the container.

## Codex review

`docs/reviews/raw/codex-adversarial-014-enlistment-session-scope-decisions-2026-09-24.md`, complete
(ends with "END OF CODEX REVIEW"; gpt-6-astra, reasoning effort ultra, 144,802 tokens). It reviewed
`41754a03..a67792c4` read-only, decompiled the load, new-campaign, teardown and settlement-leave
paths from the installed DLLs, walked eight lifecycle scenarios and cross-referenced the settings,
string keys and test ids. Verdict: ISSUES FOUND, P1 0, P2 1, P3 2.

### Phase 3d assessment

| # | Codex severity | My severity | Agree? | Reason |
|---|---|---|---|---|
| 1 | P2 | LOW (behaviour, one feature path) | Yes | Re-read: `Assess` returns Attached on a pass (`ServiceAttachmentService.cs:120`, `:144-145`), so `ExitSettlementForService` never runs and the presenter's settlement id survives to the next arrival (`EnlistmentWaitMenuPresenter.cs:139`). Codex proposed the main party's departure as the stop end; I used the commander's departure instead, because the player can leave and be re-followed into a stop that is still running, and a main-party clear would re-ask inside one stop after the cooldown. Fixed |
| 2 | P3 | NIT | Yes | The substring check matched a commented-out call. Fixed: comment lines ignored, test renamed to what it proves |
| 3 | P3 | NIT | Yes | Teardown order reversed in the comment. Fixed |

- **Confirmed bugs:** 1 (finding 1). **False positives:** 0. **Design questions:** none from Codex.
- **Things Codex missed:** the CHANGELOG and doc heap-release overclaim beyond the test name (the
  `_lastSessionStarter` and `_lastSeenMapEvent` roots), the stale "(a load or a new campaign)"
  interface docs, the untested `ColumnLeftSettlement` wiring, the co-op client load-with-data cell,
  the `RepoPath` duplicate and the ADR-002 line growth (it disputed that suspect, citing the
  existing debt).

### Phase 3e root cause

| # | Bug | Category | Why missed | Preventive action |
|---|---|---|---|---|
| 1 | Stop end signalled only by the exit sweep | Stale state / lifecycle | The end of a stop was defined by one of the ways the player leaves, and the new tests called the handler directly | Lesson in `lessons/state-lifecycle-save.md`; `EnlistmentStopEndTests` |
| 2 | Source pin accepts a commented-out call | Other (test oracle) | The pin was written as a substring search | Comment lines filtered in both source pins; covered by the second lesson |
| 3 | Teardown order reversed | Assumed API order | Written without opening `Game.Destroy` (recurrence of the "open its caller" lesson) | Covered by the second lesson |

## Action items

1. Fixed: decision 6 now ends the stop on the commander's settlement-left edge (covers the shore-leave route).
2. Fixed: heap-release overclaims narrowed; teardown order corrected; source pins ignore comments.
3. Fixed: test gaps F2, F3, F4; stale comments and docs.
4. NEEDS MIKE: the items listed below.
5. Owed: the Step 4 convergence pass on the fix diff (a single `deep-reviewer`; this delegate cannot spawn agents), and the in-game smokes in the CHANGELOG list.

## Improvements (Step 4)

APPLIED:
- `TAOM.Tests/Features/Enlistment/EnlistmentSessionResetTests.cs`: `FromRepoRoot` deleted, `RepoPath("Main", "SubModule.cs")` used (Agent 6 P3, Agents 1, 4, 5). Characterisation: the game-end test green before (baseline run) and after (renamed `GameEnd_CallsTheEnlistmentSessionReset`).
- CHANGELOG, `docs/features/enlistment.md`, `ServiceMaintenanceService.ResetSessionCaches` summary: the heap-release claim narrowed to "handles no longer point into the finished campaign; heap effect unmeasured" (Agent 3 F1, text only).

NOT APPLIED:
- `Main/Features/Enlistment/ServiceAttachmentService.cs:239-249`, `IServiceAttachmentService.cs:25-31`, `Hooks/EnlistmentMenuBehavior.cs:72-73`: delete `ColumnLeftSettlement` (Agent 6 P1, second half). Parity with the commander edge is unproven for a sweep that no commander edge preceded. Needs Mike.
- `Main/Features/Enlistment/EnlistmentReconciler.cs`: delete `_lossAnnouncedFor` (Agent 6 P2). Reverses the letter of decision 3. Needs Mike.
- `Main/SubModule.cs:797-798`: a separate `try` for the Enlistment reset (Agent 1 N5). Changes the failure path against decision 5's recorded placement, in a single-owner file. Needs Mike.
- `Hooks/EnlistmentMenuBehavior.cs`: have the presenter subscribe itself to keep the hook at 160 (Agent 1 S1 option). Adds an attachment dependency to the presenter to save two lines of a file that stays over 150 either way; the split issue is the fix. Needs Mike.

FOLLOW-UP (pre-existing code; no issue filed, because filing is a public write this delegate is not authorized to make):
- Clear `_lastSessionStarter` on the singleton behaviors (Enlistment, EnlistmentContent, Messengers) and `CommanderLordAdapter._lastSeenMapEvent` at game end; take a heap snapshot before any release claim (Agents 2, 3).
- Guard the `OnGameEnd` resolve with `game.GameType is Campaign` so a Custom Battle or shader-precompile process does not build the Enlistment graph at teardown (Agent 1, Codex, UNVERIFIED cost).
- Drop the redundant `OnSessionLaunched` store clear (Agent 6 P4; co-op client flow UNVERIFIED).
- `EnlistmentBehavior.cs:145-146` "reused on the 2nd load" comment (Agent 2 F2).
- `TownLeavePolicy.cs:23-26` class doc says the pass dies when the commander leaves (Agents 4, 5).
- `ArmyMembershipAdapter.cs:149` "This runs on load" (Agent 4).
- The offer latch and dwell anchor survive a discharge (Agent 5).
- An automated ADR-002 line-count test, which `docs/adrs/002-thin-entry-points.md:327` says exists and does not (Agent 1).
- `ColumnEnteredSettlement`'s raise has no test (Agent 4).

## Needs Mike

1. File the `EnlistmentMenuBehavior` split issue (ADR-002; 162 lines).
2. Update the #656 body (Solution step 4, "Still not reset", Files Changed, Testing, Status).
3. Delete `ColumnLeftSettlement` now that the commander's edge ends the stop.
4. Delete `_lossAnnouncedFor` instead of clearing it (decision 3 alternative).
5. Separate `try` for the Enlistment reset in `SubModule.OnGameEnd`.
6. Real heap release at game end (`_lastSessionStarter`, `_lastSeenMapEvent`), with a heap snapshot.
7. Guard the game-end reset to campaign games.

## AGENTS.md lessons (pending)

Phase 3h is consolidated later for all branches. Lessons to add:

- Bugs Codex typically misses: an overclaim repeated in several prose locations (it flagged the test
  name, not the CHANGELOG and feature doc that make the same claim); line-count growth in a hook
  already over the ADR-002 ceiling.
- What Codex does well: it traced the vanilla Leave menu consequence down to
  `LeaveSettlementAction` to show the pass route bypasses TAOM's exit sweep, with a numbered
  reproduction; it caught that a source-presence test passes on a commented-out call.
- False positives: none this review.

## Verification

- Baseline before any change: `dotnet test TAOM.Tests -p:DisableModuleCopy=true -p:ModuleId=`:
  "Failed: 2, Passed: 10259, Skipped: 2, Total: 10263".
- Enlistment filter after the fixes: "Passed: 1112, Failed: 0".
- Full suite after the fixes: "Failed: 2, Passed: 10266, Skipped: 2, Total: 10270". The two
  failures are the known live-Armory tests `TheElkItem_DeclaresTheScaleTheReachIsTunedFor` and
  `AnimaliaActionSets_BindOnlyHorseActions_ToClipsThatExist`.
- `python tools/lint_docs.py --summary --dash-base a67792c4`: `dead_links 0`, `ai_dashes 0`
  (`context_budget 7` are size warnings on rule files this change does not touch).

RCA: [rca-enlistment-session-scope-decisions-2026-09-24.md](rca-enlistment-session-scope-decisions-2026-09-24.md).

VERDICT: READY FOR COMMIT. Every confirmed defect in the changed code is fixed except S1 (the
ADR-002 split, which needs an issue) and F9 (the issue body), both waiting on Mike. The Step 4
convergence pass on this fix diff is still owed.
