using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.GameComponents;
using TaleWorlds.CampaignSystem.Settlements;
using TaleWorlds.Core;
using TaleWorlds.Localization;

namespace TAOM.Features.CulturalFeats.Models;

public class TaomVillageProductionModel : DefaultVillageProductionCalculatorModel
{
    private static TextObject? _cultureText;
    private static TextObject CultureText => _cultureText ??= GameTexts.FindText("str_culture");

    public override ExplainedNumber CalculateDailyProductionAmount(Village village, ItemObject item)
    {
        var result = base.CalculateDailyProductionAmount(village, item);

        var culture = village.Settlement?.OwnerClan?.Culture;
        if (culture == null)
            return result;

        if (culture.HasFeat(TaomCulturalFeats.EreborProductionFeat))
            result.AddFactor(TaomCulturalFeats.EreborProductionFeat.EffectBonus, CultureText);

        bool isGrain = item.ItemCategory == DefaultItemCategories.Grain;

        if (isGrain && culture.HasFeat(TaomCulturalFeats.GundabadGrainProductionFeat))
            result.AddFactor(TaomCulturalFeats.GundabadGrainProductionFeat.EffectBonus, CultureText);

        if (isGrain && culture.HasFeat(TaomCulturalFeats.MordorGrainProductionFeat))
            result.AddFactor(TaomCulturalFeats.MordorGrainProductionFeat.EffectBonus, CultureText);

        return result;
    }
}
