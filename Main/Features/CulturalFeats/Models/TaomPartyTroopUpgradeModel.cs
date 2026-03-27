using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.GameComponents;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.Core;
using TaleWorlds.Localization;

namespace TAOM.Features.CulturalFeats.Models;

public class TaomPartyTroopUpgradeModel : DefaultPartyTroopUpgradeModel
{
    private static readonly TextObject CultureText = GameTexts.FindText("str_culture");

    public override ExplainedNumber GetGoldCostForUpgrade(
        PartyBase party, CharacterObject characterObject, CharacterObject upgradeTarget)
    {
        var result = base.GetGoldCostForUpgrade(party, characterObject, upgradeTarget);

        var culture = party.Owner?.Culture ?? party.Culture;
        if (characterObject.IsMounted)
        {
            if (culture?.HasFeat(TaomCulturalFeats.IsengardCheaperRecruitsFeat) == true)
                result.AddFactor(TaomCulturalFeats.IsengardCheaperRecruitsFeat.EffectBonus, CultureText);

            if (culture?.HasFeat(TaomCulturalFeats.RohanMountedCostFeat) == true)
                result.AddFactor(TaomCulturalFeats.RohanMountedCostFeat.EffectBonus, CultureText);
        }

        return result;
    }
}
