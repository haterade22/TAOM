using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using TAOM.Core.Validation;
using TAOM.Features.DevConsole;
using TAOM.Features.WarChronicle.Effects;
using TAOM.Features.WarChronicle.Rally;

namespace TAOM.Features.WarChronicle.Cheats;

/// <summary>
/// The pure half of the War Chronicle console commands: argument validation for
/// <c>taom.war_effect_add</c> and the text of <c>taom.war_effects</c>. Invariant culture throughout, and
/// every number is finite-checked before a range check (DevConsoleArgs; csharp-architecture.md).
/// </summary>
internal static class WarChronicleCheatReport
{
    internal const string AddUsage =
        "Format is \"taom.war_effect_add <kingdomId> <VolunteerRate|PrisonerEscape> <magnitude> <days>\".\n"
        + "Adds a timed effect from source \"console\" to a kingdom: magnitude is a fraction from -1 to 1 "
        + "(0.15 is +15%), days from just above 0 to 365. Run it again to refresh or change the same effect.";

    internal const float MaxMagnitude = 1f;
    internal const float MaxDays = 365f;

    internal static bool TryBuildEffect(
        IReadOnlyList<string> args, double nowHours, IReadOnlyCollection<string> knownKingdomIds,
        out WarEffect? effect, out string error)
    {
        effect = null;
        if (args == null || args.Count != 4)
        {
            error = AddUsage;
            return false;
        }

        var kingdomId = args[0]?.Trim() ?? string.Empty;
        if (!knownKingdomIds.Contains(kingdomId, StringComparer.Ordinal))
        {
            error = $"'{kingdomId}' is not a kingdom in this campaign. Known: {string.Join(", ", knownKingdomIds)}.";
            return false;
        }

        if (!TryParseKind(args[1], out var kind))
        {
            error = $"'{args[1]}' is not an effect kind. Use {string.Join(" or ", Enum.GetNames(typeof(WarEffectKind)))}.";
            return false;
        }

        if (!DevConsoleArgs.TryParseAmount(args[2], out var magnitude, out var magnitudeError))
        {
            error = "Magnitude: " + magnitudeError;
            return false;
        }

        if (Math.Abs(magnitude) > MaxMagnitude)
        {
            error = $"Magnitude must be from -{MaxMagnitude.ToString(CultureInfo.InvariantCulture)} to {MaxMagnitude.ToString(CultureInfo.InvariantCulture)} (got {magnitude.ToString(CultureInfo.InvariantCulture)}).";
            return false;
        }

        if (!DevConsoleArgs.TryParseAmount(args[3], out var days, out var daysError))
        {
            error = "Days: " + daysError;
            return false;
        }

        if (!FiniteFloatValidator.IsFinite(days) || days <= 0f || days > MaxDays)
        {
            error = $"Days must be above 0 and at most {MaxDays.ToString(CultureInfo.InvariantCulture)} (got {days.ToString(CultureInfo.InvariantCulture)}).";
            return false;
        }

        if (!FiniteFloatValidator.IsFinite(nowHours))
        {
            error = "The campaign clock is not readable, so no end time can be set.";
            return false;
        }

        effect = new WarEffect("console", kingdomId, kind, magnitude, nowHours + days * 24d);
        error = string.Empty;
        return true;
    }

    internal static string FormatEffects(
        IReadOnlyList<WarEffect> effects, double nowHours, Func<string, WarEffectKind, float> multiplier)
    {
        if (effects == null || effects.Count == 0)
            return "[WarEffects] No active war effects.";

        var sb = new StringBuilder();
        sb.AppendLine(Inv($"[WarEffects] {effects.Count} active (campaign hour {nowHours:0})"));
        foreach (var e in effects)
        {
            var left = (e.EndTimeHours - nowHours) / 24d;
            var timing = left > 0d ? Inv($"ends in {left:0.0} days") : "ended, removed at the next daily tick";
            sb.AppendLine(Inv($"[WarEffects]   {e.SourceId}: {e.KingdomId} {e.Kind} {e.Magnitude:+0.000;-0.000;+0.000}, {timing}"));
        }

        sb.AppendLine("[WarEffects] Multipliers (MCM strength applied):");
        var kinds = Enum.GetValues(typeof(WarEffectKind)).Cast<WarEffectKind>().ToList();
        foreach (var kingdom in effects.Select(e => e.KingdomId).Distinct(StringComparer.Ordinal).OrderBy(k => k, StringComparer.Ordinal))
        {
            var parts = kinds.Select(kind => Inv($"{kind} x{multiplier(kingdom, kind):0.00}"));
            sb.AppendLine($"[WarEffects]   {kingdom}: {string.Join(", ", parts)}");
        }

        return sb.ToString().TrimEnd();
    }

    internal static string FormatAdded(WarEffect effect, float before, float after) =>
        Inv($"[WarEffects] {effect.KingdomId} {effect.Kind} {effect.Magnitude:+0.000;-0.000;+0.000} from \"{effect.SourceId}\" until campaign hour {effect.EndTimeHours:0}: multiplier x{before:0.00} -> x{after:0.00}");

    internal static string FormatRally(IReadOnlyList<RallyStatusRow> rows, bool rallyOn)
    {
        var header = rallyOn
            ? "[Rally] On (data file and MCM switch)."
            : "[Rally] Off (data file or MCM switch): tiers are still tracked, no effects are written.";
        if (rows == null || rows.Count == 0)
            return header + "\n[Rally] No kingdoms.";

        var sb = new StringBuilder();
        sb.AppendLine(header);
        sb.AppendLine("[Rally] kingdom: points / baseline, loss, tier, eligible");
        foreach (var row in rows.OrderBy(r => r.KingdomId, StringComparer.Ordinal))
        {
            var baseline = row.Baseline.HasValue ? row.Baseline.Value.ToString(CultureInfo.InvariantCulture) : "na";
            var loss = row.Loss.HasValue ? row.Loss.Value.ToString("0.000", CultureInfo.InvariantCulture) : "na";
            sb.AppendLine(Inv($"[Rally]   {row.KingdomId}: {row.Points} / {baseline}, loss {loss}, tier {row.Tier}, eligible {(row.Eligible ? "yes" : "no")}"));
        }

        return sb.ToString().TrimEnd();
    }

    private static bool TryParseKind(string? raw, out WarEffectKind kind)
    {
        kind = default;
        var name = raw?.Trim();
        if (string.IsNullOrEmpty(name))
            return false;
        foreach (var candidate in Enum.GetNames(typeof(WarEffectKind)))
        {
            if (string.Equals(candidate, name, StringComparison.OrdinalIgnoreCase))
            {
                kind = (WarEffectKind)Enum.Parse(typeof(WarEffectKind), candidate);
                return true;
            }
        }

        return false;
    }

    private static string Inv(FormattableString text) => text.ToString(CultureInfo.InvariantCulture);
}
