# Career System

**Status:** Verified in-game (2026-04-14). Career button with sprite on Character Developer screen, GameState-based screen opening (no crash), career selection in Character Creation. Gondor campaign tested.

**2026-05-04 update.** Ability activation rebuilt as a uniform 30-second cooldown timer. The original charge-based readiness model (DamageDone / Kills / DamageTaken accumulators) was replaced because per-archetype charge types produced confusing UX — defensive careers like Captain of Osgiliath only charged when the player took damage, so back-line players never saw the ability ready. See [Cooldown System](#cooldown-system) and `CHANGELOG.md` (issue #103).

## Overview

Career/class progression system where each hero can have a career that provides passive bonuses, an active ability, and a 3-tier choice tree. 50 LOTR-themed careers across 16 factions, fully XML-driven. Each career has 31 choices (1 root + 6 groups x 5 choices) with keystones, passives, and ability mutations.

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
- **UI:** `GauntletCareerScreen` with `CareerScreenVM` hierarchy (TOR-pattern expandable panels, portraits, ability icons, lock chains), `CharacterDeveloperCareerMixin` (UIExtenderEx) for career button with sprite. See [gui-sprite-system.md](gui-sprite-system.md) for full UI details.
- **Battle:** `CareerPerkMissionBehavior` for per-second cooldown tick + `V`-key activation handling. `CareerAbilityService` injects `ICareerConfigProvider` and forces `ChargeType.CooldownOnly` for all 50 careers — readiness is purely cooldown-timer based (see [Cooldown System](#cooldown-system)).
- **Ability effects:** `CareerAbilityEffectRegistry` dispatches to per-career `ICareerAbilityEffectExecutor` implementations. 3 role-based archetypes (Infantry/Ranged/Cavalry) serve all 50 careers with XML-tunable values via `taom_ability_tuning.xml`. All three archetypes apply AoE friendly-troop buffs within a 50-unit radius (standardized in templates): Infantry (damage + damage reduction), Ranged (speed + ranged damage + draw speed), Cavalry (mount speed + charge damage + damage). Buffs applied via `CareerAbilityBuffTracker` with separate hero and ally buff dictionaries (read by `TaomAgentStatCalculateModel` — survives stat recalc).

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
(session/level/death)   (battle tick/charge/ability effects)
  |                           |
CareerCreationHandler   CareerAbilityEffectRegistry
(CC integration)        → InfantryAbilityExecutor (AoE damage + reduction)
  |                     → RangedAbilityExecutor (speed + ranged dmg + draw)
  |                     → CavalryAbilityExecutor (mount speed + charge + dmg)
  |                     → CareerAbilityBuffTracker (hero + ally buffs, read by stat model)
CareerSwitchService     → MissionAbilityExecutionContext (boundary adapter)
(NPC dialogue switching)
  |
GauntletCareerScreen → CareerScreenVM → CareerChoiceGroupObjectVM → CareerChoiceObjectVM
                                      → CareerAbilityEffectVM (ability effects list)
```

## Configuration

### Career Definitions (`Main/_Module/ModuleData/career_system/taom_careers.xml`)

Defines careers with: id, display name, description, portrait sprite, ability template ID, eligible cultures, choice group IDs, root choice id, min clan tier. `max_perk_points` attribute on root element (default 30). (Pre-2026-05-04 the schema also had `charge_type` and `max_charge` — both removed; cooldown is global, not per-career.)

### Choice Trees (`Main/_Module/ModuleData/career_system/taom_career_choices.xml`)

Defines standalone root choices and choice groups. Each group has a tier (1/2/3) and contains choices (Keystone or Passive). Choices can have PassiveEffect (type + magnitude + operation) and Mutations (target template + property + calculator + params).

### Ability Templates (`Main/_Module/ModuleData/career_system/taom_ability_templates.xml`)

Defines per-ability tunables: id, display name, duration (effect window), radius (AoE), max charge (used by mutation system to scale charge thresholds — internal value, not consumed by readiness logic), particle/sound effects, tooltip. Cooldown is *not* per-template; see [Cooldown System](#cooldown-system).

### Cooldown System

`Main/_Module/ModuleData/career_system/taom_ability_tuning.xml` declares a single `<Global cooldown_seconds="30" />` element shared by all 50 careers. Edit to retune.

```xml
<AbilityTuning>
  <Global cooldown_seconds="30" />
  <Infantry .../>
  <Ranged .../>
  <Cavalry .../>
</AbilityTuning>
```

- **Default:** 30 seconds.
- **Validation:** Must be in `(0, 3600]`. Out-of-range, malformed, or missing values fall back to 30s with a `LogWarning` (`CareerConfigProvider.ParseGlobalTuning`).
- **Reload scope:** `CareerConfigProvider` is a `Reuse.Singleton` and caches the parsed config. Changes require a full Bannerlord application restart — not a save-load.
- **Per-career override:** Not supported. Readiness is uniform across all 50 careers by design (UX simplification — see #103 motivation).

In-battle UX:
- Abilities start ready at battle open.
- Pressing `V` while ready: yellow *"<Ability> activated!"* message + buff/sound/particle effect.
- Pressing `V` while on cooldown: throttled gray *"Career ability still charging — Ns remaining"* (one message per 2s).
- One-shot green *"Career ability is ready! Press V to activate"* when the cooldown elapses.

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
| `Main/Features/CareerSystem/Abilities/` (10 files) | CareerAbility, ability service, effect registry, 3 executors, buff tracker, execution context |
| `Main/Features/CareerSystem/CareerCampaignBehavior.cs` | Campaign lifecycle events |
| `Main/Features/CareerSystem/CareerPerkMissionBehavior.cs` | Battle tick + V-key activation + HUD lifecycle (302 LOC; refactor tracked in #102) |
| `Main/Features/CareerSystem/CareerCreationHandler.cs` | Character creation integration |
| `Main/Features/CareerSystem/CareerSwitchService.cs` | Career switching with validation |
| `Main/Features/CareerSystem/UI/` (7 files) | Career screen + VM hierarchy + UIExtenderEx mixin + ability HUD + prefab. See [gui-sprite-system.md](gui-sprite-system.md) |
| `Main/Features/CareerSystem/Models/` (3 files) | TaomAgentApplyDamageModel, TaomAgentStatCalculateModel, TaomClanTierModel |
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
| CareerAbilityTests | 20 | Charge types + cooldown + activation + ReadyProgress01 |
| CareerAbilityServiceTests | 10 | Force-CooldownOnly + configured cooldown duration + GetCooldownRemaining (hero present/absent) + IsAbilityReady transitions |
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

### Retune the global ability cooldown
1. Edit `Main/_Module/ModuleData/career_system/taom_ability_tuning.xml` `<Global cooldown_seconds="N" />` (must be in `(0, 3600]`)
2. Restart Bannerlord (provider caches via `Reuse.Singleton`; save-load is NOT enough)

### Add a new ability icon
See #101 — currently 41 of 50 careers have no PNG. Drop a 256x256 PNG into `Main/_Module/GUI/SpriteParts/ui_taom_career_system/CareerSystem/Abilities/<career_id>_ability.png` and add the corresponding `<Name>CareerSystem\Abilities\<career_id>_ability</Name>` registration in `Main/_Module/GUI/TAOMSpriteData.xml`.
