using System;
using System.Collections.Generic;
using TAOM.Adapters;
using TAOM.Features.LongDefeat.Domain;

namespace TAOM.Tests.Features.LongDefeat;

/// <summary>
/// The effects adapter with no engine. It writes into the field fake's call list when given one, so a test can read the order of a
/// publish against a spawn, and answers the scale read from a number the test sets.
/// </summary>
internal sealed class FakeLongDefeatEffects : ILongDefeatEffectsAdapter
{
    private readonly List<string> _calls;

    public FakeLongDefeatEffects(List<string>? calls = null) => _calls = calls ?? new List<string>();

    public List<LdStatTable> Published { get; } = new();

    public int Reads { get; private set; }

    /// <summary>The property lists the director asked ReadStats for, in order.</summary>
    public List<IReadOnlyList<string>> StatsAsked { get; } = new();

    /// <summary>What a stat read answers; no attacker by default.</summary>
    public Func<IReadOnlyList<string>, LdStatSample> StatsFor { get; set; } = _ => LdStatSample.None;

    /// <summary>What a scale read answers: a standing attacker at the factor, 40 blows amplified.</summary>
    public Func<int, LdScaleSample> SampleFor { get; set; } = _ => new LdScaleSample("orc", 110f, 100f, 40);

    public void PublishStats(LdStatTable table)
    {
        Published.Add(table);
        _calls.Add("Publish:" + table.AttackerDamage.ToString("F2", System.Globalization.CultureInfo.InvariantCulture));
    }

    public LdStatSample ReadStats(IReadOnlyList<string> stats)
    {
        StatsAsked.Add(stats);
        _calls.Add("ReadStats");
        return StatsFor(stats);
    }

    /// <summary>What a target read answers; by default a soldier of the group standing at the health limit and stat baselines the boons owned give, so a correct run passes.</summary>
    public Func<BoonTarget, bool, IReadOnlyList<string>, LdTargetSample> TargetFor { get; set; } = (_, _, _) => LdTargetSample.None;

    public List<(BoonTarget Target, bool Newest, IReadOnlyList<string> Stats)> TargetReads { get; } = new();

    public LdTouched RefreshStats(BoonTarget target)
    {
        _calls.Add("RefreshStats:" + target);
        return new LdTouched(7, 0.5);
    }

    public LdTouched ScaleHealth(BoonTarget target, float factor)
    {
        _calls.Add($"ScaleHealth:{target}:{factor.ToString("F2", System.Globalization.CultureInfo.InvariantCulture)}");
        return new LdTouched(7, 0.5);
    }

    public void SetPlayerHealth(float factor) =>
        _calls.Add("SetPlayerHealth:" + factor.ToString("F2", System.Globalization.CultureInfo.InvariantCulture));

    public LdTargetSample ReadTarget(BoonTarget target, IReadOnlyList<string> stats, bool newest)
    {
        TargetReads.Add((target, newest, stats));
        _calls.Add($"ReadTarget:{target}:{(newest ? "newest" : "oldest")}");
        return TargetFor(target, newest, stats);
    }

    public LdScaleSample ReadScale()
    {
        Reads++;
        _calls.Add("ReadScale");
        return SampleFor(Reads);
    }
}
