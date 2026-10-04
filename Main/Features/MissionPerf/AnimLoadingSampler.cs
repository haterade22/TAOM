using System;
using TAOM.Adapters;

namespace TAOM.Features.MissionPerf;

/// <summary>
/// Whether an animation clip is loading from disk, sampled once per frame by the Patch98 pre-tick bracket.
/// The native call walks every on-demand clip record, so its cost is measured first (<see cref="MeasureCost"/>,
/// once per instance, at the first tick of the first measured mission): <see cref="CostCalls"/> calls, each
/// between two clock reads, and sampling turns on only when the median is within <see cref="BudgetUs"/>
/// (0.2% of a 10 ms frame). An adapter exception turns sampling off for good and leaves one reason line for
/// the caller (<see cref="TakePendingFault"/>), so the log says why the flag reads <c>na</c>. Main thread.
/// </summary>
public sealed class AnimLoadingSampler
{
    public const int CostCalls = 32;
    public const double BudgetUs = 20.0;

    private readonly IAnimationLoadingAdapter _adapter;
    private readonly Func<long> _clock;
    private readonly long _ticksPerSecond;
    private bool _measured;
    private string? _pendingFault;

    public AnimLoadingSampler(IAnimationLoadingAdapter adapter, Func<long> clock, long ticksPerSecond)
    {
        _adapter = adapter;
        _clock = clock;
        _ticksPerSecond = ticksPerSecond > 0 ? ticksPerSecond : 1;
    }

    /// <summary>False until <see cref="MeasureCost"/> finds the call within budget; false again after a fault.</summary>
    public bool Enabled { get; private set; }

    /// <summary>Times the native call <see cref="CostCalls"/> times and decides <see cref="Enabled"/>. Returns
    /// the cost line (log it with LogInfo), or null on a second call or on an adapter exception (the fault line
    /// then waits in <see cref="TakePendingFault"/>).</summary>
    public string? MeasureCost()
    {
        if (_measured)
            return null;
        _measured = true;
        try
        {
            var costs = new long[CostCalls];
            for (var i = 0; i < CostCalls; i++)
            {
                var t0 = _clock();
                _adapter.IsAnyAnimationLoadingFromDisk();
                costs[i] = _clock() - t0;
            }
            Array.Sort(costs);
            var medianTicks = (costs[CostCalls / 2 - 1] + costs[CostCalls / 2]) / 2d;
            var medianUs = medianTicks * 1_000_000d / _ticksPerSecond;
            Enabled = medianUs <= BudgetUs;
            return HitchProbeLines.BuildAnimCostLine(medianUs, CostCalls, BudgetUs, Enabled);
        }
        catch (Exception ex)
        {
            Enabled = false;
            _pendingFault = HitchProbeLines.BuildAnimFault(ex);
            return null;
        }
    }

    /// <summary>One sample: true while a clip is loading from disk. False when sampling is off; an adapter
    /// exception turns it off for good and queues the fault line.</summary>
    public bool Sample()
    {
        if (!Enabled)
            return false;
        try
        {
            return _adapter.IsAnyAnimationLoadingFromDisk();
        }
        catch (Exception ex)
        {
            Enabled = false;
            _pendingFault = HitchProbeLines.BuildAnimFault(ex);
            return false;
        }
    }

    /// <summary>The queued fault line once (log it with LogWarning), then null.</summary>
    public string? TakePendingFault()
    {
        var line = _pendingFault;
        _pendingFault = null;
        return line;
    }
}
