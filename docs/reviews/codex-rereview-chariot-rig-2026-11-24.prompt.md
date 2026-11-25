# Codex re-review: war chariot rig replacement, the fix pass (2026-11-24)

You are an independent reviewer. Review only; change no file. Repository root: this working directory
(`E:/repos/taom-chariot-land`, branch `feat/chariot-rig-land`, base `bannerlord-1.5.x` at `0425b033c`, all
changes uncommitted). Read `AGENTS.md` and `.ai/roles/reviewer.md` first. Never write the name of the upstream
chariot pack or of its author: write "the upstream chariot pack".

Your first review of this change is in `docs/reviews/raw/codex-prereview-chariot-rig-2026-11-24.md` (two P2, two
P3). Four Claude review lenses ran beside it and found one HIGH. The findings and what was done are in
`docs/reviews/rca-chariot-rig-2026-11-24.md`. This pass reviews the fixes.

## What changed since your first review

- `tools/tpac_slim_package.py`: `forbidden_hits` (the `--forbid` search now reads each compressed segment's
  payload); the round-trip gate catches a failure of the re-serialise step; the write uses mode `xb` inside the
  `try`; the printed package id is `UUID(bytes_le=...)`; the option `--externalise-textures` is removed.
- `tools/tpac_clone_metamesh.py` `_read_item`: refuses a negative length or count (your P2 on the segment count).
- `tools/verify_mount_assets.py`: `quad_tagged(meta)` walks a clip's metadata to its usage list (your P2 on the
  text search); exit 2 with an `ERROR:` line when the install is missing; the Monster's `rein_skeleton` is checked
  for creatures with `monster_xml`; CHECK 3 reads every action set of the file and `action_sets.xslt` for the
  rider clip prefixes of the creature.
- `tools/audit_mount_parity.py`: `chariot_clips` asks `quad_tagged`; `chariot_rider_clips` also matches the map
  clips.
- `tools/data_gates.py`: gate `chariot-body`, with `*/action_sets.xslt` among its paths.
- Tests for each of these in `tools/tests/`.
- `docs/reference/lotrlome-armory-snapshot/`: `action_sets.xml`, `action_types.xml`, `monster_usage_sets.xml`,
  `monster_usage_sets.xslt` and `project.mbproj` copied from the live Armory; `README.md` updated.
- `docs/features/chariot.md`, `docs/reviews/lessons/animation-skeleton.md`,
  `docs/reviews/lessons/build-tooling-workflow.md`, `docs/ai-includes/creature-mount-authoring.md`.

## Questions to answer with evidence

1. `quad_tagged`: is the field walk right for metadata versions 5 and 6 (compare with
   `tools/set_clip_balance_name.py` `parse` and with real clips under
   `E:/Steam/steamapps/common/Mount & Blade II Bannerlord/Modules/Native/AssetPackages/animation_clips.tpac` and
   `.../Modules/LOTRLOME_Armory/Assets/creature/`)? Can it return True for a clip with no `quad_movement` usage
   record, or False for a clip that has one (a second usage record before it, a flag count that is negative or
   very large, a version above 6)? Does your first counterexample now fail the check?
2. `forbidden_hits`: can a forbidden text still be in the output and not be found (a text split across two
   segments does not count)? What happens when `lz4` is not installed, or a segment does not decompress?
3. `_read_item`: does the new refusal change the result for any real package (run
   `python -m unittest tools.tests.test_tpac_clone_metamesh tools.tests.test_build_ds_skeleton_pack
   tools.tests.test_prefab_asset_packages` if your Python has the modules; say so if it has not)? Is any other
   count or length in the item still read unchecked in a way that lets a damaged package round-trip?
4. The rein skeleton and rider clip checks in `verify_mount_assets.py`: can either pass on missing data (a
   Monster file that is absent, a stylesheet that is absent, a clip name that is a prefix of another)? Is a
   missing Monster file a FAIL or a silent pass, and which should it be?
5. The snapshot copy: does any of the five snapshot files now differ from its live file other than by line
   endings? Does a test or tool that reads the snapshot (search `tools/` and `TAOM.Tests/` for
   `lotrlome-armory-snapshot`) now read something it did not expect (for example
   `tools/tests/test_armory_kick_typed_actions.py`, `tools/bind_warg_clips.py --check`,
   `tools/bind_spider_clips.py --check`, `tools/bind_dwarf_attack_actions.py --check`)?
6. Does any new or changed test pass when the code it tests is wrong?
7. Does any statement in the changed docs or in the RCA contradict the files?

## Output

Findings grouped by severity (P1, P2, P3), each with file and line, the evidence you read, and the smallest fix.
Mark anything you could not verify as UNVERIFIED. End with a one-line verdict.
