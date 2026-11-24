# RCA: the review of the warg's walk and trot, the dire wolf's gaits and the rider's turn clips (#1565, 2026-11-24)

`/deep-review` at tier 1 (live Armory data): a Codex pre-review at `ultra` (prompt
`docs/reviews/codex-prereview-warg-walk-trot-2026-11-24.prompt.md`, raw log
`docs/reviews/raw/codex-prereview-warg-walk-trot-2026-11-24.md`) and four lenses in one wave: Data flow, Engine
compatibility, XML and Tooling. No finding was critical or high (Codex: no P1), so no second wave ran, and the
Standards, Efficiency, Completeness and Design lenses were skipped by tier. This file exists because one finding
repeats a cause that the #1563 RCA already recorded (F6 below, against its F11), and because the work itself
found three defects that #1563 left behind (D1 to D3). Every finding was read in the code or run before it was
fixed.

## What #1563 left behind

| # | Defect | How it was found | Root cause |
|---|---|---|---|
| D1 | `tools/gen_dire_wolf_anim_clips.ps1 -Verify` named two dire wolf records that nobody had changed | Run by hand while the walk's twin was planned | The script read the warg's travel to plan the wolf's strafe travel, #1563 changed that travel, and no gate or test ran `-Verify` |
| D2 | A rider in a gallop turn moved out of step with the warg | The maintainer asked for "anything else that uses the wargs animations"; a read of the rider records' flags | The rider's two gallop turn clips had no `synch_with_horse` and kept step only by an equal `Duration` (0.58 s), which #1563 made variable on the warg |
| D3 | The dire wolf's four gaits claimed 26% to 57% less ground than its paws cover | The same question; a measure of the installed masters | Its records were clones of the warg's with the pack's typed travel, never measured (#1565 item 3) |

## Findings of the review

| # | Sev | Finding | Found by | Why missed | Fix |
|---|---|---|---|---|---|
| F1 | MED | `verify_mount_assets.py dire_wolf` failed on the new `dire_wolf_walk_stand` and printed "do NOT battle-test" | Data flow, XML and Engine lenses, Codex (P2) | The tool exempts in-place twins by exact pair, and the author searched for users of the records, not for lists of their kind. No gate ran the tool for the wolf | The pair is in `untagged_ok`; the `dire-wolf-body` row of `tools/data_gates.py`; `test_the_dire_wolf_exempts_each_in_place_twin_its_gait_plan_binds` |
| F2 | MED | The bind tool wrote from bytes read earlier: another session's edit of the live file between read and write was lost, from the backup too | Codex (P2), by running the write loop | Modelled on the older bind tools; the tests ran alone | The file is read again and a difference refused before the first write; the backup opens in mode `x`; a failed write puts the file back |
| F3 | MED | `bind_warg_clips.py --check` exited 0 with no snapshot file | Tooling lens, Codex (P2) | A missing file was skipped for both paths, and only the live one may be absent | A missing snapshot is a refusal in every mode |
| F4 | MED | The reinstall recipe bound the twins before `as_dire_wolf` existed, and the warg ledger's own order could not finish | Data flow lens, Codex (P2) | The bind step was written into the recipe where the cook is, not where its inputs exist | The bind is the last part of step 7 of the wolf's replay; the warg ledger points there |
| F5 | LOW | A write of a new clip that failed left a partial file, and the next run refused it | Tooling lens, Codex (P2) | Only the read-back had a cleanup | One cleanup covers the write and the read-back; `test_a_write_that_fails_leaves_no_file_of_the_new_clip` |
| F6 | LOW | The gait gates' globs left out `creature_fit.py` and `_gamedir.py`, which the check imports | Tooling lens, Codex (P3) | A repeat of #1563's F11: the globs listed the tool, not what its command runs | `GAIT_TOOL_GLOBS`; a selection test |
| F7 | LOW | The wolf's canter played keys 1 to 16 of a 0 to 16 loop: the paws jumped up to 0.39 m at the seam | Codex (P3) | Kept "as the pack made it"; the stride measure does not see a seam | The three canter records play 0 to 16; the installed test holds each loop closed |
| F8 | LOW | No test tied the frame-0 permission to the master that is played from 0 | Tooling lens, by mutation | The author's mutation made the permission always true, not true for another master | `test_frame_0_of_one_master_does_not_open_frame_0_of_another` |
| F9 | LOW | A failure in the middle of `--apply` ended in a traceback | Tooling lens | `main` caught refusals of the plan only | `refused:` and exit 1, with what is left on disk |
| F10 | LOW | `clone()` accepted a template keyed to a clip or generated from two | Tooling lens | Both templates in use are plain twins | Refused; a test |
| F11 | LOW | The ledger cited the tick at a v1.5.3 address and said the sync flags were not read on v1.5.7 | Engine lens, which read both on v1.5.7 (0x659350) | The author quoted the system map without its version table | The address and the read are corrected |
| F12 | LOW | The generator held the keys of a record the gait plan names, so the canter's new range would have failed `-Verify` | Data flow lens, then the F7 fix | `$planOwned` skipped two of the four fields the plan states | It skips the keys too; the name, the flags and the master stay the script's |

What the Engine lens settled, read on the installed v1.5.7 DLL: the loop-time rule the plans rest on (0x749B10),
the weight rule (0x74E9C0), that no movement, AI or rider code reads the travel, and that a pace's "none" row on
an untagged in-place clip is vanilla's own shape (`as_horse` binds `act_horse_forward_walk_stand` to
`gait_walkfast`). Not settled: that the node's speed is divided by agent scale (read on v1.5.6 only).

## Root-cause pattern

D1 to D3 and F1 share one cause: a change was scoped by who PLAYS a record, and the dependants were elsewhere.
Records made from it (the wolf's clones), clips timed beside it (the rider's), tools that derive from it (the
generator) and lists that name its kind (the exemption list) are each a different search. The lessons entries
below make them one list.

## Why each reviewer missed what another found

- **The author's own gates** ran the plan checks and the bind check, and not the one tool whose list F1 is in.
- **Data flow** traced every chain and did not run the write loop under a second writer (F2).
- **Tooling** found F3, F5 and F8 by mutation and did not decode the masters (F7).
- **Codex** could not run the native helper or the tests that need `xxhash` and `numpy`, so the engine claims
  stayed unverified there and came from the Engine lens.

## Not applied

- Pinning the wolf's step points in its plan (Data flow F5): a re-made record would differ in when its footsteps
  sound and in nothing else, and the pin is four lists of 255ths. Stated in the wolf's ledger.
- Tighter planted-key counts for the wolf's gallop (Tooling F5): the gallop's stance is two or three keys in the
  master itself. The test now holds the loop closed and at least two planted keys per paw.

## Follow-ups

- FOLLOW-UP #1565: the warg in `tools/check_gait_stride.py` (item 2 of the issue).
- FOLLOW-UP #1588: a release check that a channel's Armory package and its `action_sets.xml` ship together (XML
  lens). Today the rule is prose in `docs/reference/release-process.md`.

Lessons: [animation-skeleton.md](lessons/animation-skeleton.md) (two entries of 2026-11-24) and
[build-tooling-workflow.md](lessons/build-tooling-workflow.md) (two entries of 2026-11-24).
