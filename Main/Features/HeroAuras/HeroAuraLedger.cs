using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using TAOM.Features.RaceAbilities.Domain;

namespace TAOM.Features.HeroAuras;

/// <summary>
/// The aura each soldier carries now, and until when. The mission logic stamps and expires on the main thread; the
/// stat, damage and morale models read <see cref="EffectsOf"/> from any thread, so each entry is immutable and the map
/// concurrent. Keyed by the agent reference, never by <c>Agent.Index</c>, which the engine reuses within a mission.
/// Also counts, per aura, how many soldiers it reached in the battle, for the end-of-battle log line.
/// </summary>
public sealed class HeroAuraLedger<TKey> where TKey : class
{
    private readonly ConcurrentDictionary<TKey, Entry> _entries = new ConcurrentDictionary<TKey, Entry>();
    private readonly Dictionary<string, HashSet<TKey>> _reach = new Dictionary<string, HashSet<TKey>>();

    public int Count => _entries.Count;

    public RaceAbilityEffects? EffectsOf(TKey key) => _entries.TryGetValue(key, out var entry) ? entry.Effects : null;

    // Same aura key as before: only the end moves, and the soldier's effects come back. Null: he needs a new stamp,
    // and his stats a refresh. The aura key alone decides it (HeroAura.KeyFor).
    public RaceAbilityEffects? TryExtend(TKey key, string auraKey, float until)
    {
        if (!_entries.TryGetValue(key, out var old) || old.AuraKey != auraKey)
            return null;
        _entries[key] = new Entry(auraKey, old.Effects, until);
        return old.Effects;
    }

    public void Stamp(TKey key, string auraKey, RaceAbilityEffects effects, float until) =>
        _entries[key] = new Entry(auraKey, effects, until);

    public void Expire(float now, List<TKey> expired)
    {
        expired.Clear();
        foreach (var pair in _entries)
            if (pair.Value.Until <= now)
                expired.Add(pair.Key);
        foreach (var key in expired)
            _entries.TryRemove(key, out _);
    }

    public void Remove(TKey key) => _entries.TryRemove(key, out _);

    public void RecordReach(string auraId, TKey key)
    {
        if (!_reach.TryGetValue(auraId, out var soldiers))
            _reach[auraId] = soldiers = new HashSet<TKey>();
        soldiers.Add(key);
    }

    // Soldiers reached by any aura, each counted once.
    public int ReachTotal => _reach.Values.SelectMany(soldiers => soldiers).Distinct().Count();

    public List<string> ReachLines() =>
        _reach.OrderBy(pair => pair.Key).Select(pair => $"{pair.Key}: {pair.Value.Count} soldier(s)").ToList();

    public void Clear()
    {
        _entries.Clear();
        _reach.Clear();
    }

    private sealed class Entry
    {
        public Entry(string auraKey, RaceAbilityEffects effects, float until)
        {
            AuraKey = auraKey;
            Effects = effects;
            Until = until;
        }

        public string AuraKey { get; }

        public RaceAbilityEffects Effects { get; }

        public float Until { get; }
    }
}
