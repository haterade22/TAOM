# Phase 8 Kickoff — Documentation Audit

For the next session. Read this + [feature-manifest.md](feature-manifest.md) + [test-coverage.md](test-coverage.md) + the prior cluster docs ([cluster-gamemodels.md](cluster-gamemodels.md), [cluster-campaign-behaviors.md](cluster-campaign-behaviors.md), [cluster-harmony-patches.md](cluster-harmony-patches.md), [cluster-ui.md](cluster-ui.md), [cluster-cross-feature.md](cluster-cross-feature.md)) before starting.

## Audit state at start of Phase 8

| Phase | Status | Output |
|---|---|---|
| 0 (Manifest) | Complete | [feature-manifest.md](feature-manifest.md) |
| 1 (Wiring) | Complete | [wiring-matrix.md](wiring-matrix.md) + issue #122 |
| 2 (GameModels) | Complete | [cluster-gamemodels.md](cluster-gamemodels.md) + issues #134, #135, #137, #138, #140, #142, #144, #145, #147, #148 |
| 3 (CampaignBehaviors) | Complete | [cluster-campaign-behaviors.md](cluster-campaign-behaviors.md) + issues #123–#131, #132, #133, #136, #139, #141, #143, #146 |
| 4 (Harmony patches) | Complete | [cluster-harmony-patches.md](cluster-harmony-patches.md) + issues #149–#164 |
| 5 (UI / Mixin / Prefab) | Complete | [cluster-ui.md](cluster-ui.md) + issues #165–#169 |
| 6 (Cross-feature handshake) | Complete | [cluster-cross-feature.md](cluster-cross-feature.md) + issues #170–#175 |
| 7 (Tests) | Complete (2026-05-13) | [test-coverage.md](test-coverage.md) + issues #176–#195 |
| **8 (Docs)** | **Not started** | This phase |
| 9 (Triage + Fix) | Not started | issues + commits |

## Goal

Phases 0–7 reviewed feature **correctness** (in isolation, at cross-feature boundaries) and **test coverage**. Phase 8 reviews feature **documentation** — the question is: for each of 44 features, does `docs/features/<name>.md` exist, match `docs/features/TEMPLATE.md`'s shape, and reflect the current implementation?

Output: `docs/audits/docs-gaps.md` + GitHub issues for missing / non-conformant docs (`audit-impl` + `audit-docs` label).

This is **doc presence + shape conformance.** It is NOT a re-review of feature implementation; it is the question "if a future session reads `docs/features/<name>.md`, will they have enough context to maintain the feature without re-decompiling?"

## Inputs

- `docs/features/TEMPLATE.md` — the canonical shape (Overview, Why This Exists, Architecture, Configuration, Key Files, Dependencies, Tests, How-To, Performance).
- `docs/features/*.md` — existing feature docs (verify presence per feature, check shape conformance).
- `Main/Features/<X>/` — production code (just for confirming the doc isn't lying about file locations).
- Phase 0 manifest's "Doc" column — sets baseline expectation.
- `detect-docs-gaps.sh` SessionStart hook — already flags `Main/Features/<X>` directories with no matching `docs/features/*.md`. Phase 8 must address every flag.

## Known doc gaps inherited from prior phases

1. **`Execution → docs/features/execution.md`** — flagged by `detect-docs-gaps.sh` since at least Phase 0. Phase 0 manifest row 19 explicitly notes "**Doc gap**". Phase 8 must close this.
2. **Siege has three doc files** (`siege.md`, `siege-defense.md`, `siege-trebuchets.md`) — Phase 0 manifest row 34 notes the redundancy. Phase 8 normalizes to one primary file per feature (with cross-references as needed for sub-systems).
3. **Manifest 43 vs disk 44 off-by-one** — Phase 7 surfaced; Phase 8 corrects the manifest text.

## Procedure

Phase 8 is one or two parallel `Explore` agents per doc cluster. Each agent enumerates the gaps; a final aggregation pass produces the master report.

### Per-feature check

For each feature in `Main/Features/<X>/`:

1. Does `docs/features/<X>.md` exist? (Use feature dir name verbatim, then check kebab-case variants like `companion-tactics.md`.)
2. If present: does it cover the TEMPLATE.md sections? Score by presence:
   - Overview (mandatory)
   - Why This Exists (mandatory)
   - Architecture (mandatory)
   - Configuration (when applicable — config dir exists?)
   - Key Files (mandatory)
   - Dependencies (mandatory)
   - Tests (mandatory — link to `TAOM.Tests/Features/<X>/`)
   - How-To (mandatory for non-trivial features)
   - Performance (optional)
3. Is the content stale? Spot-check 2-3 file paths in the doc — do they still exist in `Main/Features/<X>/`?

### Output severity rubric

- **P1:** Doc missing entirely AND feature has non-trivial runtime logic.
- **P2:** Doc present but missing ≥3 mandatory sections OR stale file references.
- **P3:** Doc present, ≥6 sections, but minor staleness or weak coverage of one section.

## Output format

`docs/audits/docs-gaps.md` mirrors the other cluster docs:

```markdown
# Documentation Audit — Phase 8

Last updated: <date>
Scope: 44 features × docs/features/<X>.md presence + shape conformance.

## Executive summary
…

## Master findings table
| # | Severity | Feature | Doc path | Missing sections / Staleness | Issue |

## Per-feature reports
…

## Cross-cuts
- Naming convention (kebab-case vs PascalCase) — file name mismatches
- Siege multi-doc normalization
- Manifest 43 vs disk 44 correction
- detect-docs-gaps.sh hook future-proofing

## GitHub issues opened
…

## Phase 8 complete
```

## Constraint

**No doc-writing this phase.** Findings → issues only (or one summary issue for the doc gap pile). Phase 9 batches the writes.

## Done condition

Phase 8 is complete when:

1. `docs/audits/docs-gaps.md` has master findings table + per-feature reports (44 entries) + cross-cuts populated.
2. Every P1/P2 has a GitHub issue (`audit-impl` + `audit-docs`).
3. `docs/audits/phase-9-kickoff.md` written for the fix-execution phase — note the cumulative issue queue (now #121–#195+ = roughly 55+ open audit issues, plus Phase 8 additions).
4. `docs/audits/README.md` phases table updated.
5. `/context-save` ran with descriptor `phase8-docs-complete`.

## Pre-flight

1. `/context-restore` to load the latest snapshot (`phase7-tests-complete`).
2. Read this brief + the 5 cluster docs from Phases 2–6 + `test-coverage.md`.
3. Run `bash .claude/hooks/detect-docs-gaps.sh` (or equivalent grep) to get the canonical list of missing-doc features.
4. Spawn 1–2 `Explore` agents per cluster of features (5 clusters, same batching as Phase 7 for continuity).

## What this phase will NOT cover

- Doc quality beyond presence/shape conformance (Phase 9 may rewrite).
- Test quality (Phase 7 already done).
- Any actual fix work (Phase 9).

## Looking ahead — Phase 9 size estimate

Phase 9 begins with **approximately 55+ open `audit-*` issues** (#121–#175 from Phases 1–6, #176–#195 from Phase 7, plus Phase 8 additions). At a sane rate of ~5 issues per session, Phase 9 will span **10+ fix sessions**.

**Phase 9 strategy options** (decision for the start of Phase 9):
- **Pattern-grouped:** organize Phase 9 sessions by recurring pattern (R1-R5 from Phase 3, "thread-safety asymmetry" from Phase 6, "wiring regression" from Phase 7, "model-layer untested" from Phase 7, "fix-without-regression-test" from Phase 7). Likely the right approach for a substantial queue.
- **Feature-grouped:** organize by feature, closing all issues per feature before moving on. Cleaner per-feature traceability but more context-switching across patterns.
- **Severity-first:** P1s first across all features, then P2s, then P3s. Best risk reduction; worst context locality.

The pattern-grouped approach gets the strongest leverage from Phases 3/6/7 cross-cuts and matches how the recurring patterns were identified.
