using System;
using System.Collections.Concurrent;

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

    /// <summary>Callbacks waiting for the next drain.</summary>
    public int Count => _queue.Count;

    public void Enqueue(Action callback)
    {
        if (callback == null) throw new ArgumentNullException(nameof(callback));
        _queue.Enqueue(callback);
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
