using System;
using System.Collections.Generic;
using TAOM.Features.LongDefeat.Domain;

namespace TAOM.Adapters;

/// <summary>
/// What the Long Defeat field keeps about the lord's body, apart from the engine (phase2-plan.md Slice 10, spike S9). Three jobs, all by object reference and never by <c>Agent.Index</c>, which the
/// engine hands to the next agent it builds (csharp-architecture.md "Mission-scope agent handles"): the body a remount fades is a planned removal, matched by identity for whatever state the engine
/// reports; the callbacks that reach the lord's bodies are kept until the director logs them on the main thread; and a hit is sorted into sally contact. Every member is safe from the engine's callback
/// threads: one lock, and no engine call. Holds <c>object</c>, so the rules are unit-tested without an engine; the battlefield adapter casts back to <c>Agent</c>.
/// </summary>
internal sealed class LongDefeatLordWatch
{
    private readonly object _gate = new();
    private readonly List<LdBodyEvent> _events = new();
    private volatile object? _lord;
    private volatile object? _planned;

    /// <summary>The body the field spawned for the lord last, or null.</summary>
    public object? Lord => _lord;

    public void SetLord(object? lord) => _lord = lord;

    /// <summary>The body a remount is about to fade: its removal, in any state, is no death of the lord's. Kept until <see cref="Clear"/>: native reports the faded body Routed inside <c>FadeOut</c>, before the new body spawns, and only its deletion comes later.</summary>
    public void Plan(object old) => _planned = old;

    public bool IsPlanned(object? agent) => agent != null && ReferenceEquals(agent, _planned);

    /// <summary>
    /// A removal or a deletion reached an agent. Kept when it is the planned body, the lord's current one or the main agent (<paramref name="isMain"/>), for the director to log; anyone else's is not.
    /// <paramref name="loss"/> says the state is a kill or a knock-out.
    /// </summary>
    public void Note(object agent, string callback, string state, bool loss, bool isMain)
    {
        var planned = IsPlanned(agent);
        if (!planned && !isMain && !ReferenceEquals(agent, _lord))
            return;

        lock (_gate)
            _events.Add(new LdBodyEvent(callback, state, planned, loss));
    }

    /// <summary>The events kept so far, once, in the order they came.</summary>
    public IReadOnlyList<LdBodyEvent> Take()
    {
        lock (_gate)
        {
            if (_events.Count == 0)
                return Array.Empty<LdBodyEvent>();

            var taken = _events.ToArray();
            _events.Clear();
            return taken;
        }
    }

    /// <summary>
    /// Sorts one hit. Contact is the lord's blow (or his horse's) on an attacker, an attacker's blow on the lord, or on his horse: the other side must be an attacker (<paramref name="isAttacker"/>), so a friend's
    /// blow, a stranger's and the lord's own on himself are nothing. A kill is the lord's blow that left its victim under 1 health, read in the hit callback after the engine wrote it. False for a hit with no
    /// lord, no victim or no affector.
    /// </summary>
    public bool TryContact(object? affected, object? affector, object? lordMount, float victimHealth, Func<object?, bool> isAttacker, out LdContact contact)
    {
        contact = default;
        var lord = _lord;
        if (lord == null || affected == null || affector == null || ReferenceEquals(affected, affector))
            return false;

        var byLord = ReferenceEquals(affector, lord) || (lordMount != null && ReferenceEquals(affector, lordMount));
        if (byLord)
        {
            if (!isAttacker(affected))
                return false;

            contact = new LdContact(LdContactKind.Given, victimHealth < 1f);
            return true;
        }

        var onLord = ReferenceEquals(affected, lord);
        var onMount = lordMount != null && ReferenceEquals(affected, lordMount);
        if ((!onLord && !onMount) || !isAttacker(affector))
            return false;

        contact = new LdContact(onLord ? LdContactKind.Taken : LdContactKind.MountTaken, false);
        return true;
    }

    public void Clear()
    {
        _lord = null;
        _planned = null;
        lock (_gate)
            _events.Clear();
    }
}
