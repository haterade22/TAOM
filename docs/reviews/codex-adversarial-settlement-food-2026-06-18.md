OpenAI Codex v0.128.0 (research preview)
--------
workdir: C:\Users\mikew\source\repos\TAOM
model: gpt-5.5
provider: openai
approval: never
sandbox: workspace-write [workdir, /tmp, C:\Users\mikew\.codex\memories]
reasoning effort: xhigh
reasoning summaries: none
session id: 019edc47-c6a3-70c2-8688-2ca195b75654
--------
user
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

ERROR: The process "67440" not found.
codex
I’ll review this as a read-only pass. I’m going to pull the SettlementFood implementation, tests, troop-weight patch/config, and the vanilla decompiled food model so each suspect is tied to concrete lines rather than assumptions.
exec
"C:\\Program Files\\PowerShell\\7\\pwsh.exe" -Command 'rg -n "class TaomSettlementFoodModel|CalculateTownFoodStocksChange|NumberOfMenOnGarrisonToEatOneFood|FoodStocksUpperLimit|CastleFoodStockUpperLimitBonus|NumberOfProsperityToEatOneFood" Main/Features/SettlementFood/Models/TaomSettlementFoodModel.cs' in C:\Users\mikew\source\repos\TAOM
exec
"C:\\Program Files\\PowerShell\\7\\pwsh.exe" -Command 'rg -n "NumberOfAllMembers|Patch17|TroopWeight|EnableTroopWeight|TotalManCount|Postfix" Main/Features/TroopWeight Main/Features/TaomSettings.cs' in C:\Users\mikew\source\repos\TAOM
exec
"C:\\Program Files\\PowerShell\\7\\pwsh.exe" -Command 'rg -n "class SettlementFoodService|Calculate|Garrison|Village|Prosperity|Flat|IsUnderSiege|WeightedGarrisonCount|RawGarrisonCount" Main/Features/SettlementFood/SettlementFoodService.cs Main/Features/SettlementFood/TownFoodSnapshot.cs Main/Features/SettlementFood/SettlementFoodConfig.cs Main/Features/SettlementFood/SettlementFoodConfigProvider.cs' in C:\Users\mikew\source\repos\TAOM
exec
"C:\\Program Files\\PowerShell\\7\\pwsh.exe" -Command 'rg -n "SettlementFood|EnableSettlementFoodTuning|RegisterSettlementFoodFeature|TaomSettlementFoodModel|AddModel" Main/Features/SettlementFood Main/Features/TaomSettings.cs Main/IoC.cs Main/SubModule.cs' in C:\Users\mikew\source\repos\TAOM
2026-06-18T19:49:33.008692Z ERROR codex_core::tools::router: error=Exit code: 1
Wall time: 5.1 seconds
Output:
[31;1mrg: [0m
[31;1m[36;1mLine |[0m
[31;1m[36;1m[36;1m   2 | [0m [36;1mrg[0m -n "NumberOfAllMembers|Patch17|TroopWeight|EnableTroopWeight|Total …[0m
[31;1m[36;1m[36;1m[0m[36;1m[0m[36;1m     | [31;1m ~~[0m
[31;1m[36;1m[36;1m[0m[36;1m[0m[36;1m[31;1m[31;1m[36;1m     | [31;1mThe term 'rg' is not recognized as a name of a cmdlet, function, script file, or executable program.[0m
[31;1m[36;1m[36;1m[0m[36;1m[0m[36;1m[31;1m[31;1m[36;1m[31;1mCheck the spelling of the name, or if a path was included, verify that the path is correct and try again.[0m

2026-06-18T19:49:33.009636Z ERROR codex_core::tools::router: error=Exit code: 1
Wall time: 5.1 seconds
Output:
[31;1mrg: [0m
[31;1m[36;1mLine |[0m
[31;1m[36;1m[36;1m   2 | [0m [36;1mrg[0m -n "SettlementFood|EnableSettlementFoodTuning|RegisterSettlementFo …[0m
[31;1m[36;1m[36;1m[0m[36;1m[0m[36;1m     | [31;1m ~~[0m
[31;1m[36;1m[36;1m[0m[36;1m[0m[36;1m[31;1m[31;1m[36;1m     | [31;1mThe term 'rg' is not recognized as a name of a cmdlet, function, script file, or executable program.[0m
[31;1m[36;1m[36;1m[0m[36;1m[0m[36;1m[31;1m[31;1m[36;1m[31;1mCheck the spelling of the name, or if a path was included, verify that the path is correct and try again.[0m

 exited 1 in 5054ms:
[31;1mrg: [0m
[31;1m[36;1mLine |[0m
[31;1m[36;1m[36;1m   2 | [0m [36;1mrg[0m -n "SettlementFood|EnableSettlementFoodTuning|RegisterSettlementFo …[0m
[31;1m[36;1m[36;1m[0m[36;1m[0m[36;1m     | [31;1m ~~[0m
[31;1m[36;1m[36;1m[0m[36;1m[0m[36;1m[31;1m[31;1m[36;1m     | [31;1mThe term 'rg' is not recognized as a name of a cmdlet, function, script file, or executable program.[0m
[31;1m[36;1m[36;1m[0m[36;1m[0m[36;1m[31;1m[31;1m[36;1m[31;1mCheck the spelling of the name, or if a path was included, verify that the path is correct and try again.[0m

 exited 1 in 5055ms:
[31;1mrg: [0m
[31;1m[36;1mLine |[0m
[31;1m[36;1m[36;1m   2 | [0m [36;1mrg[0m -n "NumberOfAllMembers|Patch17|TroopWeight|EnableTroopWeight|Total …[0m
[31;1m[36;1m[36;1m[0m[36;1m[0m[36;1m     | [31;1m ~~[0m
[31;1m[36;1m[36;1m[0m[36;1m[0m[36;1m[31;1m[31;1m[36;1m     | [31;1mThe term 'rg' is not recognized as a name of a cmdlet, function, script file, or executable program.[0m
[31;1m[36;1m[36;1m[0m[36;1m[0m[36;1m[31;1m[31;1m[36;1m[31;1mCheck the spelling of the name, or if a path was included, verify that the path is correct and try again.[0m

 exited 1 in 5056ms:
[31;1mrg: [0m
[31;1m[36;1mLine |[0m
[31;1m[36;1m[36;1m   2 | [0m [36;1mrg[0m -n "class TaomSettlementFoodModel|CalculateTownFoodStocksChange|Nu …[0m
[31;1m[36;1m[36;1m[0m[36;1m[0m[36;1m     | [31;1m ~~[0m
[31;1m[36;1m[36;1m[0m[36;1m[0m[36;1m[31;1m[31;1m[36;1m     | [31;1mThe term 'rg' is not recognized as a name of a cmdlet, function, script file, or executable program.[0m
[31;1m[36;1m[36;1m[0m[36;1m[0m[36;1m[31;1m[31;1m[36;1m[31;1mCheck the spelling of the name, or if a path was included, verify that the path is correct and try again.[0m

 exited 1 in 5054ms:
[31;1mrg: [0m
[31;1m[36;1mLine |[0m
[31;1m[36;1m[36;1m   2 | [0m [36;1mrg[0m -n "class SettlementFoodService|Calculate|Garrison|Village|Prosper …[0m
[31;1m[36;1m[36;1m[0m[36;1m[0m[36;1m     | [31;1m ~~[0m
[31;1m[36;1m[36;1m[0m[36;1m[0m[36;1m[31;1m[31;1m[36;1m     | [31;1mThe term 'rg' is not recognized as a name of a cmdlet, function, script file, or executable program.[0m
[31;1m[36;1m[36;1m[0m[36;1m[0m[36;1m[31;1m[31;1m[36;1m[31;1mCheck the spelling of the name, or if a path was included, verify that the path is correct and try again.[0m

2026-06-18T19:49:33.011266Z ERROR codex_core::tools::router: error=Exit code: 1
Wall time: 5.1 seconds
Output:
[31;1mrg: [0m
[31;1m[36;1mLine |[0m
[31;1m[36;1m[36;1m   2 | [0m [36;1mrg[0m -n "class TaomSettlementFoodModel|CalculateTownFoodStocksChange|Nu …[0m
[31;1m[36;1m[36;1m[0m[36;1m[0m[36;1m     | [31;1m ~~[0m
[31;1m[36;1m[36;1m[0m[36;1m[0m[36;1m[31;1m[31;1m[36;1m     | [31;1mThe term 'rg' is not recognized as a name of a cmdlet, function, script file, or executable program.[0m
[31;1m[36;1m[36;1m[0m[36;1m[0m[36;1m[31;1m[31;1m[36;1m[31;1mCheck the spelling of the name, or if a path was included, verify that the path is correct and try again.[0m

2026-06-18T19:49:33.011796Z ERROR codex_core::tools::router: error=Exit code: 1
Wall time: 5.1 seconds
Output:
[31;1mrg: [0m
[31;1m[36;1mLine |[0m
[31;1m[36;1m[36;1m   2 | [0m [36;1mrg[0m -n "class SettlementFoodService|Calculate|Garrison|Village|Prosper …[0m
[31;1m[36;1m[36;1m[0m[36;1m[0m[36;1m     | [31;1m ~~[0m
[31;1m[36;1m[36;1m[0m[36;1m[0m[36;1m[31;1m[31;1m[36;1m     | [31;1mThe term 'rg' is not recognized as a name of a cmdlet, function, script file, or executable program.[0m
[31;1m[36;1m[36;1m[0m[36;1m[0m[36;1m[31;1m[31;1m[36;1m[31;1mCheck the spelling of the name, or if a path was included, verify that the path is correct and try again.[0m

codex
`rg` is not installed in this shell, so I’m falling back to PowerShell `Select-String` and line-numbered reads. The first pass will still be source-backed; I’ll keep it targeted to the files in scope.
exec
"C:\\Program Files\\PowerShell\\7\\pwsh.exe" -Command "Select-String -Path 'Main/Features/SettlementFood/Models/TaomSettlementFoodModel.cs' -Pattern 'class TaomSettlementFoodModel|CalculateTownFoodStocksChange|NumberOfMenOnGarrisonToEatOneFood|FoodStocksUpperLimit|CastleFoodStockUpperLimitBonus|NumberOfProsperityToEatOneFood|base\\.' | ForEach-Object { \"{0}:{1}: {2}\" -f "'$_.Path,$_.LineNumber,$_.Line.Trim() }' in C:\Users\mikew\source\repos\TAOM
exec
"C:\\Program Files\\PowerShell\\7\\pwsh.exe" -Command "Select-String -Path 'Main/Features/SettlementFood/SettlementFoodService.cs','Main/Features/SettlementFood/TownFoodSnapshot.cs','Main/Features/SettlementFood/SettlementFoodConfig.cs','Main/Features/SettlementFood/SettlementFoodConfigProvider.cs' -Pattern 'class SettlementFoodService|Calculate|Garrison|Village|Prosperity|Flat|IsUnderSiege|WeightedGarrisonCount|RawGarrisonCount|FiniteFloat|Validate|Divisor|return|if' | ForEach-Object { \"{0}:{1}: {2}\" -f "'$_.Path,$_.LineNumber,$_.Line.Trim() }' in C:\Users\mikew\source\repos\TAOM
exec
"C:\\Program Files\\PowerShell\\7\\pwsh.exe" -Command "Select-String -Path 'Main/Features/SettlementFood/*','Main/Features/TaomSettings.cs','Main/IoC.cs','Main/SubModule.cs' -Pattern 'SettlementFood|EnableSettlementFoodTuning|RegisterSettlementFoodFeature|TaomSettlementFoodModel|AddModel' | ForEach-Object { \"{0}:{1}: {2}\" -f "'$_.Path,$_.LineNumber,$_.Line.Trim() }' in C:\Users\mikew\source\repos\TAOM
exec
"C:\\Program Files\\PowerShell\\7\\pwsh.exe" -Command "Select-String -Path 'Main/Features/TroopWeight/*','Main/Features/TaomSettings.cs' -Pattern 'NumberOfAllMembers|Patch17|TroopWeight|EnableTroopWeight|TotalManCount|Postfix|ApplyWeight|PatchCategory' | ForEach-Object { \"{0}:{1}: {2}\" -f "'$_.Path,$_.LineNumber,$_.Line.Trim() }' in C:\Users\mikew\source\repos\TAOM
 succeeded in 551ms:
C:\Users\mikew\source\repos\TAOM\Main\Features\SettlementFood\Models\TaomSettlementFoodModel.cs:23: public class TaomSettlementFoodModel : DefaultSettlementFoodModel
C:\Users\mikew\source\repos\TAOM\Main\Features\SettlementFood\Models\TaomSettlementFoodModel.cs:36: public override int NumberOfMenOnGarrisonToEatOneFood => Enabled ? _config.GarrisonFoodDivisor : 20;
C:\Users\mikew\source\repos\TAOM\Main\Features\SettlementFood\Models\TaomSettlementFoodModel.cs:38: public override int NumberOfProsperityToEatOneFood => Enabled ? _config.ProsperityFoodDivisor : 40;
C:\Users\mikew\source\repos\TAOM\Main\Features\SettlementFood\Models\TaomSettlementFoodModel.cs:40: public override int FoodStocksUpperLimit => Enabled ? _config.FoodStocksUpperLimit : 300;
C:\Users\mikew\source\repos\TAOM\Main\Features\SettlementFood\Models\TaomSettlementFoodModel.cs:42: public override int CastleFoodStockUpperLimitBonus => Enabled ? _config.CastleFoodStockUpperLimitBonus : 150;
C:\Users\mikew\source\repos\TAOM\Main\Features\SettlementFood\Models\TaomSettlementFoodModel.cs:44: public override ExplainedNumber CalculateTownFoodStocksChange(Town town, bool includeMarketStocks = true, bool includeDescriptions = false)
C:\Users\mikew\source\repos\TAOM\Main\Features\SettlementFood\Models\TaomSettlementFoodModel.cs:46: var result = base.CalculateTownFoodStocksChange(town, includeMarketStocks, includeDescriptions);

 succeeded in 572ms:
C:\Users\mikew\source\repos\TAOM\Main\Features\SettlementFood\SettlementFoodService.cs:6: public class SettlementFoodService : ISettlementFoodService
C:\Users\mikew\source\repos\TAOM\Main\Features\SettlementFood\SettlementFoodService.cs:14: private const float VanillaVillageMultiplier = 6f;
C:\Users\mikew\source\repos\TAOM\Main\Features\SettlementFood\SettlementFoodService.cs:18: if (!enabled || snapshot == null || config == null)
C:\Users\mikew\source\repos\TAOM\Main\Features\SettlementFood\SettlementFoodService.cs:19: return 0f;
C:\Users\mikew\source\repos\TAOM\Main\Features\SettlementFood\SettlementFoodService.cs:23: // Garrison raw-count correction (always — the troop-weight inflation is a bug regardless of
C:\Users\mikew\source\repos\TAOM\Main\Features\SettlementFood\SettlementFoodService.cs:24: // siege). Base subtracted weighted/divisor; we want raw/divisor, so add back the over-count.
C:\Users\mikew\source\repos\TAOM\Main\Features\SettlementFood\SettlementFoodService.cs:25: // Uses the SAME divisor the model's NumberOfMenOnGarrisonToEatOneFood override fed to base.
C:\Users\mikew\source\repos\TAOM\Main\Features\SettlementFood\SettlementFoodService.cs:26: int garrisonDivisor = config.GarrisonFoodDivisor > 0 ? config.GarrisonFoodDivisor : 20;
C:\Users\mikew\source\repos\TAOM\Main\Features\SettlementFood\SettlementFoodService.cs:27: int overCount = snapshot.WeightedGarrisonCount - snapshot.RawGarrisonCount;
C:\Users\mikew\source\repos\TAOM\Main\Features\SettlementFood\SettlementFoodService.cs:28: if (overCount > 0)
C:\Users\mikew\source\repos\TAOM\Main\Features\SettlementFood\SettlementFoodService.cs:29: delta += overCount / (float)garrisonDivisor;
C:\Users\mikew\source\repos\TAOM\Main\Features\SettlementFood\SettlementFoodService.cs:31: // Production knobs are siege-gated: vanilla zeroes all village/lands production under siege,
C:\Users\mikew\source\repos\TAOM\Main\Features\SettlementFood\SettlementFoodService.cs:33: if (!snapshot.IsUnderSiege)
C:\Users\mikew\source\repos\TAOM\Main\Features\SettlementFood\SettlementFoodService.cs:39: float multiplierDelta = config.VillageFoodMultiplier - VanillaVillageMultiplier;
C:\Users\mikew\source\repos\TAOM\Main\Features\SettlementFood\SettlementFoodService.cs:40: if (multiplierDelta != 0f && snapshot.NormalVillageHearthLevels != null)
C:\Users\mikew\source\repos\TAOM\Main\Features\SettlementFood\SettlementFoodService.cs:42: foreach (var hearthLevel in snapshot.NormalVillageHearthLevels)
C:\Users\mikew\source\repos\TAOM\Main\Features\SettlementFood\SettlementFoodService.cs:46: delta += config.FlatFoodBonus;
C:\Users\mikew\source\repos\TAOM\Main\Features\SettlementFood\SettlementFoodService.cs:49: return delta;
C:\Users\mikew\source\repos\TAOM\Main\Features\SettlementFood\SettlementFoodService.cs:60: if (delta == 0f)
C:\Users\mikew\source\repos\TAOM\Main\Features\SettlementFood\SettlementFoodService.cs:61: return;
C:\Users\mikew\source\repos\TAOM\Main\Features\SettlementFood\TownFoodSnapshot.cs:10: /// <para><see cref="WeightedGarrisonCount"/> reads the (Troop-Weight-patched) <c>NumberOfAllMembers</c>
C:\Users\mikew\source\repos\TAOM\Main\Features\SettlementFood\TownFoodSnapshot.cs:11: /// getter; <see cref="RawGarrisonCount"/> reads the unpatched <c>MemberRoster.TotalManCount</c>. Vanilla
C:\Users\mikew\source\repos\TAOM\Main\Features\SettlementFood\TownFoodSnapshot.cs:12: /// <c>PartyBase.NumberOfAllMembers => MemberRoster.TotalManCount</c>, so their difference is exactly the
C:\Users\mikew\source\repos\TAOM\Main\Features\SettlementFood\TownFoodSnapshot.cs:13: /// weight inflation the food model must undo.</para>
C:\Users\mikew\source\repos\TAOM\Main\Features\SettlementFood\TownFoodSnapshot.cs:18: public bool IsUnderSiege { get; }
C:\Users\mikew\source\repos\TAOM\Main\Features\SettlementFood\TownFoodSnapshot.cs:19: public int RawGarrisonCount { get; }
C:\Users\mikew\source\repos\TAOM\Main\Features\SettlementFood\TownFoodSnapshot.cs:20: public int WeightedGarrisonCount { get; }
C:\Users\mikew\source\repos\TAOM\Main\Features\SettlementFood\TownFoodSnapshot.cs:22: /// <summary>Hearth levels (0/1/2) of every bound village currently in the Normal state.</summary>
C:\Users\mikew\source\repos\TAOM\Main\Features\SettlementFood\TownFoodSnapshot.cs:23: public IReadOnlyList<int> NormalVillageHearthLevels { get; }
C:\Users\mikew\source\repos\TAOM\Main\Features\SettlementFood\TownFoodSnapshot.cs:27: bool isUnderSiege,
C:\Users\mikew\source\repos\TAOM\Main\Features\SettlementFood\TownFoodSnapshot.cs:28: int rawGarrisonCount,
C:\Users\mikew\source\repos\TAOM\Main\Features\SettlementFood\TownFoodSnapshot.cs:29: int weightedGarrisonCount,
C:\Users\mikew\source\repos\TAOM\Main\Features\SettlementFood\TownFoodSnapshot.cs:30: IReadOnlyList<int> normalVillageHearthLevels)
C:\Users\mikew\source\repos\TAOM\Main\Features\SettlementFood\TownFoodSnapshot.cs:33: IsUnderSiege = isUnderSiege;
C:\Users\mikew\source\repos\TAOM\Main\Features\SettlementFood\TownFoodSnapshot.cs:34: RawGarrisonCount = rawGarrisonCount;
C:\Users\mikew\source\repos\TAOM\Main\Features\SettlementFood\TownFoodSnapshot.cs:35: WeightedGarrisonCount = weightedGarrisonCount;
C:\Users\mikew\source\repos\TAOM\Main\Features\SettlementFood\TownFoodSnapshot.cs:36: NormalVillageHearthLevels = normalVillageHearthLevels ?? new List<int>();
C:\Users\mikew\source\repos\TAOM\Main\Features\SettlementFood\TownFoodSnapshot.cs:45: var garrison = town?.GarrisonParty;
C:\Users\mikew\source\repos\TAOM\Main\Features\SettlementFood\TownFoodSnapshot.cs:46: int weighted = garrison?.Party.NumberOfAllMembers ?? 0;
C:\Users\mikew\source\repos\TAOM\Main\Features\SettlementFood\TownFoodSnapshot.cs:47: int raw = garrison?.MemberRoster?.TotalManCount ?? 0;
C:\Users\mikew\source\repos\TAOM\Main\Features\SettlementFood\TownFoodSnapshot.cs:50: var boundVillages = town?.Owner?.Settlement?.BoundVillages;
C:\Users\mikew\source\repos\TAOM\Main\Features\SettlementFood\TownFoodSnapshot.cs:51: if (boundVillages != null)
C:\Users\mikew\source\repos\TAOM\Main\Features\SettlementFood\TownFoodSnapshot.cs:53: foreach (var village in boundVillages)
C:\Users\mikew\source\repos\TAOM\Main\Features\SettlementFood\TownFoodSnapshot.cs:55: if (village != null && village.VillageState == Village.VillageStates.Normal)
C:\Users\mikew\source\repos\TAOM\Main\Features\SettlementFood\TownFoodSnapshot.cs:56: hearthLevels.Add(village.GetHearthLevel());
C:\Users\mikew\source\repos\TAOM\Main\Features\SettlementFood\TownFoodSnapshot.cs:60: return new TownFoodSnapshot(
C:\Users\mikew\source\repos\TAOM\Main\Features\SettlementFood\TownFoodSnapshot.cs:62: isUnderSiege: town?.IsUnderSiege ?? false,
C:\Users\mikew\source\repos\TAOM\Main\Features\SettlementFood\TownFoodSnapshot.cs:63: rawGarrisonCount: raw,
C:\Users\mikew\source\repos\TAOM\Main\Features\SettlementFood\TownFoodSnapshot.cs:64: weightedGarrisonCount: weighted,
C:\Users\mikew\source\repos\TAOM\Main\Features\SettlementFood\TownFoodSnapshot.cs:65: normalVillageHearthLevels: hearthLevels);
C:\Users\mikew\source\repos\TAOM\Main\Features\SettlementFood\SettlementFoodConfig.cs:6: /// production/consumption-rate side — the only always-on change is the garrison raw-count correction
C:\Users\mikew\source\repos\TAOM\Main\Features\SettlementFood\SettlementFoodConfig.cs:7: /// (which neutralises the Troop Weight feature inflating garrison food consumption).
C:\Users\mikew\source\repos\TAOM\Main\Features\SettlementFood\SettlementFoodConfig.cs:9: /// Divisors are "men/prosperity per 1 food eaten" — RAISING them makes garrisons/civilians cheaper
C:\Users\mikew\source\repos\TAOM\Main\Features\SettlementFood\SettlementFoodConfig.cs:11: /// docs/reference/engine/settlement-economy-food-prosperity.md.
C:\Users\mikew\source\repos\TAOM\Main\Features\SettlementFood\SettlementFoodConfig.cs:15: // Consumption divisors (vanilla: garrison 20, prosperity 40). Higher = less food eaten.
C:\Users\mikew\source\repos\TAOM\Main\Features\SettlementFood\SettlementFoodConfig.cs:16: public int GarrisonFoodDivisor { get; set; } = 20;
C:\Users\mikew\source\repos\TAOM\Main\Features\SettlementFood\SettlementFoodConfig.cs:17: public int ProsperityFoodDivisor { get; set; } = 40;
C:\Users\mikew\source\repos\TAOM\Main\Features\SettlementFood\SettlementFoodConfig.cs:23: // Per Normal-state bound village: (hearthLevel + 1) * multiplier (vanilla multiplier: 6).
C:\Users\mikew\source\repos\TAOM\Main\Features\SettlementFood\SettlementFoodConfig.cs:24: public float VillageFoodMultiplier { get; set; } = 6f;
C:\Users\mikew\source\repos\TAOM\Main\Features\SettlementFood\SettlementFoodConfig.cs:26: // Flat daily food added to every fortification (vanilla: 0). Siege-gated like all production.
C:\Users\mikew\source\repos\TAOM\Main\Features\SettlementFood\SettlementFoodConfig.cs:27: public float FlatFoodBonus { get; set; } = 0f;
C:\Users\mikew\source\repos\TAOM\Main\Features\SettlementFood\SettlementFoodConfigProvider.cs:29: if (!File.Exists(path))
C:\Users\mikew\source\repos\TAOM\Main\Features\SettlementFood\SettlementFoodConfigProvider.cs:32: return new SettlementFoodConfig();
C:\Users\mikew\source\repos\TAOM\Main\Features\SettlementFood\SettlementFoodConfigProvider.cs:44: return new SettlementFoodConfig();
C:\Users\mikew\source\repos\TAOM\Main\Features\SettlementFood\SettlementFoodConfigProvider.cs:47: return Validate(parsed);
C:\Users\mikew\source\repos\TAOM\Main\Features\SettlementFood\SettlementFoodConfigProvider.cs:50: private SettlementFoodConfig Validate(SettlementFoodConfig parsed)
C:\Users\mikew\source\repos\TAOM\Main\Features\SettlementFood\SettlementFoodConfigProvider.cs:54: GarrisonFoodDivisor = parsed.GarrisonFoodDivisor,
C:\Users\mikew\source\repos\TAOM\Main\Features\SettlementFood\SettlementFoodConfigProvider.cs:55: ProsperityFoodDivisor = parsed.ProsperityFoodDivisor,
C:\Users\mikew\source\repos\TAOM\Main\Features\SettlementFood\SettlementFoodConfigProvider.cs:58: VillageFoodMultiplier = parsed.VillageFoodMultiplier,
C:\Users\mikew\source\repos\TAOM\Main\Features\SettlementFood\SettlementFoodConfigProvider.cs:59: FlatFoodBonus = parsed.FlatFoodBonus,
C:\Users\mikew\source\repos\TAOM\Main\Features\SettlementFood\SettlementFoodConfigProvider.cs:67: // Divisors MUST be >= 1 — a 0 divisor poisons the vanilla food formula with Infinity.
C:\Users\mikew\source\repos\TAOM\Main\Features\SettlementFood\SettlementFoodConfigProvider.cs:68: if (sanitized.GarrisonFoodDivisor < 1 || sanitized.GarrisonFoodDivisor > 10000)
C:\Users\mikew\source\repos\TAOM\Main\Features\SettlementFood\SettlementFoodConfigProvider.cs:70: _logger.LogWarning($"SettlementFoodConfigProvider: garrisonFoodDivisor={sanitized.GarrisonFoodDivisor} outside [1,10000], reverting to default {defaults.GarrisonFoodDivisor}");
C:\Users\mikew\source\repos\TAOM\Main\Features\SettlementFood\SettlementFoodConfigProvider.cs:71: sanitized.GarrisonFoodDivisor = defaults.GarrisonFoodDivisor;
C:\Users\mikew\source\repos\TAOM\Main\Features\SettlementFood\SettlementFoodConfigProvider.cs:75: if (sanitized.ProsperityFoodDivisor < 1 || sanitized.ProsperityFoodDivisor > 10000)
C:\Users\mikew\source\repos\TAOM\Main\Features\SettlementFood\SettlementFoodConfigProvider.cs:77: _logger.LogWarning($"SettlementFoodConfigProvider: prosperityFoodDivisor={sanitized.ProsperityFoodDivisor} outside [1,10000], reverting to default {defaults.ProsperityFoodDivisor}");
C:\Users\mikew\source\repos\TAOM\Main\Features\SettlementFood\SettlementFoodConfigProvider.cs:78: sanitized.ProsperityFoodDivisor = defaults.ProsperityFoodDivisor;
C:\Users\mikew\source\repos\TAOM\Main\Features\SettlementFood\SettlementFoodConfigProvider.cs:83: if (!FiniteFloatValidator.IsFiniteInRange(sanitized.TownBaseFood, 0f, 10000f))
C:\Users\mikew\source\repos\TAOM\Main\Features\SettlementFood\SettlementFoodConfigProvider.cs:90: if (!FiniteFloatValidator.IsFiniteInRange(sanitized.CastleBaseFood, 0f, 10000f))
C:\Users\mikew\source\repos\TAOM\Main\Features\SettlementFood\SettlementFoodConfigProvider.cs:97: if (!FiniteFloatValidator.IsFiniteInRange(sanitized.VillageFoodMultiplier, 0f, 10000f))
C:\Users\mikew\source\repos\TAOM\Main\Features\SettlementFood\SettlementFoodConfigProvider.cs:99: _logger.LogWarning($"SettlementFoodConfigProvider: villageFoodMultiplier={sanitized.VillageFoodMultiplier} must be a finite value in [0,10000], reverting to default {defaults.VillageFoodMultiplier}");
C:\Users\mikew\source\repos\TAOM\Main\Features\SettlementFood\SettlementFoodConfigProvider.cs:100: sanitized.VillageFoodMultiplier = defaults.VillageFoodMultiplier;
C:\Users\mikew\source\repos\TAOM\Main\Features\SettlementFood\SettlementFoodConfigProvider.cs:104: if (!FiniteFloatValidator.IsFiniteInRange(sanitized.FlatFoodBonus, 0f, 100000f))
C:\Users\mikew\source\repos\TAOM\Main\Features\SettlementFood\SettlementFoodConfigProvider.cs:106: _logger.LogWarning($"SettlementFoodConfigProvider: flatFoodBonus={sanitized.FlatFoodBonus} must be a finite value in [0,100000], reverting to default {defaults.FlatFoodBonus}");
C:\Users\mikew\source\repos\TAOM\Main\Features\SettlementFood\SettlementFoodConfigProvider.cs:107: sanitized.FlatFoodBonus = defaults.FlatFoodBonus;
C:\Users\mikew\source\repos\TAOM\Main\Features\SettlementFood\SettlementFoodConfigProvider.cs:112: if (sanitized.FoodStocksUpperLimit < 1 || sanitized.FoodStocksUpperLimit > 1000000)
C:\Users\mikew\source\repos\TAOM\Main\Features\SettlementFood\SettlementFoodConfigProvider.cs:119: if (sanitized.CastleFoodStockUpperLimitBonus < 0 || sanitized.CastleFoodStockUpperLimitBonus > 1000000)
C:\Users\mikew\source\repos\TAOM\Main\Features\SettlementFood\SettlementFoodConfigProvider.cs:126: if (rejected)
C:\Users\mikew\source\repos\TAOM\Main\Features\SettlementFood\SettlementFoodConfigProvider.cs:131: return sanitized;

 succeeded in 567ms:
C:\Users\mikew\source\repos\TAOM\Main\Features\SettlementFood\ISettlementFoodConfigProvider.cs:1: namespace TAOM.Features.SettlementFood;
C:\Users\mikew\source\repos\TAOM\Main\Features\SettlementFood\ISettlementFoodConfigProvider.cs:3: public interface ISettlementFoodConfigProvider
C:\Users\mikew\source\repos\TAOM\Main\Features\SettlementFood\ISettlementFoodConfigProvider.cs:5: SettlementFoodConfig GetConfig();
C:\Users\mikew\source\repos\TAOM\Main\Features\SettlementFood\ISettlementFoodService.cs:3: namespace TAOM.Features.SettlementFood;
C:\Users\mikew\source\repos\TAOM\Main\Features\SettlementFood\ISettlementFoodService.cs:5: public interface ISettlementFoodService
C:\Users\mikew\source\repos\TAOM\Main\Features\SettlementFood\ISettlementFoodService.cs:12: float ComputeFoodDelta(TownFoodSnapshot snapshot, SettlementFoodConfig config, bool enabled);
C:\Users\mikew\source\repos\TAOM\Main\Features\SettlementFood\ISettlementFoodService.cs:20: SettlementFoodConfig config,
C:\Users\mikew\source\repos\TAOM\Main\Features\SettlementFood\SettlementFoodConfig.cs:1: namespace TAOM.Features.SettlementFood;
C:\Users\mikew\source\repos\TAOM\Main\Features\SettlementFood\SettlementFoodConfig.cs:4: /// Tunable knobs for <see cref="Models.TaomSettlementFoodModel"/>. Every default equals the vanilla
C:\Users\mikew\source\repos\TAOM\Main\Features\SettlementFood\SettlementFoodConfig.cs:5: /// <c>DefaultSettlementFoodModel</c> constant, so an unedited config is behaviourally vanilla on the
C:\Users\mikew\source\repos\TAOM\Main\Features\SettlementFood\SettlementFoodConfig.cs:13: public class SettlementFoodConfig
C:\Users\mikew\source\repos\TAOM\Main\Features\SettlementFood\SettlementFoodConfigProvider.cs:8: namespace TAOM.Features.SettlementFood;
C:\Users\mikew\source\repos\TAOM\Main\Features\SettlementFood\SettlementFoodConfigProvider.cs:10: public class SettlementFoodConfigProvider : ISettlementFoodConfigProvider
C:\Users\mikew\source\repos\TAOM\Main\Features\SettlementFood\SettlementFoodConfigProvider.cs:14: private readonly Lazy<SettlementFoodConfig> _config;
C:\Users\mikew\source\repos\TAOM\Main\Features\SettlementFood\SettlementFoodConfigProvider.cs:16: public SettlementFoodConfigProvider(IPathService pathService, IModLogger logger)
C:\Users\mikew\source\repos\TAOM\Main\Features\SettlementFood\SettlementFoodConfigProvider.cs:20: _config = new Lazy<SettlementFoodConfig>(LoadConfig);
C:\Users\mikew\source\repos\TAOM\Main\Features\SettlementFood\SettlementFoodConfigProvider.cs:23: public SettlementFoodConfig GetConfig() => _config.Value;
C:\Users\mikew\source\repos\TAOM\Main\Features\SettlementFood\SettlementFoodConfigProvider.cs:25: private SettlementFoodConfig LoadConfig()
C:\Users\mikew\source\repos\TAOM\Main\Features\SettlementFood\SettlementFoodConfigProvider.cs:31: _logger.LogWarning($"SettlementFoodConfigProvider: settlement_food_config.json not found at {path}, using defaults");
C:\Users\mikew\source\repos\TAOM\Main\Features\SettlementFood\SettlementFoodConfigProvider.cs:32: return new SettlementFoodConfig();
C:\Users\mikew\source\repos\TAOM\Main\Features\SettlementFood\SettlementFoodConfigProvider.cs:35: SettlementFoodConfig parsed;
C:\Users\mikew\source\repos\TAOM\Main\Features\SettlementFood\SettlementFoodConfigProvider.cs:39: parsed = JsonConvert.DeserializeObject<SettlementFoodConfig>(json) ?? new SettlementFoodConfig();
C:\Users\mikew\source\repos\TAOM\Main\Features\SettlementFood\SettlementFoodConfigProvider.cs:43: _logger.LogError($"SettlementFoodConfigProvider: Failed to parse settlement_food_config.json: {ex.Message}");
C:\Users\mikew\source\repos\TAOM\Main\Features\SettlementFood\SettlementFoodConfigProvider.cs:44: return new SettlementFoodConfig();
C:\Users\mikew\source\repos\TAOM\Main\Features\SettlementFood\SettlementFoodConfigProvider.cs:50: private SettlementFoodConfig Validate(SettlementFoodConfig parsed)
C:\Users\mikew\source\repos\TAOM\Main\Features\SettlementFood\SettlementFoodConfigProvider.cs:52: var sanitized = new SettlementFoodConfig
C:\Users\mikew\source\repos\TAOM\Main\Features\SettlementFood\SettlementFoodConfigProvider.cs:64: var defaults = new SettlementFoodConfig();
C:\Users\mikew\source\repos\TAOM\Main\Features\SettlementFood\SettlementFoodConfigProvider.cs:70: _logger.LogWarning($"SettlementFoodConfigProvider: garrisonFoodDivisor={sanitized.GarrisonFoodDivisor} outside [1,10000], reverting to default {defaults.GarrisonFoodDivisor}");
C:\Users\mikew\source\repos\TAOM\Main\Features\SettlementFood\SettlementFoodConfigProvider.cs:77: _logger.LogWarning($"SettlementFoodConfigProvider: prosperityFoodDivisor={sanitized.ProsperityFoodDivisor} outside [1,10000], reverting to default {defaults.ProsperityFoodDivisor}");
C:\Users\mikew\source\repos\TAOM\Main\Features\SettlementFood\SettlementFoodConfigProvider.cs:85: _logger.LogWarning($"SettlementFoodConfigProvider: townBaseFood={sanitized.TownBaseFood} must be a finite value in [0,10000], reverting to default {defaults.TownBaseFood}");
C:\Users\mikew\source\repos\TAOM\Main\Features\SettlementFood\SettlementFoodConfigProvider.cs:92: _logger.LogWarning($"SettlementFoodConfigProvider: castleBaseFood={sanitized.CastleBaseFood} must be a finite value in [0,10000], reverting to default {defaults.CastleBaseFood}");
C:\Users\mikew\source\repos\TAOM\Main\Features\SettlementFood\SettlementFoodConfigProvider.cs:99: _logger.LogWarning($"SettlementFoodConfigProvider: villageFoodMultiplier={sanitized.VillageFoodMultiplier} must be a finite value in [0,10000], reverting to default {defaults.VillageFoodMultiplier}");
C:\Users\mikew\source\repos\TAOM\Main\Features\SettlementFood\SettlementFoodConfigProvider.cs:106: _logger.LogWarning($"SettlementFoodConfigProvider: flatFoodBonus={sanitized.FlatFoodBonus} must be a finite value in [0,100000], reverting to default {defaults.FlatFoodBonus}");
C:\Users\mikew\source\repos\TAOM\Main\Features\SettlementFood\SettlementFoodConfigProvider.cs:114: _logger.LogWarning($"SettlementFoodConfigProvider: foodStocksUpperLimit={sanitized.FoodStocksUpperLimit} outside [1,1000000], reverting to default {defaults.FoodStocksUpperLimit}");
C:\Users\mikew\source\repos\TAOM\Main\Features\SettlementFood\SettlementFoodConfigProvider.cs:121: _logger.LogWarning($"SettlementFoodConfigProvider: castleFoodStockUpperLimitBonus={sanitized.CastleFoodStockUpperLimitBonus} outside [0,1000000], reverting to default {defaults.CastleFoodStockUpperLimitBonus}");
C:\Users\mikew\source\repos\TAOM\Main\Features\SettlementFood\SettlementFoodConfigProvider.cs:127: _logger.LogWarning("SettlementFoodConfigProvider: settlement_food_config.json contained invalid values. See prior warnings for details.");
C:\Users\mikew\source\repos\TAOM\Main\Features\SettlementFood\SettlementFoodConfigProvider.cs:129: _logger.LogInfo("SettlementFoodConfigProvider: Loaded settlement_food_config.json");
C:\Users\mikew\source\repos\TAOM\Main\Features\SettlementFood\SettlementFoodIoC.cs:3: namespace TAOM.Features.SettlementFood;
C:\Users\mikew\source\repos\TAOM\Main\Features\SettlementFood\SettlementFoodIoC.cs:5: public static class SettlementFoodIoC
C:\Users\mikew\source\repos\TAOM\Main\Features\SettlementFood\SettlementFoodIoC.cs:7: public static void RegisterSettlementFoodFeature(IContainer container)
C:\Users\mikew\source\repos\TAOM\Main\Features\SettlementFood\SettlementFoodIoC.cs:9: container.Register<ISettlementFoodConfigProvider, SettlementFoodConfigProvider>(Reuse.Singleton);
C:\Users\mikew\source\repos\TAOM\Main\Features\SettlementFood\SettlementFoodIoC.cs:10: container.Register<ISettlementFoodService, SettlementFoodService>(Reuse.Singleton);
C:\Users\mikew\source\repos\TAOM\Main\Features\SettlementFood\SettlementFoodService.cs:4: namespace TAOM.Features.SettlementFood;
C:\Users\mikew\source\repos\TAOM\Main\Features\SettlementFood\SettlementFoodService.cs:6: public class SettlementFoodService : ISettlementFoodService
C:\Users\mikew\source\repos\TAOM\Main\Features\SettlementFood\SettlementFoodService.cs:11: // Vanilla DefaultSettlementFoodModel constants the knobs are deltas against.
C:\Users\mikew\source\repos\TAOM\Main\Features\SettlementFood\SettlementFoodService.cs:16: public float ComputeFoodDelta(TownFoodSnapshot snapshot, SettlementFoodConfig config, bool enabled)
C:\Users\mikew\source\repos\TAOM\Main\Features\SettlementFood\SettlementFoodService.cs:54: SettlementFoodConfig config,
C:\Users\mikew\source\repos\TAOM\Main\Features\SettlementFood\TownFoodSnapshot.cs:4: namespace TAOM.Features.SettlementFood;
C:\Users\mikew\source\repos\TAOM\Main\Features\SettlementFood\TownFoodSnapshot.cs:8: /// so <see cref="SettlementFoodService"/> never touches sealed TaleWorlds types (ADR-007).
C:\Users\mikew\source\repos\TAOM\Main\Features\TaomSettings.cs:36: public bool EnableSettlementFoodTuning { get; set; } = true;
C:\Users\mikew\source\repos\TAOM\Main\IoC.cs:37: using TAOM.Features.SettlementFood;
C:\Users\mikew\source\repos\TAOM\Main\IoC.cs:102: SettlementFoodIoC.RegisterSettlementFoodFeature(container);
C:\Users\mikew\source\repos\TAOM\Main\SubModule.cs:64: using TAOM.Features.SettlementFood;
C:\Users\mikew\source\repos\TAOM\Main\SubModule.cs:65: using TAOM.Features.SettlementFood.Models;
C:\Users\mikew\source\repos\TAOM\Main\SubModule.cs:362: campaignStarter.AddModel(new TaomCharacterStatsModel());
C:\Users\mikew\source\repos\TAOM\Main\SubModule.cs:363: campaignStarter.AddModel(new TaomPartyWageModel(costService, careerPassives, wageModifiers));
C:\Users\mikew\source\repos\TAOM\Main\SubModule.cs:364: campaignStarter.AddModel(new TaomVolunteerModel(volunteerService, recruitmentService, volunteerContextAdapter, culturalFeats, recruitmentAlignment));
C:\Users\mikew\source\repos\TAOM\Main\SubModule.cs:370: campaignStarter.AddModel(new TaomAgeModel(raceAgeService));
C:\Users\mikew\source\repos\TAOM\Main\SubModule.cs:371: campaignStarter.AddModel(new TaomPregnancyModel(raceAgeService));
C:\Users\mikew\source\repos\TAOM\Main\SubModule.cs:372: campaignStarter.AddModel(new TaomHeroCreationModel());
C:\Users\mikew\source\repos\TAOM\Main\SubModule.cs:379: campaignStarter.AddModel(new TaomAllianceModel(diplomacyService));
C:\Users\mikew\source\repos\TAOM\Main\SubModule.cs:380: campaignStarter.AddModel(new TaomKingdomDecisionPermissionModel(diplomacyService, wotrService));
C:\Users\mikew\source\repos\TAOM\Main\SubModule.cs:381: campaignStarter.AddModel(new TaomDiplomacyModel(wotrService));
C:\Users\mikew\source\repos\TAOM\Main\SubModule.cs:389: campaignStarter.AddModel(new TaomSiegeEventModel(IoC.Resolve<ISiegeEngineAvailabilityService>()));
C:\Users\mikew\source\repos\TAOM\Main\SubModule.cs:393: campaignStarter.AddModel(new TaomExecutionRelationModel(executionRelationService, playerContext));
C:\Users\mikew\source\repos\TAOM\Main\SubModule.cs:399: campaignStarter.AddModel(new TaomArmyManagementModel(culturalFeats));
C:\Users\mikew\source\repos\TAOM\Main\SubModule.cs:400: campaignStarter.AddModel(new TaomPartySpeedModel(culturalFeats, careerPassives));
C:\Users\mikew\source\repos\TAOM\Main\SubModule.cs:401: campaignStarter.AddModel(new TaomSettlementProsperityModel(culturalFeats));
C:\Users\mikew\source\repos\TAOM\Main\SubModule.cs:402: campaignStarter.AddModel(new TaomSettlementMilitiaModel(culturalFeats));
C:\Users\mikew\source\repos\TAOM\Main\SubModule.cs:403: campaignStarter.AddModel(new TaomBuildingConstructionModel(culturalFeats));
C:\Users\mikew\source\repos\TAOM\Main\SubModule.cs:404: campaignStarter.AddModel(new TaomVillageProductionModel(culturalFeats));
C:\Users\mikew\source\repos\TAOM\Main\SubModule.cs:405: campaignStarter.AddModel(new TaomCaravanModel(culturalFeats));
C:\Users\mikew\source\repos\TAOM\Main\SubModule.cs:406: campaignStarter.AddModel(new TaomBattleRewardModel(culturalFeats, careerPassives));
C:\Users\mikew\source\repos\TAOM\Main\SubModule.cs:407: campaignStarter.AddModel(new TaomTournamentModel(IoC.Resolve<TAOM.Features.Arena.ITournamentService>()));
C:\Users\mikew\source\repos\TAOM\Main\SubModule.cs:408: campaignStarter.AddModel(new TaomPartyTroopUpgradeModel(culturalFeats, careerPassives));
C:\Users\mikew\source\repos\TAOM\Main\SubModule.cs:409: campaignStarter.AddModel(new TaomPartySizeModel(culturalFeats, careerPassives));
C:\Users\mikew\source\repos\TAOM\Main\SubModule.cs:410: campaignStarter.AddModel(new TaomFoodConsumptionModel(culturalFeats));
C:\Users\mikew\source\repos\TAOM\Main\SubModule.cs:411: campaignStarter.AddModel(new TaomSettlementLoyaltyModel(culturalFeats, IoC.Resolve<IRevoltTuningConfigProvider>()));
C:\Users\mikew\source\repos\TAOM\Main\SubModule.cs:412: campaignStarter.AddModel(new TaomSettlementFoodModel(IoC.Resolve<ISettlementFoodService>(), IoC.Resolve<ISettlementFoodConfigProvider>()));
C:\Users\mikew\source\repos\TAOM\Main\SubModule.cs:413: campaignStarter.AddModel(new TaomBanditDensityModel(IoC.Resolve<IBanditScalingService>()));
C:\Users\mikew\source\repos\TAOM\Main\SubModule.cs:414: campaignStarter.AddModel(new TaomPartyMoraleModel(culturalFeats, careerPassives));
C:\Users\mikew\source\repos\TAOM\Main\SubModule.cs:415: campaignStarter.AddModel(new TaomSmithingModel(culturalFeats, careerPassives));
C:\Users\mikew\source\repos\TAOM\Main\SubModule.cs:416: campaignStarter.AddModel(new TaomClanFinanceModel(culturalFeats));
C:\Users\mikew\source\repos\TAOM\Main\SubModule.cs:417: campaignStarter.AddModel(new TaomRaidModel(culturalFeats, careerPassives));
C:\Users\mikew\source\repos\TAOM\Main\SubModule.cs:418: campaignStarter.AddModel(new TaomNotableSpawnModel(culturalFeats));
C:\Users\mikew\source\repos\TAOM\Main\SubModule.cs:423: campaignStarter.AddModel(new TaomMilitaryPowerModel(battleBalanceSettings, battleBalanceConfig));
C:\Users\mikew\source\repos\TAOM\Main\SubModule.cs:424: campaignStarter.AddModel(new TaomCombatSimulationModel(battleBalanceSettings));
C:\Users\mikew\source\repos\TAOM\Main\SubModule.cs:425: campaignStarter.AddModel(new TaomPartyHealingModel(battleBalanceSettings, battleBalanceConfig));
C:\Users\mikew\source\repos\TAOM\Main\SubModule.cs:427: campaignStarter.AddModel(new TaomInformationRestrictionModel(IoC.Resolve<IEncyclopediaSettingsProvider>()));
C:\Users\mikew\source\repos\TAOM\Main\SubModule.cs:430: campaignStarter.AddModel(new TaomTargetScoreModel(armyTargetingService));
C:\Users\mikew\source\repos\TAOM\Main\SubModule.cs:468: campaignStarter.AddModel(new TaomMapVisibilityModel(careerPassives));
C:\Users\mikew\source\repos\TAOM\Main\SubModule.cs:469: campaignStarter.AddModel(new TaomInventoryCapacityModel(careerPassives));
C:\Users\mikew\source\repos\TAOM\Main\SubModule.cs:472: campaignStarter.AddModel<AgentStatCalculateModel>(new TaomAgentStatCalculateModel(careerPassiveService, careerAgentStat, elephantAttackService, spiderAttackService));
C:\Users\mikew\source\repos\TAOM\Main\SubModule.cs:473: campaignStarter.AddModel<AgentApplyDamageModel>(new TaomAgentApplyDamageModel(careerAgentStat));
C:\Users\mikew\source\repos\TAOM\Main\SubModule.cs:474: campaignStarter.AddModel(new TaomClanTierModel(careerPassiveService));

 succeeded in 535ms:
C:\Users\mikew\source\repos\TAOM\Main\Features\TroopWeight\ITroopWeightService.cs:6: namespace TAOM.Features.TroopWeight;
C:\Users\mikew\source\repos\TAOM\Main\Features\TroopWeight\ITroopWeightService.cs:8: public interface ITroopWeightService
C:\Users\mikew\source\repos\TAOM\Main\Features\TroopWeight\ITroopWeightService.cs:10: float GetTroopWeight(string troopStringId);
C:\Users\mikew\source\repos\TAOM\Main\Features\TroopWeight\ITroopWeightService.cs:11: float GetTroopWeight(CharacterObject character);
C:\Users\mikew\source\repos\TAOM\Main\Features\TroopWeight\ITroopWeightXmlLoader.cs:3: namespace TAOM.Features.TroopWeight;
C:\Users\mikew\source\repos\TAOM\Main\Features\TroopWeight\ITroopWeightXmlLoader.cs:5: public interface ITroopWeightXmlLoader
C:\Users\mikew\source\repos\TAOM\Main\Features\TroopWeight\ITroopWeightXmlLoader.cs:7: Dictionary<string, float> GetTroopWeights();
C:\Users\mikew\source\repos\TAOM\Main\Features\TroopWeight\TroopShedPlanning.cs:1: namespace TAOM.Features.TroopWeight;
C:\Users\mikew\source\repos\TAOM\Main\Features\TroopWeight\TroopShedPlanning.cs:5: /// pure <see cref="ITroopWeightService.PlanShed"/> planner needs so it never touches a sealed
C:\Users\mikew\source\repos\TAOM\Main\Features\TroopWeight\TroopShedPlanning.cs:7: /// match <see cref="ITroopWeightService.CalculateWeightedMemberCount"/> / the patched
C:\Users\mikew\source\repos\TAOM\Main\Features\TroopWeight\TroopShedPlanning.cs:8: /// <c>NumberOfAllMembers</c> the party-size limit is compared against.
C:\Users\mikew\source\repos\TAOM\Main\Features\TroopWeight\TroopWeightIoC.cs:2: using TAOM.Features.TroopWeight.Hooks;
C:\Users\mikew\source\repos\TAOM\Main\Features\TroopWeight\TroopWeightIoC.cs:4: namespace TAOM.Features.TroopWeight;
C:\Users\mikew\source\repos\TAOM\Main\Features\TroopWeight\TroopWeightIoC.cs:6: public static class TroopWeightIoC
C:\Users\mikew\source\repos\TAOM\Main\Features\TroopWeight\TroopWeightIoC.cs:8: public static void RegisterTroopWeightFeature(IContainer container)
C:\Users\mikew\source\repos\TAOM\Main\Features\TroopWeight\TroopWeightIoC.cs:10: container.Register<ITroopWeightXmlLoader, TroopWeightXmlLoader>(Reuse.Singleton);
C:\Users\mikew\source\repos\TAOM\Main\Features\TroopWeight\TroopWeightIoC.cs:11: container.Register<ITroopWeightService, TroopWeightService>(Reuse.Singleton);
C:\Users\mikew\source\repos\TAOM\Main\Features\TroopWeight\TroopWeightIoC.cs:13: container.Register<IOnPartyBaseNumberOfAllMembers, PartyBaseNumberOfAllMembersHook>(Reuse.Singleton);
C:\Users\mikew\source\repos\TAOM\Main\Features\TroopWeight\TroopWeightIoC.cs:20: container.Register<TroopWeightDisplayHook>(Reuse.Singleton);
C:\Users\mikew\source\repos\TAOM\Main\Features\TroopWeight\TroopWeightIoC.cs:24: IOnPartyBaseNumberOfAllMembers allMembersHook,
C:\Users\mikew\source\repos\TAOM\Main\Features\TroopWeight\TroopWeightIoC.cs:29: TroopWeightDisplayHook displayHook)
C:\Users\mikew\source\repos\TAOM\Main\Features\TroopWeight\TroopWeightIoC.cs:31: PartyBase_NumberOfAllMembers_Patch.Initialize(allMembersHook);
C:\Users\mikew\source\repos\TAOM\Main\Features\TroopWeight\TroopWeightService.cs:9: namespace TAOM.Features.TroopWeight;
C:\Users\mikew\source\repos\TAOM\Main\Features\TroopWeight\TroopWeightService.cs:11: public class TroopWeightService : ITroopWeightService
C:\Users\mikew\source\repos\TAOM\Main\Features\TroopWeight\TroopWeightService.cs:14: private readonly ITroopWeightXmlLoader _xmlLoader;
C:\Users\mikew\source\repos\TAOM\Main\Features\TroopWeight\TroopWeightService.cs:30: public TroopWeightService(IModLogger logger, ITroopWeightXmlLoader xmlLoader)
C:\Users\mikew\source\repos\TAOM\Main\Features\TroopWeight\TroopWeightService.cs:34: _weights = xmlLoader.GetTroopWeights();
C:\Users\mikew\source\repos\TAOM\Main\Features\TroopWeight\TroopWeightService.cs:35: _logger.LogInfo($"[TroopWeight] Service initialized with {_weights.Count} weighted troop definitions");
C:\Users\mikew\source\repos\TAOM\Main\Features\TroopWeight\TroopWeightService.cs:38: public float GetTroopWeight(string troopStringId)
C:\Users\mikew\source\repos\TAOM\Main\Features\TroopWeight\TroopWeightService.cs:46: public float GetTroopWeight(CharacterObject character)
C:\Users\mikew\source\repos\TAOM\Main\Features\TroopWeight\TroopWeightService.cs:48: return GetTroopWeight(character?.StringId);
C:\Users\mikew\source\repos\TAOM\Main\Features\TroopWeight\TroopWeightService.cs:77: _logger.LogWarning($"[TroopWeight] Roster iteration failed (count={roster?.Count}): {ex.GetType().Name}: {ex.Message}");
C:\Users\mikew\source\repos\TAOM\Main\Features\TroopWeight\TroopWeightService.cs:87: var weight = GetTroopWeight(element.Character);
C:\Users\mikew\source\repos\TAOM\Main\Features\TroopWeight\TroopWeightService.cs:119: var (h, w) = WeightedContribution(GetTroopWeight(e.TroopId), e.Number, e.WoundedNumber);
C:\Users\mikew\source\repos\TAOM\Main\Features\TroopWeight\TroopWeightService.cs:147: var (h, w) = WeightedContribution(GetTroopWeight(element.Character), element.Number, element.WoundedNumber);
C:\Users\mikew\source\repos\TAOM\Main\Features\TroopWeight\TroopWeightService.cs:159: _logger.LogWarning($"[TroopWeight] GetWeightedHealthAndWounded failed (count={party?.MemberRoster?.Count}): {ex.GetType().Name}: {ex.Message}");
C:\Users\mikew\source\repos\TAOM\Main\Features\TroopWeight\TroopWeightService.cs:171: // NumberOfAllMembers the size limit is checked against (Number * weight, incl. wounded).
C:\Users\mikew\source\repos\TAOM\Main\Features\TroopWeight\TroopWeightService.cs:217: _weights = _xmlLoader.GetTroopWeights();
C:\Users\mikew\source\repos\TAOM\Main\Features\TroopWeight\TroopWeightXmlLoader.cs:9: namespace TAOM.Features.TroopWeight;
C:\Users\mikew\source\repos\TAOM\Main\Features\TroopWeight\TroopWeightXmlLoader.cs:11: public class TroopWeightXmlLoader : ITroopWeightXmlLoader
C:\Users\mikew\source\repos\TAOM\Main\Features\TroopWeight\TroopWeightXmlLoader.cs:15: private Dictionary<string, float> _troopWeights;
C:\Users\mikew\source\repos\TAOM\Main\Features\TroopWeight\TroopWeightXmlLoader.cs:18: public TroopWeightXmlLoader(IPathService pathService, IModLogger logger)
C:\Users\mikew\source\repos\TAOM\Main\Features\TroopWeight\TroopWeightXmlLoader.cs:24: public Dictionary<string, float> GetTroopWeights()
C:\Users\mikew\source\repos\TAOM\Main\Features\TroopWeight\TroopWeightXmlLoader.cs:27: LoadTroopWeights();
C:\Users\mikew\source\repos\TAOM\Main\Features\TroopWeight\TroopWeightXmlLoader.cs:29: return _troopWeights;
C:\Users\mikew\source\repos\TAOM\Main\Features\TroopWeight\TroopWeightXmlLoader.cs:35: LoadTroopWeights();
C:\Users\mikew\source\repos\TAOM\Main\Features\TroopWeight\TroopWeightXmlLoader.cs:38: private void LoadTroopWeights()
C:\Users\mikew\source\repos\TAOM\Main\Features\TroopWeight\TroopWeightXmlLoader.cs:40: var xmlPath = Path.Combine(_pathService.ModuleDataPath, "TroopWeights", "troop_weights.xml");
C:\Users\mikew\source\repos\TAOM\Main\Features\TroopWeight\TroopWeightXmlLoader.cs:46: _troopWeights = weights;
C:\Users\mikew\source\repos\TAOM\Main\Features\TroopWeight\TroopWeightXmlLoader.cs:56: var weightNodes = doc.SelectNodes("//TroopWeight");
C:\Users\mikew\source\repos\TAOM\Main\Features\TroopWeight\TroopWeightXmlLoader.cs:59: _troopWeights = weights;
C:\Users\mikew\source\repos\TAOM\Main\Features\TroopWeight\TroopWeightXmlLoader.cs:71: _logger.LogWarning("TroopWeight element missing required 'id' attribute — skipping");
C:\Users\mikew\source\repos\TAOM\Main\Features\TroopWeight\TroopWeightXmlLoader.cs:100: _troopWeights = weights;
C:\Users\mikew\source\repos\TAOM\Main\Features\TaomSettings.cs:29: public bool EnableTroopWeight { get; set; } = true;

codex
The initial source skim shows the model is implemented as `base + delta`, with all exposed constants overridden. Next I’m going deeper on the exact arithmetic, config validation, and the TroopWeight patch target so I can confirm whether the deltas are actually deltas under all toggles.
exec
"C:\\Program Files\\PowerShell\\7\\pwsh.exe" -Command '$i=0; Get-Content '"'Main/Features/SettlementFood/Models/TaomSettlementFoodModel.cs' | ForEach-Object { "'$i++; '"'{0}:{1}: {2}' -f (Resolve-Path 'Main/Features/SettlementFood/Models/TaomSettlementFoodModel.cs'),"'$i,$_ }' in C:\Users\mikew\source\repos\TAOM
exec
"C:\\Program Files\\PowerShell\\7\\pwsh.exe" -Command '$i=0; Get-Content '"'Main/Features/SettlementFood/SettlementFoodService.cs' | ForEach-Object { "'$i++; '"'{0}:{1}: {2}' -f (Resolve-Path 'Main/Features/SettlementFood/SettlementFoodService.cs'),"'$i,$_ }' in C:\Users\mikew\source\repos\TAOM
exec
"C:\\Program Files\\PowerShell\\7\\pwsh.exe" -Command '$i=0; Get-Content '"'Main/Features/SettlementFood/TownFoodSnapshot.cs' | ForEach-Object { "'$i++; '"'{0}:{1}: {2}' -f (Resolve-Path 'Main/Features/SettlementFood/TownFoodSnapshot.cs'),"'$i,$_ }' in C:\Users\mikew\source\repos\TAOM
exec
"C:\\Program Files\\PowerShell\\7\\pwsh.exe" -Command '$i=0; Get-Content '"'Main/Features/SettlementFood/SettlementFoodConfigProvider.cs' | ForEach-Object { "'$i++; '"'{0}:{1}: {2}' -f (Resolve-Path 'Main/Features/SettlementFood/SettlementFoodConfigProvider.cs'),"'$i,$_ }' in C:\Users\mikew\source\repos\TAOM
 succeeded in 4984ms:
C:\Users\mikew\source\repos\TAOM\Main\Features\SettlementFood\Models\TaomSettlementFoodModel.cs:1: using TaleWorlds.CampaignSystem;
C:\Users\mikew\source\repos\TAOM\Main\Features\SettlementFood\Models\TaomSettlementFoodModel.cs:2: using TaleWorlds.CampaignSystem.GameComponents;
C:\Users\mikew\source\repos\TAOM\Main\Features\SettlementFood\Models\TaomSettlementFoodModel.cs:3: using TaleWorlds.CampaignSystem.Settlements;
C:\Users\mikew\source\repos\TAOM\Main\Features\SettlementFood\Models\TaomSettlementFoodModel.cs:4: using TAOM.Features.SettlementFood;
C:\Users\mikew\source\repos\TAOM\Main\Features\SettlementFood\Models\TaomSettlementFoodModel.cs:5: 
C:\Users\mikew\source\repos\TAOM\Main\Features\SettlementFood\Models\TaomSettlementFoodModel.cs:6: namespace TAOM.Features.SettlementFood.Models;
C:\Users\mikew\source\repos\TAOM\Main\Features\SettlementFood\Models\TaomSettlementFoodModel.cs:7: 
C:\Users\mikew\source\repos\TAOM\Main\Features\SettlementFood\Models\TaomSettlementFoodModel.cs:8: /// <summary>
C:\Users\mikew\source\repos\TAOM\Main\Features\SettlementFood\Models\TaomSettlementFoodModel.cs:9: /// Tunable settlement-food override. Two jobs (see docs/features/settlement-food.md):
C:\Users\mikew\source\repos\TAOM\Main\Features\SettlementFood\Models\TaomSettlementFoodModel.cs:10: ///
C:\Users\mikew\source\repos\TAOM\Main\Features\SettlementFood\Models\TaomSettlementFoodModel.cs:11: /// <list type="number">
C:\Users\mikew\source\repos\TAOM\Main\Features\SettlementFood\Models\TaomSettlementFoodModel.cs:12: /// <item>Fix the Troop-Weight leak: vanilla reads <c>GarrisonParty.Party.NumberOfAllMembers</c> (which
C:\Users\mikew\source\repos\TAOM\Main\Features\SettlementFood\Models\TaomSettlementFoodModel.cs:13: /// Patch17 inflates to the weighted count) for the garrison food term, so elite garrisons ate 2–3×
C:\Users\mikew\source\repos\TAOM\Main\Features\SettlementFood\Models\TaomSettlementFoodModel.cs:14: /// intended. The service adds back the over-count so the garrison term uses the RAW body count.</item>
C:\Users\mikew\source\repos\TAOM\Main\Features\SettlementFood\Models\TaomSettlementFoodModel.cs:15: /// <item>Expose vanilla's hardcoded constants (consumption divisors, base/village/flat production,
C:\Users\mikew\source\repos\TAOM\Main\Features\SettlementFood\Models\TaomSettlementFoodModel.cs:16: /// storage caps) as MCM/JSON knobs so the high-prosperity food squeeze can be dialled out.</item>
C:\Users\mikew\source\repos\TAOM\Main\Features\SettlementFood\Models\TaomSettlementFoodModel.cs:17: /// </list>
C:\Users\mikew\source\repos\TAOM\Main\Features\SettlementFood\Models\TaomSettlementFoodModel.cs:18: ///
C:\Users\mikew\source\repos\TAOM\Main\Features\SettlementFood\Models\TaomSettlementFoodModel.cs:19: /// Thin per ADR-002 / gamemodels.md: the constant properties are single expressions; the calculation
C:\Users\mikew\source\repos\TAOM\Main\Features\SettlementFood\Models\TaomSettlementFoodModel.cs:20: /// override does boundary conversion (<see cref="TownFoodSnapshot.FromTown"/>) then delegates the math
C:\Users\mikew\source\repos\TAOM\Main\Features\SettlementFood\Models\TaomSettlementFoodModel.cs:21: /// to <see cref="ISettlementFoodService"/>. Master toggle off ⇒ vanilla constants + zero delta.
C:\Users\mikew\source\repos\TAOM\Main\Features\SettlementFood\Models\TaomSettlementFoodModel.cs:22: /// </summary>
C:\Users\mikew\source\repos\TAOM\Main\Features\SettlementFood\Models\TaomSettlementFoodModel.cs:23: public class TaomSettlementFoodModel : DefaultSettlementFoodModel
C:\Users\mikew\source\repos\TAOM\Main\Features\SettlementFood\Models\TaomSettlementFoodModel.cs:24: {
C:\Users\mikew\source\repos\TAOM\Main\Features\SettlementFood\Models\TaomSettlementFoodModel.cs:25:     private readonly ISettlementFoodService _service;
C:\Users\mikew\source\repos\TAOM\Main\Features\SettlementFood\Models\TaomSettlementFoodModel.cs:26:     private readonly SettlementFoodConfig _config;
C:\Users\mikew\source\repos\TAOM\Main\Features\SettlementFood\Models\TaomSettlementFoodModel.cs:27: 
C:\Users\mikew\source\repos\TAOM\Main\Features\SettlementFood\Models\TaomSettlementFoodModel.cs:28:     public TaomSettlementFoodModel(ISettlementFoodService service, ISettlementFoodConfigProvider configProvider)
C:\Users\mikew\source\repos\TAOM\Main\Features\SettlementFood\Models\TaomSettlementFoodModel.cs:29:     {
C:\Users\mikew\source\repos\TAOM\Main\Features\SettlementFood\Models\TaomSettlementFoodModel.cs:30:         _service = service;
C:\Users\mikew\source\repos\TAOM\Main\Features\SettlementFood\Models\TaomSettlementFoodModel.cs:31:         _config = configProvider.GetConfig();
C:\Users\mikew\source\repos\TAOM\Main\Features\SettlementFood\Models\TaomSettlementFoodModel.cs:32:     }
C:\Users\mikew\source\repos\TAOM\Main\Features\SettlementFood\Models\TaomSettlementFoodModel.cs:33: 
C:\Users\mikew\source\repos\TAOM\Main\Features\SettlementFood\Models\TaomSettlementFoodModel.cs:34:     private bool Enabled => TaomSettings.Instance?.EnableSettlementFoodTuning ?? true;
C:\Users\mikew\source\repos\TAOM\Main\Features\SettlementFood\Models\TaomSettlementFoodModel.cs:35: 
C:\Users\mikew\source\repos\TAOM\Main\Features\SettlementFood\Models\TaomSettlementFoodModel.cs:36:     public override int NumberOfMenOnGarrisonToEatOneFood => Enabled ? _config.GarrisonFoodDivisor : 20;
C:\Users\mikew\source\repos\TAOM\Main\Features\SettlementFood\Models\TaomSettlementFoodModel.cs:37: 
C:\Users\mikew\source\repos\TAOM\Main\Features\SettlementFood\Models\TaomSettlementFoodModel.cs:38:     public override int NumberOfProsperityToEatOneFood => Enabled ? _config.ProsperityFoodDivisor : 40;
C:\Users\mikew\source\repos\TAOM\Main\Features\SettlementFood\Models\TaomSettlementFoodModel.cs:39: 
C:\Users\mikew\source\repos\TAOM\Main\Features\SettlementFood\Models\TaomSettlementFoodModel.cs:40:     public override int FoodStocksUpperLimit => Enabled ? _config.FoodStocksUpperLimit : 300;
C:\Users\mikew\source\repos\TAOM\Main\Features\SettlementFood\Models\TaomSettlementFoodModel.cs:41: 
C:\Users\mikew\source\repos\TAOM\Main\Features\SettlementFood\Models\TaomSettlementFoodModel.cs:42:     public override int CastleFoodStockUpperLimitBonus => Enabled ? _config.CastleFoodStockUpperLimitBonus : 150;
C:\Users\mikew\source\repos\TAOM\Main\Features\SettlementFood\Models\TaomSettlementFoodModel.cs:43: 
C:\Users\mikew\source\repos\TAOM\Main\Features\SettlementFood\Models\TaomSettlementFoodModel.cs:44:     public override ExplainedNumber CalculateTownFoodStocksChange(Town town, bool includeMarketStocks = true, bool includeDescriptions = false)
C:\Users\mikew\source\repos\TAOM\Main\Features\SettlementFood\Models\TaomSettlementFoodModel.cs:45:     {
C:\Users\mikew\source\repos\TAOM\Main\Features\SettlementFood\Models\TaomSettlementFoodModel.cs:46:         var result = base.CalculateTownFoodStocksChange(town, includeMarketStocks, includeDescriptions);
C:\Users\mikew\source\repos\TAOM\Main\Features\SettlementFood\Models\TaomSettlementFoodModel.cs:47:         _service.ApplyFoodAdjustment(TownFoodSnapshot.FromTown(town), _config, Enabled, ref result, includeDescriptions);
C:\Users\mikew\source\repos\TAOM\Main\Features\SettlementFood\Models\TaomSettlementFoodModel.cs:48:         return result;
C:\Users\mikew\source\repos\TAOM\Main\Features\SettlementFood\Models\TaomSettlementFoodModel.cs:49:     }
C:\Users\mikew\source\repos\TAOM\Main\Features\SettlementFood\Models\TaomSettlementFoodModel.cs:50: }

 succeeded in 4959ms:
C:\Users\mikew\source\repos\TAOM\Main\Features\SettlementFood\SettlementFoodService.cs:1: using TaleWorlds.CampaignSystem;
C:\Users\mikew\source\repos\TAOM\Main\Features\SettlementFood\SettlementFoodService.cs:2: using TaleWorlds.Localization;
C:\Users\mikew\source\repos\TAOM\Main\Features\SettlementFood\SettlementFoodService.cs:3: 
C:\Users\mikew\source\repos\TAOM\Main\Features\SettlementFood\SettlementFoodService.cs:4: namespace TAOM.Features.SettlementFood;
C:\Users\mikew\source\repos\TAOM\Main\Features\SettlementFood\SettlementFoodService.cs:5: 
C:\Users\mikew\source\repos\TAOM\Main\Features\SettlementFood\SettlementFoodService.cs:6: public class SettlementFoodService : ISettlementFoodService
C:\Users\mikew\source\repos\TAOM\Main\Features\SettlementFood\SettlementFoodService.cs:7: {
C:\Users\mikew\source\repos\TAOM\Main\Features\SettlementFood\SettlementFoodService.cs:8:     private static readonly TextObject AdjustmentText =
C:\Users\mikew\source\repos\TAOM\Main\Features\SettlementFood\SettlementFoodService.cs:9:         new TextObject("{=taom_settlement_food_adjustment}Settlement food (TAOM)");
C:\Users\mikew\source\repos\TAOM\Main\Features\SettlementFood\SettlementFoodService.cs:10: 
C:\Users\mikew\source\repos\TAOM\Main\Features\SettlementFood\SettlementFoodService.cs:11:     // Vanilla DefaultSettlementFoodModel constants the knobs are deltas against.
C:\Users\mikew\source\repos\TAOM\Main\Features\SettlementFood\SettlementFoodService.cs:12:     private const float VanillaTownBaseFood = 15f;
C:\Users\mikew\source\repos\TAOM\Main\Features\SettlementFood\SettlementFoodService.cs:13:     private const float VanillaCastleBaseFood = 10f;
C:\Users\mikew\source\repos\TAOM\Main\Features\SettlementFood\SettlementFoodService.cs:14:     private const float VanillaVillageMultiplier = 6f;
C:\Users\mikew\source\repos\TAOM\Main\Features\SettlementFood\SettlementFoodService.cs:15: 
C:\Users\mikew\source\repos\TAOM\Main\Features\SettlementFood\SettlementFoodService.cs:16:     public float ComputeFoodDelta(TownFoodSnapshot snapshot, SettlementFoodConfig config, bool enabled)
C:\Users\mikew\source\repos\TAOM\Main\Features\SettlementFood\SettlementFoodService.cs:17:     {
C:\Users\mikew\source\repos\TAOM\Main\Features\SettlementFood\SettlementFoodService.cs:18:         if (!enabled || snapshot == null || config == null)
C:\Users\mikew\source\repos\TAOM\Main\Features\SettlementFood\SettlementFoodService.cs:19:             return 0f;
C:\Users\mikew\source\repos\TAOM\Main\Features\SettlementFood\SettlementFoodService.cs:20: 
C:\Users\mikew\source\repos\TAOM\Main\Features\SettlementFood\SettlementFoodService.cs:21:         float delta = 0f;
C:\Users\mikew\source\repos\TAOM\Main\Features\SettlementFood\SettlementFoodService.cs:22: 
C:\Users\mikew\source\repos\TAOM\Main\Features\SettlementFood\SettlementFoodService.cs:23:         // Garrison raw-count correction (always — the troop-weight inflation is a bug regardless of
C:\Users\mikew\source\repos\TAOM\Main\Features\SettlementFood\SettlementFoodService.cs:24:         // siege). Base subtracted weighted/divisor; we want raw/divisor, so add back the over-count.
C:\Users\mikew\source\repos\TAOM\Main\Features\SettlementFood\SettlementFoodService.cs:25:         // Uses the SAME divisor the model's NumberOfMenOnGarrisonToEatOneFood override fed to base.
C:\Users\mikew\source\repos\TAOM\Main\Features\SettlementFood\SettlementFoodService.cs:26:         int garrisonDivisor = config.GarrisonFoodDivisor > 0 ? config.GarrisonFoodDivisor : 20;
C:\Users\mikew\source\repos\TAOM\Main\Features\SettlementFood\SettlementFoodService.cs:27:         int overCount = snapshot.WeightedGarrisonCount - snapshot.RawGarrisonCount;
C:\Users\mikew\source\repos\TAOM\Main\Features\SettlementFood\SettlementFoodService.cs:28:         if (overCount > 0)
C:\Users\mikew\source\repos\TAOM\Main\Features\SettlementFood\SettlementFoodService.cs:29:             delta += overCount / (float)garrisonDivisor;
C:\Users\mikew\source\repos\TAOM\Main\Features\SettlementFood\SettlementFoodService.cs:30: 
C:\Users\mikew\source\repos\TAOM\Main\Features\SettlementFood\SettlementFoodService.cs:31:         // Production knobs are siege-gated: vanilla zeroes all village/lands production under siege,
C:\Users\mikew\source\repos\TAOM\Main\Features\SettlementFood\SettlementFoodService.cs:32:         // and we must not undermine the siege-starvation mechanic.
C:\Users\mikew\source\repos\TAOM\Main\Features\SettlementFood\SettlementFoodService.cs:33:         if (!snapshot.IsUnderSiege)
C:\Users\mikew\source\repos\TAOM\Main\Features\SettlementFood\SettlementFoodService.cs:34:         {
C:\Users\mikew\source\repos\TAOM\Main\Features\SettlementFood\SettlementFoodService.cs:35:             float vanillaBase = snapshot.IsTown ? VanillaTownBaseFood : VanillaCastleBaseFood;
C:\Users\mikew\source\repos\TAOM\Main\Features\SettlementFood\SettlementFoodService.cs:36:             float configBase = snapshot.IsTown ? config.TownBaseFood : config.CastleBaseFood;
C:\Users\mikew\source\repos\TAOM\Main\Features\SettlementFood\SettlementFoodService.cs:37:             delta += configBase - vanillaBase;
C:\Users\mikew\source\repos\TAOM\Main\Features\SettlementFood\SettlementFoodService.cs:38: 
C:\Users\mikew\source\repos\TAOM\Main\Features\SettlementFood\SettlementFoodService.cs:39:             float multiplierDelta = config.VillageFoodMultiplier - VanillaVillageMultiplier;
C:\Users\mikew\source\repos\TAOM\Main\Features\SettlementFood\SettlementFoodService.cs:40:             if (multiplierDelta != 0f && snapshot.NormalVillageHearthLevels != null)
C:\Users\mikew\source\repos\TAOM\Main\Features\SettlementFood\SettlementFoodService.cs:41:             {
C:\Users\mikew\source\repos\TAOM\Main\Features\SettlementFood\SettlementFoodService.cs:42:                 foreach (var hearthLevel in snapshot.NormalVillageHearthLevels)
C:\Users\mikew\source\repos\TAOM\Main\Features\SettlementFood\SettlementFoodService.cs:43:                     delta += (hearthLevel + 1) * multiplierDelta;
C:\Users\mikew\source\repos\TAOM\Main\Features\SettlementFood\SettlementFoodService.cs:44:             }
C:\Users\mikew\source\repos\TAOM\Main\Features\SettlementFood\SettlementFoodService.cs:45: 
C:\Users\mikew\source\repos\TAOM\Main\Features\SettlementFood\SettlementFoodService.cs:46:             delta += config.FlatFoodBonus;
C:\Users\mikew\source\repos\TAOM\Main\Features\SettlementFood\SettlementFoodService.cs:47:         }
C:\Users\mikew\source\repos\TAOM\Main\Features\SettlementFood\SettlementFoodService.cs:48: 
C:\Users\mikew\source\repos\TAOM\Main\Features\SettlementFood\SettlementFoodService.cs:49:         return delta;
C:\Users\mikew\source\repos\TAOM\Main\Features\SettlementFood\SettlementFoodService.cs:50:     }
C:\Users\mikew\source\repos\TAOM\Main\Features\SettlementFood\SettlementFoodService.cs:51: 
C:\Users\mikew\source\repos\TAOM\Main\Features\SettlementFood\SettlementFoodService.cs:52:     public void ApplyFoodAdjustment(
C:\Users\mikew\source\repos\TAOM\Main\Features\SettlementFood\SettlementFoodService.cs:53:         TownFoodSnapshot snapshot,
C:\Users\mikew\source\repos\TAOM\Main\Features\SettlementFood\SettlementFoodService.cs:54:         SettlementFoodConfig config,
C:\Users\mikew\source\repos\TAOM\Main\Features\SettlementFood\SettlementFoodService.cs:55:         bool enabled,
C:\Users\mikew\source\repos\TAOM\Main\Features\SettlementFood\SettlementFoodService.cs:56:         ref ExplainedNumber result,
C:\Users\mikew\source\repos\TAOM\Main\Features\SettlementFood\SettlementFoodService.cs:57:         bool includeDescriptions)
C:\Users\mikew\source\repos\TAOM\Main\Features\SettlementFood\SettlementFoodService.cs:58:     {
C:\Users\mikew\source\repos\TAOM\Main\Features\SettlementFood\SettlementFoodService.cs:59:         float delta = ComputeFoodDelta(snapshot, config, enabled);
C:\Users\mikew\source\repos\TAOM\Main\Features\SettlementFood\SettlementFoodService.cs:60:         if (delta == 0f)
C:\Users\mikew\source\repos\TAOM\Main\Features\SettlementFood\SettlementFoodService.cs:61:             return;
C:\Users\mikew\source\repos\TAOM\Main\Features\SettlementFood\SettlementFoodService.cs:62: 
C:\Users\mikew\source\repos\TAOM\Main\Features\SettlementFood\SettlementFoodService.cs:63:         result.Add(delta, includeDescriptions ? AdjustmentText : null);
C:\Users\mikew\source\repos\TAOM\Main\Features\SettlementFood\SettlementFoodService.cs:64:     }
C:\Users\mikew\source\repos\TAOM\Main\Features\SettlementFood\SettlementFoodService.cs:65: }

 succeeded in 4977ms:
C:\Users\mikew\source\repos\TAOM\Main\Features\SettlementFood\TownFoodSnapshot.cs:1: using System.Collections.Generic;
C:\Users\mikew\source\repos\TAOM\Main\Features\SettlementFood\TownFoodSnapshot.cs:2: using TaleWorlds.CampaignSystem.Settlements;
C:\Users\mikew\source\repos\TAOM\Main\Features\SettlementFood\TownFoodSnapshot.cs:3: 
C:\Users\mikew\source\repos\TAOM\Main\Features\SettlementFood\TownFoodSnapshot.cs:4: namespace TAOM.Features.SettlementFood;
C:\Users\mikew\source\repos\TAOM\Main\Features\SettlementFood\TownFoodSnapshot.cs:5: 
C:\Users\mikew\source\repos\TAOM\Main\Features\SettlementFood\TownFoodSnapshot.cs:6: /// <summary>
C:\Users\mikew\source\repos\TAOM\Main\Features\SettlementFood\TownFoodSnapshot.cs:7: /// Primitive snapshot of a <see cref="Town"/>'s food-relevant state, built at the GameModel boundary
C:\Users\mikew\source\repos\TAOM\Main\Features\SettlementFood\TownFoodSnapshot.cs:8: /// so <see cref="SettlementFoodService"/> never touches sealed TaleWorlds types (ADR-007).
C:\Users\mikew\source\repos\TAOM\Main\Features\SettlementFood\TownFoodSnapshot.cs:9: ///
C:\Users\mikew\source\repos\TAOM\Main\Features\SettlementFood\TownFoodSnapshot.cs:10: /// <para><see cref="WeightedGarrisonCount"/> reads the (Troop-Weight-patched) <c>NumberOfAllMembers</c>
C:\Users\mikew\source\repos\TAOM\Main\Features\SettlementFood\TownFoodSnapshot.cs:11: /// getter; <see cref="RawGarrisonCount"/> reads the unpatched <c>MemberRoster.TotalManCount</c>. Vanilla
C:\Users\mikew\source\repos\TAOM\Main\Features\SettlementFood\TownFoodSnapshot.cs:12: /// <c>PartyBase.NumberOfAllMembers => MemberRoster.TotalManCount</c>, so their difference is exactly the
C:\Users\mikew\source\repos\TAOM\Main\Features\SettlementFood\TownFoodSnapshot.cs:13: /// weight inflation the food model must undo.</para>
C:\Users\mikew\source\repos\TAOM\Main\Features\SettlementFood\TownFoodSnapshot.cs:14: /// </summary>
C:\Users\mikew\source\repos\TAOM\Main\Features\SettlementFood\TownFoodSnapshot.cs:15: public sealed class TownFoodSnapshot
C:\Users\mikew\source\repos\TAOM\Main\Features\SettlementFood\TownFoodSnapshot.cs:16: {
C:\Users\mikew\source\repos\TAOM\Main\Features\SettlementFood\TownFoodSnapshot.cs:17:     public bool IsTown { get; }
C:\Users\mikew\source\repos\TAOM\Main\Features\SettlementFood\TownFoodSnapshot.cs:18:     public bool IsUnderSiege { get; }
C:\Users\mikew\source\repos\TAOM\Main\Features\SettlementFood\TownFoodSnapshot.cs:19:     public int RawGarrisonCount { get; }
C:\Users\mikew\source\repos\TAOM\Main\Features\SettlementFood\TownFoodSnapshot.cs:20:     public int WeightedGarrisonCount { get; }
C:\Users\mikew\source\repos\TAOM\Main\Features\SettlementFood\TownFoodSnapshot.cs:21: 
C:\Users\mikew\source\repos\TAOM\Main\Features\SettlementFood\TownFoodSnapshot.cs:22:     /// <summary>Hearth levels (0/1/2) of every bound village currently in the Normal state.</summary>
C:\Users\mikew\source\repos\TAOM\Main\Features\SettlementFood\TownFoodSnapshot.cs:23:     public IReadOnlyList<int> NormalVillageHearthLevels { get; }
C:\Users\mikew\source\repos\TAOM\Main\Features\SettlementFood\TownFoodSnapshot.cs:24: 
C:\Users\mikew\source\repos\TAOM\Main\Features\SettlementFood\TownFoodSnapshot.cs:25:     public TownFoodSnapshot(
C:\Users\mikew\source\repos\TAOM\Main\Features\SettlementFood\TownFoodSnapshot.cs:26:         bool isTown,
C:\Users\mikew\source\repos\TAOM\Main\Features\SettlementFood\TownFoodSnapshot.cs:27:         bool isUnderSiege,
C:\Users\mikew\source\repos\TAOM\Main\Features\SettlementFood\TownFoodSnapshot.cs:28:         int rawGarrisonCount,
C:\Users\mikew\source\repos\TAOM\Main\Features\SettlementFood\TownFoodSnapshot.cs:29:         int weightedGarrisonCount,
C:\Users\mikew\source\repos\TAOM\Main\Features\SettlementFood\TownFoodSnapshot.cs:30:         IReadOnlyList<int> normalVillageHearthLevels)
C:\Users\mikew\source\repos\TAOM\Main\Features\SettlementFood\TownFoodSnapshot.cs:31:     {
C:\Users\mikew\source\repos\TAOM\Main\Features\SettlementFood\TownFoodSnapshot.cs:32:         IsTown = isTown;
C:\Users\mikew\source\repos\TAOM\Main\Features\SettlementFood\TownFoodSnapshot.cs:33:         IsUnderSiege = isUnderSiege;
C:\Users\mikew\source\repos\TAOM\Main\Features\SettlementFood\TownFoodSnapshot.cs:34:         RawGarrisonCount = rawGarrisonCount;
C:\Users\mikew\source\repos\TAOM\Main\Features\SettlementFood\TownFoodSnapshot.cs:35:         WeightedGarrisonCount = weightedGarrisonCount;
C:\Users\mikew\source\repos\TAOM\Main\Features\SettlementFood\TownFoodSnapshot.cs:36:         NormalVillageHearthLevels = normalVillageHearthLevels ?? new List<int>();
C:\Users\mikew\source\repos\TAOM\Main\Features\SettlementFood\TownFoodSnapshot.cs:37:     }
C:\Users\mikew\source\repos\TAOM\Main\Features\SettlementFood\TownFoodSnapshot.cs:38: 
C:\Users\mikew\source\repos\TAOM\Main\Features\SettlementFood\TownFoodSnapshot.cs:39:     /// <summary>
C:\Users\mikew\source\repos\TAOM\Main\Features\SettlementFood\TownFoodSnapshot.cs:40:     /// Boundary factory — converts a sealed <see cref="Town"/> into a primitive snapshot. Uses <c>?.</c>
C:\Users\mikew\source\repos\TAOM\Main\Features\SettlementFood\TownFoodSnapshot.cs:41:     /// throughout because TaleWorlds computed getters can dereference null internally (adapters.md).
C:\Users\mikew\source\repos\TAOM\Main\Features\SettlementFood\TownFoodSnapshot.cs:42:     /// </summary>
C:\Users\mikew\source\repos\TAOM\Main\Features\SettlementFood\TownFoodSnapshot.cs:43:     public static TownFoodSnapshot FromTown(Town town)
C:\Users\mikew\source\repos\TAOM\Main\Features\SettlementFood\TownFoodSnapshot.cs:44:     {
C:\Users\mikew\source\repos\TAOM\Main\Features\SettlementFood\TownFoodSnapshot.cs:45:         var garrison = town?.GarrisonParty;
C:\Users\mikew\source\repos\TAOM\Main\Features\SettlementFood\TownFoodSnapshot.cs:46:         int weighted = garrison?.Party.NumberOfAllMembers ?? 0;
C:\Users\mikew\source\repos\TAOM\Main\Features\SettlementFood\TownFoodSnapshot.cs:47:         int raw = garrison?.MemberRoster?.TotalManCount ?? 0;
C:\Users\mikew\source\repos\TAOM\Main\Features\SettlementFood\TownFoodSnapshot.cs:48: 
C:\Users\mikew\source\repos\TAOM\Main\Features\SettlementFood\TownFoodSnapshot.cs:49:         var hearthLevels = new List<int>();
C:\Users\mikew\source\repos\TAOM\Main\Features\SettlementFood\TownFoodSnapshot.cs:50:         var boundVillages = town?.Owner?.Settlement?.BoundVillages;
C:\Users\mikew\source\repos\TAOM\Main\Features\SettlementFood\TownFoodSnapshot.cs:51:         if (boundVillages != null)
C:\Users\mikew\source\repos\TAOM\Main\Features\SettlementFood\TownFoodSnapshot.cs:52:         {
C:\Users\mikew\source\repos\TAOM\Main\Features\SettlementFood\TownFoodSnapshot.cs:53:             foreach (var village in boundVillages)
C:\Users\mikew\source\repos\TAOM\Main\Features\SettlementFood\TownFoodSnapshot.cs:54:             {
C:\Users\mikew\source\repos\TAOM\Main\Features\SettlementFood\TownFoodSnapshot.cs:55:                 if (village != null && village.VillageState == Village.VillageStates.Normal)
C:\Users\mikew\source\repos\TAOM\Main\Features\SettlementFood\TownFoodSnapshot.cs:56:                     hearthLevels.Add(village.GetHearthLevel());
C:\Users\mikew\source\repos\TAOM\Main\Features\SettlementFood\TownFoodSnapshot.cs:57:             }
C:\Users\mikew\source\repos\TAOM\Main\Features\SettlementFood\TownFoodSnapshot.cs:58:         }
C:\Users\mikew\source\repos\TAOM\Main\Features\SettlementFood\TownFoodSnapshot.cs:59: 
C:\Users\mikew\source\repos\TAOM\Main\Features\SettlementFood\TownFoodSnapshot.cs:60:         return new TownFoodSnapshot(
C:\Users\mikew\source\repos\TAOM\Main\Features\SettlementFood\TownFoodSnapshot.cs:61:             isTown: town?.IsTown ?? false,
C:\Users\mikew\source\repos\TAOM\Main\Features\SettlementFood\TownFoodSnapshot.cs:62:             isUnderSiege: town?.IsUnderSiege ?? false,
C:\Users\mikew\source\repos\TAOM\Main\Features\SettlementFood\TownFoodSnapshot.cs:63:             rawGarrisonCount: raw,
C:\Users\mikew\source\repos\TAOM\Main\Features\SettlementFood\TownFoodSnapshot.cs:64:             weightedGarrisonCount: weighted,
C:\Users\mikew\source\repos\TAOM\Main\Features\SettlementFood\TownFoodSnapshot.cs:65:             normalVillageHearthLevels: hearthLevels);
C:\Users\mikew\source\repos\TAOM\Main\Features\SettlementFood\TownFoodSnapshot.cs:66:     }
C:\Users\mikew\source\repos\TAOM\Main\Features\SettlementFood\TownFoodSnapshot.cs:67: }

 succeeded in 4986ms:
C:\Users\mikew\source\repos\TAOM\Main\Features\SettlementFood\SettlementFoodConfigProvider.cs:1: using System;
C:\Users\mikew\source\repos\TAOM\Main\Features\SettlementFood\SettlementFoodConfigProvider.cs:2: using System.IO;
C:\Users\mikew\source\repos\TAOM\Main\Features\SettlementFood\SettlementFoodConfigProvider.cs:3: using Newtonsoft.Json;
C:\Users\mikew\source\repos\TAOM\Main\Features\SettlementFood\SettlementFoodConfigProvider.cs:4: using TAOM.Core.Infrastructure;
C:\Users\mikew\source\repos\TAOM\Main\Features\SettlementFood\SettlementFoodConfigProvider.cs:5: using TAOM.Core.Logging;
C:\Users\mikew\source\repos\TAOM\Main\Features\SettlementFood\SettlementFoodConfigProvider.cs:6: using TAOM.Core.Validation;
C:\Users\mikew\source\repos\TAOM\Main\Features\SettlementFood\SettlementFoodConfigProvider.cs:7: 
C:\Users\mikew\source\repos\TAOM\Main\Features\SettlementFood\SettlementFoodConfigProvider.cs:8: namespace TAOM.Features.SettlementFood;
C:\Users\mikew\source\repos\TAOM\Main\Features\SettlementFood\SettlementFoodConfigProvider.cs:9: 
C:\Users\mikew\source\repos\TAOM\Main\Features\SettlementFood\SettlementFoodConfigProvider.cs:10: public class SettlementFoodConfigProvider : ISettlementFoodConfigProvider
C:\Users\mikew\source\repos\TAOM\Main\Features\SettlementFood\SettlementFoodConfigProvider.cs:11: {
C:\Users\mikew\source\repos\TAOM\Main\Features\SettlementFood\SettlementFoodConfigProvider.cs:12:     private readonly IPathService _pathService;
C:\Users\mikew\source\repos\TAOM\Main\Features\SettlementFood\SettlementFoodConfigProvider.cs:13:     private readonly IModLogger _logger;
C:\Users\mikew\source\repos\TAOM\Main\Features\SettlementFood\SettlementFoodConfigProvider.cs:14:     private readonly Lazy<SettlementFoodConfig> _config;
C:\Users\mikew\source\repos\TAOM\Main\Features\SettlementFood\SettlementFoodConfigProvider.cs:15: 
C:\Users\mikew\source\repos\TAOM\Main\Features\SettlementFood\SettlementFoodConfigProvider.cs:16:     public SettlementFoodConfigProvider(IPathService pathService, IModLogger logger)
C:\Users\mikew\source\repos\TAOM\Main\Features\SettlementFood\SettlementFoodConfigProvider.cs:17:     {
C:\Users\mikew\source\repos\TAOM\Main\Features\SettlementFood\SettlementFoodConfigProvider.cs:18:         _pathService = pathService;
C:\Users\mikew\source\repos\TAOM\Main\Features\SettlementFood\SettlementFoodConfigProvider.cs:19:         _logger = logger;
C:\Users\mikew\source\repos\TAOM\Main\Features\SettlementFood\SettlementFoodConfigProvider.cs:20:         _config = new Lazy<SettlementFoodConfig>(LoadConfig);
C:\Users\mikew\source\repos\TAOM\Main\Features\SettlementFood\SettlementFoodConfigProvider.cs:21:     }
C:\Users\mikew\source\repos\TAOM\Main\Features\SettlementFood\SettlementFoodConfigProvider.cs:22: 
C:\Users\mikew\source\repos\TAOM\Main\Features\SettlementFood\SettlementFoodConfigProvider.cs:23:     public SettlementFoodConfig GetConfig() => _config.Value;
C:\Users\mikew\source\repos\TAOM\Main\Features\SettlementFood\SettlementFoodConfigProvider.cs:24: 
C:\Users\mikew\source\repos\TAOM\Main\Features\SettlementFood\SettlementFoodConfigProvider.cs:25:     private SettlementFoodConfig LoadConfig()
C:\Users\mikew\source\repos\TAOM\Main\Features\SettlementFood\SettlementFoodConfigProvider.cs:26:     {
C:\Users\mikew\source\repos\TAOM\Main\Features\SettlementFood\SettlementFoodConfigProvider.cs:27:         var path = Path.Combine(_pathService.ModuleDataPath, "settlement_food", "settlement_food_config.json");
C:\Users\mikew\source\repos\TAOM\Main\Features\SettlementFood\SettlementFoodConfigProvider.cs:28: 
C:\Users\mikew\source\repos\TAOM\Main\Features\SettlementFood\SettlementFoodConfigProvider.cs:29:         if (!File.Exists(path))
C:\Users\mikew\source\repos\TAOM\Main\Features\SettlementFood\SettlementFoodConfigProvider.cs:30:         {
C:\Users\mikew\source\repos\TAOM\Main\Features\SettlementFood\SettlementFoodConfigProvider.cs:31:             _logger.LogWarning($"SettlementFoodConfigProvider: settlement_food_config.json not found at {path}, using defaults");
C:\Users\mikew\source\repos\TAOM\Main\Features\SettlementFood\SettlementFoodConfigProvider.cs:32:             return new SettlementFoodConfig();
C:\Users\mikew\source\repos\TAOM\Main\Features\SettlementFood\SettlementFoodConfigProvider.cs:33:         }
C:\Users\mikew\source\repos\TAOM\Main\Features\SettlementFood\SettlementFoodConfigProvider.cs:34: 
C:\Users\mikew\source\repos\TAOM\Main\Features\SettlementFood\SettlementFoodConfigProvider.cs:35:         SettlementFoodConfig parsed;
C:\Users\mikew\source\repos\TAOM\Main\Features\SettlementFood\SettlementFoodConfigProvider.cs:36:         try
C:\Users\mikew\source\repos\TAOM\Main\Features\SettlementFood\SettlementFoodConfigProvider.cs:37:         {
C:\Users\mikew\source\repos\TAOM\Main\Features\SettlementFood\SettlementFoodConfigProvider.cs:38:             var json = File.ReadAllText(path);
C:\Users\mikew\source\repos\TAOM\Main\Features\SettlementFood\SettlementFoodConfigProvider.cs:39:             parsed = JsonConvert.DeserializeObject<SettlementFoodConfig>(json) ?? new SettlementFoodConfig();
C:\Users\mikew\source\repos\TAOM\Main\Features\SettlementFood\SettlementFoodConfigProvider.cs:40:         }
C:\Users\mikew\source\repos\TAOM\Main\Features\SettlementFood\SettlementFoodConfigProvider.cs:41:         catch (Exception ex)
C:\Users\mikew\source\repos\TAOM\Main\Features\SettlementFood\SettlementFoodConfigProvider.cs:42:         {
C:\Users\mikew\source\repos\TAOM\Main\Features\SettlementFood\SettlementFoodConfigProvider.cs:43:             _logger.LogError($"SettlementFoodConfigProvider: Failed to parse settlement_food_config.json: {ex.Message}");
C:\Users\mikew\source\repos\TAOM\Main\Features\SettlementFood\SettlementFoodConfigProvider.cs:44:             return new SettlementFoodConfig();
C:\Users\mikew\source\repos\TAOM\Main\Features\SettlementFood\SettlementFoodConfigProvider.cs:45:         }
C:\Users\mikew\source\repos\TAOM\Main\Features\SettlementFood\SettlementFoodConfigProvider.cs:46: 
C:\Users\mikew\source\repos\TAOM\Main\Features\SettlementFood\SettlementFoodConfigProvider.cs:47:         return Validate(parsed);
C:\Users\mikew\source\repos\TAOM\Main\Features\SettlementFood\SettlementFoodConfigProvider.cs:48:     }
C:\Users\mikew\source\repos\TAOM\Main\Features\SettlementFood\SettlementFoodConfigProvider.cs:49: 
C:\Users\mikew\source\repos\TAOM\Main\Features\SettlementFood\SettlementFoodConfigProvider.cs:50:     private SettlementFoodConfig Validate(SettlementFoodConfig parsed)
C:\Users\mikew\source\repos\TAOM\Main\Features\SettlementFood\SettlementFoodConfigProvider.cs:51:     {
C:\Users\mikew\source\repos\TAOM\Main\Features\SettlementFood\SettlementFoodConfigProvider.cs:52:         var sanitized = new SettlementFoodConfig
C:\Users\mikew\source\repos\TAOM\Main\Features\SettlementFood\SettlementFoodConfigProvider.cs:53:         {
C:\Users\mikew\source\repos\TAOM\Main\Features\SettlementFood\SettlementFoodConfigProvider.cs:54:             GarrisonFoodDivisor = parsed.GarrisonFoodDivisor,
C:\Users\mikew\source\repos\TAOM\Main\Features\SettlementFood\SettlementFoodConfigProvider.cs:55:             ProsperityFoodDivisor = parsed.ProsperityFoodDivisor,
C:\Users\mikew\source\repos\TAOM\Main\Features\SettlementFood\SettlementFoodConfigProvider.cs:56:             TownBaseFood = parsed.TownBaseFood,
C:\Users\mikew\source\repos\TAOM\Main\Features\SettlementFood\SettlementFoodConfigProvider.cs:57:             CastleBaseFood = parsed.CastleBaseFood,
C:\Users\mikew\source\repos\TAOM\Main\Features\SettlementFood\SettlementFoodConfigProvider.cs:58:             VillageFoodMultiplier = parsed.VillageFoodMultiplier,
C:\Users\mikew\source\repos\TAOM\Main\Features\SettlementFood\SettlementFoodConfigProvider.cs:59:             FlatFoodBonus = parsed.FlatFoodBonus,
C:\Users\mikew\source\repos\TAOM\Main\Features\SettlementFood\SettlementFoodConfigProvider.cs:60:             FoodStocksUpperLimit = parsed.FoodStocksUpperLimit,
C:\Users\mikew\source\repos\TAOM\Main\Features\SettlementFood\SettlementFoodConfigProvider.cs:61:             CastleFoodStockUpperLimitBonus = parsed.CastleFoodStockUpperLimitBonus,
C:\Users\mikew\source\repos\TAOM\Main\Features\SettlementFood\SettlementFoodConfigProvider.cs:62:         };
C:\Users\mikew\source\repos\TAOM\Main\Features\SettlementFood\SettlementFoodConfigProvider.cs:63: 
C:\Users\mikew\source\repos\TAOM\Main\Features\SettlementFood\SettlementFoodConfigProvider.cs:64:         var defaults = new SettlementFoodConfig();
C:\Users\mikew\source\repos\TAOM\Main\Features\SettlementFood\SettlementFoodConfigProvider.cs:65:         var rejected = false;
C:\Users\mikew\source\repos\TAOM\Main\Features\SettlementFood\SettlementFoodConfigProvider.cs:66: 
C:\Users\mikew\source\repos\TAOM\Main\Features\SettlementFood\SettlementFoodConfigProvider.cs:67:         // Divisors MUST be >= 1 — a 0 divisor poisons the vanilla food formula with Infinity.
C:\Users\mikew\source\repos\TAOM\Main\Features\SettlementFood\SettlementFoodConfigProvider.cs:68:         if (sanitized.GarrisonFoodDivisor < 1 || sanitized.GarrisonFoodDivisor > 10000)
C:\Users\mikew\source\repos\TAOM\Main\Features\SettlementFood\SettlementFoodConfigProvider.cs:69:         {
C:\Users\mikew\source\repos\TAOM\Main\Features\SettlementFood\SettlementFoodConfigProvider.cs:70:             _logger.LogWarning($"SettlementFoodConfigProvider: garrisonFoodDivisor={sanitized.GarrisonFoodDivisor} outside [1,10000], reverting to default {defaults.GarrisonFoodDivisor}");
C:\Users\mikew\source\repos\TAOM\Main\Features\SettlementFood\SettlementFoodConfigProvider.cs:71:             sanitized.GarrisonFoodDivisor = defaults.GarrisonFoodDivisor;
C:\Users\mikew\source\repos\TAOM\Main\Features\SettlementFood\SettlementFoodConfigProvider.cs:72:             rejected = true;
C:\Users\mikew\source\repos\TAOM\Main\Features\SettlementFood\SettlementFoodConfigProvider.cs:73:         }
C:\Users\mikew\source\repos\TAOM\Main\Features\SettlementFood\SettlementFoodConfigProvider.cs:74: 
C:\Users\mikew\source\repos\TAOM\Main\Features\SettlementFood\SettlementFoodConfigProvider.cs:75:         if (sanitized.ProsperityFoodDivisor < 1 || sanitized.ProsperityFoodDivisor > 10000)
C:\Users\mikew\source\repos\TAOM\Main\Features\SettlementFood\SettlementFoodConfigProvider.cs:76:         {
C:\Users\mikew\source\repos\TAOM\Main\Features\SettlementFood\SettlementFoodConfigProvider.cs:77:             _logger.LogWarning($"SettlementFoodConfigProvider: prosperityFoodDivisor={sanitized.ProsperityFoodDivisor} outside [1,10000], reverting to default {defaults.ProsperityFoodDivisor}");
C:\Users\mikew\source\repos\TAOM\Main\Features\SettlementFood\SettlementFoodConfigProvider.cs:78:             sanitized.ProsperityFoodDivisor = defaults.ProsperityFoodDivisor;
C:\Users\mikew\source\repos\TAOM\Main\Features\SettlementFood\SettlementFoodConfigProvider.cs:79:             rejected = true;
C:\Users\mikew\source\repos\TAOM\Main\Features\SettlementFood\SettlementFoodConfigProvider.cs:80:         }
C:\Users\mikew\source\repos\TAOM\Main\Features\SettlementFood\SettlementFoodConfigProvider.cs:81: 
C:\Users\mikew\source\repos\TAOM\Main\Features\SettlementFood\SettlementFoodConfigProvider.cs:82:         // Production knobs: finite, non-negative (a negative would worsen the deficit it exists to relieve).
C:\Users\mikew\source\repos\TAOM\Main\Features\SettlementFood\SettlementFoodConfigProvider.cs:83:         if (!FiniteFloatValidator.IsFiniteInRange(sanitized.TownBaseFood, 0f, 10000f))
C:\Users\mikew\source\repos\TAOM\Main\Features\SettlementFood\SettlementFoodConfigProvider.cs:84:         {
C:\Users\mikew\source\repos\TAOM\Main\Features\SettlementFood\SettlementFoodConfigProvider.cs:85:             _logger.LogWarning($"SettlementFoodConfigProvider: townBaseFood={sanitized.TownBaseFood} must be a finite value in [0,10000], reverting to default {defaults.TownBaseFood}");
C:\Users\mikew\source\repos\TAOM\Main\Features\SettlementFood\SettlementFoodConfigProvider.cs:86:             sanitized.TownBaseFood = defaults.TownBaseFood;
C:\Users\mikew\source\repos\TAOM\Main\Features\SettlementFood\SettlementFoodConfigProvider.cs:87:             rejected = true;
C:\Users\mikew\source\repos\TAOM\Main\Features\SettlementFood\SettlementFoodConfigProvider.cs:88:         }
C:\Users\mikew\source\repos\TAOM\Main\Features\SettlementFood\SettlementFoodConfigProvider.cs:89: 
C:\Users\mikew\source\repos\TAOM\Main\Features\SettlementFood\SettlementFoodConfigProvider.cs:90:         if (!FiniteFloatValidator.IsFiniteInRange(sanitized.CastleBaseFood, 0f, 10000f))
C:\Users\mikew\source\repos\TAOM\Main\Features\SettlementFood\SettlementFoodConfigProvider.cs:91:         {
C:\Users\mikew\source\repos\TAOM\Main\Features\SettlementFood\SettlementFoodConfigProvider.cs:92:             _logger.LogWarning($"SettlementFoodConfigProvider: castleBaseFood={sanitized.CastleBaseFood} must be a finite value in [0,10000], reverting to default {defaults.CastleBaseFood}");
C:\Users\mikew\source\repos\TAOM\Main\Features\SettlementFood\SettlementFoodConfigProvider.cs:93:             sanitized.CastleBaseFood = defaults.CastleBaseFood;
C:\Users\mikew\source\repos\TAOM\Main\Features\SettlementFood\SettlementFoodConfigProvider.cs:94:             rejected = true;
C:\Users\mikew\source\repos\TAOM\Main\Features\SettlementFood\SettlementFoodConfigProvider.cs:95:         }
C:\Users\mikew\source\repos\TAOM\Main\Features\SettlementFood\SettlementFoodConfigProvider.cs:96: 
C:\Users\mikew\source\repos\TAOM\Main\Features\SettlementFood\SettlementFoodConfigProvider.cs:97:         if (!FiniteFloatValidator.IsFiniteInRange(sanitized.VillageFoodMultiplier, 0f, 10000f))
C:\Users\mikew\source\repos\TAOM\Main\Features\SettlementFood\SettlementFoodConfigProvider.cs:98:         {
C:\Users\mikew\source\repos\TAOM\Main\Features\SettlementFood\SettlementFoodConfigProvider.cs:99:             _logger.LogWarning($"SettlementFoodConfigProvider: villageFoodMultiplier={sanitized.VillageFoodMultiplier} must be a finite value in [0,10000], reverting to default {defaults.VillageFoodMultiplier}");
C:\Users\mikew\source\repos\TAOM\Main\Features\SettlementFood\SettlementFoodConfigProvider.cs:100:             sanitized.VillageFoodMultiplier = defaults.VillageFoodMultiplier;
C:\Users\mikew\source\repos\TAOM\Main\Features\SettlementFood\SettlementFoodConfigProvider.cs:101:             rejected = true;
C:\Users\mikew\source\repos\TAOM\Main\Features\SettlementFood\SettlementFoodConfigProvider.cs:102:         }
C:\Users\mikew\source\repos\TAOM\Main\Features\SettlementFood\SettlementFoodConfigProvider.cs:103: 
C:\Users\mikew\source\repos\TAOM\Main\Features\SettlementFood\SettlementFoodConfigProvider.cs:104:         if (!FiniteFloatValidator.IsFiniteInRange(sanitized.FlatFoodBonus, 0f, 100000f))
C:\Users\mikew\source\repos\TAOM\Main\Features\SettlementFood\SettlementFoodConfigProvider.cs:105:         {
C:\Users\mikew\source\repos\TAOM\Main\Features\SettlementFood\SettlementFoodConfigProvider.cs:106:             _logger.LogWarning($"SettlementFoodConfigProvider: flatFoodBonus={sanitized.FlatFoodBonus} must be a finite value in [0,100000], reverting to default {defaults.FlatFoodBonus}");
C:\Users\mikew\source\repos\TAOM\Main\Features\SettlementFood\SettlementFoodConfigProvider.cs:107:             sanitized.FlatFoodBonus = defaults.FlatFoodBonus;
C:\Users\mikew\source\repos\TAOM\Main\Features\SettlementFood\SettlementFoodConfigProvider.cs:108:             rejected = true;
C:\Users\mikew\source\repos\TAOM\Main\Features\SettlementFood\SettlementFoodConfigProvider.cs:109:         }
C:\Users\mikew\source\repos\TAOM\Main\Features\SettlementFood\SettlementFoodConfigProvider.cs:110: 
C:\Users\mikew\source\repos\TAOM\Main\Features\SettlementFood\SettlementFoodConfigProvider.cs:111:         // Storage caps: town limit >= 1, castle bonus >= 0.
C:\Users\mikew\source\repos\TAOM\Main\Features\SettlementFood\SettlementFoodConfigProvider.cs:112:         if (sanitized.FoodStocksUpperLimit < 1 || sanitized.FoodStocksUpperLimit > 1000000)
C:\Users\mikew\source\repos\TAOM\Main\Features\SettlementFood\SettlementFoodConfigProvider.cs:113:         {
C:\Users\mikew\source\repos\TAOM\Main\Features\SettlementFood\SettlementFoodConfigProvider.cs:114:             _logger.LogWarning($"SettlementFoodConfigProvider: foodStocksUpperLimit={sanitized.FoodStocksUpperLimit} outside [1,1000000], reverting to default {defaults.FoodStocksUpperLimit}");
C:\Users\mikew\source\repos\TAOM\Main\Features\SettlementFood\SettlementFoodConfigProvider.cs:115:             sanitized.FoodStocksUpperLimit = defaults.FoodStocksUpperLimit;
C:\Users\mikew\source\repos\TAOM\Main\Features\SettlementFood\SettlementFoodConfigProvider.cs:116:             rejected = true;
C:\Users\mikew\source\repos\TAOM\Main\Features\SettlementFood\SettlementFoodConfigProvider.cs:117:         }
C:\Users\mikew\source\repos\TAOM\Main\Features\SettlementFood\SettlementFoodConfigProvider.cs:118: 
C:\Users\mikew\source\repos\TAOM\Main\Features\SettlementFood\SettlementFoodConfigProvider.cs:119:         if (sanitized.CastleFoodStockUpperLimitBonus < 0 || sanitized.CastleFoodStockUpperLimitBonus > 1000000)
C:\Users\mikew\source\repos\TAOM\Main\Features\SettlementFood\SettlementFoodConfigProvider.cs:120:         {
C:\Users\mikew\source\repos\TAOM\Main\Features\SettlementFood\SettlementFoodConfigProvider.cs:121:             _logger.LogWarning($"SettlementFoodConfigProvider: castleFoodStockUpperLimitBonus={sanitized.CastleFoodStockUpperLimitBonus} outside [0,1000000], reverting to default {defaults.CastleFoodStockUpperLimitBonus}");
C:\Users\mikew\source\repos\TAOM\Main\Features\SettlementFood\SettlementFoodConfigProvider.cs:122:             sanitized.CastleFoodStockUpperLimitBonus = defaults.CastleFoodStockUpperLimitBonus;
C:\Users\mikew\source\repos\TAOM\Main\Features\SettlementFood\SettlementFoodConfigProvider.cs:123:             rejected = true;
C:\Users\mikew\source\repos\TAOM\Main\Features\SettlementFood\SettlementFoodConfigProvider.cs:124:         }
C:\Users\mikew\source\repos\TAOM\Main\Features\SettlementFood\SettlementFoodConfigProvider.cs:125: 
C:\Users\mikew\source\repos\TAOM\Main\Features\SettlementFood\SettlementFoodConfigProvider.cs:126:         if (rejected)
C:\Users\mikew\source\repos\TAOM\Main\Features\SettlementFood\SettlementFoodConfigProvider.cs:127:             _logger.LogWarning("SettlementFoodConfigProvider: settlement_food_config.json contained invalid values. See prior warnings for details.");
C:\Users\mikew\source\repos\TAOM\Main\Features\SettlementFood\SettlementFoodConfigProvider.cs:128:         else
C:\Users\mikew\source\repos\TAOM\Main\Features\SettlementFood\SettlementFoodConfigProvider.cs:129:             _logger.LogInfo("SettlementFoodConfigProvider: Loaded settlement_food_config.json");
C:\Users\mikew\source\repos\TAOM\Main\Features\SettlementFood\SettlementFoodConfigProvider.cs:130: 
C:\Users\mikew\source\repos\TAOM\Main\Features\SettlementFood\SettlementFoodConfigProvider.cs:131:         return sanitized;
C:\Users\mikew\source\repos\TAOM\Main\Features\SettlementFood\SettlementFoodConfigProvider.cs:132:     }
C:\Users\mikew\source\repos\TAOM\Main\Features\SettlementFood\SettlementFoodConfigProvider.cs:133: }

codex
So far the service terms are syntactically deltas, and the disabled path returns vanilla constants plus zero service adjustment. I’m checking the remaining risk surfaces now: validation details, test coverage, and whether the TroopWeight patch really hooks the exact getter the food model/base reads.
exec
"C:\\Program Files\\PowerShell\\7\\pwsh.exe" -Command '$i=0; Get-Content '"'Main/Features/SettlementFood/SettlementFoodConfig.cs' | ForEach-Object { "'$i++; '"'{0}:{1}: {2}' -f (Resolve-Path 'Main/Features/SettlementFood/SettlementFoodConfig.cs'),"'$i,$_ }' in C:\Users\mikew\source\repos\TAOM
exec
"C:\\Program Files\\PowerShell\\7\\pwsh.exe" -Command '$i=0; Get-Content '"'TAOM.Tests/Features/SettlementFood/SettlementFoodServiceTests.cs' | ForEach-Object { "'$i++; '"'{0}:{1}: {2}' -f (Resolve-Path 'TAOM.Tests/Features/SettlementFood/SettlementFoodServiceTests.cs'),"'$i,$_ }' in C:\Users\mikew\source\repos\TAOM
exec
"C:\\Program Files\\PowerShell\\7\\pwsh.exe" -Command '$i=0; Get-Content '"'Main/_Module/ModuleData/settlement_food/settlement_food_config.json' | ForEach-Object { "'$i++; '"'{0}:{1}: {2}' -f (Resolve-Path 'Main/_Module/ModuleData/settlement_food/settlement_food_config.json'),"'$i,$_ }' in C:\Users\mikew\source\repos\TAOM
exec
"C:\\Program Files\\PowerShell\\7\\pwsh.exe" -Command '$i=0; Get-Content '"'TAOM.Tests/Features/SettlementFood/SettlementFoodConfigProviderTests.cs' | ForEach-Object { "'$i++; '"'{0}:{1}: {2}' -f (Resolve-Path 'TAOM.Tests/Features/SettlementFood/SettlementFoodConfigProviderTests.cs'),"'$i,$_ }' in C:\Users\mikew\source\repos\TAOM
 succeeded in 657ms:
C:\Users\mikew\source\repos\TAOM\Main\_Module\ModuleData\settlement_food\settlement_food_config.json:1: {
C:\Users\mikew\source\repos\TAOM\Main\_Module\ModuleData\settlement_food\settlement_food_config.json:2:   "garrisonFoodDivisor": 20,
C:\Users\mikew\source\repos\TAOM\Main\_Module\ModuleData\settlement_food\settlement_food_config.json:3:   "prosperityFoodDivisor": 40,
C:\Users\mikew\source\repos\TAOM\Main\_Module\ModuleData\settlement_food\settlement_food_config.json:4:   "townBaseFood": 15,
C:\Users\mikew\source\repos\TAOM\Main\_Module\ModuleData\settlement_food\settlement_food_config.json:5:   "castleBaseFood": 10,
C:\Users\mikew\source\repos\TAOM\Main\_Module\ModuleData\settlement_food\settlement_food_config.json:6:   "villageFoodMultiplier": 6,
C:\Users\mikew\source\repos\TAOM\Main\_Module\ModuleData\settlement_food\settlement_food_config.json:7:   "flatFoodBonus": 0,
C:\Users\mikew\source\repos\TAOM\Main\_Module\ModuleData\settlement_food\settlement_food_config.json:8:   "foodStocksUpperLimit": 300,
C:\Users\mikew\source\repos\TAOM\Main\_Module\ModuleData\settlement_food\settlement_food_config.json:9:   "castleFoodStockUpperLimitBonus": 150
C:\Users\mikew\source\repos\TAOM\Main\_Module\ModuleData\settlement_food\settlement_food_config.json:10: }

 succeeded in 697ms:
C:\Users\CodexSandboxOffline\.codex\.sandbox\cwd\742debdb14bb7258\Main\Features\SettlementFood\SettlementFoodConfig.cs:1: namespace TAOM.Features.SettlementFood;
C:\Users\CodexSandboxOffline\.codex\.sandbox\cwd\742debdb14bb7258\Main\Features\SettlementFood\SettlementFoodConfig.cs:2: 
C:\Users\CodexSandboxOffline\.codex\.sandbox\cwd\742debdb14bb7258\Main\Features\SettlementFood\SettlementFoodConfig.cs:3: /// <summary>
C:\Users\CodexSandboxOffline\.codex\.sandbox\cwd\742debdb14bb7258\Main\Features\SettlementFood\SettlementFoodConfig.cs:4: /// Tunable knobs for <see cref="Models.TaomSettlementFoodModel"/>. Every default equals the vanilla
C:\Users\CodexSandboxOffline\.codex\.sandbox\cwd\742debdb14bb7258\Main\Features\SettlementFood\SettlementFoodConfig.cs:5: /// <c>DefaultSettlementFoodModel</c> constant, so an unedited config is behaviourally vanilla on the
C:\Users\CodexSandboxOffline\.codex\.sandbox\cwd\742debdb14bb7258\Main\Features\SettlementFood\SettlementFoodConfig.cs:6: /// production/consumption-rate side — the only always-on change is the garrison raw-count correction
C:\Users\CodexSandboxOffline\.codex\.sandbox\cwd\742debdb14bb7258\Main\Features\SettlementFood\SettlementFoodConfig.cs:7: /// (which neutralises the Troop Weight feature inflating garrison food consumption).
C:\Users\CodexSandboxOffline\.codex\.sandbox\cwd\742debdb14bb7258\Main\Features\SettlementFood\SettlementFoodConfig.cs:8: ///
C:\Users\CodexSandboxOffline\.codex\.sandbox\cwd\742debdb14bb7258\Main\Features\SettlementFood\SettlementFoodConfig.cs:9: /// Divisors are "men/prosperity per 1 food eaten" — RAISING them makes garrisons/civilians cheaper
C:\Users\CodexSandboxOffline\.codex\.sandbox\cwd\742debdb14bb7258\Main\Features\SettlementFood\SettlementFoodConfig.cs:10: /// to feed (relieves the deficit). Production knobs ADD food. See
C:\Users\CodexSandboxOffline\.codex\.sandbox\cwd\742debdb14bb7258\Main\Features\SettlementFood\SettlementFoodConfig.cs:11: /// docs/reference/engine/settlement-economy-food-prosperity.md.
C:\Users\CodexSandboxOffline\.codex\.sandbox\cwd\742debdb14bb7258\Main\Features\SettlementFood\SettlementFoodConfig.cs:12: /// </summary>
C:\Users\CodexSandboxOffline\.codex\.sandbox\cwd\742debdb14bb7258\Main\Features\SettlementFood\SettlementFoodConfig.cs:13: public class SettlementFoodConfig
C:\Users\CodexSandboxOffline\.codex\.sandbox\cwd\742debdb14bb7258\Main\Features\SettlementFood\SettlementFoodConfig.cs:14: {
C:\Users\CodexSandboxOffline\.codex\.sandbox\cwd\742debdb14bb7258\Main\Features\SettlementFood\SettlementFoodConfig.cs:15:     // Consumption divisors (vanilla: garrison 20, prosperity 40). Higher = less food eaten.
C:\Users\CodexSandboxOffline\.codex\.sandbox\cwd\742debdb14bb7258\Main\Features\SettlementFood\SettlementFoodConfig.cs:16:     public int GarrisonFoodDivisor { get; set; } = 20;
C:\Users\CodexSandboxOffline\.codex\.sandbox\cwd\742debdb14bb7258\Main\Features\SettlementFood\SettlementFoodConfig.cs:17:     public int ProsperityFoodDivisor { get; set; } = 40;
C:\Users\CodexSandboxOffline\.codex\.sandbox\cwd\742debdb14bb7258\Main\Features\SettlementFood\SettlementFoodConfig.cs:18: 
C:\Users\CodexSandboxOffline\.codex\.sandbox\cwd\742debdb14bb7258\Main\Features\SettlementFood\SettlementFoodConfig.cs:19:     // Base "lands around settlement" production (vanilla: town 15, castle 10).
C:\Users\CodexSandboxOffline\.codex\.sandbox\cwd\742debdb14bb7258\Main\Features\SettlementFood\SettlementFoodConfig.cs:20:     public float TownBaseFood { get; set; } = 15f;
C:\Users\CodexSandboxOffline\.codex\.sandbox\cwd\742debdb14bb7258\Main\Features\SettlementFood\SettlementFoodConfig.cs:21:     public float CastleBaseFood { get; set; } = 10f;
C:\Users\CodexSandboxOffline\.codex\.sandbox\cwd\742debdb14bb7258\Main\Features\SettlementFood\SettlementFoodConfig.cs:22: 
C:\Users\CodexSandboxOffline\.codex\.sandbox\cwd\742debdb14bb7258\Main\Features\SettlementFood\SettlementFoodConfig.cs:23:     // Per Normal-state bound village: (hearthLevel + 1) * multiplier (vanilla multiplier: 6).
C:\Users\CodexSandboxOffline\.codex\.sandbox\cwd\742debdb14bb7258\Main\Features\SettlementFood\SettlementFoodConfig.cs:24:     public float VillageFoodMultiplier { get; set; } = 6f;
C:\Users\CodexSandboxOffline\.codex\.sandbox\cwd\742debdb14bb7258\Main\Features\SettlementFood\SettlementFoodConfig.cs:25: 
C:\Users\CodexSandboxOffline\.codex\.sandbox\cwd\742debdb14bb7258\Main\Features\SettlementFood\SettlementFoodConfig.cs:26:     // Flat daily food added to every fortification (vanilla: 0). Siege-gated like all production.
C:\Users\CodexSandboxOffline\.codex\.sandbox\cwd\742debdb14bb7258\Main\Features\SettlementFood\SettlementFoodConfig.cs:27:     public float FlatFoodBonus { get; set; } = 0f;
C:\Users\CodexSandboxOffline\.codex\.sandbox\cwd\742debdb14bb7258\Main\Features\SettlementFood\SettlementFoodConfig.cs:28: 
C:\Users\CodexSandboxOffline\.codex\.sandbox\cwd\742debdb14bb7258\Main\Features\SettlementFood\SettlementFoodConfig.cs:29:     // Storage caps (vanilla: town limit 300, castle +150 bonus).
C:\Users\CodexSandboxOffline\.codex\.sandbox\cwd\742debdb14bb7258\Main\Features\SettlementFood\SettlementFoodConfig.cs:30:     public int FoodStocksUpperLimit { get; set; } = 300;
C:\Users\CodexSandboxOffline\.codex\.sandbox\cwd\742debdb14bb7258\Main\Features\SettlementFood\SettlementFoodConfig.cs:31:     public int CastleFoodStockUpperLimitBonus { get; set; } = 150;
C:\Users\CodexSandboxOffline\.codex\.sandbox\cwd\742debdb14bb7258\Main\Features\SettlementFood\SettlementFoodConfig.cs:32: }

 succeeded in 699ms:
C:\Users\mikew\source\repos\TAOM\TAOM.Tests\Features\SettlementFood\SettlementFoodServiceTests.cs:1: using System.Collections.Generic;
C:\Users\mikew\source\repos\TAOM\TAOM.Tests\Features\SettlementFood\SettlementFoodServiceTests.cs:2: using Microsoft.VisualStudio.TestTools.UnitTesting;
C:\Users\mikew\source\repos\TAOM\TAOM.Tests\Features\SettlementFood\SettlementFoodServiceTests.cs:3: using TaleWorlds.CampaignSystem;
C:\Users\mikew\source\repos\TAOM\TAOM.Tests\Features\SettlementFood\SettlementFoodServiceTests.cs:4: using TAOM.Features.SettlementFood;
C:\Users\mikew\source\repos\TAOM\TAOM.Tests\Features\SettlementFood\SettlementFoodServiceTests.cs:5: 
C:\Users\mikew\source\repos\TAOM\TAOM.Tests\Features\SettlementFood\SettlementFoodServiceTests.cs:6: namespace TAOM.Tests.Features.SettlementFood;
C:\Users\mikew\source\repos\TAOM\TAOM.Tests\Features\SettlementFood\SettlementFoodServiceTests.cs:7: 
C:\Users\mikew\source\repos\TAOM\TAOM.Tests\Features\SettlementFood\SettlementFoodServiceTests.cs:8: [TestClass]
C:\Users\mikew\source\repos\TAOM\TAOM.Tests\Features\SettlementFood\SettlementFoodServiceTests.cs:9: public class SettlementFoodServiceTests
C:\Users\mikew\source\repos\TAOM\TAOM.Tests\Features\SettlementFood\SettlementFoodServiceTests.cs:10: {
C:\Users\mikew\source\repos\TAOM\TAOM.Tests\Features\SettlementFood\SettlementFoodServiceTests.cs:11:     private SettlementFoodService _sut = null!;
C:\Users\mikew\source\repos\TAOM\TAOM.Tests\Features\SettlementFood\SettlementFoodServiceTests.cs:12: 
C:\Users\mikew\source\repos\TAOM\TAOM.Tests\Features\SettlementFood\SettlementFoodServiceTests.cs:13:     [TestInitialize]
C:\Users\mikew\source\repos\TAOM\TAOM.Tests\Features\SettlementFood\SettlementFoodServiceTests.cs:14:     public void Setup() => _sut = new SettlementFoodService();
C:\Users\mikew\source\repos\TAOM\TAOM.Tests\Features\SettlementFood\SettlementFoodServiceTests.cs:15: 
C:\Users\mikew\source\repos\TAOM\TAOM.Tests\Features\SettlementFood\SettlementFoodServiceTests.cs:16:     private static TownFoodSnapshot Snapshot(
C:\Users\mikew\source\repos\TAOM\TAOM.Tests\Features\SettlementFood\SettlementFoodServiceTests.cs:17:         bool isTown = true,
C:\Users\mikew\source\repos\TAOM\TAOM.Tests\Features\SettlementFood\SettlementFoodServiceTests.cs:18:         bool isUnderSiege = false,
C:\Users\mikew\source\repos\TAOM\TAOM.Tests\Features\SettlementFood\SettlementFoodServiceTests.cs:19:         int rawGarrison = 0,
C:\Users\mikew\source\repos\TAOM\TAOM.Tests\Features\SettlementFood\SettlementFoodServiceTests.cs:20:         int weightedGarrison = 0,
C:\Users\mikew\source\repos\TAOM\TAOM.Tests\Features\SettlementFood\SettlementFoodServiceTests.cs:21:         params int[] normalVillageHearthLevels) =>
C:\Users\mikew\source\repos\TAOM\TAOM.Tests\Features\SettlementFood\SettlementFoodServiceTests.cs:22:         new TownFoodSnapshot(isTown, isUnderSiege, rawGarrison, weightedGarrison,
C:\Users\mikew\source\repos\TAOM\TAOM.Tests\Features\SettlementFood\SettlementFoodServiceTests.cs:23:             new List<int>(normalVillageHearthLevels));
C:\Users\mikew\source\repos\TAOM\TAOM.Tests\Features\SettlementFood\SettlementFoodServiceTests.cs:24: 
C:\Users\mikew\source\repos\TAOM\TAOM.Tests\Features\SettlementFood\SettlementFoodServiceTests.cs:25:     private static SettlementFoodConfig Vanilla() => new SettlementFoodConfig();
C:\Users\mikew\source\repos\TAOM\TAOM.Tests\Features\SettlementFood\SettlementFoodServiceTests.cs:26: 
C:\Users\mikew\source\repos\TAOM\TAOM.Tests\Features\SettlementFood\SettlementFoodServiceTests.cs:27:     // --- Master gate ---
C:\Users\mikew\source\repos\TAOM\TAOM.Tests\Features\SettlementFood\SettlementFoodServiceTests.cs:28: 
C:\Users\mikew\source\repos\TAOM\TAOM.Tests\Features\SettlementFood\SettlementFoodServiceTests.cs:29:     [TestMethod]
C:\Users\mikew\source\repos\TAOM\TAOM.Tests\Features\SettlementFood\SettlementFoodServiceTests.cs:30:     public void ComputeFoodDelta_Disabled_ReturnsZero()
C:\Users\mikew\source\repos\TAOM\TAOM.Tests\Features\SettlementFood\SettlementFoodServiceTests.cs:31:     {
C:\Users\mikew\source\repos\TAOM\TAOM.Tests\Features\SettlementFood\SettlementFoodServiceTests.cs:32:         // Even with inflated garrison + non-vanilla production knobs, disabled => no adjustment.
C:\Users\mikew\source\repos\TAOM\TAOM.Tests\Features\SettlementFood\SettlementFoodServiceTests.cs:33:         var snapshot = Snapshot(rawGarrison: 200, weightedGarrison: 400, normalVillageHearthLevels: new[] { 1, 2 });
C:\Users\mikew\source\repos\TAOM\TAOM.Tests\Features\SettlementFood\SettlementFoodServiceTests.cs:34:         var config = new SettlementFoodConfig { TownBaseFood = 50f, VillageFoodMultiplier = 20f, FlatFoodBonus = 30f };
C:\Users\mikew\source\repos\TAOM\TAOM.Tests\Features\SettlementFood\SettlementFoodServiceTests.cs:35: 
C:\Users\mikew\source\repos\TAOM\TAOM.Tests\Features\SettlementFood\SettlementFoodServiceTests.cs:36:         Assert.AreEqual(0f, _sut.ComputeFoodDelta(snapshot, config, enabled: false), 0.001f);
C:\Users\mikew\source\repos\TAOM\TAOM.Tests\Features\SettlementFood\SettlementFoodServiceTests.cs:37:     }
C:\Users\mikew\source\repos\TAOM\TAOM.Tests\Features\SettlementFood\SettlementFoodServiceTests.cs:38: 
C:\Users\mikew\source\repos\TAOM\TAOM.Tests\Features\SettlementFood\SettlementFoodServiceTests.cs:39:     // --- Garrison raw-count correction (the troop-weight leak fix) ---
C:\Users\mikew\source\repos\TAOM\TAOM.Tests\Features\SettlementFood\SettlementFoodServiceTests.cs:40: 
C:\Users\mikew\source\repos\TAOM\TAOM.Tests\Features\SettlementFood\SettlementFoodServiceTests.cs:41:     [TestMethod]
C:\Users\mikew\source\repos\TAOM\TAOM.Tests\Features\SettlementFood\SettlementFoodServiceTests.cs:42:     public void ComputeFoodDelta_GarrisonInflated_AddsBackOverCountDividedByGarrisonDivisor()
C:\Users\mikew\source\repos\TAOM\TAOM.Tests\Features\SettlementFood\SettlementFoodServiceTests.cs:43:     {
C:\Users\mikew\source\repos\TAOM\TAOM.Tests\Features\SettlementFood\SettlementFoodServiceTests.cs:44:         // Base subtracted weighted/20; we want raw/20. Correction = (400-200)/20 = +10.
C:\Users\mikew\source\repos\TAOM\TAOM.Tests\Features\SettlementFood\SettlementFoodServiceTests.cs:45:         var snapshot = Snapshot(rawGarrison: 200, weightedGarrison: 400);
C:\Users\mikew\source\repos\TAOM\TAOM.Tests\Features\SettlementFood\SettlementFoodServiceTests.cs:46: 
C:\Users\mikew\source\repos\TAOM\TAOM.Tests\Features\SettlementFood\SettlementFoodServiceTests.cs:47:         Assert.AreEqual(10f, _sut.ComputeFoodDelta(snapshot, Vanilla(), enabled: true), 0.001f);
C:\Users\mikew\source\repos\TAOM\TAOM.Tests\Features\SettlementFood\SettlementFoodServiceTests.cs:48:     }
C:\Users\mikew\source\repos\TAOM\TAOM.Tests\Features\SettlementFood\SettlementFoodServiceTests.cs:49: 
C:\Users\mikew\source\repos\TAOM\TAOM.Tests\Features\SettlementFood\SettlementFoodServiceTests.cs:50:     [TestMethod]
C:\Users\mikew\source\repos\TAOM\TAOM.Tests\Features\SettlementFood\SettlementFoodServiceTests.cs:51:     public void ComputeFoodDelta_GarrisonNotInflated_NoCorrection()
C:\Users\mikew\source\repos\TAOM\TAOM.Tests\Features\SettlementFood\SettlementFoodServiceTests.cs:52:     {
C:\Users\mikew\source\repos\TAOM\TAOM.Tests\Features\SettlementFood\SettlementFoodServiceTests.cs:53:         // Troop weight off / all weight-1 troops => weighted == raw => no correction, vanilla knobs => 0.
C:\Users\mikew\source\repos\TAOM\TAOM.Tests\Features\SettlementFood\SettlementFoodServiceTests.cs:54:         var snapshot = Snapshot(rawGarrison: 250, weightedGarrison: 250);
C:\Users\mikew\source\repos\TAOM\TAOM.Tests\Features\SettlementFood\SettlementFoodServiceTests.cs:55: 
C:\Users\mikew\source\repos\TAOM\TAOM.Tests\Features\SettlementFood\SettlementFoodServiceTests.cs:56:         Assert.AreEqual(0f, _sut.ComputeFoodDelta(snapshot, Vanilla(), enabled: true), 0.001f);
C:\Users\mikew\source\repos\TAOM\TAOM.Tests\Features\SettlementFood\SettlementFoodServiceTests.cs:57:     }
C:\Users\mikew\source\repos\TAOM\TAOM.Tests\Features\SettlementFood\SettlementFoodServiceTests.cs:58: 
C:\Users\mikew\source\repos\TAOM\TAOM.Tests\Features\SettlementFood\SettlementFoodServiceTests.cs:59:     [TestMethod]
C:\Users\mikew\source\repos\TAOM\TAOM.Tests\Features\SettlementFood\SettlementFoodServiceTests.cs:60:     public void ComputeFoodDelta_RaisedGarrisonDivisor_ShrinksCorrection()
C:\Users\mikew\source\repos\TAOM\TAOM.Tests\Features\SettlementFood\SettlementFoodServiceTests.cs:61:     {
C:\Users\mikew\source\repos\TAOM\TAOM.Tests\Features\SettlementFood\SettlementFoodServiceTests.cs:62:         // Correction uses the (effective) garrison divisor so it stays consistent with base's term.
C:\Users\mikew\source\repos\TAOM\TAOM.Tests\Features\SettlementFood\SettlementFoodServiceTests.cs:63:         var snapshot = Snapshot(rawGarrison: 200, weightedGarrison: 400);
C:\Users\mikew\source\repos\TAOM\TAOM.Tests\Features\SettlementFood\SettlementFoodServiceTests.cs:64:         var config = new SettlementFoodConfig { GarrisonFoodDivisor = 40 };
C:\Users\mikew\source\repos\TAOM\TAOM.Tests\Features\SettlementFood\SettlementFoodServiceTests.cs:65: 
C:\Users\mikew\source\repos\TAOM\TAOM.Tests\Features\SettlementFood\SettlementFoodServiceTests.cs:66:         Assert.AreEqual(5f, _sut.ComputeFoodDelta(snapshot, config, enabled: true), 0.001f); // (400-200)/40
C:\Users\mikew\source\repos\TAOM\TAOM.Tests\Features\SettlementFood\SettlementFoodServiceTests.cs:67:     }
C:\Users\mikew\source\repos\TAOM\TAOM.Tests\Features\SettlementFood\SettlementFoodServiceTests.cs:68: 
C:\Users\mikew\source\repos\TAOM\TAOM.Tests\Features\SettlementFood\SettlementFoodServiceTests.cs:69:     [TestMethod]
C:\Users\mikew\source\repos\TAOM\TAOM.Tests\Features\SettlementFood\SettlementFoodServiceTests.cs:70:     public void ComputeFoodDelta_GarrisonCorrection_AppliesEvenUnderSiege()
C:\Users\mikew\source\repos\TAOM\TAOM.Tests\Features\SettlementFood\SettlementFoodServiceTests.cs:71:     {
C:\Users\mikew\source\repos\TAOM\TAOM.Tests\Features\SettlementFood\SettlementFoodServiceTests.cs:72:         // The weight inflation is a bug regardless of siege; the correction is NOT siege-gated.
C:\Users\mikew\source\repos\TAOM\TAOM.Tests\Features\SettlementFood\SettlementFoodServiceTests.cs:73:         var snapshot = Snapshot(isUnderSiege: true, rawGarrison: 200, weightedGarrison: 400);
C:\Users\mikew\source\repos\TAOM\TAOM.Tests\Features\SettlementFood\SettlementFoodServiceTests.cs:74: 
C:\Users\mikew\source\repos\TAOM\TAOM.Tests\Features\SettlementFood\SettlementFoodServiceTests.cs:75:         Assert.AreEqual(10f, _sut.ComputeFoodDelta(snapshot, Vanilla(), enabled: true), 0.001f);
C:\Users\mikew\source\repos\TAOM\TAOM.Tests\Features\SettlementFood\SettlementFoodServiceTests.cs:76:     }
C:\Users\mikew\source\repos\TAOM\TAOM.Tests\Features\SettlementFood\SettlementFoodServiceTests.cs:77: 
C:\Users\mikew\source\repos\TAOM\TAOM.Tests\Features\SettlementFood\SettlementFoodServiceTests.cs:78:     // --- Production knobs (siege-gated) ---
C:\Users\mikew\source\repos\TAOM\TAOM.Tests\Features\SettlementFood\SettlementFoodServiceTests.cs:79: 
C:\Users\mikew\source\repos\TAOM\TAOM.Tests\Features\SettlementFood\SettlementFoodServiceTests.cs:80:     [TestMethod]
C:\Users\mikew\source\repos\TAOM\TAOM.Tests\Features\SettlementFood\SettlementFoodServiceTests.cs:81:     public void ComputeFoodDelta_TownProductionKnobs_AddsBasePlusVillagePlusFlat()
C:\Users\mikew\source\repos\TAOM\TAOM.Tests\Features\SettlementFood\SettlementFoodServiceTests.cs:82:     {
C:\Users\mikew\source\repos\TAOM\TAOM.Tests\Features\SettlementFood\SettlementFoodServiceTests.cs:83:         // base (25-15)=10; villages (1+1)*(10-6)=8 + (2+1)*(10-6)=12 => 20; flat +5 => 35.
C:\Users\mikew\source\repos\TAOM\TAOM.Tests\Features\SettlementFood\SettlementFoodServiceTests.cs:84:         var snapshot = Snapshot(isTown: true, normalVillageHearthLevels: new[] { 1, 2 });
C:\Users\mikew\source\repos\TAOM\TAOM.Tests\Features\SettlementFood\SettlementFoodServiceTests.cs:85:         var config = new SettlementFoodConfig { TownBaseFood = 25f, VillageFoodMultiplier = 10f, FlatFoodBonus = 5f };
C:\Users\mikew\source\repos\TAOM\TAOM.Tests\Features\SettlementFood\SettlementFoodServiceTests.cs:86: 
C:\Users\mikew\source\repos\TAOM\TAOM.Tests\Features\SettlementFood\SettlementFoodServiceTests.cs:87:         Assert.AreEqual(35f, _sut.ComputeFoodDelta(snapshot, config, enabled: true), 0.001f);
C:\Users\mikew\source\repos\TAOM\TAOM.Tests\Features\SettlementFood\SettlementFoodServiceTests.cs:88:     }
C:\Users\mikew\source\repos\TAOM\TAOM.Tests\Features\SettlementFood\SettlementFoodServiceTests.cs:89: 
C:\Users\mikew\source\repos\TAOM\TAOM.Tests\Features\SettlementFood\SettlementFoodServiceTests.cs:90:     [TestMethod]
C:\Users\mikew\source\repos\TAOM\TAOM.Tests\Features\SettlementFood\SettlementFoodServiceTests.cs:91:     public void ComputeFoodDelta_Castle_UsesCastleBaseFoodDelta()
C:\Users\mikew\source\repos\TAOM\TAOM.Tests\Features\SettlementFood\SettlementFoodServiceTests.cs:92:     {
C:\Users\mikew\source\repos\TAOM\TAOM.Tests\Features\SettlementFood\SettlementFoodServiceTests.cs:93:         // Castle base (20-10)=10; no villages; vanilla mult/flat => 10.
C:\Users\mikew\source\repos\TAOM\TAOM.Tests\Features\SettlementFood\SettlementFoodServiceTests.cs:94:         var snapshot = Snapshot(isTown: false);
C:\Users\mikew\source\repos\TAOM\TAOM.Tests\Features\SettlementFood\SettlementFoodServiceTests.cs:95:         var config = new SettlementFoodConfig { CastleBaseFood = 20f };
C:\Users\mikew\source\repos\TAOM\TAOM.Tests\Features\SettlementFood\SettlementFoodServiceTests.cs:96: 
C:\Users\mikew\source\repos\TAOM\TAOM.Tests\Features\SettlementFood\SettlementFoodServiceTests.cs:97:         Assert.AreEqual(10f, _sut.ComputeFoodDelta(snapshot, config, enabled: true), 0.001f);
C:\Users\mikew\source\repos\TAOM\TAOM.Tests\Features\SettlementFood\SettlementFoodServiceTests.cs:98:     }
C:\Users\mikew\source\repos\TAOM\TAOM.Tests\Features\SettlementFood\SettlementFoodServiceTests.cs:99: 
C:\Users\mikew\source\repos\TAOM\TAOM.Tests\Features\SettlementFood\SettlementFoodServiceTests.cs:100:     [TestMethod]
C:\Users\mikew\source\repos\TAOM\TAOM.Tests\Features\SettlementFood\SettlementFoodServiceTests.cs:101:     public void ComputeFoodDelta_VanillaVillageMultiplier_NoVillageDelta()
C:\Users\mikew\source\repos\TAOM\TAOM.Tests\Features\SettlementFood\SettlementFoodServiceTests.cs:102:     {
C:\Users\mikew\source\repos\TAOM\TAOM.Tests\Features\SettlementFood\SettlementFoodServiceTests.cs:103:         var snapshot = Snapshot(normalVillageHearthLevels: new[] { 0, 1, 2 });
C:\Users\mikew\source\repos\TAOM\TAOM.Tests\Features\SettlementFood\SettlementFoodServiceTests.cs:104: 
C:\Users\mikew\source\repos\TAOM\TAOM.Tests\Features\SettlementFood\SettlementFoodServiceTests.cs:105:         Assert.AreEqual(0f, _sut.ComputeFoodDelta(snapshot, Vanilla(), enabled: true), 0.001f);
C:\Users\mikew\source\repos\TAOM\TAOM.Tests\Features\SettlementFood\SettlementFoodServiceTests.cs:106:     }
C:\Users\mikew\source\repos\TAOM\TAOM.Tests\Features\SettlementFood\SettlementFoodServiceTests.cs:107: 
C:\Users\mikew\source\repos\TAOM\TAOM.Tests\Features\SettlementFood\SettlementFoodServiceTests.cs:108:     [TestMethod]
C:\Users\mikew\source\repos\TAOM\TAOM.Tests\Features\SettlementFood\SettlementFoodServiceTests.cs:109:     public void ComputeFoodDelta_UnderSiege_SuppressesProductionKnobs()
C:\Users\mikew\source\repos\TAOM\TAOM.Tests\Features\SettlementFood\SettlementFoodServiceTests.cs:110:     {
C:\Users\mikew\source\repos\TAOM\TAOM.Tests\Features\SettlementFood\SettlementFoodServiceTests.cs:111:         // Production is lost under siege (vanilla); only the garrison correction survives.
C:\Users\mikew\source\repos\TAOM\TAOM.Tests\Features\SettlementFood\SettlementFoodServiceTests.cs:112:         var snapshot = Snapshot(isUnderSiege: true, rawGarrison: 200, weightedGarrison: 400,
C:\Users\mikew\source\repos\TAOM\TAOM.Tests\Features\SettlementFood\SettlementFoodServiceTests.cs:113:             normalVillageHearthLevels: new[] { 1, 2 });
C:\Users\mikew\source\repos\TAOM\TAOM.Tests\Features\SettlementFood\SettlementFoodServiceTests.cs:114:         var config = new SettlementFoodConfig { TownBaseFood = 50f, VillageFoodMultiplier = 20f, FlatFoodBonus = 30f };
C:\Users\mikew\source\repos\TAOM\TAOM.Tests\Features\SettlementFood\SettlementFoodServiceTests.cs:115: 
C:\Users\mikew\source\repos\TAOM\TAOM.Tests\Features\SettlementFood\SettlementFoodServiceTests.cs:116:         // garrison correction only: (400-200)/20 = 10
C:\Users\mikew\source\repos\TAOM\TAOM.Tests\Features\SettlementFood\SettlementFoodServiceTests.cs:117:         Assert.AreEqual(10f, _sut.ComputeFoodDelta(snapshot, config, enabled: true), 0.001f);
C:\Users\mikew\source\repos\TAOM\TAOM.Tests\Features\SettlementFood\SettlementFoodServiceTests.cs:118:     }
C:\Users\mikew\source\repos\TAOM\TAOM.Tests\Features\SettlementFood\SettlementFoodServiceTests.cs:119: 
C:\Users\mikew\source\repos\TAOM\TAOM.Tests\Features\SettlementFood\SettlementFoodServiceTests.cs:120:     [TestMethod]
C:\Users\mikew\source\repos\TAOM\TAOM.Tests\Features\SettlementFood\SettlementFoodServiceTests.cs:121:     public void ComputeFoodDelta_CombinedGarrisonAndProduction_SumsBoth()
C:\Users\mikew\source\repos\TAOM\TAOM.Tests\Features\SettlementFood\SettlementFoodServiceTests.cs:122:     {
C:\Users\mikew\source\repos\TAOM\TAOM.Tests\Features\SettlementFood\SettlementFoodServiceTests.cs:123:         var snapshot = Snapshot(isTown: true, rawGarrison: 200, weightedGarrison: 300,
C:\Users\mikew\source\repos\TAOM\TAOM.Tests\Features\SettlementFood\SettlementFoodServiceTests.cs:124:             normalVillageHearthLevels: new[] { 2 });
C:\Users\mikew\source\repos\TAOM\TAOM.Tests\Features\SettlementFood\SettlementFoodServiceTests.cs:125:         var config = new SettlementFoodConfig { TownBaseFood = 20f, VillageFoodMultiplier = 8f, FlatFoodBonus = 3f };
C:\Users\mikew\source\repos\TAOM\TAOM.Tests\Features\SettlementFood\SettlementFoodServiceTests.cs:126: 
C:\Users\mikew\source\repos\TAOM\TAOM.Tests\Features\SettlementFood\SettlementFoodServiceTests.cs:127:         // garrison (300-200)/20=5; base (20-15)=5; village (2+1)*(8-6)=6; flat 3 => 19
C:\Users\mikew\source\repos\TAOM\TAOM.Tests\Features\SettlementFood\SettlementFoodServiceTests.cs:128:         Assert.AreEqual(19f, _sut.ComputeFoodDelta(snapshot, config, enabled: true), 0.001f);
C:\Users\mikew\source\repos\TAOM\TAOM.Tests\Features\SettlementFood\SettlementFoodServiceTests.cs:129:     }
C:\Users\mikew\source\repos\TAOM\TAOM.Tests\Features\SettlementFood\SettlementFoodServiceTests.cs:130: 
C:\Users\mikew\source\repos\TAOM\TAOM.Tests\Features\SettlementFood\SettlementFoodServiceTests.cs:131:     // --- ApplyFoodAdjustment (ExplainedNumber integration) ---
C:\Users\mikew\source\repos\TAOM\TAOM.Tests\Features\SettlementFood\SettlementFoodServiceTests.cs:132: 
C:\Users\mikew\source\repos\TAOM\TAOM.Tests\Features\SettlementFood\SettlementFoodServiceTests.cs:133:     [TestMethod]
C:\Users\mikew\source\repos\TAOM\TAOM.Tests\Features\SettlementFood\SettlementFoodServiceTests.cs:134:     public void ApplyFoodAdjustment_NonZeroDelta_AddsToResult()
C:\Users\mikew\source\repos\TAOM\TAOM.Tests\Features\SettlementFood\SettlementFoodServiceTests.cs:135:     {
C:\Users\mikew\source\repos\TAOM\TAOM.Tests\Features\SettlementFood\SettlementFoodServiceTests.cs:136:         var snapshot = Snapshot(rawGarrison: 200, weightedGarrison: 400);
C:\Users\mikew\source\repos\TAOM\TAOM.Tests\Features\SettlementFood\SettlementFoodServiceTests.cs:137:         var result = new ExplainedNumber(100f);
C:\Users\mikew\source\repos\TAOM\TAOM.Tests\Features\SettlementFood\SettlementFoodServiceTests.cs:138: 
C:\Users\mikew\source\repos\TAOM\TAOM.Tests\Features\SettlementFood\SettlementFoodServiceTests.cs:139:         _sut.ApplyFoodAdjustment(snapshot, Vanilla(), enabled: true, ref result, includeDescriptions: false);
C:\Users\mikew\source\repos\TAOM\TAOM.Tests\Features\SettlementFood\SettlementFoodServiceTests.cs:140: 
C:\Users\mikew\source\repos\TAOM\TAOM.Tests\Features\SettlementFood\SettlementFoodServiceTests.cs:141:         Assert.AreEqual(110f, result.ResultNumber, 0.001f);
C:\Users\mikew\source\repos\TAOM\TAOM.Tests\Features\SettlementFood\SettlementFoodServiceTests.cs:142:     }
C:\Users\mikew\source\repos\TAOM\TAOM.Tests\Features\SettlementFood\SettlementFoodServiceTests.cs:143: 
C:\Users\mikew\source\repos\TAOM\TAOM.Tests\Features\SettlementFood\SettlementFoodServiceTests.cs:144:     [TestMethod]
C:\Users\mikew\source\repos\TAOM\TAOM.Tests\Features\SettlementFood\SettlementFoodServiceTests.cs:145:     public void ApplyFoodAdjustment_ZeroDelta_LeavesResultUnchanged()
C:\Users\mikew\source\repos\TAOM\TAOM.Tests\Features\SettlementFood\SettlementFoodServiceTests.cs:146:     {
C:\Users\mikew\source\repos\TAOM\TAOM.Tests\Features\SettlementFood\SettlementFoodServiceTests.cs:147:         var snapshot = Snapshot(rawGarrison: 250, weightedGarrison: 250);
C:\Users\mikew\source\repos\TAOM\TAOM.Tests\Features\SettlementFood\SettlementFoodServiceTests.cs:148:         var result = new ExplainedNumber(100f);
C:\Users\mikew\source\repos\TAOM\TAOM.Tests\Features\SettlementFood\SettlementFoodServiceTests.cs:149: 
C:\Users\mikew\source\repos\TAOM\TAOM.Tests\Features\SettlementFood\SettlementFoodServiceTests.cs:150:         _sut.ApplyFoodAdjustment(snapshot, Vanilla(), enabled: true, ref result, includeDescriptions: false);
C:\Users\mikew\source\repos\TAOM\TAOM.Tests\Features\SettlementFood\SettlementFoodServiceTests.cs:151: 
C:\Users\mikew\source\repos\TAOM\TAOM.Tests\Features\SettlementFood\SettlementFoodServiceTests.cs:152:         Assert.AreEqual(100f, result.ResultNumber, 0.001f);
C:\Users\mikew\source\repos\TAOM\TAOM.Tests\Features\SettlementFood\SettlementFoodServiceTests.cs:153:     }
C:\Users\mikew\source\repos\TAOM\TAOM.Tests\Features\SettlementFood\SettlementFoodServiceTests.cs:154: 
C:\Users\mikew\source\repos\TAOM\TAOM.Tests\Features\SettlementFood\SettlementFoodServiceTests.cs:155:     [TestMethod]
C:\Users\mikew\source\repos\TAOM\TAOM.Tests\Features\SettlementFood\SettlementFoodServiceTests.cs:156:     public void ApplyFoodAdjustment_Disabled_LeavesResultUnchanged()
C:\Users\mikew\source\repos\TAOM\TAOM.Tests\Features\SettlementFood\SettlementFoodServiceTests.cs:157:     {
C:\Users\mikew\source\repos\TAOM\TAOM.Tests\Features\SettlementFood\SettlementFoodServiceTests.cs:158:         var snapshot = Snapshot(rawGarrison: 200, weightedGarrison: 400);
C:\Users\mikew\source\repos\TAOM\TAOM.Tests\Features\SettlementFood\SettlementFoodServiceTests.cs:159:         var result = new ExplainedNumber(100f);
C:\Users\mikew\source\repos\TAOM\TAOM.Tests\Features\SettlementFood\SettlementFoodServiceTests.cs:160: 
C:\Users\mikew\source\repos\TAOM\TAOM.Tests\Features\SettlementFood\SettlementFoodServiceTests.cs:161:         _sut.ApplyFoodAdjustment(snapshot, Vanilla(), enabled: false, ref result, includeDescriptions: false);
C:\Users\mikew\source\repos\TAOM\TAOM.Tests\Features\SettlementFood\SettlementFoodServiceTests.cs:162: 
C:\Users\mikew\source\repos\TAOM\TAOM.Tests\Features\SettlementFood\SettlementFoodServiceTests.cs:163:         Assert.AreEqual(100f, result.ResultNumber, 0.001f);
C:\Users\mikew\source\repos\TAOM\TAOM.Tests\Features\SettlementFood\SettlementFoodServiceTests.cs:164:     }
C:\Users\mikew\source\repos\TAOM\TAOM.Tests\Features\SettlementFood\SettlementFoodServiceTests.cs:165: 
C:\Users\mikew\source\repos\TAOM\TAOM.Tests\Features\SettlementFood\SettlementFoodServiceTests.cs:166:     // --- Default config = vanilla constants ---
C:\Users\mikew\source\repos\TAOM\TAOM.Tests\Features\SettlementFood\SettlementFoodServiceTests.cs:167: 
C:\Users\mikew\source\repos\TAOM\TAOM.Tests\Features\SettlementFood\SettlementFoodServiceTests.cs:168:     [TestMethod]
C:\Users\mikew\source\repos\TAOM\TAOM.Tests\Features\SettlementFood\SettlementFoodServiceTests.cs:169:     public void DefaultConfig_MatchesVanillaFoodModelConstants()
C:\Users\mikew\source\repos\TAOM\TAOM.Tests\Features\SettlementFood\SettlementFoodServiceTests.cs:170:     {
C:\Users\mikew\source\repos\TAOM\TAOM.Tests\Features\SettlementFood\SettlementFoodServiceTests.cs:171:         var c = new SettlementFoodConfig();
C:\Users\mikew\source\repos\TAOM\TAOM.Tests\Features\SettlementFood\SettlementFoodServiceTests.cs:172:         Assert.AreEqual(20, c.GarrisonFoodDivisor, "vanilla NumberOfMenOnGarrisonToEatOneFood");
C:\Users\mikew\source\repos\TAOM\TAOM.Tests\Features\SettlementFood\SettlementFoodServiceTests.cs:173:         Assert.AreEqual(40, c.ProsperityFoodDivisor, "vanilla NumberOfProsperityToEatOneFood");
C:\Users\mikew\source\repos\TAOM\TAOM.Tests\Features\SettlementFood\SettlementFoodServiceTests.cs:174:         Assert.AreEqual(15f, c.TownBaseFood, 0.001f, "vanilla town lands-around food");
C:\Users\mikew\source\repos\TAOM\TAOM.Tests\Features\SettlementFood\SettlementFoodServiceTests.cs:175:         Assert.AreEqual(10f, c.CastleBaseFood, 0.001f, "vanilla castle lands-around food");
C:\Users\mikew\source\repos\TAOM\TAOM.Tests\Features\SettlementFood\SettlementFoodServiceTests.cs:176:         Assert.AreEqual(6f, c.VillageFoodMultiplier, 0.001f, "vanilla (hearthLevel+1)*6");
C:\Users\mikew\source\repos\TAOM\TAOM.Tests\Features\SettlementFood\SettlementFoodServiceTests.cs:177:         Assert.AreEqual(0f, c.FlatFoodBonus, 0.001f);
C:\Users\mikew\source\repos\TAOM\TAOM.Tests\Features\SettlementFood\SettlementFoodServiceTests.cs:178:         Assert.AreEqual(300, c.FoodStocksUpperLimit, "vanilla FoodStocksUpperLimit");
C:\Users\mikew\source\repos\TAOM\TAOM.Tests\Features\SettlementFood\SettlementFoodServiceTests.cs:179:         Assert.AreEqual(150, c.CastleFoodStockUpperLimitBonus, "vanilla CastleFoodStockUpperLimitBonus");
C:\Users\mikew\source\repos\TAOM\TAOM.Tests\Features\SettlementFood\SettlementFoodServiceTests.cs:180:     }
C:\Users\mikew\source\repos\TAOM\TAOM.Tests\Features\SettlementFood\SettlementFoodServiceTests.cs:181: }

 succeeded in 715ms:
C:\Users\mikew\source\repos\TAOM\TAOM.Tests\Features\SettlementFood\SettlementFoodConfigProviderTests.cs:1: using System.IO;
C:\Users\mikew\source\repos\TAOM\TAOM.Tests\Features\SettlementFood\SettlementFoodConfigProviderTests.cs:2: using Microsoft.VisualStudio.TestTools.UnitTesting;
C:\Users\mikew\source\repos\TAOM\TAOM.Tests\Features\SettlementFood\SettlementFoodConfigProviderTests.cs:3: using NSubstitute;
C:\Users\mikew\source\repos\TAOM\TAOM.Tests\Features\SettlementFood\SettlementFoodConfigProviderTests.cs:4: using TAOM.Core.Infrastructure;
C:\Users\mikew\source\repos\TAOM\TAOM.Tests\Features\SettlementFood\SettlementFoodConfigProviderTests.cs:5: using TAOM.Core.Logging;
C:\Users\mikew\source\repos\TAOM\TAOM.Tests\Features\SettlementFood\SettlementFoodConfigProviderTests.cs:6: using TAOM.Core.Validation;
C:\Users\mikew\source\repos\TAOM\TAOM.Tests\Features\SettlementFood\SettlementFoodConfigProviderTests.cs:7: using TAOM.Features.SettlementFood;
C:\Users\mikew\source\repos\TAOM\TAOM.Tests\Features\SettlementFood\SettlementFoodConfigProviderTests.cs:8: 
C:\Users\mikew\source\repos\TAOM\TAOM.Tests\Features\SettlementFood\SettlementFoodConfigProviderTests.cs:9: namespace TAOM.Tests.Features.SettlementFood;
C:\Users\mikew\source\repos\TAOM\TAOM.Tests\Features\SettlementFood\SettlementFoodConfigProviderTests.cs:10: 
C:\Users\mikew\source\repos\TAOM\TAOM.Tests\Features\SettlementFood\SettlementFoodConfigProviderTests.cs:11: [TestClass]
C:\Users\mikew\source\repos\TAOM\TAOM.Tests\Features\SettlementFood\SettlementFoodConfigProviderTests.cs:12: public class SettlementFoodConfigProviderTests
C:\Users\mikew\source\repos\TAOM\TAOM.Tests\Features\SettlementFood\SettlementFoodConfigProviderTests.cs:13: {
C:\Users\mikew\source\repos\TAOM\TAOM.Tests\Features\SettlementFood\SettlementFoodConfigProviderTests.cs:14:     private string _tempDir = null!;
C:\Users\mikew\source\repos\TAOM\TAOM.Tests\Features\SettlementFood\SettlementFoodConfigProviderTests.cs:15:     private string _configDir = null!;
C:\Users\mikew\source\repos\TAOM\TAOM.Tests\Features\SettlementFood\SettlementFoodConfigProviderTests.cs:16:     private IPathService _pathService = null!;
C:\Users\mikew\source\repos\TAOM\TAOM.Tests\Features\SettlementFood\SettlementFoodConfigProviderTests.cs:17:     private IModLogger _logger = null!;
C:\Users\mikew\source\repos\TAOM\TAOM.Tests\Features\SettlementFood\SettlementFoodConfigProviderTests.cs:18:     private SettlementFoodConfigProvider _sut = null!;
C:\Users\mikew\source\repos\TAOM\TAOM.Tests\Features\SettlementFood\SettlementFoodConfigProviderTests.cs:19: 
C:\Users\mikew\source\repos\TAOM\TAOM.Tests\Features\SettlementFood\SettlementFoodConfigProviderTests.cs:20:     [TestInitialize]
C:\Users\mikew\source\repos\TAOM\TAOM.Tests\Features\SettlementFood\SettlementFoodConfigProviderTests.cs:21:     public void Setup()
C:\Users\mikew\source\repos\TAOM\TAOM.Tests\Features\SettlementFood\SettlementFoodConfigProviderTests.cs:22:     {
C:\Users\mikew\source\repos\TAOM\TAOM.Tests\Features\SettlementFood\SettlementFoodConfigProviderTests.cs:23:         _tempDir = Path.Combine(Path.GetTempPath(), "TAOM_SettlementFood_" + Path.GetRandomFileName());
C:\Users\mikew\source\repos\TAOM\TAOM.Tests\Features\SettlementFood\SettlementFoodConfigProviderTests.cs:24:         _configDir = Path.Combine(_tempDir, "settlement_food");
C:\Users\mikew\source\repos\TAOM\TAOM.Tests\Features\SettlementFood\SettlementFoodConfigProviderTests.cs:25:         Directory.CreateDirectory(_configDir);
C:\Users\mikew\source\repos\TAOM\TAOM.Tests\Features\SettlementFood\SettlementFoodConfigProviderTests.cs:26: 
C:\Users\mikew\source\repos\TAOM\TAOM.Tests\Features\SettlementFood\SettlementFoodConfigProviderTests.cs:27:         _pathService = Substitute.For<IPathService>();
C:\Users\mikew\source\repos\TAOM\TAOM.Tests\Features\SettlementFood\SettlementFoodConfigProviderTests.cs:28:         _pathService.ModuleDataPath.Returns(_tempDir);
C:\Users\mikew\source\repos\TAOM\TAOM.Tests\Features\SettlementFood\SettlementFoodConfigProviderTests.cs:29:         _logger = Substitute.For<IModLogger>();
C:\Users\mikew\source\repos\TAOM\TAOM.Tests\Features\SettlementFood\SettlementFoodConfigProviderTests.cs:30: 
C:\Users\mikew\source\repos\TAOM\TAOM.Tests\Features\SettlementFood\SettlementFoodConfigProviderTests.cs:31:         _sut = new SettlementFoodConfigProvider(_pathService, _logger);
C:\Users\mikew\source\repos\TAOM\TAOM.Tests\Features\SettlementFood\SettlementFoodConfigProviderTests.cs:32:     }
C:\Users\mikew\source\repos\TAOM\TAOM.Tests\Features\SettlementFood\SettlementFoodConfigProviderTests.cs:33: 
C:\Users\mikew\source\repos\TAOM\TAOM.Tests\Features\SettlementFood\SettlementFoodConfigProviderTests.cs:34:     [TestCleanup]
C:\Users\mikew\source\repos\TAOM\TAOM.Tests\Features\SettlementFood\SettlementFoodConfigProviderTests.cs:35:     public void Cleanup()
C:\Users\mikew\source\repos\TAOM\TAOM.Tests\Features\SettlementFood\SettlementFoodConfigProviderTests.cs:36:     {
C:\Users\mikew\source\repos\TAOM\TAOM.Tests\Features\SettlementFood\SettlementFoodConfigProviderTests.cs:37:         if (Directory.Exists(_tempDir))
C:\Users\mikew\source\repos\TAOM\TAOM.Tests\Features\SettlementFood\SettlementFoodConfigProviderTests.cs:38:             Directory.Delete(_tempDir, true);
C:\Users\mikew\source\repos\TAOM\TAOM.Tests\Features\SettlementFood\SettlementFoodConfigProviderTests.cs:39:     }
C:\Users\mikew\source\repos\TAOM\TAOM.Tests\Features\SettlementFood\SettlementFoodConfigProviderTests.cs:40: 
C:\Users\mikew\source\repos\TAOM\TAOM.Tests\Features\SettlementFood\SettlementFoodConfigProviderTests.cs:41:     private void WriteConfig(string json) =>
C:\Users\mikew\source\repos\TAOM\TAOM.Tests\Features\SettlementFood\SettlementFoodConfigProviderTests.cs:42:         File.WriteAllText(Path.Combine(_configDir, "settlement_food_config.json"), json);
C:\Users\mikew\source\repos\TAOM\TAOM.Tests\Features\SettlementFood\SettlementFoodConfigProviderTests.cs:43: 
C:\Users\mikew\source\repos\TAOM\TAOM.Tests\Features\SettlementFood\SettlementFoodConfigProviderTests.cs:44:     [TestMethod]
C:\Users\mikew\source\repos\TAOM\TAOM.Tests\Features\SettlementFood\SettlementFoodConfigProviderTests.cs:45:     public void GetConfig_ValidJson_ParsesAllFields()
C:\Users\mikew\source\repos\TAOM\TAOM.Tests\Features\SettlementFood\SettlementFoodConfigProviderTests.cs:46:     {
C:\Users\mikew\source\repos\TAOM\TAOM.Tests\Features\SettlementFood\SettlementFoodConfigProviderTests.cs:47:         WriteConfig(@"{
C:\Users\mikew\source\repos\TAOM\TAOM.Tests\Features\SettlementFood\SettlementFoodConfigProviderTests.cs:48:   ""garrisonFoodDivisor"": 30,
C:\Users\mikew\source\repos\TAOM\TAOM.Tests\Features\SettlementFood\SettlementFoodConfigProviderTests.cs:49:   ""prosperityFoodDivisor"": 60,
C:\Users\mikew\source\repos\TAOM\TAOM.Tests\Features\SettlementFood\SettlementFoodConfigProviderTests.cs:50:   ""townBaseFood"": 25,
C:\Users\mikew\source\repos\TAOM\TAOM.Tests\Features\SettlementFood\SettlementFoodConfigProviderTests.cs:51:   ""castleBaseFood"": 18,
C:\Users\mikew\source\repos\TAOM\TAOM.Tests\Features\SettlementFood\SettlementFoodConfigProviderTests.cs:52:   ""villageFoodMultiplier"": 9,
C:\Users\mikew\source\repos\TAOM\TAOM.Tests\Features\SettlementFood\SettlementFoodConfigProviderTests.cs:53:   ""flatFoodBonus"": 12,
C:\Users\mikew\source\repos\TAOM\TAOM.Tests\Features\SettlementFood\SettlementFoodConfigProviderTests.cs:54:   ""foodStocksUpperLimit"": 500,
C:\Users\mikew\source\repos\TAOM\TAOM.Tests\Features\SettlementFood\SettlementFoodConfigProviderTests.cs:55:   ""castleFoodStockUpperLimitBonus"": 250
C:\Users\mikew\source\repos\TAOM\TAOM.Tests\Features\SettlementFood\SettlementFoodConfigProviderTests.cs:56: }");
C:\Users\mikew\source\repos\TAOM\TAOM.Tests\Features\SettlementFood\SettlementFoodConfigProviderTests.cs:57: 
C:\Users\mikew\source\repos\TAOM\TAOM.Tests\Features\SettlementFood\SettlementFoodConfigProviderTests.cs:58:         var c = _sut.GetConfig();
C:\Users\mikew\source\repos\TAOM\TAOM.Tests\Features\SettlementFood\SettlementFoodConfigProviderTests.cs:59: 
C:\Users\mikew\source\repos\TAOM\TAOM.Tests\Features\SettlementFood\SettlementFoodConfigProviderTests.cs:60:         Assert.AreEqual(30, c.GarrisonFoodDivisor);
C:\Users\mikew\source\repos\TAOM\TAOM.Tests\Features\SettlementFood\SettlementFoodConfigProviderTests.cs:61:         Assert.AreEqual(60, c.ProsperityFoodDivisor);
C:\Users\mikew\source\repos\TAOM\TAOM.Tests\Features\SettlementFood\SettlementFoodConfigProviderTests.cs:62:         Assert.AreEqual(25f, c.TownBaseFood, 0.001f);
C:\Users\mikew\source\repos\TAOM\TAOM.Tests\Features\SettlementFood\SettlementFoodConfigProviderTests.cs:63:         Assert.AreEqual(18f, c.CastleBaseFood, 0.001f);
C:\Users\mikew\source\repos\TAOM\TAOM.Tests\Features\SettlementFood\SettlementFoodConfigProviderTests.cs:64:         Assert.AreEqual(9f, c.VillageFoodMultiplier, 0.001f);
C:\Users\mikew\source\repos\TAOM\TAOM.Tests\Features\SettlementFood\SettlementFoodConfigProviderTests.cs:65:         Assert.AreEqual(12f, c.FlatFoodBonus, 0.001f);
C:\Users\mikew\source\repos\TAOM\TAOM.Tests\Features\SettlementFood\SettlementFoodConfigProviderTests.cs:66:         Assert.AreEqual(500, c.FoodStocksUpperLimit);
C:\Users\mikew\source\repos\TAOM\TAOM.Tests\Features\SettlementFood\SettlementFoodConfigProviderTests.cs:67:         Assert.AreEqual(250, c.CastleFoodStockUpperLimitBonus);
C:\Users\mikew\source\repos\TAOM\TAOM.Tests\Features\SettlementFood\SettlementFoodConfigProviderTests.cs:68:         _logger.Received().LogInfo(Arg.Is<string>(s => s.Contains("Loaded")));
C:\Users\mikew\source\repos\TAOM\TAOM.Tests\Features\SettlementFood\SettlementFoodConfigProviderTests.cs:69:     }
C:\Users\mikew\source\repos\TAOM\TAOM.Tests\Features\SettlementFood\SettlementFoodConfigProviderTests.cs:70: 
C:\Users\mikew\source\repos\TAOM\TAOM.Tests\Features\SettlementFood\SettlementFoodConfigProviderTests.cs:71:     [TestMethod]
C:\Users\mikew\source\repos\TAOM\TAOM.Tests\Features\SettlementFood\SettlementFoodConfigProviderTests.cs:72:     public void GetConfig_MissingFile_ReturnsDefaultsAndLogsWarning()
C:\Users\mikew\source\repos\TAOM\TAOM.Tests\Features\SettlementFood\SettlementFoodConfigProviderTests.cs:73:     {
C:\Users\mikew\source\repos\TAOM\TAOM.Tests\Features\SettlementFood\SettlementFoodConfigProviderTests.cs:74:         var c = _sut.GetConfig();
C:\Users\mikew\source\repos\TAOM\TAOM.Tests\Features\SettlementFood\SettlementFoodConfigProviderTests.cs:75: 
C:\Users\mikew\source\repos\TAOM\TAOM.Tests\Features\SettlementFood\SettlementFoodConfigProviderTests.cs:76:         Assert.AreEqual(20, c.GarrisonFoodDivisor);
C:\Users\mikew\source\repos\TAOM\TAOM.Tests\Features\SettlementFood\SettlementFoodConfigProviderTests.cs:77:         Assert.AreEqual(40, c.ProsperityFoodDivisor);
C:\Users\mikew\source\repos\TAOM\TAOM.Tests\Features\SettlementFood\SettlementFoodConfigProviderTests.cs:78:         _logger.Received().LogWarning(Arg.Is<string>(s => s.Contains("not found")));
C:\Users\mikew\source\repos\TAOM\TAOM.Tests\Features\SettlementFood\SettlementFoodConfigProviderTests.cs:79:     }
C:\Users\mikew\source\repos\TAOM\TAOM.Tests\Features\SettlementFood\SettlementFoodConfigProviderTests.cs:80: 
C:\Users\mikew\source\repos\TAOM\TAOM.Tests\Features\SettlementFood\SettlementFoodConfigProviderTests.cs:81:     [TestMethod]
C:\Users\mikew\source\repos\TAOM\TAOM.Tests\Features\SettlementFood\SettlementFoodConfigProviderTests.cs:82:     public void GetConfig_MalformedJson_ReturnsDefaultsAndLogsError()
C:\Users\mikew\source\repos\TAOM\TAOM.Tests\Features\SettlementFood\SettlementFoodConfigProviderTests.cs:83:     {
C:\Users\mikew\source\repos\TAOM\TAOM.Tests\Features\SettlementFood\SettlementFoodConfigProviderTests.cs:84:         WriteConfig("not valid json {{{");
C:\Users\mikew\source\repos\TAOM\TAOM.Tests\Features\SettlementFood\SettlementFoodConfigProviderTests.cs:85: 
C:\Users\mikew\source\repos\TAOM\TAOM.Tests\Features\SettlementFood\SettlementFoodConfigProviderTests.cs:86:         var c = _sut.GetConfig();
C:\Users\mikew\source\repos\TAOM\TAOM.Tests\Features\SettlementFood\SettlementFoodConfigProviderTests.cs:87: 
C:\Users\mikew\source\repos\TAOM\TAOM.Tests\Features\SettlementFood\SettlementFoodConfigProviderTests.cs:88:         Assert.AreEqual(20, c.GarrisonFoodDivisor);
C:\Users\mikew\source\repos\TAOM\TAOM.Tests\Features\SettlementFood\SettlementFoodConfigProviderTests.cs:89:         Assert.AreEqual(40, c.ProsperityFoodDivisor);
C:\Users\mikew\source\repos\TAOM\TAOM.Tests\Features\SettlementFood\SettlementFoodConfigProviderTests.cs:90:         _logger.Received().LogError(Arg.Is<string>(s => s.Contains("Failed to parse")));
C:\Users\mikew\source\repos\TAOM\TAOM.Tests\Features\SettlementFood\SettlementFoodConfigProviderTests.cs:91:     }
C:\Users\mikew\source\repos\TAOM\TAOM.Tests\Features\SettlementFood\SettlementFoodConfigProviderTests.cs:92: 
C:\Users\mikew\source\repos\TAOM\TAOM.Tests\Features\SettlementFood\SettlementFoodConfigProviderTests.cs:93:     [TestMethod]
C:\Users\mikew\source\repos\TAOM\TAOM.Tests\Features\SettlementFood\SettlementFoodConfigProviderTests.cs:94:     public void GetConfig_PartialJson_MergesWithDefaults()
C:\Users\mikew\source\repos\TAOM\TAOM.Tests\Features\SettlementFood\SettlementFoodConfigProviderTests.cs:95:     {
C:\Users\mikew\source\repos\TAOM\TAOM.Tests\Features\SettlementFood\SettlementFoodConfigProviderTests.cs:96:         WriteConfig(@"{ ""garrisonFoodDivisor"": 35 }");
C:\Users\mikew\source\repos\TAOM\TAOM.Tests\Features\SettlementFood\SettlementFoodConfigProviderTests.cs:97: 
C:\Users\mikew\source\repos\TAOM\TAOM.Tests\Features\SettlementFood\SettlementFoodConfigProviderTests.cs:98:         var c = _sut.GetConfig();
C:\Users\mikew\source\repos\TAOM\TAOM.Tests\Features\SettlementFood\SettlementFoodConfigProviderTests.cs:99: 
C:\Users\mikew\source\repos\TAOM\TAOM.Tests\Features\SettlementFood\SettlementFoodConfigProviderTests.cs:100:         Assert.AreEqual(35, c.GarrisonFoodDivisor);
C:\Users\mikew\source\repos\TAOM\TAOM.Tests\Features\SettlementFood\SettlementFoodConfigProviderTests.cs:101:         Assert.AreEqual(40, c.ProsperityFoodDivisor);
C:\Users\mikew\source\repos\TAOM\TAOM.Tests\Features\SettlementFood\SettlementFoodConfigProviderTests.cs:102:         Assert.AreEqual(15f, c.TownBaseFood, 0.001f);
C:\Users\mikew\source\repos\TAOM\TAOM.Tests\Features\SettlementFood\SettlementFoodConfigProviderTests.cs:103:     }
C:\Users\mikew\source\repos\TAOM\TAOM.Tests\Features\SettlementFood\SettlementFoodConfigProviderTests.cs:104: 
C:\Users\mikew\source\repos\TAOM\TAOM.Tests\Features\SettlementFood\SettlementFoodConfigProviderTests.cs:105:     [TestMethod]
C:\Users\mikew\source\repos\TAOM\TAOM.Tests\Features\SettlementFood\SettlementFoodConfigProviderTests.cs:106:     public void GetConfig_CalledTwice_ReturnsSameCachedInstance()
C:\Users\mikew\source\repos\TAOM\TAOM.Tests\Features\SettlementFood\SettlementFoodConfigProviderTests.cs:107:     {
C:\Users\mikew\source\repos\TAOM\TAOM.Tests\Features\SettlementFood\SettlementFoodConfigProviderTests.cs:108:         WriteConfig(@"{ ""garrisonFoodDivisor"": 25 }");
C:\Users\mikew\source\repos\TAOM\TAOM.Tests\Features\SettlementFood\SettlementFoodConfigProviderTests.cs:109: 
C:\Users\mikew\source\repos\TAOM\TAOM.Tests\Features\SettlementFood\SettlementFoodConfigProviderTests.cs:110:         Assert.AreSame(_sut.GetConfig(), _sut.GetConfig());
C:\Users\mikew\source\repos\TAOM\TAOM.Tests\Features\SettlementFood\SettlementFoodConfigProviderTests.cs:111:     }
C:\Users\mikew\source\repos\TAOM\TAOM.Tests\Features\SettlementFood\SettlementFoodConfigProviderTests.cs:112: 
C:\Users\mikew\source\repos\TAOM\TAOM.Tests\Features\SettlementFood\SettlementFoodConfigProviderTests.cs:113:     [TestMethod]
C:\Users\mikew\source\repos\TAOM\TAOM.Tests\Features\SettlementFood\SettlementFoodConfigProviderTests.cs:114:     public void GetConfig_ZeroGarrisonDivisor_RevertsToDefaultAndWarns()
C:\Users\mikew\source\repos\TAOM\TAOM.Tests\Features\SettlementFood\SettlementFoodConfigProviderTests.cs:115:     {
C:\Users\mikew\source\repos\TAOM\TAOM.Tests\Features\SettlementFood\SettlementFoodConfigProviderTests.cs:116:         // A 0 divisor would poison the vanilla food formula with Infinity — must be rejected.
C:\Users\mikew\source\repos\TAOM\TAOM.Tests\Features\SettlementFood\SettlementFoodConfigProviderTests.cs:117:         WriteConfig(@"{ ""garrisonFoodDivisor"": 0 }");
C:\Users\mikew\source\repos\TAOM\TAOM.Tests\Features\SettlementFood\SettlementFoodConfigProviderTests.cs:118: 
C:\Users\mikew\source\repos\TAOM\TAOM.Tests\Features\SettlementFood\SettlementFoodConfigProviderTests.cs:119:         var c = _sut.GetConfig();
C:\Users\mikew\source\repos\TAOM\TAOM.Tests\Features\SettlementFood\SettlementFoodConfigProviderTests.cs:120: 
C:\Users\mikew\source\repos\TAOM\TAOM.Tests\Features\SettlementFood\SettlementFoodConfigProviderTests.cs:121:         Assert.AreEqual(20, c.GarrisonFoodDivisor);
C:\Users\mikew\source\repos\TAOM\TAOM.Tests\Features\SettlementFood\SettlementFoodConfigProviderTests.cs:122:         _logger.Received().LogWarning(Arg.Is<string>(s => s.Contains("garrisonFoodDivisor=0")));
C:\Users\mikew\source\repos\TAOM\TAOM.Tests\Features\SettlementFood\SettlementFoodConfigProviderTests.cs:123:     }
C:\Users\mikew\source\repos\TAOM\TAOM.Tests\Features\SettlementFood\SettlementFoodConfigProviderTests.cs:124: 
C:\Users\mikew\source\repos\TAOM\TAOM.Tests\Features\SettlementFood\SettlementFoodConfigProviderTests.cs:125:     [TestMethod]
C:\Users\mikew\source\repos\TAOM\TAOM.Tests\Features\SettlementFood\SettlementFoodConfigProviderTests.cs:126:     public void GetConfig_NegativeProsperityDivisor_RevertsToDefaultAndWarns()
C:\Users\mikew\source\repos\TAOM\TAOM.Tests\Features\SettlementFood\SettlementFoodConfigProviderTests.cs:127:     {
C:\Users\mikew\source\repos\TAOM\TAOM.Tests\Features\SettlementFood\SettlementFoodConfigProviderTests.cs:128:         WriteConfig(@"{ ""prosperityFoodDivisor"": -10 }");
C:\Users\mikew\source\repos\TAOM\TAOM.Tests\Features\SettlementFood\SettlementFoodConfigProviderTests.cs:129: 
C:\Users\mikew\source\repos\TAOM\TAOM.Tests\Features\SettlementFood\SettlementFoodConfigProviderTests.cs:130:         var c = _sut.GetConfig();
C:\Users\mikew\source\repos\TAOM\TAOM.Tests\Features\SettlementFood\SettlementFoodConfigProviderTests.cs:131: 
C:\Users\mikew\source\repos\TAOM\TAOM.Tests\Features\SettlementFood\SettlementFoodConfigProviderTests.cs:132:         Assert.AreEqual(40, c.ProsperityFoodDivisor);
C:\Users\mikew\source\repos\TAOM\TAOM.Tests\Features\SettlementFood\SettlementFoodConfigProviderTests.cs:133:         _logger.Received().LogWarning(Arg.Is<string>(s => s.Contains("prosperityFoodDivisor=-10")));
C:\Users\mikew\source\repos\TAOM\TAOM.Tests\Features\SettlementFood\SettlementFoodConfigProviderTests.cs:134:     }
C:\Users\mikew\source\repos\TAOM\TAOM.Tests\Features\SettlementFood\SettlementFoodConfigProviderTests.cs:135: 
C:\Users\mikew\source\repos\TAOM\TAOM.Tests\Features\SettlementFood\SettlementFoodConfigProviderTests.cs:136:     [TestMethod]
C:\Users\mikew\source\repos\TAOM\TAOM.Tests\Features\SettlementFood\SettlementFoodConfigProviderTests.cs:137:     public void GetConfig_NegativeTownBaseFood_RevertsToDefaultAndWarns()
C:\Users\mikew\source\repos\TAOM\TAOM.Tests\Features\SettlementFood\SettlementFoodConfigProviderTests.cs:138:     {
C:\Users\mikew\source\repos\TAOM\TAOM.Tests\Features\SettlementFood\SettlementFoodConfigProviderTests.cs:139:         WriteConfig(@"{ ""townBaseFood"": -5 }");
C:\Users\mikew\source\repos\TAOM\TAOM.Tests\Features\SettlementFood\SettlementFoodConfigProviderTests.cs:140: 
C:\Users\mikew\source\repos\TAOM\TAOM.Tests\Features\SettlementFood\SettlementFoodConfigProviderTests.cs:141:         var c = _sut.GetConfig();
C:\Users\mikew\source\repos\TAOM\TAOM.Tests\Features\SettlementFood\SettlementFoodConfigProviderTests.cs:142: 
C:\Users\mikew\source\repos\TAOM\TAOM.Tests\Features\SettlementFood\SettlementFoodConfigProviderTests.cs:143:         Assert.AreEqual(15f, c.TownBaseFood, 0.001f);
C:\Users\mikew\source\repos\TAOM\TAOM.Tests\Features\SettlementFood\SettlementFoodConfigProviderTests.cs:144:         _logger.Received().LogWarning(Arg.Is<string>(s => s.Contains("townBaseFood=-5")));
C:\Users\mikew\source\repos\TAOM\TAOM.Tests\Features\SettlementFood\SettlementFoodConfigProviderTests.cs:145:     }
C:\Users\mikew\source\repos\TAOM\TAOM.Tests\Features\SettlementFood\SettlementFoodConfigProviderTests.cs:146: 
C:\Users\mikew\source\repos\TAOM\TAOM.Tests\Features\SettlementFood\SettlementFoodConfigProviderTests.cs:147:     [TestMethod]
C:\Users\mikew\source\repos\TAOM\TAOM.Tests\Features\SettlementFood\SettlementFoodConfigProviderTests.cs:148:     public void GetConfig_NaNVillageMultiplier_RevertsToFiniteDefault()
C:\Users\mikew\source\repos\TAOM\TAOM.Tests\Features\SettlementFood\SettlementFoodConfigProviderTests.cs:149:     {
C:\Users\mikew\source\repos\TAOM\TAOM.Tests\Features\SettlementFood\SettlementFoodConfigProviderTests.cs:150:         // NaN must never reach the consumer (IEEE-754 range checks pass NaN through if written naively).
C:\Users\mikew\source\repos\TAOM\TAOM.Tests\Features\SettlementFood\SettlementFoodConfigProviderTests.cs:151:         WriteConfig(@"{ ""villageFoodMultiplier"": NaN }");
C:\Users\mikew\source\repos\TAOM\TAOM.Tests\Features\SettlementFood\SettlementFoodConfigProviderTests.cs:152: 
C:\Users\mikew\source\repos\TAOM\TAOM.Tests\Features\SettlementFood\SettlementFoodConfigProviderTests.cs:153:         var c = _sut.GetConfig();
C:\Users\mikew\source\repos\TAOM\TAOM.Tests\Features\SettlementFood\SettlementFoodConfigProviderTests.cs:154: 
C:\Users\mikew\source\repos\TAOM\TAOM.Tests\Features\SettlementFood\SettlementFoodConfigProviderTests.cs:155:         Assert.IsTrue(FiniteFloatValidator.IsFinite(c.VillageFoodMultiplier),
C:\Users\mikew\source\repos\TAOM\TAOM.Tests\Features\SettlementFood\SettlementFoodConfigProviderTests.cs:156:             "NaN villageFoodMultiplier must be rejected, never surfaced");
C:\Users\mikew\source\repos\TAOM\TAOM.Tests\Features\SettlementFood\SettlementFoodConfigProviderTests.cs:157:         Assert.AreEqual(6f, c.VillageFoodMultiplier, 0.001f);
C:\Users\mikew\source\repos\TAOM\TAOM.Tests\Features\SettlementFood\SettlementFoodConfigProviderTests.cs:158:     }
C:\Users\mikew\source\repos\TAOM\TAOM.Tests\Features\SettlementFood\SettlementFoodConfigProviderTests.cs:159: 
C:\Users\mikew\source\repos\TAOM\TAOM.Tests\Features\SettlementFood\SettlementFoodConfigProviderTests.cs:160:     [TestMethod]
C:\Users\mikew\source\repos\TAOM\TAOM.Tests\Features\SettlementFood\SettlementFoodConfigProviderTests.cs:161:     public void GetConfig_NegativeFlatFoodBonus_RevertsToDefaultAndWarns()
C:\Users\mikew\source\repos\TAOM\TAOM.Tests\Features\SettlementFood\SettlementFoodConfigProviderTests.cs:162:     {
C:\Users\mikew\source\repos\TAOM\TAOM.Tests\Features\SettlementFood\SettlementFoodConfigProviderTests.cs:163:         WriteConfig(@"{ ""flatFoodBonus"": -20 }");
C:\Users\mikew\source\repos\TAOM\TAOM.Tests\Features\SettlementFood\SettlementFoodConfigProviderTests.cs:164: 
C:\Users\mikew\source\repos\TAOM\TAOM.Tests\Features\SettlementFood\SettlementFoodConfigProviderTests.cs:165:         var c = _sut.GetConfig();
C:\Users\mikew\source\repos\TAOM\TAOM.Tests\Features\SettlementFood\SettlementFoodConfigProviderTests.cs:166: 
C:\Users\mikew\source\repos\TAOM\TAOM.Tests\Features\SettlementFood\SettlementFoodConfigProviderTests.cs:167:         Assert.AreEqual(0f, c.FlatFoodBonus, 0.001f);
C:\Users\mikew\source\repos\TAOM\TAOM.Tests\Features\SettlementFood\SettlementFoodConfigProviderTests.cs:168:         _logger.Received().LogWarning(Arg.Is<string>(s => s.Contains("flatFoodBonus=-20")));
C:\Users\mikew\source\repos\TAOM\TAOM.Tests\Features\SettlementFood\SettlementFoodConfigProviderTests.cs:169:     }
C:\Users\mikew\source\repos\TAOM\TAOM.Tests\Features\SettlementFood\SettlementFoodConfigProviderTests.cs:170: 
C:\Users\mikew\source\repos\TAOM\TAOM.Tests\Features\SettlementFood\SettlementFoodConfigProviderTests.cs:171:     [TestMethod]
C:\Users\mikew\source\repos\TAOM\TAOM.Tests\Features\SettlementFood\SettlementFoodConfigProviderTests.cs:172:     public void GetConfig_ZeroFoodStocksUpperLimit_RevertsToDefaultAndWarns()
C:\Users\mikew\source\repos\TAOM\TAOM.Tests\Features\SettlementFood\SettlementFoodConfigProviderTests.cs:173:     {
C:\Users\mikew\source\repos\TAOM\TAOM.Tests\Features\SettlementFood\SettlementFoodConfigProviderTests.cs:174:         WriteConfig(@"{ ""foodStocksUpperLimit"": 0 }");
C:\Users\mikew\source\repos\TAOM\TAOM.Tests\Features\SettlementFood\SettlementFoodConfigProviderTests.cs:175: 
C:\Users\mikew\source\repos\TAOM\TAOM.Tests\Features\SettlementFood\SettlementFoodConfigProviderTests.cs:176:         var c = _sut.GetConfig();
C:\Users\mikew\source\repos\TAOM\TAOM.Tests\Features\SettlementFood\SettlementFoodConfigProviderTests.cs:177: 
C:\Users\mikew\source\repos\TAOM\TAOM.Tests\Features\SettlementFood\SettlementFoodConfigProviderTests.cs:178:         Assert.AreEqual(300, c.FoodStocksUpperLimit);
C:\Users\mikew\source\repos\TAOM\TAOM.Tests\Features\SettlementFood\SettlementFoodConfigProviderTests.cs:179:         _logger.Received().LogWarning(Arg.Is<string>(s => s.Contains("foodStocksUpperLimit=0")));
C:\Users\mikew\source\repos\TAOM\TAOM.Tests\Features\SettlementFood\SettlementFoodConfigProviderTests.cs:180:     }
C:\Users\mikew\source\repos\TAOM\TAOM.Tests\Features\SettlementFood\SettlementFoodConfigProviderTests.cs:181: 
C:\Users\mikew\source\repos\TAOM\TAOM.Tests\Features\SettlementFood\SettlementFoodConfigProviderTests.cs:182:     [TestMethod]
C:\Users\mikew\source\repos\TAOM\TAOM.Tests\Features\SettlementFood\SettlementFoodConfigProviderTests.cs:183:     public void GetConfig_NegativeCastleStockBonus_RevertsToDefaultAndWarns()
C:\Users\mikew\source\repos\TAOM\TAOM.Tests\Features\SettlementFood\SettlementFoodConfigProviderTests.cs:184:     {
C:\Users\mikew\source\repos\TAOM\TAOM.Tests\Features\SettlementFood\SettlementFoodConfigProviderTests.cs:185:         WriteConfig(@"{ ""castleFoodStockUpperLimitBonus"": -50 }");
C:\Users\mikew\source\repos\TAOM\TAOM.Tests\Features\SettlementFood\SettlementFoodConfigProviderTests.cs:186: 
C:\Users\mikew\source\repos\TAOM\TAOM.Tests\Features\SettlementFood\SettlementFoodConfigProviderTests.cs:187:         var c = _sut.GetConfig();
C:\Users\mikew\source\repos\TAOM\TAOM.Tests\Features\SettlementFood\SettlementFoodConfigProviderTests.cs:188: 
C:\Users\mikew\source\repos\TAOM\TAOM.Tests\Features\SettlementFood\SettlementFoodConfigProviderTests.cs:189:         Assert.AreEqual(150, c.CastleFoodStockUpperLimitBonus);
C:\Users\mikew\source\repos\TAOM\TAOM.Tests\Features\SettlementFood\SettlementFoodConfigProviderTests.cs:190:         _logger.Received().LogWarning(Arg.Is<string>(s => s.Contains("castleFoodStockUpperLimitBonus=-50")));
C:\Users\mikew\source\repos\TAOM\TAOM.Tests\Features\SettlementFood\SettlementFoodConfigProviderTests.cs:191:     }
C:\Users\mikew\source\repos\TAOM\TAOM.Tests\Features\SettlementFood\SettlementFoodConfigProviderTests.cs:192: 
C:\Users\mikew\source\repos\TAOM\TAOM.Tests\Features\SettlementFood\SettlementFoodConfigProviderTests.cs:193:     [TestMethod]
C:\Users\mikew\source\repos\TAOM\TAOM.Tests\Features\SettlementFood\SettlementFoodConfigProviderTests.cs:194:     public void GetConfig_ValidValues_LogsInfoNotWarning()
C:\Users\mikew\source\repos\TAOM\TAOM.Tests\Features\SettlementFood\SettlementFoodConfigProviderTests.cs:195:     {
C:\Users\mikew\source\repos\TAOM\TAOM.Tests\Features\SettlementFood\SettlementFoodConfigProviderTests.cs:196:         WriteConfig(@"{ ""garrisonFoodDivisor"": 30, ""prosperityFoodDivisor"": 60 }");
C:\Users\mikew\source\repos\TAOM\TAOM.Tests\Features\SettlementFood\SettlementFoodConfigProviderTests.cs:197: 
C:\Users\mikew\source\repos\TAOM\TAOM.Tests\Features\SettlementFood\SettlementFoodConfigProviderTests.cs:198:         _sut.GetConfig();
C:\Users\mikew\source\repos\TAOM\TAOM.Tests\Features\SettlementFood\SettlementFoodConfigProviderTests.cs:199: 
C:\Users\mikew\source\repos\TAOM\TAOM.Tests\Features\SettlementFood\SettlementFoodConfigProviderTests.cs:200:         _logger.Received().LogInfo(Arg.Is<string>(s => s.Contains("Loaded")));
C:\Users\mikew\source\repos\TAOM\TAOM.Tests\Features\SettlementFood\SettlementFoodConfigProviderTests.cs:201:         _logger.DidNotReceive().LogWarning(Arg.Is<string>(s => s.Contains("outside") || s.Contains("must be")));
C:\Users\mikew\source\repos\TAOM\TAOM.Tests\Features\SettlementFood\SettlementFoodConfigProviderTests.cs:202:     }
C:\Users\mikew\source\repos\TAOM\TAOM.Tests\Features\SettlementFood\SettlementFoodConfigProviderTests.cs:203: }

exec
"C:\\Program Files\\PowerShell\\7\\pwsh.exe" -Command '$files=@('"'Main/Features/TroopWeight/Hooks/PartyBase_NumberOfAllMembers_Patch.cs','Main/Features/TroopWeight/Hooks/PartyBaseNumberOfAllMembersHook.cs','Main/Features/TroopWeight/TroopWeightService.cs','Main/Features/TroopWeight/TroopWeightXmlLoader.cs'); foreach("'$f in $files){ if(Test-Path $f){ Select-String -Path $f -Pattern '"'HarmonyPatch|NumberOfAllMembers|get_NumberOfAllMembers|Postfix|EnableTroopWeight|CalculateWeightedMemberCount|TotalManCount|WeightedContribution|GetTroopWeight|return|weight <|weight >|Math' | ForEach-Object { \"{0}:{1}: {2}\" -f "'$_.Path,$_.LineNumber,$_.Line.Trim() } } }' in C:\Users\mikew\source\repos\TAOM
exec
"C:\\Program Files\\PowerShell\\7\\pwsh.exe" -Command "Get-ChildItem -Path 'Main/_Module/ModuleData' -Recurse -Filter 'troop_weights.xml' | ForEach-Object { "'$_.FullName }' in C:\Users\mikew\source\repos\TAOM
exec
"C:\\Program Files\\PowerShell\\7\\pwsh.exe" -Command "Select-String -Path 'Main/Core/Validation/*.cs','Main/**/*.cs' -Pattern 'class FiniteFloatValidator|IsFiniteInRange|IsFinite\\(' | ForEach-Object { \"{0}:{1}: {2}\" -f "'$_.Path,$_.LineNumber,$_.Line.Trim() }' in C:\Users\mikew\source\repos\TAOM
exec
"C:\\Program Files\\PowerShell\\7\\pwsh.exe" -Command "Select-String -Path 'Main/Features/TaomSettings.cs' -Pattern 'EnableTroopWeight|EnableSettlementFoodTuning|SettingProperty|DefaultValue|HintText' | ForEach-Object { \"{0}:{1}: {2}\" -f "'$_.Path,$_.LineNumber,$_.Line.Trim() }' in C:\Users\mikew\source\repos\TAOM
 succeeded in 760ms:
C:\Users\mikew\source\repos\TAOM\Main\_Module\ModuleData\TroopWeights\troop_weights.xml

 succeeded in 782ms:
C:\Users\mikew\source\repos\TAOM\Main\Features\TroopWeight\Hooks\PartyBase_NumberOfAllMembers_Patch.cs:6: [HarmonyPatch(typeof(PartyBase), nameof(PartyBase.NumberOfAllMembers), MethodType.Getter)]
C:\Users\mikew\source\repos\TAOM\Main\Features\TroopWeight\Hooks\PartyBase_NumberOfAllMembers_Patch.cs:7: [HarmonyPatchCategory("Patch17_TroopWeight")]
C:\Users\mikew\source\repos\TAOM\Main\Features\TroopWeight\Hooks\PartyBase_NumberOfAllMembers_Patch.cs:8: public static class PartyBase_NumberOfAllMembers_Patch
C:\Users\mikew\source\repos\TAOM\Main\Features\TroopWeight\Hooks\PartyBase_NumberOfAllMembers_Patch.cs:10: private static IOnPartyBaseNumberOfAllMembers? _hook;
C:\Users\mikew\source\repos\TAOM\Main\Features\TroopWeight\Hooks\PartyBase_NumberOfAllMembers_Patch.cs:12: public static void Initialize(IOnPartyBaseNumberOfAllMembers hook) => _hook = hook;
C:\Users\mikew\source\repos\TAOM\Main\Features\TroopWeight\Hooks\PartyBase_NumberOfAllMembers_Patch.cs:14: [HarmonyPostfix]
C:\Users\mikew\source\repos\TAOM\Main\Features\TroopWeight\Hooks\PartyBase_NumberOfAllMembers_Patch.cs:15: public static void Postfix(PartyBase __instance, ref int __result)
C:\Users\mikew\source\repos\TAOM\Main\Features\TroopWeight\Hooks\PartyBase_NumberOfAllMembers_Patch.cs:17: if (!(TaomSettings.Instance?.EnableTroopWeight ?? true)) return;
C:\Users\mikew\source\repos\TAOM\Main\Features\TroopWeight\Hooks\PartyBase_NumberOfAllMembers_Patch.cs:18: _hook?.OnPartyBaseNumberOfAllMembers(__instance, ref __result);
C:\Users\mikew\source\repos\TAOM\Main\Features\TroopWeight\Hooks\PartyBaseNumberOfAllMembersHook.cs:8: public class PartyBaseNumberOfAllMembersHook : IOnPartyBaseNumberOfAllMembers
C:\Users\mikew\source\repos\TAOM\Main\Features\TroopWeight\Hooks\PartyBaseNumberOfAllMembersHook.cs:13: public PartyBaseNumberOfAllMembersHook(ITroopWeightService troopWeightService, IModLogger logger)
C:\Users\mikew\source\repos\TAOM\Main\Features\TroopWeight\Hooks\PartyBaseNumberOfAllMembersHook.cs:18: public void OnPartyBaseNumberOfAllMembers(PartyBase partyBase, ref int __result)
C:\Users\mikew\source\repos\TAOM\Main\Features\TroopWeight\Hooks\PartyBaseNumberOfAllMembersHook.cs:23: return;
C:\Users\mikew\source\repos\TAOM\Main\Features\TroopWeight\Hooks\PartyBaseNumberOfAllMembersHook.cs:32: return;
C:\Users\mikew\source\repos\TAOM\Main\Features\TroopWeight\Hooks\PartyBaseNumberOfAllMembersHook.cs:35: var weightedCount = _troopWeightService.CalculateWeightedMemberCount(partyBase);
C:\Users\mikew\source\repos\TAOM\Main\Features\TroopWeight\Hooks\PartyBaseNumberOfAllMembersHook.cs:36: var weightedResult = (int)Math.Ceiling(weightedCount);
C:\Users\mikew\source\repos\TAOM\Main\Features\TroopWeight\TroopWeightService.cs:34: _weights = xmlLoader.GetTroopWeights();
C:\Users\mikew\source\repos\TAOM\Main\Features\TroopWeight\TroopWeightService.cs:38: public float GetTroopWeight(string troopStringId)
C:\Users\mikew\source\repos\TAOM\Main\Features\TroopWeight\TroopWeightService.cs:41: return 1.0f;
C:\Users\mikew\source\repos\TAOM\Main\Features\TroopWeight\TroopWeightService.cs:43: return _weights.TryGetValue(troopStringId, out var weight) ? weight : 1.0f;
C:\Users\mikew\source\repos\TAOM\Main\Features\TroopWeight\TroopWeightService.cs:46: public float GetTroopWeight(CharacterObject character)
C:\Users\mikew\source\repos\TAOM\Main\Features\TroopWeight\TroopWeightService.cs:48: return GetTroopWeight(character?.StringId);
C:\Users\mikew\source\repos\TAOM\Main\Features\TroopWeight\TroopWeightService.cs:51: public float CalculateWeightedMemberCount(PartyBase party)
C:\Users\mikew\source\repos\TAOM\Main\Features\TroopWeight\TroopWeightService.cs:54: return 0f;
C:\Users\mikew\source\repos\TAOM\Main\Features\TroopWeight\TroopWeightService.cs:56: return CalculateWeightedRosterCount(party.MemberRoster);
C:\Users\mikew\source\repos\TAOM\Main\Features\TroopWeight\TroopWeightService.cs:62: return 0f;
C:\Users\mikew\source\repos\TAOM\Main\Features\TroopWeight\TroopWeightService.cs:73: return totalWeight;
C:\Users\mikew\source\repos\TAOM\Main\Features\TroopWeight\TroopWeightService.cs:78: return 0f;
C:\Users\mikew\source\repos\TAOM\Main\Features\TroopWeight\TroopWeightService.cs:85: return element.Number;
C:\Users\mikew\source\repos\TAOM\Main\Features\TroopWeight\TroopWeightService.cs:87: var weight = GetTroopWeight(element.Character);
C:\Users\mikew\source\repos\TAOM\Main\Features\TroopWeight\TroopWeightService.cs:88: return element.Number * weight;
C:\Users\mikew\source\repos\TAOM\Main\Features\TroopWeight\TroopWeightService.cs:98: private static (float Healthy, float Wounded) WeightedContribution(float weight, int number, int woundedNumber)
C:\Users\mikew\source\repos\TAOM\Main\Features\TroopWeight\TroopWeightService.cs:105: return (healthy * weight, wounded * weight);
C:\Users\mikew\source\repos\TAOM\Main\Features\TroopWeight\TroopWeightService.cs:112: return (0, 0);
C:\Users\mikew\source\repos\TAOM\Main\Features\TroopWeight\TroopWeightService.cs:119: var (h, w) = WeightedContribution(GetTroopWeight(e.TroopId), e.Number, e.WoundedNumber);
C:\Users\mikew\source\repos\TAOM\Main\Features\TroopWeight\TroopWeightService.cs:124: return ((int)Math.Ceiling(weightedHealthy), (int)Math.Ceiling(weightedWounded));
C:\Users\mikew\source\repos\TAOM\Main\Features\TroopWeight\TroopWeightService.cs:130: return (0, 0);
C:\Users\mikew\source\repos\TAOM\Main\Features\TroopWeight\TroopWeightService.cs:138: return (box.Healthy, box.Wounded);
C:\Users\mikew\source\repos\TAOM\Main\Features\TroopWeight\TroopWeightService.cs:147: var (h, w) = WeightedContribution(GetTroopWeight(element.Character), element.Number, element.WoundedNumber);
C:\Users\mikew\source\repos\TAOM\Main\Features\TroopWeight\TroopWeightService.cs:153: box.Healthy = (int)Math.Ceiling(weightedHealthy);
C:\Users\mikew\source\repos\TAOM\Main\Features\TroopWeight\TroopWeightService.cs:154: box.Wounded = (int)Math.Ceiling(weightedWounded);
C:\Users\mikew\source\repos\TAOM\Main\Features\TroopWeight\TroopWeightService.cs:155: return (box.Healthy, box.Wounded);
C:\Users\mikew\source\repos\TAOM\Main\Features\TroopWeight\TroopWeightService.cs:160: return (0, 0);
C:\Users\mikew\source\repos\TAOM\Main\Features\TroopWeight\TroopWeightService.cs:168: return result;
C:\Users\mikew\source\repos\TAOM\Main\Features\TroopWeight\TroopWeightService.cs:170: // Weighted member total on the same basis as CalculateWeightedMemberCount / the patched
C:\Users\mikew\source\repos\TAOM\Main\Features\TroopWeight\TroopWeightService.cs:171: // NumberOfAllMembers the size limit is checked against (Number * weight, incl. wounded).
C:\Users\mikew\source\repos\TAOM\Main\Features\TroopWeight\TroopWeightService.cs:182: return result;
C:\Users\mikew\source\repos\TAOM\Main\Features\TroopWeight\TroopWeightService.cs:189: if (!e.IsHero && e.Count > 0 && e.Weight > 0f)
C:\Users\mikew\source\repos\TAOM\Main\Features\TroopWeight\TroopWeightService.cs:195: return byTier != 0 ? byTier : a.Weight.CompareTo(b.Weight);
C:\Users\mikew\source\repos\TAOM\Main\Features\TroopWeight\TroopWeightService.cs:203: int bodiesNeeded = (int)Math.Ceiling(overflow / e.Weight);
C:\Users\mikew\source\repos\TAOM\Main\Features\TroopWeight\TroopWeightService.cs:204: int shed = Math.Min(e.Count, bodiesNeeded);
C:\Users\mikew\source\repos\TAOM\Main\Features\TroopWeight\TroopWeightService.cs:212: return result;
C:\Users\mikew\source\repos\TAOM\Main\Features\TroopWeight\TroopWeightService.cs:217: _weights = _xmlLoader.GetTroopWeights();
C:\Users\mikew\source\repos\TAOM\Main\Features\TroopWeight\TroopWeightXmlLoader.cs:24: public Dictionary<string, float> GetTroopWeights()
C:\Users\mikew\source\repos\TAOM\Main\Features\TroopWeight\TroopWeightXmlLoader.cs:29: return _troopWeights;
C:\Users\mikew\source\repos\TAOM\Main\Features\TroopWeight\TroopWeightXmlLoader.cs:48: return;
C:\Users\mikew\source\repos\TAOM\Main\Features\TroopWeight\TroopWeightXmlLoader.cs:61: return;
C:\Users\mikew\source\repos\TAOM\Main\Features\TroopWeight\TroopWeightXmlLoader.cs:81: if (weight <= 0)

 succeeded in 756ms:
C:\Users\mikew\source\repos\TAOM\Main\Features\TaomSettings.cs:19: [SettingPropertyGroup("Encyclopedia")]
C:\Users\mikew\source\repos\TAOM\Main\Features\TaomSettings.cs:20: [SettingPropertyBool("Show All Characters", Order = 0,
C:\Users\mikew\source\repos\TAOM\Main\Features\TaomSettings.cs:21: HintText = "Reveals all characters in the encyclopedia, including those not yet encountered. Equivalent to the 'campaign.toggle_information_restrictions' cheat.")]
C:\Users\mikew\source\repos\TAOM\Main\Features\TaomSettings.cs:26: [SettingPropertyGroup("Troop Weight")]
C:\Users\mikew\source\repos\TAOM\Main\Features\TaomSettings.cs:27: [SettingPropertyBool("Enable Troop Weight", Order = 0,
C:\Users\mikew\source\repos\TAOM\Main\Features\TaomSettings.cs:28: HintText = "Weighted party size — elite units consume more party capacity. Cave trolls (4x), elves (2x), warg riders (2x).")]
C:\Users\mikew\source\repos\TAOM\Main\Features\TaomSettings.cs:29: public bool EnableTroopWeight { get; set; } = true;
C:\Users\mikew\source\repos\TAOM\Main\Features\TaomSettings.cs:33: [SettingPropertyGroup("Settlement Food")]
C:\Users\mikew\source\repos\TAOM\Main\Features\TaomSettings.cs:34: [SettingPropertyBool("Enable Settlement Food Tuning", Order = 0,
C:\Users\mikew\source\repos\TAOM\Main\Features\TaomSettings.cs:35: HintText = "Corrects garrison food consumption (Troop Weight no longer inflates it for elite garrisons) and applies the tunable food knobs in settlement_food/settlement_food_config.json (consumption divisors, base/village/flat production, storage caps). Off = vanilla engine food math (garrison food stays weighted). Config edits need an app restart.")]
C:\Users\mikew\source\repos\TAOM\Main\Features\TaomSettings.cs:36: public bool EnableSettlementFoodTuning { get; set; } = true;
C:\Users\mikew\source\repos\TAOM\Main\Features\TaomSettings.cs:40: [SettingPropertyGroup("Castle Recruitment")]
C:\Users\mikew\source\repos\TAOM\Main\Features\TaomSettings.cs:41: [SettingPropertyBool("Enable Castle Recruitment", Order = 0,
C:\Users\mikew\source\repos\TAOM\Main\Features\TaomSettings.cs:42: HintText = "When enabled, castles gain notables with recruitable volunteers — the player can 'Recruit troops' at any accessible castle. Existing notables remain in the save if you later disable this.")]
C:\Users\mikew\source\repos\TAOM\Main\Features\TaomSettings.cs:45: [SettingPropertyGroup("Castle Recruitment")]
C:\Users\mikew\source\repos\TAOM\Main\Features\TaomSettings.cs:46: [SettingPropertyBool("AI Recruits From Castles", Order = 1,
C:\Users\mikew\source\repos\TAOM\Main\Features\TaomSettings.cs:47: HintText = "When enabled, AI lord parties also score, travel to, and recruit volunteers from castles like they do from towns. Requires Enable Castle Recruitment.")]
C:\Users\mikew\source\repos\TAOM\Main\Features\TaomSettings.cs:50: [SettingPropertyGroup("Castle Recruitment")]
C:\Users\mikew\source\repos\TAOM\Main\Features\TaomSettings.cs:51: [SettingPropertyInteger("Notables Per Castle", 1, 5, Order = 2,
C:\Users\mikew\source\repos\TAOM\Main\Features\TaomSettings.cs:52: HintText = "How many recruiters each castle is populated with (vanilla towns = 5, villages = 3). Higher = more recruitment volume per castle. Default: 3.")]
C:\Users\mikew\source\repos\TAOM\Main\Features\TaomSettings.cs:57: [SettingPropertyGroup("Culture Conversion")]
C:\Users\mikew\source\repos\TAOM\Main\Features\TaomSettings.cs:58: [SettingPropertyBool("Enable Culture Conversion", Order = 0,
C:\Users\mikew\source\repos\TAOM\Main\Features\TaomSettings.cs:59: HintText = "When enabled, a town/castle (and its villages) conquered by a different culture gradually adopts the new owner's culture — producing their troops, militia, and identity. Disabling stops NEW conversions; already-converted settlements stay converted.")]
C:\Users\mikew\source\repos\TAOM\Main\Features\TaomSettings.cs:62: [SettingPropertyGroup("Culture Conversion")]
C:\Users\mikew\source\repos\TAOM\Main\Features\TaomSettings.cs:63: [SettingPropertyInteger("Days To Convert", 1, 365, Order = 1,
C:\Users\mikew\source\repos\TAOM\Main\Features\TaomSettings.cs:64: HintText = "Days the new owner must hold a cross-culture fief before it converts. Lower = faster cultural takeover. Default: 45.")]
C:\Users\mikew\source\repos\TAOM\Main\Features\TaomSettings.cs:67: [SettingPropertyGroup("Culture Conversion")]
C:\Users\mikew\source\repos\TAOM\Main\Features\TaomSettings.cs:68: [SettingPropertyBool("Require Stable Loyalty", Order = 2,
C:\Users\mikew\source\repos\TAOM\Main\Features\TaomSettings.cs:69: HintText = "When enabled, a conquered fief only converts once its loyalty is high enough (configured in culture_conversion_config.json), so a city in unrest never flips. Default: off.")]
C:\Users\mikew\source\repos\TAOM\Main\Features\TaomSettings.cs:74: [SettingPropertyGroup("War of the Ring")]
C:\Users\mikew\source\repos\TAOM\Main\Features\TaomSettings.cs:75: [SettingPropertyBool("Enable War of the Ring", Order = 0,
C:\Users\mikew\source\repos\TAOM\Main\Features\TaomSettings.cs:76: HintText = "When enabled, a scripted war will escalate between Free Peoples and Dark Powers.")]
C:\Users\mikew\source\repos\TAOM\Main\Features\TaomSettings.cs:79: [SettingPropertyGroup("War of the Ring")]
C:\Users\mikew\source\repos\TAOM\Main\Features\TaomSettings.cs:80: [SettingPropertyInteger("Phase 1 Start Day", 1, 365, Order = 1,
C:\Users\mikew\source\repos\TAOM\Main\Features\TaomSettings.cs:81: HintText = "Days after campaign start when Isengard and Dunland attack Rohan. Default 2.")]
C:\Users\mikew\source\repos\TAOM\Main\Features\TaomSettings.cs:84: [SettingPropertyGroup("War of the Ring")]
C:\Users\mikew\source\repos\TAOM\Main\Features\TaomSettings.cs:85: [SettingPropertyInteger("Phase 2 Start Day", 1, 365, Order = 2,
C:\Users\mikew\source\repos\TAOM\Main\Features\TaomSettings.cs:86: HintText = "Days after campaign start when all hostile kingdoms go to war and peace between hostile tiers is blocked. Default 14.")]
C:\Users\mikew\source\repos\TAOM\Main\Features\TaomSettings.cs:89: [SettingPropertyGroup("War of the Ring/Test Mode")]
C:\Users\mikew\source\repos\TAOM\Main\Features\TaomSettings.cs:90: [SettingPropertyBool("Enable Test Mode", Order = 0,
C:\Users\mikew\source\repos\TAOM\Main\Features\TaomSettings.cs:91: HintText = "Uses short delays (2/5 days) for rapid testing. Overrides Phase 1/2 days.")]
C:\Users\mikew\source\repos\TAOM\Main\Features\TaomSettings.cs:96: [SettingPropertyGroup("Battle Balance/Troop Power")]
C:\Users\mikew\source\repos\TAOM\Main\Features\TaomSettings.cs:97: [SettingPropertyBool("Enable Custom Troop Power", Order = 0,
C:\Users\mikew\source\repos\TAOM\Main\Features\TaomSettings.cs:98: HintText = "Enables configurable T7-T10 troop power values for battle simulation.")]
C:\Users\mikew\source\repos\TAOM\Main\Features\TaomSettings.cs:101: [SettingPropertyGroup("Battle Balance/Troop Power")]
C:\Users\mikew\source\repos\TAOM\Main\Features\TaomSettings.cs:102: [SettingPropertyBool("Override Vanilla Tiers (T1-T6)", Order = 1,
C:\Users\mikew\source\repos\TAOM\Main\Features\TaomSettings.cs:103: HintText = "If enabled, battle_balance_config.json TierPower values replace the vanilla formula for T1-T6.")]
C:\Users\mikew\source\repos\TAOM\Main\Features\TaomSettings.cs:106: [SettingPropertyGroup("Battle Balance/Troop Power")]
C:\Users\mikew\source\repos\TAOM\Main\Features\TaomSettings.cs:107: [SettingPropertyFloatingInteger("Tier 7 Base Power", 2.0f, 6.0f, "#0.00", Order = 2,
C:\Users\mikew\source\repos\TAOM\Main\Features\TaomSettings.cs:108: HintText = "Base simulation power for T7 troops (vanilla formula extrapolation = 3.06).")]
C:\Users\mikew\source\repos\TAOM\Main\Features\TaomSettings.cs:111: [SettingPropertyGroup("Battle Balance/Troop Power")]
C:\Users\mikew\source\repos\TAOM\Main\Features\TaomSettings.cs:112: [SettingPropertyFloatingInteger("Tier 8 Base Power", 2.0f, 7.0f, "#0.00", Order = 3,
C:\Users\mikew\source\repos\TAOM\Main\Features\TaomSettings.cs:113: HintText = "Base simulation power for T8 troops (vanilla formula extrapolation = 3.60).")]
C:\Users\mikew\source\repos\TAOM\Main\Features\TaomSettings.cs:116: [SettingPropertyGroup("Battle Balance/Troop Power")]
C:\Users\mikew\source\repos\TAOM\Main\Features\TaomSettings.cs:117: [SettingPropertyFloatingInteger("Tier 9 Base Power", 2.0f, 8.0f, "#0.00", Order = 4,
C:\Users\mikew\source\repos\TAOM\Main\Features\TaomSettings.cs:118: HintText = "Base simulation power for T9 troops (vanilla formula extrapolation = 4.18).")]
C:\Users\mikew\source\repos\TAOM\Main\Features\TaomSettings.cs:121: [SettingPropertyGroup("Battle Balance/Troop Power")]
C:\Users\mikew\source\repos\TAOM\Main\Features\TaomSettings.cs:122: [SettingPropertyFloatingInteger("Tier 10 Base Power", 2.0f, 9.0f, "#0.00", Order = 5,
C:\Users\mikew\source\repos\TAOM\Main\Features\TaomSettings.cs:123: HintText = "Base simulation power for T10 troops (vanilla formula extrapolation = 4.80).")]
C:\Users\mikew\source\repos\TAOM\Main\Features\TaomSettings.cs:126: [SettingPropertyGroup("Battle Balance/Troop Power")]
C:\Users\mikew\source\repos\TAOM\Main\Features\TaomSettings.cs:127: [SettingPropertyFloatingInteger("Hero Power Multiplier", 1.0f, 3.0f, "#0.0", Order = 6,
C:\Users\mikew\source\repos\TAOM\Main\Features\TaomSettings.cs:128: HintText = "Multiplier applied to heroes in battle simulation. Vanilla = 1.5.")]
C:\Users\mikew\source\repos\TAOM\Main\Features\TaomSettings.cs:131: [SettingPropertyGroup("Battle Balance/Troop Power")]
C:\Users\mikew\source\repos\TAOM\Main\Features\TaomSettings.cs:132: [SettingPropertyFloatingInteger("Mounted Power Multiplier", 1.0f, 2.0f, "#0.0", Order = 7,
C:\Users\mikew\source\repos\TAOM\Main\Features\TaomSettings.cs:133: HintText = "Multiplier applied to mounted troops in battle simulation. Vanilla = 1.2.")]
C:\Users\mikew\source\repos\TAOM\Main\Features\TaomSettings.cs:138: [SettingPropertyGroup("Battle Balance/Casualty Ratios")]
C:\Users\mikew\source\repos\TAOM\Main\Features\TaomSettings.cs:139: [SettingPropertyBool("Enable Custom Casualty Ratios", Order = 0,
C:\Users\mikew\source\repos\TAOM\Main\Features\TaomSettings.cs:140: HintText = "Enables configurable wound/kill ratios for battle simulation.")]
C:\Users\mikew\source\repos\TAOM\Main\Features\TaomSettings.cs:143: [SettingPropertyGroup("Battle Balance/Casualty Ratios")]
C:\Users\mikew\source\repos\TAOM\Main\Features\TaomSettings.cs:144: [SettingPropertyFloatingInteger("Player Battle Blunt Chance", 0.0f, 1.0f, "#0.00", Order = 1,
C:\Users\mikew\source\repos\TAOM\Main\Features\TaomSettings.cs:145: HintText = "Blunt (wound-only) damage chance in player battles. Vanilla = 0.30.")]
C:\Users\mikew\source\repos\TAOM\Main\Features\TaomSettings.cs:148: [SettingPropertyGroup("Battle Balance/Casualty Ratios")]
C:\Users\mikew\source\repos\TAOM\Main\Features\TaomSettings.cs:149: [SettingPropertyFloatingInteger("AI Battle Blunt Chance", 0.0f, 1.0f, "#0.00", Order = 2,
C:\Users\mikew\source\repos\TAOM\Main\Features\TaomSettings.cs:150: HintText = "Blunt damage chance in AI vs AI battles. Vanilla = 0.10.")]
C:\Users\mikew\source\repos\TAOM\Main\Features\TaomSettings.cs:153: [SettingPropertyGroup("Battle Balance/Casualty Ratios")]
C:\Users\mikew\source\repos\TAOM\Main\Features\TaomSettings.cs:154: [SettingPropertyBool("Enable Cultural Survival Bonuses", Order = 3,
C:\Users\mikew\source\repos\TAOM\Main\Features\TaomSettings.cs:155: HintText = "Applies per-culture survival modifiers from battle_balance_config.json. Gondor +30%, Lothlorien +50%, Mordor -20%.")]
C:\Users\mikew\source\repos\TAOM\Main\Features\TaomSettings.cs:160: [SettingPropertyGroup("Siege Defense")]
C:\Users\mikew\source\repos\TAOM\Main\Features\TaomSettings.cs:161: [SettingPropertyBool("Enable Siege Defense Events", Order = 0,
C:\Users\mikew\source\repos\TAOM\Main\Features\TaomSettings.cs:162: HintText = "When enabled, you receive an event when a watched faction's settlement is besieged, with a timed window to help defend.")]
C:\Users\mikew\source\repos\TAOM\Main\Features\TaomSettings.cs:165: [SettingPropertyGroup("Siege Defense")]
C:\Users\mikew\source\repos\TAOM\Main\Features\TaomSettings.cs:166: [SettingPropertyInteger("Response Window (Days)", 1, 14, Order = 1,
C:\Users\mikew\source\repos\TAOM\Main\Features\TaomSettings.cs:167: HintText = "Number of in-game days to travel to a besieged settlement before the event expires.")]
C:\Users\mikew\source\repos\TAOM\Main\Features\TaomSettings.cs:172: [SettingPropertyGroup("AI Strategic Intelligence")]
C:\Users\mikew\source\repos\TAOM\Main\Features\TaomSettings.cs:173: [SettingPropertyBool("Enable AI Strategic Intelligence", Order = 0,
C:\Users\mikew\source\repos\TAOM\Main\Features\TaomSettings.cs:174: HintText = "When enabled, AI armies stick to their current target rather than re-optimising every 3 hours. Reduces army thrashing and improves siege follow-through.")]
C:\Users\mikew\source\repos\TAOM\Main\Features\TaomSettings.cs:177: [SettingPropertyGroup("AI Strategic Intelligence")]
C:\Users\mikew\source\repos\TAOM\Main\Features\TaomSettings.cs:178: [SettingPropertyFloatingInteger("Commitment Multiplier", 1.0f, 10.0f, "#0.0", Order = 1,
C:\Users\mikew\source\repos\TAOM\Main\Features\TaomSettings.cs:179: HintText = "How strongly an army commits to its current target. 4.0 = the alternative must score 4x better before the army will divert. Vanilla implicit = 1.3.")]
C:\Users\mikew\source\repos\TAOM\Main\Features\TaomSettings.cs:182: [SettingPropertyGroup("AI Strategic Intelligence")]
C:\Users\mikew\source\repos\TAOM\Main\Features\TaomSettings.cs:183: [SettingPropertyFloatingInteger("Priority List Boost", 1.0f, 5.0f, "#0.0", Order = 2,
C:\Users\mikew\source\repos\TAOM\Main\Features\TaomSettings.cs:184: HintText = "Score multiplier applied to the first settlement in a faction's priority list. Decays linearly to 1.0 at the last entry. Affects Mordor, Isengard etc.")]
C:\Users\mikew\source\repos\TAOM\Main\Features\TaomSettings.cs:187: [SettingPropertyFloatingInteger("Evil Faction Aggression Scale", 0.5f, 3.0f, "#0.0", Order = 3,
C:\Users\mikew\source\repos\TAOM\Main\Features\TaomSettings.cs:188: HintText = "Global multiplier applied to all per-faction strength inflation values from army_targeting.json. 1.0 = use JSON defaults. Raise to make evil factions siege even when outnumbered.")]
C:\Users\mikew\source\repos\TAOM\Main\Features\TaomSettings.cs:189: [SettingPropertyGroup("AI Strategic Intelligence")]
C:\Users\mikew\source\repos\TAOM\Main\Features\TaomSettings.cs:192: [SettingPropertyFloatingInteger("Long-Range Priority Boost Scale", 1.0f, 5.0f, "#0.0", Order = 4,
C:\Users\mikew\source\repos\TAOM\Main\Features\TaomSettings.cs:193: HintText = "Global multiplier applied to per-faction distance compensation values from army_targeting.json. 1.0 = use JSON defaults. Raise if priority-list targets are still being ignored due to map distance.")]
C:\Users\mikew\source\repos\TAOM\Main\Features\TaomSettings.cs:194: [SettingPropertyGroup("AI Strategic Intelligence")]
C:\Users\mikew\source\repos\TAOM\Main\Features\TaomSettings.cs:197: [SettingPropertyFloatingInteger("Border Proximity Floor", 0.0f, 1.0f, "#0.00", Order = 5,
C:\Users\mikew\source\repos\TAOM\Main\Features\TaomSettings.cs:198: HintText = "Minimum border-proximity score substituted for priority-list targets that vanilla rejects as out-of-range. 0 = vanilla (may ignore distant priority targets entirely). 0.15 = allow long-range priority targets to be scored.")]
C:\Users\mikew\source\repos\TAOM\Main\Features\TaomSettings.cs:199: [SettingPropertyGroup("AI Strategic Intelligence")]
C:\Users\mikew\source\repos\TAOM\Main\Features\TaomSettings.cs:204: [SettingPropertyGroup("Time Acceleration", GroupOrder = 10)]
C:\Users\mikew\source\repos\TAOM\Main\Features\TaomSettings.cs:205: [SettingPropertyInteger("Fast Forward Multiplier", 1, 128, Order = 0,
C:\Users\mikew\source\repos\TAOM\Main\Features\TaomSettings.cs:206: HintText = "Speed multiplier applied when pressing the fast-forward button (Space). Default: 4.")]
C:\Users\mikew\source\repos\TAOM\Main\Features\TaomSettings.cs:209: [SettingPropertyGroup("Time Acceleration")]
C:\Users\mikew\source\repos\TAOM\Main\Features\TaomSettings.cs:210: [SettingPropertyInteger("Extra Fast Forward Multiplier", 1, 128, Order = 1,
C:\Users\mikew\source\repos\TAOM\Main\Features\TaomSettings.cs:211: HintText = "Speed multiplier applied with the extra fast-forward button (E). Default: 8.")]
C:\Users\mikew\source\repos\TAOM\Main\Features\TaomSettings.cs:214: [SettingPropertyGroup("Time Acceleration")]
C:\Users\mikew\source\repos\TAOM\Main\Features\TaomSettings.cs:215: [SettingPropertyInteger("Turbo Multiplier (Ctrl+Space)", 1, 128, Order = 2,
C:\Users\mikew\source\repos\TAOM\Main\Features\TaomSettings.cs:216: HintText = "Speed multiplier while holding Ctrl+Space. Releases back to prior speed on key-up. Default: 16.")]
C:\Users\mikew\source\repos\TAOM\Main\Features\TaomSettings.cs:221: [SettingPropertyGroup("Battle Tactics/Siege Dismount", GroupOrder = 20)]
C:\Users\mikew\source\repos\TAOM\Main\Features\TaomSettings.cs:222: [SettingPropertyBool("Enable Siege Dismount", Order = 0,
C:\Users\mikew\source\repos\TAOM\Main\Features\TaomSettings.cs:223: HintText = "Master toggle for the siege auto-dismount feature. When off, sieges behave vanilla (mount stays equipped).")]
C:\Users\mikew\source\repos\TAOM\Main\Features\TaomSettings.cs:226: [SettingPropertyGroup("Battle Tactics/Siege Dismount")]
C:\Users\mikew\source\repos\TAOM\Main\Features\TaomSettings.cs:227: [SettingPropertyInteger("Siege Mount Behavior (0=Vanilla, 1=Reserved, 2=ToInventory, 3=AutoRemount)", 0, 3, Order = 1,
C:\Users\mikew\source\repos\TAOM\Main\Features\TaomSettings.cs:228: HintText = "0 = Vanilla (no change). 1 = RESERVED (currently equivalent to Vanilla — full implementation deferred; would spawn the horse on the map separately). 2 = Mount moves to inventory for siege duration; player must re-equip manually after. 3 = Mount moves to inventory and is auto-restored after siege ends. Default: 3.")]
C:\Users\mikew\source\repos\TAOM\Main\Features\TaomSettings.cs:231: [SettingPropertyGroup("Battle Tactics/Siege Dismount")]
C:\Users\mikew\source\repos\TAOM\Main\Features\TaomSettings.cs:232: [SettingPropertyBool("Siege Dismount Debug Mode", Order = 2,
C:\Users\mikew\source\repos\TAOM\Main\Features\TaomSettings.cs:233: HintText = "Show diagnostic [SiegeDismount] messages on the in-game HUD. Off = file log only.")]
C:\Users\mikew\source\repos\TAOM\Main\Features\TaomSettings.cs:238: [SettingPropertyGroup("Messengers", GroupOrder = 25)]
C:\Users\mikew\source\repos\TAOM\Main\Features\TaomSettings.cs:239: [SettingPropertyBool("Enable Messengers", Order = 0,
C:\Users\mikew\source\repos\TAOM\Main\Features\TaomSettings.cs:240: HintText = "Send paid messengers to heroes you have already met. They travel for several days and trigger a conversation on arrival. Disable to remove the encyclopedia button and dialog hook.")]
C:\Users\mikew\source\repos\TAOM\Main\Features\TaomSettings.cs:243: [SettingPropertyGroup("Messengers")]
C:\Users\mikew\source\repos\TAOM\Main\Features\TaomSettings.cs:244: [SettingPropertyInteger("Gold Cost", 10, 500, Order = 1,
C:\Users\mikew\source\repos\TAOM\Main\Features\TaomSettings.cs:245: HintText = "Denar cost to dispatch one messenger.")]
C:\Users\mikew\source\repos\TAOM\Main\Features\TaomSettings.cs:248: [SettingPropertyGroup("Messengers")]
C:\Users\mikew\source\repos\TAOM\Main\Features\TaomSettings.cs:249: [SettingPropertyInteger("Travel Days", 1, 10, Order = 2,
C:\Users\mikew\source\repos\TAOM\Main\Features\TaomSettings.cs:250: HintText = "In-game days a messenger spends in transit before arriving at the target. Speed scales to map size.")]
C:\Users\mikew\source\repos\TAOM\Main\Features\TaomSettings.cs:253: [SettingPropertyGroup("Messengers")]
C:\Users\mikew\source\repos\TAOM\Main\Features\TaomSettings.cs:254: [SettingPropertyBool("Enable Accidents", Order = 3,
C:\Users\mikew\source\repos\TAOM\Main\Features\TaomSettings.cs:255: HintText = "Random ambush chance during travel. The base hourly probability lives in messenger_config.json (default 0.2%).")]
C:\Users\mikew\source\repos\TAOM\Main\Features\TaomSettings.cs:260: [SettingPropertyGroup("Battle Tactics/Mixed Formations", GroupOrder = 21)]
C:\Users\mikew\source\repos\TAOM\Main\Features\TaomSettings.cs:261: [SettingPropertyBool("Enable Mixed Formations", Order = 0,
C:\Users\mikew\source\repos\TAOM\Main\Features\TaomSettings.cs:262: HintText = "Master toggle. When off, formations use vanilla positioning. When on, formations with mixed melee + ranged units are reordered per the chosen layout while holding position.")]
C:\Users\mikew\source\repos\TAOM\Main\Features\TaomSettings.cs:265: [SettingPropertyGroup("Battle Tactics/Mixed Formations")]
C:\Users\mikew\source\repos\TAOM\Main\Features\TaomSettings.cs:266: [SettingPropertyInteger("Default Layout (0=InfFront, 1=RngFront, 2=Wings, 3=Checkerboard)", 0, 3, Order = 1,
C:\Users\mikew\source\repos\TAOM\Main\Features\TaomSettings.cs:267: HintText = "Default layout auto-applied to mixed-class formations (>=5 minority units AND >=20% minority share AND >=10 total units). 0=Infantry front + Ranged back. 1=Ranged front + Infantry back. 2=Ranged on the wings, Infantry in the center. 3=Checkerboard. Default: 0.")]
C:\Users\mikew\source\repos\TAOM\Main\Features\TaomSettings.cs:270: [SettingPropertyGroup("Battle Tactics/Mixed Formations")]
C:\Users\mikew\source\repos\TAOM\Main\Features\TaomSettings.cs:271: [SettingPropertyText("Cycle Layout Hotkey", Order = 2,
C:\Users\mikew\source\repos\TAOM\Main\Features\TaomSettings.cs:272: HintText = "Bannerlord InputKey name. Pressing this while a formation is selected cycles its layout to the next; pressing while no formation is selected cycles all formations. Default: L.")]
C:\Users\mikew\source\repos\TAOM\Main\Features\TaomSettings.cs:275: [SettingPropertyGroup("Battle Tactics/Mixed Formations")]
C:\Users\mikew\source\repos\TAOM\Main\Features\TaomSettings.cs:276: [SettingPropertyBool("Mixed Formations Debug Mode", Order = 3,
C:\Users\mikew\source\repos\TAOM\Main\Features\TaomSettings.cs:277: HintText = "Show diagnostic [MixedFormations] messages on the in-game HUD. Off = file log only.")]
C:\Users\mikew\source\repos\TAOM\Main\Features\TaomSettings.cs:282: [SettingPropertyGroup("Fief Management", GroupOrder = 26)]
C:\Users\mikew\source\repos\TAOM\Main\Features\TaomSettings.cs:283: [SettingPropertyBool("Enable Fief Management", Order = 0,
C:\Users\mikew\source\repos\TAOM\Main\Features\TaomSettings.cs:284: HintText = "Master toggle. When off, the F6 hotkey is inert and the carousel options are disabled. Effective immediately at runtime. Default: true.")]
C:\Users\mikew\source\repos\TAOM\Main\Features\TaomSettings.cs:287: [SettingPropertyGroup("Fief Management")]
C:\Users\mikew\source\repos\TAOM\Main\Features\TaomSettings.cs:288: [SettingPropertyBool("Allow Remote Building Queue", Order = 1,
C:\Users\mikew\source\repos\TAOM\Main\Features\TaomSettings.cs:289: HintText = "When on, you can manage any owned fief from anywhere via F6. When off, the Manage option is disabled unless you are physically at the selected fief. Default: true.")]
C:\Users\mikew\source\repos\TAOM\Main\Features\TaomSettings.cs:292: [SettingPropertyGroup("Fief Management")]
C:\Users\mikew\source\repos\TAOM\Main\Features\TaomSettings.cs:293: [SettingPropertyBool("Fief Management Debug Mode", Order = 2,
C:\Users\mikew\source\repos\TAOM\Main\Features\TaomSettings.cs:294: HintText = "Write diagnostic [FiefManagement] messages to the TAOM file log. Off = silent.")]
C:\Users\mikew\source\repos\TAOM\Main\Features\TaomSettings.cs:299: [SettingPropertyGroup("Inventory/Quick Actions", GroupOrder = 30)]
C:\Users\mikew\source\repos\TAOM\Main\Features\TaomSettings.cs:300: [SettingPropertyBool("Enable Quick Actions", Order = 0,
C:\Users\mikew\source\repos\TAOM\Main\Features\TaomSettings.cs:301: HintText = "Master toggle. When off, inventory 'Sell All' uses vanilla. When on, it opens a 4-option menu.")]
C:\Users\mikew\source\repos\TAOM\Main\Features\TaomSettings.cs:304: [SettingPropertyGroup("Inventory/Quick Actions")]
C:\Users\mikew\source\repos\TAOM\Main\Features\TaomSettings.cs:305: [SettingPropertyBool("Enable Inventory Search", Order = 1,
C:\Users\mikew\source\repos\TAOM\Main\Features\TaomSettings.cs:306: HintText = "Inventory search box visibility; persists per save and reconciles to MCM each campaign frame.")]
C:\Users\mikew\source\repos\TAOM\Main\Features\TaomSettings.cs:309: [SettingPropertyGroup("Inventory/Quick Actions/Sell Damaged", GroupOrder = 30)]
C:\Users\mikew\source\repos\TAOM\Main\Features\TaomSettings.cs:310: [SettingPropertyDropdown("Damage Threshold Preset", Order = 0,
C:\Users\mikew\source\repos\TAOM\Main\Features\TaomSettings.cs:311: HintText = "Items at or below this damage level are sold. Pristine = unused (sentinel). Default: Moderate (-20%).")]
C:\Users\mikew\source\repos\TAOM\Main\Features\TaomSettings.cs:315: [SettingPropertyGroup("Inventory/Quick Actions/Sell Damaged")]
C:\Users\mikew\source\repos\TAOM\Main\Features\TaomSettings.cs:316: [SettingPropertyFloatingInteger("Custom Damage Threshold", -1.0f, 0.0f, "#0.00", Order = 1,
C:\Users\mikew\source\repos\TAOM\Main\Features\TaomSettings.cs:317: HintText = "Custom threshold. Only used when 'Use Custom Threshold' is on. Default: -0.20.")]
C:\Users\mikew\source\repos\TAOM\Main\Features\TaomSettings.cs:320: [SettingPropertyGroup("Inventory/Quick Actions/Sell Damaged")]
C:\Users\mikew\source\repos\TAOM\Main\Features\TaomSettings.cs:321: [SettingPropertyBool("Use Custom Threshold", Order = 2, HintText = "Toggle dropdown vs custom value above.")]
C:\Users\mikew\source\repos\TAOM\Main\Features\TaomSettings.cs:324: [SettingPropertyGroup("Inventory/Quick Actions/Sell Damaged")]
C:\Users\mikew\source\repos\TAOM\Main\Features\TaomSettings.cs:325: [SettingPropertyBool("Sell Damaged Equipped", Order = 3, HintText = "Include items currently equipped on heroes.")]
C:\Users\mikew\source\repos\TAOM\Main\Features\TaomSettings.cs:328: [SettingPropertyGroup("Inventory/Quick Actions/Sell Damaged")]
C:\Users\mikew\source\repos\TAOM\Main\Features\TaomSettings.cs:329: [SettingPropertyBool("Exclude Damaged Horses", Order = 4, HintText = "Skip horses/mounts when selling damaged. Default: true.")]
C:\Users\mikew\source\repos\TAOM\Main\Features\TaomSettings.cs:332: [SettingPropertyGroup("Inventory/Quick Actions/Sell Low Value", GroupOrder = 31)]
C:\Users\mikew\source\repos\TAOM\Main\Features\TaomSettings.cs:333: [SettingPropertyInteger("Low Value Threshold (denars)", 1, 10000, Order = 0,
C:\Users\mikew\source\repos\TAOM\Main\Features\TaomSettings.cs:334: HintText = "Items at or below this denars value are sold. Default: 100.")]
C:\Users\mikew\source\repos\TAOM\Main\Features\TaomSettings.cs:337: [SettingPropertyGroup("Inventory/Quick Actions/Sell Low Value")]
C:\Users\mikew\source\repos\TAOM\Main\Features\TaomSettings.cs:338: [SettingPropertyBool("Sell Low Value Equipped", Order = 1, HintText = "Include items currently equipped. Default: false.")]
C:\Users\mikew\source\repos\TAOM\Main\Features\TaomSettings.cs:341: [SettingPropertyGroup("Inventory/Quick Actions/Sell Low Value")]
C:\Users\mikew\source\repos\TAOM\Main\Features\TaomSettings.cs:342: [SettingPropertyBool("Exclude Low Value Food", Order = 2, HintText = "Skip food items. Default: true.")]
C:\Users\mikew\source\repos\TAOM\Main\Features\TaomSettings.cs:345: [SettingPropertyGroup("Inventory/Quick Actions/Sell Low Value")]
C:\Users\mikew\source\repos\TAOM\Main\Features\TaomSettings.cs:346: [SettingPropertyBool("Exclude Low Value Horses", Order = 3, HintText = "Skip horses/mounts. Default: true.")]
C:\Users\mikew\source\repos\TAOM\Main\Features\TaomSettings.cs:349: [SettingPropertyGroup("Inventory/Quick Actions/Sell Low Value")]
C:\Users\mikew\source\repos\TAOM\Main\Features\TaomSettings.cs:350: [SettingPropertyBool("Exclude Low Value Trade Goods", Order = 4, HintText = "Skip trade goods. Default: false.")]
C:\Users\mikew\source\repos\TAOM\Main\Features\TaomSettings.cs:353: [SettingPropertyGroup("Inventory/Quick Actions/Misc", GroupOrder = 32)]
C:\Users\mikew\source\repos\TAOM\Main\Features\TaomSettings.cs:354: [SettingPropertyBool("Show Confirmation Dialog", Order = 0, HintText = "Ask for confirmation before bulk-selling. Default: true.")]
C:\Users\mikew\source\repos\TAOM\Main\Features\TaomSettings.cs:357: [SettingPropertyGroup("Inventory/Quick Actions/Misc")]
C:\Users\mikew\source\repos\TAOM\Main\Features\TaomSettings.cs:358: [SettingPropertyBool("Play Sounds", Order = 1, HintText = "Play 'event:/ui/transfer' chime after each batch action. Default: true.")]
C:\Users\mikew\source\repos\TAOM\Main\Features\TaomSettings.cs:361: [SettingPropertyGroup("Inventory/Quick Actions/Misc")]
C:\Users\mikew\source\repos\TAOM\Main\Features\TaomSettings.cs:362: [SettingPropertyBool("Quick Actions Debug Mode", Order = 2, HintText = "Show diagnostic [QuickActions] HUD messages. Off = file log only.")]
C:\Users\mikew\source\repos\TAOM\Main\Features\TaomSettings.cs:367: [SettingPropertyGroup("Inventory/Equipment Presets", GroupOrder = 33)]
C:\Users\mikew\source\repos\TAOM\Main\Features\TaomSettings.cs:368: [SettingPropertyBool("Enable Equipment Presets", Order = 0,
C:\Users\mikew\source\repos\TAOM\Main\Features\TaomSettings.cs:369: HintText = "Master toggle. When off, the Presets overlay is not added to the inventory screen and existing presets are inert (preserved in save).")]
C:\Users\mikew\source\repos\TAOM\Main\Features\TaomSettings.cs:372: [SettingPropertyGroup("Inventory/Equipment Presets")]
C:\Users\mikew\source\repos\TAOM\Main\Features\TaomSettings.cs:373: [SettingPropertyInteger("Max Presets Per Character", 1, 20, Order = 1,
C:\Users\mikew\source\repos\TAOM\Main\Features\TaomSettings.cs:374: HintText = "Maximum saved presets per hero. Default: 10.")]
C:\Users\mikew\source\repos\TAOM\Main\Features\TaomSettings.cs:377: [SettingPropertyGroup("Inventory/Equipment Presets")]
C:\Users\mikew\source\repos\TAOM\Main\Features\TaomSettings.cs:378: [SettingPropertyBool("Equipment Presets Debug Mode", Order = 2,
C:\Users\mikew\source\repos\TAOM\Main\Features\TaomSettings.cs:379: HintText = "Show diagnostic [EquipPresets] messages on the in-game HUD. Off = file log only.")]
C:\Users\mikew\source\repos\TAOM\Main\Features\TaomSettings.cs:384: [SettingPropertyGroup("Battle Tactics/Smart Cavalry", GroupOrder = 22)]
C:\Users\mikew\source\repos\TAOM\Main\Features\TaomSettings.cs:385: [SettingPropertyBool("Enable Smart Cavalry AI", Order = 0,
C:\Users\mikew\source\repos\TAOM\Main\Features\TaomSettings.cs:386: HintText = "Master toggle. When off, cavalry uses vanilla charge logic. When on, the player's cavalry formations execute coordinated line charges with passthrough + reform behavior. Default OFF while war-elephant interaction is being tuned.")]
C:\Users\mikew\source\repos\TAOM\Main\Features\TaomSettings.cs:389: [SettingPropertyGroup("Battle Tactics/Smart Cavalry")]
C:\Users\mikew\source\repos\TAOM\Main\Features\TaomSettings.cs:390: [SettingPropertyBool("Enable Friendly Collision Avoidance", Order = 1,
C:\Users\mikew\source\repos\TAOM\Main\Features\TaomSettings.cs:391: HintText = "When charging, cavalry will reroute around friendly infantry on the charge line. Off = vanilla collision behavior (cavalry trample friendly).")]
C:\Users\mikew\source\repos\TAOM\Main\Features\TaomSettings.cs:394: [SettingPropertyGroup("Battle Tactics/Smart Cavalry")]
C:\Users\mikew\source\repos\TAOM\Main\Features\TaomSettings.cs:395: [SettingPropertyFloatingInteger("Charge Formation Strictness", 0.0f, 1.0f, "#0.00", Order = 2,
C:\Users\mikew\source\repos\TAOM\Main\Features\TaomSettings.cs:396: HintText = "How tightly the cavalry line must form before charging AND before reform completes. 0 = launch immediately; 1 = wait until every unit is in perfect line. Default 0.7.")]
C:\Users\mikew\source\repos\TAOM\Main\Features\TaomSettings.cs:399: [SettingPropertyGroup("Battle Tactics/Smart Cavalry")]
C:\Users\mikew\source\repos\TAOM\Main\Features\TaomSettings.cs:400: [SettingPropertyFloatingInteger("Reform Distance After Charge", 10f, 80f, "#0", Order = 3,
C:\Users\mikew\source\repos\TAOM\Main\Features\TaomSettings.cs:401: HintText = "Meters past the target before cavalry reforms a new line. Larger = wider passthrough sweep. Default 25.")]
C:\Users\mikew\source\repos\TAOM\Main\Features\TaomSettings.cs:404: [SettingPropertyGroup("Battle Tactics/Smart Cavalry")]
C:\Users\mikew\source\repos\TAOM\Main\Features\TaomSettings.cs:405: [SettingPropertyFloatingInteger("Charge Line Spacing Multiplier", 0.8f, 3.0f, "#0.0", Order = 4,
C:\Users\mikew\source\repos\TAOM\Main\Features\TaomSettings.cs:406: HintText = "Multiplier on default unit spacing during line formation. 1.0 = vanilla. 1.2 (default) = slightly wider line for cleaner charge.")]
C:\Users\mikew\source\repos\TAOM\Main\Features\TaomSettings.cs:409: [SettingPropertyGroup("Battle Tactics/Smart Cavalry")]
C:\Users\mikew\source\repos\TAOM\Main\Features\TaomSettings.cs:410: [SettingPropertyBool("Smart Cavalry Debug Mode", Order = 5,
C:\Users\mikew\source\repos\TAOM\Main\Features\TaomSettings.cs:411: HintText = "Show diagnostic [SmartCavalryAI] state-transition messages on the in-game HUD. Off = file log only.")]
C:\Users\mikew\source\repos\TAOM\Main\Features\TaomSettings.cs:418: [SettingPropertyGroup("Battle Tactics/Companion Roles", GroupOrder = 27)]
C:\Users\mikew\source\repos\TAOM\Main\Features\TaomSettings.cs:419: [SettingPropertyBool("Enable Companion Role Tooltips", Order = 0,
C:\Users\mikew\source\repos\TAOM\Main\Features\TaomSettings.cs:420: HintText = "Append detected combat role (e.g., [BOW], [INF]) to companion/troop tooltips on the party screen.")]
C:\Users\mikew\source\repos\TAOM\Main\Features\TaomSettings.cs:423: [SettingPropertyGroup("Battle Tactics/Companion Roles")]
C:\Users\mikew\source\repos\TAOM\Main\Features\TaomSettings.cs:424: [SettingPropertyBool("Enable OOB Role Display", Order = 1,
C:\Users\mikew\source\repos\TAOM\Main\Features\TaomSettings.cs:425: HintText = "Show role indicators on hero items in the Order of Battle screen.")]
C:\Users\mikew\source\repos\TAOM\Main\Features\TaomSettings.cs:428: [SettingPropertyGroup("Battle Tactics/Companion Roles")]
C:\Users\mikew\source\repos\TAOM\Main\Features\TaomSettings.cs:429: [SettingPropertyBool("Companion Roles Debug Mode", Order = 2,
C:\Users\mikew\source\repos\TAOM\Main\Features\TaomSettings.cs:430: HintText = "Show diagnostic [CompanionRoles] messages on the in-game HUD.")]
C:\Users\mikew\source\repos\TAOM\Main\Features\TaomSettings.cs:435: [SettingPropertyGroup("Battle Tactics/Formation Presets", GroupOrder = 28)]
C:\Users\mikew\source\repos\TAOM\Main\Features\TaomSettings.cs:436: [SettingPropertyBool("Enable Formation Presets", Order = 0,
C:\Users\mikew\source\repos\TAOM\Main\Features\TaomSettings.cs:437: HintText = "Save/load named OOB hero-to-formation assignments per campaign.")]
C:\Users\mikew\source\repos\TAOM\Main\Features\TaomSettings.cs:440: [SettingPropertyGroup("Battle Tactics/Formation Presets")]
C:\Users\mikew\source\repos\TAOM\Main\Features\TaomSettings.cs:441: [SettingPropertyInteger("Max Formation Presets", 1, 20, Order = 1,
C:\Users\mikew\source\repos\TAOM\Main\Features\TaomSettings.cs:442: HintText = "Maximum saved formation presets per campaign. Save attempts beyond this limit are refused with a warning. Default: 10.")]
C:\Users\mikew\source\repos\TAOM\Main\Features\TaomSettings.cs:445: [SettingPropertyGroup("Battle Tactics/Formation Presets")]
C:\Users\mikew\source\repos\TAOM\Main\Features\TaomSettings.cs:446: [SettingPropertyBool("Formation Presets Debug Mode", Order = 2,
C:\Users\mikew\source\repos\TAOM\Main\Features\TaomSettings.cs:447: HintText = "Show diagnostic [FormationPresets] messages.")]
C:\Users\mikew\source\repos\TAOM\Main\Features\TaomSettings.cs:452: [SettingPropertyGroup("Battle Tactics/Battle Action Bar", GroupOrder = 29)]
C:\Users\mikew\source\repos\TAOM\Main\Features\TaomSettings.cs:453: [SettingPropertyBool("Enable Battle Action Bar", Order = 0,
C:\Users\mikew\source\repos\TAOM\Main\Features\TaomSettings.cs:454: HintText = "Show contextual action bar during field battles (1-9 hotkeys for stance toggles). Stances are display-only — they record state but do not change formation behavior.")]
C:\Users\mikew\source\repos\TAOM\Main\Features\TaomSettings.cs:457: [SettingPropertyGroup("Battle Tactics/Battle Action Bar")]
C:\Users\mikew\source\repos\TAOM\Main\Features\TaomSettings.cs:458: [SettingPropertyBool("Cancel Stance On Move", Order = 1,
C:\Users\mikew\source\repos\TAOM\Main\Features\TaomSettings.cs:459: HintText = "Auto-clear stance when the formation receives a movement order.")]
C:\Users\mikew\source\repos\TAOM\Main\Features\TaomSettings.cs:462: [SettingPropertyGroup("Battle Tactics/Battle Action Bar")]
C:\Users\mikew\source\repos\TAOM\Main\Features\TaomSettings.cs:463: [SettingPropertyBool("Enable Volley Fire", Order = 2,
C:\Users\mikew\source\repos\TAOM\Main\Features\TaomSettings.cs:464: HintText = "Include 'Volley Fire' as a ranged action option.")]
C:\Users\mikew\source\repos\TAOM\Main\Features\TaomSettings.cs:467: [SettingPropertyGroup("Battle Tactics/Battle Action Bar")]
C:\Users\mikew\source\repos\TAOM\Main\Features\TaomSettings.cs:468: [SettingPropertyBool("Battle Action Bar Debug Mode", Order = 3,
C:\Users\mikew\source\repos\TAOM\Main\Features\TaomSettings.cs:469: HintText = "Show diagnostic [BattleActionBar] messages.")]
C:\Users\mikew\source\repos\TAOM\Main\Features\TaomSettings.cs:476: [SettingPropertyGroup("World/Bandit Scaling", GroupOrder = 35)]
C:\Users\mikew\source\repos\TAOM\Main\Features\TaomSettings.cs:477: [SettingPropertyBool("Enable Bandit Scaling", Order = 0,
C:\Users\mikew\source\repos\TAOM\Main\Features\TaomSettings.cs:478: HintText = "Master toggle. When off, hideout density + bandit party sizes use vanilla values. When on, both scale with PlayerProgress (0.0 new campaign -> 1.0 endgame) per the curves below.")]
C:\Users\mikew\source\repos\TAOM\Main\Features\TaomSettings.cs:481: [SettingPropertyGroup("World/Bandit Scaling")]
C:\Users\mikew\source\repos\TAOM\Main\Features\TaomSettings.cs:482: [SettingPropertyFloatingInteger("Density Curve", 0.0f, 5.0f, "#0.0", Order = 1,
C:\Users\mikew\source\repos\TAOM\Main\Features\TaomSettings.cs:483: HintText = "Multiplier on hideout count + parties-per-hideout at PlayerProgress=1.0. Curve: 1 + curve * progress. 0 = vanilla density throughout. 1.5 (default) = up to 2.5x density in endgame.")]
C:\Users\mikew\source\repos\TAOM\Main\Features\TaomSettings.cs:486: [SettingPropertyGroup("World/Bandit Scaling")]
C:\Users\mikew\source\repos\TAOM\Main\Features\TaomSettings.cs:487: [SettingPropertyFloatingInteger("Party Size Curve", 0.0f, 5.0f, "#0.0", Order = 2,
C:\Users\mikew\source\repos\TAOM\Main\Features\TaomSettings.cs:488: HintText = "Multiplier on bandit party troop counts at PlayerProgress=1.0. Vanilla already scales 0.4 -> 1.2; this is a final multiplier on top. 1.5 (default) = up to 2.5x bandit party sizes in endgame.")]
C:\Users\mikew\source\repos\TAOM\Main\Features\TaomSettings.cs:491: [SettingPropertyGroup("World/Bandit Scaling")]
C:\Users\mikew\source\repos\TAOM\Main\Features\TaomSettings.cs:492: [SettingPropertyFloatingInteger("Boss Fight Curve", 0.0f, 5.0f, "#0.0", Order = 3,
C:\Users\mikew\source\repos\TAOM\Main\Features\TaomSettings.cs:493: HintText = "Multiplier on first-fight + boss-fight troop counts inside hideouts at PlayerProgress=1.0. 1.5 (default) = up to 2.5x bandits per hideout assault in endgame.")]
C:\Users\mikew\source\repos\TAOM\Main\Features\TaomSettings.cs:496: [SettingPropertyGroup("World/Bandit Scaling")]
C:\Users\mikew\source\repos\TAOM\Main\Features\TaomSettings.cs:497: [SettingPropertyInteger("Max Hideouts Per Faction Cap", 1, 100, Order = 4,
C:\Users\mikew\source\repos\TAOM\Main\Features\TaomSettings.cs:498: HintText = "Hard cap on hideouts per bandit faction regardless of scaling curve. Vanilla = 9. Default: 100 (effectively the physical hideout count per faction).")]
C:\Users\mikew\source\repos\TAOM\Main\Features\TaomSettings.cs:501: [SettingPropertyGroup("World/Bandit Scaling")]
C:\Users\mikew\source\repos\TAOM\Main\Features\TaomSettings.cs:502: [SettingPropertyInteger("Max Parties Per Hideout Cap", 1, 20, Order = 5,
C:\Users\mikew\source\repos\TAOM\Main\Features\TaomSettings.cs:503: HintText = "Hard cap on bandit parties per hideout regardless of scaling curve. Vanilla = 3. Default: 3.")]
C:\Users\mikew\source\repos\TAOM\Main\Features\TaomSettings.cs:506: [SettingPropertyGroup("World/Bandit Scaling")]
C:\Users\mikew\source\repos\TAOM\Main\Features\TaomSettings.cs:507: [SettingPropertyInteger("Initial Hideouts Per Faction", 1, 30, Order = 6,
C:\Users\mikew\source\repos\TAOM\Main\Features\TaomSettings.cs:508: HintText = "Hideouts each bandit faction starts with on a new campaign. Vanilla = 7. Default: 14. Higher = denser early game (the world settles toward the steady-state max as you clear them).")]
C:\Users\mikew\source\repos\TAOM\Main\Features\TaomSettings.cs:513: [SettingPropertyGroup("World/Recruitment Alignment", GroupOrder = 36)]
C:\Users\mikew\source\repos\TAOM\Main\Features\TaomSettings.cs:514: [SettingPropertyBool("Enable Recruitment Alignment Block", Order = 0,
C:\Users\mikew\source\repos\TAOM\Main\Features\TaomSettings.cs:515: HintText = "When enabled, a recruiter cannot recruit volunteers at a settlement controlled by an opposed-alignment kingdom (Free vs Evil). Alignment comes from execution/alignment.json, keyed by the kingdom you serve. Neutral factions (Umbar etc.) never block. When off, recruitment is vanilla.")]
C:\Users\mikew\source\repos\TAOM\Main\Features\TaomSettings.cs:518: [SettingPropertyGroup("World/Recruitment Alignment")]
C:\Users\mikew\source\repos\TAOM\Main\Features\TaomSettings.cs:519: [SettingPropertyBool("Only Good Rejects Evil", Order = 1,
C:\Users\mikew\source\repos\TAOM\Main\Features\TaomSettings.cs:520: HintText = "When ON, only a Free-aligned recruiter is blocked from Evil-controlled settlements; Evil recruiters may recruit anywhere. When OFF (default), the block is symmetric — Free and Evil each refuse the other.")]
C:\Users\mikew\source\repos\TAOM\Main\Features\TaomSettings.cs:523: [SettingPropertyGroup("World/Recruitment Alignment")]
C:\Users\mikew\source\repos\TAOM\Main\Features\TaomSettings.cs:524: [SettingPropertyBool("Apply To Player", Order = 2,
C:\Users\mikew\source\repos\TAOM\Main\Features\TaomSettings.cs:525: HintText = "When ON (default), YOU are blocked from recruiting in opposed-alignment settlements. When OFF, you may recruit anyone regardless of alignment (AI lords are still gated if 'Apply To AI Lords' is on). The master 'Enable Recruitment Alignment Block' toggle off disables the whole feature for everyone.")]
C:\Users\mikew\source\repos\TAOM\Main\Features\TaomSettings.cs:528: [SettingPropertyGroup("World/Recruitment Alignment")]
C:\Users\mikew\source\repos\TAOM\Main\Features\TaomSettings.cs:529: [SettingPropertyBool("Apply To AI Lords", Order = 3,
C:\Users\mikew\source\repos\TAOM\Main\Features\TaomSettings.cs:530: HintText = "When ON (default), AI lords are also blocked from recruiting in opposed-alignment settlements. When OFF, AI recruits freely (you are still gated if 'Apply To Player' is on).")]
C:\Users\mikew\source\repos\TAOM\Main\Features\TaomSettings.cs:535: [SettingPropertyGroup("Map UI/Settlement Nameplates", GroupOrder = 40)]
C:\Users\mikew\source\repos\TAOM\Main\Features\TaomSettings.cs:536: [SettingPropertyBool("Enable Settlement Nameplate Fade", Order = 0,
C:\Users\mikew\source\repos\TAOM\Main\Features\TaomSettings.cs:537: HintText = "Fade settlement nameplates with camera distance. When off, all nameplates display at full visibility regardless of distance (vanilla).")]
C:\Users\mikew\source\repos\TAOM\Main\Features\TaomSettings.cs:540: [SettingPropertyGroup("Map UI/Settlement Nameplates")]
C:\Users\mikew\source\repos\TAOM\Main\Features\TaomSettings.cs:541: [SettingPropertyFloatingInteger("Fade Start Distance", 5f, 500f, "#0", Order = 1,
C:\Users\mikew\source\repos\TAOM\Main\Features\TaomSettings.cs:542: HintText = "Camera distance at which fade begins. Nameplates closer than this stay fully opaque. Default 80.")]
C:\Users\mikew\source\repos\TAOM\Main\Features\TaomSettings.cs:545: [SettingPropertyGroup("Map UI/Settlement Nameplates")]
C:\Users\mikew\source\repos\TAOM\Main\Features\TaomSettings.cs:546: [SettingPropertyFloatingInteger("Fade End Distance", 10f, 1000f, "#0", Order = 2,
C:\Users\mikew\source\repos\TAOM\Main\Features\TaomSettings.cs:547: HintText = "Camera distance at which fade completes. Nameplates farther than this are fully hidden. Must be greater than Fade Start Distance. Default 200.")]
C:\Users\mikew\source\repos\TAOM\Main\Features\TaomSettings.cs:560: [SettingPropertyGroup("Map Tools/Distance Cache Rebuild", GroupOrder = 100)]
C:\Users\mikew\source\repos\TAOM\Main\Features\TaomSettings.cs:561: [SettingPropertyButton("Rebuild Settlement Distance Cache",
C:\Users\mikew\source\repos\TAOM\Main\Features\TaomSettings.cs:564: HintText = "Spawns a 10-30 minute background task that recomputes the settlement distance cache against the live map scene. Requires an active campaign. Game stays playable but pathfinding queries during the rebuild may be inconsistent — best run from main menu after loading a save.")]

 succeeded in 764ms:
C:\Users\mikew\source\repos\TAOM\Main\Core\Validation\FiniteFloatValidator.cs:19: public static class FiniteFloatValidator
C:\Users\mikew\source\repos\TAOM\Main\Core\Validation\FiniteFloatValidator.cs:22: public static bool IsFinite(float value) =>
C:\Users\mikew\source\repos\TAOM\Main\Core\Validation\FiniteFloatValidator.cs:26: public static bool IsFinite(double value) =>
C:\Users\mikew\source\repos\TAOM\Main\Core\Validation\FiniteFloatValidator.cs:33: public static bool IsFiniteInRange(float value, float min, float max) =>
C:\Users\mikew\source\repos\TAOM\Main\Core\Validation\FiniteFloatValidator.cs:34: IsFinite(value) && value >= min && value <= max;
C:\Users\mikew\source\repos\TAOM\Main\Core\Validation\FiniteFloatValidator.cs:42: IsFinite(value) && value <= max;
C:\Users\mikew\source\repos\TAOM\Main\Core\Validation\FiniteFloatValidator.cs:50: IsFinite(value) && value >= min;
C:\Users\mikew\source\repos\TAOM\Main\Core\Validation\FiniteFloatValidator.cs:57: public static bool IsFiniteInRange(double value, double min, double max) =>
C:\Users\mikew\source\repos\TAOM\Main\Core\Validation\FiniteFloatValidator.cs:58: IsFinite(value) && value >= min && value <= max;
C:\Users\mikew\source\repos\TAOM\Main\Core\Validation\FiniteFloatValidator.cs:62: IsFinite(value) && value <= max;
C:\Users\mikew\source\repos\TAOM\Main\Core\Validation\FiniteFloatValidator.cs:66: IsFinite(value) && value >= min;
C:\Users\mikew\source\repos\TAOM\Main\SceneScripts\CS_Road.cs:133: if (!FiniteFloatValidator.IsFinite(Width) || Width <= 0f)
C:\Users\mikew\source\repos\TAOM\Main\SceneScripts\CS_Road.cs:138: if (!FiniteFloatValidator.IsFinite(ElevationOffset))
C:\Users\mikew\source\repos\TAOM\Main\SceneScripts\CS_Road.cs:143: if (!FiniteFloatValidator.IsFinite(RepeatU) || !FiniteFloatValidator.IsFinite(RepeatV))
C:\Users\mikew\source\repos\TAOM\Main\SceneScripts\CS_Road.cs:175: if (!FiniteFloatValidator.IsFinite(totalDistance) || totalDistance <= 0f)

exec
"C:\\Program Files\\PowerShell\\7\\pwsh.exe" -Command '$i=0; Get-Content '"'Main/Features/TroopWeight/Hooks/PartyBase_NumberOfAllMembers_Patch.cs' | ForEach-Object { "'$i++; '"'{0}:{1}: {2}' -f (Resolve-Path 'Main/Features/TroopWeight/Hooks/PartyBase_NumberOfAllMembers_Patch.cs'),"'$i,$_ }' in C:\Users\mikew\source\repos\TAOM
exec
"C:\\Program Files\\PowerShell\\7\\pwsh.exe" -Command '$i=0; Get-Content '"'Main/Features/TroopWeight/Hooks/PartyBaseNumberOfAllMembersHook.cs' | ForEach-Object { "'$i++; '"'{0}:{1}: {2}' -f (Resolve-Path 'Main/Features/TroopWeight/Hooks/PartyBaseNumberOfAllMembersHook.cs'),"'$i,$_ }' in C:\Users\mikew\source\repos\TAOM
exec
"C:\\Program Files\\PowerShell\\7\\pwsh.exe" -Command '$i=0; Get-Content '"'Main/Features/TroopWeight/TroopWeightService.cs' | Select-Object -First 115 | ForEach-Object { "'$i++; '"'{0}:{1}: {2}' -f (Resolve-Path 'Main/Features/TroopWeight/TroopWeightService.cs'),"'$i,$_ }' in C:\Users\mikew\source\repos\TAOM
exec
"C:\\Program Files\\PowerShell\\7\\pwsh.exe" -Command '$i=0; Get-Content '"'Main/Features/TroopWeight/TroopWeightXmlLoader.cs' | ForEach-Object { "'$i++; '"'{0}:{1}: {2}' -f (Resolve-Path 'Main/Features/TroopWeight/TroopWeightXmlLoader.cs'),"'$i,$_ }' in C:\Users\mikew\source\repos\TAOM
 succeeded in 503ms:
C:\Users\mikew\source\repos\TAOM\Main\Features\TroopWeight\Hooks\PartyBase_NumberOfAllMembers_Patch.cs:1: using HarmonyLib;
C:\Users\mikew\source\repos\TAOM\Main\Features\TroopWeight\Hooks\PartyBase_NumberOfAllMembers_Patch.cs:2: using TaleWorlds.CampaignSystem.Party;
C:\Users\mikew\source\repos\TAOM\Main\Features\TroopWeight\Hooks\PartyBase_NumberOfAllMembers_Patch.cs:3: 
C:\Users\mikew\source\repos\TAOM\Main\Features\TroopWeight\Hooks\PartyBase_NumberOfAllMembers_Patch.cs:4: namespace TAOM.Features.TroopWeight.Hooks;
C:\Users\mikew\source\repos\TAOM\Main\Features\TroopWeight\Hooks\PartyBase_NumberOfAllMembers_Patch.cs:5: 
C:\Users\mikew\source\repos\TAOM\Main\Features\TroopWeight\Hooks\PartyBase_NumberOfAllMembers_Patch.cs:6: [HarmonyPatch(typeof(PartyBase), nameof(PartyBase.NumberOfAllMembers), MethodType.Getter)]
C:\Users\mikew\source\repos\TAOM\Main\Features\TroopWeight\Hooks\PartyBase_NumberOfAllMembers_Patch.cs:7: [HarmonyPatchCategory("Patch17_TroopWeight")]
C:\Users\mikew\source\repos\TAOM\Main\Features\TroopWeight\Hooks\PartyBase_NumberOfAllMembers_Patch.cs:8: public static class PartyBase_NumberOfAllMembers_Patch
C:\Users\mikew\source\repos\TAOM\Main\Features\TroopWeight\Hooks\PartyBase_NumberOfAllMembers_Patch.cs:9: {
C:\Users\mikew\source\repos\TAOM\Main\Features\TroopWeight\Hooks\PartyBase_NumberOfAllMembers_Patch.cs:10:     private static IOnPartyBaseNumberOfAllMembers? _hook;
C:\Users\mikew\source\repos\TAOM\Main\Features\TroopWeight\Hooks\PartyBase_NumberOfAllMembers_Patch.cs:11: 
C:\Users\mikew\source\repos\TAOM\Main\Features\TroopWeight\Hooks\PartyBase_NumberOfAllMembers_Patch.cs:12:     public static void Initialize(IOnPartyBaseNumberOfAllMembers hook) => _hook = hook;
C:\Users\mikew\source\repos\TAOM\Main\Features\TroopWeight\Hooks\PartyBase_NumberOfAllMembers_Patch.cs:13: 
C:\Users\mikew\source\repos\TAOM\Main\Features\TroopWeight\Hooks\PartyBase_NumberOfAllMembers_Patch.cs:14:     [HarmonyPostfix]
C:\Users\mikew\source\repos\TAOM\Main\Features\TroopWeight\Hooks\PartyBase_NumberOfAllMembers_Patch.cs:15:     public static void Postfix(PartyBase __instance, ref int __result)
C:\Users\mikew\source\repos\TAOM\Main\Features\TroopWeight\Hooks\PartyBase_NumberOfAllMembers_Patch.cs:16:     {
C:\Users\mikew\source\repos\TAOM\Main\Features\TroopWeight\Hooks\PartyBase_NumberOfAllMembers_Patch.cs:17:         if (!(TaomSettings.Instance?.EnableTroopWeight ?? true)) return;
C:\Users\mikew\source\repos\TAOM\Main\Features\TroopWeight\Hooks\PartyBase_NumberOfAllMembers_Patch.cs:18:         _hook?.OnPartyBaseNumberOfAllMembers(__instance, ref __result);
C:\Users\mikew\source\repos\TAOM\Main\Features\TroopWeight\Hooks\PartyBase_NumberOfAllMembers_Patch.cs:19:     }
C:\Users\mikew\source\repos\TAOM\Main\Features\TroopWeight\Hooks\PartyBase_NumberOfAllMembers_Patch.cs:20: }

 succeeded in 537ms:
C:\Users\mikew\source\repos\TAOM\Main\Features\TroopWeight\Hooks\PartyBaseNumberOfAllMembersHook.cs:1: using System;
C:\Users\mikew\source\repos\TAOM\Main\Features\TroopWeight\Hooks\PartyBaseNumberOfAllMembersHook.cs:2: using System.Collections.Generic;
C:\Users\mikew\source\repos\TAOM\Main\Features\TroopWeight\Hooks\PartyBaseNumberOfAllMembersHook.cs:3: using TAOM.Core.Logging;
C:\Users\mikew\source\repos\TAOM\Main\Features\TroopWeight\Hooks\PartyBaseNumberOfAllMembersHook.cs:4: using TaleWorlds.CampaignSystem.Party;
C:\Users\mikew\source\repos\TAOM\Main\Features\TroopWeight\Hooks\PartyBaseNumberOfAllMembersHook.cs:5: 
C:\Users\mikew\source\repos\TAOM\Main\Features\TroopWeight\Hooks\PartyBaseNumberOfAllMembersHook.cs:6: namespace TAOM.Features.TroopWeight.Hooks;
C:\Users\mikew\source\repos\TAOM\Main\Features\TroopWeight\Hooks\PartyBaseNumberOfAllMembersHook.cs:7: 
C:\Users\mikew\source\repos\TAOM\Main\Features\TroopWeight\Hooks\PartyBaseNumberOfAllMembersHook.cs:8: public class PartyBaseNumberOfAllMembersHook : IOnPartyBaseNumberOfAllMembers
C:\Users\mikew\source\repos\TAOM\Main\Features\TroopWeight\Hooks\PartyBaseNumberOfAllMembersHook.cs:9: {
C:\Users\mikew\source\repos\TAOM\Main\Features\TroopWeight\Hooks\PartyBaseNumberOfAllMembersHook.cs:10:     private readonly ITroopWeightService _troopWeightService;
C:\Users\mikew\source\repos\TAOM\Main\Features\TroopWeight\Hooks\PartyBaseNumberOfAllMembersHook.cs:11:     private readonly Dictionary<int, (int Version, int WeightedResult)> _cache = new();
C:\Users\mikew\source\repos\TAOM\Main\Features\TroopWeight\Hooks\PartyBaseNumberOfAllMembersHook.cs:12: 
C:\Users\mikew\source\repos\TAOM\Main\Features\TroopWeight\Hooks\PartyBaseNumberOfAllMembersHook.cs:13:     public PartyBaseNumberOfAllMembersHook(ITroopWeightService troopWeightService, IModLogger logger)
C:\Users\mikew\source\repos\TAOM\Main\Features\TroopWeight\Hooks\PartyBaseNumberOfAllMembersHook.cs:14:     {
C:\Users\mikew\source\repos\TAOM\Main\Features\TroopWeight\Hooks\PartyBaseNumberOfAllMembersHook.cs:15:         _troopWeightService = troopWeightService;
C:\Users\mikew\source\repos\TAOM\Main\Features\TroopWeight\Hooks\PartyBaseNumberOfAllMembersHook.cs:16:     }
C:\Users\mikew\source\repos\TAOM\Main\Features\TroopWeight\Hooks\PartyBaseNumberOfAllMembersHook.cs:17: 
C:\Users\mikew\source\repos\TAOM\Main\Features\TroopWeight\Hooks\PartyBaseNumberOfAllMembersHook.cs:18:     public void OnPartyBaseNumberOfAllMembers(PartyBase partyBase, ref int __result)
C:\Users\mikew\source\repos\TAOM\Main\Features\TroopWeight\Hooks\PartyBaseNumberOfAllMembersHook.cs:19:     {
C:\Users\mikew\source\repos\TAOM\Main\Features\TroopWeight\Hooks\PartyBaseNumberOfAllMembersHook.cs:20:         try
C:\Users\mikew\source\repos\TAOM\Main\Features\TroopWeight\Hooks\PartyBaseNumberOfAllMembersHook.cs:21:         {
C:\Users\mikew\source\repos\TAOM\Main\Features\TroopWeight\Hooks\PartyBaseNumberOfAllMembersHook.cs:22:             if (partyBase?.MemberRoster == null)
C:\Users\mikew\source\repos\TAOM\Main\Features\TroopWeight\Hooks\PartyBaseNumberOfAllMembersHook.cs:23:                 return;
C:\Users\mikew\source\repos\TAOM\Main\Features\TroopWeight\Hooks\PartyBaseNumberOfAllMembersHook.cs:24: 
C:\Users\mikew\source\repos\TAOM\Main\Features\TroopWeight\Hooks\PartyBaseNumberOfAllMembersHook.cs:25:             int cacheKey = partyBase.GetHashCode();
C:\Users\mikew\source\repos\TAOM\Main\Features\TroopWeight\Hooks\PartyBaseNumberOfAllMembersHook.cs:26:             int currentVersion = partyBase.MemberRoster.VersionNo;
C:\Users\mikew\source\repos\TAOM\Main\Features\TroopWeight\Hooks\PartyBaseNumberOfAllMembersHook.cs:27: 
C:\Users\mikew\source\repos\TAOM\Main\Features\TroopWeight\Hooks\PartyBaseNumberOfAllMembersHook.cs:28:             if (_cache.TryGetValue(cacheKey, out var cached) && cached.Version == currentVersion)
C:\Users\mikew\source\repos\TAOM\Main\Features\TroopWeight\Hooks\PartyBaseNumberOfAllMembersHook.cs:29:             {
C:\Users\mikew\source\repos\TAOM\Main\Features\TroopWeight\Hooks\PartyBaseNumberOfAllMembersHook.cs:30:                 if (cached.WeightedResult > __result)
C:\Users\mikew\source\repos\TAOM\Main\Features\TroopWeight\Hooks\PartyBaseNumberOfAllMembersHook.cs:31:                     __result = cached.WeightedResult;
C:\Users\mikew\source\repos\TAOM\Main\Features\TroopWeight\Hooks\PartyBaseNumberOfAllMembersHook.cs:32:                 return;
C:\Users\mikew\source\repos\TAOM\Main\Features\TroopWeight\Hooks\PartyBaseNumberOfAllMembersHook.cs:33:             }
C:\Users\mikew\source\repos\TAOM\Main\Features\TroopWeight\Hooks\PartyBaseNumberOfAllMembersHook.cs:34: 
C:\Users\mikew\source\repos\TAOM\Main\Features\TroopWeight\Hooks\PartyBaseNumberOfAllMembersHook.cs:35:             var weightedCount = _troopWeightService.CalculateWeightedMemberCount(partyBase);
C:\Users\mikew\source\repos\TAOM\Main\Features\TroopWeight\Hooks\PartyBaseNumberOfAllMembersHook.cs:36:             var weightedResult = (int)Math.Ceiling(weightedCount);
C:\Users\mikew\source\repos\TAOM\Main\Features\TroopWeight\Hooks\PartyBaseNumberOfAllMembersHook.cs:37: 
C:\Users\mikew\source\repos\TAOM\Main\Features\TroopWeight\Hooks\PartyBaseNumberOfAllMembersHook.cs:38:             _cache[cacheKey] = (currentVersion, weightedResult);
C:\Users\mikew\source\repos\TAOM\Main\Features\TroopWeight\Hooks\PartyBaseNumberOfAllMembersHook.cs:39: 
C:\Users\mikew\source\repos\TAOM\Main\Features\TroopWeight\Hooks\PartyBaseNumberOfAllMembersHook.cs:40:             if (weightedResult > __result)
C:\Users\mikew\source\repos\TAOM\Main\Features\TroopWeight\Hooks\PartyBaseNumberOfAllMembersHook.cs:41:                 __result = weightedResult;
C:\Users\mikew\source\repos\TAOM\Main\Features\TroopWeight\Hooks\PartyBaseNumberOfAllMembersHook.cs:42:         }
C:\Users\mikew\source\repos\TAOM\Main\Features\TroopWeight\Hooks\PartyBaseNumberOfAllMembersHook.cs:43:         catch
C:\Users\mikew\source\repos\TAOM\Main\Features\TroopWeight\Hooks\PartyBaseNumberOfAllMembersHook.cs:44:         {
C:\Users\mikew\source\repos\TAOM\Main\Features\TroopWeight\Hooks\PartyBaseNumberOfAllMembersHook.cs:45:         }
C:\Users\mikew\source\repos\TAOM\Main\Features\TroopWeight\Hooks\PartyBaseNumberOfAllMembersHook.cs:46:     }
C:\Users\mikew\source\repos\TAOM\Main\Features\TroopWeight\Hooks\PartyBaseNumberOfAllMembersHook.cs:47: }

 succeeded in 526ms:
C:\Users\mikew\source\repos\TAOM\Main\Features\TroopWeight\TroopWeightXmlLoader.cs:1: using System;
C:\Users\mikew\source\repos\TAOM\Main\Features\TroopWeight\TroopWeightXmlLoader.cs:2: using System.Collections.Generic;
C:\Users\mikew\source\repos\TAOM\Main\Features\TroopWeight\TroopWeightXmlLoader.cs:3: using System.Globalization;
C:\Users\mikew\source\repos\TAOM\Main\Features\TroopWeight\TroopWeightXmlLoader.cs:4: using System.IO;
C:\Users\mikew\source\repos\TAOM\Main\Features\TroopWeight\TroopWeightXmlLoader.cs:5: using System.Xml;
C:\Users\mikew\source\repos\TAOM\Main\Features\TroopWeight\TroopWeightXmlLoader.cs:6: using TAOM.Core.Infrastructure;
C:\Users\mikew\source\repos\TAOM\Main\Features\TroopWeight\TroopWeightXmlLoader.cs:7: using TAOM.Core.Logging;
C:\Users\mikew\source\repos\TAOM\Main\Features\TroopWeight\TroopWeightXmlLoader.cs:8: 
C:\Users\mikew\source\repos\TAOM\Main\Features\TroopWeight\TroopWeightXmlLoader.cs:9: namespace TAOM.Features.TroopWeight;
C:\Users\mikew\source\repos\TAOM\Main\Features\TroopWeight\TroopWeightXmlLoader.cs:10: 
C:\Users\mikew\source\repos\TAOM\Main\Features\TroopWeight\TroopWeightXmlLoader.cs:11: public class TroopWeightXmlLoader : ITroopWeightXmlLoader
C:\Users\mikew\source\repos\TAOM\Main\Features\TroopWeight\TroopWeightXmlLoader.cs:12: {
C:\Users\mikew\source\repos\TAOM\Main\Features\TroopWeight\TroopWeightXmlLoader.cs:13:     private readonly IPathService _pathService;
C:\Users\mikew\source\repos\TAOM\Main\Features\TroopWeight\TroopWeightXmlLoader.cs:14:     private readonly IModLogger _logger;
C:\Users\mikew\source\repos\TAOM\Main\Features\TroopWeight\TroopWeightXmlLoader.cs:15:     private Dictionary<string, float> _troopWeights;
C:\Users\mikew\source\repos\TAOM\Main\Features\TroopWeight\TroopWeightXmlLoader.cs:16:     private bool _isLoaded;
C:\Users\mikew\source\repos\TAOM\Main\Features\TroopWeight\TroopWeightXmlLoader.cs:17: 
C:\Users\mikew\source\repos\TAOM\Main\Features\TroopWeight\TroopWeightXmlLoader.cs:18:     public TroopWeightXmlLoader(IPathService pathService, IModLogger logger)
C:\Users\mikew\source\repos\TAOM\Main\Features\TroopWeight\TroopWeightXmlLoader.cs:19:     {
C:\Users\mikew\source\repos\TAOM\Main\Features\TroopWeight\TroopWeightXmlLoader.cs:20:         _pathService = pathService;
C:\Users\mikew\source\repos\TAOM\Main\Features\TroopWeight\TroopWeightXmlLoader.cs:21:         _logger = logger;
C:\Users\mikew\source\repos\TAOM\Main\Features\TroopWeight\TroopWeightXmlLoader.cs:22:     }
C:\Users\mikew\source\repos\TAOM\Main\Features\TroopWeight\TroopWeightXmlLoader.cs:23: 
C:\Users\mikew\source\repos\TAOM\Main\Features\TroopWeight\TroopWeightXmlLoader.cs:24:     public Dictionary<string, float> GetTroopWeights()
C:\Users\mikew\source\repos\TAOM\Main\Features\TroopWeight\TroopWeightXmlLoader.cs:25:     {
C:\Users\mikew\source\repos\TAOM\Main\Features\TroopWeight\TroopWeightXmlLoader.cs:26:         if (!_isLoaded)
C:\Users\mikew\source\repos\TAOM\Main\Features\TroopWeight\TroopWeightXmlLoader.cs:27:             LoadTroopWeights();
C:\Users\mikew\source\repos\TAOM\Main\Features\TroopWeight\TroopWeightXmlLoader.cs:28: 
C:\Users\mikew\source\repos\TAOM\Main\Features\TroopWeight\TroopWeightXmlLoader.cs:29:         return _troopWeights;
C:\Users\mikew\source\repos\TAOM\Main\Features\TroopWeight\TroopWeightXmlLoader.cs:30:     }
C:\Users\mikew\source\repos\TAOM\Main\Features\TroopWeight\TroopWeightXmlLoader.cs:31: 
C:\Users\mikew\source\repos\TAOM\Main\Features\TroopWeight\TroopWeightXmlLoader.cs:32:     public void ReloadWeights()
C:\Users\mikew\source\repos\TAOM\Main\Features\TroopWeight\TroopWeightXmlLoader.cs:33:     {
C:\Users\mikew\source\repos\TAOM\Main\Features\TroopWeight\TroopWeightXmlLoader.cs:34:         _isLoaded = false;
C:\Users\mikew\source\repos\TAOM\Main\Features\TroopWeight\TroopWeightXmlLoader.cs:35:         LoadTroopWeights();
C:\Users\mikew\source\repos\TAOM\Main\Features\TroopWeight\TroopWeightXmlLoader.cs:36:     }
C:\Users\mikew\source\repos\TAOM\Main\Features\TroopWeight\TroopWeightXmlLoader.cs:37: 
C:\Users\mikew\source\repos\TAOM\Main\Features\TroopWeight\TroopWeightXmlLoader.cs:38:     private void LoadTroopWeights()
C:\Users\mikew\source\repos\TAOM\Main\Features\TroopWeight\TroopWeightXmlLoader.cs:39:     {
C:\Users\mikew\source\repos\TAOM\Main\Features\TroopWeight\TroopWeightXmlLoader.cs:40:         var xmlPath = Path.Combine(_pathService.ModuleDataPath, "TroopWeights", "troop_weights.xml");
C:\Users\mikew\source\repos\TAOM\Main\Features\TroopWeight\TroopWeightXmlLoader.cs:41:         var weights = new Dictionary<string, float>(StringComparer.OrdinalIgnoreCase);
C:\Users\mikew\source\repos\TAOM\Main\Features\TroopWeight\TroopWeightXmlLoader.cs:42: 
C:\Users\mikew\source\repos\TAOM\Main\Features\TroopWeight\TroopWeightXmlLoader.cs:43:         if (!File.Exists(xmlPath))
C:\Users\mikew\source\repos\TAOM\Main\Features\TroopWeight\TroopWeightXmlLoader.cs:44:         {
C:\Users\mikew\source\repos\TAOM\Main\Features\TroopWeight\TroopWeightXmlLoader.cs:45:             _logger.LogWarning($"Troop weights XML not found at: {xmlPath} — all troops will use default weight 1.0");
C:\Users\mikew\source\repos\TAOM\Main\Features\TroopWeight\TroopWeightXmlLoader.cs:46:             _troopWeights = weights;
C:\Users\mikew\source\repos\TAOM\Main\Features\TroopWeight\TroopWeightXmlLoader.cs:47:             _isLoaded = true;
C:\Users\mikew\source\repos\TAOM\Main\Features\TroopWeight\TroopWeightXmlLoader.cs:48:             return;
C:\Users\mikew\source\repos\TAOM\Main\Features\TroopWeight\TroopWeightXmlLoader.cs:49:         }
C:\Users\mikew\source\repos\TAOM\Main\Features\TroopWeight\TroopWeightXmlLoader.cs:50: 
C:\Users\mikew\source\repos\TAOM\Main\Features\TroopWeight\TroopWeightXmlLoader.cs:51:         try
C:\Users\mikew\source\repos\TAOM\Main\Features\TroopWeight\TroopWeightXmlLoader.cs:52:         {
C:\Users\mikew\source\repos\TAOM\Main\Features\TroopWeight\TroopWeightXmlLoader.cs:53:             var doc = new XmlDocument();
C:\Users\mikew\source\repos\TAOM\Main\Features\TroopWeight\TroopWeightXmlLoader.cs:54:             doc.Load(xmlPath);
C:\Users\mikew\source\repos\TAOM\Main\Features\TroopWeight\TroopWeightXmlLoader.cs:55: 
C:\Users\mikew\source\repos\TAOM\Main\Features\TroopWeight\TroopWeightXmlLoader.cs:56:             var weightNodes = doc.SelectNodes("//TroopWeight");
C:\Users\mikew\source\repos\TAOM\Main\Features\TroopWeight\TroopWeightXmlLoader.cs:57:             if (weightNodes == null)
C:\Users\mikew\source\repos\TAOM\Main\Features\TroopWeight\TroopWeightXmlLoader.cs:58:             {
C:\Users\mikew\source\repos\TAOM\Main\Features\TroopWeight\TroopWeightXmlLoader.cs:59:                 _troopWeights = weights;
C:\Users\mikew\source\repos\TAOM\Main\Features\TroopWeight\TroopWeightXmlLoader.cs:60:                 _isLoaded = true;
C:\Users\mikew\source\repos\TAOM\Main\Features\TroopWeight\TroopWeightXmlLoader.cs:61:                 return;
C:\Users\mikew\source\repos\TAOM\Main\Features\TroopWeight\TroopWeightXmlLoader.cs:62:             }
C:\Users\mikew\source\repos\TAOM\Main\Features\TroopWeight\TroopWeightXmlLoader.cs:63: 
C:\Users\mikew\source\repos\TAOM\Main\Features\TroopWeight\TroopWeightXmlLoader.cs:64:             foreach (XmlNode node in weightNodes)
C:\Users\mikew\source\repos\TAOM\Main\Features\TroopWeight\TroopWeightXmlLoader.cs:65:             {
C:\Users\mikew\source\repos\TAOM\Main\Features\TroopWeight\TroopWeightXmlLoader.cs:66:                 var id = node.Attributes?["id"]?.Value;
C:\Users\mikew\source\repos\TAOM\Main\Features\TroopWeight\TroopWeightXmlLoader.cs:67:                 var weightStr = node.Attributes?["weight"]?.Value;
C:\Users\mikew\source\repos\TAOM\Main\Features\TroopWeight\TroopWeightXmlLoader.cs:68: 
C:\Users\mikew\source\repos\TAOM\Main\Features\TroopWeight\TroopWeightXmlLoader.cs:69:                 if (string.IsNullOrEmpty(id))
C:\Users\mikew\source\repos\TAOM\Main\Features\TroopWeight\TroopWeightXmlLoader.cs:70:                 {
C:\Users\mikew\source\repos\TAOM\Main\Features\TroopWeight\TroopWeightXmlLoader.cs:71:                     _logger.LogWarning("TroopWeight element missing required 'id' attribute — skipping");
C:\Users\mikew\source\repos\TAOM\Main\Features\TroopWeight\TroopWeightXmlLoader.cs:72:                     continue;
C:\Users\mikew\source\repos\TAOM\Main\Features\TroopWeight\TroopWeightXmlLoader.cs:73:                 }
C:\Users\mikew\source\repos\TAOM\Main\Features\TroopWeight\TroopWeightXmlLoader.cs:74: 
C:\Users\mikew\source\repos\TAOM\Main\Features\TroopWeight\TroopWeightXmlLoader.cs:75:                 if (!float.TryParse(weightStr, NumberStyles.Float, CultureInfo.InvariantCulture, out var weight))
C:\Users\mikew\source\repos\TAOM\Main\Features\TroopWeight\TroopWeightXmlLoader.cs:76:                 {
C:\Users\mikew\source\repos\TAOM\Main\Features\TroopWeight\TroopWeightXmlLoader.cs:77:                     _logger.LogWarning($"Invalid weight value '{weightStr}' for troop '{id}' — skipping");
C:\Users\mikew\source\repos\TAOM\Main\Features\TroopWeight\TroopWeightXmlLoader.cs:78:                     continue;
C:\Users\mikew\source\repos\TAOM\Main\Features\TroopWeight\TroopWeightXmlLoader.cs:79:                 }
C:\Users\mikew\source\repos\TAOM\Main\Features\TroopWeight\TroopWeightXmlLoader.cs:80: 
C:\Users\mikew\source\repos\TAOM\Main\Features\TroopWeight\TroopWeightXmlLoader.cs:81:                 if (weight <= 0)
C:\Users\mikew\source\repos\TAOM\Main\Features\TroopWeight\TroopWeightXmlLoader.cs:82:                 {
C:\Users\mikew\source\repos\TAOM\Main\Features\TroopWeight\TroopWeightXmlLoader.cs:83:                     _logger.LogWarning($"Weight must be positive for troop '{id}' (got {weight}) — skipping");
C:\Users\mikew\source\repos\TAOM\Main\Features\TroopWeight\TroopWeightXmlLoader.cs:84:                     continue;
C:\Users\mikew\source\repos\TAOM\Main\Features\TroopWeight\TroopWeightXmlLoader.cs:85:                 }
C:\Users\mikew\source\repos\TAOM\Main\Features\TroopWeight\TroopWeightXmlLoader.cs:86: 
C:\Users\mikew\source\repos\TAOM\Main\Features\TroopWeight\TroopWeightXmlLoader.cs:87:                 if (weights.TryGetValue(id, out _))
C:\Users\mikew\source\repos\TAOM\Main\Features\TroopWeight\TroopWeightXmlLoader.cs:88:                     _logger.LogWarning($"Duplicate troop ID '{id}' — using last value");
C:\Users\mikew\source\repos\TAOM\Main\Features\TroopWeight\TroopWeightXmlLoader.cs:89: 
C:\Users\mikew\source\repos\TAOM\Main\Features\TroopWeight\TroopWeightXmlLoader.cs:90:                 weights[id] = weight;
C:\Users\mikew\source\repos\TAOM\Main\Features\TroopWeight\TroopWeightXmlLoader.cs:91:             }
C:\Users\mikew\source\repos\TAOM\Main\Features\TroopWeight\TroopWeightXmlLoader.cs:92: 
C:\Users\mikew\source\repos\TAOM\Main\Features\TroopWeight\TroopWeightXmlLoader.cs:93:             _logger.LogInfo($"Loaded {weights.Count} troop weights from XML");
C:\Users\mikew\source\repos\TAOM\Main\Features\TroopWeight\TroopWeightXmlLoader.cs:94:         }
C:\Users\mikew\source\repos\TAOM\Main\Features\TroopWeight\TroopWeightXmlLoader.cs:95:         catch (Exception ex)
C:\Users\mikew\source\repos\TAOM\Main\Features\TroopWeight\TroopWeightXmlLoader.cs:96:         {
C:\Users\mikew\source\repos\TAOM\Main\Features\TroopWeight\TroopWeightXmlLoader.cs:97:             _logger.LogError($"Failed to load troop weights: {ex.Message}");
C:\Users\mikew\source\repos\TAOM\Main\Features\TroopWeight\TroopWeightXmlLoader.cs:98:         }
C:\Users\mikew\source\repos\TAOM\Main\Features\TroopWeight\TroopWeightXmlLoader.cs:99: 
C:\Users\mikew\source\repos\TAOM\Main\Features\TroopWeight\TroopWeightXmlLoader.cs:100:         _troopWeights = weights;
C:\Users\mikew\source\repos\TAOM\Main\Features\TroopWeight\TroopWeightXmlLoader.cs:101:         _isLoaded = true;
C:\Users\mikew\source\repos\TAOM\Main\Features\TroopWeight\TroopWeightXmlLoader.cs:102:     }
C:\Users\mikew\source\repos\TAOM\Main\Features\TroopWeight\TroopWeightXmlLoader.cs:103: }

 succeeded in 577ms:
C:\Users\mikew\source\repos\TAOM\Main\Features\TroopWeight\TroopWeightService.cs:1: using System;
C:\Users\mikew\source\repos\TAOM\Main\Features\TroopWeight\TroopWeightService.cs:2: using System.Collections.Generic;
C:\Users\mikew\source\repos\TAOM\Main\Features\TroopWeight\TroopWeightService.cs:3: using System.Runtime.CompilerServices;
C:\Users\mikew\source\repos\TAOM\Main\Features\TroopWeight\TroopWeightService.cs:4: using TAOM.Core.Logging;
C:\Users\mikew\source\repos\TAOM\Main\Features\TroopWeight\TroopWeightService.cs:5: using TaleWorlds.CampaignSystem;
C:\Users\mikew\source\repos\TAOM\Main\Features\TroopWeight\TroopWeightService.cs:6: using TaleWorlds.CampaignSystem.Party;
C:\Users\mikew\source\repos\TAOM\Main\Features\TroopWeight\TroopWeightService.cs:7: using TaleWorlds.CampaignSystem.Roster;
C:\Users\mikew\source\repos\TAOM\Main\Features\TroopWeight\TroopWeightService.cs:8: 
C:\Users\mikew\source\repos\TAOM\Main\Features\TroopWeight\TroopWeightService.cs:9: namespace TAOM.Features.TroopWeight;
C:\Users\mikew\source\repos\TAOM\Main\Features\TroopWeight\TroopWeightService.cs:10: 
C:\Users\mikew\source\repos\TAOM\Main\Features\TroopWeight\TroopWeightService.cs:11: public class TroopWeightService : ITroopWeightService
C:\Users\mikew\source\repos\TAOM\Main\Features\TroopWeight\TroopWeightService.cs:12: {
C:\Users\mikew\source\repos\TAOM\Main\Features\TroopWeight\TroopWeightService.cs:13:     private readonly IModLogger _logger;
C:\Users\mikew\source\repos\TAOM\Main\Features\TroopWeight\TroopWeightService.cs:14:     private readonly ITroopWeightXmlLoader _xmlLoader;
C:\Users\mikew\source\repos\TAOM\Main\Features\TroopWeight\TroopWeightService.cs:15:     private Dictionary<string, float> _weights;
C:\Users\mikew\source\repos\TAOM\Main\Features\TroopWeight\TroopWeightService.cs:16: 
C:\Users\mikew\source\repos\TAOM\Main\Features\TroopWeight\TroopWeightService.cs:17:     // Per-party weighted (healthy, wounded) cache for the display surfaces. GetWeightedHealthAndWounded
C:\Users\mikew\source\repos\TAOM\Main\Features\TroopWeight\TroopWeightService.cs:18:     // is called on the nameplate path (PartyBaseHelper.GetPartySizeText) for every visible party each
C:\Users\mikew\source\repos\TAOM\Main\Features\TroopWeight\TroopWeightService.cs:19:     // refresh, and an O(n) roster walk per call adds up. Reference-keyed + auto-evicting on party GC
C:\Users\mikew\source\repos\TAOM\Main\Features\TroopWeight\TroopWeightService.cs:20:     // (no GetHashCode collisions, no unbounded growth — unlike the hashcode-dict caches in the count hooks).
C:\Users\mikew\source\repos\TAOM\Main\Features\TroopWeight\TroopWeightService.cs:21:     private readonly ConditionalWeakTable<PartyBase, WeightedHealthBox> _healthCache = new();
C:\Users\mikew\source\repos\TAOM\Main\Features\TroopWeight\TroopWeightService.cs:22: 
C:\Users\mikew\source\repos\TAOM\Main\Features\TroopWeight\TroopWeightService.cs:23:     private sealed class WeightedHealthBox
C:\Users\mikew\source\repos\TAOM\Main\Features\TroopWeight\TroopWeightService.cs:24:     {
C:\Users\mikew\source\repos\TAOM\Main\Features\TroopWeight\TroopWeightService.cs:25:         public int Version = -1; // VersionNo is >= 0, so -1 forces a compute on first access
C:\Users\mikew\source\repos\TAOM\Main\Features\TroopWeight\TroopWeightService.cs:26:         public int Healthy;
C:\Users\mikew\source\repos\TAOM\Main\Features\TroopWeight\TroopWeightService.cs:27:         public int Wounded;
C:\Users\mikew\source\repos\TAOM\Main\Features\TroopWeight\TroopWeightService.cs:28:     }
C:\Users\mikew\source\repos\TAOM\Main\Features\TroopWeight\TroopWeightService.cs:29: 
C:\Users\mikew\source\repos\TAOM\Main\Features\TroopWeight\TroopWeightService.cs:30:     public TroopWeightService(IModLogger logger, ITroopWeightXmlLoader xmlLoader)
C:\Users\mikew\source\repos\TAOM\Main\Features\TroopWeight\TroopWeightService.cs:31:     {
C:\Users\mikew\source\repos\TAOM\Main\Features\TroopWeight\TroopWeightService.cs:32:         _logger = logger;
C:\Users\mikew\source\repos\TAOM\Main\Features\TroopWeight\TroopWeightService.cs:33:         _xmlLoader = xmlLoader;
C:\Users\mikew\source\repos\TAOM\Main\Features\TroopWeight\TroopWeightService.cs:34:         _weights = xmlLoader.GetTroopWeights();
C:\Users\mikew\source\repos\TAOM\Main\Features\TroopWeight\TroopWeightService.cs:35:         _logger.LogInfo($"[TroopWeight] Service initialized with {_weights.Count} weighted troop definitions");
C:\Users\mikew\source\repos\TAOM\Main\Features\TroopWeight\TroopWeightService.cs:36:     }
C:\Users\mikew\source\repos\TAOM\Main\Features\TroopWeight\TroopWeightService.cs:37: 
C:\Users\mikew\source\repos\TAOM\Main\Features\TroopWeight\TroopWeightService.cs:38:     public float GetTroopWeight(string troopStringId)
C:\Users\mikew\source\repos\TAOM\Main\Features\TroopWeight\TroopWeightService.cs:39:     {
C:\Users\mikew\source\repos\TAOM\Main\Features\TroopWeight\TroopWeightService.cs:40:         if (string.IsNullOrEmpty(troopStringId))
C:\Users\mikew\source\repos\TAOM\Main\Features\TroopWeight\TroopWeightService.cs:41:             return 1.0f;
C:\Users\mikew\source\repos\TAOM\Main\Features\TroopWeight\TroopWeightService.cs:42: 
C:\Users\mikew\source\repos\TAOM\Main\Features\TroopWeight\TroopWeightService.cs:43:         return _weights.TryGetValue(troopStringId, out var weight) ? weight : 1.0f;
C:\Users\mikew\source\repos\TAOM\Main\Features\TroopWeight\TroopWeightService.cs:44:     }
C:\Users\mikew\source\repos\TAOM\Main\Features\TroopWeight\TroopWeightService.cs:45: 
C:\Users\mikew\source\repos\TAOM\Main\Features\TroopWeight\TroopWeightService.cs:46:     public float GetTroopWeight(CharacterObject character)
C:\Users\mikew\source\repos\TAOM\Main\Features\TroopWeight\TroopWeightService.cs:47:     {
C:\Users\mikew\source\repos\TAOM\Main\Features\TroopWeight\TroopWeightService.cs:48:         return GetTroopWeight(character?.StringId);
C:\Users\mikew\source\repos\TAOM\Main\Features\TroopWeight\TroopWeightService.cs:49:     }
C:\Users\mikew\source\repos\TAOM\Main\Features\TroopWeight\TroopWeightService.cs:50: 
C:\Users\mikew\source\repos\TAOM\Main\Features\TroopWeight\TroopWeightService.cs:51:     public float CalculateWeightedMemberCount(PartyBase party)
C:\Users\mikew\source\repos\TAOM\Main\Features\TroopWeight\TroopWeightService.cs:52:     {
C:\Users\mikew\source\repos\TAOM\Main\Features\TroopWeight\TroopWeightService.cs:53:         if (party?.MemberRoster == null)
C:\Users\mikew\source\repos\TAOM\Main\Features\TroopWeight\TroopWeightService.cs:54:             return 0f;
C:\Users\mikew\source\repos\TAOM\Main\Features\TroopWeight\TroopWeightService.cs:55: 
C:\Users\mikew\source\repos\TAOM\Main\Features\TroopWeight\TroopWeightService.cs:56:         return CalculateWeightedRosterCount(party.MemberRoster);
C:\Users\mikew\source\repos\TAOM\Main\Features\TroopWeight\TroopWeightService.cs:57:     }
C:\Users\mikew\source\repos\TAOM\Main\Features\TroopWeight\TroopWeightService.cs:58: 
C:\Users\mikew\source\repos\TAOM\Main\Features\TroopWeight\TroopWeightService.cs:59:     public float CalculateWeightedRosterCount(TroopRoster roster)
C:\Users\mikew\source\repos\TAOM\Main\Features\TroopWeight\TroopWeightService.cs:60:     {
C:\Users\mikew\source\repos\TAOM\Main\Features\TroopWeight\TroopWeightService.cs:61:         if (roster == null || roster.Count <= 0)
C:\Users\mikew\source\repos\TAOM\Main\Features\TroopWeight\TroopWeightService.cs:62:             return 0f;
C:\Users\mikew\source\repos\TAOM\Main\Features\TroopWeight\TroopWeightService.cs:63: 
C:\Users\mikew\source\repos\TAOM\Main\Features\TroopWeight\TroopWeightService.cs:64:         try
C:\Users\mikew\source\repos\TAOM\Main\Features\TroopWeight\TroopWeightService.cs:65:         {
C:\Users\mikew\source\repos\TAOM\Main\Features\TroopWeight\TroopWeightService.cs:66:             float totalWeight = 0f;
C:\Users\mikew\source\repos\TAOM\Main\Features\TroopWeight\TroopWeightService.cs:67:             int count = roster.Count;
C:\Users\mikew\source\repos\TAOM\Main\Features\TroopWeight\TroopWeightService.cs:68:             for (int i = 0; i < count; i++)
C:\Users\mikew\source\repos\TAOM\Main\Features\TroopWeight\TroopWeightService.cs:69:             {
C:\Users\mikew\source\repos\TAOM\Main\Features\TroopWeight\TroopWeightService.cs:70:                 var element = roster.GetElementCopyAtIndex(i);
C:\Users\mikew\source\repos\TAOM\Main\Features\TroopWeight\TroopWeightService.cs:71:                 totalWeight += CalculateWeightedElementCount(element);
C:\Users\mikew\source\repos\TAOM\Main\Features\TroopWeight\TroopWeightService.cs:72:             }
C:\Users\mikew\source\repos\TAOM\Main\Features\TroopWeight\TroopWeightService.cs:73:             return totalWeight;
C:\Users\mikew\source\repos\TAOM\Main\Features\TroopWeight\TroopWeightService.cs:74:         }
C:\Users\mikew\source\repos\TAOM\Main\Features\TroopWeight\TroopWeightService.cs:75:         catch (Exception ex)
C:\Users\mikew\source\repos\TAOM\Main\Features\TroopWeight\TroopWeightService.cs:76:         {
C:\Users\mikew\source\repos\TAOM\Main\Features\TroopWeight\TroopWeightService.cs:77:             _logger.LogWarning($"[TroopWeight] Roster iteration failed (count={roster?.Count}): {ex.GetType().Name}: {ex.Message}");
C:\Users\mikew\source\repos\TAOM\Main\Features\TroopWeight\TroopWeightService.cs:78:             return 0f;
C:\Users\mikew\source\repos\TAOM\Main\Features\TroopWeight\TroopWeightService.cs:79:         }
C:\Users\mikew\source\repos\TAOM\Main\Features\TroopWeight\TroopWeightService.cs:80:     }
C:\Users\mikew\source\repos\TAOM\Main\Features\TroopWeight\TroopWeightService.cs:81: 
C:\Users\mikew\source\repos\TAOM\Main\Features\TroopWeight\TroopWeightService.cs:82:     public float CalculateWeightedElementCount(TroopRosterElement element)
C:\Users\mikew\source\repos\TAOM\Main\Features\TroopWeight\TroopWeightService.cs:83:     {
C:\Users\mikew\source\repos\TAOM\Main\Features\TroopWeight\TroopWeightService.cs:84:         if (element.Character == null)
C:\Users\mikew\source\repos\TAOM\Main\Features\TroopWeight\TroopWeightService.cs:85:             return element.Number;
C:\Users\mikew\source\repos\TAOM\Main\Features\TroopWeight\TroopWeightService.cs:86: 
C:\Users\mikew\source\repos\TAOM\Main\Features\TroopWeight\TroopWeightService.cs:87:         var weight = GetTroopWeight(element.Character);
C:\Users\mikew\source\repos\TAOM\Main\Features\TroopWeight\TroopWeightService.cs:88:         return element.Number * weight;
C:\Users\mikew\source\repos\TAOM\Main\Features\TroopWeight\TroopWeightService.cs:89:     }
C:\Users\mikew\source\repos\TAOM\Main\Features\TroopWeight\TroopWeightService.cs:90: 
C:\Users\mikew\source\repos\TAOM\Main\Features\TroopWeight\TroopWeightService.cs:91:     // Weighted contribution of one roster element. Shared by the pure (testable) and the
C:\Users\mikew\source\repos\TAOM\Main\Features\TroopWeight\TroopWeightService.cs:92:     // roster-walking (cached) entry points so their arithmetic can never drift apart.
C:\Users\mikew\source\repos\TAOM\Main\Features\TroopWeight\TroopWeightService.cs:93:     // Separate-ceiling note: ComputeWeightedHealthyAndWounded ceilings Healthy and Wounded
C:\Users\mikew\source\repos\TAOM\Main\Features\TroopWeight\TroopWeightService.cs:94:     // independently, matching PartyVMPopulatePartyListLabelHook. For integer weights (what TAOM
C:\Users\mikew\source\repos\TAOM\Main\Features\TroopWeight\TroopWeightService.cs:95:     // ships) Healthy + Wounded == the weighted member total exactly. With fractional weights and
C:\Users\mikew\source\repos\TAOM\Main\Features\TroopWeight\TroopWeightService.cs:96:     // mixed wound states the two ceilings can sum to 1 above Ceiling(total) — a cosmetic-only,
C:\Users\mikew\source\repos\TAOM\Main\Features\TroopWeight\TroopWeightService.cs:97:     // intentional consistency with the existing party-list label.
C:\Users\mikew\source\repos\TAOM\Main\Features\TroopWeight\TroopWeightService.cs:98:     private static (float Healthy, float Wounded) WeightedContribution(float weight, int number, int woundedNumber)
C:\Users\mikew\source\repos\TAOM\Main\Features\TroopWeight\TroopWeightService.cs:99:     {
C:\Users\mikew\source\repos\TAOM\Main\Features\TroopWeight\TroopWeightService.cs:100:         int wounded = woundedNumber < 0 ? 0 : woundedNumber;
C:\Users\mikew\source\repos\TAOM\Main\Features\TroopWeight\TroopWeightService.cs:101:         int healthy = number - wounded;
C:\Users\mikew\source\repos\TAOM\Main\Features\TroopWeight\TroopWeightService.cs:102:         if (healthy < 0)
C:\Users\mikew\source\repos\TAOM\Main\Features\TroopWeight\TroopWeightService.cs:103:             healthy = 0;
C:\Users\mikew\source\repos\TAOM\Main\Features\TroopWeight\TroopWeightService.cs:104: 
C:\Users\mikew\source\repos\TAOM\Main\Features\TroopWeight\TroopWeightService.cs:105:         return (healthy * weight, wounded * weight);
C:\Users\mikew\source\repos\TAOM\Main\Features\TroopWeight\TroopWeightService.cs:106:     }
C:\Users\mikew\source\repos\TAOM\Main\Features\TroopWeight\TroopWeightService.cs:107: 
C:\Users\mikew\source\repos\TAOM\Main\Features\TroopWeight\TroopWeightService.cs:108:     public (int Healthy, int Wounded) ComputeWeightedHealthyAndWounded(
C:\Users\mikew\source\repos\TAOM\Main\Features\TroopWeight\TroopWeightService.cs:109:         IEnumerable<(string TroopId, int Number, int WoundedNumber)> elements)
C:\Users\mikew\source\repos\TAOM\Main\Features\TroopWeight\TroopWeightService.cs:110:     {
C:\Users\mikew\source\repos\TAOM\Main\Features\TroopWeight\TroopWeightService.cs:111:         if (elements == null)
C:\Users\mikew\source\repos\TAOM\Main\Features\TroopWeight\TroopWeightService.cs:112:             return (0, 0);
C:\Users\mikew\source\repos\TAOM\Main\Features\TroopWeight\TroopWeightService.cs:113: 
C:\Users\mikew\source\repos\TAOM\Main\Features\TroopWeight\TroopWeightService.cs:114:         float weightedHealthy = 0f;
C:\Users\mikew\source\repos\TAOM\Main\Features\TroopWeight\TroopWeightService.cs:115:         float weightedWounded = 0f;

exec
"C:\\Program Files\\PowerShell\\7\\pwsh.exe" -Command '$i=0; Get-Content '"'Main/Core/Validation/FiniteFloatValidator.cs' | ForEach-Object { "'$i++; '"'{0}:{1}: {2}' -f (Resolve-Path 'Main/Core/Validation/FiniteFloatValidator.cs'),"'$i,$_ }' in C:\Users\mikew\source\repos\TAOM
exec
"C:\\Program Files\\PowerShell\\7\\pwsh.exe" -Command '$path='"'Main/_Module/ModuleData/TroopWeights/troop_weights.xml'; "'$matches=Select-String -Path $path -Pattern '"'weight=\"(["'^"]+)"'"'; "'$items=@(); foreach($m in $matches){ $line=$m.Line; if($line -match '"'id=\"(["'^"]+)".*weight="([''^"]+)"'"'){ "'$items += [pscustomobject]@{Line=$m.LineNumber; Id=$matches[0]; Weight=[float]$matches[2]; Text=$line.Trim()} } }; $min=($items | Sort-Object Weight | Select-Object -First 1); $below=($items | Where-Object { $_.Weight -lt 1 }); "count=$($items.Count); minLine=$($min.Line); minWeight=$($min.Weight); belowOne=$($below.Count)"; if($below.Count -gt 0){ $below | ForEach-Object { "$($_.Line): $($_.Text)" } } else { $items | Sort-Object Weight,Line | Select-Object -First 8 | ForEach-Object { "$($_.Line): $($_.Text)" } }' in C:\Users\mikew\source\repos\TAOM
exec
"C:\\Program Files\\PowerShell\\7\\pwsh.exe" -Command "Select-String -Path 'Main/_Module/ModuleData/TroopWeights/troop_weights.xml' -Pattern '<TroopWeight|weight=\"1|weight=\"2|weight=\"3|weight=\"4' | Select-Object -First 40 | ForEach-Object { \"{0}:{1}: {2}\" -f "'$_.Path,$_.LineNumber,$_.Line.Trim() }' in C:\Users\mikew\source\repos\TAOM
exec
"C:\\Program Files\\PowerShell\\7\\pwsh.exe" -Command "Get-ChildItem -Path 'E:\\Decompiled_Bannerlord' -Recurse -Filter '*DefaultSettlementFoodModel*' -ErrorAction SilentlyContinue | Select-Object -First 20 | ForEach-Object { "'$_.FullName }' in C:\Users\mikew\source\repos\TAOM
 succeeded in 522ms:
C:\Users\mikew\source\repos\TAOM\Main\Core\Validation\FiniteFloatValidator.cs:1: namespace TAOM.Core.Validation;
C:\Users\mikew\source\repos\TAOM\Main\Core\Validation\FiniteFloatValidator.cs:2: 
C:\Users\mikew\source\repos\TAOM\Main\Core\Validation\FiniteFloatValidator.cs:3: /// <summary>
C:\Users\mikew\source\repos\TAOM\Main\Core\Validation\FiniteFloatValidator.cs:4: /// Centralized float-validation helpers for config providers.
C:\Users\mikew\source\repos\TAOM\Main\Core\Validation\FiniteFloatValidator.cs:5: ///
C:\Users\mikew\source\repos\TAOM\Main\Core\Validation\FiniteFloatValidator.cs:6: /// Why this exists: range checks like `value &lt; min || value &gt; max` evaluate false for `NaN`
C:\Users\mikew\source\repos\TAOM\Main\Core\Validation\FiniteFloatValidator.cs:7: /// (all NaN comparisons return false per IEEE-754), so a `NaN` config value sneaks past validation
C:\Users\mikew\source\repos\TAOM\Main\Core\Validation\FiniteFloatValidator.cs:8: /// and then breaks downstream comparisons in unpredictable ways. This has shipped twice:
C:\Users\mikew\source\repos\TAOM\Main\Core\Validation\FiniteFloatValidator.cs:9: ///
C:\Users\mikew\source\repos\TAOM\Main\Core\Validation\FiniteFloatValidator.cs:10: /// * Career cooldown review #31 (2026-05-04) — NaN cooldown made `IsOnCooldown =&gt; CooldownRemaining &gt; 0f`
C:\Users\mikew\source\repos\TAOM\Main\Core\Validation\FiniteFloatValidator.cs:11: ///   evaluate false → ability "always ready" → V re-activates indefinitely.
C:\Users\mikew\source\repos\TAOM\Main\Core\Validation\FiniteFloatValidator.cs:12: /// * EditorCacheRebuild Codex review #38 (2026-05-12) — NaN `SmokeTestDistanceTolerance` made the gate's
C:\Users\mikew\source\repos\TAOM\Main\Core\Validation\FiniteFloatValidator.cs:13: ///   `maxDelta &gt; tolerance` evaluate false → smoke test silently disabled → potential threading
C:\Users\mikew\source\repos\TAOM\Main\Core\Validation\FiniteFloatValidator.cs:14: ///   issues never caught.
C:\Users\mikew\source\repos\TAOM\Main\Core\Validation\FiniteFloatValidator.cs:15: ///
C:\Users\mikew\source\repos\TAOM\Main\Core\Validation\FiniteFloatValidator.cs:16: /// Use these helpers BEFORE every range check on a `float`/`double` config field. Bool/int fields
C:\Users\mikew\source\repos\TAOM\Main\Core\Validation\FiniteFloatValidator.cs:17: /// don't need this — only IEEE-754 types are affected.
C:\Users\mikew\source\repos\TAOM\Main\Core\Validation\FiniteFloatValidator.cs:18: /// </summary>
C:\Users\mikew\source\repos\TAOM\Main\Core\Validation\FiniteFloatValidator.cs:19: public static class FiniteFloatValidator
C:\Users\mikew\source\repos\TAOM\Main\Core\Validation\FiniteFloatValidator.cs:20: {
C:\Users\mikew\source\repos\TAOM\Main\Core\Validation\FiniteFloatValidator.cs:21:     /// <summary>Returns true if <paramref name="value"/> is a real, finite number (not NaN, not ±Infinity).</summary>
C:\Users\mikew\source\repos\TAOM\Main\Core\Validation\FiniteFloatValidator.cs:22:     public static bool IsFinite(float value) =>
C:\Users\mikew\source\repos\TAOM\Main\Core\Validation\FiniteFloatValidator.cs:23:         !float.IsNaN(value) && !float.IsInfinity(value);
C:\Users\mikew\source\repos\TAOM\Main\Core\Validation\FiniteFloatValidator.cs:24: 
C:\Users\mikew\source\repos\TAOM\Main\Core\Validation\FiniteFloatValidator.cs:25:     /// <summary>Returns true if <paramref name="value"/> is a real, finite number (not NaN, not ±Infinity).</summary>
C:\Users\mikew\source\repos\TAOM\Main\Core\Validation\FiniteFloatValidator.cs:26:     public static bool IsFinite(double value) =>
C:\Users\mikew\source\repos\TAOM\Main\Core\Validation\FiniteFloatValidator.cs:27:         !double.IsNaN(value) && !double.IsInfinity(value);
C:\Users\mikew\source\repos\TAOM\Main\Core\Validation\FiniteFloatValidator.cs:28: 
C:\Users\mikew\source\repos\TAOM\Main\Core\Validation\FiniteFloatValidator.cs:29:     /// <summary>
C:\Users\mikew\source\repos\TAOM\Main\Core\Validation\FiniteFloatValidator.cs:30:     /// Returns true if <paramref name="value"/> is finite AND within [min, max] (inclusive).
C:\Users\mikew\source\repos\TAOM\Main\Core\Validation\FiniteFloatValidator.cs:31:     /// NaN/Infinity always return false. Standard pattern for config range validation.
C:\Users\mikew\source\repos\TAOM\Main\Core\Validation\FiniteFloatValidator.cs:32:     /// </summary>
C:\Users\mikew\source\repos\TAOM\Main\Core\Validation\FiniteFloatValidator.cs:33:     public static bool IsFiniteInRange(float value, float min, float max) =>
C:\Users\mikew\source\repos\TAOM\Main\Core\Validation\FiniteFloatValidator.cs:34:         IsFinite(value) && value >= min && value <= max;
C:\Users\mikew\source\repos\TAOM\Main\Core\Validation\FiniteFloatValidator.cs:35: 
C:\Users\mikew\source\repos\TAOM\Main\Core\Validation\FiniteFloatValidator.cs:36:     /// <summary>
C:\Users\mikew\source\repos\TAOM\Main\Core\Validation\FiniteFloatValidator.cs:37:     /// Returns true if <paramref name="value"/> is finite AND less than or equal to <paramref name="max"/>.
C:\Users\mikew\source\repos\TAOM\Main\Core\Validation\FiniteFloatValidator.cs:38:     /// Use for penalty fields constrained to be non-positive (e.g., loyalty penalties must be ≤ 0).
C:\Users\mikew\source\repos\TAOM\Main\Core\Validation\FiniteFloatValidator.cs:39:     /// NaN/Infinity always return false.
C:\Users\mikew\source\repos\TAOM\Main\Core\Validation\FiniteFloatValidator.cs:40:     /// </summary>
C:\Users\mikew\source\repos\TAOM\Main\Core\Validation\FiniteFloatValidator.cs:41:     public static bool IsFiniteAtMost(float value, float max) =>
C:\Users\mikew\source\repos\TAOM\Main\Core\Validation\FiniteFloatValidator.cs:42:         IsFinite(value) && value <= max;
C:\Users\mikew\source\repos\TAOM\Main\Core\Validation\FiniteFloatValidator.cs:43: 
C:\Users\mikew\source\repos\TAOM\Main\Core\Validation\FiniteFloatValidator.cs:44:     /// <summary>
C:\Users\mikew\source\repos\TAOM\Main\Core\Validation\FiniteFloatValidator.cs:45:     /// Returns true if <paramref name="value"/> is finite AND greater than or equal to <paramref name="min"/>.
C:\Users\mikew\source\repos\TAOM\Main\Core\Validation\FiniteFloatValidator.cs:46:     /// Use for bonus fields constrained to be non-negative.
C:\Users\mikew\source\repos\TAOM\Main\Core\Validation\FiniteFloatValidator.cs:47:     /// NaN/Infinity always return false.
C:\Users\mikew\source\repos\TAOM\Main\Core\Validation\FiniteFloatValidator.cs:48:     /// </summary>
C:\Users\mikew\source\repos\TAOM\Main\Core\Validation\FiniteFloatValidator.cs:49:     public static bool IsFiniteAtLeast(float value, float min) =>
C:\Users\mikew\source\repos\TAOM\Main\Core\Validation\FiniteFloatValidator.cs:50:         IsFinite(value) && value >= min;
C:\Users\mikew\source\repos\TAOM\Main\Core\Validation\FiniteFloatValidator.cs:51: 
C:\Users\mikew\source\repos\TAOM\Main\Core\Validation\FiniteFloatValidator.cs:52:     // Phase 9b #126 — double overloads. InitialChildGeneration (and other features that parse
C:\Users\mikew\source\repos\TAOM\Main\Core\Validation\FiniteFloatValidator.cs:53:     // JSON via Newtonsoft `Value<double?>()`) need double-typed validation. Same semantics as
C:\Users\mikew\source\repos\TAOM\Main\Core\Validation\FiniteFloatValidator.cs:54:     // the float overloads — NaN/Infinity always fail, range is inclusive.
C:\Users\mikew\source\repos\TAOM\Main\Core\Validation\FiniteFloatValidator.cs:55: 
C:\Users\mikew\source\repos\TAOM\Main\Core\Validation\FiniteFloatValidator.cs:56:     /// <summary>Returns true if <paramref name="value"/> is finite AND within [min, max] (inclusive).</summary>
C:\Users\mikew\source\repos\TAOM\Main\Core\Validation\FiniteFloatValidator.cs:57:     public static bool IsFiniteInRange(double value, double min, double max) =>
C:\Users\mikew\source\repos\TAOM\Main\Core\Validation\FiniteFloatValidator.cs:58:         IsFinite(value) && value >= min && value <= max;
C:\Users\mikew\source\repos\TAOM\Main\Core\Validation\FiniteFloatValidator.cs:59: 
C:\Users\mikew\source\repos\TAOM\Main\Core\Validation\FiniteFloatValidator.cs:60:     /// <summary>Returns true if <paramref name="value"/> is finite AND ≤ <paramref name="max"/>.</summary>
C:\Users\mikew\source\repos\TAOM\Main\Core\Validation\FiniteFloatValidator.cs:61:     public static bool IsFiniteAtMost(double value, double max) =>
C:\Users\mikew\source\repos\TAOM\Main\Core\Validation\FiniteFloatValidator.cs:62:         IsFinite(value) && value <= max;
C:\Users\mikew\source\repos\TAOM\Main\Core\Validation\FiniteFloatValidator.cs:63: 
C:\Users\mikew\source\repos\TAOM\Main\Core\Validation\FiniteFloatValidator.cs:64:     /// <summary>Returns true if <paramref name="value"/> is finite AND ≥ <paramref name="min"/>.</summary>
C:\Users\mikew\source\repos\TAOM\Main\Core\Validation\FiniteFloatValidator.cs:65:     public static bool IsFiniteAtLeast(double value, double min) =>
C:\Users\mikew\source\repos\TAOM\Main\Core\Validation\FiniteFloatValidator.cs:66:         IsFinite(value) && value >= min;
C:\Users\mikew\source\repos\TAOM\Main\Core\Validation\FiniteFloatValidator.cs:67: }

 succeeded in 502ms:
C:\Users\mikew\source\repos\TAOM\Main\_Module\ModuleData\TroopWeights\troop_weights.xml:2: <TroopWeights>
C:\Users\mikew\source\repos\TAOM\Main\_Module\ModuleData\TroopWeights\troop_weights.xml:7: <TroopWeight id="cave_troll" weight="4.0" />
C:\Users\mikew\source\repos\TAOM\Main\_Module\ModuleData\TroopWeights\troop_weights.xml:12: <TroopWeight id="rivendell_glorfindel_guard" weight="3.0" />
C:\Users\mikew\source\repos\TAOM\Main\_Module\ModuleData\TroopWeights\troop_weights.xml:13: <TroopWeight id="rivendell_gondolin_battlemaster" weight="3.0" />
C:\Users\mikew\source\repos\TAOM\Main\_Module\ModuleData\TroopWeights\troop_weights.xml:14: <TroopWeight id="rivendell_high_captain" weight="3.0" />
C:\Users\mikew\source\repos\TAOM\Main\_Module\ModuleData\TroopWeights\troop_weights.xml:15: <TroopWeight id="rivendell_knight_golden_flower" weight="3.0" />
C:\Users\mikew\source\repos\TAOM\Main\_Module\ModuleData\TroopWeights\troop_weights.xml:16: <TroopWeight id="rivendell_warden_gondolin" weight="3.0" />
C:\Users\mikew\source\repos\TAOM\Main\_Module\ModuleData\TroopWeights\troop_weights.xml:18: <TroopWeight id="rivendell_royal_knight" weight="2.0" />
C:\Users\mikew\source\repos\TAOM\Main\_Module\ModuleData\TroopWeights\troop_weights.xml:19: <TroopWeight id="rivendell_royal_guard" weight="2.0" />
C:\Users\mikew\source\repos\TAOM\Main\_Module\ModuleData\TroopWeights\troop_weights.xml:20: <TroopWeight id="noldorin_lancer" weight="2.0" />
C:\Users\mikew\source\repos\TAOM\Main\_Module\ModuleData\TroopWeights\troop_weights.xml:21: <TroopWeight id="rider_of_himring" weight="2.0" />
C:\Users\mikew\source\repos\TAOM\Main\_Module\ModuleData\TroopWeights\troop_weights.xml:22: <TroopWeight id="battlemaster_of_the_first_age" weight="2.0" />
C:\Users\mikew\source\repos\TAOM\Main\_Module\ModuleData\TroopWeights\troop_weights.xml:23: <TroopWeight id="imladris_blademaster" weight="2.0" />
C:\Users\mikew\source\repos\TAOM\Main\_Module\ModuleData\TroopWeights\troop_weights.xml:24: <TroopWeight id="imladris_horse_archer" weight="2.0" />
C:\Users\mikew\source\repos\TAOM\Main\_Module\ModuleData\TroopWeights\troop_weights.xml:25: <TroopWeight id="imladris_marchwarden" weight="2.0" />
C:\Users\mikew\source\repos\TAOM\Main\_Module\ModuleData\TroopWeights\troop_weights.xml:26: <TroopWeight id="imladris_outrider" weight="2.0" />
C:\Users\mikew\source\repos\TAOM\Main\_Module\ModuleData\TroopWeights\troop_weights.xml:27: <TroopWeight id="imladris_warden" weight="2.0" />
C:\Users\mikew\source\repos\TAOM\Main\_Module\ModuleData\TroopWeights\troop_weights.xml:28: <TroopWeight id="imladris_guardsman" weight="2.0" />
C:\Users\mikew\source\repos\TAOM\Main\_Module\ModuleData\TroopWeights\troop_weights.xml:29: <TroopWeight id="imladris_marksman" weight="2.0" />
C:\Users\mikew\source\repos\TAOM\Main\_Module\ModuleData\TroopWeights\troop_weights.xml:30: <TroopWeight id="imladris_nobleman" weight="2.0" />
C:\Users\mikew\source\repos\TAOM\Main\_Module\ModuleData\TroopWeights\troop_weights.xml:31: <TroopWeight id="imladris_swordguard" weight="2.0" />
C:\Users\mikew\source\repos\TAOM\Main\_Module\ModuleData\TroopWeights\troop_weights.xml:32: <TroopWeight id="imladris_archer" weight="2.0" />
C:\Users\mikew\source\repos\TAOM\Main\_Module\ModuleData\TroopWeights\troop_weights.xml:33: <TroopWeight id="imladris_bowman" weight="2.0" />
C:\Users\mikew\source\repos\TAOM\Main\_Module\ModuleData\TroopWeights\troop_weights.xml:34: <TroopWeight id="rivendell_noble" weight="2.0" />
C:\Users\mikew\source\repos\TAOM\Main\_Module\ModuleData\TroopWeights\troop_weights.xml:38: <TroopWeight id="mirkwood_palaceguard" weight="3.0" />
C:\Users\mikew\source\repos\TAOM\Main\_Module\ModuleData\TroopWeights\troop_weights.xml:39: <TroopWeight id="mirkwood_thingolheir" weight="3.0" />
C:\Users\mikew\source\repos\TAOM\Main\_Module\ModuleData\TroopWeights\troop_weights.xml:41: <TroopWeight id="mirkwood_beleglas" weight="2.0" />
C:\Users\mikew\source\repos\TAOM\Main\_Module\ModuleData\TroopWeights\troop_weights.xml:42: <TroopWeight id="mirkwood_borderwardens" weight="2.0" />
C:\Users\mikew\source\repos\TAOM\Main\_Module\ModuleData\TroopWeights\troop_weights.xml:43: <TroopWeight id="mirkwood_glaivesman" weight="2.0" />
C:\Users\mikew\source\repos\TAOM\Main\_Module\ModuleData\TroopWeights\troop_weights.xml:44: <TroopWeight id="mirkwood_guardians" weight="2.0" />
C:\Users\mikew\source\repos\TAOM\Main\_Module\ModuleData\TroopWeights\troop_weights.xml:45: <TroopWeight id="mirkwood_guards" weight="2.0" />
C:\Users\mikew\source\repos\TAOM\Main\_Module\ModuleData\TroopWeights\troop_weights.xml:46: <TroopWeight id="mirkwood_rochenlas" weight="2.0" />
C:\Users\mikew\source\repos\TAOM\Main\_Module\ModuleData\TroopWeights\troop_weights.xml:47: <TroopWeight id="mirkwood_sentinels" weight="2.0" />
C:\Users\mikew\source\repos\TAOM\Main\_Module\ModuleData\TroopWeights\troop_weights.xml:48: <TroopWeight id="mirkwood_swordsman" weight="2.0" />
C:\Users\mikew\source\repos\TAOM\Main\_Module\ModuleData\TroopWeights\troop_weights.xml:49: <TroopWeight id="mirkwood_wardens" weight="2.0" />
C:\Users\mikew\source\repos\TAOM\Main\_Module\ModuleData\TroopWeights\troop_weights.xml:50: <TroopWeight id="mirkwood_woodsman" weight="2.0" />
C:\Users\mikew\source\repos\TAOM\Main\_Module\ModuleData\TroopWeights\troop_weights.xml:51: <TroopWeight id="mirkwood_recruit" weight="2.0" />
C:\Users\mikew\source\repos\TAOM\Main\_Module\ModuleData\TroopWeights\troop_weights.xml:55: <TroopWeight id="erebor_oathsworn_royal_legionary" weight="3.0" />
C:\Users\mikew\source\repos\TAOM\Main\_Module\ModuleData\TroopWeights\troop_weights.xml:56: <TroopWeight id="erebor_noble_royal_warden" weight="3.0" />
C:\Users\mikew\source\repos\TAOM\Main\_Module\ModuleData\TroopWeights\troop_weights.xml:57: <TroopWeight id="iron_hills_noble_royal_warden" weight="3.0" />

 succeeded in 505ms:
E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\TaleWorlds.CampaignSystem.GameComponents\DefaultSettlementFoodModel.cs

 succeeded in 553ms:
count=88; minLine=64; minWeight=2; belowOne=0
18: <TroopWeight id="rivendell_royal_knight" weight="2.0" />
19: <TroopWeight id="rivendell_royal_guard" weight="2.0" />
20: <TroopWeight id="noldorin_lancer" weight="2.0" />
21: <TroopWeight id="rider_of_himring" weight="2.0" />
22: <TroopWeight id="battlemaster_of_the_first_age" weight="2.0" />
23: <TroopWeight id="imladris_blademaster" weight="2.0" />
24: <TroopWeight id="imladris_horse_archer" weight="2.0" />
25: <TroopWeight id="imladris_marchwarden" weight="2.0" />

codex
The shipped troop-weight data has 88 explicit weights and the scan found `belowOne=0`, with the minimum shipped value at 2.0. The loader would allow positive fractional values, but the current XML does not ship any, and the postfix also refuses to lower vanilla counts even if a future value did.
exec
"C:\\Program Files\\PowerShell\\7\\pwsh.exe" -Command '$i=0; Get-Content '"'Main/_Module/ModuleData/TroopWeights/troop_weights.xml' | ForEach-Object { "'$i++; if($i -ge 55 -and $i -le 70){ '"'{0}:{1}: {2}' -f (Resolve-Path 'Main/_Module/ModuleData/TroopWeights/troop_weights.xml'),"'$i,$_ } }' in C:\Users\mikew\source\repos\TAOM
exec
"C:\\Program Files\\PowerShell\\7\\pwsh.exe" -Command '$path='"'E:\\Decompiled_Bannerlord\\Campaign\\TaleWorlds.CampaignSystem\\TaleWorlds.CampaignSystem.GameComponents\\DefaultSettlementFoodModel.cs'; "'$i=0; Get-Content $path | ForEach-Object { $i++; if($i -le 260){ '"'{0}:{1}: {2}' -f "'$path,$i,$_ } }' in C:\Users\mikew\source\repos\TAOM
exec
"C:\\Program Files\\PowerShell\\7\\pwsh.exe" -Command "Get-ChildItem -Path 'E:\\Decompiled_Bannerlord' -Recurse -Filter 'PartyBase.cs' -ErrorAction SilentlyContinue | Select-Object -First 20 | ForEach-Object { "'$_.FullName }' in C:\Users\mikew\source\repos\TAOM
exec
"C:\\Program Files\\PowerShell\\7\\pwsh.exe" -Command "Get-ChildItem -Path 'E:\\Decompiled_Bannerlord' -Recurse -Filter 'MobileParty.cs' -ErrorAction SilentlyContinue | Select-Object -First 20 | ForEach-Object { "'$_.FullName }' in C:\Users\mikew\source\repos\TAOM
 succeeded in 491ms:
C:\Users\mikew\source\repos\TAOM\Main\_Module\ModuleData\TroopWeights\troop_weights.xml:55:     <TroopWeight id="erebor_oathsworn_royal_legionary" weight="3.0" />
C:\Users\mikew\source\repos\TAOM\Main\_Module\ModuleData\TroopWeights\troop_weights.xml:56:     <TroopWeight id="erebor_noble_royal_warden" weight="3.0" />
C:\Users\mikew\source\repos\TAOM\Main\_Module\ModuleData\TroopWeights\troop_weights.xml:57:     <TroopWeight id="iron_hills_noble_royal_warden" weight="3.0" />
C:\Users\mikew\source\repos\TAOM\Main\_Module\ModuleData\TroopWeights\troop_weights.xml:58:     <!-- Elite tier (Weight 2.0) -->
C:\Users\mikew\source\repos\TAOM\Main\_Module\ModuleData\TroopWeights\troop_weights.xml:59:     <TroopWeight id="erebor_oathsworn" weight="2.0" />
C:\Users\mikew\source\repos\TAOM\Main\_Module\ModuleData\TroopWeights\troop_weights.xml:60:     <TroopWeight id="erebor_oathsworn_legionary" weight="2.0" />
C:\Users\mikew\source\repos\TAOM\Main\_Module\ModuleData\TroopWeights\troop_weights.xml:61:     <TroopWeight id="erebor_noble_gate_warden" weight="2.0" />
C:\Users\mikew\source\repos\TAOM\Main\_Module\ModuleData\TroopWeights\troop_weights.xml:62:     <TroopWeight id="erebor_noble_shield_breaker" weight="2.0" />
C:\Users\mikew\source\repos\TAOM\Main\_Module\ModuleData\TroopWeights\troop_weights.xml:63:     <!-- Iron Hills Noble line (#212 KEYforce revamp) — T5–T8 -->
C:\Users\mikew\source\repos\TAOM\Main\_Module\ModuleData\TroopWeights\troop_weights.xml:64:     <TroopWeight id="iron_hills_noble_sharpshooter" weight="2.0" />
C:\Users\mikew\source\repos\TAOM\Main\_Module\ModuleData\TroopWeights\troop_weights.xml:65:     <TroopWeight id="iron_hills_noble_veteran_sharpshooter" weight="2.0" />
C:\Users\mikew\source\repos\TAOM\Main\_Module\ModuleData\TroopWeights\troop_weights.xml:66:     <TroopWeight id="iron_hills_noble_infantry" weight="2.0" />
C:\Users\mikew\source\repos\TAOM\Main\_Module\ModuleData\TroopWeights\troop_weights.xml:67:     <TroopWeight id="iron_hills_noble_guard" weight="2.0" />
C:\Users\mikew\source\repos\TAOM\Main\_Module\ModuleData\TroopWeights\troop_weights.xml:68:     <TroopWeight id="iron_hills_noble_shield_guard" weight="2.0" />
C:\Users\mikew\source\repos\TAOM\Main\_Module\ModuleData\TroopWeights\troop_weights.xml:69:     <TroopWeight id="iron_hills_noble_gate_warden" weight="2.0" />
C:\Users\mikew\source\repos\TAOM\Main\_Module\ModuleData\TroopWeights\troop_weights.xml:70:     <TroopWeight id="iron_hills_noble_hammer_guard" weight="2.0" />

 succeeded in 474ms:
E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\TaleWorlds.CampaignSystem.Party\PartyBase.cs

 succeeded in 479ms:
E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\TaleWorlds.CampaignSystem.GameComponents\DefaultSettlementFoodModel.cs:1: using Helpers;
E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\TaleWorlds.CampaignSystem.GameComponents\DefaultSettlementFoodModel.cs:2: using TaleWorlds.CampaignSystem.CharacterDevelopment;
E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\TaleWorlds.CampaignSystem.GameComponents\DefaultSettlementFoodModel.cs:3: using TaleWorlds.CampaignSystem.ComponentInterfaces;
E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\TaleWorlds.CampaignSystem.GameComponents\DefaultSettlementFoodModel.cs:4: using TaleWorlds.CampaignSystem.Issues;
E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\TaleWorlds.CampaignSystem.GameComponents\DefaultSettlementFoodModel.cs:5: using TaleWorlds.CampaignSystem.Settlements;
E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\TaleWorlds.CampaignSystem.GameComponents\DefaultSettlementFoodModel.cs:6: using TaleWorlds.CampaignSystem.Settlements.Buildings;
E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\TaleWorlds.CampaignSystem.GameComponents\DefaultSettlementFoodModel.cs:7: using TaleWorlds.Core;
E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\TaleWorlds.CampaignSystem.GameComponents\DefaultSettlementFoodModel.cs:8: using TaleWorlds.Localization;
E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\TaleWorlds.CampaignSystem.GameComponents\DefaultSettlementFoodModel.cs:9: 
E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\TaleWorlds.CampaignSystem.GameComponents\DefaultSettlementFoodModel.cs:10: namespace TaleWorlds.CampaignSystem.GameComponents;
E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\TaleWorlds.CampaignSystem.GameComponents\DefaultSettlementFoodModel.cs:11: 
E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\TaleWorlds.CampaignSystem.GameComponents\DefaultSettlementFoodModel.cs:12: public class DefaultSettlementFoodModel : SettlementFoodModel
E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\TaleWorlds.CampaignSystem.GameComponents\DefaultSettlementFoodModel.cs:13: {
E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\TaleWorlds.CampaignSystem.GameComponents\DefaultSettlementFoodModel.cs:14: 	private readonly TextObject ProsperityText = GameTexts.FindText("str_prosperity");
E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\TaleWorlds.CampaignSystem.GameComponents\DefaultSettlementFoodModel.cs:15: 
E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\TaleWorlds.CampaignSystem.GameComponents\DefaultSettlementFoodModel.cs:16: 	private readonly TextObject GarrisonText = GameTexts.FindText("str_garrison");
E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\TaleWorlds.CampaignSystem.GameComponents\DefaultSettlementFoodModel.cs:17: 
E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\TaleWorlds.CampaignSystem.GameComponents\DefaultSettlementFoodModel.cs:18: 	private readonly TextObject LandsAroundSettlementText = GameTexts.FindText("str_lands_around_settlement");
E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\TaleWorlds.CampaignSystem.GameComponents\DefaultSettlementFoodModel.cs:19: 
E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\TaleWorlds.CampaignSystem.GameComponents\DefaultSettlementFoodModel.cs:20: 	private readonly TextObject NormalVillagesText = GameTexts.FindText("str_normal_villages");
E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\TaleWorlds.CampaignSystem.GameComponents\DefaultSettlementFoodModel.cs:21: 
E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\TaleWorlds.CampaignSystem.GameComponents\DefaultSettlementFoodModel.cs:22: 	private readonly TextObject RaidedVillagesText = GameTexts.FindText("str_raided_villages");
E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\TaleWorlds.CampaignSystem.GameComponents\DefaultSettlementFoodModel.cs:23: 
E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\TaleWorlds.CampaignSystem.GameComponents\DefaultSettlementFoodModel.cs:24: 	private readonly TextObject VillagesUnderSiegeText = GameTexts.FindText("str_villages_under_siege");
E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\TaleWorlds.CampaignSystem.GameComponents\DefaultSettlementFoodModel.cs:25: 
E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\TaleWorlds.CampaignSystem.GameComponents\DefaultSettlementFoodModel.cs:26: 	private readonly TextObject FoodBoughtByCiviliansText = GameTexts.FindText("str_food_bought_by_civilians");
E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\TaleWorlds.CampaignSystem.GameComponents\DefaultSettlementFoodModel.cs:27: 
E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\TaleWorlds.CampaignSystem.GameComponents\DefaultSettlementFoodModel.cs:28: 	private const int FoodProductionPerVillage = 10;
E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\TaleWorlds.CampaignSystem.GameComponents\DefaultSettlementFoodModel.cs:29: 
E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\TaleWorlds.CampaignSystem.GameComponents\DefaultSettlementFoodModel.cs:30: 	public override int FoodStocksUpperLimit => 300;
E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\TaleWorlds.CampaignSystem.GameComponents\DefaultSettlementFoodModel.cs:31: 
E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\TaleWorlds.CampaignSystem.GameComponents\DefaultSettlementFoodModel.cs:32: 	public override int NumberOfProsperityToEatOneFood => 40;
E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\TaleWorlds.CampaignSystem.GameComponents\DefaultSettlementFoodModel.cs:33: 
E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\TaleWorlds.CampaignSystem.GameComponents\DefaultSettlementFoodModel.cs:34: 	public override int NumberOfMenOnGarrisonToEatOneFood => 20;
E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\TaleWorlds.CampaignSystem.GameComponents\DefaultSettlementFoodModel.cs:35: 
E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\TaleWorlds.CampaignSystem.GameComponents\DefaultSettlementFoodModel.cs:36: 	public override int CastleFoodStockUpperLimitBonus => 150;
E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\TaleWorlds.CampaignSystem.GameComponents\DefaultSettlementFoodModel.cs:37: 
E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\TaleWorlds.CampaignSystem.GameComponents\DefaultSettlementFoodModel.cs:38: 	public override ExplainedNumber CalculateTownFoodStocksChange(Town town, bool includeMarketStocks = true, bool includeDescriptions = false)
E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\TaleWorlds.CampaignSystem.GameComponents\DefaultSettlementFoodModel.cs:39: 	{
E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\TaleWorlds.CampaignSystem.GameComponents\DefaultSettlementFoodModel.cs:40: 		return CalculateTownFoodChangeInternal(town, includeMarketStocks, includeDescriptions);
E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\TaleWorlds.CampaignSystem.GameComponents\DefaultSettlementFoodModel.cs:41: 	}
E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\TaleWorlds.CampaignSystem.GameComponents\DefaultSettlementFoodModel.cs:42: 
E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\TaleWorlds.CampaignSystem.GameComponents\DefaultSettlementFoodModel.cs:43: 	private ExplainedNumber CalculateTownFoodChangeInternal(Town town, bool includeMarketStocks, bool includeDescriptions)
E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\TaleWorlds.CampaignSystem.GameComponents\DefaultSettlementFoodModel.cs:44: 	{
E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\TaleWorlds.CampaignSystem.GameComponents\DefaultSettlementFoodModel.cs:45: 		ExplainedNumber bonuses = new ExplainedNumber(0f, includeDescriptions);
E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\TaleWorlds.CampaignSystem.GameComponents\DefaultSettlementFoodModel.cs:46: 		ExplainedNumber bonuses2 = new ExplainedNumber(0f, includeDescriptions);
E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\TaleWorlds.CampaignSystem.GameComponents\DefaultSettlementFoodModel.cs:47: 		ExplainedNumber bonuses3 = new ExplainedNumber(town.Prosperity / (float)NumberOfProsperityToEatOneFood);
E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\TaleWorlds.CampaignSystem.GameComponents\DefaultSettlementFoodModel.cs:48: 		ExplainedNumber bonuses4 = new ExplainedNumber((((float?)town.GarrisonParty?.Party.NumberOfAllMembers) ?? 0f) / (float)NumberOfMenOnGarrisonToEatOneFood);
E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\TaleWorlds.CampaignSystem.GameComponents\DefaultSettlementFoodModel.cs:49: 		if (town.IsUnderSiege)
E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\TaleWorlds.CampaignSystem.GameComponents\DefaultSettlementFoodModel.cs:50: 		{
E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\TaleWorlds.CampaignSystem.GameComponents\DefaultSettlementFoodModel.cs:51: 			PerkHelper.AddPerkBonusForTown(DefaultPerks.Steward.Gourmet, town, ref bonuses4);
E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\TaleWorlds.CampaignSystem.GameComponents\DefaultSettlementFoodModel.cs:52: 			PerkHelper.AddPerkBonusForTown(DefaultPerks.Medicine.TriageTent, town, ref bonuses2);
E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\TaleWorlds.CampaignSystem.GameComponents\DefaultSettlementFoodModel.cs:53: 		}
E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\TaleWorlds.CampaignSystem.GameComponents\DefaultSettlementFoodModel.cs:54: 		PerkHelper.AddPerkBonusForTown(DefaultPerks.Steward.MasterOfWarcraft, town, ref bonuses3);
E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\TaleWorlds.CampaignSystem.GameComponents\DefaultSettlementFoodModel.cs:55: 		bonuses2.Add(bonuses3.ResultNumber, ProsperityText);
E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\TaleWorlds.CampaignSystem.GameComponents\DefaultSettlementFoodModel.cs:56: 		bonuses2.Add(bonuses4.ResultNumber, GarrisonText);
E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\TaleWorlds.CampaignSystem.GameComponents\DefaultSettlementFoodModel.cs:57: 		town.AddEffectOfBuildings(BuildingEffectEnum.FoodConsumption, ref bonuses2);
E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\TaleWorlds.CampaignSystem.GameComponents\DefaultSettlementFoodModel.cs:58: 		Kingdom kingdom = town.Settlement.OwnerClan?.Kingdom;
E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\TaleWorlds.CampaignSystem.GameComponents\DefaultSettlementFoodModel.cs:59: 		if (kingdom != null && kingdom.HasPolicy(DefaultPolicies.HuntingRights))
E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\TaleWorlds.CampaignSystem.GameComponents\DefaultSettlementFoodModel.cs:60: 		{
E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\TaleWorlds.CampaignSystem.GameComponents\DefaultSettlementFoodModel.cs:61: 			bonuses.Add(2f, DefaultPolicies.HuntingRights.Name);
E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\TaleWorlds.CampaignSystem.GameComponents\DefaultSettlementFoodModel.cs:62: 		}
E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\TaleWorlds.CampaignSystem.GameComponents\DefaultSettlementFoodModel.cs:63: 		if (!town.IsUnderSiege)
E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\TaleWorlds.CampaignSystem.GameComponents\DefaultSettlementFoodModel.cs:64: 		{
E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\TaleWorlds.CampaignSystem.GameComponents\DefaultSettlementFoodModel.cs:65: 			int num = (town.IsTown ? 15 : 10);
E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\TaleWorlds.CampaignSystem.GameComponents\DefaultSettlementFoodModel.cs:66: 			bonuses.Add(num, LandsAroundSettlementText);
E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\TaleWorlds.CampaignSystem.GameComponents\DefaultSettlementFoodModel.cs:67: 			foreach (Village boundVillage in town.Owner.Settlement.BoundVillages)
E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\TaleWorlds.CampaignSystem.GameComponents\DefaultSettlementFoodModel.cs:68: 			{
E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\TaleWorlds.CampaignSystem.GameComponents\DefaultSettlementFoodModel.cs:69: 				float value = 0f;
E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\TaleWorlds.CampaignSystem.GameComponents\DefaultSettlementFoodModel.cs:70: 				if (boundVillage.VillageState == Village.VillageStates.Normal)
E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\TaleWorlds.CampaignSystem.GameComponents\DefaultSettlementFoodModel.cs:71: 				{
E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\TaleWorlds.CampaignSystem.GameComponents\DefaultSettlementFoodModel.cs:72: 					value = (boundVillage.GetHearthLevel() + 1) * 6;
E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\TaleWorlds.CampaignSystem.GameComponents\DefaultSettlementFoodModel.cs:73: 				}
E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\TaleWorlds.CampaignSystem.GameComponents\DefaultSettlementFoodModel.cs:74: 				bonuses.Add(value, boundVillage.Name);
E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\TaleWorlds.CampaignSystem.GameComponents\DefaultSettlementFoodModel.cs:75: 			}
E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\TaleWorlds.CampaignSystem.GameComponents\DefaultSettlementFoodModel.cs:76: 			town.AddEffectOfBuildings(BuildingEffectEnum.FoodProduction, ref bonuses);
E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\TaleWorlds.CampaignSystem.GameComponents\DefaultSettlementFoodModel.cs:77: 		}
E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\TaleWorlds.CampaignSystem.GameComponents\DefaultSettlementFoodModel.cs:78: 		else
E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\TaleWorlds.CampaignSystem.GameComponents\DefaultSettlementFoodModel.cs:79: 		{
E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\TaleWorlds.CampaignSystem.GameComponents\DefaultSettlementFoodModel.cs:80: 			PerkHelper.AddPerkBonusForTown(DefaultPerks.Roguery.DirtyFighting, town, ref bonuses);
E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\TaleWorlds.CampaignSystem.GameComponents\DefaultSettlementFoodModel.cs:81: 		}
E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\TaleWorlds.CampaignSystem.GameComponents\DefaultSettlementFoodModel.cs:82: 		if (includeMarketStocks)
E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\TaleWorlds.CampaignSystem.GameComponents\DefaultSettlementFoodModel.cs:83: 		{
E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\TaleWorlds.CampaignSystem.GameComponents\DefaultSettlementFoodModel.cs:84: 			foreach (Town.SellLog soldItem in town.SoldItems)
E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\TaleWorlds.CampaignSystem.GameComponents\DefaultSettlementFoodModel.cs:85: 			{
E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\TaleWorlds.CampaignSystem.GameComponents\DefaultSettlementFoodModel.cs:86: 				if (soldItem.Category.Properties == ItemCategory.Property.BonusToFoodStores)
E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\TaleWorlds.CampaignSystem.GameComponents\DefaultSettlementFoodModel.cs:87: 				{
E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\TaleWorlds.CampaignSystem.GameComponents\DefaultSettlementFoodModel.cs:88: 					bonuses.Add(soldItem.Number, includeDescriptions ? soldItem.Category.GetName() : null);
E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\TaleWorlds.CampaignSystem.GameComponents\DefaultSettlementFoodModel.cs:89: 				}
E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\TaleWorlds.CampaignSystem.GameComponents\DefaultSettlementFoodModel.cs:90: 			}
E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\TaleWorlds.CampaignSystem.GameComponents\DefaultSettlementFoodModel.cs:91: 		}
E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\TaleWorlds.CampaignSystem.GameComponents\DefaultSettlementFoodModel.cs:92: 		ExplainedNumber explainedNumber = new ExplainedNumber(0f, includeDescriptions);
E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\TaleWorlds.CampaignSystem.GameComponents\DefaultSettlementFoodModel.cs:93: 		explainedNumber.AddFromExplainedNumber(bonuses, null);
E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\TaleWorlds.CampaignSystem.GameComponents\DefaultSettlementFoodModel.cs:94: 		explainedNumber.SubtractFromExplainedNumber(bonuses2, null);
E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\TaleWorlds.CampaignSystem.GameComponents\DefaultSettlementFoodModel.cs:95: 		GetSettlementFoodChangeDueToIssues(town, ref explainedNumber);
E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\TaleWorlds.CampaignSystem.GameComponents\DefaultSettlementFoodModel.cs:96: 		return explainedNumber;
E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\TaleWorlds.CampaignSystem.GameComponents\DefaultSettlementFoodModel.cs:97: 	}
E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\TaleWorlds.CampaignSystem.GameComponents\DefaultSettlementFoodModel.cs:98: 
E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\TaleWorlds.CampaignSystem.GameComponents\DefaultSettlementFoodModel.cs:99: 	private static void GetSettlementFoodChangeDueToIssues(Town town, ref ExplainedNumber explainedNumber)
E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\TaleWorlds.CampaignSystem.GameComponents\DefaultSettlementFoodModel.cs:100: 	{
E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\TaleWorlds.CampaignSystem.GameComponents\DefaultSettlementFoodModel.cs:101: 		Campaign.Current.Models.IssueModel.GetIssueEffectsOfSettlement(DefaultIssueEffects.SettlementFood, town.Settlement, ref explainedNumber);
E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\TaleWorlds.CampaignSystem.GameComponents\DefaultSettlementFoodModel.cs:102: 	}
E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\TaleWorlds.CampaignSystem.GameComponents\DefaultSettlementFoodModel.cs:103: }

 succeeded in 491ms:
E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\TaleWorlds.CampaignSystem.Party\MobileParty.cs

exec
"C:\\Program Files\\PowerShell\\7\\pwsh.exe" -Command "Select-String -Path 'E:\\Decompiled_Bannerlord\\Campaign\\TaleWorlds.CampaignSystem\\TaleWorlds.CampaignSystem.Party\\MobileParty.cs' -Pattern 'public PartyBase Party|PartyBase _party|new PartyBase|set;|get;' -Context 3,4 | Select-Object -First 120 | ForEach-Object { "'$_.Context.PreContext; '"'{0}:{1}: {2}' -f "'$_.Path,$_.LineNumber,$_.Line; $_.Context.PostContext }' in C:\Users\mikew\source\repos\TAOM
exec
"C:\\Program Files\\PowerShell\\7\\pwsh.exe" -Command "Get-ChildItem -Path 'E:\\Decompiled_Bannerlord\\Campaign' -Recurse -Filter 'Town.cs' -ErrorAction SilentlyContinue | Select-Object -First 20 | ForEach-Object { "'$_.FullName }' in C:\Users\mikew\source\repos\TAOM
exec
"C:\\Program Files\\PowerShell\\7\\pwsh.exe" -Command "Select-String -Path 'E:\\Decompiled_Bannerlord\\Campaign\\TaleWorlds.CampaignSystem\\TaleWorlds.CampaignSystem.Party\\PartyBase.cs' -Pattern 'NumberOfAllMembers|MemberRoster|public TroopRoster|PartyBase\\(' -Context 3,5 | ForEach-Object { "'$_.Context.PreContext; '"'{0}:{1}: {2}' -f "'$_.Path,$_.LineNumber,$_.Line; $_.Context.PostContext }' in C:\Users\mikew\source\repos\TAOM
exec
"C:\\Program Files\\PowerShell\\7\\pwsh.exe" -Command "Get-ChildItem -Path 'E:\\Decompiled_Bannerlord\\Campaign' -Recurse -Filter 'Village.cs' -ErrorAction SilentlyContinue | Select-Object -First 20 | ForEach-Object { "'$_.FullName }' in C:\Users\mikew\source\repos\TAOM
 succeeded in 520ms:
	}

	[SaveableProperty(1002)]
E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\TaleWorlds.CampaignSystem.Party\MobileParty.cs:299: 	public Settlement LastVisitedSettlement { get; private set; }

	[SaveableProperty(1004)]
	public Vec2 Bearing { get; internal set; }

	public Settlement LastVisitedSettlement { get; private set; }

	[SaveableProperty(1004)]
E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\TaleWorlds.CampaignSystem.Party\MobileParty.cs:302: 	public Vec2 Bearing { get; internal set; }

	public MBReadOnlyList<MobileParty> AttachedParties => _attachedParties;

	[SaveableProperty(1099)]
	public MBReadOnlyList<MobileParty> AttachedParties => _attachedParties;

	[SaveableProperty(1099)]
E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\TaleWorlds.CampaignSystem.Party\MobileParty.cs:307: 	public bool HasLandNavigationCapability { get; private set; } = true;

	public MBReadOnlyList<Ship> Ships => Party.Ships;

	public bool HasNavalNavigationCapability => Campaign.Current.Models.PartyNavigationModel.HasNavalNavigationCapability(this);
	public bool HasNavalNavigationCapability => Campaign.Current.Models.PartyNavigationModel.HasNavalNavigationCapability(this);

	[SaveableProperty(1009)]
E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\TaleWorlds.CampaignSystem.Party\MobileParty.cs:314: 	public float Aggressiveness { get; set; }

	public int PaymentLimit => _partyComponent?.WagePaymentLimit ?? Campaign.Current.Models.PartyWageModel.MaxWagePaymentLimit;

	public Banner Banner
	}

	[SaveableProperty(1005)]
E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\TaleWorlds.CampaignSystem.Party\MobileParty.cs:339: 	public Vec2 ArmyPositionAdder { get; private set; }

	public CampaignVec2 AiBehaviorTarget => Ai.BehaviorTarget;

	[SaveableProperty(1090)]
	[SaveableProperty(1005)]
	public Vec2 ArmyPositionAdder { get; private set; }

E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\TaleWorlds.CampaignSystem.Party\MobileParty.cs:341: 	public CampaignVec2 AiBehaviorTarget => Ai.BehaviorTarget;

	[SaveableProperty(1090)]
	public PartyObjective Objective { get; private set; }

	public CampaignVec2 AiBehaviorTarget => Ai.BehaviorTarget;

	[SaveableProperty(1090)]
E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\TaleWorlds.CampaignSystem.Party\MobileParty.cs:344: 	public PartyObjective Objective { get; private set; }

	[CachedData]
	MobileParty ILocatable<MobileParty>.NextLocatable { get; set; }

	public PartyObjective Objective { get; private set; }

	[CachedData]
E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\TaleWorlds.CampaignSystem.Party\MobileParty.cs:347: 	MobileParty ILocatable<MobileParty>.NextLocatable { get; set; }

	[SaveableProperty(1019)]
	public MobilePartyAi Ai { get; private set; }

	MobileParty ILocatable<MobileParty>.NextLocatable { get; set; }

	[SaveableProperty(1019)]
E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\TaleWorlds.CampaignSystem.Party\MobileParty.cs:350: 	public MobilePartyAi Ai { get; private set; }

	[SaveableProperty(1020)]
	public PartyBase Party { get; private set; }

	public MobilePartyAi Ai { get; private set; }

	[SaveableProperty(1020)]
E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\TaleWorlds.CampaignSystem.Party\MobileParty.cs:353: 	public PartyBase Party { get; private set; }

	[SaveableProperty(1023)]
	public bool IsActive { get; set; }

	public PartyBase Party { get; private set; }

	[SaveableProperty(1023)]
E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\TaleWorlds.CampaignSystem.Party\MobileParty.cs:356: 	public bool IsActive { get; set; }

	public bool IsInRaftState
	{
		get
	public float LastCalculatedBaseSpeed => _lastCalculatedBaseSpeedExplained.ResultNumber;

	[CachedData]
E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\TaleWorlds.CampaignSystem.Party\MobileParty.cs:384: 	public PartyThinkParams ThinkParamsCache { get; private set; }

	public float Speed
	{
		get
	public bool IsCurrentlyUsedByAQuest => _isCurrentlyUsedByAQuest;

	[SaveableProperty(1050)]
E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\TaleWorlds.CampaignSystem.Party\MobileParty.cs:419: 	public AiBehavior ShortTermBehavior { get; internal set; }

	[SaveableProperty(1958)]
	public bool IsPartyTradeActive { get; private set; }

	public AiBehavior ShortTermBehavior { get; internal set; }

	[SaveableProperty(1958)]
E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\TaleWorlds.CampaignSystem.Party\MobileParty.cs:422: 	public bool IsPartyTradeActive { get; private set; }

	public int PartyTradeGold
	{
		get
	}

	[SaveableProperty(1957)]
E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\TaleWorlds.CampaignSystem.Party\MobileParty.cs:448: 	public int PartyTradeTaxGold { get; private set; }

	[SaveableProperty(1960)]
	public CampaignTime StationaryStartTime { get; private set; }

	public int PartyTradeTaxGold { get; private set; }

	[SaveableProperty(1960)]
E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\TaleWorlds.CampaignSystem.Party\MobileParty.cs:451: 	public CampaignTime StationaryStartTime { get; private set; }

	[CachedData]
	public int VersionNo { get; private set; }

	public CampaignTime StationaryStartTime { get; private set; }

	[CachedData]
E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\TaleWorlds.CampaignSystem.Party\MobileParty.cs:454: 	public int VersionNo { get; private set; }

	[SaveableProperty(1080)]
	public bool ShouldJoinPlayerBattles { get; set; }

	public int VersionNo { get; private set; }

	[SaveableProperty(1080)]
E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\TaleWorlds.CampaignSystem.Party\MobileParty.cs:457: 	public bool ShouldJoinPlayerBattles { get; set; }

	[SaveableProperty(1081)]
	public bool IsDisbanding { get; set; }

	public bool ShouldJoinPlayerBattles { get; set; }

	[SaveableProperty(1081)]
E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\TaleWorlds.CampaignSystem.Party\MobileParty.cs:460: 	public bool IsDisbanding { get; set; }

	public int RandomValue => Party.RandomValue;

	public NavigationType NavigationCapability
	}

	[CachedData]
E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\TaleWorlds.CampaignSystem.Party\MobileParty.cs:509: 	public bool IsNavalVisualDirty { get; private set; }

	public bool IsTargetingPort
	{
		get
	}

	[SaveableProperty(1092)]
E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\TaleWorlds.CampaignSystem.Party\MobileParty.cs:528: 	public AnchorPoint Anchor { get; private set; }

	public bool IsTransitionInProgress => NavigationTransitionStartTime != CampaignTime.Zero;

	[SaveableProperty(223)]
	public bool IsTransitionInProgress => NavigationTransitionStartTime != CampaignTime.Zero;

	[SaveableProperty(223)]
E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\TaleWorlds.CampaignSystem.Party\MobileParty.cs:533: 	public CampaignVec2 EndPositionForNavigationTransition { get; private set; }

	public CampaignTime NavigationTransitionStartTime
	{
		get
	}

	[SaveableProperty(1097)]
E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\TaleWorlds.CampaignSystem.Party\MobileParty.cs:563: 	public CampaignTime NavigationTransitionDuration { get; private set; } = CampaignTime.Zero;

	public NavigationType DesiredAiNavigationType
	{
		get
	public Hero LeaderHero => PartyComponent?.Leader;

	[SaveableProperty(1070)]
E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\TaleWorlds.CampaignSystem.Party\MobileParty.cs:778: 	private Hero Scout { get; set; }

	[SaveableProperty(1072)]
	private Hero Engineer { get; set; }

	private Hero Scout { get; set; }

	[SaveableProperty(1072)]
E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\TaleWorlds.CampaignSystem.Party\MobileParty.cs:781: 	private Hero Engineer { get; set; }

	[SaveableProperty(1071)]
	private Hero Quartermaster { get; set; }

	private Hero Engineer { get; set; }

	[SaveableProperty(1071)]
E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\TaleWorlds.CampaignSystem.Party\MobileParty.cs:784: 	private Hero Quartermaster { get; set; }

	[SaveableProperty(1073)]
	private Hero Surgeon { get; set; }

	private Hero Quartermaster { get; set; }

	[SaveableProperty(1073)]
E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\TaleWorlds.CampaignSystem.Party\MobileParty.cs:787: 	private Hero Surgeon { get; set; }

	[SaveableProperty(1076)]
	private Hero FirstMate { get; set; }

	private Hero Surgeon { get; set; }

	[SaveableProperty(1076)]
E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\TaleWorlds.CampaignSystem.Party\MobileParty.cs:790: 	private Hero FirstMate { get; set; }

	[SaveableProperty(1077)]
	private Hero Navigator { get; set; }

	private Hero FirstMate { get; set; }

	[SaveableProperty(1077)]
E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\TaleWorlds.CampaignSystem.Party\MobileParty.cs:793: 	private Hero Navigator { get; set; }

	public Hero Owner => _partyComponent?.PartyOwner;

	public Hero EffectiveScout
	public PathFaceRecord CurrentNavigationFace => Position.Face;

	[CachedData]
E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\TaleWorlds.CampaignSystem.Party\MobileParty.cs:988: 	public int PathBegin { get; private set; }

	[CachedData]
	public bool ForceAiNoPathMode { get; set; }

	public int PathBegin { get; private set; }

	[CachedData]
E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\TaleWorlds.CampaignSystem.Party\MobileParty.cs:991: 	public bool ForceAiNoPathMode { get; set; }

	public Vec2 EventPositionAdder
	{
		get
	public PartyComponent PartyComponent => _partyComponent;

	[CachedData]
E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\TaleWorlds.CampaignSystem.Party\MobileParty.cs:1234: 	public bool IsMilitia { get; private set; }

	[CachedData]
	public bool IsLordParty { get; private set; }

	public bool IsMilitia { get; private set; }

	[CachedData]
E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\TaleWorlds.CampaignSystem.Party\MobileParty.cs:1237: 	public bool IsLordParty { get; private set; }

	[CachedData]
	public bool IsVillager { get; private set; }

	public bool IsLordParty { get; private set; }

	[CachedData]
E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\TaleWorlds.CampaignSystem.Party\MobileParty.cs:1240: 	public bool IsVillager { get; private set; }

	[CachedData]
	public bool IsCaravan { get; private set; }

	public bool IsVillager { get; private set; }

	[CachedData]
E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\TaleWorlds.CampaignSystem.Party\MobileParty.cs:1243: 	public bool IsCaravan { get; private set; }

	[CachedData]
	public bool IsPatrolParty { get; private set; }

	public bool IsCaravan { get; private set; }

	[CachedData]
E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\TaleWorlds.CampaignSystem.Party\MobileParty.cs:1246: 	public bool IsPatrolParty { get; private set; }

	[CachedData]
	public bool IsGarrison { get; private set; }

	public bool IsPatrolParty { get; private set; }

	[CachedData]
E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\TaleWorlds.CampaignSystem.Party\MobileParty.cs:1249: 	public bool IsGarrison { get; private set; }

	[CachedData]
	public bool IsCustomParty { get; private set; }

	public bool IsGarrison { get; private set; }

	[CachedData]
E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\TaleWorlds.CampaignSystem.Party\MobileParty.cs:1252: 	public bool IsCustomParty { get; private set; }

	[CachedData]
	public bool IsBandit { get; private set; }

	public bool IsCustomParty { get; private set; }

	[CachedData]
E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\TaleWorlds.CampaignSystem.Party\MobileParty.cs:1255: 	public bool IsBandit { get; private set; }

	public bool IsBanditBossParty
	{
		get
		_isVisible = false;
		IsActive = true;
		_isCurrentlyUsedByAQuest = false;
E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\TaleWorlds.CampaignSystem.Party\MobileParty.cs:1856: 		Party = new PartyBase(this);
		Anchor = new AnchorPoint(this);
		InitMembers();
		InitCached();
		Initialize();

 succeeded in 526ms:
	public bool IsMobile => MobileParty != null;

	[SaveableProperty(3)]
E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\TaleWorlds.CampaignSystem.Party\PartyBase.cs:129: 	public TroopRoster MemberRoster { get; private set; }

	[SaveableProperty(4)]
	public TroopRoster PrisonRoster { get; private set; }

	[SaveableProperty(5)]
	public TroopRoster MemberRoster { get; private set; }

	[SaveableProperty(4)]
E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\TaleWorlds.CampaignSystem.Party\PartyBase.cs:132: 	public TroopRoster PrisonRoster { get; private set; }

	[SaveableProperty(5)]
	public ItemRoster ItemRoster { get; private set; }

	public TextObject Name
	{
		get
		{
E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\TaleWorlds.CampaignSystem.Party\PartyBase.cs:347: 			int versionNo = MemberRoster.VersionNo;
			if (_partyMemberSizeLastCheckVersion != versionNo || _cachedPartyMemberSizeLimit == 0)
			{
				_partyMemberSizeLastCheckVersion = versionNo;
				_cachedPartyMemberSizeLimit = (int)Campaign.Current.Models.PartySizeLimitModel.GetPartyMemberSizeLimit(this).ResultNumber;
			}

	public ExplainedNumber PrisonerSizeLimitExplainer => Campaign.Current.Models.PartySizeLimitModel.GetPartyPrisonerSizeLimit(this, includeDescriptions: true);

E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\TaleWorlds.CampaignSystem.Party\PartyBase.cs:375: 	public int NumberOfHealthyMembers => MemberRoster.TotalManCount - MemberRoster.TotalWounded;

	public int NumberOfRegularMembers => MemberRoster.TotalRegulars;

	public int NumberOfWoundedTotalMembers => MemberRoster.TotalWounded;


	public int NumberOfHealthyMembers => MemberRoster.TotalManCount - MemberRoster.TotalWounded;

E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\TaleWorlds.CampaignSystem.Party\PartyBase.cs:377: 	public int NumberOfRegularMembers => MemberRoster.TotalRegulars;

	public int NumberOfWoundedTotalMembers => MemberRoster.TotalWounded;

	public int NumberOfAllMembers => MemberRoster.TotalManCount;


	public int NumberOfRegularMembers => MemberRoster.TotalRegulars;

E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\TaleWorlds.CampaignSystem.Party\PartyBase.cs:379: 	public int NumberOfWoundedTotalMembers => MemberRoster.TotalWounded;

	public int NumberOfAllMembers => MemberRoster.TotalManCount;

	public int NumberOfPrisoners => PrisonRoster.TotalManCount;


	public int NumberOfWoundedTotalMembers => MemberRoster.TotalWounded;

E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\TaleWorlds.CampaignSystem.Party\PartyBase.cs:381: 	public int NumberOfAllMembers => MemberRoster.TotalManCount;

	public int NumberOfPrisoners => PrisonRoster.TotalManCount;

	public int NumberOfMounts => ItemRoster.NumberOfMounts;

	{
		get
		{
E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\TaleWorlds.CampaignSystem.Party\PartyBase.cs:411: 			if (_lastNumberOfMenWithHorseVersionNo != MemberRoster.VersionNo)
			{
				RecalculateNumberOfMenWithHorses();
				_lastNumberOfMenWithHorseVersionNo = MemberRoster.VersionNo;
			}
			return _numberOfMenWithHorse;
			if (_lastNumberOfMenWithHorseVersionNo != MemberRoster.VersionNo)
			{
				RecalculateNumberOfMenWithHorses();
E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\TaleWorlds.CampaignSystem.Party\PartyBase.cs:414: 				_lastNumberOfMenWithHorseVersionNo = MemberRoster.VersionNo;
			}
			return _numberOfMenWithHorse;
		}
	}

		}
	}

E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\TaleWorlds.CampaignSystem.Party\PartyBase.cs:420: 	public int NumberOfMenWithoutHorse => NumberOfAllMembers - NumberOfMenWithHorse;

	public float EstimatedStrength
	{
		get
		{
	[CachedData]
	public bool IsVisualDirty { get; private set; }

E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\TaleWorlds.CampaignSystem.Party\PartyBase.cs:456: 	internal static void AutoGeneratedStaticCollectObjectsPartyBase(object o, List<object> collectedObjects)
	{
		((PartyBase)o).AutoGeneratedInstanceCollectObjects(collectedObjects);
	}

	private void AutoGeneratedInstanceCollectObjects(List<object> collectedObjects)
		collectedObjects.Add(_ships);
		collectedObjects.Add(Settlement);
		collectedObjects.Add(MobileParty);
E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\TaleWorlds.CampaignSystem.Party\PartyBase.cs:469: 		collectedObjects.Add(MemberRoster);
		collectedObjects.Add(PrisonRoster);
		collectedObjects.Add(ItemRoster);
		collectedObjects.Add(CustomName);
		collectedObjects.Add(CustomBanner);
	}
		return ((PartyBase)o).MobileParty;
	}

E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\TaleWorlds.CampaignSystem.Party\PartyBase.cs:486: 	internal static object AutoGeneratedGetMemberValueMemberRoster(object o)
	{
		return ((PartyBase)o).MemberRoster;
	}

	internal static object AutoGeneratedGetMemberValuePrisonRoster(object o)

	internal static object AutoGeneratedGetMemberValueMemberRoster(object o)
	{
E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\TaleWorlds.CampaignSystem.Party\PartyBase.cs:488: 		return ((PartyBase)o).MemberRoster;
	}

	internal static object AutoGeneratedGetMemberValuePrisonRoster(object o)
	{
		return ((PartyBase)o).PrisonRoster;
		}
		if (MBSaveLoad.LastLoadedGameVersion < ApplicationVersion.FromString("v1.2.0"))
		{
E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\TaleWorlds.CampaignSystem.Party\PartyBase.cs:772: 			MemberRoster.RemoveZeroCounts();
		}
	}

	internal void InitCache()
	{
			_numberOfHealthyMenPerTier = new int[num + 1];
			flag = true;
		}
E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\TaleWorlds.CampaignSystem.Party\PartyBase.cs:809: 		else if (_lastNumberOfMenPerTierVersionNo != MemberRoster.VersionNo)
		{
			flag = true;
		}
		if (flag)
		{
			{
				_numberOfHealthyMenPerTier[i] = 0;
			}
E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\TaleWorlds.CampaignSystem.Party\PartyBase.cs:819: 			for (int j = 0; j < MemberRoster.Count; j++)
			{
				CharacterObject characterAtIndex = MemberRoster.GetCharacterAtIndex(j);
				if (characterAtIndex != null && !characterAtIndex.IsHero)
				{
					int tier2 = characterAtIndex.Tier;
			}
			for (int j = 0; j < MemberRoster.Count; j++)
			{
E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\TaleWorlds.CampaignSystem.Party\PartyBase.cs:821: 				CharacterObject characterAtIndex = MemberRoster.GetCharacterAtIndex(j);
				if (characterAtIndex != null && !characterAtIndex.IsHero)
				{
					int tier2 = characterAtIndex.Tier;
					if (tier2 >= 0 && tier2 < _numberOfHealthyMenPerTier.Length)
					{
					int tier2 = characterAtIndex.Tier;
					if (tier2 >= 0 && tier2 < _numberOfHealthyMenPerTier.Length)
					{
E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\TaleWorlds.CampaignSystem.Party\PartyBase.cs:827: 						int num2 = MemberRoster.GetElementNumber(j) - MemberRoster.GetElementWoundedNumber(j);
						_numberOfHealthyMenPerTier[tier2] += num2;
					}
				}
			}
			_lastNumberOfMenPerTierVersionNo = MemberRoster.VersionNo;
					}
				}
			}
E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\TaleWorlds.CampaignSystem.Party\PartyBase.cs:832: 			_lastNumberOfMenPerTierVersionNo = MemberRoster.VersionNo;
		}
		return _numberOfHealthyMenPerTier[tier];
	}

	private int GetStrengthVersionNo()

	private int GetStrengthVersionNo()
	{
E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\TaleWorlds.CampaignSystem.Party\PartyBase.cs:839: 		int num = MemberRoster.VersionNo;
		if (IsMobile && MobileParty.IsCurrentlyAtSea)
		{
			num = GetShipsVersion() + num * 13;
		}
		num <<= 1;
		return Campaign.Current.Models.MilitaryPowerModel.GetPowerOfParty(this, side, context);
	}

E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\TaleWorlds.CampaignSystem.Party\PartyBase.cs:899: 	public PartyBase(MobileParty mobileParty)
		: this(mobileParty, null)
	{
	}

	public PartyBase(Settlement settlement)
	{
	}

E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\TaleWorlds.CampaignSystem.Party\PartyBase.cs:904: 	public PartyBase(Settlement settlement)
		: this(null, settlement)
	{
	}

	private PartyBase(MobileParty mobileParty, Settlement settlement)
	{
	}

E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\TaleWorlds.CampaignSystem.Party\PartyBase.cs:909: 	private PartyBase(MobileParty mobileParty, Settlement settlement)
	{
		Index = Campaign.Current.GeneratePartyId(this);
		MobileParty = mobileParty;
		Settlement = settlement;
		ItemRoster = new ItemRoster();
		MobileParty = mobileParty;
		Settlement = settlement;
		ItemRoster = new ItemRoster();
E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\TaleWorlds.CampaignSystem.Party\PartyBase.cs:915: 		MemberRoster = new TroopRoster(this);
		PrisonRoster = new TroopRoster(this);
	}

	internal void AddShipInternal(Ship ship)
	{
	private void RecalculateNumberOfMenWithHorses()
	{
		_numberOfMenWithHorse = 0;
E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\TaleWorlds.CampaignSystem.Party\PartyBase.cs:943: 		for (int i = 0; i < MemberRoster.Count; i++)
		{
			TroopRosterElement elementCopyAtIndex = MemberRoster.GetElementCopyAtIndex(i);
			if (elementCopyAtIndex.Character != null && elementCopyAtIndex.Character.IsMounted)
			{
				_numberOfMenWithHorse += elementCopyAtIndex.Number;
		_numberOfMenWithHorse = 0;
		for (int i = 0; i < MemberRoster.Count; i++)
		{
E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\TaleWorlds.CampaignSystem.Party\PartyBase.cs:945: 			TroopRosterElement elementCopyAtIndex = MemberRoster.GetElementCopyAtIndex(i);
			if (elementCopyAtIndex.Character != null && elementCopyAtIndex.Character.IsMounted)
			{
				_numberOfMenWithHorse += elementCopyAtIndex.Number;
			}
		}
	public int GetNumberOfMenWith(TraitObject trait)
	{
		int num = 0;
E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\TaleWorlds.CampaignSystem.Party\PartyBase.cs:956: 		foreach (TroopRosterElement item in MemberRoster.GetTroopRoster())
		{
			if (item.Character.GetTraitLevel(trait) > 0)
			{
				num += item.Number;
			}

	public int AddMember(CharacterObject element, int numberToAdd, int numberToAddWounded = 0)
	{
E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\TaleWorlds.CampaignSystem.Party\PartyBase.cs:973: 		return MemberRoster.AddToCounts(element, numberToAdd, insertAtFront: false, numberToAddWounded);
	}

	public void AddPrisoners(TroopRoster roster)
	{
		foreach (TroopRosterElement item in roster.GetTroopRoster())

	public void AddMembers(TroopRoster roster)
	{
E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\TaleWorlds.CampaignSystem.Party\PartyBase.cs:986: 		MemberRoster.Add(roster);
	}

	public override string ToString()
	{
		if (!IsSettlement)
		return Settlement.Name.ToString();
	}

E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\TaleWorlds.CampaignSystem.Party\PartyBase.cs:998: 	public int AddElementToMemberRoster(CharacterObject element, int numberToAdd, bool insertAtFront = false)
	{
		return MemberRoster.AddToCounts(element, numberToAdd, insertAtFront);
	}

	public void AddToMemberRosterElementAtIndex(int index, int numberToAdd, int woundedCount = 0)

	public int AddElementToMemberRoster(CharacterObject element, int numberToAdd, bool insertAtFront = false)
	{
E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\TaleWorlds.CampaignSystem.Party\PartyBase.cs:1000: 		return MemberRoster.AddToCounts(element, numberToAdd, insertAtFront);
	}

	public void AddToMemberRosterElementAtIndex(int index, int numberToAdd, int woundedCount = 0)
	{
		MemberRoster.AddToCountsAtIndex(index, numberToAdd, woundedCount);
		return MemberRoster.AddToCounts(element, numberToAdd, insertAtFront);
	}

E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\TaleWorlds.CampaignSystem.Party\PartyBase.cs:1003: 	public void AddToMemberRosterElementAtIndex(int index, int numberToAdd, int woundedCount = 0)
	{
		MemberRoster.AddToCountsAtIndex(index, numberToAdd, woundedCount);
	}

	public void WoundMemberRosterElements(CharacterObject elementObj, int numberToWound)

	public void AddToMemberRosterElementAtIndex(int index, int numberToAdd, int woundedCount = 0)
	{
E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\TaleWorlds.CampaignSystem.Party\PartyBase.cs:1005: 		MemberRoster.AddToCountsAtIndex(index, numberToAdd, woundedCount);
	}

	public void WoundMemberRosterElements(CharacterObject elementObj, int numberToWound)
	{
		MemberRoster.AddToCounts(elementObj, 0, insertAtFront: false, numberToWound);
		MemberRoster.AddToCountsAtIndex(index, numberToAdd, woundedCount);
	}

E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\TaleWorlds.CampaignSystem.Party\PartyBase.cs:1008: 	public void WoundMemberRosterElements(CharacterObject elementObj, int numberToWound)
	{
		MemberRoster.AddToCounts(elementObj, 0, insertAtFront: false, numberToWound);
	}

	public void WoundMemberRosterElementsWithIndex(int elementIndex, int numberToWound)

	public void WoundMemberRosterElements(CharacterObject elementObj, int numberToWound)
	{
E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\TaleWorlds.CampaignSystem.Party\PartyBase.cs:1010: 		MemberRoster.AddToCounts(elementObj, 0, insertAtFront: false, numberToWound);
	}

	public void WoundMemberRosterElementsWithIndex(int elementIndex, int numberToWound)
	{
		MemberRoster.AddToCountsAtIndex(elementIndex, 0, numberToWound);
		MemberRoster.AddToCounts(elementObj, 0, insertAtFront: false, numberToWound);
	}

E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\TaleWorlds.CampaignSystem.Party\PartyBase.cs:1013: 	public void WoundMemberRosterElementsWithIndex(int elementIndex, int numberToWound)
	{
		MemberRoster.AddToCountsAtIndex(elementIndex, 0, numberToWound);
	}

	public void UpdateVisibilityAndInspected(CampaignVec2 fromPosition, float mainPartySeeingRange = 0f)

	public void WoundMemberRosterElementsWithIndex(int elementIndex, int numberToWound)
	{
E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\TaleWorlds.CampaignSystem.Party\PartyBase.cs:1015: 		MemberRoster.AddToCountsAtIndex(elementIndex, 0, numberToWound);
	}

	public void UpdateVisibilityAndInspected(CampaignVec2 fromPosition, float mainPartySeeingRange = 0f)
	{
		bool isVisible = false;

	internal void OnRosterSizeChanged(TroopRoster roster)
	{
E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\TaleWorlds.CampaignSystem.Party\PartyBase.cs:1199: 		if (object.Equals(roster, MemberRoster))
		{
			CampaignEventDispatcher.Instance.OnPartySizeChanged(this);
		}
	}
}

 succeeded in 531ms:
E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\TaleWorlds.CampaignSystem.Settlements\Village.cs

 succeeded in 548ms:
E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\TaleWorlds.CampaignSystem.Settlements\Town.cs

codex
The vanilla food model matches the pasted shape: it computes prosperity and garrison consumption before the siege branch, then only adds lands/village production inside `!town.IsUnderSiege`. I’m checking the actual `Town`/`MobileParty` boundaries next so the null/edge suspect is calibrated against vanilla rather than treated as a TAOM-only issue.
exec
"C:\\Program Files\\PowerShell\\7\\pwsh.exe" -Command "Select-String -Path 'E:\\Decompiled_Bannerlord\\Campaign\\TaleWorlds.CampaignSystem\\TaleWorlds.CampaignSystem.Settlements\\Town.cs' -Pattern 'GarrisonParty|Owner|BoundVillages|IsUnderSiege|IsTown' -Context 2,4 | Select-Object -First 100 | ForEach-Object { "'$_.Context.PreContext; '"'{0}:{1}: {2}' -f "'$_.Path,$_.LineNumber,$_.Line; $_.Context.PostContext }' in C:\Users\mikew\source\repos\TAOM
exec
"C:\\Program Files\\PowerShell\\7\\pwsh.exe" -Command "Select-String -Path 'E:\\Decompiled_Bannerlord\\Campaign\\TaleWorlds.CampaignSystem\\TaleWorlds.CampaignSystem.Settlements\\Village.cs' -Pattern 'enum VillageStates|VillageState|GetHearthLevel|Normal' -Context 2,5 | Select-Object -First 120 | ForEach-Object { "'$_.Context.PreContext; '"'{0}:{1}: {2}' -f "'$_.Path,$_.LineNumber,$_.Line; $_.Context.PostContext }' in C:\Users\mikew\source\repos\TAOM
exec
"C:\\Program Files\\PowerShell\\7\\pwsh.exe" -Command '$path='"'E:\\Decompiled_Bannerlord\\Campaign\\TaleWorlds.CampaignSystem\\TaleWorlds.CampaignSystem.Party\\MobileParty.cs'; "'$i=0; Get-Content $path | ForEach-Object { $i++; if(($i -ge 345 -and $i -le 360) -or ($i -ge 1848 -and $i -le 1862)){ '"'{0}:{1}: {2}' -f "'$path,$i,$_ } }' in C:\Users\mikew\source\repos\TAOM
exec
"C:\\Program Files\\PowerShell\\7\\pwsh.exe" -Command '$path='"'E:\\Decompiled_Bannerlord\\Campaign\\TaleWorlds.CampaignSystem\\TaleWorlds.CampaignSystem.Party\\PartyBase.cs'; "'$i=0; Get-Content $path | ForEach-Object { $i++; if(($i -ge 375 -and $i -le 382) -or ($i -ge 899 -and $i -le 916)){ '"'{0}:{1}: {2}' -f "'$path,$i,$_ } }' in C:\Users\mikew\source\repos\TAOM
 succeeded in 498ms:

	[SaveableField(1040)]
E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\TaleWorlds.CampaignSystem.Settlements\Town.cs:77: 	private Clan _ownerClan;

	[SaveableField(1015)]
	private float _security;


	[CachedData]
E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\TaleWorlds.CampaignSystem.Settlements\Town.cs:86: 	private MBList<Village> _tradeBoundVillagesCache;

	[SaveableField(1006)]
	public MBList<Building> Buildings;

	}

E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\TaleWorlds.CampaignSystem.Settlements\Town.cs:128: 	public CultureObject Culture => base.Owner.Settlement.Culture;

	public float ProsperityChange => Campaign.Current.Models.SettlementProsperityModel.CalculateProsperityChange(this).ResultNumber;

	public ExplainedNumber ProsperityChangeExplanation => Campaign.Current.Models.SettlementProsperityModel.CalculateProsperityChange(this, includeDescriptions: true);
	public ExplainedNumber SecurityChangeExplanation => Campaign.Current.Models.SettlementSecurityModel.CalculateSecurityChange(this, includeDescriptions: true);

E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\TaleWorlds.CampaignSystem.Settlements\Town.cs:148: 	public float MilitiaChange => Campaign.Current.Models.SettlementMilitiaModel.CalculateMilitiaChange(base.Owner.Settlement).ResultNumber;

	public ExplainedNumber MilitiaChangeExplanation => Campaign.Current.Models.SettlementMilitiaModel.CalculateMilitiaChange(base.Owner.Settlement, includeDescriptions: true);

	public float Construction => Campaign.Current.Models.BuildingConstructionModel.CalculateDailyConstructionPower(this).ResultNumber;
	public float MilitiaChange => Campaign.Current.Models.SettlementMilitiaModel.CalculateMilitiaChange(base.Owner.Settlement).ResultNumber;

E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\TaleWorlds.CampaignSystem.Settlements\Town.cs:150: 	public ExplainedNumber MilitiaChangeExplanation => Campaign.Current.Models.SettlementMilitiaModel.CalculateMilitiaChange(base.Owner.Settlement, includeDescriptions: true);

	public float Construction => Campaign.Current.Models.BuildingConstructionModel.CalculateDailyConstructionPower(this).ResultNumber;

	public ExplainedNumber ConstructionExplanation => Campaign.Current.Models.BuildingConstructionModel.CalculateDailyConstructionPower(this, includeDescriptions: true);
	public ExplainedNumber ConstructionExplanation => Campaign.Current.Models.BuildingConstructionModel.CalculateDailyConstructionPower(this, includeDescriptions: true);

E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\TaleWorlds.CampaignSystem.Settlements\Town.cs:156: 	public Clan OwnerClan
	{
		get
		{
			return _ownerClan;
		get
		{
E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\TaleWorlds.CampaignSystem.Settlements\Town.cs:160: 			return _ownerClan;
		}
		set
		{
			if (_ownerClan != value)
		set
		{
E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\TaleWorlds.CampaignSystem.Settlements\Town.cs:164: 			if (_ownerClan != value)
			{
				ChangeClanInternal(value);
			}
		}
	}

E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\TaleWorlds.CampaignSystem.Settlements\Town.cs:211: 	public MBReadOnlyList<Village> TradeBoundVillages => _tradeBoundVillagesCache;

	[SaveableProperty(1005)]
	public Workshop[] Workshops { get; protected set; }

	public static MBReadOnlyList<Town> AllCastles => Campaign.Current.AllCastles;

E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\TaleWorlds.CampaignSystem.Settlements\Town.cs:298: 	public override bool IsTown => !_isCastle;

	public override bool IsCastle => _isCastle;

	public IReadOnlyCollection<SellLog> SoldItems => _soldItems;
	public IReadOnlyCollection<SellLog> SoldItems => _soldItems;

E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\TaleWorlds.CampaignSystem.Settlements\Town.cs:304: 	public override IFaction MapFaction => OwnerClan?.MapFaction;

	public bool IsUnderSiege => base.Settlement.IsUnderSiege;

	[CachedData]
	public override IFaction MapFaction => OwnerClan?.MapFaction;

E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\TaleWorlds.CampaignSystem.Settlements\Town.cs:306: 	public bool IsUnderSiege => base.Settlement.IsUnderSiege;

	[CachedData]
	public MBReadOnlyList<Village> Villages => base.Settlement.BoundVillages;


	[CachedData]
E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\TaleWorlds.CampaignSystem.Settlements\Town.cs:309: 	public MBReadOnlyList<Village> Villages => base.Settlement.BoundVillages;

	[SaveableProperty(1030)]
	public Clan LastCapturedBy { get; set; }

		get
		{
E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\TaleWorlds.CampaignSystem.Settlements\Town.cs:318: 			if (IsTown)
			{
				return Campaign.Current.TournamentManager.GetTournamentGame(this) != null;
			}
			return false;
		collectedObjects.Add(Buildings);
		collectedObjects.Add(BuildingsInProgress);
E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\TaleWorlds.CampaignSystem.Settlements\Town.cs:336: 		collectedObjects.Add(_ownerClan);
		collectedObjects.Add(_marketData);
		collectedObjects.Add(_governor);
		collectedObjects.Add(_soldItems);
		collectedObjects.Add(Workshops);
	}

E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\TaleWorlds.CampaignSystem.Settlements\Town.cs:384: 	internal static object AutoGeneratedGetMemberValue_ownerClan(object o)
	{
		return ((Town)o)._ownerClan;
	}

	internal static object AutoGeneratedGetMemberValue_ownerClan(object o)
	{
E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\TaleWorlds.CampaignSystem.Settlements\Town.cs:386: 		return ((Town)o)._ownerClan;
	}

	internal static object AutoGeneratedGetMemberValue_security(object o)
	{
	internal void SetTradeBoundVillageInternal(Village village)
	{
E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\TaleWorlds.CampaignSystem.Settlements\Town.cs:452: 		_tradeBoundVillagesCache.Add(village);
	}

	internal void RemoveTradeBoundVillageInternal(Village village)
	{
	internal void RemoveTradeBoundVillageInternal(Village village)
	{
E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\TaleWorlds.CampaignSystem.Settlements\Town.cs:457: 		_tradeBoundVillagesCache.Remove(village);
	}

	public int FoodStocksUpperLimit()
	{
		Workshops = new Workshop[0];
		_marketData = new TownMarketData(this);
E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\TaleWorlds.CampaignSystem.Settlements\Town.cs:477: 		_tradeBoundVillagesCache = new MBList<Village>();
	}

	public override void OnInit()
	{
	public override void OnInit()
	{
E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\TaleWorlds.CampaignSystem.Settlements\Town.cs:482: 		Loyalty = base.Owner.RandomIntWithSeed(1337u, 30, 70);
		Security = base.Owner.RandomIntWithSeed(1001u, 40, 60);
		TradeTaxAccumulated = (IsTown ? (1000 + MBRandom.RandomInt(1000)) : 0);
		ChangeGold(20000);
	}
	{
		Loyalty = base.Owner.RandomIntWithSeed(1337u, 30, 70);
E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\TaleWorlds.CampaignSystem.Settlements\Town.cs:483: 		Security = base.Owner.RandomIntWithSeed(1001u, 40, 60);
		TradeTaxAccumulated = (IsTown ? (1000 + MBRandom.RandomInt(1000)) : 0);
		ChangeGold(20000);
	}

		Loyalty = base.Owner.RandomIntWithSeed(1337u, 30, 70);
		Security = base.Owner.RandomIntWithSeed(1001u, 40, 60);
E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\TaleWorlds.CampaignSystem.Settlements\Town.cs:484: 		TradeTaxAccumulated = (IsTown ? (1000 + MBRandom.RandomInt(1000)) : 0);
		ChangeGold(20000);
	}

	public override void OnSessionStart()
			for (int i = 0; i < count; i++)
			{
E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\TaleWorlds.CampaignSystem.Settlements\Town.cs:502: 				Workshops[i] = new Workshop(base.Owner.Settlement, "workshop_" + i);
			}
		}
	}

	private void OnLoad()
	{
E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\TaleWorlds.CampaignSystem.Settlements\Town.cs:510: 		_tradeBoundVillagesCache = new MBList<Village>();
	}

	protected override void PreAfterLoad()
	{
	protected override void PreAfterLoad()
	{
E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\TaleWorlds.CampaignSystem.Settlements\Town.cs:515: 		_ownerClan?.OnFortificationAdded(this);
	}

	protected override void AfterLoad()
	{
			BuildingsInProgress.Clear();
		}
E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\TaleWorlds.CampaignSystem.Settlements\Town.cs:550: 		if (MBSaveLoad.IsUpdatingGameVersion && MBSaveLoad.LastLoadedGameVersion < ApplicationVersion.FromString("v1.3.0") && (!OwnerClan.MapFaction.IsKingdomFaction || (OwnerClan.MapFaction as Kingdom).Clans.Count == 1))
		{
			base.IsOwnerUnassigned = false;
		}
		if (Governor != null && Governor.GovernorOf == null)
		if (MBSaveLoad.IsUpdatingGameVersion && MBSaveLoad.LastLoadedGameVersion < ApplicationVersion.FromString("v1.3.0") && (!OwnerClan.MapFaction.IsKingdomFaction || (OwnerClan.MapFaction as Kingdom).Clans.Count == 1))
		{
E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\TaleWorlds.CampaignSystem.Settlements\Town.cs:552: 			base.IsOwnerUnassigned = false;
		}
		if (Governor != null && Governor.GovernorOf == null)
		{
			Governor = null;
	private void ChangeClanInternal(Clan value)
	{
E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\TaleWorlds.CampaignSystem.Settlements\Town.cs:562: 		if (_ownerClan != null)
		{
			RemoveOwnerClan();
		}
		_ownerClan = value;
		if (_ownerClan != null)
		{
E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\TaleWorlds.CampaignSystem.Settlements\Town.cs:564: 			RemoveOwnerClan();
		}
		_ownerClan = value;
		if (_ownerClan != null)
		{
			RemoveOwnerClan();
		}
E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\TaleWorlds.CampaignSystem.Settlements\Town.cs:566: 		_ownerClan = value;
		if (_ownerClan != null)
		{
			SetNewOwnerClan();
		}
		}
		_ownerClan = value;
E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\TaleWorlds.CampaignSystem.Settlements\Town.cs:567: 		if (_ownerClan != null)
		{
			SetNewOwnerClan();
		}
	}
		if (_ownerClan != null)
		{
E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\TaleWorlds.CampaignSystem.Settlements\Town.cs:569: 			SetNewOwnerClan();
		}
	}

	public void AddEffectOfBuildings(BuildingEffectEnum buildingEffect, ref ExplainedNumber result)
	}

E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\TaleWorlds.CampaignSystem.Settlements\Town.cs:581: 	private void SetNewOwnerClan()
	{
		_ownerClan.OnFortificationAdded(this);
		foreach (Village boundVillage in base.Settlement.BoundVillages)
		{
	private void SetNewOwnerClan()
	{
E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\TaleWorlds.CampaignSystem.Settlements\Town.cs:583: 		_ownerClan.OnFortificationAdded(this);
		foreach (Village boundVillage in base.Settlement.BoundVillages)
		{
			boundVillage.Settlement.Party.SetVisualAsDirty();
			boundVillage.VillagerPartyComponent?.MobileParty.Party.SetVisualAsDirty();
	{
		_ownerClan.OnFortificationAdded(this);
E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\TaleWorlds.CampaignSystem.Settlements\Town.cs:584: 		foreach (Village boundVillage in base.Settlement.BoundVillages)
		{
			boundVillage.Settlement.Party.SetVisualAsDirty();
			boundVillage.VillagerPartyComponent?.MobileParty.Party.SetVisualAsDirty();
		}
	}

E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\TaleWorlds.CampaignSystem.Settlements\Town.cs:591: 	private void RemoveOwnerClan()
	{
		_ownerClan.OnFortificationRemoved(this);
	}

	private void RemoveOwnerClan()
	{
E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\TaleWorlds.CampaignSystem.Settlements\Town.cs:593: 		_ownerClan.OnFortificationRemoved(this);
	}

	internal void DailyTick()
	{
		if (base.FoodStocks > 0f)
		{
E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\TaleWorlds.CampaignSystem.Settlements\Town.cs:602: 			base.Owner.OnConsumedFood();
		}
		base.FoodStocks += FoodChange;
		if (base.FoodStocks < 0f)
		{
		{
			base.FoodStocks = 0f;
E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\TaleWorlds.CampaignSystem.Settlements\Town.cs:608: 			base.Owner.RemainingFoodPercentage = -100;
		}
		else
		{
			base.Owner.RemainingFoodPercentage = 0;
		else
		{
E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\TaleWorlds.CampaignSystem.Settlements\Town.cs:612: 			base.Owner.RemainingFoodPercentage = 0;
		}
		if (base.FoodStocks > (float)FoodStocksUpperLimit())
		{
			base.FoodStocks = FoodStocksUpperLimit();
		}
		Prosperity += ProsperityChange;
E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\TaleWorlds.CampaignSystem.Settlements\Town.cs:638: 		base.Owner.Settlement.Militia += MilitiaChange;
		RepairWallsOfSettlementDaily();
	}

	private void RepairWallsOfSettlementDaily()
		foreach (Building building in Buildings)
		{
E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\TaleWorlds.CampaignSystem.Settlements\Town.cs:662: 			if (building.BuildingType == DefaultBuildingTypes.SettlementFortifications && IsTown)
			{
				result = building.CurrentLevel;
				break;
			}

 succeeded in 505ms:
public class Village : SettlementComponent
{
E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\TaleWorlds.CampaignSystem.Settlements\Village.cs:16: 	public enum VillageStates
	{
		Normal,
		BeingRaided,
		ForcedForVolunteers,
		ForcedForSupplies,
	public enum VillageStates
	{
E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\TaleWorlds.CampaignSystem.Settlements\Village.cs:18: 		Normal,
		BeingRaided,
		ForcedForVolunteers,
		ForcedForSupplies,
		Looted
	}

	[SaveableField(104)]
E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\TaleWorlds.CampaignSystem.Settlements\Village.cs:37: 	private VillageStates _villageState;

	[SaveableField(106)]
	private Settlement _bound;

	public VillageType VillageType;
	public static MBReadOnlyList<Village> All => Campaign.Current.AllVillages;

E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\TaleWorlds.CampaignSystem.Settlements\Village.cs:51: 	public VillageStates VillageState
	{
		get
		{
			return _villageState;
		}
		get
		{
E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\TaleWorlds.CampaignSystem.Settlements\Village.cs:55: 			return _villageState;
		}
		set
		{
			if (_villageState != value)
			{
		set
		{
E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\TaleWorlds.CampaignSystem.Settlements\Village.cs:59: 			if (_villageState != value)
			{
				_villageState = value;
				switch (_villageState)
				{
				case VillageStates.Normal:
			if (_villageState != value)
			{
E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\TaleWorlds.CampaignSystem.Settlements\Village.cs:61: 				_villageState = value;
				switch (_villageState)
				{
				case VillageStates.Normal:
					CampaignEventDispatcher.Instance.OnVillageBecomeNormal(this);
					break;
			{
				_villageState = value;
E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\TaleWorlds.CampaignSystem.Settlements\Village.cs:62: 				switch (_villageState)
				{
				case VillageStates.Normal:
					CampaignEventDispatcher.Instance.OnVillageBecomeNormal(this);
					break;
				case VillageStates.BeingRaided:
				switch (_villageState)
				{
E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\TaleWorlds.CampaignSystem.Settlements\Village.cs:64: 				case VillageStates.Normal:
					CampaignEventDispatcher.Instance.OnVillageBecomeNormal(this);
					break;
				case VillageStates.BeingRaided:
					CampaignEventDispatcher.Instance.OnVillageBeingRaided(this);
					break;
				{
				case VillageStates.Normal:
E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\TaleWorlds.CampaignSystem.Settlements\Village.cs:65: 					CampaignEventDispatcher.Instance.OnVillageBecomeNormal(this);
					break;
				case VillageStates.BeingRaided:
					CampaignEventDispatcher.Instance.OnVillageBeingRaided(this);
					break;
				case VillageStates.Looted:
					CampaignEventDispatcher.Instance.OnVillageBecomeNormal(this);
					break;
E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\TaleWorlds.CampaignSystem.Settlements\Village.cs:67: 				case VillageStates.BeingRaided:
					CampaignEventDispatcher.Instance.OnVillageBeingRaided(this);
					break;
				case VillageStates.Looted:
					CampaignEventDispatcher.Instance.OnVillageLooted(this);
					break;
					CampaignEventDispatcher.Instance.OnVillageBeingRaided(this);
					break;
E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\TaleWorlds.CampaignSystem.Settlements\Village.cs:70: 				case VillageStates.Looted:
					CampaignEventDispatcher.Instance.OnVillageLooted(this);
					break;
				case VillageStates.ForcedForVolunteers:
				case VillageStates.ForcedForSupplies:
					break;
					CampaignEventDispatcher.Instance.OnVillageLooted(this);
					break;
E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\TaleWorlds.CampaignSystem.Settlements\Village.cs:73: 				case VillageStates.ForcedForVolunteers:
				case VillageStates.ForcedForSupplies:
					break;
				}
			}
		}
					break;
				case VillageStates.ForcedForVolunteers:
E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\TaleWorlds.CampaignSystem.Settlements\Village.cs:74: 				case VillageStates.ForcedForSupplies:
					break;
				}
			}
		}
	}
	}

E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\TaleWorlds.CampaignSystem.Settlements\Village.cs:81: 	public bool IsDeserted => _villageState == VillageStates.Looted;

	[SaveableProperty(105)]
	public float LastDemandSatisfiedTime { get; private set; }

	public Settlement Bound
	}

E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\TaleWorlds.CampaignSystem.Settlements\Village.cs:171: 	internal static object AutoGeneratedGetMemberValue_villageState(object o)
	{
		return ((Village)o)._villageState;
	}

	internal static object AutoGeneratedGetMemberValue_bound(object o)
	internal static object AutoGeneratedGetMemberValue_villageState(object o)
	{
E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\TaleWorlds.CampaignSystem.Settlements\Village.cs:173: 		return ((Village)o)._villageState;
	}

	internal static object AutoGeneratedGetMemberValue_bound(object o)
	{
		return ((Village)o)._bound;
	public void DailyTick()
	{
E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\TaleWorlds.CampaignSystem.Settlements\Village.cs:225: 		int hearthLevel = GetHearthLevel();
		Hearth += HearthChange;
		if (hearthLevel != GetHearthLevel())
		{
			base.Settlement.Party.SetLevelMaskIsDirty();
		}
		int hearthLevel = GetHearthLevel();
		Hearth += HearthChange;
E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\TaleWorlds.CampaignSystem.Settlements\Village.cs:227: 		if (hearthLevel != GetHearthLevel())
		{
			base.Settlement.Party.SetLevelMaskIsDirty();
		}
		if (Hearth < 10f)
		{
	public override void OnInit()
	{
E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\TaleWorlds.CampaignSystem.Settlements\Village.cs:244: 		ChangeVillageStateAction.ApplyBySettingToNormal(base.Settlement);
		ChangeGold(1000);
	}

	public int GetWarehouseCapacity()
	{
	}

E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\TaleWorlds.CampaignSystem.Settlements\Village.cs:320: 	public int GetHearthLevel()
	{
		if (Hearth >= 600f)
		{
			return 2;
		}
	public override ProsperityLevel GetProsperityLevel()
	{
E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\TaleWorlds.CampaignSystem.Settlements\Village.cs:335: 		if (GetHearthLevel() >= 2)
		{
			return ProsperityLevel.High;
		}
		if (GetHearthLevel() >= 1)
		{
			return ProsperityLevel.High;
		}
E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\TaleWorlds.CampaignSystem.Settlements\Village.cs:339: 		if (GetHearthLevel() >= 1)
		{
			return ProsperityLevel.Mid;
		}
		return ProsperityLevel.Low;
	}

 succeeded in 453ms:
E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\TaleWorlds.CampaignSystem.Party\PartyBase.cs:375: 	public int NumberOfHealthyMembers => MemberRoster.TotalManCount - MemberRoster.TotalWounded;
E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\TaleWorlds.CampaignSystem.Party\PartyBase.cs:376: 
E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\TaleWorlds.CampaignSystem.Party\PartyBase.cs:377: 	public int NumberOfRegularMembers => MemberRoster.TotalRegulars;
E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\TaleWorlds.CampaignSystem.Party\PartyBase.cs:378: 
E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\TaleWorlds.CampaignSystem.Party\PartyBase.cs:379: 	public int NumberOfWoundedTotalMembers => MemberRoster.TotalWounded;
E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\TaleWorlds.CampaignSystem.Party\PartyBase.cs:380: 
E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\TaleWorlds.CampaignSystem.Party\PartyBase.cs:381: 	public int NumberOfAllMembers => MemberRoster.TotalManCount;
E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\TaleWorlds.CampaignSystem.Party\PartyBase.cs:382: 
E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\TaleWorlds.CampaignSystem.Party\PartyBase.cs:899: 	public PartyBase(MobileParty mobileParty)
E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\TaleWorlds.CampaignSystem.Party\PartyBase.cs:900: 		: this(mobileParty, null)
E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\TaleWorlds.CampaignSystem.Party\PartyBase.cs:901: 	{
E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\TaleWorlds.CampaignSystem.Party\PartyBase.cs:902: 	}
E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\TaleWorlds.CampaignSystem.Party\PartyBase.cs:903: 
E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\TaleWorlds.CampaignSystem.Party\PartyBase.cs:904: 	public PartyBase(Settlement settlement)
E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\TaleWorlds.CampaignSystem.Party\PartyBase.cs:905: 		: this(null, settlement)
E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\TaleWorlds.CampaignSystem.Party\PartyBase.cs:906: 	{
E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\TaleWorlds.CampaignSystem.Party\PartyBase.cs:907: 	}
E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\TaleWorlds.CampaignSystem.Party\PartyBase.cs:908: 
E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\TaleWorlds.CampaignSystem.Party\PartyBase.cs:909: 	private PartyBase(MobileParty mobileParty, Settlement settlement)
E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\TaleWorlds.CampaignSystem.Party\PartyBase.cs:910: 	{
E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\TaleWorlds.CampaignSystem.Party\PartyBase.cs:911: 		Index = Campaign.Current.GeneratePartyId(this);
E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\TaleWorlds.CampaignSystem.Party\PartyBase.cs:912: 		MobileParty = mobileParty;
E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\TaleWorlds.CampaignSystem.Party\PartyBase.cs:913: 		Settlement = settlement;
E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\TaleWorlds.CampaignSystem.Party\PartyBase.cs:914: 		ItemRoster = new ItemRoster();
E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\TaleWorlds.CampaignSystem.Party\PartyBase.cs:915: 		MemberRoster = new TroopRoster(this);
E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\TaleWorlds.CampaignSystem.Party\PartyBase.cs:916: 		PrisonRoster = new TroopRoster(this);

 succeeded in 515ms:
E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\TaleWorlds.CampaignSystem.Party\MobileParty.cs:345: 
E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\TaleWorlds.CampaignSystem.Party\MobileParty.cs:346: 	[CachedData]
E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\TaleWorlds.CampaignSystem.Party\MobileParty.cs:347: 	MobileParty ILocatable<MobileParty>.NextLocatable { get; set; }
E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\TaleWorlds.CampaignSystem.Party\MobileParty.cs:348: 
E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\TaleWorlds.CampaignSystem.Party\MobileParty.cs:349: 	[SaveableProperty(1019)]
E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\TaleWorlds.CampaignSystem.Party\MobileParty.cs:350: 	public MobilePartyAi Ai { get; private set; }
E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\TaleWorlds.CampaignSystem.Party\MobileParty.cs:351: 
E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\TaleWorlds.CampaignSystem.Party\MobileParty.cs:352: 	[SaveableProperty(1020)]
E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\TaleWorlds.CampaignSystem.Party\MobileParty.cs:353: 	public PartyBase Party { get; private set; }
E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\TaleWorlds.CampaignSystem.Party\MobileParty.cs:354: 
E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\TaleWorlds.CampaignSystem.Party\MobileParty.cs:355: 	[SaveableProperty(1023)]
E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\TaleWorlds.CampaignSystem.Party\MobileParty.cs:356: 	public bool IsActive { get; set; }
E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\TaleWorlds.CampaignSystem.Party\MobileParty.cs:357: 
E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\TaleWorlds.CampaignSystem.Party\MobileParty.cs:358: 	public bool IsInRaftState
E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\TaleWorlds.CampaignSystem.Party\MobileParty.cs:359: 	{
E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\TaleWorlds.CampaignSystem.Party\MobileParty.cs:360: 		get
E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\TaleWorlds.CampaignSystem.Party\MobileParty.cs:1848: 		}
E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\TaleWorlds.CampaignSystem.Party\MobileParty.cs:1849: 	}
E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\TaleWorlds.CampaignSystem.Party\MobileParty.cs:1850: 
E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\TaleWorlds.CampaignSystem.Party\MobileParty.cs:1851: 	public MobileParty()
E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\TaleWorlds.CampaignSystem.Party\MobileParty.cs:1852: 	{
E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\TaleWorlds.CampaignSystem.Party\MobileParty.cs:1853: 		_isVisible = false;
E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\TaleWorlds.CampaignSystem.Party\MobileParty.cs:1854: 		IsActive = true;
E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\TaleWorlds.CampaignSystem.Party\MobileParty.cs:1855: 		_isCurrentlyUsedByAQuest = false;
E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\TaleWorlds.CampaignSystem.Party\MobileParty.cs:1856: 		Party = new PartyBase(this);
E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\TaleWorlds.CampaignSystem.Party\MobileParty.cs:1857: 		Anchor = new AnchorPoint(this);
E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\TaleWorlds.CampaignSystem.Party\MobileParty.cs:1858: 		InitMembers();
E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\TaleWorlds.CampaignSystem.Party\MobileParty.cs:1859: 		InitCached();
E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\TaleWorlds.CampaignSystem.Party\MobileParty.cs:1860: 		Initialize();
E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\TaleWorlds.CampaignSystem.Party\MobileParty.cs:1861: 	}
E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\TaleWorlds.CampaignSystem.Party\MobileParty.cs:1862: 

exec
"C:\\Program Files\\PowerShell\\7\\pwsh.exe" -Command '$path='"'E:\\Decompiled_Bannerlord\\Campaign\\TaleWorlds.CampaignSystem\\TaleWorlds.CampaignSystem.Settlements\\Town.cs'; "'$i=0; Get-Content $path | ForEach-Object { $i++; if($i -ge 459 -and $i -le 472){ '"'{0}:{1}: {2}' -f "'$path,$i,$_ } }' in C:\Users\mikew\source\repos\TAOM
exec
"C:\\Program Files\\PowerShell\\7\\pwsh.exe" -Command "Select-String -Path 'Main/SubModule.cs','Main/IoC.cs','Main/Features/SettlementFood/SettlementFoodIoC.cs' -Pattern 'RegisterSettlementFoodFeature|TaomSettlementFoodModel|ISettlementFood|AddModel' | ForEach-Object { \"{0}:{1}: {2}\" -f "'$_.Path,$_.LineNumber,$_.Line.Trim() }' in C:\Users\mikew\source\repos\TAOM
exec
"C:\\Program Files\\PowerShell\\7\\pwsh.exe" -Command "Select-String -Path 'docs/features/settlement-food.md','docs/reference/engine/settlement-economy-food-prosperity.md' -Pattern 'garrison|NumberOfAllMembers|production|siege|VillageState|Normal|divisor|delta|base|FoodStocksUpperLimit|CastleFoodStockUpperLimitBonus|prosperity' | ForEach-Object { \"{0}:{1}: {2}\" -f "'$_.Path,$_.LineNumber,$_.Line.Trim() }' in C:\Users\mikew\source\repos\TAOM
exec
"C:\\Program Files\\PowerShell\\7\\pwsh.exe" -Command 'git status --short' in C:\Users\mikew\source\repos\TAOM
 succeeded in 314ms:
A  .claude/hooks/check-native-dll-crt.sh
 M .claude/settings.json
 M .github/workflows/build.yml
 M CHANGELOG.md
 M CLAUDE.md
 M Dependencies/NativeSkinFixes.NativeHooks/Build.ps1
 M Dependencies/NativeSkinFixes.NativeHooks/NativeSkinFixes.NativeHooks.vcxproj
 M Main/Features/BattleLoadDiagnostics/BattleLoadStallWatchdog.cs
 M Main/Features/NativeSkinFixes/Interop/NativeHookLoader.cs
 M Main/Features/ShaderPrecompilation/ShaderPrecompilationIoC.cs
 M Main/Features/ShaderPrecompilation/ShaderPrecompileRunner.cs
 M Main/Features/TaomSettings.cs
 M Main/IoC.cs
 M Main/SubModule.cs
 M Main/_Module/ModuleData/taom_module_strings.xml
 M Main/_Module/bin/Win64_Shipping_Client/TAOM.NativeSkinFixes.dll
 M docs/INDEX.md
 M docs/features/native-skin-fixes.md
?? Main/Features/SettlementFood/
?? Main/Features/ShaderPrecompilation/IShaderPrecompileCrashGuard.cs
?? Main/Features/ShaderPrecompilation/ShaderPrecompileCrashGuard.cs
?? Main/_Module/ModuleData/settlement_food/
?? TAOM.Tests/Features/SettlementFood/
?? TAOM.Tests/Features/ShaderPrecompilation/ShaderPrecompileCrashGuardTests.cs
?? docs/features/settlement-food.md
?? docs/investigations/
?? docs/reference/engine/settlement-economy-food-prosperity.md
?? docs/reviews/codex-adversarial-settlement-food-2026-06-18.md
?? docs/reviews/codex-adversarial-settlement-food-2026-06-18.prompt.md
?? docs/reviews/codex-prereview-shader-crash-guard-2026-06-18.md
?? docs/reviews/codex-prereview-shader-crash-guard-2026-06-18.prompt.md
warning: could not open directory '.codex_tmp/tmp1fft9w7e/': Permission denied
warning: could not open directory '.codex_tmp/tmp1fyfj2_j/': Permission denied
warning: could not open directory '.codex_tmp/tmp29jamgyn/': Permission denied
warning: could not open directory '.codex_tmp/tmp2owwhlqy/': Permission denied
warning: could not open directory '.codex_tmp/tmp5516wbdv/': Permission denied
warning: could not open directory '.codex_tmp/tmp78qze76y/': Permission denied
warning: could not open directory '.codex_tmp/tmp8gds9xfp/': Permission denied
warning: could not open directory '.codex_tmp/tmpbwbm4o8d/': Permission denied
warning: could not open directory '.codex_tmp/tmpc85yzrbw/': Permission denied
warning: could not open directory '.codex_tmp/tmpi3maldm8/': Permission denied
warning: could not open directory '.codex_tmp/tmpkr2gpims/': Permission denied
warning: could not open directory '.codex_tmp/tmpktgkfgay/': Permission denied
warning: could not open directory '.codex_tmp/tmplcnrd81l/': Permission denied
warning: could not open directory '.codex_tmp/tmplorpih3w/': Permission denied
warning: could not open directory '.codex_tmp/tmpm39f2s15/': Permission denied
warning: could not open directory '.codex_tmp/tmpov4yy7nx/': Permission denied
warning: could not open directory '.codex_tmp/tmpsanxk9fp/': Permission denied
warning: could not open directory '.codex_tmp/tmpwy0s04pv/': Permission denied
warning: could not open directory '.codex_tmp/tmpyqy6lg5o/': Permission denied
warning: could not open directory '.codex_tmp2/tmpulsj5ei5/': Permission denied
warning: could not open directory '.pytest_cache/': Permission denied

 succeeded in 468ms:
E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\TaleWorlds.CampaignSystem.Settlements\Town.cs:459: 
E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\TaleWorlds.CampaignSystem.Settlements\Town.cs:460: 	public int FoodStocksUpperLimit()
E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\TaleWorlds.CampaignSystem.Settlements\Town.cs:461: 	{
E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\TaleWorlds.CampaignSystem.Settlements\Town.cs:462: 		ExplainedNumber result = new ExplainedNumber(Campaign.Current.Models.SettlementFoodModel.FoodStocksUpperLimit);
E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\TaleWorlds.CampaignSystem.Settlements\Town.cs:463: 		if (IsCastle)
E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\TaleWorlds.CampaignSystem.Settlements\Town.cs:464: 		{
E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\TaleWorlds.CampaignSystem.Settlements\Town.cs:465: 			result.Add(Campaign.Current.Models.SettlementFoodModel.CastleFoodStockUpperLimitBonus);
E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\TaleWorlds.CampaignSystem.Settlements\Town.cs:466: 		}
E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\TaleWorlds.CampaignSystem.Settlements\Town.cs:467: 		AddEffectOfBuildings(BuildingEffectEnum.FoodStock, ref result);
E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\TaleWorlds.CampaignSystem.Settlements\Town.cs:468: 		return (int)result.ResultNumber;
E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\TaleWorlds.CampaignSystem.Settlements\Town.cs:469: 	}
E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\TaleWorlds.CampaignSystem.Settlements\Town.cs:470: 
E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\TaleWorlds.CampaignSystem.Settlements\Town.cs:471: 	public Town()
E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\TaleWorlds.CampaignSystem.Settlements\Town.cs:472: 	{

 succeeded in 451ms:
C:\Users\mikew\source\repos\TAOM\Main\SubModule.cs:362: campaignStarter.AddModel(new TaomCharacterStatsModel());
C:\Users\mikew\source\repos\TAOM\Main\SubModule.cs:363: campaignStarter.AddModel(new TaomPartyWageModel(costService, careerPassives, wageModifiers));
C:\Users\mikew\source\repos\TAOM\Main\SubModule.cs:364: campaignStarter.AddModel(new TaomVolunteerModel(volunteerService, recruitmentService, volunteerContextAdapter, culturalFeats, recruitmentAlignment));
C:\Users\mikew\source\repos\TAOM\Main\SubModule.cs:370: campaignStarter.AddModel(new TaomAgeModel(raceAgeService));
C:\Users\mikew\source\repos\TAOM\Main\SubModule.cs:371: campaignStarter.AddModel(new TaomPregnancyModel(raceAgeService));
C:\Users\mikew\source\repos\TAOM\Main\SubModule.cs:372: campaignStarter.AddModel(new TaomHeroCreationModel());
C:\Users\mikew\source\repos\TAOM\Main\SubModule.cs:379: campaignStarter.AddModel(new TaomAllianceModel(diplomacyService));
C:\Users\mikew\source\repos\TAOM\Main\SubModule.cs:380: campaignStarter.AddModel(new TaomKingdomDecisionPermissionModel(diplomacyService, wotrService));
C:\Users\mikew\source\repos\TAOM\Main\SubModule.cs:381: campaignStarter.AddModel(new TaomDiplomacyModel(wotrService));
C:\Users\mikew\source\repos\TAOM\Main\SubModule.cs:389: campaignStarter.AddModel(new TaomSiegeEventModel(IoC.Resolve<ISiegeEngineAvailabilityService>()));
C:\Users\mikew\source\repos\TAOM\Main\SubModule.cs:393: campaignStarter.AddModel(new TaomExecutionRelationModel(executionRelationService, playerContext));
C:\Users\mikew\source\repos\TAOM\Main\SubModule.cs:399: campaignStarter.AddModel(new TaomArmyManagementModel(culturalFeats));
C:\Users\mikew\source\repos\TAOM\Main\SubModule.cs:400: campaignStarter.AddModel(new TaomPartySpeedModel(culturalFeats, careerPassives));
C:\Users\mikew\source\repos\TAOM\Main\SubModule.cs:401: campaignStarter.AddModel(new TaomSettlementProsperityModel(culturalFeats));
C:\Users\mikew\source\repos\TAOM\Main\SubModule.cs:402: campaignStarter.AddModel(new TaomSettlementMilitiaModel(culturalFeats));
C:\Users\mikew\source\repos\TAOM\Main\SubModule.cs:403: campaignStarter.AddModel(new TaomBuildingConstructionModel(culturalFeats));
C:\Users\mikew\source\repos\TAOM\Main\SubModule.cs:404: campaignStarter.AddModel(new TaomVillageProductionModel(culturalFeats));
C:\Users\mikew\source\repos\TAOM\Main\SubModule.cs:405: campaignStarter.AddModel(new TaomCaravanModel(culturalFeats));
C:\Users\mikew\source\repos\TAOM\Main\SubModule.cs:406: campaignStarter.AddModel(new TaomBattleRewardModel(culturalFeats, careerPassives));
C:\Users\mikew\source\repos\TAOM\Main\SubModule.cs:407: campaignStarter.AddModel(new TaomTournamentModel(IoC.Resolve<TAOM.Features.Arena.ITournamentService>()));
C:\Users\mikew\source\repos\TAOM\Main\SubModule.cs:408: campaignStarter.AddModel(new TaomPartyTroopUpgradeModel(culturalFeats, careerPassives));
C:\Users\mikew\source\repos\TAOM\Main\SubModule.cs:409: campaignStarter.AddModel(new TaomPartySizeModel(culturalFeats, careerPassives));
C:\Users\mikew\source\repos\TAOM\Main\SubModule.cs:410: campaignStarter.AddModel(new TaomFoodConsumptionModel(culturalFeats));
C:\Users\mikew\source\repos\TAOM\Main\SubModule.cs:411: campaignStarter.AddModel(new TaomSettlementLoyaltyModel(culturalFeats, IoC.Resolve<IRevoltTuningConfigProvider>()));
C:\Users\mikew\source\repos\TAOM\Main\SubModule.cs:412: campaignStarter.AddModel(new TaomSettlementFoodModel(IoC.Resolve<ISettlementFoodService>(), IoC.Resolve<ISettlementFoodConfigProvider>()));
C:\Users\mikew\source\repos\TAOM\Main\SubModule.cs:413: campaignStarter.AddModel(new TaomBanditDensityModel(IoC.Resolve<IBanditScalingService>()));
C:\Users\mikew\source\repos\TAOM\Main\SubModule.cs:414: campaignStarter.AddModel(new TaomPartyMoraleModel(culturalFeats, careerPassives));
C:\Users\mikew\source\repos\TAOM\Main\SubModule.cs:415: campaignStarter.AddModel(new TaomSmithingModel(culturalFeats, careerPassives));
C:\Users\mikew\source\repos\TAOM\Main\SubModule.cs:416: campaignStarter.AddModel(new TaomClanFinanceModel(culturalFeats));
C:\Users\mikew\source\repos\TAOM\Main\SubModule.cs:417: campaignStarter.AddModel(new TaomRaidModel(culturalFeats, careerPassives));
C:\Users\mikew\source\repos\TAOM\Main\SubModule.cs:418: campaignStarter.AddModel(new TaomNotableSpawnModel(culturalFeats));
C:\Users\mikew\source\repos\TAOM\Main\SubModule.cs:423: campaignStarter.AddModel(new TaomMilitaryPowerModel(battleBalanceSettings, battleBalanceConfig));
C:\Users\mikew\source\repos\TAOM\Main\SubModule.cs:424: campaignStarter.AddModel(new TaomCombatSimulationModel(battleBalanceSettings));
C:\Users\mikew\source\repos\TAOM\Main\SubModule.cs:425: campaignStarter.AddModel(new TaomPartyHealingModel(battleBalanceSettings, battleBalanceConfig));
C:\Users\mikew\source\repos\TAOM\Main\SubModule.cs:427: campaignStarter.AddModel(new TaomInformationRestrictionModel(IoC.Resolve<IEncyclopediaSettingsProvider>()));
C:\Users\mikew\source\repos\TAOM\Main\SubModule.cs:430: campaignStarter.AddModel(new TaomTargetScoreModel(armyTargetingService));
C:\Users\mikew\source\repos\TAOM\Main\SubModule.cs:468: campaignStarter.AddModel(new TaomMapVisibilityModel(careerPassives));
C:\Users\mikew\source\repos\TAOM\Main\SubModule.cs:469: campaignStarter.AddModel(new TaomInventoryCapacityModel(careerPassives));
C:\Users\mikew\source\repos\TAOM\Main\SubModule.cs:472: campaignStarter.AddModel<AgentStatCalculateModel>(new TaomAgentStatCalculateModel(careerPassiveService, careerAgentStat, elephantAttackService, spiderAttackService));
C:\Users\mikew\source\repos\TAOM\Main\SubModule.cs:473: campaignStarter.AddModel<AgentApplyDamageModel>(new TaomAgentApplyDamageModel(careerAgentStat));
C:\Users\mikew\source\repos\TAOM\Main\SubModule.cs:474: campaignStarter.AddModel(new TaomClanTierModel(careerPassiveService));
C:\Users\mikew\source\repos\TAOM\Main\IoC.cs:102: SettlementFoodIoC.RegisterSettlementFoodFeature(container);
C:\Users\mikew\source\repos\TAOM\Main\Features\SettlementFood\SettlementFoodIoC.cs:7: public static void RegisterSettlementFoodFeature(IContainer container)
C:\Users\mikew\source\repos\TAOM\Main\Features\SettlementFood\SettlementFoodIoC.cs:9: container.Register<ISettlementFoodConfigProvider, SettlementFoodConfigProvider>(Reuse.Singleton);
C:\Users\mikew\source\repos\TAOM\Main\Features\SettlementFood\SettlementFoodIoC.cs:10: container.Register<ISettlementFoodService, SettlementFoodService>(Reuse.Singleton);

 succeeded in 457ms:
C:\Users\mikew\source\repos\TAOM\docs\features\settlement-food.md:5: Overrides `DefaultSettlementFoodModel` to (1) fix a Troop-Weight side effect that inflated garrison
C:\Users\mikew\source\repos\TAOM\docs\features\settlement-food.md:6: food consumption for elite garrisons, and (2) expose vanilla's hardcoded food constants as MCM/JSON
C:\Users\mikew\source\repos\TAOM\docs\features\settlement-food.md:7: knobs so the high-prosperity food squeeze can be tuned. Defaults are vanilla, so out of the box the
C:\Users\mikew\source\repos\TAOM\docs\features\settlement-food.md:8: only behavioral change is the garrison correction.
C:\Users\mikew\source\repos\TAOM\docs\features\settlement-food.md:12: Towns/castles ran chronic food deficits — garrisons and civilians outpacing production. Root causes
C:\Users\mikew\source\repos\TAOM\docs\features\settlement-food.md:13: (full mechanics + decompile cites: [docs/reference/engine/settlement-economy-food-prosperity.md](../reference/engine/settlement-economy-food-prosperity.md)):
C:\Users\mikew\source\repos\TAOM\docs\features\settlement-food.md:16: `PartyBase.NumberOfAllMembers` getter and bumps it to the *weighted* count. `DefaultSettlementFoodModel`
C:\Users\mikew\source\repos\TAOM\docs\features\settlement-food.md:17: reads exactly that getter for the garrison food term (`NumberOfAllMembers / 20`), so an elite
C:\Users\mikew\source\repos\TAOM\docs\features\settlement-food.md:18: garrison (troop weights 2.0–3.0) consumed 2–3× the intended food. The Troop Weight feature was
C:\Users\mikew\source\repos\TAOM\docs\features\settlement-food.md:19: designed for field-party size budgeting; weighting garrisons for food was never intended.
C:\Users\mikew\source\repos\TAOM\docs\features\settlement-food.md:20: 2. **Vanilla high-prosperity squeeze (not a bug, but the dominant term).** `Prosperity / 40` is the
C:\Users\mikew\source\repos\TAOM\docs\features\settlement-food.md:21: largest consumer while production caps low (base 15 + ≤18/village). Vanilla self-limits prosperous
C:\Users\mikew\source\repos\TAOM\docs\features\settlement-food.md:22: towns into deficit; TAOM amplifies it with large elite garrisons, frequent raids (looted villages
C:\Users\mikew\source\repos\TAOM\docs\features\settlement-food.md:25: Not contributors (ruled out): cultural food-consumption feats (mobile-party only — garrisons are
C:\Users\mikew\source\repos\TAOM\docs\features\settlement-food.md:27: (cosmetic battle agents, not in the garrison roster).
C:\Users\mikew\source\repos\TAOM\docs\features\settlement-food.md:35: (`NumberOfMenOnGarrisonToEatOneFood`, `NumberOfProsperityToEatOneFood`, `FoodStocksUpperLimit`,
C:\Users\mikew\source\repos\TAOM\docs\features\settlement-food.md:36: `CastleFoodStockUpperLimitBonus`) to return config values (or vanilla when the master toggle is off),
C:\Users\mikew\source\repos\TAOM\docs\features\settlement-food.md:37: and overrides `CalculateTownFoodStocksChange` to call `base(...)` then add the service delta.
C:\Users\mikew\source\repos\TAOM\docs\features\settlement-food.md:38: - **`SettlementFoodService.ComputeFoodDelta`** — pure (no TaleWorlds types): garrison raw-count
C:\Users\mikew\source\repos\TAOM\docs\features\settlement-food.md:39: correction `(weighted − raw)/divisor` (always) + siege-gated production knobs (base-food delta,
C:\Users\mikew\source\repos\TAOM\docs\features\settlement-food.md:42: (raw `MemberRoster.TotalManCount` vs patched `NumberOfAllMembers`, per-Normal-village hearth levels).
C:\Users\mikew\source\repos\TAOM\docs\features\settlement-food.md:45: ### Garrison correction math
C:\Users\mikew\source\repos\TAOM\docs\features\settlement-food.md:47: Vanilla `PartyBase.NumberOfAllMembers == MemberRoster.TotalManCount` (`PartyBase.cs:381`); Patch17 only
C:\Users\mikew\source\repos\TAOM\docs\features\settlement-food.md:48: ever raises it. So `weighted − raw` is exactly the inflation, and adding back `(weighted−raw)/divisor`
C:\Users\mikew\source\repos\TAOM\docs\features\settlement-food.md:49: makes the garrison term use the raw body count. The global getter stays weighted, so AI strength reads
C:\Users\mikew\source\repos\TAOM\docs\features\settlement-food.md:50: and `DefaultSettlementGarrisonModel` capacity are unchanged (food-model-only fix). No-op when Troop
C:\Users\mikew\source\repos\TAOM\docs\features\settlement-food.md:59: | `garrisonFoodDivisor` | 20 | ↑ = garrisons cheaper to feed | 25–30 |
C:\Users\mikew\source\repos\TAOM\docs\features\settlement-food.md:60: | `prosperityFoodDivisor` | 40 | ↑ = relieves the dominant civilian term | 55–60 |
C:\Users\mikew\source\repos\TAOM\docs\features\settlement-food.md:61: | `townBaseFood` / `castleBaseFood` | 15 / 10 | flat production floor | +5–10 |
C:\Users\mikew\source\repos\TAOM\docs\features\settlement-food.md:63: | `flatFoodBonus` | 0 | flat daily production add | 0–10 |
C:\Users\mikew\source\repos\TAOM\docs\features\settlement-food.md:64: | `foodStocksUpperLimit` / `castleFoodStockUpperLimitBonus` | 300 / 150 | storage caps | as desired |
C:\Users\mikew\source\repos\TAOM\docs\features\settlement-food.md:66: Validation: divisors must be ≥ 1 (a 0 would poison the formula with Infinity); floats must be finite
C:\Users\mikew\source\repos\TAOM\docs\features\settlement-food.md:70: (garrison food reverts to the weighted count). The JSON is loaded once (`Reuse.Singleton`), so **edits
C:\Users\mikew\source\repos\TAOM\docs\features\settlement-food.md:78: | `Main/Features/SettlementFood/SettlementFoodService.cs` | Pure food-delta math |
C:\Users\mikew\source\repos\TAOM\docs\features\settlement-food.md:94: - Reads, but does not modify, the Troop Weight feature's effect on `NumberOfAllMembers`.
C:\Users\mikew\source\repos\TAOM\docs\features\settlement-food.md:100: - `SettlementFoodServiceTests` (13) — garrison correction (inflated/not-inflated/raised-divisor/under
C:\Users\mikew\source\repos\TAOM\docs\features\settlement-food.md:101: siege), production knobs (town/castle base, village multiplier, flat, siege suppression), combined,
C:\Users\mikew\source\repos\TAOM\docs\features\settlement-food.md:104: one test per validation rule (zero/negative divisor, negative/NaN floats, zero cap → revert + warn).
C:\Users\mikew\source\repos\TAOM\docs\features\settlement-food.md:110: **Relieve starvation:** edit `settlement_food_config.json` — raise `prosperityFoodDivisor` (biggest
C:\Users\mikew\source\repos\TAOM\docs\features\settlement-food.md:111: lever) and/or `garrisonFoodDivisor`, optionally bump `villageFoodMultiplier`/`townBaseFood`. Restart
C:\Users\mikew\source\repos\TAOM\docs\features\settlement-food.md:115: engine math, including the weighted garrison food).
C:\Users\mikew\source\repos\TAOM\docs\reference\engine\settlement-economy-food-prosperity.md:1: # Settlement economy: food, prosperity, hearth, caravans
C:\Users\mikew\source\repos\TAOM\docs\reference\engine\settlement-economy-food-prosperity.md:3: How a town/castle's food balance, prosperity, and village hearth actually compute in Bannerlord
C:\Users\mikew\source\repos\TAOM\docs\reference\engine\settlement-economy-food-prosperity.md:11: - A fief's daily food = **production − consumption** on the `Town.FoodStocks` pool (cap 300 town / 450 castle).
C:\Users\mikew\source\repos\TAOM\docs\reference\engine\settlement-economy-food-prosperity.md:12: - **Consumption** is dominated by `Prosperity / 40`; the garrison adds `NumberOfAllMembers / 20`.
C:\Users\mikew\source\repos\TAOM\docs\reference\engine\settlement-economy-food-prosperity.md:13: - **Production** is small: base +15 town / +10 castle, plus only `(hearthLevel+1) × 6` per village
C:\Users\mikew\source\repos\TAOM\docs\reference\engine\settlement-economy-food-prosperity.md:15: - High prosperity is *designed* to outrun production and push food to a deficit — that's vanilla's
C:\Users\mikew\source\repos\TAOM\docs\reference\engine\settlement-economy-food-prosperity.md:16: negative feedback that caps town growth. Starvation then bleeds prosperity (`foodChange × 0.5`).
C:\Users\mikew\source\repos\TAOM\docs\reference\engine\settlement-economy-food-prosperity.md:17: - **Garrison troops never starve to death** — they eat from the town pool, not the mobile-party path.
C:\Users\mikew\source\repos\TAOM\docs\reference\engine\settlement-economy-food-prosperity.md:19: - **TAOM-specific:** the Troop Weight feature inflates the garrison's `NumberOfAllMembers`, so elite
C:\Users\mikew\source\repos\TAOM\docs\reference\engine\settlement-economy-food-prosperity.md:20: garrisons ate 2–3× the food vanilla intends. `TaomSettlementFoodModel` corrects this and exposes
C:\Users\mikew\source\repos\TAOM\docs\reference\engine\settlement-economy-food-prosperity.md:26: lines 43–97). Net daily change = production − consumption:
C:\Users\mikew\source\repos\TAOM\docs\reference\engine\settlement-economy-food-prosperity.md:32: | Civilians (prosperity) | `town.Prosperity / 40` | `NumberOfProsperityToEatOneFood = 40` (line 32, used 47) |
C:\Users\mikew\source\repos\TAOM\docs\reference\engine\settlement-economy-food-prosperity.md:33: | Garrison | `town.GarrisonParty.Party.NumberOfAllMembers / 20` | `NumberOfMenOnGarrisonToEatOneFood = 20` (line 34, used 48) |
C:\Users\mikew\source\repos\TAOM\docs\reference\engine\settlement-economy-food-prosperity.md:35: | Siege perks | Gourmet / TriageTent reduce consumption, **only while besieged** | lines 49–53 |
C:\Users\mikew\source\repos\TAOM\docs\reference\engine\settlement-economy-food-prosperity.md:37: `Prosperity / 40` is almost always the largest consumer: a 3000-prosperity town eats **75 food/day**
C:\Users\mikew\source\repos\TAOM\docs\reference\engine\settlement-economy-food-prosperity.md:38: from civilians alone, vs ~25 for a 500-man garrison.
C:\Users\mikew\source\repos\TAOM\docs\reference\engine\settlement-economy-food-prosperity.md:40: ### Production (added — only when NOT under siege, lines 63–77)
C:\Users\mikew\source\repos\TAOM\docs\reference\engine\settlement-economy-food-prosperity.md:45: | Per bound village (Normal state) | `(village.GetHearthLevel() + 1) × 6` (line 72) |
C:\Users\mikew\source\repos\TAOM\docs\reference\engine\settlement-economy-food-prosperity.md:46: | Buildings | `FoodProduction` building effect (line 76) |
C:\Users\mikew\source\repos\TAOM\docs\reference\engine\settlement-economy-food-prosperity.md:54: **Under siege, ALL production is dropped** (the `else` branch, lines 78–81) — only consumption applies.
C:\Users\mikew\source\repos\TAOM\docs\reference\engine\settlement-economy-food-prosperity.md:55: This is the intended siege-starvation pressure.
C:\Users\mikew\source\repos\TAOM\docs\reference\engine\settlement-economy-food-prosperity.md:59: - `FoodStocksUpperLimit` = **300** (town); castles add `CastleFoodStockUpperLimitBonus` = **150** → 450;
C:\Users\mikew\source\repos\TAOM\docs\reference\engine\settlement-economy-food-prosperity.md:64: ## Prosperity — `DefaultSettlementProsperityModel`
C:\Users\mikew\source\repos\TAOM\docs\reference\engine\settlement-economy-food-prosperity.md:66: `CalculateProsperityChangeInternal` (lines 72–200). The load-bearing terms:
C:\Users\mikew\source\repos\TAOM\docs\reference\engine\settlement-economy-food-prosperity.md:70: | **Starvation penalty** | if `IsStarving`: `prosperity += foodChange × 0.5` (foodChange < 0 → loss) | 74–79 |
C:\Users\mikew\source\repos\TAOM\docs\reference\engine\settlement-economy-food-prosperity.md:71: | **Housing costs** | +6/+5/+4/+3/+2/+1 per day as prosperity climbs through 250/500/750/1000/1250/1500; negative above 6000…21000 | 81–131 |
C:\Users\mikew\source\repos\TAOM\docs\reference\engine\settlement-economy-food-prosperity.md:72: | **Surplus food** | if `FoodStocks + foodChange` overflows the cap: `prosperity += overflow × 0.1` | 132–137 |
C:\Users\mikew\source\repos\TAOM\docs\reference\engine\settlement-economy-food-prosperity.md:73: | **Market goods** | `BonusToProsperity` sold items × 0.1 | 138–145 |
C:\Users\mikew\source\repos\TAOM\docs\reference\engine\settlement-economy-food-prosperity.md:79: Prosperity is both a **food consumer** (`Prosperity/40`) and the thing food shortage attacks:
C:\Users\mikew\source\repos\TAOM\docs\reference\engine\settlement-economy-food-prosperity.md:82: high prosperity ─▶ Prosperity/40 consumption ─▶ food deficit ─▶ FoodStocks hits 0 (IsStarving)
C:\Users\mikew\source\repos\TAOM\docs\reference\engine\settlement-economy-food-prosperity.md:84: └──────────── prosperity recovers ◀── (foodChange × 0.5 penalty) ◀───┘   [drains prosperity]
C:\Users\mikew\source\repos\TAOM\docs\reference\engine\settlement-economy-food-prosperity.md:87: A starving town loses prosperity at half its daily food deficit, which *eventually* lowers the
C:\Users\mikew\source\repos\TAOM\docs\reference\engine\settlement-economy-food-prosperity.md:88: `Prosperity/40` consumption until it re-balances — that's the vanilla self-limiter. The pain the
C:\Users\mikew\source\repos\TAOM\docs\reference\engine\settlement-economy-food-prosperity.md:90: by large elite garrisons (next section), frequent raids zeroing village food, and the hearth-growth
C:\Users\mikew\source\repos\TAOM\docs\reference\engine\settlement-economy-food-prosperity.md:96: `−1`; `GrazingRights` policy `−0.25`. Hearth feeds village **food production** (above) and militia. It
C:\Users\mikew\source\repos\TAOM\docs\reference\engine\settlement-economy-food-prosperity.md:99: ## Garrison food is NOT the mobile-party path
C:\Users\mikew\source\repos\TAOM\docs\reference\engine\settlement-economy-food-prosperity.md:103: which **excludes garrisons, militia, caravans, villagers, bandits**. Consequences:
C:\Users\mikew\source\repos\TAOM\docs\reference\engine\settlement-economy-food-prosperity.md:105: - **Garrison troops do not die from starvation.** They consume from the town `FoodStocks` pool via the
C:\Users\mikew\source\repos\TAOM\docs\reference\engine\settlement-economy-food-prosperity.md:106: food model above; a starving fief damages *prosperity* (and indirectly recruitment/militia), not the
C:\Users\mikew\source\repos\TAOM\docs\reference\engine\settlement-economy-food-prosperity.md:107: garrison roster directly.
C:\Users\mikew\source\repos\TAOM\docs\reference\engine\settlement-economy-food-prosperity.md:108: - **The cultural food-consumption feats do not touch garrisons.** `TaomFoodConsumptionModel` extends
C:\Users\mikew\source\repos\TAOM\docs\reference\engine\settlement-economy-food-prosperity.md:110: **mobile field parties**. A "ravenous orc garrison eating its town dry" is not a real mechanic.
C:\Users\mikew\source\repos\TAOM\docs\reference\engine\settlement-economy-food-prosperity.md:115: tweaks Umbar's forming cost). They **do not deliver food to a garrison or town**. Food enters a town
C:\Users\mikew\source\repos\TAOM\docs\reference\engine\settlement-economy-food-prosperity.md:123: `PartyBase.NumberOfAllMembers => MemberRoster.TotalManCount` in vanilla (`PartyBase.cs:381`). The Troop
C:\Users\mikew\source\repos\TAOM\docs\reference\engine\settlement-economy-food-prosperity.md:125: member count (`PartyBase_NumberOfAllMembers_Patch.cs` → `TroopWeightService.CalculateWeightedMemberCount`,
C:\Users\mikew\source\repos\TAOM\docs\reference\engine\settlement-economy-food-prosperity.md:126: which has **no garrison guard**). The food model reads exactly that getter for the garrison term
C:\Users\mikew\source\repos\TAOM\docs\reference\engine\settlement-economy-food-prosperity.md:127: (`/20`), so an elite garrison (troop weights up to 2.0–3.0) consumed **2–3× the food vanilla intends**.
C:\Users\mikew\source\repos\TAOM\docs\reference\engine\settlement-economy-food-prosperity.md:135: 1. **Garrison raw-count correction (always on when the feature is enabled, siege or not):** since
C:\Users\mikew\source\repos\TAOM\docs\reference\engine\settlement-economy-food-prosperity.md:136: vanilla `NumberOfAllMembers == TotalManCount`, the inflation equals `weighted − raw`. The model adds
C:\Users\mikew\source\repos\TAOM\docs\reference\engine\settlement-economy-food-prosperity.md:137: back `(weighted − raw) / garrisonDivisor` so the garrison term uses the **raw body count**. This is
C:\Users\mikew\source\repos\TAOM\docs\reference\engine\settlement-economy-food-prosperity.md:139: reads and garrison-capacity (`DefaultSettlementGarrisonModel`) are unchanged.
C:\Users\mikew\source\repos\TAOM\docs\reference\engine\settlement-economy-food-prosperity.md:141: (`settlement_food/settlement_food_config.json`), so the high-prosperity squeeze can be dialed out:
C:\Users\mikew\source\repos\TAOM\docs\reference\engine\settlement-economy-food-prosperity.md:145: | `garrisonFoodDivisor` | 20 | ↑ = garrisons cheaper to feed |
C:\Users\mikew\source\repos\TAOM\docs\reference\engine\settlement-economy-food-prosperity.md:146: | `prosperityFoodDivisor` | 40 | ↑ = relieves the dominant civilian-consumption term |
C:\Users\mikew\source\repos\TAOM\docs\reference\engine\settlement-economy-food-prosperity.md:147: | `townBaseFood` / `castleBaseFood` | 15 / 10 | flat production floor |
C:\Users\mikew\source\repos\TAOM\docs\reference\engine\settlement-economy-food-prosperity.md:149: | `flatFoodBonus` | 0 | flat daily production add |
C:\Users\mikew\source\repos\TAOM\docs\reference\engine\settlement-economy-food-prosperity.md:150: | `foodStocksUpperLimit` / `castleFoodStockUpperLimitBonus` | 300 / 150 | storage caps |
C:\Users\mikew\source\repos\TAOM\docs\reference\engine\settlement-economy-food-prosperity.md:152: Production knobs (base/village/flat) are **siege-gated** — they never apply under siege, preserving
C:\Users\mikew\source\repos\TAOM\docs\reference\engine\settlement-economy-food-prosperity.md:153: the siege-starvation mechanic. Divisor and storage-cap knobs flow through the model's overridden
C:\Users\mikew\source\repos\TAOM\docs\reference\engine\settlement-economy-food-prosperity.md:154: virtual constants (so vanilla's own formula uses them); the garrison correction + production knobs
C:\Users\mikew\source\repos\TAOM\docs\reference\engine\settlement-economy-food-prosperity.md:155: are added on top of `base.CalculateTownFoodStocksChange`.
C:\Users\mikew\source\repos\TAOM\docs\reference\engine\settlement-economy-food-prosperity.md:157: The pure math lives in `SettlementFoodService.ComputeFoodDelta` (100% unit-tested); the JSON is
C:\Users\mikew\source\repos\TAOM\docs\reference\engine\settlement-economy-food-prosperity.md:158: validated by `SettlementFoodConfigProvider` (divisors must be ≥ 1; floats finite ≥ 0; invalid → revert
C:\Users\mikew\source\repos\TAOM\docs\reference\engine\settlement-economy-food-prosperity.md:160: Tuning** (on by default; off = vanilla engine math, garrison food stays weighted). JSON is loaded once
C:\Users\mikew\source\repos\TAOM\docs\reference\engine\settlement-economy-food-prosperity.md:163: ### Worked example (high-prosperity Gondor city)
C:\Users\mikew\source\repos\TAOM\docs\reference\engine\settlement-economy-food-prosperity.md:165: Prosperity 3000, a 500-man elite garrison (avg troop weight ~1.5 → reads 750), 3 villages at hearth
C:\Users\mikew\source\repos\TAOM\docs\reference\engine\settlement-economy-food-prosperity.md:168: | | Vanilla | TAOM (defaults, garrison fix only) |
C:\Users\mikew\source\repos\TAOM\docs\reference\engine\settlement-economy-food-prosperity.md:170: | Production | 15 + 3×12 = **+51** | +51 |
C:\Users\mikew\source\repos\TAOM\docs\reference\engine\settlement-economy-food-prosperity.md:172: | Garrison consumption | 750/20 = **−37.5** (weighted) | 500/20 = **−25** (raw) |
C:\Users\mikew\source\repos\TAOM\docs\reference\engine\settlement-economy-food-prosperity.md:175: The fix recovers ~12.5 food/day here; raising `prosperityFoodDivisor` to 60 would cut the civilian
C:\Users\mikew\source\repos\TAOM\docs\reference\engine\settlement-economy-food-prosperity.md:176: term to −50 and bring the example net positive. The prosperity term is the bigger absolute lever — the
C:\Users\mikew\source\repos\TAOM\docs\reference\engine\settlement-economy-food-prosperity.md:182: that drives `FoodStocks`/prosperity updates.
C:\Users\mikew\source\repos\TAOM\docs\reference\engine\settlement-economy-food-prosperity.md:186: `NumberOfAllMembers` getter that this model corrects for the garrison food term.

codex
The vanilla storage cap path also uses the same virtual `SettlementFoodModel` constants, so the cap knobs are wired through the engine’s own call site rather than recomputed in TAOM. I’m going to run the targeted SettlementFood tests now if the local test project is available.
exec
"C:\\Program Files\\PowerShell\\7\\pwsh.exe" -Command "Get-ChildItem -Path . -Filter '*.sln' | ForEach-Object { "'$_.FullName }' in C:\Users\mikew\source\repos\TAOM
exec
"C:\\Program Files\\PowerShell\\7\\pwsh.exe" -Command "Get-ChildItem -Path 'TAOM.Tests' -Filter '*.csproj' | ForEach-Object { "'$_.FullName }' in C:\Users\mikew\source\repos\TAOM
exec
"C:\\Program Files\\PowerShell\\7\\pwsh.exe" -Command "dotnet test 'TAOM.Tests/TAOM.Tests.csproj' --filter SettlementFood --no-restore" in C:\Users\mikew\source\repos\TAOM
2026-06-18T19:52:08.809340Z ERROR codex_core::tools::router: error=Exit code: 1
Wall time: 0.4 seconds
Output:
Failed to add 'C:\Users\CodexSandboxOffline\.dotnet\tools' to the PATH environment variable. Add this directory to your PATH to use tools installed with 'dotnet tool install'.
System.UnauthorizedAccessException: Access to the path 'C:\Users\CodexSandboxOffline\.dotnet\10.0.301.toolpath.sentinel' is denied.
   at Microsoft.Win32.SafeHandles.SafeFileHandle.CreateFile(String fullPath, FileMode mode, FileAccess access, FileShare share, FileOptions options)
   at Microsoft.Win32.SafeHandles.SafeFileHandle.Open(String fullPath, FileMode mode, FileAccess access, FileShare share, FileOptions options, Int64 preallocationSize, Nullable`1 unixCreateMode)
   at System.IO.Strategies.OSFileStreamStrategy..ctor(String path, FileMode mode, FileAccess access, FileShare share, FileOptions options, Int64 preallocationSize, Nullable`1 unixCreateMode)
   at System.IO.Strategies.FileStreamHelpers.ChooseStrategyCore(String path, FileMode mode, FileAccess access, FileShare share, FileOptions options, Int64 preallocationSize, Nullable`1 unixCreateMode)
   at System.IO.File.Create(String path)
   at Microsoft.Extensions.EnvironmentAbstractions.FileWrapper.CreateEmptyFile(String path)
   at Microsoft.DotNet.Cli.Utils.FileAccessRetrier.RetryOnIOException(Action action)
   at Microsoft.DotNet.Configurer.DotnetFirstTimeUseConfigurer.Configure()
   at Microsoft.DotNet.Cli.Program.ConfigureDotNetForFirstTimeUse(IFirstTimeUseNoticeSentinel firstTimeUseNoticeSentinel, IAspNetCertificateSentinel aspNetCertificateSentinel, IFileSentinel toolPathSentinel, Boolean isDotnetBeingInvokedFromNativeInstaller, DotnetFirstRunConfiguration dotnetFirstRunConfiguration, IEnvironmentProvider environmentProvider, Dictionary`2 performanceMeasurements, Boolean skipFirstTimeUseCheck)
   at Microsoft.DotNet.Cli.Program.ProcessArgs(String[] args, TimeSpan startupTime)
   at Microsoft.DotNet.Cli.Program.Main(String[] args)

 exited 1 in 361ms:
Failed to add 'C:\Users\CodexSandboxOffline\.dotnet\tools' to the PATH environment variable. Add this directory to your PATH to use tools installed with 'dotnet tool install'.
System.UnauthorizedAccessException: Access to the path 'C:\Users\CodexSandboxOffline\.dotnet\10.0.301.toolpath.sentinel' is denied.
   at Microsoft.Win32.SafeHandles.SafeFileHandle.CreateFile(String fullPath, FileMode mode, FileAccess access, FileShare share, FileOptions options)
   at Microsoft.Win32.SafeHandles.SafeFileHandle.Open(String fullPath, FileMode mode, FileAccess access, FileShare share, FileOptions options, Int64 preallocationSize, Nullable`1 unixCreateMode)
   at System.IO.Strategies.OSFileStreamStrategy..ctor(String path, FileMode mode, FileAccess access, FileShare share, FileOptions options, Int64 preallocationSize, Nullable`1 unixCreateMode)
   at System.IO.Strategies.FileStreamHelpers.ChooseStrategyCore(String path, FileMode mode, FileAccess access, FileShare share, FileOptions options, Int64 preallocationSize, Nullable`1 unixCreateMode)
   at System.IO.File.Create(String path)
   at Microsoft.Extensions.EnvironmentAbstractions.FileWrapper.CreateEmptyFile(String path)
   at Microsoft.DotNet.Cli.Utils.FileAccessRetrier.RetryOnIOException(Action action)
   at Microsoft.DotNet.Configurer.DotnetFirstTimeUseConfigurer.Configure()
   at Microsoft.DotNet.Cli.Program.ConfigureDotNetForFirstTimeUse(IFirstTimeUseNoticeSentinel firstTimeUseNoticeSentinel, IAspNetCertificateSentinel aspNetCertificateSentinel, IFileSentinel toolPathSentinel, Boolean isDotnetBeingInvokedFromNativeInstaller, DotnetFirstRunConfiguration dotnetFirstRunConfiguration, IEnvironmentProvider environmentProvider, Dictionary`2 performanceMeasurements, Boolean skipFirstTimeUseCheck)
   at Microsoft.DotNet.Cli.Program.ProcessArgs(String[] args, TimeSpan startupTime)
   at Microsoft.DotNet.Cli.Program.Main(String[] args)

 succeeded in 462ms:
C:\Users\mikew\source\repos\TAOM\TAOM.Tests\TAOM.Tests.csproj

 succeeded in 465ms:
C:\Users\mikew\source\repos\TAOM\TAOM.sln

codex
The first test invocation didn’t reach the test runner because the dotnet first-run setup tried to write under the sandbox user profile and was denied. I’m retrying with `DOTNET_CLI_HOME` redirected to `C:\tmp` so the command can initialize without touching the blocked profile.
exec
"C:\\Program Files\\PowerShell\\7\\pwsh.exe" -Command '$env:DOTNET_CLI_HOME='"'C:\\tmp\\dotnet_home'; "'$env:DOTNET_SKIP_FIRST_TIME_EXPERIENCE='"'1'; dotnet test 'TAOM.Tests/TAOM.Tests.csproj' --filter SettlementFood --no-restore" in C:\Users\mikew\source\repos\TAOM
2026-06-18T19:52:15.904154Z ERROR codex_core::tools::router: error=Exit code: 1
Wall time: 0.4 seconds
Output:
Failed to add 'C:\tmp\dotnet_home\.dotnet\tools' to the PATH environment variable. Add this directory to your PATH to use tools installed with 'dotnet tool install'.
System.UnauthorizedAccessException: Access to the path 'C:\tmp\dotnet_home\.dotnet' is denied.
   at System.IO.FileSystem.CreateDirectory(String fullPath, Byte[] securityDescriptor)
   at System.IO.Directory.CreateDirectory(String path)
   at Microsoft.Extensions.EnvironmentAbstractions.DirectoryWrapper.CreateDirectory(String path)
   at Microsoft.DotNet.Configurer.FileSystemExtensions.<>c__DisplayClass0_0.<CreateIfNotExists>b__0()
   at Microsoft.DotNet.Cli.Utils.FileAccessRetrier.RetryOnIOException(Action action)
   at Microsoft.DotNet.Configurer.DotnetFirstTimeUseConfigurer.Configure()
   at Microsoft.DotNet.Cli.Program.ConfigureDotNetForFirstTimeUse(IFirstTimeUseNoticeSentinel firstTimeUseNoticeSentinel, IAspNetCertificateSentinel aspNetCertificateSentinel, IFileSentinel toolPathSentinel, Boolean isDotnetBeingInvokedFromNativeInstaller, DotnetFirstRunConfiguration dotnetFirstRunConfiguration, IEnvironmentProvider environmentProvider, Dictionary`2 performanceMeasurements, Boolean skipFirstTimeUseCheck)
   at Microsoft.DotNet.Cli.Program.ProcessArgs(String[] args, TimeSpan startupTime)
   at Microsoft.DotNet.Cli.Program.Main(String[] args)

 exited 1 in 416ms:
Failed to add 'C:\tmp\dotnet_home\.dotnet\tools' to the PATH environment variable. Add this directory to your PATH to use tools installed with 'dotnet tool install'.
System.UnauthorizedAccessException: Access to the path 'C:\tmp\dotnet_home\.dotnet' is denied.
   at System.IO.FileSystem.CreateDirectory(String fullPath, Byte[] securityDescriptor)
   at System.IO.Directory.CreateDirectory(String path)
   at Microsoft.Extensions.EnvironmentAbstractions.DirectoryWrapper.CreateDirectory(String path)
   at Microsoft.DotNet.Configurer.FileSystemExtensions.<>c__DisplayClass0_0.<CreateIfNotExists>b__0()
   at Microsoft.DotNet.Cli.Utils.FileAccessRetrier.RetryOnIOException(Action action)
   at Microsoft.DotNet.Configurer.DotnetFirstTimeUseConfigurer.Configure()
   at Microsoft.DotNet.Cli.Program.ConfigureDotNetForFirstTimeUse(IFirstTimeUseNoticeSentinel firstTimeUseNoticeSentinel, IAspNetCertificateSentinel aspNetCertificateSentinel, IFileSentinel toolPathSentinel, Boolean isDotnetBeingInvokedFromNativeInstaller, DotnetFirstRunConfiguration dotnetFirstRunConfiguration, IEnvironmentProvider environmentProvider, Dictionary`2 performanceMeasurements, Boolean skipFirstTimeUseCheck)
   at Microsoft.DotNet.Cli.Program.ProcessArgs(String[] args, TimeSpan startupTime)
   at Microsoft.DotNet.Cli.Program.Main(String[] args)

exec
"C:\\Program Files\\PowerShell\\7\\pwsh.exe" -Command "Test-Path 'C:\\tmp'; Get-Item 'C:\\tmp' -ErrorAction SilentlyContinue | Select-Object FullName,Attributes" in C:\Users\mikew\source\repos\TAOM
 succeeded in 440ms:
True

[32;1mFullName[0m[32;1m Attributes[0m
[32;1m--------[0m [32;1m----------[0m
C:\tmp    Directory


codex
`C:\tmp` exists, but this sandbox still denied the dotnet first-run directory creation there. I’m going to treat test execution as blocked unless a repo-local dotnet home works; the source review itself is not blocked by that.
exec
"C:\\Program Files\\PowerShell\\7\\pwsh.exe" -Command '$home = Join-Path (Get-Location) '"'.codex-dotnet-home'; New-Item -ItemType Directory -Path "'$home -Force | Out-Null; $env:DOTNET_CLI_HOME=$home; $env:DOTNET_SKIP_FIRST_TIME_EXPERIENCE='"'1'; dotnet test 'TAOM.Tests/TAOM.Tests.csproj' --filter SettlementFood --no-restore" in C:\Users\mikew\source\repos\TAOM
2026-06-18T19:52:34.173218Z ERROR codex_core::tools::router: error=Exit code: 1
Wall time: 0.8 seconds
Output:
C:\Program Files\dotnet\sdk\10.0.301\Microsoft.Common.CurrentVersion.targets(93,5): error MSB4184: The expression "[Microsoft.Build.Utilities.ToolLocationHelper]::GetPlatformSDKLocation(Windows, 7.0)" cannot be evaluated. Access to the path 'C:\Users\mikew\AppData\Local\Microsoft SDKs' is denied. [C:\Users\mikew\source\repos\TAOM\TAOM.Tests\TAOM.Tests.csproj]
[31;1mWriteError: [0m
[31;1m[36;1mLine |[0m
[31;1m[36;1m[36;1m   2 | [0m [36;1m$home = Join-Path (Get-Location) '.codex-dotnet-home'[0m; New-Item -Item …[0m
[31;1m[36;1m[36;1m[0m[36;1m[0m[36;1m     | [31;1m ~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~[0m
[31;1m[36;1m[36;1m[0m[36;1m[0m[36;1m[31;1m[31;1m[36;1m     | [31;1mCannot overwrite variable HOME because it is read-only or constant.[0m

 exited 1 in 765ms:
C:\Program Files\dotnet\sdk\10.0.301\Microsoft.Common.CurrentVersion.targets(93,5): error MSB4184: The expression "[Microsoft.Build.Utilities.ToolLocationHelper]::GetPlatformSDKLocation(Windows, 7.0)" cannot be evaluated. Access to the path 'C:\Users\mikew\AppData\Local\Microsoft SDKs' is denied. [C:\Users\mikew\source\repos\TAOM\TAOM.Tests\TAOM.Tests.csproj]
[31;1mWriteError: [0m
[31;1m[36;1mLine |[0m
[31;1m[36;1m[36;1m   2 | [0m [36;1m$home = Join-Path (Get-Location) '.codex-dotnet-home'[0m; New-Item -Item …[0m
[31;1m[36;1m[36;1m[0m[36;1m[0m[36;1m     | [31;1m ~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~[0m
[31;1m[36;1m[36;1m[0m[36;1m[0m[36;1m[31;1m[31;1m[36;1m     | [31;1mCannot overwrite variable HOME because it is read-only or constant.[0m

codex
One architectural nuance surfaced: the pure `ComputeFoodDelta` path is engine-free, but the service class also has an `ApplyFoodAdjustment` helper that takes `ExplainedNumber`. I’m checking whether that’s a sealed-type ADR issue or just a harmless value-type boundary helper before deciding whether it belongs in findings.
exec
"C:\\Program Files\\PowerShell\\7\\pwsh.exe" -Command "Get-ChildItem -Path 'E:\\Decompiled_Bannerlord' -Recurse -Filter 'ExplainedNumber.cs' -ErrorAction SilentlyContinue | Select-Object -First 20 | ForEach-Object { "'$_.FullName }' in C:\Users\mikew\source\repos\TAOM
exec
"C:\\Program Files\\PowerShell\\7\\pwsh.exe" -Command '$i=0; Get-Content '"'Main/Features/SettlementFood/ISettlementFoodService.cs' | ForEach-Object { "'$i++; '"'{0}:{1}: {2}' -f (Resolve-Path 'Main/Features/SettlementFood/ISettlementFoodService.cs'),"'$i,$_ }' in C:\Users\mikew\source\repos\TAOM
exec
"C:\\Program Files\\PowerShell\\7\\pwsh.exe" -Command '$i=0; Get-Content '"'Main/Features/SettlementFood/ISettlementFoodConfigProvider.cs' | ForEach-Object { "'$i++; '"'{0}:{1}: {2}' -f (Resolve-Path 'Main/Features/SettlementFood/ISettlementFoodConfigProvider.cs'),"'$i,$_ }' in C:\Users\mikew\source\repos\TAOM
exec
"C:\\Program Files\\PowerShell\\7\\pwsh.exe" -Command "Select-String -Path 'docs/*.md','docs/**/*.md' -Pattern 'ADR-007|sealed TaleWorlds|service never touches|pure.*TaleWorlds|ExplainedNumber' | Select-Object -First 120 | ForEach-Object { \"{0}:{1}: {2}\" -f "'$_.Path,$_.LineNumber,$_.Line.Trim() }' in C:\Users\mikew\source\repos\TAOM
 succeeded in 459ms:
C:\Users\mikew\source\repos\TAOM\Main\Features\SettlementFood\ISettlementFoodConfigProvider.cs:1: namespace TAOM.Features.SettlementFood;
C:\Users\mikew\source\repos\TAOM\Main\Features\SettlementFood\ISettlementFoodConfigProvider.cs:2: 
C:\Users\mikew\source\repos\TAOM\Main\Features\SettlementFood\ISettlementFoodConfigProvider.cs:3: public interface ISettlementFoodConfigProvider
C:\Users\mikew\source\repos\TAOM\Main\Features\SettlementFood\ISettlementFoodConfigProvider.cs:4: {
C:\Users\mikew\source\repos\TAOM\Main\Features\SettlementFood\ISettlementFoodConfigProvider.cs:5:     SettlementFoodConfig GetConfig();
C:\Users\mikew\source\repos\TAOM\Main\Features\SettlementFood\ISettlementFoodConfigProvider.cs:6: }

 succeeded in 494ms:
E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\TaleWorlds.CampaignSystem\ExplainedNumber.cs

 succeeded in 479ms:
C:\Users\mikew\source\repos\TAOM\Main\Features\SettlementFood\ISettlementFoodService.cs:1: using TaleWorlds.CampaignSystem;
C:\Users\mikew\source\repos\TAOM\Main\Features\SettlementFood\ISettlementFoodService.cs:2: 
C:\Users\mikew\source\repos\TAOM\Main\Features\SettlementFood\ISettlementFoodService.cs:3: namespace TAOM.Features.SettlementFood;
C:\Users\mikew\source\repos\TAOM\Main\Features\SettlementFood\ISettlementFoodService.cs:4: 
C:\Users\mikew\source\repos\TAOM\Main\Features\SettlementFood\ISettlementFoodService.cs:5: public interface ISettlementFoodService
C:\Users\mikew\source\repos\TAOM\Main\Features\SettlementFood\ISettlementFoodService.cs:6: {
C:\Users\mikew\source\repos\TAOM\Main\Features\SettlementFood\ISettlementFoodService.cs:7:     /// <summary>
C:\Users\mikew\source\repos\TAOM\Main\Features\SettlementFood\ISettlementFoodService.cs:8:     /// Pure: the daily food adjustment to add on top of vanilla's <c>CalculateTownFoodStocksChange</c>.
C:\Users\mikew\source\repos\TAOM\Main\Features\SettlementFood\ISettlementFoodService.cs:9:     /// Combines the garrison raw-count correction (always) with the siege-gated production knobs.
C:\Users\mikew\source\repos\TAOM\Main\Features\SettlementFood\ISettlementFoodService.cs:10:     /// Returns 0 when <paramref name="enabled"/> is false.
C:\Users\mikew\source\repos\TAOM\Main\Features\SettlementFood\ISettlementFoodService.cs:11:     /// </summary>
C:\Users\mikew\source\repos\TAOM\Main\Features\SettlementFood\ISettlementFoodService.cs:12:     float ComputeFoodDelta(TownFoodSnapshot snapshot, SettlementFoodConfig config, bool enabled);
C:\Users\mikew\source\repos\TAOM\Main\Features\SettlementFood\ISettlementFoodService.cs:13: 
C:\Users\mikew\source\repos\TAOM\Main\Features\SettlementFood\ISettlementFoodService.cs:14:     /// <summary>
C:\Users\mikew\source\repos\TAOM\Main\Features\SettlementFood\ISettlementFoodService.cs:15:     /// Applies <see cref="ComputeFoodDelta"/> to <paramref name="result"/> (the vanilla food
C:\Users\mikew\source\repos\TAOM\Main\Features\SettlementFood\ISettlementFoodService.cs:16:     /// ExplainedNumber). No-op when the delta is zero.
C:\Users\mikew\source\repos\TAOM\Main\Features\SettlementFood\ISettlementFoodService.cs:17:     /// </summary>
C:\Users\mikew\source\repos\TAOM\Main\Features\SettlementFood\ISettlementFoodService.cs:18:     void ApplyFoodAdjustment(
C:\Users\mikew\source\repos\TAOM\Main\Features\SettlementFood\ISettlementFoodService.cs:19:         TownFoodSnapshot snapshot,
C:\Users\mikew\source\repos\TAOM\Main\Features\SettlementFood\ISettlementFoodService.cs:20:         SettlementFoodConfig config,
C:\Users\mikew\source\repos\TAOM\Main\Features\SettlementFood\ISettlementFoodService.cs:21:         bool enabled,
C:\Users\mikew\source\repos\TAOM\Main\Features\SettlementFood\ISettlementFoodService.cs:22:         ref ExplainedNumber result,
C:\Users\mikew\source\repos\TAOM\Main\Features\SettlementFood\ISettlementFoodService.cs:23:         bool includeDescriptions);
C:\Users\mikew\source\repos\TAOM\Main\Features\SettlementFood\ISettlementFoodService.cs:24: }

 succeeded in 560ms:
C:\Users\mikew\source\repos\TAOM\docs\INDEX.md:12: | Authoring a new feature module | [ai-includes/architecture.md](ai-includes/architecture.md) + [ADR-002](adrs/002-thin-entry-points.md) + [ADR-007](adrs/007-adapter-pattern.md), then pick the nearest existing feature doc as a template |
C:\Users\mikew\source\repos\TAOM\docs\INDEX.md:132: - [campaign-object-graph](reference/engine/campaign-object-graph.md) (Phase 16 — `Hero`∈`Clan`∈`Kingdom` (IFaction), `MobileParty`→`PartyBase`, `Settlement`(town/castle/village, `OwnerClan`); the graph every TAOM campaign behavior mutates; ADR-007 adapters, `?.`-chains, non-saved `Settlement.Culture`, castle `.Village==null`; **entirely managed, no native boundary**)
C:\Users\mikew\source\repos\TAOM\docs\INDEX.md:152: - [ADR-007 Adapter Pattern](adrs/007-adapter-pattern.md) — services use `IHeroAdapter` etc., NEVER `Hero` directly
C:\Users\mikew\source\repos\TAOM\docs\adrs\002-thin-entry-points.md:207: public override ExplainedNumber CalculateTownFoodStocksChange(Town town, ...)
C:\Users\mikew\source\repos\TAOM\docs\adrs\002-thin-entry-points.md:227: public override ExplainedNumber CalculateTownFoodStocksChange(Town town, ...)
C:\Users\mikew\source\repos\TAOM\docs\adrs\002-thin-entry-points.md:229: ExplainedNumber result = new ExplainedNumber(0f, includeDescriptions);
C:\Users\mikew\source\repos\TAOM\docs\adrs\002-thin-entry-points.md:333: - **ADR-007**: Adapter Pattern for Sealed Classes - Detailed adapter pattern implementation
C:\Users\mikew\source\repos\TAOM\docs\adrs\007-adapter-pattern.md:1: # ADR-007: Adapter Pattern Enforcement for Sealed Game Classes
C:\Users\mikew\source\repos\TAOM\docs\adrs\007-adapter-pattern.md:5: **Context**: Mount & Blade II modding with sealed TaleWorlds game classes
C:\Users\mikew\source\repos\TAOM\docs\adrs\007-adapter-pattern.md:9: Service layer MUST use adapter interfaces for all sealed TaleWorlds game classes. Entry points (CampaignBehaviors, GameModels, Harmony patches, MissionViews) convert sealed types to adapters before calling services. **Adapters MUST recursively wrap ALL nested sealed types** - when a sealed class contains properties that return other sealed classes, those nested types must also be wrapped in adapter interfaces. This enables 100% testability of business logic.
C:\Users\mikew\source\repos\TAOM\docs\adrs\007-adapter-pattern.md:48: **Services MUST NOT accept sealed TaleWorlds types**:
C:\Users\mikew\source\repos\TAOM\docs\adrs\007-adapter-pattern.md:559: - `ExplainedNumber` - Number with explanation breakdown
C:\Users\mikew\source\repos\TAOM\docs\adrs\008-testability-requirements.md:260: - **ADR-007**: Adapter Pattern for Sealed Classes - Services use adapters, not sealed types
C:\Users\mikew\source\repos\TAOM\docs\adrs\README.md:15: | [007](./007-adapter-pattern.md) | Adapter Pattern for Sealed Classes | Services use adapters, NOT sealed TaleWorlds types | Accepted | **Mandatory** |
C:\Users\mikew\source\repos\TAOM\docs\adrs\README.md:32: - Services MUST use adapter interfaces (IHeroAdapter, etc.) (ADR-007)
C:\Users\mikew\source\repos\TAOM\docs\adrs\README.md:33: - Services MUST NOT accept sealed types (Hero, Settlement, etc.) (ADR-007)
C:\Users\mikew\source\repos\TAOM\docs\adrs\README.md:39: - **New Features**: Read [ADR-002](./002-thin-entry-points.md), [ADR-007](./007-adapter-pattern.md)
C:\Users\mikew\source\repos\TAOM\docs\adrs\README.md:42: - **Service Design**: Read [ADR-007](./007-adapter-pattern.md) (adapter pattern), [ADR-005](./005-no-preprocessor-directives.md) (environment handling)
C:\Users\mikew\source\repos\TAOM\docs\ai-includes\agent-operating-manual.md:71: - Adapter pattern (ADR-007), thin entry points (ADR-002), no #region/[Obsolete]/#if DEBUG (ADR-003/004/005): [`docs/adrs/`](../adrs/README.md)
C:\Users\mikew\source\repos\TAOM\docs\ai-includes\agent-teams.md:77: Follow the adapter pattern (ADR-007). Do NOT edit IoC.cs or SubModule.cs — report needed
C:\Users\mikew\source\repos\TAOM\docs\ai-includes\agent-teams.md:223: - Follow adapter pattern (ADR-007): use IHeroAdapter etc, never Hero directly
C:\Users\mikew\source\repos\TAOM\docs\ai-includes\architecture.md:108: - Only use adapter interfaces, never sealed types (ADR-007)
C:\Users\mikew\source\repos\TAOM\docs\ai-includes\architecture.md:148: **Rules for Adapters (ADR-007):**
C:\Users\mikew\source\repos\TAOM\docs\ai-includes\architecture.md:237: 1. Decompile the sealed TaleWorlds class
C:\Users\mikew\source\repos\TAOM\docs\ai-includes\architecture.md:433: | ADR-007 | Adapter Pattern | Testability for sealed types |
C:\Users\mikew\source\repos\TAOM\docs\ai-includes\code-quality.md:216: - If one service uses adapter interfaces, all should (ADR-007)
C:\Users\mikew\source\repos\TAOM\docs\ai-includes\code-quality.md:317: ### Depend on Abstractions (ADR-007)
C:\Users\mikew\source\repos\TAOM\docs\ai-includes\code-quality.md:499: - [ ] Uses adapter interfaces, not sealed types (ADR-007)
C:\Users\mikew\source\repos\TAOM\docs\ai-includes\creature-mount-authoring.md:216: | Attack service | pure, TaleWorlds-free (`ShouldEngage` / cooldowns / damage), boundary nodes hold the raw `Agent` | warg-pattern rider damage attribution |
C:\Users\mikew\source\repos\TAOM\docs\ai-includes\decompiled-code-analysis.md:101: → Adapter (wraps sealed TaleWorlds types)
C:\Users\mikew\source\repos\TAOM\docs\ai-includes\decompiled-code-analysis.md:252: | **Adapter Pattern** | Services use `IHeroAdapter` etc, NEVER `Hero` etc (ADR-007) |
C:\Users\mikew\source\repos\TAOM\docs\ai-includes\multi-approach-validation.md:206: - [ ] Services use adapter interfaces only (ADR-007)
C:\Users\mikew\source\repos\TAOM\docs\ai-includes\patterns.md:633: ### ❌ Using `object` in Hook Interfaces (ADR-007 Violation)
C:\Users\mikew\source\repos\TAOM\docs\ai-includes\patterns.md:647: ### ✅ Using Adapter Interfaces in Hooks (ADR-007 Compliant)
C:\Users\mikew\source\repos\TAOM\docs\ai-includes\taleworlds-research-guide.md:27: - Verify all properties and methods on the sealed TaleWorlds type
C:\Users\mikew\source\repos\TAOM\docs\ai-includes\taleworlds-research-guide.md:30: - Discover nested sealed types that need recursive wrapping (ADR-007)
C:\Users\mikew\source\repos\TAOM\docs\ai-includes\taleworlds-research-guide.md:685: - Reference [ADR-007 Null-Conditional Operators section](../adrs/007-adapter-pattern.md#null-conditional-operators-for-computed-properties-critical)
C:\Users\mikew\source\repos\TAOM\docs\ai-includes\taleworlds-research-guide.md:765: - **Adapter Pattern (ADR-007)**: `docs/adrs/007-adapter-pattern.md`
C:\Users\mikew\source\repos\TAOM\docs\ai-includes\tdd-enforcement.md:288: // ❌ Cannot mock sealed TaleWorlds type
C:\Users\mikew\source\repos\TAOM\docs\ai-includes\testing-guide.md:109: ### The Solution: Adapter Interfaces (ADR-007)
C:\Users\mikew\source\repos\TAOM\docs\audits\cluster-campaign-behaviors.md:99: - **P2** — `CharacterCreationContentService` uses sealed `Hero`/`MobileParty`/`Settlement`/`MBObjectManager` directly (ADR-007). 7 violation sites at lines 166-176, 218, 235, 327-332, 347. Service is untestable. Fix: extract `IPlayerHeroAdapter`, `IPlayerPartyAdapter`, `ISettlementAdapter`, `ICultureCreationDataProvider`.
C:\Users\mikew\source\repos\TAOM\docs\audits\cluster-campaign-behaviors.md:167: ### RaceAge ❌ ADR-007 + R3 + R4
C:\Users\mikew\source\repos\TAOM\docs\audits\cluster-campaign-behaviors.md:171: - **P1** — `TaomPregnancyModel.GetDailyChanceOfPregnancyForHero` contains 32 lines of inline business logic (sealed `Hero` access, `Math.Min`, `ExplainedNumber`, `GetPerkValue`, full vanilla pregnancy reimplementation) at `TaomPregnancyModel.cs:18-58`. Out of Phase 3 scope (Phase 2 GameModel territory) but flagged P1 because it's an ADR-007 + GameModel rule double-violation. Fix: extract to `IRaceAgeService.GetDailyPregnancyChance(IHeroAgeInfo)`.
C:\Users\mikew\source\repos\TAOM\docs\audits\cluster-campaign-behaviors.md:244: - **P3** — `FiefManagementGameState.Fief` exposes sealed `Settlement` (ADR-007). Fix: store `string SettlementId` instead.
C:\Users\mikew\source\repos\TAOM\docs\audits\cluster-campaign-behaviors.md:307: - GameModel reviews (Phase 2 / future): `TaomPregnancyModel` ADR-007 violation; `TaomAgeModel` hardcoded constants; `TaomSiegeEventModel`; `TaomTargetScoreModel` (untouched).
C:\Users\mikew\source\repos\TAOM\docs\audits\cluster-cross-feature.md:48: | 12 | P3 | RaceAge | `TaomHeroCreationModel.GetCharacterTemplateForOffspring` | `TaomHeroCreationModel.cs:9-17` | Returns sealed `CharacterObject` (forced by TaleWorlds API). ADR-007 design smell. | — |
C:\Users\mikew\source\repos\TAOM\docs\audits\cluster-cross-feature.md:55: | 19 | P3 | BannerColorPersistence | Direct `Clan.PlayerClan` access | `PartyCharacterVM_GetCharacterCode_Patch.cs:39-42` | Bypasses adapter that sibling patches use. ADR-007 inconsistency. | — |
C:\Users\mikew\source\repos\TAOM\docs\audits\cluster-cross-feature.md:59: | 23 | P2 | CulturalFeats (Smithing) | Int truncation before career passive | `TaomSmithingModel.cs:46, 51` | `(int)` cast on culture-feat-modified cost before applying career passive multiplier. Result shifts by 1 vs same-line `ExplainedNumber.AddFactor` composition. | #173 |
C:\Users\mikew\source\repos\TAOM\docs\audits\cluster-cross-feature.md:125: - **F4 (P3):** `TaomHeroCreationModel.GetCharacterTemplateForOffspring` returns sealed `CharacterObject` — TaleWorlds-forced; document as ADR-007 constraint.
C:\Users\mikew\source\repos\TAOM\docs\audits\cluster-cross-feature.md:158: - **F4 (P2):** `TaomSmithingModel` applies an `(int)` truncation to culture-feat-modified cost BEFORE applying career passive — shifts result by 1 vs same-line `ExplainedNumber` composition. All other models compose in `ExplainedNumber` end-to-end.
C:\Users\mikew\source\repos\TAOM\docs\audits\cluster-cross-feature.md:161: **Precedence verdict:** Consistent and additive across all 10 call sites: `base.X()`, then helper. `ExplainedNumber.AddFactor` is multiplicative on the running result; order between culture feats and career passive is commutative.
C:\Users\mikew\source\repos\TAOM\docs\audits\cluster-gamemodels.md:13: - **P2** — degraded or silently inert. Rule 4 violations (inline branching in override body); missing config-provider validation; unguarded `.X` chains where `.X` can be null at common call times; service-locator inside model body; ADR-007 sealed-type access in model body; cross-feature static helper coupling.
C:\Users\mikew\source\repos\TAOM\docs\audits\cluster-gamemodels.md:48: | 19 | P2 | Diplomacy | TaomDiplomacyModel | TaomDiplomacyModel.cs:32–38 | `GetRelationChange...VotingInSettlementOwner...` Isengard feat branch inline in model — **untestable** because takes sealed `Hero` (rule 4 + ADR-007) |
C:\Users\mikew\source\repos\TAOM\docs\audits\cluster-gamemodels.md:54: | 25 | P2 | Execution | TaomExecutionRelationModel | TaomExecutionRelationModel.cs:20 | `Hero.MainHero.MapFaction.StringId` direct access — sealed type in model body (ADR-007) |
C:\Users\mikew\source\repos\TAOM\docs\audits\cluster-gamemodels.md:314: 1 confirmed (`TaomExecutionRelationModel.cs:20`). ADR-007 violation — sealed types should not cross the model boundary. Phase 6 may surface more.
C:\Users\mikew\source\repos\TAOM\docs\audits\cluster-gamemodels.md:330: | [#147](https://github.com/haterade22/TAOM/issues/147) | Execution — TaomExecutionRelationModel architectural smell (hook injected into model) + ADR-007 + rule-4 (3 P2) | P2 ×3 | Execution |
C:\Users\mikew\source\repos\TAOM\docs\audits\cluster-gamemodels.md:337: - **#131 RaceAge — TaomPregnancyModel ADR-007 violation + singleton race cache stale + R3+R4** already captures the Pregnancy model's rule 4 violation, NaN/Infinity config gap, and validate-before-lookup pattern. The new null-safety findings (`hero.Spouse` / `hero.Clan` unguarded chains at lines 40-41) and the `TaomAgeModel` sentinel-constants P3 are **noted in this cluster doc's RaceAge section** but do not warrant a separate issue — Phase 9 triage of #131 should cover them when the model is touched.
C:\Users\mikew\source\repos\TAOM\docs\audits\phase-1-kickoff.md:46: 3. For each parameter: is it a primitive (constant-OK), a sealed TaleWorlds type (boundary-OK), or an interface (`Ixxx`)?
C:\Users\mikew\source\repos\TAOM\docs\audits\phase-9-completion.md:90: 2. **Warg ADR-007 IAgentBattleAdapter expansion** (#178) — design-first, then implement
C:\Users\mikew\source\repos\TAOM\docs\audits\phase-9-completion.md:102: | **#144 + #176** CulturalFeats systemic | `4431cff` | `ICulturalFeatsService` (19 methods) + `ICultureFeatAdapter` (ADR-007) + concrete service + IoC + all 16 `Taom*Model.cs` refactored to thin boundaries. **+49 tests** in `CulturalFeatsServiceTests`. 26 files, +1437/-367. |
C:\Users\mikew\source\repos\TAOM\docs\audits\phase-9-completion.md:103: | **#178** Warg ADR-007 | `5a61e17` | `IWargAttackService` now adapter-pure. No new IAgentAdapter surface needed (existing surface sufficient). **+15 tests** (7 → 22; 2 deferred via `[Ignore]` for `ActionIndexCache.Create` engine dependency). |
C:\Users\mikew\source\repos\TAOM\docs\audits\phase-9-fix-queue.md:75: | #125 | P2 | CharacterCreation | (not R1-R5; ADR-007) | Extract `IPlayerHeroAdapter` / `IPlayerPartyAdapter` / `ISettlementAdapter` / `ICultureCreationDataProvider`; constructor-inject `ICareerCreationHandler` + `ICareerRegistry`; widen adapter to include `IsFemale` (per A1 batch note); reset `SelectedCareerStringId` in `OnSessionLaunched` |
C:\Users\mikew\source\repos\TAOM\docs\audits\phase-9-fix-queue.md:187: | **#178** | **P1** | Warg | **Refactor `IWargAttackService` to accept `IAgentAdapter` per ADR-007 FIRST**, then add tests for `HandleWargTargetHit` + `WargAttack`. The refactor is the unlock — without it, 2 methods stay untestable. |
C:\Users\mikew\source\repos\TAOM\docs\audits\phase-9-fix-queue.md:201: | #199 | P2 | Warg | Update `warg-combat.md:117` Tests section — `WargAttackServiceTests.cs` exists with 7 tests; cross-reference #178 ADR-007 blocker for the 2 untestable methods |
C:\Users\mikew\source\repos\TAOM\docs\audits\README.md:18: | 7 | Test coverage audit — ADR-008 compliance per feature | `test-coverage.md` | **Complete (2026-05-13)** — 20 issues opened (#176–#195): 3 P1 (CulturalFeats 16-models-zero-tests, FiefManagement 5-callbacks-untested, Warg ADR-007-blocks-testing) + 17 P2 (wiring-regression test gaps, cross-feature handshake test gaps, untested GameModel branches). 8 P3 + 16 OK. Phase 0 carryovers resolved: CharacterSelection (transpiler — untestable by design, documented); BattleScenes (disabled — correct absence). Phase 5 #168 verified RESOLVED. Dominant gap: 80% behavior-hook coverage and manual-Harmony patch wiring tests. |
C:\Users\mikew\source\repos\TAOM\docs\audits\test-coverage.md:32: | 3 | **P1** | Warg | Two of four `WargAttackService` public methods (`HandleWargTargetHit`, `WargAttack`) accept sealed `Agent` — **untestable** per test-file comment lines 9-20. ADR-007 violation blocks ADR-008 100% target. | `Main/Features/Warg/WargAttackService.cs:32, :79` | `WargAttackServiceTests.cs` | #178 |
C:\Users\mikew\source\repos\TAOM\docs\audits\test-coverage.md:109: | #178 | P1 | audit-tests: Warg — refactor IWargAttackService to use IAgentAdapter (ADR-007); 2 methods currently untestable | new |
C:\Users\mikew\source\repos\TAOM\docs\audits\triage-results-A1.md:27: | 125 | CharacterCreation ADR-007 violations + IoC.Resolve in service body | VALID | P2+P2+P2+P3 | Extract IPlayerHeroAdapter / IPlayerPartyAdapter / ISettlementAdapter / ICultureCreationDataProvider; ctor-inject ICareerCreationHandler + ICareerRegistry; reset SelectedCareerStringId in OnSessionLaunched; null-guard MobileParty.MainParty | — | Confirmed `Hero.MainHero`, `MobileParty.MainParty.Position`, `Settlement.Find`, `MBObjectManager.Instance.GetObject<>` all touched directly; IoC.Resolve at lines 218 + 235 |
C:\Users\mikew\source\repos\TAOM\docs\audits\triage-results-A1.md:33: | 131 | RaceAge TaomPregnancyModel ADR-007 violation + R3 + R4 | VALID | P1+P1+P2+P2+P3 | Extract GetDailyPregnancyChance(IHeroAgeInfo) to IRaceAgeService; reset _raceIdCache on new-campaign; add FiniteFloatValidator + ordering invariants; IsValidRaceId gate before GetRaceNameFromId | Adapter for IHeroAgeInfo (new) | TaomPregnancyModel.cs:18-58 is 40+ lines of inline biz logic against sealed Hero — clear gamemodels.md rule-4 violation |
C:\Users\mikew\source\repos\TAOM\docs\audits\triage-results-A1.md:147: ### #125 — audit(impl): CharacterCreation — ADR-007 violations in service + IoC.Resolve in service body
C:\Users\mikew\source\repos\TAOM\docs\audits\triage-results-A1.md:187: **Audit finding:** Service touches sealed Hero / MobileParty / Settlement / MBObjectManager directly (ADR-007 violations); IoC.Resolve inside service body; CareerMenuService.SelectedCareerStringId not reset between sessions; MobileParty.MainParty.Position lacks null-guard.
C:\Users\mikew\source\repos\TAOM\docs\audits\triage-results-A1.md:191: **Reasoning:** All four findings confirmed verbatim against current source. ADR-007 mandates services use adapters, not sealed types; csharp-architecture.md "no service locator in services" rule is unambiguously violated at lines 218/235.
C:\Users\mikew\source\repos\TAOM\docs\audits\triage-results-A1.md:412: ### #131 — audit(impl): RaceAge — TaomPregnancyModel ADR-007 violation + singleton race cache stale + R3+R4
C:\Users\mikew\source\repos\TAOM\docs\audits\triage-results-A1.md:433: var result = new ExplainedNumber(baseChance);
C:\Users\mikew\source\repos\TAOM\docs\audits\triage-results-A1.md:438: (Direct sealed `Hero` access, `Math.Min`, `ExplainedNumber`, multi-line computation — gamemodels.md rule 4 explicitly forbids this.)
C:\Users\mikew\source\repos\TAOM\docs\audits\triage-results-A1.md:467: **Audit finding:** Inline business logic in TaomPregnancyModel (ADR-007 + GameModel rule 4); _raceIdCache singleton not reset; missing validate-before-lookup; missing semantic config validation.
C:\Users\mikew\source\repos\TAOM\docs\audits\triage-results-A1.md:574: 1. **#125 has an additional ADR-007 violation the audit didn't enumerate.** `CharacterCreationContentService.GrantPlayerStartupResources` (lines 181-209) reads `Hero.MainHero` and `Hero.MainHero.IsFemale` directly — same class as the audit's documented violations. Already covered by the audit's general extract-`IPlayerHeroAdapter` recommendation, so the fix scope doesn't change; just calling it out so the IPlayerHeroAdapter surface should include `IsFemale`.
C:\Users\mikew\source\repos\TAOM\docs\audits\triage-results-A2.md:21: One small textual drift was observed in #147 (the audit body cited `Hero.MainHero.MapFaction.StringId` while the current code uses `Hero.MainHero?.Clan?.Kingdom?.StringId`). The ADR-007 violation (direct sealed-type access in a model body) persists — the lookup path changed but the static-sealed access did not. Verdict VALID.
C:\Users\mikew\source\repos\TAOM\docs\audits\triage-results-A2.md:78: public override ExplainedNumber CalculateFinalSpeed(MobileParty mobileParty, ExplainedNumber finalSpeed)
C:\Users\mikew\source\repos\TAOM\docs\audits\triage-results-A2.md:421: - **P3 ADR-007 + IGameStateListener stubs:** flagged as P3 in audit body, no action this batch.
C:\Users\mikew\source\repos\TAOM\docs\audits\triage-results-A2.md:543: ### #147 — TaomExecutionRelationModel architectural smell + ADR-007 + rule-4 (3 × P2, VALID)
C:\Users\mikew\source\repos\TAOM\docs\audits\triage-results-A2.md:583: - **ADR-007 sealed access:** line 20 `Hero.MainHero?.Clan?.Kingdom?.StringId` reaches a sealed `Hero` static directly in the model body. Audit cited `MapFaction.StringId` — current path is `Clan?.Kingdom?.StringId`. The exact dereference chain differs but the violation (direct sealed-type access in model) holds.
C:\Users\mikew\source\repos\TAOM\docs\audits\triage-results-A2.md:596: public override ExplainedNumber GetTotalWage(MobileParty mobileParty, TroopRoster troopRoster, bool includeDescriptions = false)
C:\Users\mikew\source\repos\TAOM\docs\audits\triage-results-A2.md:669: - **#147 path drift:** the audit body cites `Hero.MainHero.MapFaction.StringId` at line 20; current code is `Hero.MainHero?.Clan?.Kingdom?.StringId`. ADR-007 violation persists either way. Mention this in the fix-PR description so the maintainer knows the audit text didn't lie — the lookup path simply evolved without the violation being addressed.
C:\Users\mikew\source\repos\TAOM\docs\audits\triage-results-C.md:268: var explained = new ExplainedNumber(featResult, false);
C:\Users\mikew\source\repos\TAOM\docs\audits\triage-results-D.md:70: ### #178 — Warg: 2 methods untestable per ADR-007 violation
C:\Users\mikew\source\repos\TAOM\docs\features\advanced-combat.md:15: 1. `Mission` and `Agent` are sealed TaleWorlds types. Their internal `RegisterBlow` method is non-public, requiring reflection to obtain a delegate at startup.
C:\Users\mikew\source\repos\TAOM\docs\features\alignment-recruitment.md:28: IRecruitmentAlignmentService.IsRecruitmentBlocked        ← pure: alignment predicate (no TaleWorlds types)
C:\Users\mikew\source\repos\TAOM\docs\features\arena.md:21: > **Architecture note (Phase 9b #137):** all decision logic was extracted from the GameModel into [`TournamentService`](../../Main/Features/Arena/TournamentService.cs) to satisfy the rule-4 "no inline branching in GameModel overrides" constraint ([.claude/rules/gamemodels.md](../../.claude/rules/gamemodels.md)). [`TaomTournamentModel`](../../Main/Features/Arena/Models/TaomTournamentModel.cs) is now a **thin entry point** that converts sealed TaleWorlds params to primitives at the boundary and delegates to the injected `ITournamentService`. Earlier revisions of this doc described logic living on the model — that is no longer accurate.
C:\Users\mikew\source\repos\TAOM\docs\features\bandit-management.md:15: Standard TAOM feature module pattern (ADR-002 thin entry, ADR-007 adapter, single-responsibility services). The runtime side has three pieces:
C:\Users\mikew\source\repos\TAOM\docs\features\bandit-management.md:25: IBanditScalingService                                    ← pure math, no TaleWorlds deps
C:\Users\mikew\source\repos\TAOM\docs\features\bandit-management.md:158: `IHideoutDescriptionService.GetDescription(cultureStringId)` returns the `{=key}default` template for the five TAOM bandit cultures and `null` for any other culture (vanilla / other-mod hideouts keep their own engine description untouched). The service takes a `string` and returns a `string` — no TaleWorlds types cross the boundary (ADR-007).
C:\Users\mikew\source\repos\TAOM\docs\features\battle-load-diagnostics.md:85: | `Main/Adapters/IEquipmentSnapshotAdapter.cs` / `EquipmentSnapshotAdapter.cs` | ADR-007 boundary: `Agent`/`Equipment`/`ItemObject` → `EquipmentSnapshot` |
C:\Users\mikew\source\repos\TAOM\docs\features\career-quest-system.md:22: - **Adapter (ADR-007)** — `IQuestHeroAdapter` (reads skill/renown/gold; sinks renown/influence/item); the service never touches `Hero`.
C:\Users\mikew\source\repos\TAOM\docs\features\career-system.md:218: - TaleWorlds.CampaignSystem (Hero, CampaignEvents, ExplainedNumber)
C:\Users\mikew\source\repos\TAOM\docs\features\character-creation-body-properties.md:25: `BodyProperties` is also a sealed `TaleWorlds.Core` struct. Per ADR-007 it cannot cross service boundaries; only `IPlayerBodyPropertiesAdapter` parses it (`BodyProperties.FromString`) and applies it to `Hero.MainHero` + `CharacterObject.PlayerCharacter`.
C:\Users\mikew\source\repos\TAOM\docs\features\character-creation-body-properties.md:158: The adapter (`PlayerBodyPropertiesAdapter`) and the three Harmony patches are intentionally not unit-tested — they are thin wrappers / boundary classes over sealed TaleWorlds engine APIs (`BodyProperties.FromString`, `UpdatePlayerCharacterBodyProperties`, Hero property setters, `NarrativeMenuCharacter.UpdateBodyProperties`). Coverage is via in-game verification.
C:\Users\mikew\source\repos\TAOM\docs\features\companion-tactics.md:33: ADR-007 mandates services see only `IXxxAdapter`. Sealed `Hero` / `Agent` / `Equipment` cross the boundary only at adapter implementations + boundary classes (Harmony patches, MissionView, ViewModels, OOBOverlayService).
C:\Users\mikew\source\repos\TAOM\docs\features\companion-tactics.md:147: - `IFormationAdapter`, `IHeroCombatAdapter`, `IAgentCombatAdapter`, `IBattleEquipmentSnapshot` — sealed-type wrappers per ADR-007.
C:\Users\mikew\source\repos\TAOM\docs\features\crash-report.md:143: | [Main/Features/CrashReport/Domain/](../../Main/Features/CrashReport/Domain) | 18 record types — pure CLR DTOs, no TaleWorlds deps (ADR-007) |
C:\Users\mikew\source\repos\TAOM\docs\features\cultural-feats.md:159: Four cultures gain a flat `AddFactor` bonus on the per-notable daily volunteer-production probability returned by `DefaultVolunteerModel.GetDailyVolunteerProductionProbability(Hero notable, int index, Settlement settlement)`. The vanilla value (typically 0.7–0.95) is wrapped in `ExplainedNumber`, our factor is added, and the result is clamped to `[0,1]`. Keyed on `settlement.OwnerClan?.Culture` — economic/recruitment effects follow ownership (a Mordor village produces faster while Mordor owns it; conquest by another culture removes the bonus on the next daily tick). Matches how `TaomSettlementMilitiaModel` resolves the same trade-off.
C:\Users\mikew\source\repos\TAOM\docs\features\cultural-feats.md:161: No vanilla culture has a volunteer-rate feat to mirror, so this is a brand-new hook site. The `ExplainedNumber + AddFactor` pattern matches how vanilla itself applies the Cantons kingdom policy and the CavalryTactics perk inside the same `DefaultVolunteerModel` method.
C:\Users\mikew\source\repos\TAOM\docs\features\cultural-feats.md:172: `TaomNotableSpawnModel : DefaultNotableSpawnModel` overrides `GetTargetNotableCountForSettlement(Settlement, Occupation)`. Maps the sealed TaleWorlds `Occupation` to TAOM-owned `NotableOccupationKind` at the boundary (ADR-007). Keyed on `settlement.Culture` (settlement identity, NOT `OwnerClan.Culture` — an Isengard town stays Isengard-flavored even when conquered).
C:\Users\mikew\source\repos\TAOM\docs\features\cultural-feats.md:223: | `Main/Features/CulturalFeats/TerrainKind.cs` | TAOM-owned terrain enum (boundary type for the speed model → service, ADR-007) |
C:\Users\mikew\source\repos\TAOM\docs\features\culture-marketplace.md:94: | `Main/Adapters/ITownRosterAdapter.cs` + `TownRosterAdapter.cs` | Wraps `Settlement.OwnerClan.Culture` + `Settlement.ItemRoster` operations per ADR-007. Exposes `AddItem`, `GetItemCount`, `RemoveItem` (via `AddToCounts(-N)`), `EnumerateRoster` (returning `RosterItemSnapshot` DTOs that keep `ItemObject` out of the service layer). |
C:\Users\mikew\source\repos\TAOM\docs\features\diplomacy.md:16: 1. **Permanent alliances** — `AllianceCampaignBehavior.EndAlliance` and `DeclareWarAction.ApplyInternal` are sealed TaleWorlds methods. They cannot be overridden; Harmony Prefix patches are needed to block execution when the affected kingdoms are permanently allied.
C:\Users\mikew\source\repos\TAOM\docs\features\diplomacy.md:145: - `IAllianceAdapter` — wraps `AllianceCampaignBehavior`, `StanceLink`, `Kingdom` (sealed TaleWorlds types); provides `AreAllied`, `StartAlliance`, `AreAtWar`, `DeclareWar`, `MakePeace`, `GetAllKingdomIds`
C:\Users\mikew\source\repos\TAOM\docs\features\editor-cache-rebuild.md:91: | `Main/Adapters/ICampaignSessionAdapter.cs` + `CampaignSessionAdapter.cs` + `CampaignSnapshot.cs` | Adapter wrapping `Campaign.Current` checks, `SandBoxNavigationCache` construction, and diagnostic campaign-state snapshot. Keeps the service ADR-007 compliant (no TaleWorlds types in service body). |
C:\Users\mikew\source\repos\TAOM\docs\features\elephant.md:191: | [`Main/Features/Elephant/IElephantAttackService.cs`](../../Main/Features/Elephant/IElephantAttackService.cs) + [`ElephantAttackService.cs`](../../Main/Features/Elephant/ElephantAttackService.cs) | **Pure** logic (no TaleWorlds deps): `IsElephantMonster`, `ShouldEngage(facingDot, alreadyAttacking)` (facing gate; the BT scan passes -1 when no enemy in range), `IsOffCooldown(lastFired, now, seconds)` (inclusive ≥; future stamps read as ON cooldown), `ComputeInflictedDamage(kind, blocking, roll)` = `round((min + roll·(max−min)) · (blocking?0.25:1))` with the band chosen by `ElephantAttackKind` (Trample 50-100, SideAttack/tusk 50-75; roll is a [0,1] `MBRandom.RandomFloat` supplied per victim by the BT, clamped + NaN-guarded). Unit-tested. |
C:\Users\mikew\source\repos\TAOM\docs\features\equip-presets.md:23: 2. **Adapter boundary (ADR-007).** Services never see sealed TaleWorlds types. The service layer only sees `string`-based StringIds (for `ItemObject` and `ItemModifier`); the full `EquipmentElement` lives inside the `EquipmentSlotAdapter`.
C:\Users\mikew\source\repos\TAOM\docs\features\execution.md:121: No `Adapter` interfaces — the feature operates entirely on kingdom `StringId` strings extracted at the patch entry point. The sealed types (`Hero`, `Clan`, `Kingdom`) are touched only in the patches and in the `TaomExecutionRelationModel` override body, both of which are entry-point classes per ADR-002 / ADR-007.
C:\Users\mikew\source\repos\TAOM\docs\features\hero-race.md:13: `CharacterTableau` and `CharacterSpawner` are sealed TaleWorlds classes. Their `InitializeAgentVisuals` and `InitWithCharacter` methods are private, and their internal fields (`_agentVisuals`, `_agentEntity`, `_race`, etc.) are private. The methods must be fully reimplemented when a non-human race is involved.
C:\Users\mikew\source\repos\TAOM\docs\features\quick-actions.md:115: 4. Implement the service method (returns `QuickActionResult`). Iterate `_inventory.GetRightPaneItems()`, apply filters via `IInventoryItemAdapter` properties (do NOT cast `UnderlyingVm` back to `SPItemVM` — keeps ADR-007 intact).
C:\Users\mikew\source\repos\TAOM\docs\features\scene-scripts.md:40: The entry point's body is purely orchestration + validation + lifecycle handling. All algorithmic logic is in the helpers, which take simple value types (`IReadOnlyList<StepKey>`, `Vec3`, `Vec2`, primitive floats) and have no engine-side dependencies beyond `TaleWorlds.Library` value structs.
C:\Users\mikew\source\repos\TAOM\docs\features\settlement-food.md:31: Thin GameModel → pure service → primitive snapshot (ADR-002 / ADR-007), mirroring
C:\Users\mikew\source\repos\TAOM\docs\features\settlement-food.md:38: - **`SettlementFoodService.ComputeFoodDelta`** — pure (no TaleWorlds types): garrison raw-count
C:\Users\mikew\source\repos\TAOM\docs\features\siege-dismount.md:46: `IMountSnapshot` is an opaque token the service stores between mission start and mission end. The service never sees `EquipmentElement` or `ItemObject` (ADR-007).
C:\Users\mikew\source\repos\TAOM\docs\features\siege.md:14: `BesiegerCamp` is a sealed TaleWorlds type. The `GetSiegeCampPartyPosition` method cannot be overridden. The only safe intercept point that allows both short-circuit (return false to skip the original) and result injection (`ref __result`) is a Harmony Prefix.
C:\Users\mikew\source\repos\TAOM\docs\features\smart-cavalry-ai.md:21: 3. **ADR-007 — no sealed types in services.** The service tracks an "original target formation" across the Rerouting branch. We can't store a `Formation` reference in the service; we use opaque `object` tokens that only the command adapter unwraps.
C:\Users\mikew\source\repos\TAOM\docs\features\spider.md:150: | `SpiderAttackService` | pure (TaleWorlds-free): `SelectActionName`/`SelectBones` (pounce=front/charge by speed; side=left/right by bearing), `IsOffCooldown`, warg-pattern rider damage attribution, `IsSpiderMonster()`; `SpiderAttack` fires the bone-collision `CustomAttack` + the `[Spider][diag] ATTACK fire` log |

exec
"C:\\Program Files\\PowerShell\\7\\pwsh.exe" -Command '$path='"'E:\\Decompiled_Bannerlord\\Campaign\\TaleWorlds.CampaignSystem\\TaleWorlds.CampaignSystem\\ExplainedNumber.cs'; "'$i=0; Get-Content $path | ForEach-Object { $i++; if($i -le 90){ '"'{0}:{1}: {2}' -f "'$path,$i,$_ } }' in C:\Users\mikew\source\repos\TAOM
 succeeded in 456ms:
E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\TaleWorlds.CampaignSystem\ExplainedNumber.cs:1: using System.Collections.Generic;
E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\TaleWorlds.CampaignSystem\ExplainedNumber.cs:2: using TaleWorlds.Library;
E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\TaleWorlds.CampaignSystem\ExplainedNumber.cs:3: using TaleWorlds.Localization;
E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\TaleWorlds.CampaignSystem\ExplainedNumber.cs:4: 
E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\TaleWorlds.CampaignSystem\ExplainedNumber.cs:5: namespace TaleWorlds.CampaignSystem;
E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\TaleWorlds.CampaignSystem\ExplainedNumber.cs:6: 
E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\TaleWorlds.CampaignSystem\ExplainedNumber.cs:7: public struct ExplainedNumber
E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\TaleWorlds.CampaignSystem\ExplainedNumber.cs:8: {
E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\TaleWorlds.CampaignSystem\ExplainedNumber.cs:9: 	private class StatExplainer
E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\TaleWorlds.CampaignSystem\ExplainedNumber.cs:10: 	{
E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\TaleWorlds.CampaignSystem\ExplainedNumber.cs:11: 		public enum OperationType
E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\TaleWorlds.CampaignSystem\ExplainedNumber.cs:12: 		{
E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\TaleWorlds.CampaignSystem\ExplainedNumber.cs:13: 			Base,
E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\TaleWorlds.CampaignSystem\ExplainedNumber.cs:14: 			Add,
E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\TaleWorlds.CampaignSystem\ExplainedNumber.cs:15: 			Multiply,
E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\TaleWorlds.CampaignSystem\ExplainedNumber.cs:16: 			LimitMin,
E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\TaleWorlds.CampaignSystem\ExplainedNumber.cs:17: 			LimitMax
E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\TaleWorlds.CampaignSystem\ExplainedNumber.cs:18: 		}
E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\TaleWorlds.CampaignSystem\ExplainedNumber.cs:19: 
E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\TaleWorlds.CampaignSystem\ExplainedNumber.cs:20: 		public readonly struct ExplanationLine(string name, float number, OperationType operationType)
E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\TaleWorlds.CampaignSystem\ExplainedNumber.cs:21: 		{
E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\TaleWorlds.CampaignSystem\ExplainedNumber.cs:22: 			public readonly float Number = number;
E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\TaleWorlds.CampaignSystem\ExplainedNumber.cs:23: 
E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\TaleWorlds.CampaignSystem\ExplainedNumber.cs:24: 			public readonly string Name = name;
E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\TaleWorlds.CampaignSystem\ExplainedNumber.cs:25: 
E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\TaleWorlds.CampaignSystem\ExplainedNumber.cs:26: 			public readonly OperationType OperationType = operationType;
E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\TaleWorlds.CampaignSystem\ExplainedNumber.cs:27: 		}
E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\TaleWorlds.CampaignSystem\ExplainedNumber.cs:28: 
E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\TaleWorlds.CampaignSystem\ExplainedNumber.cs:29: 		public List<ExplanationLine> Lines { get; private set; } = new List<ExplanationLine>();
E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\TaleWorlds.CampaignSystem\ExplainedNumber.cs:30: 
E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\TaleWorlds.CampaignSystem\ExplainedNumber.cs:31: 		public ExplanationLine? BaseLine { get; private set; }
E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\TaleWorlds.CampaignSystem\ExplainedNumber.cs:32: 
E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\TaleWorlds.CampaignSystem\ExplainedNumber.cs:33: 		public ExplanationLine? LimitMinLine { get; private set; }
E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\TaleWorlds.CampaignSystem\ExplainedNumber.cs:34: 
E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\TaleWorlds.CampaignSystem\ExplainedNumber.cs:35: 		public ExplanationLine? LimitMaxLine { get; private set; }
E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\TaleWorlds.CampaignSystem\ExplainedNumber.cs:36: 
E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\TaleWorlds.CampaignSystem\ExplainedNumber.cs:37: 		public List<(string name, float number)> GetLines(float baseNumber, float unclampedResultNumber, TextObject overrideBaseLineText = null, TextObject overrideMaximumLineText = null, TextObject overrideMinimumLineText = null)
E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\TaleWorlds.CampaignSystem\ExplainedNumber.cs:38: 		{
E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\TaleWorlds.CampaignSystem\ExplainedNumber.cs:39: 			List<(string, float)> list = new List<(string, float)>();
E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\TaleWorlds.CampaignSystem\ExplainedNumber.cs:40: 			if (BaseLine.HasValue)
E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\TaleWorlds.CampaignSystem\ExplainedNumber.cs:41: 			{
E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\TaleWorlds.CampaignSystem\ExplainedNumber.cs:42: 				list.Add(((overrideBaseLineText != null) ? overrideBaseLineText.ToString() : BaseLine.Value.Name, BaseLine.Value.Number));
E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\TaleWorlds.CampaignSystem\ExplainedNumber.cs:43: 			}
E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\TaleWorlds.CampaignSystem\ExplainedNumber.cs:44: 			foreach (ExplanationLine line in Lines)
E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\TaleWorlds.CampaignSystem\ExplainedNumber.cs:45: 			{
E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\TaleWorlds.CampaignSystem\ExplainedNumber.cs:46: 				float num = line.Number;
E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\TaleWorlds.CampaignSystem\ExplainedNumber.cs:47: 				if (line.OperationType == OperationType.Multiply)
E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\TaleWorlds.CampaignSystem\ExplainedNumber.cs:48: 				{
E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\TaleWorlds.CampaignSystem\ExplainedNumber.cs:49: 					num = baseNumber * num * 0.01f;
E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\TaleWorlds.CampaignSystem\ExplainedNumber.cs:50: 				}
E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\TaleWorlds.CampaignSystem\ExplainedNumber.cs:51: 				list.Add((line.Name, num));
E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\TaleWorlds.CampaignSystem\ExplainedNumber.cs:52: 			}
E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\TaleWorlds.CampaignSystem\ExplainedNumber.cs:53: 			if (LimitMinLine.HasValue && LimitMinLine.Value.Number > unclampedResultNumber)
E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\TaleWorlds.CampaignSystem\ExplainedNumber.cs:54: 			{
E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\TaleWorlds.CampaignSystem\ExplainedNumber.cs:55: 				list.Add(((overrideMinimumLineText != null) ? overrideMinimumLineText.ToString() : LimitMinLine.Value.Name, LimitMinLine.Value.Number - unclampedResultNumber));
E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\TaleWorlds.CampaignSystem\ExplainedNumber.cs:56: 			}
E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\TaleWorlds.CampaignSystem\ExplainedNumber.cs:57: 			if (LimitMaxLine.HasValue && LimitMaxLine.Value.Number < unclampedResultNumber)
E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\TaleWorlds.CampaignSystem\ExplainedNumber.cs:58: 			{
E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\TaleWorlds.CampaignSystem\ExplainedNumber.cs:59: 				list.Add(((overrideMaximumLineText != null) ? overrideMaximumLineText.ToString() : LimitMaxLine.Value.Name, LimitMaxLine.Value.Number - unclampedResultNumber));
E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\TaleWorlds.CampaignSystem\ExplainedNumber.cs:60: 			}
E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\TaleWorlds.CampaignSystem\ExplainedNumber.cs:61: 			return list;
E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\TaleWorlds.CampaignSystem\ExplainedNumber.cs:62: 		}
E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\TaleWorlds.CampaignSystem\ExplainedNumber.cs:63: 
E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\TaleWorlds.CampaignSystem\ExplainedNumber.cs:64: 		public void AddLine(string name, float number, OperationType opType)
E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\TaleWorlds.CampaignSystem\ExplainedNumber.cs:65: 		{
E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\TaleWorlds.CampaignSystem\ExplainedNumber.cs:66: 			ExplanationLine explanationLine = new ExplanationLine(name, number, opType);
E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\TaleWorlds.CampaignSystem\ExplainedNumber.cs:67: 			switch (opType)
E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\TaleWorlds.CampaignSystem\ExplainedNumber.cs:68: 			{
E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\TaleWorlds.CampaignSystem\ExplainedNumber.cs:69: 			case OperationType.Add:
E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\TaleWorlds.CampaignSystem\ExplainedNumber.cs:70: 			case OperationType.Multiply:
E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\TaleWorlds.CampaignSystem\ExplainedNumber.cs:71: 			{
E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\TaleWorlds.CampaignSystem\ExplainedNumber.cs:72: 				int num = -1;
E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\TaleWorlds.CampaignSystem\ExplainedNumber.cs:73: 				for (int i = 0; i < Lines.Count; i++)
E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\TaleWorlds.CampaignSystem\ExplainedNumber.cs:74: 				{
E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\TaleWorlds.CampaignSystem\ExplainedNumber.cs:75: 					if (Lines[i].Name.Equals(name) && Lines[i].OperationType == opType)
E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\TaleWorlds.CampaignSystem\ExplainedNumber.cs:76: 					{
E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\TaleWorlds.CampaignSystem\ExplainedNumber.cs:77: 						num = i;
E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\TaleWorlds.CampaignSystem\ExplainedNumber.cs:78: 						break;
E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\TaleWorlds.CampaignSystem\ExplainedNumber.cs:79: 					}
E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\TaleWorlds.CampaignSystem\ExplainedNumber.cs:80: 				}
E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\TaleWorlds.CampaignSystem\ExplainedNumber.cs:81: 				if (num < 0)
E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\TaleWorlds.CampaignSystem\ExplainedNumber.cs:82: 				{
E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\TaleWorlds.CampaignSystem\ExplainedNumber.cs:83: 					Lines.Add(explanationLine);
E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\TaleWorlds.CampaignSystem\ExplainedNumber.cs:84: 					break;
E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\TaleWorlds.CampaignSystem\ExplainedNumber.cs:85: 				}
E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\TaleWorlds.CampaignSystem\ExplainedNumber.cs:86: 				explanationLine = new ExplanationLine(name, number + Lines[num].Number, opType);
E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\TaleWorlds.CampaignSystem\ExplainedNumber.cs:87: 				Lines[num] = explanationLine;
E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\TaleWorlds.CampaignSystem\ExplainedNumber.cs:88: 				break;
E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\TaleWorlds.CampaignSystem\ExplainedNumber.cs:89: 			}
E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\TaleWorlds.CampaignSystem\ExplainedNumber.cs:90: 			case OperationType.Base:

codex
A deeper arithmetic risk is whether vanilla building effects modify the base/village production before the TAOM delta is added. I’m checking `Town.AddEffectOfBuildings` because if it applies factors instead of flat additions, a post-base delta may intentionally avoid or accidentally bypass those multipliers.
exec
"C:\\Program Files\\PowerShell\\7\\pwsh.exe" -Command "Select-String -Path 'E:\\Decompiled_Bannerlord\\Campaign\\TaleWorlds.CampaignSystem\\TaleWorlds.CampaignSystem.Settlements\\Town.cs' -Pattern 'void AddEffectOfBuildings|AddEffectOfBuildings\\(' -Context 0,60 | ForEach-Object { '{0}:{1}: {2}' -f "'$_.Path,$_.LineNumber,$_.Line; $_.Context.PostContext }' in C:\Users\mikew\source\repos\TAOM
 succeeded in 466ms:
E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\TaleWorlds.CampaignSystem.Settlements\Town.cs:467: 		AddEffectOfBuildings(BuildingEffectEnum.FoodStock, ref result);
		return (int)result.ResultNumber;
	}

	public Town()
	{
		Buildings = new MBList<Building>();
		BuildingsInProgress = new Queue<Building>();
		Workshops = new Workshop[0];
		_marketData = new TownMarketData(this);
		_tradeBoundVillagesCache = new MBList<Village>();
	}

	public override void OnInit()
	{
		Loyalty = base.Owner.RandomIntWithSeed(1337u, 30, 70);
		Security = base.Owner.RandomIntWithSeed(1001u, 40, 60);
		TradeTaxAccumulated = (IsTown ? (1000 + MBRandom.RandomInt(1000)) : 0);
		ChangeGold(20000);
	}

	public override void OnSessionStart()
	{
		Campaign.Current.MapSceneWrapper.GetSiegeCampFrames(base.Settlement, out var siegeCamp1GlobalFrames, out var siegeCamp2GlobalFrames);
		BesiegerCampPositions1 = siegeCamp1GlobalFrames.ToArray();
		BesiegerCampPositions2 = siegeCamp2GlobalFrames.ToArray();
	}

	public void InitializeWorkshops(int count)
	{
		if (count > 0)
		{
			Workshops = new Workshop[count];
			for (int i = 0; i < count; i++)
			{
				Workshops[i] = new Workshop(base.Owner.Settlement, "workshop_" + i);
			}
		}
	}

	[LoadInitializationCallback]
	private void OnLoad()
	{
		_tradeBoundVillagesCache = new MBList<Village>();
	}

	protected override void PreAfterLoad()
	{
		_ownerClan?.OnFortificationAdded(this);
	}

	protected override void AfterLoad()
	{
		Workshop[] workshops = Workshops;
		for (int i = 0; i < workshops.Length; i++)
		{
			workshops[i].AfterLoad();
		}
		bool flag = false;
		for (int num = Buildings.Count - 1; num >= 0; num--)
		{
E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\TaleWorlds.CampaignSystem.Settlements\Town.cs:573: 	public void AddEffectOfBuildings(BuildingEffectEnum buildingEffect, ref ExplainedNumber result)
	{
		foreach (Building building in Buildings)
		{
			building.AddEffectOfBuilding(buildingEffect, ref result);
		}
	}

	private void SetNewOwnerClan()
	{
		_ownerClan.OnFortificationAdded(this);
		foreach (Village boundVillage in base.Settlement.BoundVillages)
		{
			boundVillage.Settlement.Party.SetVisualAsDirty();
			boundVillage.VillagerPartyComponent?.MobileParty.Party.SetVisualAsDirty();
		}
	}

	private void RemoveOwnerClan()
	{
		_ownerClan.OnFortificationRemoved(this);
	}

	internal void DailyTick()
	{
		Loyalty += LoyaltyChange;
		Security += SecurityChange;
		if (base.FoodStocks > 0f)
		{
			base.Owner.OnConsumedFood();
		}
		base.FoodStocks += FoodChange;
		if (base.FoodStocks < 0f)
		{
			base.FoodStocks = 0f;
			base.Owner.RemainingFoodPercentage = -100;
		}
		else
		{
			base.Owner.RemainingFoodPercentage = 0;
		}
		if (base.FoodStocks > (float)FoodStocksUpperLimit())
		{
			base.FoodStocks = FoodStocksUpperLimit();
		}
		if (Governor != null)
		{
			if (Governor.GetPerkValue(DefaultPerks.Roguery.WhiteLies) && MBRandom.RandomFloat <= DefaultPerks.Roguery.WhiteLies.SecondaryBonus)
			{
				Hero randomElement = base.Settlement.Notables.GetRandomElement();
				if (randomElement != null)
				{
					ChangeRelationAction.ApplyRelationChangeBetweenHeroes(Governor, randomElement, 1);
				}
			}
			if (Governor.GetPerkValue(DefaultPerks.Roguery.Scarface) && MBRandom.RandomFloat <= DefaultPerks.Roguery.Scarface.SecondaryBonus)
			{
				Hero randomElementWithPredicate = base.Settlement.Notables.GetRandomElementWithPredicate((Hero x) => x.IsGangLeader);
				if (randomElementWithPredicate != null)
				{
					ChangeRelationAction.ApplyRelationChangeBetweenHeroes(Governor, randomElementWithPredicate, 1);

exec
"C:\\Program Files\\PowerShell\\7\\pwsh.exe" -Command "Get-ChildItem -Path 'E:\\Decompiled_Bannerlord\\Campaign' -Recurse -Filter 'Building.cs' -ErrorAction SilentlyContinue | Select-Object -First 20 | ForEach-Object { "'$_.FullName }' in C:\Users\mikew\source\repos\TAOM
exec
"C:\\Program Files\\PowerShell\\7\\pwsh.exe" -Command "Get-ChildItem -Path 'E:\\Decompiled_Bannerlord\\Campaign' -Recurse -Filter 'BuildingType.cs' -ErrorAction SilentlyContinue | Select-Object -First 20 | ForEach-Object { "'$_.FullName }' in C:\Users\mikew\source\repos\TAOM
exec
"C:\\Program Files\\PowerShell\\7\\pwsh.exe" -Command "Get-ChildItem -Path 'E:\\Decompiled_Bannerlord\\Campaign' -Recurse -Filter '*BuildingEffect*' -ErrorAction SilentlyContinue | Select-Object -First 50 | ForEach-Object { "'$_.FullName }' in C:\Users\mikew\source\repos\TAOM
 succeeded in 468ms:
E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\TaleWorlds.CampaignSystem.Settlements.Buildings\Building.cs

 succeeded in 483ms:
E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\TaleWorlds.CampaignSystem.Settlements.Buildings\BuildingType.cs

 succeeded in 442ms:
E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\TaleWorlds.CampaignSystem.ComponentInterfaces\BuildingEffectModel.cs
E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\TaleWorlds.CampaignSystem.GameComponents\DefaultBuildingEffectModel.cs
E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\TaleWorlds.CampaignSystem.Settlements.Buildings\BuildingEffectEnum.cs
E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\TaleWorlds.CampaignSystem.Settlements.Buildings\BuildingEffectIncrementType.cs

exec
"C:\\Program Files\\PowerShell\\7\\pwsh.exe" -Command "Select-String -Path 'E:\\Decompiled_Bannerlord\\Campaign\\TaleWorlds.CampaignSystem\\TaleWorlds.CampaignSystem.Settlements.Buildings\\Building.cs' -Pattern 'AddEffectOfBuilding|BuildingEffectEnum|AddFactor|Add\\(' -Context 0,80 | ForEach-Object { '{0}:{1}: {2}' -f "'$_.Path,$_.LineNumber,$_.Line; $_.Context.PostContext }' in C:\Users\mikew\source\repos\TAOM
exec
"C:\\Program Files\\PowerShell\\7\\pwsh.exe" -Command '$path='"'E:\\Decompiled_Bannerlord\\Campaign\\TaleWorlds.CampaignSystem\\TaleWorlds.CampaignSystem.GameComponents\\DefaultBuildingEffectModel.cs'; Select-String -Path "'$path -Pattern '"'FoodProduction|FoodConsumption|FoodStock|BuildingEffectEnum|CalculateEffect' -Context 3,10 | Select-Object -First 120 | ForEach-Object { "'$_.Context.PreContext; '"'{0}:{1}: {2}' -f "'$_.Path,$_.LineNumber,$_.Line; $_.Context.PostContext }' in C:\Users\mikew\source\repos\TAOM
exec
"C:\\Program Files\\PowerShell\\7\\pwsh.exe" -Command '$path='"'E:\\Decompiled_Bannerlord\\Campaign\\TaleWorlds.CampaignSystem\\TaleWorlds.CampaignSystem.Settlements.Buildings\\BuildingEffectIncrementType.cs'; "'$i=0; Get-Content $path | ForEach-Object { $i++; '"'{0}:{1}: {2}' -f "'$path,$i,$_ }' in C:\Users\mikew\source\repos\TAOM
exec
"C:\\Program Files\\PowerShell\\7\\pwsh.exe" -Command '$path='"'E:\\Decompiled_Bannerlord\\Campaign\\TaleWorlds.CampaignSystem\\TaleWorlds.CampaignSystem.Settlements.Buildings\\BuildingType.cs'; Select-String -Path "'$path -Pattern '"'BuildingEffect|IncrementType|FoodProduction|BuildingEffectEnum' -Context 2,8 | Select-Object -First 120 | ForEach-Object { "'$_.Context.PreContext; '"'{0}:{1}: {2}' -f "'$_.Path,$_.LineNumber,$_.Line; $_.Context.PostContext }' in C:\Users\mikew\source\repos\TAOM
 succeeded in 438ms:

public class DefaultBuildingEffectModel : BuildingEffectModel
{
E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\TaleWorlds.CampaignSystem.GameComponents\DefaultBuildingEffectModel.cs:11: 	public override ExplainedNumber GetBuildingEffect(Building building, BuildingEffectEnum effect)
	{
		float baseBuildingEffectAmount = building.BuildingType.GetBaseBuildingEffectAmount(effect, building.CurrentLevel);
		ExplainedNumber bonuses = new ExplainedNumber(baseBuildingEffectAmount);
		if (effect == BuildingEffectEnum.DenarByBoundVillageHeartPerDay)
		{
			float num = 0f;
			foreach (Village village in building.Town.Villages)
			{
				num += village.Hearth;
			}
	{
		float baseBuildingEffectAmount = building.BuildingType.GetBaseBuildingEffectAmount(effect, building.CurrentLevel);
		ExplainedNumber bonuses = new ExplainedNumber(baseBuildingEffectAmount);
E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\TaleWorlds.CampaignSystem.GameComponents\DefaultBuildingEffectModel.cs:15: 		if (effect == BuildingEffectEnum.DenarByBoundVillageHeartPerDay)
		{
			float num = 0f;
			foreach (Village village in building.Town.Villages)
			{
				num += village.Hearth;
			}
			bonuses = new ExplainedNumber(num * baseBuildingEffectAmount);
		}
		if (effect == BuildingEffectEnum.FoodStock && (building.BuildingType == DefaultBuildingTypes.CastleGranary || building.BuildingType == DefaultBuildingTypes.SettlementWarehouse))
		{
			}
			bonuses = new ExplainedNumber(num * baseBuildingEffectAmount);
		}
E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\TaleWorlds.CampaignSystem.GameComponents\DefaultBuildingEffectModel.cs:24: 		if (effect == BuildingEffectEnum.FoodStock && (building.BuildingType == DefaultBuildingTypes.CastleGranary || building.BuildingType == DefaultBuildingTypes.SettlementWarehouse))
		{
			PerkHelper.AddPerkBonusForTown(DefaultPerks.Engineering.Battlements, building.Town, ref bonuses);
		}
		PerkHelper.AddPerkBonusForTown(DefaultPerks.Steward.Contractors, building.Town, ref bonuses);
		if (building.BuildingType.IsDailyProject)
		{
			PerkHelper.AddPerkBonusForTown(DefaultPerks.Steward.MasterOfPlanning, building.Town, ref bonuses);
		}
		if (building.BuildingType == DefaultBuildingTypes.SettlementMarketplace || building.BuildingType == DefaultBuildingTypes.SettlementDailyFestivalAndGames)
		{

 succeeded in 496ms:
E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\TaleWorlds.CampaignSystem.Settlements.Buildings\Building.cs:61: 		collectedObjects.Add(_buildingType);
		collectedObjects.Add(Town);
	}

	internal static object AutoGeneratedGetMemberValueTown(object o)
	{
		return ((Building)o).Town;
	}

	internal static object AutoGeneratedGetMemberValueBuildingProgress(object o)
	{
		return ((Building)o).BuildingProgress;
	}

	internal static object AutoGeneratedGetMemberValueIsCurrentlyDefault(object o)
	{
		return ((Building)o).IsCurrentlyDefault;
	}

	internal static object AutoGeneratedGetMemberValue_buildingType(object o)
	{
		return ((Building)o)._buildingType;
	}

	internal static object AutoGeneratedGetMemberValue_currentLevel(object o)
	{
		return ((Building)o)._currentLevel;
	}

	internal static object AutoGeneratedGetMemberValue_hitpoints(object o)
	{
		return ((Building)o)._hitpoints;
	}

	public Building(BuildingType buildingType, Town town, float buildingProgress = 0f, int currentLevel = 0)
	{
		_buildingType = buildingType;
		BuildingProgress = buildingProgress;
		Town = town;
		_currentLevel = currentLevel;
		IsCurrentlyDefault = false;
		_ = buildingType.IsDailyProject;
	}

	[LateLoadInitializationCallback]
	private void OnLoad()
	{
		UpdateBuildingTypeForOldSaves();
	}

	public override int GetHashCode()
	{
		int num = _buildingType?.GetHashCode() ?? 0;
		return (Town != null) ? ((num * 397) ^ Town.GetHashCode()) : num;
	}

	public int GetConstructionCost()
	{
		float num = 1f;
		if (Town.Settlement.IsCastle && Town.Settlement.OwnerClan.Kingdom != null && Town.Settlement.OwnerClan.Kingdom.ActivePolicies.Contains(DefaultPolicies.CastleCharters))
		{
			num = 0.8f;
		}
		return (int)((float)_buildingType.GetProductionCost(_currentLevel) * num);
	}

	public void LevelUp()
	{
		if (CurrentLevel < 3)
		{
			int constructionCost = GetConstructionCost();
			CurrentLevel++;
			BuildingProgress -= constructionCost;
			CampaignEventDispatcher.Instance.OnBuildingLevelChanged(Town, this, 1);
		}
	}

	public void LevelDown()
	{
		if (CurrentLevel != _buildingType.StartLevel)
		{
E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\TaleWorlds.CampaignSystem.Settlements.Buildings\Building.cs:62: 		collectedObjects.Add(Town);
	}

	internal static object AutoGeneratedGetMemberValueTown(object o)
	{
		return ((Building)o).Town;
	}

	internal static object AutoGeneratedGetMemberValueBuildingProgress(object o)
	{
		return ((Building)o).BuildingProgress;
	}

	internal static object AutoGeneratedGetMemberValueIsCurrentlyDefault(object o)
	{
		return ((Building)o).IsCurrentlyDefault;
	}

	internal static object AutoGeneratedGetMemberValue_buildingType(object o)
	{
		return ((Building)o)._buildingType;
	}

	internal static object AutoGeneratedGetMemberValue_currentLevel(object o)
	{
		return ((Building)o)._currentLevel;
	}

	internal static object AutoGeneratedGetMemberValue_hitpoints(object o)
	{
		return ((Building)o)._hitpoints;
	}

	public Building(BuildingType buildingType, Town town, float buildingProgress = 0f, int currentLevel = 0)
	{
		_buildingType = buildingType;
		BuildingProgress = buildingProgress;
		Town = town;
		_currentLevel = currentLevel;
		IsCurrentlyDefault = false;
		_ = buildingType.IsDailyProject;
	}

	[LateLoadInitializationCallback]
	private void OnLoad()
	{
		UpdateBuildingTypeForOldSaves();
	}

	public override int GetHashCode()
	{
		int num = _buildingType?.GetHashCode() ?? 0;
		return (Town != null) ? ((num * 397) ^ Town.GetHashCode()) : num;
	}

	public int GetConstructionCost()
	{
		float num = 1f;
		if (Town.Settlement.IsCastle && Town.Settlement.OwnerClan.Kingdom != null && Town.Settlement.OwnerClan.Kingdom.ActivePolicies.Contains(DefaultPolicies.CastleCharters))
		{
			num = 0.8f;
		}
		return (int)((float)_buildingType.GetProductionCost(_currentLevel) * num);
	}

	public void LevelUp()
	{
		if (CurrentLevel < 3)
		{
			int constructionCost = GetConstructionCost();
			CurrentLevel++;
			BuildingProgress -= constructionCost;
			CampaignEventDispatcher.Instance.OnBuildingLevelChanged(Town, this, 1);
		}
	}

	public void LevelDown()
	{
		if (CurrentLevel != _buildingType.StartLevel)
		{
			CurrentLevel--;
E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\TaleWorlds.CampaignSystem.Settlements.Buildings\Building.cs:161: 	public void AddEffectOfBuilding(BuildingEffectEnum buildingEffect, ref ExplainedNumber result)
	{
		if (_currentLevel < _buildingType.StartLevel || _currentLevel > 3)
		{
			Debug.FailedAssert(string.Concat("Building: ", Name, " current level is out of bounds!"), "C:\\BuildAgent\\work\\mb3\\Source\\Bannerlord\\TaleWorlds.CampaignSystem\\Settlements\\Buildings\\Building.cs", "AddEffectOfBuilding", 142);
		}
		if (_currentLevel != 0 && (!BuildingType.IsDailyProject || Town.CurrentDefaultBuilding == this) && BuildingType.HasEffect(buildingEffect))
		{
			BuildingEffectIncrementType buildingEffectType = BuildingType.GetBuildingEffectType(buildingEffect);
			float resultNumber = Campaign.Current.Models.BuildingEffectModel.GetBuildingEffect(this, buildingEffect).ResultNumber;
			switch (buildingEffectType)
			{
			case BuildingEffectIncrementType.Add:
				result.Add(resultNumber, Name);
				break;
			case BuildingEffectIncrementType.AddFactor:
				result.AddFactor(resultNumber, Name);
				break;
			default:
				Debug.FailedAssert("Unsupported BuildingEffectIncrementType!", "C:\\BuildAgent\\work\\mb3\\Source\\Bannerlord\\TaleWorlds.CampaignSystem\\Settlements\\Buildings\\Building.cs", "AddEffectOfBuilding", 172);
				break;
			}
		}
	}

	public TextObject GetBonusExplanation()
	{
		if (_currentLevel == 0)
		{
			return TextObject.GetEmpty();
		}
		return GetBonusExplanations()[_currentLevel - 1];
	}

	private TextObject[] GetBonusExplanations()
	{
		TextObject[] array = new TextObject[3]
		{
			TextObject.GetEmpty(),
			TextObject.GetEmpty(),
			TextObject.GetEmpty()
		};
		if (_currentLevel == 0 || _currentLevel > 3)
		{
			return array;
		}
		for (int i = 0; i < _currentLevel; i++)
		{
			array[i] = _buildingType.GetExplanationAtLevel(i);
		}
		return array;
	}

	private void UpdateBuildingTypeForOldSaves()
	{
		if (MBSaveLoad.LastLoadedGameVersion.IsOlderThan(ApplicationVersion.FromString("v1.3.0.0")) && new Dictionary<string, string>
		{
			{ "building_fortifications", "building_settlement_fortifications" },
			{ "building_settlement_garrison_barracks", "building_settlement_barracks" },
			{ "building_settlement_militia_barracks", "building_settlement_guard_house" },
			{ "building_siege_workshop", "building_settlement_siege_workshop" },
			{ "building_settlement_marketplace", "building_settlement_tax_office" },
			{ "building_settlement_forum", "building_settlement_marketplace" },
			{ "building_settlement_granary", "building_settlement_warehouse" },
			{ "building_settlement_workshop", "building_settlement_mason" },
			{ "building_settlement_aquaducts", "building_settlement_waterworks" },
			{ "building_settlement_fairgrounds", "building_settlement_courthouse" },
			{ "building_settlement_lime_kilns", "building_settlement_roads_and_paths" },
			{ "building_wall", "building_castle_fortifications" },
			{ "building_castle_gardens", "building_castle_farmlands" },
			{ "building_castle_workshops", "building_castle_mason" },
			{ "building_castle_fairgrounds", "building_castle_roads_and_paths" },
			{ "building_castle_militia_barracks", "building_castle_guard_house" },
			{ "building_castle_lime_kilns", "building_castle_craftmans_quarters" },
			{
				"building_daily_build_house",
				Town.IsCastle ? "building_castle_daily_slacken_garrison" : "building_settlement_daily_housing"
			},
			{
				"building_daily_train_militia",
				Town.IsCastle ? "building_castle_daily_raise_troops" : "building_settlement_daily_train_militia"
E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\TaleWorlds.CampaignSystem.Settlements.Buildings\Building.cs:165: 			Debug.FailedAssert(string.Concat("Building: ", Name, " current level is out of bounds!"), "C:\\BuildAgent\\work\\mb3\\Source\\Bannerlord\\TaleWorlds.CampaignSystem\\Settlements\\Buildings\\Building.cs", "AddEffectOfBuilding", 142);
		}
		if (_currentLevel != 0 && (!BuildingType.IsDailyProject || Town.CurrentDefaultBuilding == this) && BuildingType.HasEffect(buildingEffect))
		{
			BuildingEffectIncrementType buildingEffectType = BuildingType.GetBuildingEffectType(buildingEffect);
			float resultNumber = Campaign.Current.Models.BuildingEffectModel.GetBuildingEffect(this, buildingEffect).ResultNumber;
			switch (buildingEffectType)
			{
			case BuildingEffectIncrementType.Add:
				result.Add(resultNumber, Name);
				break;
			case BuildingEffectIncrementType.AddFactor:
				result.AddFactor(resultNumber, Name);
				break;
			default:
				Debug.FailedAssert("Unsupported BuildingEffectIncrementType!", "C:\\BuildAgent\\work\\mb3\\Source\\Bannerlord\\TaleWorlds.CampaignSystem\\Settlements\\Buildings\\Building.cs", "AddEffectOfBuilding", 172);
				break;
			}
		}
	}

	public TextObject GetBonusExplanation()
	{
		if (_currentLevel == 0)
		{
			return TextObject.GetEmpty();
		}
		return GetBonusExplanations()[_currentLevel - 1];
	}

	private TextObject[] GetBonusExplanations()
	{
		TextObject[] array = new TextObject[3]
		{
			TextObject.GetEmpty(),
			TextObject.GetEmpty(),
			TextObject.GetEmpty()
		};
		if (_currentLevel == 0 || _currentLevel > 3)
		{
			return array;
		}
		for (int i = 0; i < _currentLevel; i++)
		{
			array[i] = _buildingType.GetExplanationAtLevel(i);
		}
		return array;
	}

	private void UpdateBuildingTypeForOldSaves()
	{
		if (MBSaveLoad.LastLoadedGameVersion.IsOlderThan(ApplicationVersion.FromString("v1.3.0.0")) && new Dictionary<string, string>
		{
			{ "building_fortifications", "building_settlement_fortifications" },
			{ "building_settlement_garrison_barracks", "building_settlement_barracks" },
			{ "building_settlement_militia_barracks", "building_settlement_guard_house" },
			{ "building_siege_workshop", "building_settlement_siege_workshop" },
			{ "building_settlement_marketplace", "building_settlement_tax_office" },
			{ "building_settlement_forum", "building_settlement_marketplace" },
			{ "building_settlement_granary", "building_settlement_warehouse" },
			{ "building_settlement_workshop", "building_settlement_mason" },
			{ "building_settlement_aquaducts", "building_settlement_waterworks" },
			{ "building_settlement_fairgrounds", "building_settlement_courthouse" },
			{ "building_settlement_lime_kilns", "building_settlement_roads_and_paths" },
			{ "building_wall", "building_castle_fortifications" },
			{ "building_castle_gardens", "building_castle_farmlands" },
			{ "building_castle_workshops", "building_castle_mason" },
			{ "building_castle_fairgrounds", "building_castle_roads_and_paths" },
			{ "building_castle_militia_barracks", "building_castle_guard_house" },
			{ "building_castle_lime_kilns", "building_castle_craftmans_quarters" },
			{
				"building_daily_build_house",
				Town.IsCastle ? "building_castle_daily_slacken_garrison" : "building_settlement_daily_housing"
			},
			{
				"building_daily_train_militia",
				Town.IsCastle ? "building_castle_daily_raise_troops" : "building_settlement_daily_train_militia"
			},
			{
				"building_festivals_and_games",
				Town.IsCastle ? "building_castle_daily_drills" : "building_settlement_daily_festival_and_games"
E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\TaleWorlds.CampaignSystem.Settlements.Buildings\Building.cs:174: 				result.Add(resultNumber, Name);
				break;
			case BuildingEffectIncrementType.AddFactor:
				result.AddFactor(resultNumber, Name);
				break;
			default:
				Debug.FailedAssert("Unsupported BuildingEffectIncrementType!", "C:\\BuildAgent\\work\\mb3\\Source\\Bannerlord\\TaleWorlds.CampaignSystem\\Settlements\\Buildings\\Building.cs", "AddEffectOfBuilding", 172);
				break;
			}
		}
	}

	public TextObject GetBonusExplanation()
	{
		if (_currentLevel == 0)
		{
			return TextObject.GetEmpty();
		}
		return GetBonusExplanations()[_currentLevel - 1];
	}

	private TextObject[] GetBonusExplanations()
	{
		TextObject[] array = new TextObject[3]
		{
			TextObject.GetEmpty(),
			TextObject.GetEmpty(),
			TextObject.GetEmpty()
		};
		if (_currentLevel == 0 || _currentLevel > 3)
		{
			return array;
		}
		for (int i = 0; i < _currentLevel; i++)
		{
			array[i] = _buildingType.GetExplanationAtLevel(i);
		}
		return array;
	}

	private void UpdateBuildingTypeForOldSaves()
	{
		if (MBSaveLoad.LastLoadedGameVersion.IsOlderThan(ApplicationVersion.FromString("v1.3.0.0")) && new Dictionary<string, string>
		{
			{ "building_fortifications", "building_settlement_fortifications" },
			{ "building_settlement_garrison_barracks", "building_settlement_barracks" },
			{ "building_settlement_militia_barracks", "building_settlement_guard_house" },
			{ "building_siege_workshop", "building_settlement_siege_workshop" },
			{ "building_settlement_marketplace", "building_settlement_tax_office" },
			{ "building_settlement_forum", "building_settlement_marketplace" },
			{ "building_settlement_granary", "building_settlement_warehouse" },
			{ "building_settlement_workshop", "building_settlement_mason" },
			{ "building_settlement_aquaducts", "building_settlement_waterworks" },
			{ "building_settlement_fairgrounds", "building_settlement_courthouse" },
			{ "building_settlement_lime_kilns", "building_settlement_roads_and_paths" },
			{ "building_wall", "building_castle_fortifications" },
			{ "building_castle_gardens", "building_castle_farmlands" },
			{ "building_castle_workshops", "building_castle_mason" },
			{ "building_castle_fairgrounds", "building_castle_roads_and_paths" },
			{ "building_castle_militia_barracks", "building_castle_guard_house" },
			{ "building_castle_lime_kilns", "building_castle_craftmans_quarters" },
			{
				"building_daily_build_house",
				Town.IsCastle ? "building_castle_daily_slacken_garrison" : "building_settlement_daily_housing"
			},
			{
				"building_daily_train_militia",
				Town.IsCastle ? "building_castle_daily_raise_troops" : "building_settlement_daily_train_militia"
			},
			{
				"building_festivals_and_games",
				Town.IsCastle ? "building_castle_daily_drills" : "building_settlement_daily_festival_and_games"
			},
			{
				"building_irrigation",
				Town.IsCastle ? "building_castle_daily_irrigation" : "building_settlement_daily_irrigation"
			}
		}.TryGetValue(_buildingType.StringId, out var value))
		{
			_buildingType = MBObjectManager.Instance.RegisterPresumedObject(new BuildingType(value));
		}
E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\TaleWorlds.CampaignSystem.Settlements.Buildings\Building.cs:176: 			case BuildingEffectIncrementType.AddFactor:
				result.AddFactor(resultNumber, Name);
				break;
			default:
				Debug.FailedAssert("Unsupported BuildingEffectIncrementType!", "C:\\BuildAgent\\work\\mb3\\Source\\Bannerlord\\TaleWorlds.CampaignSystem\\Settlements\\Buildings\\Building.cs", "AddEffectOfBuilding", 172);
				break;
			}
		}
	}

	public TextObject GetBonusExplanation()
	{
		if (_currentLevel == 0)
		{
			return TextObject.GetEmpty();
		}
		return GetBonusExplanations()[_currentLevel - 1];
	}

	private TextObject[] GetBonusExplanations()
	{
		TextObject[] array = new TextObject[3]
		{
			TextObject.GetEmpty(),
			TextObject.GetEmpty(),
			TextObject.GetEmpty()
		};
		if (_currentLevel == 0 || _currentLevel > 3)
		{
			return array;
		}
		for (int i = 0; i < _currentLevel; i++)
		{
			array[i] = _buildingType.GetExplanationAtLevel(i);
		}
		return array;
	}

	private void UpdateBuildingTypeForOldSaves()
	{
		if (MBSaveLoad.LastLoadedGameVersion.IsOlderThan(ApplicationVersion.FromString("v1.3.0.0")) && new Dictionary<string, string>
		{
			{ "building_fortifications", "building_settlement_fortifications" },
			{ "building_settlement_garrison_barracks", "building_settlement_barracks" },
			{ "building_settlement_militia_barracks", "building_settlement_guard_house" },
			{ "building_siege_workshop", "building_settlement_siege_workshop" },
			{ "building_settlement_marketplace", "building_settlement_tax_office" },
			{ "building_settlement_forum", "building_settlement_marketplace" },
			{ "building_settlement_granary", "building_settlement_warehouse" },
			{ "building_settlement_workshop", "building_settlement_mason" },
			{ "building_settlement_aquaducts", "building_settlement_waterworks" },
			{ "building_settlement_fairgrounds", "building_settlement_courthouse" },
			{ "building_settlement_lime_kilns", "building_settlement_roads_and_paths" },
			{ "building_wall", "building_castle_fortifications" },
			{ "building_castle_gardens", "building_castle_farmlands" },
			{ "building_castle_workshops", "building_castle_mason" },
			{ "building_castle_fairgrounds", "building_castle_roads_and_paths" },
			{ "building_castle_militia_barracks", "building_castle_guard_house" },
			{ "building_castle_lime_kilns", "building_castle_craftmans_quarters" },
			{
				"building_daily_build_house",
				Town.IsCastle ? "building_castle_daily_slacken_garrison" : "building_settlement_daily_housing"
			},
			{
				"building_daily_train_militia",
				Town.IsCastle ? "building_castle_daily_raise_troops" : "building_settlement_daily_train_militia"
			},
			{
				"building_festivals_and_games",
				Town.IsCastle ? "building_castle_daily_drills" : "building_settlement_daily_festival_and_games"
			},
			{
				"building_irrigation",
				Town.IsCastle ? "building_castle_daily_irrigation" : "building_settlement_daily_irrigation"
			}
		}.TryGetValue(_buildingType.StringId, out var value))
		{
			_buildingType = MBObjectManager.Instance.RegisterPresumedObject(new BuildingType(value));
		}
	}
}
E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\TaleWorlds.CampaignSystem.Settlements.Buildings\Building.cs:177: 				result.AddFactor(resultNumber, Name);
				break;
			default:
				Debug.FailedAssert("Unsupported BuildingEffectIncrementType!", "C:\\BuildAgent\\work\\mb3\\Source\\Bannerlord\\TaleWorlds.CampaignSystem\\Settlements\\Buildings\\Building.cs", "AddEffectOfBuilding", 172);
				break;
			}
		}
	}

	public TextObject GetBonusExplanation()
	{
		if (_currentLevel == 0)
		{
			return TextObject.GetEmpty();
		}
		return GetBonusExplanations()[_currentLevel - 1];
	}

	private TextObject[] GetBonusExplanations()
	{
		TextObject[] array = new TextObject[3]
		{
			TextObject.GetEmpty(),
			TextObject.GetEmpty(),
			TextObject.GetEmpty()
		};
		if (_currentLevel == 0 || _currentLevel > 3)
		{
			return array;
		}
		for (int i = 0; i < _currentLevel; i++)
		{
			array[i] = _buildingType.GetExplanationAtLevel(i);
		}
		return array;
	}

	private void UpdateBuildingTypeForOldSaves()
	{
		if (MBSaveLoad.LastLoadedGameVersion.IsOlderThan(ApplicationVersion.FromString("v1.3.0.0")) && new Dictionary<string, string>
		{
			{ "building_fortifications", "building_settlement_fortifications" },
			{ "building_settlement_garrison_barracks", "building_settlement_barracks" },
			{ "building_settlement_militia_barracks", "building_settlement_guard_house" },
			{ "building_siege_workshop", "building_settlement_siege_workshop" },
			{ "building_settlement_marketplace", "building_settlement_tax_office" },
			{ "building_settlement_forum", "building_settlement_marketplace" },
			{ "building_settlement_granary", "building_settlement_warehouse" },
			{ "building_settlement_workshop", "building_settlement_mason" },
			{ "building_settlement_aquaducts", "building_settlement_waterworks" },
			{ "building_settlement_fairgrounds", "building_settlement_courthouse" },
			{ "building_settlement_lime_kilns", "building_settlement_roads_and_paths" },
			{ "building_wall", "building_castle_fortifications" },
			{ "building_castle_gardens", "building_castle_farmlands" },
			{ "building_castle_workshops", "building_castle_mason" },
			{ "building_castle_fairgrounds", "building_castle_roads_and_paths" },
			{ "building_castle_militia_barracks", "building_castle_guard_house" },
			{ "building_castle_lime_kilns", "building_castle_craftmans_quarters" },
			{
				"building_daily_build_house",
				Town.IsCastle ? "building_castle_daily_slacken_garrison" : "building_settlement_daily_housing"
			},
			{
				"building_daily_train_militia",
				Town.IsCastle ? "building_castle_daily_raise_troops" : "building_settlement_daily_train_militia"
			},
			{
				"building_festivals_and_games",
				Town.IsCastle ? "building_castle_daily_drills" : "building_settlement_daily_festival_and_games"
			},
			{
				"building_irrigation",
				Town.IsCastle ? "building_castle_daily_irrigation" : "building_settlement_daily_irrigation"
			}
		}.TryGetValue(_buildingType.StringId, out var value))
		{
			_buildingType = MBObjectManager.Instance.RegisterPresumedObject(new BuildingType(value));
		}
	}
}
E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\TaleWorlds.CampaignSystem.Settlements.Buildings\Building.cs:180: 				Debug.FailedAssert("Unsupported BuildingEffectIncrementType!", "C:\\BuildAgent\\work\\mb3\\Source\\Bannerlord\\TaleWorlds.CampaignSystem\\Settlements\\Buildings\\Building.cs", "AddEffectOfBuilding", 172);
				break;
			}
		}
	}

	public TextObject GetBonusExplanation()
	{
		if (_currentLevel == 0)
		{
			return TextObject.GetEmpty();
		}
		return GetBonusExplanations()[_currentLevel - 1];
	}

	private TextObject[] GetBonusExplanations()
	{
		TextObject[] array = new TextObject[3]
		{
			TextObject.GetEmpty(),
			TextObject.GetEmpty(),
			TextObject.GetEmpty()
		};
		if (_currentLevel == 0 || _currentLevel > 3)
		{
			return array;
		}
		for (int i = 0; i < _currentLevel; i++)
		{
			array[i] = _buildingType.GetExplanationAtLevel(i);
		}
		return array;
	}

	private void UpdateBuildingTypeForOldSaves()
	{
		if (MBSaveLoad.LastLoadedGameVersion.IsOlderThan(ApplicationVersion.FromString("v1.3.0.0")) && new Dictionary<string, string>
		{
			{ "building_fortifications", "building_settlement_fortifications" },
			{ "building_settlement_garrison_barracks", "building_settlement_barracks" },
			{ "building_settlement_militia_barracks", "building_settlement_guard_house" },
			{ "building_siege_workshop", "building_settlement_siege_workshop" },
			{ "building_settlement_marketplace", "building_settlement_tax_office" },
			{ "building_settlement_forum", "building_settlement_marketplace" },
			{ "building_settlement_granary", "building_settlement_warehouse" },
			{ "building_settlement_workshop", "building_settlement_mason" },
			{ "building_settlement_aquaducts", "building_settlement_waterworks" },
			{ "building_settlement_fairgrounds", "building_settlement_courthouse" },
			{ "building_settlement_lime_kilns", "building_settlement_roads_and_paths" },
			{ "building_wall", "building_castle_fortifications" },
			{ "building_castle_gardens", "building_castle_farmlands" },
			{ "building_castle_workshops", "building_castle_mason" },
			{ "building_castle_fairgrounds", "building_castle_roads_and_paths" },
			{ "building_castle_militia_barracks", "building_castle_guard_house" },
			{ "building_castle_lime_kilns", "building_castle_craftmans_quarters" },
			{
				"building_daily_build_house",
				Town.IsCastle ? "building_castle_daily_slacken_garrison" : "building_settlement_daily_housing"
			},
			{
				"building_daily_train_militia",
				Town.IsCastle ? "building_castle_daily_raise_troops" : "building_settlement_daily_train_militia"
			},
			{
				"building_festivals_and_games",
				Town.IsCastle ? "building_castle_daily_drills" : "building_settlement_daily_festival_and_games"
			},
			{
				"building_irrigation",
				Town.IsCastle ? "building_castle_daily_irrigation" : "building_settlement_daily_irrigation"
			}
		}.TryGetValue(_buildingType.StringId, out var value))
		{
			_buildingType = MBObjectManager.Instance.RegisterPresumedObject(new BuildingType(value));
		}
	}
}

 succeeded in 428ms:
E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\TaleWorlds.CampaignSystem.Settlements.Buildings\BuildingEffectIncrementType.cs:1: namespace TaleWorlds.CampaignSystem.Settlements.Buildings;
E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\TaleWorlds.CampaignSystem.Settlements.Buildings\BuildingEffectIncrementType.cs:2: 
E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\TaleWorlds.CampaignSystem.Settlements.Buildings\BuildingEffectIncrementType.cs:3: public enum BuildingEffectIncrementType
E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\TaleWorlds.CampaignSystem.Settlements.Buildings\BuildingEffectIncrementType.cs:4: {
E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\TaleWorlds.CampaignSystem.Settlements.Buildings\BuildingEffectIncrementType.cs:5: 	Add,
E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\TaleWorlds.CampaignSystem.Settlements.Buildings\BuildingEffectIncrementType.cs:6: 	AddFactor
E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\TaleWorlds.CampaignSystem.Settlements.Buildings\BuildingEffectIncrementType.cs:7: }

 succeeded in 450ms:
	public struct EffectInfo
	{
E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\TaleWorlds.CampaignSystem.Settlements.Buildings\BuildingType.cs:15: 		public BuildingEffectEnum BuildingEffect { get; }

		public BuildingEffectIncrementType BuildingEffectIncrementType { get; }

		public float Level1Effect { get; }

		public float Level2Effect { get; }

		public float Level3Effect { get; }
		public BuildingEffectEnum BuildingEffect { get; }

E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\TaleWorlds.CampaignSystem.Settlements.Buildings\BuildingType.cs:17: 		public BuildingEffectIncrementType BuildingEffectIncrementType { get; }

		public float Level1Effect { get; }

		public float Level2Effect { get; }

		public float Level3Effect { get; }

		public float GetEffectValue(int i)
		}

E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\TaleWorlds.CampaignSystem.Settlements.Buildings\BuildingType.cs:35: 		public EffectInfo(BuildingEffectEnum effect, BuildingEffectIncrementType effectIncrementType, float[] effectValues)
		{
			BuildingEffect = effect;
			BuildingEffectIncrementType = effectIncrementType;
			Level1Effect = effectValues[0];
			Level2Effect = effectValues[1];
			Level3Effect = effectValues[2];
		}

		public EffectInfo(BuildingEffectEnum effect, BuildingEffectIncrementType effectIncrementType, float[] effectValues)
		{
E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\TaleWorlds.CampaignSystem.Settlements.Buildings\BuildingType.cs:37: 			BuildingEffect = effect;
			BuildingEffectIncrementType = effectIncrementType;
			Level1Effect = effectValues[0];
			Level2Effect = effectValues[1];
			Level3Effect = effectValues[2];
		}

		public EffectInfo(BuildingEffectEnum effect, BuildingEffectIncrementType effectIncrementType, float effectValue1, float effectValue2, float effectValue3)
		{
		{
			BuildingEffect = effect;
E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\TaleWorlds.CampaignSystem.Settlements.Buildings\BuildingType.cs:38: 			BuildingEffectIncrementType = effectIncrementType;
			Level1Effect = effectValues[0];
			Level2Effect = effectValues[1];
			Level3Effect = effectValues[2];
		}

		public EffectInfo(BuildingEffectEnum effect, BuildingEffectIncrementType effectIncrementType, float effectValue1, float effectValue2, float effectValue3)
		{
			BuildingEffect = effect;
		}

E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\TaleWorlds.CampaignSystem.Settlements.Buildings\BuildingType.cs:44: 		public EffectInfo(BuildingEffectEnum effect, BuildingEffectIncrementType effectIncrementType, float effectValue1, float effectValue2, float effectValue3)
		{
			BuildingEffect = effect;
			BuildingEffectIncrementType = effectIncrementType;
			Level1Effect = effectValue1;
			Level2Effect = effectValue2;
			Level3Effect = effectValue3;
		}
	}
		public EffectInfo(BuildingEffectEnum effect, BuildingEffectIncrementType effectIncrementType, float effectValue1, float effectValue2, float effectValue3)
		{
E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\TaleWorlds.CampaignSystem.Settlements.Buildings\BuildingType.cs:46: 			BuildingEffect = effect;
			BuildingEffectIncrementType = effectIncrementType;
			Level1Effect = effectValue1;
			Level2Effect = effectValue2;
			Level3Effect = effectValue3;
		}
	}

	public const int MaxLevel = 3;
		{
			BuildingEffect = effect;
E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\TaleWorlds.CampaignSystem.Settlements.Buildings\BuildingType.cs:47: 			BuildingEffectIncrementType = effectIncrementType;
			Level1Effect = effectValue1;
			Level2Effect = effectValue2;
			Level3Effect = effectValue3;
		}
	}

	public const int MaxLevel = 3;

	}

E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\TaleWorlds.CampaignSystem.Settlements.Buildings\BuildingType.cs:94: 	public void Initialize(TextObject name, TextObject explanation, int[] productionCosts, Tuple<BuildingEffectEnum, BuildingEffectIncrementType, float, float, float>[] effects, bool isMilitaryProject, float varianceChance, int startLevel = 0)
	{
		base.Initialize();
		Name = name;
		Explanation = explanation;
		IsDailyProject = false;
		IsMilitaryProject = isMilitaryProject;
		VarianceChance = varianceChance;
		StartLevel = startLevel;
		StartLevel = startLevel;
		_productionCosts = productionCosts;
E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\TaleWorlds.CampaignSystem.Settlements.Buildings\BuildingType.cs:104: 		_effects = effects.Select((Tuple<BuildingEffectEnum, BuildingEffectIncrementType, float, float, float> x) => new EffectInfo(x.Item1, x.Item2, x.Item3, x.Item4, x.Item5)).ToArray();
		AfterInitialized();
	}

	public void InitializeDailyProject(TextObject name, TextObject explanation, Tuple<BuildingEffectEnum, BuildingEffectIncrementType, float, float, float>[] effects)
	{
		base.Initialize();
		Name = name;
		Explanation = explanation;
	}

E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\TaleWorlds.CampaignSystem.Settlements.Buildings\BuildingType.cs:108: 	public void InitializeDailyProject(TextObject name, TextObject explanation, Tuple<BuildingEffectEnum, BuildingEffectIncrementType, float, float, float>[] effects)
	{
		base.Initialize();
		Name = name;
		Explanation = explanation;
		IsDailyProject = true;
		IsMilitaryProject = false;
		VarianceChance = 0f;
		StartLevel = 1;
		StartLevel = 1;
		_productionCosts = new int[3];
E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\TaleWorlds.CampaignSystem.Settlements.Buildings\BuildingType.cs:118: 		_effects = effects.Select((Tuple<BuildingEffectEnum, BuildingEffectIncrementType, float, float, float> x) => new EffectInfo(x.Item1, x.Item2, x.Item3, x.Item4, x.Item5)).ToArray();
		AfterInitialized();
	}

	public override string ToString()
	{
		return Name.ToString();
	}

	}

E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\TaleWorlds.CampaignSystem.Settlements.Buildings\BuildingType.cs:136: 	public float GetBaseBuildingEffectAmount(BuildingEffectEnum effect, int level)
	{
		for (int i = 0; i < _effects.Length; i++)
		{
			if (_effects[i].BuildingEffect == effect)
			{
				return _effects[i].GetEffectValue(level);
			}
		}
		for (int i = 0; i < _effects.Length; i++)
		{
E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\TaleWorlds.CampaignSystem.Settlements.Buildings\BuildingType.cs:140: 			if (_effects[i].BuildingEffect == effect)
			{
				return _effects[i].GetEffectValue(level);
			}
		}
		return 0f;
	}

	public bool HasEffect(BuildingEffectEnum effect)
	}

E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\TaleWorlds.CampaignSystem.Settlements.Buildings\BuildingType.cs:148: 	public bool HasEffect(BuildingEffectEnum effect)
	{
		for (int i = 0; i < _effects.Length; i++)
		{
			if (_effects[i].BuildingEffect == effect)
			{
				return true;
			}
		}
		for (int i = 0; i < _effects.Length; i++)
		{
E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\TaleWorlds.CampaignSystem.Settlements.Buildings\BuildingType.cs:152: 			if (_effects[i].BuildingEffect == effect)
			{
				return true;
			}
		}
		return false;
	}

	public TextObject GetExplanationAtLevel(int level)
		if (array.Length == 1)
		{
E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\TaleWorlds.CampaignSystem.Settlements.Buildings\BuildingType.cs:170: 			textObject = GameTexts.FindText("str_building_effect_explanation", Enum.GetName(typeof(BuildingEffectEnum), array[0].BuildingEffect));
			textObject.SetTextVariable("BONUS_AMOUNT", array[0].GetEffectValue(level));
			textObject.SetTextVariable("BONUS_AMOUNT_PERCENT", array[0].GetEffectValue(level) * 100f);
		}
		else if (array.Length >= 2)
		{
			textObject = GameTexts.FindText("str_string_newline_string");
			TextObject textObject2 = GameTexts.FindText("str_building_effect_explanation", Enum.GetName(typeof(BuildingEffectEnum), array[0].BuildingEffect));
			textObject2.SetTextVariable("BONUS_AMOUNT", array[0].GetEffectValue(level));
		{
			textObject = GameTexts.FindText("str_string_newline_string");
E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\TaleWorlds.CampaignSystem.Settlements.Buildings\BuildingType.cs:177: 			TextObject textObject2 = GameTexts.FindText("str_building_effect_explanation", Enum.GetName(typeof(BuildingEffectEnum), array[0].BuildingEffect));
			textObject2.SetTextVariable("BONUS_AMOUNT", array[0].GetEffectValue(level));
			textObject2.SetTextVariable("BONUS_AMOUNT_PERCENT", array[0].GetEffectValue(level) * 100f);
			TextObject textObject3 = GameTexts.FindText("str_building_effect_explanation", Enum.GetName(typeof(BuildingEffectEnum), array[1].BuildingEffect));
			textObject3.SetTextVariable("BONUS_AMOUNT", array[1].GetEffectValue(level));
			textObject3.SetTextVariable("BONUS_AMOUNT_PERCENT", array[1].GetEffectValue(level) * 100f);
			textObject.SetTextVariable("STR1", textObject2);
			textObject.SetTextVariable("STR2", textObject3);
			textObject.SetTextVariable("newline", "\n");
			textObject2.SetTextVariable("BONUS_AMOUNT", array[0].GetEffectValue(level));
			textObject2.SetTextVariable("BONUS_AMOUNT_PERCENT", array[0].GetEffectValue(level) * 100f);
E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\TaleWorlds.CampaignSystem.Settlements.Buildings\BuildingType.cs:180: 			TextObject textObject3 = GameTexts.FindText("str_building_effect_explanation", Enum.GetName(typeof(BuildingEffectEnum), array[1].BuildingEffect));
			textObject3.SetTextVariable("BONUS_AMOUNT", array[1].GetEffectValue(level));
			textObject3.SetTextVariable("BONUS_AMOUNT_PERCENT", array[1].GetEffectValue(level) * 100f);
			textObject.SetTextVariable("STR1", textObject2);
			textObject.SetTextVariable("STR2", textObject3);
			textObject.SetTextVariable("newline", "\n");
			for (int num = 2; num < array.Length; num++)
			{
				TextObject textObject4 = GameTexts.FindText("str_string_newline_string");
				TextObject textObject4 = GameTexts.FindText("str_string_newline_string");
				textObject4.SetTextVariable("STR1", textObject);
E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\TaleWorlds.CampaignSystem.Settlements.Buildings\BuildingType.cs:190: 				TextObject textObject5 = GameTexts.FindText("str_building_effect_explanation", Enum.GetName(typeof(BuildingEffectEnum), array[num].BuildingEffect));
				textObject5.SetTextVariable("BONUS_AMOUNT", array[num].GetEffectValue(level));
				textObject5.SetTextVariable("BONUS_AMOUNT_PERCENT", array[num].GetEffectValue(level) * 100f);
				textObject4.SetTextVariable("STR2", textObject5);
				textObject4.SetTextVariable("newline", "\n");
				textObject = textObject4;
			}
		}
		return textObject;
	}

E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\TaleWorlds.CampaignSystem.Settlements.Buildings\BuildingType.cs:201: 	public BuildingEffectIncrementType GetBuildingEffectType(BuildingEffectEnum effect)
	{
		EffectInfo[] effects = _effects;
		for (int i = 0; i < effects.Length; i++)
		{
			EffectInfo effectInfo = effects[i];
			if (effectInfo.BuildingEffect == effect)
			{
				return effectInfo.BuildingEffectIncrementType;
		{
			EffectInfo effectInfo = effects[i];
E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\TaleWorlds.CampaignSystem.Settlements.Buildings\BuildingType.cs:207: 			if (effectInfo.BuildingEffect == effect)
			{
				return effectInfo.BuildingEffectIncrementType;
			}
		}
		return BuildingEffectIncrementType.Add;
	}
}
			if (effectInfo.BuildingEffect == effect)
			{
E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\TaleWorlds.CampaignSystem.Settlements.Buildings\BuildingType.cs:209: 				return effectInfo.BuildingEffectIncrementType;
			}
		}
		return BuildingEffectIncrementType.Add;
	}
}
			}
		}
E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\TaleWorlds.CampaignSystem.Settlements.Buildings\BuildingType.cs:212: 		return BuildingEffectIncrementType.Add;
	}
}

codex
`AddEffectOfBuildings` can add either flat values or factors. I’m checking the actual `FoodProduction` building definitions now; if vanilla uses `AddFactor`, a post-base delta would not be equivalent to replacing vanilla’s constants inside the original formula.
exec
"C:\\Program Files\\PowerShell\\7\\pwsh.exe" -Command "Select-String -Path 'E:\\Decompiled_Bannerlord\\Campaign\\**\\*.cs' -Pattern 'FoodProduction' -CaseSensitive:"'$false | Select-Object -First 80 | ForEach-Object { "{0}:{1}: {2}" -f $_.Path,$_.LineNumber,$_.Line.Trim() }' in C:\Users\mikew\source\repos\TAOM
exec
"C:\\Program Files\\PowerShell\\7\\pwsh.exe" -Command "Select-String -Path 'Main/_Module/ModuleData/**/*.xml','Main/_Module/ModuleData/*.xml' -Pattern 'FoodProduction|food_production|building_.*food|VillageFood' -CaseSensitive:"'$false | Select-Object -First 120 | ForEach-Object { "{0}:{1}: {2}" -f $_.Path,$_.LineNumber,$_.Line.Trim() }' in C:\Users\mikew\source\repos\TAOM
exec
"C:\\Program Files\\PowerShell\\7\\pwsh.exe" -Command "Get-ChildItem -Path 'E:\\Decompiled_Bannerlord' -Recurse -Include '*.xml' -ErrorAction SilentlyContinue | Select-String -Pattern 'FoodProduction|food_production' -CaseSensitive:"'$false | Select-Object -First 80 | ForEach-Object { "{0}:{1}: {2}" -f $_.Path,$_.LineNumber,$_.Line.Trim() }' in C:\Users\mikew\source\repos\TAOM
exec
"C:\\Program Files\\PowerShell\\7\\pwsh.exe" -Command "Get-ChildItem -Path 'E:\\Decompiled_Bannerlord' -Recurse -Filter 'DefaultBuildingTypes.cs' -ErrorAction SilentlyContinue | Select-Object -First 20 | ForEach-Object { "'$_.FullName }' in C:\Users\mikew\source\repos\TAOM
 succeeded in 460ms:
 succeeded in 471ms:
E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\TaleWorlds.CampaignSystem.Settlements.Buildings\DefaultBuildingTypes.cs

 succeeded in 551ms:
 succeeded in 644ms:
codex
The broad text search didn’t find XML definitions, so I’m reading `DefaultBuildingTypes.cs` directly from the decompile. That is the source of truth for whether food-production building effects are additive or multiplicative in this build.
exec
"C:\\Program Files\\PowerShell\\7\\pwsh.exe" -Command '$path='"'E:\\Decompiled_Bannerlord\\Campaign\\TaleWorlds.CampaignSystem\\TaleWorlds.CampaignSystem.Settlements.Buildings\\DefaultBuildingTypes.cs'; Select-String -Path "'$path -Pattern '"'FoodProduction|Farmlands|Gardens|Orchard|Granary|Warehouse|BuildingEffectEnum' -Context 5,12 | Select-Object -First 200 | ForEach-Object { "'$_.Context.PreContext; '"'{0}:{1}: {2}' -f "'$_.Path,$_.LineNumber,$_.Line; $_.Context.PostContext }' in C:\Users\mikew\source\repos\TAOM
 succeeded in 562ms:

	private BuildingType _buildingSettlementGuardHouse;

	private BuildingType _buildingSettlementTaxOffice;

E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\TaleWorlds.CampaignSystem.Settlements.Buildings\DefaultBuildingTypes.cs:25: 	private BuildingType _buildingSettlementWarehouse;

	private BuildingType _buildingSettlementMason;

	private BuildingType _buildingSettlementCourthouse;

	private BuildingType _buildingSettlementWaterworks;

	private BuildingType _buildingSettlementRoadsAndPaths;

	private BuildingType _buildingCastleFortifications;

	private BuildingType _buildingCastleBarracks;

	private BuildingType _buildingCastleBarracks;

	private BuildingType _buildingCastleTrainingFields;

E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\TaleWorlds.CampaignSystem.Settlements.Buildings\DefaultBuildingTypes.cs:41: 	private BuildingType _buildingCastleGranary;

	private BuildingType _buildingCastleGuardHouse;

	private BuildingType _buildingCastleCastallansOffice;

	private BuildingType _buildingCastleSiegeWorkshop;

	private BuildingType _buildingCastleCraftmansQuarters;

	private BuildingType _buildingCastleFarmlands;

	private BuildingType _buildingSettlementDailyHousing;

	private BuildingType _buildingCastleSiegeWorkshop;

	private BuildingType _buildingCastleCraftmansQuarters;

E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\TaleWorlds.CampaignSystem.Settlements.Buildings\DefaultBuildingTypes.cs:51: 	private BuildingType _buildingCastleFarmlands;

	private BuildingType _buildingSettlementDailyHousing;

	private BuildingType _buildingCastleMason;

	private BuildingType _buildingCastleRoadsAndPaths;

	private BuildingType _buildingSettlementDailyIrrigation;

	private BuildingType _buildingSettlementDailyTrainMilitia;

	private BuildingType _buildingCastleDailySlackenGarrison;

	public static BuildingType SettlementGuardHouse => Instance._buildingSettlementGuardHouse;

	public static BuildingType SettlementTaxOffice => Instance._buildingSettlementTaxOffice;

E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\TaleWorlds.CampaignSystem.Settlements.Buildings\DefaultBuildingTypes.cs:85: 	public static BuildingType SettlementWarehouse => Instance._buildingSettlementWarehouse;

	public static BuildingType SettlementMason => Instance._buildingSettlementMason;

	public static BuildingType SettlementSiegeWorkshop => Instance._buildingSettlementSiegeWorkshop;

	public static BuildingType SettlementWaterworks => Instance._buildingSettlementWaterworks;

	public static BuildingType SettlementCourthouse => Instance._buildingSettlementCourthouse;

	public static BuildingType SettlementMarketplace => Instance._buildingSettlementMarketplace;

	public static BuildingType SettlementRoadsAndPaths => Instance._buildingSettlementRoadsAndPaths;

	public static BuildingType CastleSiegeWorkshop => Instance._buildingCastleSiegeWorkshop;

	public static BuildingType CastleCraftmansQuarters => Instance._buildingCastleCraftmansQuarters;

E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\TaleWorlds.CampaignSystem.Settlements.Buildings\DefaultBuildingTypes.cs:113: 	public static BuildingType CastleFarmlands => Instance._buildingCastleFarmlands;

	public static BuildingType CastleGranary => Instance._buildingCastleGranary;

	public static BuildingType CastleMason => Instance._buildingCastleMason;

	public static BuildingType CastleRoadsAndPaths => Instance._buildingCastleRoadsAndPaths;

	public static BuildingType SettlementDailyHousing => Instance._buildingSettlementDailyHousing;

	public static BuildingType SettlementDailyTrainMilitia => Instance._buildingSettlementDailyTrainMilitia;

	public static BuildingType SettlementDailyFestivalAndGames => Instance._buildingSettlementDailyFestivalAndGames;

	public static BuildingType CastleCraftmansQuarters => Instance._buildingCastleCraftmansQuarters;

	public static BuildingType CastleFarmlands => Instance._buildingCastleFarmlands;

E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\TaleWorlds.CampaignSystem.Settlements.Buildings\DefaultBuildingTypes.cs:115: 	public static BuildingType CastleGranary => Instance._buildingCastleGranary;

	public static BuildingType CastleMason => Instance._buildingCastleMason;

	public static BuildingType CastleRoadsAndPaths => Instance._buildingCastleRoadsAndPaths;

	public static BuildingType SettlementDailyHousing => Instance._buildingSettlementDailyHousing;

	public static BuildingType SettlementDailyTrainMilitia => Instance._buildingSettlementDailyTrainMilitia;

	public static BuildingType SettlementDailyFestivalAndGames => Instance._buildingSettlementDailyFestivalAndGames;

	public static BuildingType SettlementDailyIrrigation => Instance._buildingSettlementDailyIrrigation;
		_buildingSettlementTrainingFields = Create("building_settlement_training_fields");
		_buildingSettlementGuardHouse = Create("building_settlement_guard_house");
		_buildingSettlementSiegeWorkshop = Create("building_settlement_siege_workshop");
		_buildingSettlementTaxOffice = Create("building_settlement_tax_office");
		_buildingSettlementMarketplace = Create("building_settlement_marketplace");
E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\TaleWorlds.CampaignSystem.Settlements.Buildings\DefaultBuildingTypes.cs:151: 		_buildingSettlementWarehouse = Create("building_settlement_warehouse");
		_buildingSettlementMason = Create("building_settlement_mason");
		_buildingSettlementWaterworks = Create("building_settlement_waterworks");
		_buildingSettlementCourthouse = Create("building_settlement_courthouse");
		_buildingSettlementRoadsAndPaths = Create("building_settlement_roads_and_paths");
		_buildingCastleFortifications = Create("building_castle_fortifications");
		_buildingCastleBarracks = Create("building_castle_barracks");
		_buildingCastleTrainingFields = Create("building_castle_training_fields");
		_buildingCastleGuardHouse = Create("building_castle_guard_house");
		_buildingCastleSiegeWorkshop = Create("building_castle_siege_workshop");
		_buildingCastleCastallansOffice = Create("building_castle_castallans_office");
		_buildingCastleGranary = Create("building_castle_granary");
		_buildingCastleCraftmansQuarters = Create("building_castle_craftmans_quarters");
		_buildingCastleBarracks = Create("building_castle_barracks");
		_buildingCastleTrainingFields = Create("building_castle_training_fields");
		_buildingCastleGuardHouse = Create("building_castle_guard_house");
		_buildingCastleSiegeWorkshop = Create("building_castle_siege_workshop");
		_buildingCastleCastallansOffice = Create("building_castle_castallans_office");
E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\TaleWorlds.CampaignSystem.Settlements.Buildings\DefaultBuildingTypes.cs:162: 		_buildingCastleGranary = Create("building_castle_granary");
		_buildingCastleCraftmansQuarters = Create("building_castle_craftmans_quarters");
		_buildingCastleFarmlands = Create("building_castle_farmlands");
		_buildingCastleMason = Create("building_castle_mason");
		_buildingCastleRoadsAndPaths = Create("building_castle_roads_and_paths");
		_buildingSettlementDailyHousing = Create("building_settlement_daily_housing");
		_buildingSettlementDailyTrainMilitia = Create("building_settlement_daily_train_militia");
		_buildingSettlementDailyFestivalAndGames = Create("building_settlement_daily_festival_and_games");
		_buildingSettlementDailyIrrigation = Create("building_settlement_daily_irrigation");
		_buildingCastleDailySlackenGarrison = Create("building_castle_daily_slacken_garrison");
		_buildingCastleDailyRaiseTroops = Create("building_castle_daily_raise_troops");
		_buildingCastleDailyDrills = Create("building_castle_daily_drills");
		_buildingCastleDailyIrrigation = Create("building_castle_daily_irrigation");
		_buildingCastleGuardHouse = Create("building_castle_guard_house");
		_buildingCastleSiegeWorkshop = Create("building_castle_siege_workshop");
		_buildingCastleCastallansOffice = Create("building_castle_castallans_office");
		_buildingCastleGranary = Create("building_castle_granary");
		_buildingCastleCraftmansQuarters = Create("building_castle_craftmans_quarters");
E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\TaleWorlds.CampaignSystem.Settlements.Buildings\DefaultBuildingTypes.cs:164: 		_buildingCastleFarmlands = Create("building_castle_farmlands");
		_buildingCastleMason = Create("building_castle_mason");
		_buildingCastleRoadsAndPaths = Create("building_castle_roads_and_paths");
		_buildingSettlementDailyHousing = Create("building_settlement_daily_housing");
		_buildingSettlementDailyTrainMilitia = Create("building_settlement_daily_train_militia");
		_buildingSettlementDailyFestivalAndGames = Create("building_settlement_daily_festival_and_games");
		_buildingSettlementDailyIrrigation = Create("building_settlement_daily_irrigation");
		_buildingCastleDailySlackenGarrison = Create("building_castle_daily_slacken_garrison");
		_buildingCastleDailyRaiseTroops = Create("building_castle_daily_raise_troops");
		_buildingCastleDailyDrills = Create("building_castle_daily_drills");
		_buildingCastleDailyIrrigation = Create("building_castle_daily_irrigation");
		InitializeAll();
	}
		return Game.Current.ObjectManager.RegisterPresumedObject(new BuildingType(stringId));
	}

	private void InitializeAll()
	{
E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\TaleWorlds.CampaignSystem.Settlements.Buildings\DefaultBuildingTypes.cs:185: 		_buildingSettlementFortifications.Initialize(new TextObject("{=CVdK1ax1}Fortifications"), new TextObject("{=dIM6xa2O}Better fortifications and higher walls around town, also increases the max garrison limit since it provides more space for the resident troops."), new int[3] { 0, 6000, 12000 }, new Tuple<BuildingEffectEnum, BuildingEffectIncrementType, float, float, float>[2]
		{
			new Tuple<BuildingEffectEnum, BuildingEffectIncrementType, float, float, float>(BuildingEffectEnum.GarrisonCapacity, BuildingEffectIncrementType.Add, 60f, 90f, 120f),
			new Tuple<BuildingEffectEnum, BuildingEffectIncrementType, float, float, float>(BuildingEffectEnum.PrisonCapacity, BuildingEffectIncrementType.Add, 50f, 75f, 100f)
		}, isMilitaryProject: true, 0f, 1);
		_buildingSettlementBarracks.Initialize(new TextObject("{=x2B0OjhI}Barracks"), new TextObject("{=JalrbDBC}Lodgings for garrison troops. Each level increases garrison limit and decreases garrison wage."), new int[3] { 1800, 3000, 4200 }, new Tuple<BuildingEffectEnum, BuildingEffectIncrementType, float, float, float>[2]
		{
			new Tuple<BuildingEffectEnum, BuildingEffectIncrementType, float, float, float>(BuildingEffectEnum.GarrisonCapacity, BuildingEffectIncrementType.Add, 60f, 90f, 120f),
			new Tuple<BuildingEffectEnum, BuildingEffectIncrementType, float, float, float>(BuildingEffectEnum.GarrisonWageReduction, BuildingEffectIncrementType.AddFactor, -0.05f, -0.1f, -0.15f)
		}, isMilitaryProject: true, 0f);
		_buildingSettlementTrainingFields.Initialize(new TextObject("{=BkTiRPT4}Training Fields"), new TextObject("{=NYzORuQm}Provides experience for garrison troops and increases militia veterancy."), new int[3] { 1500, 2100, 2700 }, new Tuple<BuildingEffectEnum, BuildingEffectIncrementType, float, float, float>[2]
		{
			new Tuple<BuildingEffectEnum, BuildingEffectIncrementType, float, float, float>(BuildingEffectEnum.ExperiencePerDay, BuildingEffectIncrementType.Add, 1f, 2f, 3f),

	private void InitializeAll()
	{
		_buildingSettlementFortifications.Initialize(new TextObject("{=CVdK1ax1}Fortifications"), new TextObject("{=dIM6xa2O}Better fortifications and higher walls around town, also increases the max garrison limit since it provides more space for the resident troops."), new int[3] { 0, 6000, 12000 }, new Tuple<BuildingEffectEnum, BuildingEffectIncrementType, float, float, float>[2]
		{
E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\TaleWorlds.CampaignSystem.Settlements.Buildings\DefaultBuildingTypes.cs:187: 			new Tuple<BuildingEffectEnum, BuildingEffectIncrementType, float, float, float>(BuildingEffectEnum.GarrisonCapacity, BuildingEffectIncrementType.Add, 60f, 90f, 120f),
			new Tuple<BuildingEffectEnum, BuildingEffectIncrementType, float, float, float>(BuildingEffectEnum.PrisonCapacity, BuildingEffectIncrementType.Add, 50f, 75f, 100f)
		}, isMilitaryProject: true, 0f, 1);
		_buildingSettlementBarracks.Initialize(new TextObject("{=x2B0OjhI}Barracks"), new TextObject("{=JalrbDBC}Lodgings for garrison troops. Each level increases garrison limit and decreases garrison wage."), new int[3] { 1800, 3000, 4200 }, new Tuple<BuildingEffectEnum, BuildingEffectIncrementType, float, float, float>[2]
		{
			new Tuple<BuildingEffectEnum, BuildingEffectIncrementType, float, float, float>(BuildingEffectEnum.GarrisonCapacity, BuildingEffectIncrementType.Add, 60f, 90f, 120f),
			new Tuple<BuildingEffectEnum, BuildingEffectIncrementType, float, float, float>(BuildingEffectEnum.GarrisonWageReduction, BuildingEffectIncrementType.AddFactor, -0.05f, -0.1f, -0.15f)
		}, isMilitaryProject: true, 0f);
		_buildingSettlementTrainingFields.Initialize(new TextObject("{=BkTiRPT4}Training Fields"), new TextObject("{=NYzORuQm}Provides experience for garrison troops and increases militia veterancy."), new int[3] { 1500, 2100, 2700 }, new Tuple<BuildingEffectEnum, BuildingEffectIncrementType, float, float, float>[2]
		{
			new Tuple<BuildingEffectEnum, BuildingEffectIncrementType, float, float, float>(BuildingEffectEnum.ExperiencePerDay, BuildingEffectIncrementType.Add, 1f, 2f, 3f),
			new Tuple<BuildingEffectEnum, BuildingEffectIncrementType, float, float, float>(BuildingEffectEnum.MilitiaVeterancyChance, BuildingEffectIncrementType.Add, 0.1f, 0.15f, 0.2f)
		}, isMilitaryProject: true, 0f);
	private void InitializeAll()
	{
		_buildingSettlementFortifications.Initialize(new TextObject("{=CVdK1ax1}Fortifications"), new TextObject("{=dIM6xa2O}Better fortifications and higher walls around town, also increases the max garrison limit since it provides more space for the resident troops."), new int[3] { 0, 6000, 12000 }, new Tuple<BuildingEffectEnum, BuildingEffectIncrementType, float, float, float>[2]
		{
			new Tuple<BuildingEffectEnum, BuildingEffectIncrementType, float, float, float>(BuildingEffectEnum.GarrisonCapacity, BuildingEffectIncrementType.Add, 60f, 90f, 120f),
E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\TaleWorlds.CampaignSystem.Settlements.Buildings\DefaultBuildingTypes.cs:188: 			new Tuple<BuildingEffectEnum, BuildingEffectIncrementType, float, float, float>(BuildingEffectEnum.PrisonCapacity, BuildingEffectIncrementType.Add, 50f, 75f, 100f)
		}, isMilitaryProject: true, 0f, 1);
		_buildingSettlementBarracks.Initialize(new TextObject("{=x2B0OjhI}Barracks"), new TextObject("{=JalrbDBC}Lodgings for garrison troops. Each level increases garrison limit and decreases garrison wage."), new int[3] { 1800, 3000, 4200 }, new Tuple<BuildingEffectEnum, BuildingEffectIncrementType, float, float, float>[2]
		{
			new Tuple<BuildingEffectEnum, BuildingEffectIncrementType, float, float, float>(BuildingEffectEnum.GarrisonCapacity, BuildingEffectIncrementType.Add, 60f, 90f, 120f),
			new Tuple<BuildingEffectEnum, BuildingEffectIncrementType, float, float, float>(BuildingEffectEnum.GarrisonWageReduction, BuildingEffectIncrementType.AddFactor, -0.05f, -0.1f, -0.15f)
		}, isMilitaryProject: true, 0f);
		_buildingSettlementTrainingFields.Initialize(new TextObject("{=BkTiRPT4}Training Fields"), new TextObject("{=NYzORuQm}Provides experience for garrison troops and increases militia veterancy."), new int[3] { 1500, 2100, 2700 }, new Tuple<BuildingEffectEnum, BuildingEffectIncrementType, float, float, float>[2]
		{
			new Tuple<BuildingEffectEnum, BuildingEffectIncrementType, float, float, float>(BuildingEffectEnum.ExperiencePerDay, BuildingEffectIncrementType.Add, 1f, 2f, 3f),
			new Tuple<BuildingEffectEnum, BuildingEffectIncrementType, float, float, float>(BuildingEffectEnum.MilitiaVeterancyChance, BuildingEffectIncrementType.Add, 0.1f, 0.15f, 0.2f)
		}, isMilitaryProject: true, 0f);
		_buildingSettlementGuardHouse.Initialize(new TextObject("{=OHEiwoHC}Guard House"), new TextObject("{=doojtAwr}Increases prisoner limit and provides a patrol party that improves security."), new int[3] { 1500, 2100, 2700 }, new Tuple<BuildingEffectEnum, BuildingEffectIncrementType, float, float, float>[2]
		_buildingSettlementFortifications.Initialize(new TextObject("{=CVdK1ax1}Fortifications"), new TextObject("{=dIM6xa2O}Better fortifications and higher walls around town, also increases the max garrison limit since it provides more space for the resident troops."), new int[3] { 0, 6000, 12000 }, new Tuple<BuildingEffectEnum, BuildingEffectIncrementType, float, float, float>[2]
		{
			new Tuple<BuildingEffectEnum, BuildingEffectIncrementType, float, float, float>(BuildingEffectEnum.GarrisonCapacity, BuildingEffectIncrementType.Add, 60f, 90f, 120f),
			new Tuple<BuildingEffectEnum, BuildingEffectIncrementType, float, float, float>(BuildingEffectEnum.PrisonCapacity, BuildingEffectIncrementType.Add, 50f, 75f, 100f)
		}, isMilitaryProject: true, 0f, 1);
E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\TaleWorlds.CampaignSystem.Settlements.Buildings\DefaultBuildingTypes.cs:190: 		_buildingSettlementBarracks.Initialize(new TextObject("{=x2B0OjhI}Barracks"), new TextObject("{=JalrbDBC}Lodgings for garrison troops. Each level increases garrison limit and decreases garrison wage."), new int[3] { 1800, 3000, 4200 }, new Tuple<BuildingEffectEnum, BuildingEffectIncrementType, float, float, float>[2]
		{
			new Tuple<BuildingEffectEnum, BuildingEffectIncrementType, float, float, float>(BuildingEffectEnum.GarrisonCapacity, BuildingEffectIncrementType.Add, 60f, 90f, 120f),
			new Tuple<BuildingEffectEnum, BuildingEffectIncrementType, float, float, float>(BuildingEffectEnum.GarrisonWageReduction, BuildingEffectIncrementType.AddFactor, -0.05f, -0.1f, -0.15f)
		}, isMilitaryProject: true, 0f);
		_buildingSettlementTrainingFields.Initialize(new TextObject("{=BkTiRPT4}Training Fields"), new TextObject("{=NYzORuQm}Provides experience for garrison troops and increases militia veterancy."), new int[3] { 1500, 2100, 2700 }, new Tuple<BuildingEffectEnum, BuildingEffectIncrementType, float, float, float>[2]
		{
			new Tuple<BuildingEffectEnum, BuildingEffectIncrementType, float, float, float>(BuildingEffectEnum.ExperiencePerDay, BuildingEffectIncrementType.Add, 1f, 2f, 3f),
			new Tuple<BuildingEffectEnum, BuildingEffectIncrementType, float, float, float>(BuildingEffectEnum.MilitiaVeterancyChance, BuildingEffectIncrementType.Add, 0.1f, 0.15f, 0.2f)
		}, isMilitaryProject: true, 0f);
		_buildingSettlementGuardHouse.Initialize(new TextObject("{=OHEiwoHC}Guard House"), new TextObject("{=doojtAwr}Increases prisoner limit and provides a patrol party that improves security."), new int[3] { 1500, 2100, 2700 }, new Tuple<BuildingEffectEnum, BuildingEffectIncrementType, float, float, float>[2]
		{
			new Tuple<BuildingEffectEnum, BuildingEffectIncrementType, float, float, float>(BuildingEffectEnum.PatrolPartyStrength, BuildingEffectIncrementType.Add, 1f, 2f, 3f),
			new Tuple<BuildingEffectEnum, BuildingEffectIncrementType, float, float, float>(BuildingEffectEnum.GarrisonCapacity, BuildingEffectIncrementType.Add, 60f, 90f, 120f),
			new Tuple<BuildingEffectEnum, BuildingEffectIncrementType, float, float, float>(BuildingEffectEnum.PrisonCapacity, BuildingEffectIncrementType.Add, 50f, 75f, 100f)
		}, isMilitaryProject: true, 0f, 1);
		_buildingSettlementBarracks.Initialize(new TextObject("{=x2B0OjhI}Barracks"), new TextObject("{=JalrbDBC}Lodgings for garrison troops. Each level increases garrison limit and decreases garrison wage."), new int[3] { 1800, 3000, 4200 }, new Tuple<BuildingEffectEnum, BuildingEffectIncrementType, float, float, float>[2]
		{
E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\TaleWorlds.CampaignSystem.Settlements.Buildings\DefaultBuildingTypes.cs:192: 			new Tuple<BuildingEffectEnum, BuildingEffectIncrementType, float, float, float>(BuildingEffectEnum.GarrisonCapacity, BuildingEffectIncrementType.Add, 60f, 90f, 120f),
			new Tuple<BuildingEffectEnum, BuildingEffectIncrementType, float, float, float>(BuildingEffectEnum.GarrisonWageReduction, BuildingEffectIncrementType.AddFactor, -0.05f, -0.1f, -0.15f)
		}, isMilitaryProject: true, 0f);
		_buildingSettlementTrainingFields.Initialize(new TextObject("{=BkTiRPT4}Training Fields"), new TextObject("{=NYzORuQm}Provides experience for garrison troops and increases militia veterancy."), new int[3] { 1500, 2100, 2700 }, new Tuple<BuildingEffectEnum, BuildingEffectIncrementType, float, float, float>[2]
		{
			new Tuple<BuildingEffectEnum, BuildingEffectIncrementType, float, float, float>(BuildingEffectEnum.ExperiencePerDay, BuildingEffectIncrementType.Add, 1f, 2f, 3f),
			new Tuple<BuildingEffectEnum, BuildingEffectIncrementType, float, float, float>(BuildingEffectEnum.MilitiaVeterancyChance, BuildingEffectIncrementType.Add, 0.1f, 0.15f, 0.2f)
		}, isMilitaryProject: true, 0f);
		_buildingSettlementGuardHouse.Initialize(new TextObject("{=OHEiwoHC}Guard House"), new TextObject("{=doojtAwr}Increases prisoner limit and provides a patrol party that improves security."), new int[3] { 1500, 2100, 2700 }, new Tuple<BuildingEffectEnum, BuildingEffectIncrementType, float, float, float>[2]
		{
			new Tuple<BuildingEffectEnum, BuildingEffectIncrementType, float, float, float>(BuildingEffectEnum.PatrolPartyStrength, BuildingEffectIncrementType.Add, 1f, 2f, 3f),
			new Tuple<BuildingEffectEnum, BuildingEffectIncrementType, float, float, float>(BuildingEffectEnum.PrisonCapacity, BuildingEffectIncrementType.Add, 30f, 60f, 90f)
		}, isMilitaryProject: true, 0f);
			new Tuple<BuildingEffectEnum, BuildingEffectIncrementType, float, float, float>(BuildingEffectEnum.PrisonCapacity, BuildingEffectIncrementType.Add, 50f, 75f, 100f)
		}, isMilitaryProject: true, 0f, 1);
		_buildingSettlementBarracks.Initialize(new TextObject("{=x2B0OjhI}Barracks"), new TextObject("{=JalrbDBC}Lodgings for garrison troops. Each level increases garrison limit and decreases garrison wage."), new int[3] { 1800, 3000, 4200 }, new Tuple<BuildingEffectEnum, BuildingEffectIncrementType, float, float, float>[2]
		{
			new Tuple<BuildingEffectEnum, BuildingEffectIncrementType, float, float, float>(BuildingEffectEnum.GarrisonCapacity, BuildingEffectIncrementType.Add, 60f, 90f, 120f),
E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\TaleWorlds.CampaignSystem.Settlements.Buildings\DefaultBuildingTypes.cs:193: 			new Tuple<BuildingEffectEnum, BuildingEffectIncrementType, float, float, float>(BuildingEffectEnum.GarrisonWageReduction, BuildingEffectIncrementType.AddFactor, -0.05f, -0.1f, -0.15f)
		}, isMilitaryProject: true, 0f);
		_buildingSettlementTrainingFields.Initialize(new TextObject("{=BkTiRPT4}Training Fields"), new TextObject("{=NYzORuQm}Provides experience for garrison troops and increases militia veterancy."), new int[3] { 1500, 2100, 2700 }, new Tuple<BuildingEffectEnum, BuildingEffectIncrementType, float, float, float>[2]
		{
			new Tuple<BuildingEffectEnum, BuildingEffectIncrementType, float, float, float>(BuildingEffectEnum.ExperiencePerDay, BuildingEffectIncrementType.Add, 1f, 2f, 3f),
			new Tuple<BuildingEffectEnum, BuildingEffectIncrementType, float, float, float>(BuildingEffectEnum.MilitiaVeterancyChance, BuildingEffectIncrementType.Add, 0.1f, 0.15f, 0.2f)
		}, isMilitaryProject: true, 0f);
		_buildingSettlementGuardHouse.Initialize(new TextObject("{=OHEiwoHC}Guard House"), new TextObject("{=doojtAwr}Increases prisoner limit and provides a patrol party that improves security."), new int[3] { 1500, 2100, 2700 }, new Tuple<BuildingEffectEnum, BuildingEffectIncrementType, float, float, float>[2]
		{
			new Tuple<BuildingEffectEnum, BuildingEffectIncrementType, float, float, float>(BuildingEffectEnum.PatrolPartyStrength, BuildingEffectIncrementType.Add, 1f, 2f, 3f),
			new Tuple<BuildingEffectEnum, BuildingEffectIncrementType, float, float, float>(BuildingEffectEnum.PrisonCapacity, BuildingEffectIncrementType.Add, 30f, 60f, 90f)
		}, isMilitaryProject: true, 0f);
		_buildingSettlementSiegeWorkshop.Initialize(new TextObject("{=9Bnwttn6}Siege Workshop"), new TextObject("{=MharAceZ}Builds and maintains siege engines for defense of the settlement."), new int[3] { 1200, 1800, 3000 }, new Tuple<BuildingEffectEnum, BuildingEffectIncrementType, float, float, float>[3]
		_buildingSettlementBarracks.Initialize(new TextObject("{=x2B0OjhI}Barracks"), new TextObject("{=JalrbDBC}Lodgings for garrison troops. Each level increases garrison limit and decreases garrison wage."), new int[3] { 1800, 3000, 4200 }, new Tuple<BuildingEffectEnum, BuildingEffectIncrementType, float, float, float>[2]
		{
			new Tuple<BuildingEffectEnum, BuildingEffectIncrementType, float, float, float>(BuildingEffectEnum.GarrisonCapacity, BuildingEffectIncrementType.Add, 60f, 90f, 120f),
			new Tuple<BuildingEffectEnum, BuildingEffectIncrementType, float, float, float>(BuildingEffectEnum.GarrisonWageReduction, BuildingEffectIncrementType.AddFactor, -0.05f, -0.1f, -0.15f)
		}, isMilitaryProject: true, 0f);
E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\TaleWorlds.CampaignSystem.Settlements.Buildings\DefaultBuildingTypes.cs:195: 		_buildingSettlementTrainingFields.Initialize(new TextObject("{=BkTiRPT4}Training Fields"), new TextObject("{=NYzORuQm}Provides experience for garrison troops and increases militia veterancy."), new int[3] { 1500, 2100, 2700 }, new Tuple<BuildingEffectEnum, BuildingEffectIncrementType, float, float, float>[2]
		{
			new Tuple<BuildingEffectEnum, BuildingEffectIncrementType, float, float, float>(BuildingEffectEnum.ExperiencePerDay, BuildingEffectIncrementType.Add, 1f, 2f, 3f),
			new Tuple<BuildingEffectEnum, BuildingEffectIncrementType, float, float, float>(BuildingEffectEnum.MilitiaVeterancyChance, BuildingEffectIncrementType.Add, 0.1f, 0.15f, 0.2f)
		}, isMilitaryProject: true, 0f);
		_buildingSettlementGuardHouse.Initialize(new TextObject("{=OHEiwoHC}Guard House"), new TextObject("{=doojtAwr}Increases prisoner limit and provides a patrol party that improves security."), new int[3] { 1500, 2100, 2700 }, new Tuple<BuildingEffectEnum, BuildingEffectIncrementType, float, float, float>[2]
		{
			new Tuple<BuildingEffectEnum, BuildingEffectIncrementType, float, float, float>(BuildingEffectEnum.PatrolPartyStrength, BuildingEffectIncrementType.Add, 1f, 2f, 3f),
			new Tuple<BuildingEffectEnum, BuildingEffectIncrementType, float, float, float>(BuildingEffectEnum.PrisonCapacity, BuildingEffectIncrementType.Add, 30f, 60f, 90f)
		}, isMilitaryProject: true, 0f);
		_buildingSettlementSiegeWorkshop.Initialize(new TextObject("{=9Bnwttn6}Siege Workshop"), new TextObject("{=MharAceZ}Builds and maintains siege engines for defense of the settlement."), new int[3] { 1200, 1800, 3000 }, new Tuple<BuildingEffectEnum, BuildingEffectIncrementType, float, float, float>[3]
		{
			new Tuple<BuildingEffectEnum, BuildingEffectIncrementType, float, float, float>(BuildingEffectEnum.BallistaOnSiegeStart, BuildingEffectIncrementType.Add, 1f, 1f, 2f),
			new Tuple<BuildingEffectEnum, BuildingEffectIncrementType, float, float, float>(BuildingEffectEnum.GarrisonCapacity, BuildingEffectIncrementType.Add, 60f, 90f, 120f),
			new Tuple<BuildingEffectEnum, BuildingEffectIncrementType, float, float, float>(BuildingEffectEnum.GarrisonWageReduction, BuildingEffectIncrementType.AddFactor, -0.05f, -0.1f, -0.15f)
		}, isMilitaryProject: true, 0f);
		_buildingSettlementTrainingFields.Initialize(new TextObject("{=BkTiRPT4}Training Fields"), new TextObject("{=NYzORuQm}Provides experience for garrison troops and increases militia veterancy."), new int[3] { 1500, 2100, 2700 }, new Tuple<BuildingEffectEnum, BuildingEffectIncrementType, float, float, float>[2]
		{
E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\TaleWorlds.CampaignSystem.Settlements.Buildings\DefaultBuildingTypes.cs:197: 			new Tuple<BuildingEffectEnum, BuildingEffectIncrementType, float, float, float>(BuildingEffectEnum.ExperiencePerDay, BuildingEffectIncrementType.Add, 1f, 2f, 3f),
			new Tuple<BuildingEffectEnum, BuildingEffectIncrementType, float, float, float>(BuildingEffectEnum.MilitiaVeterancyChance, BuildingEffectIncrementType.Add, 0.1f, 0.15f, 0.2f)
		}, isMilitaryProject: true, 0f);
		_buildingSettlementGuardHouse.Initialize(new TextObject("{=OHEiwoHC}Guard House"), new TextObject("{=doojtAwr}Increases prisoner limit and provides a patrol party that improves security."), new int[3] { 1500, 2100, 2700 }, new Tuple<BuildingEffectEnum, BuildingEffectIncrementType, float, float, float>[2]
		{
			new Tuple<BuildingEffectEnum, BuildingEffectIncrementType, float, float, float>(BuildingEffectEnum.PatrolPartyStrength, BuildingEffectIncrementType.Add, 1f, 2f, 3f),
			new Tuple<BuildingEffectEnum, BuildingEffectIncrementType, float, float, float>(BuildingEffectEnum.PrisonCapacity, BuildingEffectIncrementType.Add, 30f, 60f, 90f)
		}, isMilitaryProject: true, 0f);
		_buildingSettlementSiegeWorkshop.Initialize(new TextObject("{=9Bnwttn6}Siege Workshop"), new TextObject("{=MharAceZ}Builds and maintains siege engines for defense of the settlement."), new int[3] { 1200, 1800, 3000 }, new Tuple<BuildingEffectEnum, BuildingEffectIncrementType, float, float, float>[3]
		{
			new Tuple<BuildingEffectEnum, BuildingEffectIncrementType, float, float, float>(BuildingEffectEnum.BallistaOnSiegeStart, BuildingEffectIncrementType.Add, 1f, 1f, 2f),
			new Tuple<BuildingEffectEnum, BuildingEffectIncrementType, float, float, float>(BuildingEffectEnum.CatapultOnSiegeStart, BuildingEffectIncrementType.Add, 0f, 1f, 1f),
			new Tuple<BuildingEffectEnum, BuildingEffectIncrementType, float, float, float>(BuildingEffectEnum.SiegeEngineSpeed, BuildingEffectIncrementType.AddFactor, 0.3f, 0.6f, 1f)
			new Tuple<BuildingEffectEnum, BuildingEffectIncrementType, float, float, float>(BuildingEffectEnum.GarrisonWageReduction, BuildingEffectIncrementType.AddFactor, -0.05f, -0.1f, -0.15f)
		}, isMilitaryProject: true, 0f);
		_buildingSettlementTrainingFields.Initialize(new TextObject("{=BkTiRPT4}Training Fields"), new TextObject("{=NYzORuQm}Provides experience for garrison troops and increases militia veterancy."), new int[3] { 1500, 2100, 2700 }, new Tuple<BuildingEffectEnum, BuildingEffectIncrementType, float, float, float>[2]
		{
			new Tuple<BuildingEffectEnum, BuildingEffectIncrementType, float, float, float>(BuildingEffectEnum.ExperiencePerDay, BuildingEffectIncrementType.Add, 1f, 2f, 3f),
E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\TaleWorlds.CampaignSystem.Settlements.Buildings\DefaultBuildingTypes.cs:198: 			new Tuple<BuildingEffectEnum, BuildingEffectIncrementType, float, float, float>(BuildingEffectEnum.MilitiaVeterancyChance, BuildingEffectIncrementType.Add, 0.1f, 0.15f, 0.2f)
		}, isMilitaryProject: true, 0f);
		_buildingSettlementGuardHouse.Initialize(new TextObject("{=OHEiwoHC}Guard House"), new TextObject("{=doojtAwr}Increases prisoner limit and provides a patrol party that improves security."), new int[3] { 1500, 2100, 2700 }, new Tuple<BuildingEffectEnum, BuildingEffectIncrementType, float, float, float>[2]
		{
			new Tuple<BuildingEffectEnum, BuildingEffectIncrementType, float, float, float>(BuildingEffectEnum.PatrolPartyStrength, BuildingEffectIncrementType.Add, 1f, 2f, 3f),
			new Tuple<BuildingEffectEnum, BuildingEffectIncrementType, float, float, float>(BuildingEffectEnum.PrisonCapacity, BuildingEffectIncrementType.Add, 30f, 60f, 90f)
		}, isMilitaryProject: true, 0f);
		_buildingSettlementSiegeWorkshop.Initialize(new TextObject("{=9Bnwttn6}Siege Workshop"), new TextObject("{=MharAceZ}Builds and maintains siege engines for defense of the settlement."), new int[3] { 1200, 1800, 3000 }, new Tuple<BuildingEffectEnum, BuildingEffectIncrementType, float, float, float>[3]
		{
			new Tuple<BuildingEffectEnum, BuildingEffectIncrementType, float, float, float>(BuildingEffectEnum.BallistaOnSiegeStart, BuildingEffectIncrementType.Add, 1f, 1f, 2f),
			new Tuple<BuildingEffectEnum, BuildingEffectIncrementType, float, float, float>(BuildingEffectEnum.CatapultOnSiegeStart, BuildingEffectIncrementType.Add, 0f, 1f, 1f),
			new Tuple<BuildingEffectEnum, BuildingEffectIncrementType, float, float, float>(BuildingEffectEnum.SiegeEngineSpeed, BuildingEffectIncrementType.AddFactor, 0.3f, 0.6f, 1f)
		}, isMilitaryProject: false, 0f);
		_buildingSettlementTrainingFields.Initialize(new TextObject("{=BkTiRPT4}Training Fields"), new TextObject("{=NYzORuQm}Provides experience for garrison troops and increases militia veterancy."), new int[3] { 1500, 2100, 2700 }, new Tuple<BuildingEffectEnum, BuildingEffectIncrementType, float, float, float>[2]
		{
			new Tuple<BuildingEffectEnum, BuildingEffectIncrementType, float, float, float>(BuildingEffectEnum.ExperiencePerDay, BuildingEffectIncrementType.Add, 1f, 2f, 3f),
			new Tuple<BuildingEffectEnum, BuildingEffectIncrementType, float, float, float>(BuildingEffectEnum.MilitiaVeterancyChance, BuildingEffectIncrementType.Add, 0.1f, 0.15f, 0.2f)
		}, isMilitaryProject: true, 0f);
E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\TaleWorlds.CampaignSystem.Settlements.Buildings\DefaultBuildingTypes.cs:200: 		_buildingSettlementGuardHouse.Initialize(new TextObject("{=OHEiwoHC}Guard House"), new TextObject("{=doojtAwr}Increases prisoner limit and provides a patrol party that improves security."), new int[3] { 1500, 2100, 2700 }, new Tuple<BuildingEffectEnum, BuildingEffectIncrementType, float, float, float>[2]
		{
			new Tuple<BuildingEffectEnum, BuildingEffectIncrementType, float, float, float>(BuildingEffectEnum.PatrolPartyStrength, BuildingEffectIncrementType.Add, 1f, 2f, 3f),
			new Tuple<BuildingEffectEnum, BuildingEffectIncrementType, float, float, float>(BuildingEffectEnum.PrisonCapacity, BuildingEffectIncrementType.Add, 30f, 60f, 90f)
		}, isMilitaryProject: true, 0f);
		_buildingSettlementSiegeWorkshop.Initialize(new TextObject("{=9Bnwttn6}Siege Workshop"), new TextObject("{=MharAceZ}Builds and maintains siege engines for defense of the settlement."), new int[3] { 1200, 1800, 3000 }, new Tuple<BuildingEffectEnum, BuildingEffectIncrementType, float, float, float>[3]
		{
			new Tuple<BuildingEffectEnum, BuildingEffectIncrementType, float, float, float>(BuildingEffectEnum.BallistaOnSiegeStart, BuildingEffectIncrementType.Add, 1f, 1f, 2f),
			new Tuple<BuildingEffectEnum, BuildingEffectIncrementType, float, float, float>(BuildingEffectEnum.CatapultOnSiegeStart, BuildingEffectIncrementType.Add, 0f, 1f, 1f),
			new Tuple<BuildingEffectEnum, BuildingEffectIncrementType, float, float, float>(BuildingEffectEnum.SiegeEngineSpeed, BuildingEffectIncrementType.AddFactor, 0.3f, 0.6f, 1f)
		}, isMilitaryProject: false, 0f);
		_buildingSettlementTaxOffice.Initialize(new TextObject("{=LG84byW0}Tax Office"), new TextObject("{=nQ6ytZeF}Increases tax income."), new int[3] { 1800, 3000, 4200 }, new Tuple<BuildingEffectEnum, BuildingEffectIncrementType, float, float, float>[1]
		{
			new Tuple<BuildingEffectEnum, BuildingEffectIncrementType, float, float, float>(BuildingEffectEnum.ExperiencePerDay, BuildingEffectIncrementType.Add, 1f, 2f, 3f),
			new Tuple<BuildingEffectEnum, BuildingEffectIncrementType, float, float, float>(BuildingEffectEnum.MilitiaVeterancyChance, BuildingEffectIncrementType.Add, 0.1f, 0.15f, 0.2f)
		}, isMilitaryProject: true, 0f);
		_buildingSettlementGuardHouse.Initialize(new TextObject("{=OHEiwoHC}Guard House"), new TextObject("{=doojtAwr}Increases prisoner limit and provides a patrol party that improves security."), new int[3] { 1500, 2100, 2700 }, new Tuple<BuildingEffectEnum, BuildingEffectIncrementType, float, float, float>[2]
		{
E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\TaleWorlds.CampaignSystem.Settlements.Buildings\DefaultBuildingTypes.cs:202: 			new Tuple<BuildingEffectEnum, BuildingEffectIncrementType, float, float, float>(BuildingEffectEnum.PatrolPartyStrength, BuildingEffectIncrementType.Add, 1f, 2f, 3f),
			new Tuple<BuildingEffectEnum, BuildingEffectIncrementType, float, float, float>(BuildingEffectEnum.PrisonCapacity, BuildingEffectIncrementType.Add, 30f, 60f, 90f)
		}, isMilitaryProject: true, 0f);
		_buildingSettlementSiegeWorkshop.Initialize(new TextObject("{=9Bnwttn6}Siege Workshop"), new TextObject("{=MharAceZ}Builds and maintains siege engines for defense of the settlement."), new int[3] { 1200, 1800, 3000 }, new Tuple<BuildingEffectEnum, BuildingEffectIncrementType, float, float, float>[3]
		{
			new Tuple<BuildingEffectEnum, BuildingEffectIncrementType, float, float, float>(BuildingEffectEnum.BallistaOnSiegeStart, BuildingEffectIncrementType.Add, 1f, 1f, 2f),
			new Tuple<BuildingEffectEnum, BuildingEffectIncrementType, float, float, float>(BuildingEffectEnum.CatapultOnSiegeStart, BuildingEffectIncrementType.Add, 0f, 1f, 1f),
			new Tuple<BuildingEffectEnum, BuildingEffectIncrementType, float, float, float>(BuildingEffectEnum.SiegeEngineSpeed, BuildingEffectIncrementType.AddFactor, 0.3f, 0.6f, 1f)
		}, isMilitaryProject: false, 0f);
		_buildingSettlementTaxOffice.Initialize(new TextObject("{=LG84byW0}Tax Office"), new TextObject("{=nQ6ytZeF}Increases tax income."), new int[3] { 1800, 3000, 4200 }, new Tuple<BuildingEffectEnum, BuildingEffectIncrementType, float, float, float>[1]
		{
			new Tuple<BuildingEffectEnum, BuildingEffectIncrementType, float, float, float>(BuildingEffectEnum.TaxPerDay, BuildingEffectIncrementType.AddFactor, 0.05f, 0.1f, 0.15f)
		}, isMilitaryProject: false, 0f);
			new Tuple<BuildingEffectEnum, BuildingEffectIncrementType, float, float, float>(BuildingEffectEnum.MilitiaVeterancyChance, BuildingEffectIncrementType.Add, 0.1f, 0.15f, 0.2f)
		}, isMilitaryProject: true, 0f);
		_buildingSettlementGuardHouse.Initialize(new TextObject("{=OHEiwoHC}Guard House"), new TextObject("{=doojtAwr}Increases prisoner limit and provides a patrol party that improves security."), new int[3] { 1500, 2100, 2700 }, new Tuple<BuildingEffectEnum, BuildingEffectIncrementType, float, float, float>[2]
		{
			new Tuple<BuildingEffectEnum, BuildingEffectIncrementType, float, float, float>(BuildingEffectEnum.PatrolPartyStrength, BuildingEffectIncrementType.Add, 1f, 2f, 3f),
E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\TaleWorlds.CampaignSystem.Settlements.Buildings\DefaultBuildingTypes.cs:203: 			new Tuple<BuildingEffectEnum, BuildingEffectIncrementType, float, float, float>(BuildingEffectEnum.PrisonCapacity, BuildingEffectIncrementType.Add, 30f, 60f, 90f)
		}, isMilitaryProject: true, 0f);
		_buildingSettlementSiegeWorkshop.Initialize(new TextObject("{=9Bnwttn6}Siege Workshop"), new TextObject("{=MharAceZ}Builds and maintains siege engines for defense of the settlement."), new int[3] { 1200, 1800, 3000 }, new Tuple<BuildingEffectEnum, BuildingEffectIncrementType, float, float, float>[3]
		{
			new Tuple<BuildingEffectEnum, BuildingEffectIncrementType, float, float, float>(BuildingEffectEnum.BallistaOnSiegeStart, BuildingEffectIncrementType.Add, 1f, 1f, 2f),
			new Tuple<BuildingEffectEnum, BuildingEffectIncrementType, float, float, float>(BuildingEffectEnum.CatapultOnSiegeStart, BuildingEffectIncrementType.Add, 0f, 1f, 1f),
			new Tuple<BuildingEffectEnum, BuildingEffectIncrementType, float, float, float>(BuildingEffectEnum.SiegeEngineSpeed, BuildingEffectIncrementType.AddFactor, 0.3f, 0.6f, 1f)
		}, isMilitaryProject: false, 0f);
		_buildingSettlementTaxOffice.Initialize(new TextObject("{=LG84byW0}Tax Office"), new TextObject("{=nQ6ytZeF}Increases tax income."), new int[3] { 1800, 3000, 4200 }, new Tuple<BuildingEffectEnum, BuildingEffectIncrementType, float, float, float>[1]
		{
			new Tuple<BuildingEffectEnum, BuildingEffectIncrementType, float, float, float>(BuildingEffectEnum.TaxPerDay, BuildingEffectIncrementType.AddFactor, 0.05f, 0.1f, 0.15f)
		}, isMilitaryProject: false, 0f);
		_buildingSettlementMarketplace.Initialize(new TextObject("{=zLdXCpne}Marketplace"), new TextObject("{=Z0xf3Bbd}Increases the tariff collected from trades made in town"), new int[3] { 2400, 3600, 4800 }, new Tuple<BuildingEffectEnum, BuildingEffectIncrementType, float, float, float>[2]
		_buildingSettlementGuardHouse.Initialize(new TextObject("{=OHEiwoHC}Guard House"), new TextObject("{=doojtAwr}Increases prisoner limit and provides a patrol party that improves security."), new int[3] { 1500, 2100, 2700 }, new Tuple<BuildingEffectEnum, BuildingEffectIncrementType, float, float, float>[2]
		{
			new Tuple<BuildingEffectEnum, BuildingEffectIncrementType, float, float, float>(BuildingEffectEnum.PatrolPartyStrength, BuildingEffectIncrementType.Add, 1f, 2f, 3f),
			new Tuple<BuildingEffectEnum, BuildingEffectIncrementType, float, float, float>(BuildingEffectEnum.PrisonCapacity, BuildingEffectIncrementType.Add, 30f, 60f, 90f)
		}, isMilitaryProject: true, 0f);
E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\TaleWorlds.CampaignSystem.Settlements.Buildings\DefaultBuildingTypes.cs:205: 		_buildingSettlementSiegeWorkshop.Initialize(new TextObject("{=9Bnwttn6}Siege Workshop"), new TextObject("{=MharAceZ}Builds and maintains siege engines for defense of the settlement."), new int[3] { 1200, 1800, 3000 }, new Tuple<BuildingEffectEnum, BuildingEffectIncrementType, float, float, float>[3]
		{
			new Tuple<BuildingEffectEnum, BuildingEffectIncrementType, float, float, float>(BuildingEffectEnum.BallistaOnSiegeStart, BuildingEffectIncrementType.Add, 1f, 1f, 2f),
			new Tuple<BuildingEffectEnum, BuildingEffectIncrementType, float, float, float>(BuildingEffectEnum.CatapultOnSiegeStart, BuildingEffectIncrementType.Add, 0f, 1f, 1f),
			new Tuple<BuildingEffectEnum, BuildingEffectIncrementType, float, float, float>(BuildingEffectEnum.SiegeEngineSpeed, BuildingEffectIncrementType.AddFactor, 0.3f, 0.6f, 1f)
		}, isMilitaryProject: false, 0f);
		_buildingSettlementTaxOffice.Initialize(new TextObject("{=LG84byW0}Tax Office"), new TextObject("{=nQ6ytZeF}Increases tax income."), new int[3] { 1800, 3000, 4200 }, new Tuple<BuildingEffectEnum, BuildingEffectIncrementType, float, float, float>[1]
		{
			new Tuple<BuildingEffectEnum, BuildingEffectIncrementType, float, float, float>(BuildingEffectEnum.TaxPerDay, BuildingEffectIncrementType.AddFactor, 0.05f, 0.1f, 0.15f)
		}, isMilitaryProject: false, 0f);
		_buildingSettlementMarketplace.Initialize(new TextObject("{=zLdXCpne}Marketplace"), new TextObject("{=Z0xf3Bbd}Increases the tariff collected from trades made in town"), new int[3] { 2400, 3600, 4800 }, new Tuple<BuildingEffectEnum, BuildingEffectIncrementType, float, float, float>[2]
		{
			new Tuple<BuildingEffectEnum, BuildingEffectIncrementType, float, float, float>(BuildingEffectEnum.TariffIncome, BuildingEffectIncrementType.AddFactor, 0.1f, 0.2f, 0.3f),
			new Tuple<BuildingEffectEnum, BuildingEffectIncrementType, float, float, float>(BuildingEffectEnum.PatrolPartyStrength, BuildingEffectIncrementType.Add, 1f, 2f, 3f),
			new Tuple<BuildingEffectEnum, BuildingEffectIncrementType, float, float, float>(BuildingEffectEnum.PrisonCapacity, BuildingEffectIncrementType.Add, 30f, 60f, 90f)
		}, isMilitaryProject: true, 0f);
		_buildingSettlementSiegeWorkshop.Initialize(new TextObject("{=9Bnwttn6}Siege Workshop"), new TextObject("{=MharAceZ}Builds and maintains siege engines for defense of the settlement."), new int[3] { 1200, 1800, 3000 }, new Tuple<BuildingEffectEnum, BuildingEffectIncrementType, float, float, float>[3]
		{
E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\TaleWorlds.CampaignSystem.Settlements.Buildings\DefaultBuildingTypes.cs:207: 			new Tuple<BuildingEffectEnum, BuildingEffectIncrementType, float, float, float>(BuildingEffectEnum.BallistaOnSiegeStart, BuildingEffectIncrementType.Add, 1f, 1f, 2f),
			new Tuple<BuildingEffectEnum, BuildingEffectIncrementType, float, float, float>(BuildingEffectEnum.CatapultOnSiegeStart, BuildingEffectIncrementType.Add, 0f, 1f, 1f),
			new Tuple<BuildingEffectEnum, BuildingEffectIncrementType, float, float, float>(BuildingEffectEnum.SiegeEngineSpeed, BuildingEffectIncrementType.AddFactor, 0.3f, 0.6f, 1f)
		}, isMilitaryProject: false, 0f);
		_buildingSettlementTaxOffice.Initialize(new TextObject("{=LG84byW0}Tax Office"), new TextObject("{=nQ6ytZeF}Increases tax income."), new int[3] { 1800, 3000, 4200 }, new Tuple<BuildingEffectEnum, BuildingEffectIncrementType, float, float, float>[1]
		{
			new Tuple<BuildingEffectEnum, BuildingEffectIncrementType, float, float, float>(BuildingEffectEnum.TaxPerDay, BuildingEffectIncrementType.AddFactor, 0.05f, 0.1f, 0.15f)
		}, isMilitaryProject: false, 0f);
		_buildingSettlementMarketplace.Initialize(new TextObject("{=zLdXCpne}Marketplace"), new TextObject("{=Z0xf3Bbd}Increases the tariff collected from trades made in town"), new int[3] { 2400, 3600, 4800 }, new Tuple<BuildingEffectEnum, BuildingEffectIncrementType, float, float, float>[2]
		{
			new Tuple<BuildingEffectEnum, BuildingEffectIncrementType, float, float, float>(BuildingEffectEnum.TariffIncome, BuildingEffectIncrementType.AddFactor, 0.1f, 0.2f, 0.3f),
			new Tuple<BuildingEffectEnum, BuildingEffectIncrementType, float, float, float>(BuildingEffectEnum.CaravanAccessibility, BuildingEffectIncrementType.AddFactor, 1.02f, 1.04f, 1.06f)
		}, isMilitaryProject: false, 0f);
			new Tuple<BuildingEffectEnum, BuildingEffectIncrementType, float, float, float>(BuildingEffectEnum.PrisonCapacity, BuildingEffectIncrementType.Add, 30f, 60f, 90f)
		}, isMilitaryProject: true, 0f);
		_buildingSettlementSiegeWorkshop.Initialize(new TextObject("{=9Bnwttn6}Siege Workshop"), new TextObject("{=MharAceZ}Builds and maintains siege engines for defense of the settlement."), new int[3] { 1200, 1800, 3000 }, new Tuple<BuildingEffectEnum, BuildingEffectIncrementType, float, float, float>[3]
		{
			new Tuple<BuildingEffectEnum, BuildingEffectIncrementType, float, float, float>(BuildingEffectEnum.BallistaOnSiegeStart, BuildingEffectIncrementType.Add, 1f, 1f, 2f),
E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\TaleWorlds.CampaignSystem.Settlements.Buildings\DefaultBuildingTypes.cs:208: 			new Tuple<BuildingEffectEnum, BuildingEffectIncrementType, float, float, float>(BuildingEffectEnum.CatapultOnSiegeStart, BuildingEffectIncrementType.Add, 0f, 1f, 1f),
			new Tuple<BuildingEffectEnum, BuildingEffectIncrementType, float, float, float>(BuildingEffectEnum.SiegeEngineSpeed, BuildingEffectIncrementType.AddFactor, 0.3f, 0.6f, 1f)
		}, isMilitaryProject: false, 0f);
		_buildingSettlementTaxOffice.Initialize(new TextObject("{=LG84byW0}Tax Office"), new TextObject("{=nQ6ytZeF}Increases tax income."), new int[3] { 1800, 3000, 4200 }, new Tuple<BuildingEffectEnum, BuildingEffectIncrementType, float, float, float>[1]
		{
			new Tuple<BuildingEffectEnum, BuildingEffectIncrementType, float, float, float>(BuildingEffectEnum.TaxPerDay, BuildingEffectIncrementType.AddFactor, 0.05f, 0.1f, 0.15f)
		}, isMilitaryProject: false, 0f);
		_buildingSettlementMarketplace.Initialize(new TextObject("{=zLdXCpne}Marketplace"), new TextObject("{=Z0xf3Bbd}Increases the tariff collected from trades made in town"), new int[3] { 2400, 3600, 4800 }, new Tuple<BuildingEffectEnum, BuildingEffectIncrementType, float, float, float>[2]
		{
			new Tuple<BuildingEffectEnum, BuildingEffectIncrementType, float, float, float>(BuildingEffectEnum.TariffIncome, BuildingEffectIncrementType.AddFactor, 0.1f, 0.2f, 0.3f),
			new Tuple<BuildingEffectEnum, BuildingEffectIncrementType, float, float, float>(BuildingEffectEnum.CaravanAccessibility, BuildingEffectIncrementType.AddFactor, 1.02f, 1.04f, 1.06f)
		}, isMilitaryProject: false, 0f);
		_buildingSettlementWarehouse.Initialize(new TextObject("{=anTRftmb}Warehouse"), new TextObject("{=hhKDZJeM}Increases Food storage limits and improves workshop productivity."), new int[3] { 1800, 2400, 3000 }, new Tuple<BuildingEffectEnum, BuildingEffectIncrementType, float, float, float>[2]
		}, isMilitaryProject: true, 0f);
		_buildingSettlementSiegeWorkshop.Initialize(new TextObject("{=9Bnwttn6}Siege Workshop"), new TextObject("{=MharAceZ}Builds and maintains siege engines for defense of the settlement."), new int[3] { 1200, 1800, 3000 }, new Tuple<BuildingEffectEnum, BuildingEffectIncrementType, float, float, float>[3]
		{
			new Tuple<BuildingEffectEnum, BuildingEffectIncrementType, float, float, float>(BuildingEffectEnum.BallistaOnSiegeStart, BuildingEffectIncrementType.Add, 1f, 1f, 2f),
			new Tuple<BuildingEffectEnum, BuildingEffectIncrementType, float, float, float>(BuildingEffectEnum.CatapultOnSiegeStart, BuildingEffectIncrementType.Add, 0f, 1f, 1f),
E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\TaleWorlds.CampaignSystem.Settlements.Buildings\DefaultBuildingTypes.cs:209: 			new Tuple<BuildingEffectEnum, BuildingEffectIncrementType, float, float, float>(BuildingEffectEnum.SiegeEngineSpeed, BuildingEffectIncrementType.AddFactor, 0.3f, 0.6f, 1f)
		}, isMilitaryProject: false, 0f);
		_buildingSettlementTaxOffice.Initialize(new TextObject("{=LG84byW0}Tax Office"), new TextObject("{=nQ6ytZeF}Increases tax income."), new int[3] { 1800, 3000, 4200 }, new Tuple<BuildingEffectEnum, BuildingEffectIncrementType, float, float, float>[1]
		{
			new Tuple<BuildingEffectEnum, BuildingEffectIncrementType, float, float, float>(BuildingEffectEnum.TaxPerDay, BuildingEffectIncrementType.AddFactor, 0.05f, 0.1f, 0.15f)
		}, isMilitaryProject: false, 0f);
		_buildingSettlementMarketplace.Initialize(new TextObject("{=zLdXCpne}Marketplace"), new TextObject("{=Z0xf3Bbd}Increases the tariff collected from trades made in town"), new int[3] { 2400, 3600, 4800 }, new Tuple<BuildingEffectEnum, BuildingEffectIncrementType, float, float, float>[2]
		{
			new Tuple<BuildingEffectEnum, BuildingEffectIncrementType, float, float, float>(BuildingEffectEnum.TariffIncome, BuildingEffectIncrementType.AddFactor, 0.1f, 0.2f, 0.3f),
			new Tuple<BuildingEffectEnum, BuildingEffectIncrementType, float, float, float>(BuildingEffectEnum.CaravanAccessibility, BuildingEffectIncrementType.AddFactor, 1.02f, 1.04f, 1.06f)
		}, isMilitaryProject: false, 0f);
		_buildingSettlementWarehouse.Initialize(new TextObject("{=anTRftmb}Warehouse"), new TextObject("{=hhKDZJeM}Increases Food storage limits and improves workshop productivity."), new int[3] { 1800, 2400, 3000 }, new Tuple<BuildingEffectEnum, BuildingEffectIncrementType, float, float, float>[2]
		{
		{
			new Tuple<BuildingEffectEnum, BuildingEffectIncrementType, float, float, float>(BuildingEffectEnum.BallistaOnSiegeStart, BuildingEffectIncrementType.Add, 1f, 1f, 2f),
			new Tuple<BuildingEffectEnum, BuildingEffectIncrementType, float, float, float>(BuildingEffectEnum.CatapultOnSiegeStart, BuildingEffectIncrementType.Add, 0f, 1f, 1f),
			new Tuple<BuildingEffectEnum, BuildingEffectIncrementType, float, float, float>(BuildingEffectEnum.SiegeEngineSpeed, BuildingEffectIncrementType.AddFactor, 0.3f, 0.6f, 1f)
		}, isMilitaryProject: false, 0f);
E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\TaleWorlds.CampaignSystem.Settlements.Buildings\DefaultBuildingTypes.cs:211: 		_buildingSettlementTaxOffice.Initialize(new TextObject("{=LG84byW0}Tax Office"), new TextObject("{=nQ6ytZeF}Increases tax income."), new int[3] { 1800, 3000, 4200 }, new Tuple<BuildingEffectEnum, BuildingEffectIncrementType, float, float, float>[1]
		{
			new Tuple<BuildingEffectEnum, BuildingEffectIncrementType, float, float, float>(BuildingEffectEnum.TaxPerDay, BuildingEffectIncrementType.AddFactor, 0.05f, 0.1f, 0.15f)
		}, isMilitaryProject: false, 0f);
		_buildingSettlementMarketplace.Initialize(new TextObject("{=zLdXCpne}Marketplace"), new TextObject("{=Z0xf3Bbd}Increases the tariff collected from trades made in town"), new int[3] { 2400, 3600, 4800 }, new Tuple<BuildingEffectEnum, BuildingEffectIncrementType, float, float, float>[2]
		{
			new Tuple<BuildingEffectEnum, BuildingEffectIncrementType, float, float, float>(BuildingEffectEnum.TariffIncome, BuildingEffectIncrementType.AddFactor, 0.1f, 0.2f, 0.3f),
			new Tuple<BuildingEffectEnum, BuildingEffectIncrementType, float, float, float>(BuildingEffectEnum.CaravanAccessibility, BuildingEffectIncrementType.AddFactor, 1.02f, 1.04f, 1.06f)
		}, isMilitaryProject: false, 0f);
		_buildingSettlementWarehouse.Initialize(new TextObject("{=anTRftmb}Warehouse"), new TextObject("{=hhKDZJeM}Increases Food storage limits and improves workshop productivity."), new int[3] { 1800, 2400, 3000 }, new Tuple<BuildingEffectEnum, BuildingEffectIncrementType, float, float, float>[2]
		{
			new Tuple<BuildingEffectEnum, BuildingEffectIncrementType, float, float, float>(BuildingEffectEnum.FoodStock, BuildingEffectIncrementType.Add, 100f, 300f, 500f),
			new Tuple<BuildingEffectEnum, BuildingEffectIncrementType, float, float, float>(BuildingEffectEnum.WorkshopProduction, BuildingEffectIncrementType.AddFactor, 0.05f, 0.1f, 0.15f)
			new Tuple<BuildingEffectEnum, BuildingEffectIncrementType, float, float, float>(BuildingEffectEnum.CatapultOnSiegeStart, BuildingEffectIncrementType.Add, 0f, 1f, 1f),
			new Tuple<BuildingEffectEnum, BuildingEffectIncrementType, float, float, float>(BuildingEffectEnum.SiegeEngineSpeed, BuildingEffectIncrementType.AddFactor, 0.3f, 0.6f, 1f)
		}, isMilitaryProject: false, 0f);
		_buildingSettlementTaxOffice.Initialize(new TextObject("{=LG84byW0}Tax Office"), new TextObject("{=nQ6ytZeF}Increases tax income."), new int[3] { 1800, 3000, 4200 }, new Tuple<BuildingEffectEnum, BuildingEffectIncrementType, float, float, float>[1]
		{
E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\TaleWorlds.CampaignSystem.Settlements.Buildings\DefaultBuildingTypes.cs:213: 			new Tuple<BuildingEffectEnum, BuildingEffectIncrementType, float, float, float>(BuildingEffectEnum.TaxPerDay, BuildingEffectIncrementType.AddFactor, 0.05f, 0.1f, 0.15f)
		}, isMilitaryProject: false, 0f);
		_buildingSettlementMarketplace.Initialize(new TextObject("{=zLdXCpne}Marketplace"), new TextObject("{=Z0xf3Bbd}Increases the tariff collected from trades made in town"), new int[3] { 2400, 3600, 4800 }, new Tuple<BuildingEffectEnum, BuildingEffectIncrementType, float, float, float>[2]
		{
			new Tuple<BuildingEffectEnum, BuildingEffectIncrementType, float, float, float>(BuildingEffectEnum.TariffIncome, BuildingEffectIncrementType.AddFactor, 0.1f, 0.2f, 0.3f),
			new Tuple<BuildingEffectEnum, BuildingEffectIncrementType, float, float, float>(BuildingEffectEnum.CaravanAccessibility, BuildingEffectIncrementType.AddFactor, 1.02f, 1.04f, 1.06f)
		}, isMilitaryProject: false, 0f);
		_buildingSettlementWarehouse.Initialize(new TextObject("{=anTRftmb}Warehouse"), new TextObject("{=hhKDZJeM}Increases Food storage limits and improves workshop productivity."), new int[3] { 1800, 2400, 3000 }, new Tuple<BuildingEffectEnum, BuildingEffectIncrementType, float, float, float>[2]
		{
			new Tuple<BuildingEffectEnum, BuildingEffectIncrementType, float, float, float>(BuildingEffectEnum.FoodStock, BuildingEffectIncrementType.Add, 100f, 300f, 500f),
			new Tuple<BuildingEffectEnum, BuildingEffectIncrementType, float, float, float>(BuildingEffectEnum.WorkshopProduction, BuildingEffectIncrementType.AddFactor, 0.05f, 0.1f, 0.15f)
		}, isMilitaryProject: false, 0f);
		_buildingSettlementMason.Initialize(new TextObject("{=R7ssoDHW}Mason"), new TextObject("{=hqUPvnaj}Increase bricks per day, increasing building and repair speed."), new int[3] { 2400, 3000, 4800 }, new Tuple<BuildingEffectEnum, BuildingEffectIncrementType, float, float, float>[2]
		}, isMilitaryProject: false, 0f);
		_buildingSettlementTaxOffice.Initialize(new TextObject("{=LG84byW0}Tax Office"), new TextObject("{=nQ6ytZeF}Increases tax income."), new int[3] { 1800, 3000, 4200 }, new Tuple<BuildingEffectEnum, BuildingEffectIncrementType, float, float, float>[1]
		{
			new Tuple<BuildingEffectEnum, BuildingEffectIncrementType, float, float, float>(BuildingEffectEnum.TaxPerDay, BuildingEffectIncrementType.AddFactor, 0.05f, 0.1f, 0.15f)
		}, isMilitaryProject: false, 0f);
E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\TaleWorlds.CampaignSystem.Settlements.Buildings\DefaultBuildingTypes.cs:215: 		_buildingSettlementMarketplace.Initialize(new TextObject("{=zLdXCpne}Marketplace"), new TextObject("{=Z0xf3Bbd}Increases the tariff collected from trades made in town"), new int[3] { 2400, 3600, 4800 }, new Tuple<BuildingEffectEnum, BuildingEffectIncrementType, float, float, float>[2]
		{
			new Tuple<BuildingEffectEnum, BuildingEffectIncrementType, float, float, float>(BuildingEffectEnum.TariffIncome, BuildingEffectIncrementType.AddFactor, 0.1f, 0.2f, 0.3f),
			new Tuple<BuildingEffectEnum, BuildingEffectIncrementType, float, float, float>(BuildingEffectEnum.CaravanAccessibility, BuildingEffectIncrementType.AddFactor, 1.02f, 1.04f, 1.06f)
		}, isMilitaryProject: false, 0f);
		_buildingSettlementWarehouse.Initialize(new TextObject("{=anTRftmb}Warehouse"), new TextObject("{=hhKDZJeM}Increases Food storage limits and improves workshop productivity."), new int[3] { 1800, 2400, 3000 }, new Tuple<BuildingEffectEnum, BuildingEffectIncrementType, float, float, float>[2]
		{
			new Tuple<BuildingEffectEnum, BuildingEffectIncrementType, float, float, float>(BuildingEffectEnum.FoodStock, BuildingEffectIncrementType.Add, 100f, 300f, 500f),
			new Tuple<BuildingEffectEnum, BuildingEffectIncrementType, float, float, float>(BuildingEffectEnum.WorkshopProduction, BuildingEffectIncrementType.AddFactor, 0.05f, 0.1f, 0.15f)
		}, isMilitaryProject: false, 0f);
		_buildingSettlementMason.Initialize(new TextObject("{=R7ssoDHW}Mason"), new TextObject("{=hqUPvnaj}Increase bricks per day, increasing building and repair speed."), new int[3] { 2400, 3000, 4800 }, new Tuple<BuildingEffectEnum, BuildingEffectIncrementType, float, float, float>[2]
		{
			new Tuple<BuildingEffectEnum, BuildingEffectIncrementType, float, float, float>(BuildingEffectEnum.ConstructionPerDay, BuildingEffectIncrementType.Add, 3f, 6f, 9f),
		{
			new Tuple<BuildingEffectEnum, BuildingEffectIncrementType, float, float, float>(BuildingEffectEnum.TaxPerDay, BuildingEffectIncrementType.AddFactor, 0.05f, 0.1f, 0.15f)
		}, isMilitaryProject: false, 0f);
		_buildingSettlementMarketplace.Initialize(new TextObject("{=zLdXCpne}Marketplace"), new TextObject("{=Z0xf3Bbd}Increases the tariff collected from trades made in town"), new int[3] { 2400, 3600, 4800 }, new Tuple<BuildingEffectEnum, BuildingEffectIncrementType, float, float, float>[2]
		{
E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\TaleWorlds.CampaignSystem.Settlements.Buildings\DefaultBuildingTypes.cs:217: 			new Tuple<BuildingEffectEnum, BuildingEffectIncrementType, float, float, float>(BuildingEffectEnum.TariffIncome, BuildingEffectIncrementType.AddFactor, 0.1f, 0.2f, 0.3f),
			new Tuple<BuildingEffectEnum, BuildingEffectIncrementType, float, float, float>(BuildingEffectEnum.CaravanAccessibility, BuildingEffectIncrementType.AddFactor, 1.02f, 1.04f, 1.06f)
		}, isMilitaryProject: false, 0f);
		_buildingSettlementWarehouse.Initialize(new TextObject("{=anTRftmb}Warehouse"), new TextObject("{=hhKDZJeM}Increases Food storage limits and improves workshop productivity."), new int[3] { 1800, 2400, 3000 }, new Tuple<BuildingEffectEnum, BuildingEffectIncrementType, float, float, float>[2]
		{
			new Tuple<BuildingEffectEnum, BuildingEffectIncrementType, float, float, float>(BuildingEffectEnum.FoodStock, BuildingEffectIncrementType.Add, 100f, 300f, 500f),
			new Tuple<BuildingEffectEnum, BuildingEffectIncrementType, float, float, float>(BuildingEffectEnum.WorkshopProduction, BuildingEffectIncrementType.AddFactor, 0.05f, 0.1f, 0.15f)
		}, isMilitaryProject: false, 0f);
		_buildingSettlementMason.Initialize(new TextObject("{=R7ssoDHW}Mason"), new TextObject("{=hqUPvnaj}Increase bricks per day, increasing building and repair speed."), new int[3] { 2400, 3000, 4800 }, new Tuple<BuildingEffectEnum, BuildingEffectIncrementType, float, float, float>[2]
		{
			new Tuple<BuildingEffectEnum, BuildingEffectIncrementType, float, float, float>(BuildingEffectEnum.ConstructionPerDay, BuildingEffectIncrementType.Add, 3f, 6f, 9f),
			new Tuple<BuildingEffectEnum, BuildingEffectIncrementType, float, float, float>(BuildingEffectEnum.WallRepairSpeed, BuildingEffectIncrementType.AddFactor, 0.1f, 0.2f, 0.3f)
		}, isMilitaryProject: false, 0f);
			new Tuple<BuildingEffectEnum, BuildingEffectIncrementType, float, float, float>(BuildingEffectEnum.TaxPerDay, BuildingEffectIncrementType.AddFactor, 0.05f, 0.1f, 0.15f)
		}, isMilitaryProject: false, 0f);
		_buildingSettlementMarketplace.Initialize(new TextObject("{=zLdXCpne}Marketplace"), new TextObject("{=Z0xf3Bbd}Increases the tariff collected from trades made in town"), new int[3] { 2400, 3600, 4800 }, new Tuple<BuildingEffectEnum, BuildingEffectIncrementType, float, float, float>[2]
		{
			new Tuple<BuildingEffectEnum, BuildingEffectIncrementType, float, float, float>(BuildingEffectEnum.TariffIncome, BuildingEffectIncrementType.AddFactor, 0.1f, 0.2f, 0.3f),
E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\TaleWorlds.CampaignSystem.Settlements.Buildings\DefaultBuildingTypes.cs:218: 			new Tuple<BuildingEffectEnum, BuildingEffectIncrementType, float, float, float>(BuildingEffectEnum.CaravanAccessibility, BuildingEffectIncrementType.AddFactor, 1.02f, 1.04f, 1.06f)
		}, isMilitaryProject: false, 0f);
		_buildingSettlementWarehouse.Initialize(new TextObject("{=anTRftmb}Warehouse"), new TextObject("{=hhKDZJeM}Increases Food storage limits and improves workshop productivity."), new int[3] { 1800, 2400, 3000 }, new Tuple<BuildingEffectEnum, BuildingEffectIncrementType, float, float, float>[2]
		{
			new Tuple<BuildingEffectEnum, BuildingEffectIncrementType, float, float, float>(BuildingEffectEnum.FoodStock, BuildingEffectIncrementType.Add, 100f, 300f, 500f),
			new Tuple<BuildingEffectEnum, BuildingEffectIncrementType, float, float, float>(BuildingEffectEnum.WorkshopProduction, BuildingEffectIncrementType.AddFactor, 0.05f, 0.1f, 0.15f)
		}, isMilitaryProject: false, 0f);
		_buildingSettlementMason.Initialize(new TextObject("{=R7ssoDHW}Mason"), new TextObject("{=hqUPvnaj}Increase bricks per day, increasing building and repair speed."), new int[3] { 2400, 3000, 4800 }, new Tuple<BuildingEffectEnum, BuildingEffectIncrementType, float, float, float>[2]
		{
			new Tuple<BuildingEffectEnum, BuildingEffectIncrementType, float, float, float>(BuildingEffectEnum.ConstructionPerDay, BuildingEffectIncrementType.Add, 3f, 6f, 9f),
			new Tuple<BuildingEffectEnum, BuildingEffectIncrementType, float, float, float>(BuildingEffectEnum.WallRepairSpeed, BuildingEffectIncrementType.AddFactor, 0.1f, 0.2f, 0.3f)
		}, isMilitaryProject: false, 0f);
		_buildingSettlementWaterworks.Initialize(new TextObject("{=DA0y7B3S}Waterworks"), new TextObject("{=SfbwSASh}Waterways and sanitation, decrease food consumption."), new int[3] { 1800, 3600, 5400 }, new Tuple<BuildingEffectEnum, BuildingEffectIncrementType, float, float, float>[1]
		_buildingSettlementMarketplace.Initialize(new TextObject("{=zLdXCpne}Marketplace"), new TextObject("{=Z0xf3Bbd}Increases the tariff collected from trades made in town"), new int[3] { 2400, 3600, 4800 }, new Tuple<BuildingEffectEnum, BuildingEffectIncrementType, float, float, float>[2]
		{
			new Tuple<BuildingEffectEnum, BuildingEffectIncrementType, float, float, float>(BuildingEffectEnum.TariffIncome, BuildingEffectIncrementType.AddFactor, 0.1f, 0.2f, 0.3f),
			new Tuple<BuildingEffectEnum, BuildingEffectIncrementType, float, float, float>(BuildingEffectEnum.CaravanAccessibility, BuildingEffectIncrementType.AddFactor, 1.02f, 1.04f, 1.06f)
		}, isMilitaryProject: false, 0f);
E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\TaleWorlds.CampaignSystem.Settlements.Buildings\DefaultBuildingTypes.cs:220: 		_buildingSettlementWarehouse.Initialize(new TextObject("{=anTRftmb}Warehouse"), new TextObject("{=hhKDZJeM}Increases Food storage limits and improves workshop productivity."), new int[3] { 1800, 2400, 3000 }, new Tuple<BuildingEffectEnum, BuildingEffectIncrementType, float, float, float>[2]
		{
			new Tuple<BuildingEffectEnum, BuildingEffectIncrementType, float, float, float>(BuildingEffectEnum.FoodStock, BuildingEffectIncrementType.Add, 100f, 300f, 500f),
			new Tuple<BuildingEffectEnum, BuildingEffectIncrementType, float, float, float>(BuildingEffectEnum.WorkshopProduction, BuildingEffectIncrementType.AddFactor, 0.05f, 0.1f, 0.15f)
		}, isMilitaryProject: false, 0f);
		_buildingSettlementMason.Initialize(new TextObject("{=R7ssoDHW}Mason"), new TextObject("{=hqUPvnaj}Increase bricks per day, increasing building and repair speed."), new int[3] { 2400, 3000, 4800 }, new Tuple<BuildingEffectEnum, BuildingEffectIncrementType, float, float, float>[2]
		{
			new Tuple<BuildingEffectEnum, BuildingEffectIncrementType, float, float, float>(BuildingEffectEnum.ConstructionPerDay, BuildingEffectIncrementType.Add, 3f, 6f, 9f),
			new Tuple<BuildingEffectEnum, BuildingEffectIncrementType, float, float, float>(BuildingEffectEnum.WallRepairSpeed, BuildingEffectIncrementType.AddFactor, 0.1f, 0.2f, 0.3f)
		}, isMilitaryProject: false, 0f);
		_buildingSettlementWaterworks.Initialize(new TextObject("{=DA0y7B3S}Waterworks"), new TextObject("{=SfbwSASh}Waterways and sanitation, decrease food consumption."), new int[3] { 1800, 3600, 5400 }, new Tuple<BuildingEffectEnum, BuildingEffectIncrementType, float, float, float>[1]
		{
			new Tuple<BuildingEffectEnum, BuildingEffectIncrementType, float, float, float>(BuildingEffectEnum.FoodConsumption, BuildingEffectIncrementType.AddFactor, -0.05f, -0.1f, -0.15f)
			new Tuple<BuildingEffectEnum, BuildingEffectIncrementType, float, float, float>(BuildingEffectEnum.TariffIncome, BuildingEffectIncrementType.AddFactor, 0.1f, 0.2f, 0.3f),
			new Tuple<BuildingEffectEnum, BuildingEffectIncrementType, float, float, float>(BuildingEffectEnum.CaravanAccessibility, BuildingEffectIncrementType.AddFactor, 1.02f, 1.04f, 1.06f)
		}, isMilitaryProject: false, 0f);
		_buildingSettlementWarehouse.Initialize(new TextObject("{=anTRftmb}Warehouse"), new TextObject("{=hhKDZJeM}Increases Food storage limits and improves workshop productivity."), new int[3] { 1800, 2400, 3000 }, new Tuple<BuildingEffectEnum, BuildingEffectIncrementType, float, float, float>[2]
		{
E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\TaleWorlds.CampaignSystem.Settlements.Buildings\DefaultBuildingTypes.cs:222: 			new Tuple<BuildingEffectEnum, BuildingEffectIncrementType, float, float, float>(BuildingEffectEnum.FoodStock, BuildingEffectIncrementType.Add, 100f, 300f, 500f),
			new Tuple<BuildingEffectEnum, BuildingEffectIncrementType, float, float, float>(BuildingEffectEnum.WorkshopProduction, BuildingEffectIncrementType.AddFactor, 0.05f, 0.1f, 0.15f)
		}, isMilitaryProject: false, 0f);
		_buildingSettlementMason.Initialize(new TextObject("{=R7ssoDHW}Mason"), new TextObject("{=hqUPvnaj}Increase bricks per day, increasing building and repair speed."), new int[3] { 2400, 3000, 4800 }, new Tuple<BuildingEffectEnum, BuildingEffectIncrementType, float, float, float>[2]
		{
			new Tuple<BuildingEffectEnum, BuildingEffectIncrementType, float, float, float>(BuildingEffectEnum.ConstructionPerDay, BuildingEffectIncrementType.Add, 3f, 6f, 9f),
			new Tuple<BuildingEffectEnum, BuildingEffectIncrementType, float, float, float>(BuildingEffectEnum.WallRepairSpeed, BuildingEffectIncrementType.AddFactor, 0.1f, 0.2f, 0.3f)
		}, isMilitaryProject: false, 0f);
		_buildingSettlementWaterworks.Initialize(new TextObject("{=DA0y7B3S}Waterworks"), new TextObject("{=SfbwSASh}Waterways and sanitation, decrease food consumption."), new int[3] { 1800, 3600, 5400 }, new Tuple<BuildingEffectEnum, BuildingEffectIncrementType, float, float, float>[1]
		{
			new Tuple<BuildingEffectEnum, BuildingEffectIncrementType, float, float, float>(BuildingEffectEnum.FoodConsumption, BuildingEffectIncrementType.AddFactor, -0.05f, -0.1f, -0.15f)
		}, isMilitaryProject: false, 0f);
		_buildingSettlementCourthouse.Initialize(new TextObject("{=Bw8kAvGY}Courthouse"), new TextObject("{=tmLJvPlz}Local judges manage disputes and maintain law and order. Provides influence and loyalty per day."), new int[3] { 2400, 3600, 5400 }, new Tuple<BuildingEffectEnum, BuildingEffectIncrementType, float, float, float>[2]
			new Tuple<BuildingEffectEnum, BuildingEffectIncrementType, float, float, float>(BuildingEffectEnum.CaravanAccessibility, BuildingEffectIncrementType.AddFactor, 1.02f, 1.04f, 1.06f)
		}, isMilitaryProject: false, 0f);
		_buildingSettlementWarehouse.Initialize(new TextObject("{=anTRftmb}Warehouse"), new TextObject("{=hhKDZJeM}Increases Food storage limits and improves workshop productivity."), new int[3] { 1800, 2400, 3000 }, new Tuple<BuildingEffectEnum, BuildingEffectIncrementType, float, float, float>[2]
		{
			new Tuple<BuildingEffectEnum, BuildingEffectIncrementType, float, float, float>(BuildingEffectEnum.FoodStock, BuildingEffectIncrementType.Add, 100f, 300f, 500f),
E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\TaleWorlds.CampaignSystem.Settlements.Buildings\DefaultBuildingTypes.cs:223: 			new Tuple<BuildingEffectEnum, BuildingEffectIncrementType, float, float, float>(BuildingEffectEnum.WorkshopProduction, BuildingEffectIncrementType.AddFactor, 0.05f, 0.1f, 0.15f)
		}, isMilitaryProject: false, 0f);
		_buildingSettlementMason.Initialize(new TextObject("{=R7ssoDHW}Mason"), new TextObject("{=hqUPvnaj}Increase bricks per day, increasing building and repair speed."), new int[3] { 2400, 3000, 4800 }, new Tuple<BuildingEffectEnum, BuildingEffectIncrementType, float, float, float>[2]
		{
			new Tuple<BuildingEffectEnum, BuildingEffectIncrementType, float, float, float>(BuildingEffectEnum.ConstructionPerDay, BuildingEffectIncrementType.Add, 3f, 6f, 9f),
			new Tuple<BuildingEffectEnum, BuildingEffectIncrementType, float, float, float>(BuildingEffectEnum.WallRepairSpeed, BuildingEffectIncrementType.AddFactor, 0.1f, 0.2f, 0.3f)
		}, isMilitaryProject: false, 0f);
		_buildingSettlementWaterworks.Initialize(new TextObject("{=DA0y7B3S}Waterworks"), new TextObject("{=SfbwSASh}Waterways and sanitation, decrease food consumption."), new int[3] { 1800, 3600, 5400 }, new Tuple<BuildingEffectEnum, BuildingEffectIncrementType, float, float, float>[1]
		{
			new Tuple<BuildingEffectEnum, BuildingEffectIncrementType, float, float, float>(BuildingEffectEnum.FoodConsumption, BuildingEffectIncrementType.AddFactor, -0.05f, -0.1f, -0.15f)
		}, isMilitaryProject: false, 0f);
		_buildingSettlementCourthouse.Initialize(new TextObject("{=Bw8kAvGY}Courthouse"), new TextObject("{=tmLJvPlz}Local judges manage disputes and maintain law and order. Provides influence and loyalty per day."), new int[3] { 2400, 3600, 5400 }, new Tuple<BuildingEffectEnum, BuildingEffectIncrementType, float, float, float>[2]
		{
		_buildingSettlementWarehouse.Initialize(new TextObject("{=anTRftmb}Warehouse"), new TextObject("{=hhKDZJeM}Increases Food storage limits and improves workshop productivity."), new int[3] { 1800, 2400, 3000 }, new Tuple<BuildingEffectEnum, BuildingEffectIncrementType, float, float, float>[2]
		{
			new Tuple<BuildingEffectEnum, BuildingEffectIncrementType, float, float, float>(BuildingEffectEnum.FoodStock, BuildingEffectIncrementType.Add, 100f, 300f, 500f),
			new Tuple<BuildingEffectEnum, BuildingEffectIncrementType, float, float, float>(BuildingEffectEnum.WorkshopProduction, BuildingEffectIncrementType.AddFactor, 0.05f, 0.1f, 0.15f)
		}, isMilitaryProject: false, 0f);
E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\TaleWorlds.CampaignSystem.Settlements.Buildings\DefaultBuildingTypes.cs:225: 		_buildingSettlementMason.Initialize(new TextObject("{=R7ssoDHW}Mason"), new TextObject("{=hqUPvnaj}Increase bricks per day, increasing building and repair speed."), new int[3] { 2400, 3000, 4800 }, new Tuple<BuildingEffectEnum, BuildingEffectIncrementType, float, float, float>[2]
		{
			new Tuple<BuildingEffectEnum, BuildingEffectIncrementType, float, float, float>(BuildingEffectEnum.ConstructionPerDay, BuildingEffectIncrementType.Add, 3f, 6f, 9f),
			new Tuple<BuildingEffectEnum, BuildingEffectIncrementType, float, float, float>(BuildingEffectEnum.WallRepairSpeed, BuildingEffectIncrementType.AddFactor, 0.1f, 0.2f, 0.3f)
		}, isMilitaryProject: false, 0f);
		_buildingSettlementWaterworks.Initialize(new TextObject("{=DA0y7B3S}Waterworks"), new TextObject("{=SfbwSASh}Waterways and sanitation, decrease food consumption."), new int[3] { 1800, 3600, 5400 }, new Tuple<BuildingEffectEnum, BuildingEffectIncrementType, float, float, float>[1]
		{
			new Tuple<BuildingEffectEnum, BuildingEffectIncrementType, float, float, float>(BuildingEffectEnum.FoodConsumption, BuildingEffectIncrementType.AddFactor, -0.05f, -0.1f, -0.15f)
		}, isMilitaryProject: false, 0f);
		_buildingSettlementCourthouse.Initialize(new TextObject("{=Bw8kAvGY}Courthouse"), new TextObject("{=tmLJvPlz}Local judges manage disputes and maintain law and order. Provides influence and loyalty per day."), new int[3] { 2400, 3600, 5400 }, new Tuple<BuildingEffectEnum, BuildingEffectIncrementType, float, float, float>[2]
		{
			new Tuple<BuildingEffectEnum, BuildingEffectIncrementType, float, float, float>(BuildingEffectEnum.Loyalty, BuildingEffectIncrementType.Add, 0.3f, 0.6f, 1f),
			new Tuple<BuildingEffectEnum, BuildingEffectIncrementType, float, float, float>(BuildingEffectEnum.Influence, BuildingEffectIncrementType.Add, 0.2f, 0.5f, 1f)
			new Tuple<BuildingEffectEnum, BuildingEffectIncrementType, float, float, float>(BuildingEffectEnum.FoodStock, BuildingEffectIncrementType.Add, 100f, 300f, 500f),
			new Tuple<BuildingEffectEnum, BuildingEffectIncrementType, float, float, float>(BuildingEffectEnum.WorkshopProduction, BuildingEffectIncrementType.AddFactor, 0.05f, 0.1f, 0.15f)
		}, isMilitaryProject: false, 0f);
		_buildingSettlementMason.Initialize(new TextObject("{=R7ssoDHW}Mason"), new TextObject("{=hqUPvnaj}Increase bricks per day, increasing building and repair speed."), new int[3] { 2400, 3000, 4800 }, new Tuple<BuildingEffectEnum, BuildingEffectIncrementType, float, float, float>[2]
		{
E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\TaleWorlds.CampaignSystem.Settlements.Buildings\DefaultBuildingTypes.cs:227: 			new Tuple<BuildingEffectEnum, BuildingEffectIncrementType, float, float, float>(BuildingEffectEnum.ConstructionPerDay, BuildingEffectIncrementType.Add, 3f, 6f, 9f),
			new Tuple<BuildingEffectEnum, BuildingEffectIncrementType, float, float, float>(BuildingEffectEnum.WallRepairSpeed, BuildingEffectIncrementType.AddFactor, 0.1f, 0.2f, 0.3f)
		}, isMilitaryProject: false, 0f);
		_buildingSettlementWaterworks.Initialize(new TextObject("{=DA0y7B3S}Waterworks"), new TextObject("{=SfbwSASh}Waterways and sanitation, decrease food consumption."), new int[3] { 1800, 3600, 5400 }, new Tuple<BuildingEffectEnum, BuildingEffectIncrementType, float, float, float>[1]
		{
			new Tuple<BuildingEffectEnum, BuildingEffectIncrementType, float, float, float>(BuildingEffectEnum.FoodConsumption, BuildingEffectIncrementType.AddFactor, -0.05f, -0.1f, -0.15f)
		}, isMilitaryProject: false, 0f);
		_buildingSettlementCourthouse.Initialize(new TextObject("{=Bw8kAvGY}Courthouse"), new TextObject("{=tmLJvPlz}Local judges manage disputes and maintain law and order. Provides influence and loyalty per day."), new int[3] { 2400, 3600, 5400 }, new Tuple<BuildingEffectEnum, BuildingEffectIncrementType, float, float, float>[2]
		{
			new Tuple<BuildingEffectEnum, BuildingEffectIncrementType, float, float, float>(BuildingEffectEnum.Loyalty, BuildingEffectIncrementType.Add, 0.3f, 0.6f, 1f),
			new Tuple<BuildingEffectEnum, BuildingEffectIncrementType, float, float, float>(BuildingEffectEnum.Influence, BuildingEffectIncrementType.Add, 0.2f, 0.5f, 1f)
		}, isMilitaryProject: false, 0f);
		_buildingSettlementRoadsAndPaths.Initialize(new TextObject("{=maEmutDP}Roads and Paths"), new TextObject("{=YPFDiwuy}Increase village production and village hearth growth."), new int[3] { 2400, 3600, 4800 }, new Tuple<BuildingEffectEnum, BuildingEffectIncrementType, float, float, float>[2]
			new Tuple<BuildingEffectEnum, BuildingEffectIncrementType, float, float, float>(BuildingEffectEnum.WorkshopProduction, BuildingEffectIncrementType.AddFactor, 0.05f, 0.1f, 0.15f)
		}, isMilitaryProject: false, 0f);
		_buildingSettlementMason.Initialize(new TextObject("{=R7ssoDHW}Mason"), new TextObject("{=hqUPvnaj}Increase bricks per day, increasing building and repair speed."), new int[3] { 2400, 3000, 4800 }, new Tuple<BuildingEffectEnum, BuildingEffectIncrementType, float, float, float>[2]
		{
			new Tuple<BuildingEffectEnum, BuildingEffectIncrementType, float, float, float>(BuildingEffectEnum.ConstructionPerDay, BuildingEffectIncrementType.Add, 3f, 6f, 9f),
E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\TaleWorlds.CampaignSystem.Settlements.Buildings\DefaultBuildingTypes.cs:228: 			new Tuple<BuildingEffectEnum, BuildingEffectIncrementType, float, float, float>(BuildingEffectEnum.WallRepairSpeed, BuildingEffectIncrementType.AddFactor, 0.1f, 0.2f, 0.3f)
		}, isMilitaryProject: false, 0f);
		_buildingSettlementWaterworks.Initialize(new TextObject("{=DA0y7B3S}Waterworks"), new TextObject("{=SfbwSASh}Waterways and sanitation, decrease food consumption."), new int[3] { 1800, 3600, 5400 }, new Tuple<BuildingEffectEnum, BuildingEffectIncrementType, float, float, float>[1]
		{
			new Tuple<BuildingEffectEnum, BuildingEffectIncrementType, float, float, float>(BuildingEffectEnum.FoodConsumption, BuildingEffectIncrementType.AddFactor, -0.05f, -0.1f, -0.15f)
		}, isMilitaryProject: false, 0f);
		_buildingSettlementCourthouse.Initialize(new TextObject("{=Bw8kAvGY}Courthouse"), new TextObject("{=tmLJvPlz}Local judges manage disputes and maintain law and order. Provides influence and loyalty per day."), new int[3] { 2400, 3600, 5400 }, new Tuple<BuildingEffectEnum, BuildingEffectIncrementType, float, float, float>[2]
		{
			new Tuple<BuildingEffectEnum, BuildingEffectIncrementType, float, float, float>(BuildingEffectEnum.Loyalty, BuildingEffectIncrementType.Add, 0.3f, 0.6f, 1f),
			new Tuple<BuildingEffectEnum, BuildingEffectIncrementType, float, float, float>(BuildingEffectEnum.Influence, BuildingEffectIncrementType.Add, 0.2f, 0.5f, 1f)
		}, isMilitaryProject: false, 0f);
		_buildingSettlementRoadsAndPaths.Initialize(new TextObject("{=maEmutDP}Roads and Paths"), new TextObject("{=YPFDiwuy}Increase village production and village hearth growth."), new int[3] { 2400, 3600, 4800 }, new Tuple<BuildingEffectEnum, BuildingEffectIncrementType, float, float, float>[2]
		{
		_buildingSettlementMason.Initialize(new TextObject("{=R7ssoDHW}Mason"), new TextObject("{=hqUPvnaj}Increase bricks per day, increasing building and repair speed."), new int[3] { 2400, 3000, 4800 }, new Tuple<BuildingEffectEnum, BuildingEffectIncrementType, float, float, float>[2]
		{
			new Tuple<BuildingEffectEnum, BuildingEffectIncrementType, float, float, float>(BuildingEffectEnum.ConstructionPerDay, BuildingEffectIncrementType.Add, 3f, 6f, 9f),
			new Tuple<BuildingEffectEnum, BuildingEffectIncrementType, float, float, float>(BuildingEffectEnum.WallRepairSpeed, BuildingEffectIncrementType.AddFactor, 0.1f, 0.2f, 0.3f)
		}, isMilitaryProject: false, 0f);
E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\TaleWorlds.CampaignSystem.Settlements.Buildings\DefaultBuildingTypes.cs:230: 		_buildingSettlementWaterworks.Initialize(new TextObject("{=DA0y7B3S}Waterworks"), new TextObject("{=SfbwSASh}Waterways and sanitation, decrease food consumption."), new int[3] { 1800, 3600, 5400 }, new Tuple<BuildingEffectEnum, BuildingEffectIncrementType, float, float, float>[1]
		{
			new Tuple<BuildingEffectEnum, BuildingEffectIncrementType, float, float, float>(BuildingEffectEnum.FoodConsumption, BuildingEffectIncrementType.AddFactor, -0.05f, -0.1f, -0.15f)
		}, isMilitaryProject: false, 0f);
		_buildingSettlementCourthouse.Initialize(new TextObject("{=Bw8kAvGY}Courthouse"), new TextObject("{=tmLJvPlz}Local judges manage disputes and maintain law and order. Provides influence and loyalty per day."), new int[3] { 2400, 3600, 5400 }, new Tuple<BuildingEffectEnum, BuildingEffectIncrementType, float, float, float>[2]
		{
			new Tuple<BuildingEffectEnum, BuildingEffectIncrementType, float, float, float>(BuildingEffectEnum.Loyalty, BuildingEffectIncrementType.Add, 0.3f, 0.6f, 1f),
			new Tuple<BuildingEffectEnum, BuildingEffectIncrementType, float, float, float>(BuildingEffectEnum.Influence, BuildingEffectIncrementType.Add, 0.2f, 0.5f, 1f)
		}, isMilitaryProject: false, 0f);
		_buildingSettlementRoadsAndPaths.Initialize(new TextObject("{=maEmutDP}Roads and Paths"), new TextObject("{=YPFDiwuy}Increase village production and village hearth growth."), new int[3] { 2400, 3600, 4800 }, new Tuple<BuildingEffectEnum, BuildingEffectIncrementType, float, float, float>[2]
		{
			new Tuple<BuildingEffectEnum, BuildingEffectIncrementType, float, float, float>(BuildingEffectEnum.VillageProduction, BuildingEffectIncrementType.AddFactor, 0.05f, 0.1f, 0.15f),
			new Tuple<BuildingEffectEnum, BuildingEffectIncrementType, float, float, float>(BuildingEffectEnum.VillageHeartsPerDay, BuildingEffectIncrementType.Add, 0.1f, 0.2f, 0.3f)
			new Tuple<BuildingEffectEnum, BuildingEffectIncrementType, float, float, float>(BuildingEffectEnum.ConstructionPerDay, BuildingEffectIncrementType.Add, 3f, 6f, 9f),
			new Tuple<BuildingEffectEnum, BuildingEffectIncrementType, float, float, float>(BuildingEffectEnum.WallRepairSpeed, BuildingEffectIncrementType.AddFactor, 0.1f, 0.2f, 0.3f)
		}, isMilitaryProject: false, 0f);
		_buildingSettlementWaterworks.Initialize(new TextObject("{=DA0y7B3S}Waterworks"), new TextObject("{=SfbwSASh}Waterways and sanitation, decrease food consumption."), new int[3] { 1800, 3600, 5400 }, new Tuple<BuildingEffectEnum, BuildingEffectIncrementType, float, float, float>[1]
		{
E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\TaleWorlds.CampaignSystem.Settlements.Buildings\DefaultBuildingTypes.cs:232: 			new Tuple<BuildingEffectEnum, BuildingEffectIncrementType, float, float, float>(BuildingEffectEnum.FoodConsumption, BuildingEffectIncrementType.AddFactor, -0.05f, -0.1f, -0.15f)
		}, isMilitaryProject: false, 0f);
		_buildingSettlementCourthouse.Initialize(new TextObject("{=Bw8kAvGY}Courthouse"), new TextObject("{=tmLJvPlz}Local judges manage disputes and maintain law and order. Provides influence and loyalty per day."), new int[3] { 2400, 3600, 5400 }, new Tuple<BuildingEffectEnum, BuildingEffectIncrementType, float, float, float>[2]
		{
			new Tuple<BuildingEffectEnum, BuildingEffectIncrementType, float, float, float>(BuildingEffectEnum.Loyalty, BuildingEffectIncrementType.Add, 0.3f, 0.6f, 1f),
			new Tuple<BuildingEffectEnum, BuildingEffectIncrementType, float, float, float>(BuildingEffectEnum.Influence, BuildingEffectIncrementType.Add, 0.2f, 0.5f, 1f)
		}, isMilitaryProject: false, 0f);
		_buildingSettlementRoadsAndPaths.Initialize(new TextObject("{=maEmutDP}Roads and Paths"), new TextObject("{=YPFDiwuy}Increase village production and village hearth growth."), new int[3] { 2400, 3600, 4800 }, new Tuple<BuildingEffectEnum, BuildingEffectIncrementType, float, float, float>[2]
		{
			new Tuple<BuildingEffectEnum, BuildingEffectIncrementType, float, float, float>(BuildingEffectEnum.VillageProduction, BuildingEffectIncrementType.AddFactor, 0.05f, 0.1f, 0.15f),
			new Tuple<BuildingEffectEnum, BuildingEffectIncrementType, float, float, float>(BuildingEffectEnum.VillageHeartsPerDay, BuildingEffectIncrementType.Add, 0.1f, 0.2f, 0.3f)
		}, isMilitaryProject: false, 0f);
		_buildingCastleFortifications.Initialize(new TextObject("{=CVdK1ax1}Fortifications"), new TextObject("{=oS5Nesmi}Better fortifications and higher walls around the keep, also increases the max garrison limit since it provides more space for the resident troops."), new int[3] { 0, 1400, 2800 }, new Tuple<BuildingEffectEnum, BuildingEffectIncrementType, float, float, float>[2]
		}, isMilitaryProject: false, 0f);
		_buildingSettlementWaterworks.Initialize(new TextObject("{=DA0y7B3S}Waterworks"), new TextObject("{=SfbwSASh}Waterways and sanitation, decrease food consumption."), new int[3] { 1800, 3600, 5400 }, new Tuple<BuildingEffectEnum, BuildingEffectIncrementType, float, float, float>[1]
		{
			new Tuple<BuildingEffectEnum, BuildingEffectIncrementType, float, float, float>(BuildingEffectEnum.FoodConsumption, BuildingEffectIncrementType.AddFactor, -0.05f, -0.1f, -0.15f)
		}, isMilitaryProject: false, 0f);
E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\TaleWorlds.CampaignSystem.Settlements.Buildings\DefaultBuildingTypes.cs:234: 		_buildingSettlementCourthouse.Initialize(new TextObject("{=Bw8kAvGY}Courthouse"), new TextObject("{=tmLJvPlz}Local judges manage disputes and maintain law and order. Provides influence and loyalty per day."), new int[3] { 2400, 3600, 5400 }, new Tuple<BuildingEffectEnum, BuildingEffectIncrementType, float, float, float>[2]
		{
			new Tuple<BuildingEffectEnum, BuildingEffectIncrementType, float, float, float>(BuildingEffectEnum.Loyalty, BuildingEffectIncrementType.Add, 0.3f, 0.6f, 1f),
			new Tuple<BuildingEffectEnum, BuildingEffectIncrementType, float, float, float>(BuildingEffectEnum.Influence, BuildingEffectIncrementType.Add, 0.2f, 0.5f, 1f)
		}, isMilitaryProject: false, 0f);
		_buildingSettlementRoadsAndPaths.Initialize(new TextObject("{=maEmutDP}Roads and Paths"), new TextObject("{=YPFDiwuy}Increase village production and village hearth growth."), new int[3] { 2400, 3600, 4800 }, new Tuple<BuildingEffectEnum, BuildingEffectIncrementType, float, float, float>[2]
		{
			new Tuple<BuildingEffectEnum, BuildingEffectIncrementType, float, float, float>(BuildingEffectEnum.VillageProduction, BuildingEffectIncrementType.AddFactor, 0.05f, 0.1f, 0.15f),
			new Tuple<BuildingEffectEnum, BuildingEffectIncrementType, float, float, float>(BuildingEffectEnum.VillageHeartsPerDay, BuildingEffectIncrementType.Add, 0.1f, 0.2f, 0.3f)
		}, isMilitaryProject: false, 0f);
		_buildingCastleFortifications.Initialize(new TextObject("{=CVdK1ax1}Fortifications"), new TextObject("{=oS5Nesmi}Better fortifications and higher walls around the keep, also increases the max garrison limit since it provides more space for the resident troops."), new int[3] { 0, 1400, 2800 }, new Tuple<BuildingEffectEnum, BuildingEffectIncrementType, float, float, float>[2]
		{
			new Tuple<BuildingEffectEnum, BuildingEffectIncrementType, float, float, float>(BuildingEffectEnum.GarrisonCapacity, BuildingEffectIncrementType.Add, 50f, 75f, 100f),
		{
			new Tuple<BuildingEffectEnum, BuildingEffectIncrementType, float, float, float>(BuildingEffectEnum.FoodConsumption, BuildingEffectIncrementType.AddFactor, -0.05f, -0.1f, -0.15f)
		}, isMilitaryProject: false, 0f);
		_buildingSettlementCourthouse.Initialize(new TextObject("{=Bw8kAvGY}Courthouse"), new TextObject("{=tmLJvPlz}Local judges manage disputes and maintain law and order. Provides influence and loyalty per day."), new int[3] { 2400, 3600, 5400 }, new Tuple<BuildingEffectEnum, BuildingEffectIncrementType, float, float, float>[2]
		{
E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\TaleWorlds.CampaignSystem.Settlements.Buildings\DefaultBuildingTypes.cs:236: 			new Tuple<BuildingEffectEnum, BuildingEffectIncrementType, float, float, float>(BuildingEffectEnum.Loyalty, BuildingEffectIncrementType.Add, 0.3f, 0.6f, 1f),
			new Tuple<BuildingEffectEnum, BuildingEffectIncrementType, float, float, float>(BuildingEffectEnum.Influence, BuildingEffectIncrementType.Add, 0.2f, 0.5f, 1f)
		}, isMilitaryProject: false, 0f);
		_buildingSettlementRoadsAndPaths.Initialize(new TextObject("{=maEmutDP}Roads and Paths"), new TextObject("{=YPFDiwuy}Increase village production and village hearth growth."), new int[3] { 2400, 3600, 4800 }, new Tuple<BuildingEffectEnum, BuildingEffectIncrementType, float, float, float>[2]
		{
			new Tuple<BuildingEffectEnum, BuildingEffectIncrementType, float, float, float>(BuildingEffectEnum.VillageProduction, BuildingEffectIncrementType.AddFactor, 0.05f, 0.1f, 0.15f),
			new Tuple<BuildingEffectEnum, BuildingEffectIncrementType, float, float, float>(BuildingEffectEnum.VillageHeartsPerDay, BuildingEffectIncrementType.Add, 0.1f, 0.2f, 0.3f)
		}, isMilitaryProject: false, 0f);
		_buildingCastleFortifications.Initialize(new TextObject("{=CVdK1ax1}Fortifications"), new TextObject("{=oS5Nesmi}Better fortifications and higher walls around the keep, also increases the max garrison limit since it provides more space for the resident troops."), new int[3] { 0, 1400, 2800 }, new Tuple<BuildingEffectEnum, BuildingEffectIncrementType, float, float, float>[2]
		{
			new Tuple<BuildingEffectEnum, BuildingEffectIncrementType, float, float, float>(BuildingEffectEnum.GarrisonCapacity, BuildingEffectIncrementType.Add, 50f, 75f, 100f),
			new Tuple<BuildingEffectEnum, BuildingEffectIncrementType, float, float, float>(BuildingEffectEnum.PrisonCapacity, BuildingEffectIncrementType.Add, 30f, 45f, 60f)
		}, isMilitaryProject: true, 0f, 1);
			new Tuple<BuildingEffectEnum, BuildingEffectIncrementType, float, float, float>(BuildingEffectEnum.FoodConsumption, BuildingEffectIncrementType.AddFactor, -0.05f, -0.1f, -0.15f)
		}, isMilitaryProject: false, 0f);
		_buildingSettlementCourthouse.Initialize(new TextObject("{=Bw8kAvGY}Courthouse"), new TextObject("{=tmLJvPlz}Local judges manage disputes and maintain law and order. Provides influence and loyalty per day."), new int[3] { 2400, 3600, 5400 }, new Tuple<BuildingEffectEnum, BuildingEffectIncrementType, float, float, float>[2]
		{
			new Tuple<BuildingEffectEnum, BuildingEffectIncrementType, float, float, float>(BuildingEffectEnum.Loyalty, BuildingEffectIncrementType.Add, 0.3f, 0.6f, 1f),
E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\TaleWorlds.CampaignSystem.Settlements.Buildings\DefaultBuildingTypes.cs:237: 			new Tuple<BuildingEffectEnum, BuildingEffectIncrementType, float, float, float>(BuildingEffectEnum.Influence, BuildingEffectIncrementType.Add, 0.2f, 0.5f, 1f)
		}, isMilitaryProject: false, 0f);
		_buildingSettlementRoadsAndPaths.Initialize(new TextObject("{=maEmutDP}Roads and Paths"), new TextObject("{=YPFDiwuy}Increase village production and village hearth growth."), new int[3] { 2400, 3600, 4800 }, new Tuple<BuildingEffectEnum, BuildingEffectIncrementType, float, float, float>[2]
		{
			new Tuple<BuildingEffectEnum, BuildingEffectIncrementType, float, float, float>(BuildingEffectEnum.VillageProduction, BuildingEffectIncrementType.AddFactor, 0.05f, 0.1f, 0.15f),
			new Tuple<BuildingEffectEnum, BuildingEffectIncrementType, float, float, float>(BuildingEffectEnum.VillageHeartsPerDay, BuildingEffectIncrementType.Add, 0.1f, 0.2f, 0.3f)
		}, isMilitaryProject: false, 0f);
		_buildingCastleFortifications.Initialize(new TextObject("{=CVdK1ax1}Fortifications"), new TextObject("{=oS5Nesmi}Better fortifications and higher walls around the keep, also increases the max garrison limit since it provides more space for the resident troops."), new int[3] { 0, 1400, 2800 }, new Tuple<BuildingEffectEnum, BuildingEffectIncrementType, float, float, float>[2]
		{
			new Tuple<BuildingEffectEnum, BuildingEffectIncrementType, float, float, float>(BuildingEffectEnum.GarrisonCapacity, BuildingEffectIncrementType.Add, 50f, 75f, 100f),
			new Tuple<BuildingEffectEnum, BuildingEffectIncrementType, float, float, float>(BuildingEffectEnum.PrisonCapacity, BuildingEffectIncrementType.Add, 30f, 45f, 60f)
		}, isMilitaryProject: true, 0f, 1);
		_buildingCastleBarracks.Initialize(new TextObject("{=x2B0OjhI}Barracks"), new TextObject("{=JalrbDBC}Lodgings for garrison troops. Each level increases garrison limit and decreases garrison wage."), new int[3] { 420, 700, 1120 }, new Tuple<BuildingEffectEnum, BuildingEffectIncrementType, float, float, float>[2]
		_buildingSettlementCourthouse.Initialize(new TextObject("{=Bw8kAvGY}Courthouse"), new TextObject("{=tmLJvPlz}Local judges manage disputes and maintain law and order. Provides influence and loyalty per day."), new int[3] { 2400, 3600, 5400 }, new Tuple<BuildingEffectEnum, BuildingEffectIncrementType, float, float, float>[2]
		{
			new Tuple<BuildingEffectEnum, BuildingEffectIncrementType, float, float, float>(BuildingEffectEnum.Loyalty, BuildingEffectIncrementType.Add, 0.3f, 0.6f, 1f),
			new Tuple<BuildingEffectEnum, BuildingEffectIncrementType, float, float, float>(BuildingEffectEnum.Influence, BuildingEffectIncrementType.Add, 0.2f, 0.5f, 1f)
		}, isMilitaryProject: false, 0f);
E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\TaleWorlds.CampaignSystem.Settlements.Buildings\DefaultBuildingTypes.cs:239: 		_buildingSettlementRoadsAndPaths.Initialize(new TextObject("{=maEmutDP}Roads and Paths"), new TextObject("{=YPFDiwuy}Increase village production and village hearth growth."), new int[3] { 2400, 3600, 4800 }, new Tuple<BuildingEffectEnum, BuildingEffectIncrementType, float, float, float>[2]
		{
			new Tuple<BuildingEffectEnum, BuildingEffectIncrementType, float, float, float>(BuildingEffectEnum.VillageProduction, BuildingEffectIncrementType.AddFactor, 0.05f, 0.1f, 0.15f),
			new Tuple<BuildingEffectEnum, BuildingEffectIncrementType, float, float, float>(BuildingEffectEnum.VillageHeartsPerDay, BuildingEffectIncrementType.Add, 0.1f, 0.2f, 0.3f)
		}, isMilitaryProject: false, 0f);
		_buildingCastleFortifications.Initialize(new TextObject("{=CVdK1ax1}Fortifications"), new TextObject("{=oS5Nesmi}Better fortifications and higher walls around the keep, also increases the max garrison limit since it provides more space for the resident troops."), new int[3] { 0, 1400, 2800 }, new Tuple<BuildingEffectEnum, BuildingEffectIncrementType, float, float, float>[2]
		{
			new Tuple<BuildingEffectEnum, BuildingEffectIncrementType, float, float, float>(BuildingEffectEnum.GarrisonCapacity, BuildingEffectIncrementType.Add, 50f, 75f, 100f),
			new Tuple<BuildingEffectEnum, BuildingEffectIncrementType, float, float, float>(BuildingEffectEnum.PrisonCapacity, BuildingEffectIncrementType.Add, 30f, 45f, 60f)
		}, isMilitaryProject: true, 0f, 1);
		_buildingCastleBarracks.Initialize(new TextObject("{=x2B0OjhI}Barracks"), new TextObject("{=JalrbDBC}Lodgings for garrison troops. Each level increases garrison limit and decreases garrison wage."), new int[3] { 420, 700, 1120 }, new Tuple<BuildingEffectEnum, BuildingEffectIncrementType, float, float, float>[2]
		{
			new Tuple<BuildingEffectEnum, BuildingEffectIncrementType, float, float, float>(BuildingEffectEnum.GarrisonCapacity, BuildingEffectIncrementType.Add, 20f, 40f, 80f),
			new Tuple<BuildingEffectEnum, BuildingEffectIncrementType, float, float, float>(BuildingEffectEnum.Loyalty, BuildingEffectIncrementType.Add, 0.3f, 0.6f, 1f),
			new Tuple<BuildingEffectEnum, BuildingEffectIncrementType, float, float, float>(BuildingEffectEnum.Influence, BuildingEffectIncrementType.Add, 0.2f, 0.5f, 1f)
		}, isMilitaryProject: false, 0f);
		_buildingSettlementRoadsAndPaths.Initialize(new TextObject("{=maEmutDP}Roads and Paths"), new TextObject("{=YPFDiwuy}Increase village production and village hearth growth."), new int[3] { 2400, 3600, 4800 }, new Tuple<BuildingEffectEnum, BuildingEffectIncrementType, float, float, float>[2]
		{
E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\TaleWorlds.CampaignSystem.Settlements.Buildings\DefaultBuildingTypes.cs:241: 			new Tuple<BuildingEffectEnum, BuildingEffectIncrementType, float, float, float>(BuildingEffectEnum.VillageProduction, BuildingEffectIncrementType.AddFactor, 0.05f, 0.1f, 0.15f),
			new Tuple<BuildingEffectEnum, BuildingEffectIncrementType, float, float, float>(BuildingEffectEnum.VillageHeartsPerDay, BuildingEffectIncrementType.Add, 0.1f, 0.2f, 0.3f)
		}, isMilitaryProject: false, 0f);
		_buildingCastleFortifications.Initialize(new TextObject("{=CVdK1ax1}Fortifications"), new TextObject("{=oS5Nesmi}Better fortifications and higher walls around the keep, also increases the max garrison limit since it provides more space for the resident troops."), new int[3] { 0, 1400, 2800 }, new Tuple<BuildingEffectEnum, BuildingEffectIncrementType, float, float, float>[2]
		{
			new Tuple<BuildingEffectEnum, BuildingEffectIncrementType, float, float, float>(BuildingEffectEnum.GarrisonCapacity, BuildingEffectIncrementType.Add, 50f, 75f, 100f),
			new Tuple<BuildingEffectEnum, BuildingEffectIncrementType, float, float, float>(BuildingEffectEnum.PrisonCapacity, BuildingEffectIncrementType.Add, 30f, 45f, 60f)
		}, isMilitaryProject: true, 0f, 1);
		_buildingCastleBarracks.Initialize(new TextObject("{=x2B0OjhI}Barracks"), new TextObject("{=JalrbDBC}Lodgings for garrison troops. Each level increases garrison limit and decreases garrison wage."), new int[3] { 420, 700, 1120 }, new Tuple<BuildingEffectEnum, BuildingEffectIncrementType, float, float, float>[2]
		{
			new Tuple<BuildingEffectEnum, BuildingEffectIncrementType, float, float, float>(BuildingEffectEnum.GarrisonCapacity, BuildingEffectIncrementType.Add, 20f, 40f, 80f),
			new Tuple<BuildingEffectEnum, BuildingEffectIncrementType, float, float, float>(BuildingEffectEnum.GarrisonWageReduction, BuildingEffectIncrementType.AddFactor, -0.1f, -0.2f, -0.3f)
		}, isMilitaryProject: true, 0f);
			new Tuple<BuildingEffectEnum, BuildingEffectIncrementType, float, float, float>(BuildingEffectEnum.Influence, BuildingEffectIncrementType.Add, 0.2f, 0.5f, 1f)
		}, isMilitaryProject: false, 0f);
		_buildingSettlementRoadsAndPaths.Initialize(new TextObject("{=maEmutDP}Roads and Paths"), new TextObject("{=YPFDiwuy}Increase village production and village hearth growth."), new int[3] { 2400, 3600, 4800 }, new Tuple<BuildingEffectEnum, BuildingEffectIncrementType, float, float, float>[2]
		{
			new Tuple<BuildingEffectEnum, BuildingEffectIncrementType, float, float, float>(BuildingEffectEnum.VillageProduction, BuildingEffectIncrementType.AddFactor, 0.05f, 0.1f, 0.15f),
E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\TaleWorlds.CampaignSystem.Settlements.Buildings\DefaultBuildingTypes.cs:242: 			new Tuple<BuildingEffectEnum, BuildingEffectIncrementType, float, float, float>(BuildingEffectEnum.VillageHeartsPerDay, BuildingEffectIncrementType.Add, 0.1f, 0.2f, 0.3f)
		}, isMilitaryProject: false, 0f);
		_buildingCastleFortifications.Initialize(new TextObject("{=CVdK1ax1}Fortifications"), new TextObject("{=oS5Nesmi}Better fortifications and higher walls around the keep, also increases the max garrison limit since it provides more space for the resident troops."), new int[3] { 0, 1400, 2800 }, new Tuple<BuildingEffectEnum, BuildingEffectIncrementType, float, float, float>[2]
		{
			new Tuple<BuildingEffectEnum, BuildingEffectIncrementType, float, float, float>(BuildingEffectEnum.GarrisonCapacity, BuildingEffectIncrementType.Add, 50f, 75f, 100f),
			new Tuple<BuildingEffectEnum, BuildingEffectIncrementType, float, float, float>(BuildingEffectEnum.PrisonCapacity, BuildingEffectIncrementType.Add, 30f, 45f, 60f)
		}, isMilitaryProject: true, 0f, 1);
		_buildingCastleBarracks.Initialize(new TextObject("{=x2B0OjhI}Barracks"), new TextObject("{=JalrbDBC}Lodgings for garrison troops. Each level increases garrison limit and decreases garrison wage."), new int[3] { 420, 700, 1120 }, new Tuple<BuildingEffectEnum, BuildingEffectIncrementType, float, float, float>[2]
		{
			new Tuple<BuildingEffectEnum, BuildingEffectIncrementType, float, float, float>(BuildingEffectEnum.GarrisonCapacity, BuildingEffectIncrementType.Add, 20f, 40f, 80f),
			new Tuple<BuildingEffectEnum, BuildingEffectIncrementType, float, float, float>(BuildingEffectEnum.GarrisonWageReduction, BuildingEffectIncrementType.AddFactor, -0.1f, -0.2f, -0.3f)
		}, isMilitaryProject: true, 0f);
		_buildingCastleTrainingFields.Initialize(new TextObject("{=BkTiRPT4}Training Fields"), new TextObject("{=otWlERkc}A field for military drills that increases the daily experience gain of all garrisoned units."), new int[3] { 420, 560, 700 }, new Tuple<BuildingEffectEnum, BuildingEffectIncrementType, float, float, float>[2]
		_buildingSettlementRoadsAndPaths.Initialize(new TextObject("{=maEmutDP}Roads and Paths"), new TextObject("{=YPFDiwuy}Increase village production and village hearth growth."), new int[3] { 2400, 3600, 4800 }, new Tuple<BuildingEffectEnum, BuildingEffectIncrementType, float, float, float>[2]
		{
			new Tuple<BuildingEffectEnum, BuildingEffectIncrementType, float, float, float>(BuildingEffectEnum.VillageProduction, BuildingEffectIncrementType.AddFactor, 0.05f, 0.1f, 0.15f),
			new Tuple<BuildingEffectEnum, BuildingEffectIncrementType, float, float, float>(BuildingEffectEnum.VillageHeartsPerDay, BuildingEffectIncrementType.Add, 0.1f, 0.2f, 0.3f)
		}, isMilitaryProject: false, 0f);
E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\TaleWorlds.CampaignSystem.Settlements.Buildings\DefaultBuildingTypes.cs:244: 		_buildingCastleFortifications.Initialize(new TextObject("{=CVdK1ax1}Fortifications"), new TextObject("{=oS5Nesmi}Better fortifications and higher walls around the keep, also increases the max garrison limit since it provides more space for the resident troops."), new int[3] { 0, 1400, 2800 }, new Tuple<BuildingEffectEnum, BuildingEffectIncrementType, float, float, float>[2]
		{
			new Tuple<BuildingEffectEnum, BuildingEffectIncrementType, float, float, float>(BuildingEffectEnum.GarrisonCapacity, BuildingEffectIncrementType.Add, 50f, 75f, 100f),
			new Tuple<BuildingEffectEnum, BuildingEffectIncrementType, float, float, float>(BuildingEffectEnum.PrisonCapacity, BuildingEffectIncrementType.Add, 30f, 45f, 60f)
		}, isMilitaryProject: true, 0f, 1);
		_buildingCastleBarracks.Initialize(new TextObject("{=x2B0OjhI}Barracks"), new TextObject("{=JalrbDBC}Lodgings for garrison troops. Each level increases garrison limit and decreases garrison wage."), new int[3] { 420, 700, 1120 }, new Tuple<BuildingEffectEnum, BuildingEffectIncrementType, float, float, float>[2]
		{
			new Tuple<BuildingEffectEnum, BuildingEffectIncrementType, float, float, float>(BuildingEffectEnum.GarrisonCapacity, BuildingEffectIncrementType.Add, 20f, 40f, 80f),
			new Tuple<BuildingEffectEnum, BuildingEffectIncrementType, float, float, float>(BuildingEffectEnum.GarrisonWageReduction, BuildingEffectIncrementType.AddFactor, -0.1f, -0.2f, -0.3f)
		}, isMilitaryProject: true, 0f);
		_buildingCastleTrainingFields.Initialize(new TextObject("{=BkTiRPT4}Training Fields"), new TextObject("{=otWlERkc}A field for military drills that increases the daily experience gain of all garrisoned units."), new int[3] { 420, 560, 700 }, new Tuple<BuildingEffectEnum, BuildingEffectIncrementType, float, float, float>[2]
		{
			new Tuple<BuildingEffectEnum, BuildingEffectIncrementType, float, float, float>(BuildingEffectEnum.ExperiencePerDay, BuildingEffectIncrementType.Add, 3f, 4f, 5f),
			new Tuple<BuildingEffectEnum, BuildingEffectIncrementType, float, float, float>(BuildingEffectEnum.VillageProduction, BuildingEffectIncrementType.AddFactor, 0.05f, 0.1f, 0.15f),
			new Tuple<BuildingEffectEnum, BuildingEffectIncrementType, float, float, float>(BuildingEffectEnum.VillageHeartsPerDay, BuildingEffectIncrementType.Add, 0.1f, 0.2f, 0.3f)
		}, isMilitaryProject: false, 0f);
		_buildingCastleFortifications.Initialize(new TextObject("{=CVdK1ax1}Fortifications"), new TextObject("{=oS5Nesmi}Better fortifications and higher walls around the keep, also increases the max garrison limit since it provides more space for the resident troops."), new int[3] { 0, 1400, 2800 }, new Tuple<BuildingEffectEnum, BuildingEffectIncrementType, float, float, float>[2]
		{
E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\TaleWorlds.CampaignSystem.Settlements.Buildings\DefaultBuildingTypes.cs:246: 			new Tuple<BuildingEffectEnum, BuildingEffectIncrementType, float, float, float>(BuildingEffectEnum.GarrisonCapacity, BuildingEffectIncrementType.Add, 50f, 75f, 100f),
			new Tuple<BuildingEffectEnum, BuildingEffectIncrementType, float, float, float>(BuildingEffectEnum.PrisonCapacity, BuildingEffectIncrementType.Add, 30f, 45f, 60f)
		}, isMilitaryProject: true, 0f, 1);
		_buildingCastleBarracks.Initialize(new TextObject("{=x2B0OjhI}Barracks"), new TextObject("{=JalrbDBC}Lodgings for garrison troops. Each level increases garrison limit and decreases garrison wage."), new int[3] { 420, 700, 1120 }, new Tuple<BuildingEffectEnum, BuildingEffectIncrementType, float, float, float>[2]
		{
			new Tuple<BuildingEffectEnum, BuildingEffectIncrementType, float, float, float>(BuildingEffectEnum.GarrisonCapacity, BuildingEffectIncrementType.Add, 20f, 40f, 80f),
			new Tuple<BuildingEffectEnum, BuildingEffectIncrementType, float, float, float>(BuildingEffectEnum.GarrisonWageReduction, BuildingEffectIncrementType.AddFactor, -0.1f, -0.2f, -0.3f)
		}, isMilitaryProject: true, 0f);
		_buildingCastleTrainingFields.Initialize(new TextObject("{=BkTiRPT4}Training Fields"), new TextObject("{=otWlERkc}A field for military drills that increases the daily experience gain of all garrisoned units."), new int[3] { 420, 560, 700 }, new Tuple<BuildingEffectEnum, BuildingEffectIncrementType, float, float, float>[2]
		{
			new Tuple<BuildingEffectEnum, BuildingEffectIncrementType, float, float, float>(BuildingEffectEnum.ExperiencePerDay, BuildingEffectIncrementType.Add, 3f, 4f, 5f),
			new Tuple<BuildingEffectEnum, BuildingEffectIncrementType, float, float, float>(BuildingEffectEnum.MilitiaVeterancyChance, BuildingEffectIncrementType.Add, 0.1f, 0.15f, 0.2f)
		}, isMilitaryProject: true, 0f);
			new Tuple<BuildingEffectEnum, BuildingEffectIncrementType, float, float, float>(BuildingEffectEnum.VillageHeartsPerDay, BuildingEffectIncrementType.Add, 0.1f, 0.2f, 0.3f)
		}, isMilitaryProject: false, 0f);
		_buildingCastleFortifications.Initialize(new TextObject("{=CVdK1ax1}Fortifications"), new TextObject("{=oS5Nesmi}Better fortifications and higher walls around the keep, also increases the max garrison limit since it provides more space for the resident troops."), new int[3] { 0, 1400, 2800 }, new Tuple<BuildingEffectEnum, BuildingEffectIncrementType, float, float, float>[2]
		{
			new Tuple<BuildingEffectEnum, BuildingEffectIncrementType, float, float, float>(BuildingEffectEnum.GarrisonCapacity, BuildingEffectIncrementType.Add, 50f, 75f, 100f),
E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\TaleWorlds.CampaignSystem.Settlements.Buildings\DefaultBuildingTypes.cs:247: 			new Tuple<BuildingEffectEnum, BuildingEffectIncrementType, float, float, float>(BuildingEffectEnum.PrisonCapacity, BuildingEffectIncrementType.Add, 30f, 45f, 60f)
		}, isMilitaryProject: true, 0f, 1);
		_buildingCastleBarracks.Initialize(new TextObject("{=x2B0OjhI}Barracks"), new TextObject("{=JalrbDBC}Lodgings for garrison troops. Each level increases garrison limit and decreases garrison wage."), new int[3] { 420, 700, 1120 }, new Tuple<BuildingEffectEnum, BuildingEffectIncrementType, float, float, float>[2]
		{
			new Tuple<BuildingEffectEnum, BuildingEffectIncrementType, float, float, float>(BuildingEffectEnum.GarrisonCapacity, BuildingEffectIncrementType.Add, 20f, 40f, 80f),
			new Tuple<BuildingEffectEnum, BuildingEffectIncrementType, float, float, float>(BuildingEffectEnum.GarrisonWageReduction, BuildingEffectIncrementType.AddFactor, -0.1f, -0.2f, -0.3f)
		}, isMilitaryProject: true, 0f);
		_buildingCastleTrainingFields.Initialize(new TextObject("{=BkTiRPT4}Training Fields"), new TextObject("{=otWlERkc}A field for military drills that increases the daily experience gain of all garrisoned units."), new int[3] { 420, 560, 700 }, new Tuple<BuildingEffectEnum, BuildingEffectIncrementType, float, float, float>[2]
		{
			new Tuple<BuildingEffectEnum, BuildingEffectIncrementType, float, float, float>(BuildingEffectEnum.ExperiencePerDay, BuildingEffectIncrementType.Add, 3f, 4f, 5f),
			new Tuple<BuildingEffectEnum, BuildingEffectIncrementType, float, float, float>(BuildingEffectEnum.MilitiaVeterancyChance, BuildingEffectIncrementType.Add, 0.1f, 0.15f, 0.2f)
		}, isMilitaryProject: true, 0f);
		_buildingCastleGuardHouse.Initialize(new TextObject("{=OHEiwoHC}Guard House"), new TextObject("{=K0cbj7o3}Increase militia recruitment, and prisoner limit."), new int[3] { 350, 490, 630 }, new Tuple<BuildingEffectEnum, BuildingEffectIncrementType, float, float, float>[2]
		_buildingCastleFortifications.Initialize(new TextObject("{=CVdK1ax1}Fortifications"), new TextObject("{=oS5Nesmi}Better fortifications and higher walls around the keep, also increases the max garrison limit since it provides more space for the resident troops."), new int[3] { 0, 1400, 2800 }, new Tuple<BuildingEffectEnum, BuildingEffectIncrementType, float, float, float>[2]
		{
			new Tuple<BuildingEffectEnum, BuildingEffectIncrementType, float, float, float>(BuildingEffectEnum.GarrisonCapacity, BuildingEffectIncrementType.Add, 50f, 75f, 100f),
			new Tuple<BuildingEffectEnum, BuildingEffectIncrementType, float, float, float>(BuildingEffectEnum.PrisonCapacity, BuildingEffectIncrementType.Add, 30f, 45f, 60f)
		}, isMilitaryProject: true, 0f, 1);
E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\TaleWorlds.CampaignSystem.Settlements.Buildings\DefaultBuildingTypes.cs:249: 		_buildingCastleBarracks.Initialize(new TextObject("{=x2B0OjhI}Barracks"), new TextObject("{=JalrbDBC}Lodgings for garrison troops. Each level increases garrison limit and decreases garrison wage."), new int[3] { 420, 700, 1120 }, new Tuple<BuildingEffectEnum, BuildingEffectIncrementType, float, float, float>[2]
		{
			new Tuple<BuildingEffectEnum, BuildingEffectIncrementType, float, float, float>(BuildingEffectEnum.GarrisonCapacity, BuildingEffectIncrementType.Add, 20f, 40f, 80f),
			new Tuple<BuildingEffectEnum, BuildingEffectIncrementType, float, float, float>(BuildingEffectEnum.GarrisonWageReduction, BuildingEffectIncrementType.AddFactor, -0.1f, -0.2f, -0.3f)
		}, isMilitaryProject: true, 0f);
		_buildingCastleTrainingFields.Initialize(new TextObject("{=BkTiRPT4}Training Fields"), new TextObject("{=otWlERkc}A field for military drills that increases the daily experience gain of all garrisoned units."), new int[3] { 420, 560, 700 }, new Tuple<BuildingEffectEnum, BuildingEffectIncrementType, float, float, float>[2]
		{
			new Tuple<BuildingEffectEnum, BuildingEffectIncrementType, float, float, float>(BuildingEffectEnum.ExperiencePerDay, BuildingEffectIncrementType.Add, 3f, 4f, 5f),
			new Tuple<BuildingEffectEnum, BuildingEffectIncrementType, float, float, float>(BuildingEffectEnum.MilitiaVeterancyChance, BuildingEffectIncrementType.Add, 0.1f, 0.15f, 0.2f)
		}, isMilitaryProject: true, 0f);
		_buildingCastleGuardHouse.Initialize(new TextObject("{=OHEiwoHC}Guard House"), new TextObject("{=K0cbj7o3}Increase militia recruitment, and prisoner limit."), new int[3] { 350, 490, 630 }, new Tuple<BuildingEffectEnum, BuildingEffectIncrementType, float, float, float>[2]
		{
			new Tuple<BuildingEffectEnum, BuildingEffectIncrementType, float, float, float>(BuildingEffectEnum.Militia, BuildingEffectIncrementType.Add, 1f, 2f, 3f),
			new Tuple<BuildingEffectEnum, BuildingEffectIncrementType, float, float, float>(BuildingEffectEnum.GarrisonCapacity, BuildingEffectIncrementType.Add, 50f, 75f, 100f),
			new Tuple<BuildingEffectEnum, BuildingEffectIncrementType, float, float, float>(BuildingEffectEnum.PrisonCapacity, BuildingEffectIncrementType.Add, 30f, 45f, 60f)
		}, isMilitaryProject: true, 0f, 1);
		_buildingCastleBarracks.Initialize(new TextObject("{=x2B0OjhI}Barracks"), new TextObject("{=JalrbDBC}Lodgings for garrison troops. Each level increases garrison limit and decreases garrison wage."), new int[3] { 420, 700, 1120 }, new Tuple<BuildingEffectEnum, BuildingEffectIncrementType, float, float, float>[2]
		{
E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\TaleWorlds.CampaignSystem.Settlements.Buildings\DefaultBuildingTypes.cs:251: 			new Tuple<BuildingEffectEnum, BuildingEffectIncrementType, float, float, float>(BuildingEffectEnum.GarrisonCapacity, BuildingEffectIncrementType.Add, 20f, 40f, 80f),
			new Tuple<BuildingEffectEnum, BuildingEffectIncrementType, float, float, float>(BuildingEffectEnum.GarrisonWageReduction, BuildingEffectIncrementType.AddFactor, -0.1f, -0.2f, -0.3f)
		}, isMilitaryProject: true, 0f);
		_buildingCastleTrainingFields.Initialize(new TextObject("{=BkTiRPT4}Training Fields"), new TextObject("{=otWlERkc}A field for military drills that increases the daily experience gain of all garrisoned units."), new int[3] { 420, 560, 700 }, new Tuple<BuildingEffectEnum, BuildingEffectIncrementType, float, float, float>[2]
		{
			new Tuple<BuildingEffectEnum, BuildingEffectIncrementType, float, float, float>(BuildingEffectEnum.ExperiencePerDay, BuildingEffectIncrementType.Add, 3f, 4f, 5f),
			new Tuple<BuildingEffectEnum, BuildingEffectIncrementType, float, float, float>(BuildingEffectEnum.MilitiaVeterancyChance, BuildingEffectIncrementType.Add, 0.1f, 0.15f, 0.2f)
		}, isMilitaryProject: true, 0f);
		_buildingCastleGuardHouse.Initialize(new TextObject("{=OHEiwoHC}Guard House"), new TextObject("{=K0cbj7o3}Increase militia recruitment, and prisoner limit."), new int[3] { 350, 490, 630 }, new Tuple<BuildingEffectEnum, BuildingEffectIncrementType, float, float, float>[2]
		{
			new Tuple<BuildingEffectEnum, BuildingEffectIncrementType, float, float, float>(BuildingEffectEnum.Militia, BuildingEffectIncrementType.Add, 1f, 2f, 3f),
			new Tuple<BuildingEffectEnum, BuildingEffectIncrementType, float, float, float>(BuildingEffectEnum.PrisonCapacity, BuildingEffectIncrementType.Add, 10f, 30f, 50f)
		}, isMilitaryProject: true, 0f);
			new Tuple<BuildingEffectEnum, BuildingEffectIncrementType, float, float, float>(BuildingEffectEnum.PrisonCapacity, BuildingEffectIncrementType.Add, 30f, 45f, 60f)
		}, isMilitaryProject: true, 0f, 1);
		_buildingCastleBarracks.Initialize(new TextObject("{=x2B0OjhI}Barracks"), new TextObject("{=JalrbDBC}Lodgings for garrison troops. Each level increases garrison limit and decreases garrison wage."), new int[3] { 420, 700, 1120 }, new Tuple<BuildingEffectEnum, BuildingEffectIncrementType, float, float, float>[2]
		{
			new Tuple<BuildingEffectEnum, BuildingEffectIncrementType, float, float, float>(BuildingEffectEnum.GarrisonCapacity, BuildingEffectIncrementType.Add, 20f, 40f, 80f),
E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\TaleWorlds.CampaignSystem.Settlements.Buildings\DefaultBuildingTypes.cs:252: 			new Tuple<BuildingEffectEnum, BuildingEffectIncrementType, float, float, float>(BuildingEffectEnum.GarrisonWageReduction, BuildingEffectIncrementType.AddFactor, -0.1f, -0.2f, -0.3f)
		}, isMilitaryProject: true, 0f);
		_buildingCastleTrainingFields.Initialize(new TextObject("{=BkTiRPT4}Training Fields"), new TextObject("{=otWlERkc}A field for military drills that increases the daily experience gain of all garrisoned units."), new int[3] { 420, 560, 700 }, new Tuple<BuildingEffectEnum, BuildingEffectIncrementType, float, float, float>[2]
		{
			new Tuple<BuildingEffectEnum, BuildingEffectIncrementType, float, float, float>(BuildingEffectEnum.ExperiencePerDay, BuildingEffectIncrementType.Add, 3f, 4f, 5f),
			new Tuple<BuildingEffectEnum, BuildingEffectIncrementType, float, float, float>(BuildingEffectEnum.MilitiaVeterancyChance, BuildingEffectIncrementType.Add, 0.1f, 0.15f, 0.2f)
		}, isMilitaryProject: true, 0f);
		_buildingCastleGuardHouse.Initialize(new TextObject("{=OHEiwoHC}Guard House"), new TextObject("{=K0cbj7o3}Increase militia recruitment, and prisoner limit."), new int[3] { 350, 490, 630 }, new Tuple<BuildingEffectEnum, BuildingEffectIncrementType, float, float, float>[2]
		{
			new Tuple<BuildingEffectEnum, BuildingEffectIncrementType, float, float, float>(BuildingEffectEnum.Militia, BuildingEffectIncrementType.Add, 1f, 2f, 3f),
			new Tuple<BuildingEffectEnum, BuildingEffectIncrementType, float, float, float>(BuildingEffectEnum.PrisonCapacity, BuildingEffectIncrementType.Add, 10f, 30f, 50f)
		}, isMilitaryProject: true, 0f);
		_buildingCastleSiegeWorkshop.Initialize(new TextObject("{=9Bnwttn6}Siege Workshop"), new TextObject("{=YRCW0oFd}Builds and maintains siege engines for defense of the settlement."), new int[3] { 280, 420, 700 }, new Tuple<BuildingEffectEnum, BuildingEffectIncrementType, float, float, float>[3]
		_buildingCastleBarracks.Initialize(new TextObject("{=x2B0OjhI}Barracks"), new TextObject("{=JalrbDBC}Lodgings for garrison troops. Each level increases garrison limit and decreases garrison wage."), new int[3] { 420, 700, 1120 }, new Tuple<BuildingEffectEnum, BuildingEffectIncrementType, float, float, float>[2]
		{
			new Tuple<BuildingEffectEnum, BuildingEffectIncrementType, float, float, float>(BuildingEffectEnum.GarrisonCapacity, BuildingEffectIncrementType.Add, 20f, 40f, 80f),
			new Tuple<BuildingEffectEnum, BuildingEffectIncrementType, float, float, float>(BuildingEffectEnum.GarrisonWageReduction, BuildingEffectIncrementType.AddFactor, -0.1f, -0.2f, -0.3f)
		}, isMilitaryProject: true, 0f);
E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\TaleWorlds.CampaignSystem.Settlements.Buildings\DefaultBuildingTypes.cs:254: 		_buildingCastleTrainingFields.Initialize(new TextObject("{=BkTiRPT4}Training Fields"), new TextObject("{=otWlERkc}A field for military drills that increases the daily experience gain of all garrisoned units."), new int[3] { 420, 560, 700 }, new Tuple<BuildingEffectEnum, BuildingEffectIncrementType, float, float, float>[2]
		{
			new Tuple<BuildingEffectEnum, BuildingEffectIncrementType, float, float, float>(BuildingEffectEnum.ExperiencePerDay, BuildingEffectIncrementType.Add, 3f, 4f, 5f),
			new Tuple<BuildingEffectEnum, BuildingEffectIncrementType, float, float, float>(BuildingEffectEnum.MilitiaVeterancyChance, BuildingEffectIncrementType.Add, 0.1f, 0.15f, 0.2f)
		}, isMilitaryProject: true, 0f);
		_buildingCastleGuardHouse.Initialize(new TextObject("{=OHEiwoHC}Guard House"), new TextObject("{=K0cbj7o3}Increase militia recruitment, and prisoner limit."), new int[3] { 350, 490, 630 }, new Tuple<BuildingEffectEnum, BuildingEffectIncrementType, float, float, float>[2]
		{
			new Tuple<BuildingEffectEnum, BuildingEffectIncrementType, float, float, float>(BuildingEffectEnum.Militia, BuildingEffectIncrementType.Add, 1f, 2f, 3f),
			new Tuple<BuildingEffectEnum, BuildingEffectIncrementType, float, float, float>(BuildingEffectEnum.PrisonCapacity, BuildingEffectIncrementType.Add, 10f, 30f, 50f)
		}, isMilitaryProject: true, 0f);
		_buildingCastleSiegeWorkshop.Initialize(new TextObject("{=9Bnwttn6}Siege Workshop"), new TextObject("{=YRCW0oFd}Builds and maintains siege engines for defense of the settlement."), new int[3] { 280, 420, 700 }, new Tuple<BuildingEffectEnum, BuildingEffectIncrementType, float, float, float>[3]
		{
			new Tuple<BuildingEffectEnum, BuildingEffectIncrementType, float, float, float>(BuildingEffectEnum.BallistaOnSiegeStart, BuildingEffectIncrementType.Add, 1f, 2f, 3f),
			new Tuple<BuildingEffectEnum, BuildingEffectIncrementType, float, float, float>(BuildingEffectEnum.GarrisonCapacity, BuildingEffectIncrementType.Add, 20f, 40f, 80f),
			new Tuple<BuildingEffectEnum, BuildingEffectIncrementType, float, float, float>(BuildingEffectEnum.GarrisonWageReduction, BuildingEffectIncrementType.AddFactor, -0.1f, -0.2f, -0.3f)
		}, isMilitaryProject: true, 0f);
		_buildingCastleTrainingFields.Initialize(new TextObject("{=BkTiRPT4}Training Fields"), new TextObject("{=otWlERkc}A field for military drills that increases the daily experience gain of all garrisoned units."), new int[3] { 420, 560, 700 }, new Tuple<BuildingEffectEnum, BuildingEffectIncrementType, float, float, float>[2]
		{
E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\TaleWorlds.CampaignSystem.Settlements.Buildings\DefaultBuildingTypes.cs:256: 			new Tuple<BuildingEffectEnum, BuildingEffectIncrementType, float, float, float>(BuildingEffectEnum.ExperiencePerDay, BuildingEffectIncrementType.Add, 3f, 4f, 5f),
			new Tuple<BuildingEffectEnum, BuildingEffectIncrementType, float, float, float>(BuildingEffectEnum.MilitiaVeterancyChance, BuildingEffectIncrementType.Add, 0.1f, 0.15f, 0.2f)
		}, isMilitaryProject: true, 0f);
		_buildingCastleGuardHouse.Initialize(new TextObject("{=OHEiwoHC}Guard House"), new TextObject("{=K0cbj7o3}Increase militia recruitment, and prisoner limit."), new int[3] { 350, 490, 630 }, new Tuple<BuildingEffectEnum, BuildingEffectIncrementType, float, float, float>[2]
		{
			new Tuple<BuildingEffectEnum, BuildingEffectIncrementType, float, float, float>(BuildingEffectEnum.Militia, BuildingEffectIncrementType.Add, 1f, 2f, 3f),
			new Tuple<BuildingEffectEnum, BuildingEffectIncrementType, float, float, float>(BuildingEffectEnum.PrisonCapacity, BuildingEffectIncrementType.Add, 10f, 30f, 50f)
		}, isMilitaryProject: true, 0f);
		_buildingCastleSiegeWorkshop.Initialize(new TextObject("{=9Bnwttn6}Siege Workshop"), new TextObject("{=YRCW0oFd}Builds and maintains siege engines for defense of the settlement."), new int[3] { 280, 420, 700 }, new Tuple<BuildingEffectEnum, BuildingEffectIncrementType, float, float, float>[3]
		{
			new Tuple<BuildingEffectEnum, BuildingEffectIncrementType, float, float, float>(BuildingEffectEnum.BallistaOnSiegeStart, BuildingEffectIncrementType.Add, 1f, 2f, 3f),
			new Tuple<BuildingEffectEnum, BuildingEffectIncrementType, float, float, float>(BuildingEffectEnum.CatapultOnSiegeStart, BuildingEffectIncrementType.Add, 0f, 1f, 2f),
			new Tuple<BuildingEffectEnum, BuildingEffectIncrementType, float, float, float>(BuildingEffectEnum.SiegeEngineSpeed, BuildingEffectIncrementType.AddFactor, 0.2f, 0.4f, 0.8f)
			new Tuple<BuildingEffectEnum, BuildingEffectIncrementType, float, float, float>(BuildingEffectEnum.GarrisonWageReduction, BuildingEffectIncrementType.AddFactor, -0.1f, -0.2f, -0.3f)
		}, isMilitaryProject: true, 0f);
		_buildingCastleTrainingFields.Initialize(new TextObject("{=BkTiRPT4}Training Fields"), new TextObject("{=otWlERkc}A field for military drills that increases the daily experience gain of all garrisoned units."), new int[3] { 420, 560, 700 }, new Tuple<BuildingEffectEnum, BuildingEffectIncrementType, float, float, float>[2]
		{
			new Tuple<BuildingEffectEnum, BuildingEffectIncrementType, float, float, float>(BuildingEffectEnum.ExperiencePerDay, BuildingEffectIncrementType.Add, 3f, 4f, 5f),
E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\TaleWorlds.CampaignSystem.Settlements.Buildings\DefaultBuildingTypes.cs:257: 			new Tuple<BuildingEffectEnum, BuildingEffectIncrementType, float, float, float>(BuildingEffectEnum.MilitiaVeterancyChance, BuildingEffectIncrementType.Add, 0.1f, 0.15f, 0.2f)
		}, isMilitaryProject: true, 0f);
		_buildingCastleGuardHouse.Initialize(new TextObject("{=OHEiwoHC}Guard House"), new TextObject("{=K0cbj7o3}Increase militia recruitment, and prisoner limit."), new int[3] { 350, 490, 630 }, new Tuple<BuildingEffectEnum, BuildingEffectIncrementType, float, float, float>[2]
		{
			new Tuple<BuildingEffectEnum, BuildingEffectIncrementType, float, float, float>(BuildingEffectEnum.Militia, BuildingEffectIncrementType.Add, 1f, 2f, 3f),
			new Tuple<BuildingEffectEnum, BuildingEffectIncrementType, float, float, float>(BuildingEffectEnum.PrisonCapacity, BuildingEffectIncrementType.Add, 10f, 30f, 50f)
		}, isMilitaryProject: true, 0f);
		_buildingCastleSiegeWorkshop.Initialize(new TextObject("{=9Bnwttn6}Siege Workshop"), new TextObject("{=YRCW0oFd}Builds and maintains siege engines for defense of the settlement."), new int[3] { 280, 420, 700 }, new Tuple<BuildingEffectEnum, BuildingEffectIncrementType, float, float, float>[3]
		{
			new Tuple<BuildingEffectEnum, BuildingEffectIncrementType, float, float, float>(BuildingEffectEnum.BallistaOnSiegeStart, BuildingEffectIncrementType.Add, 1f, 2f, 3f),
			new Tuple<BuildingEffectEnum, BuildingEffectIncrementType, float, float, float>(BuildingEffectEnum.CatapultOnSiegeStart, BuildingEffectIncrementType.Add, 0f, 1f, 2f),
			new Tuple<BuildingEffectEnum, BuildingEffectIncrementType, float, float, float>(BuildingEffectEnum.SiegeEngineSpeed, BuildingEffectIncrementType.AddFactor, 0.2f, 0.4f, 0.8f)
		}, isMilitaryProject: true, 0f);
		_buildingCastleTrainingFields.Initialize(new TextObject("{=BkTiRPT4}Training Fields"), new TextObject("{=otWlERkc}A field for military drills that increases the daily experience gain of all garrisoned units."), new int[3] { 420, 560, 700 }, new Tuple<BuildingEffectEnum, BuildingEffectIncrementType, float, float, float>[2]
		{
			new Tuple<BuildingEffectEnum, BuildingEffectIncrementType, float, float, float>(BuildingEffectEnum.ExperiencePerDay, BuildingEffectIncrementType.Add, 3f, 4f, 5f),
			new Tuple<BuildingEffectEnum, BuildingEffectIncrementType, float, float, float>(BuildingEffectEnum.MilitiaVeterancyChance, BuildingEffectIncrementType.Add, 0.1f, 0.15f, 0.2f)
		}, isMilitaryProject: true, 0f);
E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\TaleWorlds.CampaignSystem.Settlements.Buildings\DefaultBuildingTypes.cs:259: 		_buildingCastleGuardHouse.Initialize(new TextObject("{=OHEiwoHC}Guard House"), new TextObject("{=K0cbj7o3}Increase militia recruitment, and prisoner limit."), new int[3] { 350, 490, 630 }, new Tuple<BuildingEffectEnum, BuildingEffectIncrementType, float, float, float>[2]
		{
			new Tuple<BuildingEffectEnum, BuildingEffectIncrementType, float, float, float>(BuildingEffectEnum.Militia, BuildingEffectIncrementType.Add, 1f, 2f, 3f),
			new Tuple<BuildingEffectEnum, BuildingEffectIncrementType, float, float, float>(BuildingEffectEnum.PrisonCapacity, BuildingEffectIncrementType.Add, 10f, 30f, 50f)
		}, isMilitaryProject: true, 0f);
		_buildingCastleSiegeWorkshop.Initialize(new TextObject("{=9Bnwttn6}Siege Workshop"), new TextObject("{=YRCW0oFd}Builds and maintains siege engines for defense of the settlement."), new int[3] { 280, 420, 700 }, new Tuple<BuildingEffectEnum, BuildingEffectIncrementType, float, float, float>[3]
		{
			new Tuple<BuildingEffectEnum, BuildingEffectIncrementType, float, float, float>(BuildingEffectEnum.BallistaOnSiegeStart, BuildingEffectIncrementType.Add, 1f, 2f, 3f),
			new Tuple<BuildingEffectEnum, BuildingEffectIncrementType, float, float, float>(BuildingEffectEnum.CatapultOnSiegeStart, BuildingEffectIncrementType.Add, 0f, 1f, 2f),
			new Tuple<BuildingEffectEnum, BuildingEffectIncrementType, float, float, float>(BuildingEffectEnum.SiegeEngineSpeed, BuildingEffectIncrementType.AddFactor, 0.2f, 0.4f, 0.8f)
		}, isMilitaryProject: true, 0f);
		_buildingCastleCastallansOffice.Initialize(new TextObject("{=kLNnFMR9}Castellan's Office"), new TextObject("{=GDsI6daq}Increases auto recruitment, and decreases garrison wage."), new int[3] { 560, 840, 1260 }, new Tuple<BuildingEffectEnum, BuildingEffectIncrementType, float, float, float>[2]
		{
			new Tuple<BuildingEffectEnum, BuildingEffectIncrementType, float, float, float>(BuildingEffectEnum.ExperiencePerDay, BuildingEffectIncrementType.Add, 3f, 4f, 5f),
			new Tuple<BuildingEffectEnum, BuildingEffectIncrementType, float, float, float>(BuildingEffectEnum.MilitiaVeterancyChance, BuildingEffectIncrementType.Add, 0.1f, 0.15f, 0.2f)
		}, isMilitaryProject: true, 0f);
		_buildingCastleGuardHouse.Initialize(new TextObject("{=OHEiwoHC}Guard House"), new TextObject("{=K0cbj7o3}Increase militia recruitment, and prisoner limit."), new int[3] { 350, 490, 630 }, new Tuple<BuildingEffectEnum, BuildingEffectIncrementType, float, float, float>[2]
		{
E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\TaleWorlds.CampaignSystem.Settlements.Buildings\DefaultBuildingTypes.cs:261: 			new Tuple<BuildingEffectEnum, BuildingEffectIncrementType, float, float, float>(BuildingEffectEnum.Militia, BuildingEffectIncrementType.Add, 1f, 2f, 3f),
			new Tuple<BuildingEffectEnum, BuildingEffectIncrementType, float, float, float>(BuildingEffectEnum.PrisonCapacity, BuildingEffectIncrementType.Add, 10f, 30f, 50f)
		}, isMilitaryProject: true, 0f);
		_buildingCastleSiegeWorkshop.Initialize(new TextObject("{=9Bnwttn6}Siege Workshop"), new TextObject("{=YRCW0oFd}Builds and maintains siege engines for defense of the settlement."), new int[3] { 280, 420, 700 }, new Tuple<BuildingEffectEnum, BuildingEffectIncrementType, float, float, float>[3]
		{
			new Tuple<BuildingEffectEnum, BuildingEffectIncrementType, float, float, float>(BuildingEffectEnum.BallistaOnSiegeStart, BuildingEffectIncrementType.Add, 1f, 2f, 3f),
			new Tuple<BuildingEffectEnum, BuildingEffectIncrementType, float, float, float>(BuildingEffectEnum.CatapultOnSiegeStart, BuildingEffectIncrementType.Add, 0f, 1f, 2f),
			new Tuple<BuildingEffectEnum, BuildingEffectIncrementType, float, float, float>(BuildingEffectEnum.SiegeEngineSpeed, BuildingEffectIncrementType.AddFactor, 0.2f, 0.4f, 0.8f)
		}, isMilitaryProject: true, 0f);
		_buildingCastleCastallansOffice.Initialize(new TextObject("{=kLNnFMR9}Castellan's Office"), new TextObject("{=GDsI6daq}Increases auto recruitment, and decreases garrison wage."), new int[3] { 560, 840, 1260 }, new Tuple<BuildingEffectEnum, BuildingEffectIncrementType, float, float, float>[2]
		{
			new Tuple<BuildingEffectEnum, BuildingEffectIncrementType, float, float, float>(BuildingEffectEnum.GarrisonWageReduction, BuildingEffectIncrementType.AddFactor, -0.1f, -0.2f, -0.3f),
			new Tuple<BuildingEffectEnum, BuildingEffectIncrementType, float, float, float>(BuildingEffectEnum.GarrisonAutoRecruitment, BuildingEffectIncrementType.Add, 1f, 2f, 3f)
			new Tuple<BuildingEffectEnum, BuildingEffectIncrementType, float, float, float>(BuildingEffectEnum.MilitiaVeterancyChance, BuildingEffectIncrementType.Add, 0.1f, 0.15f, 0.2f)
		}, isMilitaryProject: true, 0f);
		_buildingCastleGuardHouse.Initialize(new TextObject("{=OHEiwoHC}Guard House"), new TextObject("{=K0cbj7o3}Increase militia recruitment, and prisoner limit."), new int[3] { 350, 490, 630 }, new Tuple<BuildingEffectEnum, BuildingEffectIncrementType, float, float, float>[2]
		{
			new Tuple<BuildingEffectEnum, BuildingEffectIncrementType, float, float, float>(BuildingEffectEnum.Militia, BuildingEffectIncrementType.Add, 1f, 2f, 3f),
E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\TaleWorlds.CampaignSystem.Settlements.Buildings\DefaultBuildingTypes.cs:262: 			new Tuple<BuildingEffectEnum, BuildingEffectIncrementType, float, float, float>(BuildingEffectEnum.PrisonCapacity, BuildingEffectIncrementType.Add, 10f, 30f, 50f)
		}, isMilitaryProject: true, 0f);
		_buildingCastleSiegeWorkshop.Initialize(new TextObject("{=9Bnwttn6}Siege Workshop"), new TextObject("{=YRCW0oFd}Builds and maintains siege engines for defense of the settlement."), new int[3] { 280, 420, 700 }, new Tuple<BuildingEffectEnum, BuildingEffectIncrementType, float, float, float>[3]
		{
			new Tuple<BuildingEffectEnum, BuildingEffectIncrementType, float, float, float>(BuildingEffectEnum.BallistaOnSiegeStart, BuildingEffectIncrementType.Add, 1f, 2f, 3f),
			new Tuple<BuildingEffectEnum, BuildingEffectIncrementType, float, float, float>(BuildingEffectEnum.CatapultOnSiegeStart, BuildingEffectIncrementType.Add, 0f, 1f, 2f),
			new Tuple<BuildingEffectEnum, BuildingEffectIncrementType, float, float, float>(BuildingEffectEnum.SiegeEngineSpeed, BuildingEffectIncrementType.AddFactor, 0.2f, 0.4f, 0.8f)
		}, isMilitaryProject: true, 0f);
		_buildingCastleCastallansOffice.Initialize(new TextObject("{=kLNnFMR9}Castellan's Office"), new TextObject("{=GDsI6daq}Increases auto recruitment, and decreases garrison wage."), new int[3] { 560, 840, 1260 }, new Tuple<BuildingEffectEnum, BuildingEffectIncrementType, float, float, float>[2]
		{
			new Tuple<BuildingEffectEnum, BuildingEffectIncrementType, float, float, float>(BuildingEffectEnum.GarrisonWageReduction, BuildingEffectIncrementType.AddFactor, -0.1f, -0.2f, -0.3f),
			new Tuple<BuildingEffectEnum, BuildingEffectIncrementType, float, float, float>(BuildingEffectEnum.GarrisonAutoRecruitment, BuildingEffectIncrementType.Add, 1f, 2f, 3f)
		}, isMilitaryProject: true, 0f);
		_buildingCastleGuardHouse.Initialize(new TextObject("{=OHEiwoHC}Guard House"), new TextObject("{=K0cbj7o3}Increase militia recruitment, and prisoner limit."), new int[3] { 350, 490, 630 }, new Tuple<BuildingEffectEnum, BuildingEffectIncrementType, float, float, float>[2]
		{
			new Tuple<BuildingEffectEnum, BuildingEffectIncrementType, float, float, float>(BuildingEffectEnum.Militia, BuildingEffectIncrementType.Add, 1f, 2f, 3f),
			new Tuple<BuildingEffectEnum, BuildingEffectIncrementType, float, float, float>(BuildingEffectEnum.PrisonCapacity, BuildingEffectIncrementType.Add, 10f, 30f, 50f)
		}, isMilitaryProject: true, 0f);
E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\TaleWorlds.CampaignSystem.Settlements.Buildings\DefaultBuildingTypes.cs:264: 		_buildingCastleSiegeWorkshop.Initialize(new TextObject("{=9Bnwttn6}Siege Workshop"), new TextObject("{=YRCW0oFd}Builds and maintains siege engines for defense of the settlement."), new int[3] { 280, 420, 700 }, new Tuple<BuildingEffectEnum, BuildingEffectIncrementType, float, float, float>[3]
		{
			new Tuple<BuildingEffectEnum, BuildingEffectIncrementType, float, float, float>(BuildingEffectEnum.BallistaOnSiegeStart, BuildingEffectIncrementType.Add, 1f, 2f, 3f),
			new Tuple<BuildingEffectEnum, BuildingEffectIncrementType, float, float, float>(BuildingEffectEnum.CatapultOnSiegeStart, BuildingEffectIncrementType.Add, 0f, 1f, 2f),
			new Tuple<BuildingEffectEnum, BuildingEffectIncrementType, float, float, float>(BuildingEffectEnum.SiegeEngineSpeed, BuildingEffectIncrementType.AddFactor, 0.2f, 0.4f, 0.8f)
		}, isMilitaryProject: true, 0f);
		_buildingCastleCastallansOffice.Initialize(new TextObject("{=kLNnFMR9}Castellan's Office"), new TextObject("{=GDsI6daq}Increases auto recruitment, and decreases garrison wage."), new int[3] { 560, 840, 1260 }, new Tuple<BuildingEffectEnum, BuildingEffectIncrementType, float, float, float>[2]
		{
			new Tuple<BuildingEffectEnum, BuildingEffectIncrementType, float, float, float>(BuildingEffectEnum.GarrisonWageReduction, BuildingEffectIncrementType.AddFactor, -0.1f, -0.2f, -0.3f),
			new Tuple<BuildingEffectEnum, BuildingEffectIncrementType, float, float, float>(BuildingEffectEnum.GarrisonAutoRecruitment, BuildingEffectIncrementType.Add, 1f, 2f, 3f)
		}, isMilitaryProject: true, 0f);
		_buildingCastleGranary.Initialize(new TextObject("{=PstO2f5I}Granary"), new TextObject("{=iazij7fO}Increases food storage limits."), new int[3] { 420, 560, 700 }, new Tuple<BuildingEffectEnum, BuildingEffectIncrementType, float, float, float>[1]
		{
			new Tuple<BuildingEffectEnum, BuildingEffectIncrementType, float, float, float>(BuildingEffectEnum.Militia, BuildingEffectIncrementType.Add, 1f, 2f, 3f),
			new Tuple<BuildingEffectEnum, BuildingEffectIncrementType, float, float, float>(BuildingEffectEnum.PrisonCapacity, BuildingEffectIncrementType.Add, 10f, 30f, 50f)
		}, isMilitaryProject: true, 0f);
		_buildingCastleSiegeWorkshop.Initialize(new TextObject("{=9Bnwttn6}Siege Workshop"), new TextObject("{=YRCW0oFd}Builds and maintains siege engines for defense of the settlement."), new int[3] { 280, 420, 700 }, new Tuple<BuildingEffectEnum, BuildingEffectIncrementType, float, float, float>[3]
		{
E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\TaleWorlds.CampaignSystem.Settlements.Buildings\DefaultBuildingTypes.cs:266: 			new Tuple<BuildingEffectEnum, BuildingEffectIncrementType, float, float, float>(BuildingEffectEnum.BallistaOnSiegeStart, BuildingEffectIncrementType.Add, 1f, 2f, 3f),
			new Tuple<BuildingEffectEnum, BuildingEffectIncrementType, float, float, float>(BuildingEffectEnum.CatapultOnSiegeStart, BuildingEffectIncrementType.Add, 0f, 1f, 2f),
			new Tuple<BuildingEffectEnum, BuildingEffectIncrementType, float, float, float>(BuildingEffectEnum.SiegeEngineSpeed, BuildingEffectIncrementType.AddFactor, 0.2f, 0.4f, 0.8f)
		}, isMilitaryProject: true, 0f);
		_buildingCastleCastallansOffice.Initialize(new TextObject("{=kLNnFMR9}Castellan's Office"), new TextObject("{=GDsI6daq}Increases auto recruitment, and decreases garrison wage."), new int[3] { 560, 840, 1260 }, new Tuple<BuildingEffectEnum, BuildingEffectIncrementType, float, float, float>[2]
		{
			new Tuple<BuildingEffectEnum, BuildingEffectIncrementType, float, float, float>(BuildingEffectEnum.GarrisonWageReduction, BuildingEffectIncrementType.AddFactor, -0.1f, -0.2f, -0.3f),
			new Tuple<BuildingEffectEnum, BuildingEffectIncrementType, float, float, float>(BuildingEffectEnum.GarrisonAutoRecruitment, BuildingEffectIncrementType.Add, 1f, 2f, 3f)
		}, isMilitaryProject: true, 0f);
		_buildingCastleGranary.Initialize(new TextObject("{=PstO2f5I}Granary"), new TextObject("{=iazij7fO}Increases food storage limits."), new int[3] { 420, 560, 700 }, new Tuple<BuildingEffectEnum, BuildingEffectIncrementType, float, float, float>[1]
		{
			new Tuple<BuildingEffectEnum, BuildingEffectIncrementType, float, float, float>(BuildingEffectEnum.FoodStock, BuildingEffectIncrementType.Add, 100f, 200f, 300f)
		}, isMilitaryProject: false, 0f);
			new Tuple<BuildingEffectEnum, BuildingEffectIncrementType, float, float, float>(BuildingEffectEnum.PrisonCapacity, BuildingEffectIncrementType.Add, 10f, 30f, 50f)
		}, isMilitaryProject: true, 0f);
		_buildingCastleSiegeWorkshop.Initialize(new TextObject("{=9Bnwttn6}Siege Workshop"), new TextObject("{=YRCW0oFd}Builds and maintains siege engines for defense of the settlement."), new int[3] { 280, 420, 700 }, new Tuple<BuildingEffectEnum, BuildingEffectIncrementType, float, float, float>[3]
		{
			new Tuple<BuildingEffectEnum, BuildingEffectIncrementType, float, float, float>(BuildingEffectEnum.BallistaOnSiegeStart, BuildingEffectIncrementType.Add, 1f, 2f, 3f),
E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\TaleWorlds.CampaignSystem.Settlements.Buildings\DefaultBuildingTypes.cs:267: 			new Tuple<BuildingEffectEnum, BuildingEffectIncrementType, float, float, float>(BuildingEffectEnum.CatapultOnSiegeStart, BuildingEffectIncrementType.Add, 0f, 1f, 2f),
			new Tuple<BuildingEffectEnum, BuildingEffectIncrementType, float, float, float>(BuildingEffectEnum.SiegeEngineSpeed, BuildingEffectIncrementType.AddFactor, 0.2f, 0.4f, 0.8f)
		}, isMilitaryProject: true, 0f);
		_buildingCastleCastallansOffice.Initialize(new TextObject("{=kLNnFMR9}Castellan's Office"), new TextObject("{=GDsI6daq}Increases auto recruitment, and decreases garrison wage."), new int[3] { 560, 840, 1260 }, new Tuple<BuildingEffectEnum, BuildingEffectIncrementType, float, float, float>[2]
		{
			new Tuple<BuildingEffectEnum, BuildingEffectIncrementType, float, float, float>(BuildingEffectEnum.GarrisonWageReduction, BuildingEffectIncrementType.AddFactor, -0.1f, -0.2f, -0.3f),
			new Tuple<BuildingEffectEnum, BuildingEffectIncrementType, float, float, float>(BuildingEffectEnum.GarrisonAutoRecruitment, BuildingEffectIncrementType.Add, 1f, 2f, 3f)
		}, isMilitaryProject: true, 0f);
		_buildingCastleGranary.Initialize(new TextObject("{=PstO2f5I}Granary"), new TextObject("{=iazij7fO}Increases food storage limits."), new int[3] { 420, 560, 700 }, new Tuple<BuildingEffectEnum, BuildingEffectIncrementType, float, float, float>[1]
		{
			new Tuple<BuildingEffectEnum, BuildingEffectIncrementType, float, float, float>(BuildingEffectEnum.FoodStock, BuildingEffectIncrementType.Add, 100f, 200f, 300f)
		}, isMilitaryProject: false, 0f);
		_buildingCastleCraftmansQuarters.Initialize(new TextObject("{=KE1KUayw}Craftmans Quarters"), new TextObject("{=2qZ14G9p}Provides income based on bound village hearts"), new int[3] { 350, 490, 630 }, new Tuple<BuildingEffectEnum, BuildingEffectIncrementType, float, float, float>[1]
		}, isMilitaryProject: true, 0f);
		_buildingCastleSiegeWorkshop.Initialize(new TextObject("{=9Bnwttn6}Siege Workshop"), new TextObject("{=YRCW0oFd}Builds and maintains siege engines for defense of the settlement."), new int[3] { 280, 420, 700 }, new Tuple<BuildingEffectEnum, BuildingEffectIncrementType, float, float, float>[3]
		{
			new Tuple<BuildingEffectEnum, BuildingEffectIncrementType, float, float, float>(BuildingEffectEnum.BallistaOnSiegeStart, BuildingEffectIncrementType.Add, 1f, 2f, 3f),
			new Tuple<BuildingEffectEnum, BuildingEffectIncrementType, float, float, float>(BuildingEffectEnum.CatapultOnSiegeStart, BuildingEffectIncrementType.Add, 0f, 1f, 2f),
E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\TaleWorlds.CampaignSystem.Settlements.Buildings\DefaultBuildingTypes.cs:268: 			new Tuple<BuildingEffectEnum, BuildingEffectIncrementType, float, float, float>(BuildingEffectEnum.SiegeEngineSpeed, BuildingEffectIncrementType.AddFactor, 0.2f, 0.4f, 0.8f)
		}, isMilitaryProject: true, 0f);
		_buildingCastleCastallansOffice.Initialize(new TextObject("{=kLNnFMR9}Castellan's Office"), new TextObject("{=GDsI6daq}Increases auto recruitment, and decreases garrison wage."), new int[3] { 560, 840, 1260 }, new Tuple<BuildingEffectEnum, BuildingEffectIncrementType, float, float, float>[2]
		{
			new Tuple<BuildingEffectEnum, BuildingEffectIncrementType, float, float, float>(BuildingEffectEnum.GarrisonWageReduction, BuildingEffectIncrementType.AddFactor, -0.1f, -0.2f, -0.3f),
			new Tuple<BuildingEffectEnum, BuildingEffectIncrementType, float, float, float>(BuildingEffectEnum.GarrisonAutoRecruitment, BuildingEffectIncrementType.Add, 1f, 2f, 3f)
		}, isMilitaryProject: true, 0f);
		_buildingCastleGranary.Initialize(new TextObject("{=PstO2f5I}Granary"), new TextObject("{=iazij7fO}Increases food storage limits."), new int[3] { 420, 560, 700 }, new Tuple<BuildingEffectEnum, BuildingEffectIncrementType, float, float, float>[1]
		{
			new Tuple<BuildingEffectEnum, BuildingEffectIncrementType, float, float, float>(BuildingEffectEnum.FoodStock, BuildingEffectIncrementType.Add, 100f, 200f, 300f)
		}, isMilitaryProject: false, 0f);
		_buildingCastleCraftmansQuarters.Initialize(new TextObject("{=KE1KUayw}Craftmans Quarters"), new TextObject("{=2qZ14G9p}Provides income based on bound village hearts"), new int[3] { 350, 490, 630 }, new Tuple<BuildingEffectEnum, BuildingEffectIncrementType, float, float, float>[1]
		{
		{
			new Tuple<BuildingEffectEnum, BuildingEffectIncrementType, float, float, float>(BuildingEffectEnum.BallistaOnSiegeStart, BuildingEffectIncrementType.Add, 1f, 2f, 3f),
			new Tuple<BuildingEffectEnum, BuildingEffectIncrementType, float, float, float>(BuildingEffectEnum.CatapultOnSiegeStart, BuildingEffectIncrementType.Add, 0f, 1f, 2f),
			new Tuple<BuildingEffectEnum, BuildingEffectIncrementType, float, float, float>(BuildingEffectEnum.SiegeEngineSpeed, BuildingEffectIncrementType.AddFactor, 0.2f, 0.4f, 0.8f)
		}, isMilitaryProject: true, 0f);
E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\TaleWorlds.CampaignSystem.Settlements.Buildings\DefaultBuildingTypes.cs:270: 		_buildingCastleCastallansOffice.Initialize(new TextObject("{=kLNnFMR9}Castellan's Office"), new TextObject("{=GDsI6daq}Increases auto recruitment, and decreases garrison wage."), new int[3] { 560, 840, 1260 }, new Tuple<BuildingEffectEnum, BuildingEffectIncrementType, float, float, float>[2]
		{
			new Tuple<BuildingEffectEnum, BuildingEffectIncrementType, float, float, float>(BuildingEffectEnum.GarrisonWageReduction, BuildingEffectIncrementType.AddFactor, -0.1f, -0.2f, -0.3f),
			new Tuple<BuildingEffectEnum, BuildingEffectIncrementType, float, float, float>(BuildingEffectEnum.GarrisonAutoRecruitment, BuildingEffectIncrementType.Add, 1f, 2f, 3f)
		}, isMilitaryProject: true, 0f);
		_buildingCastleGranary.Initialize(new TextObject("{=PstO2f5I}Granary"), new TextObject("{=iazij7fO}Increases food storage limits."), new int[3] { 420, 560, 700 }, new Tuple<BuildingEffectEnum, BuildingEffectIncrementType, float, float, float>[1]
		{
			new Tuple<BuildingEffectEnum, BuildingEffectIncrementType, float, float, float>(BuildingEffectEnum.FoodStock, BuildingEffectIncrementType.Add, 100f, 200f, 300f)
		}, isMilitaryProject: false, 0f);
		_buildingCastleCraftmansQuarters.Initialize(new TextObject("{=KE1KUayw}Craftmans Quarters"), new TextObject("{=2qZ14G9p}Provides income based on bound village hearts"), new int[3] { 350, 490, 630 }, new Tuple<BuildingEffectEnum, BuildingEffectIncrementType, float, float, float>[1]
		{
			new Tuple<BuildingEffectEnum, BuildingEffectIncrementType, float, float, float>(BuildingEffectEnum.DenarByBoundVillageHeartPerDay, BuildingEffectIncrementType.Add, 0.2f, 0.4f, 0.6f)
		}, isMilitaryProject: false, 0f);
			new Tuple<BuildingEffectEnum, BuildingEffectIncrementType, float, float, float>(BuildingEffectEnum.CatapultOnSiegeStart, BuildingEffectIncrementType.Add, 0f, 1f, 2f),
			new Tuple<BuildingEffectEnum, BuildingEffectIncrementType, float, float, float>(BuildingEffectEnum.SiegeEngineSpeed, BuildingEffectIncrementType.AddFactor, 0.2f, 0.4f, 0.8f)
		}, isMilitaryProject: true, 0f);
		_buildingCastleCastallansOffice.Initialize(new TextObject("{=kLNnFMR9}Castellan's Office"), new TextObject("{=GDsI6daq}Increases auto recruitment, and decreases garrison wage."), new int[3] { 560, 840, 1260 }, new Tuple<BuildingEffectEnum, BuildingEffectIncrementType, float, float, float>[2]
		{
E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\TaleWorlds.CampaignSystem.Settlements.Buildings\DefaultBuildingTypes.cs:272: 			new Tuple<BuildingEffectEnum, BuildingEffectIncrementType, float, float, float>(BuildingEffectEnum.GarrisonWageReduction, BuildingEffectIncrementType.AddFactor, -0.1f, -0.2f, -0.3f),
			new Tuple<BuildingEffectEnum, BuildingEffectIncrementType, float, float, float>(BuildingEffectEnum.GarrisonAutoRecruitment, BuildingEffectIncrementType.Add, 1f, 2f, 3f)
		}, isMilitaryProject: true, 0f);
		_buildingCastleGranary.Initialize(new TextObject("{=PstO2f5I}Granary"), new TextObject("{=iazij7fO}Increases food storage limits."), new int[3] { 420, 560, 700 }, new Tuple<BuildingEffectEnum, BuildingEffectIncrementType, float, float, float>[1]
		{
			new Tuple<BuildingEffectEnum, BuildingEffectIncrementType, float, float, float>(BuildingEffectEnum.FoodStock, BuildingEffectIncrementType.Add, 100f, 200f, 300f)
		}, isMilitaryProject: false, 0f);
		_buildingCastleCraftmansQuarters.Initialize(new TextObject("{=KE1KUayw}Craftmans Quarters"), new TextObject("{=2qZ14G9p}Provides income based on bound village hearts"), new int[3] { 350, 490, 630 }, new Tuple<BuildingEffectEnum, BuildingEffectIncrementType, float, float, float>[1]
		{
			new Tuple<BuildingEffectEnum, BuildingEffectIncrementType, float, float, float>(BuildingEffectEnum.DenarByBoundVillageHeartPerDay, BuildingEffectIncrementType.Add, 0.2f, 0.4f, 0.6f)
		}, isMilitaryProject: false, 0f);
		_buildingCastleFarmlands.Initialize(new TextObject("{=l4eZqegY}Farmlands"), new TextObject("{=tajCl8Bg}Provides daily food."), new int[3] { 420, 630, 840 }, new Tuple<BuildingEffectEnum, BuildingEffectIncrementType, float, float, float>[1]
		{
			new Tuple<BuildingEffectEnum, BuildingEffectIncrementType, float, float, float>(BuildingEffectEnum.SiegeEngineSpeed, BuildingEffectIncrementType.AddFactor, 0.2f, 0.4f, 0.8f)
		}, isMilitaryProject: true, 0f);
		_buildingCastleCastallansOffice.Initialize(new TextObject("{=kLNnFMR9}Castellan's Office"), new TextObject("{=GDsI6daq}Increases auto recruitment, and decreases garrison wage."), new int[3] { 560, 840, 1260 }, new Tuple<BuildingEffectEnum, BuildingEffectIncrementType, float, float, float>[2]
		{
			new Tuple<BuildingEffectEnum, BuildingEffectIncrementType, float, float, float>(BuildingEffectEnum.GarrisonWageReduction, BuildingEffectIncrementType.AddFactor, -0.1f, -0.2f, -0.3f),
E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\TaleWorlds.CampaignSystem.Settlements.Buildings\DefaultBuildingTypes.cs:273: 			new Tuple<BuildingEffectEnum, BuildingEffectIncrementType, float, float, float>(BuildingEffectEnum.GarrisonAutoRecruitment, BuildingEffectIncrementType.Add, 1f, 2f, 3f)
		}, isMilitaryProject: true, 0f);
		_buildingCastleGranary.Initialize(new TextObject("{=PstO2f5I}Granary"), new TextObject("{=iazij7fO}Increases food storage limits."), new int[3] { 420, 560, 700 }, new Tuple<BuildingEffectEnum, BuildingEffectIncrementType, float, float, float>[1]
		{
			new Tuple<BuildingEffectEnum, BuildingEffectIncrementType, float, float, float>(BuildingEffectEnum.FoodStock, BuildingEffectIncrementType.Add, 100f, 200f, 300f)
		}, isMilitaryProject: false, 0f);
		_buildingCastleCraftmansQuarters.Initialize(new TextObject("{=KE1KUayw}Craftmans Quarters"), new TextObject("{=2qZ14G9p}Provides income based on bound village hearts"), new int[3] { 350, 490, 630 }, new Tuple<BuildingEffectEnum, BuildingEffectIncrementType, float, float, float>[1]
		{
			new Tuple<BuildingEffectEnum, BuildingEffectIncrementType, float, float, float>(BuildingEffectEnum.DenarByBoundVillageHeartPerDay, BuildingEffectIncrementType.Add, 0.2f, 0.4f, 0.6f)
		}, isMilitaryProject: false, 0f);
		_buildingCastleFarmlands.Initialize(new TextObject("{=l4eZqegY}Farmlands"), new TextObject("{=tajCl8Bg}Provides daily food."), new int[3] { 420, 630, 840 }, new Tuple<BuildingEffectEnum, BuildingEffectIncrementType, float, float, float>[1]
		{
			new Tuple<BuildingEffectEnum, BuildingEffectIncrementType, float, float, float>(BuildingEffectEnum.FoodProduction, BuildingEffectIncrementType.Add, 6f, 12f, 18f)
		_buildingCastleCastallansOffice.Initialize(new TextObject("{=kLNnFMR9}Castellan's Office"), new TextObject("{=GDsI6daq}Increases auto recruitment, and decreases garrison wage."), new int[3] { 560, 840, 1260 }, new Tuple<BuildingEffectEnum, BuildingEffectIncrementType, float, float, float>[2]
		{
			new Tuple<BuildingEffectEnum, BuildingEffectIncrementType, float, float, float>(BuildingEffectEnum.GarrisonWageReduction, BuildingEffectIncrementType.AddFactor, -0.1f, -0.2f, -0.3f),
			new Tuple<BuildingEffectEnum, BuildingEffectIncrementType, float, float, float>(BuildingEffectEnum.GarrisonAutoRecruitment, BuildingEffectIncrementType.Add, 1f, 2f, 3f)
		}, isMilitaryProject: true, 0f);
E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\TaleWorlds.CampaignSystem.Settlements.Buildings\DefaultBuildingTypes.cs:275: 		_buildingCastleGranary.Initialize(new TextObject("{=PstO2f5I}Granary"), new TextObject("{=iazij7fO}Increases food storage limits."), new int[3] { 420, 560, 700 }, new Tuple<BuildingEffectEnum, BuildingEffectIncrementType, float, float, float>[1]
		{
			new Tuple<BuildingEffectEnum, BuildingEffectIncrementType, float, float, float>(BuildingEffectEnum.FoodStock, BuildingEffectIncrementType.Add, 100f, 200f, 300f)
		}, isMilitaryProject: false, 0f);
		_buildingCastleCraftmansQuarters.Initialize(new TextObject("{=KE1KUayw}Craftmans Quarters"), new TextObject("{=2qZ14G9p}Provides income based on bound village hearts"), new int[3] { 350, 490, 630 }, new Tuple<BuildingEffectEnum, BuildingEffectIncrementType, float, float, float>[1]
		{
			new Tuple<BuildingEffectEnum, BuildingEffectIncrementType, float, float, float>(BuildingEffectEnum.DenarByBoundVillageHeartPerDay, BuildingEffectIncrementType.Add, 0.2f, 0.4f, 0.6f)
		}, isMilitaryProject: false, 0f);
		_buildingCastleFarmlands.Initialize(new TextObject("{=l4eZqegY}Farmlands"), new TextObject("{=tajCl8Bg}Provides daily food."), new int[3] { 420, 630, 840 }, new Tuple<BuildingEffectEnum, BuildingEffectIncrementType, float, float, float>[1]
		{
			new Tuple<BuildingEffectEnum, BuildingEffectIncrementType, float, float, float>(BuildingEffectEnum.FoodProduction, BuildingEffectIncrementType.Add, 6f, 12f, 18f)
		}, isMilitaryProject: false, 0f);
		_buildingCastleMason.Initialize(new TextObject("{=R7ssoDHW}Mason"), new TextObject("{=hqUPvnaj}Increase bricks per day, increasing building and repair speed."), new int[3] { 560, 700, 1120 }, new Tuple<BuildingEffectEnum, BuildingEffectIncrementType, float, float, float>[2]
			new Tuple<BuildingEffectEnum, BuildingEffectIncrementType, float, float, float>(BuildingEffectEnum.GarrisonWageReduction, BuildingEffectIncrementType.AddFactor, -0.1f, -0.2f, -0.3f),
			new Tuple<BuildingEffectEnum, BuildingEffectIncrementType, float, float, float>(BuildingEffectEnum.GarrisonAutoRecruitment, BuildingEffectIncrementType.Add, 1f, 2f, 3f)
		}, isMilitaryProject: true, 0f);
		_buildingCastleGranary.Initialize(new TextObject("{=PstO2f5I}Granary"), new TextObject("{=iazij7fO}Increases food storage limits."), new int[3] { 420, 560, 700 }, new Tuple<BuildingEffectEnum, BuildingEffectIncrementType, float, float, float>[1]
		{
E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\TaleWorlds.CampaignSystem.Settlements.Buildings\DefaultBuildingTypes.cs:277: 			new Tuple<BuildingEffectEnum, BuildingEffectIncrementType, float, float, float>(BuildingEffectEnum.FoodStock, BuildingEffectIncrementType.Add, 100f, 200f, 300f)
		}, isMilitaryProject: false, 0f);
		_buildingCastleCraftmansQuarters.Initialize(new TextObject("{=KE1KUayw}Craftmans Quarters"), new TextObject("{=2qZ14G9p}Provides income based on bound village hearts"), new int[3] { 350, 490, 630 }, new Tuple<BuildingEffectEnum, BuildingEffectIncrementType, float, float, float>[1]
		{
			new Tuple<BuildingEffectEnum, BuildingEffectIncrementType, float, float, float>(BuildingEffectEnum.DenarByBoundVillageHeartPerDay, BuildingEffectIncrementType.Add, 0.2f, 0.4f, 0.6f)
		}, isMilitaryProject: false, 0f);
		_buildingCastleFarmlands.Initialize(new TextObject("{=l4eZqegY}Farmlands"), new TextObject("{=tajCl8Bg}Provides daily food."), new int[3] { 420, 630, 840 }, new Tuple<BuildingEffectEnum, BuildingEffectIncrementType, float, float, float>[1]
		{
			new Tuple<BuildingEffectEnum, BuildingEffectIncrementType, float, float, float>(BuildingEffectEnum.FoodProduction, BuildingEffectIncrementType.Add, 6f, 12f, 18f)
		}, isMilitaryProject: false, 0f);
		_buildingCastleMason.Initialize(new TextObject("{=R7ssoDHW}Mason"), new TextObject("{=hqUPvnaj}Increase bricks per day, increasing building and repair speed."), new int[3] { 560, 700, 1120 }, new Tuple<BuildingEffectEnum, BuildingEffectIncrementType, float, float, float>[2]
		{
			new Tuple<BuildingEffectEnum, BuildingEffectIncrementType, float, float, float>(BuildingEffectEnum.ConstructionPerDay, BuildingEffectIncrementType.Add, 2f, 4f, 6f),
		}, isMilitaryProject: true, 0f);
		_buildingCastleGranary.Initialize(new TextObject("{=PstO2f5I}Granary"), new TextObject("{=iazij7fO}Increases food storage limits."), new int[3] { 420, 560, 700 }, new Tuple<BuildingEffectEnum, BuildingEffectIncrementType, float, float, float>[1]
		{
			new Tuple<BuildingEffectEnum, BuildingEffectIncrementType, float, float, float>(BuildingEffectEnum.FoodStock, BuildingEffectIncrementType.Add, 100f, 200f, 300f)
		}, isMilitaryProject: false, 0f);
E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\TaleWorlds.CampaignSystem.Settlements.Buildings\DefaultBuildingTypes.cs:279: 		_buildingCastleCraftmansQuarters.Initialize(new TextObject("{=KE1KUayw}Craftmans Quarters"), new TextObject("{=2qZ14G9p}Provides income based on bound village hearts"), new int[3] { 350, 490, 630 }, new Tuple<BuildingEffectEnum, BuildingEffectIncrementType, float, float, float>[1]
		{
			new Tuple<BuildingEffectEnum, BuildingEffectIncrementType, float, float, float>(BuildingEffectEnum.DenarByBoundVillageHeartPerDay, BuildingEffectIncrementType.Add, 0.2f, 0.4f, 0.6f)
		}, isMilitaryProject: false, 0f);
		_buildingCastleFarmlands.Initialize(new TextObject("{=l4eZqegY}Farmlands"), new TextObject("{=tajCl8Bg}Provides daily food."), new int[3] { 420, 630, 840 }, new Tuple<BuildingEffectEnum, BuildingEffectIncrementType, float, float, float>[1]
		{
			new Tuple<BuildingEffectEnum, BuildingEffectIncrementType, float, float, float>(BuildingEffectEnum.FoodProduction, BuildingEffectIncrementType.Add, 6f, 12f, 18f)
		}, isMilitaryProject: false, 0f);
		_buildingCastleMason.Initialize(new TextObject("{=R7ssoDHW}Mason"), new TextObject("{=hqUPvnaj}Increase bricks per day, increasing building and repair speed."), new int[3] { 560, 700, 1120 }, new Tuple<BuildingEffectEnum, BuildingEffectIncrementType, float, float, float>[2]
		{
			new Tuple<BuildingEffectEnum, BuildingEffectIncrementType, float, float, float>(BuildingEffectEnum.ConstructionPerDay, BuildingEffectIncrementType.Add, 2f, 4f, 6f),
			new Tuple<BuildingEffectEnum, BuildingEffectIncrementType, float, float, float>(BuildingEffectEnum.WallRepairSpeed, BuildingEffectIncrementType.AddFactor, 0.1f, 0.3f, 0.6f)
		}, isMilitaryProject: false, 0f);
		{
			new Tuple<BuildingEffectEnum, BuildingEffectIncrementType, float, float, float>(BuildingEffectEnum.FoodStock, BuildingEffectIncrementType.Add, 100f, 200f, 300f)
		}, isMilitaryProject: false, 0f);
		_buildingCastleCraftmansQuarters.Initialize(new TextObject("{=KE1KUayw}Craftmans Quarters"), new TextObject("{=2qZ14G9p}Provides income based on bound village hearts"), new int[3] { 350, 490, 630 }, new Tuple<BuildingEffectEnum, BuildingEffectIncrementType, float, float, float>[1]
		{
E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\TaleWorlds.CampaignSystem.Settlements.Buildings\DefaultBuildingTypes.cs:281: 			new Tuple<BuildingEffectEnum, BuildingEffectIncrementType, float, float, float>(BuildingEffectEnum.DenarByBoundVillageHeartPerDay, BuildingEffectIncrementType.Add, 0.2f, 0.4f, 0.6f)
		}, isMilitaryProject: false, 0f);
		_buildingCastleFarmlands.Initialize(new TextObject("{=l4eZqegY}Farmlands"), new TextObject("{=tajCl8Bg}Provides daily food."), new int[3] { 420, 630, 840 }, new Tuple<BuildingEffectEnum, BuildingEffectIncrementType, float, float, float>[1]
		{
			new Tuple<BuildingEffectEnum, BuildingEffectIncrementType, float, float, float>(BuildingEffectEnum.FoodProduction, BuildingEffectIncrementType.Add, 6f, 12f, 18f)
		}, isMilitaryProject: false, 0f);
		_buildingCastleMason.Initialize(new TextObject("{=R7ssoDHW}Mason"), new TextObject("{=hqUPvnaj}Increase bricks per day, increasing building and repair speed."), new int[3] { 560, 700, 1120 }, new Tuple<BuildingEffectEnum, BuildingEffectIncrementType, float, float, float>[2]
		{
			new Tuple<BuildingEffectEnum, BuildingEffectIncrementType, float, float, float>(BuildingEffectEnum.ConstructionPerDay, BuildingEffectIncrementType.Add, 2f, 4f, 6f),
			new Tuple<BuildingEffectEnum, BuildingEffectIncrementType, float, float, float>(BuildingEffectEnum.WallRepairSpeed, BuildingEffectIncrementType.AddFactor, 0.1f, 0.3f, 0.6f)
		}, isMilitaryProject: false, 0f);
		_buildingCastleRoadsAndPaths.Initialize(new TextObject("{=maEmutDP}Roads and Paths"), new TextObject("{=YPFDiwuy}Increase village production and village hearth growth."), new int[3] { 560, 840, 1120 }, new Tuple<BuildingEffectEnum, BuildingEffectIncrementType, float, float, float>[2]
		{
		}, isMilitaryProject: false, 0f);
		_buildingCastleCraftmansQuarters.Initialize(new TextObject("{=KE1KUayw}Craftmans Quarters"), new TextObject("{=2qZ14G9p}Provides income based on bound village hearts"), new int[3] { 350, 490, 630 }, new Tuple<BuildingEffectEnum, BuildingEffectIncrementType, float, float, float>[1]
		{
			new Tuple<BuildingEffectEnum, BuildingEffectIncrementType, float, float, float>(BuildingEffectEnum.DenarByBoundVillageHeartPerDay, BuildingEffectIncrementType.Add, 0.2f, 0.4f, 0.6f)
		}, isMilitaryProject: false, 0f);
E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\TaleWorlds.CampaignSystem.Settlements.Buildings\DefaultBuildingTypes.cs:283: 		_buildingCastleFarmlands.Initialize(new TextObject("{=l4eZqegY}Farmlands"), new TextObject("{=tajCl8Bg}Provides daily food."), new int[3] { 420, 630, 840 }, new Tuple<BuildingEffectEnum, BuildingEffectIncrementType, float, float, float>[1]
		{
			new Tuple<BuildingEffectEnum, BuildingEffectIncrementType, float, float, float>(BuildingEffectEnum.FoodProduction, BuildingEffectIncrementType.Add, 6f, 12f, 18f)
		}, isMilitaryProject: false, 0f);
		_buildingCastleMason.Initialize(new TextObject("{=R7ssoDHW}Mason"), new TextObject("{=hqUPvnaj}Increase bricks per day, increasing building and repair speed."), new int[3] { 560, 700, 1120 }, new Tuple<BuildingEffectEnum, BuildingEffectIncrementType, float, float, float>[2]
		{
			new Tuple<BuildingEffectEnum, BuildingEffectIncrementType, float, float, float>(BuildingEffectEnum.ConstructionPerDay, BuildingEffectIncrementType.Add, 2f, 4f, 6f),
			new Tuple<BuildingEffectEnum, BuildingEffectIncrementType, float, float, float>(BuildingEffectEnum.WallRepairSpeed, BuildingEffectIncrementType.AddFactor, 0.1f, 0.3f, 0.6f)
		}, isMilitaryProject: false, 0f);
		_buildingCastleRoadsAndPaths.Initialize(new TextObject("{=maEmutDP}Roads and Paths"), new TextObject("{=YPFDiwuy}Increase village production and village hearth growth."), new int[3] { 560, 840, 1120 }, new Tuple<BuildingEffectEnum, BuildingEffectIncrementType, float, float, float>[2]
		{
			new Tuple<BuildingEffectEnum, BuildingEffectIncrementType, float, float, float>(BuildingEffectEnum.VillageProduction, BuildingEffectIncrementType.AddFactor, 0.05f, 0.1f, 0.15f),
			new Tuple<BuildingEffectEnum, BuildingEffectIncrementType, float, float, float>(BuildingEffectEnum.VillageHeartsPerDay, BuildingEffectIncrementType.Add, 0.1f, 0.2f, 0.3f)
		{
			new Tuple<BuildingEffectEnum, BuildingEffectIncrementType, float, float, float>(BuildingEffectEnum.DenarByBoundVillageHeartPerDay, BuildingEffectIncrementType.Add, 0.2f, 0.4f, 0.6f)
		}, isMilitaryProject: false, 0f);
		_buildingCastleFarmlands.Initialize(new TextObject("{=l4eZqegY}Farmlands"), new TextObject("{=tajCl8Bg}Provides daily food."), new int[3] { 420, 630, 840 }, new Tuple<BuildingEffectEnum, BuildingEffectIncrementType, float, float, float>[1]
		{
E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\TaleWorlds.CampaignSystem.Settlements.Buildings\DefaultBuildingTypes.cs:285: 			new Tuple<BuildingEffectEnum, BuildingEffectIncrementType, float, float, float>(BuildingEffectEnum.FoodProduction, BuildingEffectIncrementType.Add, 6f, 12f, 18f)
		}, isMilitaryProject: false, 0f);
		_buildingCastleMason.Initialize(new TextObject("{=R7ssoDHW}Mason"), new TextObject("{=hqUPvnaj}Increase bricks per day, increasing building and repair speed."), new int[3] { 560, 700, 1120 }, new Tuple<BuildingEffectEnum, BuildingEffectIncrementType, float, float, float>[2]
		{
			new Tuple<BuildingEffectEnum, BuildingEffectIncrementType, float, float, float>(BuildingEffectEnum.ConstructionPerDay, BuildingEffectIncrementType.Add, 2f, 4f, 6f),
			new Tuple<BuildingEffectEnum, BuildingEffectIncrementType, float, float, float>(BuildingEffectEnum.WallRepairSpeed, BuildingEffectIncrementType.AddFactor, 0.1f, 0.3f, 0.6f)
		}, isMilitaryProject: false, 0f);
		_buildingCastleRoadsAndPaths.Initialize(new TextObject("{=maEmutDP}Roads and Paths"), new TextObject("{=YPFDiwuy}Increase village production and village hearth growth."), new int[3] { 560, 840, 1120 }, new Tuple<BuildingEffectEnum, BuildingEffectIncrementType, float, float, float>[2]
		{
			new Tuple<BuildingEffectEnum, BuildingEffectIncrementType, float, float, float>(BuildingEffectEnum.VillageProduction, BuildingEffectIncrementType.AddFactor, 0.05f, 0.1f, 0.15f),
			new Tuple<BuildingEffectEnum, BuildingEffectIncrementType, float, float, float>(BuildingEffectEnum.VillageHeartsPerDay, BuildingEffectIncrementType.Add, 0.1f, 0.2f, 0.3f)
		}, isMilitaryProject: false, 0f);
		_buildingSettlementDailyHousing.InitializeDailyProject(new TextObject("{=F4V7oaVx}Housing"), new TextObject("{=yWXtcxqb}Construct housing so that more folks can settle, increasing population."), new Tuple<BuildingEffectEnum, BuildingEffectIncrementType, float, float, float>[1]
		}, isMilitaryProject: false, 0f);
		_buildingCastleFarmlands.Initialize(new TextObject("{=l4eZqegY}Farmlands"), new TextObject("{=tajCl8Bg}Provides daily food."), new int[3] { 420, 630, 840 }, new Tuple<BuildingEffectEnum, BuildingEffectIncrementType, float, float, float>[1]
		{
			new Tuple<BuildingEffectEnum, BuildingEffectIncrementType, float, float, float>(BuildingEffectEnum.FoodProduction, BuildingEffectIncrementType.Add, 6f, 12f, 18f)
		}, isMilitaryProject: false, 0f);
E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\TaleWorlds.CampaignSystem.Settlements.Buildings\DefaultBuildingTypes.cs:287: 		_buildingCastleMason.Initialize(new TextObject("{=R7ssoDHW}Mason"), new TextObject("{=hqUPvnaj}Increase bricks per day, increasing building and repair speed."), new int[3] { 560, 700, 1120 }, new Tuple<BuildingEffectEnum, BuildingEffectIncrementType, float, float, float>[2]
		{
			new Tuple<BuildingEffectEnum, BuildingEffectIncrementType, float, float, float>(BuildingEffectEnum.ConstructionPerDay, BuildingEffectIncrementType.Add, 2f, 4f, 6f),
			new Tuple<BuildingEffectEnum, BuildingEffectIncrementType, float, float, float>(BuildingEffectEnum.WallRepairSpeed, BuildingEffectIncrementType.AddFactor, 0.1f, 0.3f, 0.6f)
		}, isMilitaryProject: false, 0f);
		_buildingCastleRoadsAndPaths.Initialize(new TextObject("{=maEmutDP}Roads and Paths"), new TextObject("{=YPFDiwuy}Increase village production and village hearth growth."), new int[3] { 560, 840, 1120 }, new Tuple<BuildingEffectEnum, BuildingEffectIncrementType, float, float, float>[2]
		{
			new Tuple<BuildingEffectEnum, BuildingEffectIncrementType, float, float, float>(BuildingEffectEnum.VillageProduction, BuildingEffectIncrementType.AddFactor, 0.05f, 0.1f, 0.15f),
			new Tuple<BuildingEffectEnum, BuildingEffectIncrementType, float, float, float>(BuildingEffectEnum.VillageHeartsPerDay, BuildingEffectIncrementType.Add, 0.1f, 0.2f, 0.3f)
		}, isMilitaryProject: false, 0f);
		_buildingSettlementDailyHousing.InitializeDailyProject(new TextObject("{=F4V7oaVx}Housing"), new TextObject("{=yWXtcxqb}Construct housing so that more folks can settle, increasing population."), new Tuple<BuildingEffectEnum, BuildingEffectIncrementType, float, float, float>[1]
		{
			new Tuple<BuildingEffectEnum, BuildingEffectIncrementType, float, float, float>(BuildingEffectEnum.Prosperity, BuildingEffectIncrementType.Add, 2f, 2f, 2f)
		{
			new Tuple<BuildingEffectEnum, BuildingEffectIncrementType, float, float, float>(BuildingEffectEnum.FoodProduction, BuildingEffectIncrementType.Add, 6f, 12f, 18f)
		}, isMilitaryProject: false, 0f);
		_buildingCastleMason.Initialize(new TextObject("{=R7ssoDHW}Mason"), new TextObject("{=hqUPvnaj}Increase bricks per day, increasing building and repair speed."), new int[3] { 560, 700, 1120 }, new Tuple<BuildingEffectEnum, BuildingEffectIncrementType, float, float, float>[2]
		{
E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\TaleWorlds.CampaignSystem.Settlements.Buildings\DefaultBuildingTypes.cs:289: 			new Tuple<BuildingEffectEnum, BuildingEffectIncrementType, float, float, float>(BuildingEffectEnum.ConstructionPerDay, BuildingEffectIncrementType.Add, 2f, 4f, 6f),
			new Tuple<BuildingEffectEnum, BuildingEffectIncrementType, float, float, float>(BuildingEffectEnum.WallRepairSpeed, BuildingEffectIncrementType.AddFactor, 0.1f, 0.3f, 0.6f)
		}, isMilitaryProject: false, 0f);
		_buildingCastleRoadsAndPaths.Initialize(new TextObject("{=maEmutDP}Roads and Paths"), new TextObject("{=YPFDiwuy}Increase village production and village hearth growth."), new int[3] { 560, 840, 1120 }, new Tuple<BuildingEffectEnum, BuildingEffectIncrementType, float, float, float>[2]
		{
			new Tuple<BuildingEffectEnum, BuildingEffectIncrementType, float, float, float>(BuildingEffectEnum.VillageProduction, BuildingEffectIncrementType.AddFactor, 0.05f, 0.1f, 0.15f),
			new Tuple<BuildingEffectEnum, BuildingEffectIncrementType, float, float, float>(BuildingEffectEnum.VillageHeartsPerDay, BuildingEffectIncrementType.Add, 0.1f, 0.2f, 0.3f)
		}, isMilitaryProject: false, 0f);
		_buildingSettlementDailyHousing.InitializeDailyProject(new TextObject("{=F4V7oaVx}Housing"), new TextObject("{=yWXtcxqb}Construct housing so that more folks can settle, increasing population."), new Tuple<BuildingEffectEnum, BuildingEffectIncrementType, float, float, float>[1]
		{
			new Tuple<BuildingEffectEnum, BuildingEffectIncrementType, float, float, float>(BuildingEffectEnum.Prosperity, BuildingEffectIncrementType.Add, 2f, 2f, 2f)
		});
		_buildingSettlementDailyTrainMilitia.InitializeDailyProject(new TextObject("{=p1Y3EU5O}Train Militia"), new TextObject("{=61J1wa6k}Schedule drills for commoners, increasing militia recruitment."), new Tuple<BuildingEffectEnum, BuildingEffectIncrementType, float, float, float>[2]
			new Tuple<BuildingEffectEnum, BuildingEffectIncrementType, float, float, float>(BuildingEffectEnum.FoodProduction, BuildingEffectIncrementType.Add, 6f, 12f, 18f)
		}, isMilitaryProject: false, 0f);
		_buildingCastleMason.Initialize(new TextObject("{=R7ssoDHW}Mason"), new TextObject("{=hqUPvnaj}Increase bricks per day, increasing building and repair speed."), new int[3] { 560, 700, 1120 }, new Tuple<BuildingEffectEnum, BuildingEffectIncrementType, float, float, float>[2]
		{
			new Tuple<BuildingEffectEnum, BuildingEffectIncrementType, float, float, float>(BuildingEffectEnum.ConstructionPerDay, BuildingEffectIncrementType.Add, 2f, 4f, 6f),
E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\TaleWorlds.CampaignSystem.Settlements.Buildings\DefaultBuildingTypes.cs:290: 			new Tuple<BuildingEffectEnum, BuildingEffectIncrementType, float, float, float>(BuildingEffectEnum.WallRepairSpeed, BuildingEffectIncrementType.AddFactor, 0.1f, 0.3f, 0.6f)
		}, isMilitaryProject: false, 0f);
		_buildingCastleRoadsAndPaths.Initialize(new TextObject("{=maEmutDP}Roads and Paths"), new TextObject("{=YPFDiwuy}Increase village production and village hearth growth."), new int[3] { 560, 840, 1120 }, new Tuple<BuildingEffectEnum, BuildingEffectIncrementType, float, float, float>[2]
		{
			new Tuple<BuildingEffectEnum, BuildingEffectIncrementType, float, float, float>(BuildingEffectEnum.VillageProduction, BuildingEffectIncrementType.AddFactor, 0.05f, 0.1f, 0.15f),
			new Tuple<BuildingEffectEnum, BuildingEffectIncrementType, float, float, float>(BuildingEffectEnum.VillageHeartsPerDay, BuildingEffectIncrementType.Add, 0.1f, 0.2f, 0.3f)
		}, isMilitaryProject: false, 0f);
		_buildingSettlementDailyHousing.InitializeDailyProject(new TextObject("{=F4V7oaVx}Housing"), new TextObject("{=yWXtcxqb}Construct housing so that more folks can settle, increasing population."), new Tuple<BuildingEffectEnum, BuildingEffectIncrementType, float, float, float>[1]
		{
			new Tuple<BuildingEffectEnum, BuildingEffectIncrementType, float, float, float>(BuildingEffectEnum.Prosperity, BuildingEffectIncrementType.Add, 2f, 2f, 2f)
		});
		_buildingSettlementDailyTrainMilitia.InitializeDailyProject(new TextObject("{=p1Y3EU5O}Train Militia"), new TextObject("{=61J1wa6k}Schedule drills for commoners, increasing militia recruitment."), new Tuple<BuildingEffectEnum, BuildingEffectIncrementType, float, float, float>[2]
		{
		_buildingCastleMason.Initialize(new TextObject("{=R7ssoDHW}Mason"), new TextObject("{=hqUPvnaj}Increase bricks per day, increasing building and repair speed."), new int[3] { 560, 700, 1120 }, new Tuple<BuildingEffectEnum, BuildingEffectIncrementType, float, float, float>[2]
		{
			new Tuple<BuildingEffectEnum, BuildingEffectIncrementType, float, float, float>(BuildingEffectEnum.ConstructionPerDay, BuildingEffectIncrementType.Add, 2f, 4f, 6f),
			new Tuple<BuildingEffectEnum, BuildingEffectIncrementType, float, float, float>(BuildingEffectEnum.WallRepairSpeed, BuildingEffectIncrementType.AddFactor, 0.1f, 0.3f, 0.6f)
		}, isMilitaryProject: false, 0f);
E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\TaleWorlds.CampaignSystem.Settlements.Buildings\DefaultBuildingTypes.cs:292: 		_buildingCastleRoadsAndPaths.Initialize(new TextObject("{=maEmutDP}Roads and Paths"), new TextObject("{=YPFDiwuy}Increase village production and village hearth growth."), new int[3] { 560, 840, 1120 }, new Tuple<BuildingEffectEnum, BuildingEffectIncrementType, float, float, float>[2]
		{
			new Tuple<BuildingEffectEnum, BuildingEffectIncrementType, float, float, float>(BuildingEffectEnum.VillageProduction, BuildingEffectIncrementType.AddFactor, 0.05f, 0.1f, 0.15f),
			new Tuple<BuildingEffectEnum, BuildingEffectIncrementType, float, float, float>(BuildingEffectEnum.VillageHeartsPerDay, BuildingEffectIncrementType.Add, 0.1f, 0.2f, 0.3f)
		}, isMilitaryProject: false, 0f);
		_buildingSettlementDailyHousing.InitializeDailyProject(new TextObject("{=F4V7oaVx}Housing"), new TextObject("{=yWXtcxqb}Construct housing so that more folks can settle, increasing population."), new Tuple<BuildingEffectEnum, BuildingEffectIncrementType, float, float, float>[1]
		{
			new Tuple<BuildingEffectEnum, BuildingEffectIncrementType, float, float, float>(BuildingEffectEnum.Prosperity, BuildingEffectIncrementType.Add, 2f, 2f, 2f)
		});
		_buildingSettlementDailyTrainMilitia.InitializeDailyProject(new TextObject("{=p1Y3EU5O}Train Militia"), new TextObject("{=61J1wa6k}Schedule drills for commoners, increasing militia recruitment."), new Tuple<BuildingEffectEnum, BuildingEffectIncrementType, float, float, float>[2]
		{
			new Tuple<BuildingEffectEnum, BuildingEffectIncrementType, float, float, float>(BuildingEffectEnum.Militia, BuildingEffectIncrementType.Add, 2f, 2f, 2f),
			new Tuple<BuildingEffectEnum, BuildingEffectIncrementType, float, float, float>(BuildingEffectEnum.GarrisonAutoRecruitment, BuildingEffectIncrementType.Add, 1f, 1f, 1f)
			new Tuple<BuildingEffectEnum, BuildingEffectIncrementType, float, float, float>(BuildingEffectEnum.ConstructionPerDay, BuildingEffectIncrementType.Add, 2f, 4f, 6f),
			new Tuple<BuildingEffectEnum, BuildingEffectIncrementType, float, float, float>(BuildingEffectEnum.WallRepairSpeed, BuildingEffectIncrementType.AddFactor, 0.1f, 0.3f, 0.6f)
		}, isMilitaryProject: false, 0f);
		_buildingCastleRoadsAndPaths.Initialize(new TextObject("{=maEmutDP}Roads and Paths"), new TextObject("{=YPFDiwuy}Increase village production and village hearth growth."), new int[3] { 560, 840, 1120 }, new Tuple<BuildingEffectEnum, BuildingEffectIncrementType, float, float, float>[2]
		{
E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\TaleWorlds.CampaignSystem.Settlements.Buildings\DefaultBuildingTypes.cs:294: 			new Tuple<BuildingEffectEnum, BuildingEffectIncrementType, float, float, float>(BuildingEffectEnum.VillageProduction, BuildingEffectIncrementType.AddFactor, 0.05f, 0.1f, 0.15f),
			new Tuple<BuildingEffectEnum, BuildingEffectIncrementType, float, float, float>(BuildingEffectEnum.VillageHeartsPerDay, BuildingEffectIncrementType.Add, 0.1f, 0.2f, 0.3f)
		}, isMilitaryProject: false, 0f);
		_buildingSettlementDailyHousing.InitializeDailyProject(new TextObject("{=F4V7oaVx}Housing"), new TextObject("{=yWXtcxqb}Construct housing so that more folks can settle, increasing population."), new Tuple<BuildingEffectEnum, BuildingEffectIncrementType, float, float, float>[1]
		{
			new Tuple<BuildingEffectEnum, BuildingEffectIncrementType, float, float, float>(BuildingEffectEnum.Prosperity, BuildingEffectIncrementType.Add, 2f, 2f, 2f)
		});
		_buildingSettlementDailyTrainMilitia.InitializeDailyProject(new TextObject("{=p1Y3EU5O}Train Militia"), new TextObject("{=61J1wa6k}Schedule drills for commoners, increasing militia recruitment."), new Tuple<BuildingEffectEnum, BuildingEffectIncrementType, float, float, float>[2]
		{
			new Tuple<BuildingEffectEnum, BuildingEffectIncrementType, float, float, float>(BuildingEffectEnum.Militia, BuildingEffectIncrementType.Add, 2f, 2f, 2f),
			new Tuple<BuildingEffectEnum, BuildingEffectIncrementType, float, float, float>(BuildingEffectEnum.GarrisonAutoRecruitment, BuildingEffectIncrementType.Add, 1f, 1f, 1f)
		});
		_buildingSettlementDailyFestivalAndGames.InitializeDailyProject(new TextObject("{=aEmYZadz}Festival and Games"), new TextObject("{=ovDbQIo9}Organize festivals and games in the settlement, increasing loyalty."), new Tuple<BuildingEffectEnum, BuildingEffectIncrementType, float, float, float>[1]
			new Tuple<BuildingEffectEnum, BuildingEffectIncrementType, float, float, float>(BuildingEffectEnum.WallRepairSpeed, BuildingEffectIncrementType.AddFactor, 0.1f, 0.3f, 0.6f)
		}, isMilitaryProject: false, 0f);
		_buildingCastleRoadsAndPaths.Initialize(new TextObject("{=maEmutDP}Roads and Paths"), new TextObject("{=YPFDiwuy}Increase village production and village hearth growth."), new int[3] { 560, 840, 1120 }, new Tuple<BuildingEffectEnum, BuildingEffectIncrementType, float, float, float>[2]
		{
			new Tuple<BuildingEffectEnum, BuildingEffectIncrementType, float, float, float>(BuildingEffectEnum.VillageProduction, BuildingEffectIncrementType.AddFactor, 0.05f, 0.1f, 0.15f),
E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\TaleWorlds.CampaignSystem.Settlements.Buildings\DefaultBuildingTypes.cs:295: 			new Tuple<BuildingEffectEnum, BuildingEffectIncrementType, float, float, float>(BuildingEffectEnum.VillageHeartsPerDay, BuildingEffectIncrementType.Add, 0.1f, 0.2f, 0.3f)
		}, isMilitaryProject: false, 0f);
		_buildingSettlementDailyHousing.InitializeDailyProject(new TextObject("{=F4V7oaVx}Housing"), new TextObject("{=yWXtcxqb}Construct housing so that more folks can settle, increasing population."), new Tuple<BuildingEffectEnum, BuildingEffectIncrementType, float, float, float>[1]
		{
			new Tuple<BuildingEffectEnum, BuildingEffectIncrementType, float, float, float>(BuildingEffectEnum.Prosperity, BuildingEffectIncrementType.Add, 2f, 2f, 2f)
		});
		_buildingSettlementDailyTrainMilitia.InitializeDailyProject(new TextObject("{=p1Y3EU5O}Train Militia"), new TextObject("{=61J1wa6k}Schedule drills for commoners, increasing militia recruitment."), new Tuple<BuildingEffectEnum, BuildingEffectIncrementType, float, float, float>[2]
		{
			new Tuple<BuildingEffectEnum, BuildingEffectIncrementType, float, float, float>(BuildingEffectEnum.Militia, BuildingEffectIncrementType.Add, 2f, 2f, 2f),
			new Tuple<BuildingEffectEnum, BuildingEffectIncrementType, float, float, float>(BuildingEffectEnum.GarrisonAutoRecruitment, BuildingEffectIncrementType.Add, 1f, 1f, 1f)
		});
		_buildingSettlementDailyFestivalAndGames.InitializeDailyProject(new TextObject("{=aEmYZadz}Festival and Games"), new TextObject("{=ovDbQIo9}Organize festivals and games in the settlement, increasing loyalty."), new Tuple<BuildingEffectEnum, BuildingEffectIncrementType, float, float, float>[1]
		{
		_buildingCastleRoadsAndPaths.Initialize(new TextObject("{=maEmutDP}Roads and Paths"), new TextObject("{=YPFDiwuy}Increase village production and village hearth growth."), new int[3] { 560, 840, 1120 }, new Tuple<BuildingEffectEnum, BuildingEffectIncrementType, float, float, float>[2]
		{
			new Tuple<BuildingEffectEnum, BuildingEffectIncrementType, float, float, float>(BuildingEffectEnum.VillageProduction, BuildingEffectIncrementType.AddFactor, 0.05f, 0.1f, 0.15f),
			new Tuple<BuildingEffectEnum, BuildingEffectIncrementType, float, float, float>(BuildingEffectEnum.VillageHeartsPerDay, BuildingEffectIncrementType.Add, 0.1f, 0.2f, 0.3f)
		}, isMilitaryProject: false, 0f);
E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\TaleWorlds.CampaignSystem.Settlements.Buildings\DefaultBuildingTypes.cs:297: 		_buildingSettlementDailyHousing.InitializeDailyProject(new TextObject("{=F4V7oaVx}Housing"), new TextObject("{=yWXtcxqb}Construct housing so that more folks can settle, increasing population."), new Tuple<BuildingEffectEnum, BuildingEffectIncrementType, float, float, float>[1]
		{
			new Tuple<BuildingEffectEnum, BuildingEffectIncrementType, float, float, float>(BuildingEffectEnum.Prosperity, BuildingEffectIncrementType.Add, 2f, 2f, 2f)
		});
		_buildingSettlementDailyTrainMilitia.InitializeDailyProject(new TextObject("{=p1Y3EU5O}Train Militia"), new TextObject("{=61J1wa6k}Schedule drills for commoners, increasing militia recruitment."), new Tuple<BuildingEffectEnum, BuildingEffectIncrementType, float, float, float>[2]
		{
			new Tuple<BuildingEffectEnum, BuildingEffectIncrementType, float, float, float>(BuildingEffectEnum.Militia, BuildingEffectIncrementType.Add, 2f, 2f, 2f),
			new Tuple<BuildingEffectEnum, BuildingEffectIncrementType, float, float, float>(BuildingEffectEnum.GarrisonAutoRecruitment, BuildingEffectIncrementType.Add, 1f, 1f, 1f)
		});
		_buildingSettlementDailyFestivalAndGames.InitializeDailyProject(new TextObject("{=aEmYZadz}Festival and Games"), new TextObject("{=ovDbQIo9}Organize festivals and games in the settlement, increasing loyalty."), new Tuple<BuildingEffectEnum, BuildingEffectIncrementType, float, float, float>[1]
		{
			new Tuple<BuildingEffectEnum, BuildingEffectIncrementType, float, float, float>(BuildingEffectEnum.Loyalty, BuildingEffectIncrementType.Add, 3f, 3f, 3f)
		});
			new Tuple<BuildingEffectEnum, BuildingEffectIncrementType, float, float, float>(BuildingEffectEnum.VillageProduction, BuildingEffectIncrementType.AddFactor, 0.05f, 0.1f, 0.15f),
			new Tuple<BuildingEffectEnum, BuildingEffectIncrementType, float, float, float>(BuildingEffectEnum.VillageHeartsPerDay, BuildingEffectIncrementType.Add, 0.1f, 0.2f, 0.3f)
		}, isMilitaryProject: false, 0f);
		_buildingSettlementDailyHousing.InitializeDailyProject(new TextObject("{=F4V7oaVx}Housing"), new TextObject("{=yWXtcxqb}Construct housing so that more folks can settle, increasing population."), new Tuple<BuildingEffectEnum, BuildingEffectIncrementType, float, float, float>[1]
		{
E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\TaleWorlds.CampaignSystem.Settlements.Buildings\DefaultBuildingTypes.cs:299: 			new Tuple<BuildingEffectEnum, BuildingEffectIncrementType, float, float, float>(BuildingEffectEnum.Prosperity, BuildingEffectIncrementType.Add, 2f, 2f, 2f)
		});
		_buildingSettlementDailyTrainMilitia.InitializeDailyProject(new TextObject("{=p1Y3EU5O}Train Militia"), new TextObject("{=61J1wa6k}Schedule drills for commoners, increasing militia recruitment."), new Tuple<BuildingEffectEnum, BuildingEffectIncrementType, float, float, float>[2]
		{
			new Tuple<BuildingEffectEnum, BuildingEffectIncrementType, float, float, float>(BuildingEffectEnum.Militia, BuildingEffectIncrementType.Add, 2f, 2f, 2f),
			new Tuple<BuildingEffectEnum, BuildingEffectIncrementType, float, float, float>(BuildingEffectEnum.GarrisonAutoRecruitment, BuildingEffectIncrementType.Add, 1f, 1f, 1f)
		});
		_buildingSettlementDailyFestivalAndGames.InitializeDailyProject(new TextObject("{=aEmYZadz}Festival and Games"), new TextObject("{=ovDbQIo9}Organize festivals and games in the settlement, increasing loyalty."), new Tuple<BuildingEffectEnum, BuildingEffectIncrementType, float, float, float>[1]
		{
			new Tuple<BuildingEffectEnum, BuildingEffectIncrementType, float, float, float>(BuildingEffectEnum.Loyalty, BuildingEffectIncrementType.Add, 3f, 3f, 3f)
		});
		_buildingSettlementDailyIrrigation.InitializeDailyProject(new TextObject("{=O4cknzhW}Irrigation"), new TextObject("{=CU9g49fo}Provide irrigation, increasing hearth growth in bound villages."), new Tuple<BuildingEffectEnum, BuildingEffectIncrementType, float, float, float>[1]
		{
		}, isMilitaryProject: false, 0f);
		_buildingSettlementDailyHousing.InitializeDailyProject(new TextObject("{=F4V7oaVx}Housing"), new TextObject("{=yWXtcxqb}Construct housing so that more folks can settle, increasing population."), new Tuple<BuildingEffectEnum, BuildingEffectIncrementType, float, float, float>[1]
		{
			new Tuple<BuildingEffectEnum, BuildingEffectIncrementType, float, float, float>(BuildingEffectEnum.Prosperity, BuildingEffectIncrementType.Add, 2f, 2f, 2f)
		});
E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\TaleWorlds.CampaignSystem.Settlements.Buildings\DefaultBuildingTypes.cs:301: 		_buildingSettlementDailyTrainMilitia.InitializeDailyProject(new TextObject("{=p1Y3EU5O}Train Militia"), new TextObject("{=61J1wa6k}Schedule drills for commoners, increasing militia recruitment."), new Tuple<BuildingEffectEnum, BuildingEffectIncrementType, float, float, float>[2]
		{
			new Tuple<BuildingEffectEnum, BuildingEffectIncrementType, float, float, float>(BuildingEffectEnum.Militia, BuildingEffectIncrementType.Add, 2f, 2f, 2f),
			new Tuple<BuildingEffectEnum, BuildingEffectIncrementType, float, float, float>(BuildingEffectEnum.GarrisonAutoRecruitment, BuildingEffectIncrementType.Add, 1f, 1f, 1f)
		});
		_buildingSettlementDailyFestivalAndGames.InitializeDailyProject(new TextObject("{=aEmYZadz}Festival and Games"), new TextObject("{=ovDbQIo9}Organize festivals and games in the settlement, increasing loyalty."), new Tuple<BuildingEffectEnum, BuildingEffectIncrementType, float, float, float>[1]
		{
			new Tuple<BuildingEffectEnum, BuildingEffectIncrementType, float, float, float>(BuildingEffectEnum.Loyalty, BuildingEffectIncrementType.Add, 3f, 3f, 3f)
		});
		_buildingSettlementDailyIrrigation.InitializeDailyProject(new TextObject("{=O4cknzhW}Irrigation"), new TextObject("{=CU9g49fo}Provide irrigation, increasing hearth growth in bound villages."), new Tuple<BuildingEffectEnum, BuildingEffectIncrementType, float, float, float>[1]
		{
			new Tuple<BuildingEffectEnum, BuildingEffectIncrementType, float, float, float>(BuildingEffectEnum.VillageHeartsPerDay, BuildingEffectIncrementType.Add, 1f, 1f, 1f)
		});
		{
			new Tuple<BuildingEffectEnum, BuildingEffectIncrementType, float, float, float>(BuildingEffectEnum.Prosperity, BuildingEffectIncrementType.Add, 2f, 2f, 2f)
		});
		_buildingSettlementDailyTrainMilitia.InitializeDailyProject(new TextObject("{=p1Y3EU5O}Train Militia"), new TextObject("{=61J1wa6k}Schedule drills for commoners, increasing militia recruitment."), new Tuple<BuildingEffectEnum, BuildingEffectIncrementType, float, float, float>[2]
		{
E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\TaleWorlds.CampaignSystem.Settlements.Buildings\DefaultBuildingTypes.cs:303: 			new Tuple<BuildingEffectEnum, BuildingEffectIncrementType, float, float, float>(BuildingEffectEnum.Militia, BuildingEffectIncrementType.Add, 2f, 2f, 2f),
			new Tuple<BuildingEffectEnum, BuildingEffectIncrementType, float, float, float>(BuildingEffectEnum.GarrisonAutoRecruitment, BuildingEffectIncrementType.Add, 1f, 1f, 1f)
		});
		_buildingSettlementDailyFestivalAndGames.InitializeDailyProject(new TextObject("{=aEmYZadz}Festival and Games"), new TextObject("{=ovDbQIo9}Organize festivals and games in the settlement, increasing loyalty."), new Tuple<BuildingEffectEnum, BuildingEffectIncrementType, float, float, float>[1]
		{
			new Tuple<BuildingEffectEnum, BuildingEffectIncrementType, float, float, float>(BuildingEffectEnum.Loyalty, BuildingEffectIncrementType.Add, 3f, 3f, 3f)
		});
		_buildingSettlementDailyIrrigation.InitializeDailyProject(new TextObject("{=O4cknzhW}Irrigation"), new TextObject("{=CU9g49fo}Provide irrigation, increasing hearth growth in bound villages."), new Tuple<BuildingEffectEnum, BuildingEffectIncrementType, float, float, float>[1]
		{
			new Tuple<BuildingEffectEnum, BuildingEffectIncrementType, float, float, float>(BuildingEffectEnum.VillageHeartsPerDay, BuildingEffectIncrementType.Add, 1f, 1f, 1f)
		});
		_buildingCastleDailySlackenGarrison.InitializeDailyProject(new TextObject("{=cHIa0Xty}Slacken Garrison"), new TextObject("{=5VBbLVBt}Decrease garrison wages."), new Tuple<BuildingEffectEnum, BuildingEffectIncrementType, float, float, float>[1]
		{
			new Tuple<BuildingEffectEnum, BuildingEffectIncrementType, float, float, float>(BuildingEffectEnum.Prosperity, BuildingEffectIncrementType.Add, 2f, 2f, 2f)
		});
		_buildingSettlementDailyTrainMilitia.InitializeDailyProject(new TextObject("{=p1Y3EU5O}Train Militia"), new TextObject("{=61J1wa6k}Schedule drills for commoners, increasing militia recruitment."), new Tuple<BuildingEffectEnum, BuildingEffectIncrementType, float, float, float>[2]
		{
			new Tuple<BuildingEffectEnum, BuildingEffectIncrementType, float, float, float>(BuildingEffectEnum.Militia, BuildingEffectIncrementType.Add, 2f, 2f, 2f),
E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\TaleWorlds.CampaignSystem.Settlements.Buildings\DefaultBuildingTypes.cs:304: 			new Tuple<BuildingEffectEnum, BuildingEffectIncrementType, float, float, float>(BuildingEffectEnum.GarrisonAutoRecruitment, BuildingEffectIncrementType.Add, 1f, 1f, 1f)
		});
		_buildingSettlementDailyFestivalAndGames.InitializeDailyProject(new TextObject("{=aEmYZadz}Festival and Games"), new TextObject("{=ovDbQIo9}Organize festivals and games in the settlement, increasing loyalty."), new Tuple<BuildingEffectEnum, BuildingEffectIncrementType, float, float, float>[1]
		{
			new Tuple<BuildingEffectEnum, BuildingEffectIncrementType, float, float, float>(BuildingEffectEnum.Loyalty, BuildingEffectIncrementType.Add, 3f, 3f, 3f)
		});
		_buildingSettlementDailyIrrigation.InitializeDailyProject(new TextObject("{=O4cknzhW}Irrigation"), new TextObject("{=CU9g49fo}Provide irrigation, increasing hearth growth in bound villages."), new Tuple<BuildingEffectEnum, BuildingEffectIncrementType, float, float, float>[1]
		{
			new Tuple<BuildingEffectEnum, BuildingEffectIncrementType, float, float, float>(BuildingEffectEnum.VillageHeartsPerDay, BuildingEffectIncrementType.Add, 1f, 1f, 1f)
		});
		_buildingCastleDailySlackenGarrison.InitializeDailyProject(new TextObject("{=cHIa0Xty}Slacken Garrison"), new TextObject("{=5VBbLVBt}Decrease garrison wages."), new Tuple<BuildingEffectEnum, BuildingEffectIncrementType, float, float, float>[1]
		{
			new Tuple<BuildingEffectEnum, BuildingEffectIncrementType, float, float, float>(BuildingEffectEnum.GarrisonWageReduction, BuildingEffectIncrementType.AddFactor, -0.05f, -0.05f, -0.05f)
		_buildingSettlementDailyTrainMilitia.InitializeDailyProject(new TextObject("{=p1Y3EU5O}Train Militia"), new TextObject("{=61J1wa6k}Schedule drills for commoners, increasing militia recruitment."), new Tuple<BuildingEffectEnum, BuildingEffectIncrementType, float, float, float>[2]
		{
			new Tuple<BuildingEffectEnum, BuildingEffectIncrementType, float, float, float>(BuildingEffectEnum.Militia, BuildingEffectIncrementType.Add, 2f, 2f, 2f),
			new Tuple<BuildingEffectEnum, BuildingEffectIncrementType, float, float, float>(BuildingEffectEnum.GarrisonAutoRecruitment, BuildingEffectIncrementType.Add, 1f, 1f, 1f)
		});
E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\TaleWorlds.CampaignSystem.Settlements.Buildings\DefaultBuildingTypes.cs:306: 		_buildingSettlementDailyFestivalAndGames.InitializeDailyProject(new TextObject("{=aEmYZadz}Festival and Games"), new TextObject("{=ovDbQIo9}Organize festivals and games in the settlement, increasing loyalty."), new Tuple<BuildingEffectEnum, BuildingEffectIncrementType, float, float, float>[1]
		{
			new Tuple<BuildingEffectEnum, BuildingEffectIncrementType, float, float, float>(BuildingEffectEnum.Loyalty, BuildingEffectIncrementType.Add, 3f, 3f, 3f)
		});
		_buildingSettlementDailyIrrigation.InitializeDailyProject(new TextObject("{=O4cknzhW}Irrigation"), new TextObject("{=CU9g49fo}Provide irrigation, increasing hearth growth in bound villages."), new Tuple<BuildingEffectEnum, BuildingEffectIncrementType, float, float, float>[1]
		{
			new Tuple<BuildingEffectEnum, BuildingEffectIncrementType, float, float, float>(BuildingEffectEnum.VillageHeartsPerDay, BuildingEffectIncrementType.Add, 1f, 1f, 1f)
		});
		_buildingCastleDailySlackenGarrison.InitializeDailyProject(new TextObject("{=cHIa0Xty}Slacken Garrison"), new TextObject("{=5VBbLVBt}Decrease garrison wages."), new Tuple<BuildingEffectEnum, BuildingEffectIncrementType, float, float, float>[1]
		{
			new Tuple<BuildingEffectEnum, BuildingEffectIncrementType, float, float, float>(BuildingEffectEnum.GarrisonWageReduction, BuildingEffectIncrementType.AddFactor, -0.05f, -0.05f, -0.05f)
		});
		_buildingCastleDailyRaiseTroops.InitializeDailyProject(new TextObject("{=jm1ScaoK}Raise Troops"), new TextObject("{=UsHhePdk}Increase militia recruitment, and auto recruitment."), new Tuple<BuildingEffectEnum, BuildingEffectIncrementType, float, float, float>[2]
			new Tuple<BuildingEffectEnum, BuildingEffectIncrementType, float, float, float>(BuildingEffectEnum.Militia, BuildingEffectIncrementType.Add, 2f, 2f, 2f),
			new Tuple<BuildingEffectEnum, BuildingEffectIncrementType, float, float, float>(BuildingEffectEnum.GarrisonAutoRecruitment, BuildingEffectIncrementType.Add, 1f, 1f, 1f)
		});
		_buildingSettlementDailyFestivalAndGames.InitializeDailyProject(new TextObject("{=aEmYZadz}Festival and Games"), new TextObject("{=ovDbQIo9}Organize festivals and games in the settlement, increasing loyalty."), new Tuple<BuildingEffectEnum, BuildingEffectIncrementType, float, float, float>[1]
		{
E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\TaleWorlds.CampaignSystem.Settlements.Buildings\DefaultBuildingTypes.cs:308: 			new Tuple<BuildingEffectEnum, BuildingEffectIncrementType, float, float, float>(BuildingEffectEnum.Loyalty, BuildingEffectIncrementType.Add, 3f, 3f, 3f)
		});
		_buildingSettlementDailyIrrigation.InitializeDailyProject(new TextObject("{=O4cknzhW}Irrigation"), new TextObject("{=CU9g49fo}Provide irrigation, increasing hearth growth in bound villages."), new Tuple<BuildingEffectEnum, BuildingEffectIncrementType, float, float, float>[1]
		{
			new Tuple<BuildingEffectEnum, BuildingEffectIncrementType, float, float, float>(BuildingEffectEnum.VillageHeartsPerDay, BuildingEffectIncrementType.Add, 1f, 1f, 1f)
		});
		_buildingCastleDailySlackenGarrison.InitializeDailyProject(new TextObject("{=cHIa0Xty}Slacken Garrison"), new TextObject("{=5VBbLVBt}Decrease garrison wages."), new Tuple<BuildingEffectEnum, BuildingEffectIncrementType, float, float, float>[1]
		{
			new Tuple<BuildingEffectEnum, BuildingEffectIncrementType, float, float, float>(BuildingEffectEnum.GarrisonWageReduction, BuildingEffectIncrementType.AddFactor, -0.05f, -0.05f, -0.05f)
		});
		_buildingCastleDailyRaiseTroops.InitializeDailyProject(new TextObject("{=jm1ScaoK}Raise Troops"), new TextObject("{=UsHhePdk}Increase militia recruitment, and auto recruitment."), new Tuple<BuildingEffectEnum, BuildingEffectIncrementType, float, float, float>[2]
		{
			new Tuple<BuildingEffectEnum, BuildingEffectIncrementType, float, float, float>(BuildingEffectEnum.Militia, BuildingEffectIncrementType.Add, 3f, 3f, 3f),
		});
		_buildingSettlementDailyFestivalAndGames.InitializeDailyProject(new TextObject("{=aEmYZadz}Festival and Games"), new TextObject("{=ovDbQIo9}Organize festivals and games in the settlement, increasing loyalty."), new Tuple<BuildingEffectEnum, BuildingEffectIncrementType, float, float, float>[1]
		{
			new Tuple<BuildingEffectEnum, BuildingEffectIncrementType, float, float, float>(BuildingEffectEnum.Loyalty, BuildingEffectIncrementType.Add, 3f, 3f, 3f)
		});
E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\TaleWorlds.CampaignSystem.Settlements.Buildings\DefaultBuildingTypes.cs:310: 		_buildingSettlementDailyIrrigation.InitializeDailyProject(new TextObject("{=O4cknzhW}Irrigation"), new TextObject("{=CU9g49fo}Provide irrigation, increasing hearth growth in bound villages."), new Tuple<BuildingEffectEnum, BuildingEffectIncrementType, float, float, float>[1]
		{
			new Tuple<BuildingEffectEnum, BuildingEffectIncrementType, float, float, float>(BuildingEffectEnum.VillageHeartsPerDay, BuildingEffectIncrementType.Add, 1f, 1f, 1f)
		});
		_buildingCastleDailySlackenGarrison.InitializeDailyProject(new TextObject("{=cHIa0Xty}Slacken Garrison"), new TextObject("{=5VBbLVBt}Decrease garrison wages."), new Tuple<BuildingEffectEnum, BuildingEffectIncrementType, float, float, float>[1]
		{
			new Tuple<BuildingEffectEnum, BuildingEffectIncrementType, float, float, float>(BuildingEffectEnum.GarrisonWageReduction, BuildingEffectIncrementType.AddFactor, -0.05f, -0.05f, -0.05f)
		});
		_buildingCastleDailyRaiseTroops.InitializeDailyProject(new TextObject("{=jm1ScaoK}Raise Troops"), new TextObject("{=UsHhePdk}Increase militia recruitment, and auto recruitment."), new Tuple<BuildingEffectEnum, BuildingEffectIncrementType, float, float, float>[2]
		{
			new Tuple<BuildingEffectEnum, BuildingEffectIncrementType, float, float, float>(BuildingEffectEnum.Militia, BuildingEffectIncrementType.Add, 3f, 3f, 3f),
			new Tuple<BuildingEffectEnum, BuildingEffectIncrementType, float, float, float>(BuildingEffectEnum.GarrisonAutoRecruitment, BuildingEffectIncrementType.Add, 2f, 2f, 2f)
		});
		{
			new Tuple<BuildingEffectEnum, BuildingEffectIncrementType, float, float, float>(BuildingEffectEnum.Loyalty, BuildingEffectIncrementType.Add, 3f, 3f, 3f)
		});
		_buildingSettlementDailyIrrigation.InitializeDailyProject(new TextObject("{=O4cknzhW}Irrigation"), new TextObject("{=CU9g49fo}Provide irrigation, increasing hearth growth in bound villages."), new Tuple<BuildingEffectEnum, BuildingEffectIncrementType, float, float, float>[1]
		{
E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\TaleWorlds.CampaignSystem.Settlements.Buildings\DefaultBuildingTypes.cs:312: 			new Tuple<BuildingEffectEnum, BuildingEffectIncrementType, float, float, float>(BuildingEffectEnum.VillageHeartsPerDay, BuildingEffectIncrementType.Add, 1f, 1f, 1f)
		});
		_buildingCastleDailySlackenGarrison.InitializeDailyProject(new TextObject("{=cHIa0Xty}Slacken Garrison"), new TextObject("{=5VBbLVBt}Decrease garrison wages."), new Tuple<BuildingEffectEnum, BuildingEffectIncrementType, float, float, float>[1]
		{
			new Tuple<BuildingEffectEnum, BuildingEffectIncrementType, float, float, float>(BuildingEffectEnum.GarrisonWageReduction, BuildingEffectIncrementType.AddFactor, -0.05f, -0.05f, -0.05f)
		});
		_buildingCastleDailyRaiseTroops.InitializeDailyProject(new TextObject("{=jm1ScaoK}Raise Troops"), new TextObject("{=UsHhePdk}Increase militia recruitment, and auto recruitment."), new Tuple<BuildingEffectEnum, BuildingEffectIncrementType, float, float, float>[2]
		{
			new Tuple<BuildingEffectEnum, BuildingEffectIncrementType, float, float, float>(BuildingEffectEnum.Militia, BuildingEffectIncrementType.Add, 3f, 3f, 3f),
			new Tuple<BuildingEffectEnum, BuildingEffectIncrementType, float, float, float>(BuildingEffectEnum.GarrisonAutoRecruitment, BuildingEffectIncrementType.Add, 2f, 2f, 2f)
		});
		_buildingCastleDailyDrills.InitializeDailyProject(new TextObject("{=JpiQagYa}Drills"), new TextObject("{=e9V1W7nW}Provides experience to garrison."), new Tuple<BuildingEffectEnum, BuildingEffectIncrementType, float, float, float>[1]
		{
		});
		_buildingSettlementDailyIrrigation.InitializeDailyProject(new TextObject("{=O4cknzhW}Irrigation"), new TextObject("{=CU9g49fo}Provide irrigation, increasing hearth growth in bound villages."), new Tuple<BuildingEffectEnum, BuildingEffectIncrementType, float, float, float>[1]
		{
			new Tuple<BuildingEffectEnum, BuildingEffectIncrementType, float, float, float>(BuildingEffectEnum.VillageHeartsPerDay, BuildingEffectIncrementType.Add, 1f, 1f, 1f)
		});
E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\TaleWorlds.CampaignSystem.Settlements.Buildings\DefaultBuildingTypes.cs:314: 		_buildingCastleDailySlackenGarrison.InitializeDailyProject(new TextObject("{=cHIa0Xty}Slacken Garrison"), new TextObject("{=5VBbLVBt}Decrease garrison wages."), new Tuple<BuildingEffectEnum, BuildingEffectIncrementType, float, float, float>[1]
		{
			new Tuple<BuildingEffectEnum, BuildingEffectIncrementType, float, float, float>(BuildingEffectEnum.GarrisonWageReduction, BuildingEffectIncrementType.AddFactor, -0.05f, -0.05f, -0.05f)
		});
		_buildingCastleDailyRaiseTroops.InitializeDailyProject(new TextObject("{=jm1ScaoK}Raise Troops"), new TextObject("{=UsHhePdk}Increase militia recruitment, and auto recruitment."), new Tuple<BuildingEffectEnum, BuildingEffectIncrementType, float, float, float>[2]
		{
			new Tuple<BuildingEffectEnum, BuildingEffectIncrementType, float, float, float>(BuildingEffectEnum.Militia, BuildingEffectIncrementType.Add, 3f, 3f, 3f),
			new Tuple<BuildingEffectEnum, BuildingEffectIncrementType, float, float, float>(BuildingEffectEnum.GarrisonAutoRecruitment, BuildingEffectIncrementType.Add, 2f, 2f, 2f)
		});
		_buildingCastleDailyDrills.InitializeDailyProject(new TextObject("{=JpiQagYa}Drills"), new TextObject("{=e9V1W7nW}Provides experience to garrison."), new Tuple<BuildingEffectEnum, BuildingEffectIncrementType, float, float, float>[1]
		{
			new Tuple<BuildingEffectEnum, BuildingEffectIncrementType, float, float, float>(BuildingEffectEnum.ExperiencePerDay, BuildingEffectIncrementType.Add, 8f, 8f, 8f)
		});
		{
			new Tuple<BuildingEffectEnum, BuildingEffectIncrementType, float, float, float>(BuildingEffectEnum.VillageHeartsPerDay, BuildingEffectIncrementType.Add, 1f, 1f, 1f)
		});
		_buildingCastleDailySlackenGarrison.InitializeDailyProject(new TextObject("{=cHIa0Xty}Slacken Garrison"), new TextObject("{=5VBbLVBt}Decrease garrison wages."), new Tuple<BuildingEffectEnum, BuildingEffectIncrementType, float, float, float>[1]
		{
E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\TaleWorlds.CampaignSystem.Settlements.Buildings\DefaultBuildingTypes.cs:316: 			new Tuple<BuildingEffectEnum, BuildingEffectIncrementType, float, float, float>(BuildingEffectEnum.GarrisonWageReduction, BuildingEffectIncrementType.AddFactor, -0.05f, -0.05f, -0.05f)
		});
		_buildingCastleDailyRaiseTroops.InitializeDailyProject(new TextObject("{=jm1ScaoK}Raise Troops"), new TextObject("{=UsHhePdk}Increase militia recruitment, and auto recruitment."), new Tuple<BuildingEffectEnum, BuildingEffectIncrementType, float, float, float>[2]
		{
			new Tuple<BuildingEffectEnum, BuildingEffectIncrementType, float, float, float>(BuildingEffectEnum.Militia, BuildingEffectIncrementType.Add, 3f, 3f, 3f),
			new Tuple<BuildingEffectEnum, BuildingEffectIncrementType, float, float, float>(BuildingEffectEnum.GarrisonAutoRecruitment, BuildingEffectIncrementType.Add, 2f, 2f, 2f)
		});
		_buildingCastleDailyDrills.InitializeDailyProject(new TextObject("{=JpiQagYa}Drills"), new TextObject("{=e9V1W7nW}Provides experience to garrison."), new Tuple<BuildingEffectEnum, BuildingEffectIncrementType, float, float, float>[1]
		{
			new Tuple<BuildingEffectEnum, BuildingEffectIncrementType, float, float, float>(BuildingEffectEnum.ExperiencePerDay, BuildingEffectIncrementType.Add, 8f, 8f, 8f)
		});
		_buildingCastleDailyIrrigation.InitializeDailyProject(new TextObject("{=O4cknzhW}Irrigation"), new TextObject("{=CU9g49fo}Provide irrigation, increasing hearth growth in bound villages."), new Tuple<BuildingEffectEnum, BuildingEffectIncrementType, float, float, float>[1]
		{
		});
		_buildingCastleDailySlackenGarrison.InitializeDailyProject(new TextObject("{=cHIa0Xty}Slacken Garrison"), new TextObject("{=5VBbLVBt}Decrease garrison wages."), new Tuple<BuildingEffectEnum, BuildingEffectIncrementType, float, float, float>[1]
		{
			new Tuple<BuildingEffectEnum, BuildingEffectIncrementType, float, float, float>(BuildingEffectEnum.GarrisonWageReduction, BuildingEffectIncrementType.AddFactor, -0.05f, -0.05f, -0.05f)
		});
E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\TaleWorlds.CampaignSystem.Settlements.Buildings\DefaultBuildingTypes.cs:318: 		_buildingCastleDailyRaiseTroops.InitializeDailyProject(new TextObject("{=jm1ScaoK}Raise Troops"), new TextObject("{=UsHhePdk}Increase militia recruitment, and auto recruitment."), new Tuple<BuildingEffectEnum, BuildingEffectIncrementType, float, float, float>[2]
		{
			new Tuple<BuildingEffectEnum, BuildingEffectIncrementType, float, float, float>(BuildingEffectEnum.Militia, BuildingEffectIncrementType.Add, 3f, 3f, 3f),
			new Tuple<BuildingEffectEnum, BuildingEffectIncrementType, float, float, float>(BuildingEffectEnum.GarrisonAutoRecruitment, BuildingEffectIncrementType.Add, 2f, 2f, 2f)
		});
		_buildingCastleDailyDrills.InitializeDailyProject(new TextObject("{=JpiQagYa}Drills"), new TextObject("{=e9V1W7nW}Provides experience to garrison."), new Tuple<BuildingEffectEnum, BuildingEffectIncrementType, float, float, float>[1]
		{
			new Tuple<BuildingEffectEnum, BuildingEffectIncrementType, float, float, float>(BuildingEffectEnum.ExperiencePerDay, BuildingEffectIncrementType.Add, 8f, 8f, 8f)
		});
		_buildingCastleDailyIrrigation.InitializeDailyProject(new TextObject("{=O4cknzhW}Irrigation"), new TextObject("{=CU9g49fo}Provide irrigation, increasing hearth growth in bound villages."), new Tuple<BuildingEffectEnum, BuildingEffectIncrementType, float, float, float>[1]
		{
			new Tuple<BuildingEffectEnum, BuildingEffectIncrementType, float, float, float>(BuildingEffectEnum.VillageHeartsPerDay, BuildingEffectIncrementType.AddFactor, 0.5f, 0.5f, 0.5f)
		});
		{
			new Tuple<BuildingEffectEnum, BuildingEffectIncrementType, float, float, float>(BuildingEffectEnum.GarrisonWageReduction, BuildingEffectIncrementType.AddFactor, -0.05f, -0.05f, -0.05f)
		});
		_buildingCastleDailyRaiseTroops.InitializeDailyProject(new TextObject("{=jm1ScaoK}Raise Troops"), new TextObject("{=UsHhePdk}Increase militia recruitment, and auto recruitment."), new Tuple<BuildingEffectEnum, BuildingEffectIncrementType, float, float, float>[2]
		{
E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\TaleWorlds.CampaignSystem.Settlements.Buildings\DefaultBuildingTypes.cs:320: 			new Tuple<BuildingEffectEnum, BuildingEffectIncrementType, float, float, float>(BuildingEffectEnum.Militia, BuildingEffectIncrementType.Add, 3f, 3f, 3f),
			new Tuple<BuildingEffectEnum, BuildingEffectIncrementType, float, float, float>(BuildingEffectEnum.GarrisonAutoRecruitment, BuildingEffectIncrementType.Add, 2f, 2f, 2f)
		});
		_buildingCastleDailyDrills.InitializeDailyProject(new TextObject("{=JpiQagYa}Drills"), new TextObject("{=e9V1W7nW}Provides experience to garrison."), new Tuple<BuildingEffectEnum, BuildingEffectIncrementType, float, float, float>[1]
		{
			new Tuple<BuildingEffectEnum, BuildingEffectIncrementType, float, float, float>(BuildingEffectEnum.ExperiencePerDay, BuildingEffectIncrementType.Add, 8f, 8f, 8f)
		});
		_buildingCastleDailyIrrigation.InitializeDailyProject(new TextObject("{=O4cknzhW}Irrigation"), new TextObject("{=CU9g49fo}Provide irrigation, increasing hearth growth in bound villages."), new Tuple<BuildingEffectEnum, BuildingEffectIncrementType, float, float, float>[1]
		{
			new Tuple<BuildingEffectEnum, BuildingEffectIncrementType, float, float, float>(BuildingEffectEnum.VillageHeartsPerDay, BuildingEffectIncrementType.AddFactor, 0.5f, 0.5f, 0.5f)
		});
	}
}
			new Tuple<BuildingEffectEnum, BuildingEffectIncrementType, float, float, float>(BuildingEffectEnum.GarrisonWageReduction, BuildingEffectIncrementType.AddFactor, -0.05f, -0.05f, -0.05f)
		});
		_buildingCastleDailyRaiseTroops.InitializeDailyProject(new TextObject("{=jm1ScaoK}Raise Troops"), new TextObject("{=UsHhePdk}Increase militia recruitment, and auto recruitment."), new Tuple<BuildingEffectEnum, BuildingEffectIncrementType, float, float, float>[2]
		{
			new Tuple<BuildingEffectEnum, BuildingEffectIncrementType, float, float, float>(BuildingEffectEnum.Militia, BuildingEffectIncrementType.Add, 3f, 3f, 3f),
E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\TaleWorlds.CampaignSystem.Settlements.Buildings\DefaultBuildingTypes.cs:321: 			new Tuple<BuildingEffectEnum, BuildingEffectIncrementType, float, float, float>(BuildingEffectEnum.GarrisonAutoRecruitment, BuildingEffectIncrementType.Add, 2f, 2f, 2f)
		});
		_buildingCastleDailyDrills.InitializeDailyProject(new TextObject("{=JpiQagYa}Drills"), new TextObject("{=e9V1W7nW}Provides experience to garrison."), new Tuple<BuildingEffectEnum, BuildingEffectIncrementType, float, float, float>[1]
		{
			new Tuple<BuildingEffectEnum, BuildingEffectIncrementType, float, float, float>(BuildingEffectEnum.ExperiencePerDay, BuildingEffectIncrementType.Add, 8f, 8f, 8f)
		});
		_buildingCastleDailyIrrigation.InitializeDailyProject(new TextObject("{=O4cknzhW}Irrigation"), new TextObject("{=CU9g49fo}Provide irrigation, increasing hearth growth in bound villages."), new Tuple<BuildingEffectEnum, BuildingEffectIncrementType, float, float, float>[1]
		{
			new Tuple<BuildingEffectEnum, BuildingEffectIncrementType, float, float, float>(BuildingEffectEnum.VillageHeartsPerDay, BuildingEffectIncrementType.AddFactor, 0.5f, 0.5f, 0.5f)
		});
	}
}
		_buildingCastleDailyRaiseTroops.InitializeDailyProject(new TextObject("{=jm1ScaoK}Raise Troops"), new TextObject("{=UsHhePdk}Increase militia recruitment, and auto recruitment."), new Tuple<BuildingEffectEnum, BuildingEffectIncrementType, float, float, float>[2]
		{
			new Tuple<BuildingEffectEnum, BuildingEffectIncrementType, float, float, float>(BuildingEffectEnum.Militia, BuildingEffectIncrementType.Add, 3f, 3f, 3f),
			new Tuple<BuildingEffectEnum, BuildingEffectIncrementType, float, float, float>(BuildingEffectEnum.GarrisonAutoRecruitment, BuildingEffectIncrementType.Add, 2f, 2f, 2f)
		});
E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\TaleWorlds.CampaignSystem.Settlements.Buildings\DefaultBuildingTypes.cs:323: 		_buildingCastleDailyDrills.InitializeDailyProject(new TextObject("{=JpiQagYa}Drills"), new TextObject("{=e9V1W7nW}Provides experience to garrison."), new Tuple<BuildingEffectEnum, BuildingEffectIncrementType, float, float, float>[1]
		{
			new Tuple<BuildingEffectEnum, BuildingEffectIncrementType, float, float, float>(BuildingEffectEnum.ExperiencePerDay, BuildingEffectIncrementType.Add, 8f, 8f, 8f)
		});
		_buildingCastleDailyIrrigation.InitializeDailyProject(new TextObject("{=O4cknzhW}Irrigation"), new TextObject("{=CU9g49fo}Provide irrigation, increasing hearth growth in bound villages."), new Tuple<BuildingEffectEnum, BuildingEffectIncrementType, float, float, float>[1]
		{
			new Tuple<BuildingEffectEnum, BuildingEffectIncrementType, float, float, float>(BuildingEffectEnum.VillageHeartsPerDay, BuildingEffectIncrementType.AddFactor, 0.5f, 0.5f, 0.5f)
		});
	}
}
			new Tuple<BuildingEffectEnum, BuildingEffectIncrementType, float, float, float>(BuildingEffectEnum.Militia, BuildingEffectIncrementType.Add, 3f, 3f, 3f),
			new Tuple<BuildingEffectEnum, BuildingEffectIncrementType, float, float, float>(BuildingEffectEnum.GarrisonAutoRecruitment, BuildingEffectIncrementType.Add, 2f, 2f, 2f)
		});
		_buildingCastleDailyDrills.InitializeDailyProject(new TextObject("{=JpiQagYa}Drills"), new TextObject("{=e9V1W7nW}Provides experience to garrison."), new Tuple<BuildingEffectEnum, BuildingEffectIncrementType, float, float, float>[1]
		{
E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\TaleWorlds.CampaignSystem.Settlements.Buildings\DefaultBuildingTypes.cs:325: 			new Tuple<BuildingEffectEnum, BuildingEffectIncrementType, float, float, float>(BuildingEffectEnum.ExperiencePerDay, BuildingEffectIncrementType.Add, 8f, 8f, 8f)
		});
		_buildingCastleDailyIrrigation.InitializeDailyProject(new TextObject("{=O4cknzhW}Irrigation"), new TextObject("{=CU9g49fo}Provide irrigation, increasing hearth growth in bound villages."), new Tuple<BuildingEffectEnum, BuildingEffectIncrementType, float, float, float>[1]
		{
			new Tuple<BuildingEffectEnum, BuildingEffectIncrementType, float, float, float>(BuildingEffectEnum.VillageHeartsPerDay, BuildingEffectIncrementType.AddFactor, 0.5f, 0.5f, 0.5f)
		});
	}
}
		});
		_buildingCastleDailyDrills.InitializeDailyProject(new TextObject("{=JpiQagYa}Drills"), new TextObject("{=e9V1W7nW}Provides experience to garrison."), new Tuple<BuildingEffectEnum, BuildingEffectIncrementType, float, float, float>[1]
		{
			new Tuple<BuildingEffectEnum, BuildingEffectIncrementType, float, float, float>(BuildingEffectEnum.ExperiencePerDay, BuildingEffectIncrementType.Add, 8f, 8f, 8f)
		});
E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\TaleWorlds.CampaignSystem.Settlements.Buildings\DefaultBuildingTypes.cs:327: 		_buildingCastleDailyIrrigation.InitializeDailyProject(new TextObject("{=O4cknzhW}Irrigation"), new TextObject("{=CU9g49fo}Provide irrigation, increasing hearth growth in bound villages."), new Tuple<BuildingEffectEnum, BuildingEffectIncrementType, float, float, float>[1]
		{
			new Tuple<BuildingEffectEnum, BuildingEffectIncrementType, float, float, float>(BuildingEffectEnum.VillageHeartsPerDay, BuildingEffectIncrementType.AddFactor, 0.5f, 0.5f, 0.5f)
		});
	}
}
		{
			new Tuple<BuildingEffectEnum, BuildingEffectIncrementType, float, float, float>(BuildingEffectEnum.ExperiencePerDay, BuildingEffectIncrementType.Add, 8f, 8f, 8f)
		});
		_buildingCastleDailyIrrigation.InitializeDailyProject(new TextObject("{=O4cknzhW}Irrigation"), new TextObject("{=CU9g49fo}Provide irrigation, increasing hearth growth in bound villages."), new Tuple<BuildingEffectEnum, BuildingEffectIncrementType, float, float, float>[1]
		{
E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\TaleWorlds.CampaignSystem.Settlements.Buildings\DefaultBuildingTypes.cs:329: 			new Tuple<BuildingEffectEnum, BuildingEffectIncrementType, float, float, float>(BuildingEffectEnum.VillageHeartsPerDay, BuildingEffectIncrementType.AddFactor, 0.5f, 0.5f, 0.5f)
		});
	}
}

exec
"C:\\Program Files\\PowerShell\\7\\pwsh.exe" -Command "Select-String -Path 'E:\\Decompiled_Bannerlord\\Campaign\\TaleWorlds.CampaignSystem\\TaleWorlds.CampaignSystem.Settlements.Buildings\\DefaultBuildingTypes.cs' -Pattern 'FoodProduction' | ForEach-Object { \"{0}:{1}: {2}\" -f "'$_.Path,$_.LineNumber,$_.Line.Trim() }' in C:\Users\mikew\source\repos\TAOM
 succeeded in 443ms:
E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\TaleWorlds.CampaignSystem.Settlements.Buildings\DefaultBuildingTypes.cs:285: new Tuple<BuildingEffectEnum, BuildingEffectIncrementType, float, float, float>(BuildingEffectEnum.FoodProduction, BuildingEffectIncrementType.Add, 6f, 12f, 18f)

codex
The actual food-production building effect is flat `Add`, not `AddFactor`, so the post-base production delta is not missing a multiplier in vanilla v1.4.x. That removes the building-effect drift concern.
exec
"C:\\Program Files\\PowerShell\\7\\pwsh.exe" -Command '$path='"'E:\\Decompiled_Bannerlord\\Campaign\\TaleWorlds.CampaignSystem\\TaleWorlds.CampaignSystem.Settlements.Buildings\\DefaultBuildingTypes.cs'; "'$i=0; Get-Content $path | ForEach-Object { $i++; if($i -ge 276 -and $i -le 288){ '"'{0}:{1}: {2}' -f "'$path,$i,$_ } }' in C:\Users\mikew\source\repos\TAOM
 succeeded in 456ms:
E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\TaleWorlds.CampaignSystem.Settlements.Buildings\DefaultBuildingTypes.cs:276: 		{
E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\TaleWorlds.CampaignSystem.Settlements.Buildings\DefaultBuildingTypes.cs:277: 			new Tuple<BuildingEffectEnum, BuildingEffectIncrementType, float, float, float>(BuildingEffectEnum.FoodStock, BuildingEffectIncrementType.Add, 100f, 200f, 300f)
E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\TaleWorlds.CampaignSystem.Settlements.Buildings\DefaultBuildingTypes.cs:278: 		}, isMilitaryProject: false, 0f);
E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\TaleWorlds.CampaignSystem.Settlements.Buildings\DefaultBuildingTypes.cs:279: 		_buildingCastleCraftmansQuarters.Initialize(new TextObject("{=KE1KUayw}Craftmans Quarters"), new TextObject("{=2qZ14G9p}Provides income based on bound village hearts"), new int[3] { 350, 490, 630 }, new Tuple<BuildingEffectEnum, BuildingEffectIncrementType, float, float, float>[1]
E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\TaleWorlds.CampaignSystem.Settlements.Buildings\DefaultBuildingTypes.cs:280: 		{
E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\TaleWorlds.CampaignSystem.Settlements.Buildings\DefaultBuildingTypes.cs:281: 			new Tuple<BuildingEffectEnum, BuildingEffectIncrementType, float, float, float>(BuildingEffectEnum.DenarByBoundVillageHeartPerDay, BuildingEffectIncrementType.Add, 0.2f, 0.4f, 0.6f)
E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\TaleWorlds.CampaignSystem.Settlements.Buildings\DefaultBuildingTypes.cs:282: 		}, isMilitaryProject: false, 0f);
E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\TaleWorlds.CampaignSystem.Settlements.Buildings\DefaultBuildingTypes.cs:283: 		_buildingCastleFarmlands.Initialize(new TextObject("{=l4eZqegY}Farmlands"), new TextObject("{=tajCl8Bg}Provides daily food."), new int[3] { 420, 630, 840 }, new Tuple<BuildingEffectEnum, BuildingEffectIncrementType, float, float, float>[1]
E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\TaleWorlds.CampaignSystem.Settlements.Buildings\DefaultBuildingTypes.cs:284: 		{
E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\TaleWorlds.CampaignSystem.Settlements.Buildings\DefaultBuildingTypes.cs:285: 			new Tuple<BuildingEffectEnum, BuildingEffectIncrementType, float, float, float>(BuildingEffectEnum.FoodProduction, BuildingEffectIncrementType.Add, 6f, 12f, 18f)
E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\TaleWorlds.CampaignSystem.Settlements.Buildings\DefaultBuildingTypes.cs:286: 		}, isMilitaryProject: false, 0f);
E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\TaleWorlds.CampaignSystem.Settlements.Buildings\DefaultBuildingTypes.cs:287: 		_buildingCastleMason.Initialize(new TextObject("{=R7ssoDHW}Mason"), new TextObject("{=hqUPvnaj}Increase bricks per day, increasing building and repair speed."), new int[3] { 560, 700, 1120 }, new Tuple<BuildingEffectEnum, BuildingEffectIncrementType, float, float, float>[2]
E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\TaleWorlds.CampaignSystem.Settlements.Buildings\DefaultBuildingTypes.cs:288: 		{

codex
**KNOWN SUSPECTS**

1. CONFIRMED -- no double-counting found. `TaomSettlementFoodModel` does `base.CalculateTownFoodStocksChange(...)` then applies one service adjustment [Main/Features/SettlementFood/Models/TaomSettlementFoodModel.cs:44-48]. The service uses deltas only: garrison `(weighted - raw) / divisor` [Main/Features/SettlementFood/SettlementFoodService.cs:26-29], base food `configBase - vanillaBase` [Main/Features/SettlementFood/SettlementFoodService.cs:35-37], village `(hearth + 1) * (config - 6)` [Main/Features/SettlementFood/SettlementFoodService.cs:39-43], flat bonus once [Main/Features/SettlementFood/SettlementFoodService.cs:46]. Default JSON matches vanilla values [Main/_Module/ModuleData/settlement_food/settlement_food_config.json:2-9], so production deltas are zero at defaults; garrison correction is intentionally nonzero only when weighted > raw.

2. CONFIRMED -- divisor consistency holds. The model returns `config.GarrisonFoodDivisor` when enabled and vanilla `20` when disabled [Main/Features/SettlementFood/Models/TaomSettlementFoodModel.cs:34-38]. The correction uses the same `config.GarrisonFoodDivisor` [Main/Features/SettlementFood/SettlementFoodService.cs:26-29]. Vanilla base consumes `NumberOfAllMembers / NumberOfMenOnGarrisonToEatOneFood` [E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\TaleWorlds.CampaignSystem.GameComponents\DefaultSettlementFoodModel.cs:47-48]. Disabled is consistent: model returns vanilla constants, service returns zero [Main/Features/SettlementFood/SettlementFoodService.cs:18-19].

3. CONFIRMED -- siege gating matches vanilla. TAOM keeps garrison correction before the siege guard [Main/Features/SettlementFood/SettlementFoodService.cs:23-33] and puts base/village/flat production inside `!IsUnderSiege` [Main/Features/SettlementFood/SettlementFoodService.cs:33-47]. Vanilla computes prosperity/garrison consumption before the siege production branch [E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\TaleWorlds.CampaignSystem.GameComponents\DefaultSettlementFoodModel.cs:47-57], adds lands/villages only under `!town.IsUnderSiege` [E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\TaleWorlds.CampaignSystem.GameComponents\DefaultSettlementFoodModel.cs:63-76], and otherwise only applies DirtyFighting [E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\TaleWorlds.CampaignSystem.GameComponents\DefaultSettlementFoodModel.cs:78-80].

4. CONFIRMED -- weighted/raw guard is clean. Snapshot reads weighted through `garrison?.Party.NumberOfAllMembers` and raw through `garrison?.MemberRoster?.TotalManCount` [Main/Features/SettlementFood/TownFoodSnapshot.cs:45-47]. Patch17 targets the exact getter [Main/Features/TroopWeight/Hooks/PartyBase_NumberOfAllMembers_Patch.cs:6], exits when Troop Weight is off [Main/Features/TroopWeight/Hooks/PartyBase_NumberOfAllMembers_Patch.cs:17], and the hook only raises `__result` when weighted is greater [Main/Features/TroopWeight/Hooks/PartyBaseNumberOfAllMembersHook.cs:35-41]. Shipped troop weights scan: 88 entries, `belowOne=0`, min weight 2.0 at [Main/_Module/ModuleData/TroopWeights/troop_weights.xml:64]; examples show 4.0, 3.0, and 2.0 weights [Main/_Module/ModuleData/TroopWeights/troop_weights.xml:7], [Main/_Module/ModuleData/TroopWeights/troop_weights.xml:55-70].

5. CONFIRMED -- null and village-state behavior matches vanilla. TAOM null-guards `garrison` but not `garrison.Party` [Main/Features/SettlementFood/TownFoodSnapshot.cs:45-46]; vanilla has the same shape with `town.GarrisonParty?.Party.NumberOfAllMembers` [E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\TaleWorlds.CampaignSystem.GameComponents\DefaultSettlementFoodModel.cs:48]. Valid constructed `MobileParty` creates `Party = new PartyBase(this)` [E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\TaleWorlds.CampaignSystem.Party\MobileParty.cs:1851-1856], and `PartyBase.NumberOfAllMembers` is `MemberRoster.TotalManCount` in vanilla [E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\TaleWorlds.CampaignSystem.Party\PartyBase.cs:381]. TAOM filters only `VillageStates.Normal` [Main/Features/SettlementFood/TownFoodSnapshot.cs:53-56], matching vanilla’s only productive village state [E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\TaleWorlds.CampaignSystem.GameComponents\DefaultSettlementFoodModel.cs:67-74].

6. CONFIRMED with one LOW issue. Divisors reject values outside `[1,10000]` [Main/Features/SettlementFood/SettlementFoodConfigProvider.cs:67-79]. Float fields use `FiniteFloatValidator.IsFiniteInRange` [Main/Features/SettlementFood/SettlementFoodConfigProvider.cs:82-108], and the validator rejects NaN/Infinity [Main/Core/Validation/FiniteFloatValidator.cs:22-34]. Parse failures fall back to defaults [Main/Features/SettlementFood/SettlementFoodConfigProvider.cs:36-44]. Nuance: below-vanilla but nonnegative production values are accepted and produce negative deltas; see finding 1.

**DEEP ANALYSIS**

Scenario: town, prosperity 3000, raw garrison 500, weighted garrison 750, 3 Normal villages at hearthLevel 1, config `garrisonDivisor=30`, `prosperityDivisor=60`, `villageMultiplier=9`, default town base 15 and flat 0.

Base call in TAOM uses overridden divisors but vanilla hardcoded production:
`production = 15 + 3 * ((1 + 1) * 6) = 51`
`consumption = 3000 / 60 + 750 / 30 = 50 + 25 = 75`
`base result = 51 - 75 = -24`

Service delta:
`garrison correction = (750 - 500) / 30 = 8.3333`
`base delta = 15 - 15 = 0`
`village delta = 3 * ((1 + 1) * (9 - 6)) = 18`
`flat = 0`
`total delta = 26.3333`

Final:
`-24 + 26.3333 = +2.3333`

Equivalent intended final formula:
`production = 15 + 3 * ((1 + 1) * 9) = 69`
`consumption = 3000 / 60 + 500 / 30 = 50 + 16.6667 = 66.6667`
`final = 69 - 66.6667 = +2.3333`

No double-count in this scenario.

**FINDINGS**

1. [LOW] Main/Features/SettlementFood/SettlementFoodConfigProvider.cs:82 -- Config contract -- Production validation accepts below-vanilla values even though comments/docs say production knobs add food and validation prevents worsening the deficit. Example: `townBaseFood=0` passes `[0,10000]` validation [Main/Features/SettlementFood/SettlementFoodConfigProvider.cs:83-87], then service applies `0 - 15` [Main/Features/SettlementFood/SettlementFoodService.cs:35-37]. Same shape for `villageFoodMultiplier=0` [Main/Features/SettlementFood/SettlementFoodService.cs:39-43]. Fix: if relief-only is intended, validate `TownBaseFood >= 15`, `CastleBaseFood >= 10`, and `VillageFoodMultiplier >= 6`; otherwise update the comments/docs to say these replace vanilla constants and can lower production.

Verification note: attempted `dotnet test TAOM.Tests/TAOM.Tests.csproj --filter SettlementFood --no-restore`; blocked by sandbox access to `C:\Users\mikew\AppData\Local\Microsoft SDKs`.

CRITICAL: 0 | HIGH: 0 | MEDIUM: 0 | LOW: 1  
VERDICT: ISSUES FOUND
SUCCESS: The process with PID 36396 (child process of PID 69996) has been terminated.
SUCCESS: The process with PID 69996 (child process of PID 63800) has been terminated.
SUCCESS: The process with PID 63800 (child process of PID 30232) has been terminated.
SUCCESS: The process with PID 30232 (child process of PID 31536) has been terminated.
SUCCESS: The process with PID 31536 (child process of PID 73468) has been terminated.
SUCCESS: The process with PID 73336 (child process of PID 52024) has been terminated.
SUCCESS: The process with PID 52024 (child process of PID 66696) has been terminated.
SUCCESS: The process with PID 66696 (child process of PID 13764) has been terminated.
SUCCESS: The process with PID 13764 (child process of PID 73468) has been terminated.
tokens used
211,969
**KNOWN SUSPECTS**

1. CONFIRMED -- no double-counting found. `TaomSettlementFoodModel` does `base.CalculateTownFoodStocksChange(...)` then applies one service adjustment [Main/Features/SettlementFood/Models/TaomSettlementFoodModel.cs:44-48]. The service uses deltas only: garrison `(weighted - raw) / divisor` [Main/Features/SettlementFood/SettlementFoodService.cs:26-29], base food `configBase - vanillaBase` [Main/Features/SettlementFood/SettlementFoodService.cs:35-37], village `(hearth + 1) * (config - 6)` [Main/Features/SettlementFood/SettlementFoodService.cs:39-43], flat bonus once [Main/Features/SettlementFood/SettlementFoodService.cs:46]. Default JSON matches vanilla values [Main/_Module/ModuleData/settlement_food/settlement_food_config.json:2-9], so production deltas are zero at defaults; garrison correction is intentionally nonzero only when weighted > raw.

2. CONFIRMED -- divisor consistency holds. The model returns `config.GarrisonFoodDivisor` when enabled and vanilla `20` when disabled [Main/Features/SettlementFood/Models/TaomSettlementFoodModel.cs:34-38]. The correction uses the same `config.GarrisonFoodDivisor` [Main/Features/SettlementFood/SettlementFoodService.cs:26-29]. Vanilla base consumes `NumberOfAllMembers / NumberOfMenOnGarrisonToEatOneFood` [E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\TaleWorlds.CampaignSystem.GameComponents\DefaultSettlementFoodModel.cs:47-48]. Disabled is consistent: model returns vanilla constants, service returns zero [Main/Features/SettlementFood/SettlementFoodService.cs:18-19].

3. CONFIRMED -- siege gating matches vanilla. TAOM keeps garrison correction before the siege guard [Main/Features/SettlementFood/SettlementFoodService.cs:23-33] and puts base/village/flat production inside `!IsUnderSiege` [Main/Features/SettlementFood/SettlementFoodService.cs:33-47]. Vanilla computes prosperity/garrison consumption before the siege production branch [E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\TaleWorlds.CampaignSystem.GameComponents\DefaultSettlementFoodModel.cs:47-57], adds lands/villages only under `!town.IsUnderSiege` [E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\TaleWorlds.CampaignSystem.GameComponents\DefaultSettlementFoodModel.cs:63-76], and otherwise only applies DirtyFighting [E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\TaleWorlds.CampaignSystem.GameComponents\DefaultSettlementFoodModel.cs:78-80].

4. CONFIRMED -- weighted/raw guard is clean. Snapshot reads weighted through `garrison?.Party.NumberOfAllMembers` and raw through `garrison?.MemberRoster?.TotalManCount` [Main/Features/SettlementFood/TownFoodSnapshot.cs:45-47]. Patch17 targets the exact getter [Main/Features/TroopWeight/Hooks/PartyBase_NumberOfAllMembers_Patch.cs:6], exits when Troop Weight is off [Main/Features/TroopWeight/Hooks/PartyBase_NumberOfAllMembers_Patch.cs:17], and the hook only raises `__result` when weighted is greater [Main/Features/TroopWeight/Hooks/PartyBaseNumberOfAllMembersHook.cs:35-41]. Shipped troop weights scan: 88 entries, `belowOne=0`, min weight 2.0 at [Main/_Module/ModuleData/TroopWeights/troop_weights.xml:64]; examples show 4.0, 3.0, and 2.0 weights [Main/_Module/ModuleData/TroopWeights/troop_weights.xml:7], [Main/_Module/ModuleData/TroopWeights/troop_weights.xml:55-70].

5. CONFIRMED -- null and village-state behavior matches vanilla. TAOM null-guards `garrison` but not `garrison.Party` [Main/Features/SettlementFood/TownFoodSnapshot.cs:45-46]; vanilla has the same shape with `town.GarrisonParty?.Party.NumberOfAllMembers` [E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\TaleWorlds.CampaignSystem.GameComponents\DefaultSettlementFoodModel.cs:48]. Valid constructed `MobileParty` creates `Party = new PartyBase(this)` [E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\TaleWorlds.CampaignSystem.Party\MobileParty.cs:1851-1856], and `PartyBase.NumberOfAllMembers` is `MemberRoster.TotalManCount` in vanilla [E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\TaleWorlds.CampaignSystem.Party\PartyBase.cs:381]. TAOM filters only `VillageStates.Normal` [Main/Features/SettlementFood/TownFoodSnapshot.cs:53-56], matching vanilla’s only productive village state [E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\TaleWorlds.CampaignSystem.GameComponents\DefaultSettlementFoodModel.cs:67-74].

6. CONFIRMED with one LOW issue. Divisors reject values outside `[1,10000]` [Main/Features/SettlementFood/SettlementFoodConfigProvider.cs:67-79]. Float fields use `FiniteFloatValidator.IsFiniteInRange` [Main/Features/SettlementFood/SettlementFoodConfigProvider.cs:82-108], and the validator rejects NaN/Infinity [Main/Core/Validation/FiniteFloatValidator.cs:22-34]. Parse failures fall back to defaults [Main/Features/SettlementFood/SettlementFoodConfigProvider.cs:36-44]. Nuance: below-vanilla but nonnegative production values are accepted and produce negative deltas; see finding 1.

**DEEP ANALYSIS**

Scenario: town, prosperity 3000, raw garrison 500, weighted garrison 750, 3 Normal villages at hearthLevel 1, config `garrisonDivisor=30`, `prosperityDivisor=60`, `villageMultiplier=9`, default town base 15 and flat 0.

Base call in TAOM uses overridden divisors but vanilla hardcoded production:
`production = 15 + 3 * ((1 + 1) * 6) = 51`
`consumption = 3000 / 60 + 750 / 30 = 50 + 25 = 75`
`base result = 51 - 75 = -24`

Service delta:
`garrison correction = (750 - 500) / 30 = 8.3333`
`base delta = 15 - 15 = 0`
`village delta = 3 * ((1 + 1) * (9 - 6)) = 18`
`flat = 0`
`total delta = 26.3333`

Final:
`-24 + 26.3333 = +2.3333`

Equivalent intended final formula:
`production = 15 + 3 * ((1 + 1) * 9) = 69`
`consumption = 3000 / 60 + 500 / 30 = 50 + 16.6667 = 66.6667`
`final = 69 - 66.6667 = +2.3333`

No double-count in this scenario.

**FINDINGS**

1. [LOW] Main/Features/SettlementFood/SettlementFoodConfigProvider.cs:82 -- Config contract -- Production validation accepts below-vanilla values even though comments/docs say production knobs add food and validation prevents worsening the deficit. Example: `townBaseFood=0` passes `[0,10000]` validation [Main/Features/SettlementFood/SettlementFoodConfigProvider.cs:83-87], then service applies `0 - 15` [Main/Features/SettlementFood/SettlementFoodService.cs:35-37]. Same shape for `villageFoodMultiplier=0` [Main/Features/SettlementFood/SettlementFoodService.cs:39-43]. Fix: if relief-only is intended, validate `TownBaseFood >= 15`, `CastleBaseFood >= 10`, and `VillageFoodMultiplier >= 6`; otherwise update the comments/docs to say these replace vanilla constants and can lower production.

Verification note: attempted `dotnet test TAOM.Tests/TAOM.Tests.csproj --filter SettlementFood --no-restore`; blocked by sandbox access to `C:\Users\mikew\AppData\Local\Microsoft SDKs`.

CRITICAL: 0 | HIGH: 0 | MEDIUM: 0 | LOW: 1  
VERDICT: ISSUES FOUND
