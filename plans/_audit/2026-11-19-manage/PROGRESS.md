# PROGRESS: 2026-11-19-manage

Baseline `c6a82aa04` on `bannerlord-1.5.x`, 2026-11-19, version `v2.0.40`. Root
`E:\repos\taom-improve\2026-11-19` (worktrees; scratch `E:\repos\taom-improve\2026-11-19\scratch`, temp
`E:\repos\taom-improve\2026-11-19\scratch\tmp`). Branch mode: run branch `improve/2026-11-19-run` in
`wt-run`. Models: plan writers, reviewers and revisers Opus (the default); executors Opus at high
(native signature scan; mission behaviour registration in `SubModule.cs`). Pool 2.
Codex: not asked (out of usage since 2026-11-15). Second Codex pass: replaced by convergence.
Shape: no audit. Two plans from the second `/manage` tick; review with a `tier` per item and `--stop-on-low`.

## Authorizations
| Date | The maintainer's words | Scope | Until |
|---|---|---|---|
| 2026-11-19 | "Proceed with your best recommendations throughout your process please" | the second `/manage` tick at ADR-015 stage 0: plan, execute, review, push each READY branch, open a pull request with the `manager` label; no merge | this tick |
| 2026-11-12 | standing word in `think-before-coding.md` | repo-required issues; both issues exist (#784, #785) | while it stands |

## Status
| Phase or batch | State | Workflow or agent ids | Commits, tips | Notes |
|---|---|---|---|---|
| 0 Preflight, baseline | DONE | | | build exit 0; C# 42943 passed, 3 failed (three language-file tests), 9 skipped; Python 10494 ran, 3 failed (three `test_run_long_defeat` sally tests), 13 skipped |
| 5 Plans 057, 058 | RUNNING | `wf_b0860d87-293` (task `wimft7o9l`) | | |
| 6 Plans commit | TODO | | | |
| 7 Execute | TODO | | | |
| 8 Spec check, review | TODO | | | |
| 9 Push, pull requests, digest | TODO | | | |

## Log
- 2026-11-19: tick 2 selected #784 (Performance) and #785 (Harness and tools). Skipped: #593, #623, #626 (fix lies in `.props`, `.csproj` or `/tools/`, which CODEOWNERS gives to the maintainer alone; manage rule 2); Stability bugs #635, #1025, #1226 (need game evidence), #1458 (overlaps the live claim `orphan-mapevent-repair`); #1134, #1093, #1103 (need a profiled game run); #850 (player-visible change, not approved); Creatures #563, #594 (gameplay changes, to propose); #479, #620 (live Armory data).
- 2026-11-19: found already fixed and closed: #640 (`efe326242`) and the 14 perf issues of plans 028 to 042; #1035 labelled `triage-needs-ingame`.

## Next
Baseline totals, then `plans.js` for 057 (#784) and 058 (#785).

## Blockers
None.

## Dead ends
None yet.
