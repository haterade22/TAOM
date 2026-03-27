using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.GameComponents;
using TaleWorlds.CampaignSystem.Settlements;

namespace TAOM.Features.CulturalFeats.Models;

public class TaomSettlementMilitiaModel : DefaultSettlementMilitiaModel
{
    public override ExplainedNumber CalculateVeteranMilitiaSpawnChance(Settlement settlement)
    {
        var result = base.CalculateVeteranMilitiaSpawnChance(settlement);

        var culture = settlement.OwnerClan?.Culture;
        if (culture == null)
            return result;

        if (culture.HasFeat(TaomCulturalFeats.MirkwoodMilitiaProductionFeat))
            result.Add(TaomCulturalFeats.MirkwoodMilitiaProductionFeat.EffectBonus);

        if (culture.HasFeat(TaomCulturalFeats.DolGuldurMilitiaProductionFeat))
            result.Add(TaomCulturalFeats.DolGuldurMilitiaProductionFeat.EffectBonus);

        return result;
    }
}
