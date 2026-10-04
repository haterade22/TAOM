using System;
using System.Collections.Generic;
using System.Threading;

namespace BehaviorTreeWrapper;

/// <summary>
/// Counts what <see cref="BehaviorTreeMissionLogic"/>'s early returns no longer show in taom_debug.log (plan 033,
/// DECISIONS D6): every routed callback that returned because no tree listened, and every callback parked off-thread
/// for the mission tick. It writes nothing itself: <see cref="NoteSkip"/> returns the mission's one reason line and
/// <see cref="Summary"/> the mission-end line, which the mission logic logs. Any thread. Engine-free, so both lines
/// are pinned without the game.
/// </summary>
internal sealed class CallbackSkipLedger
{
    /// <summary>The engine callbacks the mission logic routes to tree listeners.</summary>
    internal enum Callback
    {
        OnAgentDismount, OnAgentFleeing, OnAgentDeleted, OnAgentMount, OnAgentPanicked, OnAgentRemoved,
        OnAgentShootMissile, OnFocusGained, OnFocusLost, OnAgentAlarmedStateChanged, OnAgentHit, OnObjectUsed,
        OnObjectDisabled, OnObjectStoppedBeingUsed,
    }

    private static readonly Callback[] Callbacks = (Callback[])Enum.GetValues(typeof(Callback));
    private readonly int[] _skipped = new int[Callbacks.Length];
    private int _parked;
    private int _skipNoted;

    /// <summary>Counts a callback that returned early. Returns the reason line for the mission's first one, else null.</summary>
    internal string? NoteSkip(Callback callback)
    {
        Interlocked.Increment(ref _skipped[(int)callback]);
        if (Interlocked.Exchange(ref _skipNoted, 1) != 0) return null;
        return $"[BehaviorTree] {callback} had no tree listener, so it returned before building arguments or parking " +
            "a replay; every such skip this mission is counted in the mission-end summary.";
    }

    /// <summary>Counts a callback parked off-thread for the next mission tick.</summary>
    internal void NoteParked() => Interlocked.Increment(ref _parked);

    /// <summary>The mission-end line: every skip per callback, and every parked callback.</summary>
    internal string Summary()
    {
        int total = 0;
        var parts = new List<string>();
        foreach (Callback callback in Callbacks)
        {
            int count = Volatile.Read(ref _skipped[(int)callback]);
            if (count == 0) continue;
            total += count;
            parts.Add($"{callback} {count}");
        }
        string detail = parts.Count > 0 ? " (" + string.Join(", ", parts) + ")" : "";
        return $"[BehaviorTree] Mission end: {total} callbacks skipped with no tree listener{detail}; " +
            $"{Volatile.Read(ref _parked)} parked off-thread for the mission tick.";
    }

    /// <summary>Starts the counts and the reason line again for the next mission.</summary>
    internal void Reset()
    {
        Array.Clear(_skipped, 0, _skipped.Length);
        Interlocked.Exchange(ref _parked, 0);
        Interlocked.Exchange(ref _skipNoted, 0);
    }
}
