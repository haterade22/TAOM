using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.GameComponents;
using TaleWorlds.Core;
using TAOM.Features.CareerSystem;
using TAOM.Features.CareerSystem.Domain;

namespace TAOM.Features.CulturalFeats.Models;

public class TaomSmithingModel : DefaultSmithingModel
{
    public override int GetEnergyCostForSmithing(ItemObject item, Hero hero)
    {
        var baseCost = base.GetEnergyCostForSmithing(item, hero);
        return ApplySmithingFeatReduction(baseCost, hero);
    }

    public override int GetEnergyCostForSmelting(ItemObject item, Hero hero)
    {
        var baseCost = base.GetEnergyCostForSmelting(item, hero);
        return ApplySmithingFeatReduction(baseCost, hero);
    }

    public override int GetEnergyCostForRefining(ref Crafting.RefiningFormula refineFormula, Hero hero)
    {
        var baseCost = base.GetEnergyCostForRefining(ref refineFormula, hero);
        return ApplySmithingFeatReduction(baseCost, hero);
    }

    private static int ApplySmithingFeatReduction(int baseCost, Hero hero)
    {
        var culture = hero?.Culture;
        if (culture == null)
            return baseCost;

        float factor = 0f;

        if (culture.HasFeat(TaomCulturalFeats.EreborSmithingFeat))
            factor += TaomCulturalFeats.EreborSmithingFeat.EffectBonus;

        if (culture.HasFeat(TaomCulturalFeats.IsengardSmithingFeat))
            factor += TaomCulturalFeats.IsengardSmithingFeat.EffectBonus;

        if (factor == 0f && hero == null)
            return baseCost;

        int featResult = factor != 0f ? (int)(baseCost * (1f + factor)) : baseCost;

        if (hero != null)
        {
            var explained = new ExplainedNumber(featResult, false);
            CareerPassiveHelper.ApplyFactor(hero, ref explained, PassiveEffectType.EnchantmentCostReduction);
            return (int)explained.ResultNumber;
        }

        return featResult;
    }
}
