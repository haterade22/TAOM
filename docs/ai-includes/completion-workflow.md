# Completion Workflow & Issue/Doc Requirements

Moved from CLAUDE.md (repo-reorg 2026-07-12). This is the existing Claude builder
workflow. For provider-neutral work use [the shared workflow](../../.ai/README.md).
In a shared review packet, the assigned role and shared policy take precedence:
review alone does not authorize fixes, issue changes or additional paid agents.
An explicit request to conduct paid dispatch authorizes its stated scope under
the [shared policy](../../.ai/policy.md#paid-dispatch-authority). The commands below
remain available for authorized Claude builder work; other clients do not need
these slash commands. Codex uses the [operating guide](codex-operating-guide.md)
and repository workflow skills. External writes require task authority, not just
this document.

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

**Before the commit:** re-read the issue body against `git diff --stat` and the untracked files.
The file list (Files changed, or Implementation for a feature) names every shipped file and no
"(planned)" row, and the Solution or Design and the Testing sections describe what ships, not the plan. A body written before the build is a plan, even when it was correct then.

**Lifecycle:**
- Label issues appropriately (`bug`, `feature`, `crash`, `enhancement`)
- Reference the issue number in commits when possible
- **Close the issue** with `gh issue close` when the work is complete and verified
- A closing keyword in a commit body closes only the number that follows it: `Closes #1036, #1037,
  #1040` closed #1036 alone on push (2026-10-27). Write the keyword before each number
  (`Closes #1036, closes #1037`), or close the rest with `gh issue close`, and check every issue's
  state after the push.
- If an in-game check is still owed at close, label the closed issue `triage-needs-ingame`, and
  remove the label once the check is done; a decision still owed is `triage-blocked-decision`.
  Before you add the label, write the check as steps in the feature doc's `## How to verify in game`
  section, and link that section in the closing comment.
  The backlog is a query, visible from every machine and client:
  `gh issue list --state all --label triage-needs-ingame`

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
- How to verify in game: preconditions, then each `taom.*` command or UI action with the result
  that proves it. `tools/lint_docs.py` blocks the commit of a new feature doc without this heading
- How-To — common operations (e.g., "How to add a new X")
- Performance — any optimization notes (if applicable)

**Existing examples:** `docs/features/race-age-system.md`, `docs/features/offspring-race-inheritance.md`

**Rule:** If a future session needs to understand a feature, the doc should contain enough detail that ZERO decompilation, code reading, or re-analysis is needed for the conceptual understanding. Code reading is only for the current state of the implementation.

### Completion Workflow

Before closing out a feature or fix, run this sequence. `/ship` runs it for you.

1. `/verify`: build and tests pass.
2. `/deep-review [feature]`: its "Risk tier" sets the lenses, the waves and the Codex level. Codex is a
   paid call and runs only on the maintainer's word ([shared policy](../../.ai/policy.md#paid-dispatch-authority)).
3. Fix confirmed findings. Fix a HIGH in session, or record its deferral.
4. Re-review a fix only as `/deep-review` "Fix-loop guidance" says.
5. `/verify` again: the final build and tests pass.
6. Close out: close the GitHub issue with full details, write or update `docs/features/<name>.md`, and
   write the commit body as the changelog entry (`/release` generates `CHANGELOG.md`).

The issue exists before the closing commit: open it when the work starts, reference it in commit
messages, and close it with the final commit. No hook enforces this.

**Process docs:** `docs/reviews/REVIEW-GUIDE.md` (prompt templates), `docs/reviews/REVIEW-LOG.md` (scoring history)

---

<!-- backlinks-start auto-generated; edit lint_docs.py / build_backlinks.py to change -->

## Referenced by

- [docs/INDEX.md](../INDEX.md)
- [docs/reference/release-process.md](../reference/release-process.md)

<!-- backlinks-end -->
