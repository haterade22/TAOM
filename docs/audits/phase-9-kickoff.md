# Phase 9 Kickoff — Triage + Fix Execution

For the next session(s). This is the **largest phase**: it consumes the cumulative audit queue from Phases 1–8.

Read this brief + [docs-gaps.md](docs-gaps.md) + [test-coverage.md](test-coverage.md) + the five cluster docs from Phases 2–6 + [feature-manifest.md](feature-manifest.md) before doing anything.

## Audit state at start of Phase 9

| Phase | Status | Issues opened |
|---|---|---|
| 0 (Manifest) | Complete | — |
| 1 (Wiring) | Complete | #122 |
| 2 (GameModels) | Complete | #134, #135, #137, #138, #140, #142, #144, #145, #147, #148 (10) |
| 3 (CampaignBehaviors) | Complete | #123–#131, #132, #133, #136, #139, #141, #143, #146 (16) |
| 4 (Harmony patches) | Complete | #149–#164 (16) |
| 5 (UI / Mixin / Prefab) | Complete | #165–#169 (5) |
| 6 (Cross-feature) | Complete | #170–#175 (6) |
| 7 (Tests) | Complete | #176–#195 (20) |
| 8 (Docs) | Complete | #196–#199 (4) |
| **9 (Triage + Fix)** | **Not started** | (issues + commits) |

**Cumulative open audit-* queue: 79 issues** (#121, #122, #123–#175, #176–#195, #196–#199).

## Goal

Close out the audit. Two outputs:

1. **A triage table** (`docs/audits/phase-9-triage.md`) — every audit issue grouped by recurring pattern, sequenced for fix execution.
2. **Closing commits + closed issues** — the actual fix work. Spans multiple sessions.

This phase does NOT introduce new findings (that would re-open Phases 2–8). Anything net-new during fix work goes into the relevant feature's CHANGELOG entry, not a new audit issue.

## Triage strategy — pattern-grouped (recommended)

Phases 3, 6, and 7 each identified recurring **patterns** that span multiple features. Fixing by pattern gives the strongest leverage:

| Pattern | Sources | Affected issues (representative) | Estimated effort |
|---|---|---|---|
| **R1 — Adapter pattern violations (sealed types in service signatures)** | Phase 3 R1, Phase 7 #178 (Warg) | #178, plus any other service that accepts sealed types | 1 session |
| **R2 — Naked TaleWorlds property access (`.Spouse`, `.Clan`, `.Race`) in models / services** | Phase 3 R2, Phase 6 #27, #28 | #131 (RaceAge), #144 (CulturalFeats), #148 (TroopProgression) and related | 1-2 sessions |
| **R3 — Empty / drop-on-load `SyncData`** | Phase 3 R3 | #132 (Siege), #136 (StartupResources), #139 (CompanionTactics), #141 (EquipPresets), #143 (FiefManagement), #146 (QuickActions) | 1 session |
| **R4 — `IoC.Resolve` in service/VM/engine bodies** | Phase 3 R4, Phase 6 cross-cuts | #173 (CareerPassiveHelper systemic) | 1-2 sessions |
| **R5 — Patches that drop vanilla safety gates** | Phase 3 R5, Phase 4 cluster, Phase 6 #36 (replicate-vanilla-safety-gates memory) | #149 (Patch35 race), #150 (MapConversationTableau no-op), Patch30 MixedFormations findings | 2 sessions |
| **R6 — Wiring-class regression test gaps (the audit-motivating class)** | Phase 7 #191 (Messengers), #192 (SettlementGuards), #193 (SiegeDismount) | #191, #192, #193, #195 (TroopWeight hooks) | 1 session |
| **R7 — Cross-feature handshake test gaps** | Phase 6 #170, #171, #172, #173, #174, #175 + Phase 7 carries | #170, #171, #172, #175, plus Phase 7 #181, #182, #183, #187, #188, #189, #190, #194 | 1-2 sessions |
| **R8 — Stale doc claims (test sections + status)** | Phase 8 #196, #197, #198, #199 | #196 (Execution doc missing), #197 (CompanionTactics build status), #198 (AdvancedCombat tests), #199 (Warg tests) | 1 session (fast — pure doc edits) |
| **R9 — Model-layer untested branches** | Phase 7 #176 (CulturalFeats 16 models), #177 (FiefManagement 5 callbacks), #179 (RaceAge pregnancy), #180 (TroopProgression wage) | #176, #177, #179, #180 | 2-3 sessions |
| **R10 — Fix-without-regression-test backfill** | Phase 7 #194 (SpecialResources tiered-cost), #187 (Banner triplet), etc. | #194, #187, possibly others | 1 session |

**Total estimated: 11–15 fix sessions.**

## Triage strategy — alternatives

- **Severity-first** (P1s across all features, then P2s, then P3s): best risk reduction; worst context locality. Each session bounces between unrelated features.
- **Feature-grouped** (close all issues per feature before moving on): cleanest per-feature traceability; most context-switching across patterns.

**Recommendation:** pattern-grouped. The 10 patterns above were identified BY the audit specifically because they recur; fixing one pattern's instances together is much faster than fixing each feature in isolation.

## Pre-flight session 1 (recommended order)

The fastest visible wins, in order:

1. **R8 — Docs (1 session, ~2 hours).** Fix #196 (write `execution.md`), #198 + #199 (correct the two stale "no tests" claims), #197 (CompanionTactics status disclosure). Cleanup, doc-only commits, easy to ship.
2. **R6 — Wiring regression tests (1 session).** #191 + #192 + #193 + #195. All four are the same pattern: add a behavior-registration / patch-binding smoke test. Likely a shared test helper emerges.
3. **R3 — Empty SyncData (1 session).** Pattern #132, #136, #139, #141, #143, #146. Methodical, each behavior gets its persistence fields filled. Likely catches latent save-load bugs.
4. **R10 — Regression-test backfill (1 session).** #194 (SpecialResources tiered-cost), #187 (Banner triplet). Add the regression tests for bugs that were fixed without test coverage.
5. **R1 — Adapter violations (1 session).** #178 (Warg). Refactor `IWargAttackService` to accept `IAgentAdapter`. Unlocks the 2 currently untestable methods.

After these 5 sessions, ~25 issues should be closed. The remaining ~54 are heavier (R2 naked property access, R4 IoC.Resolve systemic, R5 vanilla safety gates, R7 cross-feature handshakes, R9 model-layer branches) and should follow.

## Per-pattern fix discipline

For EACH pattern session:

1. **Identify the canonical fix.** Look at the most recent feature already in compliance — copy that approach.
2. **Write the fix in ONE feature first.** Get it passing tests. Then propagate.
3. **Run `/verify` after EACH feature.** Don't batch 10 features into one untested commit.
4. **Update CHANGELOG.md** with one line per fixed issue (\"closes #NNN — <pattern> fix in <feature>\").
5. **`gh issue close <N>`** with a comment citing the commit SHA.

## Constraints

- **No new audit findings.** This phase consumes the queue; it does not extend it.
- **Match the scope of each issue.** If issue #176 says \"add behavior-callback tests for 16 GameModels,\" do those 16 — don't refactor the whole CulturalFeats architecture (that's #144's scope).
- **TDD on every fix.** RED → GREEN → REFACTOR. ADR-008 applies; pre-commit hook enforces.
- **Use `/freeze` per session.** Each pattern session locks scope to the relevant directories — see [.claude/skills/freeze/](../../.claude/skills/freeze/). Prevents drift.
- **`/deep-review` and `/review-codex` on every commit touching ≥2 files.** Phase 9's commits will be reviewed; no exceptions.

## Done condition

Phase 9 is complete when:

1. All 79 audit-* issues are closed OR explicitly deferred with a documented reason in `docs/audits/phase-9-triage.md`.
2. `CHANGELOG.md` reflects each fix session's pattern + closed-issue list.
3. `docs/audits/README.md` Phase 9 row → Complete with closed-issue count.
4. A summary commit closes out the audit:`feat(audit): close <N> audit-* issues across phases 1–8 (R1-R10 patterns)`.
5. The audit-motivating crash class (Messengers #121 wiring regression) has documented regression-test coverage in `MessengerCampaignBehaviorTests` AND a generic IoC-registration test pattern is published for all behaviors.

## Risks

- **Cascading conflicts** — multiple R2 / R4 issues touch the same files (`CareerPassiveHelper`, `TaomSettlementLoyaltyModel`). Sequence carefully; don't parallelize fix sessions on overlapping code.
- **Test fragility** — adding behavior-callback tests (R3, R6, R9) requires careful mocking of `CampaignEvents`; the existing `RacePersistenceBehaviorTests` pattern is the model.
- **Scope creep** — fixing a wiring regression test could surface a "while I'm here, this other thing is also wrong" temptation. Resist; file a new (Phase 10 if needed) issue instead.
- **Codex-review cost** — `/deep-review --codex` is billed. Use judiciously; bundle reviewable changes per pattern session, not per individual fix.

## Cumulative audit takeaways (for the eventual retrospective)

The audit identified 79 issues across 9 phases. Three structural takeaways for future TAOM work:

1. **Wiring regression tests are the missing safety net.** Messengers #121 was the trigger; Phase 7 found 4 more features (#191-#195) shipping the same regression class untested. Phase 9 R6 establishes the pattern; future feature work should adopt it by default.
2. **Cross-feature contracts need integration tests, not per-feature tests.** Phase 6 found 41 cross-feature findings; Phase 7 confirmed most cross-feature touchpoints have ZERO integration coverage. The fix is a small library of cross-feature contract tests (R7); future features should be unable to merge without one.
3. **Doc staleness is real but specific — test claims drift fastest.** Phase 8 expected v1.2 staleness; the actual finding was stale Tests sections (R8 #198, #199). Future features should auto-link Tests sections to `TAOM.Tests/Features/<X>/` or auto-fail CI when the count drifts.

## Last verified: 2026-05-13
