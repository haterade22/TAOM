# RCA: plan 015, warg tick costs (deep review and Codex, 2026-09-24)

## Top-line

Six-lens `/deep-review` (standards, engine, efficiency, completeness, data flow, design) and a Codex
gpt-6-astra ultra adversarial pass on `improve/015-warg-tick-costs`, diff `7f02fc8d..66a85b08`: the
warg tree's per-tick `IoC.Resolve` calls, per-scan list allocations, 343-cell grid scans and per-target
`GetSkeleton` wrappers. Report: `deep-review-015-warg-tick-costs-2026-09-24.md`.

**No HIGH, no engine incompatibility, no false positive.** Eleven confirmed LOW findings, all fixed on
the branch the same day except those needing Mike (the GitHub issue, the service-locator policy, the
plan amendment in the main checkout). The one runtime defect was a moved range gate that let a NaN
frame through to a native skeleton fetch. Three were test oracles that could not fail for the
regression they named. The rest were claims, tests and docs that trailed the code.

## Findings

| # | Sev | Bug | Category | Why missed | Preventive action |
|---|---|---|---|---|---|
| F1 | LOW | The range gate moved from `FindBoneInRange` into `CheckTargets` kept its inverted form (`LengthSquared > gate -> continue`), so a NaN visuals frame passed it and paid a native `GetSkeleton` wrapper. No hit results: `FindBoneInRange`'s `<=` rejects NaN | Missing NaN gate (**repeat offender**: `csharp-architecture.md` "Engine-Float Decision Gates" and testing-qa "Write engine-float decision gates as positive requirements") | The plan treated the move as a cut and paste of a line already in production, and a moved line reads as reviewed. The NaN sweep is written for gates a change writes, and nobody classed a relocated gate as written | Gate rewritten as `!(d <= gate)`, NaN test added. Lesson "A gate moved into new code is new code" (testing-qa) |
| F2 | LOW | `WargTickCostTests.CallsInIfLoadable` caught `FileNotFoundException` and returned no calls, so `UpdateWargRiderHandle` was never scanned in a filtered run and a per-frame Resolve there would pass | Test oracle fails open | The builder met the load failure, documented it and moved on, treating "cannot read" as "nothing to find". The test had no positive control, so nothing showed that the scan could see a Resolve one level down | `EnsureLoaded` in `[ClassInitialize]`, an unreadable body fails (inconclusive only without a game), and a control fixture the rule must reject. Lesson "An IL rule test fails on a body it cannot read" (testing-qa) |
| F3 | LOW | The "scans into a reused buffer" tests checked only the overload's arity; `GetNearAliveAgentsInRange(60, agent, new List<Agent>())` passed | Test oracle checks a proxy | The test pinned the symptom the old code showed (the two-argument overload) rather than the property named (no allocation per call) | Constructor check plus a negative-control fixture. Same lesson as F2 |
| F4 | LOW | "Every scan returns the same set of agents" was false for a grid queried between rebuilds: without z cells a column returns an agent that moved vertically into the sphere since the rebuild, which the z-keyed grid missed. The new result is a superset, all inside the live sphere | Stale state (claim, not result) | The equivalence proof and the brute-force test both used a grid built from the positions being queried; the production grid is up to 2 s old (`AdvancedCombatBehavior.cs:16`). Build-time cells against query-time positions was never modelled | Claims corrected, stale query pinned by a test, keep-or-restore put to Mike. Lesson "An equivalence test for a rebuilt index queries it stale" (testing-qa) |
| F5 | LOW | `CheckTargets`' null-entry, fading-out and null-visuals guards had no test, and every test used a one-element list, so a lost `i--` could not fail | Missing skip-guard tests (`tests.md` Skip-Guard Exhaustion) | The plan scoped its tests to the gate it moved, and the guards around it were unchanged lines | Three single-guard tests and a mixed-list test. The rule exists; no new lesson |
| F6 | LOW | The documented column-order change was unpinned: every multi-point assert was order-blind | A documented behaviour change without a test | The order change was written up as a maintenance note, not a behaviour to assert | `CollectInRadius_OneColumnAtTwoHeights_ReturnsThemInBuildOrder`. One-off |
| F7 | LOW | "No node service or buffer is static" was a plan decision with no test; a static initializer runs in the type initializer, which the IL scans never reach | A design rule enforced only in review | The IL scans were chosen for the per-call rules and their blind spot was not listed | Reflection test over the node types. One-off |
| F8 | LOW | Feature docs: no Changelog entries, stale Tests section and diagram, and "resolved once per node" overclaimed (`LogTask` still resolves per Execute) | Docs trail code | The plan limited doc edits to named lines, so the sections around them were never re-read | Both docs updated. The completion workflow already requires this; no new lesson |
| F9 | LOW | The two lessons the plan owed (the `GetSkeleton` wrapper cost, `== null` on a NativeObject in the test host) were not written | Owed lesson dropped | Plan Step 11 listed them as proposals and the executor stopped at code and CHANGELOG | Written to adapters-taleworlds-api |
| F10 | LOW | The CHANGELOG quoted suite totals nobody in the review could see | Unverified claim | Written by the builder from its own run; reviewers do not run `dotnet` | Replaced with this pass's run. Rule exists (`evidence-over-claims.md` §B) |
| F11 | LOW | The plan and CHANGELOG said order matters only to a bite; the spider's engage decorator also keeps the first of equal-distance candidates | Incomplete consumer list | The consumer survey stopped at the warg's own nodes | CHANGELOG corrected; the plan text is Mike's (NEEDS MIKE 4). One-off |

Not RCA'd because they need Mike, not a fix: the missing GitHub issue, the service-locator exception
for creature BT nodes, and the plan amendment left uncommitted in the main checkout.

## Root-cause pattern

Four of the eleven share one theme: **the change was proved against a fresh, fully readable, single
case.** F2 skipped the body it could not read, F3 checked a proxy that the fresh code satisfied, F4
queried a grid built a moment earlier, and F5 ran every guard on a one-element list. Each test passed
for the code as written and could not fail for the regression it was named after. The fix in each case
was a control or a stale or mixed input that the rule must reject.

## Why each agent missed these

- **Agent 1 (standards):** found F5 and F8 and the issue gap. F1 was not in its checklist (NaN is
  data flow's), and F2 to F4 are test semantics, not standards.
- **Agent 2 (engine):** found F2 (the load context) and noted the `is null` inconsistency. F1 is not
  an engine incompatibility, and F4 needs the rebuild cadence, which it did not trace.
- **Agent 3 (efficiency):** its scope is cost; it confirmed the win and found the empty-scan lookup
  the hoist added. Correctness of gates and tests is outside it.
- **Agent 4 (completeness):** found F5, F6, F7, F8, F9 and F10, and flagged F2 as a limit. It did not
  test the buffer oracle against an allocating call (F3).
- **Agent 5 (data flow):** found F1 and noted F4 as "superset under staleness" without calling the
  claim wrong; it also found F2's weakness and F8's overclaim. It did not attack the buffer oracle (F3).
- **Agent 6 (design):** judged the stale-grid superset acceptable and the catch in `CallsInIfLoadable`
  sound, reading both as design rather than as evidence a claim or test was wrong.
- **Codex:** found F2, F3 and F4 with a worked counterexample, but checked the moved gate only for its
  comparison and equality, not NaN (F1), and did not ask for the skip-guard tests (F5).

## Feedback memories to codify

None. The systemic lessons went to `docs/reviews/lessons/testing-qa.md` (three) and
`docs/reviews/lessons/adapters-taleworlds-api.md` (two owed by the plan).
