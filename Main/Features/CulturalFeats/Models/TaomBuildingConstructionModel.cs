using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.GameComponents;
using TaleWorlds.CampaignSystem.Settlements;
using TaleWorlds.CampaignSystem.Settlements.Buildings;
using TaleWorlds.Core;
using TaleWorlds.Localization;

namespace TAOM.Features.CulturalFeats.Models;

public class TaomBuildingConstructionModel : DefaultBuildingConstructionModel
{
    private static TextObject CultureText => GameTexts.FindText("str_culture");

    public override ExplainedNumber CalculateDailyConstructionPower(Town town, bool includeDescriptions = false)
    {
        var result = base.CalculateDailyConstructionPower(town, includeDescriptions);

        var culture = town.OwnerClan?.Culture;
        if (culture == null)
            return result;

        if (culture.HasFeat(TaomCulturalFeats.EreborConstructionSpeedFeat))
            result.AddFactor(TaomCulturalFeats.EreborConstructionSpeedFeat.EffectBonus, CultureText);

        if (culture.HasFeat(TaomCulturalFeats.LothlorienConstructionSpeedFeat))
            result.AddFactor(TaomCulturalFeats.LothlorienConstructionSpeedFeat.EffectBonus, CultureText);

        if (culture.HasFeat(TaomCulturalFeats.DolGuldurConstructionSpeedFeat))
            result.AddFactor(TaomCulturalFeats.DolGuldurConstructionSpeedFeat.EffectBonus, CultureText);

        if (culture.HasFeat(TaomCulturalFeats.IsengardConstructionSpeedFeat))
            result.AddFactor(TaomCulturalFeats.IsengardConstructionSpeedFeat.EffectBonus, CultureText);

        return result;
    }
}
