using System;
using System.Globalization;

namespace TAOM.Features.FactionMap.Widgets;

/// <summary>
/// Pure parsing/formatting of <c>PolygonWidget.Points</c> ("x,y;x,y;…", invariant culture) — extracted
/// verbatim from the widget (T4 refactor) so the culture handling and malformed-entry skipping are
/// unit-tested. Malformed pairs are silently skipped (pre-extraction behavior); formatting is F3.
/// </summary>
public static class PolygonPointParser
{
    public static (float[] Xs, float[] Ys) Parse(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return (Array.Empty<float>(), Array.Empty<float>());

        var pointStrings = value.Split(';');
        var xList = new System.Collections.Generic.List<float>();
        var yList = new System.Collections.Generic.List<float>();

        foreach (var ps in pointStrings)
        {
            var coords = ps.Trim().Split(',');
            if (coords.Length >= 2 &&
                float.TryParse(coords[0].Trim(), NumberStyles.Float, CultureInfo.InvariantCulture, out float x) &&
                float.TryParse(coords[1].Trim(), NumberStyles.Float, CultureInfo.InvariantCulture, out float y))
            {
                xList.Add(x);
                yList.Add(y);
            }
        }

        return (xList.ToArray(), yList.ToArray());
    }

    public static string Format(float[] xs, float[] ys, int count)
    {
        if (count == 0) return "";
        var parts = new string[count];
        for (int i = 0; i < count; i++)
            parts[i] = string.Format(CultureInfo.InvariantCulture, "{0:F3},{1:F3}", xs[i], ys[i]);
        return string.Join(";", parts);
    }
}
