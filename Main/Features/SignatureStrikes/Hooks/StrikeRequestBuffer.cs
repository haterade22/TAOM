using System.Collections.Generic;

namespace TAOM.Features.SignatureStrikes.Hooks;

/// <summary>
/// The one-frame deferral between <c>OnMeleeHit</c> (enqueue) and <c>OnMissionTick</c> (drain).
/// Two lists swapped on drain, so anything enqueued while the drained list is being iterated
/// lands in the next drain rather than under the iterator. Allocation-free after warm-up, which
/// is why this is not the BT logic's <c>DeferredCallbackQueue</c> (a closure per enqueue).
///
/// Both ends run on the main mission thread (<c>MeleeHitCallback</c> is on the same engine
/// callback chain as <c>OnAgentHit</c>); <c>MissionThreadGuard.NoteCall</c> at the enqueue site
/// is the tripwire that would justify a concurrent queue.
/// </summary>
public sealed class StrikeRequestBuffer
{
    private List<StrikeRequest> _pending = new List<StrikeRequest>();
    private List<StrikeRequest> _draining = new List<StrikeRequest>();

    public int PendingCount => _pending.Count;

    public void Enqueue(in StrikeRequest request) => _pending.Add(request);

    /// <summary>Returns everything enqueued since the last swap; the returned list is reused by
    /// the swap after next, so iterate it before calling again.</summary>
    public List<StrikeRequest> Swap()
    {
        var drained = _draining;
        drained.Clear();
        _draining = _pending;
        _pending = drained;
        return _draining;
    }

    /// <summary>Drops both lists. Agent handles must not outlive the mission.</summary>
    public void Clear()
    {
        _pending.Clear();
        _draining.Clear();
    }
}
