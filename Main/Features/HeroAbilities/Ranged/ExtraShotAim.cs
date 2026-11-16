using System;
using TAOM.Features.FordsOfWar.Domain;

namespace TAOM.Features.HeroAbilities.Ranged;

/// <summary>
/// The arithmetic around the engine's vertical aim solver (Mission.GetMissileVerticalAimCorrection): the lead
/// point for a moving target and the launch direction for a solved angle. A NaN or infinite input gives null.
/// </summary>
internal static class ExtraShotAim
{
    private const float HalfPi = (float)(Math.PI / 2.0);

    // The native solver answers pi when the target is out of reach, else the asin of the solved direction; pi, any angle
    // past straight up, or a NaN is no usable shot.
    public static bool IsUsableAngle(float angle) => IsFinite(angle) && angle <= HalfPi && angle >= -HalfPi;

    /// <summary>Horizontal unit toward the aim times cos(angle), plus up times sin(angle).</summary>
    public static Vec3f? Direction(Vec3f origin, Vec3f aim, float angle)
    {
        if (!IsFinite(origin) || !IsFinite(aim) || !IsFinite(angle))
            return null;
        var dx = aim.X - origin.X;
        var dy = aim.Y - origin.Y;
        var horizontal = Math.Sqrt((double)dx * dx + (double)dy * dy);
        if (!(horizontal > 1e-4))
            return null;
        var cos = Math.Cos(angle);
        return new Vec3f((float)(dx / horizontal * cos), (float)(dy / horizontal * cos), (float)Math.Sin(angle));
    }

    /// <summary>The chest moved along its horizontal velocity for the flight time, solved twice (the second pass uses the first's range).</summary>
    public static Vec3f? LeadPoint(Vec3f origin, Vec3f chest, Vec3f vel, float speed, float angle)
    {
        if (!IsFinite(origin) || !IsFinite(chest) || !IsFinite(vel) || !IsFinite(speed) || !IsFinite(angle) || !(speed > 0f))
            return null;
        var horizontalSpeed = speed * Math.Cos(angle);
        if (!(horizontalSpeed > 1e-3))
            return null;

        var aim = chest;
        for (var pass = 0; pass < 2; pass++)
        {
            var t = origin.Distance2D(aim) / horizontalSpeed;
            aim = new Vec3f((float)(chest.X + vel.X * t), (float)(chest.Y + vel.Y * t), chest.Z);
        }
        return IsFinite(aim) ? aim : (Vec3f?)null;
    }

    private static bool IsFinite(Vec3f v) => IsFinite(v.X) && IsFinite(v.Y) && IsFinite(v.Z);

    private static bool IsFinite(float f) => !float.IsNaN(f) && !float.IsInfinity(f);
}
