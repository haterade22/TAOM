# Phase 7 Kickoff — Test Coverage Audit

For the next session. Read this + [feature-manifest.md](feature-manifest.md) + [wiring-matrix.md](wiring-matrix.md) + the five completed cluster docs ([cluster-gamemodels.md](cluster-gamemodels.md), [cluster-campaign-behaviors.md](cluster-campaign-behaviors.md), [cluster-harmony-patches.md](cluster-harmony-patches.md), [cluster-ui.md](cluster-ui.md), [cluster-cross-feature.md](cluster-cross-feature.md)) before doing anything else.

## Audit state at start of Phase 7

| Phase | Status | Output |
|---|---|---|
| 0 (Manifest) | Complete | [feature-manifest.md](feature-manifest.md) |
| 1 (Wiring) | Complete | [wiring-matrix.md](wiring-matrix.md) + issue #122 |
| 2 (GameModels) | Complete | [cluster-gamemodels.md](cluster-gamemodels.md) + issues #134, #135, #137, #138, #140, #142, #144, #145, #147, #148 |
| 3 (CampaignBehaviors) | Complete | [cluster-campaign-behaviors.md](cluster-campaign-behaviors.md) + issues #123–#131, #132, #133, #136, #139, #141, #143, #146 |
| 4 (Harmony patches) | Complete | [cluster-harmony-patches.md](cluster-harmony-patches.md) + issues #149–#164 |
| 5 (UI / Mixin / Prefab) | Complete | [cluster-ui.md](cluster-ui.md) + issues #165–#169 |
| 6 (Cross-feature handshake) | Complete (2026-05-13) | [cluster-cross-feature.md](cluster-cross-feature.md) + issues #170–#175 |
| **7 (Tests)** | **Not started** | This phase |
| 8 (Docs) | Not started | `docs-gaps.md` |
| 9 (Triage + Fix) | Not started | issues + commits |

## Goal

Phases 2–6 reviewed feature CORRECTNESS in isolation and at cross-feature boundaries. Phase 7 reviews feature **test coverage** — the question is: for every claim previous phases made (or could have made) about runtime behavior, is there a test that would catch a regression?

Output: `docs/audits/test-coverage.md`. P1 (no tests at all for a feature with non-trivial runtime logic) and P2 (significant call-paths untested, esp. services or hooks identified as cross-feature touchpoints in Phase 6) → GitHub issues with `audit-impl` label.

This is **test-suite gap analysis.** It is NOT a re-review of feature implementation; it is the question "if the implementation broke, would a test fail?"

## Inputs

- `TAOM.Tests/Features/<X>/` directories — what tests exist per feature
- `Main/Features/<X>/` — production code under test
- Phase 6 cluster-cross-feature.md — Findings #1 (SmartCavalryAI handshake test gap), #3 P3 (RaceAge same-tick consistency), #8 P3 (TaomSettlementLoyaltyModel untested) explicitly call out test gaps
- Phase 3 cluster-campaign-behaviors.md — many P1/P2 findings mention behavior callbacks that have no test coverage
- ADR-008 — TAOM TDD policy (RED → GREEN → REFACTOR; 100% coverage of service logic)

## Procedure

Phase 7 is one or two parallel agents per feature-cluster (5 logical clusters: GameModels, CampaignBehaviors, Harmony, UI, Services). Each agent enumerates the gaps; a final aggregation pass produces the master report.

### Per-cluster check

For each feature in the cluster:

1. Inventory `Main/Features/<X>/**.cs` excluding patches/POCOs — what classes have non-trivial logic? (service classes, behavior callbacks, model overrides, adapter implementations)
2. Inventory `TAOM.Tests/Features/<X>/**.cs` — what tests exist? Map test class → production class. Note coverage type (unit / integration / structural / smoke).
3. Identify untested classes / untested public methods on tested classes.
4. Cross-check Phase 6 cross-feature touchpoints — any service or model named in a cross-feature finding without a test that exercises the touchpoint is P2 at minimum.
5. Identify tests that ARE present but test only happy-path / structural reflection — flag as "coverage exists but weak."

### Output severity rubric

- **P1:** A feature with non-trivial runtime logic has zero tests, or tests exist but exercise only construction. A regression in service logic would not be caught.
- **P2:** A feature has some tests, but a documented cross-feature touchpoint (Phase 6) or a critical service path (Phase 3) is uncovered. ADR-008 specifically requires 100% service coverage; this is a measurable gap.
- **P3:** Coverage exists but is shallow (happy path only, no error / edge cases). No imminent regression risk but ADR-008 weakened.

## Inputs identified during Phase 6 that auto-belong in Phase 7

From `docs/audits/cluster-cross-feature.md`:
- **Finding #1 (P2, issue #170):** No cross-feature test for SmartCavalryAI × MixedFormations handshake. The two `RepresentativeIsCavalry` guards in `FormationLayoutService.cs:74, 191` have no test that exercises them. Phase 7 must inventory all such cross-feature contracts and flag missing tests.
- **Finding #8 (P3):** `TaomSettlementLoyaltyModel.CalculateLoyaltyChange` has no behavioral tests; RevoltTuning provider has 12 tests but the consumer is bare.
- **Finding #13 (P3):** `RaceAgeBehavior.OnDailyTick` and `TaomPregnancyModel` both read `hero.Race` in same tick; no test asserts same-hero same-day consistency.
- **Cluster doc cross-cuts:** the "Static-helper integration points" section names `CareerPassiveHelper` — none of the 10 callers have tests asserting the helper's per-call composition.

From `docs/audits/cluster-campaign-behaviors.md`:
- 24 P1 findings — many reference behavior callbacks (`OnDailyTick`, `OnHourlyTick`, `OnNewGameCreated`, `OnGameLoaded`) without test coverage. Phase 7 should classify which are amenable to unit testing (NSubstitute the adapters) vs which require integration tests (`Not-tested:` trailer).

## Output format

`docs/audits/test-coverage.md` mirrors the other cluster docs:

```markdown
# Test Coverage Audit — Phase 7

Last updated: <date>
Scope: 43 features × test directory presence + test depth analysis

## Executive summary
…

## Master findings table
| # | Severity | Feature | Test gap | File:Line (production) | Test file (or "MISSING") | Issue |

## Per-feature reports
### CharacterCreation
…
(etc. — 43 entries)

## Cross-cuts
- ADR-008 compliance summary (% features at ≥80% service coverage)
- Cross-feature contract test gaps (referenced from Phase 6)
- Patterns that block testability (sealed types, static helpers)

## GitHub issues opened

## Phase 7 complete
```

## Constraint

**No test-writing this phase.** Findings → issues only. Phase 9 batches the test additions.

## Done condition

Phase 7 is complete when:

1. `docs/audits/test-coverage.md` has master findings table + per-feature reports (43 entries) + cross-cuts populated.
2. Every P1/P2 has a GitHub issue (`audit-impl`).
3. `docs/audits/phase-8-kickoff.md` written for the next session (Docs audit — `docs/features/<name>.md` per feature, matches `TEMPLATE.md`).
4. `docs/audits/README.md` phases table updated.
5. `/context-save` ran with descriptor `phase7-tests-complete`.

## Pre-flight

1. `/context-restore` to load the latest snapshot.
2. Read this brief + the 5 cluster docs from Phases 2–6.
3. Spawn 1–2 `feature-dev:code-explorer` agents per feature-cluster (GameModels / CampaignBehaviors / Harmony / UI / Services) in parallel. Each agent inventories the test gap for its cluster's features.
4. Aggregate to `test-coverage.md`.

## What this phase will NOT cover

- Test quality beyond presence/depth (Phase 9 may improve tests).
- Doc staleness (Phase 8).
- Any actual fix work (Phase 9).

## What still hasn't been audited at all (forward-looking)

After Phase 7, only Phase 8 (Docs) and Phase 9 (Fix execution) remain. Phase 9's queue is now substantial: 36+ open `audit-*` issues across Phases 1–6 (#121–#175). Phase 7 will add more. The fix phase will span multiple sessions; consider organizing by issue label or feature dependency graph at that point.
