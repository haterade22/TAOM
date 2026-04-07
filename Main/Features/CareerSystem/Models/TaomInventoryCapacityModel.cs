using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.GameComponents;
using TaleWorlds.CampaignSystem.Party;
using TAOM.Features.CareerSystem.Domain;

namespace TAOM.Features.CareerSystem.Models;

public class TaomInventoryCapacityModel : DefaultInventoryCapacityModel
{
    public override ExplainedNumber CalculateInventoryCapacity(
        MobileParty mobileParty, bool isCurrentlyAtSea, bool includeDescriptions = false,
        int additionalTroops = 0, int additionalSpareMounts = 0,
        int additionalPackAnimals = 0, bool includeFollowers = false)
    {
        var result = base.CalculateInventoryCapacity(
            mobileParty, isCurrentlyAtSea, includeDescriptions,
            additionalTroops, additionalSpareMounts, additionalPackAnimals, includeFollowers);

        var hero = mobileParty?.LeaderHero;
        if (hero != null)
            CareerPassiveHelper.ApplyFactor(hero, ref result, PassiveEffectType.InventoryCapacity);

        return result;
    }
}
