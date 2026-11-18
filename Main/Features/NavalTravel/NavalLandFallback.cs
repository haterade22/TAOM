namespace TAOM.Features.NavalTravel;

/// <summary>
/// Pure decision for <c>Patch168_NavalLandPathfindGuard</c>: which land anchor replaces the crashing native
/// nearest-face search. No engine types, so it is unit-testable.
/// </summary>
public static class NavalLandFallback
{
    /// <summary>
    /// Index of the anchor nearest to (<paramref name="x"/>, <paramref name="y"/>) by squared distance, or -1
    /// when there is none. Ties keep the lower index. A non-finite query or anchor never wins.
    /// </summary>
    public static int NearestIndex(float x, float y, System.Collections.Generic.IReadOnlyList<(float X, float Y)> anchors)
    {
        if (anchors == null || float.IsNaN(x) || float.IsNaN(y) || float.IsInfinity(x) || float.IsInfinity(y))
            return -1;

        var best = -1;
        var bestD = float.MaxValue;
        for (var i = 0; i < anchors.Count; i++)
        {
            var dx = anchors[i].X - x;
            var dy = anchors[i].Y - y;
            var d = (dx * dx) + (dy * dy);
            if (float.IsNaN(d) || float.IsInfinity(d)) continue;
            if (d < bestD)
            {
                bestD = d;
                best = i;
            }
        }
        return best;
    }
}
