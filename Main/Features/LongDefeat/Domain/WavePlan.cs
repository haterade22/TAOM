using System;
using System.Collections.Generic;
using System.Linq;

namespace TAOM.Features.LongDefeat.Domain;

/// <summary>
/// The waves of a roster: the authored list, then the last one grown: more soldiers, the <see cref="EndlessScale"/> health and damage
/// factors, and troops stepped up its tier chain. No randomness, so a wave is the same every run.
/// </summary>
internal sealed class WavePlan
{
    private readonly IReadOnlyList<WaveSpec> _waves;
    private readonly WaveScaling _scaling;

    /// <param name="endless">How the waves past the list grow beyond their count; null changes nothing (factors 1, no tier links).</param>
    /// <param name="commanders">Which waves bring a commander and what he does; null: none does.</param>
    public WavePlan(IReadOnlyList<WaveSpec> waves, WaveScaling scaling, EndlessScale? endless = null, CommanderPlan? commanders = null)
    {
        if (waves.Count == 0)
            throw new ArgumentException("a wave plan needs at least one wave", nameof(waves));
        _waves = waves;
        _scaling = scaling;
        Endless = endless ?? EndlessScale.None;
        Commanders = commanders ?? CommanderPlan.None;
    }

    public IReadOnlyList<WaveSpec> Authored => _waves;

    public EndlessScale Endless { get; }

    public CommanderPlan Commanders { get; }

    /// <summary>The smallest count of any authored wave as this plan sends it, so a hard plan carries its factor.</summary>
    public int SmallestAuthoredCount => Enumerable.Range(1, _waves.Count).Min(w => ForWave(w).Count);

    /// <summary>The hardest count a hard run asks for, the top of waves.json "maxCount".</summary>
    private const int HardMaxCount = 1000;

    private float _countFactor = 1f;

    /// <summary>
    /// The same waves on hard (waves.json <c>rules.hard</c>): every count is the normal count (after its own cap) times <paramref name="factor"/>, rounded once, half away
    /// from zero, and the health and damage growth per wave is multiplied by it. The cap on those factors and the tier steps stay. A factor of 1 returns this plan.
    /// </summary>
    public WavePlan Harder(float factor)
    {
        if (factor == 1f)
            return this;

        var growth = Endless with { HealthPerWave = (float)(Endless.HealthPerWave * factor), DamagePerWave = (float)(Endless.DamagePerWave * factor) };
        return new WavePlan(_waves, _scaling, growth, Commanders) { _countFactor = _countFactor * factor };
    }

    /// <summary>The wave's spec; a number below 1 is wave 1.</summary>
    public WaveSpec ForWave(int wave)
    {
        var spec = NormalWave(wave);
        if (_countFactor == 1f)
            return spec;

        var scaled = (int)Math.Min(HardMaxCount, Math.Round(spec.Count * (double)_countFactor, MidpointRounding.AwayFromZero));
        return spec with { Count = scaled };
    }

    private WaveSpec NormalWave(int wave)
    {
        wave = Math.Max(1, wave);
        if (wave <= _waves.Count)
            return _waves[wave - 1];

        var last = _waves[_waves.Count - 1];
        var extra = wave - _waves.Count;
        var count = (int)Math.Min(_scaling.MaxCount, last.Count + (long)_scaling.CountPerWave * extra);
        var steps = Endless.TierSteps(extra);
        return new WaveSpec(count, StepMix(last.Mix, steps), Endless.HealthFactor(extra), Endless.DamageFactor(extra), steps);
    }

    /// <summary>
    /// The mix with every row moved <paramref name="steps"/> links along the tier chain. Rows that land on one troop (and one class) merge their
    /// weights into the first of them, so the total never changes.
    /// </summary>
    private IReadOnlyList<WaveTroop> StepMix(IReadOnlyList<WaveTroop> mix, int steps)
    {
        if (steps <= 0 || Endless.Tiers.Count == 0)
            return mix;

        var merged = new List<WaveTroop>();
        foreach (var row in mix)
        {
            var troop = Endless.Advance(row.TroopId, steps);
            var at = merged.FindIndex(m => m.TroopId == troop && m.Slot == row.Slot);
            if (at < 0)
                merged.Add(row with { TroopId = troop });
            else
                merged[at] = merged[at] with { Weight = merged[at].Weight + row.Weight };
        }

        return merged;
    }

    /// <summary>
    /// The wave as a queue of soldiers, ranged first so the archers reach their formation before the line. Each row gets its
    /// weighted share rounded down, and the leftover soldiers go to the largest remainders, earlier rows first on a tie.
    /// </summary>
    public IReadOnlyList<WaveSoldier> Expand(int wave)
    {
        var spec = ForWave(wave);
        return Expand(spec.Count, spec.Mix);
    }

    /// <summary>The same split for any count and mix, so a later summon uses the wave's own rule.</summary>
    public static IReadOnlyList<WaveSoldier> Expand(int count, IReadOnlyList<WaveTroop> mix)
    {
        var total = mix.Sum(m => (long)m.Weight);
        if (total <= 0 || count <= 0)
            return Array.Empty<WaveSoldier>();

        var shares = mix.Select(m => (int)(count * (long)m.Weight / total)).ToArray();
        var leftover = count - shares.Sum();
        var byRemainder = Enumerable.Range(0, mix.Count)
            .OrderByDescending(i => count * (long)mix[i].Weight % total)
            .ThenBy(i => i);
        foreach (var i in byRemainder.Take(leftover))
            shares[i]++;

        return mix
            .Select((m, i) => (m, n: shares[i]))
            .OrderBy(x => x.m.Slot == FormationSlot.Ranged ? 0 : 1)
            .SelectMany(x => Enumerable.Repeat(new WaveSoldier(x.m.TroopId, x.m.Slot), x.n))
            .ToList();
    }
}
