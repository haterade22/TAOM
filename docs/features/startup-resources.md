# Startup Resources — Culture-Based Gold & Influence Distribution

## Overview

Distributes starting gold to individual Lord heroes and starting influence to clans at new game creation, with amounts configured per culture via XML. This establishes faction-appropriate economic baselines reflecting Middle-earth power dynamics.

## Why This Exists

- **Vanilla behavior:** All factions start with identical default gold/influence regardless of lore
- **TAOM requirement:** Elven factions (ancient, wealthy) should start rich; Orcish warchest factions (Isengard, Gundabad) need military funding; Human kingdoms start modest
- **Without this feature:** Economic parity breaks immersion — Rivendell starts as poor as a human frontier settlement

## Architecture

### Design Challenge

Gold must target individual Lord heroes (TaleWorlds' `GiveGoldAction` operates on `Hero`), while influence targets clans (set via `Clan.Influence`). Both use the same trigger and culture-based config, so they share a feature but have separate services and adapters.

### Solution Approach

Single `CampaignBehavior` registers on `OnNewGameCreatedPartialFollowUpEvent` at index 1 (after InitialChildGeneration at index 0). Delegates to two services, each with their own adapter. An idempotency flag prevents double-distribution.

### Component Diagram

```
startup_resources_config.xml
        |
  ConfigProvider (XDocument, cached)
       / \
      /   \
GoldService    InfluenceService
    |               |
StartupHero    ClanStartup
Adapter        Adapter
    |               |
GoldGift       clan.Influence
Adapter            +=
    |
GiveGoldAction
```

## Configuration

### Config File: `Main/_Module/ModuleData/startup_resources/startup_resources_config.xml`

Each `<Culture>` element maps a culture ID to gold (per Lord hero) and influence (per clan).

| Attribute | Type | Description |
|-----------|------|-------------|
| `id` | string | Culture string ID (case-insensitive match) |
| `gold` | int | Gold given to each alive Lord hero in this culture (0 = skip) |
| `influence` | float | Influence added to each eligible clan in this culture (0 = skip) |

### Current Values

| Culture | Gold | Influence | Rationale |
|---------|------|-----------|-----------|
| rivendell | 6,000,000 | 2,000 | Ancient Elven wealth, major diplomatic power |
| lothlorien | 6,000,000 | 2,000 | Ancient Elven wealth, major diplomatic power |
| mirkwood | 6,000,000 | 50 | Wealthy but isolated, low political influence |
| erebor | 1,000,000 | 50 | Dwarven treasure hoard, isolationist |
| gondor | 500,000 | 100 | Fading kingdom, moderate resources |
| vlandia (Rohan) | 500,000 | 50 | Horse-lords, modest economy |
| sturgia (Dale) | 500,000 | 50 | Frontier kingdom, modest economy |
| battania (Khand/Dunland) | 500,000 | 100 | Eastern/tribal cultures |
| aserai (Harad) | 500,000 | 100 | Southern kingdom |
| khuzait (Rhun) | 500,000 | 1,000 | Eastern empire, significant political power |
| mordor | 500,000 | 100 | Economy driven by conquest, not wealth |
| isengard | 2,000,000 | 2,000 | Saruman's war machine, high mobilization |
| gundabad | 2,000,000 | 2,000 | Orcish warchest |
| dolguldur | 2,000,000 | 2,000 | Shadow fortress resources |
| umbar | 2,000,000 | 100 | Corsair wealth, low inland influence |

## Key Files

| File | Purpose |
|------|---------|
| `Main/Features/StartupResources/StartupResourcesBehavior.cs` | CampaignBehavior — fires at index 1, delegates to both services |
| `Main/Features/StartupResources/StartupGoldService.cs` | Iterates Lord heroes, gives culture-based gold |
| `Main/Features/StartupResources/IStartupGoldService.cs` | Service interface |
| `Main/Features/StartupResources/StartupInfluenceService.cs` | Iterates eligible clans, adds culture-based influence |
| `Main/Features/StartupResources/IStartupInfluenceService.cs` | Service interface |
| `Main/Features/StartupResources/StartupResourcesConfigProvider.cs` | XDocument XML parser with caching |
| `Main/Features/StartupResources/IStartupResourcesConfigProvider.cs` | Config provider interface |
| `Main/Features/StartupResources/Config/StartupResourcesConfig.cs` | Config POCOs |
| `Main/Features/StartupResources/StartupResourcesIoC.cs` | DryIoc registration |
| `Main/Adapters/IStartupHeroAdapter.cs` | Interface — `GetAliveLordHeroes()` |
| `Main/Adapters/StartupHeroAdapter.cs` | Wraps `Hero.AllAliveHeroes`, filters `Occupation.Lord` |
| `Main/Adapters/IGoldGiftAdapter.cs` | Interface — `GiveGoldToHero(heroId, amount)` |
| `Main/Adapters/GoldGiftAdapter.cs` | Wraps `GiveGoldAction.ApplyBetweenCharacters(null, hero, amount, true)` |
| `Main/Adapters/IClanStartupAdapter.cs` | Interface — `GetEligibleClans()`, `AddInfluence()` |
| `Main/Adapters/ClanStartupAdapter.cs` | Wraps `Clan.All` filtering + `clan.Influence +=` |
| `Main/_Module/ModuleData/startup_resources/startup_resources_config.xml` | Culture gold/influence values |

## Dependencies

- `IPathService` (Core) — resolves `ModuleDataPath` for config file location
- `IModLogger` (Core) — logging
- `IStartupHeroAdapter` (Adapters) — wraps `Hero.AllAliveHeroes` + `Occupation.Lord` filter
- `IGoldGiftAdapter` (Adapters) — wraps `GiveGoldAction.ApplyBetweenCharacters`
- `IClanStartupAdapter` (Adapters) — wraps `Clan.All` filtering + `Clan.Influence` setter

## Tests

- `TAOM.Tests/Features/StartupResources/StartupResourcesConfigProviderTests.cs` — 6 tests: valid XML parsing, missing file, malformed XML, caching, decimal influence, missing attributes
- `TAOM.Tests/Features/StartupResources/StartupGoldServiceTests.cs` — 8 tests: culture match, player skip, missing culture, multiple lords, zero gold, case-insensitive, no heroes, logging
- `TAOM.Tests/Features/StartupResources/StartupInfluenceServiceTests.cs` — 6 tests: culture match, missing culture, multiple clans, zero influence, no clans, logging
- `TAOM.Tests/Features/StartupResources/StartupResourcesBehaviorTests.cs` — 4 tests: index 1 triggers, index 0/2 skip, idempotency guard

## How to Add or Adjust a Culture's Starting Resources

1. Open `Main/_Module/ModuleData/startup_resources/startup_resources_config.xml`
2. Add or edit a `<Culture>` element: `<Culture id="culture_id" gold="AMOUNT" influence="AMOUNT" />`
3. No code changes needed — the config provider loads all entries dynamically
4. Set `gold="0"` or `influence="0"` to skip distribution for that dimension
5. Cultures not listed in the config receive nothing (no fallback/default)

## TaleWorlds API Notes

- `GiveGoldAction.ApplyBetweenCharacters(null, hero, amount, disableNotification: true)` — null source is safe when `disableNotification: true` (short-circuits the `giverHero == Hero.MainHero` check)
- `Clan.Influence` has a public setter; increasing it does not trigger `SkillLevelingManager.OnInfluenceSpent` (only decreasing does)
- `Hero.AllAliveHeroes` returns `MBReadOnlyList<Hero>`, safe to iterate at campaign start index 1

## GitHub Issue

- **Issue:** #42 — [feat: culture-based startup gold and influence distribution](https://github.com/haterade22/TAOM/issues/42)
- **Status:** Open
