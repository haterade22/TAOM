using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.GameComponents;
using TaleWorlds.Core;
using TAOM.Features.CareerSystem;
using TAOM.Features.CareerSystem.Domain;

namespace TAOM.Features.CulturalFeats.Models;

public class TaomSmithingModel : DefaultSmithingModel
{
    private readonly ICulturalFeatsService _feats;
    private readonly ICareerPassiveService _careerPassives;

    public TaomSmithingModel(ICulturalFeatsService feats, ICareerPassiveService careerPassives)
    {
        _feats = feats;
        _careerPassives = careerPassives;
    }

    public override int GetEnergyCostForSmithing(ItemObject item, Hero hero)
        => ApplyFeatReduction(base.GetEnergyCostForSmithing(item, hero), hero);

    public override int GetEnergyCostForSmelting(ItemObject item, Hero hero)
        => ApplyFeatReduction(base.GetEnergyCostForSmelting(item, hero), hero);

    public override int GetEnergyCostForRefining(ref Crafting.RefiningFormula refineFormula, Hero hero)
        => ApplyFeatReduction(base.GetEnergyCostForRefining(ref refineFormula, hero), hero);

    /// <summary>
    /// Boundary helper — single shared <see cref="ExplainedNumber"/> + cast composes
    /// the culture feat factor and the career passive factor without the double
    /// truncation bug fixed in Phase 9b #173 F4.
    /// </summary>
    private int ApplyFeatReduction(int baseCost, Hero hero)
    {
        var result = new ExplainedNumber(baseCost, false);
        _feats.ApplySmithingFeats(CultureFeatAdapter.FromOrNull(hero?.Culture), ref result);
        _careerPassives.ApplyFactor(hero?.StringId, ref result, PassiveEffectType.SmithingCostReduction);
        return (int)result.ResultNumber;
    }
}
