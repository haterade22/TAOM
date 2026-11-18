# PROGRESS: 2026-11-18-manage

Baseline `3164713da` on `bannerlord-1.5.x`, 2026-11-18, version `v2.0.40`. Root
`E:\repos\taom-improve\2026-11-18` (worktrees; scratch `E:\repos\taom-improve\2026-11-18\scratch`, temp
`E:\repos\taom-improve\2026-11-18\scratch\tmp`). Branch mode: run branch `improve/2026-11-18-run` in
`wt-run`. Models: plan writers, reviewers and revisers Opus (the default); executors Opus at high
(production C# in a mission tick and in campaign menus). Pool 4.
Codex: not asked (out of usage since 2026-11-15). Second Codex pass on fix diffs: replaced by convergence.
Shape: no audit. Two plans from a `/manage` tick; review with a `tier` per item and `--stop-on-low`.

## Authorizations
| Date | The maintainer's words | Scope | Until |
|---|---|---|---|
| 2026-11-18 | "merge 1465 and run the tick" | one `/manage` tick at ADR-015 stage 0: plan, execute, review, push each READY branch, open a pull request with the `manager` label; no merge | this tick |
| 2026-11-12 | standing word in `think-before-coding.md` | repo-required issues; both issues here exist already (#511, #607) | while it stands |

## Status
| Phase or batch | State | Workflow or agent ids | Commits, tips | Notes |
|---|---|---|---|---|
| 0 Preflight, baseline | DONE | | | build exit 0; C# 42911 passed, 3 failed (three language-file tests), 9 skipped; Python 10453 ran, 3 failed (three `test_run_long_defeat` sally tests), 13 skipped |
| 5 Plans 055, 056 | DONE | `wf_6a2afe2f-ae1` (task `wd0gtt129`) | | both REVISED after one cold review, no residual; 055 now gates on `!HasPlayerEncounter` (a grace player who enters a town himself keeps vanilla); 056's census found MissionDiagnosticBehavior the one unguarded writer (spot-checked: FieldCommission locks in its service) |
| 6 Plans commit | TODO | | | |
| 7 Execute | TODO | | | |
| 8 Spec check, review | TODO | | | |

## Log
- 2026-11-18 16:15: tick selected #511, #607, #771, #1099 from the Stability milestone. A check of each against the baseline code found #771 fixed (both subscribers remove before they add) and #1099 fixed (`fa51a8123`, in-game check owed). Both got a manager comment; #1099 got the label `triage-needs-ingame`. No plan for either.
- 2026-11-18 16:15: #607 is narrower than its body: of its five overrides, four now defer or lock; `FieldCommissionMissionLogic.OnAgentRemoved` does not, and overrides added since the issue are unchecked.

## Next
Baseline totals, then `plans.js` for 055 (#511) and 056 (#607).

## Blockers
None.

## Dead ends
- `graphify_taom.py affected` returns nothing for shell functions; it is for C# types only.
