# RCA: the warg's canter and gallop, and the review of the fix (#1563, 2026-11-23)

## Top-line

Players on v2.0.41 reported that AI warg riders "walk everywhere". The wargs ran at full speed. Their legs did
not match it, because of three values in the warg's clip records in the unversioned `LOTRLOME_Armory`, in place
since the warg's absorption in August. Seven records were rewritten from a plan file on 2026-11-23, cooked by one
Modding Kit start and checked in game by the maintainer. No C# game code was wrong.

`/deep-review` then ran at tier 1 on the uncommitted fix (worktree `E:\repos\taom-warg-walk`, branch
`test/warg-gait-trial`, HEAD `63e7d82ba`): Data flow, Engine compatibility and Tooling in the first wave;
Efficiency, Completeness and Design in a second wave, because the Tooling lens found one HIGH. The XML lens was
not in scope (no XML changed) and the Standards lens was not run (the HIGH is in a Python tool). A Codex
pre-review at `ultra` ran beside them (prompt `docs/reviews/codex-prereview-warg-gaits-2026-11-23.prompt.md`,
raw log `docs/reviews/raw/codex-prereview-warg-gaits-2026-11-23.md`): no P1, two P2 and two P3. One convergence
pass by the Tooling lens then read the fixes. Every finding below was read in the code or run before it was
fixed.

## The defect that reached players

| # | Fault | Since | Fix |
|---|---|---|---|
| D1 | `warg_canter` and its four side records played the trot's masters; the pack's canter, `Warg_Slow_run`, was bound to no record | 2026-08-28 | The five records play `Warg_Slow_run` and the run's turning masters |
| D2 | `warg_forward_gallop_left_foot` claimed 8.5 m of travel a loop; its paws cover 3.92 m | 2026-08-28 | Travel 3.92 m, measured |
| D3 | Each pace's front record and its in-place twin carried one `Duration`, so the legs kept one cadence at every speed | 2026-08-28 | Each `Duration` is travel divided by the pace limit it plays at |

The values, the measure and the native reads are in
[lotrlome-warg-gait-changes.md](../reference/lotrlome-warg-gait-changes.md).

**Why missed.** The warg was the reference mount: the authoring guide says "when in doubt, do what the warg
does", so nobody measured it. `tools/check_gait_stride.py` judges only the creatures listed in it, and the warg
was never one. The record named its master by GUID, and the file names suggested another pairing: `warg_canter`
beside `Warg_Slow_run_geo.tpac` and `Warg_Trot_geo.tpac` looks bound to neither by name. This pattern has shipped
before: the elephant's and the mumak's cloned records carried their templates' `Duration` and limits (#1474,
`lessons/animation-skeleton.md`, "The mount movement node takes cadence and pace from the clip records"). That
lesson fixed the two creatures in the gate and did not ask which other mounts had never been measured.

**The investigation's own detour.** The report said "walk", and the first hour searched C# and XML for a change
of speed, then built a trial step that read a mount's channel-0 action to learn its gait. Channel 0 holds
`act_none` on a moving mount (23042 of 23254 reads, run `20261123-143843`), because the native movement node
plays the gaits. The feet were measured only after the maintainer said the gallop looks like a walk.

## Review findings

| # | Sev | Finding | Found by | Why missed when written | Fix and proof |
|---|---|---|---|---|---|
| F1 | HIGH | `plan()` read a row's optional fields with `r.get`, so a misspelled field (`travle`) was skipped, the record read `DONE` and the `warg-gaits` gate passed with that value never compared | Tooling | The plan had one author and was right; the tests wrote only correct rows | `_refuse_bad_rows`: an unknown or a missing field is refused by name before anything is planned; `PlanRowTests` |
| F2 | MED | Two rows for one clip: each is built from the file as it is, so the second write replaced the first, and neither the dry run nor `--apply` said so | Tooling | The same: no test wrote a plan a person could mistype | A clip named by two rows is refused; `test_a_clip_named_by_two_rows_is_refused_and_nothing_is_planned` |
| F3 | MED | On a machine without the Armory or without `xxhash` the gate ended in a traceback with exit 1. `tools/data_gates.py` reads 2 as NOT RUN and 1 as a finding | Tooling; the Codex sandbox hit the `xxhash` case | The gate row was tested for selection, never run without the install | `main` returns 2 with `NOT RUN:` for a missing clips folder or `xxhash`, and 1 with `refused:` for a plan it refuses; three tests |
| F4 | LOW | Two texts said the trial step reports gait clips, which it cannot see; the README row and a docstring described the old check | Data flow, Design | Written before the gait reading was removed | Reworded; the step's doc says a PASS never means the gaits are right |
| F5 | LOW | Guards with no test: a record with two usages, bytes after the usage, a step point of -0.5, equal limits, a reverted master or range | Tooling | Tests were written for the values the plan uses | Seven cases added to `GaitFieldTests` |
| F6 | LOW | `WargStepTests` sat below the `__main__` block, so running the file skipped it; the `Known = ...` assertion stopped before the warg row | Tooling, Data flow | An append to the end of the file | Moved; the assertion holds the whole line |
| F7 | LOW | Engine claims stated as fact without a read on v1.5.7: speed divided by scale, `MountSpeed` in metres a second, how the upper-body turn rows use `Duration` | Engine compatibility | Taken from `elephant.md`, which already marks the first as a v1.5.6 read | The ledger and `WargTrialRules` say which is read and which is not |
| F8 | LOW | The ledger argued that a stale dependency entry is harmless from records that have no entry, which proves a missing entry and not a wrong one | Engine compatibility | An argument by analogy | Replaced by a read of the cooked entries: the canter's hold the slow run, and its turn records hold byte for byte what the gallop's turn records hold |
| F9 | MED | The completion paperwork was missing: the step's doc, `## How to verify in game`, the release note, the follow-up issues, two lists of the trial's steps | Completeness | The ledger was taken for the whole of the docs | `warg-combat.md`, `release-process.md`, `unattended-game-runs.md`, `feature-map.md`, the game-run skill; issues #1565, #1566, #1567 |
| F10 | MED | `--check` read `cooked=True` when a master's keys changed under one GUID: record and stamp stay alike. The gate selects master files, so it could pass the one change that needs a new cook | Codex (P2), with a changed key in memory | The cook was defined by the record alone; #1566 had filed it as low | `cook_fault` also fails an entry older than its master's file; `test_a_master_written_after_the_cook_fails_though_the_record_did_not_change` |
| F11 | MED | A change to a cache entry alone selected no gate | Codex (P2) | The globs listed what the tool writes, not what its check reads | `WARG_GLOBS` takes the Armory's cache entries, and only the warg's animation packages, no longer its mesh |
| F12 | LOW | The plan tests held the turn records' masters and not their key ranges, and accepted a step point list of any length | Codex (P3), 4 of 45 single changes survived | The ranges were checked only as "inside the master's keys" | Literal ranges; four step points on the canter and none elsewhere |
| F13 | LOW | The ledger and the lesson paired the baseline speeds wrongly (the runs gave warg 9.4 with horse 11.1, and 9.8 with 10.3) | Codex (P3) | Two lists joined by "against" | Each pair stated with its run |
| F14 | MED | The fix of F3 caught every `ImportError` as "no xxhash, NOT RUN", so an import fault of the tool itself would have read as an environment gap | Convergence pass, with a broken import in a scratch copy | The test mocked the one error it expected | Only `ModuleNotFoundError` named `xxhash` is NOT RUN; a test with two other import faults |
| F15 | LOW | A plan that is not a list of objects, is not JSON, is missing or is empty ended in a traceback, or (empty) passed the gate having compared nothing | Convergence pass | F1's fix judged rows, not the plan | `refused:` and exit 1 for each; five cases |
| F16 | LOW | Two new tests could not fail: the two-usages case was refused by another guard, and the NaN test's stand-in returned NaN on every reading, which also fails as a speed of 0 | Convergence pass, by mutation | Each test was run green and never against the code it guards | A usage count that differs from the usages written, a non-empty list, and one NaN reading among good ones (`OneReadingThatIsNoNumber_AmongGoodOnes_StillFails`) |

The Efficiency lens found nothing to apply. Codex's disassembly of the v1.5.7 DLL supports the same two facts as
the Engine compatibility lens's read, adds that the comparisons at the pace limits are strict (0x74E890), and
found all 109 installed quadruped records in the layout the tool guards. Codex could not run the live check: its
sandbox has no `xxhash`, which is how F3 was first seen.

The Codex run overlapped the first round of fixes and read them: its log holds the diff of `_refuse_bad_rows`
and `PlanRowTests` (raw log lines 17425 to 17689) and its mutation runs against them, so it stands as the Codex
pass on the fix of F1. The fixes of F10 to F16 came after it; each is MEDIUM or LOW and is held by its test. The Engine compatibility lens confirmed the two facts the plan rests
on with a native read: the loop time runs from the "none" clip's `Duration` at the lower limit to the front
clip's at the upper (0x749B10), and a `none/none` row fills every slot no earlier row holds (0x621E40), so the
in-place twins are the warg's "none" clips.

## Root-cause pattern

D1 to D3 and F1 to F3 share one cause: a thing was trusted because it had worked for its first user. The warg's
records worked well enough to ship, so the warg became the reference. The plan tool's `--check` worked for the
plan its author wrote, so it became a gate. A gate reads input that a later person types and runs on machines
its author does not have, and neither was tried.

## Why each agent missed or caught these

- **Data flow** traced each value of the plan to the bytes and found no gap, because every value in the plan
  flows. F1 is a value that is not in the plan. The lens has no rule for input the code does not read.
- **Engine compatibility** does not read tools.
- **Tooling** caught F1 to F3 with a probe in a temp folder: a misspelled key, a doubled row, a missing module.
- **Design** proposed the shape of F1's fix (one tuple of accepted fields that the refusal and the call both
  read) and did not find F1.
- **The author's own tests** broke each value of a correct row and never wrote a wrong row.
- **Codex** caught F10 to F13 by changing one thing at a time against the real files, and the convergence pass
  caught F14 to F16 the same way. Three of the seven (F14, F15, F16) were faults in the first round's fixes and
  tests: a fix is new code, and it got the review the first code got only because the skill requires the pass.

## Lessons recorded

- `docs/reviews/lessons/animation-skeleton.md`, "The wargs walk" was a cadence defect: measure the feet before
  reading code, and take a record's master from its GUID.
- `docs/reviews/lessons/build-tooling-workflow.md`, a check that reads a hand-typed plan refuses what it does not
  know.

## Deferred

- FOLLOW-UP #1565: the walk and trot records, and the warg in `tools/check_gait_stride.py`.
- FOLLOW-UP #1566: the low findings on `tools/repoint_anim_clip.py` (a summary when a middle row of `--apply`
  fails, `--under` held inside `Assets`, a NaN source, the 0x58C500 citation, the reader of the stored record,
  a cook check that reads the cooked keys and not the stamp and the dates). Two of its first items were fixed
  here: the check names a missing entry, and it holds the entry against its master's date.
- FOLLOW-UP #1567: one "runs with enter only" rule for the crew, spider and warg steps of the command trial.
- Not applied, no issue: passing the parsed clip into `cooked()` and caching masters in `plan()` (Efficiency:
  microseconds on seven rows, at the cost of a second calling shape).
- Owed in game, on #1563: a turn at speed, the change from canter to gallop, a fell warg; and an Armory package
  for each release channel.
