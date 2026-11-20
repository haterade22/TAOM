using System.Collections.Concurrent;
using System.Collections.Generic;

namespace TAOM.Core.Collections;

/// <summary>
/// Who holds a claim on one shared switch per key, for a switch that several features need on at the same time
/// (#1497: the engine's one AI input callback flag per agent, used by Marshal's Word and by stamina). An owner is one
/// bit. The switch is on while any owner holds a claim. Keys compare by reference. Writers run on one thread (the
/// mission tick); <see cref="Holds"/> may be asked from any thread.
/// </summary>
public sealed class OwnerClaimLedger<TKey>
    where TKey : class
{
    private readonly ConcurrentDictionary<TKey, int> _claims = new ConcurrentDictionary<TKey, int>(ReferenceIdentity.Instance);

    public int Count => _claims.Count;

    /// <summary>Adds the owner's claim. True when this is the first claim on the key: the caller switches the switch on.</summary>
    public bool Claim(TKey key, int owner)
    {
        _claims.TryGetValue(key, out var before);
        var after = before | owner;
        if (after != before)
            _claims[key] = after;
        return before == 0 && after != 0;
    }

    /// <summary>Removes the owner's claim. True when it was the last claim on the key: the caller switches the switch off.</summary>
    public bool Release(TKey key, int owner)
    {
        if (!_claims.TryGetValue(key, out var before))
            return false;
        var after = before & ~owner;
        if (after == 0)
            _claims.TryRemove(key, out _);
        else if (after != before)
            _claims[key] = after;
        return before != 0 && after == 0;
    }

    public bool Holds(TKey key, int owner) => _claims.TryGetValue(key, out var held) && (held & owner) != 0;

    /// <summary>Removes the owner's claim from every key, for a mission end. Returns nothing: the caller makes no switch write.</summary>
    public void DropOwner(int owner)
    {
        foreach (var pair in _claims)
        {
            var after = pair.Value & ~owner;
            if (after == 0)
                ((ICollection<KeyValuePair<TKey, int>>)_claims).Remove(pair);
            else if (after != pair.Value)
                _claims.TryUpdate(pair.Key, after, pair.Value);
        }
    }
}
