---
paths:
  - "Main/_Module/ModuleData/**/*.xml"
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

## Culture Attribute References
Culture XML attributes (`merchant_notary`, `artisan_notary`, etc.) must reference the FIRST NPC of each occupation type.

## Region Codes
EN=Rohan, ES=Mordor, EW=Gondor, A=Harad, B=Dunland, V=Vlandia, K=Easterlings, S=Dale/North, DG=Dol Guldur, E=Erebor, G=Gundabad, I=Isengard, L=Lothlorien, M=Mirkwood, R=Rivendell, RU=Rhun, U=Umbar

## Config ID Cross-Reference (MANDATORY)

After writing ANY XML/JSON config containing culture, kingdom, or settlement IDs, cross-reference EVERY ID against this table before moving on.

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

## Formatting
- 2-space indentation (per .editorconfig)
- UTF-8 encoding
- CRLF line endings
