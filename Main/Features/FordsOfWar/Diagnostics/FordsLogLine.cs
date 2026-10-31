using System.Globalization;

namespace TAOM.Features.FordsOfWar.Diagnostics;

/// <summary>Builds every <c>[FordsOfWar][Topic]</c> line: invariant culture, text values quoted, one shape.</summary>
internal static class FordsLogLine
{
    internal static string Of(string topic, int run, double wall, double mt, string body)
    {
        var head = "[FordsOfWar][" + topic + "] run=" + run.ToString(CultureInfo.InvariantCulture) + " w=" + F3(wall) + " mt=" + F1(mt);
        return string.IsNullOrEmpty(body) ? head : head + " " + body;
    }

    /// <summary>A quoted text value; a double quote inside it becomes a single quote so the line stays parseable.</summary>
    internal static string Q(string? s) => "\"" + (s ?? "").Replace('"', '\'') + "\"";

    internal static string F1(double v) => v.ToString("F1", CultureInfo.InvariantCulture);

    internal static string F3(double v) => v.ToString("F3", CultureInfo.InvariantCulture);
}
