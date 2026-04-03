using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.GameComponents;
using TaleWorlds.CampaignSystem.Settlements;
using TaleWorlds.Core;
using TaleWorlds.Localization;

namespace TAOM.Features.CulturalFeats.Models;

public class TaomClanFinanceModel : DefaultClanFinanceModel
{
    private static TextObject CultureText => GameTexts.FindText("str_culture");

    public override ExplainedNumber CalculateTownIncomeFromTariffs(
        Clan clan, Town town, bool applyWithdrawals = false)
    {
        var result = base.CalculateTownIncomeFromTariffs(clan, town, applyWithdrawals);

        var culture = clan?.Culture;
        if (culture == null)
            return result;

        if (culture.HasFeat(TaomCulturalFeats.UmbarTariffIncomeFeat))
            result.AddFactor(TaomCulturalFeats.UmbarTariffIncomeFeat.EffectBonus, CultureText);

        return result;
    }
}
