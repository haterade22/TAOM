using System.Collections.Generic;
using System.Runtime.CompilerServices;

namespace TAOM.Features.HeroAbilities.Domain;

/// <summary>
/// The heroes whose ranged kit is armed, keyed by reference. Model methods run on any thread, so a reader takes
/// the published map and never sees a half-built one; a writer copies, changes and publishes under a lock.
/// </summary>
public sealed class HeroRangedRoster<TKey> where TKey : class
{
    private sealed class ReferenceComparer : IEqualityComparer<TKey>
    {
        public bool Equals(TKey? x, TKey? y) => ReferenceEquals(x, y);

        public int GetHashCode(TKey obj) => RuntimeHelpers.GetHashCode(obj);
    }

    private static readonly ReferenceComparer Comparer = new ReferenceComparer();

    private readonly object _writeLock = new object();
    private volatile Dictionary<TKey, HeroRangedConfig> _map = new Dictionary<TKey, HeroRangedConfig>(Comparer);

    public bool IsEmpty => _map.Count == 0;

    // A published map is never written again, so its keys are a snapshot.
    public IReadOnlyCollection<TKey> Keys => _map.Keys;

    public HeroRangedConfig? KitOf(TKey? key)
    {
        if (key == null)
            return null;
        return _map.TryGetValue(key, out var kit) ? kit : null;
    }

    public void Add(TKey key, HeroRangedConfig kit)
    {
        lock (_writeLock)
        {
            var copy = new Dictionary<TKey, HeroRangedConfig>(_map, Comparer);
            copy[key] = kit;
            _map = copy;
        }
    }

    public bool Remove(TKey key)
    {
        if (key == null)
            return false;
        lock (_writeLock)
        {
            if (!_map.ContainsKey(key))
                return false;
            var copy = new Dictionary<TKey, HeroRangedConfig>(_map, Comparer);
            copy.Remove(key);
            _map = copy;
            return true;
        }
    }

    public void Clear()
    {
        lock (_writeLock)
            _map = new Dictionary<TKey, HeroRangedConfig>(Comparer);
    }
}
