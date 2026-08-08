# LOTR-Themed Minor Factions

## Overview

All 14 vanilla Bannerlord minor factions (mercenaries, mafias, sects, nomads) have been renamed and re-lored to fit Middle-earth. Each faction retains its vanilla mechanical type and leader NPCs but receives a new LOTR-appropriate name, description, and (where needed) settlement and culture assignment.

## Why This Exists

- **Vanilla behavior:** Minor factions have Calradian names and lore (Ghilman, Skolderbroda, etc.)
- **TAOM requirement:** Every faction in the game world should feel like it belongs in Middle-earth
- **Without this feature:** Players encounter immersion-breaking Calradian faction names in a LOTR total conversion

## Architecture

### Design Challenge

Minor factions are defined in SandBox/ModuleData/spclans.xml. TAOM cannot replace this file entirely (other modules depend on it), so changes must be applied via XSLT transformation at load time.

### Solution Approach

XSLT templates in `spclans.xslt` match each minor faction by ID and override specific attributes (`name`, `short_name`, `text`) while passing through all other vanilla attributes and child elements unchanged. For factions that need geographic relocation, `initial_home_settlement` is also overridden. One faction (`brotherhood_of_woods`) also has its `culture` changed.

### Component Diagram

```
SandBox/spclans.xml (vanilla)
        |
  spclans.xslt (TAOM XSLT transform)
        |
  Engine loads transformed XML
        |
  taom_module_strings.xml (localization keys)
```

## The 14 Factions

### Mercenary Clans (`is_clan_type_mercenary`)

| Vanilla Name | LOTR Name | Culture | Home Settlement | Description |
|-------------|-----------|---------|-----------------|-------------|
| Ghilman | **Serpent Guard** | darshi | castle_A7 (Harad) | Haradrim mounted mercenaries sworn to the Serpentlord |
| Legion of the Betrayed | **The Grey Company** | empire | castle_village_EW2_2 (Gondor) | Last brotherhood of the Dúnedain Rangers |
| Skolderbroda | **Axemen of Erebor** | nord | castle_S1 (Dale) | Dwarven veterans fighting in shield wall with axe and mattock |
| Company of the Golden Boar | **Corsair Blades** | vlandia | castle_V3 | Umbar ex-pirates fighting as crossbowmen on land |

### Mafia Factions (`is_mafia`, `is_outlaw`)

| Vanilla Name | LOTR Name | Culture | Home Settlement | Description |
|-------------|-----------|---------|-----------------|-------------|
| Beni Zilal | **The Blind Eye** | aserai | village_A3_1 (Harad) | Haradrim smuggler-assassin brotherhood |
| Wolfskins | **Variag Ravagers** | battania | castle_K5 (Khand) | Young Variag warriors on ritual wildling raids |
| Brotherhood of the Woods | **Dunlending Reavers** | empire (**changed**) | castle_EN3 (**changed**, Tûr Morva) | Hill-clan remnants driven from their lands by Rohirrim ancestors |
| Hidden Hand | **The Mouth's Servants** | empire | castle_ES6 (Mordor) | Mordor intelligence network beyond the Black Gate |
| Lake Rats | **Wreckers of the Long Lake** | sturgia | village_S4_1 (Dale) | Esgaroth smugglers who lure barges onto shoals |

### Sect (`is_sect`, `is_outlaw`)

| Vanilla Name | LOTR Name | Culture | Home Settlement | Description |
|-------------|-----------|---------|-----------------|-------------|
| Embers of the Flame | **Cult of the Lidless Eye** | empire | castle_EN5 (Dunland) | Black Númenórean sect venerating Sauron |

### Nomad Factions (`is_nomad`, `is_outlaw`)

| Vanilla Name | LOTR Name | Culture | Home Settlement | Description |
|-------------|-----------|---------|-----------------|-------------|
| Jawwal | **The Sand-Riders** | aserai | town_A2 (Harad) | Nomadic desert Haradrim demanding caravan tribute |
| Karakhergit | **The Wild Easterlings** | khuzait | castle_RU10 (**changed**, Nîrakh) | Unconquered steppe clans of Rhûn |
| Forest People | **The Drúedain** | vakken | village_V1_1 | Ancient Woses of Drúadan Forest |
| Eleftheroi | **The Beornings** | empire | castle_M1 (**changed**, Glad Thaw) | Skin-changers and woodmen of the Anduin vale |

## Key Files

| File | Purpose |
|------|---------|
| `Main/_Module/ModuleData/spclans.xslt` | 14 XSLT templates overriding faction names, descriptions, settlements |
| `Main/_Module/ModuleData/taom_module_strings.xml` | 42 localization strings (name + short_name + text per faction) |

## Dependencies

- SandBox/ModuleData/spclans.xml (vanilla source data)
- Vanilla `minor_faction_character_templates` NPC definitions (4 leader NPCs per faction, unchanged)
- Vanilla party templates for each faction type (unchanged)

## Configuration

No runtime configuration. All data is in the XSLT and string table. To change a faction's name or description, edit the corresponding XSLT template in `spclans.xslt` and the matching string entry in `taom_module_strings.xml`.

## How to Add or Modify a Minor Faction

1. Find the vanilla faction ID in `SandBox/ModuleData/spclans.xml`
2. Add/update an `<xsl:template match="Faction[@id='...']">` in `spclans.xslt`
3. Use `<xsl:apply-templates select="@*[local-name() != 'name' and ...]"/>` to exclude attributes you're overriding
4. Add `<xsl:attribute name="...">` for each override
5. Always end with `<xsl:apply-templates select="node()"/>` to pass through child elements
6. Add corresponding `<string id="TAOM_..." text="..."/>` entries in `taom_module_strings.xml`

## Design Decisions

- **No culture changes except Dunlending Reavers** — changing culture without matching party templates risks broken spawns
- **Corsair Blades keep vlandia culture** — no Umbar mercenary party template exists yet
- **Drúedain keep vakken culture** — vanilla passthrough, lore-accurate cultural distinctness
- **Vanilla leader NPCs preserved** — the 4 leader character templates per faction pass through unchanged (visual appearance is still vanilla)

## Tests

No C# code changed — no unit tests applicable. Verified via:
- XSLT passthrough validation (all vanilla attributes preserved)
- Build succeeds (0 errors)
- String key format matches TAOM conventions

## Changelog

- 2026-04-06 — All 14 vanilla minor factions replaced with lore-appropriate Middle-earth equivalents via `spclans.xslt` (14 templates) + `taom_module_strings.xml` (42 strings); 3 settlement remaps and the Dunlending Reavers vlandia→empire culture change.
- 2026-03-26 — Fixed `NullReferenceException` at `CharacterObject.get_StealthEquipments()` when spawning minor faction heroes (e.g. Ghilman) on new-game start, by setting `default_stealth_equipment_roster` in the 4 XSLT culture templates (`spcultures.xslt`).

## GitHub Issue

- **Issue:** #71 — feat: LOTR-themed minor factions (mercenaries, mafias, sects, nomads)
- **Status:** Closed (2026-08-08 issue triage)

---

<!-- backlinks-start auto-generated; edit lint_docs.py / build_backlinks.py to change -->

## Referenced by

- [docs/INDEX.md](../INDEX.md)

<!-- backlinks-end -->
