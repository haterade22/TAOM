# RCA: dire wolf resize and seat (#1081, 2026-11-06)

## Top-line

`/deep-review` at tier 1 on the uncommitted #1081 resize work on `bannerlord-1.5.x` (HEAD `97d8cb72b` when the review
started): the rig rebuilt at warg size, the animation masters re-exported, the skeleton and the Monster resized, and
the live Armory files those tools wrote. Reviewers: Codex at `ultra`
(`docs/reviews/raw/codex-prereview-dire-wolf-resize-2026-11-06.md`), Lens 5 Data flow, Lens 2 Engine compatibility,
Lens 7 XML and the Tooling lens. No CRITICAL or HIGH, so no second wave. After the review, Mike's first ride showed the
rider over the wolf's hips; the engine lens traced the seat, the rig was rebuilt a second time (2.08, moved 0.52 toward
the tail), and one more Tooling lens reviewed that change. Every finding below was fixed except the deferred items.
Nothing after the second rebuild has run in game.

## Findings

| # | Sev | Finding | Found by | Fix and proof |
|---|---|---|---|---|
| F1 | MED | The clip generator wrote each gait clip's `LoopDisplacement` as the pack's times `RIG_SCALE` times the Monster's size, on the belief that the engine does not scale it. Native v1.5.6 compares the clip's travel with the agent's velocity divided by its agent scale (`FUN_1805f7580`), so the size counted twice and every stride was 10% long. | Lens 2 (native read); Lens 5 flagged it; Codex left it UNVERIFIED | Travel is the pack's times `RIG_SCALE` only; the 10 gait clips regenerated. Lesson in `animation-skeleton.md`. |
| F2 | MED | The rider sat over the wolf's hips. The plan was a new `rider_sit_bone`, from the doc line "the rider is seated at this bone". Native seats the rider where the usage's rider clip puts it in model space (`FUN_1806402f0`, `FUN_18063e700`); the sit bone only sets what the rider follows. The wolf uses the warg's usage, and its saddle was about 0.46 model units ahead of and 0.25 below the warg's. | Mike's ride; Lens 2 (native read) | Rig at 2.08 and moved 0.52 toward the tail in the meshes, the skeleton root, the masters and the body capsule; `taom_body_length` 115 keeps the battle size. The prep gate measures both seats: wolf y -0.102, z 1.52 against the warg's y -0.108, z 1.51. Four docs corrected; trap row "Mount size, seat". In-game check owed. |
| F3 | MED | No gate compared a master's frame count with the pack master's; one clip ends on the last frame of its master, so a one-frame-short import would point past the end. | Tooling | `Get-MasterFrameProblem` in `gen_dire_wolf_anim_clips.ps1`, three harness cases. |
| F4 | MED | `apply_dire_wolf_skeleton.py --masters-from` still copied the pack's masters, which are pack size, so a replay through it installs the wrong size. | Tooling, Lens 5 | The copy mode is removed; `test_the_pack_master_copy_is_gone`. |
| F5 | MED | No test made `verify_segment` refuse, so a guard that never fires would pass. | Tooling | `VerifySegmentTests`: another segment changed, and a wrong payload, are refused. |
| F6 | MED | Nothing held the live Monster file to the writer's recipe; the live file was hand-edited twice. | Tooling, Lens 7, Lens 5 | `LiveRecipeParityTests.test_the_live_monster_is_the_recipe`; the live file is now written from `monster_text`. |
| F7 | MED | The second rebuild's seat gate returned each seat's y as a bin centre, so a y error under 0.05 could not show and up to 0.1 passed. | Tooling (second review) | `seat_point` returns the vertex's own y; `test_a_small_shift_moves_the_seat_by_the_same_amount`. |
| F8 | LOW | The five copies of `RIG_SCALE` were not held equal; the second rebuild added `RIG_BACK` (four copies) and `TAIL` (two). | Lens 5; Tooling (second review) | `RigScaleTests`: three parity tests. |
| F9 | LOW | The replay steps omitted the capsule fit's `--limit` and `--margin`. | Codex, Lens 5 | The ledger's replay carries them (now 0.416 and 0.0624). |
| F10 | LOW | Two masters with one name and different GUIDs: the last one scanned won silently. | Codex | `Add-MasterName` reports `TWO MASTERS NAMED`. |
| F11 | LOW | `fbx_takes.take_names` dropped a take name over 1,019 bytes without an error. | Codex, Tooling | It raises; `test_fbx_takes` covers it. |
| F12 | LOW | A missing `-Pack` or `-PackMasters` folder exited 1 (errors) instead of 2 (bad arguments). | Tooling | Both are in the guard; harness case. |
| F13 | LOW | The Monster's header comment and several docs stated the old size story. | Lens 7, Lens 5 | Header written by `monster_text`, `test_the_header_says_where_the_size_lives`; docs corrected. |
| F14 | LOW | With `--back 0` the prep gate took the tail's sign from the move, so it walked toward the nose. | Tooling (second review) | The sign comes from `TAIL`. |
| F15 | LOW | `test_points_off_the_midline_are_ignored` passed with the midline filter removed. | Tooling (second review) | Its point now opens a bin the walk would reach. |
| F16 | LOW | `scale_one` reused `back` for the move and for the re-imported armature. | Tooling (second review) | Renamed `again`. |

## Root-cause pattern

F1 and F2 are the same failure: an engine behaviour stated as fact (or as "not established") without a native read,
then built on. Both were in model-space data that only native code consumes, which no managed decompile shows. F3 to
F6 and F8 share another: a tool that writes to the unversioned Armory gained a mode or a value with no test that fails
when the live data and the recipe part ways. F7, F14 and F15 are gates whose test inputs could not reach the failure
they guard.

## Why each reviewer missed what it missed

- **Codex** checked the managed consumers of the Monster lengths and the take names; it reads no native code, so it
  left F1 UNVERIFIED and could not see F2.
- **Lens 7** read the XML against its siblings and found F6 and F13; tool code and native seating were outside it.
- **Lens 5** traced every artifact's factor and found F1, F4, F6 and F8; it flagged F1 but could not decide it.
- **Lens 2** decided F1 from native code. F2 was not in the first review's scope, because the seat had not been
  seen in game; the same lens traced it after Mike's ride.
- **Tooling** found F3 to F5, F11 and F12 in the first review, and F7, F8 and F14 to F16 in the second.
- **No reviewer** caught that a move inside armature space turns with an animated armature object: the animation
  tool's own re-import gate failed on 5 of 6 files before any review (lesson in `animation-skeleton.md`).

## Deferred

- FOLLOW-UP (no issue: no documented call does it; one guard line): `scale_dire_wolf_anims.py` would overwrite the
  purchased pack's FBX if `--out` equalled `--src` (Tooling L3).
- FOLLOW-UP (no issue: both callers comply today): `rig_scale.scale_rig` assumes meshes without shape keys and without
  shared mesh data (Tooling L4).
- FOLLOW-UP (no issue: test strength only, no live effect): the skeleton fixture's joints sit at the origin and its
  bounding box is zero, so a mutant that skips them passes; `rig_scale.fcurves` has no test of its two branches
  (Tooling L5).
- FOLLOW-UP (no issue: Mike to decide on the doc's columns): the `monster-size.md` table mixes measured and predicted
  heights in one column (Lens 5).
- Not adopted: Codex asked `fbx_takes` to read take names from `Objects/AnimationStack` nodes. The `\x00\x01AnimStack`
  suffix is the FBX naming of that node's name property, and a stray match needs NUL bytes inside a property.
