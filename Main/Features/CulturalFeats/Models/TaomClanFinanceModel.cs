using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.GameComponents;
using TaleWorlds.CampaignSystem.Settlements;
using TaleWorlds.Core;

namespace TAOM.Features.CulturalFeats.Models;

public class TaomClanFinanceModel : DefaultClanFinanceModel
{
    private readonly ICulturalFeatsService _feats;

    public TaomClanFinanceModel(ICulturalFeatsService feats)
    {
        _feats = feats;
    }

    public override ExplainedNumber CalculateTownIncomeFromTariffs(
        Clan clan, Town town, bool applyWithdrawals = false)
    {
        var result = base.CalculateTownIncomeFromTariffs(clan, town, applyWithdrawals);
        _feats.ApplyTariffIncomeFeats(CultureFeatAdapter.FromOrNull(clan?.Culture), ref result);
        return result;
    }
}
