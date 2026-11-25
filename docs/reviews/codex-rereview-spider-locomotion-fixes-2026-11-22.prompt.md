# Codex re-review: the fixes for the spider locomotion review (#1541), 2026-11-22

You are the reviewer (see `.ai/roles/reviewer.md`). Review only: change no file, run no tool with
`--apply` or `-Apply`, write nothing to the game install, start no game, run no git command that
changes anything. Report defects with evidence (file, line, the input that fails).

## What to review

Your first pass (`docs/reviews/raw/codex-prereview-spider-locomotion-2026-11-22.md`, the findings at its
end) found four P1, one P2 and one P3 in the uncommitted work on `feat/spider-anim`. The fixes are in
the tree now. Verify each fix the way you found the defect: change one value in memory and run the
real code on it. Then look for what the fixes broke.

The account of each finding and its fix: `docs/reviews/rca-spider-locomotion-2026-11-22.md` (F1 to F8).

| Your finding | The fix | Where |
|---|---|---|
| P1-1 a wrong clip on an existing row passes the binder | `_add_rows` upserts through `_put`; `planned_problems` parses the planned files and compares every owned pair, the declarations and the movement rows | `tools/bind_spider_clips.py` |
| (the Tooling lens) a row inside an XML comment counted as a row | `ROW` matches a comment first, whole; `_mask` blanks comments for the set, rider-set, usage and declaration searches | `tools/bind_spider_clips.py` |
| P1-2 an exempt twin on a moving usage row passes | the pair is exempt only while its action sits on `direction="none"`, `turn_direction="none"` rows alone | `tools/verify_mount_assets.py` |
| P1-3 `pending` and `turns` exempt a gait action bound to those clips | `record_findings` fails a pace action whose clip is in either list | `tools/check_gait_stride.py` |
| P1-4 an interrupted bind leaves incompatible live files | every file is written to `<name>.spider-bind-tmp` first, then moved with `os.replace` in `FILES` order, live before snapshot; no journal (see the RCA, "Not fixed here": is that residue acceptable, and is the stated order really the one whose every prefix loads?) | `tools/bind_spider_clips.py` `main` |
| P2-5 explicit records validated after the save | an explicit row is judged in the plan loop, dry run included (`INVALID`): no duration, travel with `usage: false`, a planned sound point at or past (Duration - template BlendOutPeriod) / Duration | `tools/gen_elephant_anim_clips.ps1` |
| P3-6 a straight leg misses silently | `main` fails a build whose `tip_error_m` exceeds `TIP_TOLERANCE`; the error is measured per leg | `tools/spider_gait.py` |

Also changed in the same pass: `spider_gait.py` lost `--only`, imports `RZ`, `unit` and `rot_between`
from the elephant tools, and a twin's report row has no limits; `check_gait_stride.py` gained
`clip_problems` (both binders call it; `tools/bind_elephant_clips.py` delegates) and a finding for two
record files with one clip name; the C# picture name rule moved to `SeatTrialRules.PictureName`; four
stale comments in the live and snapshot XML were corrected and the binder's own comment in the movement
block changed.

## Questions

1. Does each fix hold against the mutation you used the first time, and against its neighbours: a
   DUPLICATE row for one action in one set with two clips (which does the upsert rewrite, and what does
   `planned_problems` say); a row whose attributes are in another order or on two lines (`ROW` needs
   `type` then `animation`); a comment that holds `-->` inside a CDATA or an unterminated comment; a
   rider set whose `act_spider_run_forward` row is commented out; a usage row with `direction` missing
   (the exemption treats it as moving: is failing closed right for every creature in `CREATURES`, the
   dire wolf and the elephant included)?
2. Can `--check` still print "bound" for a state the game would load differently? Compare what
   `planned_problems` reads with what the engine merges: several `<action_set id="as_human_warrior">`
   partial blocks exist in the live file, and a child set (`base_set`) can override a row.
3. `main`'s write: the temp files are made for all six targets before the first move. If the run dies
   after the temps exist and before any move, do stale `*.spider-bind-tmp` files in `ModuleData` harm a
   game start or the next run? If the backup copy fails for the second live file, what is on disk?
4. `gen_elephant_anim_clips.ps1`: does the plan-time check ever refuse a row the editor accepts (a
   template with BlendOutPeriod 0 and step points of -1; the idle with duration 4 and blend-out 0.8), or
   accept one it refuses? Is the elephant's own (non-explicit) path byte-for-byte what it was?
5. Did moving `clip_problems` or the picture name rule change behaviour for the elephant binder or the
   crew step?

## Output

P1, P2, P3 with file, line, the failing input and the evidence, then "verified fixed" or "not fixed"
for each row of the table above. Say UNVERIFIED where you could not run something, and what you did
not review.
