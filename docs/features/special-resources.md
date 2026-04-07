# Special Resources

## Overview

Per-kingdom special resources that gate elite troop upgrades and maintenance. Each kingdom can define a unique resource (e.g., Mordor's "Scraps") earned through combat and spent on T6+ troop upgrades and daily upkeep. Resources are displayed in the map bar and enforced in the party screen.

## Why This Exists

- **Vanilla behavior:** All troop upgrades cost only gold and XP — no faction flavor
- **TAOM requirement:** Elite troops should feel expensive and faction-specific. Mordor orcs scavenge battlefield scraps; dwarves hoard mithril; Gondor earns noble authority
- **Without this feature:** Elite armies are trivially affordable, reducing faction identity and strategic tension

## Architecture

### Design Challenge

Bannerlord has no concept of per-faction resources beyond gold and influence. The party screen upgrade flow is hardcoded to check only gold costs. We need to inject resource checks without breaking vanilla upgrade logic.

### Solution Approach

- **XML-driven config:** Resource definitions and troop costs in sidecar XML files (no recompilation to add kingdoms)
- **CampaignBehavior:** Hooks into DailyTick, MapEventEnded, RaidCompleted, PrisonerTaken for earning
- **Harmony patches (Patch26):** Postfix on PartyCharacterVM.InitializeUpgrades (grey out + hint) and PartyScreenLogic.UpgradeTroop (deduct on upgrade)
- **UIExtenderEx mixin:** MapInfoVM mixin displays resource in map bar with tooltip breakdown
- **SyncData persistence:** Dictionary<string, float> per hero saved via CampaignBehaviorBase.SyncData

### Component Diagram

```
special_resources_config.xml + troop_resource_costs.xml
        |
  SpecialResourceConfigProvider (loads + caches XML)
        |
  SpecialResourceService (earn, spend, validate, daily tick)
       / \         \
      /   \         \
Behavior  Patch26    MapBarMixin
(events)  (party UI) (map bar)
```

## Configuration

### Resource Definitions: `Main/_Module/ModuleData/special_resources/special_resources_config.xml`

| Attribute | Type | Description |
|-----------|------|-------------|
| `id` | string | Unique resource identifier |
| `kingdom_id` | string | Kingdom this resource belongs to |
| `display_name` | string | Shown in UI |
| `icon_sprite` | string | Sprite name for map bar icon |
| `cap` | float | Maximum stockpile |
| `starting_amount` | float | Amount on new game |
| `daily_per_town` | float | Passive daily income per owned town |
| `per_battle_victory_base` | float | Base earned per battle (scaled by enemy ratio) |
| `per_raid` | float | Earned per successful raid |
| `per_siege_victory` | float | Earned per siege victory |
| `per_prisoner` | float | Earned per prisoner taken |

### Troop Costs: `Main/_Module/ModuleData/special_resources/troop_resource_costs.xml`

| Attribute | Type | Description |
|-----------|------|-------------|
| `id` | string | Troop character ID |
| `resource_id` | string | Which resource this costs |
| `upgrade_cost` | int | Resource cost to upgrade TO this troop |
| `daily_upkeep` | float | Daily resource drain per troop |

### Current Values (Mordor Pilot)

- Cap: 500, Starting: 30
- Daily per town: +0.5, Battle: +10 (x enemy ratio), Raid: +8, Siege: +15, Prisoner: +1
- 12 elite troops costed (T6+ melee, ranged, shield, command lines)

## Key Files

| File | Purpose |
|------|---------|
| `Main/Features/SpecialResources/ISpecialResourceService.cs` | Service interface |
| `Main/Features/SpecialResources/SpecialResourceService.cs` | Core logic (earn, spend, validate) |
| `Main/Features/SpecialResources/ISpecialResourceStorageService.cs` | Storage interface |
| `Main/Features/SpecialResources/SpecialResourceStorageService.cs` | In-memory dict persistence |
| `Main/Features/SpecialResources/ISpecialResourceConfigProvider.cs` | Config interface |
| `Main/Features/SpecialResources/SpecialResourceConfigProvider.cs` | XML loader with caching |
| `Main/Features/SpecialResources/SpecialResourcesBehavior.cs` | CampaignBehavior (events + SyncData) |
| `Main/Features/SpecialResources/SpecialResourcesIoC.cs` | DryIoc registrations |
| `Main/Features/SpecialResources/Domain/SpecialResource.cs` | Resource definition record |
| `Main/Features/SpecialResources/Domain/TroopResourceCostEntry.cs` | Per-troop cost record |
| `Main/Features/SpecialResources/Models/TaomSpecialResourceModel.cs` | GameModel facade |
| `Main/Features/SpecialResources/Hooks/PartyCharacterVM_InitializeUpgrades_Patch.cs` | Grey out upgrades |
| `Main/Features/SpecialResources/Hooks/PartyScreenLogic_UpgradeTroop_Patch.cs` | Deduct on upgrade |
| `Main/Features/SpecialResources/Hooks/IOnPartyUpgradeResourceCheck.cs` | Hook interface |
| `Main/Features/SpecialResources/Hooks/PartyUpgradeResourceCheckHook.cs` | Hook implementation |
| `Main/Features/SpecialResources/UI/SpecialResourceMapBarMixin.cs` | Map bar UIExtenderEx mixin |
| `Main/_Module/ModuleData/special_resources/special_resources_config.xml` | Resource definitions |
| `Main/_Module/ModuleData/special_resources/troop_resource_costs.xml` | Troop costs |

## Dependencies

- `IPathService` (Core) — module data path resolution
- `IModLogger` (Core) — logging

## Tests

- `TAOM.Tests/Features/SpecialResources/SpecialResourceServiceTests.cs` — 17 tests (earn, spend, validate, daily tick, cap, edge cases)
- `TAOM.Tests/Features/SpecialResources/SpecialResourceStorageServiceTests.cs` — 7 tests (get/set/add, clamp, multi-hero)

## How to Add a New Kingdom's Resource

1. Add a `<Resource>` row to `special_resources_config.xml` with the kingdom's ID and earning rates
2. Add `<Troop>` rows to `troop_resource_costs.xml` for T6+ troops requiring the resource
3. No C# changes needed — the system is fully data-driven
4. If the kingdom needs unique earning mechanics (e.g., mining), implement `IKingdomResourceStrategy` (Phase 3 pattern)

## Performance

- IoC.Resolve cached in MapBarMixin constructor (not per-refresh)
- No LINQ in hot paths — direct enumeration loops
- List pre-allocated with capacity(8) for tooltip
- String formatting only when amount changes (cached)
- Empty upkeep list reused as static readonly
