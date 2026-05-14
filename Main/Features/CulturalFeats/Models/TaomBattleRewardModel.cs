using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.GameComponents;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.Core;
using TAOM.Features.CareerSystem;
using TAOM.Features.CareerSystem.Domain;

namespace TAOM.Features.CulturalFeats.Models;

public class TaomBattleRewardModel : DefaultBattleRewardModel
{
    private readonly ICulturalFeatsService _feats;
    private readonly ICareerPassiveService _careerPassives;

    public TaomBattleRewardModel(ICulturalFeatsService feats, ICareerPassiveService careerPassives)
    {
        _feats = feats;
        _careerPassives = careerPassives;
    }

    public override ExplainedNumber CalculateRenownGain(
        PartyBase party, float renownValueOfBattle, float contributionShare)
    {
        var result = base.CalculateRenownGain(party, renownValueOfBattle, contributionShare);
        _feats.ApplyRenownFeats(CultureFeatAdapter.FromOrNull(party.Owner?.Culture ?? party.Culture), ref result);
        _careerPassives.ApplyFactor((party.Owner ?? party.LeaderHero)?.StringId, ref result, PassiveEffectType.BattleRenownGain);
        return result;
    }
}
