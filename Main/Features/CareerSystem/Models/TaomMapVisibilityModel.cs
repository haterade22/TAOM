using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.GameComponents;
using TaleWorlds.CampaignSystem.Party;
using TAOM.Features.CareerSystem.Domain;

namespace TAOM.Features.CareerSystem.Models;

public class TaomMapVisibilityModel : DefaultMapVisibilityModel
{
    private readonly ICareerPassiveService _careerPassives;

    public TaomMapVisibilityModel(ICareerPassiveService careerPassives)
    {
        _careerPassives = careerPassives;
    }

    public override ExplainedNumber GetPartySpottingRange(MobileParty party, bool includeDescriptions = false)
    {
        var result = base.GetPartySpottingRange(party, includeDescriptions);
        _careerPassives.ApplyFactor(party?.LeaderHero?.StringId, ref result, PassiveEffectType.PartySpottingRange);
        return result;
    }

    // StealthBonus — how easily OTHERS spot this party. A LOWER ratio means the party must be
    // closer before it is seen, so a positive bonus reduces it (party is harder to detect). The
    // direction-inverting math lives in the testable CareerPassiveMath.ApplyStealthRatio. The
    // inverse direction (how far the party itself sees) is GetPartySpottingRange above.
    public override float GetPartySpottingRatioForMainPartySeeingRange(MobileParty party)
    {
        var ratio = base.GetPartySpottingRatioForMainPartySeeingRange(party);
        var bonus = _careerPassives.GetPassiveMagnitude(party?.LeaderHero?.StringId, PassiveEffectType.StealthBonus);
        return CareerPassiveMath.ApplyStealthRatio(ratio, bonus);
    }
}
