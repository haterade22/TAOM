# Troop Weight System

## Overview

Elite and supernatural units consume more party capacity than standard troops. A cave troll takes 4 party slots, Rivendell elves take 2 each, and legendary commanders take 3. This prevents players from fielding armies composed entirely of elite units, encouraging balanced army compositions that fit Middle-earth lore.

## Why This Exists

- **Vanilla behavior:** All troops count as 1 party member regardless of power level. A party of 100 cave trolls uses the same capacity as 100 peasant militia.
- **TAOM requirement:** LOTR factions have wildly different power tiers. Elven warriors are individually far more powerful than orc grunts. Without constraints, players (and AI) would always recruit the highest-tier units, making army composition meaningless.
- **Without this feature:** Players can field 100+ Rivendell blademasters or cave trolls, trivializing combat and breaking the intended faction asymmetry where evil factions rely on numbers and good factions on quality.

## Architecture

### Design Challenge

Bannerlord's party size system is deeply integrated — `PartyBase.NumberOfAllMembers` and related properties are called hundreds of times per campaign tick for movement, wages, party limit warnings, and AI decisions. The solution must:
1. Be performant (called very frequently)
2. Never decrease the member count (would break game systems expecting raw count)
3. Update UI consistently (recruitment screen, party management screen)
4. Be toggleable (MCM setting for players who don't want this restriction)

### Solution Approach

Four Harmony postfix/prefix patches intercept the property getters that return party member counts. When the weighted count exceeds the raw count, the patch increases `__result`. This approach modifies the *perceived* party size without changing actual troop storage, so all vanilla systems (recruitment, AI, save/load) work unchanged.

Patches target `PartyBase`-level getters only (not `TroopRoster` getters) to avoid firing on every roster in the game (prisoner, garrison, temp rosters). Two additional UI patches ensure the recruitment screen and party management screen display the correct weighted counts.

### Component Diagram

```
troop_weights.xml
        |
  TroopWeightXmlLoader (IPathService for path resolution)
        |
  TroopWeightService (caches weights by StringId)
       / \
      /   \
PartyBase    UI Hooks
Hooks (2)    (2 - Recruitment + Party VM)
      \   /
       \ /
  Harmony Patches (Patch17_TroopWeight)
  [TaomSettings.EnableTroopWeight guard]
```

## Configuration

### Config File: `Main/_Module/ModuleData/TroopWeights/troop_weights.xml`

Simple XML format with one element per weighted troop. Any troop not listed defaults to weight 1.0.

```xml
<TroopWeights>
    <TroopWeight id="cave_troll" weight="4.0" />
    <TroopWeight id="imladris_blademaster" weight="2.0" />
</TroopWeights>
```

| Attribute | Type | Description |
|-----------|------|-------------|
| `id` | string | NPCCharacter StringId (case-insensitive) |
| `weight` | float | Party capacity multiplier (must be > 0) |

### Current Weight Tiers

| Weight | Count | Troop Types |
|--------|-------|-------------|
| 4.0 | 1 | Cave trolls |
| 3.0 | 7 | Rivendell Gondolin units (5), Mirkwood palace guard (2), Erebor royal elite (2) |
| 2.0 | ~70 | All Imladris/Mirkwood elves, warg riders (all cultures), Black Numenoreans, Khamul's elite, Dol Guldur uruk black guard, Mordor elite captains, Orthanc guard, Erebor oathsworn |
| 1.0 | default | All standard human/orc/goblin infantry, archers, militia, cavalry |

### MCM Setting

`TaomSettings.EnableTroopWeight` (default: `true`) — toggleable at runtime, checked by every patch before executing.

## Key Files

| File | Purpose |
|------|---------|
| `Main/Features/TroopWeight/ITroopWeightService.cs` | Service interface: `GetTroopWeight(string)`, `CalculateWeightedMemberCount(PartyBase)`, etc. |
| `Main/Features/TroopWeight/TroopWeightService.cs` | Core implementation with `Dictionary<string, float>` cache (case-insensitive) |
| `Main/Features/TroopWeight/ITroopWeightXmlLoader.cs` | Loader interface |
| `Main/Features/TroopWeight/TroopWeightXmlLoader.cs` | XML parser using `IPathService`, graceful degradation on missing file |
| `Main/Features/TroopWeight/TroopWeightIoC.cs` | `RegisterTroopWeightFeature()` + `InitializeHooks()` |
| `Main/Features/TroopWeight/Hooks/IOn*.cs` | 4 hook interfaces (2 PartyBase + 2 UI) |
| `Main/Features/TroopWeight/Hooks/*Hook.cs` | 4 hook implementations (2 PartyBase + 2 UI) |
| `Main/Features/TroopWeight/Hooks/*_Patch.cs` | 4 Harmony patches (Patch17_TroopWeight) |
| `Main/_Module/ModuleData/TroopWeights/troop_weights.xml` | Weight definitions (~80 entries) |
| `Main/Features/TaomSettings.cs` | MCM toggle (`EnableTroopWeight`) |

## Dependencies

- `IPathService` (Core/Infrastructure) — Resolves `ModuleDataPath` for XML file location
- `IModLogger` (Core/Logging) — Error/warning logging
- `TaomSettings` (Features) — MCM toggle check in every patch

## Tests

- `TAOM.Tests/Features/TroopWeight/TroopWeightServiceTests.cs` — 9 tests covering null/empty/known/unknown IDs, caching, case insensitivity, cache clearing
- `TAOM.Tests/Features/TroopWeight/TroopWeightXmlLoaderTests.cs` — 10 tests covering valid XML, missing file, lazy loading, duplicates, zero/negative weights, missing attributes, invalid values, case insensitivity, reload

## How to Add a New Weighted Troop

1. Open `Main/_Module/ModuleData/TroopWeights/troop_weights.xml`
2. Add a `<TroopWeight id="troop_string_id" weight="2.0" />` element
3. The `id` must match the NPCCharacter's `id` attribute in the troop XML files (case-insensitive)
4. No code changes needed — the loader picks up new entries on next game load
5. To force a mid-game reload, the `TroopWeightXmlLoader.ReloadWeights()` method is available but not currently exposed via UI

## How to Add a New Weight Tier

Weight values are continuous floats — any positive value works. Common tiers:
- `1.0` — Standard (default for unlisted troops)
- `2.0` — Elite (occupies 2 party slots)
- `3.0` — Legendary (occupies 3 party slots)
- `4.0` — Monster (occupies 4 party slots)

## Performance

- **Troop weight lookup:** `Dictionary<string, float>` eagerly populated at startup — O(1) per troop, no lazy caching or writes on hot path
- **Party member count cache:** `Dictionary<int, (int Version, float Weight)>` in `PartyBaseNumberOfAllMembersHook` — keyed by party hash, invalidated by `TroopRoster.VersionNo` changes, trims 25% at 2000 entries
- **PartyBase-only patching:** Patches target `PartyBase.NumberOfAllMembers` / `NumberOfRegularMembers` only, NOT `TroopRoster.TotalManCount` / `TotalHealthyCount`. TroopRoster getters fire for every roster in the game (prisoners, garrisons, temp rosters); patching them caused IndexOutOfRange on partially-initialized rosters during game loading.
- **Single-threaded:** All caches use `Dictionary` (not `ConcurrentDictionary`) because the campaign tick loop is single-threaded

## GitHub Issues

- **Feature:** #41 — [feat: Troop Weight System — Elite unit party capacity](https://github.com/haterade22/TAOM/issues/41) — Closed
- **Bug fix:** #45 — [fix: TroopWeight crashes and freezes from TroopRoster-level patches](https://github.com/haterade22/TAOM/issues/45) — Closed
