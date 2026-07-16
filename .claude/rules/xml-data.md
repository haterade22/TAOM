---
paths:
  - "Main/_Module/ModuleData/**/*.xml"
  - "Main/_Module/ModuleData/**/*.json"
  - "Main/_Module/ModuleData/characters/**"
  - "Main/_Module/ModuleData/factionmap/**"
---

# XML Data File Rules

## File Types
- **XSLT transforms** (`*.xslt`) — Modify vanilla XML at load time (see xslt.md rule)
- **New entity XML** (`characters/*.xml`, `taom_*.xml`) — Entities not in vanilla
- **JSON config** (`factionmap/*.json`) — Feature-specific data

## Culture NPC Naming Convention
Each culture has 26 notable NPCs in `characters/npcs_{culture}.xml`:
- `spc_notable_{culture}_0` through `_4b` — Merchants (10)
- `spc_notable_{culture}_5/_6/_7` — Preachers (3)
- `spc_notable_{culture}_8/_9` — Artisans (2)
- `spc_notable_{culture}_gl1/_10/_11/_gl4/_12/_13` — Gang Leaders (6)
- `spc_notable_{culture}_21/_22` — Rural Notables (2)
- `spc_{culture}_headman_1/_2/_3` — Headmen (3)

**TWO-LAYER REGISTRATION (mandatory):** NPCs with `is_template="true"` are only reachable when the culture's `<notable_templates>` block in `Main/_Module/ModuleData/taom_spcultures.xml` (or its XSLT override) lists them via `<template name="NPCCharacter.<id>" />`. Adding an NPC to `npcs_{culture}.xml` is necessary but **not sufficient** — both layers are required, or the engine ignores the new NPC and reuses an existing template (producing clone notables with identical names/traits).

To extend a pool (e.g. add a 3rd Rural Notable `_23` to support a higher notable-count target):
1. Define `<NPCCharacter id="spc_notable_{culture}_23" …>` in `characters/npcs_{culture}.xml`.
2. Add `<template name="NPCCharacter.spc_notable_{culture}_23" />` inside that culture's `<notable_templates>` block in `taom_spcultures.xml`.

The same applies to additional Preachers, Headmen, or any new notable beyond the base 26. Why: the engine populates the spawn pool from `<notable_templates>` (read by vanilla `NotablesCampaignBehavior` / `HeroCreator.CreateNotable`), NOT by enumerating `npcs_*.xml`. RCA: `docs/reviews/rca-cultural-feats-3pack-2026-05-31.md`. Memory: `feedback_notable_template_two_layer_registration`.

## Culture Attribute References
Culture XML attributes (`merchant_notary`, `artisan_notary`, etc.) must reference the FIRST NPC of each occupation type.

## Region Codes
EN=Rohan, ES=Mordor, EW=Gondor, A=Harad, B=Dunland, V=Vlandia, K=Easterlings, S=Dale/North, DG=Dol Guldur, E=Erebor, G=Gundabad, I=Isengard, L=Lothlorien, M=Mirkwood, R=Rivendell, RU=Rhun, U=Umbar, MM=Misty Mountain Orcs, GT=Goblins (Goblin-town, settlements), LN=Lindon

**Lord/hero id region prefixes** (`lord_<CODE><clanN>_<lordN>`) differ from settlement codes for the new orc kingdoms: Misty Mountain Orcs lords use `MM`, **Goblin lords use `GB`** (settlements use `GT`), Lindon lords use `LN`. Goblin's settlement code (`GT`) and lord code (`GB`) are independent id-spaces.

## Config ID Cross-Reference (MANDATORY)

After writing ANY XML/JSON config containing culture, kingdom, or settlement IDs, cross-reference EVERY ID against this table before moving on.

> **This section shipped a bug on 2026-07-16 because its own `paths:` excluded the file that broke it.** BannerBearers keyed a culture map on `rohan`/`dale`/`khand`/`dunland`/`harad`/`rhun` — the exact six names the "Common mistake" line below names as WRONG — in `banner_bearers_config.json`. The rule said "ANY XML/JSON config" while the glob was `**/*.xml`, so it never loaded for the `.json` file (58 of TAOM's 59 ModuleData JSON configs were outside the trigger). Fixed by adding `**/*.json`. **Prose scope and glob scope must agree**: if a rule says it governs a file type, its `paths:` must actually match that file type. When keying a config on entity ids, also ship a test asserting every KEY resolves — a dead dictionary key is silent at every layer. See `docs/reviews/rca-banner-bearers-2026-07-16.md`.

### Culture StringIds (runtime values)

| Type | StringIds | Note |
|------|-----------|------|
| **Custom cultures** | `gondor`, `mordor`, `erebor`, `rivendell`, `lothlorien`, `mirkwood`, `isengard`, `gundabad`, `dolguldur`, `umbar` | Use LOTR names |
| **XSLT cultures** | `vlandia` (Rohan), `empire` (Dunland), `aserai` (Harad), `khuzait` (Easterlings), `sturgia` (Dale), `battania` (Khand) | Use vanilla engine IDs |

**Common mistake:** Writing lore names for XSLT cultures. `rohan` is WRONG — use `vlandia`. `dunland` is WRONG — use `empire`. `harad`/`rhun`/`dale`/`khand` are WRONG — use `aserai`/`khuzait`/`sturgia`/`battania`.

### Checklist

| Step | What to check |
|------|---------------|
| 1 | Every `culture=` attribute uses a StringId from the table above |
| 2 | Every `kingdom=` attribute uses a kingdom ID from CLAUDE.md cheatsheet |
| 3 | Every `settlement=` attribute exists in `settlements.xml` |
| 4 | Every `troop=` attribute exists in `troops/troops_{culture}.xml` |

### Why this matters

This exact bug pattern has been caught in 5+ Codex reviews. Custom cultures happen to use LOTR names as StringIds, which makes it easy to assume ALL cultures do — but XSLT cultures inherit vanilla engine IDs.

## EquipmentRosters Schema (MANDATORY for `equipmentsets/*.xml`)

The standalone `<EquipmentRosters>` pattern (used by `Main/_Module/ModuleData/equipmentsets/taom_equipment_sets_*.xml`, mirroring vanilla `SandBoxCore/ModuleData/sandboxcore_equipment_sets.xml`) requires:

| Roster purpose | Inner `<EquipmentSet>` opening tag |
|---|---|
| **Battle** (default) | `<EquipmentSet>` — implicit, no attribute |
| **Civilian** | `<EquipmentSet equipmentType="Civilian">` — REQUIRED |

Without `equipmentType="Civilian"` on a civilian roster, the engine treats it as battle equipment regardless of the roster ID containing `_civ_` / `_civ_equipment`. There is NO `equipmentType="Battle"` in vanilla (verified zero matches across SandBoxCore) — battle is the implicit default.

**The `<EquipmentRoster>` element itself also needs `culture="Culture.<id>"`** (1.4.3+ — mirror vanilla; see [docs/migration/templates/equipment-rosters.md](../../docs/migration/templates/equipment-rosters.md) attribute table). The engine logs `EquipmentRoster with id: <id> don't have culture definition` once per roster at load when it's absent. Non-fatal — the roster still loads, and code-driven lookups (e.g. `child_education_equipments_*` resolved by `EducationCampaignBehavior`) key off the id, not the resolved culture — but it's per-roster log noise and a template gap. For the `_<culture>`-suffixed education/child/wanderer rosters the value is the id's final token (`Culture.gundabad`). This bit `taom_education_equipment_templates.xml`: **980** rosters shipped without it (fixed 2026-06-22 via `tools/add_education_roster_cultures.py`; the orc-culture set added in #267 inherited the omission). When you author or mirror a standalone roster, set BOTH the roster `culture=` AND each civilian `<EquipmentSet equipmentType="Civilian">`.

**Why this matters:** Encyclopedia portraits, settlement-walk views, dialog scenes, and random equipment selection at hero spawn all key off this attribute, not off the roster ID. A misclassified civilian roster manifests as the wrong outfit in non-combat contexts — exactly the Faramir/Boromir bug pattern (memory: feedback_equipmenttype_civilian_required.md).

**Catch list before commit** — when editing any `taom_equipment_sets_*.xml`:
1. Grep for `<EquipmentRoster id="[^"]*_civ` matches in the file.
2. Verify each match's next `<EquipmentSet>` line has `equipmentType="Civilian"`.
3. Quick validator:
   ```powershell
   Get-ChildItem Main\_Module\ModuleData\equipmentsets\taom_equipment_sets_*.xml | ForEach-Object {
     $x = [xml](Get-Content $_.FullName -Raw)
     $civ = $x.SelectNodes('//EquipmentRoster[contains(@id, ''_civ'')]/EquipmentSet')
     $t = ($civ | Where-Object { $_.equipmentType -eq 'Civilian' }).Count
     Write-Host "$($_.Name): $t/$($civ.Count) civilian sets tagged"
   }
   ```
   Should report N/N for every file.

**This rule is for the STANDALONE roster pattern only.** Inline equipment under `<NPCCharacter><Equipments>...</Equipments>` (in `characters/*.xml` and `troops/troops_*.xml`) uses a different attribute (`civilian="true"` on `<EquipmentRoster>`) — that pattern is governed separately and is NOT affected by this rule.

## Townsfolk/notables need a plain (battle) roster too — arena spectators (#295)

Arena stand spectators are the settlement culture's `townsman`/`townswoman` + notables, spawned engine/scene-side with **battle** equipment. An `<NPCCharacter>` whose inline `<Equipments>` block is **civilian-only** (`<EquipmentRoster civilian="true">` with NO plain `<EquipmentRoster>`) has an empty `FirstBattleEquipment` → it spawns **naked in the arena**. The town walk uses *civilian* equipment, so the same character looks fine there — that asymmetry is why the bug was arena-only and every-culture. Every TAOM culture's townsfolk/notables shipped civilian-only and were naked in every arena until `tools/add_townsfolk_battle_rosters.py` appended a battle twin of each civilian roster (1089 NPCs / 20 cultures).

**When authoring a new townsfolk/notable NPCCharacter, add a plain `<EquipmentRoster>` (mirroring the civilian one) in addition to the `civilian="true"` one — or re-run `tools/add_townsfolk_battle_rosters.py`.**

Aside (for future arena/crowd debugging): `CharacterSpawner.InitWithCharacter` is the UI **tableau** spawner (encyclopedia / clan / character-creation previews), NOT the arena stand crowd — the arena spectators are real `Mission.Agent`s spawned engine/scene-side. Don't chase `CharacterSpawner` for arena render bugs.

## Formatting
- 2-space indentation (per .editorconfig)
- UTF-8 encoding
- CRLF line endings
