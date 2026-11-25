# RCA: spider locomotion tools and gates (deep-review, 2026-11-22)

## Top-line

`/deep-review` of the uncommitted #1541 work on `feat/spider-anim` (tier 1: live `LOTRLOME_Armory` data, clip
packages, and a harness step that reads agents in the mission tick; Codex at `xhigh` and eight lenses in two
waves) found no defect in the live action sets, the usage table, the clip records or the C# step. The engine lens
verified the change's main risk by a native read: a `direction="none"` row whose clip has no `quad_movement`
usage is safe at every pace. Codex found four P1 defects, all in the gates and the writer this work added: each
let a wrong state pass, or could leave one behind. Two lenses found the first of them independently. All four
are fixed in the same change, with a test for each case a reviewer broke. Two unattended custom battles had run
on the bound data before the review, with no fault; nothing they showed changes.

## Findings and root causes

| # | Sev | Finding | Category | Why missed | Preventive action |
|---|---|---|---|---|---|
| F1 | HIGH (Codex P1, Tooling) | `tools/bind_spider_clips.py` `_add_rows` asked only whether a row for a new action existed. A row that was there on the wrong clip (a trot action on the walk clip, a rider row on another rider clip) made the plan equal the file, so `--check` printed "bound" and exited 0. 8 rows of `as_spider` and 16 rider rows were covered that way. | A check of presence where the tool owns a value | The tool was written from the first run's need (the rows are missing: add them) and `--check` was "the plan changes nothing". The tests compared the output with `NEW` and `MOVEMENTS`, the tables that made it. | A row is upserted, and `planned_problems` parses the planned files and compares every pair the tool owns. `PlannedProblemTests` breaks each owned value alone; some expectations are literals. |
| F2 | HIGH (Codex P1) | `tools/verify_mount_assets.py` exempted an (action, clip) pair from the `quad_movement` check wherever the action sat. It collected action names from the movement block with no direction, so a twin's action on a `front` row was still exempt. The dire wolf's entry had the same hole. | An exemption wider than its reason | The exemption's comment stated the narrow case (a reference row) and the code keyed on the name. The existing test moved the clip to another action; nobody moved the action to another row. | The pair is exempt only while its action sits on `direction="none"`, `turn_direction="none"` rows alone; a row with no such attributes counts as moving. `test_an_exempt_pair_on_a_row_that_moves_fails`. |
| F3 | HIGH (Codex P1) | `tools/check_gait_stride.py` took the turn clips and a pending June clip out of the stride check by clip name. A gait action bound to one of them was then measured by nothing. | An exclusion keyed on the thing, not on its use | The lists were added to make the gate pass on two honest cases (a turn has no straight stride; the jump start waits for phase 2), with no thought for what else could carry those names. | `record_findings` fails a pace action whose clip is in either list. `test_a_pace_action_on_a_clip_the_stride_check_skips_is_a_finding`. |
| F4 | HIGH (Codex P1; the Tooling lens rated it LOW) | The binder wrote six files with six `write_bytes` calls. A death between two left the live files in a mixed state, and a death inside one could cut a file short. Codex showed the mixed state after the second write fails `verify_mount_assets.py` (old usage rows on the untagged idle). | A multi-file write with no order and no whole-file guarantee | "All six planned before the first write" was taken for safety. It guards against a bad plan, not against a stop. | Every file is written beside its target and moved in with `os.replace`, declarations first, then rows, then the usage table, live before snapshot: no file is ever cut short, and a second `--apply` finishes the job. `test_a_run_that_stops_between_two_files...`. A window between the second and third move remains: see "Not fixed here". |
| F5 | MEDIUM (Tooling) | The binder's row regex matched rows inside XML comments. A commented-out row counted as present, and a commented-out June row satisfied the rebind. The live file holds 483 commented-out rows. | Text matching on a file whose comments hold the same text | The fixtures had no comments. | A comment is matched first and whole; set and usage searches run on a copy with comments blanked. Three tests. |
| F6 | MEDIUM (Codex P2, Tooling, Design) | `tools/gen_elephant_anim_clips.ps1` judged an explicit row (the sound point rule, a missing limit) on the file it had just written. The script never overwrites a clip, so a refused record stayed in the live folder. The header and the feature doc both said "before it writes". | A check after the write it guards | The rule was added to `Get-Problem`, the function that compares a file with the plan, on the day the Kit refused four records. Nothing ran a bad row through a dry run. | An explicit row is judged in the plan loop, dry run included: no duration, travel with no usage, or a late sound point is INVALID and no file is made. Proved by a run of three bad rows into an empty folder (exit 1, 0 files). The script has no automated tests. |
| F7 | MEDIUM (Tooling) | `spider_gait.py --only <clip>` wrote `report.json` with only that clip, and a wrong name wrote an empty one with exit 0. The record writer then planned and verified fewer clips. | A convenience flag that rewrites a shared output | Added while tuning one gait, never used after. | The flag is gone: a full build takes about a second. |
| F8 | LOW (Codex P3, Tooling) | A straight leg has no bend plane: `solve_leg` left the tip where it was and reported no shortfall, and `main` failed a build only on `short_m`. No real leg is straight at rest. | A solver's blind case behind a gate on another number | The tests drew targets around the rest pose. | `main` fails a build whose tip ends more than 0.1 mm off its target; the error is measured per leg. |

Also fixed, LOW: the backward pace's `none` row plays the walk's twin, and three texts said each pace plays its own
gait's (corrected; the twin itself is #1550); four comments in the live XML described the June rows; the step's
`riders=11` against 12 fielded is the battle size cap of 400 on 413 soldiers (now in the doc); a doc comment and a
test still named a zoom that was removed; the stand-in world accepted a rider choice the real host refuses when
every mount is dead; one duplicate test; the trap sheet did not load for the four spider tools and said "keys / 30"
where the spider's masters hold 60 a second; `tools/README.md` had no row for the two new tools; five docs still
said 223 dwarf rider rows; the two binders shared a copy of the cache readiness check (one function now, in
`check_gait_stride.py`); the two trial steps shared a copy of the picture name rule (now `SeatTrialRules`).

## Root-cause pattern: a gate that checks less than it says

F1, F2, F3 and F5 are one fault. Each gate was built to pass on the state the work had just produced, and nobody
then asked what wrong state it would also pass. Each had a sentence above it that promised more than the code
below it did ("bound", "the pair is the key", "none escapes the stride check").

This has shipped before. [rca-elephant-gait-gate-2026-11-20.md](rca-elephant-gait-gate-2026-11-20.md) names "the
gate was built one defect at a time" and its F3 is "a gate that passes when it read nothing"; the lesson from it
("A gate fails on what it could not read") covers a gate with missing INPUT. These four are the next case along:
the input is all there, and the gate reads it too loosely. A repeat needs more than another lesson entry, so the
rule goes where a tool author meets it: `.claude/rules/data-scripts.md` now says that a gate or a `--check` ships
with one test for each value it owns, broken alone, and names the three shapes met here.

## Why each lens saw or missed these

- **Codex** found all four P1s, by mutating the live files in memory and running the real gates on the result.
  That is the method: a gate is reviewed by feeding it a wrong state.
- **Tooling** reproduced F1 and F5 on the fixtures and found F6 and F7. It rated F4 LOW (the write order is safe
  and a second run completes) and passed F2 ("keyed on the pair"), reading the exemption's key and not where the
  action may sit.
- **Data flow** traced every name end to end on the CORRECT data and found it connected. A trace of right data
  cannot show what a gate does with wrong data.
- **Engine** verified the mechanics the exemption rests on; the exemption's scope is a tooling question.
- **XML** ran every gate and read their PASS lines as evidence. They were true: the data was right.
- **Standards, Efficiency, Design, Completeness** ran in the second wave, after the findings. Completeness named
  the missing tests for exactly these paths, and that two binder tests compared output with the tables that made
  it. Design gave the shape of the F1 fix (an upsert, and a parse of the plan in place of a second layer).

## Not fixed here

- F4's remaining window, which Codex's second pass rated P1 again. Between the move of `action_sets.xml` and the
  move of `monster_usage_sets.xml` the live module holds new rows and the old usage table. Codex ran the asset
  verifier on every prefix of the six moves: 0, 0, 1, 0, 0, 0, 0. The failing state is unsafe for a reason the
  engine lens read: the stand pace's front slot is filled by its `none` row, the June row names `act_spider_idle`,
  that action now plays a clip with no usage, and the node dereferences the front slot's usage. No order of the
  three files avoids it: the new usage rows need the new bindings, and the new bindings break the old rows. The
  first version of the fix claimed "every prefix still loads"; the header now says what is true and tells the
  reader not to start the game on that state. The tool refuses while the game runs, `--check` reports the state,
  and a second `--apply` ends it. Two ways close the window, and neither is built: a marker file that makes
  `--check` and the next run say "an interrupted bind", or a `quad_movement` usage on `spider_idle`, which makes
  the middle state valid and also ends the mount asset check's warning for the idle rows (it needs a new record
  and one Kit start). FOLLOW-UP: no issue: a guard for a stop inside a few milliseconds is the maintainer's call
  (`.claude/rules/simplicity-criterion.md`); the commit body carries it as a known limitation.
- The reviewers' open engine points: the turn records' unit, the quick stops on a 4.0 s record, one action per
  movement row, the backward twin. FOLLOW-UP: #1550.
- The three "runs alone" rules of the command trial's crew, stamina and spider rows are three copies of one
  function. FOLLOW-UP: no issue: two of the three predate this change, and the copy is 12 lines; fold them when a
  fourth exclusive step arrives.
- `tools/gen_elephant_anim_clips.ps1` still has no automated test. FOLLOW-UP: #1550 (its explicit-row path grows
  there).

## The pass after the fixes

Two reviewers read the fix diff: Codex at `high` and one convergence pass with the Tooling lens.

Both reproduced the first pass's failing cases against the new code and found F1's original mutation, F2, F3,
F5, F6's three cases, F7 and F8's exit gate fixed. The elephant path was byte-identical: its gate, its binder
check, its record plan, and the mount asset check for the elephant, the mumak and the dire wolf.

They found six more points, fixed in a second round:

| # | Sev | Finding | Fix |
|---|---|---|---|
| R1 | HIGH (Codex P1) | F1 again, one layer out. The engine merges every block of one set id and lets a child set override its base. A second partial `as_human_warrior` block with one wrong rider row has no run row for the binder to find it by, and `--check` said "bound". Codex ran the game's own `MergeTwoXmls` on it: the wrong row wins. | `planned_problems` reads every set of the file: a row for an owned action in any other set must be on the owned clip. Three tests. |
| R2 | HIGH (Codex P1) | F4's window: see "Not fixed here". | The false header claim corrected; the window recorded as a known limitation. |
| R3 | MEDIUM (Codex P2) | A row that claims travel on a template with no usage passed the plan and failed after the save: the writer sets travel on a usage that is there and makes none. | Refused in the plan loop. Proved by a dry run (INVALID, exit 1, no file). |
| R4 | MEDIUM (Codex P2) | The duplicate-record finding looked only at clips with no file of their own name, so a second file with a canonical clip's name was missed. | `records_in` reads every record file of the folder by the clip's own name. One test. |
| R5 | MEDIUM (Tooling) | Moving the picture name rule to `SeatTrialRules` left two wrapper tests looking in the old files: the test module failed. | The tests read the new home and the two callers. |
| R6 | LOW (Tooling) | A backup that failed on the second file left the first file's temp file in the live folder, under a message that said nothing was changed. | The temp files are removed and the message says what is true. One test. |

R1 is the pattern of this file once more: the fix for F1 checked the sets the tool edits, and the engine reads
more sets than that. The rule in `.claude/rules/data-scripts.md` says to verify a result "with a parser"; the
sharper form is to verify it as its READER reads it, merged and inherited.

After the second round: 380 tests in the seven affected tool modules pass, the full tool suite ran 10,986 tests
with the two R5 failures as its only ones before their fix, the C# suite ran 45,073 tests with two failures that
this branch's base already had (the Long Defeat language seeding tests, fixed on trunk), and
`bind_spider_clips.py --check`, both stride gates, `bind_elephant_clips.py --check` and
`verify_mount_assets.py spider` pass on the live files.

A third Codex pass, on the second round only, found R1's original case, R3, R4, R5 and R6 fixed and accepted R2
as recorded. It raised four points:

- **P1, closed by saying less.** `--check` reads three files, and the game builds its action sets from every
  module's file, each through that module's stylesheet. Codex changed DualWield's stylesheet in memory to drop a
  spider row and `--check` still said "bound". The binder cannot own other modules' stylesheets, so its answer is
  now "the three files carry the binding", its header says what it does not read, and the limit is here. With the
  stylesheets as they are, the rows survive: Codex ran the game's own merge and transform on them.
  FOLLOW-UP: no issue: no tool here models the whole merge, and a stylesheet change has its own review.
- **P2, fixed:** a set whose base chain reaches `as_spider` through a middle set was held to the rider's clips. The
  whole chain is resolved now. One test.
- **P3, fixed:** `records_in` raised on a record file cut short; it names the file as a finding. One test.
- **P3, fixed:** one comment in the write loop still said every prefix loads.

The review stops here. The last P1 was closed by narrowing a claim, which leaves no new behaviour to re-review.

## Owed in game

A person's eye on the gaits (walk, trot and canter paces, a slow-down from a charge, a step back, a strafe, a turn
in place), a hard stop from a charge, and the spider in the party screen, the encyclopedia and a conversation.
The two custom battles of 2026-11-22 show the spiders standing, charging at 10 m/s and stopping, in stills.
