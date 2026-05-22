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
        PartyBase winnerParty,
        float renownValueOfBattleForWinnerSide,
        float contributionShareOfWinnerParty,
        float renownMultiplierForWinnerSide,
        bool includeDescriptions)
    {
        var result = base.CalculateRenownGain(
            winnerParty,
            renownValueOfBattleForWinnerSide,
            contributionShareOfWinnerParty,
            renownMultiplierForWinnerSide,
            includeDescriptions);
        _feats.ApplyRenownFeats(CultureFeatAdapter.FromOrNull(winnerParty.Owner?.Culture ?? winnerParty.Culture), ref result);
        _careerPassives.ApplyFactor((winnerParty.Owner ?? winnerParty.LeaderHero)?.StringId, ref result, PassiveEffectType.BattleRenownGain);
        return result;
    }
}
