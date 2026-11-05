using System.Collections.Generic;
using TAOM.Adapters;
using TAOM.Core.Logging;
using TAOM.Features.MarshalsWord.Domain;

namespace TAOM.Features.MarshalsWord;

/// <summary>
/// The proof of build-spec 8.1: once a second it samples the player's companies that hold a mark, writes what the soldiers aim at, and judges a
/// ranged company on Stop once the mark has stood <c>MarkSettleSeconds</c>.
/// </summary>
internal sealed class MarshalMarkWatch
{
    private readonly IMarshalOrderAdapter _orders;
    private readonly IOutcomeLog _outcomes;
    private readonly IModLogger _logger;
    private readonly string _key;
    private readonly Dictionary<(int Company, int Target), double> _since = new();
    private readonly List<(int Company, int Target)> _gone = new();
    private readonly Dictionary<int, MarkSample> _latest = new();
    private readonly Dictionary<int, MarkVerdict> _judged = new();
    private double _nextPass = double.NegativeInfinity;

    internal MarshalMarkWatch(IMarshalOrderAdapter orders, IOutcomeLog outcomes, IModLogger logger, string missionKey)
    {
        _orders = orders;
        _outcomes = outcomes;
        _logger = logger;
        _key = missionKey;
    }

    internal void Pass()
    {
        var now = _orders.Now;
        if (now < _nextPass)
            return;
        _nextPass = now + MarshalTuning.MarkPassSeconds;

        var samples = _orders.ReadMarks();
        Forget(samples);
        _latest.Clear();
        foreach (var sample in samples)
        {
            var pair = (sample.Company, sample.Target);
            _latest[sample.Company] = sample;
            if (!_since.TryGetValue(pair, out var since))
                _since[pair] = since = now;
            _logger.LogInfo("[Marshal] mark company=" + Count(sample.Company) + " target=" + Count(sample.Target) + " onTarget=" + Count(sample.OnTarget)
                + " of=" + Count(sample.Of));
            if (now - since >= MarshalTuning.MarkSettleSeconds && MarshalMarkRules.Judgeable(sample))
                Judge(sample);
        }
    }

    /// <summary>The player's team changed, so each company number names a formation of the old team.</summary>
    internal void OnPlayerTeamChanged()
    {
        _since.Clear();
        _latest.Clear();
        _judged.Clear();
    }

    /// <summary>The latest read of a marked company, or null when it has no target company.</summary>
    internal MarkSample? LatestOf(int company) => _latest.TryGetValue(company, out var sample) ? sample : null;

    /// <summary>The latest verdict written for a marked company, the same one the check line carries, or null while the mark has not been judged or after it was cleared.</summary>
    internal MarkVerdict? JudgedOf(int company) => _judged.TryGetValue(company, out var verdict) ? verdict : null;

    private void Judge(MarkSample sample)
    {
        var detail = "mission=" + _key + " company=" + Count(sample.Company) + " target=" + Count(sample.Target) + " context=" + sample.Context
            + " onTarget=" + Count(sample.OnTarget) + " of=" + Count(sample.Of);
        var key = _key + ":" + Count(sample.Company) + ":" + sample.Context;
        var aims = MarshalMarkRules.Aims(sample);
        _judged[sample.Company] = new MarkVerdict(sample, aims);
        if (aims)
            _outcomes.Pass(MarshalChecks.MarkEffectId, key, detail);
        else
            _outcomes.Fail(MarshalChecks.MarkEffectId, key, detail);
    }

    private void Forget(IReadOnlyList<MarkSample> samples)
    {
        _gone.Clear();
        foreach (var pair in _since.Keys)
        {
            var stillMarked = false;
            foreach (var sample in samples)
                stillMarked |= sample.Company == pair.Company && sample.Target == pair.Target;
            if (!stillMarked)
                _gone.Add(pair);
        }

        foreach (var pair in _gone)
        {
            _since.Remove(pair);
            _judged.Remove(pair.Company);
        }
    }

    private static string Count(int value) => value.ToString(System.Globalization.CultureInfo.InvariantCulture);
}
