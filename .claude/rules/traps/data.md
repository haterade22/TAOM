---
paths:
  - "Main/_Module/ModuleData/troops/**"
  - "**/characters/*.xml"
  - "**/equipmentsets/*.xml"
  - "**/taom_spcultures.xml"
  - "**/spcultures.xslt"
  - "Main/_Module/ModuleData/charactercreation/**"
  - "Main/Features/CharacterCreation/**"
  - "Main/Features/LordSpawnGuard/**"
  - "tools/generate_ranged_ladder_items.py"
  - "tools/ranged_ladders.json"
  - "tools/audit_polearm_shield_parity.py"
  - "tools/armour_cover_flags.json"
  - "tools/apply_armour_cover_flags.py"
---

# Trap sheet: troop and culture data

Troop and character XML, equipment sets, player start kits, culture files and ranged ladders.

One line per trap: its trigger and what never to do. The linked doc holds the mechanism, the
history and the gate. A new trap for this area is a new row here, never a row in the always-loaded
[trap index](../../../docs/ai-includes/orientation.md)
([ADR-011](../../../docs/adrs/011-knowledge-delivery-tiers.md)).

| Trap | Rule | Doc |
|---|---|---|
| Shield plus polearm or two-hander | A shield troop never draws a polearm absent from `OneHandedPolearm`, or a two-handed weapon; a new pair fails `audit_polearm_shield_parity.py` (#1312) | [pipeline](../../../docs/features/weapon-xml-pipeline.md) |
| Landless cultures | A culture owning no settlement CTDs the daily clan tick: `Patch65`, `LANDLESS_CULTURE` | [spawn guard](../../../docs/features/lord-spawn-guard.md) |
| NPCCharacter `<face>` | Missing: toddler; copied key: twin lords; a lady on a template: her own hair tag. Gates in the doc | [body properties](../../../docs/modding/body-properties.md) |
| Player start kits | Defaults are `starter_<donor>` twins, careers take troop gear; override the roster, never a set | [start kits](../../../docs/features/starting-equipment-tuning.md) |
| Troop bows | Generated `ladder_*` items in the unversioned Armory: `generate_ranged_ladder_items.py --verify` | [ladders](../../../docs/features/ranged-ladders.md) |
| Armour cover attributes | A reinstall, sync or art drop resets hair, beard, `covers_*` and mane values in the unversioned Armory: change them only through `armour_cover_flags.json` and `apply_armour_cover_flags.py --check`, never by hand alone | [ledger](../../../docs/reference/lotrlome-beard-cover-changes.md) |
| Couch and brace | A spear is couched or braced only if the Armory's `weapon_descriptions.xslt` lists every piece it uses under `TwoHandedPolearm_Couchable` and `_Bracing`; the file is unversioned, so register through `register_couch_brace_polearms.py` and its spec, never by hand alone | [pipeline](../../../docs/features/weapon-xml-pipeline.md) |
| Main weapon in Item0 | A lance rider and a troop named for its polearm open with that polearm; `rebalance_troops.py --main-weapon-slots` plans it, `TROOP_MAIN_WEAPON_NOT_ITEM0` warns, and the second root is `troop_purpose.EXTRA_TROOP_FILES` | [rule](../../../docs/features/troop-skill-balance.md) |
| Troop weapon category | A balance pass changes a weapon's numbers or swaps it within its category, never across categories or between a shield kit and a two handed one; `fix_melee_ladder.py --apply` stays unrun until it holds that | [rule](../../../docs/features/melee-damage-model.md#a-troop-keeps-its-weapon-category) |
