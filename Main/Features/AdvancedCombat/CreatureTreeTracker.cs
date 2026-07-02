using System;
using System.Collections.Generic;
using BehaviorTreeWrapper;
using TAOM.Core.Logging;
using TaleWorlds.MountAndBlade;

namespace TAOM.Features.AdvancedCombat;

/// <summary>
/// Shared per-agent behavior-tree attach/prune bookkeeping for creature MissionBehaviors
/// (Spider / Elephant / Mûmakil — the warg predates the pattern and keeps its own wiring):
/// dedup attach keyed on a creature predicate, late-spawn attach counting (custom-battle
/// deployment spawns AFTER the first mission tick, so the first-tick scan typically reports 0
/// and every creature arrives via <c>OnAgentBuild</c>), and dead-agent pruning (the engine's
/// <c>Agent.Tick</c> auto-ticks each component — never tick them manually; only drop dead
/// creatures from the shadow list). Extracted from the three formerly-identical per-feature
/// copies (R4, 2026-07-01) so the attach discipline has one home — the copies had already
/// drifted (Spider/Mûmakil gained late-attach telemetry the elephant copy never did).
/// Boundary code (raw Agent/component); game-tested per ADR-008.
/// </summary>
public sealed class CreatureTreeTracker
{
    private readonly string _treeName;
    private readonly string _logTag;
    private readonly Func<Agent, bool> _isCreature;
    private readonly IModLogger _logger;
    private readonly List<(Agent agent, BehaviorTreeAgentComponent comp)> _components = new();

    public CreatureTreeTracker(string treeName, string logTag, Func<Agent, bool> isCreature, IModLogger logger)
    {
        _treeName = treeName;
        _logTag = logTag;
        _isCreature = isCreature;
        _logger = logger;
    }

    /// <summary>Creatures currently tracked (attached and not yet pruned).</summary>
    public int AliveCount => _components.Count;

    /// <summary>Trees attached via <see cref="TryLateAttach"/> (i.e., after the first-tick scan).</summary>
    public int LateAttachCount { get; private set; }

    /// <summary>
    /// Attaches a behavior-tree component to the agent if it is this tracker's creature and not
    /// already tracked. Returns true when a tree was newly attached. Safe to call before/after the
    /// first-tick scan (de-duplicates).
    /// </summary>
    public bool TryAttach(Agent agent)
    {
        if (agent == null || !_isCreature(agent)) return false;
        for (int i = 0; i < _components.Count; i++)
            if (_components[i].agent == agent) return false;   // already attached

        var comp = new BehaviorTreeAgentComponent(agent, _treeName, Array.Empty<object>());
        agent.AddComponent(comp);
        if (comp.Tree != null)
        {
            _components.Add((agent, comp));
            return true;
        }
        _logger.LogError($"{_logTag} BT build failed for {agent.Name} (Rider={agent.RiderAgent?.Name ?? "null"})");
        return false;
    }

    /// <summary>First-tick scan: attaches to every matching agent already in the mission.</summary>
    public int AttachAll(IEnumerable<Agent> agents)
    {
        int count = 0;
        foreach (Agent a in agents)
            if (TryAttach(a)) count++;
        return count;
    }

    /// <summary>
    /// Late-spawn attach (from <c>OnAgentBuild</c>, once the tree name is registered): attaches,
    /// counts, and logs the first occurrence for scan-line visibility.
    /// </summary>
    public bool TryLateAttach(Agent agent)
    {
        if (!TryAttach(agent)) return false;
        LateAttachCount++;
        if (LateAttachCount == 1)
            _logger.LogInfo($"{_logTag} First late-spawn tree attached (deployment spawns after the first tick)");
        return true;
    }

    /// <summary>Drops dead creatures from the shadow list so it doesn't grow unbounded.</summary>
    public void PruneDead()
    {
        for (int i = _components.Count - 1; i >= 0; i--)
            if (!_components[i].agent.IsActive())
                _components.RemoveAt(i);
    }

    /// <summary>Mission-end cleanup.</summary>
    public void Clear() => _components.Clear();
}
