# Codex pre-review: dwarven war chariot (2026-11-24)

You are an independent reviewer. Review only; change no file. Repository root: this working directory
(`E:/repos/taom-dwarf-chariot`, branch `feat/dwarf-chariot`, base `bannerlord-1.5.x` at `bcecab80a`). Read
`AGENTS.md` and `.ai/roles/reviewer.md` first, then `.claude/rules/traps/data.md` (path rules do not load here).

## The change

A data-only feature for the Bannerlord mod TAOM: a war chariot for the dwarves of Erebor, pulled by two war rams,
on the existing chariot rig (Monster `chariot`, skeleton `horse_skeleton_chariot`, action set `as_chariot`).

Uncommitted repo files (see `git diff`):

- `Main/_Module/ModuleData/troops/troops_erebor.xml`: new troops `ironpass_war_chariot` (level 41) and
  `ironpass_steel_chariot` (level 46); `ironpass_ram_vanguard` gains a second `upgrade_target`.
- `Main/_Module/ModuleData/taom_troop_name_strings.xml`: two generated name rows.
- `tools/taom_schema.py`: `taom_ram_chariot_a` added to `DWARF_MOUNT_IDS`.
- `tools/tests/test_validate_moduledata.py`: two cases in `MountedDwarfTests`.
- `docs/features/chariot.md`: new section "Dwarven war chariot (Erebor, 2026-11-24)".

Live, unversioned files changed for it (read them at these absolute paths):

- `E:/Steam/steamapps/common/Mount & Blade II Bannerlord/Modules/LOTRLOME_Armory/ModuleData/LOTRLOME_items/LOTRAOM_horses.xml`:
  three new items after `taom_chariot_armour_imperial`: Horse `taom_ram_chariot_a`, HorseHarness
  `taom_chariot_armour_dwarf` and `taom_chariot_armour_dwarf_b`.
- `E:/Steam/steamapps/common/Mount & Blade II Bannerlord/Modules/LOTRLOME_Armory/ModuleData/Languages/loc_LOTRAOM_horses.xml`:
  three English rows.

Decisions the maintainer took (not findings): two troops at levels 41 and 46; the two ram bardings may pass
through each other; the ram team keeps the horse voices of `Monster.chariot` for now; both chariots share
`family_type="4"` with the Rhûn Wainrider chariot.

## Questions to answer with evidence

1. Can a `race="dwarf"` troop ride an item on `Monster.chariot` without the failure that `MOUNTED_DWARF` exists
   for? The claim in `tools/taom_schema.py` and the doc is that the rider stands on the cart on chariot rider rows
   of the dwarf action set. Check the live Armory `ModuleData/action_sets.xml` (the dwarf warrior sets and their
   `act_chariot_*` rows), `monster_usage_sets.xml`, and `Monsters/LOTR/lotr_monster_chariot.xml`. Is every action
   that `monster_usage="chariot"` asks of a rider bound for the dwarf sets, female and other dwarf sets included?
2. Is anything in the repo keyed on `DWARF_MOUNT_IDS` that the new id changes in a way nobody intended
   (`tools/generate_tournament_templates.py`, enlistment roster tools, C# under `Main/Features/` that names the war
   ram items)? Is any generated file now stale?
3. Do the two troops break a data gate or a generator: upgrade monotonicity of skills and armour, the armour band
   clone ledger (`tools/armour_band_clones.json`, its `wrote` indexes), `tools/rebalance_troops.py` fixed point,
   Fords of War copies, enlistment rosters, volunteer recruitment (`VolunteerRecruitmentService` and its tests),
   troop upgrade requirements for a mounted upgrade, party templates, the Long Defeat shop?
4. The Horse item copies `skeleton_scale="empire_horse"` from `taom_chariot_a`. That scale entry is declared for
   `horse_skeleton` with vanilla bone names. What does the engine do with it on `horse_skeleton_chariot`? Quote the
   code or mark it UNVERIFIED.
5. Anything in the new items that can hang or crash a load (a mesh, material or monster reference that does not
   resolve, an attribute the item XSD rejects), given that the three meshes were imported through the Modding Kit
   today into `LOTRLOME_Armory/Assets/creature/chariot/mesh/`.

## Output

Findings grouped by severity (P1, P2, P3), each with file and line, the evidence you read, and the smallest fix.
Mark anything you could not verify as UNVERIFIED. End with a one-line verdict.
