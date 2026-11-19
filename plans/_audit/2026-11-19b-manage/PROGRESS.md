# PROGRESS: 2026-11-19b-manage

Baseline `db5680c8a` on `bannerlord-1.5.x`, 2026-11-19, version `v2.0.40`. Root
`E:\repos\taom-improve\2026-11-19b` (worktrees; scratch `E:\repos\taom-improve\2026-11-19b\scratch`, temp
`E:\repos\taom-improve\2026-11-19b\scratch\tmp`). Branch mode: run branch `improve/2026-11-19b-run` in
`wt-run`. Models: plan writers, reviewers and revisers Opus (the default); executors Opus at high.
Pool 4. Codex: not asked (out of usage since 2026-11-15). Second Codex pass: replaced by convergence.
Shape: no audit. Four plans from the third `/manage` tick; review with a `tier` per item and `--stop-on-low`.

## Authorizations
| Date | The maintainer's words | Scope | Until |
|---|---|---|---|
| 2026-11-19 | "proceed with your best recommendations", answering digest #1457 decisions 1 to 3 with the recommended A | #623 and #626 may edit `/tools/` and `.claude/` paths CODEOWNERS gives to the maintainer alone (#593 too, next tick); #563 and #594 gameplay changes approved as proposed; plan, execute, review, push, open pull requests with the `manager` label; no merge | this tick |
| 2026-11-12 | standing word in `think-before-coding.md` | repo-required issues; all four exist | while it stands |

## Status
| Phase or batch | State | Workflow or agent ids | Commits, tips | Notes |
|---|---|---|---|---|
| 0 Preflight, baseline | DONE | | | build exit 0; C# 43028 passed, 3 failed (three language-file tests), 9 skipped; Python 10549 ran, 0 failed, 13 skipped |
| 5 Plans 059 to 062 | RUNNING | `wf_8bd3135f-bee` (task `wimgt9rkn`) | | 059 and 060 safetyGate (two review rounds) |
| 6 Plans commit | TODO | | | |
| 7 Execute | TODO | | | |
| 8 Review | TODO | | | |
| 9 Push, pull requests, digest | TODO | | | |

## Log
- 2026-11-19: tick 3 dispatched the four approved items #623, #626 (Harness and tools), #563, #594 (Creatures). #593 waits for tick 4: it edits `Directory.Build.props`. Worker runs today after this dispatch: 6 of 8.

## Next
Baseline totals, then `plans.js` for 059 to 062.

## Blockers
None.

## Dead ends
None yet.
