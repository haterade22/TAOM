using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.GameComponents;
using TaleWorlds.Core;
using TAOM.Features.CareerSystem;
using TAOM.Features.CareerSystem.Domain;

namespace TAOM.Features.CulturalFeats.Models;

public class TaomSmithingModel : DefaultSmithingModel
{
    private readonly ICareerPassiveService _careerPassives;

    public TaomSmithingModel(ICareerPassiveService careerPassives)
    {
        _careerPassives = careerPassives;
    }

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

    private int ApplySmithingFeatReduction(int baseCost, Hero hero)
    {
        // Phase 9b #173 F4 — composed math BEFORE the int cast. Pre-fix the culture feat factor
        // was applied as `(int)(baseCost * (1f + factor))` (truncating to int), THEN the career
        // passive was applied via a NEW ExplainedNumber starting from the truncated value. Two
        // distinct truncations + composition stages compounded rounding error vs the
        // "single-shot ExplainedNumber.AddFactor + final cast" path. Now: single ExplainedNumber,
        // both feat + career applied as factors, final cast once at the end.
        var culture = hero?.Culture;
        var result = new ExplainedNumber(baseCost, false);

        if (culture != null)
        {
            if (culture.HasFeat(TaomCulturalFeats.EreborSmithingFeat))
                result.AddFactor(TaomCulturalFeats.EreborSmithingFeat.EffectBonus);

            if (culture.HasFeat(TaomCulturalFeats.IsengardSmithingFeat))
                result.AddFactor(TaomCulturalFeats.IsengardSmithingFeat.EffectBonus);
        }

        _careerPassives.ApplyFactor(hero?.StringId, ref result, PassiveEffectType.EnchantmentCostReduction);

        return (int)result.ResultNumber;
    }
}
