# Codex Adversarial Review: SettlementFood (TaomSettlementFoodModel)

You are an adversarial reviewer. Attack the correctness of a new TAOM feature. CONFIRM or DISPUTE each Known Suspect with evidence from the actual source files and the vanilla decompile. Do not flag vanilla-matching code as a bug. Use -- not em-dash.

## Feature

New `TaomSettlementFoodModel : DefaultSettlementFoodModel` (Bannerlord v1.4.x total-conversion mod). Two jobs:
1. Fix a Troop-Weight side effect: TAOM's `Patch17_TroopWeight` postfixes the global `PartyBase.NumberOfAllMembers` getter and raises it to a weighted count (elite troops count >1). Vanilla `DefaultSettlementFoodModel` reads `town.GarrisonParty.Party.NumberOfAllMembers / NumberOfMenOnGarrisonToEatOneFood` for garrison food consumption, so elite garrisons ate 2-3x the intended food. The model neutralizes this by adding back `(NumberOfAllMembers - MemberRoster.TotalManCount) / garrisonDivisor` (a positive correction reducing consumption).
2. Expose vanilla's hardcoded food constants as MCM/JSON-tunable knobs (consumption divisors, base/village/flat production, storage caps).

## READ FIRST (repo root: c:\Users\mikew\source\repos\TAOM)

- docs/features/settlement-food.md
- docs/reference/engine/settlement-economy-food-prosperity.md (full vanilla mechanics with file:line cites)
- Main/_Module/ModuleData/settlement_food/settlement_food_config.json

## Known Suspects (CONFIRM or DISPUTE each, with evidence)

1. DOUBLE-COUNTING vs base. The override calls `base.CalculateTownFoodStocksChange(...)` then adds a service delta. CONFIRM every service term is a DELTA against vanilla (garrison correction = (weighted-raw)/div; base food = configBase - 15/10; village = (hearthLevel+1)*(mult-6); flat = configFlat), NOT an absolute re-addition. At vanilla-default config the total delta MUST be exactly 0 (garrison correction is 0 only when weighted==raw). Attack: is there any config or state where the model adds vanilla's full garrison/production a second time?

2. DIVISOR CONSISTENCY. `base` computes garrison consumption as `weighted / NumberOfMenOnGarrisonToEatOneFood` using the model's OVERRIDDEN constant. The service correction divides `(weighted-raw)` by `config.GarrisonFoodDivisor`. These MUST be the same number, or the net garrison term is wrong. CONFIRM the model's `NumberOfMenOnGarrisonToEatOneFood` returns `config.GarrisonFoodDivisor` (when enabled) AND the service uses the same `config.GarrisonFoodDivisor`. Attack the disabled case: when the MCM toggle is OFF, the constant returns vanilla 20 and the service returns 0 -- is that consistent?

3. SIEGE-GATING. Production knobs (base/village/flat) are wrapped in `if (!IsUnderSiege)`; the garrison correction is OUTSIDE that guard. Vanilla drops ALL production under siege but still consumes garrison food. CONFIRM: applying production knobs under siege would re-introduce food vanilla removed (so gating them is correct), and the garrison correction must apply under siege (the weight inflation exists during siege too). Attack: is the guard inverted or misplaced? Does base actually zero production under siege in v1.4.x?

4. WEIGHTED>RAW GUARD + Patch17 re-trigger. The correction only adds when `WeightedGarrisonCount > RawGarrisonCount`. `WeightedGarrisonCount` is read via `garrison.Party.NumberOfAllMembers` (which re-invokes the Patch17 postfix -> weighted), `RawGarrisonCount` via `garrison.MemberRoster.TotalManCount` (unpatched). Attack: (a) can weighted ever be LESS than raw (troop weights <1)? Check TAOM troop_weights.xml -- are any weights <1.0? (b) If Troop Weight MCM toggle is OFF, Patch17 early-returns, so NumberOfAllMembers==raw, correction==0 -- confirm this is a clean no-op. (c) Does reading NumberOfAllMembers inside the food model reliably hit the patched value (is the postfix applied to that getter)?

5. NULL/EDGE in TownFoodSnapshot.FromTown. `garrison?.Party.NumberOfAllMembers ?? 0` -- note the `?.` is on garrison, then `.Party.NumberOfAllMembers` is NOT null-guarded. Can `garrison.Party` be null for a valid garrison MobileParty? Vanilla reads `town.GarrisonParty?.Party.NumberOfAllMembers` the same way -- is the risk profile identical? Also: villages filtered to `VillageStates.Normal` -- does this match the set vanilla applies *6 to?

6. CONFIG VALIDATION. SettlementFoodConfigProvider validates divisors >= 1 (reject 0 -> Infinity), floats finite >= 0 via FiniteFloatValidator, reverts invalid to vanilla. Attack: any field reachable with a value that divides-by-zero, produces NaN/Infinity in the ExplainedNumber, or flips a production knob negative (worsening starvation)?

## Files

GameModel: Main/Features/SettlementFood/Models/TaomSettlementFoodModel.cs
Service (pure math): Main/Features/SettlementFood/SettlementFoodService.cs
Snapshot (boundary): Main/Features/SettlementFood/TownFoodSnapshot.cs
Config POCO: Main/Features/SettlementFood/SettlementFoodConfig.cs
Config provider: Main/Features/SettlementFood/SettlementFoodConfigProvider.cs
Interfaces: Main/Features/SettlementFood/ISettlementFoodService.cs, ISettlementFoodConfigProvider.cs
IoC: Main/Features/SettlementFood/SettlementFoodIoC.cs
MCM: Main/Features/TaomSettings.cs (EnableSettlementFoodTuning property)
Registration: Main/IoC.cs (RegisterSettlementFoodFeature call), Main/SubModule.cs (AddModel(new TaomSettlementFoodModel(...)))
Config JSON: Main/_Module/ModuleData/settlement_food/settlement_food_config.json
Tests: TAOM.Tests/Features/SettlementFood/SettlementFoodServiceTests.cs, SettlementFoodConfigProviderTests.cs
Troop weights (for Suspect 4a): Main/_Module/ModuleData/troop_weights.xml
Troop-weight patch: Main/Features/TroopWeight/Hooks/PartyBase_NumberOfAllMembers_Patch.cs, Main/Features/TroopWeight/TroopWeightService.cs

## VANILLA CODE (v1.4.x DefaultSettlementFoodModel.CalculateTownFoodChangeInternal -- the method base calls)

```
ExplainedNumber bonuses3 = new ExplainedNumber(town.Prosperity / (float)NumberOfProsperityToEatOneFood);
ExplainedNumber bonuses4 = new ExplainedNumber((((float?)town.GarrisonParty?.Party.NumberOfAllMembers) ?? 0f) / (float)NumberOfMenOnGarrisonToEatOneFood);
// ... perks ...
bonuses2.Add(bonuses3.ResultNumber, ProsperityText);
bonuses2.Add(bonuses4.ResultNumber, GarrisonText);
town.AddEffectOfBuildings(BuildingEffectEnum.FoodConsumption, ref bonuses2);
// HuntingRights +2
if (!town.IsUnderSiege) {
    int num = (town.IsTown ? 15 : 10);
    bonuses.Add(num, LandsAroundSettlementText);
    foreach (Village boundVillage in town.Owner.Settlement.BoundVillages) {
        float value = 0f;
        if (boundVillage.VillageState == Village.VillageStates.Normal)
            value = (boundVillage.GetHearthLevel() + 1) * 6;
        bonuses.Add(value, boundVillage.Name);
    }
    town.AddEffectOfBuildings(BuildingEffectEnum.FoodProduction, ref bonuses);
} else { /* DirtyFighting perk only */ }
// market stocks (BonusToFoodStores) added to bonuses
explainedNumber.AddFromExplainedNumber(bonuses);      // production
explainedNumber.SubtractFromExplainedNumber(bonuses2); // consumption
```
Constants: NumberOfProsperityToEatOneFood=40, NumberOfMenOnGarrisonToEatOneFood=20, FoodStocksUpperLimit=300, CastleFoodStockUpperLimitBonus=150. `PartyBase.NumberOfAllMembers => MemberRoster.TotalManCount`.

## REQUIRED OUTPUT SECTIONS

- KNOWN SUSPECTS: per suspect -- CONFIRMED / DISPUTED + 2-4 lines of evidence (cite file:line).
- DEEP ANALYSIS: walk one concrete scenario end-to-end -- town, prosperity 3000, garrison raw 500 weighted 750, 3 Normal villages hearthLevel 1, config raised (garrisonDivisor 30, prosperityDivisor 60, villageMultiplier 9). Compute base result and the model's final result by hand; show the arithmetic; confirm no double-count and the math is what the feature doc claims.
- FINDINGS: numbered, each with severity (HIGH/MED/LOW), file:line, and the exact fix. If none, say so explicitly.

## QUALITY GATES

Cite file:line for every claim. Do NOT flag code that matches the pasted vanilla. Do NOT assume IDs. If you cannot verify a claim, say UNVERIFIED -- do not guess.
