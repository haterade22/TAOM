using TaleWorlds.MountAndBlade;
using TAOM.Features.Enlistment.Content;

namespace TAOM.Features.Enlistment.Hooks;

/// <summary>
/// The one part of merit sampling that has to touch the sealed engine types: per tick, how far the
/// player is from their formation captain, from the nearest allied hero, and from the nearest
/// enemy. It measures and hands the three squared distances straight to
/// <see cref="MeritGeometryAccumulator"/>, which owns every threshold and ratio — so the part that
/// cannot be unit-tested is this one method, and nothing else.
/// </summary>
internal static class MeritGeometryScanner
{
    /// <summary>Sentinel for a distance that was not observable this tick.</summary>
    private const float Absent = -1f;

    internal static void Sample(Mission? mission, Agent? main, MeritGeometryAccumulator? accumulator)
    {
        if (mission == null || main == null || accumulator == null)
            return;

        var position = main.Position;

        var captain = main.Formation?.Captain;
        var captainSq = captain != null && captain != main && captain.IsActive()
            ? captain.Position.DistanceSquared(position)
            : Absent;

        var nearestHeroSq = Absent;
        var nearestAllySq = Absent;
        var nearestEnemySq = Absent;
        foreach (var agent in mission.Agents)
        {
            if (agent == main || !agent.IsHuman || !agent.IsActive() || agent.Team == null)
                continue;

            // Positive requirement: a non-finite distance is dropped rather than latched as the
            // running minimum, where it would poison every later comparison against it.
            var distanceSq = agent.Position.DistanceSquared(position);
            if (!(distanceSq >= 0f))
                continue;

            if (agent.Team.IsEnemyOf(main.Team))
            {
                if (nearestEnemySq < 0f || distanceSq < nearestEnemySq)
                    nearestEnemySq = distanceSq;
                continue;
            }

            // Every ally, not just the heroes among them. The hero reading is the commander-proximity
            // input and stays separate; this one is the cohesion fallback for a player whose
            // formation has no captain to measure against.
            if (nearestAllySq < 0f || distanceSq < nearestAllySq)
                nearestAllySq = distanceSq;

            if (agent.IsHero && (nearestHeroSq < 0f || distanceSq < nearestHeroSq))
                nearestHeroSq = distanceSq;
        }

        // Named arguments, because this call is the one place the whole cohesion fix can break
        // silently. `nearestAllySq` and `nearestHeroSq` are same-typed locals feeding different
        // gates, and swapping them compiles, reads plausibly, and is invisible to the suite: nothing
        // in TAOM.Tests reaches this method (there is no InternalsVisibleTo and no Mission to build),
        // so the mapping is pinned by review rather than by a test. Naming it is what makes a swap
        // visible in a diff.
        accumulator.AddSample(
            captainDistanceSq: MeritGeometryAccumulator.CohesionDistanceSq(captainSq, nearestAllySq),
            alliedHeroDistanceSq: nearestHeroSq,
            enemyDistanceSq: nearestEnemySq);
    }
}
