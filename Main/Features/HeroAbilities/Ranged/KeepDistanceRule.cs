using System;
using System.Collections.Generic;

namespace TAOM.Features.HeroAbilities.Ranged;

/// <summary>
/// The rules of the ranged hero's step away from a melee threat that need no engine type (#1415,
/// docs/features/hero-abilities.md). HeroKeepDistance reads the field, these decide, and the engine's timed scripted move
/// (HumanAIComponent.SetScriptedPositionAndDirectionTimed) walks him there and releases him.
/// </summary>
public static class KeepDistanceRule
{
    /// <summary>How often the field is read for each hero.</summary>
    public const float TickSeconds = 0.25f;

    /// <summary>After a step, or a look that found no usable point, he is not moved again for this long.</summary>
    public const float CooldownSeconds = 3f;

    /// <summary>The scripted move ends after this long; then his formation's order moves him again (not proven in game).</summary>
    public const float HoldSeconds = 2.5f;

    /// <summary>A point more than this above or below him is another level (a courtyard below a wall).</summary>
    public const float MaxHeightMeters = 1.5f;

    /// <summary>An enemy more than this above or below him is no melee threat (an attacker below the wall).</summary>
    public const float ThreatHeightMeters = 2.5f;

    /// <summary>A point whose walking path is longer than this many times the straight line is refused (the factor vanilla's
    /// ArrangementOrder.CreateStrategicAreas applies, ArrangementOrder.cs:347; nothing calls it in v1.5.7).</summary>
    public const float PathFactor = 2f;

    // Straight away, then 45 degrees to each side, then 90 degrees to each side.
    private static readonly float[] Turns = { 0f, 45f, -45f, 90f, -90f };

    /// <summary>
    /// True when an enemy stands within keepDistance and the cooldown is over. Both distances must be above 0, so a kit
    /// with either at 0 never steps. Written as requirements, so a NaN anywhere refuses the step.
    /// </summary>
    public static bool ShouldStep(float nearestThreat, float keepDistance, float stepBack, float now, float nextAllowed)
    {
        if (!(keepDistance > 0f) || !(stepBack > 0f))
            return false;
        if (!(nearestThreat <= keepDistance))
            return false;
        return now >= nextAllowed;
    }

    /// <summary>
    /// The unit direction away from the threats, each weighted by 1 over its distance, so the nearest pushes hardest.
    /// Null when no threat gives a direction (none, one on top of him, or threats that cancel out); a NaN threat is skipped.
    /// </summary>
    public static (float X, float Y)? AwayFrom(float heroX, float heroY, IEnumerable<(float X, float Y)> threats)
    {
        var sumX = 0f;
        var sumY = 0f;
        foreach (var (tx, ty) in threats)
        {
            var dx = heroX - tx;
            var dy = heroY - ty;
            var squared = dx * dx + dy * dy;
            if (!(squared > 1e-6f) || float.IsInfinity(squared))
                continue;
            sumX += dx / squared;
            sumY += dy / squared;
        }

        var length = (float)Math.Sqrt(sumX * sumX + sumY * sumY);
        if (!(length > 1e-4f))
            return null;
        return (sumX / length, sumY / length);
    }

    /// <summary>The points to try, in order: stepBack metres straight away, then turned 45 and 90 degrees to each side.</summary>
    public static IEnumerable<(float X, float Y)> Candidates(float heroX, float heroY, float awayX, float awayY, float stepBack)
    {
        foreach (var degrees in Turns)
        {
            var radians = degrees * (float)Math.PI / 180f;
            var cos = (float)Math.Cos(radians);
            var sin = (float)Math.Sin(radians);
            var x = awayX * cos - awayY * sin;
            var y = awayX * sin + awayY * cos;
            yield return (heroX + x * stepBack, heroY + y * stepBack);
        }
    }

    /// <summary>The engine rotation (Vec2.RotationInRadians, Atan2(-x, y)) that faces back toward the threats.</summary>
    public static float FacingRadians(float awayX, float awayY) => (float)Math.Atan2(awayX, -awayY);

    /// <summary>
    /// True when he moved at least half the step along its line, from the start point toward the target. The check that
    /// the engine walked him, not only that the call ran. A step of no length, or a NaN, is never walked.
    /// </summary>
    public static bool Walked(float startX, float startY, float toX, float toY, float atX, float atY)
    {
        var stepX = toX - startX;
        var stepY = toY - startY;
        var length = (float)Math.Sqrt(stepX * stepX + stepY * stepY);
        if (!(length > 1e-3f))
            return false;
        var along = ((atX - startX) * stepX + (atY - startY) * stepY) / length;
        return along >= length / 2f;
    }
}
