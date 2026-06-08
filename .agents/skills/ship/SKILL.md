---
name: ship
description: Orchestrate the mandatory completion sequence before merging a C# feature — /verify, /deep-review, fix findings, /review-codex, then close the issue and update docs + CHANGELOG.
argument-hint: [feature-name]
---

# Ship a Feature (completion workflow orchestrator)

Run the **mandatory** completion sequence from AGENTS.md "Completion Workflow" end-to-end. This skill exists because the sequence is prose in AGENTS.md and steps get silently skipped — the RCA history (`docs/reviews/rca-crash-report-2026-05-25.md`) shows a 60-file feature shipped with unfixed HIGH/MED findings because Phases 2 and 4 were skipped.

## When to invoke

- A C# feature or fix touching **≥2 files or any feature module** is ready to merge.
- **Skip for** one-line fixes, XML/config/docs-only changes — running 5+ review agents + Codex on those is wasteful and costs money.

## Phases (do not skip any — each exists because the prior proved insufficient)

### Phase 1 — Build & internal review
1. `/verify` — build + tests must pass. If red, stop and fix.
2. `/deep-review $ARGUMENTS` — 5+ parallel agents (standards, compat, efficiency, completeness, data-flow; C++ checks auto-fire if `.cpp`/`.h` in scope). It triages findings spec-compliance-first (see `docs/ai-includes/agent-teams.md`).
3. Fix all confirmed findings. **HIGH must be fixed in-session** — no silent deferrals (`.Codex/skills/deep-review/SKILL.md`).

### Phase 2 — Codex adversarial review (costs money — explicit go-ahead only)
4. `/review-codex` — writes the prompt and dispatches Codex via `codex exec` in the background; tells the user the 10–45 min window once.
5. Harness notifies on completion — auto-resume, verify each finding against TAOM source + decompiled vanilla, implement confirmed fixes.

### Phase 3 — Self-review of our fixes
6. `/review-codex` again against the post-fix diff. Verify + fix. Write the Phase 3e RCA for **every** confirmed bug (not just HIGH — see `.Codex/rules/harness-facts.md`).

### Phase 4 — Close out
7. `/verify` — final build + tests green.
8. The GitHub issue must **already exist** (open it when work started, per `/issue`). Close it with the final commit — do NOT create it retroactively.
9. Write/update `docs/features/<name>.md`.
10. Update `CHANGELOG.md`.

## Gotchas
- `/review-codex` and `/codex-verify` cost real money — confirm with the user before Phase 2 unless they already authorized the ship.
- Do not commit before Phases 1–3 are clean — they are blocking gates.
- This skill **invokes** the sub-skills; it does not reimplement them. Treat each sub-skill's SKILL.md as the source of truth for that step.
