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
}
