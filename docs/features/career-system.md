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

### Starting Equipment Override (per-archetype)

After the culture-default starting roster is applied at `OnCharacterCreationFinalize`, the player's career archetype drives a second roster application that overwrites the loadout. The archetype is one of three values:

| Archetype | Weapons | Armor |
|-----------|---------|-------|
| **Ranged** | bow + arrows + sword | light (low armor, very low weight) |
| **Cavalry** | spear + shield + sword + horse + harness | medium (chainmail) |
| **Infantry** | 1H + shield + (2H or spear — culture-decides) | heavy (plate-tier weight) |

**Single source of truth:** [`CareerSystemIoC.GetCareerArchetypeMap()`](../../Main/Features/CareerSystem/CareerSystemIoC.cs) maps each careerId to a `CareerArchetype`. The same dictionary is consumed by the ability executor registry (Infantry/Ranged/Cavalry executors) and by [`ICareerArchetypeService`](../../Main/Features/CareerSystem/ICareerArchetypeService.cs). Cached in a static field — one allocation per app lifetime.

**Roster ID convention:** `player_career_{cultureId}_{infantry|ranged|cavalry}_{f|m}`. Built by [`CareerEquipmentRosterIds.Build`](../../Main/Features/CharacterCreation/CareerEquipmentRosterIds.cs), looked up via `MBObjectManager.GetObject<MBEquipmentRoster>`. Rosters are authored in [`Main/_Module/ModuleData/equipmentsets/taom_career_starting_equipment.xml`](../../Main/_Module/ModuleData/equipmentsets/taom_career_starting_equipment.xml).

**Graceful fallback:** When no roster exists for a given (culture, archetype, gender) combination, [`CareerStartingEquipmentService`](../../Main/Features/CharacterCreation/CareerStartingEquipmentService.cs) logs a warning and leaves the already-applied culture default in place. This lets new cultures ship incrementally without code changes.

**Critical: `FillFrom` does NOT clear unspecified slots.** `Equipment.FillFrom(source)` copies only the slots that are present in the source roster — it does not zero-clear the target's other slots first. This means if your culture-default roster sets a Horse and your career roster does not mention Horse, the horse persists. For archetypes that should be on foot (ranged, infantry), include explicit empty overrides:

```xml
<Equipment slot="Horse" id="" />
<Equipment slot="HorseHarness" id="" />
```

The empty `id=""` resolves to a null `ItemObject`, which `Equipment.DeserializeNode` accepts as an empty slot.

### How to add a new culture's career rosters

1. Create starter armor items in LOTRLOME_Armory at `LOTRLOME_items/<culture>/starter_armors.xml` — 15 items total (3 archetypes × 5 slots: head/body/leg/cape/gloves). Reuse existing meshes; vary weight + armor stats per archetype (ranged ≈ 0.5× source weight, cavalry ≈ 0.75×, infantry ≈ 1.0×). Use the `starter_{archetype}_{culture}_{slot}_a` naming convention — see Gondor [`starter_armors.xml`](file:///E:/Steam/steamapps/common/Mount%20%26%20Blade%20II%20Bannerlord/Modules/LOTRLOME_Armory/ModuleData/LOTRLOME_items/gondor/starter_armors.xml) as the template.
2. **Required cover attributes** — LOTRLOME armor items render their mesh only when the `Armor` element declares it covers the slot:
   - Head items: `hair_cover_type="..."` + `beard_cover_type="..."` (cloth → `type1`/`type2`, plate → `type1`/`all`)
   - Body items: `covers_body="true"` (required) plus optionally `covers_legs="true"` for long robes / `covers_hands="true"` for full gauntlets that extend past the arm
   - **Leg items: `covers_legs="true"` is REQUIRED** — without it the leg mesh does not render, the player appears with bare legs even though the item is equipped
   - **Glove items: `covers_hands="true"` is REQUIRED** — same failure mode for hands
   - Cape items: no cover attribute needed
   - Source-of-truth: cross-check against any existing LOTRLOME `{leg,arm}_armors.xml` entries — every leg item has `covers_legs="true"` and every glove item has `covers_hands="true"`. Don't omit these on duplicates.
3. **Path encoding trap** — the LOTRLOME_Armory path on Windows contains `&` (`Mount & Blade II Bannerlord`). The Write tool has been observed entity-encoding `&` → `&amp;` and silently writing to a phantom directory. After authoring, `ls` the real path to confirm. See `feedback_write_tool_ampersand_path_encoding.md`.
4. Append 6 rosters to [`taom_career_starting_equipment.xml`](../../Main/_Module/ModuleData/equipmentsets/taom_career_starting_equipment.xml) — one per (archetype, gender). Reference existing low-tier culture weapons + the new starter armor. **Don't forget the explicit Horse/HorseHarness clears for ranged + infantry** — `Equipment.FillFrom` is a slot-by-slot merge and will leave culture-default horses in place if you don't override.
5. Verify the archetype for each career in [`CareerSystemIoC.GetCareerArchetypeMap()`](../../Main/Features/CareerSystem/CareerSystemIoC.cs) — adjust if needed.
6. No code change required — `ICareerStartingEquipmentService` looks up by string-id at runtime.

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
| `Main/Features/CareerSystem/Domain/CareerArchetype.cs` | `enum CareerArchetype { Infantry, Ranged, Cavalry }` |
| `Main/Features/CareerSystem/CareerArchetypeService.cs` | careerId → archetype lookup; backed by static map in `CareerSystemIoC` |
| `Main/Features/CharacterCreation/CareerStartingEquipmentService.cs` | Applies archetype roster at end of CC over the culture default |
| `Main/Features/CharacterCreation/CareerEquipmentRosterIds.cs` | Roster ID builder: `player_career_{culture}_{archetype}_{f\|m}` |
| `Main/_Module/ModuleData/equipmentsets/taom_career_starting_equipment.xml` | Per-(culture, archetype, gender) rosters; Gondor only as of 2026-05-19 |

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
