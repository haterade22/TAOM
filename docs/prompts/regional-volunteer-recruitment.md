# Feature: Regional Volunteer Recruitment System

## Goal
Port and adapt the LOTRAOM regional volunteer recruitment system to TAOM. This system overrides `GetBasicVolunteer` so that troop recruitment is determined by a **settlement → clan → culture** fallback hierarchy, rather than vanilla's culture-only approach.

## Why
Gondor now has 24 `is_basic_troop=true` roots across 23 regional unit groups (e.g., Lossarnach, Pelargir, Dol Amroth, Anorien). Vanilla only offers `Culture.BasicTroop` / `Culture.EliteBasicTroop`, meaning every Gondor settlement recruits the same 1-2 troops. We need settlement-specific recruitment so that Dol Amroth recruits Dol Amroth troops, Pelargir recruits Pelargir marines, etc.

## Architecture Reference (LOTRAOM)
The old system (decompiled from `LOTRAOM.dll`) used:

### 3-tier call hierarchy
```
Settlement StringId → Owner Clan StringId → Settlement Faction Culture StringId
```
First match wins. Each tier maps to a `List<VolunteerChance>` (troop ID + probability weight). A weighted random pick selects the troop.

### Key types from LOTRAOM
```csharp
// Service interface
public interface IVolunteerCalculationService
{
    CharacterObject GetBasicVolunteer(Hero hero, CharacterObject defaultVolunteer);
    bool CanHaveRecruits(Hero hero, bool defaultCanHaveRecruits);
    float GetDailyVolunteerProductionProbability(Hero hero, int index, Settlement settlement, float defaultProbability);
    int MaximumIndexHeroCanRecruitFromHero(Hero buyerHero, Hero sellerHero, int useValueAsRelation, int defaultMaxIndex);
    int MaxVolunteerTier(int defaultMaxTier);
}

// VolunteerChance (troop ID + probability weight)
public class VolunteerChance
{
    public string CharacterId { get; }
    public int Probability { get; }
}

// Call hierarchy (in constructor):
_callHierarchy = new List<Func<Hero, List<VolunteerChance>>>
{
    GetVolunteerFromSettlementStringId,
    GetVolunteerFromOwnerClanStringId,
    GetVolunteerFromSettlementCultureStringId
};

// GetBasicVolunteer iterates hierarchy, picks weighted random from first non-null list

// Model wraps the base model:
public class AOMVolunteerModel : VolunteerModel
{
    private readonly VolunteerModel _defaultModel;
    private readonly IVolunteerCalculationService _service;

    public override CharacterObject GetBasicVolunteer(Hero hero)
    {
        CharacterObject defaultVolunteer = _defaultModel.GetBasicVolunteer(hero);
        return _service.GetBasicVolunteer(hero, defaultVolunteer);
    }
}
```

## Current TAOM State

### Existing code to modify
- **`Main/Features/TroopProgression/Models/TaomVolunteerModel.cs`** — Currently only overrides `MaxVolunteerTier => 6`. Needs `GetBasicVolunteer` override.
- **`Main/Features/TroopProgression/VolunteerTierService.cs`** — `IVolunteerTierService` with `MaxVolunteerTier => 6`
- IoC registration in `Main/Core/IoC.cs` (existing TroopProgression registrations)

### Vanilla base (decompiled DefaultVolunteerModel.GetBasicVolunteer)
```csharp
public override CharacterObject GetBasicVolunteer(Hero sellerHero)
{
    if (sellerHero.IsRuralNotable && sellerHero.CurrentSettlement.Village.Bound.IsCastle)
    {
        return sellerHero.Culture.EliteBasicTroop;
    }
    return sellerHero.Culture.BasicTroop;
}
```

## TAOM Design Requirements

### Follow TAOM architecture patterns
- **TDD mandatory**: RED → GREEN → REFACTOR
- **Adapter pattern**: Services use `IHeroAdapter`, `ISettlementAdapter`, etc. — NEVER raw `Hero`, `Settlement`
- Use `/research` skill to decompile any TaleWorlds types before implementing
- Register in IoC.cs
- Thin model, delegate to service

### New types to create
1. **`IVolunteerRecruitmentService`** — Service interface with `GetBasicVolunteer(IHeroAdapter hero, CharacterObject defaultVolunteer)`
2. **`VolunteerRecruitmentService`** — Implementation with 3-tier lookup
3. **`VolunteerChance`** — Value object (troop ID + weight)
4. **Data dictionaries** — Settlement, Clan, Culture mappings
5. **Update `TaomVolunteerModel`** — Override `GetBasicVolunteer`, delegate to service
6. **Tests** — Full coverage of lookup hierarchy, fallback, weighted random

### Gondor Settlement → Troop Mappings

**Gondor Towns (EW region)**:
| Settlement ID | Name | Regular Root | Noble Root |
|--------------|------|-------------|------------|
| town_EW1 | Minas Tirith | gondor_ano_peasant | gondor_mt_trainee (Citadel Guard) |
| town_EW2 | West Osgiliath | gondor_ano_peasant | gondor_osg_veteran |
| town_EW3 | East Osgiliath | gondor_ano_peasant | gondor_osg_veteran |
| town_EW4 | Pelargir | gondor_leb_militia | gondor_pel_skirmisher |
| town_EW5 | Dol Amroth | gondor_bel_recruit | gondor_da_noble |
| town_EW6 | Lond Cirion | gondor_bel_recruit | gondor_da_noble |
| town_EW7 | Bar Melui | gondor_leb_militia | gondor_lg_noble |
| town_EW8 | Ost Arndir | gondor_pg_volunteer | gondor_arn_noble |
| town_EW9 | Calembel | gondor_lam_clansman | gondor_cal_noble |
| town_EW10 | Town EW10 | gondor_anf_levy | gondor_ser_noble |
| town_EW11 | Town EW11 | gondor_ano_peasant | gondor_met_noble |

**Gondor Castles**:
| Settlement ID | Name | Regular Root | Noble Root |
|--------------|------|-------------|------------|
| castle_EW1 | Harlond | gondor_ano_peasant | gondor_mt_trainee |
| castle_EW2 | Ethring | gondor_lam_clansman | gondor_ring_peasant |
| castle_EW3 | Onica Castle | gondor_lam_clansman | gondor_cal_noble |
| castle_EW4 | Cair Andros | gondor_ano_peasant | gondor_ca_noble |
| castle_EW5 | Min-Rimmon | gondor_ano_peasant | gondor_ano_peasant |
| castle_EW6 | Morlad | gondor_har_conscript | gondor_har_conscript |
| castle_EW7 | Serelond | gondor_anf_levy | gondor_ser_noble |
| castle_EW8 | Amon Dîn | gondor_loss_lumberman | gondor_loss_noble |
| castle_EW9 | Tolfalas | gondor_bel_recruit | gondor_tol_arbalest |
| castle_EW10 | Castle EW10 | gondor_anf_levy | gondor_lg_noble |
| castle_EW11 | Castle EW11 | gondor_ano_peasant | gondor_met_noble |
| castle_EW12 | Castle EW12 | gondor_loss_lumberman | gondor_loss_noble |
| castle_EW13 | Castle EW13 | gondor_anf_levy | gondor_anf_levy |
| castle_EW14 | Castle EW14 | gondor_leb_militia | gondor_lin_noble |
| castle_EW15 | Castle EW15 | gondor_ano_peasant | gondor_ith_watcher |
| castle_EW16 | Castle EW16 | gondor_ano_peasant | gondor_ith_watcher |

**NOTE**: These mappings are approximate and the user may want to adjust them. Present them for review before hardcoding.

### Gondor Clan → Troop Mappings
| Clan ID | Name | Regular Root | Noble Root |
|---------|------|-------------|------------|
| clan_empire_west_1 | Húrinionath | gondor_ano_peasant | gondor_mt_trainee |
| clan_empire_west_2 | Imrazôrionath | gondor_bel_recruit | gondor_da_noble |
| clan_empire_west_3 | Eärnurionath | gondor_leb_militia | gondor_pel_skirmisher |
| clan_empire_west_4 | (Ethring area) | gondor_lam_clansman | gondor_cal_noble |
| clan_empire_west_5 | (Lossarnach area) | gondor_loss_lumberman | gondor_loss_noble |
| clan_empire_west_6 | (Pinnath Gelin) | gondor_pg_volunteer | gondor_pg_volunteer |
| clan_empire_west_7 | (Lamedon) | gondor_lam_clansman | gondor_cal_noble |
| clan_empire_west_8 | (Morlad/Harondor) | gondor_har_conscript | gondor_har_conscript |
| clan_empire_west_9 | (Serelond/BRV) | gondor_anf_levy | gondor_brv_bowman |

**NOTE**: Same caveat — present for review. Clan → troop associations depend on which region each clan "rules."

### Gondor Culture Fallback
```
"gondor" → [gondor_ano_peasant (weight 7), gondor_bel_recruit (1), gondor_lam_clansman (1), gondor_loss_lumberman (1)]
```
This ensures any Gondor settlement without a specific mapping still recruits Gondor troops.

## Implementation Plan

### Phase 1: Service + Tests
1. Create `IVolunteerRecruitmentService` interface
2. Create `VolunteerChance` value object
3. Create `VolunteerRecruitmentService` with 3-tier hierarchy
4. Write tests (mock IRandomProvider, IHeroAdapter, ISettlementAdapter)
5. Test: settlement match returns weighted troop
6. Test: no settlement match falls through to clan
7. Test: no clan match falls through to culture
8. Test: no match at all returns default
9. Test: invalid troop ID logs warning, returns default

### Phase 2: Model Integration
1. Add `GetBasicVolunteer` override to `TaomVolunteerModel`
2. Inject `IVolunteerRecruitmentService` via constructor
3. Register in IoC.cs
4. Integration tests

### Phase 3: Data Population
1. Populate Gondor settlement mappings (user reviews)
2. Populate Gondor clan mappings (user reviews)
3. Populate Gondor culture fallback
4. **Other cultures can be added incrementally in future sessions**

## All 24 Gondor `is_basic_troop=true` Roots

**8 Regular lines:**
| ID | Name | Tier | Level |
|----|------|------|-------|
| gondor_loss_lumberman | Lossarnach Lumberman | T1 | 6 |
| gondor_leb_militia | Lebennin Militia | T2 | 11 |
| gondor_lam_clansman | Lamedon Clansman | T1 | 6 |
| gondor_bel_recruit | Belfalas Recruit | T1 | 6 |
| gondor_pg_volunteer | Pinnath Gelin Volunteer | T1 | 6 |
| gondor_anf_levy | Anfalas Levy | T1 | 6 |
| gondor_har_conscript | Harondor Conscript | T1 | 6 |
| gondor_ano_peasant | Anorien Peasant | T1 | 6 |

**16 Noble lines:**
| ID | Name | Tier | Level |
|----|------|------|-------|
| gondor_loss_noble | Lossarnach Noble | T4 | 21 |
| gondor_pel_skirmisher | Pelargir Skirmisher | T4 | 21 |
| gondor_cal_noble | Calembel Noble | T4 | 21 |
| gondor_ring_peasant | Ringlo Vale Peasant | T1 | 6 |
| gondor_da_noble | Dol Amroth Noble | T3 | 16 |
| gondor_lin_noble | Linhir Noble | T3 | 16 |
| gondor_tol_arbalest | Tolfalas Arbalest | T3 | 16 |
| gondor_arn_noble | Arndir Noble | T3 | 16 |
| gondor_brv_bowman | Blackroot Vale Bowman | T3 | 16 |
| gondor_ser_noble | Serelond Noble | T3 | 16 |
| gondor_lg_noble | Lond-Galen Noble | T4 | 21 |
| gondor_met_noble | Methir Noble | T4 | 21 |
| gondor_ith_watcher | Ithil Guard Watcher | T5 | 26 |
| gondor_ca_noble | Cair Andros Noble | T3 | 16 |
| gondor_osg_veteran | Osgiliath Veteran | T3 | 16 |
| gondor_mt_trainee | Citadel Guard Trainee | T5 | 26 |

## Key Differences from LOTRAOM

1. **TAOM uses adapter pattern** — LOTRAOM accessed `Hero.CurrentSettlement` directly. TAOM must use `IHeroAdapter`, `ISettlementAdapter`, `IClanAdapter`
2. **TAOM uses proper IoC** — LOTRAOM used its own DI. TAOM uses the standard IoC.cs registration
3. **New troop IDs** — LOTRAOM used old Gondor troop IDs. TAOM uses the new regional IDs (gondor_loss_*, gondor_da_*, etc.)
4. **TDD** — LOTRAOM had no tests for this. TAOM requires full test coverage.
5. **TAOM extends `DefaultVolunteerModel`** — LOTRAOM extended `VolunteerModel`. Both approaches work in 1.3.

## Files to Read Before Starting

| File | Why |
|------|-----|
| `Main/Features/TroopProgression/Models/TaomVolunteerModel.cs` | Current model to extend |
| `Main/Features/TroopProgression/VolunteerTierService.cs` | Existing service pattern |
| `Main/Features/TroopProgression/IVolunteerTierService.cs` | Interface pattern |
| `Main/Core/IoC.cs` | Registration pattern |
| `Main/Adapters/` | Available adapters (IHeroAdapter, ISettlementAdapter, etc.) |
| `TAOM.Tests/` | Test patterns and conventions |
| `Main/_Module/ModuleData/troops/troops_gondor.xml` | All troop IDs |
| `Main/_Module/ModuleData/settlements.xml` | Settlement IDs for EW region |
| `Main/_Module/ModuleData/characters/clans.xml` | Clan IDs |

## Scope
- **This session**: Build the service, model override, and Gondor data mappings
- **Future sessions**: Add mappings for other cultures (Rohan, Mordor, Harad, etc.)
