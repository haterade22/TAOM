using System;
using System.Collections.Generic;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Settlements;
using TaleWorlds.Localization;
using TAOM.Features.Realms.Domain.Services;

namespace TAOM.Features.Realms;

/// <summary>
/// Tribute as part of each clan's daily gold change. The finance model reads it for the map bar's expected change, the
/// clan screen and the daily payment (<c>ClanVariablesCampaignBehavior.DailyTickClan</c>), so the gold moves where the base
/// game moves every clan's income and no transfer of its own is needed. The sheet is built once per in-game day, or again
/// when a tribute percentage changes in the mod options; every read after that is a lookup. Clan daily ticks spread over
/// the day, so all of them pay from the same sheet.
/// </summary>
public sealed partial class RealmsCampaign
{
    private TributeSheet? _tribute;
    private (int Day, int Receive, int Pay) _tributeKey;
    private Dictionary<string, Clan>? _tributeClans;

    /// <summary>What the clan's vassal clans pay it today, one line per vassal clan with gold.</summary>
    private IReadOnlyList<(Clan Vassal, int Gold)> TributeFrom(Clan clan)
    {
        var result = new List<(Clan, int)>();
        foreach (var line in TodaysTribute().From(clan.StringId))
            if (line.Received > 0 && _tributeClans!.TryGetValue(line.PayerClanId, out var vassal))
                result.Add((vassal, line.Received));
        return result;
    }

    /// <summary>What the clan pays its liege clans today, one line per liege clan with gold.</summary>
    private IReadOnlyList<(Clan Liege, int Gold)> TributeTo(Clan clan)
    {
        var result = new List<(Clan, int)>();
        foreach (var line in TodaysTribute().To(clan.StringId))
            if (line.Paid > 0 && _tributeClans!.TryGetValue(line.PayeeClanId, out var liege))
                result.Add((liege, line.Paid));
        return result;
    }

    private static readonly TextObject TributeFromVassals = new("{=realms_finance_tribute_in}Tribute from Vassals");
    private static readonly TextObject TributeToLiege = new("{=realms_finance_tribute_out}Tribute to Liege");

    /// <summary>
    /// Adds what the clan's vassal clans pay it today to a gold change (<c>TaomClanFinanceModel</c>). Real gold, so like
    /// DailyGold it is on every call, the paying one included: <c>DailyTickClan</c> moves each clan's whole gold change, so
    /// the vassal's daily tick takes its tribute and the liege's adds it, with no transfer of its own. Each call is a lookup
    /// in today's sheet. With details on (the map bar's tooltip) each vassal clan is its own line under the total, as the
    /// base game shows Settlement Income.
    /// </summary>
    internal void AddTributeIncome(Clan? clan, bool includeDetails, ref ExplainedNumber result)
    {
        if (clan != null)
            AddTributeLine(TributeFrom(clan), 1, includeDetails, TributeFromVassals, ref result);
    }

    /// <summary>Adds what the clan pays its liege clans today; see <see cref="AddTributeIncome"/>.</summary>
    internal void AddTributeExpenses(Clan? clan, bool includeDetails, ref ExplainedNumber result)
    {
        if (clan != null)
            AddTributeLine(TributeTo(clan), -1, includeDetails, TributeToLiege, ref result);
    }

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

    private TributeSheet TodaysTribute()
    {
        // Host only, like every Realms effect: a co-op client's gold comes from the host.
        if (IsDisabled || !_service.IsInitialized || Campaign.Current == null || !_isAuthority())
            return TributeSheet.Empty;
        var feudal = Config.Feudal;
        var key = ((int)CampaignTime.Now.ToDays, feudal.TributeReceivePercent, feudal.TributePayPercent);
        if (_tribute != null && key == _tributeKey)
            return _tribute;

        _tributeKey = key;
        _tributeClans = new Dictionary<string, Clan>(StringComparer.Ordinal);
        foreach (var clan in Clan.All)
            if (clan != null && !clan.IsEliminated && !clan.IsUnderMercenaryService && clan.Leader != null)
                _tributeClans[clan.StringId] = clan;

        var bonds = new List<(string, string)>();
        foreach (var bond in _service.TributeBonds(_claimWorld, feudal))
            if (_tributeClans.ContainsKey(bond.PayerClanId) && _tributeClans.ContainsKey(bond.PayeeClanId))
                bonds.Add(bond);

        _tribute = TributeSheet.Build(bonds, id => FiefIncome(_tributeClans[id]), id => _tributeClans[id].Gold,
            feudal.TributeReceivePercent, feudal.TributePayPercent);
        return _tribute;
    }

    /// <summary>
    /// The clan's daily income from its towns, castles and their villages: the same calls and truncation as the base game's
    /// Settlement Income line (<c>DefaultClanFinanceModel.AddSettlementIncome</c>), through the model slots so TAOM's tariff
    /// feats count. Never with withdrawals: those take accumulated trade tax out of the town on every call. It does not read
    /// the clan's gold change, so tribute never counts towards tribute.
    /// </summary>
    private static int FiefIncome(Clan clan)
    {
        var models = Campaign.Current.Models;
        var income = 0;
        foreach (Town town in clan.Fiefs)
        {
            income += (int)models.SettlementTaxModel.CalculateTownTax(town).ResultNumber;
            income += (int)models.ClanFinanceModel.CalculateTownIncomeFromTariffs(clan, town, applyWithdrawals: false).ResultNumber;
            income += models.ClanFinanceModel.CalculateTownIncomeFromProjects(town);
            foreach (var village in town.Villages)
                income += models.ClanFinanceModel.CalculateVillageIncome(clan, village, applyWithdrawals: false);
        }

        return income;
    }
}
