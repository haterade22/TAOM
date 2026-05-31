using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.GameComponents;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.Core;

namespace TAOM.Features.CulturalFeats.Models;

public class TaomFoodConsumptionModel : DefaultMobilePartyFoodConsumptionModel
{
    private readonly ICulturalFeatsService _feats;

    public TaomFoodConsumptionModel(ICulturalFeatsService feats)
    {
        _feats = feats;
    }

    public override ExplainedNumber CalculateDailyFoodConsumptionf(
        MobileParty party, ExplainedNumber baseConsumption)
    {
        var result = base.CalculateDailyFoodConsumptionf(party, baseConsumption);
        // Vanilla PartyBaseHelper.HasFeat precedence via the shared helper.
        _feats.ApplyFoodConsumptionFeats(CultureFeatAdapter.FromOrNull(party.Party), ref result);
        return result;
    }
}
