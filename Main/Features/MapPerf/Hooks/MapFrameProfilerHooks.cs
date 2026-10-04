using System.Diagnostics;
using System.Reflection;
using TaleWorlds.CampaignSystem;
using TAOM.Core.Logging;
using TAOM.Features.MissionPerf;

namespace TAOM.Features.MapPerf.Hooks;

/// <summary>
/// The Patch101 per-frame helpers: the phase brackets (prefix stamps, postfix adds), the TAOM map view probe,
/// and the timed <c>CampaignEvents.TickEvent</c> dispatch the transpiler calls in place of
/// <c>MbEvent&lt;float&gt;.Invoke</c>. Thin (ADR-002): the arithmetic is <see cref="MapFrameProfiler"/>, the
/// session logic <see cref="MapSessionHooks"/>, every line <see cref="MapProfileLines"/>. Not measuring, a
/// prefix returns 0 and its postfix does nothing. Prefix and postfix, never a finalizer: a call that throws
/// records nothing for that call and no exception path changes. Main thread only.
/// </summary>
public static class MapFrameProfilerHooks
{
    internal static MapFrameProfiler? Profiler;
    internal static IModLogger? Logger;
    internal static int TickEventSites;
    internal static readonly Assembly TaomAssembly = typeof(MapFrameProfilerHooks).Assembly;

    /// <summary>A phase prefix: a timestamp while measuring, else 0 (a QueryPerformanceCounter value is never 0).</summary>
    internal static long BeginPhase()
    {
        var profiler = Profiler;
        return profiler != null && profiler.Measuring ? Stopwatch.GetTimestamp() : 0L;
    }

    internal static void EndPhase(MapPhase phase, long start)
    {
        if (start == 0)
            return;
        Profiler?.AddPhase(phase, Stopwatch.GetTimestamp() - start);
    }

    /// <summary>TAOM's <c>SubModule.OnApplicationTick</c> postfix: its time, and the continuity count.</summary>
    internal static void EndAppTick(long start)
    {
        if (start == 0)
            return;
        Profiler?.AddAppTick(Stopwatch.GetTimestamp() - start);
    }

    internal static ProbeStamp BeginProbe()
    {
        var profiler = Profiler;
        return profiler != null && profiler.Measuring
            ? new ProbeStamp(Stopwatch.GetTimestamp(), AllocationCounter.ReadOrZero())
            : default;
    }

    /// <summary>A TAOM map view's per-frame override: one entry keyed by the view's type, TAOM-owned.</summary>
    internal static void RecordView(object instance, ProbeStamp start)
    {
        if (start.Ticks == 0)
            return;
        var profiler = Profiler;
        if (profiler == null)
            return;
        var elapsed = Stopwatch.GetTimestamp() - start.Ticks;
        profiler.RecordEntry(profiler.Entries.SlotFor(instance.GetType()), elapsed,
            AllocationCounter.ReadOrZero() - start.Alloc, taomOwned: true);
    }

    /// <summary>Replaces CampaignEvents.Tick's <c>callvirt MbEvent&lt;float&gt;::Invoke(float)</c> (Patch101
    /// transpiler). Not measuring, or the walk unbound, it calls <c>Invoke</c> unchanged; measuring, the walk
    /// times each listener and the bracket the whole dispatch. Nothing here catches.</summary>
    public static void TimedTickEvent(MbEvent<float> tickEvent, float dt)
    {
        var profiler = Profiler;
        if (profiler == null || !profiler.Measuring)
        {
            tickEvent.Invoke(dt);
            return;
        }
        var t0 = Stopwatch.GetTimestamp();
        try
        {
            if (TickEventListenerWalker.Bound)
                TickEventListenerWalker.InvokeTimed(tickEvent, dt, profiler, TaomAssembly);
            else
                tickEvent.Invoke(dt);
        }
        finally
        {
            profiler.AddPhase(MapPhase.TickEvent, Stopwatch.GetTimestamp() - t0);
        }
    }
}

/// <summary>A map view probe's start: the timestamp (0 when not measuring) and the allocation counter.</summary>
public readonly struct ProbeStamp
{
    public ProbeStamp(long ticks, long alloc)
    {
        Ticks = ticks;
        Alloc = alloc;
    }

    public long Ticks { get; }
    public long Alloc { get; }
}
