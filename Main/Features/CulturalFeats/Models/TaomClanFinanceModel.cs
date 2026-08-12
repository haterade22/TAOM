using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.GameComponents;
using TaleWorlds.CampaignSystem.Settlements;
using TaleWorlds.Core;

namespace TAOM.Features.CulturalFeats.Models;

public class TaomClanFinanceModel : DefaultClanFinanceModel
{
    private readonly ICulturalFeatsService _feats;
    private readonly TAOM.Features.Enlistment.Content.IEnlistmentWagePreview _wagePreview;

    public TaomClanFinanceModel(
        ICulturalFeatsService feats,
        TAOM.Features.Enlistment.Content.IEnlistmentWagePreview wagePreview)
    {
        _feats = feats;
        _wagePreview = wagePreview;
    }

    /// <summary>
    /// Shows the enlistment wage in the clan's expected-change projection (field report 2). TAOM pays
    /// that wage itself, from the commander's purse, so vanilla's finance arithmetic has never known
    /// the player has an income — gold arrived daily from nowhere while the projection said it never
    /// would.
    ///
    /// DISPLAY ONLY, and <c>applyWithdrawals</c> is what makes it so. Verified on installed 1.4.8:
    /// <c>ClanVariablesCampaignBehavior</c> is the caller that actually moves the money, and it is
    /// the one that passes <c>applyWithdrawals: true</c> — every other call is a projection. Adding
    /// the line unconditionally would pay the player twice for the same day, once here and once
    /// through <c>ServiceRewardService.PayDailyWage</c>.
    ///
    /// Enlistment reaching into the finance model rather than the reverse is deliberate: this is
    /// TAOM's single registered <c>ClanFinanceModel</c> and therefore the shared seam, not
    /// CulturalFeats' private property. The class simply happens to live in that folder.
    /// </summary>
    public override ExplainedNumber CalculateClanGoldChange(
        Clan clan, bool includeDescriptions = false, bool applyWithdrawals = false,
        bool includeDetails = false)
    {
        var result = base.CalculateClanGoldChange(clan, includeDescriptions, applyWithdrawals, includeDetails);
        AddServiceWageLine(clan, applyWithdrawals, ref result);
        return result;
    }

    /// <summary>
    /// The clan screen's "Income" tile, which is a SEPARATE engine path — verified on installed
    /// 1.4.8, <c>DefaultClanFinanceModel.CalculateClanIncome</c> calls
    /// <c>CalculateClanIncomeInternal</c> directly and never routes through
    /// <c>CalculateClanGoldChange</c>, so overriding that one alone left the tile blind to the wage
    /// while the expected-change tooltip beside it showed the line. Two numbers on one screen
    /// disagreeing about the same income is how field report 2 gets filed a second time.
    ///
    /// Safe to add here for a stronger reason than the tooltip's: the ONLY caller in the engine is
    /// <c>ClanManagementVM</c> (<c>CalculateClanIncome(_clan)</c>, defaulted
    /// <c>applyWithdrawals: false</c>) — nothing moves money through this method at all. The
    /// withdrawal guard is kept anyway, because a future caller that does would otherwise pay the
    /// player twice.
    /// </summary>
    public override ExplainedNumber CalculateClanIncome(
        Clan clan, bool includeDescriptions = false, bool applyWithdrawals = false,
        bool includeDetails = false)
    {
        var result = base.CalculateClanIncome(clan, includeDescriptions, applyWithdrawals, includeDetails);
        AddServiceWageLine(clan, applyWithdrawals, ref result);
        return result;
    }

    /// <summary>
    /// ONE definition of the display line, shared by both projections. Duplicating it is how the
    /// two drift: the wage would be shown by one and not the other, or the withdrawal guard would
    /// be tightened in one place and not the other — and the second of those pays the player twice.
    /// </summary>
    private void AddServiceWageLine(Clan clan, bool applyWithdrawals, ref ExplainedNumber result)
    {
        if (applyWithdrawals || clan == null || clan != Clan.PlayerClan)
            return;

        var wage = _wagePreview?.GetDailyWage() ?? 0;
        if (wage > 0)
            result.Add(wage, new TaleWorlds.Localization.TextObject("{=taom_enlist_wage_line}Service wages"));
    }

    public override ExplainedNumber CalculateTownIncomeFromTariffs(
        Clan clan, Town town, bool applyWithdrawals = false)
    {
        var result = base.CalculateTownIncomeFromTariffs(clan, town, applyWithdrawals);
        _feats.ApplyTariffIncomeFeats(CultureFeatAdapter.FromOrNull(clan?.Culture), ref result);
        return result;
    }
}
