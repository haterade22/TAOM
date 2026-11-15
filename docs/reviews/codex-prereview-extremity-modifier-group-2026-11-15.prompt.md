# Codex pre-review: extremity loot modifier group (2026-11-15, #1271)

You are the reviewer. Read `.ai/roles/reviewer.md` and `.ai/policy.md` first. Review only; change no
file, run no command that writes outside `docs/reviews/raw/`. Never write to the game install.

## Scope

Worktree `E:/repos/taom-roster-skills`, the uncommitted diff against `HEAD` (`git diff`):

- `tools/rebalance_armor.py`: `_EXTREMITY_MODIFIER_GROUPS` heavy, elite and lord move from the vanilla
  item modifier group `chain` to `leather`.
- `tools/rebalance_armor.py`, second part: `_clamped_modifier_group` (the clamp branch for hero-only
  kit and unworn merchandise now also moves an arm, leg or shoulder piece from `chain` or `plate` to
  the band's group) and a new `--check` mode (exit 1 when the live Armory differs from the curve, 0
  when equal, 2 without the install).
- `tools/data_gates.py`: new gate row `armour-restat`.
- `tools/tests/test_armor_curve_invariant.py`, `tools/tests/test_kingdom_caps.py`,
  `tools/tests/test_data_gates.py`: tests.
- `docs/features/armor-balance.md`, `docs/reference/engine/item-equipment-model.md`,
  `docs/reviews/rca-weapon-armour-rebalance-2026-11-14.md`: docs.

A Claude data flow review already ran. It confirmed the table change and found that campaign troop
spawns roll the same group (`Mission.DecideAgentSpawnEquipment`, `Equipment.GetRandomEquipmentElements`
with `!IsCoreOnlyGameMode`); verify that claim and its numbers in the doc independently. The dry run
now plans 814 items (755 from `chain`, 59 from `plate`).

Intent (maintainer's decision): bracers, greaves and pauldrons of the heavy, elite and lord bands hold
armour values of 7 to 30. On `chain` a Rusty roll takes 8, so a heavy pauldron of 9 drops to 1. On
`leather` the down-rolls are 2 and 3.

The change will be written to the live, unversioned Armory
(`E:/Steam/steamapps/common/Mount & Blade II Bannerlord/Modules/LOTRLOME_Armory/ModuleData`) by
`python tools/rebalance_armor.py --apply --all --tier-source roster-first --keep-weights --keep-material-type --backup-tag <tag>`,
then by `python tools/generate_lothlorien_armor.py --apply` and
`python tools/generate_snow_orc_armor.py --apply`.

## Questions to answer with evidence

1. Engine. Installed sources are under `C:/Users/mikew/.taom-src/v1.5.7-202611101605/`
   (`TaleWorlds.Core.ItemModifier.cs`, `ItemModifierGroup.cs`, `EquipmentElement.cs`,
   `ArmorComponent.cs`, `ItemComponent.cs`) and the data under
   `Modules/Native/ModuleData/item_modifiers.xml` and `item_modifiers_groups.xml`. Confirm or refute:
   the `leather` group's five modifiers and their armour deltas and drop scores; that
   `ModifyArmor` is `max(armor + delta, 1)` applied only to a stat above zero; that the group of an
   armour item decides only which modifier a looted or produced item can roll. Find every engine or
   TAOM C# consumer that reads an armour item's `ItemModifierGroup` or its modifiers for another
   purpose (price, tier, crafting, smelting, tournament prizes, shop stock, troop spawn equipment,
   `Main/Features/**`) and say whether `leather` on a plate bracer changes it.
2. Run the dry run (`python tools/rebalance_armor.py --dry-run --all --tier-source roster-first
   --keep-weights --keep-material-type`) and prove or disprove from the tool's code that the 720
   planned items change only `modifier_group`. Does any item of the heavy, elite or lord band keep
   `chain` or `plate` on an arm, leg or shoulder piece after the plan, and why (generator-owned ids,
   hero kit, troll kit, unworn merchandise)?
3. The two generators read the same table. `python tools/generate_lothlorien_armor.py --check` and
   `python tools/generate_snow_orc_armor.py --check` fail now. List what their `--apply` would change
   and whether anything other than `modifier_group` moves. Do other generators
   (`generate_dale_armor.py`, `generate_erebor_armor.py`, `generate_gondor_armor.py`, ...) write
   `chain` to an extremity that the rebalancer then does not own?
4. Gates. Does any test, validator code or data gate (`tools/data_gates.py`,
   `tools/analyze_armor_balance.py`, `tools/taom_schema.py`, `TAOM.Tests`) pin `chain` on these
   pieces or assume the +9 top roll? Does the two-tier invariant
   (`check_kingdom_curve_invariant`, `check_curve_invariant`) still hold, and is the claim in the doc
   correct that only the lord-over-heavy check reads the heavy top roll?
5. Docs: is every number in the changed doc text reproducible from the code or the data? Name any
   that is not.

## Output

Findings by severity (P1 blocks the write, P2 fix before merge, P3 note), each with file, line and
the evidence. Say UNVERIFIED where you could not check. End with a verdict line.
