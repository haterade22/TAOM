using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Settlements;
using TaleWorlds.Library;
using TAOM.Features.DevConsole;

namespace TAOM.Features.EconomyDiagnostics.Cheats;

/// <summary>
/// `taom.print_town_ledger [town]` — where a town's market gold actually went, by day and by flow.
///
/// Why it exists: `taom.print_town_economy` already answers "is the mint paying in enough" and for
/// Minas Tirith it says yes — a target of ~77,000 and roughly 19,000/day owed at a balance of 173.
/// That leaves the only useful question, "so who is taking it back out", which no engine code logs
/// or reports. This is that answer, and it is the gate on choosing between raising the mint,
/// reserving gold against villager deliveries, and rebaselining item values (#318).
/// </summary>
public static class EconomyDiagnosticsCheats
{
    private const string Usage =
        "Format is \"taom.print_town_ledger [TownName/TownId]\".\n"
        + "With no argument, uses the town you are currently in.\n"
        + "Prints per-day market-gold movement broken down by who moved it, plus window totals.\n"
        + "Data starts accumulating when the campaign loads; allow 3-5 in-game days for a clear read.";

    [CommandLineFunctionality.CommandLineArgumentFunction("print_town_ledger", "taom")]
    public static string PrintTownLedger(List<string> strings) =>
        TaomConsole.RunInCampaign(strings, Usage, args =>
        {
            var settlement = ResolveSettlement(args, out var error);
            if (settlement == null) return error + "\n" + Usage;

            // Only towns receive the daily gold tick (the engine iterates Town.AllTowns), and only
            // towns are recorded. Computing a report for a castle would invent a pool that the
            // engine never touches.
            if (settlement.Town == null || !settlement.IsTown)
                return $"'{settlement.Name}' is not a town. Only towns have a market-gold pool.";

            var ledger = IoC.Resolve<ITownGoldLedger>();

            return FormatLedger(
                settlement.StringId,
                settlement.Name?.ToString() ?? settlement.StringId,
                settlement.Town.Gold,
                ledger.GetHistory(settlement.StringId),
                ledger.GetTotals(settlement.StringId));
        });

    private static Settlement ResolveSettlement(List<string> args, out string error)
    {
        error = string.Empty;

        if (args.Count == 0)
        {
            var current = Settlement.CurrentSettlement;
            if (current != null) return current;
            error = "Not in a settlement. Pass a town name or id.";
            return null;
        }

        return CampaignCheats.TryGetObject(CampaignCheats.ConcatenateString(args), out Settlement settlement, out error)
            ? settlement
            : null;
    }

    /// <summary>
    /// Pure, so the report shape is testable without a campaign. Totals lead: the per-day rows are
    /// for confirming a pattern, but the actionable number is which flow dominates over the window.
    /// </summary>
    internal static string FormatLedger(
        string townId, string townName, int gold,
        IReadOnlyList<TownGoldDay> history, TownGoldDay totals)
    {
        var sb = new StringBuilder();
        sb.AppendLine($"[TownLedger] {townId} {townName}: gold={gold:N0}");

        if (history.Count == 0)
        {
            sb.Append("[TownLedger]   No movement recorded yet — let a few in-game days pass.");
            return sb.ToString();
        }

        sb.AppendLine($"[TownLedger]   Totals over the last {history.Count} day(s):");
        foreach (var flow in OrderedFlows(totals))
            sb.AppendLine($"[TownLedger]     {flow,-20} {totals.AmountFor(flow),12:+#,##0;-#,##0;0}");

        sb.AppendLine($"[TownLedger]     {"NET",-20} {totals.Net,12:+#,##0;-#,##0;0}");
        sb.AppendLine("[TownLedger]   By day (newest first):");

        for (var i = 0; i < history.Count; i++)
        {
            var day = history[i];
            var breakdown = string.Join(
                "  ", OrderedFlows(day).Select(f => $"{f}={day.AmountFor(f):+#,##0;-#,##0;0}"));

            sb.AppendLine($"[TownLedger]     -{i}d net={day.Net,+9:+#,##0;-#,##0;0}  {breakdown}");
        }

        sb.Append($"[TownLedger]   {Interpret(totals)}");
        return sb.ToString();
    }

    /// <summary>Biggest absolute mover first — that is the one worth acting on.</summary>
    private static IEnumerable<TownGoldFlow> OrderedFlows(TownGoldDay day) =>
        day.RecordedFlows.OrderByDescending(f => Math.Abs(day.AmountFor(f)));

    /// <summary>
    /// One line naming the dominant drain, because the failure mode of a breakdown table is that
    /// the reader takes the biggest number rather than the biggest *outflow*.
    /// </summary>
    private static string Interpret(TownGoldDay totals)
    {
        var drains = totals.RecordedFlows.Where(f => totals.AmountFor(f) < 0).ToList();
        if (drains.Count == 0) return "no net drain recorded in this window";

        var worst = drains.OrderBy(f => totals.AmountFor(f)).First();
        return $"largest drain: {worst} at {totals.AmountFor(worst):N0} over the window";
    }
}
