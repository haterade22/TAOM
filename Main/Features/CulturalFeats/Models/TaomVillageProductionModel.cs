using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.GameComponents;
using TaleWorlds.CampaignSystem.Settlements;
using TaleWorlds.Core;

namespace TAOM.Features.CulturalFeats.Models;

public class TaomVillageProductionModel : DefaultVillageProductionCalculatorModel
{
    private readonly ICulturalFeatsService _feats;

    public TaomVillageProductionModel(ICulturalFeatsService feats)
    {
        _feats = feats;
    }

    public override ExplainedNumber CalculateDailyProductionAmount(Village village, ItemObject item)
    {
        var result = base.CalculateDailyProductionAmount(village, item);
        _feats.ApplyVillageProductionFeats(
            CultureFeatAdapter.FromOrNull(village.Settlement?.OwnerClan?.Culture),
            isGrain: item.ItemCategory == DefaultItemCategories.Grain,
            ref result);
        return result;
    }
}
