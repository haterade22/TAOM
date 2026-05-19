# RCA — Diplomacy: same-alignment war block (2026-05-19)

## Top-line

Deep-review found no code-quality findings (Standards, Compatibility, Efficiency, Data-Flow agents all PASS). Two process gaps (no pre-fix GitHub issue, no CHANGELOG entry) and one accepted-by-design test-coverage gap. None block correctness; all are reflexive checklist items pending the closeout phase.

This RCA exists to satisfy the Phase 3e blocking-gate rule (any confirmed finding triggers RCA, regardless of severity — `feedback_root_cause_mandatory.md` + `harness-facts.md`).

## Findings

| # | Sev | Bug | Category | Why missed | Preventive action |
|---|-----|-----|----------|------------|-------------------|
| 1 | LOW | No GitHub issue exists for this fix | Process | User invoked review before closeout; CLAUDE.md says issue must exist BEFORE the closing commit. The fix-then-issue order is a known recurring violation (Codex #28 caught the same on b7e7188 → issue #92 retroactively). | Open issue NOW, before commit. Reference in commit message. |
| 2 | LOW | CHANGELOG.md not updated | Process | Same as above — closeout sequence not yet run. Pre-commit hook `check-changelog-changed.sh` will hard-block the commit and force this anyway. | Update CHANGELOG.md before commit. |
| 3 | LOW | No integration test for `TaomKingdomDecisionPermissionModel.IsWarDecisionAllowedBetweenKingdoms` delegation | Test coverage | `Kingdom` is sealed — the thin-model pattern intentionally leaves boundary delegation untested. ADR-008 explicitly permits this for `Entry Points` ("Not required — Harmony/GameModel — test via game"). | None — accepted by design. Risk: if someone later adds non-trivial logic to the override body, the test gap widens. The csharp-architecture rule already says "no inline branching in GameModel overrides", which is the real guard. |

## Root-cause pattern

None — findings #1 + #2 share a theme (closeout not yet run) but that's by user direction (they invoked `/deep-review` before `/verify` / issue-creation / CHANGELOG). Finding #3 is ADR-008 working as intended. No systemic lesson to extract.

## Why each agent missed (or didn't) these

| Agent | Finding | Status |
|-------|---------|--------|
| Standards (Agent 1) | #1, #2, #3 | Out of scope — ADR/standards-only, not process or test coverage |
| Compatibility (Agent 2) | #1, #2, #3 | Out of scope — API verification only |
| Efficiency (Agent 3) | #1, #2, #3 | Out of scope — perf only |
| Completeness (Agent 4) | #1, #2 | **CAUGHT** — both findings reported correctly |
| Data Flow (Agent 5) | #3 | **CAUGHT** — partial coverage gap flagged as low-severity by-design |

All findings caught by the agent whose scope owns them. No detection gap in the review process itself.

## Feedback memories to codify

None. No new systemic pattern emerged. The pre-commit hook + the existing `feedback_completion_workflow.md` already cover the process gaps; the test gap is documented in ADR-008.

## Action items before commit

1. Create GitHub issue: `gh issue create --title "fix(diplomacy): block war between same-alignment kingdoms" --label bug` with problem (Bard II → Erebor war against Mirkwood; Thranduil → Dale), analysis (`AllianceTier.Permanent`-only gate, missing pairs in `diplomacy.json`), solution (`IsWarAllowed` composes Permanent + same-alignment via `IAlignmentService`), and files-changed list.
2. Update `CHANGELOG.md` with `fix(diplomacy)` entry under today's date.
3. Commit with issue number in subject line, then `gh issue close` once verified in-game.
