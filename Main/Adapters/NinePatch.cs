using System;

namespace TAOM.Adapters;

/// <summary>
/// A runtime sprite's nine-patch borders in pixels, in the order the engine's
/// <c>SpriteNinePatchParameters(leftWidth, rightWidth, topHeight, bottomHeight)</c> takes them, which is
/// also the order Kysaro's <c>.nine</c> files list them in (#704).
/// </summary>
public readonly struct NinePatch : IEquatable<NinePatch>
{
    public NinePatch(int left, int right, int top, int bottom)
    {
        Left = left;
        Right = right;
        Top = top;
        Bottom = bottom;
    }

    public int Left { get; }

    public int Right { get; }

    public int Top { get; }

    public int Bottom { get; }

    public bool Equals(NinePatch other) =>
        Left == other.Left && Right == other.Right && Top == other.Top && Bottom == other.Bottom;

    public override bool Equals(object? obj) => obj is NinePatch other && Equals(other);

    public override int GetHashCode() => ((Left * 397 ^ Right) * 397 ^ Top) * 397 ^ Bottom;

    public override string ToString() => $"{Left} {Right} {Top} {Bottom}";
}
