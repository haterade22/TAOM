using System;
using System.Collections.Generic;
using TAOM.Features.FordsOfWar.Domain;

namespace TAOM.Features.FordsOfWar;

/// <summary>
/// Finds the two base points of a generated layout from the playable polygon of a scene. The polygon's tightest enclosing
/// rectangle (turned in 1 degree steps) gives the long axis; each base sits on that axis through the rectangle's center,
/// <c>insetFraction</c> of the length in from its end. West is the end with the lower X (the lower Y on a tie), so the same
/// scene always gives the same sides. Pure: the scene adapter reads the polygon and checks the points against the navmesh.
/// </summary>
internal static class FordsBaseFinder
{
    private const float MinLength = 1e-3f;

    public static bool TryFind(IReadOnlyList<Vec3f> polygon, float insetFraction, out Vec3f west, out Vec3f east)
    {
        west = default;
        east = default;
        if (polygon.Count < 3)
            return false;

        var bestArea = float.MaxValue;
        var bestAngle = 0;
        for (var degrees = 0; degrees < 90; degrees++)
        {
            Extents(polygon, degrees, out var minU, out var maxU, out var minV, out var maxV);
            var area = (maxU - minU) * (maxV - minV);
            if (area < bestArea)
            {
                bestArea = area;
                bestAngle = degrees;
            }
        }

        Extents(polygon, bestAngle, out var loU, out var hiU, out var loV, out var hiV);
        var radians = bestAngle * Math.PI / 180d;
        var ux = (float)Math.Cos(radians);
        var uy = (float)Math.Sin(radians);
        var longU = hiU - loU >= hiV - loV;
        var length = longU ? hiU - loU : hiV - loV;
        if (!(length > MinLength))
            return false;

        // The axis is the rectangle's u axis, or its v axis (u turned a quarter turn) when that side is longer.
        var ax = longU ? ux : -uy;
        var ay = longU ? uy : ux;
        if (ax < -1e-6f || (Math.Abs(ax) <= 1e-6f && ay < 0f))
        {
            ax = -ax;
            ay = -ay;
        }

        var centerU = (loU + hiU) / 2f;
        var centerV = (loV + hiV) / 2f;
        var cx = centerU * ux - centerV * uy;
        var cy = centerU * uy + centerV * ux;
        var reach = length * (0.5f - Math.Max(0f, Math.Min(0.49f, insetFraction)));
        var z = Average(polygon);
        west = new Vec3f(cx - ax * reach, cy - ay * reach, z);
        east = new Vec3f(cx + ax * reach, cy + ay * reach, z);
        return true;
    }

    private static void Extents(IReadOnlyList<Vec3f> polygon, int degrees, out float minU, out float maxU, out float minV, out float maxV)
    {
        var radians = degrees * Math.PI / 180d;
        var ux = (float)Math.Cos(radians);
        var uy = (float)Math.Sin(radians);
        minU = minV = float.MaxValue;
        maxU = maxV = float.MinValue;
        foreach (var p in polygon)
        {
            var u = p.X * ux + p.Y * uy;
            var v = -p.X * uy + p.Y * ux;
            minU = Math.Min(minU, u);
            maxU = Math.Max(maxU, u);
            minV = Math.Min(minV, v);
            maxV = Math.Max(maxV, v);
        }
    }

    private static float Average(IReadOnlyList<Vec3f> polygon)
    {
        var total = 0f;
        foreach (var p in polygon)
            total += p.Z;
        return total / polygon.Count;
    }
}
