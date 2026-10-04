using System.Collections.Generic;
using TAOM.Features.LoadTimeStamps.Domain;

namespace TAOM.Adapters;

/// <summary>
/// Times the campaign handlers of one lifecycle event by swapping each listener's delegate for a
/// timing wrapper for the length of one dispatch (docs/features/load-time-stamps.md). Hides the
/// engine's private MbEvent listener records from the service.
/// </summary>
public interface ICampaignListenerAdapter
{
    /// <summary>Null when every listener-list member resolved; otherwise the first one missing.</summary>
    string? BindingProblem { get; }

    /// <summary>Swaps each listener of the event for a wrapper that times the original call and
    /// records it (elapsed ticks, the int argument or -1) on that listener's timing; returns the
    /// timings in invoke order. Throws only on a reflection failure, after undoing its own swaps.</summary>
    IReadOnlyList<ListenerTiming> WrapListeners(LifecycleEvent lifecycleEvent);

    /// <summary>Puts back every original this adapter swapped for the event; safe to call twice.</summary>
    void RestoreListeners(LifecycleEvent lifecycleEvent);
}
