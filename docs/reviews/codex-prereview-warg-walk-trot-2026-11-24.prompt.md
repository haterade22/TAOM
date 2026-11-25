# Codex pre-review: the warg's walk and trot, the dire wolf's gaits, the rider's gallop turn clips (2026-11-24)

You are the reviewer (`.ai/roles/reviewer.md`). This is a review-only assignment: do not modify,
create or delete any file, do not run a build that deploys (`./build.ps1`), do not run any tool with
`--apply`, and do not commit. Read `AGENTS.md`, `.ai/policy.md` and `.ai/review-reference.md` first.
Every claim needs a source location you read in this run; mark anything you cannot prove UNVERIFIED.

## The change

Uncommitted working tree in the worktree `E:\repos\taom-warg-walk-trot`, branch `fix/warg-walk-trot`,
HEAD `3dcf173c7`. Run `git status --short` and `git diff`; four files are untracked
(`tools/bind_warg_clips.py`, `tools/dire_wolf_gait_plan.json`, `tools/tests/test_bind_warg_clips.py`,
`tools/tests/test_dire_wolf_gait_plan.py`). Issue #1565, a follow-up of #1563
(`docs/reviews/rca-warg-gaits-2026-11-23.md`).

- `tools/repoint_anim_clip.py`, `tools/set_clip_balance_name.py` (one field, `flags_at`),
  `tools/tests/test_repoint_anim_clip.py`: three new things a plan row may do. `template` makes a
  clip that is not in the folder from another record (`clone`: new name, package GUID and item GUID
  from `uuid5` of the name). `flags` rewrites the record's whole flag list (`_with_flags`). A range
  may start at frame 0 on a master that a record of the folder already plays from frame 0
  (`rest_frame_played`).
- `tools/warg_gait_plan.json`, `tools/dire_wolf_gait_plan.json` and their tests
  (`test_warg_gait_plan.py`, `test_dire_wolf_gait_plan.py`): the values.
- `tools/bind_warg_clips.py` and its tests: binds `act_warg_forward_walk_stand` to each creature's
  new in-place twin in `as_warg` and `as_dire_wolf`, in the live Armory and in
  `docs/reference/lotrlome-armory-snapshot/action_sets.xml`, only after the twin is cooked.
- `tools/gen_dire_wolf_anim_clips.ps1` and its test: the strafe ratios are numbers (`$FAMILY`),
  `$WARG_ONLY`, and the records the gait plan names are not held to the pack's Duration and travel.
- `tools/data_gates.py` and its test: the rows `warg-clips` and `dire-wolf-gaits`.
- Docs: `docs/reference/lotrlome-warg-gait-changes.md` (section "The walk and the trot (#1565)" and
  below) and `docs/reference/lotrlome-dire-wolf-changes.md` (section "Gaits") state every value and
  the claimed causes: read them first. Also `docs/features/warg-combat.md`, `dire-wolf.md`,
  `.claude/rules/traps/creatures-assets.md`, `tools/README.md`, `docs/reference/release-process.md`.

Written to the LIVE, unversioned Armory
(`E:\Steam\steamapps\common\Mount & Blade II Bannerlord\Modules\LOTRLOME_Armory`), then cooked by one
Modding Kit start on 2026-11-24 10:17, then bound at 10:20:
`Assets/creature/warg/animations/`: `warg_walkfast`, `warg_walk_stand` (new), `warg_trot`,
`warg_trot_stand`, `rider_warg_gallop_turn_left_head`, `rider_warg_gallop_turn_right_head`;
`Assets/creature/direwolf/animations/`: `dire_wolf_walkfast`, `dire_wolf_walk_stand` (new),
`dire_wolf_trot`, `dire_wolf_trot_stand`, `dire_wolf_canter`, `dire_wolf_canter_stand`,
`dire_wolf_forward_gallop_left_foot`, `dire_wolf_forward_gallop_left_foot_stand` (each
`<name>_anm.tpac`; a changed record has a `.bak-repoint-20261123-2008xx` or `-2029xx` backup beside
it); and two rows of `ModuleData/action_sets.xml` (backup `.bak-warg-bind-20261124-102002`).

Read-only checks you may run:
`python tools/repoint_anim_clip.py --plan tools/warg_gait_plan.json --under creature/warg/animations --check`,
the same with `tools/dire_wolf_gait_plan.json` and `creature/direwolf/animations`,
`python tools/bind_warg_clips.py --check`, and
`python -m unittest tools.tests.test_repoint_anim_clip tools.tests.test_bind_warg_clips tools.tests.test_warg_gait_plan tools.tests.test_dire_wolf_gait_plan tools.tests.test_data_gates tools.tests.test_gen_dire_wolf_anim_clips`
(they need `xxhash` and `numpy`; say so if your sandbox lacks them).

## Claims to verify (each is the author's, none is settled)

1. **Loop time.** The mount movement node (`rglAnim_node_horse_movement`, TaleWorlds.Native.dll
   v1.5.7; `docs/features/elephant.md`, "What the engine does with a clip record") plays one loop in
   the `Duration` of the pace's `direction="none"` clip at the front clip's `PaceSwitchLimitMin`, in
   the front clip's `Duration` at `PaceSwitchLimitMax`, on a straight line between. The plans choose
   the two Durations so that speed x loop time stays within 8% of the travel over the pace (the walk
   from 1.2 m/s). Is the arithmetic right for every pace of both plans? Is a "none" clip with a
   2.06 s or 2.38 s Duration at a 0.1 m/s lower limit safe (clamps, a division, the standing pace's
   -0.4..0.4 limits, the node's weight `speed / (k x LoopDisplacement / loop time)`)?
2. **The new in-place twin.** A record with no `quad_movement` usage and no flags, cloned from
   `warg_forward_gallop_left_foot_stand`, bound as the walk's "none" row. Does the "none" row of
   pace 2 behave as the trot's, canter's and gallop's "none" rows do? Does anything else read
   `act_warg_forward_walk_stand` (rider rows in `as_human_warrior` and `as_dwarf_warrior` bind the
   same action type to `rider_warg_forward_walk`; child sets; `monster_usage_sets.xml`)?
3. **`clone`.** The bytes: name at offset 72, package size at 28, package GUID at 8, item GUID at 52.
   Are there other places in a single-item AnimationClip package, or in the engine's or the Kit's
   reading of it, where the package GUID, the item GUID or the name must also change (a table of
   contents, a dependency entry, the cache entry)? Is a version-5 GUID acceptable to the engine and
   the Kit? Could two clips ever get one GUID?
4. **`_with_flags`.** The flag list is rewritten between `flags_at` and `usages_at`, and the metadata
   length and the package size follow. Is anything else length-dependent (an offset table, the
   checksum region `mlen_at..ck_at`)? The rider's two gallop turn records got `synch_with_horse`.
   The docs claim, from `docs/reference/bannerlord-animation-system-map.md` (the tick at 0x655F90),
   that the flag takes the clip's progress from the mount, and that `synch_with_movement` does the
   same from the movement phase so a turn record's `Duration` is unread while the beast moves. That
   reading is from v1.5.5 addresses: verify it on the installed v1.5.7 DLL if you can
   (`python tools/native_decompile.py --rva ...`), or mark it UNVERIFIED. Does an upper-body turn
   row behave differently with the flag (these two are bound to `act_warg_gallop_turn_*_head` on the
   rider)?
5. **Frame 0.** The dire wolf's masters are keyed 0..N and its records play from 0. The tool now
   allows frame 0 on a master that some record of the folder already plays from 0. Can that rule let
   a rest frame through for another creature that uses this tool (the hill troll's plans)?
6. **The dire wolf's travel.** Its measure takes each paw's height from that paw's own lowest point
   (`test_dire_wolf_gait_plan.py`, `InstalledMastersTests.soles`), because the rig's paws do not rest
   at z = 0. Is that measure sound (walk 2.08 m, trot 3.94 m, canter 5.83 m, gallop 6.22 m against
   the records' 1.248, 1.706, 2.496, 4.576)? A gallop stance is 2 or 3 keys long. The canter plays
   keys 1..16 of a 0..16 master.
7. **The generator.** `gen_dire_wolf_anim_clips.ps1` no longer holds `Duration` and travel for the
   records the gait plan names, and its strafe ratios are literals. Can a later `-Apply` (a deleted
   record, a reinstall) now produce a state that every gate passes and that is wrong? What sets the
   pace limits of a re-made wolf record?
8. **The bind tool.** Byte-faithful edit, the cook gate (`twin_fault`), `--check`, exit codes, the
   two-file write order, a partly bound state, another session editing the live `action_sets.xml`.
9. **Who else uses these records.** The author searched action sets and clip records and found the
   warg sets, the dire wolf sets and the rider rows only. Look for a user the search missed: other
   modules under `Modules/`, TAOM C# that names these clips or actions, co-op, the dedicated server's
   packages (`Main/_Module/DsAssetPackages`), `tools/verify_mount_assets.py`,
   `tools/audit_mount_parity.py`, the release channels.
10. **Tests that cannot fail**, and gate rows whose globs miss a file the check reads.

## Output

Findings by severity (P1 blocks the commit, P2 should be fixed, P3 note), each with file and line,
the evidence you read, and a concrete failure. Then a list of what you could not verify.
