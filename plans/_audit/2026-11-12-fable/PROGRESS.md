# PROGRESS: 2026-11-12-fable

Baseline `7087fa633` on `bannerlord-1.5.x`, 2026-11-12, version `v2.0.38`. Root
`E:\repos\taom-improve\2026-11-12` (worktrees; scratch `E:\repos\taom-improve\2026-11-12\scratch`, temp
`E:\repos\taom-improve\2026-11-12\scratch\tmp`). Pool 4.
Codex: not asked (out of usage, 2026-11-12). Second Codex pass on fix diffs: replaced by convergence.

**Run branch:** `improve/2026-11-12-run`, worktree `E:\repos\taom-improve\2026-11-12\wt-run`. The
maintainer asked (2026-11-12) for every change on a new branch that he merges later. So this run folder
and the plans live on the run branch, not in the main checkout, and each `improve/NNN-<slug>` branch
merges into the run branch only. Nothing is merged into the trunk and nothing is pushed.

**Models** (the maintainer asked for a model and effort per task; orchestrator: Fable 5.2, high):

| Role | Model, effort | Why |
|---|---|---|
| Audit lanes arch, perf, harness | `claude-opus-5-5`, high | cross-cutting judgment |
| Audit lanes tests, tools, docs | `claude-sonnet-5-5`, high | wide reading, clear criteria |
| Verifiers | `claude-opus-5-5`, high | a wrong verdict costs a plan |
| Critic | `claude-opus-5-5`, xhigh | one agent, decides the top ten |
| Plan writer, reviser | `claude-opus-5-5`, high | the plan is the product |
| Plan cold reviewer | `claude-opus-5-5`, high | |
| Executors | `claude-sonnet-5-5`, medium for S and docs plans; `claude-opus-5-5`, high for C# refactors | cheapest tier that fits |
| Review lenses | `deep-reviewer` definition (no model passed) | rule |

## Authorizations

| Date | The maintainer's words | Scope | Until |
|---|---|---|---|
| 2026-11-12 | "You have full authority to run throughout the night. If anything stops you move onto the next thing and add issues for those things to come back to." | Run phases 0 to 9 unattended; file one issue per blocked item | the end of this run |
| 2026-11-12 | "ensure you make all changes on a new branch we can merge later" | All commits on `improve/2026-11-12-run` and its plan branches; no trunk merge, no push | the end of this run |
| 2026-11-12 | "Keep it reasonable" (agents); "pick the correct model and effort for each task" | Agent count and tiers as in the Models table | the end of this run |
| 2026-11-12 | "I don't want to waste usage on things they already did" (Kimi) | BRIEF.md "Already done" | the end of this run |
| 2026-11-12 (standing, think-before-coding.md) | "do what you recommend, repo-required issues too" | Issue-first for each executed plan | standing |

Not authorized: merge into the trunk, push, paid calls (Codex, translator), protected-file edits,
deleting what this run did not create, a game launch or deploy (taom-b4 holds the install tonight).

## Status

| Phase or batch | State | Workflow or agent ids | Commits, tips | Notes |
|---|---|---|---|---|
| 0 Preflight | DONE | | | see Log |
| 0 Baseline | DONE | orchestrator | | `baseline.md`: C# 39,250 passed, 0 failed; Python 8,578 run, 7 failures and 2 errors (heraldry data drift on the trunk); hooks 1,267 passed |
| 1 Reconcile | DONE | orchestrator | | 043 and 044 still TODO with issues; 045 complete |
| 2 Audit lanes | DONE | workflow `wf_4cb8a62c-160`, script `scratch\wf\lanes.js` | | 6 of 6 returned; 1.51 M subagent tokens; files `lane-*.findings.md` |
| 3 Verify, critic | DONE | workflow `wf_516bc699-dcb`, script `scratch\wf\verify.js` | | 3 of 3 returned; 0.79 M subagent tokens; 27 findings checked: 5 confirmed, 21 narrowed, part of 1 refuted; no critic (DECISIONS 3) |
| 4 REPORT, selection | DONE | orchestrator | | nine items, 046 to 054 (DECISIONS 4) |
| 5 Plans 046 to 051 | RUNNING | workflow `wf_997c0618-f9b` (task `w047nzgwk`), script `scratch\wf\plans.js` | | writer, cold reviewer, reviser; all Opus high |
| 5b Items 052 to 054 | TODO | orchestrator, direct | | harness text, hooks, docs (DECISIONS 5) |
| 6 Plans commit, issues | TODO | | | |
| 7 Execute | TODO | | | |
| 8 Review | TODO | | | |
| 9 Decisions, follow-ups | TODO | | | |
| 10 Merge into the run branch | TODO | | | never the trunk |
| 11 Wrap-up | TODO | | | |

## Log

- 2026-11-12 21:40: preflight. C: 14 GB free (98 % used), E: 1.4 TB free. `TEMP`, `TMP` and
  `NUGET_PACKAGES` are on E:. `core.autocrlf` true; the five workflow scripts are `w/lf`.
  `core.hooksPath` is `e:\repos\TAOM\.githooks` (exists). `gh auth status`: logged in. 39 worktrees
  before this run; none of them on C:. No Bannerlord process at 21:40.
- 2026-11-12 21:40: taom-b4 (another session) deploys and launches the game tonight. This run reads
  the installed DLLs as build references only.
- 2026-11-12 21:45: Kimi record found in the Kimi client's session log (not in the repo): one
  refactor, `e99fcccc9`, with its docs. Its four open leads are in BRIEF.md.
- 2026-11-12 21:42: run branch and baseline worktree made at `7087fa633`. Baseline started.
- 2026-11-12 21:46: lanes workflow launched through a wrapper script (`scratch\mkwf.py` embeds the args
  JSON and calls `fanout.js` with `workflow()`), so the 24 KB of args are never typed into a tool call.
- 2026-11-12 22:02: baseline complete. The trunk's Python suite is red before this run (9 tests, all
  heraldry and kingdom generators against shipped data).
- 2026-11-12 22:14: lanes complete. Findings: arch 10, perf 5, tests 8, tools 10, harness 11, docs 9.
  The perf lane found no code change worth a plan (the mission tick is 0.39 ms of an 8.69 ms frame in
  23 profiled battles); its top item is a measurement for the maintainer.
- 2026-11-12 22:15: verification launched.
- 2026-11-12 22:33: verification complete. The refuters cut the tools lane's two largest findings
  (25 one-off scripts to move became 2; five scripts to delete became 3) and found two traps the plans
  now carry (an eager clock read in the Refuge extraction; the messenger arrival rule as it executes).
- 2026-11-12 22:45: REPORT.md, FOR-MIKE.md and DECISIONS.md written. Plans workflow launched for 046 to
  051.

## Next

Commit the run folder. Make 052 (harness text), 053 (hooks) and 054 (docs) on the run branch. When the
plans return: read each, commit them, open the issues, make one worktree per plan, execute.

## Blockers

None.

## Dead ends

- None yet.
