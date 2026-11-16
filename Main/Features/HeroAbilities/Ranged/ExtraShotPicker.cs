using System;
using System.Collections.Generic;
using TAOM.Features.FordsOfWar.Domain;

namespace TAOM.Features.HeroAbilities.Ranged;

/// <summary>Primary is the index of the target the shot flies at (-1 = none); Extras the indices that get an extra arrow.</summary>
internal sealed record ExtraShotPick(int Primary, IReadOnlyList<int> Extras);

/// <summary>
/// Chooses the targets of the extra arrows from the enemies around a shot. The primary is the enemy nearest the
/// shot's line, so an extra never goes out when the hero shoots at nothing.
/// </summary>
internal static class ExtraShotPicker
{
    private const float ConeDegrees = 6f;

    private static readonly IReadOnlyList<int> NoExtras = new int[0];
    private static readonly ExtraShotPick None = new ExtraShotPick(-1, NoExtras);

    public static ExtraShotPick Pick(Vec3f origin, Vec3f shotDir, IReadOnlyList<Vec3f> candidates, int extra, float spread, float range)
    {
        var shotLen = Math.Sqrt((double)shotDir.X * shotDir.X + (double)shotDir.Y * shotDir.Y);
        if (!(shotLen > 1e-6) || !IsFinite(origin) || !(range > 0f))
            return None;

        var cosCone = Math.Cos(ConeDegrees * Math.PI / 180.0);
        var primary = -1;
        var bestCos = -2.0;
        var bestDistance = float.MaxValue;
        for (var i = 0; i < candidates.Count; i++)
        {
            var c = candidates[i];
            if (!IsFinite(c) || !(origin.Distance(c) <= range))
                continue;
            var dx = c.X - origin.X;
            var dy = c.Y - origin.Y;
            var horizontal = Math.Sqrt((double)dx * dx + (double)dy * dy);
            if (!(horizontal > 1e-6))
                continue;
            var cos = (dx * shotDir.X + dy * shotDir.Y) / (horizontal * shotLen);
            if (cos < cosCone)
                continue;
            var distance = origin.Distance(c);
            if (cos > bestCos || (cos == bestCos && distance < bestDistance))
            {
                primary = i;
                bestCos = cos;
                bestDistance = distance;
            }
        }
        if (primary < 0)
            return None;
        if (extra <= 0 || !(spread > 0f))
            return new ExtraShotPick(primary, NoExtras);

        var near = new List<KeyValuePair<float, int>>();
        for (var i = 0; i < candidates.Count; i++)
        {
            var c = candidates[i];
            if (i == primary || !IsFinite(c) || !(origin.Distance(c) <= range))
                continue;
            var d = candidates[primary].Distance2D(c);
            if (d <= spread)
                near.Add(new KeyValuePair<float, int>(d, i));
        }
        near.Sort((a, b) => a.Key != b.Key ? a.Key.CompareTo(b.Key) : a.Value.CompareTo(b.Value));
        var extras = new List<int>();
        for (var k = 0; k < near.Count && k < extra; k++)
            extras.Add(near[k].Value);
        return new ExtraShotPick(primary, extras);
    }

    private static bool IsFinite(Vec3f v) => IsFinite(v.X) && IsFinite(v.Y) && IsFinite(v.Z);

    private static bool IsFinite(float f) => !float.IsNaN(f) && !float.IsInfinity(f);
}
