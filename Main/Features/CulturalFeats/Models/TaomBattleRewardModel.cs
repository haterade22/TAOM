using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.GameComponents;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.Core;
using TaleWorlds.Localization;

namespace TAOM.Features.CulturalFeats.Models;

public class TaomBattleRewardModel : DefaultBattleRewardModel
{
    private static TextObject CultureText => GameTexts.FindText("str_culture");

    public override ExplainedNumber CalculateRenownGain(
        PartyBase party, float renownValueOfBattle, float contributionShare)
    {
        var result = base.CalculateRenownGain(party, renownValueOfBattle, contributionShare);

        var culture = party.Owner?.Culture ?? party.Culture;
        if (culture?.HasFeat(TaomCulturalFeats.UmbarRenownFeat) == true)
            result.AddFactor(TaomCulturalFeats.UmbarRenownFeat.EffectBonus, CultureText);

        return result;
    }
}
