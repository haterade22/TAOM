using System;

namespace TAOM.Features.FordsOfWar.Domain;

/// <summary>
/// A world position or offset that does not drag an engine type into the rules. X and Y are the ground plane, Z is height;
/// the 2D helpers ignore Z.
/// </summary>
internal readonly record struct Vec3f(float X, float Y, float Z)
{
    public float Distance(Vec3f other)
    {
        var dx = X - other.X;
        var dy = Y - other.Y;
        var dz = Z - other.Z;
        return (float)Math.Sqrt(dx * dx + dy * dy + dz * dz);
    }

    public float Distance2D(Vec3f other)
    {
        var dx = X - other.X;
        var dy = Y - other.Y;
        return (float)Math.Sqrt(dx * dx + dy * dy);
    }

    /// <summary>The point <paramref name="t"/> of the way from a to b; t is clamped to 0..1.</summary>
    public static Vec3f Lerp(Vec3f a, Vec3f b, float t)
    {
        if (t <= 0f)
            return a;
        if (t >= 1f)
            return b;
        return new Vec3f(a.X + (b.X - a.X) * t, a.Y + (b.Y - a.Y) * t, a.Z + (b.Z - a.Z) * t);
    }

    public static Vec3f operator +(Vec3f a, Vec3f b) => new(a.X + b.X, a.Y + b.Y, a.Z + b.Z);

    public static Vec3f operator -(Vec3f a, Vec3f b) => new(a.X - b.X, a.Y - b.Y, a.Z - b.Z);

    public static Vec3f operator *(Vec3f a, float s) => new(a.X * s, a.Y * s, a.Z * s);

    public static Vec3f operator *(float s, Vec3f a) => a * s;
}
