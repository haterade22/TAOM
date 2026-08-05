using System;
using System.Collections.Generic;
using TAOM.Features.Enlistment.Content.Domain;

namespace TAOM.Features.Enlistment.Content;

/// <summary>Raw mission-side merit inputs (ratios pre-computed by the sampler; all 0..1).</summary>
public sealed class MeritSample
{
    public int Kills { get; set; }
    public float SurvivalRatio { get; set; }
    public float CohesionRatio { get; set; }
    public float CommanderProximityRatio { get; set; }
    public float EngagementRatio { get; set; }
    public bool FellEarly { get; set; }
    public bool RoleFit { get; set; }
}

/// <summary>
/// Pure battle-merit scoring (the donor's formula, config-typed): capped kills + weighted
/// ratios + role fit − fell-early penalty, clamped 0-100. Non-finite ratios score as 0
/// contribution (NaN must fail the gate, never inflate it).
/// </summary>
public static class BattleMeritScorer
{
    public static int Score(MeritSample sample, MeritScoringConfig config)
    {
        if (sample == null || config == null)
            return 0;

        var score = 0f;
        score += Math.Min(Math.Max(0, sample.Kills), config.KillCountCap) * config.KillWeight;
        score += SafeRatio(sample.SurvivalRatio) * config.SurvivalWeight;
        score += SafeRatio(sample.CohesionRatio) * config.CohesionWeight;
        score += SafeRatio(sample.CommanderProximityRatio) * config.CommanderProximityWeight;
        score += SafeRatio(sample.EngagementRatio) * config.EngagementWeight;
        if (sample.RoleFit)
            score += config.RoleFitBonus;
        if (sample.FellEarly)
            score -= config.FellEarlyPenalty;

        return (int)Math.Max(0f, Math.Min(100f, score));
    }

    /// <summary>First band whose MinScore &lt;= score (bands are validated descending-unique ending at 0).</summary>
    public static MeritBand ResolveBand(int score, IReadOnlyList<MeritBand> bands)
    {
        if (bands == null || bands.Count == 0)
            return null;
        foreach (var band in bands)
        {
            if (score >= band.MinScore)
                return band;
        }
        return bands[bands.Count - 1];
    }

    private static float SafeRatio(float value)
    {
        if (float.IsNaN(value) || float.IsInfinity(value))
            return 0f;
        return Math.Max(0f, Math.Min(1f, value));
    }
}
