using System;
using System.Collections.Generic;
using TAOM.Features.MissionStartGuard.Models;
using TaleWorlds.MountAndBlade;

namespace TAOM.Features.MissionStartGuard.Hooks;

/// <summary>
/// Queues a mission behaviour that a start call adds while the engine walks <c>Mission.MissionBehaviors</c> (#1028).
/// <c>Mission.AfterStart</c> walks that list with <c>foreach</c> around every <c>EarlyStart</c> and <c>AfterStart</c>
/// call, so an add made inside one of them makes the engine's next step throw "Collection was modified" and load the
/// mission again. DualWield does it: its <c>Mission.SpawnAgent</c> postfix adds its behaviour on the first human spawn,
/// and an arena practice fight started from the town menu spawns its fighters from a <c>CampaignEvents.AfterMissionStarted</c>
/// listener, which <c>CampaignMissionComponent.AfterStart</c> raises inside the walk.
///
/// <para>The window is open only around those two wrapped calls (<see cref="MissionStartGuardCalls"/> opens and closes
/// it; a throw that is not survived leaves it open until Patch103's finalizer calls <see cref="Flush"/>). An add outside
/// them is never touched: <c>OnBehaviorInitialize</c> is an index loop over the live count, so an add there is safe and
/// vanilla makes one on purpose (four <c>OnMissionStarted</c> listeners), and <c>OnMissionBehaviorInitialize</c> runs before
/// the walk. The queue is applied through the engine's own <c>AddMissionBehavior</c> when <c>Mission.AfterStart</c> ends
/// (<see cref="Flush"/>, from Patch103's finalizer), in add order. A queued behaviour misses the start calls the walk
/// would have given it; <c>AddMissionBehavior</c> itself runs in full, <c>OnCreated</c> included.</para>
///
/// <para>Main thread only, like <c>Mission.AfterStart</c>. The state holds the mission as an opaque token, so it can
/// be tested without an engine mission.</para>
/// </summary>
internal static class MissionBehaviorDeferral
{
    // [ThreadStatic]: the window is opened by the helpers on the thread that runs Mission.AfterStart, and an add from
    // any other thread sees no window and runs vanilla. The queue lives with the window (harmony-patches.md: patches
    // on two methods of one call chain share a thread-static, cleared in a finalizer).
    [ThreadStatic] private static List<MissionBehavior>? _queue;
    [ThreadStatic] private static object? _queueMission;
    [ThreadStatic] private static object? _windowMission;
    [ThreadStatic] private static StartCall _windowCall;
    [ThreadStatic] private static object? _windowOwner;

    /// <summary>The mission whose start call is running, or null when no window is open.</summary>
    internal static object? WindowMission => _windowMission;

    internal static StartCall WindowCall => _windowCall;

    /// <summary>The behaviour whose <c>EarlyStart</c> or <c>AfterStart</c> is running: the call during which an add was made.</summary>
    internal static object? WindowOwner => _windowOwner;

    internal static int Pending => _queue?.Count ?? 0;

    /// <summary>Opens the window for one wrapped call. A null mission (no current mission) opens none.</summary>
    internal static void Open(object? mission, StartCall call, object? owner)
    {
        _windowMission = mission;
        _windowCall = call;
        _windowOwner = mission == null ? null : owner;
    }

    internal static void Close()
    {
        _windowMission = null;
        _windowOwner = null;
    }

    internal static bool IsOpenFor(object mission) => _windowMission != null && ReferenceEquals(_windowMission, mission);

    /// <summary>Queues <paramref name="behavior"/> when a window is open for <paramref name="mission"/>; true means the engine's add must be skipped.</summary>
    internal static bool TryDefer(object mission, MissionBehavior? behavior)
    {
        if (behavior == null || !IsOpenFor(mission)) return false;
        _queueMission = mission;
        (_queue ??= new List<MissionBehavior>()).Add(behavior);
        return true;
    }

    /// <summary>Closes the window and drops anything queued: a new mission start begins with nothing from an old one.</summary>
    internal static void Reset()
    {
        Close();
        _queue?.Clear();
        _queueMission = null;
    }

    /// <summary>
    /// Closes the window first (so an add made by <paramref name="add"/> is not queued again), then hands every queued
    /// behaviour to <paramref name="add"/> in the order it was queued. One add that throws costs only that behaviour.
    /// Returns how many were added.
    /// </summary>
    internal static int Flush(Action<object, MissionBehavior> add)
    {
        var mission = _queueMission;
        var items = _queue?.ToArray() ?? Array.Empty<MissionBehavior>();
        Reset();
        if (mission == null) return 0;

        var added = 0;
        foreach (var behavior in items)
        {
            try { add(mission, behavior); added++; }
            catch { /* swallow-ok: the engine's add would have thrown into the mod that called it; the rest still go in */ }
        }
        return added;
    }
}
