using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.GameComponents;
using TaleWorlds.CampaignSystem.Party;
using TAOM.Features.CareerSystem.Domain;

namespace TAOM.Features.CareerSystem.Models;

public class TaomInventoryCapacityModel : DefaultInventoryCapacityModel
{
    private readonly ICareerPassiveService _careerPassives;

    public TaomInventoryCapacityModel(ICareerPassiveService careerPassives)
    {
        _careerPassives = careerPassives;
    }

    public override ExplainedNumber CalculateInventoryCapacity(
        MobileParty mobileParty, bool isCurrentlyAtSea, bool includeDescriptions = false,
        int additionalTroops = 0, int additionalSpareMounts = 0,
        int additionalPackAnimals = 0, bool includeFollowers = false)
    {
        var result = base.CalculateInventoryCapacity(
            mobileParty, isCurrentlyAtSea, includeDescriptions,
            additionalTroops, additionalSpareMounts, additionalPackAnimals, includeFollowers);

        _careerPassives.ApplyFactor(mobileParty?.LeaderHero?.StringId, ref result, PassiveEffectType.InventoryCapacity);
        return result;
    }
}
