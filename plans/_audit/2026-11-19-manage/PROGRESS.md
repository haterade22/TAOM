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
| 5 Plans 057, 058 | DONE | `wf_b0860d87-293` (task `wimft7o9l`) | | 057 CLEAN; 058 REVISED (three blocking items fixed; design change: a failed build reports to the mission-start guard so the player notice stays); residual: compile checks unverified, covered by the plan's STOP conditions |
| 6 Plans commit | DONE | | `8e836fd40` on `improve/2026-11-19-run` | no new issues (both exist) |
| 7 Execute | DONE | `wf_cdf14435-162` (task `wt8p2b4ae`) | 057 `66fcd4204` (2 files, C# 42954 passed, 3 known failures); 058 `3e1b1dc9b` (13 files, 42953 passed, 3 known failures) | Opus at high; spec checks passed (057: bounded IndexOf, old path for a leading wildcard; 058: 30 factories, engine add outside the isolation, failure reported to the guard) |
| 8 Review | DONE | `wf_d6cfb5d3-2c7` (task `w8twzjbgj`) | 057 lead `b1fe2262d`, my LOW fix `a200bffa9`; 058 lead `079081c8e` (OOM no longer contained), my LOW fix and docs pass `13409ddce` | both leads READY FOR COMMIT; one convergence round each, LOW only (`--stop-on-low`); 057 parity harness 21,516,800 exhaustive cases, 0 mismatches |
| 9 Push, pull requests, digest | DONE | | PR #1486 (057, ready at `a200bffa9`), PR #1487 (058, trunk merged in at `168b2326b`, ready there) | manager re-ran build and full C# suite at each head: 42954 passed (057), 42985 passed after the trunk merge (058), 3 known failures each; digest #1457 rewritten |
| 10 Merge | DONE | | #1486 `d055b77bc`, #1487 `a9c413ab2` (2026-11-19) | merged on the maintainer's word; #1487 took trunk again at `c2d710f4a` (REVIEW-LOG conflict, both entries kept), re-run there: build exit 0, C# 43007 passed, 3 known failures; #784 and #785 closed, #785 with `triage-needs-ingame` |

## Log
- 2026-11-19: tick 2 selected #784 (Performance) and #785 (Harness and tools). Skipped: #593, #623, #626 (fix lies in `.props`, `.csproj` or `/tools/`, which CODEOWNERS gives to the maintainer alone; manage rule 2); Stability bugs #635, #1025, #1226 (need game evidence), #1458 (overlaps the live claim `orphan-mapevent-repair`); #1134, #1093, #1103 (need a profiled game run); #850 (player-visible change, not approved); Creatures #563, #594 (gameplay changes, to propose); #479, #620 (live Armory data).
- 2026-11-19: found already fixed and closed: #640 (`efe326242`) and the 14 perf issues of plans 028 to 042; #1035 labelled `triage-needs-ingame`.

## Next
The run is done. Owed: the in-game check of #785 (its PR body gives the steps).

## Blockers
None.

## Dead ends
None yet.
