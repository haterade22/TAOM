# Codex pre-review: the warg's canter and gallop records, their tool and the trial step (2026-11-23)

You are the reviewer (`.ai/roles/reviewer.md`). This is a review-only assignment: do not modify,
create or delete any file, do not run a build that deploys (`./build.ps1`), do not run any tool with
`--apply`, and do not commit. Read `AGENTS.md`, `.ai/policy.md` and `.ai/review-reference.md` first.
Every claim needs a source location you read in this run; mark anything you cannot prove UNVERIFIED.

## The change

Uncommitted working tree in the worktree `E:\repos\taom-warg-walk`, branch `test/warg-gait-trial`,
HEAD `63e7d82ba`. Run `git status --short` and `git diff`; new files are untracked. Ignore
`docs/audits/armory-ref-audit.md`, which is not part of the change. Issue #1563.

- `tools/repoint_anim_clip.py`, `tools/set_clip_balance_name.py` (one field, `usages_at`),
  `tools/tests/test_repoint_anim_clip.py`: a plan row may now set a gait record's `travel`,
  `limits` and `step_points`; `--under`, `--module`; `--check` fails a record the Kit has not
  cooked (`cooked`).
- `tools/warg_gait_plan.json`, `tools/tests/test_warg_gait_plan.py`: the plan and its tests.
- `tools/data_gates.py`, `tools/tests/test_data_gates.py`: the `warg-gaits` row.
- `Main/Features/Warg/WargTrialRules.cs`, `WargTrialRows.cs`,
  `Client/Features/Warg/Trial/*.cs`, `Main/Features/SeatOfSeeing/Domain/SeatTrialSteps.cs`,
  `SeatTrialRoster.cs`, `Client/Features/SeatOfSeeing/Trial/CommandTrialScript.cs`,
  `tools/run_command_trial.py` and tests: a `warg_gaits` step of the unattended command trial.
- `docs/reference/lotrlome-warg-gait-changes.md` (the ledger: read it first, it states every value
  and the claimed cause), `docs/reviews/lessons/animation-skeleton.md` (last entry),
  `.claude/rules/traps/creatures-assets.md`, `tools/README.md`, `docs/reference/lotrlome-warg-changes.md`.

Seven clip records in the LIVE, unversioned Armory were rewritten by the tool on 2026-11-23 15:40,
then cooked by one Modding Kit start. They are under
`E:\Steam\steamapps\common\Mount & Blade II Bannerlord\Modules\LOTRLOME_Armory\Assets\creature\warg\animations\`:
`warg_canter`, `warg_canter_stand`, `warg_canter_strafe`, `warg_canter_l`, `warg_canter_r`,
`warg_forward_gallop_left_foot`, `warg_forward_gallop_left_foot_stand` (each `<name>_anm.tpac`, with a
`.bak-repoint-20261123-154054` backup beside it). Read-only checks you may run:
`python tools/repoint_anim_clip.py --plan tools/warg_gait_plan.json --under creature/warg/animations --check`
and `python -m pytest tools/tests/test_repoint_anim_clip.py tools/tests/test_warg_gait_plan.py -q`.

## The claimed mechanism (verify it)

The mount movement node (`rglAnim_node_horse_movement`, TaleWorlds.Native.dll v1.5.7; the read is in
`docs/features/elephant.md`, "What the engine does with a clip record", and
`tools/check_gait_stride.py`'s header) picks a pace from speed divided by agent scale against each
front record's pace limits, and takes the pace's loop time from the `Duration` of the pace's
`direction="none"` clip at the lower limit and of its front clip at the upper, linear between. The
plan sets each `Duration` to travel divided by that limit. Native tools:
`python tools/native_decompile.py` (`docs/features/ghidra-native-decompile.md`).

## Known suspects (give each a verdict with evidence)

1. **The Duration rule.** Is "none clip's Duration at the lower limit, front clip's at the upper" what
   the node does for the canter and gallop paces of `monster_usage_set id="warg"`
   (`docs/reference/lotrlome-armory-snapshot/monster_usage_sets.xml`)? Which row is each pace's
   "none" clip there, given that a `none/none` row fills every key no earlier row holds? The gallop
   has left-foot and right-foot rows that bind the same records.
2. **Records the plan did not change.** `warg_forward_gallop_left_foot_strafe` and the two gallop
   turn records keep `Duration` 0.58 while the gallop's front record is now 0.33 and its twin 0.56;
   `warg_canter_strafe` and the canter's turn records took 0.46. Does the node read a strafe or turn
   record's `Duration` in a moving pace? Is any of these choices a visible fault?
3. **Other readers of the changed records.** Any action, in any module's action set, that plays one
   of the seven records as a one-shot, where a shorter `Duration` or another master changes its
   timing (the ledger says only `as_warg` binds them; the fell warg and `Monster` files share that
   set). The upper-body and rider rows of the usage set.
4. **The pace limits.** Canter 4.6 to 7.0, gallop 7.0 to 12.0, trot unchanged at 2.2 to 4.8. Is a
   shared boundary of 7.0 a flicker risk given how the node steps up and down? Is a speed above
   12.0 at model size reachable (item `speed`, Riding, perks, a hero ability), and what does the node
   do there?
5. **The byte writer.** `_travel_at` assumes the gait usage is the record's last field: name, an
   int 0, three floats, then the checksum. Is that true of every `quad_movement` record a plan could
   name (check several creatures' records)? Can `repoint` corrupt a record it accepts? Does the
   `usages_at` field break any other user of `set_clip_balance_name.parse`?
6. **The cooked check.** `cooked` compares the cache entry's stamp at 0x68 with the record's stored
   checksum (`set_clip_balance_name.rdc_current`). Can it read True for a record whose cooked keys
   are stale (a master changed and the record did not), or False for a good state? The `--check`
   exit code changed for every existing user of the tool: who runs it?
7. **The gate and its tests.** Does each test in `test_warg_gait_plan.py` and the new classes of
   `test_repoint_anim_clip.py` fail when its value is wrong (break each value alone)? Does the
   `warg-gaits` row select on the right paths and nothing it should not?
8. **The trial step.** `CommandTrialHost.Warg.cs` reads agents in the mission tick: thread safety,
   deleted-agent handles (`AgentSlotIdentity`), the `HashSet<Agent>` across a mission, NaN handling
   in `CommandTrialScript.Warg.cs`, and whether the step can pass when no warg ran.
9. **The docs.** Any number or claim in the ledger, the lesson, the trap row or the README row that
   the files or the code contradict.

Report findings by severity (P1, P2, P3) with file and line, then a verdict per suspect.
