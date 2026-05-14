using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.GameComponents;
using TaleWorlds.CampaignSystem.Settlements;
using TaleWorlds.Core;

namespace TAOM.Features.CulturalFeats.Models;

public class TaomSettlementProsperityModel : DefaultSettlementProsperityModel
{
    private readonly ICulturalFeatsService _feats;

    public TaomSettlementProsperityModel(ICulturalFeatsService feats)
    {
        _feats = feats;
    }

    public override ExplainedNumber CalculateHearthChange(Village village, bool includeDescriptions = false)
    {
        var result = base.CalculateHearthChange(village, includeDescriptions);
        _feats.ApplyHearthGrowthFeats(CultureFeatAdapter.FromOrNull(village.Settlement?.OwnerClan?.Culture), ref result);
        return result;
    }
}
