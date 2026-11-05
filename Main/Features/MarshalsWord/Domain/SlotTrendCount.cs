namespace TAOM.Features.MarshalsWord.Domain;

/// <summary>The steered hold reads that ended nearer the slot and farther from it, all steered reads, and the reads beyond the slack that no replaced tick preceded.</summary>
internal readonly record struct SlotCounts(int Closer, int Farther, int Steered, int Loose)
{
    public static SlotCounts operator +(SlotCounts left, SlotCounts right) =>
        new(left.Closer + right.Closer, left.Farther + right.Farther, left.Steered + right.Steered, left.Loose + right.Loose);
}

/// <summary>One soldier's slot trend: the steered interval since his previous hold read, and the counts of the reads that ended before the driver took them.</summary>
internal sealed class SlotTrendCount
{
    private float _baseline = float.NaN;
    private bool _steered;
    private SlotCounts _counts;

    /// <summary>The clamp replaced an input at this distance to the slot; the first steer of an interval sets the baseline.</summary>
    internal void Steer(float distanceToSlot)
    {
        if (!_steered)
            _baseline = distanceToSlot;
        _steered = true;
    }

    /// <summary>A hold read ended the interval at this distance to the slot, which is not a number when no slot is stored; a soldier out of the line adds to no count.</summary>
    internal void Read(float distance, bool strikingBack, bool inTheLine)
    {
        var trend = inTheLine ? MarshalHoldLineRules.Trend(_baseline, distance, _steered, strikingBack) : SlotTrend.None;
        var closer = trend == SlotTrend.Closer ? 1 : 0;
        var farther = trend == SlotTrend.Farther ? 1 : 0;
        var steered = trend != SlotTrend.None ? 1 : 0;
        var loose = MarshalHoldLineRules.IsLoose(distance, _steered, inTheLine) ? 1 : 0;
        _counts += new SlotCounts(closer, farther, steered, loose);
        _steered = false;
        _baseline = float.NaN;
    }

    /// <summary>The counts since the previous call, which starts them again.</summary>
    internal SlotCounts Take()
    {
        var taken = _counts;
        _counts = default;
        return taken;
    }

    internal void Forget()
    {
        _steered = false;
        _baseline = float.NaN;
        _counts = default;
    }
}
