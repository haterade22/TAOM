# Codex re-review, second round: the spider locomotion fixes (#1541), 2026-11-22

You are the reviewer (see `.ai/roles/reviewer.md`). Review only: change no file, run no tool with
`--apply` or `-Apply`, write nothing to the game install, start no game, run no git command that
changes anything. Report defects with evidence (file, line, the input that fails).

## What to review

Your second pass (`docs/reviews/raw/codex-rereview-spider-locomotion-fixes-2026-11-22.md`, the findings
at its end) found two P1 and two P2. This pass is narrow: the four fixes below and nothing else. Use
the method of your earlier passes: change one value in memory, run the real code on it.
`docs/reviews/rca-spider-locomotion-2026-11-22.md`, "The pass after the fixes", rows R1 to R6, is the
account.

| Your finding | The fix | Where |
|---|---|---|
| P1 a second partial `as_human_warrior` block overrides an owned rider row and `--check` passes | `planned_problems` now reads every `action_set` of the file: in any set other than `as_spider` and the rider sets it found, a row for an owned action must be on the owned clip (`WANT` for a set whose `base_set` is `as_spider`, `RIDER_WANT` for any other) | `tools/bind_spider_clips.py` `planned_problems`, the loop over `sets` at its end |
| P1 the interruption window | NOT closed, on purpose. The header no longer claims every prefix loads; it names the unsafe state and says not to start the game on it. The RCA records it under "Not fixed here" as the maintainer's decision, with the two ways to close it | `tools/bind_spider_clips.py` header; the RCA |
| P2 travel on a template with no usage passes the plan | the template cache holds `HasUsage`; an explicit row with travel on such a template is `INVALID` in the plan loop | `tools/gen_elephant_anim_clips.ps1` |
| P2 duplicate records missed for a clip with its own file | `records_in(folder, clips)` reads every `*_anm.tpac` of the folder by the clip's own name and reports any name held by two files | `tools/check_gait_stride.py` |

Also changed in this round: two tests in `tools/tests/test_run_command_trial.py` read the picture name
rule's new home; a backup that fails part way removes the temp files it made (`made` in `main`).

## Questions

1. The strict rule in `planned_problems`: does it now REFUSE a legitimate file? Run it on the live
   `action_sets.xml` (it must return no problem). Is there a set in the live file, other than
   `as_spider` and the two rider sets, that legitimately carries a row for one of the eight new actions
   on another clip (a female set, a race set with no `base_set`, a map set)? Does a set whose
   `base_set` chain reaches `as_spider` through a middle set escape (only a direct `base_set="as_spider"`
   selects `WANT`)? Does an owned action in a set with `base_set="as_spider"` that the tool does not own
   (`as_spider_map`) trip it wrongly?
2. Can `--check` still print "bound" for a state where the effective binding of an owned row is wrong?
   Think of the XSLT step: the Armory's `action_sets.xslt` and DualWield's run after the merge.
3. `records_in`: a record file whose internal name differs from its file name while another file has
   that file name's clip; an unreadable or truncated `_anm.tpac` (does `clip_name` raise, and does the
   elephant gate then die where it used to pass); a clip in `clips` with no record at all.
4. The ps1 `HasUsage` test uses `Get-Loop`, which returns the usage's LoopDisplacement or `$null`. A
   template whose usage has LoopDisplacement 0 (`elephant_stand_for_movement_data`): is `HasUsage` true
   for it (`$null -ne 0`)?
5. The header's statement about the unsafe state: is it accurate as written?

## Output

P1, P2, P3 with file, line, the failing input and the evidence, then "verified fixed", "not fixed" or
"accepted as recorded" for each row of the table. Say UNVERIFIED where you could not run something.
