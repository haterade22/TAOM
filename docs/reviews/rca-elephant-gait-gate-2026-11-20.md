# RCA: elephant and mumak gait gate and clip records (deep-review, 2026-11-20)

## Top-line

`/deep-review` of the uncommitted #1474 work on `feat/elephant-anim` (tier 1: live `LOTRLOME_Armory` data and
clip packages; seven lenses in two waves; Codex skipped, out of usage since 2026-11-15) found no defect in the
live action sets, the monster file or the binding tool. It found one HIGH gap in the gate this work added, one
record defect that a player would have met on the war elephant, and a list of smaller gate and doc faults. All
of them are fixed in the same change except where a follow-up issue is named. Mike had judged the mumak's walk,
attack and death in game before the review; nothing he judged changes.

## Findings and root causes

| # | Sev | Finding | Category | Why missed | Preventive action |
|---|---|---|---|---|---|
| F1 | HIGH (Tooling, Data flow) | The standing-pose check in `tools/check_gait_stride.py` measured the master and never read the clip's cache entry. With the master grounded by `--root-z` and the Kit not yet started, the gate passed while the game still played the floating keys. No gate covered the cache state of the 17 clips on `adod_elephant_geo.tpac`. | A check that reads the wrong layer | The check was written on 2026-11-19 for one question (is the pose on the ground in the file), in the same tool whose header says the game plays the cooked entry. The trap was already on the creature trap sheet ("Cooked clip keys"). | `stand_findings` compares the entry of every bound clip on the skeleton's package with that package. Test: `StandFindingsTests.test_a_master_written_after_its_clips_were_cooked_is_a_finding`. |
| F2 | MEDIUM (Engine) | The two trot records were clones of the walk record, limits included (0.2 to 2.5). The node steps down only below the current pace's lower limit, so a beast that once passed 2.5 m/s trotted down to 0.2 m/s. The war elephant passes 2.5 in every charge. | A cloned value nobody judged | The generator's header gave a reason for the clone ("the walk's limits are the ones the trot action has run with"). In ADOD's set the trot pace played the walk clip, so its limits could not be seen. The node's step-down rule was known from the backward-walk fix and was not applied to the other paces. | The trot takes ADOD's own trot limits, 2.3 to 4.5 (`$LIMITS_FROM`). The gate fails a faster pace whose lower limit is not above the walk's. Test: `PaceTableTests.test_a_faster_pace_whose_minimum_is_not_above_the_walks_is_a_finding`. |
| F3 | MEDIUM (Tooling) | The pace and stand checks passed when they read nothing: a missing stand record hit a `continue`, a renamed stand action gave an empty loop, a missing backward or strafe record was skipped. Nothing printed what was read. | A gate that can pass on absent input | A test pinned the silent pass as correct. | The gate names its sets and actions (`"sets"`, `"pace_actions"`); a missing set, row or record is a finding; one line prints the counts. |
| F4 | MEDIUM (Tooling, Data flow) | `tools/data_gates.py` did not select the elephant gates when an elephant `.tpac` or one of the three writer tools changed. | A trigger that misses its inputs | The rows were written from the files the binding tool edits, not from the files the gates read. | `ELEPHANT_GLOBS`; `test_the_action_sets_the_mumak_monster_and_the_bind_tool_select_the_elephant_gates`. |
| F5 | MEDIUM (Tooling) | `set_root_z` moved the keys again on every run when the reference key came before the first moved key. The documented call did not do that. | An argument combination outside the tested path | The idempotence test used one combination. | The function refuses that combination; the two CLI flags that could reach it (`--from-key`, `--item`) are gone. |
| F6 | MEDIUM (Tooling) | The in-place limit rewrite in `tools/gen_elephant_anim_clips.ps1` overwrote a live record with no backup. | A writer without its backup | The 2026-11-19 run had a hand-made backup folder, so the gap did not show. | The rewrite copies the record to `.bak-limits-<stamp>` first. |
| F7 | LOW to MEDIUM (Engine, Completeness) | Doc text stated engine behaviour wrongly or against itself: the strafe threshold and "whatever the speed", the turn product for the war elephant, the gallop's `Duration`, a banner that said the clips still waited for a Kit start, the disproved "speed over LoopDisplacement" rule in two places. | Text written before the read was complete | Each sentence was true of an earlier state or an earlier belief and was not re-read after the next finding. | Corrected in `docs/features/elephant.md`, `tools/check_gait_stride.py`, `tools/gen_elephant_anim_clips.ps1` and the lesson. |
| F8 | MEDIUM (Completeness) | The retargeted motion had no row in the provenance register, the snapshot README had no restore steps for this work, and the two follow-ups had no issue. | Completion duties left for "after the look in game" | The work ran as a live session for two days, and the docs followed each fix one step behind. | A provenance paragraph (the maintainer decided on 2026-11-20 against a register row), an APPLIED EDIT section, #1504 and #1505. |

Also fixed, LOW: `cache_state` compares an entry with the newer of its record and its master (the Kit cooked two
entries again after a limits-only rewrite, so a record change does need a Kit start); the strafe `Duration` check
has an upper bound; `set_rows` no longer files rows under a self-closing set; the gate reads only `as_elephant`
and `as_mumakil`; `bind_elephant_clips.py` takes its strafe actions and prefixes from the gate and finds a set
without scanning the file 20 times; one copy of the segment-store code in `elephant_write_masters.py`.

## Root-cause pattern: the gate was built one defect at a time

F1, F3 and F4 are one fault. Each check went into `main()` on the day its defect was found, read exactly what
that defect needed, and skipped everything else with a `continue`. Nothing stated what the gate expects to find,
so nothing could notice that it was missing. The structure that removes the class: the config names the sets and
the actions, one function reads every named row and returns findings, and absence is a finding like any other.

F2 and F7 are a second one. The engine rule was read in three sittings (the backward walk, the strafe, then the
review), and after each one only the text and the records for that defect were corrected. A read that changes
the rule is a reason to re-check every record and every sentence the old rule had touched.

## Why each lens saw or missed these

- **Tooling** found F1, F3 to F6 by putting each of the three original defects back on a copy of the install and
  then five more variants; that method is the reason it found the silent passes.
- **Data flow** found F1 independently and rated it MEDIUM; it traced every chain in the live data and found none
  broken, so the fault was in the gate and not in what shipped.
- **Engine compatibility** found F2 and F7 by decompiling the node again, including the usage-set loader, which
  the author had not read.
- **XML** found the live files byte-faithful and equal to the snapshot. It could not find F2: the limits are in
  binary records, not in XML.
- **Efficiency, Completeness, Design** ran as the second wave. Design proposed the structure that fixes the
  pattern; Completeness found F8.
- **Codex** did not run (no usage). Its pass is owed before merge when usage is back.

## Not fixed here

- FOLLOW-UP #1504: the fast-pace table (trot, canter and gallop cadence, the gallop's lower limit under the
  canter's, the idle on the walk's "none" row, and whether `verify_mount_assets.py` is too strict for a "none" row).
- FOLLOW-UP #1505: TAOM's own clips for the ADOD ranges still bound, the stop clip, the turn clips, the head-turn
  overlay rows, and the check of the rider's strafe record.
- FOLLOW-UP no issue: `elephant_write_masters.py --apply` keeps its own process guard and not
  `_gamedir.game_or_kit_running`; the guard predates this change and the tool is run by hand with the game closed.
- FOLLOW-UP no issue: no test drives `main()` of the gate or of the binding tool; the review's copy-and-break
  variants are in the session scratchpad and are not a repo test.
- FOLLOW-UP no issue: the cache comparison is a comparison of file dates, and the Kit sometimes saves a package
  again without cooking it (`elephant_stand_2_geo.tpac` on 2026-11-19, 16 header bytes). If it does that to
  `adod_elephant_geo.tpac`, the gate goes red on 14 clips and a Kit start does not clear it. Settled at the Kit
  start of 2026-11-20, 15:33: the Kit cooked the two trot entries, whose records a tool had rewritten, and left
  the entries of `elephant_canter_right` (2026-06-06) and `elephant_stand_2` (2026-06-14) as they were, although
  their files had been saved again on 2026-11-19 with the same keys and record fields (by all signs the Kit's
  own save). So such a save brings no new entry, and a red that comes from one is a false red: compare the
  file's bytes with its backup before acting on it.
- FOLLOW-UP no issue: a malformed clip record or an unreadable skeleton package ends the gate in a traceback. The
  exit code is 1, so the state is red, but the message is not a finding.

## The pass after the fixes

One reviewer re-read the fix diff and ran 15 broken copies of the install. It confirmed five of the six changes
and found that a row outside the pace rows still passed when its clip had no record in the clip folder, so that
clip's cache entry was never compared. `record_findings` now reports every such row once per clip
(`test_any_row_of_a_named_set_whose_clip_has_no_record_is_a_finding`).

## Owed in game

The stand after a stop on the lowered root; a beast that slows from a charge to a walk; the war elephant in every
gait, in its attack, death and stand; a turn in place. #1474 carries the steps.
