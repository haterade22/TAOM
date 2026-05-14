using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.GameComponents;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.Core;
using TAOM.Features.CareerSystem;
using TAOM.Features.CareerSystem.Domain;

namespace TAOM.Features.CulturalFeats.Models;

public class TaomPartyMoraleModel : DefaultPartyMoraleModel
{
    private readonly ICulturalFeatsService _feats;
    private readonly ICareerPassiveService _careerPassives;

    public TaomPartyMoraleModel(ICulturalFeatsService feats, ICareerPassiveService careerPassives)
    {
        _feats = feats;
        _careerPassives = careerPassives;
    }

    public override ExplainedNumber GetEffectivePartyMorale(
        MobileParty party, bool includeDescription = false)
    {
        var result = base.GetEffectivePartyMorale(party, includeDescription);
        _feats.ApplyMoraleFeats(CultureFeatAdapter.FromOrNull(party.Party?.Owner?.Culture), ref result);
        _careerPassives.ApplyFactor(party.LeaderHero?.StringId, ref result, PassiveEffectType.TroopMorale);
        return result;
    }
}
