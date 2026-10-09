// Adapted from yotthani's VanillaTuning (MIT, (c) 2026 yotthani), frame-buffer-guard and frame-buffer-watch.
namespace TAOM.Features.SkeletonBuffer;

/// <summary>Where the watch reads and what protects the buffer, as the install left it.</summary>
public sealed class SkeletonBufferTarget
{
    public SkeletonBufferTarget(long globalAddress, bool pool1Guarded, long pool1CounterAddress, bool watchPool2 = false,
        bool pool2Guarded = false, long pool2CounterAddress = 0)
    {
        GlobalAddress = globalAddress;
        Pool1Guarded = pool1Guarded;
        Pool1CounterAddress = pool1CounterAddress;
        WatchPool2 = watchPool2;
        Pool2Guarded = pool2Guarded;
        Pool2CounterAddress = pool2CounterAddress;
    }

    /// <summary>Address of the engine global that points at the frame buffers.</summary>
    public long GlobalAddress { get; }

    /// <summary>True when a guard (TAOM's or another module's) stops pool 1's buffer from overflowing. The on-screen warning keys on it.</summary>
    public bool Pool1Guarded { get; }

    /// <summary>Address of TAOM's pool 1 overflow counter, or 0 when TAOM's pool 1 guard is not the one installed.</summary>
    public long Pool1CounterAddress { get; }

    /// <summary>
    /// True when the second per-frame pool's load was found exactly once and reads the same global, so the watch reads that
    /// pool's fill counters too. Independent of whether pool 2 is guarded.
    /// </summary>
    public bool WatchPool2 { get; }

    /// <summary>True when a guard (TAOM's or another module's) stops pool 2's buffer from overflowing.</summary>
    public bool Pool2Guarded { get; }

    /// <summary>Address of TAOM's pool 2 overflow counter, or 0 when TAOM's pool 2 guard is not the one installed.</summary>
    public long Pool2CounterAddress { get; }
}
