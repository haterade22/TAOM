# TAOM Project Memory

## User & Preferences
- [user_profile.md](user_profile.md) — Mike's role, expertise, development style preferences

## Feedback (Correction Patterns)
- [feedback_sandboxcore.md](feedback_sandboxcore.md) — Always use SandBoxCore (not SandBox) for vanilla XML reference
- [feedback_xslt_passthrough.md](feedback_xslt_passthrough.md) — XSLT must pass through all vanilla attributes

## External References
- [reference_bannerlord_docs.md](reference_bannerlord_docs.md) — Modding docs, decompilation tools, vanilla data sources

## Data Structures
- [settlements-notes.md](settlements-notes.md) — Settlement XML format and binary distance cache
- [lords-system.md](lords-system.md) — Lords rebalancing: 2 files, 914 lords, 12 archetypes, 13 cultures

## Key Learnings

### PowerShell Gotchas
- **`-match` is case-insensitive by default** in PowerShell. Use `-cmatch` for case-sensitive matching. This caused `_l[0-9]$` to incorrectly match L-region settlement IDs (e.g., `town_L1` matched as sub-component).
- **PowerShell variable names are case-insensitive**. `$ExistingSettlements` (parameter) and `$existingSettlements` (hashtable) collide. Rename one.

### Scene Entity Parsing
- `scene.xscene` has nested `<game_entity>` elements. Using a single greedy/lazy regex `(?s)<game_entity.*?<transform` causes parent entities (without own `<transform position=`) to "steal" child transforms. Fix: two-step approach - find each `<game_entity name="X">`, then search forward within 500-char window for `<transform position=` before any nested `<game_entity`.
- Settlement entity naming: `village_A15_1` can be bound to `castle_A15` (not just `town_A15`). The parent derivation needs castle fallback for regions where towns and castles overlap in numbering.

### Settlements Structure
- See [settlements-notes.md](settlements-notes.md) for detailed format
- Script at `tools/Generate-Settlements.ps1` generates from scene.xscene
- Output: `Main/_Module/ModuleData/settlements.xml` (658 settlements)
- 2 skipped: castle_village_EN8_1, EN8_2 (parent castle_EN8 not in scene)

### Distance Cache Format
- Binary file at `TAOM_Map/ModuleData/DistanceCaches/settlements_distance_cache_Default.bin` (7.83 MB)
- Layout: 8-byte header, Int32 count (862), then per-settlement records of length-prefixed string pairs + float distances
- 862 settlements in cache vs 658 in settlements.xml (cache includes all modules on the map)
- Distances sorted by proximity; ~830 pairs per settlement
- See [settlements-notes.md](settlements-notes.md) for full binary format details

### Notable Templates (Culture NPCs)
- 10 custom cultures in `taom_spcultures.xml`, 6 XSLT cultures in `spcultures.xslt`
- Each custom culture has 26 notary NPCs in `characters/npcs_{culture}.xml` matching vanilla distribution: 10 Merchant, 3 Preacher, 2 Artisan, 6 GangLeader, 2 RuralNotable, 3 Headman
- NPC naming: `spc_notable_{culture}_0` through `_4b` (merchants), `_5/_6/_7` (preachers), `_8/_9` (artisans), `_gl1/_10/_11/_gl4/_12/_13` (gang leaders), `_21/_22` (rural notables), `spc_{culture}_headman_1/_2/_3`
- Culture attributes (`merchant_notary`, `artisan_notary`, etc.) reference the first NPC of each occupation (e.g., `_0`, `_8`, `_5`, `_21`)
- XSLT cultures (Dunland, Harad, Rohan, Rhun, Barding, Variag) pass through vanilla notables — have custom notary NPCs in character files but NOT yet wired into XSLT

### Region Codes
EN=Empire North (Rohan), ES=Empire South (Mordor), EW=Gondor, A=Aserai (Harad), B=Battania (Dunland), V=Vlandia, K=Khuzait (Easterlings), S=Sturgia (Dale/North), DG=Dol Guldur, E=Erebor, G=Gundabad, I=Isengard, L=Lothlorien, M=Mirkwood, R=Rivendell, RU=Rhun, U=Umbar
