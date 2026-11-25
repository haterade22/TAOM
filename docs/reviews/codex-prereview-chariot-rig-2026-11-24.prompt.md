# Codex pre-review: war chariot rig replacement (2026-11-24)

You are an independent reviewer. Review only; change no file. Repository root: this working directory
(`E:/repos/taom-chariot-land`, branch `feat/chariot-rig-land`, base `bannerlord-1.5.x` at `0425b033c`). Read
`AGENTS.md` and `.ai/roles/reviewer.md` first, then `.claude/rules/traps/creatures-assets.md` and
`.claude/rules/traps/data.md` (path rules do not load here). Never write the name of the upstream chariot pack or
of its author in your output: write "the upstream chariot pack".

## The change

On 2026-11-23 the war chariot of the Bannerlord mod TAOM moved to a new rig: one package of clips and skeletons
taken from an upstream chariot pack, in the live, unversioned module `LOTRLOME_Armory`. The repo branch records
that rig and makes the repo's checks read it.

Uncommitted repo files (see `git status`, `git diff HEAD`):

- `tools/verify_mount_assets.py`: a `chariot` entry; `clip_metas(d)`; `untagged_clips` reads `quad_movement`
  from each clip's own metadata; three `untagged_ok` pairs for the chariot.
- `tools/audit_mount_parity.py`: section F reads the one-package rig through `clip_metas`;
  `VANILLA_UNTAGGED_MOVEMENT_ACTIONS`; `untagged_gait_clips`.
- `tools/tpac_slim_package.py` (new) and `tools/tests/test_tpac_slim_package.py`: writes a copy of a package
  without named items, with a new package GUID.
- `tools/data_gates.py`: new gate `chariot-body`; `tools/tests/test_data_gates.py`: its test.
- `tools/tests/test_audit_mount_parity_strict.py`, `tools/tests/test_verify_mount_assets.py`: new cases.
- `TAOM.Tests/Features/Warg/DwarfMountRiderRowsTests.cs` and
  `docs/reference/lotrlome-armory-snapshot/action_sets.xml`: the dwarf rider rows of the chariot.
- `Main/_Module/DsAssetPackages/taom_creature_skeletons.tpac`: rebuilt by `tools/build_ds_skeleton_pack.py --apply`.
- `docs/features/chariot.md` (section "The rig since 2026-11-23"), `docs/reviews/lessons/animation-skeleton.md`.

Live, unversioned files of the rig replacement, never reviewed (read them at these absolute paths, under
`E:/Steam/steamapps/common/Mount & Blade II Bannerlord/Modules/LOTRLOME_Armory/ModuleData/`):

- `Monsters/LOTR/lotr_monster_chariot.xml`
- `action_types.xml` (the `act_chariot_*` rows), `action_sets.xml` (the three `as_chariot*` sets and every
  `act_chariot_*` rider row in other sets), `action_sets.xslt`
- `monster_usage_sets.xml`, `monster_usage_sets.xslt` (usage set `chariot`)
- `module_sounds.xml` (the `taom_chariot_*` sounds), `voice_definitions.xml`, `project.mbproj`
- `CollisionInfos/LOTR/collision_infos_warg.xml` (class `taom_chariot`), `physics_materials.xml`,
  `physics_materials.xslt`, `cloth_bodies.xml`
- `LOTRLOME_items/LOTRAOM_horses.xml`: only `taom_chariot_a`, `taom_chariot_armour_rhun`,
  `taom_chariot_armour_imperial` (the three dwarf chariot rows had their review; see
  `docs/reviews/rca-dwarf-chariot-2026-11-24.md`)

The rig package is `.../LOTRLOME_Armory/Assets/creature/chariot/chariot_rig_geo.tpac`. Other sessions changed
`skins.xml`, the warg and dire wolf clip packages and non-chariot rows of `action_sets.xml`: out of scope.

Facts the maintainer gave: he ran the Rhûn chariot and both dwarven chariots in battle on this data and they
worked. Sound and a chariot death are not checked. No GitHub issue: he waived it.

## Questions to answer with evidence

1. Rider rows. `monster_usage_sets.xml` asks actions of a rider on the `chariot` usage set. For which rider
   action sets (human male and female, each race set that a TAOM troop can have) is each of those actions
   bound, and for which is one missing? Which TAOM troops carry `taom_chariot_a` or `taom_ram_chariot_a`
   (`Main/_Module/ModuleData/troops/`), and does each of their action sets have every row? State what the engine
   does for a missing row only with a quoted source; otherwise UNVERIFIED.
2. Action types. Does any `act_chariot_*` action carry a type that needs a clip parameter its clip lacks (the
   hang class that `tools/tests/test_armory_kick_typed_actions.py` guards)? Is any `act_chariot_*` action used
   in a set and not declared, or declared twice?
3. `tools/verify_mount_assets.py` `clip_metas`: is the parse of the package correct for every item layout in the
   Armory (run `python tools/verify_mount_assets.py spider`, `dire_wolf`, `chariot`)? Can it report a tagged
   clip when the clip has no tag, or miss a clip? Is each `untagged_ok` pair for the chariot justified by the
   live rows?
4. `tools/audit_mount_parity.py` `VANILLA_UNTAGGED_MOVEMENT_ACTIONS`: the claim is that the vanilla horse binds
   untagged clips on its six in-place rows, so a mount's clip there needs no `quad_movement`. Check the claim
   against the vanilla data under `E:/Steam/steamapps/common/Mount & Blade II Bannerlord/Modules/Native/` and
   say whether the rule can hide a clip that needs the tag.
5. `tools/tpac_slim_package.py`: can its output differ from the input in any byte it does not mean to change?
   Does it leave a reference to a dropped item? Does it refuse an input it cannot parse, or write a damaged
   package? Are the tests meaningful (do they fail when the code is wrong)?
6. Sounds and registration: does each `taom_chariot_*` sound name a file that exists under the module's
   `ModuleSounds` folder? Is `voice_definitions.xml` registered in `project.mbproj` and in the module's
   `SubModule.xml` the way the engine needs (compare with how Native or SandBoxCore registers its own)? Can the
   added collision class, physics material or cloth body row break a load?
7. The dedicated-server skeleton pack: which skeletons does a server need for the chariot, and does the rebuilt
   pack hold them? Does `tools/build_ds_skeleton_pack.py --check` pass?
8. `docs/features/chariot.md` "The rig since 2026-11-23": name each statement that the files contradict.

## Output

Findings grouped by severity (P1, P2, P3), each with file and line, the evidence you read, and the smallest fix.
Mark anything you could not verify as UNVERIFIED. End with a one-line verdict.
