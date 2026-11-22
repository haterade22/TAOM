using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using TAOM.Features.ArmyTargeting;
using TAOM.Features.WarChronicle.Events;

namespace TAOM.Features.WarChronicle.Cheats;

/// <summary>
/// The pure half of the chronicle's console commands (#765): the text of <c>taom.chronicle_status</c> and
/// the argument check and result line of <c>taom.chronicle_open</c>. Invariant culture throughout.
/// </summary>
internal static class ChronicleCheatReport
{
    internal const string StatusUsage =
        "Format is \"taom.chronicle_status\".\n"
        + "Prints every War Chronicle event with its state (Pending, Active, Occurred, Held, Fell, Lapsed, Skipped), "
        + "its days, and the army focus the open siege windows set. Read-only.";

    internal const string OpenUsage =
        "Format is \"taom.chronicle_open <id> [confirm]\".\n"
        + "Without 'confirm' it only reports what would happen and changes nothing. With 'confirm' it opens a "
        + "Pending chronicle event now, ignoring its day but still checking its preconditions: a narrative event "
        + "occurs and applies its effects; a siege opens its window, or resolves Fell at once when an attacker "
        + "already holds the target. Nothing undoes it. Ids are listed by taom.chronicle_status.";

    /// <summary>
    /// An id alone is a dry run; the id then exactly the literal <c>confirm</c> (ordinal) acts; anything else
    /// is refused with the usage, before any lookup.
    /// </summary>
    internal static bool TryReadOpenArgs(IReadOnlyList<string> args, out string id, out bool confirmed, out string error)
    {
        id = string.Empty;
        confirmed = false;
        error = string.Empty;
        if (args == null || args.Count < 1 || args.Count > 2 || string.IsNullOrWhiteSpace(args[0])
            || (args.Count == 2 && !string.Equals(args[1], "confirm", StringComparison.Ordinal)))
        {
            error = OpenUsage;
            return false;
        }

        id = args[0].Trim();
        confirmed = args.Count == 2;
        return true;
    }

    /// <summary>
    /// The console line for an open: a dry run says nothing changed and, when it would act, how to confirm.
    /// </summary>
    internal static string FormatOpen(string id, bool dryRun, bool acted, string reason)
    {
        if (!dryRun)
            return (acted ? "[WarChronicle] Opened: " : "[WarChronicle] Not opened: ") + reason;

        var text = "[WarChronicle] Dry run, nothing changed: " + reason;
        return acted ? text + $"\nRun \"taom.chronicle_open {id} confirm\" to do it." : text;
    }

    internal static string FormatStatus(
        IReadOnlyList<ChronicleStatusRow> rows, IReadOnlyList<ArmyTargetFocus> focus, double nowDay, bool enabled,
        bool focusScored)
    {
        var sb = new StringBuilder();
        sb.Append("[WarChronicle] Chronicle on campaign day ").Append(Day(nowDay))
          .Append(enabled ? "" : " (the chronicle is off: nothing opens or resolves)").Append('\n');

        if (rows.Count == 0)
        {
            sb.Append("No chronicle events loaded (see war_chronicle.json warnings in the log).");
            return sb.ToString();
        }

        foreach (var row in rows)
        {
            sb.Append("  ").Append(row.Id).Append(" (").Append(row.Kind).Append(") ").Append(row.State);
            if (row.Target != null)
                sb.Append(" at ").Append(row.Target);
            sb.Append(": ").Append(Days(row, nowDay)).Append('\n');
        }

        sb.Append("Army focus:");
        if (focus.Count == 0)
            sb.Append(" none");
        else if (!focusScored)
            sb.Append(" (inactive: AI Strategic Intelligence is off, so army scoring ignores it)");
        foreach (var f in focus)
        {
            sb.Append("\n  ").Append(f.SettlementId).Append(" x").Append(f.Multiplier.ToString("0.00", CultureInfo.InvariantCulture))
              .Append(": besieged by ").Append(string.Join(", ", f.AttackerFactionIds))
              .Append("; defended by ").Append(f.DefenderFactionId ?? "none");
        }

        return sb.ToString();
    }

    private static string Days(ChronicleStatusRow row, double nowDay)
    {
        switch (row.State)
        {
            case ChronicleEventState.Pending:
                return row.OpenDay.HasValue ? "opens day " + Day(row.OpenDay.Value) : "opens when its anchor is known";
            case ChronicleEventState.Active when row.Kind == ChronicleEventKind.Narrative:
                return "opened day " + Day(row.OpenDay) + ", closes day " + Day(row.CloseDay) + ", a hero's story window";
            case ChronicleEventState.Active:
                // Active past its close day only while an attacker still besieges the target (ChronicleService).
                var overtime = row.CloseDay.HasValue && nowDay >= row.CloseDay.Value;
                return "opened day " + Day(row.OpenDay) + (overtime ? ", overtime: an attacker still besieges it" : ", closes day " + Day(row.CloseDay))
                       + (row.Assaulted ? ", assaulted" : ", not assaulted yet");
            default:
                return "resolved day " + Day(row.ResolvedDay);
        }
    }

    private static string Day(double? day) =>
        day.HasValue ? day.Value.ToString("0.#", CultureInfo.InvariantCulture) : "?";
}
