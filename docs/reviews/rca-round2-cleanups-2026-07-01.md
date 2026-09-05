# RCA — Round-2 Cleanups Deep Review (2026-07-01)

**Scope reviewed:** `f5f631f9..104f07e5` on `refactor/round2-cleanups` — R1 orphaned-enum deletion (#309, cb9f801e), R2 SettingClamp consolidation (#310, f67811d7), R3 BT-builder characterization tests (#311, 0f0588e5), R4 CreatureTreeTracker extraction (#312, 104f07e5).

**Review method:** 5-dimension workflow (standards, efficiency, wiring parity, behavior preservation, completeness) + adversarial verification of every finding.

**Top line:** behavior preservation, efficiency, and wiring parity all clean — SettingClamp bodies byte-identical to the four deleted helpers, tracker logic reproduces the per-feature blocks exactly, tree names/predicates verified crossover-free. **One confirmed actionable finding** (fixed same session); the remaining raw findings were positive verification notes or out-of-scope re-flags, correctly refuted by the verifiers.

## Findings

| # | Sev | Bug | Category | Why missed | Preventive action |
|---|-----|-----|----------|------------|-------------------|
| 1 | MED | R4's commit message + CHANGELOG claimed "the elephant gains the late-attach counter + first-late log" as a delivered delta, but only the mid-mission half was wired — `ElephantMissionBehavior.OnRemoveBehavior` still lacked the mission-end summary (`LateAttachCount`/`AliveCount`) that Spider/Mûmakil emit, so the claimed telemetry was half-true. | Commit-message/diff parity | The behavior-preservation discipline pointed one way only: I audited hard for *unintended* deltas in moved code, but the *intended* deltas listed in the commit message had no verification step — they were written from intent, not from the diff. | Fixed by adding the summary log + `_loggedErrors.Clear()` (spider/mûmakil parity). Rule: LESSONS-LEARNED "A commit message's claimed deltas are part of the diff" — before committing, grep the staged diff for each delta the message claims. This is the §C evidence-over-claims rule applied to the *claims-of-change* direction. |

## Root-cause pattern

Same family as `feedback_no_write_before_reading_tool_output`: an artifact (commit message) asserted a fact ("gains telemetry") that was not read back from evidence (the staged diff). The novel wrinkle is directionality — review tooling here checks *moved code didn't change*; nothing checked *promised changes actually happened*. One sentence of claimed delta needs the same verification as a claimed count or hash.

## Why the dimensions caught / missed it

- **Standards + wiring-parity (both flagged it):** their prompts compared the three behaviors side-by-side, so the asymmetry at the same call site was visible. Credit to the side-by-side framing — keep it in future creature-feature reviews.
- **Behavior-preservation:** correctly reported the omission as "pre-existing asymmetry preserved" — true from its no-drift mandate; the defect only exists relative to the commit message's claim, which wasn't in its scope.

## Feedback memories to codify

LESSONS-LEARNED entry only (Build, Tooling & Workflow) — this is a workflow rule, not an engine fact.

---

<!-- backlinks-start auto-generated; edit lint_docs.py / build_backlinks.py to change -->

## Referenced by

- [docs/reviews/REVIEW-LOG.md](./REVIEW-LOG.md)

<!-- backlinks-end -->
