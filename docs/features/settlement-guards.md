# Settlement Guards

## Overview

Per-settlement guard customization system that replaces vanilla's culture-only guard spawning with XML-driven troop pools. Each town and castle can define which specific guard troops appear at its spawn points, with a settlement -> clan -> culture fallback chain. Also provides per-culture spear item mapping to replace vanilla's hardcoded two-spear system.

## Why This Exists

- **Vanilla behavior:** All guards in a settlement are drawn from the garrison roster (weighted by level), falling back to `culture.Guard` when the garrison is empty. Every Gondor settlement gets the same `guard_gondor` character.
- **TAOM requirement:** Minas Tirith should have Fountain Guards and Citadel Guards. Osgiliath should have Dome Guards. Dol Amroth should have Swan Guards. Each settlement's guards should reflect its regional identity.
- **Without this feature:** Every settlement of the same culture has identical-looking guards, breaking immersion in a total conversion with 14+ distinct Gondor regions.

## Architecture

### Design Challenge

The guard spawning pipeline in `GuardsCampaignBehavior` (SandBox.dll) has no extension points. The troop selection method `TakeGuardAgentDataFromGarrisonTroopList` is private, and the equipment assembly method `PrepareGuardAgentDataFromGarrison` is private static. Both need interception without breaking the vanilla equipment assembly, dialog, and behavior systems.

### Solution Approach

Two Harmony Prefixes on private methods, manually patched (not via PatchCategory since the target methods require `AccessTools.Method`):

1. **TakeGuardAgentData Prefix** -- Intercepts troop selection. If the settlement has a custom guard pool in the XML config, resolves a troop ID via weighted random selection, then delegates to vanilla's `PrepareGuardAgentDataFromGarrison` (called via cached reflection) for equipment assembly. Falls back to vanilla behavior if no config exists.

2. **GetSuitableSpear Prefix** -- Replaces the vanilla hardcode (`battania -> northern_spear_2_t3`, else `western_spear_3_t3`) with a per-culture lookup from the XML config.

### Component Diagram

```
settlement_guards_config.xml
        |
  SettlementGuardConfigProvider (lazy XML load)
        |
  SettlementGuardService (fallback chain + weighted random)
        |
  GuardsCampaignBehavior_TakeGuardAgentData_Patch (Harmony Prefix)
        |
  vanilla PrepareGuardAgentDataFromGarrison (cached reflection)
        |
  guard spawns in settlement scene
```

## Configuration

### Config File: `Main/_Module/ModuleData/settlement_guards/settlement_guards_config.xml`

Three entity types with a fallback chain:

| Entity | Lookup Key | Priority |
|--------|-----------|----------|
| `<Settlement id="...">` | Settlement StringId | 1 (highest) |
| `<Clan id="...">` | Clan StringId | 2 |
| `<Culture id="...">` | Culture StringId | 3 (lowest) |

Each entity contains a `<Guards>` block with weighted troop entries:

| Attribute | Type | Description |
|-----------|------|-------------|
| `troop` | string | NPCCharacter ID from troops XML |
| `weight` | int | Relative weight for random selection (default: 1) |
| `spawn_points` | comma-separated | Which spawn point types this guard appears at (empty = any) |

Optional `<PrisonGuard troop="..."/>` overrides the culture's default prison guard.

`<Spears>` section maps each culture StringId to a spear item ID for guards with spear override.

### Spawn Point Types

| Spawn Point | Vanilla Role |
|-------------|-------------|
| `sp_guard_castle` | Castle entrance guards (NOT prosperity-scaled) |
| `sp_guard_with_spear` | Standing guards with spear (prosperity-scaled) |
| `sp_guard` | Basic standing guards (prosperity-scaled) |
| `sp_guard_patrol` | Walking patrol routes (prosperity-scaled) |
| `sp_guard_unarmed` | Unarmed wandering guards (prosperity * castle ratio) |

### Current Values (Gondor pilot)

14 settlements configured with region-specific troops:
- Minas Tirith (town_EW1): Fountain Guards, Captains, Sergeants, Veterans
- Osgiliath (town_EW2): Dome Guards, Guards, Infantry, Archers
- Pelargir (town_EW4): Anchor Guards, Sea Guards
- Dol Amroth (town_EW5): Swan Guards, Foot Knights, Veterans, Infantry
- Linhir (town_EW7): Haven Guards, Pavise Guards
- 9 castles with region-specific guards (Cair Andros, Ithilien, Lossarnach, Harondor, etc.)

## Key Files

| File | Purpose |
|------|---------|
| `Main/Features/SettlementGuards/ISettlementGuardService.cs` | Service interface |
| `Main/Features/SettlementGuards/SettlementGuardService.cs` | Fallback chain + weighted selection + spawn-point filtering |
| `Main/Features/SettlementGuards/ISettlementGuardConfigProvider.cs` | Config provider interface |
| `Main/Features/SettlementGuards/SettlementGuardConfigProvider.cs` | Lazy XML loading |
| `Main/Features/SettlementGuards/Domain/GuardEntry.cs` | Troop ID + weight + spawn points |
| `Main/Features/SettlementGuards/Domain/GuardPool.cs` | Collection of guards + optional prison guard |
| `Main/Features/SettlementGuards/Domain/SettlementGuardContext.cs` | Settlement/clan/culture IDs for resolution |
| `Main/Features/SettlementGuards/Hooks/GuardsCampaignBehavior_TakeGuardAgentData_Patch.cs` | Harmony Prefix on troop selection |
| `Main/Features/SettlementGuards/Hooks/GuardsCampaignBehavior_GetSuitableSpear_Patch.cs` | Harmony Prefix on spear item selection |
| `Main/Features/SettlementGuards/SettlementGuardsIoC.cs` | DryIoc Singleton registrations |
| `Main/_Module/ModuleData/settlement_guards/settlement_guards_config.xml` | XML config |

## Dependencies

- `IPathService` (Core) -- ModuleDataPath for config file location
- `IModLogger` (Core) -- Logging
- `IRandomProvider` (TroopProgression) -- Weighted random selection (shared with VolunteerRecruitmentService)

## Tests

- `TAOM.Tests/Features/SettlementGuards/SettlementGuardConfigProviderTests.cs` -- 13 tests: XML parsing, lazy loading, missing file, spear mappings, default weights, multiple entries
- `TAOM.Tests/Features/SettlementGuards/SettlementGuardServiceTests.cs` -- 14 tests: fallback chain (settlement -> clan -> culture -> null), weighted selection, spawn-point filtering, spear resolution
- `TAOM.Tests/Core/ConfigIdValidationTests.cs` -- 11 tests: validates all culture IDs in config against known valid set

## How to Add Guards for a New Settlement

1. Open `Main/_Module/ModuleData/settlement_guards/settlement_guards_config.xml`
2. Add a `<Settlement id="town_XX1">` block (ID must exist in `settlements.xml`)
3. Add `<Guard>` entries with troop IDs from `troops/troops_{culture}.xml`
4. Set weights (higher = more likely to appear) and optional `spawn_points`
5. No code changes needed -- config is lazy-loaded on first settlement entry

To add a culture-level fallback (all settlements of that culture):
1. Add a `<Culture id="{cultureStringId}">` block
2. Use engine StringIds for XSLT cultures (`vlandia` not `rohan`, `empire` not `dunland`)

To add a spear mapping for a new culture:
1. Add `<Spear culture="{cultureStringId}" item="{itemId}"/>` in the `<Spears>` section

## Performance

No concerns. Guards spawn once per settlement entry (~20 guards). Config is lazy-loaded once. Reflection for `PrepareGuardAgentDataFromGarrison` is cached in a static `MethodInfo` field during `Initialize()`.

## Save Compatibility

Fully safe. `GuardsCampaignBehavior.SyncData` is empty -- guards spawn fresh every settlement entry. Adding/removing/changing guard config has zero save impact.
