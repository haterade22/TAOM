using System.Threading;

namespace TAOM.Features.MarshalsWord;

internal readonly record struct CostBucket(long Calls, long Ticks, long Most);

internal readonly record struct ThrowerCostRead(CostBucket Published, CostBucket Other, long Replaced, long Moved);

/// <summary>What the timer measured around the order position call (build-spec 9.4): published companies beside every other Advance. Lock-free, any thread.</summary>
internal static class MarshalThrowerCost
{
    private static long _pubCalls;
    private static long _pubTicks;
    private static long _pubMost;
    private static long _otherCalls;
    private static long _otherTicks;
    private static long _otherMost;
    private static long _replaced;
    private static long _moved;

    internal static void AddPublished(long ticks, bool replaced)
    {
        Interlocked.Increment(ref _pubCalls);
        Interlocked.Add(ref _pubTicks, ticks);
        RaiseMost(ref _pubMost, ticks);
        if (replaced)
            Interlocked.Increment(ref _replaced);
    }

    internal static void AddOther(long ticks)
    {
        Interlocked.Increment(ref _otherCalls);
        Interlocked.Add(ref _otherTicks, ticks);
        RaiseMost(ref _otherMost, ticks);
    }

    /// <summary>The thrower postfix moved an answer.</summary>
    internal static void AddMoved() => Interlocked.Increment(ref _moved);

    internal static ThrowerCostRead Read() => new(
        new CostBucket(Volatile.Read(ref _pubCalls), Volatile.Read(ref _pubTicks), Volatile.Read(ref _pubMost)),
        new CostBucket(Volatile.Read(ref _otherCalls), Volatile.Read(ref _otherTicks), Volatile.Read(ref _otherMost)),
        Volatile.Read(ref _replaced),
        Volatile.Read(ref _moved));

    /// <summary>Starts an epoch. The main thread calls it while the AI job is idle.</summary>
    internal static void Reset()
    {
        Volatile.Write(ref _pubCalls, 0);
        Volatile.Write(ref _pubTicks, 0);
        Volatile.Write(ref _pubMost, 0);
        Volatile.Write(ref _otherCalls, 0);
        Volatile.Write(ref _otherTicks, 0);
        Volatile.Write(ref _otherMost, 0);
        Volatile.Write(ref _replaced, 0);
        Volatile.Write(ref _moved, 0);
    }

    private static void RaiseMost(ref long most, long ticks)
    {
        long seen;
        do
        {
            seen = Volatile.Read(ref most);
            if (ticks <= seen)
                return;
        }
        while (Interlocked.CompareExchange(ref most, ticks, seen) != seen);
    }
}
