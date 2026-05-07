# TOR_Core Career System — Complete Research Reference

> Reverse-engineered from [TheOldRealms/TOR_Core](https://github.com/TheOldRealms/TOR_Core) `development` branch.  
> TOR targets **Bannerlord v1.3.2** — close to TAOM's **v1.3.15**.

---

## 1. System Overview

```
┌─────────────────────────────────────────────────────────────────────┐
│                         SubModule.cs                                │
│  RegisterType<CareerObject>(103U)                                   │
│  RegisterType<CareerChoiceObject>(104U)                             │
│  RegisterType<CareerChoiceGroupObject>(105U)                        │
│  AddBehavior: TORCareerPerkCampaignBehavior                         │
│  AddBehavior: CareerSwitchCampaignBehavior                          │
│  AddBehavior: CareerDialogOptionsCampaignBehavior                   │
│  AddBehavior: SimpleCareerQuestBehavior                             │
│  AddMissionBehavior: CareerPerkMissionBehavior                      │
└──────────┬──────────────────────────────────────────────────────────┘
           │
           ▼
┌──────────────────────────────────────────────────────────────────────┐
│                    Static Registries (Singletons)                    │
│  TORCareers (22 careers) ──► CareerObject (PropertyObject)          │
│  TORCareerChoiceGroups (~154 groups) ──► CareerChoiceGroupObject    │
│  TORCareerChoices (22 choice classes) ──► CareerChoiceObject        │
└──────────┬──────────────────────────────────────────────────────────┘
           │
           ▼
┌──────────────────────────────────────────────────────────────────────┐
│                    Per-Career Choice Trees                           │
│  TORCareerChoicesBase (abstract)                                    │
│    └─► 22 subclasses (WarriorPriestCareerChoices, etc.)             │
│         Each defines: Root + 6-7 groups × (1 keystone + 4 passives) │
└──────────┬──────────────────────────────────────────────────────────┘
           │
     ┌─────┼─────────────────────┐
     ▼     ▼                     ▼
┌─────────────┐  ┌────────────────┐  ┌──────────────────────────┐
│  Campaign   │  │   Battle/      │  │   UI Layer               │
│  Behaviors  │  │   Mission      │  │                          │
│             │  │                │  │  CareerScreenVM           │
│  Perk CB    │  │  PerkMission   │  │   └─ CareerObjectVM      │
│  Switch CB  │  │  Behavior      │  │       └─ ChoiceGroupVM   │
│  Dialog CB  │  │                │  │           └─ ChoiceVM     │
│  Quest CB   │  │  AbilityMgr    │  │                          │
│             │  │  MissionLogic  │  │  CharDevVMExtension       │
│             │  │                │  │  PartyCharVMExtension     │
│             │  │  CareerAbility │  │  CareerAbilityHUD_VM      │
└─────────────┘  └────────────────┘  └──────────────────────────┘
           │            │                      │
           └────────────┼──────────────────────┘
                        ▼
┌──────────────────────────────────────────────────────────────────────┐
│                    Shared Infrastructure                             │
│  ExtendedInfoManager ──► HeroExtendedInfo (save/load per hero)      │
│  AbilityFactory ──► AbilityTemplate (XML: tor_abilitytemplates.xml) │
│  StatusEffectManager ──► StatusEffectTemplate (XML)                  │
│  CustomResourceManager ──► Per-hero resource tracking                │
│  CareerHelper (50+ methods) ──► Caching, passives, charge calc      │
│  CareerButtonBehaviorBase ──► 13 per-career button behaviors        │
└──────────────────────────────────────────────────────────────────────┘
```

### Initialization Order

1. `SubModule.BeginGameStart()` → Register 3 ObjectManager types
2. `StatusEffectManager.LoadStatusEffects()` → XML
3. `AbilityFactory.LoadTemplates()` → XML (`tor_abilitytemplates.xml`)
4. `ItemTraitManager.LoadItemTraits()` → XML
5. `TORConfig.ReadConfig()` → XML
6. `new TORCareers()` → Register + initialize 22 careers
7. `new TORCareerChoiceGroups()` → Register ~154 groups
8. `new TORCareerChoices()` → Instantiate 22 choice classes → register all choices
9. `SubModule.InitializeGameStarter()` → Add 4 CampaignBehaviors
10. Mission start → Add `CareerPerkMissionBehavior`

---

## 2. Class Reference

### Core Data Objects (all extend `PropertyObject`)

#### CareerObject

| Property | Type | Purpose |
|----------|------|---------|
| `StringId` | string | Unique ID (e.g., "WarriorPriest") |
| `ChargeType` | enum | `CooldownOnly` or `Custom` |
| `MaxCharge` | int | Ability charge cap (e.g., 100–2500) |
| `AbilityTemplateID` | string | References `tor_abilitytemplates.xml` |
| `AbilityScriptType` | Type | C# script class for ability behavior |
| `RootNode` | CareerChoiceObject | Always-active root choice |
| `ChoiceGroups` | List\<CareerChoiceGroupObject\> | All tier-based groups |
| `AllChoices` | List\<CareerChoiceObject\> | Computed: all choices from all groups |
| `_condition` | Predicate\<Hero\> | Eligibility check (culture, clan tier) |
| `_chargeFunction` | delegate | Custom charge calculation |

**Key Methods:**
- `Initialize(name, condition, abilityID, chargeFunction, maxCharge, scriptType)`
- `IsConditionsMet(Hero)` → bool
- `MutateAbility(AbilityTemplate, Agent)` — applies all active choice mutations
- `MutateTriggeredEffect(TriggeredEffectTemplate, Agent)`
- `MutateStatusEffect(StatusEffectTemplate, Agent)`
- `GetCalculatedCareerAbilityCharge(...)` → float

#### CareerChoiceObject

| Property | Type | Purpose |
|----------|------|---------|
| `OwnerCareer` | CareerObject | Parent career |
| `BelongsToGroup` | CareerChoiceGroupObject | Tier group |
| `Type` | ChoiceType | `Keystone` or `Passive` |
| `Passive` | PassiveEffect | Stat bonus (nullable) |
| `_mutations` | List\<MutationObject\> | Template modifications |

**Nested: MutationObject** — modifies ability/effect templates at runtime:

| Field | Type | Purpose |
|-------|------|---------|
| `MutationTargetType` | Type | `typeof(AbilityTemplate)`, `typeof(TriggeredEffectTemplate)`, or `typeof(StatusEffectTemplate)` |
| `MutationTargetOriginalId` | string | Template ID to mutate |
| `PropertyName` | string | Property to modify |
| `PropertyValue` | Func\<choice, originalValue, agent, object\> | Mutation calculator |
| `MutationType` | OperationType | `Add`, `Multiply`, `Replace` |

**Nested: PassiveEffect** — stat bonuses:

| Field | Type | Purpose |
|-------|------|---------|
| `EffectMagnitude` | float | Numeric value |
| `PassiveEffectType` | enum | 44 types (Health, Damage, etc.) |
| `Operation` | OperationType | Add/Multiply/Replace |
| `InterpretAsPercentage` | bool | Apply as % |
| `DamageProportionTuple` | struct | DamageType + Percent + AttackTypeMask |
| `AttackTypeMask` | flags | Melee/Ranged filtering |
| `_specialCombatInteractionFunction` | delegate | Custom combat logic |
| `_specialCharacterEvaluationFunction` | delegate | Custom targeting logic |

#### CareerChoiceGroupObject

| Property | Type | Purpose |
|----------|------|---------|
| `OwnerCareer` | CareerObject | Parent career |
| `Tier` | int | 1, 2, or 3 |
| `Choices` | List\<CareerChoiceObject\> | Choices in this group |
| `_conditionDelegate` | delegate | Group availability check |
| `_unlockDelegate` | delegate | Unlock benefit trigger |

### Static Registries

**TORCareers** — 22 careers:

| Career | Max Charge | Ability | Charge Type |
|--------|-----------|---------|-------------|
| GrailDamsel | 2500 | FeyPaths | Spell |
| GrailKnight | 100 | KnightlyCharge | Custom |
| BloodKnight | 10 | RedFury | Kills |
| MinorVampire | 800 | ShadowStep | DamageDone |
| WarriorPriest | 300 | RighteousFury | Melee |
| Mercenary | cooldown | LetThemHaveIt | CooldownOnly |
| WitchHunter | 200 | Accusation | DamageDone |
| Necromancer | 2000 | GreaterHarbinger | DamageDone/Healed |
| BlackGrailKnight | 100 | KnightlyCharge | Custom |
| Necrarch | 1500 | BlastOfAgony | DamageDone/Healed |
| WarriorPriestUlric | 400 | AxeOfUlric | Melee |
| ImperialMagister | 120 | ArcaneConduit | Custom |
| Waywatcher | 1200 | LethalShot | Ranged |
| Spellsinger | 1500 | WrathOfTheWood | Spell |
| GreyLord | 1000 | MindControl | Spell |
| KnightOldWorld | 1200 | KnightlyStrike | DamageDone |
| Ironbreaker | 50 | Impenetrable | DamageTaken |
| Slayer | 500 | DoomSeeking | Kills/DamageTaken |
| Warden | 100 | HawkEye | Custom |
| Runelord | 100 | WisdomOfThungni | Kills |
| OrcBoss | 100 | ArmedToDaTeef | Custom |
| OrcShaman | 100 | CallOfDaGreen | Custom |

**TORCareerChoiceGroups** — ~154 groups (7 per career × 22 careers), organized by tier.

**TORCareerChoices** — 22 `TORCareerChoicesBase` subclasses, each defining root + all choices for one career.

### Enums

**ChoiceType:** `Keystone`, `Passive`

**OperationType:** `Add`, `Multiply`, `Replace`, `None`

**PassiveEffectType (44 values):**
`Special`, `Health`, `HealthRegeneration`, `Damage`, `Resistance`, `AccuracyPenalty`, `RangedMovementPenalty`, `ArmorPenetration`, `HorseHealth`, `HorseChargeDamage`, `WindsOfMagic`, `WindsCostReduction`, `WindsRegeneration`, `BuffDuration`, `DebuffDuration`, `SpellRadius`, `SpellEffectiveness`, `WindsCooldownReduction`, `PrayerCoolDownReduction`, `PartyMovementSpeed`, `PartySpottingRange`, `PartySize`, `CompanionLimit`, `TroopDamage`, `TroopResistance`, `TroopRegeneration`, `TroopMorale`, `TroopWages`, `TroopUpgradeCost`, `Ammo`, `SwingSpeed`, `EquipmentWeightReduction`, `TroopSkill`, `MovementSpeed`, `ShruggedOff`, `InventoryCapacity`, `BonusDamageShield`, `BattleRenownGain`, `MoraleDamageToEnemyOnKill`, `EnchantmentCostReduction`, `UnitPartyWeight`, `StealthBonus`, `CustomResourceUpkeepModifier`, `CustomResourceUpgradeCostModifier`, `CustomResourceGain`

**ChargeCollisionFlag:** `None`, `HitShield`, `HeadShot`

### Campaign Behaviors

| Behavior | Events | Purpose |
|----------|--------|---------|
| `TORCareerPerkCampaignBehavior` | OnSessionLaunched, DailyTick, WeeklyTick, ItemsLooted, UnitRecruited, PlayerBattleEnd, ItemDuplicated, EquipmentSmelted, ItemCrafted, ItemsRefined, HourlyTick | Main perk lifecycle — triggers career bonuses on campaign events |
| `CareerSwitchCampaignBehavior` | Custom dialogs | NPC-initiated career switching (vampire transformation, knight promotion) |
| `CareerDialogOptionsCampaignBehavior` | Custom dialogs | GrailDamsel envoy dialogue, career button dialogues |
| `SimpleCareerQuestBehavior` | HourlyTick | Launch career-specific Ink stories (OrcBoss, OrcShaman) |

### Mission Behavior

`CareerPerkMissionBehavior` — ticks every 1 second during battles. Per-career combat effects:
- Necrarch: spell damage charges ability
- GreyLord: FellfangMark siege kills
- Ironbreaker: impenetrable block tracking, damage reflection
- WitchHunter: accusation mark application/AOE
- Slayer: casualty tracking
- Vampire: Winds of Magic regen/loss

### CareerHelper (50+ static methods)

**Caching:**
- `RefreshCareerChoicesCache()` — session-scoped lookup by PassiveEffectType
- `GetCachedChoicesByType(PassiveEffectType)` → O(1) lookup

**Passive Application:**
- `ApplyBasicCareerPassives(Hero, ref ExplainedNumber, PassiveEffectType, AttackTypeMask, asFactor)`
- `ApplyCombatCareerPassives(Hero, ref ExplainedNumber, PassiveEffectType, Agent attacker, Agent victim)`
- `ApplyCareerPassivesForDamageValues(Agent, Agent, AttackTypeMask, PropertyMask)` → float[]

**Charge System:**
- `ApplyCareerAbilityCharge(amount, ChargeType, AttackTypeMask, affector, affected, collision)`
- `CalculateChargeForCareer(...)` → float

**Career Detection:**
- `IsMagicCapableCareer(CareerObject)` → bool
- `IsPriestCareer(CareerObject)` → bool

**Permanent Effects:**
- `PowerstoneEffectAssignment(Agent)` — Imperial Magister
- `PuritySealAssignment(Agent)` — Knight
- `UnitRuneAssignment(Agent)` — Runelord
- `ExtorsionAssignment(Agent)` — Greenskin

---

## 3. Data Schema

### Career Definition Pattern: HYBRID (Code + XML)

| Component | Defined In | Format |
|-----------|-----------|--------|
| Careers | `TORCareers.cs` | C# static initialization |
| Choice trees | `Choices/*.cs` (22 files) | C# per-career class |
| Choice groups | `TORCareerChoiceGroups.cs` | C# static initialization |
| Ability templates | `tor_abilitytemplates.xml` | XML (269 KB) |
| Status effects | `tor_statuseffects.xml` | XML (78 KB) |
| Item traits | `tor_itemtraits.xml` | XML (302 KB) |
| CC options | `tor_cc_options.xml` | XML (102 KB) |
| Config | `tor_config.xml` | XML (1 KB) |
| Strings | `tor_strings.xml` | XML (826 KB) |

### AbilityTemplate Schema (XML)

```xml
<AbilityTemplate StringID="RighteousFury"
    Name="{=str_key}Righteous Fury"
    SpriteName="righteous_fury_icon"
    CoolDown="10"
    WindsOfMagicCost="0"
    BaseMisCastChance="0"
    Duration="8"
    Radius="5"
    AbilityType="CareerAbility"
    AbilityEffectType="CareerAbilityEffect"
    BaseMovementSpeed="0"
    TickInterval="-1"
    TriggerType="TickOnce"
    HasLight="true"
    LightIntensity="50"
    LightRadius="10"
    ParticleEffectPrefab="righteous_fury_particle"
    SoundEffectToPlay="righteous_fury"
    CastType="Instant"
    CastTime="0"
    AnimationActionName="act_release_stone"
    AbilityTargetType="Self"
    CrosshairType="Self"
    MaxDistance="0"
    SpellTier="0"
    TooltipDescription="{=str_key}Empowers you and nearby allies...">
    <LightColorRGB x="255" y="200" z="50" w="-1" />
    <TriggeredEffect>apply_righteous_fury</TriggeredEffect>
</AbilityTemplate>
```

**Key AbilityTemplate attributes:**

| Attribute | Type | Values |
|-----------|------|--------|
| AbilityType | enum | `Innate`, `Spell`, `Prayer`, `ItemBound`, `CareerAbility` |
| AbilityEffectType | enum | `Projectile`, `Missile`, `SeekerMissile`, `Wind`, `Vortex`, `Heal`, `Augment`, `Hex`, `Summoning`, `Bombardment`, `Blast`, `ArtilleryPlacement`, `TimeWarpEffect`, `CareerAbilityEffect`, `TacticalReposition` |
| CastType | enum | `Instant`, `WindUp`, `Channel` |
| AbilityTargetType | enum | `Self`, `SingleEnemy`, `SingleAlly`, `EnemiesInAOE`, `AlliesInAOE`, `WorldPosition`, `GroundAtPosition` |
| CrosshairType | enum | `Self`, `Missile`, `SingleTarget`, `Wind`, `Pointer`, `TargetedAOE` |

### Complete Career Definition Example: Warrior Priest

```csharp
// TORCareers.cs — Registration
_warriorPriest.Initialize(
    "Warrior Priest of Sigmar",
    hero => hero.Culture == GetCulture(TORConstants.Cultures.EMPIRE)
         && hero.Clan.Tier >= 1,
    "RighteousFury",                                    // AbilityTemplateID
    CareerAbilityChargeSupplier.WarriorPriestCareerCharge, // charge function
    300);                                                // max charge

// WarriorPriestCareerChoices.cs — Root node
_warriorPriestRoot.Initialize(
    CareerID,
    "Unleash Sigmar's wrath with Righteous Fury! +20% melee damage for 8s...",
    null,           // no group (root)
    true,           // is root
    ChoiceType.Keystone,
    new List<MutationObject>() {
        new MutationObject() {
            MutationTargetType = typeof(AbilityTemplate),
            MutationTargetOriginalId = "RighteousFury",
            PropertyName = "Duration",
            PropertyValue = (choice, orig, agent) =>
                CareerHelper.AddSkillEffectToValue(choice, agent,
                    new List<SkillObject>() { TORSkills.Faith }, 0.05f),
            MutationType = OperationType.Add
        }
    });

// Tier 1 Keystone
_bookOfSigmarKeystone.Initialize(
    CareerID,
    "Righteous Fury is now also charged by melee attacks.",
    "BookOfSigmar",   // group name
    false,
    ChoiceType.Keystone,
    new List<MutationObject>() { /* ability mutations */ },
    null);

// Tier 1 Passive
_bookOfSigmarPassive2.Initialize(
    CareerID,
    "+20% personal melee Physical damage.",
    "BookOfSigmar",
    false,
    ChoiceType.Passive,
    null,
    new PassiveEffect(PassiveEffectType.Damage,
        new DamageProportionTuple(DamageType.Physical, 20),
        AttackTypeMask.Melee));
```

### Tree Structure

```
CareerRoot (always active — defines base ability + mutations)
├── Tier 1: 6-7 groups (mutually exclusive keystones)
│   ├── Group A: 1 Keystone + 4 Passives
│   ├── Group B: 1 Keystone + 4 Passives
│   └── ... (pick 1 group)
├── Tier 2: 6-7 groups (same pattern)
│   └── ... (pick 1 group, gated by CareerTier2 attribute)
└── Tier 3: 6-7 groups (same pattern)
    └── ... (pick 1 group, gated by CareerTier3 attribute)
```

**Progression:** `MaxChoices = Math.Min(hero.Level + 1, MaxPerkPoints + 1)` — one point per level.

**Tier gating:** `hero.HasAttribute("CareerTier" + tier)` — unlocked via quests/story.

---

## 4. Flow Diagrams

### Career Selection (Character Creation)

```
CC Stage 3 (Specialization)
  │
  ├─ TORCharacterCreationContentHandler.OnOptionSelected(careerID)
  │    ├─ hero.AddCareer(careerObject)
  │    │    ├─ HeroExtendedInfo.CareerID = careerID
  │    │    ├─ Clear existing choices
  │    │    ├─ Add root node to CareerChoices
  │    │    └─ InitialCareerSetup() → attributes, abilities, spells
  │    └─ Cache spawn position per career
  │
  └─ Game starts → TORCareerPerkCampaignBehavior.OnSessionLaunched()
       └─ CareerHelper.RefreshCareerChoicesCache()
```

### Career Progression / Leveling

```
Hero levels up → gains 1 career choice point
  │
  ├─ Player opens Character Developer
  │    └─ CharacterDeveloperVMExtension shows "Career" button (HasCareer=true)
  │
  ├─ Click → ExecuteNavigateToCareers()
  │    ├─ Save character changes
  │    └─ Push CareerScreenGameState
  │
  ├─ CareerScreenVM loads
  │    ├─ CurrentCareer → CareerObjectVM
  │    ├─ ChoiceGroupsTier1/2/3 → CareerChoiceGroupObjectVM[]
  │    └─ FreeCareerPoints = max - selected count
  │
  ├─ Player clicks + on a group
  │    └─ ExecuteClickIncrease() → SelectChoice()
  │         ├─ hero.TryAddCareerChoice(choice)
  │         │    ├─ Validate: not duplicate, not at max
  │         │    ├─ Add to HeroExtendedInfo.CareerChoices
  │         │    └─ RefreshCareerChoicesCache()
  │         └─ RefreshValues() → update UI
  │
  └─ Click Done → pop state
```

### Ability Activation (Battle)

```
Hot key pressed
  │
  ├─ AbilityManagerMissionLogic checks charge
  │    └─ CareerAbility.IsCharged?
  │         ├─ CooldownOnly: timer elapsed
  │         └─ Custom: _currentCharge >= _maxCharge
  │
  ├─ Enter Targeting mode
  │    ├─ Slow time to 0.3x
  │    ├─ Sheath weapons (cache wielded)
  │    └─ Show crosshair (type from template)
  │
  ├─ Player confirms target
  │    ├─ Clone ability template
  │    ├─ career.MutateAbility(template, agent)
  │    │    ├─ Apply root node mutations
  │    │    └─ Apply selected choice mutations
  │    └─ Execute ability script
  │
  ├─ Ability fires
  │    ├─ Spawn particles, play sound
  │    ├─ Apply triggered effects
  │    └─ Apply status effects (also mutated)
  │
  └─ Reset
       ├─ _currentCharge = 0
       ├─ Restore weapons
       └─ Restore time scale
```

### Ability Charging

```
Combat event occurs (damage dealt, kill, damage taken, spell cast)
  │
  ├─ CareerAbilityChargeSupplier.[Career]CareerCharge() called
  │    ├─ Check charge type match
  │    ├─ Apply career-specific modifiers:
  │    │    ├─ Ranged penalty (-50% for WitchHunter)
  │    │    ├─ Headshot bonus (+100%)
  │    │    ├─ Keystone modifiers (-10% per keystone for Slayer)
  │    │    ├─ Companion penalty (-95% for Waywatcher)
  │    │    └─ etc.
  │    └─ Return charge amount
  │
  ├─ _currentCharge += amount (capped at MaxCharge)
  │
  └─ CareerAbilityHUD_VM.RefreshValues()
       └─ ChargeLevel = _currentCharge / _maxCharge * 100
```

### Save/Load

```
Save:
  Campaign.SaveData()
    └─ ExtendedInfoManager.SyncData(IDataStore)
         └─ Per hero: HeroExtendedInfo.SyncData()
              ├─ [SaveableField] CareerID (string)
              ├─ [SaveableField] CareerChoices (List<string>)
              ├─ [SaveableField] AcquiredAbilities (List<string>)
              └─ [SaveableField] CustomResources (Dict<string, float>)

Load:
  Campaign.LoadData()
    └─ ExtendedInfoManager.SyncData(IDataStore)
         └─ HeroExtendedInfo restored per hero
              └─ OnSessionLaunched → RefreshCareerChoicesCache()
```

---

## 5. UI Components

### Prefabs

| File | Purpose | Trigger |
|------|---------|---------|
| `GUI/Prefabs/CareerSystem/CareerScreen.xml` | Main career screen — left panel (career info, ability), right panel (3-tier choice tree) | CharacterDeveloper "Career" button |
| `GUI/Prefabs/CharacterDeveloper/CharacterDeveloper.xml` | Vanilla override — adds career + spellbook buttons top-right | Native load |
| `GUI/Prefabs/Party/PartyTroopTuple.xml` | Troop rows — career action button per troop | Party screen |
| `GUI/Prefabs/TORSpecializationStage.xml` | Career selection during CC | CC Stage 3 |
| `GUI/Prefabs/AbilitySystem/AbilityHUD.xml` | Battle HUD — ability icon, charge bar, cooldown | Mission start |

### ViewModels

| Class | Binds To | Key Properties |
|-------|----------|----------------|
| `CareerScreenVM` | CareerScreen.xml | CurrentCareer, HasBattlePrayers |
| `CareerObjectVM` | Nested in CareerScreenVM | Name, Description, SpriteName, AbilityEffects, ChoiceGroupsTier1/2/3, FreeCareerPoints |
| `CareerChoiceGroupObjectVM` | ChoiceGroupsTier* list items | GroupName, IsActive, Choices, ButtonsVisible |
| `CareerChoiceObjectVM` | Choices list items | Name, Description, IconSprite, IsTaken, IsFreeToTake |
| `CareerAbilityEffectVM` | AbilityEffects list items | LineText |
| `CareerAbilityHUD_VM` | AbilityHUD.xml | IsVisible, ChargeLevel (0-100%), CareerAbility |
| `CharacterDeveloperVMExtension` | CharacterDeveloper.xml | HasCareer, IsSpellcaster |
| `PartyCharacterVMExtension` | PartyTroopTuple.xml | ShouldButtonBeVisible, IsButtonEnabled, SpriteTORButton |

### Career Screen Layout

```
┌──────────────────────────────────────────────────────────────────┐
│  Career Screen Title (top, animated)                             │
├─────────────────────┬────────────────────────────────────────────┤
│  Left Panel (500px) │  Right Panel (1420px)                      │
│                     │                                            │
│  Career Name        │  ┌─ Tier 3 ──────────────────────────────┐│
│  Career Portrait    │  │  [Group][Group][Group]...  🔒 locked  ││
│  (400×200)          │  └────────────────────────────────────────┘│
│                     │  ┌─ Tier 2 ──────────────────────────────┐│
│  Description        │  │  [Group][Group][Group]...  🔒 locked  ││
│                     │  └────────────────────────────────────────┘│
│  ─────────────────  │  ┌─ Tier 1 ──────────────────────────────┐│
│  Career Ability     │  │  [Group A]  [Group B]  [Group C] ...  ││
│  Ability Icon       │  │   ┌──────────────────────┐            ││
│  (120×120)          │  │   │ Keystone (gold/brown) │ ← hover   ││
│  Ability Name       │  │   │ Passive 1             │   expands ││
│  Effect Line 1      │  │   │ Passive 2             │   to      ││
│  Effect Line 2      │  │   │ Passive 3             │   750px   ││
│  ...                │  │   │ Passive 4             │            ││
│                     │  │   │ [+] [-] buttons       │            ││
│                     │  │   └──────────────────────┘            ││
│                     │  └────────────────────────────────────────┘│
│                     │                       Free Points: N       │
├─────────────────────┴────────────────────────────────────────────┤
│  [Done] (bottom, animated)                                       │
└──────────────────────────────────────────────────────────────────┘
```

Choice states: brown (#7f695cFF) = available, gold (#dfc395FF) = taken. Groups animate from 80px to 750px on hover.

### Career Button System

`CareerButtonBehaviorBase` (abstract) → 13 per-career subclasses:

| Career | Button Action | Visibility |
|--------|--------------|------------|
| Mercenary | Recruit T5+ companion | PaymasterPassive4 + T5 non-hero |
| ImperialMagister | Assign power stones | Has power stones in inventory |
| Runelord | Assign runes | Has rune items |
| OrcBoss | Extortion | Leadership + target is non-hero |
| (etc.) | Career-specific actions | Career-specific conditions |

### VM Extension System (NOT UIExtenderEx)

TOR implements its own `ViewModelExtensionManager` with Harmony patches on ViewModel base class:
- `ViewModel.GetPropertyValue` → postfix delegates to extension
- `ViewModel.SetPropertyValue` → prefix delegates to extension
- `ViewModel.ExecuteCommand` → prefix delegates to extension
- `ViewModel.Constructor` → postfix creates extension instances
- `ViewModel.OnFinalize` → prefix cleans up extensions

Extensions registered via `[ViewModelExtension(typeof(TargetVM), "RefreshMethod")]` attribute.

---

## 6. Harmony Patches

| Patch Class | Target | Type | Purpose |
|------------|--------|------|---------|
| ViewModelPatches | ViewModel.ctor | Postfix | Initialize VM extension instances |
| ViewModelPatches | ViewModel.OnFinalize | Prefix | Finalize VM extensions |
| ViewModelPatches | ViewModel.GetViewModelAtPath | Postfix | Delegate path resolution to extension |
| ViewModelPatches | ViewModel.GetPropertyValue | Postfix | Delegate property reads to extension |
| ViewModelPatches | ViewModel.SetPropertyValue | Prefix | Delegate property writes to extension |
| ViewModelPatches | ViewModel.ExecuteCommand | Prefix | Delegate commands to extension |
| PerkResetRelatedPatch | PerkResetCampaignBehavior.ClearPermanentBonusesIfExists | Postfix | Clear career attributes on perk reset |
| MissionPatches | Mission.FallDamageCallback | Prefix | No fall damage for vampires |
| MissionPatches | Mission.SetPlayerCanTakeControlOfAnotherAgentWhenDead | Prefix | Disable dead agent takeover |
| MissionPatches | HideoutCinematicController.StartCinematic | Prefix | Init boss fight abilities |
| MissionPatches | MissionAgentSpawnLogic.IsSideDepleted | Postfix | Prevent depletion with summoned agents |
| MissionPatches | Mission.RetreatMission | Prefix | Block retreat during Necromancer Champion |
| MissionPatches | MissionEquipment.SelectWeaponPickUpSlot | Prefix | Block troll race-locked weapons |
| MissionPatches | CampaignAgentComponent.OwnerParty (getter) | Prefix | Route summoned agents to correct party |
| ScoreboardBaseVMPatches | ScoreboardBaseVM.OnMainHeroDeath | Prefix | Don't show death UI for summoned champion |
| RefinementVMOnSelectActionPatch | RefinementVM.OnSelectAction | Postfix | Update refinement VM extension |

---

## 7. Bannerlord Integration Points

| System | How Career Hooks In |
|--------|-------------------|
| **ObjectManager** | 3 custom types registered (Career, CareerChoice, CareerChoiceGroup) |
| **CampaignBehaviorBase** | 4 behaviors subscribed to campaign events |
| **MissionBehavior** | CareerPerkMissionBehavior ticks during battles |
| **GameModel overrides** | 21 GameModels call `CareerHelper.ApplyBasicCareerPassives()` |
| **Save system** | HeroExtendedInfo uses `[SaveableField]` + `SyncData(IDataStore)` |
| **Character creation** | Custom CC stage handler for career selection |
| **ViewModel** | 6 Harmony patches on ViewModel base class for extension system |
| **GameState** | `CareerScreenGameState` pushed via `GameStateManager` |
| **ExplainedNumber** | All career passives applied via `ExplainedNumber.Add/AddFactor` |

### GameModels Modified by Career Perks (21 total)

| Model | Career Effects |
|-------|---------------|
| TORCharacterStatsModel | Max health bonuses |
| TORAgentStatCalculateModel | Ammo, movement, swing speed, accuracy, stealth, weight |
| TORAgentApplyDamageModel | Damage calculations |
| TORAbilityModel | Spell radius, effectiveness, duration, winds cost/regen |
| TORStrikeMagnitudeModel | Strike damage |
| TORBattleMoraleModel | Battle morale effects |
| TORBattleRewardModel | Post-battle rewards |
| TORCombatXpModel | XP multipliers |
| TORPartyMoraleModel | Party morale |
| TORPartySizeModel | Party size limits |
| TORPartySpeedCalculatingModel | Movement speed |
| TORPartyWageModel | Troop wages |
| TORPartyTroopUpgradeModel | Upgrade costs |
| TORPartyHealingModel | Healing rates |
| TORMobilePartyFoodConsumptionModel | Food consumption |
| TORClanFinanceModel | Income/expenses |
| TORClanTierModel | Clan tier progression |
| TORSmithingModel | Crafting bonuses |
| TORInventoryCapacityModel | Inventory size |
| TORMapVisibilityModel | Map visibility |
| TORRaidModel | Raid mechanics |

---

## 8. Version Compatibility Notes (TOR v1.3.2 → TAOM v1.3.15)

| Area | Status | Notes |
|------|--------|-------|
| ObjectManager.RegisterType | **Check** | Type IDs (103U, 104U, 105U) must not conflict with TAOM's existing registrations |
| PropertyObject inheritance | **Safe** | Core base class, stable between 1.3.x |
| CampaignBehaviorBase events | **Check** | Some event signatures may differ — verify `OnUnitRecruited`, `ItemDuplicated` |
| MissionBehavior | **Safe** | Stable API |
| ViewModel base class | **Check** | Harmony patches on ViewModel.GetPropertyValue etc. may need signature verification |
| GameStateManager | **Safe** | Stable API |
| ExplainedNumber | **Safe** | Stable API |
| [SaveableField] + SyncData | **Safe** | Stable save system API |
| CharacterCreation stages | **Check** | CC API changed slightly between 1.3.x versions |
| Gauntlet prefab format | **Safe** | XML prefab format stable |

**Key risk:** ViewModel Harmony patches — TOR patches 6 methods on `ViewModel` base class. If Bannerlord 1.3.15 changed any method signatures, these patches silently fail. Verify signatures via decompilation.

---

## 9. Dependency Boundary

### Hard Dependencies (career won't work without these)

| System | What Career Needs | Lore-Specific? | TAOM Equivalent |
|--------|------------------|----------------|-----------------|
| **ExtendedInfoManager + HeroExtendedInfo** | Per-hero data persistence (CareerID, choices, abilities) | No | Must build — no BL equivalent |
| **AbilitySystem** | Ability instantiation, casting, crosshair, cooldown, charge | No | Must build ability framework |
| **AbilityTemplates (XML)** | Career ability definitions | No | Own template XML |
| **HeroExtensions** | GetCareer(), AddCareer(), HasCareerChoice(), etc. | No | Thin extension layer |
| **StatusEffectSystem** | Apply buffs/debuffs during battles | No | Must build effect system |
| **CareerHelper** | Passive application, caching, charge calculation | No | Port directly |

### Soft Dependencies (career references but can decouple)

| System | What Career Uses | Lore-Specific? | TAOM Approach |
|--------|-----------------|----------------|---------------|
| CustomResourceManager | Winds of Magic, Oathgold, Teef | Yes | Simplify or replace with gold |
| Religion System | Priest career devotion checks | Yes | Remove priest-faith coupling |
| Race Attributes | IsOrc(), IsDwarf() checks | Yes | Replace with generic attribute checks |
| Ink narrative | OrcBoss/OrcShaman quest stories | Yes | Optional — skip initially |
| Triggered effects | Mutation targets | No | Build if needed for complex abilities |

### Not Dependencies (career doesn't use)

Custom settlements, bounty master, hireling system (beyond career switch dialogs), main menu.

---

## 10. Full Implementation Scope

Every component below is required for the complete TAOM career system. No subsystems deferred.

### Layer 1: Foundation

| Component | Complexity | What It Does |
|-----------|-----------|--------------|
| **HeroExtendedInfo + persistence** | Large | Per-hero data storage (CareerID, choices, abilities, custom resources). `[SaveableField]` + `SyncData(IDataStore)`. Must handle missing-data-on-load for existing saves. |
| **ExtendedInfoManager** | Medium | Campaign behavior managing HeroExtendedInfo lifecycle (create on hero birth, clear on death, sync on save/load). |
| **Data classes** (CareerObject, CareerChoiceObject, CareerChoiceGroupObject) | Medium | `PropertyObject` subclasses registered with ObjectManager. Core data model for careers, choices, groups. |

### Layer 2: Career Logic

| Component | Complexity | What It Does |
|-----------|-----------|--------------|
| **PassiveEffect system** | Large | 44-type enum + ExplainedNumber integration. Every passive type must wire into TAOM's existing 16+ GameModels via `CareerHelper.ApplyBasicCareerPassives()`. |
| **CareerHelper** | Large | 50+ static methods: caching, passive application, charge calculation, combat interactions, permanent effect assignment. Central glue layer. |
| **Mutation system** | Medium | Runtime modification of ability/effect/status templates. MutationObject with target type, property name, calculator function, operation type. |
| **Career definitions** (per-career choice classes) | Large (volume) | One `TaomCareerChoicesBase` subclass per career. Each defines root node + all choice groups with keystones, passives, and mutations. Data-driven XML preferred over TOR's code-driven approach. |

### Layer 3: Campaign Integration

| Component | Complexity | What It Does |
|-----------|-----------|--------------|
| **CareerPerkCampaignBehavior** | Large | Event hooks: DailyTick, WeeklyTick, HourlyTick, ItemsLooted, UnitRecruited, PlayerBattleEnd, ItemCrafted, EquipmentSmelted, ItemsRefined. Triggers career bonuses on campaign events. |
| **CareerSwitchCampaignBehavior** | Medium | NPC dialogue-initiated career switching. Clear choices, reset to root, validate faction/race eligibility. |
| **CareerDialogCampaignBehavior** | Small | Career-specific dialogue options (envoys, special interactions). |
| **CC integration** | Medium | Career selection during character creation Stage 3. Sets CareerID, adds root node, applies initial attributes/abilities. |
| **Career quests** | Medium | Per-career quest triggers (narrative events at game start or milestones). |

### Layer 4: Battle Integration

| Component | Complexity | What It Does |
|-----------|-----------|--------------|
| **CareerPerkMissionBehavior** | Large | Per-second tick during battles. Per-career combat effects: mark application, damage reflection, kill tracking, charge-on-spell-hit, etc. |
| **Career ability system** | Large | CareerAbility class: charge types (CooldownOnly, DamageDone, Kills, DamageTaken, Healed, Custom), per-career charge suppliers, double-use keystones, mounted/weapon restrictions. |
| **AbilityManagerMissionLogic** | Large | Targeting mode (slow time to 0.3x, crosshair), casting flow, weapon sheath/restore, spell cast sessions. |
| **Ability templates (XML)** | Medium | `taom_abilitytemplates.xml` defining all career abilities: effect type, cast type, cooldown, radius, particles, sound, targeting. |
| **Status effect integration** | Medium | Career buffs/debuffs applied as status effects during battle. Permanent effects assigned on mission start. |

### Layer 5: UI

| Component | Complexity | What It Does |
|-----------|-----------|--------------|
| **CareerScreen** (prefab + GameState) | Large | Full career screen: left panel (career info, ability display), right panel (3-tier choice tree with animated expanding groups). |
| **CareerScreenVM hierarchy** | Large | CareerScreenVM → CareerObjectVM → CareerChoiceGroupObjectVM → CareerChoiceObjectVM → CareerAbilityEffectVM. |
| **ViewModel extension system** | Medium | Either port TOR's custom 6-patch system or implement via UIExtenderEx. Needs: CharacterDeveloper extension (career button), PartyCharacter extension (career action button). |
| **Career button system** | Medium | CareerButtonBehaviorBase + per-career subclasses. Party screen troop interaction buttons with career-specific actions, visibility, and enabled logic. |
| **Battle HUD** | Small | Career ability display: icon, charge bar, cooldown timer. CareerAbilityHUD_VM. |
| **Brushes & sprites** | Small | Career-specific brushes, career illustrations, ability icons, choice icons, tier lock overlay. |

### Layer 6: GameModel Integration

Career passives must wire into TAOM's existing GameModels. For each model, add `CareerHelper.ApplyBasicCareerPassives()` calls:

| Existing TAOM Model | Career PassiveEffectTypes to Support |
|---------------------|--------------------------------------|
| TaomCharacterStatsModel | Health |
| TaomPartyWageModel | TroopWages |
| TaomPartySizeModel | PartySize |
| TaomPartySpeedModel | PartyMovementSpeed, MovementSpeed |
| TaomPartyMoraleModel | TroopMorale |
| TaomBattleRewardModel | BattleRenownGain |
| TaomPartyTroopUpgradeModel | TroopUpgradeCost |
| TaomPartyHealingModel | HealthRegeneration, TroopRegeneration |
| TaomFoodConsumptionModel | (food reduction passives) |
| TaomSmithingModel | EnchantmentCostReduction |
| TaomClanFinanceModel | CustomResourceUpkeepModifier |
| TaomSettlementProsperityModel | (settlement passives if applicable) |
| TaomRaidModel | TroopDamage |
| TaomCombatSimulationModel | Damage, Resistance |
| TaomMilitaryPowerModel | (troop power scaling) |
| TaomTournamentModel | (tournament bonuses if applicable) |
| **New: TaomAgentStatModel** | Ammo, SwingSpeed, ArmorPenetration, StealthBonus, EquipmentWeightReduction |
| **New: TaomAbilityModel** | SpellRadius, SpellEffectiveness, BuffDuration, DebuffDuration |
| **New: TaomMapVisibilityModel** | PartySpottingRange |
| **New: TaomInventoryModel** | InventoryCapacity |

### Implementation Dependencies (Build Order)

```
Layer 1: Foundation
  HeroExtendedInfo ──► ExtendedInfoManager ──► Data classes
                                                    │
Layer 2: Career Logic                               │
  PassiveEffect ◄────────────────────────────────────┘
  CareerHelper ◄── PassiveEffect + Data classes
  Mutation system ◄── Data classes
  Career definitions ◄── ALL of Layer 2
                            │
Layer 3: Campaign    ◄──────┘
  CC integration
  CampaignBehaviors
  Career switching
  Career quests
                            │
Layer 4: Battle      ◄──────┘
  Ability templates (XML)
  Career ability system
  AbilityManagerMissionLogic
  CareerPerkMissionBehavior
  Status effects
                            │
Layer 5: UI          ◄──────┘
  VM extension system
  CareerScreen + VMs
  Career buttons
  Battle HUD
  Brushes & sprites
                            │
Layer 6: GameModels  ◄──────┘ (can start in parallel with Layer 3+)
  Wire PassiveEffectTypes into existing TAOM models
  Build new models where needed
```

---

*Generated from TOR_Core `development` branch, April 2026.*
