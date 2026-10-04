using System;
using System.Collections.Generic;
using System.Reflection;
using HarmonyLib;
using TaleWorlds.CampaignSystem;
using TAOM.Core.Diagnostics;
using TAOM.Features.LoadTimeStamps.Domain;

namespace TAOM.Adapters;

/// <summary>
/// Swaps the delegates of an MbEvent's listener records for timing wrappers for the length of one
/// dispatch, and puts them back (docs/features/load-time-stamps.md). MbEvent keeps its listeners in a
/// private linked list of EventHandlerRec records (head first, the order Invoke walks), each with a
/// private-set Action and an Owner (v1.5.3 MbEvent`1.cs, MbEvent`2.cs); the members are resolved once
/// here, and a missing one sets <see cref="BindingProblem"/> instead of throwing. A wrapper calls the
/// original exactly once, in a try/finally with no catch, so the order, the arguments and every
/// exception are unchanged; only the stack gains the wrapper's frame.
/// </summary>
public sealed class CampaignListenerAdapter : ICampaignListenerAdapter
{
    private readonly IStampClock _clock;
    private readonly ListBinding? _one;
    private readonly ListBinding? _two;
    private readonly object _sync = new();
    private readonly Dictionary<LifecycleEvent, List<Swap>> _swaps = new();

    public CampaignListenerAdapter(IStampClock clock)
    {
        _clock = clock ?? throw new ArgumentNullException(nameof(clock));
        try
        {
            _one = ListBinding.Resolve(typeof(MbEvent<CampaignGameStarter>), "MbEvent<CampaignGameStarter>", out var problemOne);
            _two = ListBinding.Resolve(typeof(MbEvent<CampaignGameStarter, int>), "MbEvent<CampaignGameStarter, int>", out var problemTwo);
            BindingProblem = problemOne ?? problemTwo;
        }
        catch (Exception ex)
        {
            BindingProblem = "listener binding failed: " + ex.GetType().Name + ": " + ex.Message;
        }
    }

    public string? BindingProblem { get; }

    /// <summary>Test seam, null in production: runs ahead of every write of a record's Action (the swap,
    /// its rollback and the restore); a throw stands for that write failing. No real record fails one,
    /// so this is how a test makes a walk fail midway.</summary>
    internal Action<object, Delegate>? BeforeWrite { get; set; }

    public IReadOnlyList<ListenerTiming> WrapListeners(LifecycleEvent lifecycleEvent)
    {
        if (BindingProblem != null)
            throw new InvalidOperationException("listener binding missing: " + BindingProblem);

        if (lifecycleEvent == LifecycleEvent.OnNewGameCreatedPartialFollowUp)
        {
            var two = CampaignEvents.OnNewGameCreatedPartialFollowUpEvent as MbEvent<CampaignGameStarter, int>
                ?? throw new InvalidOperationException("CampaignEvents.OnNewGameCreatedPartialFollowUpEvent is not an MbEvent<CampaignGameStarter, int>");
            return WrapTwo(two, lifecycleEvent);
        }

        var one = EventOf(lifecycleEvent) as MbEvent<CampaignGameStarter>
            ?? throw new InvalidOperationException("CampaignEvents." + lifecycleEvent + "Event is not an MbEvent<CampaignGameStarter>");
        return WrapOne(one, lifecycleEvent);
    }

    public void RestoreListeners(LifecycleEvent lifecycleEvent)
    {
        List<Swap>? swaps;
        lock (_sync)
        {
            if (!_swaps.TryGetValue(lifecycleEvent, out swaps)) return;
            _swaps.Remove(lifecycleEvent);
        }

        Exception? first = null;
        // Newest first, so a record wrapped twice unwinds to its original.
        for (var i = swaps.Count - 1; i >= 0; i--)
        {
            var swap = swaps[i];
            try
            {
                if (ReferenceEquals(swap.Binding.Action.GetValue(swap.Record), swap.Wrapper))
                    Write(swap.Binding, swap.Record, swap.Original);
            }
            catch (Exception ex)
            {
                first ??= ex;
            }
        }

        if (first != null) throw first;
    }

    internal IReadOnlyList<ListenerTiming> WrapOne(MbEvent<CampaignGameStarter> mbEvent, LifecycleEvent key) =>
        Wrap(_one!, mbEvent, key, (original, timing) =>
        {
            var inner = (Action<CampaignGameStarter>)original;
            Action<CampaignGameStarter> wrapper = starter =>
            {
                var start = SafeNow();
                try { inner(starter); }
                finally { Report(timing, start, -1); }
            };
            return wrapper;
        });

    internal IReadOnlyList<ListenerTiming> WrapTwo(MbEvent<CampaignGameStarter, int> mbEvent, LifecycleEvent key) =>
        Wrap(_two!, mbEvent, key, (original, timing) =>
        {
            var inner = (Action<CampaignGameStarter, int>)original;
            Action<CampaignGameStarter, int> wrapper = (starter, argument) =>
            {
                var start = SafeNow();
                try { inner(starter, argument); }
                finally { Report(timing, start, argument); }
            };
            return wrapper;
        });

    private IReadOnlyList<ListenerTiming> Wrap(ListBinding binding, object mbEvent, LifecycleEvent key, Func<Delegate, ListenerTiming, Delegate> makeWrapper)
    {
        var swaps = new List<Swap>();
        var timings = new List<ListenerTiming>();
        try
        {
            var current = binding.List.GetValue(mbEvent);
            while (current != null)
            {
                if (binding.Action.GetValue(current) is Delegate original)
                {
                    // The timing exists before its wrapper is installed, so every call has somewhere to land.
                    var timing = Describe(binding.Owner.GetValue(current), original);
                    var wrapper = makeWrapper(original, timing);
                    Write(binding, current, wrapper);
                    swaps.Add(new Swap(binding, current, original, wrapper));
                    timings.Add(timing);
                }

                current = binding.Next.GetValue(current);
            }
        }
        catch
        {
            Undo(swaps);
            throw;
        }

        lock (_sync)
        {
            if (_swaps.TryGetValue(key, out var existing)) existing.AddRange(swaps);
            else _swaps[key] = swaps;
        }

        return timings;
    }

    private void Undo(List<Swap> swaps)
    {
        for (var i = swaps.Count - 1; i >= 0; i--)
        {
            try { Write(swaps[i].Binding, swaps[i].Record, swaps[i].Original); }
            catch { /* the caller rethrows the wrap's own failure */ }
        }
    }

    // The one place a record's Action is written: the swap, its rollback and the restore.
    private void Write(ListBinding binding, object record, Delegate value)
    {
        BeforeWrite?.Invoke(record, value);
        binding.SetAction.Invoke(record, new object[] { value });
    }

    // The assembly is the callback's own. MbEvent keeps the owner only as the token ClearListeners
    // matches, beside a delegate it never ties to it (v1.5.3 MbEvent`1.cs:22-30 and :46-79), so a plain
    // object token would read as mscorlib and a TAOM callback as someone else's. The owner still names
    // the handler, and stands in for the assembly when the callback has no declaring type (a dynamic
    // method) or is this adapter's own wrapper, a closure nested in this class: a record keeps one
    // after a failed restore (C6) or a second wrap, and it says nothing about the handler inside it.
    private static ListenerTiming Describe(object? owner, Delegate original)
    {
        var ownerType = owner?.GetType();
        var codeType = original.Method.DeclaringType;
        if (codeType?.DeclaringType == typeof(CampaignListenerAdapter)) codeType = null;
        var assembly = (codeType ?? ownerType)?.Assembly;
        return new ListenerTiming(
            ((ownerType ?? codeType)?.Name ?? "?") + "." + original.Method.Name,
            assembly?.GetName().Name ?? "?",
            assembly == typeof(CampaignListenerAdapter).Assembly);
    }

    private static IMbEvent<CampaignGameStarter> EventOf(LifecycleEvent lifecycleEvent) => lifecycleEvent switch
    {
        LifecycleEvent.OnNewGameCreated => CampaignEvents.OnNewGameCreatedEvent,
        LifecycleEvent.OnNewGameCreatedPartialFollowUpEnd => CampaignEvents.OnNewGameCreatedPartialFollowUpEndEvent,
        LifecycleEvent.OnGameEarlyLoaded => CampaignEvents.OnGameEarlyLoadedEvent,
        LifecycleEvent.OnGameLoaded => CampaignEvents.OnGameLoadedEvent,
        LifecycleEvent.OnSessionLaunched => CampaignEvents.OnSessionLaunchedEvent,
        LifecycleEvent.OnAfterSessionLaunched => CampaignEvents.OnAfterSessionLaunchedEvent,
        _ => throw new ArgumentOutOfRangeException(nameof(lifecycleEvent), lifecycleEvent, "not a CampaignGameStarter event"),
    };

    private long SafeNow()
    {
        try { return _clock.Now; }
        catch { return 0; }
    }

    // Swallows every fault of the clock or the record: a wrapper's finally must never replace the
    // handler's own exception.
    private void Report(ListenerTiming timing, long start, int argument)
    {
        try { timing.Record(_clock.Now - start, argument); }
        catch { /* a stamp must never break a load */ }
    }

    private sealed class Swap
    {
        internal Swap(ListBinding binding, object record, Delegate original, Delegate wrapper)
        {
            Binding = binding;
            Record = record;
            Original = original;
            Wrapper = wrapper;
        }

        internal ListBinding Binding { get; }
        internal object Record { get; }
        internal Delegate Original { get; }
        internal Delegate Wrapper { get; }
    }

    private sealed class ListBinding
    {
        private ListBinding(FieldInfo list, FieldInfo next, PropertyInfo action, MethodInfo setAction, PropertyInfo owner)
        {
            List = list;
            Next = next;
            Action = action;
            SetAction = setAction;
            Owner = owner;
        }

        internal FieldInfo List { get; }
        internal FieldInfo Next { get; }
        internal PropertyInfo Action { get; }
        internal MethodInfo SetAction { get; }
        internal PropertyInfo Owner { get; }

        // Null with a problem naming the first member that did not resolve.
        internal static ListBinding? Resolve(Type eventType, string name, out string? problem)
        {
            problem = null;
            var list = AccessTools.Field(eventType, "_nonSerializedListenerList");
            if (list == null) { problem = name + "._nonSerializedListenerList not found"; return null; }

            var recordType = list.FieldType;
            var next = AccessTools.Field(recordType, "Next");
            if (next == null) { problem = name + ".EventHandlerRec.Next not found"; return null; }

            var action = AccessTools.Property(recordType, "Action");
            if (action == null) { problem = name + ".EventHandlerRec.Action not found"; return null; }

            var setAction = action.GetSetMethod(true);
            if (setAction == null) { problem = name + ".EventHandlerRec.set_Action not found"; return null; }

            var owner = AccessTools.Property(recordType, "Owner");
            if (owner == null) { problem = name + ".EventHandlerRec.Owner not found"; return null; }

            return new ListBinding(list, next, action, setAction, owner);
        }
    }
}
