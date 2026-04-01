# Troop Progression

## Overview
TroopProgression extends Bannerlord's troop tier and wage systems to support TAOM's 10-tier troop trees, raises the volunteer recruitment cap from tier 4 to tier 6, and provides per-settlement and per-clan weighted recruitment pools so that each faction's locations offer culturally appropriate troops.

## Why This Exists
- **Vanilla behavior:** `DefaultCharacterStatsModel.MaxCharacterTier` is 6. `DefaultVolunteerModel.MaxVolunteerTier` is 4. Wage and recruitment cost tables only have entries for tiers 0–6.
- **TAOM requirement:** TAOM troop trees go up to tier 10 (T7–T10 elite and named units). Without extending the cap, any troop above tier 6 is treated as tier 6 by the engine, and wages at tier 7–10 collapse to the vanilla `_ => 57` fallback, making high-tier armies wildly expensive or cheap.
- **Without this feature:** T7–T10 troops are assigned incorrect wages. Volunteer pools at village notables are capped at tier 4, so no Gondor knight or Erebor noble ever volunteers. Settlement-specific recruitment is absent, so every settlement of a culture offers identical troops.

## Architecture
### Design Challenge
`DefaultCharacterStatsModel`, `DefaultVolunteerModel`, and `DefaultPartyWageModel` are all GameModel classes that Bannerlord resolves from its internal model registry. They cannot be subclassed while also injecting services, because Bannerlord instantiates models directly, not through DI. The solution is to subclass them and pull dependencies through the TAOM IoC container at construction time (models are registered as singletons in `SubModule.cs`).

The recruitment pool problem is a separate concern: which basic volunteer a settlement notary offers is determined by `GetBasicVolunteer(Hero sellerHero)`. Vanilla uses culture to pick `basic_troop`; TAOM needs settlement- and clan-specific pools with weighted random selection.

### Solution Approach
Three GameModel overrides handle the extension points:

- `TaomCharacterStatsModel` — single-line override: `MaxCharacterTier => 10`.
- `TaomVolunteerModel` — overrides `MaxVolunteerTier` (delegated to `IVolunteerTierService`) and `GetBasicVolunteer` (uses `IVolunteerRecruitmentService` with context from `IVolunteerContextAdapter`).
- `TaomPartyWageModel` — overrides `GetCharacterWage` (tier-to-wage table via `ITroopCostService`), `GetTroopRecruitmentCost` (level-based cost table + horse cost), and `GetTotalWage` (applies cultural feat bonuses for Gondor, Erebor, Lothlorien, Isengard, Gundabad, Umbar, Mordor, Rohan).

`VolunteerRecruitmentService` holds three static dictionaries (SettlementMap, ClanMap, CultureMap) initialized at class-load time. `GetVolunteerTroopId` resolves by settlement id first, then bound-settlement id, then owner clan id, then culture id (fallback), then picks a weighted random troop from the pool. The context object (`VolunteerContext`) carries these four string keys and is built by `IVolunteerContextAdapter` from the live `Hero` object — keeping the service free of TaleWorlds sealed types.

### Component Diagram
```
TaomVolunteerModel.GetBasicVolunteer(Hero)
    |-> IVolunteerContextAdapter.GetContext(Hero)   [extracts string keys]
    |-> IVolunteerRecruitmentService.GetVolunteerTroopId(VolunteerContext)
            |-> SettlementMap[settlementId] (weighted)
            |-> SettlementMap[boundSettlementId]
            |-> ClanMap[ownerClanId]
            |-> CultureMap[cultureId]
            |-> weighted random pick
    |-> IVolunteerContextAdapter.ResolveCharacter(troopId)
    |-> base.GetBasicVolunteer() [fallback]

TaomPartyWageModel.GetCharacterWage(CharacterObject)
    |-> ITroopCostService.GetCharacterWage(tier, isMounted, isMercenary)

TaomPartyWageModel.GetTotalWage(MobileParty, TroopRoster)
    |-> base.GetTotalWage()
    |-> TaomCulturalFeats.* checks (garrison + party culture feats)
```

## Configuration
None. Tier values and wage tables are hardcoded in `VolunteerTierService` and `TroopCostService`. Recruitment pools are hardcoded in `VolunteerRecruitmentService`. To change them, edit the service directly and update the corresponding tests.

## Key Files
| File | Purpose |
|------|---------|
| `Main/Features/TroopProgression/Models/TaomCharacterStatsModel.cs` | Sets `MaxCharacterTier = 10` |
| `Main/Features/TroopProgression/Models/TaomVolunteerModel.cs` | Overrides `MaxVolunteerTier` and `GetBasicVolunteer` |
| `Main/Features/TroopProgression/Models/TaomPartyWageModel.cs` | Overrides `GetCharacterWage`, `GetTroopRecruitmentCost`, `GetTotalWage` |
| `Main/Features/TroopProgression/TroopCostService.cs` | Tier-to-wage table (T0–T10) and level-to-recruitment-cost table |
| `Main/Features/TroopProgression/VolunteerTierService.cs` | `MaxVolunteerTier = 6` |
| `Main/Features/TroopProgression/VolunteerRecruitmentService.cs` | Settlement/clan/culture weighted recruitment pools for Gondor, Dol Guldur, Erebor |
| `Main/Features/TroopProgression/VolunteerContext.cs` | POCO carrying settlement, bound-settlement, clan, and culture string keys |
| `Main/Features/TroopProgression/VolunteerChance.cs` | POCO pairing a troop character id with an integer weight |
| `Main/Features/TroopProgression/TroopProgressionIoC.cs` | DryIoc registrations |
| `TAOM.Tests/Features/TroopProgression/TroopCostServiceTests.cs` | Wage and recruitment cost table coverage |
| `TAOM.Tests/Features/TroopProgression/VolunteerTierServiceTests.cs` | MaxVolunteerTier value |
| `TAOM.Tests/Features/TroopProgression/VolunteerRecruitmentServiceTests.cs` | Settlement, clan, and culture pool resolution + weighted pick |

## Dependencies
- `ITroopCostService` — wage and cost tables
- `IVolunteerTierService` — max volunteer tier constant
- `IVolunteerRecruitmentService` — weighted recruitment pools
- `IVolunteerContextAdapter` — wraps `Hero` to extract string context
- `IRandomProvider` — `System.Random` wrapper for testability
- `TaomCulturalFeats` — feat constants used in wage calculation

## Tests
- `TroopCostServiceTests.cs` — verifies wage for each tier 0–10, mounted/mercenary multipliers, and recruitment costs across the level breakpoints.
- `VolunteerTierServiceTests.cs` — verifies `MaxVolunteerTier == 6`.
- `VolunteerRecruitmentServiceTests.cs` — covers settlement lookup, bound-settlement fallback, clan fallback, culture fallback, null returns for unknown keys, and weighted distribution behavior for Gondor, Dol Guldur, and Erebor pools.

## How to Add a New Culture's Recruitment Pool
1. Write tests in `VolunteerRecruitmentServiceTests.cs` for the new settlement ids, clan ids, and culture id (RED).
2. Add `InitializeXxxSettlements()`, `InitializeXxxClans()`, and `InitializeXxxCulture()` private static methods in `VolunteerRecruitmentService.cs` following the existing pattern.
3. Call them from the static constructor.
4. Run tests to confirm GREEN.
5. Update `CHANGELOG.md`.

## GitHub Issue
- **Issue:** Unknown
- **Status:** Unknown
