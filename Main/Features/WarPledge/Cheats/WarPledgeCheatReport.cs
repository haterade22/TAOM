using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using TAOM.Features.Diplomacy.Models;
using TAOM.Features.Execution;

namespace TAOM.Features.WarPledge.Cheats;

/// <summary>
/// The pure half of War Pledge's console commands (#1113): the text of <c>taom.print_war_pledges</c> and the
/// argument check and result line of <c>taom.add_war_pledge</c>. Invariant culture throughout.
/// </summary>
internal static class WarPledgeCheatReport
{
    internal const string StatusUsage =
        "Format is \"taom.print_war_pledges\".\n"
        + "Prints the War Pledge switches, the momentum balance, the side that courts now, the day the AI may court "
        + "again, and every realm of the lean table with its lean and its pledge. Read-only.";

    internal const string JoinUsage =
        "Format is \"taom.add_war_pledge <kingdom id> <free|evil> [confirm]\".\n"
        + "Without 'confirm' it only reports what would happen and changes nothing. With 'confirm' it pledges a "
        + "Neutral realm to that side now, ignoring the MCM switch, the lean table, the balance and the wait between "
        + "AI joins: the realm makes peace with the realms of that side and goes to war with every realm of the "
        + "other side, and those wars cannot end in peace while the War of the Ring lasts. It still needs the Full "
        + "War, a live kingdom with no side and no pledge, and no Hostile row with a realm of that side. A realm you "
        + "rule is allowed. The pledge is saved and nothing undoes it. taom.print_war_pledges lists the ids of the "
        + "lean table.";

    /// <summary>
    /// An id and a side are a dry run; then exactly the literal <c>confirm</c> (ordinal) acts; anything else is
    /// refused with the usage, before any lookup.
    /// </summary>
    internal static bool TryReadJoinArgs(
        IReadOnlyList<string>? args, out string kingdomId, out FactionSide side, out bool confirmed, out string error)
    {
        kingdomId = string.Empty;
        side = FactionSide.Neutral;
        confirmed = false;
        error = JoinUsage;
        if (args == null || args.Count < 2 || args.Count > 3 || string.IsNullOrWhiteSpace(args[0]))
            return false;
        if (args.Count == 3 && !string.Equals(args[2], "confirm", StringComparison.Ordinal))
            return false;

        var sideText = args[1]?.Trim();
        if (string.Equals(sideText, "free", StringComparison.OrdinalIgnoreCase))
            side = FactionSide.Free;
        else if (string.Equals(sideText, "evil", StringComparison.OrdinalIgnoreCase))
            side = FactionSide.Evil;
        else
            return false;

        kingdomId = args[0].Trim();
        confirmed = args.Count == 3;
        error = string.Empty;
        return true;
    }

    /// <summary>The console line for a join: a dry run says nothing changed and, when it would act, how to confirm.</summary>
    internal static string FormatJoin(string kingdomId, FactionSide side, bool dryRun, bool acted, string reason)
    {
        if (!dryRun)
            return (acted ? "[WarPledge] Pledged: " : "[WarPledge] Not pledged: ") + reason;

        var text = "[WarPledge] Dry run, nothing changed: " + reason;
        return acted
            ? text + $"\nRun \"taom.add_war_pledge {kingdomId} {SideWord(side)} confirm\" to do it."
            : text;
    }

    internal static string FormatStatus(WarPledgeStatus status, int? day)
    {
        var sb = new StringBuilder();
        sb.Append("[WarPledge] Campaign day ").Append(day.HasValue ? Number(day.Value) : "?").Append(". ")
          .Append("Switch ").Append(OnOff(status.Enabled))
          .Append(", momentum tracking ").Append(OnOff(status.MomentumEnabled))
          .Append(", War of the Ring ").Append(OnOff(status.WarOfTheRingEnabled))
          .Append(" (").Append(status.Phase).Append(").\n");

        var canCourt = status.Enabled && status.MomentumEnabled && status.WarOfTheRingEnabled && status.Phase == WarPhase.FullWar;
        if (!canCourt)
            sb.Append("The AI courts no realm now: it needs all three on and the Full War.\n");

        sb.Append("Balance ").Append(Number(status.Balance))
          .Append(" (positive when the Free Peoples lead); a side courts at ").Append(Number(status.LosingBalance))
          .Append(" against it: ").Append(CourtingSide(status.CourtingSide, canCourt)).Append('\n');

        if (status.NextCourtingDay == WarPledgeStore.NoCourtingDay)
            sb.Append("The courting day is not set: the daily step has not yet seen the Full War with the switches on.");
        else
            sb.Append("The AI courts no realm before day ").Append(Number(status.NextCourtingDay))
              .Append(", and waits ").Append(Number(status.DaysBetweenAiJoins)).Append(" days after a join.");

        foreach (var row in status.Rows)
        {
            sb.Append("\n  ").Append(row.KingdomId).Append(": ").Append(Lean(row.Lean)).Append(", ")
              .Append(row.PledgedSide == FactionSide.Neutral ? "not pledged" : "pledged " + row.PledgedSide);
            if (!row.IsLive)
                sb.Append(", not a live kingdom");
            else if (row.IsPlayerRuled)
                sb.Append(", ruled by a player (the AI does not court it)");
        }

        return sb.ToString();
    }

    private static string CourtingSide(FactionSide side, bool canCourt) =>
        side == FactionSide.Neutral ? "no side courts." : "the " + side + " side " + (canCourt ? "courts." : "would court.");

    private static string Lean(FactionSide? lean)
    {
        if (!lean.HasValue)
            return "no lean row";
        return lean.Value == FactionSide.Neutral ? "true neutral (the AI does not court it)" : "leans " + lean.Value;
    }

    private static string SideWord(FactionSide side) => side == FactionSide.Free ? "free" : "evil";

    private static string OnOff(bool on) => on ? "on" : "off";

    private static string Number(int value) => value.ToString(CultureInfo.InvariantCulture);
}
