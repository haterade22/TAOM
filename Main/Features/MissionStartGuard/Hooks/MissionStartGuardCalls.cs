// Adapted from yotthani's VanillaTuning (MIT, (c) 2026 yotthani), mission-start-guard.
using System;
using System.Collections.Generic;
using TAOM.Features.MissionStartGuard.Models;
using TaleWorlds.MountAndBlade;

namespace TAOM.Features.MissionStartGuard.Hooks;

/// <summary>
/// The helpers Patch103 swaps in for the engine's six start calls inside <c>Mission.AfterStart</c>. Each takes the
/// call's instance first and then its arguments (the stack the engine pushed), makes the call inside a try, and
/// catches only when <see cref="IMissionStartGuardService.ShouldSurvive"/> says so. The exception filter runs before
/// any frame unwinds, so with the guard off, with no service, or for <see cref="OutOfMemoryException"/> the engine's
/// exception reaches its old catcher untouched and with its original stack. A report that fails costs only the report.
/// Main thread only: <c>Mission.AfterStart</c> and its callbacks.
/// </summary>
internal static class MissionStartGuardCalls
{
    internal static IMissionStartGuardService? Service { get; private set; }

    internal static void Initialize(IMissionStartGuardService? service) => Service = service;

    internal static void OnBeforeMissionBehaviorInitialize(MBSubModuleBase module, Mission mission)
    {
        try { module.OnBeforeMissionBehaviorInitialize(mission); }
        catch (Exception ex) when (Survives(ex)) { Note(StartCall.OnBeforeMissionBehaviorInitialize, module, ex); }
    }

    internal static void OnBehaviorInitialize(MissionBehavior behavior)
    {
        try { behavior.OnBehaviorInitialize(); }
        catch (Exception ex) when (Survives(ex)) { Note(StartCall.OnBehaviorInitialize, behavior, ex); }
    }

    internal static void OnMissionBehaviorInitialize(MBSubModuleBase module, Mission mission)
    {
        try { module.OnMissionBehaviorInitialize(mission); }
        catch (Exception ex) when (Survives(ex)) { Note(StartCall.OnMissionBehaviorInitialize, module, ex); }
    }

    internal static void EarlyStart(MissionBehavior behavior)
    {
        var watch = WatchBehaviors();
        OpenDeferralWindow(StartCall.EarlyStart, behavior);
        try { behavior.EarlyStart(); }
        catch (Exception ex) when (Survives(ex)) { Note(StartCall.EarlyStart, behavior, ex); }
        MissionBehaviorDeferral.Close();
        NoteListChange(StartCall.EarlyStart, behavior, watch);
    }

    internal static void AfterStart(MissionBehavior behavior)
    {
        var watch = WatchBehaviors();
        OpenDeferralWindow(StartCall.AfterStart, behavior);
        try { behavior.AfterStart(); }
        catch (Exception ex) when (Survives(ex)) { Note(StartCall.AfterStart, behavior, ex); }
        MissionBehaviorDeferral.Close();
        NoteListChange(StartCall.AfterStart, behavior, watch);
    }

    internal static void AfterMissionStart(MissionObject missionObject)
    {
        var watch = WatchObjects();
        try { missionObject.AfterMissionStart(); }
        catch (Exception ex) when (Survives(ex)) { Note(StartCall.AfterMissionStart, missionObject, ex); }
        NoteListChange(StartCall.AfterMissionStart, missionObject, watch);
    }

    // Mission.AfterStart walks MissionBehaviors with foreach around EarlyStart and AfterStart, and MissionObjects around
    // AfterMissionStart. Any change to the list inside one call (an add, a remove, an indexer write, a sort, an add paired
    // with a remove) makes the engine's next step through it throw "Collection was modified", from the engine's own
    // enumerator and so outside every try here (#1028). Each of the three helpers takes a Watch before its call and asks it
    // afterwards; the call whose watch says changed is where the change happened. The watch takes no lock and allocates
    // nothing. A Mission.Current of null (a unit test) gives an empty watch that reports nothing.

    /// <summary>
    /// A list as it was before one wrapped call: the list, its size and an enumerator taken then. Stepping a copy of that
    /// enumerator afterwards throws exactly when the engine's own next step through the list will, whatever the change was.
    /// </summary>
    internal readonly struct Watch<T> where T : class
    {
        private readonly List<T>? _list;
        private readonly List<T>.Enumerator _probe;

        internal Watch(List<T>? list)
        {
            _list = list;
            _probe = list != null ? list.GetEnumerator() : default;
            Before = list?.Count ?? -1;
        }

        internal List<T>? Items => _list;

        internal int Before { get; }

        internal bool Changed(out int after)
        {
            after = _list?.Count ?? -1;
            if (_list == null) return false;
            var probe = _probe;   // a copy: stepping it never moves the watch
            try { probe.MoveNext(); return false; }
            catch (InvalidOperationException) { return true; /* swallow-ok: the exception is the signal */ }
        }

        internal T? Newest() => _list != null && _list.Count > 0 ? _list[_list.Count - 1] : null;
    }

    internal static Watch<MissionBehavior> WatchBehaviors()
    {
        try { return new Watch<MissionBehavior>(Mission.Current?.MissionBehaviors); }
        catch { return default; /* swallow-ok: a watch must never break the mission start */ }
    }

    internal static Watch<MissionObject> WatchObjects()
    {
        try { return new Watch<MissionObject>(Mission.Current?.MissionObjects); }
        catch { return default; /* swallow-ok: a watch must never break the mission start */ }
    }

    internal static void NoteListChange<T>(StartCall call, object owner, Watch<T> watch) where T : class
    {
        try
        {
            if (!watch.Changed(out var after)) return;
            var newest = after > watch.Before ? watch.Newest() : null;   // the last item is the added one only for an append
            Service?.ReportListChange(call, TypeNameOf(owner), AssemblyNameOf(owner.GetType()),
                typeof(T) == typeof(MissionBehavior) ? "MissionBehaviors" : "MissionObjects", watch.Before, after,
                newest == null ? "n/a" : TypeNameOf(newest));
        }
        catch { /* swallow-ok: observe-only, a log line must never change the engine's outcome */ }
    }

    // #1028: while a wrapped EarlyStart or AfterStart runs, the engine is walking MissionBehaviors, so an add made then
    // is queued (MissionBehaviorDeferral) and applied when Mission.AfterStart ends. The window is closed right after the
    // call; a throw that is not survived leaves it open until Patch103's finalizer resets it.
    private static void OpenDeferralWindow(StartCall call, MissionBehavior owner)
    {
        try { MissionBehaviorDeferral.Open(Mission.Current, call, owner); }
        catch { /* swallow-ok: no window means the engine's vanilla add, which is what ran before this guard */ }
    }

    /// <summary>
    /// The prefix on <c>Mission.AddMissionBehavior</c> asks this. True queues <paramref name="behavior"/> and means the
    /// engine's add must be skipped; false (no window, another mission, the toggle off, no service, any failure) leaves
    /// the add to the engine. The queue is applied by <see cref="FlushDeferredAdds"/>.
    /// </summary>
    internal static bool TryDeferAdd(object mission, MissionBehavior behavior)
    {
        try
        {
            var service = Service;
            if (service == null || !MissionBehaviorDeferral.IsOpenFor(mission) || !service.ShouldDeferBehaviorAdds()) return false;
            if (!MissionBehaviorDeferral.TryDefer(mission, behavior)) return false;
        }
        catch { return false; /* swallow-ok: not deferred means the engine adds it now, as before */ }

        try
        {
            var owner = MissionBehaviorDeferral.WindowOwner;
            Service?.ReportDeferredAdd(MissionBehaviorDeferral.WindowCall, owner == null ? "<null>" : TypeNameOf(owner), TypeNameOf(behavior));
        }
        catch { /* swallow-ok: the behaviour is queued; a log line must not undo that */ }
        return true;
    }

    /// <summary>Applies the queued adds through the engine's own <c>AddMissionBehavior</c> once the walk is over (Patch103's finalizer).</summary>
    internal static void FlushDeferredAdds()
    {
        try { MissionBehaviorDeferral.Flush((mission, behavior) => ((Mission)mission).AddMissionBehavior(behavior)); }
        catch { /* swallow-ok: Flush isolates each add; this guards the call itself */ }
    }

    private static string TypeNameOf(object value) => value.GetType().FullName ?? value.GetType().Name;

    // A throw inside an exception filter is swallowed by the runtime as "not handled", so this is belt and braces.
    private static bool Survives(Exception ex)
    {
        try { return Service?.ShouldSurvive(ex) ?? false; }
        catch { return false; }
    }

    private static void Note(StartCall call, object? owner, Exception ex)
    {
        try
        {
            var type = owner?.GetType();
            Service?.Report(call, type?.FullName ?? type?.Name ?? "<null>", type == null ? "unknown" : AssemblyNameOf(type), ex);
        }
        catch
        {
            // The service never throws; this keeps even a broken one from replacing the exception we survived.
        }
    }

    private static string AssemblyNameOf(Type type)
    {
        try { return type.Assembly.GetName().Name ?? "unknown"; }
        catch { return "unknown"; }
    }
}
