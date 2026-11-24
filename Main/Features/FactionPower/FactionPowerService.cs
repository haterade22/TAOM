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
}
