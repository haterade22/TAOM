using System;
using System.Collections.Generic;
using TAOM.Adapters;
using TAOM.Features.FordsOfWar.Domain;
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

    /// <summary>The factor of every engine write, in order.</summary>
    public List<float> EngineFactors { get; } = new();

    /// <summary>What an engine write answers: by default a ram and a tower at the vanilla hit points times the factor, and one gate standing.</summary>
    public Func<float, LdEngineReport> EnginesFor { get; set; } = factor =>
        new LdEngineReport(new[] { new LdEngineReading("ram", 3000f, 3000f * factor, 3000f * factor), new LdEngineReading("tower", 5000f, 5000f * factor, 5000f * factor) }, 1, 0);

    public LdEngineReport SetEngineHitPoints(float factor)
    {
        EngineFactors.Add(factor);
        _calls.Add("Engines:" + factor.ToString("F2", System.Globalization.CultureInfo.InvariantCulture));
        return EnginesFor(factor);
    }

    /// <summary>The pulses the director asked for, in order.</summary>
    public List<LdMoralePulse> Pulses { get; } = new();

    /// <summary>What a pulse answers: 30 of 200 defenders drained, none below the floor, half a millisecond.</summary>
    public Func<LdPulseResult> PulseFor { get; set; } = () => new LdPulseResult(200, 30, 90f, 0, 0.5);

    public LdPulseResult PulseMorale(LdMoralePulse pulse)
    {
        Pulses.Add(pulse);
        _calls.Add("Pulse");
        return PulseFor();
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

    /// <summary>What a commander read answers: a commander standing at the factor the test sets (the rank's), or none.</summary>
    public Func<LdScaleSample> CommanderFor { get; set; } = () => new LdScaleSample("", 0f, 0f, 0);

    public int AttackerRefreshes { get; private set; }

    public LdTouched RefreshAttackers()
    {
        AttackerRefreshes++;
        _calls.Add("RefreshAttackers");
        return new LdTouched(9, 0.25);
    }

    public LdScaleSample ReadCommander()
    {
        _calls.Add("ReadCommander");
        return CommanderFor();
    }

    public string DescribeCommander() => "troop=fake at=(0.0, 0.0, 0.0)";

    /// <summary>The poison ticks the director asked for, in order: victim, poisoner, damage.</summary>
    public List<(AgentKey Victim, AgentKey Poisoner, int Damage)> Poisoned { get; } = new();

    /// <summary>What a poison tick answers: the whole damage taken, nobody killed, the poisoner owning it.</summary>
    public Func<AgentKey, AgentKey, int, LdPoisonResult> PoisonFor { get; set; } = (_, _, damage) => new LdPoisonResult(damage, false, false, false, false);

    public LdPoisonResult ApplyPoison(AgentKey victim, AgentKey poisoner, int damage)
    {
        Poisoned.Add((victim, poisoner, damage));
        _calls.Add($"Poison:{victim.Value}:{poisoner.Value}:{damage}");
        return PoisonFor(victim, poisoner, damage);
    }

    public LdScaleSample ReadScale()
    {
        Reads++;
        _calls.Add("ReadScale");
        return SampleFor(Reads);
    }
}
