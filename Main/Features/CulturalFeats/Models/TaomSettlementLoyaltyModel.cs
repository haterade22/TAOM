using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.GameComponents;
using TaleWorlds.CampaignSystem.Settlements;
using TaleWorlds.Core;
using TaleWorlds.Localization;

namespace TAOM.Features.CulturalFeats.Models;

public class TaomSettlementLoyaltyModel : DefaultSettlementLoyaltyModel
{
    private static TextObject CultureText => GameTexts.FindText("str_culture");

    public override ExplainedNumber CalculateLoyaltyChange(Town town, bool includeDescriptions = false)
    {
        var result = base.CalculateLoyaltyChange(town, includeDescriptions);

        var culture = town.Owner?.Culture;
        if (culture == null)
            return result;

        if (culture.HasFeat(TaomCulturalFeats.GondorLoyaltyFeat))
            result.Add(TaomCulturalFeats.GondorLoyaltyFeat.EffectBonus, CultureText);

        if (culture.HasFeat(TaomCulturalFeats.EreborLoyaltyFeat))
            result.Add(TaomCulturalFeats.EreborLoyaltyFeat.EffectBonus, CultureText);

        if (culture.HasFeat(TaomCulturalFeats.LothlorienLoyaltyFeat))
            result.Add(TaomCulturalFeats.LothlorienLoyaltyFeat.EffectBonus, CultureText);

        if (culture.HasFeat(TaomCulturalFeats.RivendellLoyaltyFeat))
            result.Add(TaomCulturalFeats.RivendellLoyaltyFeat.EffectBonus, CultureText);

        if (culture.HasFeat(TaomCulturalFeats.RohanLoyaltyFeat))
            result.Add(TaomCulturalFeats.RohanLoyaltyFeat.EffectBonus, CultureText);

        return result;
    }
}
