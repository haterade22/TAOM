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
| 2026-11-19 | "Please merge all and provide with your best recommendations" | merge #1493 to #1496; digest decisions #563 A (no prisoner gate in co-op) and #623 A (hook UNCHECKED on validator exit 2), applied on their branches before the merge | this tick |
| 2026-11-12 | standing word in `think-before-coding.md` | repo-required issues; all four exist | while it stands |

## Status
| Phase or batch | State | Workflow or agent ids | Commits, tips | Notes |
|---|---|---|---|---|
| 0 Preflight, baseline | DONE | | | build exit 0; C# 43028 passed, 3 failed (three language-file tests), 9 skipped; Python 10549 ran, 0 failed, 13 skipped |
| 5 Plans 059 to 062 | DONE | `wf_8bd3135f-bee` (task `wimgt9rkn`) | | 059 CLEAN (2 rounds); 060 REVISED (2 rounds); 061, 062 REVISED (1 round); no residual; open non-blocking notes passed to executors as binding notes |
| 6 Plans commit | DONE | | `af0f5f330` on `improve/2026-11-19b-run` | no new issues (all four exist) |
| 7 Execute | RUNNING | `wf_a47cff28-c2d` (task `wxkeulepc`); 061 again `wf_a11c527a-99f` (task `wzg1rk4jh`) | | Opus at high; worktrees `wt-059` to `wt-062`; 061 BLOCKED on the first run (note said reuse a key that does not exist), re-dispatched with the new key `taom_res_recruit_clamped` accepted; `/localize` owed by the orchestrator after it returns. Spec checks: 060 DONE `8dc68388b` PASS (fixture deviation accepted; `load_roster` without SkillSets still returns 0 templated on shipped data); 061 DONE `207b93986` PASS, key registered `c88502947`; 062 DONE `d0abf3f49` PASS (`IsSameTeam` has one caller; the RefAsm base totals are unverified) |
| 8 Review | DONE | 060 to 062 `wf_933e45cc-01e` (task `wi9w8rdtd`, pool 3); 059 `wf_0773cd98-0d6` (task `w2oo2wbvv`, pool 1) | heads 059 `9d147c068`, 060 `8dc68388b`, 061 `c88502947`, 062 `d0abf3f49` | tier 2 for 059, 060; tier 1 for 061, 062; `--stop-on-low`, max 2 rounds. 059 DONE `589313673` spec check PASS; orchestrator added `9d147c068` (armory-audit skill line, approved scope); plan Maintenance notes copied to wt-run uncommitted. #1489 filed for the 061 follow-ups |
| 8 Residual fixes | DONE | 062 by an implementer (Sonnet) | 059 `520c4de5e`, 060 `eb4c687d5`, 061 `85c75c374`, 062 `7d3e97a70` | all four READY FOR COMMIT with LOW residuals only; each fixed. Final runs by the orchestrator: 061 C# Passed 43046, 062 C# Passed 43065 (3 known language failures each); 059 Python 10561 OK; 060 Python 10573 OK, validator exit 0, 0 UPGRADE_SKILL_REGRESSION |
| 9 Push, pull requests, digest | DONE | | #1493 (059), #1494 (060), #1495 (061), #1496 (062) | `manager` label; digest #1457 rewritten with two decisions (#563 co-op skip, #623 hook exit 2); follow-up issues #1489 to #1492 |
| 10 Merge | DONE | review of the two decision commits `wf_e36655d7-6bf` (task `w3vscccv0`) | #1496 `a38a1013e`, #1494 `24db60d7d`, #1495 `a4d5a386f`, #1493 `19a398e3b` | 061 decision commit `96ea28e4a`, final head `4c4566eed` (C# Passed 43079); 059 decision commit `ea65759af`, trunk merge `9d081d6f2` (C# Passed 43079, Python 10585 OK, hooks 1451 passed, validator 0 errors 1823 warnings); decisions recorded on #563 and #623 |
| 11 Wrap-up | DONE | | plan rows merged; this records PR | #563, #594 labelled `triage-needs-ingame`; claims released; worktrees removed |

## Log
- 2026-11-19: tick 3 dispatched the four approved items #623, #626 (Harness and tools), #563, #594 (Creatures). #593 waits for tick 4: it edits `Directory.Build.props`. Worker runs today after this dispatch: 6 of 8.

## Next
Run complete. Owed by a person: the in-game checks on #563 and #594. Tick 4 may select #593 (it edits `Directory.Build.props`, the maintainer's file).

## Blockers
None.

## Dead ends
None yet.
