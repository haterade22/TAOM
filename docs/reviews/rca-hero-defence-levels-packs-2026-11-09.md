# RCA: Hero Defence hero levels and pack rules review, 2026-11-09

**Summary:** the review covered three pure rules added to Hero Defence (#1106) on top of commit `b3b1bedb7`: `HeroLevels` with
`HeroDefenceRun.TryLevelHero`, `PackOrderRule`, and `PackStallClock` with `HeroDefenceRun.PackStalled`. Nothing in it is
registered in IoC or `SubModule.xml`, and no engine type is used. The review was tier 2 (pure domain rules and tests). Lens 5 (data
flow) ran at `effort: "high"`, and Codex ran at `xhigh` on Mike's word for the night
(`docs/reviews/codex-prereview-hero-defence-levels-packs-2026-11-09.prompt.md`). Neither reported a CRITICAL or HIGH finding, so
no second wave ran. Lenses 1, 3, 4 and 6 were skipped by tier, and lens 2 and lens 7 were not in scope (no engine call, no XML).
Step 4 is N/A, because its two lenses (3 and 6) did not run.

**First round (P1 to P10):** five MEDIUM and five LOW findings, each confirmed by reading the code. P1 to P6 are fixed in code,
each with tests that failed first (25 failing tests in all before the fix, 444 of 444 passing after it). P10 strengthens five
tests. P7 and P9 are fixed in the feature doc, and P7's reader checks are owed (follow-ups). P8 is answered by a contract line,
not a guard.

**Second round (Q1 to Q9):** after the first fixes, a check of the fix pass (a Claude workflow: three `architect` agents for
closure, regressions and written claims, then five skeptics) found nine LOW items. The skeptics confirmed all five they checked,
and I confirmed the other four against the code. Q1 to Q4 are fixed in code with tests that failed first (4 failing, then 448 of
448 passing). Q5 and Q6 strengthen two tests, each proven by a deliberate mutant that the test now catches. Q7 to Q9 fix the docs,
this RCA and the commit text. Nothing here runs in the game yet, so no finding could crash it or touch a save.

## Findings

| # | Sev | Finding | Category | Why missed | Preventive action |
|---|---|---|---|---|---|
| P1 | MED | Two hero orders for one hero in one tick had no defined meaning. A re-placement after a level gave `HeroResets(A)` and `HeroMoves(B)`; applied moves first, the hero ended at A while `PostOf` said B. A first placement plus a level gave a spawn and a reset, and three levels gave three resets. No order carried a level. Lens 5 F1, Codex F2 and S1. | effect composition | Each call added its own order at the moment it ran, with the post it had then. Every test ticked between two calls, so no test put two calls on one hero into one tick. | Orders are made once, at the end of the tick, from the hero's final post and level: one order per hero, a spawn includes a reset and a move, a reset includes a move (`HeroWork`, `FlushHeroOrders`). `HeroOrder` carries `Level`. Six tests, RED first; director contract item 6. |
| P2 | MED | A late `PackStalled(slot)` for an emptied pack removed the new pack that took the same slot, and could charge its lives. A clock that had fired also kept returning true. Lens 5 F2, Codex F3 and S5. | identity | The slot served as the pack's name, but `ReleaseNextPack` reuses a slot as soon as it empties. | `CreepBatch.PackId`, unique in the run; each creep stores its pack; `PackStalled(packId)`. Director contract item 7: one clock per batch, dropped when the pack stalls or empties. Three tests, RED first (see Q5). |
| P3 | MED | If a stalled pack's leak took the last life, `End` set `Result` and `CreepGone` then returned early for the rest of the pack: they stayed alive in the books and on the field, with no removal. `ExpireWave` had the same defect. Codex F1. | terminal state in a loop | The early return on `Result` is right for a late engine callback. A group loop reused the per-creep method, and every test had lives to spare. | `EndGroup`: after the run ends, the rest of the group still leaves the field, with no score. `PackStalled_OnTheLastLife_StillRemovesTheWholePack`, `Tick_WaveLimitTakesTheLastLife_StillRemovesEveryCreep`, RED first (see Q2). |
| P4 | MED | The stall clock compared mean progress across different sets of creeps. When the lead creep arrived, the mean of the rest dropped below the mark, so a marching pack had to regain the old mean before it counted as moving, and could stall. Lens 5 F3. | aggregate membership | The tests fed one progress number and never changed the creeps behind it. | `Observe` takes the creep count, and a change in it is no activity. Two tests, RED first. The first fix restarted the mark at the change; Q3 corrects that. |
| P5 | MED | A bad price setting sold free levels: `PriceOf` returned 0 for a NaN, zero or negative price, and for a positive price below 0.5, and `TryLevelHero` accepted 0. The test asserted only that gold did not rise. Lens 5 F4 (LOW), Codex F4 (MED). | sentinel | 0 meant two things: "outside the ladder" and "no valid price". The guard stopped gold creation and missed free progress. | A zero, negative or NaN price is `int.MaxValue` (out of reach); a positive price costs at least 1. `TryLevelHero_BadPriceSetting_IsRefusedAndNeverFree` (three rows) asserts the result, the level, the gold and the effect lists. RED first (see Q4). |
| P6 | LOW | `MaxLevel` below 1 gave `Steps` of -1, so factors fell below 1; `int.MinValue` overflowed (the RED run read a health factor of 2.147484E+08). A NaN cooldown rate gave a NaN cooldown, because `Math.Max(0.25f, NaN)` is NaN. Lens 5 F5, Codex S2. | arithmetic guard | `Steps` clamped the level to 1 before the cap, and `Math.Max` does not remove NaN. | `Steps` takes `min(level, MaxLevel)` and returns 0 below 2; the cooldown floor uses a comparison. Two tests, RED first. |
| P7 | LOW | The band and limit records accept values that break their rules: equal or inverted bands flap, a NaN release never lets go, an infinite height pairs an infinite position, a zero step counts a standing pack as marching, a NaN window never stalls. Negative or NaN level shares give bad factors. Lens 5 F6, Codex F5. | reader range | No reader exists yet; the records trust their values. | The feature doc lists the range for each setting ("Ranges the readers must enforce"). The first follow-up below (#1106). |
| P8 | LOW | A reading with an earlier time can move the blow time back and stall a pack early. Codex F6. | contract | The clock assumed monotonic time without saying so. | Director contract item 7: feed mission time, which must never go backward. No guard: the only source is the director's mission clock, and a guard for a case no caller makes is speculative (`simplicity-criterion.md`). |
| P9 | LOW | The doc did not say that blocked hits count as blows, or how long a clock lives. Lens 5 F7. | contract | The plan line has it; the feature doc did not. | `PackStallClock` bullet and contract item 7. The open question in Lens 5 F7 goes to Mike (below). |
| P10 | LOW | Five tests passed for the wrong reason: the bad-price test checked only gold, "orders nothing" checked only resets, "returns at its level" checked no return order, the pause guard test had no creep for the guard to protect, and the non-finite time test did not prove the mark stayed. Codex section 4. | test strength | Each test asserted the one effect it was named for. | Each test now asserts the result, the state and the effect lists that its action can change; the pause test registers a late creep; the time test proves the old mark with a later step (see Q6). |
| Q1 | LOW | A hero who fell after Ready but before the next tick kept its pending work, so the tick sent a reset or a move for a fallen hero; the director would bring it back mid-wave while the run counted it as fallen. Fix-pass closure check. | sibling path | `FlushHeroOrders` read only the pending work, not the hero's state, and no test put Ready and a fall between two ticks. | Only a placed hero gets an order; the clear spawns a fallen one. `HeroDown_AfterReadyBeforeTheNextTick_DropsTheHerosPendingOrder`, RED first; contract item 6. |
| Q2 | LOW | `ObserveLeaks` is a third loop that can take the last life: with two creeps at the exit in one reading and one life left, the second creep stayed alive with no removal. Fix-pass closure and regression checks. | sibling path | The P3 fix went into the two loops the review named, and nobody listed every loop that can end the run. | `ObserveLeaks` sends the creeps at the exit through `EndGroup`. `Tick_TwoLeaksInOneReadingTakeTheLastLife_BothLeaveTheField`, RED first. |
| Q3 | LOW | The P4 fix restarted the mark at each change in the creep count, which threw away the gain since the mark. A pack that loses a creep more often than once per 5 m of march never counted a step: the check's simulation stalled 30 and 40 creep columns, 4.5 m apart at 3.1 m/s, at 41.75 s while they still marched. The default pack of 20 did not reach it. Fix-pass closure and regression checks. | fix too broad | The P4 tests changed the count once. | The mark moves by the jump in the mean, so the gain or loss since the mark still counts. `Observe_CountChangesBeforeEachStep_TheGainSoFarStillCounts`, RED first. |
| Q4 | LOW | The `int.MaxValue` price could still be bought from a purse at its cap, because the refusal was `price > Gold`. Fix-pass closure and regression checks. | sentinel | The P5 fix made a bad price large, not refused, so its guard depended on the purse staying below the cap. | `TryLevelHero` refuses `int.MaxValue` outright. `TryLevelHero_BadPriceSettingWithAFullPurse_IsStillRefused`, RED first. |
| Q5 | LOW | `PackStalled_LateReportAfterTheSlotIsReused_RemovesNothing` still passed with `PackStalled` matching on the slot: the old pack id named an empty slot. Its RED came only from the stub's pack id of 0. Fix-pass closure check. | test strength | A RED from a stub value is not a RED on the rule. | A third pack now sits in the slot whose number equals the old pack id. A mutant that matches on the slot fails the test. |
| Q6 | LOW | The non-finite time test sent its bad reading with another creep count, so the next reading's count change erased any mark the bad reading wrote. Fix-pass closure check. | test strength | The P4 change gave the clock a count, and the old test was updated without asking what the count change hides. | The bad reading uses the same count. A mutant that moves the mark on a non-finite reading fails all three rows. |
| Q7 | LOW | The range table said a negative cooldown rate takes the factor below 1 and then below 0; a negative rate makes cooldowns longer, and the other shares go below 0 only under -1/(MaxLevel - 1). Fix-pass closure and claims checks. | doc claim | The row grouped the cooldown rate with the shares that add. | Separate rows with the computed limits. |
| Q8 | LOW | This RCA claimed nine fixes "each with a test that failed first", and that every P10 test asserts every effect list. Fix-pass claims check. | doc claim | The summary was written from the plan of the fixes, not from the table. | The summary states each round's split; three tests now assert the missing parts. |
| Q9 | LOW | The plateau rule for a controlled hero read three ways: the `PackOrderRule` comment excluded a plateau hero, the feature doc and the commit text did not. Fix-pass claims check. | doc claim | Each text was written alone. | One rule in all three: never a hero on a plateau, controlled or not (the plan: plateau heroes never trigger Charge). |

The closure check also asked whether the director may report its own removal of a hero (to apply a spawn or a reset) through
`HeroDown`. It must not; contract item 8 now says so.

## Root-cause pattern

P1 to P4 share one cause: a value was recorded once and read later, after the state it described had changed. An order was made
with the post and level of its call, and the tick then changed both. A slot named a pack, and the next pack took the slot. A loop
read `Result` as "this creep is already handled" after its own first step had ended the run. A mean was compared with a mark
taken over other creeps. P5 is a near relative: one return value (0) stood for two states.

Q1 to Q4 repeat the lesson of Slices 1 and 2 at the level of the fixes: each first fix held on the path its new test drove and
missed a sibling path (a fall between Ready and the tick, the third loop, a count that changes often, a purse at its cap). Q5 and
Q6 are tests whose RED step proved nothing: the stub made them fail, not the missing rule.

## Why each reviewer caught or missed these

- **Lens 5 (data flow, high):** caught P1, P2, P4, P5, P6, P7 and P9 by following each order and report from the run to the
  director. P4 (the mean across a changed creep set) came from Lens 5 alone. It did not follow the leak loop past the last life
  (P3) and did not ask about time order (P8).
- **Codex (`xhigh`):** caught P1, P2, P3, P5, P6, P7 and P8, with transcribed reproductions for each, and the five weak tests
  (P10). P3 and P8 came from Codex alone. It missed P4. It disputed two suspects with evidence: the nearest-creep key (the plan
  and Gate 1 specify it) and the high-water mark (blows keep a pushed-back pack alive).
- **Fix-pass check (Claude workflow):** the closure agent traced each row on every path and found Q1 to Q7; the regression agent
  simulated long columns and found Q2, Q3 and Q4; the claims agent found Q7, Q8 and Q9. It also confirmed the default
  prices (400, 520, 3263, 44982) and level-20 factors (2.9, 2.52, 2.52, 0.62) by hand.
- Every finding of all three held when checked against the code; none was a false positive.

## What went wrong in the process

1. **Tests that tick between every call.** The test driver's helpers (`Own`, `InWave`) tick or are followed by a tick, so the
   tests never held two calls on one hero, or one pack, inside one tick. Every composition defect lived there.
2. **Fixes checked only by their own new tests.** The first fix pass was green and fully verified, and still missed four sibling
   paths. Only a separate check of the fix pass found them.
3. **A RED from a stub.** The stub gave every pack id the value 0, so two tests failed for that reason alone and would have
   passed against the old rule. A mutant of the rule, not the stub, proves such a test.
4. **Two dotnet runs in one worktree.** During the first verify of this slice I started the Hero Defence filter run while the
   `/verify` test run was still going in the same worktree. Both runs build into the same output folder, and the test host held
   its DLLs locked. I stopped the verify with TaskStop, checked that no dotnet or testhost process was left, and ran the full
   verify again after the filter run. The rule for this worktree: one dotnet run at a time.

What worked: independent reviewers with different methods (Lens 5 traced data, Codex transcribed reproductions, the fix-pass
check traced every path of each fix) each found something the others missed, and the RED step showed each new test failing on
its own defect before the fix.

## Verification

The Hero Defence filter ran 444 of 444 after the first fixes (25 failed before them), and 448 of 448 after the second (4 failed
before them). The two strengthened tests were each run against a deliberate mutant (`PackStalled` matching on the slot, and a
non-finite reading that moves the mark): `PackStalled_LateReportAfterTheSlotIsReused_RemovesNothing` and all three rows of
`Observe_NonFiniteTime_ChangesNothing` failed, and the mutants were then reverted.

The full `/verify` ran in the worktree on the final tree (2026-11-09, 21:56 to 21:59). Build: exit 0, 0 errors; the 10 warnings
are Harmony analyzer and nullable warnings in five other features, none in Hero Defence. Restore with `--force-evaluate`: exit 0,
no NU1900, NU1903 or NU1904. C# suite: 38,309 tests, 38,300 passed, 0 failed, 9 skipped. The canary tripped, the API snapshot
reproduces, the data gates found no gate that reads the changed files, and provenance has 0 misses. Tool tests, run from Git
Bash: 8,407 ran, OK, 12 skipped. `lint_docs.py --fail-on-drift` exits 0 with no new unowned follow-up and no new dash. Nothing
ran in the game: no part of this change is registered.

## Follow-ups

- FOLLOW-UP (#1106): the readers that add `HeroLevels` (Slice 5, `heroes.json`), `PackOrderBand` and `PackStallLimits` (Slice 6) enforce the
  ranges in the feature doc's "Ranges the readers must enforce" table, each with an invalid-value test (P7).
- FOLLOW-UP (#1106): decision for Mike. A hero under the player's control draws a Charge, so the player can lead a foot pack off the road and,
  by landing blows, keep its stall clock from firing for as long as the fight lasts. Allow it, or leash the pack to the road?
- FOLLOW-UP (#1106): untested in game: the 6 m height gate of `PackOrderRule`, and a hero who falls while a second road hero stands in the
  release band. The Slice 6 `block` check covers both.

## Lessons recorded

`docs/reviews/lessons/state-lifecycle-save.md`: "An effect list carries final state and ids that are never reused (Hero Defence
levels and packs, 2026-11-09)".
`docs/reviews/lessons/testing-qa.md`: "A refusal test asserts the result, the state and every effect list (Hero Defence levels
and packs, 2026-11-09)", and "A fix gets its own check on every path, and a RED from a stub needs a mutant (Hero Defence levels
and packs, 2026-11-09)".
