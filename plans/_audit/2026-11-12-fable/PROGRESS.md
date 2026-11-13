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
| 2026-11-13 01:55 | "commit, push and merge when you need to and proceed with your best recommendations! Deconlict with other sessions and COOP always is priority for game runs" | Push the run branch; merge into the trunk when the orchestrator's conditions hold (every branch READY, the trunk merged in, full verification green, one in-game smoke run passed); game runs allowed when the install is free, co-op sessions first | the end of this run |
| 2026-11-12 (standing, think-before-coding.md) | "do what you recommend, repo-required issues too" | Issue-first for each executed plan | standing |

Not authorized: paid calls (Codex, translator), protected-file edits, deleting what this run did not
create outside its branches. Until 2026-11-13 01:55 a trunk merge, a push and a game launch were also
not authorized; the row of that time changes that.

## Status

| Phase or batch | State | Workflow or agent ids | Commits, tips | Notes |
|---|---|---|---|---|
| 0 Preflight | DONE | | | see Log |
| 0 Baseline | DONE | orchestrator | | `baseline.md`: C# 39,250 passed, 0 failed; Python 8,578 run, 7 failures and 2 errors (heraldry data drift on the trunk); hooks 1,267 passed |
| 1 Reconcile | DONE | orchestrator | | 043 and 044 still TODO with issues; 045 complete |
| 2 Audit lanes | DONE | workflow `wf_4cb8a62c-160`, script `scratch\wf\lanes.js` | | 6 of 6 returned; 1.51 M subagent tokens; files `lane-*.findings.md` |
| 3 Verify, critic | DONE | workflow `wf_516bc699-dcb`, script `scratch\wf\verify.js` | | 3 of 3 returned; 0.79 M subagent tokens; 27 findings checked: 5 confirmed, 21 narrowed, part of 1 refuted; no critic (DECISIONS 3) |
| 4 REPORT, selection | DONE | orchestrator | | nine items, 046 to 054 (DECISIONS 4) |
| 5 Plans 046 to 051 | DONE | workflow `wf_997c0618-f9b`, script `scratch\wf\plans.js` | plans commit `664f19c5b` | 18 agents, 3.34 M subagent tokens; all six REVISED (13 blocking review items fixed); one residual in 050, an unverified compiler diagnostic that its check tolerates |
| 5b Items 052 to 054 | DONE | orchestrator, direct | 052 `e88fa0050`, 054 `4a9e001be`, 053 `2a913ecdf`, all on the run branch | 052: doc lint exit 0, entry docs 25,590 to 25,291 B. 054: doc lint exit 0, `PatchCategoryNumberTests` 4 passed. 053: hook suite `1271 passed, 0 failed` (1267 before), config security scan unchanged (one MED, pre-existing) |
| 6 Plans commit, issues | RUNNING | orchestrator | | issues filed: #1132 (umbrella for 046 to 054), #1133 (messenger to a prisoner), #1134 (Fords allocation), #1135 (Python suite red on the trunk) |
| 7 Execute 046, 048, 051 (Sonnet) | DONE | workflow `wf_76e09a37-167`, script `scratch\wf\exec-sonnet.js` | 046 `eb38f4009` (3 commits), 048 `5e788732f`, 051 `ee81570a3` (3 commits) | all three DONE, 0.42 M subagent tokens. Spec checks by the orchestrator: 048 suite `Passed: 39250` (equal to the base); 046 `Passed: 39257` (7 new tests); 051 ruff clean, doc lint exit 0, the three touched test modules 63 OK, executor's full Python run `Ran 8581`, the same nine known failures |
| 8 Review 048, 049 | DONE | workflow `wf_0224f53a-8a8`, script `scratch\wf\review-a.js` | 048 tip `7f0fe00cf`, 049 tip `268b8a9b9` | 11 agents, 1.83 M subagent tokens. 048 CLEAN (lens 5; no code finding; one record count fixed in round 1). 049 READY FOR COMMIT (lenses 5 and 2; one finding fixed by the lead); its last open item, LOW, the wording of one test comment, fixed by the orchestrator in `268b8a9b9` under the stop rule. 048 `needs_mike`: two pre-existing follow-ups (FOR-MIKE) |
| 10 Merge into the run branch | DONE | orchestrator, `integrate_branch.py`, then build and full C# suite on the staged merge before each commit | 047 `97e9053b3` (39,268 passed), 050 `e77849d92` (39,296), 048 `c34192e74` (39,296), 049 `7af4ace9e` (39,312), 046 `62ea77ed7` (39,321), 051 `0d4642051` (39,321; ruff clean, 103 touched tool tests OK) | `docs/reviews/REVIEW-LOG.md` resolved by union in five merges; no other conflict |
| 10b Trunk into the run branch | DONE | orchestrator | `45ee5b5f9` (trunk `1ed44b54d`, 11 commits since the baseline) | one conflict, `CLAUDE.md`: another session's context diet removed the same duplicated passage as item 052; the trunk's wording was taken |
| 10c Full verification at `45ee5b5f9` | DONE | orchestrator, every `/verify` step | | build 0 errors, 11 warnings; NuGet audit clean; C# `Passed: 39334, Failed: 0, Skipped: 9`; Python `Ran 8604 tests`, `OK (skipped=13)`; canary tripped; API snapshot reproduces; provenance 0 misses; doc lint exit 0; ModuleData 0 errors; hook suite `1271 passed, 0 failed`; config security scan unchanged (one MED, pre-existing) |
| 10d Push the run branch, in-game smoke, trunk | DONE | orchestrator | run branch and trunk at `db1c87d7c` (trunk `807263ca2..db1c87d7c`, a fast-forward) | trunk `807263ca2` merged in first (C# `Passed: 39350`). Smoke: `/game-run`, on the maintainer's "Run it now" (2026-11-13 08:35); `build.ps1` from the run worktree; run folder `E:\Temp\fords-runs\20261113-083940`; `reason=final`, `pass=2 fail=0 partial=9 notseen=5`; `[BuildStamp]` `+db1c87d7c`; session summary `checks_fail=0 swallowed=0 errors=0`; clean module unload. The one `[ERROR]` line (`ActionIndexCacheRepair ... REPAIRED 219 of 220`) is also in three trunk runs of the same night |
| 11 Wrap-up | DONE | orchestrator | | index rows 046 to 054 set; #1132 closed with `triage-needs-ingame`; plan worktrees and merged plan branches removed; the run worktree and the remote run branch are left for the maintainer |
| 8 Review 046, 051 | DONE | workflow `wf_0b134a15-79f`, script `scratch\wf\review-b.js` | 046 tip `efc70d4d4`, 051 tip `c482070d9` | 11 agents, 1.99 M subagent tokens. Both READY FOR COMMIT. 046: lead confirmed and fixed 2, round 1 added a probe for the `.cs` clause of the cache filter; residual LOW, a record gap (FOR-MIKE 17). 051: lead confirmed and fixed 5; round 2 CLEAN; residual LOW, one RCA table cell (FOR-MIKE 17) |
| 7 Execute 049, 047, 050 (Opus) | DONE | workflow `wf_8f9e2345-545`, script `scratch\wf\exec-opus.js` | 049 `9b783ca9a`, 047 `911373c9e` (3 commits), 050 `0848c47e2` | all three DONE, 0.49 M subagent tokens. Spec checks by the orchestrator (build 0 errors each): 049 `Passed: 39266` (16 new), 047 `Passed: 39266` (16 new), 050 `Passed: 39278` (28 new); each diff read and in scope. 047 deviation: the relation helper takes a float, because `Hero.GetRelationWithPlayer` returns float |
| 8 Review 047, 050 | DONE | workflow `wf_d4a86b15-8cd`, script `scratch\wf\review-c.js` | 047 tip `3dacbf957`, 050 tip `b55d07b35` | 12 agents, 2.17 M subagent tokens. Both READY FOR COMMIT, convergence round 2 CLEAN. 047: lead confirmed 3 findings and applied 3 (two comments, two tests), suite `Passed: 39268`. 050: lead confirmed 1 (the source pin did not check two engine reads), suite `Passed: 39278`. Residual, LOW, both commit-message wording (FOR-MIKE): `cf21b3d33` body overstates what the binding gate fails on; `3014400f8` should be `test:`, not `fix:`. 050 `needs_mike`: two more bug leads added to #1133, one optional design item |
| 8 Review | TODO | | | |
| 9 Decisions, follow-ups | TODO | | | |
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

- 2026-11-12 22:47: run folder committed (`4e5220c77`). 052 committed (`e88fa0050`).
- 2026-11-12 22:55: 054 committed (`4a9e001be`). Two migration templates still name
  `tools/migrate_equipment_type_1_4_3.py` and `tools/validate_equipment_flags_1_4_3.py`, which moved to
  `tools/oneoff/`; they were NOT repointed, because both moved scripts compute their repo root as
  `Path(__file__).resolve().parent.parent`, which is `tools/` since the move (lines 80 and 40).
- 2026-11-12 22:58: issues #1132 to #1135 filed under the standing word and "add issues".
- 2026-11-12 23:03: 053 committed (`2a913ecdf`) after the hook suite passed.

- 2026-11-12 23:40: plans committed (`664f19c5b`), six worktrees made, executors launched.
- 2026-11-13 00:15: all six executors DONE; every branch passed the orchestrator's spec check.
- 2026-11-13 00:45: #1135 closed: trunk commit `97e9627cb` had fixed the nine failing tool tests
  (41 tests of the four modules pass at `4cafca4ab`).
- 2026-11-13 01:55: the maintainer authorized commit, push and merge on the orchestrator's
  recommendation, with co-op first for game runs.
- 2026-11-13 02:45: all six branches merged into the run branch; the trunk merged in.
- 2026-11-13 03:10: full verification green at `45ee5b5f9`.
- 2026-11-13 08:16: newest trunk (`807263ca2`) merged into the run branch (`db1c87d7c`).
- 2026-11-13 08:43: Fords smoke run passed on the branch build. The smoke covers the boot, patch
  application and one 120 s match; it does not reach the campaign code of items 047, 049 and 050.
- 2026-11-13 08:46: trunk fast-forwarded to `db1c87d7c`; taom-5c and taom-b4 told; the game folder
  holds the build of `db1c87d7c`.
- Agents of the run: 6 lanes, 3 refuters, 18 plan agents, 6 executors, 34 review agents: 67 in all,
  about 12.5 M subagent tokens (lanes 1.51, refuters 0.79, plans 3.34, executors 0.91, reviews 5.99,
  as the workflow results report them; the review figure is the sum of three workflows).
- Dead end: a graphify refresh in a linked worktree takes 7 to 10 minutes and one was killed; the
  plans fall back to `git grep` for the blast radius when `graphify_taom.py status` is not fresh.

## Next

Nothing is running. The maintainer's actions: pull the trunk; restart open Claude sessions (two skills
and two hooks changed); read FOR-MIKE.md; play the in-game checks listed there; remove the worktree
`E:\repos\taom-improve\2026-11-12\wt-run` and the branch `improve/2026-11-12-run` (local and remote)
when he no longer wants them. The run's scratch folder (`E:\repos\taom-improve\2026-11-12\scratch`:
agent scratch, workflow scripts, verification logs) is outside the repo and can be deleted.

## Next (history)

When the plans return: read each plan and its review, commit them with the index, make one worktree
per plan from that commit, execute (Sonnet batch 046, 048, 051; Opus batch 047, 049, 050), then the
spec check and review per branch, then merge each READY branch into the run branch.

## Blockers

None.

## Dead ends

- None yet.
