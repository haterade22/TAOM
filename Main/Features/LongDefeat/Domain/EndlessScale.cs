using System;
using System.Collections.Generic;

namespace TAOM.Features.LongDefeat.Domain;

/// <summary>
/// How waves past the authored list grow beyond their count (phase2-plan.md section 2, "Endless scale"). With <c>extra</c> the waves
/// past the last authored one: health and damage factors of <c>min(MaxFactor, 1 + PerWave * extra)</c>, and one step along the
/// <see cref="Tiers"/> chain for every <see cref="TierEveryWaves"/> extra waves. Pure arithmetic; <see cref="None"/> changes nothing.
/// </summary>
internal sealed record EndlessScale(float HealthPerWave, float DamagePerWave, float MaxFactor, int TierEveryWaves, IReadOnlyDictionary<string, string> Tiers)
{
    /// <summary>Factors of 1 and no tier step ever: what a plan built with no growth gets.</summary>
    public static EndlessScale None { get; } = new(0f, 0f, 1f, int.MaxValue, new Dictionary<string, string>());

    /// <summary>Every troop a tier step can reach, for the launch check and the bounty table.</summary>
    public IEnumerable<string> Targets => Tiers.Values;

    public float HealthFactor(int extra) => Factor(HealthPerWave, extra);

    public float DamageFactor(int extra) => Factor(DamagePerWave, extra);

    /// <summary>How many links along the chain a mix row moves in a wave that is <paramref name="extra"/> waves past the list.</summary>
    public int TierSteps(int extra) => Math.Max(0, extra) / Math.Max(1, TierEveryWaves);

    /// <summary>The troop <paramref name="steps"/> links along the chain from <paramref name="troopId"/>, stopping at the end of the chain.</summary>
    public string Advance(string troopId, int steps)
    {
        for (var i = 0; i < steps && Tiers.TryGetValue(troopId, out var next); i++)
            troopId = next;
        return troopId;
    }

    private float Factor(float perWave, int extra) => (float)Math.Min(MaxFactor, 1.0 + (double)perWave * Math.Max(0, extra));
}
