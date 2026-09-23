using System;
using System.Collections.Concurrent;
using TAOM.Features.AdvancedCombat;

namespace BehaviorTreeWrapper;

/// <summary>
/// Engine callbacks that reach <see cref="BehaviorTreeMissionLogic"/> off the main mission thread
/// are parked here and replayed from the next <c>OnMissionTick</c>, so the listener and tree maps are
/// only ever touched from that tick. The route that needs it is verified on v1.4.8:
/// <c>CommonAIComponent.OnTick</c> runs inside the asynchronous agent tick and calls
/// <c>Mission.OnAgentPanicked</c> synchronously (<c>CommonAIComponent.cs:119-135</c>), which reaches
/// every behavior's <c>OnAgentPanicked</c> on that thread (#595, Codex review 109). Pure: no engine types.
/// </summary>
public sealed class DeferredCallbackQueue
{
    private readonly ConcurrentQueue<Action> _queue = new();
    private readonly Action<string>? _report;

    /// <param name="report">Where <see cref="RunOrDefer"/> reports a site's first off-thread call. It runs on
    /// the calling thread, so it must be thread-safe: the file log, never an on-screen logger.</param>
    public DeferredCallbackQueue(Action<string>? report = null) => _report = report;

    /// <summary>Callbacks waiting for the next drain.</summary>
    public int Count => _queue.Count;

    public void Enqueue(Action callback)
    {
        if (callback == null) throw new ArgumentNullException(nameof(callback));
        _queue.Enqueue(callback);
    }

    /// <summary>
    /// Runs <paramref name="action"/> now on the main mission thread; anywhere else parks it for the
    /// next drain and reports <paramref name="site"/> once through the queue's reporter. Returns true
    /// when parked. <c>OnAgentRemoved</c> reaches behaviors and agent components off the main thread too:
    /// a player's log caught it there on v1.4.8 (#634).
    /// </summary>
    public bool RunOrDefer(string site, Action action)
    {
        if (action == null) throw new ArgumentNullException(nameof(action));
        if (MissionThreadGuard.NoteCall(site, _report))
        {
            _queue.Enqueue(action);
            return true;
        }
        action();
        return false;
    }

    /// <summary>
    /// Runs, in order, every callback queued before this call and returns how many ran. A callback
    /// queued while draining waits for the next drain, so a replayed callback that queues another
    /// cannot spin this loop.
    /// </summary>
    public int Drain()
    {
        int budget = _queue.Count;
        int ran = 0;
        while (ran < budget && _queue.TryDequeue(out Action callback))
        {
            ran++;
            callback();
        }
        return ran;
    }

    /// <summary>Drop everything without running it (mission end).</summary>
    public void Clear()
    {
        while (_queue.TryDequeue(out _)) { }
    }
}
