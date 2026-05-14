using TaleWorlds.CampaignSystem;
using TaleWorlds.Localization;

namespace TAOM.Features.TroopProgression;

/// <summary>
/// Concrete <see cref="IWageModifierService"/>. Pure — no TaleWorlds sealed-type
/// references in method bodies (only primitives + <c>ExplainedNumber</c>/<c>TextObject</c>
/// from <c>TaleWorlds.Core</c> which are open structs/types). Fully unit-testable
/// without a campaign session.
/// </summary>
public class WageModifierService : IWageModifierService
{
    private const int HighLevelHorseCost = 500;
    private const int LowLevelHorseCost = 150;
    private const int HighLevelHorseThreshold = 26;
    private readonly ITroopCostService _costService;

    public WageModifierService(ITroopCostService costService)
    {
        _costService = costService;
    }

    public void ApplyWageModifiers(
        ref ExplainedNumber result,
        in WageFeatInputs garrison,
        in WageFeatInputs party,
        float rohanMountedWageBonus,
        float mountedWageShare,
        TextObject? cultureText)
    {
        if (garrison.IsApplicable)
        {
            AddNonZero(ref result, garrison.EreborGarrisonBonus, cultureText);
            AddNonZero(ref result, garrison.LothlorienGarrisonBonus, cultureText);
            AddNonZero(ref result, garrison.IsengardGarrisonBonus, cultureText);
            AddNonZero(ref result, garrison.GondorGarrisonBonus, cultureText);
        }

        if (party.IsApplicable)
        {
            AddNonZero(ref result, party.GundabadWageBonus, cultureText);
            AddNonZero(ref result, party.UmbarWageBonus, cultureText);
            AddNonZero(ref result, party.MordorWageBonus, cultureText);

            // Rohan mounted-wage feat — scaled by mounted wage share (matches vanilla pattern).
            // Bonus is signed (negative for reduction). Skip cleanly if either side is zero.
            if (rohanMountedWageBonus != 0f && mountedWageShare > 0f)
                result.AddFactor(rohanMountedWageBonus * mountedWageShare, cultureText);
        }
    }

    public int CalculateRecruitmentCost(
        int level,
        bool isMounted,
        bool isMercenary,
        bool withoutItemCost,
        in MountedCostFeatInputs mountedCostFeats,
        TextObject? cultureText)
    {
        int baseCost = _costService.GetTroopRecruitmentCost(level, isMercenary);
        var result = new ExplainedNumber(baseCost, includeDescriptions: false);

        if (!withoutItemCost && isMounted)
            result.Add(CalculateHorseCost(level), null);

        if (isMounted)
        {
            AddNonZero(ref result, mountedCostFeats.IsengardMountedCostBonus, cultureText);
            AddNonZero(ref result, mountedCostFeats.RohanMountedCostBonus, cultureText);
        }

        return (int)result.ResultNumber;
    }

    public int CalculateHorseCost(int troopLevel)
        => troopLevel >= HighLevelHorseThreshold ? HighLevelHorseCost : LowLevelHorseCost;

    private static void AddNonZero(ref ExplainedNumber result, float bonus, TextObject? cultureText)
    {
        if (bonus != 0f)
            result.AddFactor(bonus, cultureText);
    }
}
