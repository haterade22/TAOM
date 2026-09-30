using System;

namespace TAOM.Features.RealmBorders.Domain;

/// <summary>
/// A campaign-map position in world units (x east, y north). The border domain uses this rather than
/// TaleWorlds' <c>Vec2</c> so it runs without the engine, which lets every domain test run on hosted
/// CI; the renderer adapter converts at the boundary.
/// </summary>
public readonly record struct MapPoint(float X, float Y)
{
    public float Length => (float)Math.Sqrt(X * X + Y * Y);

    public float LengthSquared => X * X + Y * Y;

    /// <summary>This direction turned a quarter left: (-y, x).</summary>
    public MapPoint Left => new MapPoint(-Y, X);

    public MapPoint Normalized()
    {
        float length = Length;
        return length > 0f ? new MapPoint(X / length, Y / length) : default;
    }

    public float Dot(MapPoint other) => X * other.X + Y * other.Y;

    public static MapPoint operator +(MapPoint a, MapPoint b) => new MapPoint(a.X + b.X, a.Y + b.Y);

    public static MapPoint operator -(MapPoint a, MapPoint b) => new MapPoint(a.X - b.X, a.Y - b.Y);

    public static MapPoint operator *(MapPoint a, float scale) => new MapPoint(a.X * scale, a.Y * scale);
}
