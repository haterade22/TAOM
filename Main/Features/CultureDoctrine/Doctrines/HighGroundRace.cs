using System;
using System.Collections.Generic;
using TAOM.Core.Validation;

namespace TAOM.Features.CultureDoctrine.Doctrines;

/// <summary>How long a formation needs to settle into its arrangement once it arrives, and the
/// safety margin the race is decided with. Per plan: a ring around a square takes longer than
/// a line.</summary>
public readonly struct RaceTunables
{
    public RaceTunables(float formUpSeconds, float formUpSecondsPerUnit, float marginSeconds)
    {
        FormUpSeconds = formUpSeconds;
        FormUpSecondsPerUnit = formUpSecondsPerUnit;
        MarginSeconds = marginSeconds;
    }

    public float FormUpSeconds { get; }
    public float FormUpSecondsPerUnit { get; }
    public float MarginSeconds { get; }

    public float FormUpFor(int units) => FormUpSeconds + FormUpSecondsPerUnit * Math.Max(0, units);
}

/// <summary>
/// Can a formation reach a position and form up there before the enemy's foot arrives? Vanilla
/// never asks: <c>TacticDefensiveEngagement</c> lowers its weight when the high ground is far
/// and <c>BehaviorHoldHighGround</c> tracks then locks, so a wall that cannot make it is caught
/// on the march. Pure: distances and speeds in, a verdict out; every comparison is a positive
/// requirement so a NaN on our side holds and a NaN on theirs is not a racer.
/// </summary>
public static class HighGroundRace
{
    /// <summary>Seconds for a formation to cover <paramref name="distance"/> at
    /// <paramref name="speed"/>; infinite when it cannot move.</summary>
    public static float Eta(float distance, float speed)
    {
        if (!FiniteFloatValidator.IsFinite(distance))
            return float.NaN;
        return speed > 0f ? distance / speed : float.PositiveInfinity;
    }

    /// <summary>True when our arrival plus form-up plus margin beats the earliest finite enemy ETA
    /// (or there is none). <paramref name="enemyEtas"/>: seconds until each enemy foot or archer
    /// formation reaches the position; cavalry is not passed in.</summary>
    public static bool Decide(float ourDistance, float ourSpeed, int ourUnits, in RaceTunables tunables, IReadOnlyList<float> enemyEtas)
    {
        // Already there: staying is the same as going, whoever else is coming.
        if (!(ourDistance > 0f))
            return FiniteFloatValidator.IsFinite(ourDistance);
        var ours = Eta(ourDistance, ourSpeed);
        if (!FiniteFloatValidator.IsFinite(ours))
            return false;
        var ready = ours + tunables.FormUpFor(ourUnits) + tunables.MarginSeconds;
        for (var i = 0; i < enemyEtas.Count; i++)
        {
            var theirs = enemyEtas[i];
            if (!FiniteFloatValidator.IsFinite(theirs))
                continue;
            if (!(ready < theirs))
                return false;
        }
        return true;
    }
}
