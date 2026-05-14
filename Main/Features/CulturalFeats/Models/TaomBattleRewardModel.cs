using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.GameComponents;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.Core;
using TaleWorlds.Localization;
using TAOM.Features.CareerSystem;
using TAOM.Features.CareerSystem.Domain;

namespace TAOM.Features.CulturalFeats.Models;

public class TaomBattleRewardModel : DefaultBattleRewardModel
{
    private readonly ICareerPassiveService _careerPassives;
    private static TextObject? _cultureText;
    private static TextObject CultureText => _cultureText ??= GameTexts.FindText("str_culture");

    public TaomBattleRewardModel(ICareerPassiveService careerPassives)
    {
        _careerPassives = careerPassives;
    }

    public override ExplainedNumber CalculateRenownGain(
        PartyBase party, float renownValueOfBattle, float contributionShare)
    {
        var result = base.CalculateRenownGain(party, renownValueOfBattle, contributionShare);

        var culture = party.Owner?.Culture ?? party.Culture;
        if (culture?.HasFeat(TaomCulturalFeats.UmbarRenownFeat) == true)
            result.AddFactor(TaomCulturalFeats.UmbarRenownFeat.EffectBonus, CultureText);

        _careerPassives.ApplyFactor((party.Owner ?? party.LeaderHero)?.StringId, ref result, PassiveEffectType.BattleRenownGain);
        return result;
    }
}
