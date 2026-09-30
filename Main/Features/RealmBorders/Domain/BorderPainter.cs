using System;
using System.Collections.Generic;

namespace TAOM.Features.RealmBorders.Domain;

/// <summary>
/// Paints one border line into quads for the renderer, in the look its <see cref="LinePaint"/> asks
/// for. Every look is strips from <see cref="StripBuilder"/>: the Atlas watercolour fades from the
/// line into each realm, its ink is the dash-dot pattern cut as real geometry (so no texture is needed
/// for it to read as a dotted border), the heraldic bands leave a gap on the line, the gold cord sits
/// on the player's own frontier, and a war front glows on both sides.
/// </summary>
public static class BorderPainter
{
    public static void Paint(IReadOnlyList<MapPoint> line, bool closed, LinePaint paint, BorderLook look, List<BorderQuad> into)
    {
        if (line == null)
            throw new ArgumentNullException(nameof(line));
        if (look == null)
            throw new ArgumentNullException(nameof(look));
        if (into == null)
            throw new ArgumentNullException(nameof(into));
        if (line.Count < 2)
            return;

        float s = look.WidthScale;
        switch (paint.Style)
        {
            case LineStyle.Atlas:
                Strip(line, closed, 0f, look.WashWidth * s, WithAlpha(paint.LeftColour, look.WashAlpha), WithAlpha(paint.LeftColour, 0f), into);
                Strip(line, closed, 0f, -look.WashWidth * s, WithAlpha(paint.RightColour, look.WashAlpha), WithAlpha(paint.RightColour, 0f), into);
                if (paint.Gilded)
                    GoldCord(line, closed, look, into);
                else
                    Ink(line, closed, look, into);
                break;

            case LineStyle.Heraldic:
                float g = look.BandGap / 2f * s, w = look.BandWidth * s, k = look.KeylineWidth * s;
                Strip(line, closed, g, g + w, WithAlpha(paint.LeftColour, look.BandAlpha), WithAlpha(paint.LeftColour, look.BandAlpha), into);
                Strip(line, closed, -g, -(g + w), WithAlpha(paint.RightColour, look.BandAlpha), WithAlpha(paint.RightColour, look.BandAlpha), into);
                Strip(line, closed, g + w, g + w + k, look.KeylineColour, look.KeylineColour, into);
                Strip(line, closed, -(g + w), -(g + w + k), look.KeylineColour, look.KeylineColour, into);
                if (paint.Gilded)
                    GoldCord(line, closed, look, into);
                break;

            case LineStyle.WarFront:
                float reach = look.EmberWidth * s * (paint.Emphasised ? 1.6f : 1f);
                float heat = paint.Emphasised ? 0.95f : 0.75f;
                Strip(line, closed, 0f, reach, WithAlpha(look.EmberColour, heat), WithAlpha(look.EmberColour, 0f), into);
                Strip(line, closed, 0f, -reach, WithAlpha(look.EmberColour, heat), WithAlpha(look.EmberColour, 0f), into);
                Strip(line, closed, -look.EmberCoreHalfWidth * s, look.EmberCoreHalfWidth * s, look.EmberCoreColour, look.EmberCoreColour, into);
                break;
        }
    }

    public static uint WithAlpha(uint argb, float alpha)
    {
        uint a = (uint)Math.Round(Math.Max(0f, Math.Min(1f, alpha)) * 255f);
        return (argb & 0x00FFFFFFu) | (a << 24);
    }

    private static void GoldCord(IReadOnlyList<MapPoint> line, bool closed, BorderLook look, List<BorderQuad> into)
    {
        float h = look.GoldHalfWidth * look.WidthScale;
        Strip(line, closed, 0f, h, look.GoldColour, look.GoldEdgeColour, into);
        Strip(line, closed, 0f, -h, look.GoldColour, look.GoldEdgeColour, into);
    }

    private static void Strip(IReadOnlyList<MapPoint> line, bool closed, float near, float far, uint nearColour, uint farColour, List<BorderQuad> into)
    {
        var strip = StripBuilder.Build(line, closed, near, far);
        for (int i = 0; i + 1 < strip.Near.Count; i++)
            into.Add(QuadBetween(strip, i, 0f, 1f, nearColour, farColour));
    }

    /// <summary>The ink line, cut into the dash, gap, dot, gap pattern measured along the line.</summary>
    private static void Ink(IReadOnlyList<MapPoint> line, bool closed, BorderLook look, List<BorderQuad> into)
    {
        float h = look.InkHalfWidth * look.WidthScale;
        var strip = StripBuilder.Build(line, closed, -h, h);
        float dash = look.DashLength, dot = look.DotLength, gap = look.PatternGap;
        float period = dash + gap + dot + gap;
        if (!(period > 0f))
            return;

        for (int i = 0; i + 1 < strip.Near.Count; i++)
        {
            float s0 = strip.V[i], s1 = strip.V[i + 1];
            if (!(s1 > s0))
                continue;
            for (double k = Math.Floor(s0 / period); k * period < s1; k++)
            {
                float start = (float)(k * period);
                AddOn(strip, i, s0, s1, start, start + dash, look.InkColour, into);
                AddOn(strip, i, s0, s1, start + dash + gap, start + dash + gap + dot, look.InkColour, into);
            }
        }
    }

    private static void AddOn(StripGeometry strip, int i, float s0, float s1, float onStart, float onEnd, uint colour, List<BorderQuad> into)
    {
        float a = Math.Max(s0, onStart), b = Math.Min(s1, onEnd);
        if (!(b > a))
            return;
        into.Add(QuadBetween(strip, i, (a - s0) / (s1 - s0), (b - s0) / (s1 - s0), colour, colour));
    }

    /// <summary>The part of segment i of a strip between fractions t0 and t1 along it.</summary>
    private static BorderQuad QuadBetween(StripGeometry strip, int i, float t0, float t1, uint nearColour, uint farColour)
    {
        MapPoint Lerp(MapPoint p, MapPoint q, float t) => p + (q - p) * t;
        float v0 = strip.V[i] + (strip.V[i + 1] - strip.V[i]) * t0;
        float v1 = strip.V[i] + (strip.V[i + 1] - strip.V[i]) * t1;
        return new BorderQuad(
            new BorderVertex(Lerp(strip.Near[i], strip.Near[i + 1], t0), nearColour, 0f, v0),
            new BorderVertex(Lerp(strip.Far[i], strip.Far[i + 1], t0), farColour, 1f, v0),
            new BorderVertex(Lerp(strip.Far[i], strip.Far[i + 1], t1), farColour, 1f, v1),
            new BorderVertex(Lerp(strip.Near[i], strip.Near[i + 1], t1), nearColour, 0f, v1));
    }
}
