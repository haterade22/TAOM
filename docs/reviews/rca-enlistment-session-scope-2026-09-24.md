# RCA: plan 014 review, Enlistment session reset (2026-09-24)

**Summary.** Plan 014 made `ServiceMaintenanceService.ResetSessionCaches` clear three more
campaign-clock latches and run on a new campaign as well as on a load. The production change held up:
six deep-review lenses and a Codex gpt-6-astra (ultra) pass found no runtime defect in it. What they
found was text and coverage. The change's own write-up claimed more than the code does in three
places, a comment the change made false survived in a test file, the load edge (the one the
CHANGELOG leads with) had no test, and one plan decision was unpinned. All eleven confirmed findings
are fixed in the review follow-up commit. Four behaviour-changing questions, all in code the plan
kept out of scope, wait for Mike (report:
[deep-review-014-enlistment-session-scope-2026-09-24.md](deep-review-014-enlistment-session-scope-2026-09-24.md)).

## Findings

| # | Sev | Bug | Category | Why missed | Preventive action |
|---|---|---|---|---|---|
| 1 | LOW | `EnlistmentReconcilerTests.cs:835-836` still said the reset is wired to `OnGameLoaded` only | Correction not propagated (REPEAT) | Plan Step 7's grep gate covered `EnlistmentReconciler.cs` alone, and the sentence was split across two lines, so a phrase grep could not match it | Lesson in `lessons/misc.md`: a rewritten claim's grep covers the repo, tests included, and searches distinctive words, not a phrase |
| 2 | LOW | CHANGELOG "Both paths now clear every per-session value": `EnlistmentReconciler._lossAnnouncedFor` and `FieldDutyRuntime`'s pace estimate are per-session and not reset | Unverified enumeration (REPEAT) | "Every" was written from the plan's list of three latches, not from a sweep of the singletons' mutable fields | Lesson in `lessons/state-lifecycle-save.md`: an "every per-session value" claim is an enumeration; sweep the fields first |
| 3 | LOW | `enlistment.md` "Every clock-keyed latch ... on both lifecycle edges", one line after the same doc says a co-op client's load skips the reset; "Each held an absolute campaign hour" was false for the rhythm cache | Overbroad claim | Written as the summary of the three new clears, not checked against the gate on the load path | Same lesson as 2; the doc now names the host-only load and the unreset values |
| 4 | LOW | The load hook's reset call had no test; only container resolvability touched `OnGameLoaded` | Test gap | The plan judged the hook untestable because of `CampaignTime.Now` | Lesson in `lessons/testing-qa.md`: throw a sentinel from an argument evaluated before the engine read; `GameLoad_OnTheHost_ResetsTheSessionCaches_BeforeNormalizing` |
| 5 | LOW | The plan's "reset is not gated on `_justLoadedFromSave`" decision had no test | Test gap | The one hook test ran with the flag false only | `NewCampaign_AfterALoadingSyncData_StillResets_ButKeepsTheLoadedRecord` |
| 6 | LOW | The new `ServiceMaintenanceService` dependency on the wait-menu presenter had no comment, against the feature's own "a service does not call presentation" rule | Missing comment | The trade-off was recorded in the CHANGELOG, not at the field | One-line comment at the field |
| 7 | LOW | Nothing in code said why the ungated new-campaign reset is safe (every callee is an in-memory field clear) | Missing invariant comment | The reason lived only in commit `e99d705a`'s body | Stated in `IServiceMaintenanceService` and `ResetSessionCaches` docs |
| 8 | NIT | The dwell doc said a future anchor holds "until the new clock passes it"; it holds until anchor plus 6 hours | Arithmetic slip | Written from the intent, not from the comparison at `ServiceAttachmentService.cs:43-44` | Corrected |
| 9 | NIT | The reset's comment filed the rhythm cache under "a stamp left in the future"; its hazard is an equal-hour reload | Wrong mechanism in a comment | Three latches summarised in one sentence | The rhythm call has its own comment |
| 10 | NIT | `enlistment.md` "is dropped from" read as "removed from" | Prose | A rewrite kept an older sentence's verb | "is called from" |
| 11 | NIT | `OnNewGameCreated(null)` in a test raised CS8625 | Test hygiene | Warning not read in the build output | `null!` |

## Root-cause pattern

Findings 1 to 3 share one cause: **the change's description was written from the plan, not from the
code.** The plan listed three latches, so the CHANGELOG said "every"; the plan named one file for the
stale-claim grep, so the test copy survived; the plan said the load hook was untestable, so the test
list stopped at the new-campaign hook. Each of the three has an existing lesson (the #644 and #645
propagation lessons in `lessons/misc.md`, the enumeration lesson behind Review 130's S2). The new
element here is that the plan itself carried the narrow gate, and the executor ran the gate as
written. A plan's verification step is a floor, not the full check.

## Why each agent missed these

The lenses reviewed the change after the fact and caught every one of these; the misses are the
executor's (implementation) and the plan's. For the review itself:

- **Codex** found 2, 3 and 4, and missed 1, 8 and 9. It checked the rewritten claim in the reconciler's
  own comments, as the plan's grep did, and did not search the test tree.
- **Agent 3 (Efficiency)** found 2 by accident, outside its lens; its remit is cost, not text.
- **Agent 1 (Standards)** missed 1, because a test comment is outside its numbered checks. It found
  3, 6 and 7.
- **Agent 2 (Engine)** missed 2, because the CHANGELOG sentence states no engine fact. It found 1, 3
  and 8.
- **Agent 4 (Completeness)** missed 7 to 10, which are comment-level; it found 1, 2, 5 and 11.
- **Agent 5 (Data flow)** did not report 6 to 8 (comment content is not a data-flow question); it
  found 1, 2, 4 and 9.
- **Agent 6 (Design)** found 1, 2, 4 and 10.

Findings 1 to 4 were each found by three or more reviewers. Findings 5 to 11 were each found by
exactly one (5 and 11 by Agent 4, 6 and 7 by Agent 1, 8 by Agent 2, 9 by Agent 5, 10 by Agent 6),
so dropping Agent 1, 2, 4, 5 or 6 would have lost at least one of them.

## Feedback memories to codify

None beyond the three lesson entries: the propagation and enumeration rules already exist, and this
is their recurrence, recorded as new entries with the plan-gate twist.
