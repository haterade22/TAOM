using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.GameComponents;
using TaleWorlds.CampaignSystem.Party;
using TAOM.Features.CareerSystem.Domain;

namespace TAOM.Features.CareerSystem.Models;

public class TaomMapVisibilityModel : DefaultMapVisibilityModel
{
    public override ExplainedNumber GetPartySpottingRange(MobileParty party, bool includeDescriptions = false)
    {
        var result = base.GetPartySpottingRange(party, includeDescriptions);

        var hero = party?.LeaderHero;
        if (hero != null)
            CareerPassiveHelper.ApplyFactor(hero, ref result, PassiveEffectType.PartySpottingRange);

        return result;
    }
}
