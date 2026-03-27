using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.GameComponents;
using TaleWorlds.CampaignSystem.Settlements;
using TaleWorlds.Core;
using TaleWorlds.Localization;

namespace TAOM.Features.CulturalFeats.Models;

public class TaomSettlementProsperityModel : DefaultSettlementProsperityModel
{
    private static readonly TextObject CultureText = GameTexts.FindText("str_culture");

    public override ExplainedNumber CalculateHearthChange(Village village, bool includeDescriptions = false)
    {
        var result = base.CalculateHearthChange(village, includeDescriptions);

        var culture = village.Settlement?.OwnerClan?.Culture;
        if (culture == null)
            return result;

        if (culture.HasFeat(TaomCulturalFeats.RivendellHearthGrowthFeat) && result.ResultNumber >= 0f)
            result.AddFactor(TaomCulturalFeats.RivendellHearthGrowthFeat.EffectBonus, CultureText);

        if (culture.HasFeat(TaomCulturalFeats.MirkwoodHearthGrowthFeat) && result.ResultNumber >= 0f)
            result.AddFactor(TaomCulturalFeats.MirkwoodHearthGrowthFeat.EffectBonus, CultureText);

        if (culture.HasFeat(TaomCulturalFeats.GondorHearthGrowthFeat) && result.ResultNumber >= 0f)
            result.AddFactor(TaomCulturalFeats.GondorHearthGrowthFeat.EffectBonus, CultureText);

        return result;
    }
}
