using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.GameComponents;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.Core;
using TaleWorlds.Localization;
using TAOM.Features.CareerSystem;
using TAOM.Features.CareerSystem.Domain;

namespace TAOM.Features.CulturalFeats.Models;

public class TaomPartyTroopUpgradeModel : DefaultPartyTroopUpgradeModel
{
    private static TextObject? _cultureText;
    private static TextObject CultureText => _cultureText ??= GameTexts.FindText("str_culture");

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

        var hero = party.Owner ?? party.LeaderHero;
        if (hero != null)
            CareerPassiveHelper.ApplyFactor(hero, ref result, PassiveEffectType.TroopUpgradeCost);

        return result;
    }
}
