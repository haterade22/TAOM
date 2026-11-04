using System;
using System.Collections.Generic;
using System.Linq;

namespace TAOM.Features.MarshalsWord.Domain;

/// <summary>
/// One company's walk as the march reads it on a pass: metres left to its order point, its speed, and how far its order point is from the one
/// the march stored. A company that left its Move (Charge, Stop and Retreat move no order point) or went to the AI reports a shift of
/// <c>float.MaxValue</c>, which is over any limit and is still a number.
/// </summary>
internal readonly record struct MarchLeg(int Company, float Remaining, float Speed, float TargetShift);

/// <summary>A company's share of full speed, in <c>MinPace</c> to 1.</summary>
internal readonly record struct MarchPace(int Company, float Pace);

internal enum MarchEnd
{
    None,
    Arrived,
    Reordered,
    Invalid,

    /// <summary>Fewer than two companies are left: an emptied company leaves the march, and a march of one has nobody to arrive with.</summary>
    Emptied,

    /// <summary>The march ran past its patience, so one stuck company does not keep the others slow for the rest of the battle.</summary>
    Patience,
}

/// <summary>What one pass of the march decided. <c>Paces</c> is empty when the march ended.</summary>
internal readonly record struct MarchPass(bool Ended, MarchEnd Reason, IReadOnlyList<MarchPace> Paces);

/// <summary>
/// The march of build-spec 6.4: companies ordered together arrive together, because the nearer ones slow in proportion to their time
/// left.
/// </summary>
internal sealed class MarshalMarch
{
    private readonly Dictionary<int, double> _arrivedAt = new();
    private readonly Dictionary<int, double> _estimate = new();
    private float _firstLongest = -1f;
    private float _longestToSlack = -1f;
    private float _shortestToSlack = -1f;
    private bool _membersChanged;
    private double _startedAt = double.NaN;

    /// <summary>The longest time to the arrival slack on the first pass, in seconds, and 0 before the first pass (6.5).</summary>
    internal float LongestToSlack => Math.Max(0f, _longestToSlack);

    /// <summary>The shortest time to the arrival slack on the first pass, in seconds, and 0 before the first pass.</summary>
    internal float ShortestToSlack => Math.Max(0f, _shortestToSlack);

    /// <summary>Two passes named different companies, so the proof values and the spread no longer name the same companies.</summary>
    internal bool MembersChanged => _membersChanged;

    internal MarchPass Update(double now, IReadOnlyList<MarchLeg> legs)
    {
        if (legs.Count < 2)
            return End(MarchEnd.Emptied);
        if (double.IsNaN(_startedAt))
            _startedAt = now;

        _membersChanged |= _estimate.Count > 0 && (legs.Count != _estimate.Count || legs.Any(l => !_estimate.ContainsKey(l.Company)));
        _estimate.Clear();
        var etas = new float[legs.Count];
        var toSlack = new float[legs.Count];
        for (var i = 0; i < legs.Count; i++)
        {
            var leg = legs[i];
            if (IsBad(leg.Remaining) || IsBad(leg.Speed) || IsBad(leg.TargetShift))
                return End(MarchEnd.Invalid);
            var remaining = Math.Max(0f, leg.Remaining);
            var speed = Math.Max(leg.Speed, 1f);
            etas[i] = remaining / speed;
            toSlack[i] = Math.Max(0f, remaining - MarshalTuning.ArrivalSlack) / speed;
            if (remaining <= MarshalTuning.ArrivalSlack && !_arrivedAt.ContainsKey(leg.Company))
                _arrivedAt[leg.Company] = now;
            _estimate[leg.Company] = now + toSlack[i];
        }

        var longest = etas.Max();
        if (_firstLongest < 0f)
        {
            _firstLongest = longest;
            _longestToSlack = toSlack.Max();
            _shortestToSlack = toSlack.Min();
        }

        if (legs.Any(l => l.TargetShift > MarshalTuning.MarchMovedMetres))
            return End(MarchEnd.Reordered);
        if (longest < MarshalTuning.MarchEndSeconds)
            return End(MarchEnd.Arrived);
        if (now - _startedAt > Math.Max(MarshalTuning.MinPatience, MarshalTuning.PatiencePerEta * _firstLongest))
            return End(MarchEnd.Patience);

        var paces = new MarchPace[legs.Count];
        for (var i = 0; i < paces.Length; i++)
            paces[i] = new MarchPace(legs[i].Company, Math.Min(1f, Math.Max(MarshalTuning.MinPace, etas[i] / longest)));
        return new MarchPass(false, MarchEnd.None, paces);
    }

    /// <summary>
    /// Seconds between the first and the last arrival of the companies on the latest pass, where one not yet within the slack counts at its
    /// estimated arrival because the march ends before the last company is in.
    /// </summary>
    internal double Spread()
    {
        if (_estimate.Count == 0)
            return 0.0;
        var times = _estimate.Select(e => _arrivedAt.TryGetValue(e.Key, out var at) ? at : e.Value).ToList();
        return times.Max() - times.Min();
    }

    private static MarchPass End(MarchEnd reason) => new(true, reason, Array.Empty<MarchPace>());

    private static bool IsBad(float value) => float.IsNaN(value) || float.IsInfinity(value);
}

/// <summary>Where the array commit sent one company: the point the march compares with its order point on every pass.</summary>
internal readonly record struct MarchTarget(int Company, float X, float Y);
