using System.Collections.Generic;
using System.Runtime.CompilerServices;
using TAOM.Features.FordsOfWar.Domain;

namespace TAOM.Adapters;

/// <summary>
/// The agent behind each <see cref="AgentKey"/> of a Fords of War mission. Keys come from a counter that only goes up and is
/// never reused; agents are held by object reference, never by <c>Agent.Index</c>, which the engine hands to the next agent it
/// builds (csharp-architecture.md "Mission-scope agent handles"). The entry is evicted when the engine removes the agent. Every
/// member takes one lock, because the removal callback can arrive off the main thread. Holds <c>object</c> so the identity
/// rule and the counter are unit-tested without an engine; the battlefield adapter casts back to <c>Agent</c>.
/// </summary>
internal sealed class FordsAgentRegistry
{
    private sealed class Entry
    {
        public Entry(AgentKey key, object agent, MatchSide side, int lane)
        {
            Key = key;
            Agent = agent;
            Side = side;
            Lane = lane;
        }

        public AgentKey Key { get; }
        public object Agent { get; }
        public MatchSide Side { get; }
        public int Lane { get; }
    }

    private sealed class ReferenceComparer : IEqualityComparer<object>
    {
        public static readonly ReferenceComparer Instance = new();

        bool IEqualityComparer<object>.Equals(object? x, object? y) => ReferenceEquals(x, y);

        int IEqualityComparer<object>.GetHashCode(object obj) => RuntimeHelpers.GetHashCode(obj);
    }

    /// <summary>How many evicted agents keep a findable key. A killer that dies in the same frame as its victim is still credited.</summary>
    internal const int RecentLimit = 256;

    private readonly object _gate = new();
    private readonly Dictionary<object, Entry> _byAgent = new(ReferenceComparer.Instance);
    private readonly Dictionary<AgentKey, Entry> _byKey = new();
    private readonly Dictionary<object, AgentKey> _recent = new(ReferenceComparer.Instance);
    private readonly Queue<object> _recentOrder = new();
    private int _next;

    public int Count
    {
        get
        {
            lock (_gate)
                return _byAgent.Count;
        }
    }

    /// <summary>The key of <paramref name="agent"/>; a new one the first time, the same one after.</summary>
    public AgentKey Register(object agent, MatchSide side, int lane)
    {
        lock (_gate)
        {
            if (_byAgent.TryGetValue(agent, out var existing))
                return existing.Key;

            var entry = new Entry(new AgentKey(++_next), agent, side, lane);
            _byAgent[agent] = entry;
            _byKey[entry.Key] = entry;
            return entry.Key;
        }
    }

    public bool TryGetKey(object? agent, out AgentKey key)
    {
        key = default;
        if (agent == null)
            return false;

        lock (_gate)
        {
            if (!_byAgent.TryGetValue(agent, out var entry))
                return false;

            key = entry.Key;
            return true;
        }
    }

    /// <summary>The key of a live agent, or of one evicted among the last <see cref="RecentLimit"/>. For crediting a killer.</summary>
    public bool TryGetRecentKey(object? agent, out AgentKey key)
    {
        key = default;
        if (agent == null)
            return false;

        lock (_gate)
        {
            if (_byAgent.TryGetValue(agent, out var entry))
            {
                key = entry.Key;
                return true;
            }

            return _recent.TryGetValue(agent, out key);
        }
    }

    public bool TryGetAgent(AgentKey key, out object agent, out MatchSide side, out int lane)
    {
        lock (_gate)
        {
            if (_byKey.TryGetValue(key, out var entry))
            {
                agent = entry.Agent;
                side = entry.Side;
                lane = entry.Lane;
                return true;
            }
        }

        agent = null!;
        side = default;
        lane = 0;
        return false;
    }

    /// <summary>Forgets a removed agent. Its key is not handed out again.</summary>
    public bool Evict(object agent, out AgentKey key)
    {
        lock (_gate)
        {
            if (!_byAgent.TryGetValue(agent, out var entry))
            {
                key = default;
                return false;
            }

            _byAgent.Remove(agent);
            _byKey.Remove(entry.Key);
            Remember(agent, entry.Key);
            key = entry.Key;
            return true;
        }
    }

    private void Remember(object agent, AgentKey key)
    {
        _recent[agent] = key;
        _recentOrder.Enqueue(agent);
        while (_recentOrder.Count > RecentLimit)
            _recent.Remove(_recentOrder.Dequeue());
    }

    /// <summary>A copy of every registered agent, safe to walk while the engine removes agents.</summary>
    public IReadOnlyList<(AgentKey Key, object Agent, MatchSide Side, int Lane)> Snapshot()
    {
        lock (_gate)
        {
            var copy = new List<(AgentKey, object, MatchSide, int)>(_byAgent.Count);
            foreach (var entry in _byAgent.Values)
                copy.Add((entry.Key, entry.Agent, entry.Side, entry.Lane));
            return copy;
        }
    }

    /// <summary>Drops every entry (mission end). The counter keeps going, so a key is never reused within the process.</summary>
    public void Clear()
    {
        lock (_gate)
        {
            _byAgent.Clear();
            _byKey.Clear();
            _recent.Clear();
            _recentOrder.Clear();
        }
    }
}
