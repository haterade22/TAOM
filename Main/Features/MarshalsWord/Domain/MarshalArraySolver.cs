using System;
using System.Collections.Generic;
using System.Linq;

namespace TAOM.Features.MarshalsWord.Domain;

/// <summary>
/// One company as the solver sees it (build-spec 6.2): <c>X</c>, <c>Y</c> is its position (the order point when it holds one, else the
/// current position) and <c>DirX</c>, <c>DirY</c> its facing.
/// </summary>
internal readonly record struct ArrayCompany(
    int Company, float X, float Y, float Width, float MinWidth, float MaxWidth, float Depth, int Units, bool Column, float DirX, float DirY);

/// <summary>Where one company goes and how wide it stands, with the direction it faces (a drag turns them all to the new front).</summary>
internal readonly record struct ArrayPlacement(int Company, float X, float Y, float Width, float DirX, float DirY);

/// <summary>The solver's answer. <c>Ok</c> false means an input was not usable and vanilla must run.</summary>
internal readonly record struct ArrayResult(bool Ok, bool IsLineShort, IReadOnlyList<ArrayPlacement> Placements)
{
    internal static ArrayResult Failed { get; } = new(false, false, Array.Empty<ArrayPlacement>());
}

/// <summary>
/// Keep the array (build-spec 6.2): a click carries the selected companies by one offset and a drag turns, stretches and places them as
/// one body, in doubles because a float product of two large coordinates overflows.
/// </summary>
internal static class MarshalArraySolver
{
    private const double Epsilon = 1e-9;

    /// <summary>Vanilla's short line test, restated (OrderController.cs:1208-1221): under the bipedal radius, or under the minimum widths plus the gaps.</summary>
    internal static bool IsLineShort(float length, float bipedalRadius, float sumMinimumWidth, int count) =>
        length < bipedalRadius || length < sumMinimumWidth + (count - 1) * MarshalTuning.GapInLine;

    internal static ArrayResult Solve(IReadOnlyList<ArrayCompany> companies, float beginX, float beginY, float endX, float endY,
        bool keepWidths, float bipedalRadius)
    {
        if (companies.Count == 0 || !IsFinite(beginX, beginY, endX, endY, bipedalRadius) || !companies.All(IsUsable))
            return ArrayResult.Failed;

        var length = Math.Sqrt(Math.Pow((double)endX - beginX, 2) + Math.Pow((double)endY - beginY, 2));
        var isShort = IsLineShort((float)length, bipedalRadius, companies.Sum(c => c.MinWidth), companies.Count);

        var (originX, originY) = Origin(companies);
        var (forwardX, forwardY) = Forward(companies);
        var rightX = forwardY;
        var rightY = -forwardX;

        var shape = new Shape[companies.Count];
        for (var i = 0; i < shape.Length; i++)
        {
            var dx = companies[i].X - originX;
            var dy = companies[i].Y - originY;
            shape[i] = new Shape(dx * rightX + dy * rightY, dx * forwardX + dy * forwardY);
        }

        var placements = isShort
            ? Click(companies, beginX, beginY, originX, originY, forwardX, forwardY)
            : Drag(companies, shape, beginX, beginY, endX, endY, length, keepWidths);
        return placements.All(IsFinite) ? new ArrayResult(true, isShort, placements) : ArrayResult.Failed;
    }

    private readonly record struct Shape(double Across, double Along);

    private static ArrayPlacement[] Click(IReadOnlyList<ArrayCompany> companies, float beginX, float beginY, double originX, double originY,
        double forwardX, double forwardY)
    {
        var shiftX = beginX - originX;
        var shiftY = beginY - originY;
        return companies.Select(c =>
        {
            var (dx, dy) = Normalised(c.DirX, c.DirY, forwardX, forwardY);
            return new ArrayPlacement(c.Company, (float)(c.X + shiftX), (float)(c.Y + shiftY), c.Width, (float)dx, (float)dy);
        }).ToArray();
    }

    private static ArrayPlacement[] Drag(IReadOnlyList<ArrayCompany> companies, Shape[] shape, float beginX, float beginY, float endX,
        float endY, double length, bool keepWidths)
    {
        var lineX = (endX - (double)beginX) / length;
        var lineY = (endY - (double)beginY) / length;
        var newForwardX = -lineY;
        var newForwardY = lineX;

        var span = shape.Select((s, i) => s.Across + companies[i].Width / 2.0).Max() - shape.Select((s, i) => s.Across - companies[i].Width / 2.0).Min();
        var stretch = keepWidths || span <= Epsilon ? 1.0 : length / span;

        var widths = new double[companies.Count];
        var across = new double[companies.Count];
        for (var i = 0; i < widths.Length; i++)
        {
            var c = companies[i];
            widths[i] = c.Column ? c.Width : Math.Max(c.MinWidth, Math.Min(c.MaxWidth, c.Width * stretch));
            across[i] = shape[i].Across * stretch;
        }

        ShoulderPass(companies, shape, widths, across);

        var centre = (across.Select((a, i) => a + widths[i] / 2.0).Max() + across.Select((a, i) => a - widths[i] / 2.0).Min()) / 2.0;
        var frontAlong = shape.Max(s => s.Along);
        var midX = ((double)beginX + endX) / 2.0;
        var midY = ((double)beginY + endY) / 2.0;
        var placements = new ArrayPlacement[companies.Count];
        for (var i = 0; i < placements.Length; i++)
        {
            var sideways = across[i] - centre;
            var back = shape[i].Along - frontAlong;
            placements[i] = new ArrayPlacement(companies[i].Company,
                (float)(midX + sideways * lineX + back * newForwardX), (float)(midY + sideways * lineY + back * newForwardY),
                (float)widths[i], (float)newForwardX, (float)newForwardY);
        }

        return placements;
    }

    private static void ShoulderPass(IReadOnlyList<ArrayCompany> companies, Shape[] shape, double[] widths, double[] across)
    {
        var order = Enumerable.Range(0, across.Length).OrderBy(i => across[i]).ToArray();
        for (var k = 1; k < order.Length; k++)
        {
            var i = order[k];
            for (var m = 0; m < k; m++)
            {
                var j = order[m];
                if (!BandsMeet(shape[i].Along, companies[i].Depth, shape[j].Along, companies[j].Depth))
                    continue;
                var gap = (across[i] - widths[i] / 2.0) - (across[j] + widths[j] / 2.0);
                if (gap < 0)
                    across[i] += MarshalTuning.GapInLine - gap;
            }
        }
    }

    private static bool BandsMeet(double alongA, double depthA, double alongB, double depthB) =>
        alongA - depthA < alongB && alongB - depthB < alongA;

    private static (double X, double Y) Origin(IReadOnlyList<ArrayCompany> companies)
    {
        var units = companies.Sum(c => (double)Math.Max(0, c.Units));
        if (units <= Epsilon)
            return (companies.Average(c => (double)c.X), companies.Average(c => (double)c.Y));
        return (companies.Sum(c => (double)c.X * Math.Max(0, c.Units)) / units, companies.Sum(c => (double)c.Y * Math.Max(0, c.Units)) / units);
    }

    private static (double X, double Y) Forward(IReadOnlyList<ArrayCompany> companies)
    {
        var largest = companies[0];
        foreach (var c in companies)
            if (c.Units > largest.Units)
                largest = c;
        return Normalised(largest.DirX, largest.DirY, 0.0, 1.0);
    }

    private static (double X, double Y) Normalised(double x, double y, double fallbackX, double fallbackY)
    {
        var length = Math.Sqrt(x * x + y * y);
        return length <= Epsilon ? (fallbackX, fallbackY) : (x / length, y / length);
    }

    private static bool IsUsable(ArrayCompany c) =>
        IsFinite(c.X, c.Y, c.Width, c.MinWidth, c.MaxWidth, c.Depth, c.DirX, c.DirY);

    private static bool IsFinite(params float[] values) => values.All(v => !float.IsNaN(v) && !float.IsInfinity(v));

    private static bool IsFinite(ArrayPlacement p) => IsFinite(p.X, p.Y, p.Width, p.DirX, p.DirY);
}
