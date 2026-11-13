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
---

# Trap sheet: troop and culture data

Troop and character XML, equipment sets, player start kits, culture files and ranged ladders.

One line per trap: its trigger and what never to do. The linked doc holds the mechanism, the
history and the gate. A new trap for this area is a new row here, never a row in the always-loaded
[trap index](../../../docs/ai-includes/orientation.md)
([ADR-011](../../../docs/adrs/011-knowledge-delivery-tiers.md)).

| Trap | Rule | Doc |
|---|---|---|
| Shield plus polearm | A shield troop never draws a polearm absent from `OneHandedPolearm`: `audit_polearm_shield_parity.py` | [pipeline](../../../docs/features/weapon-xml-pipeline.md) |
| Landless cultures | A culture owning no settlement CTDs the daily clan tick: `Patch65`, `LANDLESS_CULTURE` | [spawn guard](../../../docs/features/lord-spawn-guard.md) |
| NPCCharacter `<face>` | Missing: toddler; copied key: twin lords; a lady on a template: her own hair tag. Gates in the doc | [body properties](../../../docs/modding/body-properties.md) |
| Player start kits | Defaults are `starter_<donor>` twins, careers take troop gear; override the roster, never a set | [start kits](../../../docs/features/starting-equipment-tuning.md) |
| Troop bows | Generated `ladder_*` items in the unversioned Armory: `generate_ranged_ladder_items.py --verify` | [ladders](../../../docs/features/ranged-ladders.md) |
