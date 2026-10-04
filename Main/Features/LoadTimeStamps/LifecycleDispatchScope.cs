using System.Collections.Generic;
using TAOM.Features.LoadTimeStamps.Domain;

namespace TAOM.Features.LoadTimeStamps;

/// <summary>
/// One timed CampaignEventDispatcher call, from its prefix to its finalizer: the dispatch, its start
/// tick and, when per-handler timing is on (the toggle is on and nothing failed), one record per
/// wrapped event. Only <see cref="LifecycleTimingService"/> reads it.
/// </summary>
public sealed class LifecycleDispatchScope
{
    internal LifecycleDispatchScope(LifecycleDispatch dispatch, long start)
    {
        Dispatch = dispatch;
        Start = start;
    }

    internal LifecycleDispatch Dispatch { get; }

    /// <summary>Re-read at the end of Begin, after the listener swap or the C4 line, so the dispatch time leaves out
    /// the stamp's own setup; a clock fault there keeps the first tick.</summary>
    internal long Start { get; set; }

    /// <summary>The events whose listeners were swapped, in dispatch order; restored at the end.</summary>
    internal List<EventRecord> Events { get; } = new();

    /// <summary>False when per-handler timing is off (toggle off, binding missing or a wrap failed): dispatch total only.</summary>
    internal bool HasListeners { get; set; }

    /// <summary>One event's listeners, each with what its calls cost during this dispatch.</summary>
    internal sealed class EventRecord
    {
        internal EventRecord(LifecycleEvent lifecycleEvent) => Event = lifecycleEvent;

        internal LifecycleEvent Event { get; }

        internal IReadOnlyList<ListenerTiming> Listeners { get; set; } = new ListenerTiming[0];
    }
}
