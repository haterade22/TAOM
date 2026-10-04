# RCA: campaign hot paths review (plan 037, 2026-10-02)

## Top line

Plan 037 made four campaign-map paths cheaper in four commits: eight settings providers read MCM once
(A), cheap filters run before per-party work (B), the marketplace builds its id sets once per culture
and counts a town's guaranteed items in one walk (C), and a Patch59 transpiler let the caravan score
postfix reuse vanilla's distance (D). Six lenses found no HIGH defect and no behaviour change in any
reachable input class. Every lens that judged Stage D under the simplicity criterion rejected it, and
the review dropped it (`b282aaf2`); the rest is a LOW tail of duplicated predicates, stale comments and
docs, and two missing tests, all fixed. Report:
`docs/reviews/deep-review-037-campaign-hot-paths-2026-10-02.md`.

## Findings

| # | Sev | Bug | Category | Why Missed | Preventive Action |
|---|---|---|---|---|---|
| 1 | MED | Stage D shipped a transpiler, a thread-static hand-off, a fallback and four log formats to skip one cache-backed distance read per positively scored town on an infrequent re-think, with no measurement, reversing `rca-caravan-trade-2026-07-04.md` row 2 | Other: simplicity Reject built from a plan | The plan was written from a call count ("302 caravans x 78 towns x two passes") rather than from what the call costs and how often it runs; the executor's job was to build the plan, and nothing between plan and build re-asked whether the stage should exist | Dropped; lesson in `lessons/build-tooling-workflow.md` |
| 2 | LOW | Stage B1 moved Patch42's two MCM toggle reads after the castle filter; once Stage A made those reads a cached field, the reorder saved nothing and cost a RequiresGame IL test (and made the feature-off case pay the filter) | Other: a stage judged before the stage it depends on landed | B1 was costed against the pre-Stage-A price of `TaomSettings.Instance`; the plan never re-weighed B1 after A | Reverted to the base guard; the same lesson as finding 1 |
| 3 | LOW | `NeedsMountedCount` copied the feat test that `ApplyRohanInfantryPenalty` applied, so a second trigger feat added to the penalty would leave the speed model handing it (0, 0) and the penalty silently off | Convention inconsistency | The same stage built `TryGetPurge` as one gate with two callers for desertion, but the speed skip-gate was written as a sibling predicate; the parity test enumerates today's cultures only | The penalty gates on `NeedsMountedCount`; lesson in `lessons/gamemodels-services.md` |
| 4 | LOW | The pooled-id cache sat beside an immutable pool and needed a reference-equality rebuild to stay correct | Other: cache placed beside the data it indexes | The plan cached in the consumer because the consumer was the file in scope; the pool's own constructor already walks every entry | `CultureItemPool.ContainsItem`, built in the constructor; one-off |
| 5 | LOW | Doc and comment drift: a test total still 117 (128), a Filter count 10 (11), the guaranteed-stock method credited to the wrong class, no changelog entry, Stage B docs untouched, comments naming the removed `GetItemCount`, a class comment describing the old read | Stale claim after a change | The plan told the executor to change the total "only if it reads 117", a conditional edit of a number nobody recomputed; Stage B's plan listed code files and tests, not the feature docs that describe the gates | Fixed from a filter run; a recurrence under "Every numeric claim in a CHANGELOG/doc/commit body comes from a command run this session" and "A structural refactor's leftover-reference sweep must cover living docs" (`lessons/build-tooling-workflow.md`) |
| 6 | LOW | No direct test of `TownRosterAdapter.GetItemCounts`'s engine-free paths, and none that the mount-count helper reads the roster when asked | Other: test gap | The plan's tests went through the service with a faked adapter, and the helper test covered only the skip branch | Two tests, each proven to fail against a mutant; one-off |

## Root-cause pattern

Findings 1 and 2 share a cause: a performance plan written as a list of call sites, each costed in
isolation and before the earlier stages landed. Stage A made the settings reads cheap, which removed
B1's reason; Stage D's saving was a call count multiplied out, never a time. A plan is a draft
(dispatch rule 12), and nothing in the build step re-asked the simplicity question of a stage once the
code existed.

## Why each agent missed these

The lenses ran on the finished branch and found all six; the question is why the build did not.
- **The executor** built what the plan specified and pinned it with tests, including the IL-order
  tests for B1 and the full hand-off machinery for D. Its contract asked for parity, not for a
  re-judgement of each stage.
- **The plan review** (`plans/_audit/2026-10-02-perf/plan-review-037.md`) raised N11, N12, N13 and N16
  (N16: B1 and B2 are thin under the simplicity criterion, "probably net wins") but did not hold
  the simplicity verdict as a blocking question for each stage, and did not ask it of Stage D at all.
- **Standards and completeness (lenses 1 and 4)** caught the doc drift that the executor's own doc
  pass missed because the plan's doc step was conditional on a stale number.

## Feedback memories to codify

None beyond the two lessons: the run's plan template is the place to add "measure, or re-weigh after
the earlier stages land" for performance stages, which is the orchestrator's file.

## Convergence rounds

| Round | Diff read | Finding | Fixed in | Why missed | Preventive action |
|---|---|---|---|---|---|
| 1 | `f9e277ce..f58ddb09` | C1: a test class summary still said the service builds pooled id sets, after Block 6 moved that index into each pool | `212d397d` | The comment sweep covered the members the fix changed, not the class summary above them | A deleted mechanism is grepped by name across the files the fix touched, summaries included |
| 1 | same | C2: the new `mcm.md` Tests line said all eight providers resolve from a container, read through and fall back; the tests pin six, seven and three of them | `212d397d` | The line was written from the plan's intent, not counted from the test rows | A coverage sentence is written from the test file's rows |
| 2 | `f58ddb09..212d397d` | none (clean) | | | |
| 3 | `8fc531d7` (the Codex follow-up) | R3-1: the two `ApplyDesertion` tests ran only with both flags false, so a gate call that passed a flag wrongly made the same call as the real one and passed (five gate-flag mutants, three calculation-flag mutants and `IsHero` to `true` survived) | `fix(campaign): v2.0.32 - convergence fixes for the plan 037 follow-up` | The tests were written to pin the gate's polarity, and the stub and the call shared one constant `false, false` | Each row of a pass-through test varies every flag it forwards, and the refusal stub answers yes to every other combination; lesson in `lessons/testing-qa.md` |
| 3 | same | R3-2: report row 2 and C2 marked Codex's second P3 fixed with only its positive half built; nothing pinned that an empty book avoids the walk (the guard moved below the walk passed all 20 tests), and two tests were named as if they did | same | The half Codex called "an observable enumeration boundary" was judged unobservable without writing that down, so the row read as complete | A claim a test cannot observe is pinned another way (here, IL order) or the test is named for what it does observe, and the row says which; same lesson |
| 4 | `53cbcb58` (the round 3 fix) | R4-1: the two `ApplyDesertion` tests ran for `(true, false)` and `(false, true)` only, rows on which `isGarrison` is always `!isPlayerOwned`, so a flag replaced by the other flag's negation made the real call and passed (six such mutants survived), and an AI lord party's call `(false, false)`, which the first version ran, was no longer exercised | `fix(campaign): v2.0.32 - residual review follow-ups for plan 037` | The rows were chosen to differ in both flags, and only the mutants the finding named were tried against them, not every value the two flags can take | Run every combination of the flags a call forwards (two flags, four rows); lesson in `lessons/testing-qa.md` |
| 4 | same | R4-2: the `*_NoRefuge_ChecksTheBookFirst` pair still named an order (book before walk) that its null-event assertions cannot observe, and two sentences claimed more than the tests show: report row 2 said an empty book dispatches nothing for "the same battle" while the two tests staged a single refuge party, and a test comment said the IL test pins "whether the walk ran" when it pins call order | same | The R3-2 rename covered the two tests the finding named, and the two sentences described the tests by their intent, not by what each stages and asserts | A finding that names two tests is swept across the file for the same claim, and a coverage sentence is checked against the rows it describes; same lesson |

Rounds 1 and 2, and the REVIEW-LOG entry's convergence line for them, were written by the orchestrator after round 2; the round 3 and round 4 rows were added with their fixes.

## Codex review (adversarial, `c8c96c09`)

The orchestrator ran Codex on the resolved head after the convergence rounds (raw output: `codex/final/037.md` in
the perf run's scratch). It found no P1 or P2 and two P3 test gaps; its reading of the settings caches, the
three early returns, the marketplace counts and the Stage D drop supported the code. Both gaps reproduce, and
both are closed by tests and documents alone: no file under `Main/` changed.

| # | Sev | Bug | Category | Why Missed | Preventive Action |
|---|---|---|---|---|---|
| C1 | P3 | The 64-case top-up oracle takes its counts from a stub of the adapter, and the one adapter test passes a null settlement, so nothing ran the new `GetItemCounts` loop: a version returning zeros for every real settlement passed all 10 cache tests | Test gap: a stubbed collaborator never exercised | The plan's tests went through the service with a faked adapter and put real-roster execution outside the unit tests; review item L5 added only the engine-free null path | `TownRosterAdapterGetItemCountsTests`: 4 tests on a real `ItemRoster`, each proven against planted mutants; lesson in `lessons/testing-qa.md` |
| C2 | P3 | The three refuge tests pass a null `MapEvent`, which walks to no party whichever way the guard points: inverting `Count == 0` in either listener passed all 13 tests | Test gap: a guard test whose input cannot tell the guard from its inverse | The plan prescribed those null-event tests (its Step 13), and review item L4 parked the inversions as needing a live `MapEvent` without trying a bare one | Four tests on a staged battle (a refuge in the book is dispatched, an empty book dispatches nothing), and an IL-order test per listener that the book is read before the battle is walked, because the walk itself cannot be observed (convergence round 3); same lesson |
| C3 | P3 | The desertion IL-order test cannot see the gate's answer used the wrong way round: inverting `ShouldEvaluate` passed it (Codex's remark under its suspect 16) | Test gap: call order pinned, polarity not | The IL tests were written as the only reachable check, on the same "needs a live engine object" assumption | Two tests that run `ApplyDesertion` on a real `TroopRoster`, each for every combination of the player-owned and garrison flags (convergence rounds 3 and 4). The speed model's call site stays untested (`CalculateFinalSpeed` calls the vanilla model first and needs a `Campaign`); it remains on the Not-tested list |

Codex's other remark under suspect 16, that the settings IL rule proves where `Instance` is called and not
first-non-null caching, is accurate and has no engine-free test (MCM is never initialised under MSTest); the
read-through and no-MCM tests are the bracket. Nothing changed for it.
