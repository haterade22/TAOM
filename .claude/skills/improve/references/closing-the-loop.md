# Closing the Loop — execute, reconcile, issues (TAOM)

<!-- Ported from shadcn/improve @ 5428507 (2026-06-12), MIT (c) 2026 shadcn.
     Dispatch briefing, model routing, review gates, and issue conventions
     recalibrated to TAOM. -->

The advisor's job doesn't end at the plan. This file covers the three follow-through flows: dispatching an executor and reviewing its work (`execute`), keeping the plan backlog alive (`reconcile`), and publishing plans where work gets picked up (`--issues`).

The founding rule survives unchanged: **the advisor never edits source code.** In `execute`, a *separate executor subagent* edits code in an isolated git worktree; the advisor dispatches, reviews, and renders a verdict — like a tech lead who doesn't push commits to your branch.

---

## `execute <plan>` — dispatch and review

### Preconditions (check all before dispatching)

- The plan file exists and its dependencies show DONE in `plans/README.md`. If not: stop, name the missing dependency.
- Run the plan's drift check yourself. If in-scope files changed since `Planned at`, reconcile the plan first — don't hand a stale plan to an executor.
- If the plan is a feature/bug fix and no GitHub issue exists (TAOM issue-first mandate), STOP and flag it to the user before dispatch — issue creation requires explicit user intent (`/issue` or the `--issues` flag); never create one yourself here.

### Dispatch

Spawn **one** subagent with `isolation: "worktree"` (mandatory — parallel executors editing the shared tree is the build-watcher cascade documented in `harness-facts.md`). Executor model: default `sonnet` (TAOM Model Routing: feature implementation = Sonnet); use what the user named if they named one.

The subagent prompt must contain the standard TAOM briefing (CLAUDE.md "Briefing subagents") plus the plan:

1. "Read `docs/ai-includes/agent-operating-manual.md` first. You cannot invoke skills or spawn agents."
2. **The full plan file text, inlined.** The worktree contains only committed files — if `plans/` is uncommitted, the executor can't read it. Never assume; always inline.
3. Tool reminders: build with `dotnet build Main/TAOM.csproj -p:DisableModuleCopy=true`, test with `dotnet test TAOM.Tests -p:DisableModuleCopy=true` (the flag on BOTH — the tests project builds Main, whose post-build target deploys to the game install without it) — NEVER `./build.ps1` (deploys to the game install; single-flight only).
4. The executor preamble:

> You are the executor for the implementation plan below. Follow it step by
> step. Run every verification command and confirm the expected result before
> moving on. Touch only the files listed as in scope. If any STOP condition
> occurs, stop immediately and report. Do not improvise around obstacles.
> Commit your work in the worktree following the plan's git workflow section.
> One override: SKIP the plan's instruction to update `plans/README.md` —
> your reviewer maintains the index. Before reporting, audit every claim in
> your report against an actual tool result from this session — only report
> what you can point to evidence for; if a verification failed or was
> skipped, say so plainly. When finished, reply with exactly the report
> format below.

5. The report format:

```
STATUS: COMPLETE | STOPPED
STEPS: per step — done/skipped + verification command result
STOPPED BECAUSE: (only if STOPPED) which STOP condition, what was observed
FILES CHANGED: list
NOTES: anything the reviewer should know (deviations, surprises, judgment calls)
```

### Review (the advisor's real job here)

Note on fresh worktrees: they share git history but not `bin/`/`obj/` — the executor's first build is cold; that's expected, not a deviation.

Review like a tech lead reviewing a PR against the spec — never fix anything yourself. The executor's self-report is a claim, not evidence (`evidence-over-claims.md` §B — subagent self-reports don't count as verification):

1. **Re-run every done criterion** in the worktree yourself.
2. **Scope compliance**: `git -C <worktree> diff --stat` against the plan's in-scope list. Any file outside scope fails review, full stop.
3. **Read the full diff — treat it as untrusted until reviewed.** Verify every hunk traces to a plan step and reject any change that doesn't, however plausible it looks. Then judge it against "Why this matters" and the conventions named in the plan (adapter discipline, thin entry points, no inline GameModel branching).
4. **Audit the new tests.** Executors game criteria — a test that asserts nothing passes `dotnet test` and proves nothing. Read what the tests assert; for dispatch logic, count the (input × branch) cells.

### Verdict

**Documented deviations are judged on merit, not reflex-blocked.** "Do not improvise" exists to stop silent drift; an executor that hits a real obstacle, adapts minimally, and explains it in NOTES has done the right thing. Treat *undocumented* deviations as review failures.

| Verdict | When | Action |
|---|---|---|
| **APPROVE** | Criteria pass, scope clean, quality holds | Update index status to DONE. Present to the user: diff summary, worktree path and branch, anything from NOTES. **Merging is the user's decision — never merge, push, or commit to their branch.** Remind the user: C# changes ≥2 files still owe `/deep-review` before the closing commit (CLAUDE.md mandatory sequence — approval here doesn't substitute). |
| **REVISE** | Fixable gaps | SendMessage to the same executor with specific, actionable feedback. **Max 2 revision rounds**, then BLOCK. |
| **BLOCK** | STOP condition hit, scope violated unrecoverably, or revisions exhausted | Mark BLOCKED in the index with the reason. Refine or rewrite the plan with what was learned. Tell the user what happened and what changed in the plan. |

Running verification commands inside the executor's worktree is fine — it's isolated and disposable. The no-mutating-commands rule protects the user's working tree, not the worktree.

---

## `reconcile` — keep `plans/` alive

Process what happened since the last session. Read `plans/README.md` and every plan file, then per status:

- **DONE** — spot-check that the done criteria still hold on the current HEAD (cheap ones only). Mark verified in the index. Don't delete plan files — they're the record.
- **BLOCKED** — read the reason. Investigate the underlying obstacle in the codebase. Either rewrite the plan around it (new number if the approach changed fundamentally, in-place refresh otherwise) or mark REJECTED with one line of rationale.
- **IN PROGRESS** (stale) — flag it to the user; an executor probably died mid-run. Check the worktree if one exists.
- **TODO** — run the drift check. If drifted: re-verify the finding still exists (it may have been fixed in passing), then refresh the "Current state" excerpts and `Planned at` SHA. If the finding is gone, mark REJECTED ("fixed independently").

Finish with a short report: what's verified done, what was refreshed, what's rejected, and what's executable right now.

---

## `--issues` — publish plans as GitHub issues

Modifier on any planning invocation. The flag is the user's authorization to create issues — never create them without it (TAOM treats issue creation as a public artifact: explicit intent only).

1. Preflight: `gh auth status` succeeds. If it fails, write the plan files as normal and say why issues were skipped (per `environment-failures.md`: report, don't fix auth).
2. Show the list of titles about to become issues; confirm once if interactive. **Warn before publishing any plan describing a security finding or credential location — issues are publicly visible.**
3. Per plan: `gh issue create --title "<plan title>" --body-file <plan file>` — the plan's self-contained body already carries the Problem/Analysis/Solution shape TAOM issues require. Labels: the category (`bug`, `enhancement`, etc.) — apply only if they exist; skip labels rather than fail.
4. Record each issue URL in the plan's Status block and the index.

The plan file remains the source of truth; the issue is distribution.
