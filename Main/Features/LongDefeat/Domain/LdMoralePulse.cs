using System;
using System.Collections.Generic;

namespace TAOM.Features.LongDefeat.Domain;

/// <summary>A place on the ground, in metres. The pulse measures across the ground, so a defender on a wall is in reach of an attacker at its foot.</summary>
internal readonly record struct LdPulseSpot(float X, float Y);

/// <summary>What one pulse did, counted inside the call: defenders looked at, defenders that lost morale, the morale lost in all, and defenders left below the floor. <c>Milliseconds</c> is the adapter's own clock.</summary>
internal sealed record LdPulseResult(int Defenders, int Drained, float Lost, int PushedBelow, double Milliseconds = 0d);

/// <summary>
/// Winged Terror's pulse (phase2-plan.md section 2, decision D7): a defender within <see cref="Radius"/> of a standing attacker loses <see cref="Amount"/> morale, never taking it below
/// <see cref="Floor"/>, and one already at or under the floor is left alone, so the pulse alone never routs anyone and routs do not cascade. Pure: the adapter hands in the spots,
/// the morale reads and the write, so the arithmetic and the counts are tested without an engine.
/// </summary>
internal sealed record LdMoralePulse(float Radius, float Amount, float Floor)
{
    /// <summary>A morale this far under the floor after the write is a push below it; less is the engine's float noise from adding a negative delta.</summary>
    internal const float Tolerance = 0.001f;

    /// <summary>The morale a defender at <paramref name="morale"/> loses: the amount, held to the headroom over the floor, and nothing at or under it (or for a morale that is not a number).</summary>
    public float Drain(float morale) => morale > Floor ? Math.Min(Amount, morale - Floor) : 0f;

    /// <summary>
    /// Runs the pulse over <paramref name="defenders"/>: for each one above the floor and within the radius of some attacker, <paramref name="lower"/> takes the drain off and the morale is read
    /// back, so a defender left more than <see cref="Tolerance"/> below the floor is counted as pushed below it in this same call.
    /// </summary>
    public LdPulseResult Apply<T>(IReadOnlyList<LdPulseSpot> attackers, IEnumerable<T> defenders, Func<T, LdPulseSpot> where, Func<T, float> morale, Action<T, float> lower)
    {
        var reach = Radius * Radius;
        int seen = 0, drained = 0, pushed = 0;
        var lost = 0f;
        foreach (var defender in defenders)
        {
            seen++;
            var before = morale(defender);
            var drain = Drain(before);
            if (!(drain > 0f) || !Reached(where(defender), attackers, reach))
                continue;

            lower(defender, drain);
            var after = morale(defender);
            drained++;
            lost += before - after;
            if (after < Floor - Tolerance)
                pushed++;
        }

        return new LdPulseResult(seen, drained, lost, pushed);
    }

    private static bool Reached(LdPulseSpot at, IReadOnlyList<LdPulseSpot> attackers, float reach)
    {
        for (var i = 0; i < attackers.Count; i++)
        {
            var dx = attackers[i].X - at.X;
            var dy = attackers[i].Y - at.Y;
            if (dx * dx + dy * dy <= reach)
                return true;
        }

        return false;
    }
}

/// <summary>A Winged Terror wave: a pulse every <see cref="PulseSeconds"/> of the wave's seconds.</summary>
internal sealed record LdTerror(float PulseSeconds, LdMoralePulse Pulse);
