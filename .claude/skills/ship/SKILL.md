---
name: ship
description: Orchestrate the mandatory completion sequence before merging a C# feature (/verify, /deep-review, fix findings, /review-codex), then close the issue and update the docs.
argument-hint: [feature-name]
---

# Ship a Feature (completion workflow orchestrator)

Run the **mandatory** completion sequence from CLAUDE.md "Completion Workflow" end-to-end. This skill exists because the sequence is prose in CLAUDE.md and steps get silently skipped — the RCA history (`docs/reviews/rca-crash-report-2026-05-25.md`) shows a 60-file feature shipped with unfixed HIGH/MED findings because Phases 2 and 4 were skipped.

## When to invoke

- A C# feature or fix touching **≥2 files or any feature module** is ready to merge.
- **Skip for** one-line fixes, XML/config/docs-only changes — running 5+ review agents + Codex on those is wasteful and costs money.

## Phases (do not skip any — each exists because the prior proved insufficient)

### Phase 1 — Build & internal review
1. `/verify` — build + tests must pass. If red, stop and fix.
2. `/deep-review $ARGUMENTS`: its Step 2 sets the tier, the lenses and the waves.
3. Handle findings as `/deep-review` requires.

### Phase 2 — Codex adversarial review (costs money — explicit go-ahead only)
4. `/review-codex`: writes the prompt and dispatches Codex via `codex exec` in the background, at a reasoning effort the session sizes to the change; tells the user the level, the reason and the expected window once.
5. Harness notifies on completion — auto-resume, verify each finding against TAOM source + decompiled vanilla, implement confirmed fixes.

### Phase 3 — Self-review of our fixes
6. `/review-codex` again against the post-fix diff. Verify + fix. Write the Phase 3e RCA for **every** confirmed bug (not just HIGH — see `.claude/rules/harness-facts.md`).

### Phase 4 — Close out
7. `/verify` — final build + tests green.
8. The issue exists before the closing commit, opened on the maintainer's word when work starts (per `/issue`). Close it with the final commit. Work already done without an issue: ask the maintainer; if the maintainer declines, the commit body says so.
9. Write/update `docs/features/<name>.md`, including `## How to verify in game`. An in-game check still owed goes there as steps before the issue gets `triage-needs-ingame`.
10. Write the commit body as the changelog entry; `/release` generates `CHANGELOG.md` from it.
11. If the work ran in a worktree, run `python tools/worktree_audit.py` once it lands and report its bucket for that worktree. Remove nothing without Mike's word.

## Gotchas
- `/review-codex` and `/codex-verify` cost real money — confirm with the user before Phase 2 unless they already authorized the ship.
- Do not commit before Phases 1–3 are clean — they are blocking gates.
- This skill **invokes** the sub-skills; it does not reimplement them. Treat each sub-skill's SKILL.md as the source of truth for that step.
