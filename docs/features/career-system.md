# Career System

## Overview

Career/class progression system where each hero can have a career that provides passive bonuses, an active ability, and a 3-tier choice tree. Careers are defined in XML and gated by culture eligibility and hero level. Mordor "Warboss" is the pilot career.

## Why This Exists

- **Vanilla behavior:** Bannerlord has perks but no career/class system with branching choice trees or active abilities
- **TAOM requirement:** Middle-earth factions need distinct playstyles beyond cultural feats — a Mordor Warboss should feel different from a Gondor Knight
- **Without this feature:** All heroes of the same culture play identically after initial perk selection

## Architecture

### Design Challenge

TOR_Core's career system uses hardcoded C# classes, static singletons, and 6 Harmony patches on ViewModel. TAOM needs XML-driven careers (add without recompilation), DryIoc injection, adapter pattern compliance, and UIExtenderEx integration.

### Solution Approach

- **Data model:** Plain C# classes (not PropertyObject) loaded from XML via `ICareerConfigProvider`
- **Persistence:** `CareerPersistenceBehavior` with `SyncData("_taom_careerData")` storing `Dictionary<string, HeroCareerData>`
- **Passive application:** `ICareerPassiveService` caches per-hero effect magnitudes, `CareerPassiveHelper` wires into 8 existing GameModels
- **Mutations:** Hybrid XML + C# calculator registry — XML defines target/params, C# provides calculator functions by ID
- **UI:** `GauntletCareerScreen` (GlobalLayer) with `CareerScreenVM` hierarchy, `CharacterDeveloperCareerMixin` (UIExtenderEx) for career button
- **Battle:** `CareerPerkMissionBehavior` for per-second tick and kill-based charge, `CareerAbilityService` for ability state

### Component Diagram

```
taom_careers.xml / taom_career_choices.xml
        |
  CareerConfigProvider (loads XML)
        |
  CareerRegistry (lookup/eligibility/tier gating)
        |
  ┌─────┴──────┐
  |            |
CareerDataService   CareerPassiveService (cache)
(per-hero CRUD)         |
  |            CareerPassiveHelper → 8 GameModels
  |
CareerCampaignBehavior  CareerPerkMissionBehavior
(session/level/death)   (battle tick/charge)
  |
CareerCreationHandler   CareerSwitchService
(CC integration)        (NPC dialogue switching)
  |
GauntletCareerScreen → CareerScreenVM → CareerChoiceGroupObjectVM → CareerChoiceObjectVM
```

## Configuration

### Career Definitions (`Main/_Module/ModuleData/career_system/taom_careers.xml`)

Defines careers with: id, display name, description, ability template ID, charge type, max charge, eligible cultures, choice group IDs. `max_perk_points` attribute on root element (default 30).

### Choice Trees (`Main/_Module/ModuleData/career_system/taom_career_choices.xml`)

Defines standalone root choices and choice groups. Each group has a tier (1/2/3) and contains choices (Keystone or Passive). Choices can have PassiveEffect (type + magnitude + operation) and Mutations (target template + property + calculator + params).

### Ability Templates (`Main/_Module/ModuleData/career_system/taom_ability_templates.xml`)

Defines career abilities with: id, name, cooldown, duration, radius, cast type, target type, particle/sound effects.

## Key Files

| File | Purpose |
|------|---------|
| `Main/Features/CareerSystem/Domain/` (11 files) | Enums + immutable data classes |
| `Main/Features/CareerSystem/ICareerDataService.cs` | Per-hero career state CRUD |
| `Main/Features/CareerSystem/CareerConfigProvider.cs` | XML config loading |
| `Main/Features/CareerSystem/CareerRegistry.cs` | Career lookup, eligibility, tier gating |
| `Main/Features/CareerSystem/CareerPassiveService.cs` | Session-scoped passive effect cache |
| `Main/Features/CareerSystem/CareerPassiveHelper.cs` | Static helper wiring passives into GameModels |
| `Main/Features/CareerSystem/Mutations/` (6 files) | Calculator registry + built-in calculators + mutation service |
| `Main/Features/CareerSystem/Abilities/` (3 files) | CareerAbility + ability service |
| `Main/Features/CareerSystem/CareerCampaignBehavior.cs` | Campaign lifecycle events |
| `Main/Features/CareerSystem/CareerPerkMissionBehavior.cs` | Battle tick + charge accumulation |
| `Main/Features/CareerSystem/CareerCreationHandler.cs` | Character creation integration |
| `Main/Features/CareerSystem/CareerSwitchService.cs` | Career switching with validation |
| `Main/Features/CareerSystem/UI/` (6 files) | Career screen GameState + VM hierarchy + UIExtenderEx mixin |
| `Main/Adapters/ICareerHeroAdapter.cs` | Wraps Hero for service boundary |
| `Main/Adapters/ICareerHeroAdapterFactory.cs` | Factory for GameModel boundary |

## Dependencies

- DryIoc (IoC container)
- UIExtenderEx (UI injection)
- TaleWorlds.CampaignSystem (Hero, CampaignEvents, ExplainedNumber)
- TaleWorlds.MountAndBlade (MissionBehavior, Agent)
- TaleWorlds.Engine.GauntletUI (GauntletLayer, GlobalLayer)

## Tests

| Test File | Methods | Coverage |
|-----------|---------|----------|
| HeroCareerDataTests | 12 | Domain data class |
| CareerDataServiceTests | 17 | CRUD + persistence round-trip |
| CareerConfigProviderTests | 5 | XML parsing + missing file |
| CareerRegistryTests | 16 | Lookup + eligibility + tier gating |
| MutationCalculatorRegistryTests | 8 | All 5 built-in calculators |
| CareerPassiveServiceTests | 7 | Cache refresh + magnitude aggregation |
| MutationServiceTests | 5 | Template cloning + mutation application |
| CareerAbilityTests | 14 | Charge types + cooldown + activation |
| CareerCreationHandlerTests | 4 | CC flow + root choice |
| CareerSwitchServiceTests | 5 | Switch validation + choice reset |
| CareerScreenVMTests | 5 | VM state + choice selection |

## How-To

### Add a new career
1. Add `<Career>` element to `taom_careers.xml` with unique id, eligible cultures, choice groups
2. Add `<ChoiceGroup>` elements to `taom_career_choices.xml` (6 groups: 2 per tier, each with 1 keystone + 4 passives)
3. Add `<Choice id="xxx_root">` as the root choice
4. Add ability template to `taom_ability_templates.xml`
5. No C# changes required

### Add a new mutation calculator
1. Add function to `BuiltInCalculators.RegisterAll()`
2. Reference by id in XML `<Mutation calculator="your_id" ... />`

### Add a new PassiveEffectType
1. Add enum value to `PassiveEffectType.cs`
2. Add `CareerPassiveHelper.ApplyFactor/ApplyFlat` call in the relevant GameModel
