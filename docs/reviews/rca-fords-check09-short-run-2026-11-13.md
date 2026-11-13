# RCA: Fords check 09 failed every 120 s smoke run (#1137, deep review 2026-11-13)

## Top-line

Check 09's `melee` part failed whenever enemy melee units stood within attack range of an enemy tower for 30 unit-seconds and
no melee blow took hit points. In a 120 s smoke run (the `/game-run` default) the waves meet near the outer towers and fight
each other before the lane rules order any strike, so both smoke runs on file failed: `20261110-154302` (49.1 unit-seconds,
0 orders) and `20261112-235217` on trunk a83f45ae6 (54.1 unit-seconds, 0 orders). The 600 s and 2400 s runs passed. A run
shorter than the default 600 live seconds now judges `melee` only once an `AttackTower` order is 30 mission seconds old at the
run's end. The tier 2 deep review (lens 5 data flow, high effort; Codex out of usage, Mike 2026-11-12) confirmed 1 MEDIUM and
1 LOW finding on the first fix. Both are fixed in this change.

## Findings

| # | Sev | Bug | Category | Why missed | Preventive action |
|---|---|---|---|---|---|
| 1 | MED | The first fix exempted a short run only when no `AttackTower` order was given at all. In the 600 s and 2400 s runs the first order came at live second 112.1 and 108.6 (mt 122.08 and 118.64), so a smoke whose first order lands near its end would still fail, and a new test pinned that case as FAIL | Verdict rule judged on presence, not timing | The fix was written from the two failing smokes, which had no order; nobody read when orders arrive in the runs that pass | The rule needs the oldest order to be 30 mission seconds old at the run's end; tests for an order at mt 118.6 (NOT_SEEN) and mt 100 (FAIL) in a run ending at mt 130 |
| 2 | LOW | The 600 s threshold was a new constant duplicating `FordsAutorunPlan.DefaultLiveSeconds` | Duplicated constant | Written beside the verdict, away from the plan | The verdict reads `FordsAutorunPlan.DefaultLiveSeconds` |

Not applied, with reasons: the lens noted that the live counter stops at a winner, so a keep fall before 600 s with no order
and no blow would be exempt; no path produces that state (a keep takes no damage until its outer tower falls, and the rules
order a strike first). It also noted that `CommandsGiven` counts an `AttackTower` that fell back to a move as given; that only
makes the rule stricter. Both are pre-existing and need no change here.

A rule on contact seconds (ordered units within 4 m of their tower), the lens's first suggestion, was tried and dropped: the
scenario fakes record no contact, so the `towers-dead` fault scenario could no longer fail (`EveryFault_MakesItsPartFail` went
red). The order-age rule keeps that fault failing.

## Root-cause pattern

A verdict threshold that suits a full match was applied to every run length. The check counted a precondition (enemies near a
tower) that holds early in any match, before the event the check judges (a strike order) can happen. The same shape exists in
any check that counts exposure from the first sample: judge only the window after the trigger it expects.

## Why each agent missed these

- **The original rule (2026-10-31 to 11-01):** written and tested against 600 s and longer runs; the 120 s smoke default came
  later with `/game-run`.
- **The first fix:** read the two failing runs only.
- **Lens 5 (data flow):** found finding 1 by reading the first order times in the passing runs, and finding 2 by grepping for
  the 600 s default.

## Changes

- `Main/Features/FordsOfWar/Diagnostics/FordsVerdicts.Combat.cs`: `melee` FAIL needs a full-length run or a strike ordered at
  least `StrikeAgeSeconds` (30) before the run's end; `OldestStrikeAge`; the rule text.
- `TAOM.Tests/Features/FordsOfWar/Diagnostics/FordsVerdictTests.cs`: a short run with no order (NOT_SEEN), with a late order
  (NOT_SEEN), with an order 30 s old (FAIL); the existing FAIL test is now a full-length run.
- `TAOM.Tests/Features/FordsOfWar/Diagnostics/FordsVerdictDiagnosticsTests.cs`: the pin test is a full-length run.
- `docs/features/fords-of-war.md`: the check 9 known-limitations bullet.
- `docs/reviews/lessons/testing-qa.md`: one lesson.

## Verification

- TDD: the short-run test was red on the old rule; the late-order and order-age tests were red on the first fix and on the
  contact rule; green on the final rule.
- Mutants: removing the age clause turns `EveryFault_MakesItsPartFail` and the order-age FAIL test red; accepting any order
  age turns the late-order test red.
- Fords tests: 2,217 passed, 0 failed (`--filter FullyQualifiedName~FordsOfWar`).
- The full verify is recorded in the commit. The next 120 s `/game-run` should read `melee=NOT_SEEN` with `attackTowerGiven=0`.
