using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace TAOM.Features.CaravanTrade.Diagnostics;

/// <summary>
/// Renders the `taom.print_caravans` report.
///
/// Split out of the cheat class rather than living beside the command it serves, because that class
/// is a Harmony/console entry point and ADR-002 caps those at 150 lines — it was 162. This is also
/// the better seam on its own terms: the formatter is pure and TaleWorlds-free, so it sits next to
/// the service whose output it renders and is testable without a campaign.
/// </summary>
internal static class CaravanGateReportFormatter
{
    /// <summary>
    /// The summary line comes first because the useful reading is "how many are stuck and why", not
    /// the per-caravan detail. The gate histogram is what turns a list into a diagnosis — five
    /// caravans all reading `Alerted` says "there is a battle nearby", which no single line conveys.
    /// </summary>
    internal static string Format(
        string settlementName,
        IReadOnlyList<(CaravanGateSnapshot Snapshot, CaravanGateVerdict Verdict)> verdicts,
        int totalFound,
        int cap)
    {
        var sb = new StringBuilder();
        var scope = settlementName ?? "all settlements";
        var blocked = verdicts.Count(v => v.Verdict.Gate != CaravanGate.NotBlocked);

        sb.AppendLine($"[Caravans] {scope}: {totalFound} in settlements, {blocked} of the {verdicts.Count} shown are blocked.");

        foreach (var group in verdicts
            .GroupBy(v => v.Verdict.Gate)
            .OrderByDescending(g => g.Count()))
        {
            sb.AppendLine($"[Caravans]   {group.Key}: {group.Count()}");
        }

        foreach (var (snapshot, verdict) in verdicts)
        {
            sb.AppendLine(
                $"[Caravans] {snapshot.CaravanName} @ {snapshot.CurrentSettlementId}"
                + $" -> target {snapshot.TargetSettlementId ?? "(none)"}");
            sb.AppendLine(
                $"[Caravans]   gate={verdict.Gate} wounded={verdict.WoundedFraction:P1}"
                + $" gold={snapshot.PartyTradeGold:N0} cargo={snapshot.CargoItemCount}"
                + $" leave/hr={verdict.EffectiveHourlyLeaveChance:P1}");
            sb.AppendLine($"[Caravans]   {verdict.Explanation}");
        }

        // Silent truncation would read as "that's all of them" — say so instead.
        if (totalFound > cap)
            sb.AppendLine($"[Caravans] ...{totalFound - cap} more not shown (capped at {cap}).");

        return sb.ToString().TrimEnd();
    }
}
