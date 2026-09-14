using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;

namespace TAOM.Adapters;

/// <summary>
/// The store behind <see cref="MissionAdapterFactory"/>: one adapter per engine agent, keyed by the
/// agent OBJECT. It was keyed by <c>Agent.Index</c>, and the engine hands a deleted agent's index to
/// the next agent it creates. The managed <c>Agent</c> outlives its deletion with its native pointers
/// still aimed at the recycled slot, so an index-keyed entry aliases whoever lives there next: on
/// 2026-09-13 a reinforcement horse inherited a dead warg's index, was served the warg's adapter,
/// got a warg behavior tree, and asked the engine to play <c>act_warg_attack_running</c> on
/// <c>as_horse</c>; the same session's second log shows dead spiders' adapters driving live spiders
/// (#592). Identity here is reference identity, explicitly, so no <c>Equals</c> override on the key
/// type can ever merge two agents. Indices are kept only to count how often the engine reuses one:
/// <see cref="Evict"/> records a freed index, <see cref="NoteBuilt"/> reports the build that lands on
/// it. Every method takes one lock: the engine's agent-removed and agent-built callbacks arrive on the
/// main thread while any code still running on the asynchronous agent tick may be reading.
/// Pure: no engine types, so the identity rule and the reuse count are unit-tested.
/// </summary>
public sealed class AgentAdapterCache
{
    private readonly object _gate = new();
    private readonly Dictionary<object, IAgentAdapter> _byAgent = new(ReferenceComparer.Instance);
    private readonly HashSet<int> _freedIndices = new();
    private int _reuseCount;

    /// <summary>Adapters currently held.</summary>
    public int Count
    {
        get { lock (_gate) return _byAgent.Count; }
    }

    /// <summary>How many builds this mission landed on an index a deleted agent had held.</summary>
    public int ReuseCount
    {
        get { lock (_gate) return _reuseCount; }
    }

    /// <summary>The adapter held for <paramref name="agent"/>, by reference; false for null or unknown.</summary>
    public bool TryGet(object agent, out IAgentAdapter adapter)
    {
        if (agent == null)
        {
            adapter = null;
            return false;
        }
        lock (_gate) return _byAgent.TryGetValue(agent, out adapter);
    }

    /// <summary>Hold <paramref name="adapter"/> for <paramref name="agent"/>, replacing any earlier entry.</summary>
    public void Add(object agent, IAgentAdapter adapter)
    {
        if (agent == null) throw new ArgumentNullException(nameof(agent));
        if (adapter == null) throw new ArgumentNullException(nameof(adapter));
        lock (_gate) _byAgent[agent] = adapter;
    }

    /// <summary>
    /// The adapter held for <paramref name="agent"/>, or the one <paramref name="build"/> makes for it,
    /// decided under the lock so two concurrent misses cannot wrap one agent twice. A hit allocates
    /// nothing; pass a cached delegate.
    /// </summary>
    public IAgentAdapter GetOrAdd(object agent, Func<object, IAgentAdapter> build)
    {
        if (agent == null) throw new ArgumentNullException(nameof(agent));
        if (build == null) throw new ArgumentNullException(nameof(build));
        lock (_gate)
        {
            if (_byAgent.TryGetValue(agent, out IAgentAdapter adapter)) return adapter;
            adapter = build(agent) ?? throw new InvalidOperationException("the adapter factory returned null");
            _byAgent[agent] = adapter;
            return adapter;
        }
    }

    /// <summary>
    /// Forget a deleted agent. Its index is remembered as freed whether or not it was ever adapted,
    /// because deletion is what makes the index available to the next agent. Returns true when an
    /// adapter was dropped.
    /// </summary>
    public bool Evict(object agent, int index)
    {
        lock (_gate)
        {
            _freedIndices.Add(index);
            return agent != null && _byAgent.Remove(agent);
        }
    }

    /// <summary>
    /// An agent was built at <paramref name="index"/>. True when that index was freed by an earlier
    /// deletion this mission: one deletion, one reuse, counted once.
    /// </summary>
    public bool NoteBuilt(int index)
    {
        lock (_gate)
        {
            if (!_freedIndices.Remove(index)) return false;
            _reuseCount++;
            return true;
        }
    }

    /// <summary>Mission end: drop everything, including the freed-index memory and the count.</summary>
    public void Clear()
    {
        lock (_gate)
        {
            _byAgent.Clear();
            _freedIndices.Clear();
            _reuseCount = 0;
        }
    }

    private sealed class ReferenceComparer : IEqualityComparer<object>
    {
        public static readonly ReferenceComparer Instance = new();

        bool IEqualityComparer<object>.Equals(object x, object y) => ReferenceEquals(x, y);

        int IEqualityComparer<object>.GetHashCode(object obj) => RuntimeHelpers.GetHashCode(obj);
    }
}
