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

## Formatting
- 2-space indentation (per .editorconfig)
- UTF-8 encoding
- CRLF line endings
