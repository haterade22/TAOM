# Career fork review table: Gondor

Approved under the maintainer's overnight delegation on 2026-10-15 (the lead's analysis; the maintainer reviews it next). Every number is a draft in tools/career_design/_rules.json.

Generated from `tools/career_design/gondor.json` with the rules in `_rules.json`. Every balance number is a draft; approve or change them here before the first apply.

VU means value units: the benefit worth one unit is the `vu_unit` of the effect type (a cost type's benefit is its negative magnitude). Class-only troop effects are worth 1/1.5 as much per unit.

## captain_of_osgiliath (infantry)

Tree version 2, a point at levels 1, 5, 10, 15, 20, 25.

**Root:** +5% melee damage. (+5% damage (melee))

| Fork | Tier | Option | Effects | VU | Description |
|---|---|---|---|---|---|
| 1 Oath of the Crossing | 1 | a Blade of the Ford | +15% damage (melee) | 3.0 | +15% melee damage. |
|  |  | b Word of the Captain | +8% troop damage [infantry] | 3.2 | Your infantry deal 8% more damage. |
| 2 Steward's Ledger | 1 | a Heir of the Faithful | +25% renown gain; +25% influence gain | 3.0 | +25% renown, and +25% influence while you serve a kingdom. |
|  |  | b Steward's Provision | -100% special resource upkeep modifier | 3.0 | Your elite troops cost no daily upkeep in your realm's special resource. |
| 3 Muster of the Fiefs | 2 | a Companies of the Outlands | +50 party size | 4.0 | +50 party size. |
|  |  | b Coffers of the Steward | +100% special resource gain | 4.0 | Double your realm's special resource from every source. |
| 4 Ward of the White Tower | 2 | a Light of Mithrandir | +60% troop fear resistance | 4.0 | Your troops lose 60% less morale to dread and terror. |
|  |  | b Locked Shields of Osgiliath | +50% troop crush hold | 4.0 | Your troops' blocks hold against a crush-through half the time. |
|  |  | c Spears of the Rammas | +50% troop knockdown resistance | 4.0 | Your troops resist knockdown from blows and light charges 50% better. |
| 5 Tower Guard Drill | 3 | a Veterans of the Citadel | +15 troop training [infantry] | 5.0 | Your infantry gain 15 experience each day. |
|  |  | b Levies of Lossarnach | +20 party size; -30% recruit cost | 4.6 | +20 party size, and recruits cost 30% less. |
| 6 Endurance of Westernesse | 3 | a Last upon the Bridge | +20% resistance | 5.0 | You take 20% less damage. |
|  |  | b Guard of the Citadel | +12% troop resistance [infantry] | 4.8 | Your infantry take 12% less damage. |
|  |  | c Horn of Gondor | Keystone: Duration +8; Radius +20 | 5.0 | Your Career Ability lasts 8 seconds longer and reaches 20 meters further. |

### Caps

| Cap | Highest any build reaches | Limit | Headroom |
|---|---|---|---|
| Damage+ArmorPenetration | 0.2 | 0.45 | 0.25 |
| Resistance | 0.2 | 0.4 | 0.2 |
| TroopDamage+TroopResistance | 0.2 | 0.2 | 0 |
| PartySize | 70 | 75 | 5 |
| SpecialResourceGain | 1 | 1 | 0 |
| SpecialResourceUpkeepModifier | 1 | 1 | 0 |
| TroopFearResistance | 0.6 | 0.75 | 0.15 |
| TroopKnockdownResistance | 0.5 | 0.6 | 0.1 |
| TroopCrushHold | 0.5 | 0.6 | 0.1 |
| TroopMorale | 0 | 0.5 | 0.5 |
| RecruitCost | 0.3 | 0.5 | 0.2 |
| TroopWages | 0 | 0.5 | 0.5 |
| FoodConsumption | 0 | 0.5 | 0.5 |
| TradePrices | 0 | 0.75 | 0.75 |
| RaidLoot | 0 | 0.8 | 0.8 |
| LootGold | 0 | 1 | 1 |
| InfluenceGain | 0.25 | 1 | 0.75 |
| RenownGain | 0.25 | 1 | 0.75 |
| DailyGold | 0 | 250 | 250 |
| TroopTraining | 15 | 25 | 10 |
| WoundRecovery | 0 | 1 | 1 |
| PrisonerConversion | 0 | 1 | 1 |
| TroopSurvival | 0 | 0.5 | 0.5 |
| TroopUpgradeCost | 0 | 0.5 | 0.5 |
| SpecialResourceUpgradeCostModifier | 0 | 0.5 | 0.5 |

- Worst build for Damage+ArmorPenetration: f1:a
- Worst build for Resistance: f6:a
- Worst build for TroopDamage+TroopResistance: f1:b f6:b
- Worst build for PartySize: f3:a f5:b
- Worst build for SpecialResourceGain: f3:b
- Worst build for SpecialResourceUpkeepModifier: f2:b
- Worst build for TroopFearResistance: f4:a
- Worst build for TroopKnockdownResistance: f4:c
- Worst build for TroopCrushHold: f4:b
- Worst build for RecruitCost: f5:b
- Worst build for InfluenceGain: f2:a
- Worst build for RenownGain: f2:a
- Worst build for TroopTraining: f5:a

## ranger_of_ithilien (ranged)

Tree version 2, a point at levels 1, 5, 10, 15, 20, 25.

**Root:** +5% ranged damage. (+5% damage (ranged))

| Fork | Tier | Option | Effects | VU | Description |
|---|---|---|---|---|---|
| 1 Oath of Henneth Annun | 1 | a Arrow of the Ithil-wood | +15% damage (ranged) | 3.0 | +15% ranged damage. |
|  |  | b Captain's Volley | +8% troop damage [ranged] | 3.2 | Your ranged troops deal 8% more damage. |
| 2 Steward's Ledger | 1 | a Heir of the Faithful | +25% renown gain; +25% influence gain | 3.0 | +25% renown, and +25% influence while you serve a kingdom. |
|  |  | b Steward's Provision | -100% special resource upkeep modifier | 3.0 | Your elite troops cost no daily upkeep in your realm's special resource. |
| 3 Muster of the Fiefs | 2 | a Companies of the Outlands | +50 party size | 4.0 | +50 party size. |
|  |  | b Coffers of the Steward | +100% special resource gain | 4.0 | Double your realm's special resource from every source. |
| 4 Ward of the White Tower | 2 | a Light of Mithrandir | +60% troop fear resistance | 4.0 | Your troops lose 60% less morale to dread and terror. |
|  |  | b Locked Shields of Osgiliath | +50% troop crush hold | 4.0 | Your troops' blocks hold against a crush-through half the time. |
|  |  | c Spears of the Rammas | +50% troop knockdown resistance | 4.0 | Your troops resist knockdown from blows and light charges 50% better. |
| 5 Watch on the Poros | 3 | a Veterans of Henneth Annun | +15 troop training [ranged] | 5.0 | Your ranged troops gain 15 experience each day. |
|  |  | b Levies of Lossarnach | +20 party size; -30% recruit cost | 4.6 | +20 party size, and recruits cost 30% less. |
| 6 Shadow of Ithilien | 3 | a Unseen Archer | +20% resistance | 5.0 | You take 20% less damage. |
|  |  | b Green-cloaked Wardens | +12% troop resistance [ranged] | 4.8 | Your ranged troops take 12% less damage. |
|  |  | c Hidden Watch | Keystone: Duration +8; Radius +20 | 5.0 | Your Career Ability lasts 8 seconds longer and reaches 20 meters further. |

### Caps

| Cap | Highest any build reaches | Limit | Headroom |
|---|---|---|---|
| Damage+ArmorPenetration | 0.2 | 0.45 | 0.25 |
| Resistance | 0.2 | 0.4 | 0.2 |
| TroopDamage+TroopResistance | 0.2 | 0.2 | 0 |
| PartySize | 70 | 75 | 5 |
| SpecialResourceGain | 1 | 1 | 0 |
| SpecialResourceUpkeepModifier | 1 | 1 | 0 |
| TroopFearResistance | 0.6 | 0.75 | 0.15 |
| TroopKnockdownResistance | 0.5 | 0.6 | 0.1 |
| TroopCrushHold | 0.5 | 0.6 | 0.1 |
| TroopMorale | 0 | 0.5 | 0.5 |
| RecruitCost | 0.3 | 0.5 | 0.2 |
| TroopWages | 0 | 0.5 | 0.5 |
| FoodConsumption | 0 | 0.5 | 0.5 |
| TradePrices | 0 | 0.75 | 0.75 |
| RaidLoot | 0 | 0.8 | 0.8 |
| LootGold | 0 | 1 | 1 |
| InfluenceGain | 0.25 | 1 | 0.75 |
| RenownGain | 0.25 | 1 | 0.75 |
| DailyGold | 0 | 250 | 250 |
| TroopTraining | 15 | 25 | 10 |
| WoundRecovery | 0 | 1 | 1 |
| PrisonerConversion | 0 | 1 | 1 |
| TroopSurvival | 0 | 0.5 | 0.5 |
| TroopUpgradeCost | 0 | 0.5 | 0.5 |
| SpecialResourceUpgradeCostModifier | 0 | 0.5 | 0.5 |

- Worst build for Damage+ArmorPenetration: f1:a
- Worst build for Resistance: f6:a
- Worst build for TroopDamage+TroopResistance: f1:b f6:b
- Worst build for PartySize: f3:a f5:b
- Worst build for SpecialResourceGain: f3:b
- Worst build for SpecialResourceUpkeepModifier: f2:b
- Worst build for TroopFearResistance: f4:a
- Worst build for TroopKnockdownResistance: f4:c
- Worst build for TroopCrushHold: f4:b
- Worst build for RecruitCost: f5:b
- Worst build for InfluenceGain: f2:a
- Worst build for RenownGain: f2:a
- Worst build for TroopTraining: f5:a

## knight_of_belfalas (cavalry)

Tree version 2, a point at levels 1, 5, 10, 15, 20, 25.

**Root:** +5% melee damage. (+5% damage (melee))

| Fork | Tier | Option | Effects | VU | Description |
|---|---|---|---|---|---|
| 1 Oath of the Swan | 1 | a Charge of the Swan-Knights | +10% mount charge damage | 3.0 | Your mount's charge deals 10% more damage. |
|  |  | b Banner of Dol Amroth | +8% troop damage [cavalry] | 3.2 | Your cavalry deal 8% more damage. |
| 2 Steward's Ledger | 1 | a Heir of the Faithful | +25% renown gain; +25% influence gain | 3.0 | +25% renown, and +25% influence while you serve a kingdom. |
|  |  | b Steward's Provision | -100% special resource upkeep modifier | 3.0 | Your elite troops cost no daily upkeep in your realm's special resource. |
| 3 Muster of the Fiefs | 2 | a Companies of the Outlands | +50 party size | 4.0 | +50 party size. |
|  |  | b Coffers of the Steward | +100% special resource gain | 4.0 | Double your realm's special resource from every source. |
| 4 Ward of the White Tower | 2 | a Light of Mithrandir | +60% troop fear resistance | 4.0 | Your troops lose 60% less morale to dread and terror. |
|  |  | b Locked Shields of Osgiliath | +50% troop crush hold | 4.0 | Your troops' blocks hold against a crush-through half the time. |
|  |  | c Spears of the Rammas | +50% troop knockdown resistance | 4.0 | Your troops resist knockdown from blows and light charges 50% better. |
| 5 Stables of Dol Amroth | 3 | a Veteran Swan-Knights | +15 troop training [cavalry] | 5.0 | Your cavalry gain 15 experience each day. |
|  |  | b Levies of Lossarnach | +20 party size; -30% recruit cost | 4.6 | +20 party size, and recruits cost 30% less. |
| 6 The Silver Swan | 3 | a Prince's Mail | +20% resistance | 5.0 | You take 20% less damage. |
|  |  | b Swan Guard | +12% troop resistance [cavalry] | 4.8 | Your cavalry take 12% less damage. |
|  |  | c Stampede of Belfalas | Keystone: Duration +8; Radius +20 | 5.0 | Your Career Ability lasts 8 seconds longer and reaches 20 meters further. |

### Caps

| Cap | Highest any build reaches | Limit | Headroom |
|---|---|---|---|
| Damage+ArmorPenetration | 0.05 | 0.45 | 0.4 |
| Resistance | 0.2 | 0.4 | 0.2 |
| TroopDamage+TroopResistance | 0.2 | 0.2 | 0 |
| PartySize | 70 | 75 | 5 |
| SpecialResourceGain | 1 | 1 | 0 |
| SpecialResourceUpkeepModifier | 1 | 1 | 0 |
| TroopFearResistance | 0.6 | 0.75 | 0.15 |
| TroopKnockdownResistance | 0.5 | 0.6 | 0.1 |
| TroopCrushHold | 0.5 | 0.6 | 0.1 |
| TroopMorale | 0 | 0.5 | 0.5 |
| RecruitCost | 0.3 | 0.5 | 0.2 |
| TroopWages | 0 | 0.5 | 0.5 |
| FoodConsumption | 0 | 0.5 | 0.5 |
| TradePrices | 0 | 0.75 | 0.75 |
| RaidLoot | 0 | 0.8 | 0.8 |
| LootGold | 0 | 1 | 1 |
| InfluenceGain | 0.25 | 1 | 0.75 |
| RenownGain | 0.25 | 1 | 0.75 |
| DailyGold | 0 | 250 | 250 |
| TroopTraining | 15 | 25 | 10 |
| WoundRecovery | 0 | 1 | 1 |
| PrisonerConversion | 0 | 1 | 1 |
| TroopSurvival | 0 | 0.5 | 0.5 |
| TroopUpgradeCost | 0 | 0.5 | 0.5 |
| SpecialResourceUpgradeCostModifier | 0 | 0.5 | 0.5 |

- Worst build for Damage+ArmorPenetration: no picks
- Worst build for Resistance: f6:a
- Worst build for TroopDamage+TroopResistance: f1:b f6:b
- Worst build for PartySize: f3:a f5:b
- Worst build for SpecialResourceGain: f3:b
- Worst build for SpecialResourceUpkeepModifier: f2:b
- Worst build for TroopFearResistance: f4:a
- Worst build for TroopKnockdownResistance: f4:c
- Worst build for TroopCrushHold: f4:b
- Worst build for RecruitCost: f5:b
- Worst build for InfluenceGain: f2:a
- Worst build for RenownGain: f2:a
- Worst build for TroopTraining: f5:a
