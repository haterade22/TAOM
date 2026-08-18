using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.GameComponents;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.Core;
using TAOM.Features.AiPartySize;

namespace TAOM.Features.CulturalFeats.Models;

public class TaomFoodConsumptionModel : DefaultMobilePartyFoodConsumptionModel
{
    private readonly ICulturalFeatsService _feats;
    private readonly IAiPartySizeService _aiPartySize;

    public TaomFoodConsumptionModel(ICulturalFeatsService feats, IAiPartySizeService aiPartySize)
    {
        _feats = feats;
        _aiPartySize = aiPartySize;
    }

    public override ExplainedNumber CalculateDailyFoodConsumptionf(
        MobileParty party, ExplainedNumber baseConsumption)
    {
        var result = base.CalculateDailyFoodConsumptionf(party, baseConsumption);
        // Vanilla PartyBaseHelper.HasFeat precedence via the shared helper.
        _feats.ApplyFoodConsumptionFeats(CultureFeatAdapter.FromOrNull(party.Party), ref result);
        // AI food relief (#461). A party big enough to matter cannot resupply: it eats
        // members/20 per day and PartiesBuyFoodCampaignBehavior tries to buy 30 days of that in one
        // town visit, which no market stocks. It then starves permanently, and starvation is a flat
        // -30 morale that opens vanilla's morale-desertion path — which ignores the party size limit
        // entirely, so raising the cap alone would not have stopped the collapse.
        _aiPartySize.ApplyAiFoodRelief(party, ref result);
        return result;
    }
}
