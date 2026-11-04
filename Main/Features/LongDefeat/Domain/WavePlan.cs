using System;
using System.Collections.Generic;
using System.Linq;

namespace TAOM.Features.LongDefeat.Domain;

/// <summary>The waves of a roster: the authored list, then the last one scaled. No randomness, so a wave is the same every run.</summary>
internal sealed class WavePlan
{
    private readonly IReadOnlyList<WaveSpec> _waves;
    private readonly WaveScaling _scaling;

    public WavePlan(IReadOnlyList<WaveSpec> waves, WaveScaling scaling)
    {
        if (waves.Count == 0)
            throw new ArgumentException("a wave plan needs at least one wave", nameof(waves));
        _waves = waves;
        _scaling = scaling;
    }

    public IReadOnlyList<WaveSpec> Authored => _waves;

    /// <summary>The wave's spec; a number below 1 is wave 1.</summary>
    public WaveSpec ForWave(int wave)
    {
        wave = Math.Max(1, wave);
        if (wave <= _waves.Count)
            return _waves[wave - 1];

        var last = _waves[_waves.Count - 1];
        var extra = wave - _waves.Count;
        var count = (int)Math.Min(_scaling.MaxCount, last.Count + (long)_scaling.CountPerWave * extra);
        return last with { Count = count };
    }

    /// <summary>
    /// The wave as a queue of soldiers, ranged first so the archers reach their formation before the line. Each row gets its
    /// weighted share rounded down, and the leftover soldiers go to the largest remainders, earlier rows first on a tie.
    /// </summary>
    public IReadOnlyList<WaveSoldier> Expand(int wave)
    {
        var spec = ForWave(wave);
        var total = spec.Mix.Sum(m => (long)m.Weight);
        if (total <= 0 || spec.Count <= 0)
            return Array.Empty<WaveSoldier>();

        var shares = spec.Mix.Select(m => (int)(spec.Count * (long)m.Weight / total)).ToArray();
        var leftover = spec.Count - shares.Sum();
        var byRemainder = Enumerable.Range(0, spec.Mix.Count)
            .OrderByDescending(i => spec.Count * (long)spec.Mix[i].Weight % total)
            .ThenBy(i => i);
        foreach (var i in byRemainder.Take(leftover))
            shares[i]++;

        return spec.Mix
            .Select((m, i) => (m, n: shares[i]))
            .OrderBy(x => x.m.Slot == FormationSlot.Ranged ? 0 : 1)
            .SelectMany(x => Enumerable.Repeat(new WaveSoldier(x.m.TroopId, x.m.Slot), x.n))
            .ToList();
    }
}
