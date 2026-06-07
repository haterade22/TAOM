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

## Phantom-Wounded Display Fix (2026-06-07)

### The bug

A brand-new campaign showed the player party as **"62 troops / 16 wounded"** with no battle fought. The wounds were **phantom**. The party genuinely had **46 soldiers, 0 wounded**, that *weighed* 62 toward the 23 cap because some were weight-≥2 troops.

Vanilla derives the displayed wounded count by subtracting two sibling getters:

```
wounded = PartyBase.NumberOfAllMembers - PartyBase.NumberOfHealthyMembers
```

This feature weights `NumberOfAllMembers` (→ 62) but deliberately leaves `NumberOfHealthyMembers` **unweighted** (→ 46), because that getter feeds gameplay, not just display. So the weight surplus (62 − 46 = 16) rendered as phantom wounds. A weight-2 troop adds 2 to `NumberOfAllMembers` but 1 to `NumberOfHealthyMembers`; the gap is the phantom count.

### Why the getter is NOT weighted (the fix is display-only)

Weighting `NumberOfHealthyMembers` globally would be the tidy fix but is **gameplay-dangerous**. Decompile-verified consumers that would break: `PartyGroupTroopSupplier` (battle troop supply), `MapEventParty._healthyManCountAtStart` + `DisorganizedStateCampaignBehavior` (casualty tracking), `DefaultTroopSacrificeModel` (sacrifice limit — would let you sacrifice more men than you have), `DefaultInventoryCapacityModel`, `DefaultPartyDesertionModel`, battle strength/winner determination. So the fix touches **display only**.

### The four display surfaces fixed

All four compute `NumberOfAllMembers − NumberOfHealthyMembers`. Each gets a display-only Postfix in `Patch17_TroopWeight` that rewrites the shown numbers with a weighted (healthy, wounded) split via `ITroopWeightService.GetWeightedHealthAndWounded`, so **battle-ready + wounded equals the weighted member total** the panel header already shows (e.g. "Battle Ready 62 / Wounded 0", matching "62/23").

| Surface | Vanilla method | What the Postfix rewrites |
|---------|----------------|---------------------------|
| Main party HUD health tooltip | `CampaignUIHelper.GetMainPartyHealthTooltip` | "Battle Ready Troops" + "Wounded Troops" values; strips the spurious healing-rate block when weighted wounded == 0 |
| Any-party health tooltip | `CampaignUIHelper.GetPartyHealthTooltip(PartyBase)` | Same |
| Encounter "X vs Y" menu item | `GameMenuPartyItemVM.RefreshCounts` | `PartySize` / `PartyWoundedSize` / `PartySizeLbl` |
| Party map nameplate text | `Helpers.PartyBaseHelper.GetPartySizeText(PartyBase)` | Rebuilds the `str_party_health` TextObject with weighted `HEALTHY_NUM` / `WOUNDED_NUM` |

All four run for **every** party (the `NumberOfAllMembers` weighting is not main-party-only), so enemy/ally party tooltips and nameplates with heavy troops are corrected too. All four gate on `TaomSettings.EnableTroopWeight` — toggling the feature off reverts every surface to vanilla.

### Known property: separate-ceiling rounding

`GetWeightedHealthAndWounded` ceilings weighted healthy and weighted wounded **independently** (matching the existing `PartyVMPopulatePartyListLabelHook`). For **integer** weights — all TAOM ships — `healthy + wounded` exactly equals the weighted member total. With *fractional* weights and mixed wound states, the two ceilings can sum to 1 above `Ceiling(total)`, making the tooltip read 1 higher than the panel header. Cosmetic-only; documented rather than "fixed" because changing it would make the tooltip disagree with the party-list label.

### Performance

`GetWeightedHealthAndWounded` walks the roster (allocation-free — no intermediate collection) and caches the result per party in a `ConditionalWeakTable<PartyBase, box>` keyed by `MemberRoster.VersionNo`. The weak table is reference-keyed (no `GetHashCode` collisions) and auto-evicts on party GC (no unbounded growth — unlike the `Dictionary<int,...>` caches in the count hooks). `VersionNo` is decompile-verified to bump on wound/heal (`TroopRoster.AddToCountsAtIndex` → `UpdateVersion()` when `woundedCountChange != 0`), so the cached wounded count is never stale after a battle.

RCA: [`docs/reviews/rca-troopweight-phantom-wounded-2026-06-07.md`](../reviews/rca-troopweight-phantom-wounded-2026-06-07.md).

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

> **Note (2026-05-14):** The `cave_troll` TroopWeight is currently commented out in `troop_weights.xml` (WIP — see CHANGELOG "Phase 9c — Disable troll content in-place"). The 4.0 tier remains documented here for reference and will reactivate when the troop is re-enabled.

| Weight | Count | Troop Types |
|--------|-------|-------------|
| 4.0 | 1 | Cave trolls (currently disabled) |
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
| `Main/Features/TroopWeight/Hooks/IOn*.cs` | 8 hook interfaces (2 PartyBase + 2 party/recruitment UI + 4 phantom-wounded display) |
| `Main/Features/TroopWeight/Hooks/*Hook.cs` | 5 hook implementations (the original 4 + `TroopWeightDisplayHook`, which implements the 4 display interfaces) |
| `Main/Features/TroopWeight/Hooks/*_Patch.cs` | 8 Harmony patches (all `Patch17_TroopWeight`): 2 PartyBase getters + RecruitmentVM + PartyVM + the 4 phantom-wounded display patches |
| `Main/_Module/ModuleData/TroopWeights/troop_weights.xml` | Weight definitions (~80 entries) |
| `Main/Features/TaomSettings.cs` | MCM toggle (`EnableTroopWeight`) |

## Dependencies

- `IPathService` (Core/Infrastructure) — Resolves `ModuleDataPath` for XML file location
- `IModLogger` (Core/Logging) — Error/warning logging
- `TaomSettings` (Features) — MCM toggle check in every patch

## Tests

- `TAOM.Tests/Features/TroopWeight/TroopWeightServiceTests.cs` — covers null/empty/known/unknown IDs, caching, case insensitivity, cache clearing, PLUS the `ComputeWeightedHealthyAndWounded` phantom-wounded core: the regression case (weight-2 troops, 0 real wounds → 0 wounded), the `healthy + wounded == weighted total` invariant, real-wounded weighting, empty/null, negative-wounded, wounded>number floor, and fractional-weight ceiling
- `TAOM.Tests/Features/TroopWeight/TroopWeightXmlLoaderTests.cs` — 10 tests covering valid XML, missing file, lazy loading, duplicates, zero/negative weights, missing attributes, invalid values, case insensitivity, reload
- `TAOM.Tests/Features/TroopWeight/TroopWeightHooksTests.cs` — construction / interface / null-tolerance for all hook implementations including `TroopWeightDisplayHook` (the display hooks touch sealed TaleWorlds types, so behavior is verified in-game; the weighted math is fully unit-tested via the service core)

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

---

<!-- backlinks-start auto-generated; edit lint_docs.py / build_backlinks.py to change -->

## Referenced by

- [docs/INDEX.md](../INDEX.md)

<!-- backlinks-end -->
