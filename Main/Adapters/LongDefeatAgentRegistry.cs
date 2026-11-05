using System.Collections.Generic;
using System.Runtime.CompilerServices;
using TAOM.Features.FordsOfWar.Domain;
using TAOM.Features.LongDefeat.Domain;

namespace TAOM.Adapters;

/// <summary>
/// The role (roster soldier, summoned soldier, commander) and the felled mark of every attacker the Long Defeat field spawned, in the shape of <c>FordsAgentRegistry</c>: keys from a
/// counter that only goes up and is never reused, agents held by object reference and never by <c>Agent.Index</c>, which the engine hands to the next agent it builds
/// (csharp-architecture.md "Mission-scope agent handles"), every member under one lock because the removal callback can arrive off the main thread, and an entry evicted when the engine
/// removes the agent. The stalemate rule's <c>Fell</c> marks each soldier before it kills him, so the removal that follows is booked as felled by identity. Holds <c>object</c> so the
/// rules are unit-tested without an engine; the battlefield adapter casts back to <c>Agent</c>.
/// </summary>
internal sealed class LongDefeatAgentRegistry
{
    private sealed class Entry
    {
        public Entry(AgentKey key, object agent, AttackerRole role)
        {
            Key = key;
            Agent = agent;
            Role = role;
        }

        public AgentKey Key { get; }
        public object Agent { get; }
        public AttackerRole Role { get; }
        public bool Felled { get; set; }
    }

    private sealed class ReferenceComparer : IEqualityComparer<object>
    {
        public static readonly ReferenceComparer Instance = new();

        bool IEqualityComparer<object>.Equals(object? x, object? y) => ReferenceEquals(x, y);

        int IEqualityComparer<object>.GetHashCode(object obj) => RuntimeHelpers.GetHashCode(obj);
    }

    private readonly object _gate = new();
    private readonly Dictionary<object, Entry> _byAgent = new(ReferenceComparer.Instance);
    private int _next;

    public int Count
    {
        get
        {
            lock (_gate)
                return _byAgent.Count;
        }
    }

    /// <summary>The key of <paramref name="agent"/>: a new one the first time, the same one after (its first role stands).</summary>
    public AgentKey Register(object agent, AttackerRole role)
    {
        lock (_gate)
        {
            if (_byAgent.TryGetValue(agent, out var existing))
                return existing.Key;

            var entry = new Entry(new AgentKey(++_next), agent, role);
            _byAgent[agent] = entry;
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

    /// <summary>The role of a registered agent; a roster soldier for one the registry does not hold.</summary>
    public AttackerRole RoleOf(object agent)
    {
        lock (_gate)
            return _byAgent.TryGetValue(agent, out var entry) ? entry.Role : AttackerRole.Roster;
    }

    /// <summary>Marks a registered agent as felled by the stalemate rule. False for one the registry does not hold.</summary>
    public bool MarkFelled(object agent)
    {
        lock (_gate)
        {
            if (!_byAgent.TryGetValue(agent, out var entry))
                return false;

            entry.Felled = true;
            return true;
        }
    }

    public bool IsFelled(object agent)
    {
        lock (_gate)
            return _byAgent.TryGetValue(agent, out var entry) && entry.Felled;
    }

    /// <summary>
    /// The engine removed the agent: returns its role and felled mark and forgets it, once. For an agent the registry does not hold: false, a roster soldier, not felled.
    /// </summary>
    public bool TakeRemoval(object agent, out AttackerRole role, out bool felled)
    {
        lock (_gate)
        {
            if (!_byAgent.TryGetValue(agent, out var entry))
            {
                role = AttackerRole.Roster;
                felled = false;
                return false;
            }

            _byAgent.Remove(agent);
            role = entry.Role;
            felled = entry.Felled;
            return true;
        }
    }

    /// <summary>A copy of every registered agent, safe to walk while the engine removes agents.</summary>
    public IReadOnlyList<(AgentKey Key, object Agent, AttackerRole Role, bool Felled)> Snapshot()
    {
        lock (_gate)
        {
            var copy = new List<(AgentKey, object, AttackerRole, bool)>(_byAgent.Count);
            foreach (var entry in _byAgent.Values)
                copy.Add((entry.Key, entry.Agent, entry.Role, entry.Felled));
            return copy;
        }
    }

    /// <summary>Drops every entry (mission end). The counter keeps going, so a key is never reused within the process.</summary>
    public void Clear()
    {
        lock (_gate)
            _byAgent.Clear();
    }
}
