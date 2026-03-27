using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.GameComponents;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.Core;
using TaleWorlds.Localization;

namespace TAOM.Features.CulturalFeats.Models;

public class TaomFoodConsumptionModel : DefaultMobilePartyFoodConsumptionModel
{
    private static readonly TextObject CultureText = GameTexts.FindText("str_culture");

    public override ExplainedNumber CalculateDailyFoodConsumptionf(
        MobileParty party, ExplainedNumber baseConsumption)
    {
        var result = base.CalculateDailyFoodConsumptionf(party, baseConsumption);

        var culture = party.Party?.Owner?.Culture;
        if (culture == null)
            return result;

        if (culture.HasFeat(TaomCulturalFeats.RivendellFoodConsumptionFeat))
            result.AddFactor(TaomCulturalFeats.RivendellFoodConsumptionFeat.EffectBonus, CultureText);

        if (culture.HasFeat(TaomCulturalFeats.MirkwoodFoodConsumptionFeat))
            result.AddFactor(TaomCulturalFeats.MirkwoodFoodConsumptionFeat.EffectBonus, CultureText);

        if (culture.HasFeat(TaomCulturalFeats.LothlorienFoodConsumptionFeat))
            result.AddFactor(TaomCulturalFeats.LothlorienFoodConsumptionFeat.EffectBonus, CultureText);

        if (culture.HasFeat(TaomCulturalFeats.DolGuldurFoodConsumptionFeat))
            result.AddFactor(TaomCulturalFeats.DolGuldurFoodConsumptionFeat.EffectBonus, CultureText);

        return result;
    }
}
