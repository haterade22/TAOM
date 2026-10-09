// Adapted from yotthani's VanillaTuning (MIT, (c) 2026 yotthani), frame-buffer-guard and frame-buffer-watch.
using System;

namespace TAOM.Features.SkeletonBuffer;

/// <summary>
/// One mission's reading of the per-frame skeleton buffer: the peak fill, what the battle looked like at the peak, and the
/// one-time warning level. Pure; the service feeds it readings.
/// </summary>
internal sealed class SkeletonBufferWatchState
{
    /// <summary>Entries in one buffer: 32 blocks of 2,048 four-byte entries.</summary>
    internal const int Capacity = 65536;

    /// <summary>Entries in one buffer of the second pool: 32 blocks of 8,192 entries, with no bound against 32 (v1.5.4).</summary>
    internal const int Pool2Capacity = 262144;

    /// <summary>What one skeleton reserves (measured by yotthani on v1.5.4).</summary>
    internal const int EntriesPerSkeleton = 28;

    private bool _warned;

    internal bool HasSamples { get; private set; }

    internal int PeakFill { get; private set; }

    internal int PeakAgents { get; private set; }

    internal double PeakSeconds { get; private set; }

    internal double PeakPercent => PeakFill * 100.0 / Capacity;

    /// <summary>True when pool 1's peak reached 90 percent of its capacity (the level the on-screen warning and the mission-end WARNING key on).</summary>
    internal bool Pool1OverNinety => (long)PeakFill * 10 >= (long)Capacity * 9;

    internal bool Pool2HasSamples { get; private set; }

    /// <summary>The second pool's peak fill (the larger of its two buffers); its own peak, kept apart from <see cref="PeakFill"/>.</summary>
    internal int Pool2PeakFill { get; private set; }

    internal double Pool2PeakPercent => Pool2PeakFill * 100.0 / Pool2Capacity;

    /// <summary>True when the second pool's peak reached 90 percent of its capacity (a log note; a WARNING-level mission-end line only while pool 2 is unguarded; no on-screen warning).</summary>
    internal bool Pool2OverNinety => (long)Pool2PeakFill * 10 >= (long)Pool2Capacity * 9;

    internal int PeakSkeletons => PeakFill / EntriesPerSkeleton;

    /// <summary>Records <paramref name="fill"/>; <paramref name="agentCount"/> is asked only when it is a new peak.</summary>
    internal void Observe(int fill, double seconds, Func<int> agentCount)
    {
        if (HasSamples && fill <= PeakFill) return;
        HasSamples = true;
        PeakFill = fill;
        PeakSeconds = seconds;
        PeakAgents = agentCount();
    }

    /// <summary>Records the second pool's <paramref name="fill"/>; only its peak is kept.</summary>
    internal void ObservePool2(int fill)
    {
        if (Pool2HasSamples && fill <= Pool2PeakFill) return;
        Pool2HasSamples = true;
        Pool2PeakFill = fill;
    }

    /// <summary>
    /// True once per mission, when the peak reaches 90 percent of the buffer and nothing guards it. A guarded call does not
    /// spend the warning.
    /// </summary>
    internal bool TakeWarning(bool guarded)
    {
        if (guarded || _warned || !Pool1OverNinety) return false;
        _warned = true;
        return true;
    }
}
