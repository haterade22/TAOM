using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.GameComponents;
using TaleWorlds.CampaignSystem.Settlements;
using System.Collections.Generic;
using TaleWorlds.Core;
using TaleWorlds.Localization;
using TAOM.Features.CareerSystem;
using TAOM.Features.CareerSystem.Domain;
using TAOM.Features.Realms;

namespace TAOM.Features.CulturalFeats.Models;

public class TaomClanFinanceModel : DefaultClanFinanceModel
{
    private readonly ICulturalFeatsService _feats;
    private readonly TAOM.Features.Enlistment.Content.IEnlistmentWagePreview _wagePreview;
    private readonly ICareerPassiveService _careerPassives;

    public TaomClanFinanceModel(
        ICulturalFeatsService feats,
        TAOM.Features.Enlistment.Content.IEnlistmentWagePreview wagePreview,
        ICareerPassiveService careerPassives)
    {
        _feats = feats;
        _wagePreview = wagePreview;
        _careerPassives = careerPassives;
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
        AddTributeIncome(clan, includeDetails, ref result);
        AddTributeExpenses(clan, includeDetails, ref result);
        AddCareerDailyGold(clan, ref result);
        return result;
    }

    /// <summary>
    /// The clan screen's "Expenses" tile (<c>ClanManagementVM</c>, the only engine caller), so it carries the tribute the
    /// clan pays as the income tile carries what it receives.
    /// </summary>
    public override ExplainedNumber CalculateClanExpenses(
        Clan clan, bool includeDescriptions = false, bool applyWithdrawals = false,
        bool includeDetails = false)
    {
        var result = base.CalculateClanExpenses(clan, includeDescriptions, applyWithdrawals, includeDetails);
        AddTributeExpenses(clan, includeDetails, ref result);
        return result;
    }

    /// <summary>
    /// Realms tribute (docs/features/realms.md): what the clan's vassal clans pay it today. Real gold, so like DailyGold it is
    /// on every call, the paying one included: <c>DailyTickClan</c> moves each clan's whole gold change, so the vassal's
    /// daily tick takes its tribute and the liege's adds it, with no transfer of its own. Read from a sheet built once a
    /// day (<see cref="RealmsCampaign"/>), so each call is a lookup. With details on (the map bar's tooltip) each vassal clan
    /// is its own line under the total, as the base game shows Settlement Income.
    /// </summary>
    private static void AddTributeIncome(Clan clan, bool includeDetails, ref ExplainedNumber result)
    {
        var realms = clan == null ? null : RealmsCampaign.Current;
        if (realms == null)
            return;
        AddTributeLine(realms.TributeFrom(clan!), 1, includeDetails, TributeFromVassals, ref result);
    }

    /// <summary>What the clan pays its liege clans today; see <see cref="AddTributeIncome"/>.</summary>
    private static void AddTributeExpenses(Clan clan, bool includeDetails, ref ExplainedNumber result)
    {
        var realms = clan == null ? null : RealmsCampaign.Current;
        if (realms == null)
            return;
        AddTributeLine(realms.TributeTo(clan!), -1, includeDetails, TributeToLiege, ref result);
    }

    private static readonly TextObject TributeFromVassals = new("{=realms_finance_tribute_in}Tribute from Vassals");
    private static readonly TextObject TributeToLiege = new("{=realms_finance_tribute_out}Tribute to Liege");

    private static void AddTributeLine(IReadOnlyList<(Clan Clan, int Gold)> lines, int sign, bool includeDetails, TextObject label,
        ref ExplainedNumber result)
    {
        if (lines.Count == 0)
            return;
        var inner = new ExplainedNumber(0f, result.IncludeDescriptions);
        foreach (var (other, gold) in lines)
            inner.Add(sign * gold, other.Name);
        if (includeDetails)
            result.AddFromExplainedNumber(inner, label);
        else
            result.Add(inner.ResultNumber, label);
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
        AddTributeIncome(clan, includeDetails, ref result);
        AddCareerDailyGold(clan, ref result);
        return result;
    }

    /// <summary>
    /// Career DailyGold (#769 E11): a flat gold per day for the clan leader, on BOTH projections, and unlike the
    /// enlistment line ABOVE the <c>applyWithdrawals</c> guard on purpose. <c>ClanVariablesCampaignBehavior.DailyTickClan</c>
    /// pays the player with <c>CalculateClanGoldChange(applyWithdrawals: true)</c> (v1.5.5 :413), so that call is the
    /// payment; the income tile (<c>ClanManagementVM</c>) and the map-bar tooltip are projections of the same
    /// number, and all three must carry the term or the tooltip stops matching the gold the clan leader receives each
    /// day. The transfer itself is silent (<c>DailyTickClan</c> passes <c>disableNotification: true</c>), but for the
    /// player clan it then shows "Daily Gold Change: {CHANGE}" (<c>dPD5zood</c>, v1.5.5 :427-432) with the same rounded
    /// payment, so that message reads the paid total, DailyGold included. Called last, after
    /// every factor the base put on the number (<c>ApplyFlat</c> divides out only the factors already there) and after
    /// the base's wage withdrawals: vanilla budgets the wages from <c>clan.Gold</c> plus the change so far (:816, :828),
    /// so on a day the clan cannot afford them the DailyGold shows in the number but does not cover that day's unpaid
    /// wages. It touches every clan's tick but only a clan leader with a career has a magnitude.
    /// </summary>
    private void AddCareerDailyGold(Clan clan, ref ExplainedNumber result)
        => _careerPassives.ApplyFlat(clan?.Leader?.StringId, ref result, PassiveEffectType.DailyGold);

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
