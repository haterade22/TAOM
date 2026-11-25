using TaleWorlds.CampaignSystem;
using TaleWorlds.Localization;
using TAOM.Features.AiPartySize;

namespace TAOM.Features.FactionPower;

/// <summary>
/// The faction power levers on plain facts. The caller asks <see cref="Multiplier"/> first and returns at 1, so a campaign at
/// 100 percent never pays for the exclusion facts. The whole player clan is excluded (every human player's clan in co-op),
/// the main party included: faction power is a per-realm AI strength knob.
/// </summary>
public sealed class FactionPowerService
{
    private static readonly TextObject PowerText = new("{=taom_fp_realm_power}Realm power");
    private static readonly TextObject WageText = new("{=taom_fp_realm_wage_relief}Realm power wage relief");

    private readonly IFactionPowerSettings _settings;

    public FactionPowerService(IFactionPowerSettings settings)
    {
        _settings = settings;
    }

    public float Multiplier(string? realmKey) => _settings.Multiplier(realmKey);

    public void ScaleLordLimit(
        float multiplier, bool isMainParty, bool isLordParty, bool hasLeaderHero, bool isPlayerClan, ref ExplainedNumber limit)
    {
        if (multiplier == 1f || !AiPartySizeService.IsScalableAiLordParty(isMainParty, isLordParty, hasLeaderHero, isPlayerClan))
            return;
        FactionPowerMath.MultiplyResult(ref limit, multiplier, PowerText);
    }

    public void ScaleGarrisonLimit(float multiplier, bool isPlayerClan, ref ExplainedNumber limit)
    {
        if (multiplier == 1f || isPlayerClan)
            return;
        FactionPowerMath.MultiplyResult(ref limit, multiplier, PowerText);
    }

    /// <summary>
    /// Lever 2b: scales the militia inflow of a fief that is not the player clan's, so its equilibrium scales by the multiplier.
    /// A looted or burning village gets no change at all from the base (no Retired row), so nothing is added there.
    /// </summary>
    public void ScaleMilitiaChange(float multiplier, bool isPlayerClan, bool isInactiveVillage, float militia, ref ExplainedNumber change)
    {
        if (multiplier == 1f || isPlayerClan || isInactiveVillage)
            return;
        var delta = FactionPowerMath.MilitiaDelta(change.ResultNumber, militia, change.SumOfFactors, multiplier);
        FactionPowerMath.AddResultFrameDelta(ref change, delta, PowerText);
    }

    /// <summary>Lever 4: scales one auto-resolve strike by the square root of the multiplier, never for the main party or the player clan.</summary>
    public void ScaleStrike(float multiplier, bool isMainParty, bool isPlayerClan, ref ExplainedNumber damage)
    {
        if (multiplier == 1f || isMainParty || isPlayerClan)
            return;
        FactionPowerMath.MultiplyResult(ref damage, FactionPowerMath.StrikeMultiplier(multiplier), PowerText);
    }

    /// <summary>Lever 5: above 100 percent, an AI lord party or garrison of the realm pays wages x 1/m. At or below 100 percent: no change.</summary>
    public void ScaleWage(
        float multiplier, bool isGarrison, bool isMainParty, bool isLordParty, bool hasLeaderHero, bool isPlayerClan, ref ExplainedNumber wage)
    {
        if (!(multiplier > 1f))
            return;
        var eligible = isGarrison
            ? !isPlayerClan
            : AiPartySizeService.IsScalableAiLordParty(isMainParty, isLordParty, hasLeaderHero, isPlayerClan);
        if (eligible)
            FactionPowerMath.MultiplyResult(ref wage, FactionPowerMath.WageMultiplier(multiplier), WageText);
    }

    /// <summary>Lever 2b: the militia a defended siege spawns, scaled and rounded; never for a player clan's fief.</summary>
    public int ScaleMilitiaAfterSiege(float multiplier, bool isPlayerClan, int militia)
        => multiplier == 1f || isPlayerClan ? militia : FactionPowerMath.ScaleCount(militia, multiplier);
}
