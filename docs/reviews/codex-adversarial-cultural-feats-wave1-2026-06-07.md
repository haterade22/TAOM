# Codex Adversarial Review - Cultural Feats Wave 1 - 2026-06-07

Scope reviewed: commits `bf9226f` and `ce07ebe`, focused on the 24 Wave 1 cultural feats, their service routing, XML/XSLT attachments, faction-map strings, tests, and balance implications.

## 1. VANILLA CODE

Decompiled with `ilspycmd` from installed v1.4.5 DLLs under `E:\Steam\steamapps\common\Mount & Blade II Bannerlord\bin\Win64_Shipping_Client\`.

### FeatObject.Initialize

Source: `TaleWorlds.CampaignSystem.dll`, type `TaleWorlds.CampaignSystem.CharacterDevelopment.FeatObject`.

```csharp
public sealed class FeatObject : PropertyObject
{
    public enum AdditionType
    {
        Add,
        AddFactor
    }

    public float EffectBonus { get; private set; }

    public AdditionType IncrementType { get; private set; }

    public bool IsPositive { get; private set; }

    public void Initialize(string name, string description, float effectBonus, bool isPositiveEffect, AdditionType incrementType)
    {
        Initialize(new TextObject(name), new TextObject(description));
        EffectBonus = effectBonus;
        IncrementType = incrementType;
        IsPositive = isPositiveEffect;
        AfterInitialized();
    }
}
```

Verdict: the five-argument signature used by the new feats is valid.

### ExplainedNumber.Add / AddFactor

The prompt named `TaleWorlds.Library.ExplainedNumber`; in this install the actual type is `TaleWorlds.CampaignSystem.ExplainedNumber` from `TaleWorlds.CampaignSystem.dll`.

```csharp
public float ResultNumber => MathF.Clamp(_unclampedResultNumber, LimitMinValue, LimitMaxValue);

public float BaseNumber { get; private set; }

public float SumOfFactors { get; private set; }

private float _unclampedResultNumber => BaseNumber + BaseNumber * SumOfFactors;

public void Add(float value, TextObject description = null, TextObject variable = null)
{
    if (value.ApproximatelyEqualsTo(0f))
    {
        return;
    }
    BaseNumber += value;
    if (_explainer != null && description != null && !value.ApproximatelyEqualsTo(0f))
    {
        if (variable != null)
        {
            description.SetTextVariable("A0", variable);
        }
        _explainer.AddLine(description.ToString(), value, StatExplainer.OperationType.Add);
    }
}

public void AddFactor(float value, TextObject description = null)
{
    if (!value.ApproximatelyEqualsTo(0f))
    {
        SumOfFactors += value;
        if (description != null && _explainer != null && !value.ApproximatelyEqualsTo(0f))
        {
            _explainer.AddLine(description.ToString(), MathF.Round(value, 3) * 100f, StatExplainer.OperationType.Multiply);
        }
    }
}
```

Verdict: negative `Add` values reduce the base number directly; negative `AddFactor` values reduce the multiplier. No assert/underflow guard is involved.

### DefaultSettlementLoyaltyModel

Source: `TaleWorlds.CampaignSystem.dll`, type `TaleWorlds.CampaignSystem.GameComponents.DefaultSettlementLoyaltyModel`.

```csharp
public override ExplainedNumber CalculateLoyaltyChange(Town town, bool includeDescriptions = false)
{
    return CalculateLoyaltyChangeInternal(town, includeDescriptions);
}

private ExplainedNumber CalculateLoyaltyChangeInternal(Town town, bool includeDescriptions = false)
{
    ExplainedNumber explainedNumber = new ExplainedNumber(0f, includeDescriptions);
    GetSettlementLoyaltyChangeDueToFoodStocks(town, ref explainedNumber);
    GetSettlementLoyaltyChangeDueToGovernorCulture(town, ref explainedNumber);
    GetSettlementLoyaltyChangeDueToOwnerCulture(town, ref explainedNumber);
    GetSettlementLoyaltyChangeDueToPolicies(town, ref explainedNumber);
    GetSettlementLoyaltyChangeDueToProjects(town, ref explainedNumber);
    GetSettlementLoyaltyChangeDueToIssues(town, ref explainedNumber);
    GetSettlementLoyaltyChangeDueToSecurity(town, ref explainedNumber);
    GetSettlementLoyaltyChangeDueToNotableRelations(town, ref explainedNumber);
    GetSettlementLoyaltyChangeDueToGovernorPerks(town, ref explainedNumber);
    GetSettlementLoyaltyChangeDueToLoyaltyDrift(town, ref explainedNumber);
    return explainedNumber;
}

private void GetSettlementLoyaltyChangeDueToOwnerCulture(Town town, ref ExplainedNumber explainedNumber)
{
    if (town.Settlement.OwnerClan.Culture != town.Settlement.Culture)
    {
        explainedNumber.Add(SettlementOwnerDifferentCultureLoyaltyEffect, CultureText);
    }
}

private void GetSettlementLoyaltyChangeDueToPolicies(Town town, ref ExplainedNumber explainedNumber)
{
    Kingdom kingdom = town.Owner.Settlement.OwnerClan.Kingdom;
    if (kingdom == null)
    {
        return;
    }
    if (kingdom.ActivePolicies.Contains(DefaultPolicies.HuntingRights))
    {
        explainedNumber.Add(-0.2f, DefaultPolicies.HuntingRights.Name);
    }
    if (kingdom.ActivePolicies.Contains(DefaultPolicies.DebasementOfTheCurrency))
    {
        explainedNumber.Add(-1f, DefaultPolicies.DebasementOfTheCurrency.Name);
    }
}

private void GetSettlementLoyaltyChangeDueToLoyaltyDrift(Town town, ref ExplainedNumber explainedNumber)
{
    explainedNumber.Add(-0.1f * (town.Loyalty - (float)LoyaltyDriftMedium), LoyaltyDriftText);
}
```

TAOM override:

```csharp
public override ExplainedNumber CalculateLoyaltyChange(Town town, bool includeDescriptions = false)
{
    var result = base.CalculateLoyaltyChange(town, includeDescriptions);
    _feats.ApplyLoyaltyFeats(CultureFeatAdapter.FromOrNull(town.Owner?.Culture), ref result);
    return result;
}
```

Verdict: a negative culture loyalty `Add` is summed into the daily loyalty-change `ExplainedNumber`. Vanilla already uses negative `Add` values for loyalty. The vanilla drift term is stabilizing, not runaway: a standalone `-0.5/day` culture modifier shifts the drift equilibrium from 50 to 45.

## 2. Service Routing Audit

All 24 new `HasFeat(...)` checks are in the intended `Apply*` methods:

| Feat | Service method | Live model caller |
|---|---|---|
| `taom_mordor_smithing` | `ApplySmithingFeats` | `TaomSmithingModel` |
| `taom_erebor_tariff_income` | `ApplyTariffIncomeFeats` | `TaomClanFinanceModel` |
| `taom_umbar_raid_damage` | `ApplyRaidDamageFeats` | `TaomRaidModel` |
| `taom_umbar_food_consumption` | `ApplyFoodConsumptionFeats` | `TaomFoodConsumptionModel` |
| `taom_lothlorien_volunteer_rate` | `ApplyVolunteerRespawnFeats` | `TaomVolunteerModel` |
| `taom_mirkwood_army_influence_cost` | `ApplyArmyInfluenceCost` | `TaomArmyManagementModel` |
| `taom_goblin_smithing` | `ApplySmithingFeats` | `TaomSmithingModel` |
| `taom_goblin_raid_damage` | `ApplyRaidDamageFeats` | `TaomRaidModel` |
| `taom_mistymountainorcs_smithing` | `ApplySmithingFeats` | `TaomSmithingModel` |
| `taom_mistymountainorcs_raid_damage` | `ApplyRaidDamageFeats` | `TaomRaidModel` |
| `taom_mistymountainorcs_construction_speed` | `ApplyConstructionSpeedFeats` | `TaomBuildingConstructionModel` |
| `taom_dale_tariff_income` | `ApplyTariffIncomeFeats` | `TaomClanFinanceModel` |
| `taom_dale_renown` | `ApplyRenownFeats` | `TaomBattleRewardModel` |
| `taom_dale_loyalty` | `ApplyLoyaltyFeats` | `TaomSettlementLoyaltyModel` |
| `taom_khand_renown` | `ApplyRenownFeats` | `TaomBattleRewardModel` |
| `taom_khand_tariff_income` | `ApplyTariffIncomeFeats` | `TaomClanFinanceModel` |
| `taom_khand_food_consumption` | `ApplyFoodConsumptionFeats` | `TaomFoodConsumptionModel` |
| `taom_khand_party_size` | `ApplyPartySizeFeats` | `TaomPartySizeModel` |
| `taom_harad_morale` | `ApplyMoraleFeats` | `TaomPartyMoraleModel` |
| `taom_harad_food_consumption` | `ApplyFoodConsumptionFeats` | `TaomFoodConsumptionModel` |
| `taom_harad_raid_damage` | `ApplyRaidDamageFeats` | `TaomRaidModel` |
| `taom_harad_army_influence_cost` | `ApplyArmyInfluenceCost` | `TaomArmyManagementModel` |
| `taom_rhun_loyalty` | `ApplyLoyaltyFeats` | `TaomSettlementLoyaltyModel` |
| `taom_rhun_raid_damage` | `ApplyRaidDamageFeats` | `TaomRaidModel` |

No dead `Apply*` method found for the new feats.

## 3. Balance Pass

No balance-blocking findings.

The two `-0.5/day` loyalty penalties are mechanically valid and do not create a standalone revolt spiral. Vanilla loyalty drift applies `-0.1 * (Loyalty - 50)`, so a constant `-0.5` culture penalty stabilizes around 45 loyalty before other factors. It can compound with starvation, low security, wrong-culture ownership, and hostile policies, but that is normal loyalty-model composition rather than runaway behavior.

Harad's stack is strong but bounded: `+5 morale`, `-15% food`, `+15% raid`, `+5% party size`, `+10% desert speed`, plus inherited Aserai economy/desert traits, offset by `+15% army influence cost` and inherited wage/trade penalties. Misty Mountain Orcs and Goblin stacks remain horde-flavored but retain food/construction penalties. No culture received an unintended duplicate axis from Wave 1.

## 4. CONFIG CROSS-REFERENCE

Mechanical checks performed:

- All 24 `Register("taom_*")` ids appear exactly once in `TaomCulturalFeats.cs`.
- All 24 ids appear exactly once in a culture attachment surface.
- Custom-culture XML context:
  - `mordor`, `erebor`, `umbar`, `lothlorien`, `mirkwood`, `goblin`, and `mistymountainorcs` attachments are under the expected `taom_spcultures.xml` culture ids.
- XSLT culture context:
  - Dale feats are under `Culture[@id='sturgia']/cultural_feats`.
  - Khand feats are under `Culture[@id='battania']/cultural_feats`.
  - Harad feats are under `Culture[@id='aserai']/cultural_feats`.
  - Rhun feats are under `Culture[@id='khuzait']/cultural_feats`.
- XSLT append templates copy existing feat nodes with `<xsl:apply-templates select="@*|node()"/>` before new `<feat/>` nodes at `spcultures.xslt:1347`, `1359`, `1369`, and `1379`.
- Parent templates do not exclude `cultural_feats`:
  - `aserai`: `spcultures.xslt:579`
  - `khuzait`: `spcultures.xslt:1138`
  - `sturgia`: `spcultures.xslt:1248`
  - `battania`: `spcultures.xslt:1341`
- `factions.json` localized keys:
  - 739 unique `taom_faction_*` keys in `factions.json`.
  - 0 missing ids in `taom_module_strings.xml`.
  - 0 U+2212 minus signs in `factions.json`.

## 5. Known Suspects

1. Sign/flag correctness for cost-reduction-as-positive feats - CONFIRMED CLEAN. Existing Erebor smithing uses `-0.3f, isPositiveEffect: true, AddFactor` at `TaomCulturalFeats.cs:534-537`. New smithing and food reductions follow that convention: negative `EffectBonus`, `isPositiveEffect: true`, `AddFactor`.

2. Army influence cost penalty direction - CONFIRMED CLEAN. `ApplyArmyInfluenceCost` accumulates `multiplier` and returns `(int)(baseCost * (1f + multiplier))` at `CulturalFeatsService.cs:55-76`. `+0.15f` for Mirkwood/Harad yields `115` from base `100`, so it is a penalty, not a discount.

3. Negative Add loyalty feats - CONFIRMED MECHANICALLY CLEAN; DISPUTED as catastrophic balance bug. `ApplyLoyaltyFeats` uses `result.Add(...)` at `CulturalFeatsService.cs:391-408`; vanilla `ExplainedNumber.Add` supports negative values; vanilla loyalty drift prevents a standalone spiral. Balance note: a constant `-0.5/day` shifts equilibrium by about 5 loyalty points.

4. XSLT culture feat attachment - CONFIRMED CLEAN. Each XSLT `cultural_feats` template copies `@*|node()` before appending new feats, and each parent culture template passes child nodes through without excluding `cultural_feats`.

5. Register-id to XML-id exact match - CONFIRMED CLEAN. All 24 ids have `register=1`, `attach=1`, and correct culture context in the mechanical cross-check.

6. `factions.json` U+2212 regression - CONFIRMED CLEAN. Full-file U+2212 count is 0; new negative lines use ASCII `-`.

7. Axis collision - CONFIRMED CLEAN. No new feat duplicates an existing axis within its culture. Notable designed stacks remain inherited-vanilla plus TAOM overlays, such as Harad desert-speed plus Aserai desert trait.

## Additional Findings

### MEDIUM

[MEDIUM] TAOM.Tests/Features/CulturalFeats/TaomCulturalFeatsDefinitionTests.cs:242 - Test coverage - `RegisterAll_UsesCorrectStringIds` does not verify string ids; it only counts private `FeatObject` fields. The Wave 1 service tests also inject fake `FeatObject` instances from a mirrored table in `CulturalFeatsServiceTests.cs:1316-1460`, so production `Register(...)`, `InitializeAll()` `EffectBonus`, `IsPositive`, `AdditionType`, and `GetAllFeats()` inclusion for the 24 new feats are not pinned by tests. A production metadata typo would be caught by this review's manual cross-check, but not by the current tests. Fix: add a metadata test for the 24 Wave 1 tuples, preferably by parsing `TaomCulturalFeats.cs` or by a test-only initializer that asserts production string id, effect bonus, `IsPositive`, `AdditionType`, and one XML/XSLT attachment.

### LOW

[LOW] docs/features/cultural-feats.md:59 - Documentation completeness - The detailed feat table still omits all 24 Wave 1 feat ids even though the intro says the total is 129 at `docs/features/cultural-feats.md:7`; the test matrix also still says "Feat property count (97)" at `docs/features/cultural-feats.md:239`. This makes the feature doc incomplete for future reviewers/tuners. Fix: add the 24 Wave 1 rows and update the test-matrix count.

[LOW] Main/_Module/ModuleData/spcultures.xslt:1345 - Comment accuracy - The XSLT append section is still labeled "terrain movement-speed feats" even though it now appends tariff, renown, loyalty, food, raid, morale, party-size, and army-cost feats. The implementation is correct, but the comment now understates the purpose of the block. Fix: rename the comment to describe all TAOM cultural-feat appends for vanilla-wrapped cultures.

## Verification Notes

Attempted:

```powershell
dotnet test TAOM.Tests --filter CulturalFeats --no-restore
```

Result: not completed. The first run failed during .NET first-run setup under the sandbox profile. A second run with `DOTNET_CLI_HOME` moved into the workspace got past that, but MSBuild failed before compilation because it attempted to access `C:\Users\mikew\AppData\Local\Microsoft SDKs`, which is outside the sandbox.

## Summary

CRITICAL: 0 | HIGH: 0 | MEDIUM: 1 | LOW: 2

VERDICT: ISSUES FOUND
