using TAOM.Core.Validation;
using TaleWorlds.Library;

namespace TAOM.Features.CultureDoctrine.Doctrines;

/// <summary>
/// Is a cavalry formation riding at us? The engine's own test
/// (<c>FormationQuerySystem.IsUnderCavalryChargeFromFront</c>, `FormationQuerySystem.cs:646-663`)
/// asks it of one formation only, the closest significant enemy, and only when the wall already
/// faces it within about 41 degrees; a horse sweeping round a flank behind an infantry screen is
/// invisible to it. This test is the same geometry without the facing clause, asked of every
/// enemy cavalry formation: its velocity points at us (dot above <see cref="ClosingDot"/>), it
/// is moving (above <see cref="MinSpeed"/>), and it arrives within <see cref="HorizonSeconds"/>.
/// Pure; positions and velocities in, a verdict out, NaN fails every comparison.
/// </summary>
public static class CavalryThreat
{
    /// <summary>The engine's own cosine bar: the velocity within about 41 degrees of the line to us.</summary>
    public const float ClosingDot = 0.75f;

    /// <summary>The engine's own horizon: fifteen seconds at the formation's current speed.</summary>
    public const float HorizonSeconds = 15f;

    /// <summary>A formation slower than this (m/s) is not charging; vanilla's Advance uses 2.</summary>
    public const float MinSpeed = 2f;

    public static bool IsInbound(Vec2 ourPosition, Vec2 theirPosition, Vec2 theirVelocity)
    {
        if (!Finite(ourPosition) || !Finite(theirPosition) || !Finite(theirVelocity))
            return false;
        var speed = theirVelocity.Length;
        if (!(speed > MinSpeed))
            return false;
        var toUs = ourPosition - theirPosition;
        var distance = toUs.Length;
        if (!(distance > 1e-3f))
            return true;
        var closing = theirVelocity.DotProduct(toUs) / (speed * distance);
        return closing > ClosingDot && distance / speed < HorizonSeconds;
    }

    private static bool Finite(Vec2 v) => FiniteFloatValidator.IsFinite(v.x) && FiniteFloatValidator.IsFinite(v.y);
}
