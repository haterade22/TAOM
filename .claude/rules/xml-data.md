---
paths:
  - "Main/_Module/ModuleData/**/*.xml"
  - "Main/_Module/ModuleData/**/*.json"
---

# XML Data File Rules

## Culture NPC Naming Convention
Each culture has at least the base 26 notable NPCs in `characters/npcs_{culture}.xml`:
- `spc_notable_{culture}_0` through `_4b` — Merchants (10)
- `spc_notable_{culture}_5/_6/_7` — Preachers (3)
- `spc_notable_{culture}_8/_9` — Artisans (2)
- `spc_notable_{culture}_gl1/_10/_11/_gl4/_12/_13` — Gang Leaders (6)
- `spc_notable_{culture}_21/_22` — Rural Notables (2)
- `spc_{culture}_headman_1/_2/_3` — Headmen (3)

**Two-layer registration:** a notable or headman NPC with `is_template="true"` in `characters/npcs_{culture}.xml` also needs `<template name="NPCCharacter.<id>" />` in that culture's `<notable_templates>` block, in `taom_spcultures.xml` or in `spcultures.xslt` for the six renamed vanilla cultures. Both layers, or the engine reuses an existing template and spawns clone notables (Preachers never spawn, listed or not): it builds the spawn pool from `<notable_templates>` (vanilla `NotablesCampaignBehavior`, `HeroCreator.CreateNotable`), not from `npcs_*.xml`. Gate: `EveryNotableAndHeadmanTemplate_IsListedInANotableTemplatesBlock`. RCA: `docs/reviews/rca-cultural-feats-3pack-2026-05-31.md`.

## Culture Attribute References
Culture XML attributes (`merchant_notary`, `artisan_notary`, etc.) must reference the FIRST NPC of each occupation type.

## Kingdom Names
A `<Kingdom>` `name` is the bare realm name (`Erebor`, `Ered Luin`); "Kingdom of X" goes in `title` and on the faction card only. Gate: `KingdomNameTests`. To rename one, follow [recipe-add-a-kingdom.md](../../docs/modding/recipe-add-a-kingdom.md) "Gotchas" (generator, language rows and translation cache together).

## Region Codes
Settlement and lord id prefixes: [id-cheatsheet](../../docs/modding/id-cheatsheet.md) Region prefixes. They are independent id spaces.

## Config ID Cross-Reference (MANDATORY)

After writing ANY XML/JSON config containing culture, kingdom, or settlement IDs, cross-reference EVERY ID against this table before moving on.

When keying a config on entity ids, ship a test asserting every key resolves: a dead dictionary key is silent at every layer.

### Culture StringIds (runtime values)

| Type | StringIds | Note |
|------|-----------|------|
| **Custom cultures** | the `is_main_culture` ids in `taom_spcultures.xml` | Check one with `mcp__taom-moduledata__culture_exists` |
| **XSLT cultures** | `vlandia` (Rohan), `empire` (Dunland), `aserai` (Harad), `khuzait` (Easterlings), `sturgia` (Dale), `battania` (Khand) | Use vanilla engine IDs |

**Common mistake:** Writing lore names for XSLT cultures. `rohan` is WRONG — use `vlandia`. `dunland` is WRONG — use `empire`. `harad`/`rhun`/`dale`/`khand` are WRONG — use `aserai`/`khuzait`/`sturgia`/`battania`.

### Checklist

XML ids: `UNKNOWN_CULTURE`, `BROKEN_TROOP_REF`, `BROKEN_FACTION_REF`. `kingdom=`, `settlement=` and every JSON key: no validator pass reads them, so check by hand and ship a key test.

## EquipmentRosters Schema (MANDATORY for `equipmentsets/*.xml`)

The standalone `<EquipmentRosters>` pattern (used by `Main/_Module/ModuleData/equipmentsets/taom_equipment_sets_*.xml`, mirroring vanilla `SandBoxCore/ModuleData/sandboxcore_equipment_sets.xml`) requires:

| Roster purpose | Inner `<EquipmentSet>` opening tag |
|---|---|
| **Battle** (default) | `<EquipmentSet>` — implicit, no attribute |
| **Civilian** | `<EquipmentSet equipmentType="Civilian">` — REQUIRED |

Without `equipmentType="Civilian"` on a civilian roster, the engine treats it as battle equipment regardless of the roster ID containing `_civ_` / `_civ_equipment`. There is NO `equipmentType="Battle"` in vanilla (verified zero matches across SandBoxCore) — battle is the implicit default.

A standalone `<EquipmentRoster>` also carries `culture=Culture.<id>`, or the engine logs a line per roster at load. It is non-fatal: code lookups key off the id.

**Why this matters:** Encyclopedia portraits, settlement-walk views, dialog scenes, and random equipment selection at hero spawn all key off this attribute, not off the roster ID. A misclassified civilian roster manifests as the wrong outfit in non-combat contexts: exactly the Faramir/Boromir bug pattern.

Gate: `MISSING_CIVILIAN_TYPE`, a warning the commit hook does not show, so run `python tools/validate_moduledata.py` and read the warnings.

**This rule is for the STANDALONE roster pattern only.** Inline equipment under `<NPCCharacter><Equipments>...</Equipments>` (in `characters/*.xml` and `troops/troops_*.xml`) uses a different attribute (`civilian="true"` on `<EquipmentRoster>`) — that pattern is governed separately and is NOT affected by this rule.

## Townsfolk/notables need a plain (battle) roster too — arena spectators (#295)

Townsfolk and notables need a plain roster beside the civilian one, or they stand naked in the arena (#295). Gate: `TOWNSFOLK_WITHOUT_BATTLE_ROSTER`; repair `python tools/add_townsfolk_battle_rosters.py --apply`. Mechanism: [equipment-rosters.md](../../docs/modding/equipment-rosters.md) Gotchas.

## Career trees: a changed shipped tree raises `tree_version`

Editing the groups or choices of a shipped career in `career_system/*.xml` without raising that career's
`tree_version` leaves every save holding picks the new tree may not contain: saves migrate only to a higher
version. Trees of forks come from `tools/generate_career_forks.py`. Full text:
[career-system.md](../../docs/features/career-system.md) "Trap: when a shipped career tree changes".

## Formatting
- 2-space indentation (per .editorconfig)
- UTF-8, usually with no BOM, but some files carry one, so round-trip the bytes instead of normalising.
- **Line endings are not uniform.** Capture each file's own terminator and re-emit it verbatim; `\r?\n` does not rescue a doubled file. Detail: [TRANSLATOR_GUIDE.md](../../docs/localization/TRANSLATOR_GUIDE.md) Line endings and encoding.
