# Special Resources

**Status:** Verified in-game (2026-04-14). Gondor Caster resource showing on map bar with rich tooltip (amount, tier, daily breakdown, per-event rates). Icon sprite loading correctly.

## Overview

Per-kingdom special resource system where all 18 TAOM kingdoms have a unique secondary currency required to recruit and maintain elite troops. 11 unique resources mapped to faction groups — shared balance within each group. Resources are earned through combat, displayed in the map bar, enforced in the party screen with pending transaction support, and trigger troop desertion when depleted.

## Why This Exists

- **Vanilla behavior:** All troop upgrades cost only gold and XP — no faction flavor
- **TAOM requirement:** Elite troops should feel expensive and faction-specific
- **Without this feature:** Elite armies are trivially affordable, reducing faction identity and strategic tension

## Resources

| Resource | Kingdoms | Theme |
|----------|----------|-------|
| War Spoils | Mordor, Isengard, Gundabad, Dol Guldur | Orc plunder from battles |
| Gems | Erebor | Dwarven mining wealth |
| Caster | Gondor | Silver coin currency |
| Marks | Rohan | Horse-lord currency |
| Elven Wine | Rivendell, Lothlorien, Mirkwood | Elven trade goods |
| Lake Fish | Dale | Laketown trade |
| War Drums | Harad, Shaghana, Abanissa | Tribal war currency |
| Tribal Relics | Khand | Sacred artifacts |
| Dunlending Ale | Dunland | Clan tribute |
| Plunder | Umbar | Corsair loot |
| War Banners | Rhun | Easterling standards |

Factions sharing a resource share the same balance (e.g., switching from Mordor to Isengard keeps your War Spoils).

## Architecture

### Design Challenge

Bannerlord has no concept of per-faction resources beyond gold and influence. The party screen upgrade flow is hardcoded to check only gold costs.

### Solution

- **XML-driven config:** Resource definitions with nested `<Kingdom>` and `<Culture>` child elements for many-to-one mappings
- **Culture fallback:** Resolves via kingdom first, then culture — supports kingdomless players
- **CampaignBehavior:** Hooks 8 events (DailyTick, MapEventEnded, RaidCompleted, PrisonerTaken, TournamentFinished, HideoutCompleted, NewGameCreated, SessionLaunched)
- **Harmony Patch26:** 3 patches — InitializeUpgrades (grey out + hint), AddCommand prefix (clamp count), UpgradeTroop postfix (queue spend)
- **Pending transaction:** Upgrades queue during party screen, commit on close, revert on cancel
- **Desertion:** At 0 balance, 10% of each upkeep-troop type deserts daily (min 1 per type)
- **Notifications:** Green chat for earnings, yellow warning at <10% cap, center-screen desertion alert
- **SyncData persistence:** Composite `heroId:resourceId` keys, cap enforcement on load
- **Career passive integration:** `CustomResourceGain` scales daily earning, `CustomResourceUpkeepModifier` reduces upkeep, `CustomResourceUpgradeCostModifier` reduces upgrade cost — all wired through `ICareerPassiveService`
- **Resource tiers:** Optional `<Tiers>` XML element defines threshold-based progression (pilot: Gems with 3 tiers). `GetCurrentTier()` resolves highest tier where balance >= threshold. Map bar shows tier name when active.
- **Map bar display:** Uses `[DataSourceProperty]` bindings on mixin + `PrefabExtensionInsertPatch` — does NOT add to `SecondaryInfoItems` (causes vanilla IndexOutOfRange crash). See [gui-sprite-system.md](gui-sprite-system.md).
- **Comprehensive logging:** `[SpecRes]` prefix throughout all components

### Component Diagram

```
special_resources_config.xml + troop_resource_costs.xml
        |
  SpecialResourceConfigProvider (loads + caches XML, multi-key indexes)
        |
  SpecialResourceService (resolve, earn, spend, validate, daily tick, desertion)
       / \         \          \
      /   \         \          \
Behavior  Patch26    MapBarMixin  SpriteWidget  ICareerPassiveService
(events)  (party UI) (map bar)   (dynamic icon) (career modifier)
                                                      |
                                              ResourceTier (domain)
                                              GetCurrentTier (service)
```

## Configuration

### Resource Definitions: `Main/_Module/ModuleData/special_resources/special_resources_config.xml`

```xml
<Resource id="war_spoils" display_name="War Spoils" icon_sprite="taom_war_spoils_icon"
  cap="500" starting_amount="30" daily_per_town="0.2"
  per_battle_victory_base="14" per_raid="12" per_siege_victory="20"
  per_prisoner="2" per_tournament_win="3" per_hideout_clear="8">
  <Kingdom id="empire_s" />
  <Kingdom id="isengard" />
  <Culture id="mordor" />
  <Culture id="isengard" />
</Resource>
```

Earning rates are now differentiated per faction identity:
- **Aggressive factions** (Mordor, Harad): high battle/raid, low daily
- **Mining/trade factions** (Erebor, Dale): high daily, low battle
- **Honor factions** (Rohan): high tournament, zero raid
- **Peaceful factions** (Elves): high daily, zero raid, lower cap

### Resource Tiers (Optional): `<Tiers>` element inside `<Resource>`

```xml
<Resource id="gems" ...>
  <Tiers>
    <Tier level="1" name="Apprentice Miner" threshold="100"
          description="Dwarven mining efficiency improves." />
    <Tier level="2" name="Journeyman Smith" threshold="250"
          description="Erebor's forges burn bright." />
    <Tier level="3" name="Master of the Treasury" threshold="400"
          description="The wealth of Erebor flows." />
  </Tiers>
</Resource>
```

Tiers are sorted by threshold at parse time. `GetCurrentTier()` reverse-walks to find the highest met threshold. Resources without `<Tiers>` have an empty list (backward compatible).

Multiple `<Kingdom>` and `<Culture>` child elements map to the same resource (many-to-one).

### Troop Costs: `Main/_Module/ModuleData/special_resources/troop_resource_costs.xml`

```xml
<Troop id="mordor_uruk_darkblade" resource_id="war_spoils" upgrade_cost="2" daily_upkeep="0.1" />
```

### Current Values (all resources)

- Cap: 500, Starting: 30
- Daily per town: +0.5, Battle: +10 (x enemy ratio 0.5-2x), Raid: +8, Siege: +15, Prisoner: +1, Tournament: +5, Hideout: +6
- 12 Mordor elite troops costed (other factions pending)

## Key Files

| File | Purpose |
|------|---------|
| `Main/Features/SpecialResources/ISpecialResourceService.cs` | Service interface + TroopUpkeepInfo + TroopDesertionEntry |
| `Main/Features/SpecialResources/SpecialResourceService.cs` | Core logic (resolve, earn, spend, desertion, session) |
| `Main/Features/SpecialResources/ISpecialResourceStorageService.cs` | Storage interface |
| `Main/Features/SpecialResources/SpecialResourceStorageService.cs` | Composite-key dict persistence |
| `Main/Features/SpecialResources/ISpecialResourceConfigProvider.cs` | Config interface (GetByKingdomId, GetByCultureId) |
| `Main/Features/SpecialResources/SpecialResourceConfigProvider.cs` | XML loader with multi-key indexing |
| `Main/Features/SpecialResources/SpecialResourcesBehavior.cs` | CampaignBehavior (8 events, desertion, notifications) |
| `Main/Features/SpecialResources/SpecialResourcesIoC.cs` | DryIoc registrations |
| `Main/Features/SpecialResources/Domain/SpecialResource.cs` | Resource definition (KingdomIds/CultureIds lists) |
| `Main/Features/SpecialResources/Domain/TroopResourceCostEntry.cs` | Per-troop cost record |
| `Main/Features/SpecialResources/Hooks/PartyCharacterVM_InitializeUpgrades_Patch.cs` | Grey out upgrades, show cost hint |
| `Main/Features/SpecialResources/Hooks/PartyScreenLogic_AddCommand_Patch.cs` | Prefix: clamp count before execution |
| `Main/Features/SpecialResources/Hooks/PartyScreenLogic_UpgradeTroop_Patch.cs` | Postfix: queue resource spend |
| `Main/Features/SpecialResources/Hooks/IOnPartyUpgradeResourceCheck.cs` | Hook interface |
| `Main/Features/SpecialResources/Hooks/PartyUpgradeResourceCheckHook.cs` | Hook implementation |
| `Main/Features/SpecialResources/UI/SpecialResourceMapBarMixin.cs` | Map bar UIExtenderEx mixin |
| `Main/Features/SpecialResources/UI/SpecialResourceSpriteWidget.cs` | Dynamic icon sprite (extends IconBrushWidget) |
| `Main/Features/SpecialResources/UI/SpecialResourcePrefab.cs` | PrefabExtension: swap widget in BottomInfoBar |
| `Main/_Module/ModuleData/special_resources/special_resources_config.xml` | 11 resource definitions |
| `Main/_Module/ModuleData/special_resources/troop_resource_costs.xml` | Mordor troop costs |

## Dependencies

- `IPathService` (Core) — module data path resolution
- `IModLogger` (Core) — logging (`[SpecRes]` prefix)
- UIExtenderEx — map bar mixin + prefab extension
- Harmony 2.x — Patch26_SpecialResources (3 patches)

## Tests

- `SpecialResourceServiceTests.cs` — 36 tests (resolve, earn, spend, validate, daily tick, pending transaction, desertion, edge cases)
- `SpecialResourceStorageServiceTests.cs` — 11 tests (get/set/add, clamp, multi-hero, multi-resource, restore-null)

## How to Add a New Kingdom's Resource

1. Add a `<Resource>` element with `<Kingdom>` and `<Culture>` children to `special_resources_config.xml`
2. Or add `<Kingdom>`/`<Culture>` children to an existing resource for shared balance
3. Add `<Troop>` rows to `troop_resource_costs.xml` for T6+ troops
4. Add a 33x33 PNG icon to `Main/_Module/GUI/SpriteParts/ui_taom/MapBar/`
5. No C# changes needed — fully data-driven

## How to Tune Earning Rates

Edit attributes on the `<Resource>` element. Each resource can have independent rates. Current values are identical across all 11 resources.

## Desertion Mechanics

- Triggers daily when resource balance is 0 and party has upkeep-costing troops
- 10% of each troop type deserts per day (minimum 1 per type)
- Center-screen notification: "X elite troops deserted — your [Resource] are depleted!"
- Uses vanilla `TroopRoster.AddToCounts(character, -count)` for roster removal

## Performance

- IoC.Resolve cached in MapBarMixin constructor (not per-refresh)
- Config provider lazy-loaded with dictionary indexes
- No LINQ in hot paths — direct enumeration loops
- SpriteWidget caches resolved sprite (loads once, not per-frame)
- String formatting only when amount changes (cached `_lastAmount`)
- Comprehensive logging uses `LogDebug` for high-frequency paths, `LogInfo` for events

---

<!-- backlinks-start auto-generated; edit lint_docs.py / build_backlinks.py to change -->

## Referenced by

- [docs/INDEX.md](../INDEX.md)

<!-- backlinks-end -->
