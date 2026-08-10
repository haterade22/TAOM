# Completion Workflow & Issue/Doc Requirements

Moved verbatim from CLAUDE.md (repo-reorg 2026-07-12) — CLAUDE.md keeps the mandates + the 4-phase outline; this doc holds the full templates and step-by-step sequence. The `/issue` and `/ship` skills encode the same requirements operationally.

### GitHub Issues — Create for ALL Work

Every feature, bug fix, crash fix, or system change MUST have a GitHub issue. No exceptions.

**When to create:**
- Starting a new feature → create issue BEFORE implementation
- Fixing a bug/crash → create issue documenting the problem FIRST
- Completing a fix that was done without an issue → create issue retroactively with full details

**Issue content — be exhaustive:**

For **bug/crash fixes**, the issue body MUST include:
1. **Problem** — exact error message, stack trace, reproduction steps
2. **Analysis** — root cause investigation, what was examined, why it happened
3. **Solution** — what was changed and WHY that approach was chosen
4. **Files changed** — list of modified files with one-line descriptions
5. **Testing** — how the fix was verified

For **features**, the issue body MUST include:
1. **Motivation** — why this feature exists, what problem it solves
2. **Design** — architecture decisions, alternatives considered
3. **Implementation** — key files, patterns used, configuration
4. **Testing** — test coverage, how to verify it works

**Lifecycle:**
- Label issues appropriately (`bug`, `feature`, `crash`, `enhancement`)
- Reference the issue number in commits when possible
- **Close the issue** with `gh issue close` when the work is complete and verified

**Commands:** Use `gh issue create` and `gh issue close` via Bash.

### Feature Documentation — `docs/features/`

Every completed feature MUST have a documentation file at `docs/features/<feature-name>.md`. This is the **knowledge base** that prevents future sessions from re-analyzing solved problems.

**Use template:** `docs/features/TEMPLATE.md`

**Sections required:**
- Overview — what it does in 2-3 sentences
- Why This Exists — the problem it solves, with specific examples
- Architecture — design challenge, solution approach, component diagram
- Configuration — config files, data formats, current values
- Key Files — table of all files with their purpose
- Dependencies — what it relies on
- Tests — test file locations and coverage summary
- How-To — common operations (e.g., "How to add a new X")
- Performance — any optimization notes (if applicable)

**Existing examples:** `docs/features/race-age-system.md`, `docs/features/offspring-race-inheritance.md`

**Rule:** If a future session needs to understand a feature, the doc should contain enough detail that ZERO decompilation, code reading, or re-analysis is needed for the conceptual understanding. Code reading is only for the current state of the implementation.

### Completion Workflow (MANDATORY — every feature, no exceptions)

Before closing out any feature or fix, run this FULL sequence:

```
Phase 1: BUILD & INTERNAL REVIEW
  1. /verify                        — build + tests pass
  2. /deep-review [feature]         — 5+ parallel agents (standards, compat, efficiency, completeness, data-flow)
  3. Fix all confirmed findings (HIGH must fix in-session)

Phase 2: CODEX ADVERSARIAL REVIEW (Claude dispatches directly, no user terminal step)
  4. /review-codex                  — writes prompt to docs/reviews/codex-adversarial-{feature}-{date}.prompt.md
                                      AND dispatches via `codex exec - < prompt.md > output.md 2>&1` (run_in_background)
                                      AND tells the user once: "dispatched, expected window 10-45 min"
  5. (harness notifies on completion — Claude auto-resumes; no /review-codex re-invocation needed)
  6. Verify each Codex finding by reading TAOM source + decompiling vanilla targets — implement confirmed fixes

Phase 3: SELF-REVIEW (review our OWN fixes)
  7. /review-codex                  — second pass, same auto-dispatch flow against the post-fix diff
  8. (harness notifies on completion)
  9. Verify findings on our fixes, implement confirmed fixes

Phase 4: CLOSE OUT
  10. /verify                       — final build + tests pass
  11. Create/close GitHub issue with full details
        ↑ Issue must exist BEFORE the closing commit, not after.
          Codex review #28 caught us creating issue #92 retroactively for
          b7e7188. The pre-commit hook only enforces CHANGELOG, not issue
          creation — discipline is on the author. Pattern: open the issue
          when starting the work, reference it in commit messages, close
          it with the final commit.
  12. Write/update docs/features/<name>.md
  13. Update CHANGELOG.md
```

**Do not skip any phase.** Phase 2 catches bugs Claude misses (43 found in codebase review). Phase 3 catches bugs in our fixes (already caught IsFemale field targeting wrong type, shaghana/abanissa alignment mismatch). Each phase exists because the previous one proved insufficient.

**Process docs:** `docs/reviews/REVIEW-GUIDE.md` (prompt templates), `docs/reviews/REVIEW-LOG.md` (scoring history)

---

<!-- backlinks-start auto-generated; edit lint_docs.py / build_backlinks.py to change -->

## Referenced by

- [docs/reference/release-process.md](../reference/release-process.md)

<!-- backlinks-end -->
