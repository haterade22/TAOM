# Codex independent pre-review: spider locomotion (#1541), 2026-11-22

You are the reviewer (see `.ai/roles/reviewer.md`). Review only: change no file, run no tool with
`--apply` or `-Apply`, write nothing to the game install, start no game, run no git command that
changes anything. Report defects with evidence (file, line, the input that fails). Engine facts come
from the installed game (`pwsh tools/taom-src.ps1 path <Type>`), never from a name.

## What to review

The uncommitted change in this worktree (branch `feat/spider-anim`, base 84f42bc85): `git status --short`
and `git diff` list it. Three parts:

1. **Tools** (Python and one PowerShell script): `tools/spider_gait.py` (new, generates the clips),
   `tools/bind_spider_clips.py` (new, edits three XML files in the live Armory and in the snapshot),
   `tools/check_gait_stride.py`, `tools/gen_elephant_anim_clips.ps1`, `tools/verify_mount_assets.py`,
   `tools/blender/elephant_apply_clip.py`, `tools/data_gates.py`, `tools/run_command_trial.py`, and
   their tests under `tools/tests/`.
2. **Data**: `docs/reference/lotrlome-armory-snapshot/action_types.xml`, `action_sets.xml` and
   `monster_usage_sets.xml`. The live files in
   `E:\Steam\steamapps\common\Mount & Blade II Bannerlord\Modules\LOTRLOME_Armory\ModuleData\` carry the
   same change (`python tools/bind_spider_clips.py --check` compares them). The clip packages
   `Assets/creature/spider/animations/spider_*_{geo,anm}.tpac` in that module are new.
3. **C#**: a step of the unattended custom battle harness, `spider_gaits`:
   `Main/Features/Spider/SpiderTrialRules.cs`, `SpiderTrialRows.cs`,
   `Client/Features/Spider/Trial/*.cs`, the edits to `SeatTrialSteps.cs`, `SeatTrialRoster.cs`,
   `CommandTrialScript.cs`, and the tests under `TAOM.Tests/Features/Spider/`. It ships in the mod but
   runs only when an environment variable arms the trial.

Read `docs/features/spider.md` from "Locomotion generated on its own rig" to "Current state" first, then
`docs/features/elephant.md` "What the engine does with a clip record" (the native rules the pace limits
and Durations follow).

## Intent (the maintainer's decisions; do not relitigate them)

- The spider's locomotion clips are generated on its own skeleton by a tool, not retargeted, the way
  #1474 did the elephant. The June `an_spi_*` gait clips stay on disk as the way back.
- New clips have new names. The action set is rebound only after the Modding Kit cooked each clip.
- Attacks, hits, deaths, the jump start, clip flags and rider clips are a later pass.

## What is already observed (do not re-prove; do challenge the reasoning built on it)

- Two unattended custom battles ran on the bound data with no crash: 11 spider riders spawned seated,
  stood at 0.0 m/s when held, reached 10.0 to 10.3 m/s at a charge and stopped. Stills only; nobody
  has judged how a gait looks.
- `python tools/check_gait_stride.py --creature spider`, `python tools/verify_mount_assets.py spider`,
  `python tools/bind_spider_clips.py --check` and `python tools/audit_mount_parity.py --strict` pass
  on the live files.

## Questions and suspects (the builder's own readings, not facts: test each)

1. **The pace table.** `bind_spider_clips.py` `MOVEMENTS` replaces the spider's movement rows. Is any
   (pace, direction, turn_direction, is_left_foot) combination that the warg or the vanilla horse has
   missing for the spider, or bound to an action with no row in `as_spider`, no declaration in
   `action_types.xml`, or no rider row in every set that seats a rider? Do other sections of the
   spider usage set (upper body movements, adders, jumps, falls, strikes, the verb attributes) still
   name an action whose clip or meaning this change moved? `act_spider_idle` was in movement rows and
   is now only in upper body rows and one verb attribute.
2. **Untagged twins.** The four `spider_*_inplace` records have no `quad_movement` usage, and
   `verify_mount_assets.py` now exempts them. The claim is that the vanilla horse's `gait_*` clips, the
   warg's stand twins and the elephant's walk `none` row have the same shape. Is the exemption keyed
   narrowly enough (the action and clip pair) that a twin bound to a moving row would still fail?
3. **Record numbers.** In `spider_gait.py`, `limits_of` sets a gait's upper limit to stride / seconds
   and the next pace's lower limit to 0.9 of that; the backward walk gets negative limits; the turn
   records claim radians / seconds (an unverified unit, stated as such). With the rules in
   elephant.md, can a spider get stuck in a pace, skip a pace, or play a clip at a wrong rate at some
   speed between 0 and 13 m/s, forward or backward? The gallop's upper limit is a typed 13.0.
4. **The generator's geometry.** `solve_leg`, `stance_q`, `frame_pose`: a tip target out of reach, a
   degenerate direction (a vertical leg, a zero-length vector), `acos` domain, the swing's overshoot
   behind and ahead of the sweep at a low duty factor, mirror symmetry of the two sides, the loop seam
   (frame N to frame 1), frame 0 as the rest pose. The tests are in `tools/tests/test_spider_gait.py`:
   which claim in the tool's header has no test?
5. **`bind_spider_clips.py` as a writer of an unversioned module.** Idempotence, CRLF and BOM kept,
   all six files planned before the first write, refusal when an anchor is missing, the backup outside
   the module, behaviour when the snapshot and the live file differ before the run, a partial write if
   the process dies between files, `_add_rows` when a set already holds some of the rows, the regex
   that finds rider sets (could it match a third set, or miss one a future race adds?).
6. **`gen_elephant_anim_clips.ps1`.** The new explicit-row path must not change what the tool does for
   the elephant's reports. Does `-Verify` still hold the elephant's records to the same plan? Can an
   explicit row with `usage: false` and travel greater than 0 write a record that claims travel?
7. **`check_gait_stride.py`.** The new `axis`, `turns`, `pending` and `key_rate` keys and the lookup of
   a record by the clip's own name: can the spider gate now pass on something it did not read, or the
   elephant gate change its verdict? `pending` skips `an_spi_jump`: is that skip keyed so nothing
   else rides on it?
8. **The C# step.** `CommandTrialHost.Spider.cs` holds `Agent` objects in a set across ticks and reads
   `MountAgent`, `Monster`, `MovementVelocity`: is every read after a liveness check, and only in the
   mission tick? Can the step's writes (`HoldAll`, `ChargeAll`) reach a formation that is not the
   player's? Does adding the step change any run that does not name it (`SeatTrialSteps.All`, the
   roster for other step lists, the wrapper's `normalised_steps`)?
9. **Tests pinned to counts.** `DwarfMountRiderRowsTests` pins moved from 122, 47 and 223 to 130, 55
   and 231. Are the three numbers consistent with the eight rider rows `NEW` adds to each rider set?

## Output

Findings grouped P1 (breaks a battle, a save, a gate's truth or the live module), P2, P3, each with the
file and line, the failing input or sequence, and the evidence you read. Say UNVERIFIED where you could
not read the engine or the installed files. End with what you did not review.
