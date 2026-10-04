using System;
using System.Diagnostics;
using System.Reflection;
using HarmonyLib;
using TaleWorlds.CampaignSystem;
using TAOM.Features.MissionPerf;

namespace TAOM.Features.MapPerf.Hooks;

/// <summary>
/// Walks <c>CampaignEvents.TickEvent</c>'s listener list itself so each listener's call can be timed and
/// attributed to its owner's type. It reproduces vanilla <c>MbEvent&lt;T&gt;.InvokeList</c> (v1.5.3)
/// statement for statement:
/// <code>
/// while (list != null) { list.Action(t); list = list.Next; }
/// </code>
/// and keeps its four behaviours: listeners run in list order (newest first, since
/// <c>AddNonSerializedListener</c> prepends); an exception from a listener leaves the loop at once,
/// unchanged, and skips the rest (nothing here catches); a listener that removes itself
/// (<c>ClearListeners(this)</c>, the Messenger shape) still continues to its old successor, because
/// <c>Next</c> is read AFTER the call and the removed record's own <c>Next</c> is not cleared; a listener
/// added during the dispatch is prepended and does not run in it, because the head is read once.
/// <c>MapFrameProfilerBindingTests.MbEventInvokeList_ReadsNextAfterTheCall_AndHasNoHandler</c> pins that
/// vanilla shape and <c>TickEventListenerWalkerTests</c> compares the two call sequences.
///
/// The four members are bound once, at install, with Harmony field-reference delegates (no per-call
/// reflection). Any member missing or of an unexpected type leaves the walk unbound; the caller then
/// invokes vanilla and logs one warning. Main thread only.
/// </summary>
internal static class TickEventListenerWalker
{
    private static AccessTools.FieldRef<MbEvent<float>, object>? _head;
    private static AccessTools.FieldRef<object, object>? _next;
    private static AccessTools.FieldRef<object, Action<float>>? _action;
    private static AccessTools.FieldRef<object, object>? _owner;

    internal static bool Bound { get; private set; }

    /// <summary>Binds the four members once; false with a one-line reason when any is missing or of another type.</summary>
    internal static bool TryBind(out string failure)
    {
        failure = string.Empty;
        if (Bound)
            return true;
        try
        {
            var head = AccessTools.Field(typeof(MbEvent<float>), "_nonSerializedListenerList");
            if (head == null)
                return Fail("MbEvent<float>._nonSerializedListenerList not found", out failure);
            var rec = head.FieldType;
            var next = AccessTools.Field(rec, "Next");
            if (next == null || next.FieldType != rec)
                return Fail(rec.Name + ".Next " + (next == null ? "not found" : "is a " + next.FieldType.Name), out failure);
            var action = AccessTools.Field(rec, "<Action>k__BackingField");
            if (action == null || action.FieldType != typeof(Action<float>))
                return Fail(rec.Name + ".<Action>k__BackingField " + (action == null ? "not found" : "is a " + action.FieldType.Name), out failure);
            var owner = AccessTools.Field(rec, "<Owner>k__BackingField");
            if (owner == null || owner.FieldType != typeof(object))
                return Fail(rec.Name + ".<Owner>k__BackingField " + (owner == null ? "not found" : "is a " + owner.FieldType.Name), out failure);

            _head = AccessTools.FieldRefAccess<MbEvent<float>, object>(head);
            _next = AccessTools.FieldRefAccess<object, object>(next);
            _action = AccessTools.FieldRefAccess<object, Action<float>>(action);
            _owner = AccessTools.FieldRefAccess<object, object>(owner);
            Bound = true;
            return true;
        }
        catch (Exception ex)
        {
            return Fail(ex.GetType().Name + ": " + ex.Message, out failure);
        }
    }

    /// <summary>Tests only: makes the walk unbound, as a failed bind would.</summary>
    internal static void Unbind() => Bound = false;

    /// <summary>Vanilla InvokeList, statement for statement (MbEvent&lt;T&gt;.InvokeList, v1.5.3), with each call timed.</summary>
    internal static void InvokeTimed(MbEvent<float> tickEvent, float dt, MapFrameProfiler profiler, Assembly taomAssembly)
    {
        var rec = _head!(tickEvent);
        while (rec != null)
        {
            var action = _action!(rec);
            var owner = _owner!(rec);
            var type = owner?.GetType() ?? action?.Method.DeclaringType ?? typeof(object);
            var slot = profiler.Entries.SlotFor(type);
            var taom = type.Assembly == taomAssembly;
            var a0 = AllocationCounter.ReadOrZero();
            var t0 = Stopwatch.GetTimestamp();
            try
            {
                action!(dt);   // a null action throws NullReferenceException, exactly as vanilla's list.Action(t)
            }
            finally
            {
                profiler.RecordEntry(slot, Stopwatch.GetTimestamp() - t0, AllocationCounter.ReadOrZero() - a0, taom);
            }
            rec = _next!(rec);   // read AFTER the call, as vanilla does: a self-removed record keeps its Next
        }
    }

    private static bool Fail(string reason, out string failure)
    {
        failure = reason;
        Bound = false;
        return false;
    }
}
