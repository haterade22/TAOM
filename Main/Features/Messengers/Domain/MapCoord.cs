using System;

namespace TAOM.Features.Messengers.Domain;

/// <summary>
/// TAOM-owned 2D map coordinate. Service / domain layers use this struct so they remain free of
/// TaleWorlds.Library types (Codex review #34, ADR-007 boundary).
/// The boundary (MessengerCampaignBehavior) converts to/from TaleWorlds.Library.Vec2.
/// NaN is the sentinel for "invalid" — matches Vec2's convention.
/// </summary>
public readonly struct MapCoord
{
    public float X { get; }
    public float Y { get; }

    public MapCoord(float x, float y)
    {
        X = x;
        Y = y;
    }

    public static MapCoord Invalid => new MapCoord(float.NaN, float.NaN);
    public static MapCoord Zero => new MapCoord(0f, 0f);

    // Matches TaleWorlds.Library.Vec2.IsValid semantics: rejects both NaN and ±Infinity.
    // Codex review #34 round 2 caught the parity gap — Vec2.IsValid uses !float.IsNaN(x) && !float.IsInfinity(x).
    public bool IsValid =>
        !float.IsNaN(X) && !float.IsInfinity(X) &&
        !float.IsNaN(Y) && !float.IsInfinity(Y);

    public float Length
    {
        get
        {
            if (!IsValid) return 0f;
            return (float)Math.Sqrt(X * X + Y * Y);
        }
    }

    public MapCoord Normalized()
    {
        var len = Length;
        if (len <= 0f || float.IsNaN(len)) return Zero;
        return new MapCoord(X / len, Y / len);
    }

    public static MapCoord operator +(MapCoord a, MapCoord b) => new MapCoord(a.X + b.X, a.Y + b.Y);
    public static MapCoord operator -(MapCoord a, MapCoord b) => new MapCoord(a.X - b.X, a.Y - b.Y);
    public static MapCoord operator *(MapCoord a, float s) => new MapCoord(a.X * s, a.Y * s);
}
