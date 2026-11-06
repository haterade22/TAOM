# 1.4.8 port programme: planning progress

The running record of the 2026-11-04 planning session. Update it as each stage finishes. All times are US Central.

| Stage | Result | Status |
|-------|--------|--------|
| Classify 1,114 commits, nine deep-dive lenses | `data/classified.jsonl`, `deepdive/` | Done |
| Build baselines of the origin tip and the #697 merge | 9154 tests, 9151 passed, 3 skipped, 0 failed on the merge | Done |
| Verify claims, make decisions | `verify/`, decisions D1 to D24 | Done |
| Write 44 plans | All 44 on disk, including plan 136 | Done |
| Cold-review each plan | 44 reviewed: 44 READY-WITH-FIXES, 0 NOT-READY. Review fixes are in the plans | Done |
| Three cross-plan consistency checks | `verify/X-consistency-*.md`: about 60 per-plan fixes, two HIGH | Done |
| Replace the `git checkout` form that stalls on WP103's prompt | 171 commands in 27 plans; replacement tested in a scratch clone | Done |
| Apply the other consistency fixes (37 plans, 81 items) | Run `wf_8d7b8719-f30`: applied after checking each one; five items partly refuted with evidence | Done |
| Plan 145 for the 62 commits after the pin | 42 ported or partly ported, 20 skipped; reviewed READY-WITH-FIXES | Done |
| Closure checks of those fixes | `verify/X-closure-*.md`: 119 closed, 35 open | Done |
| Apply the open closure items | Applied by hand from the reports' exact fixes; not re-reviewed | Done |
| Final checks, README index | 45 plans, 652 owned commits covered, dependency order holds, no player paths or Steam ids | Done |

Nothing is committed. Plans and this folder are untracked until WP101 Part C commits them.

## Usage limit on 2026-11-04

The writing run started at 12:26 and hit the account's weekly limit around 16:50 (the limit resets on Nov 6 at 11:00).
38 of its 83 agents had finished; 45 failed with the limit error, using 35.2 million subagent tokens over 4.4 hours.

- Seven writers cut off by the limit (130, 132, 137, 139, 141, 142, 144) had already written files that reach their
  closing sections. Plan 136 had no file. Those seven get no second writer: their review also checks for truncation.
- Reviewers that failed may have left partial in-place edits; their replacement reviews check for that too.
- A first resume at 17:23 was stopped within two minutes, before any plan changed: it restarted writers that had
  already finished, because a resume reuses finished work only up to the first agent that has to re-run.
- The maintainer switched accounts, and a new run (`wf_e3f7758c-23b`) finishes the job: it writes plan 136, reviews the
  other 42 and runs the checks.

## Rule changed mid-run

`plans/README.md` belongs to the integrator. Plans written before this rule told executors to edit their own row.
Each reviewer removes that, except in plan 101; plans 105 and 113 were already reviewed and get checked by hand.

## 1.5.x moved during planning

Reviewers found `origin/bannerlord-1.5.x` past the `e45f6df2c` pin. The range `e45f6df2c..4d1a9105a` holds 62 non-merge
commits, and some fix code this programme ports. Plan 145 classifies each and ports what qualifies, after the other
plans. Anything newer than `4d1a9105a` is recorded in the README as not classified.

## Overnight run (D25)

The maintainer asked for execution to proceed overnight. WP101 runs on the `v2.0.26` Armory with a provisional data
baseline; later WPs land on local `port148/overnight`. See D25 in `data/brief-workflow3-decisions.md`.

## Overnight result (2026-11-05)

Nineteen WPs merged on `port148/overnight`: 104 to 114, 117 to 120, 123 to 126 (108 PARTIAL). Trunk unmoved, nothing pushed, nothing run in
the game. Per-WP results, deferred items and in-game checks: [`OVERNIGHT-REPORT.md`](OVERNIGHT-REPORT.md). D25's holds applied at that point.

## After the overnight result (2026-11-05)

D26, D27 and D29 released most of those holds. `port148/overnight` went on to 40 `merge WP` subjects on its first-parent history
since `1ab48c21d`. Still held: 102, 103, 116, 138, 142 and 143 whole, and WP108 Step B4, WP127 Parts C and M and WP145 Parts E to G.
The status column of [`plans/README.md`](../../README.md) is the record, and nothing has been pushed or run in the game.

## Integrated review 2 and tip verification (2026-11-05)

Integrated review 2 and its fix branch merged as `6ac230b05`, record `30868fc76`. Debug and Release: 13474 total, 13466 passed, 8 skipped,
0 failed. Results and the symptom map: [`OVERNIGHT-REPORT.md`](OVERNIGHT-REPORT.md). What remains needs the maintainer: the holds above,
the public Armory, the in-game test and the merge to trunk.

## State at the end of planning

Planning is finished; execution starts with WP101. No plan has been executed: conflict lists come from simulation against
`origin/bannerlord-1.4.5` (with the #697 merge for WP101), and nothing a plan adds has been compiled on a merged base. Each
plan's prechecks and STOP conditions cover that gap. Work no WP takes is listed under "Owed after the programme" in
`plans/README.md`.

`CHANGELOG.md` is deliberately untouched during planning: WP101's Step C1 stops if a file it merges has uncommitted
edits in the main checkout, and WP101 Part C writes the programme's changelog entry itself.
