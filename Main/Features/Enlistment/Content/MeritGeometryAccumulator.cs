using System;
using TAOM.Core.Validation;
using TAOM.Features.Enlistment.Content.Domain;

namespace TAOM.Features.Enlistment.Content;

/// <summary>
/// The merit sampler's geometry, lifted out of <c>EnlistmentMeritMissionBehavior</c> so it can be
/// unit-tested without a live <c>Mission</c> (ADR-002 — the behavior carried this algorithm inline
/// and sat over the 150-line ceiling because of it). <c>MeritGeometryScanner</c> keeps the
/// <c>Mission.Agents</c> scan, which needs the sealed engine types; everything downstream of
/// "how far away was each of the three things" lives here and touches no TaleWorlds type.
///
/// Distances arrive SQUARED, with a negative sentinel meaning "absent this tick" — no formation
/// captain, no allied hero on the field, no enemy left standing. Every threshold gate is written
/// as a positive requirement, so a non-finite distance out of the engine fails it instead of
/// scoring a free hit.
/// </summary>
public sealed class MeritGeometryAccumulator
{
    /// <summary>Threshold sentinel for an unusable configured distance — nothing can be within it.</summary>
    private const float UnreachableThreshold = -1f;

    private readonly float _cohesionSq;
    private readonly float _commanderSq;
    private readonly float _engagementSq;

    private float _enemyDistanceSum;
    private int _enemyDistanceSamples;

    public MeritGeometryAccumulator(MeritScoringConfig? config)
    {
        _cohesionSq = SquareOrUnreachable(config?.CohesionDistance ?? UnreachableThreshold);
        _commanderSq = SquareOrUnreachable(config?.CommanderDistance ?? UnreachableThreshold);
        _engagementSq = SquareOrUnreachable(config?.EngagementDistance ?? UnreachableThreshold);
    }

    public int Samples { get; private set; }

    public int CohesionHits { get; private set; }

    public int CommanderHits { get; private set; }

    public int EngagementHits { get; private set; }

    public float CohesionRatio => Ratio(CohesionHits);

    public float CommanderProximityRatio => Ratio(CommanderHits);

    public float EngagementRatio => Ratio(EngagementHits);

    /// <summary>Mean distance to the nearest enemy across samples; negative when never measured.</summary>
    public float AverageEnemyDistance =>
        _enemyDistanceSamples > 0 ? _enemyDistanceSum / _enemyDistanceSamples : -1f;

    /// <summary>
    /// Record one tick's geometry. Pass a negative value for any of the three that was not
    /// observable this tick (no captain, no allied hero, no enemy).
    /// </summary>
    public void AddSample(float captainDistanceSq, float alliedHeroDistanceSq, float enemyDistanceSq)
    {
        Samples++;

        if (Within(captainDistanceSq, _cohesionSq))
            CohesionHits++;
        if (Within(alliedHeroDistanceSq, _commanderSq))
            CommanderHits++;
        if (Within(enemyDistanceSq, _engagementSq))
            EngagementHits++;

        // Mean nearest-enemy distance drives the role-fit bands (archers hold a line, cavalry work
        // the flanks) — measured, not inferred from the engagement flag.
        if (Measured(enemyDistanceSq))
        {
            _enemyDistanceSum += (float)Math.Sqrt(enemyDistanceSq);
            _enemyDistanceSamples++;
        }
    }

    private float Ratio(int hits) => Samples <= 0 ? 0f : (float)hits / Samples;

    private static bool Within(float distanceSq, float thresholdSq) =>
        Measured(distanceSq) && distanceSq <= thresholdSq;

    private static bool Measured(float distanceSq) =>
        FiniteFloatValidator.IsFiniteAtLeast(distanceSq, 0f);

    private static float SquareOrUnreachable(float distance) =>
        FiniteFloatValidator.IsFiniteAtLeast(distance, 0f) ? distance * distance : UnreachableThreshold;
}
