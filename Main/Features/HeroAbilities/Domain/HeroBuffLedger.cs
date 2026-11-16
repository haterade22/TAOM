using System.Collections.Generic;
using TAOM.Features.RaceAbilities.Domain;

namespace TAOM.Features.HeroAbilities.Domain;

/// <summary>
/// The timed effects a hero ability grants, per agent (#1359). The game models read an agent's effects from any
/// thread, so the readers see one published snapshot: every change builds a new dictionary and swaps the reference,
/// and nothing mutates a published one. The writer state (<c>_grants</c>) is main-thread only. A key is held
/// by reference (Agent has no Equals override), so a recycled slot index never inherits a grant. Several grants on
/// one key fold with <see cref="RaceAbilityEffectsMerge"/>: the stronger value per channel, never the sum.
/// </summary>
public sealed class HeroBuffLedger<TKey> where TKey : class
{
    private sealed class GrantRecord
    {
        public GrantRecord(string source, RaceAbilityEffects effects, float until)
        {
            Source = source;
            Effects = effects;
            Until = until;
        }

        public string Source { get; }
        public RaceAbilityEffects Effects { get; }
        public float Until { get; }
    }

    private readonly Dictionary<TKey, List<GrantRecord>> _grants = new Dictionary<TKey, List<GrantRecord>>();
    private volatile Dictionary<TKey, RaceAbilityEffects> _published = new Dictionary<TKey, RaceAbilityEffects>();

    public int Count => _published.Count;

    public RaceAbilityEffects? EffectsOf(TKey key)
    {
        if (key == null)
            return null;
        return _published.TryGetValue(key, out var effects) ? effects : null;
    }

    // Published before it returns: the caller refreshes the agent's stats right after. A NaN end would never expire,
    // so it grants nothing.
    public void Grant(TKey key, string source, RaceAbilityEffects effects, float until)
    {
        if (key == null || effects == null || float.IsNaN(until))
            return;
        if (!_grants.TryGetValue(key, out var list))
        {
            list = new List<GrantRecord>();
            _grants[key] = list;
        }
        list.RemoveAll(g => g.Source == source);
        list.Add(new GrantRecord(source, effects, until));
        Publish(key, Fold(list));
    }

    // "now >= until" is the positive requirement, so a NaN now expires nothing. Appends the keys that lost a grant.
    public void Expire(float now, List<TKey> changed)
    {
        if (_grants.Count == 0)
            return;
        List<TKey>? touched = null;
        foreach (var pair in _grants)
        {
            if (pair.Value.Exists(g => now >= g.Until))
                (touched ??= new List<TKey>()).Add(pair.Key);
        }
        if (touched == null)
            return;

        var next = new Dictionary<TKey, RaceAbilityEffects>(_published);
        foreach (var key in touched)
        {
            var list = _grants[key];
            list.RemoveAll(g => now >= g.Until);
            if (list.Count == 0)
            {
                _grants.Remove(key);
                next.Remove(key);
            }
            else
            {
                next[key] = Fold(list)!;
            }
            changed.Add(key);
        }
        _published = next;
    }

    public bool Remove(TKey key)
    {
        if (key == null || !_grants.Remove(key))
            return false;
        Publish(key, null);
        return true;
    }

    public void Clear()
    {
        _grants.Clear();
        _published = new Dictionary<TKey, RaceAbilityEffects>();
    }

    private static RaceAbilityEffects? Fold(List<GrantRecord> list)
    {
        RaceAbilityEffects? folded = null;
        foreach (var grant in list)
            folded = RaceAbilityEffectsMerge.Combine(folded, grant.Effects);
        return folded;
    }

    private void Publish(TKey key, RaceAbilityEffects? effects)
    {
        var next = new Dictionary<TKey, RaceAbilityEffects>(_published);
        if (effects == null)
            next.Remove(key);
        else
            next[key] = effects;
        _published = next;
    }
}
