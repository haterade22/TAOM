using System;
using TAOM.Core.Validation;
using TAOM.Features.FordsOfWar.Domain;

namespace TAOM.Features.FordsOfWar;

/// <summary>
/// Where a tower's garrison stands and how a volley is aimed. Pure maths on <see cref="Vec3f"/>, so the engine adapters
/// only convert. Positions use the tower slot's yaw, which points the way the owner's troops march, so the garrison
/// stands on the owner's side (behind the tower as seen from the enemy).
/// </summary>
internal static class FordsTowerGeometry
{
    /// <summary>Metres per second squared. The engine's arrow drop was not measured; an in-game check owes the number.</summary>
    public const float Gravity = 9.81f;

    /// <summary>How far above the tower's base position a volley leaves (an arrow loosed from the top of the barricade).</summary>
    public const float VolleyOriginHeight = 5f;

    private const float GunnerSetback = 2f;
    private const float GunnerSpacing = 1.2f;
    private const float QuarterTurn = (float)(Math.PI / 4d);

    public static Vec3f MarchDirection(TowerSlot slot) =>
        new((float)Math.Cos(slot.Yaw), (float)Math.Sin(slot.Yaw), 0f);

    /// <summary>The spot of gunner <paramref name="index"/> of <paramref name="count"/>: behind the tower, in a short row across the lane.</summary>
    public static Vec3f GunnerPosition(TowerSlot slot, int index, int count)
    {
        var forward = MarchDirection(slot);
        var side = new Vec3f(-forward.Y, forward.X, 0f);
        var offset = (index - (count - 1) / 2f) * GunnerSpacing;
        return slot.Position - forward * GunnerSetback + side * offset;
    }

    public static Vec3f VolleyOrigin(TowerSlot slot) => slot.Position + new Vec3f(0f, 0f, VolleyOriginHeight);

    /// <summary>The ground distance a missile of <paramref name="speed"/> flies at 45 degrees from level ground: speed squared over gravity. Zero for a speed that is not above zero.</summary>
    public static float Reach(float speed) => speed > 0f ? speed * speed / Gravity : 0f;

    /// <summary>
    /// A unit vector from <paramref name="origin"/> that lands a missile of <paramref name="speed"/> on
    /// <paramref name="target"/> under <see cref="Gravity"/>: the flat (low) ballistic arc. A target out of reach gets the
    /// 45 degree arc, which flies furthest. A target straight above or below gets a vertical shot. Null (no aim) when the
    /// origin, the target or the speed is not a finite number, so no NaN direction reaches the engine.
    /// </summary>
    public static Vec3f? AimDirection(Vec3f origin, Vec3f target, float speed)
    {
        var dx = target.X - origin.X;
        var dy = target.Y - origin.Y;
        var dz = target.Z - origin.Z;
        if (!FiniteFloatValidator.IsFinite(dx) || !FiniteFloatValidator.IsFinite(dy) || !FiniteFloatValidator.IsFinite(dz)
            || !FiniteFloatValidator.IsFinite(speed))
            return null;

        var flat = (float)Math.Sqrt(dx * dx + dy * dy);
        if (flat < 1e-3f || !(speed > 0f))
            return new Vec3f(0f, 0f, dz >= 0f ? 1f : -1f);

        var v2 = speed * speed;
        var discriminant = v2 * v2 - Gravity * (Gravity * flat * flat + 2f * dz * v2);
        var angle = discriminant < 0f
            ? QuarterTurn
            : (float)Math.Atan((v2 - Math.Sqrt(discriminant)) / (Gravity * flat));

        var cos = (float)Math.Cos(angle);
        return new Vec3f(dx / flat * cos, dy / flat * cos, (float)Math.Sin(angle));
    }
}
