# Battle Balance

## Overview

Three GameModel overrides that tune large-scale battle math: troop power values for tier-7 through tier-10 troops, blunt-vs-cut damage ratios per battle type (player vs AI-only), and per-culture casualty rates. All three are configurable through both an `Main/_Module/ModuleData/configs/battle_balance_config.json` file and the in-game MCM panel; MCM gates take precedence and the JSON values supply data-driven baselines.

## Why This Exists

- **Vanilla behavior:**
  - `DefaultMilitaryPowerModel.GetDefaultTroopPower` uses the formula `(2 + tier) * (10 + tier) * 0.02` and never sees a tier above 6.
  - `DefaultCombatSimulationModel.GetBluntDamageChance` returns a fixed blunt chance: 30% in a player map event, 10% otherwise.
  - `DefaultPartyHealingModel.GetSurvivalChance` produces uniform survival chances across cultures.
- **TAOM requirement:** Tier 7-10 troops exist (Cultural Feats system, lots of elite LOTR units like Dúnedain Rangers, Royal Guard); their power values must be authored, not extrapolated. LOTR cultures should also have distinct casualty profiles: the orcs of Gundabad and Dol Guldur die more easily, and elite Eldar formations should rarely take real casualties. The player should be able to dial wound-vs-kill ratios for their own battles separately from AI-vs-AI sim.
- **Without this feature:** T7+ troops fall back to a polynomial extrapolation that produces unintuitive AutoCalc results; cultural identity in losses is invisible; the player can't tune lethality without editing source.

## Architecture

### Design Challenge

Three independent vanilla GameModels each need overriding with different shapes of data — a tier-keyed dictionary, a binary player/AI switch, and a culture-keyed dictionary. The override classes need access to both the JSON config (defaults, comprehensive coverage) and the MCM settings (live override knobs). Constructor injection through DryIoc keeps each model thin and testable.

A fourth wrinkle: `TaomPartyHealingModel` integrates the [career system](career-system.md)'s passives: `TroopSurvival` gives the survival chance a multiplicative bonus (through `ICareerPassiveService.ScaleByPassive`, capped at 1) when the party's owner-first hero has the passive, `HeroHealing` is a factor on the heroes' daily healing, and `WoundRecovery` (#769 E11) a factor on the regulars' daily healing, keyed on the party's leader and applied to a positive value only. This crosses feature boundaries deliberately (career passives are an opt-in modifier on top of the cultural baseline, not a replacement).

### Solution Approach

Standard GameModel override (see [.claude/rules/gamemodels.md](../../.claude/rules/gamemodels.md)) for each of the three vanilla models. Two providers fan in the data:

- [IBattleBalanceConfigProvider](../../Main/Features/BattleBalance/IBattleBalanceConfigProvider.cs) — wraps `battle_balance_config.json` deserialization, single-pass cached, falls back to baked-in defaults if the file is missing or malformed.
- [IBattleBalanceSettingsProvider](../../Main/Features/BattleBalance/IBattleBalanceSettingsProvider.cs): exposes MCM switches and float properties, falling back to compile-time defaults while `TaomSettings.Instance` is null. It caches the settings reference on the first non-null read (never in the constructor, so an early resolve cannot pin the defaults) and reads every value through it.

Both providers register as `Reuse.Singleton`. The cache means JSON edits require a full Bannerlord process restart — see Configuration > Reload Scope below.

### Component Diagram

```
battle_balance_config.json
        |
BattleBalanceConfigProvider (Singleton, IPathService + IModLogger)
        |
        +--+
        |  |
        |  IBattleBalanceConfigProvider
        |  IBattleBalanceSettingsProvider  ← reads TaomSettings.Instance
        |  |
   +----+--+----+----------+
   |       |    |          |
   v       v    v          v
TaomMilitaryPowerModel       TaomCombatSimulationModel       TaomPartyHealingModel
  : DefaultMilitaryPowerModel  : DefaultCombatSimulationModel  : DefaultPartyHealingModel
  GetDefaultTroopPower         GetBluntDamageChance            GetSurvivalChance
                                                               GetDailyHealingForRegulars
                                                               GetDailyHealingHpForHeroes
                                                                    |
                                                                    +-> ICareerPassiveService (constructor-injected)
                                                                          ScaleByPassive(TroopSurvival)
                                                                          ApplyWoundRecovery
                                                                          ApplyFactor(HeroHealing)
```

## Configuration

### Config file

[Main/_Module/ModuleData/configs/battle_balance_config.json](../../Main/_Module/ModuleData/configs/battle_balance_config.json)

```json
{
  "TroopPower": {
    "TierPower": {
      "T0": 0.40, "T1": 0.66, "T2": 0.96, "T3": 1.30,
      "T4": 1.68, "T5": 2.10, "T6": 2.56, "T7": 2.91,
      "T8": 3.26, "T9": 3.61, "T10": 3.96
    }
  },
  "CasualtyRatios": {
    "EnableCulturalSurvivalBonuses": true,
    "CulturalSurvivalBonuses": {
      "gondor": 0.3, "vlandia": 0.2, "lothlorien": 0.5,
      "erebor": 0.3, "eredluin": 0.3, "bluecraig": 0.3, "rivendell": 0.4, "lindon": 0.4, "mirkwood": 0.4, "arthedain": 0.3,
      "cardolan": 0.3, "rhudaur": 0.2, "anduinvale": 0.1, "angaladh": 0.1, "dorwinion": 0.1, "southrhovanion": 0.2,
      "gundabad": -0.1, "dolguldur": -0.1
    }
  },
  "AutoResolve": { "PowerRatioExponent": 0.5 },
  "SiegeDefence": {
    "DefenderAdvantageBonuses": { "bluecraig": 0.1 },
    "UpkeepFreeGarrisons": { "bluecraig": true },
    "DailyGarrisonGrowth": { "bluecraig": 2 }
  }
}
```

| Section | Field | Type | Effect |
|---|---|---|---|
| `TroopPower.TierPower` | `T0..T10` | float | Per-tier power; consulted when `OverrideVanillaTierPower=true` for ≤T6, always for ≥T7 |
| `CasualtyRatios.EnableCulturalSurvivalBonuses` | bool | Master kill switch on cultural bonus application |
| `CasualtyRatios.CulturalSurvivalBonuses` | `<cultureId>` | float | bonus > 0 reduces death chance; bonus < 0 increases it. Formula: `newDeathChance = vanillaDeathChance * (1 - bonus)` |
| `SiegeDefence.DefenderAdvantageBonuses` | `<cultureId>` | float | Factor added to the defenders' battle advantage in an auto-resolved siege assault of a fortification that culture owns; see "Siege defence by culture" |
| `SiegeDefence.UpkeepFreeGarrisons` | `<cultureId>` | bool | `true`: that realm's garrisons cost no wages, eat no food and take no starvation wounds; see "Garrison upkeep by realm" |
| `SiegeDefence.DailyGarrisonGrowth` | `<cultureId>` | float | Free basic troops each of that realm's fortifications gains a day; see "Garrison upkeep by realm" |

### MCM (TaomSettings)

| Group | Setting | Default | Effect |
|---|---|---|---|
| Troop Power | `EnableCustomTroopPower` | true | Master switch; off → fall through to vanilla |
| Troop Power | `OverrideVanillaTierPower` | false | If true, also use JSON values for T0-T6 (otherwise vanilla formula) |
| Troop Power | `Tier7Power` / `Tier8Power` / `Tier9Power` / `Tier10Power` | 2.91 / 3.26 / 3.61 / 3.96 | Live MCM overrides for the high tiers |
| Troop Power | `HeroMultiplier` | 1.5 | Multiplier applied to hero `GetDefaultTroopPower` result |
| Troop Power | `MountedMultiplier` | 1.2 | Multiplier applied to non-hero mounted troops |
| Casualty Ratios | `EnableCustomCasualtyRatios` | true | Master switch for blunt-chance overrides |
| Casualty Ratios | `PlayerBluntDamageChance` | 0.30 | Blunt chance during player-involved map events |
| Casualty Ratios | `AIBluntDamageChance` | 0.10 | Blunt chance during AI-only map events |
| Casualty Ratios | `EnableCulturalSurvivalBonuses` | true | Gates the culture-keyed survival adjustment |

### Reload scope

Both providers register `Reuse.Singleton`, so the JSON file is cached for the entire Bannerlord process. **JSON edits require a full Bannerlord restart**, not a save-load and not a new campaign. MCM switches **do** apply live (each model reads `IBattleBalanceSettingsProvider` properties on every call, and the provider reads each value through its cached `TaomSettings` reference, which is the one object MCM edits in place, including on reset and preset).

## Key Files

| File | Purpose |
|---|---|
| [Main/Features/BattleBalance/Models/TaomMilitaryPowerModel.cs](../../Main/Features/BattleBalance/Models/TaomMilitaryPowerModel.cs) | Override — tier power table + hero/mounted multipliers |
| [Main/Features/BattleBalance/Models/TaomCombatSimulationModel.cs](../../Main/Features/BattleBalance/Models/TaomCombatSimulationModel.cs) | Override: player vs AI blunt-damage chance, plus a `SimulateHit` override that rescales the power ratio exponent (#1283, `AutoResolveQualityMath`) and applies the refuge defender damage reduction (#507) |
| [Main/Features/BattleBalance/AutoResolveQualityMath.cs](../../Main/Features/BattleBalance/AutoResolveQualityMath.cs) | Pure math: the strike damage factor ratio^(e - 0.7) and its translation onto the final `ExplainedNumber` |
| [Main/Features/BattleBalance/Models/TaomPartyHealingModel.cs](../../Main/Features/BattleBalance/Models/TaomPartyHealingModel.cs) | Override, a thin entry point: `GetSurvivalChance` is base plus `Hooks/SurvivalChanceLine` (cultural bonus, career passive; an enlisted player rolls with his own party) over the pure `SurvivalChanceMath`; `GetDailyHealingForRegulars` is the field camp rest line on base, then the career `WoundRecovery` (#769 E11, positive values on the non-prisoner call only), and `GetDailyHealingHpForHeroes` adds `HeroHealing` |
| [Main/Features/BattleBalance/BattleBalanceConfig.cs](../../Main/Features/BattleBalance/BattleBalanceConfig.cs) | POCOs (`TroopPowerSection`, `CasualtyRatiosSection`) with default-valued dictionaries baked in |
| [Main/Features/BattleBalance/BattleBalanceConfigProvider.cs](../../Main/Features/BattleBalance/BattleBalanceConfigProvider.cs) | Loads + caches `battle_balance_config.json`; logs warnings on missing/malformed |
| [Main/Features/BattleBalance/BattleBalanceSettingsProvider.cs](../../Main/Features/BattleBalance/BattleBalanceSettingsProvider.cs) | MCM proxy with compile-time fallbacks |
| [Main/Features/BattleBalance/IBattleBalanceConfigProvider.cs](../../Main/Features/BattleBalance/IBattleBalanceConfigProvider.cs) | Interface |
| [Main/Features/BattleBalance/IBattleBalanceSettingsProvider.cs](../../Main/Features/BattleBalance/IBattleBalanceSettingsProvider.cs) | Interface (12 properties) |
| [Main/Features/BattleBalance/BattleBalanceIoC.cs](../../Main/Features/BattleBalance/BattleBalanceIoC.cs) | Two singleton registrations |
| [Main/_Module/ModuleData/configs/battle_balance_config.json](../../Main/_Module/ModuleData/configs/battle_balance_config.json) | The JSON config file |
| [Main/SubModule.cs:1217-1219](../../Main/SubModule.cs) | `campaignStarter.AddModel(...)` for all three models |

## Dependencies

- `IBattleBalanceConfigProvider` (this feature)
- `IBattleBalanceSettingsProvider` (this feature)
- `IPathService` (Core/Infrastructure) — locates `Main/_Module/ModuleData/`
- `IModLogger` (Core/Logging)
- `ICareerPassiveService` (`CareerSystem`): injected into `TaomPartyHealingModel` through its constructor and passed to `SurvivalChanceLine.Apply`. Provides the `TroopSurvival`, `WoundRecovery` and `HeroHealing` passives per hero.

## Tests

- [TAOM.Tests/Features/BattleBalance/TaomMilitaryPowerModelTests.cs](../../TAOM.Tests/Features/BattleBalance/TaomMilitaryPowerModelTests.cs) — **10 tests**: T7-T10 configured values, T6 vanilla/override fallback, T11+ formula extension, low-tier override behavior.
- [TAOM.Tests/Features/BattleBalance/TaomCombatSimulationModelTests.cs](../../TAOM.Tests/Features/BattleBalance/TaomCombatSimulationModelTests.cs): player vs AI blunt-chance routing, and a wiring pin that the registration passes the config and `SimulateHit` passes the striker's power first, the struck troop's second and the configured exponent (#1283).
- [TAOM.Tests/Features/BattleBalance/AutoResolveQualityMathTests.cs](../../TAOM.Tests/Features/BattleBalance/AutoResolveQualityMathTests.cs) and [AutoResolveQualityApplyTests.cs](../../TAOM.Tests/Features/BattleBalance/AutoResolveQualityApplyTests.cs) (`RequiresGame`): the damage factor, its gates on a bad power or exponent, the base-relative addend, and `Apply` on a real `ExplainedNumber`, where swapping the striker and struck powers fails.
- [TAOM.Tests/Features/BattleBalance/TaomPartyHealingModelTests.cs](../../TAOM.Tests/Features/BattleBalance/TaomPartyHealingModelTests.cs): **15 `[TestMethod]`s** across three classes (this file also holds the casualty-ratio and troop-power config tests): cultural bonus zero / positive / negative, boundary clamping (0..1), and the null-party path of `SurvivalChanceLine`. The career `TroopSurvival` term (`Math.Min(1f, ScaleByPassive(...))`, floored at zero by the bounded scale) is pinned in `CareerCampaignPassivesTests`.
- [TAOM.Tests/Features/BattleBalance/ShippedBattleBalanceConfigTests.cs](../../TAOM.Tests/Features/BattleBalance/ShippedBattleBalanceConfigTests.cs): Lindon's row in the shipped JSON equals Rivendell's (#749), Arthedain's equals Gondor's, the compiled defaults carry every culture row the shipped file does, and the shipped exponent is 0.5 and equals the compiled default (#1283). They read the file with `ObjectCreationHandling.Replace`, because the provider's default merge would fill a missing key from the compiled dictionary and hide its absence.
- [TAOM.Tests/Features/BattleBalance/BattleBalanceConfigProviderValidationTests.cs](../../TAOM.Tests/Features/BattleBalance/BattleBalanceConfigProviderValidationTests.cs): one test per rejection branch of `BattleBalanceConfigProvider.Validate` (#1291): an out-of-range or non-finite survival value reverts to its compiled row (Mirkwood to 0.4, Gundabad keeps -0.1), a bad row with no compiled row is removed (survival reads 0, a tier reads the formula), the edges -1 and 1 are kept, a bad tier value reverts to its compiled default, a null section or table reverts to the compiled one with its warning and the summary, an exponent outside [0, 1] or not finite reverts to 0.5, and two rejects give one warning each plus one summary warning. Two more go through `GetConfig` and a temp file, so the Newtonsoft merge and the call to `Validate` are covered.
- [TAOM.Tests/Features/BattleBalance/BattleBalanceSettingsProviderTests.cs](../../TAOM.Tests/Features/BattleBalance/BattleBalanceSettingsProviderTests.cs): **10 tests**: the no-MCM fallbacks (six literal pins, and all twelve against the `TaomSettings` compiled defaults), read-through (one setting edited per pass, after every getter has been read once, reaches its own getter and no other), resolution from a real DryIoc container, and an IL rule that no getter or constructor calls `TaomSettings.Instance`, only the private lazy accessor (PERF-04).

The models themselves test the static helpers (`CalculateTierPower`, `CalculateBluntChance`, and `SurvivalChanceMath.ApplyCulturalSurvivalBonus`): the `override`-method paths that touch `IoC.Resolve` and live game state are exercised in-game.

## Auto-resolve: numbers over quality

Vanilla's simulated strike does `40 x (P_striker / P_struck)^0.7` damage (`DefaultCombatSimulationModel.SimulateHit`, line 21). In a field battle each side strikes about `N^0.6` times a round (`GetSimulationTicksForBattleRound`, lines 258-261). A strike removes a troop with chance damage / max hit points. So in a field battle a side's strength goes about as `N^1.6 x P^1.4`. `P` is the troop power: the tier (that is, level) value, times 1.2 when mounted, times `1 + leader modifier + context`, where the context term reads terrain, troop class and side (`DefaultMilitaryPowerModel`, lines 41-94). Gear and skills are not read.

A siege assault on a town with more than 100 defenders, or a castle with more than 30, uses lines 247-251 instead. There the attackers strike `round(1.5 + D^0.3) x 2` times a round and the defenders about `(1 + 0.7 x settlement advantage x D^0.3) x 2` times, `D` being the defender count. The attackers' numbers add no strikes until the garrison falls below that count, so in such an assault the exponent changes only the weight of each strike.

TAOM rescales the exponent to `AutoResolve.PowerRatioExponent` (shipped 0.5, #1283). Field strength then goes as `N^1.6 x P^1.0`, so troop numbers outweigh troop quality while quality still decides an even fight. Measured with a model of the v1.5.7 field loop (no morale, perks, retreat or siege branch; 150 to 300 trials per case):

| Case | At 0.7 (vanilla) | At 0.5 |
|---|---|---|
| 100 tier-7 (power 2.91) against 100 of power 1.0 | elites win, 85 left | elites win, 77 left |
| Mordor troops (1.23 per place) needed to beat 100 Gondor (1.97) | 154 | 136 |
| Tier-1 troops (0.66) needed to beat 100 tier-7 | 374 | 257 |
| 200 of power 2.9 against 1000 of power 1.0 | the 1000 win, 747 left | the 1000 win, 839 left |

Only the simulated battle changes. It changes for every simulated battle, the player's Send troops and the player's raids included; real-time battles never reach `SimulateHit`. The AI's strength estimates (war declaration, army gathering, siege targets, flee) sum `N x P` through `GetPowerOfParty` and do not read the exponent. `SimulateHit` reads the two troop powers again (`GetTroopPower` with the side, the simulation context and the leader hero's `PowerModifier`) and scales the final number through `AutoResolveQualityMath.Apply`, so the Armory_TAOM postfix on this override still sees one result. The base reads the internal `LeaderSimulationModifier`, which the engine caches at event start, after a load and when the leader party leaves. So the two reads differ only after a side's leader hero dies or is replaced during the event, by a few percent. The scaled damage also feeds hero wounds and deaths and the troops' XP (`MapEventSide`, lines 928-946), so expect more lord wounds against large hosts and faster upgrades for weak troops. 0.7 restores vanilla; 0 ignores quality. A gear (armour) factor was considered and not added: Gondor has the heaviest armour, so it would widen the lead #1283 reported.

## Siege defence by culture

In an auto-resolved siege assault the engine gives the attackers `AddFactor(-0.1)` on their battle advantage (`DefaultCombatSimulationModel.GetBattleAdvantage`, lines 278-286), and each simulated strike's damage is multiplied by the striker's advantage (`SimulateHit`, line 21). `TaomCombatSimulationModel.GetBattleAdvantage` adds a factor to the defenders' side when the besieged fortification's realm (`Settlement.MapFaction`) has a culture with a `SiegeDefence.DefenderAdvantageBonuses` row. `MapEvent.SimulateBattleRound` reads both advantages once per round. A sally-out is a different battle type and gets no bonus. An assault that a relief party joins from outside the walls becomes `SiegeOutside` (`SiegeAssaultEventComponent.OnAddInvolvedParty`), and from then on neither the bonus nor the attackers' -0.1 applies.

The shipped row is `bluecraig` 0.1 (#1283): Blue Craig stands alone in the far west, on a land-navigation island it shares only with its Eriador neighbours, and it lost its capital in three 150-day runs. With a defender with no Tactics bonus, its strikes do about 10% more damage. Blue Craig also gets the survival bonus of Arthedain and Ered Luin (0.3). The bonus keys on the fortification's realm, not on the town's culture, so a Blue Craig town that another realm takes loses it, and a town Blue Craig takes gains it. Live battles are unchanged: they do not read the battle advantage. `GetSettlementAdvantage` was considered and not used: it changes the defenders' strike count only above 100 town or 30 castle defenders, and it also delays the AI's assault decision (`BesiegerCamp`), which can starve a garrison.

## Garrison upkeep by realm

An AI garrison's size follows its wages. Each day `ClanVariablesCampaignBehavior.UpdateClanSettlementsPaymentLimit`
(lines 458-475) sets every AI fief's garrison wage limit from the realm's ideal garrison strength, the clan's economy, the
town's prosperity and its food potential. `GarrisonRecruitmentCampaignBehavior` grows a garrison only within its wage budget
(`PaymentLimit - TotalWage - 5`, divided by the troop's wage): both the base growth and the auto-recruitment (1 a day, more
with buildings, and only while the town's food change is above 0). The garrison also eats 1 food a day per 20 men from the
town (`DefaultSettlementFoodModel`, line 48). In a starving town a tenth of its regulars are wounded each day
(`DefaultPartyHealingModel`, lines 132 to 139, applied by `PartyHealCampaignBehavior.ReduceHpMemberRegulars`, which adds
wounded and kills none), and none heal while the stocks are empty.

A realm whose culture has `true` in `SiegeDefence.UpkeepFreeGarrisons` drops those costs (#1283). Each model keys on the
fortification's realm (`Settlement.MapFaction`, which is the owner clan's realm), so a player vassal of that realm, a player
kingdom of that culture and a rebel town of that culture qualify too:

- `TaomPartyWageModel.GetTotalWage` (through `Hooks/FreeGarrisonUpkeepLine.Wage`): its garrison's wage is 0, so the owner clan pays no garrison wages. The wage budget no
  longer shrinks as the garrison grows: it stays at the fief's wage limit minus 5, which still caps one day's growth.
- `TaomSettlementFoodModel.CalculateTownFoodStocksChange`: the town gets back the base's unscaled garrison term, members
  divided by `NumberOfMenOnGarrisonToEatOneFood`, on the "Garrison" tooltip line. The base scales its charge: a governor with
  Generosity +1 or +2 raises it by 10%, so that garrison still eats a tenth; FoodConsumption buildings and, in a siege, a
  Gourmet governor lower it, so there the town gains the difference. The refund is a realm rule, so it applies with the food
  tuning toggle off too.
- `TaomPartyHealingModel.GetDailyHealingForRegulars`: a negative value, which only the starving branch gives, becomes 0, so
  the garrison takes no starvation wounds. The starving morale penalty (-30) can still apply when the town's food change
  sits on the starving line; it stays above the desertion threshold.

Three gold costs remain. The owner clan still pays the recruit price of each auto-recruited troop
(`OwnerClan.AutoRecruitmentExpenses`, through `GetTroopRecruitmentCost`). The clan leader pays the upgrade price of each
garrison troop that upgrades (`PartyUpgraderCampaignBehavior`), and with a wage of 0 its wage gate no longer limits those
upgrades, so free garrisons climb tiers faster. And lords' parties that take troops from the garrison pay those troops'
wages.

`SiegeDefence.DailyGarrisonGrowth` gives each fortification of that realm free troops a day through the new
`TaomSettlementGarrisonModel.CalculateBaseGarrisonChange` (vanilla's base adds +2 for rebels and the issue effects). The
engine adds that many of the realm's basic troop (`GetBasicTroopForTown` returns `town.MapFaction.BasicTroop`; for Blue
Craig `goblin_snaga`), whatever the town's own culture, only while the fortification is in no battle or siege, and the
growth never takes the garrison past its size limit. The growth is free only with an `UpkeepFreeGarrisons` row; without
one it is wage-capped and its troops cost wages. Lords of the owner clan who enter the fortification also take troops when
it holds more than its share of their combined regulars (`GarrisonTroopsCampaignBehavior.CollectPartyGarrisonTransferData`),
never leaving fewer than 125 in a town or 75 in a castle, so part of the growth goes to the field armies.

The shipped rows give Blue Craig both, with 2 troops a day: it fell in every 150-day run, and the maintainer chose a realm of
swarm and kings that needs no gold or food for its garrisons, tuned until it lasts about 300 days.

## How to Add a Cultural Survival Bonus for a New Culture

1. Edit [Main/_Module/ModuleData/configs/battle_balance_config.json](../../Main/_Module/ModuleData/configs/battle_balance_config.json).
2. Under `CasualtyRatios.CulturalSurvivalBonuses`, add `"<cultureId>": <bonus>` where `cultureId` matches the `<Culture id="...">` value in the relevant XML and `<bonus>` is a float in `[-1.0, +1.0]` (positive = better survival, negative = worse).
3. Restart Bannerlord (the provider caches for the whole process).
4. **Verify in-game:** Trigger or sim a battle for a party of that culture. Cultural cultures (`gondor`, `gundabad`, etc.) should see materially different survival rates compared to a culture without an entry.

## How to Tune T7-T10 Power Live

Use the MCM panel: **TAOM → Troop Power → Tier7Power / Tier8Power / Tier9Power / Tier10Power**. Changes apply on the next `GetDefaultTroopPower` call, no restart needed. To make ≤T6 also use JSON values, flip MCM **OverrideVanillaTierPower** to `true`.

## Changelog

- 2026-11-16: Garrison upkeep by realm (#1283): `SiegeDefence.UpkeepFreeGarrisons` makes a realm's garrisons cost no wages, eat no food and take no starvation wounds, and `SiegeDefence.DailyGarrisonGrowth` gives its fortifications free troops each day through the new `TaomSettlementGarrisonModel`. Blue Craig gets both, with 2 troops a day.
- 2026-11-16: Siege defence by culture (#1283): `SiegeDefence.DefenderAdvantageBonuses` adds a factor to the defenders' battle advantage in an auto-resolved siege of a fortification that culture owns. Blue Craig gets 0.1, and the survival bonus 0.3 of Arthedain and Ered Luin.
- 2026-11-15: Auto-resolve raises the power ratio to 0.5, not vanilla's 0.7 (`AutoResolve.PowerRatioExponent`, #1283), so troop numbers count for more than troop quality; see "Auto-resolve: numbers over quality".
- 2026-11-15: `BattleBalanceConfigProvider` reverts a bad survival value to that culture's compiled row, not to 0, so a typo in a penalty row (Gundabad -0.1) no longer deletes the penalty (#1291). A bad row with no compiled row is removed, so the lookup's own fallback decides: 0 for survival, the tier formula for power (before, a bad `T11` became 1.0, not the formula's 5.46). A `null` section or table now reverts to the compiled one; before, a `"CasualtyRatios": null` reached `SurvivalChanceLine.Apply`, which reads the section with no null check (`SurvivalChanceLine.cs:30`). Any reversion ends with one summary warning that counts the values.
- 2026-11-14: Mordor's -0.2 survival row is removed from the JSON and the compiled default (#1283). The row multiplied the death chance of a wounded regular by 1.2. With no Medicine bonus on either side (no surgeon skill in the party, no Doctor's Oath in the enemy's) the engine's chance is 1 / (1 + level x 0.02), so a troop of level 10 or below never survived a cut or pierce blow, and a level 26 troop survived 21% of the time, against 34% with no row and 54% for Gondor. Mirkwood gets Rivendell's 0.4; it had no row.
- 2026-10-06: Lindon gets Rivendell's 0.4 survival bonus in the JSON and the compiled default (#749). It had none since Lindon became its own culture in `cc1713eb3` (2026-08-11); before that it ran on `rivendell`.
- 2026-09-24: plan 003 (PERF-04): `BattleBalanceSettingsProvider` stops resolving `TaomSettings.Instance` on every read; it caches the reference lazily on the first non-null read and reads through it, so MCM edits still apply live.
- 2026-05-13 — Phase 9b: `BattleBalanceConfigProvider` now validates per-key (TierPower T0-T10 finite + > 0, CulturalSurvivalBonuses finite + [-1, +1]); invalid values revert to compiled default with a warning (partial closes #140).
- 2026-05-07 — Feature doc `battle-balance.md` created (backfilled one of 5 missing feature docs flagged by `detect-docs-gaps.sh`).
- 2026-04-06 — Config key fixes (`rohan`→`vlandia`, `dol_guldur`→`dolguldur`) plus test DataRows, from the full-codebase adversarial review.
- 2026-03-31 — Fixed `TaomPartyHealingModel.GetSurvivalChance` NRE in arena practice by guarding the null `party` parameter (#52).

## GitHub Issue

- **Issue:** None — feature predates the mandatory issue-per-feature policy.
- **Status:** Shipping. Stable.

---

<!-- backlinks-start auto-generated; edit lint_docs.py / build_backlinks.py to change -->

## Referenced by

- [docs/features/auto-resolve-diagnostics.md](./auto-resolve-diagnostics.md)
- [docs/features/troop-skill-balance.md](./troop-skill-balance.md)
- [docs/INDEX.md](../INDEX.md)
- [docs/modding/configs-balance.md](../modding/configs-balance.md)
- [docs/modding/load-order-and-dependencies.md](../modding/load-order-and-dependencies.md)

<!-- backlinks-end -->
