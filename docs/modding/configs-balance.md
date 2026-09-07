# Balance configs

## What this file is

The balance configs are the fifteen small JSON and XML files that hold TAOM's tuning numbers: party-size weight per troop, resource prices, starting gold, tier power, food and gold economics, revolt thresholds, combat curves, lifespans, child counts, promotion rules, recruitment gates, enlisted duty difficulty and career pip magnitudes. Each one is read once by a TAOM config provider under `Main/Features/`, validated field by field, and then cached for the rest of the Bannerlord process. None of them is read by the engine, so nothing here follows the `<XmlName id>` registration rules that the content files in [File catalogue](file-catalogue.md) do.

## Where it lives and how it is registered

All fifteen live under [`Main/_Module/ModuleData/`](../../Main/_Module/ModuleData/) in the repo. They are copied into the game module by the build, so you edit the repo copy, not the installed one.

<!-- engine-ref type="TAOM config providers" file="Main/Features/<Feature>/<Feature>ConfigProvider.cs" lines="see the Read at column" -->

| File | Provider | Registered | MCM group |
|---|---|---|---|
| [`TroopWeights/troop_weights.xml`](../../Main/_Module/ModuleData/TroopWeights/troop_weights.xml) | `TroopWeightXmlLoader` | `TroopWeightIoC.cs:11` | Troop Weight (toggle only) |
| [`special_resources/special_resources_config.xml`](../../Main/_Module/ModuleData/special_resources/special_resources_config.xml) | `SpecialResourceConfigProvider` | `SpecialResourcesIoC.cs:10` | none |
| [`special_resources/troop_resource_costs.xml`](../../Main/_Module/ModuleData/special_resources/troop_resource_costs.xml) | `SpecialResourceConfigProvider` | `SpecialResourcesIoC.cs:10` | none |
| [`startup_resources/startup_resources_config.xml`](../../Main/_Module/ModuleData/startup_resources/startup_resources_config.xml) | `StartupResourcesConfigProvider` | `StartupResourcesIoC.cs:13` | none |
| [`configs/battle_balance_config.json`](../../Main/_Module/ModuleData/configs/battle_balance_config.json) | `BattleBalanceConfigProvider` | `BattleBalanceIoC.cs:9` | Battle Balance/Troop Power, Battle Balance/Casualty Ratios |
| [`settlement_food/settlement_food_config.json`](../../Main/_Module/ModuleData/settlement_food/settlement_food_config.json) | `SettlementFoodConfigProvider` | `SettlementFoodIoC.cs:9` | Settlement Food (toggle only) |
| [`settlement_economy/settlement_economy_config.json`](../../Main/_Module/ModuleData/settlement_economy/settlement_economy_config.json) | `SettlementEconomyConfigProvider` | `SettlementEconomyIoC.cs:9` | Settlement Economy (toggle only) |
| [`configs/revolt_tuning_config.json`](../../Main/_Module/ModuleData/configs/revolt_tuning_config.json) | `RevoltTuningConfigProvider` | `RevoltTuningIoC.cs:9` | none |
| [`combat_mechanics/combat_mechanics_config.json`](../../Main/_Module/ModuleData/combat_mechanics/combat_mechanics_config.json) | `CombatMechanicsConfigProvider` | `CombatMechanicsIoC.cs:9` | Combat Mechanics |
| [`raceage/race_age_config.json`](../../Main/_Module/ModuleData/raceage/race_age_config.json) | `RaceAgeConfigProvider` | `RaceAgeIoC.cs:11` | none |
| [`configs/initial_child_generation.json`](../../Main/_Module/ModuleData/configs/initial_child_generation.json) | `InitialChildGenerationConfigProvider` | `InitialChildGenerationIoC.cs:12` | none |
| [`field_commission/field_commission_config.json`](../../Main/_Module/ModuleData/field_commission/field_commission_config.json) | `FieldCommissionConfigProvider` | `FieldCommissionIoC.cs:37` | Battlefield Promotions |
| [`recruitment_alignment/recruitment_alignment_config.json`](../../Main/_Module/ModuleData/recruitment_alignment/recruitment_alignment_config.json) | `RecruitmentAlignmentConfigProvider` | `RecruitmentAlignmentIoC.cs:9` | World/Recruitment Alignment |
| [`enlistment/enlistment_duties.json`](../../Main/_Module/ModuleData/enlistment/enlistment_duties.json) | `EnlistmentContentConfigProvider` | `EnlistmentIoC.cs:70` | Enlistment (feature toggles only) |
| [`career_system/taom_career_choices.xml`](../../Main/_Module/ModuleData/career_system/taom_career_choices.xml) | `CareerConfigProvider` | `CareerSystemIoC.cs:19` | none |

**Reload scope is the same for every one of them: quit Bannerlord and relaunch.** All fourteen providers are registered `Reuse.Singleton`, so the file is read once per process and the parsed object is held until the process exits. <!-- measured: rg -n on the fourteen IoC.cs registrations listed above 2026-09-05 --> Starting a new campaign, loading a save or returning to the main menu will not re-read anything. The rule is stated for every provider in [csharp-architecture](../../.claude/rules/csharp-architecture.md) under "Doc requirement".

**MCM beats JSON, and it beats it on installs you will never see.** Where a value is exposed both in a config file and in the in-game Mod Configuration Menu, the MCM value wins. MCM has no "unset" state: once it is loaded every property reads back a value, so the slider wins even when the player has never touched it ([field-commission](../features/field-commission.md), "Precedence, and its one sharp edge"). Worse for a pack author, `TaomSettings` persists with `FormatType => "json2"` (`TaomSettings.cs:16`), so anyone who has already launched TAOM has the old value written to their disk and will never see a changed default ([shader-precompilation](../features/shader-precompilation.md)). If a number must hold for everybody, put it in a field that has no MCM knob.

**432 `[SettingProperty` declarations across 55 MCM groups** live in [`Main/Features/TaomSettings.cs`](../../Main/Features/TaomSettings.cs). <!-- measured: rg -o "\[SettingProperty" Main/Features/TaomSettings.cs | wc -l ; rg -o 'SettingPropertyGroup\("[^"]*"' Main/Features/TaomSettings.cs | sort -u | wc -l 2026-09-05 --> There is no catalogue doc for them, and some of the biggest balance levers exist only there. AI party size is the clearest case: seven knobs under "AI Party Size" (`TaomSettings.cs:40-80`) and no JSON file at all, all five numeric ones shipping at the vanilla value (`AiPartySizeService.cs:67-86`: lord factor 1.0, flat bonus 0, garrison factor 1.0, food relief 0, wage relief 0). If you want to know whether a lever is MCM-only, the file above is the only place to look.

## Attributes

### troop_weights.xml

<!-- engine-ref type="TAOM.Features.TroopWeight.TroopWeightXmlLoader" file="Main/Features/TroopWeight/TroopWeightXmlLoader.cs" lines="41-105" -->

| Attribute | Type | Required | Default when absent | What it does | Read at (file:line) |
|---|---|---|---|---|---|
| `id` | string | yes | row skipped with a warning | NPCCharacter id, matched case-insensitively | `TroopWeightXmlLoader.cs:67` |
| `weight` | float | yes | row skipped with a warning | Slots this troop costs against the party size limit | `TroopWeightXmlLoader.cs:68` |

A troop with no row weighs **1.0** (`TroopWeightService.cs:43-45`, and the header comment on line 3 of the file says the same). `weight` must be finite and greater than zero: `NaN`, `Infinity`, zero and negatives are all skipped with a warning rather than clamped, because a poisoned weight casts to `int.MinValue` downstream (`TroopWeightXmlLoader.cs:83-89`). A duplicate id keeps the last value and warns (`TroopWeightXmlLoader.cs:92-93`).

Weight is not a party-size cost by itself. The penalty is `weighted count minus raw count`, clamped so the limit never falls below 1, and it is skipped entirely when the base limit is under 2 (`TroopWeightService.cs:172-186`).

### troop_resource_costs.xml

<!-- engine-ref type="TAOM.Features.SpecialResources.SpecialResourceConfigProvider" file="Main/Features/SpecialResources/SpecialResourceConfigProvider.cs" lines="1-120" -->

| Attribute | Type | Required | Default when absent | What it does | Read at (file:line) |
|---|---|---|---|---|---|
| `id` | string | yes | row unusable | The troop this cost applies to | `special-resources.md` "Troop Costs" |
| `resource_id` | string | no | none | Documentation only. The resource charged is always the player's resolved one | `special-resources.md:134` |
| `upgrade_cost` | int | no | 0 (not charged) | Charged on the party-screen upgrade into this troop | `special-resources.md:127` |
| `recruit_cost` | int | no | 0 (not charged) | Charged once when recruited as a volunteer | `special-resources.md:128` |
| `merchant_cost` | int | no | 0, and the offer is dropped with a warning | The one-time elite emissary purchase price | `EliteEmissaryService.cs:78-81` |
| `daily_upkeep` | float | no | 0 | Charged every daily tick the troop is in the party | `special-resources.md:129` |

`merchant_cost` is the attribute the special-resources doc leaves out. It is the emissary price, and an offer troop whose row has no `merchant_cost` is dropped and warned at load rather than sold for nothing ([elite-emissary](../features/elite-emissary.md)). All four cost attributes can appear on one row; they are charged by different systems and never substitute for each other.

`special_resources_config.xml` carries the resource definitions themselves: `id`, `display_name`, `icon_sprite`, `cap`, `starting_amount`, `daily_per_town`, `per_battle_victory_base`, `per_raid`, `per_siege_victory`, `per_prisoner`, `per_tournament_win`, `per_hideout_clear`. <!-- measured: python ElementTree attribute union over Resource elements in special_resources_config.xml 2026-09-05 -->

### startup_resources_config.xml

<!-- engine-ref type="TAOM.Features.StartupResources.StartupResourcesConfigProvider" file="Main/Features/StartupResources/StartupResourcesConfigProvider.cs" lines="14-131" -->

| Attribute | Type | Required | Default when absent | What it does | Read at (file:line) |
|---|---|---|---|---|---|
| `id` | string | yes | row unusable | Culture id, matched case-insensitively | `StartupResourcesConfigProvider.cs:61` |
| `gold` | int | no | 0 | Gold given to each living lord hero of this culture. Negative or unparseable reverts to 0 with a warning | `StartupResourcesConfigProvider.cs:96-111` |
| `influence` | float | no | 0 | Influence added to each eligible clan. Must be finite and at least 0, else reverts to 0 with a warning | `StartupResourcesConfigProvider.cs:113-130` |
| `playerGold` | int | no | 0 (no warning) | Gold given to the player hero at character-creation finalize. Legal range `[0, 10000000]`; outside it reverts to 0 with a warning | `StartupResourcesConfigProvider.cs:14, 76-94` |

A culture with no row gets nothing. There is no fallback culture.

### battle_balance_config.json

<!-- engine-ref type="TAOM.Features.BattleBalance.BattleBalanceConfigProvider" file="Main/Features/BattleBalance/BattleBalanceConfigProvider.cs" lines="59-90" -->

| Field | Type | Required | Default when absent | What it does | Read at (file:line) |
|---|---|---|---|---|---|
| `TroopPower.TierPower.T0` to `T10` | float | no | `(2 + tier) * (10 + tier) * 0.02` | Base military power of a troop at that tier. Legal `[0.01, 1000]`, else reverts to the compiled default with a warning | `BattleBalanceConfigProvider.cs:66`, `BattleBalanceConfig.cs:20` |
| `CasualtyRatios.EnableCulturalSurvivalBonuses` | bool | no | `true` | Second gate on the culture bonuses below | `TaomPartyHealingModel.cs:57` |
| `CasualtyRatios.CulturalSurvivalBonuses.<cultureId>` | float | no | 0 | `newDeathChance = vanillaDeathChance * (1 - bonus)`. Legal `[-1, 1]`, else reverts to 0 with a warning | `BattleBalanceConfigProvider.cs:82-85` |

**Read the tier block twice before you edit it.** `T7` to `T10` are never read while MCM is loaded: `CalculateTierPower` switches straight to the four MCM sliders for tier 7 and up (`TaomMilitaryPowerModel.cs:42-48`), whose defaults are 2.91 / 3.26 / 3.61 / 3.96 (`TaomSettings.cs:255-270`), the same numbers the JSON happens to carry. `T0` to `T6` are read only when "Override Vanilla Tiers (T1-T6)" is on, and it ships off (`TaomSettings.cs:250`). So out of the box every value in the `TierPower` block is inert and the tier curve you feel in play is vanilla's formula plus four sliders.

### settlement_food_config.json

<!-- engine-ref type="TAOM.Features.SettlementFood.SettlementFoodConfigProvider" file="Main/Features/SettlementFood/SettlementFoodConfigProvider.cs" lines="50-153" -->

**Shipped is not the compiled default here.** Every compiled default equals the vanilla engine constant, and until 2026-09-06 the JSON shipped those same values, so the file changed nothing. It now ships tuned (#546), because 70 of 72 towns started food-negative. Both columns are given below; a missing key falls back to the compiled default, not to the shipped value.

| Field | Type | Required | Shipped | Compiled default | Legal range | What it does | Read at (file:line) |
|---|---|---|---|---|---|---|---|
| `garrisonFoodDivisor` | int | no | 20 | 20 | `[1, 10000]` | Higher makes garrisons cheaper to feed | `SettlementFoodConfigProvider.cs:69` |
| `prosperityFoodDivisor` | int | no | **45** | 40 | `[1, 10000]` | Higher relieves the civilian term | `SettlementFoodConfigProvider.cs:76` |
| `townBaseFood` | float | no | **30** | 15 | `[0, 10000]` | Replaces vanilla's flat town production | `SettlementFoodConfigProvider.cs:84` |
| `castleBaseFood` | float | no | 10 | 10 | `[0, 10000]` | Replaces vanilla's flat castle production | `SettlementFoodConfigProvider.cs:91` |
| `villageFoodMultiplier` | float | no | **8** | 6 | `[0, 10000]` | Scales `(hearthLevel + 1) * mult` per bound village | `SettlementFoodConfigProvider.cs:98` |
| `flatFoodBonus` | float | no | **5** | 0 | `[0, 100000]` | Purely additive daily production | `SettlementFoodConfigProvider.cs:105` |
| `hinterlandFoodPerProsperity` | float | no | **0.02** | 0 | `[0, 10000]` AND **strictly** `< 1 / prosperityFoodDivisor` | Adds `prosperity * rate` to production. No vanilla equivalent | `SettlementFoodConfigProvider.cs:123` |
| `foodStocksUpperLimit` | int | no | 300 | 300 | `[1, 1000000]` | Storage cap before buildings | `SettlementFoodConfigProvider.cs:132` |
| `castleFoodStockUpperLimitBonus` | int | no | 150 | 150 | `[0, 1000000]` | Extra storage for castles | `SettlementFoodConfigProvider.cs:139` |

The four base and multiplier knobs are **absolute replacements**, not bonuses, so a value below the shipped one lowers production. Validation never enforces "at least vanilla" ([settlement-food](../features/settlement-food.md)). A divisor of 0 is rejected because it would put `Infinity` into the formula.

`hinterlandFoodPerProsperity` is the only field in this chapter with a **cross-field** invariant, and it is the one to be careful with. It must stay strictly below `1 / prosperityFoodDivisor`, checked against the already-sanitized divisor so a rejected divisor cannot poison the bound. At or above it, net food stops falling as a fief grows, so the store overflows daily, vanilla converts overflow to prosperity at `+0.1` per point, and prosperity, town gold and garrison caps inflate map-wide. Raising `prosperityFoodDivisor` tightens this bound, so the two knobs cannot be tuned independently: at divisor 45 the ceiling is 0.0222, at divisor 100 it is 0.01. The provider reverts a violating value to 0 with a warning, and `SettlementFoodShippedConfigTests` fails the build so a bad edit cannot ship on a log line nobody reads.

The storage caps are pre-building figures. `Town.FoodStocksUpperLimit()` adds the `FoodStock` building effect on top, so a fully upgraded town reaches 800 (Warehouse `+100/300/500`) and a castle 750 (Granary `+100/200/300`).

### settlement_economy_config.json

<!-- engine-ref type="TAOM.Features.SettlementEconomy.SettlementEconomyConfigProvider" file="Main/Features/SettlementEconomy/SettlementEconomyConfigProvider.cs" lines="50-90" -->

| Field | Type | Required | Shipped | Legal range | What it does | Read at (file:line) |
|---|---|---|---|---|---|---|
| `townGoldBase` | float | no | 25000 | `[0, 200000]` | Flat term of the equilibrium gold target | `SettlementEconomyConfigProvider.cs:64` |
| `townGoldPerProsperity` | float | no | 12 | `[0, 100]` | Target gold per prosperity point | `SettlementEconomyConfigProvider.cs:72` |
| `townGoldRegenRate` | float | no | 0.25 | `[0, 1]` | Fraction of the deficit recovered per day; 0 freezes town gold | `SettlementEconomyConfigProvider.cs:81` |

Target is `base + prosperity * perProsperity` and the daily change is `rate * (target - currentGold)`, which goes negative above the target on purpose. The shipped 25000 is **not** vanilla's 10000 ([settlement-economy](../features/settlement-economy.md)).

### revolt_tuning_config.json

<!-- engine-ref type="TAOM.Features.RevoltTuning.RevoltTuningConfigProvider" file="Main/Features/RevoltTuning/RevoltTuningConfigProvider.cs" lines="50-101" -->

| Field | Type | Required | Shipped | Vanilla | Legal range | What it does | Read at (file:line) |
|---|---|---|---|---|---|---|---|
| `rebellionStartLoyaltyThreshold` | int | no | 5 | 15 | `[0, 100]` | Rebellion fires at loyalty at or below this | `RevoltTuningConfigProvider.cs:65` |
| `rebelliousStateStartLoyaltyThreshold` | int | no | 10 | 25 | `[0, 100]`, and must be at least the field above | Warning state begins here | `RevoltTuningConfigProvider.cs:72-79` |
| `settlementOwnerDifferentCultureLoyaltyEffect` | float | no | -1.0 | -3.0 | finite and at most 0 | Daily loyalty change when the owner's culture differs | `RevoltTuningConfigProvider.cs:85` |
| `governorDifferentCultureLoyaltyEffect` | float | no | -0.5 | -1.0 | finite and at most 0 | Daily loyalty change when the governor's culture differs | `RevoltTuningConfigProvider.cs:92` |

Inverting the two thresholds reverts **both** to defaults, not just the offender (`RevoltTuningConfigProvider.cs:79`). A positive penalty is rejected outright: a sign-flipped `1.0` would turn the feature from softening revolts into accelerating them, which is the review finding that produced the whole validation rule ([csharp-architecture](../../.claude/rules/csharp-architecture.md), "Why").

### combat_mechanics_config.json

<!-- engine-ref type="TAOM.Features.CombatMechanics.CombatMechanicsConfigProvider" file="Main/Features/CombatMechanics/CombatMechanicsConfigProvider.cs" lines="103-321" -->

| Field | Type | Shipped | Legal range | Read at (file:line) |
|---|---|---|---|---|
| `crushThrough.extraCrushThroughEnergyThreshold` | float | 25.0 | `[1, 1000]` | `CombatMechanicsConfigProvider.cs:103` |
| `crushThrough.skillTargetDelta` | int | 200 | `[1, 1000]` | `CombatMechanicsConfigProvider.cs:104` |
| `crushThrough.skillDeadZone` | int | 30 | `[0, 1000]`, must be below `skillTargetDelta` | `CombatMechanicsConfigProvider.cs:105, 119` |
| `crushThrough.maxSkillChance` | float | 0.5 | `[0, 1]` | `CombatMechanicsConfigProvider.cs:106` |
| `crushThrough.nonOverheadPenaltyFactor` | float | 0.5 | `[0, 1]` | `CombatMechanicsConfigProvider.cs:109` |
| `chargeKnockdown.neutralWeightRatio` | float | 6.0 | `[0.1, 1000]` | `CombatMechanicsConfigProvider.cs:147` |
| `chargeKnockdown.autoKnockdownWeightRatio` | float | 8.0 | `[0.1, 1000]`, must be at least `neutralWeightRatio` | `CombatMechanicsConfigProvider.cs:148, 161` |
| `chargeKnockdown.autoKnockdownMinSpeedFactor` | float | 0.4 | `[0, 1]` | `CombatMechanicsConfigProvider.cs:149` |
| `chargeKnockdown.defaultChargeSpeedReference` | float | 4.3 | `[0.1, 100]` | `CombatMechanicsConfigProvider.cs:150` |
| `chargeKnockdown.minPenetrationFactor` | float | 0.25 | `[0, 10]` | `CombatMechanicsConfigProvider.cs:151` |
| `chargeKnockdown.maxPenetrationFactor` | float | 2.5 | `[0, 10]` | `CombatMechanicsConfigProvider.cs:152` |
| `chargeKnockdown.horseChargePenetration` | float | 0.4 | `[0, 1]` | `CombatMechanicsConfigProvider.cs:153` |
| `creatures.unstoppableDamageThresholds.<monsterId>` | int | 5 entries, 10 to 30 | `[0, 500]`, else the entry is removed | `CombatMechanicsConfigProvider.cs:227` |
| `raceModifiers.<race>.ctbAttackBonus` / `ctbDefenseBonus` | float | 15 to 20 | `[0, 300]` | `CombatMechanicsConfigProvider.cs:317-318` |
| `raceModifiers.<race>.knockdownResistanceMultiplier` / `staggerThresholdMultiplier` | float | 1.25 to 3.0 | `[0, 100]` | `CombatMechanicsConfigProvider.cs:319-320` |
| `raceModifiers.<race>.swingEnergyBonusFactor` | float | 0.10 to 0.20 | `[0, 10]` | `CombatMechanicsConfigProvider.cs:321` |

The per-mechanic booleans (`enabled`, `crushThrough.skillBasedEnabled`, `chargeKnockdown.enabled`, `creatures.cleaveEnabled`, `creatures.unstoppableEnabled`, `shieldPenetration.enabled`) and the id lists (`monsterCrushMonsterIds`, `orcShieldCrushRaces`, `cleaveMonsterIds`, `weaponClasses`, `itemIds`) sit alongside them. `shieldPenetration.enabled` ships `false`. Lists replace rather than append, so writing one entry deletes the shipped set ([combat-mechanics](../features/combat-mechanics.md)). An unknown `weaponClasses` entry is skipped, not rejected.

### race_age_config.json

<!-- engine-ref type="TAOM.Features.RaceAge.RaceAgeConfigProvider" file="Main/Features/RaceAge/RaceAgeConfigProvider.cs" lines="28-92" -->

| Field | Type | Required | Default when absent | What it does | Read at (file:line) |
|---|---|---|---|---|---|
| `defaultRace` | string | yes | `human` row is used | Race entry any unknown race falls back to | `race-age-system.md:53` |
| `races.<name>.maxAge` | int | yes | none | Lifespan; heroes die past it | `RaceAgeConfigProvider.cs:84` |
| `races.<name>.becomeOld` | int | yes | none | Visual aging threshold. Must be below `maxAge`, else reset to `maxAge - 1` | `RaceAgeConfigProvider.cs:90` |
| `races.<name>.comesOfAge` | int | yes | none | Adult minimum. Must be below `fertilityEnd`, else both reset to 18 / 45 | `RaceAgeConfigProvider.cs:78` |
| `races.<name>.middleAge` | int | yes | none | Must be below `maxAge`, else both reset to 35 / 85 | `RaceAgeConfigProvider.cs:84` |
| `races.<name>.fertilityEnd` | int | yes | none | Age at which fertility reaches zero | `RaceAgeConfigProvider.cs:78` |
| `races.<name>.fertilityMod` | float | yes | reverts to 1.0 with a warning if not finite | Multiplier on vanilla pregnancy chance | `RaceAgeConfigProvider.cs:64` |
| `races.<name>.immortal` | bool | no | `false` | Never dies of age and has zero fertility | `race-age-system.md:63` |

The shipped file holds **15 race entries**. <!-- measured: python json key count over races in race_age_config.json 2026-09-05 --> Do not copy the "Current Race Values" table in [race-age-system](../features/race-age-system.md): it is stale against the file. The shipped `human` row is `maxAge` 200, `becomeOld` 170, `middleAge` 100, `fertilityEnd` 195, and **every one of the 15 races ships `comesOfAge` 18**, where the doc lists 85 for human maxAge and per-race coming-of-age values from 6 to 30. Read the file. <!-- measured: python json dump of races in race_age_config.json compared against docs/features/race-age-system.md L67-85 2026-09-05 -->

### initial_child_generation.json

<!-- engine-ref type="TAOM.Features.InitialChildGeneration.InitialChildGenerationConfigProvider" file="Main/Features/InitialChildGeneration/InitialChildGenerationConfigProvider.cs" lines="125-150" -->

| Field | Type | Required | Default when absent | What it does | Read at (file:line) |
|---|---|---|---|---|---|
| `defaults.min_age` | int | no | 2 | Youngest generated child | `initial-child-generation.md:52` |
| `defaults.max_age` | int | no | 17 | Oldest generated child | `initial-child-generation.md:53` |
| `defaults.female_ratio` | double | no | 0.49 | Chance a child is female. Must be finite in `[0, 1]` | `InitialChildGenerationConfigProvider.cs:132` |
| `defaults.child_count_multiplier` | double | no | 1.0 | Scales the calculated child count. Must be finite and at least 0 | `InitialChildGenerationConfigProvider.cs:146` |
| `excluded_cultures` | string[] | no | empty | Culture ids skipped entirely. Ships with `mordor`, `isengard`, `gundabad`, `dolguldur` | file contents |
| `excluded_clans` | string[] | no | empty | Clan ids skipped entirely | `initial-child-generation.md:57` |
| `culture_overrides` | array | no | empty | Per-culture override of any default; needs `culture_id` | `initial-child-generation.md:58` |
| `clan_overrides` | array | no | empty | Per-clan override; also accepts `fixed_child_count`, which bypasses the calculation | `initial-child-generation.md:59` |

A missing file means all defaults and no exclusions, which is not the same as the shipped state: the shipped file excludes four cultures.

### field_commission_config.json

<!-- engine-ref type="TAOM.Features.FieldCommission.FieldCommissionConfigProvider" file="Main/Features/FieldCommission/FieldCommissionConfigProvider.cs" lines="75-145" -->

| Field | Type | Shipped | Legal range | MCM | Read at (file:line) |
|---|---|---|---|---|---|
| `enabled` | bool | `true` | either | yes | `FieldCommissionSettingsProvider.cs:87` |
| `ratioThreshold` | float | 1.3 | finite and at least 0; 0 means never eligible | yes | `FieldCommissionConfigProvider.cs:89` |
| `meritPerKill` | int | 1 | at least 1 | yes | `FieldCommissionConfigProvider.cs:96` |
| `meritThreshold` | int | 32 | at least 1 | yes | `FieldCommissionConfigProvider.cs:103` |
| `retainerAllowance` | int | 0 | at least 0 | yes | `FieldCommissionConfigProvider.cs:117` |
| `maxOffersPerBattle` | int | 2 | at least 1 | yes | `FieldCommissionConfigProvider.cs:126` |
| `skillPointsPerLevel` | int | 5 | at least 1 | no, JSON only | `FieldCommissionConfigProvider.cs:110` |
| `diagnostics` | bool | `false` | either | yes | `FieldCommissionSettingsProvider.cs:104` |
| `allowedRaceNames` | string[] | human, dwarf, elf | blank entries dropped | no, JSON only | `FieldCommissionConfigProvider.cs:133, 143` |

The six MCM-exposed fields are the ones a pack author cannot rely on. The two JSON-only fields are carried through the merge by reference so a merge cannot re-admit a race the author excluded (`FieldCommissionSettingsProvider.cs:108-109`).

### recruitment_alignment_config.json

<!-- engine-ref type="TAOM.Features.AlignmentRecruitment.RecruitmentAlignmentConfigProvider" file="Main/Features/AlignmentRecruitment/RecruitmentAlignmentConfigProvider.cs" lines="36-85" -->

| Field | Type | Shipped | Legal values | What it does | Read at (file:line) |
|---|---|---|---|---|---|
| `enabled` | bool | `true` | either | Master toggle | `RecruitmentAlignmentConfigProvider.cs:36` |
| `mode` | string | `Symmetric` | `Symmetric` or `GoodRejectsEvil`, case-insensitive | Anything else reverts to `Symmetric` with a warning | `RecruitmentAlignmentConfigProvider.cs:68-78` |
| `applyToAi` | bool | `true` | either | Gate AI lords | `alignment-recruitment.md:47` |
| `applyToPlayer` | bool | `true` | either | Gate the player | `alignment-recruitment.md:48` |

All four are exposed under the MCM group "World/Recruitment Alignment", so all four are subject to the precedence warning above. The alignment data itself lives in `execution/alignment.json`, which has **24 keys and mixes kingdom ids with culture ids** (`gondor`, `mordor`, `goblin`, `lindon` and five more are cultures; `empire_w`, `khuzait`, `battania` and the rest are kingdoms). <!-- measured: python json key listing over execution/alignment.json 2026-09-05 --> Two feature docs call it a kingdom-id map. It is not, and adding a faction means checking which of the two you are adding. That file belongs to [Faction and world configs](configs-factions-and-world.md).

### enlistment_duties.json

<!-- engine-ref type="TAOM.Features.Enlistment.Duties.SkillCheckService" file="Main/Features/Enlistment/Duties/SkillCheckService.cs" lines="18-58" -->

Three arrays: **13 `fieldDuties`, 11 `interactiveDuties`, 3 `incidents`**. <!-- measured: python json array lengths over enlistment_duties.json 2026-09-05 -->

| Field | Type | Required | What it does |
|---|---|---|---|
| `id` | string | yes | Duty id, also the localization key stem |
| `difficulty` | int | yes | Target the check must reach |
| `durationHours` | int | yes | How long the duty runs |
| `supportSkills` | string[] | yes | The check uses the **better** of the two, never their sum |
| `gates.minRank` | string | yes | Rank band that unlocks the row |
| `gates.minTrust` | int | no | Standing floor |
| `gates.assignmentAffinity` | string | no | Assignment the row prefers |
| `gates.requiredContexts` | string[] | no | Situations the row needs |
| `reportReward` / `failureReward` | object | yes | `serviceXp`, `gold`, `skillId`, `skillXp`, `trust`, `relation`, `repDomain`, `repAmount` |

The check is `bestSkill + max(0, trust) * 2 + rank * 4 + Next(0..50) >= difficulty` (`SkillCheckService.cs:29, 51, 53-58`). The roll caps at 50, so a row whose difficulty exceeds the reachable total of the weakest player its own gates admit by more than 50 is impossible, not hard. Shipped difficulties run 40 to 76 and every one of the 13 field rows has `failureReward.trust` of 0. <!-- measured: python json scan of difficulty and failureReward.trust over fieldDuties in enlistment_duties.json 2026-09-05 --> `FieldDutyReachabilityTests` pins three floors on this file; adding a row that breaks one fails the suite ([enlistment](../features/enlistment.md)).

### taom_career_choices.xml

<!-- engine-ref type="TAOM.Features.CareerSystem.CareerConfigProvider" file="Main/Features/CareerSystem/CareerConfigProvider.cs" lines="240-261" -->

| Attribute on `<PassiveEffect>` | Type | Required | Default when absent | What it does | Read at (file:line) |
|---|---|---|---|---|---|
| `type` | string | yes | choice carries no passive | Which system the pip feeds; 23 distinct types ship | `CareerConfigProvider.cs:257` |
| `magnitude` | float | no | falls back to `value`, then 0 | The size of the effect | `CareerConfigProvider.cs:258` |
| `value` | float | no | 0 | Alias for `magnitude`; `magnitude` wins when both are present | `CareerConfigProvider.cs:258` |
| `attack_type_mask` | enum | no | `All` | Gates `Damage` and `Resistance` to melee or ranged hits | `CareerConfigProvider.cs:261` |
| `operation` | string | no | ignored | Read but has no effect. Removed from the parser 2026-06-25 | `CareerConfigProvider.cs:259-260` |
| `is_percentage` | bool | no | ignored | Read but has no effect. Same removal | `CareerConfigProvider.cs:259-260` |

The shipped file carries **1,735 `<PassiveEffect>` elements across 23 types**, of which 310 use the wrapped `<PassiveEffects><PassiveEffect/></PassiveEffects>` form, 290 use `value=` without `magnitude=`, and 1,445 still carry the dead `operation` attribute. <!-- measured: python ElementTree scan of taom_career_choices.xml counting PassiveEffect elements, distinct type values, PassiveEffects children and per-attribute presence 2026-09-05 --> Only the **first** `<PassiveEffect>` under a `<Choice>` is read (`CareerConfigProvider.cs:243`); today no choice carries a second one, so nothing is being dropped, but adding one silently would be. <!-- measured: python count of Choice elements carrying more than one PassiveEffect in taom_career_choices.xml 2026-09-05 -->

**Magnitude scale has to match the consumer.** Almost every type is fractional, where 0.10 means plus ten percent. `PartySize`, `Health` and `CompanionLimit` are whole counts, where 2 means two more units. Authoring a whole count on a fractional type multiplies the base instead of adding to it ([career-system](../features/career-system.md), "Magnitude scale").

## Child elements

<!-- engine-ref type="TAOM balance config root elements" file="Main/_Module/ModuleData" lines="see each file" -->

| Parent | Child | Repeats | What it holds |
|---|---|---|---|
| `<TroopWeights>` | `<TroopWeight>` | many | One weighted troop |
| `<TroopResourceCosts>` | `<Troop>` | many | One costed troop |
| `<SpecialResources>` | `<Resource>` | 11 | One resource definition |
| `<Resource>` | `<Kingdom>` | many | A kingdom mapped to this resource |
| `<Resource>` | `<Culture>` | many | A culture mapped to this resource |
| `<Resource>` | `<Tiers>` | 0 or 1 | Wrapper; absent means an empty tier list |
| `<Tiers>` | `<Tier>` | many | `level`, `name`, `threshold`, `description`; sorted by threshold at parse time |
| `<StartupResources>` | `<Culture>` | 22 | One culture's starting gold and influence |
| `<Choice>` | `<PassiveEffect>` | first only | The direct form |
| `<Choice>` | `<PassiveEffects>` | 0 or 1 | Wrapper; only its first `<PassiveEffect>` is read |

<!-- measured: python ElementTree structure walk over troop_weights.xml, troop_resource_costs.xml, special_resources_config.xml, startup_resources_config.xml and taom_career_choices.xml 2026-09-05 -->

The JSON configs nest by object rather than element: `TroopPower.TierPower` and `CasualtyRatios.CulturalSurvivalBonuses` in `battle_balance_config.json`; `crushThrough`, `chargeKnockdown`, `creatures`, `shieldPenetration` and `raceModifiers` in `combat_mechanics_config.json`; `races` in `race_age_config.json`; `defaults`, `culture_overrides` and `clan_overrides` in `initial_child_generation.json`; `fieldDuties`, `interactiveDuties` and `incidents` in `enlistment_duties.json`.

## Worked example

The header of `troop_weights.xml`, which states the fallback every unlisted troop gets:

<!-- excerpt file="Main/_Module/ModuleData/TroopWeights/troop_weights.xml" -->
```xml
<?xml version="1.0" encoding="utf-8"?>
<TroopWeights>
    <!-- Default weight is 1.0 for all unlisted troops -->
```

and five rows from the same file, lines 11 to 16:

<!-- excerpt file="Main/_Module/ModuleData/TroopWeights/troop_weights.xml" -->
```xml
    <!-- Legendary commanders (Weight 3.0) -->
    <TroopWeight id="rivendell_glorfindel_guard" weight="3.0" />
    <TroopWeight id="rivendell_gondolin_battlemaster" weight="3.0" />
    <TroopWeight id="rivendell_high_captain" weight="3.0" />
    <TroopWeight id="rivendell_knight_golden_flower" weight="3.0" />
    <TroopWeight id="rivendell_warden_gondolin" weight="3.0" />
```

1. **`id`** is the troop id exactly as it appears in the troop file, without an `NPCCharacter.` prefix. A typo is not an error: the row simply never matches and the troop keeps weight 1.0.
2. **`weight`** is what the troop costs against the party size limit. Five men at 3.0 cost fifteen slots.

One row of `troop_resource_costs.xml`, the only shape that shows all four cost attributes at once:

<!-- example file="Main/_Module/ModuleData/special_resources/troop_resource_costs.xml" id="mordor_uruk_captain" -->
```xml
  <Troop id="mordor_uruk_captain" resource_id="war_spoils" upgrade_cost="4" daily_upkeep="0.2" merchant_cost="14" />
```

1. **`upgrade_cost`** is charged on the party screen when the player upgrades into this troop.
2. **`merchant_cost`** is a different price for a different system, the elite emissary purchase. Omit it and the emissary offer for this troop is dropped with a warning.
3. **`resource_id`** does not select the resource charged. The player's own resolved resource is charged whatever this says.

A whole JSON config, `configs/revolt_tuning_config.json`:

<!-- excerpt file="Main/_Module/ModuleData/configs/revolt_tuning_config.json" -->
```json
{
  "rebellionStartLoyaltyThreshold": 5,
  "rebelliousStateStartLoyaltyThreshold": 10,
  "settlementOwnerDifferentCultureLoyaltyEffect": -1.0,
  "governorDifferentCultureLoyaltyEffect": -0.5
}
```

1. **The two thresholds are ordered.** The warning state must trigger at or above the rebellion trigger. Swap them and both revert to defaults.
2. **The two effects are penalties.** A positive number is rejected, not applied as a bonus.

## Recipes: Add / Modify / Delete

### Add

Adding a row rather than changing a number: a weighted troop, a costed troop, a culture's starting gold, a race, a duty.

1. Pick the file from the table in "Where it lives and how it is registered" and open the repo copy under [`Main/_Module/ModuleData/`](../../Main/_Module/ModuleData/).
2. Copy an existing sibling row and change it. Do not invent an attribute name: the attribute tables above are the complete set each provider reads, and an unknown attribute is silently ignored.
3. Check that any id you reference is real. A `<TroopWeight id>` or a `<Troop id>` naming a troop that does not exist is not an error, it is a row that never fires.
4. If the file is JSON, parse it before you launch: a trailing comma or a missing brace makes the provider fall back to compiled defaults for the whole file, and you would see the feature behaving as though you never edited it.
5. Launch, then read the log. The provider logs `Loaded <filename>` on a clean parse and one warning line per rejected field.

Check: `python -m json.tool Main/_Module/ModuleData/configs/revolt_tuning_config.json` for a JSON file, then after a launch `rg -n "ConfigProvider|XmlLoader" Logs/taom_debug_*.log`
Takes effect: full game restart
Code: No code changes needed for a new row in an existing file. A new **key** or attribute is `Code changes required in Main/Features/<Feature>/<Feature>ConfigProvider.cs` plus its config class and a validation test.

### Modify

Changing a value in place.

1. Read the field's row in the attribute table above and note its legal range. A value outside the range does not clamp: it reverts to the compiled default and warns.
2. Check the MCM column. If the field has an MCM knob, your edit only reaches players running without MCM, and it will never reach a player who has already launched TAOM, because their stored `json2` value wins.
3. Make the edit in the repo copy.
4. For a settlement or economy pass that also touches settlement data, run the ModuleData validator afterwards. `SETTLEMENT_ECONOMY_FLOOR` compares live settlement prosperity and hearth values against the committed spec in `tools/settlement_economy_floor.json` and tells you which culture fell below it.
5. Launch and confirm the number in play, not just in the file. For character-creation bonus changes the read-only auditor gives you the stacked per-culture totals without launching anything.

Check: `python tools/validate_moduledata.py --code SETTLEMENT_ECONOMY_FLOOR` and `python tools/audit_cc_bonuses.py --report`
Takes effect: full game restart
Code: No code changes needed

### Delete

Removing a row, a key, or a whole file.

1. Deleting a `<TroopWeight>` row returns that troop to weight 1.0. Deleting a `<Troop>` row from `troop_resource_costs.xml` makes the troop free to upgrade and free to keep, and removes it from the emissary offer list if it had a `merchant_cost`.
2. Deleting a `<Culture>` row from `startup_resources_config.xml` gives that culture nothing. There is no fallback.
3. Deleting a JSON key falls back to the compiled default in the provider's config class, which is not always the shipped value. `settlement_economy_config.json` ships 25000 for `townGoldBase` and the compiled default is also 25000, but check the class before assuming.
4. Deleting a whole file makes the provider log "not found" and use compiled defaults for every field. That is a working state, not a crash, which is why a deleted config can go unnoticed for a long time.
5. Never delete `tools/settlement_economy_floor.json`. The validator treats a missing spec as an error rather than a pass, on purpose (`taom_schema.py:577-586`).

Check: `rg -n "not found|reverting to default" Logs/taom_debug_*.log` after a launch
Takes effect: full game restart
Code: No code changes needed

## Gotchas: what fails silently and what crashes

- **Nothing in this chapter is checked by the ModuleData validator.** A grep of `tools/validate_moduledata.py` and `tools/taom_schema.py` for all fifteen filenames returns zero hits, and `tools/schemas/` holds only three schemas (`taom_npccharacter.json`, `taom_spcultures.json`, `taom_equipmentsets.json`). No balance config has a schema, no numeric range is checked outside the game, and a green `validate_moduledata.py` run says nothing about them. <!-- measured: rg -n over tools/validate_moduledata.py and tools/taom_schema.py for the fifteen filenames, and ls tools/schemas/ 2026-09-05 --> The only check that reads your values is the provider itself, at launch, into `Logs/taom_debug_*.log` (`FileLogger.cs:26, 40`).
- **A malformed JSON file is not an error you will see.** The provider logs and falls back to compiled defaults, and the game runs. Parse the file before launching.
- **A value outside range reverts, it does not clamp.** Every provider in this chapter logs one warning per rejected field plus a summary warning, then uses the compiled default. `settlement_food_config.json` with `garrisonFoodDivisor: 0` runs at 20, not at 1 (`SettlementFoodConfigProvider.cs:70`).
- **`NaN` and `Infinity` are rejected before the range check, everywhere.** This is not politeness: `value < min || value > max` is `false` for `NaN`, so a bare range check lets it through, and it has shipped three times ([csharp-architecture](../../.claude/rules/csharp-architecture.md), rule 4).
- **`battle_balance_config.json`'s tier block is inert out of the box.** Tiers 7 to 10 always come from MCM sliders, tiers 0 to 6 come from the JSON only when "Override Vanilla Tiers (T1-T6)" is on, and it ships off (`TaomMilitaryPowerModel.cs:42-53`, `TaomSettings.cs:250`).
- **List fields in `combat_mechanics_config.json` replace, they do not merge.** Deserialization uses `ObjectCreationHandling.Replace`, so writing one monster id deletes the other five ([combat-mechanics](../features/combat-mechanics.md)).
- **The `race_age_config.json` table in the feature doc is stale.** The file ships human `maxAge` 200 and `comesOfAge` 18 for all 15 races; the doc says 85 and lists per-race values from 6 to 30 (`race-age-system.md:65-83`).
- **The `~80` and `87` troop-weight counts in the feature docs are both wrong** (`troop-weight-system.md:339`, `lessons/gamemodels-services.md:418`). The file holds 105 live rows and two undocumented tiers, 4.0 and 10.0.
- **`SETTLEMENT_ECONOMY_FLOOR` guards a file outside the repo.** It compares `TAOM_Map/ModuleData/settlements.xml` against a committed spec. This file lives in the game install, not the repo; a module reinstall reverts hand edits, so land a repo-side validator gate with any fix. That gate is exactly what the check is (`taom_schema.py:559-640`).
- **Two questions this chapter cannot answer from any TAOM doc.** There is no catalogue of the MCM-only knobs and their defaults, so `Main/Features/TaomSettings.cs` is the only source. And there is no list of which troops lack a `troop_weights.xml` row or a `troop_resource_costs.xml` row; the fallbacks are documented (weight 1.0, no cost), but the coverage gap is not measured anywhere.

## Numbers in this chapter

| Number | Command | Date |
|---|---|---|
| 105 live `<TroopWeight>` rows (106 raw lines, one commented out); 93 at 2.0, 10 at 3.0, 1 at 4.0, 1 at 10.0 | python ElementTree count plus a `weight` Counter over `troop_weights.xml`, and `rg -c '<TroopWeight '` for the raw line count | 2026-09-05 |
| 77 `<Troop>` rows with six distinct attributes in `troop_resource_costs.xml` | python ElementTree attribute union over `troop_resource_costs.xml` | 2026-09-05 |
| 11 `<Resource>` rows in `special_resources_config.xml` | python ElementTree count over `special_resources_config.xml` | 2026-09-05 |
| 22 `<Culture>` rows in `startup_resources_config.xml` | python ElementTree count over `startup_resources_config.xml` | 2026-09-05 |
| 15 race entries in `race_age_config.json` | python json key count over `races` | 2026-09-05 |
| 13 field duties, 11 interactive duties, 3 incidents; difficulties 40 to 76; `failureReward.trust` is 0 on all 13 | python json array lengths and field scan over `enlistment_duties.json` | 2026-09-05 |
| 1,735 `<PassiveEffect>` elements, 23 distinct types, 310 wrapped, 290 using `value=` alone, 1,445 carrying `operation`, 0 choices with a second passive | python ElementTree scan over `taom_career_choices.xml` | 2026-09-05 |
| 24 keys in `execution/alignment.json`, mixing kingdom and culture ids | python json key listing | 2026-09-05 |
| 432 `[SettingProperty` declarations across 55 MCM groups | `rg -o "\[SettingProperty" Main/Features/TaomSettings.cs \| wc -l` and `rg -o 'SettingPropertyGroup\("[^"]*"' Main/Features/TaomSettings.cs \| sort -u \| wc -l` | 2026-09-05 |
| 14 providers, all registered `Reuse.Singleton` | `rg -n` over the fourteen IoC files named in the registration table | 2026-09-05 |
| 0 hits for any of the fifteen filenames in the ModuleData validator; 3 files in `tools/schemas/` | `rg -n` over `tools/validate_moduledata.py` and `tools/taom_schema.py`, and `ls tools/schemas/` | 2026-09-05 |

## Read next

- [troop-weight-system](../features/troop-weight-system.md), [special-resources](../features/special-resources.md), [elite-emissary](../features/elite-emissary.md), [startup-resources](../features/startup-resources.md)
- [battle-balance](../features/battle-balance.md), [combat-mechanics](../features/combat-mechanics.md), [ai-party-size](../features/ai-party-size.md)
- [settlement-food](../features/settlement-food.md), [settlement-economy](../features/settlement-economy.md), [revolt-tuning](../features/revolt-tuning.md)
- [race-age-system](../features/race-age-system.md), [initial-child-generation](../features/initial-child-generation.md), [field-commission](../features/field-commission.md), [alignment-recruitment](../features/alignment-recruitment.md), [enlistment](../features/enlistment.md), [career-system](../features/career-system.md)
- [moduledata-validation](../features/moduledata-validation.md), [csharp-architecture rule](../../.claude/rules/csharp-architecture.md), [tools README](../../tools/README.md)
- [Faction and world configs](configs-factions-and-world.md), [Balance levers](balance-levers.md), [Validation and testing](validation-and-testing.md)

---

<!-- backlinks-start auto-generated; edit lint_docs.py / build_backlinks.py to change -->

## Referenced by

- [docs/INDEX.md](../INDEX.md)
- [docs/modding/balance-levers.md](./balance-levers.md)
- [docs/modding/configs-factions-and-world.md](./configs-factions-and-world.md)
- [docs/modding/file-catalogue.md](./file-catalogue.md)
- [docs/modding/README.md](./README.md)
- [docs/modding/recipe-add-a-culture.md](./recipe-add-a-culture.md)
- [docs/modding/recipe-add-a-kingdom.md](./recipe-add-a-kingdom.md)
- [docs/modding/troubleshooting.md](./troubleshooting.md)

<!-- backlinks-end -->
