using System;
using System.Collections.Generic;
using TAOM.Adapters;
using TAOM.Features.LoadTimeStamps.Domain;

namespace TAOM.Tests.Features.LoadTimeStamps;

/// <summary>
/// A hand-driven listener adapter: each wrap hands out fresh timings, as the real adapter does, and
/// <see cref="Fire"/> plays the timing wrapper of one listener call.
/// </summary>
internal sealed class FakeCampaignListenerAdapter : ICampaignListenerAdapter
{
    private readonly Dictionary<LifecycleEvent, List<(string Handler, string Assembly, bool IsTaom)>> _listeners = new();
    private readonly Dictionary<LifecycleEvent, List<ListenerTiming>> _wrapped = new();

    public string? BindingProblem { get; set; }

    public LifecycleEvent? ThrowOnWrap { get; set; }

    public bool ThrowOnRestore { get; set; }

    /// <summary>Runs at each wrap, as the real swap's own work would (a test advances its clock here).</summary>
    public Action? OnWrap { get; set; }

    public List<LifecycleEvent> Wrapped { get; } = new();

    public List<LifecycleEvent> Restored { get; } = new();

    public IReadOnlyList<ListenerTiming> WrapListeners(LifecycleEvent lifecycleEvent)
    {
        if (ThrowOnWrap == lifecycleEvent)
            throw new InvalidOperationException("record type moved");
        OnWrap?.Invoke();
        Wrapped.Add(lifecycleEvent);
        var timings = new List<ListenerTiming>();
        if (_listeners.TryGetValue(lifecycleEvent, out var listeners))
        {
            foreach (var (handler, assembly, isTaom) in listeners)
                timings.Add(new ListenerTiming(handler, assembly, isTaom));
        }

        _wrapped[lifecycleEvent] = timings;
        return timings;
    }

    public void RestoreListeners(LifecycleEvent lifecycleEvent)
    {
        Restored.Add(lifecycleEvent);
        if (ThrowOnRestore) throw new InvalidOperationException("setter moved");
    }

    /// <summary>One call of listener <paramref name="index"/> (invoke order) of the event's last wrap.</summary>
    public void Fire(LifecycleEvent lifecycleEvent, int index, long ticks, int argument) =>
        _wrapped[lifecycleEvent][index].Record(ticks, argument);

    public void Add(LifecycleEvent lifecycleEvent, string handler, string assembly, bool isTaom)
    {
        if (!_listeners.TryGetValue(lifecycleEvent, out var list))
            _listeners[lifecycleEvent] = list = new List<(string, string, bool)>();
        list.Add((handler, assembly, isTaom));
    }
}
