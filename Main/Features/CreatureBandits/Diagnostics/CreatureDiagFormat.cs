using System.Globalization;
using System.Text;

namespace TAOM.Features.CreatureBandits.Diagnostics;

/// <summary>
/// Line format for the Creature Bandits diagnostics (#692): <c>[CreatureBandits][diag] kind t=12.35 cb=3 key=value ...</c>.
/// Invariant culture throughout (FileLogger applies none, and its timestamps are whole seconds, so every line carries
/// its own mission time). Engine floats are printed, never gated: NaN and infinities print by name. The one
/// <c>[CreatureBandits][diag]</c> prefix is what the post-sign-off strip greps for.
/// </summary>
internal static class CreatureDiagFormat
{
    internal const string Prefix = "[CreatureBandits][diag] ";

    internal static string F(float value)
    {
        if (float.IsNaN(value)) return "NaN";
        if (float.IsPositiveInfinity(value)) return "+Inf";
        if (float.IsNegativeInfinity(value)) return "-Inf";
        return value.ToString("0.00", CultureInfo.InvariantCulture);
    }

    internal static string I(long value) => value.ToString(CultureInfo.InvariantCulture);

    internal static string B(bool value) => value ? "1" : "0";

    /// <summary>A quoted engine or player-facing name, with anything that would split or forge a line removed.</summary>
    internal static string Name(string? value)
    {
        if (string.IsNullOrEmpty(value)) return "-";
        var clean = value!.Replace('"', '\'').Replace('\r', ' ').Replace('\n', ' ');
        return "\"" + clean + "\"";
    }

    /// <summary>One diagnostic line. <paramref name="serial"/> 0 is a mission-level line with no creature key.</summary>
    internal static string Line(string kind, float missionTime, int serial, params string[] pairs)
    {
        var sb = new StringBuilder(Prefix.Length + 48 + pairs.Length * 12);
        sb.Append(Prefix).Append(kind).Append(" t=").Append(F(missionTime));
        if (serial > 0) sb.Append(" cb=").Append(serial.ToString(CultureInfo.InvariantCulture));
        return AppendPairs(sb, pairs);
    }

    /// <summary>A campaign-map line, stamped with the campaign day.</summary>
    internal static string CampaignLine(string kind, double campaignDay, params string[] pairs)
    {
        var sb = new StringBuilder(Prefix.Length + 48 + pairs.Length * 12);
        sb.Append(Prefix).Append(kind).Append(" day=").Append(campaignDay.ToString("0.00", CultureInfo.InvariantCulture));
        return AppendPairs(sb, pairs);
    }

    private static string AppendPairs(StringBuilder sb, string[] pairs)
    {
        for (int i = 0; i < pairs.Length; i += 2)
        {
            sb.Append(' ').Append(pairs[i]).Append('=');
            if (i + 1 < pairs.Length) sb.Append(pairs[i + 1]);
        }
        return sb.ToString();
    }
}
