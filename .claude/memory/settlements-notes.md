# Settlements - Detailed Notes

## settlements.xml Structure

**Path**: `Main/_Module/ModuleData/settlements.xml` (658 settlements)

### Settlement Types

| Type | ID Prefix | Component | Key Attributes |
|------|-----------|-----------|----------------|
| Town | `town_` | `<Town is_castle="false">` | owner, posX/Y, gate_posX/Y, culture, prosperity, Buildings, Locations (center/arena/tavern/lordshall/prison/houses/alley) |
| Castle | `castle_` | `<Town is_castle="true">` | Same as town but uses `building_castle_*` IDs, fewer Locations (center/lordshall/prison) |
| Village | `village_` | `<Village>` | village_type, hearth, `bound="Settlement.town_X"`, CommonAreas |
| Castle Village | `castle_village_` | `<Village>` | Same as village but `bound="Settlement.castle_X"` |
| Hideout | `hideout_` | `<Hideout>` | type="Hideout", culture (bandit type), map_icon, single Location (hideout_center) |

### Name Localization

Names use `{=Settlements.Settlement.name.ID}Display Name` format. Text/descriptions use `{=Settlements.Settlement.text.ID}...` or vanilla string IDs like `{=n9WMUuSp}`.

### Village Binding

Villages are bound to parent settlements via `bound="Settlement.castle_X"` or `bound="Settlement.town_X"`. Castle villages always bind to castles. Regular villages bind to towns, but in some regions a village like `village_A15_1` can bind to `castle_A15` (castle fallback).

## Tools

### Generate-Settlements.ps1
- **Path**: `tools/Generate-Settlements.ps1`
- Parses `scene.xscene` for settlement game entities and their positions
- Carries over existing data (names, buildings, hearth, etc.) from TAOM_Map module
- Generates placeholder entries for new settlements
- Outputs `Main/_Module/ModuleData/settlements.xml`
- 2 orphans skipped: `castle_village_EN8_1`, `castle_village_EN8_2` (parent `castle_EN8` not in scene)

### Settlement-Breakdown.ps1
- **Path**: `tools/Settlement-Breakdown.ps1`
- Read-only diagnostic tool
- Loads `settlements.xml`, groups by type (town/castle/village/castle_village/hideout/other)
- Extracts region codes from IDs using `-cmatch '^([A-Z]+)'`
- Outputs counts per region per type, plus summary totals

## Distance Cache Binary Format

**Path**: `TAOM_Map/ModuleData/DistanceCaches/settlements_distance_cache_Default.bin`
**Size**: 7.83 MB

Bannerlord precomputes pathfinding distances between all settlements and caches the results. The cache is regenerated when the map changes.

### Binary Layout

| Offset | Type | Description |
|--------|------|-------------|
| 0 | 8 bytes | Header (hash/version) |
| 8 | Int32 | Settlement count (862) |
| 12+ | Record[] | One record per settlement |

### Record Format

```
[byte strLen] [ASCII string: settlement ID] [0x00 null] [Int32 pairCount] [Pair[] pairs]
```

Each pair:
```
[byte strLen] [ASCII string: target ID] [0x00 null] [float distance]
```

- Distances are sorted by proximity (nearest first)
- Pair count per settlement is ~830 (not exactly count-1; some settlements excluded)

### Key Numbers

- **862** settlements in cache (vs 658 in TAOM's settlements.xml — cache includes all modules on the map)
- **830** distance pairs for `castle_EN3` (first entry)
- Example distances from `castle_EN3`: castle_village_EN3_1 = 16.97, castle_village_EN3_2 = 20.77, castle_EN4 = 26.68
