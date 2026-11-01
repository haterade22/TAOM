using System.Globalization;
using TAOM.Features.FordsOfWar.Domain;

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

    /// <summary>A ground point as "(x,y)" with one decimal; a dash for no point or a coordinate that is not a finite number.</summary>
    internal static string Point(Vec3f? point) =>
        point is { } p && IsFinite(p.X) && IsFinite(p.Y) ? "(" + F1(p.X) + "," + F1(p.Y) + ")" : "-";

    /// <summary>A distance in metres with one decimal. A dash for "nothing there": float.MaxValue is what the rules use for no enemy or tower, and NaN or infinity is never a distance.</summary>
    internal static string Distance(float metres) => IsFinite(metres) && metres < float.MaxValue ? F1(metres) : "-";

    private static bool IsFinite(float v) => !float.IsNaN(v) && !float.IsInfinity(v);
}
