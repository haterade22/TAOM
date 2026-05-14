using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.GameComponents;
using TaleWorlds.CampaignSystem.Settlements;

namespace TAOM.Features.CulturalFeats.Models;

public class TaomSettlementMilitiaModel : DefaultSettlementMilitiaModel
{
    private readonly ICulturalFeatsService _feats;

    public TaomSettlementMilitiaModel(ICulturalFeatsService feats)
    {
        _feats = feats;
    }

    public override ExplainedNumber CalculateVeteranMilitiaSpawnChance(Settlement settlement)
    {
        var result = base.CalculateVeteranMilitiaSpawnChance(settlement);
        _feats.ApplyVeteranMilitiaFeats(CultureFeatAdapter.FromOrNull(settlement.OwnerClan?.Culture), ref result);
        return result;
    }
}
