using System;
using TAOM.Core.Logging;
using TAOM.Features.AdvancedCombat.Services;
using TaleWorlds.MountAndBlade;

namespace TAOM.Adapters;

public class MissionAdapterFactory : IMissionAdapterFactory
{
    private readonly IModLogger _logger;
    private readonly Func<IBoneCollisionService> _boneCollisionServiceFactory;
    // Keyed by the Agent object, never by Agent.Index: the engine recycles a deleted agent's index
    // within the mission, and the dead agent's managed object keeps pointing at the recycled slot.
    // Why, and what it cost: AgentAdapterCache (#592). Lifecycle calls (OnAgentBuilt, Evict,
    // ClearCache) come from AdvancedCombatBehavior, the combat-infrastructure owner every creature
    // feature shares, not from any one consuming feature.
    private readonly AgentAdapterCache _agentCache = new();
    private readonly Func<object, IAgentAdapter> _build;

    public MissionAdapterFactory(IModLogger logger, Func<IBoneCollisionService> boneCollisionServiceFactory)
    {
        _logger = logger;
        _boneCollisionServiceFactory = boneCollisionServiceFactory;
        _build = a => new AgentAdapter((Agent)a, this, _logger, _boneCollisionServiceFactory);
    }

    public IAgentAdapter GetAgentAdapter(Agent agent)
    {
        if (agent == null) return null;
        // One cached delegate under one lock: this runs per behavior-tree evaluation and per
        // bone-collision target, so a hit allocates nothing, and two concurrent misses build once.
        return _agentCache.GetOrAdd(agent, _build);
    }

    public void OnAgentBuilt(Agent agent)
    {
        if (agent == null) return;
        if (!_agentCache.NoteBuilt(agent.Index)) return;

        // One line per mission, the first time the engine hands a deleted agent's index to a new one.
        // This is the engine fact the identity rule above rests on, proven in every battle log rather
        // than assumed. Character/Monster rather than Name: both are plain managed properties.
        if (_agentCache.ReuseCount == 1)
        {
            string who = agent.Character?.StringId ?? agent.Monster?.StringId ?? "?";
            _logger.LogInfo($"[AdapterCache] agent index {agent.Index} reused within this mission by '{who}' " +
                            $"(monster '{agent.Monster?.StringId ?? "?"}'): the engine recycles indices after " +
                            "deletion, so adapters are keyed by agent object, not index (#592).");
        }
    }

    public void Evict(Agent agent)
    {
        if (agent == null) return;
        _agentCache.Evict(agent, agent.Index);
    }

    public void ClearCache()
    {
        if (_agentCache.ReuseCount > 0)
            _logger.LogInfo($"[AdapterCache] {_agentCache.ReuseCount} agent index reuse(s) this mission; " +
                            $"{_agentCache.Count} adapter(s) dropped at mission end.");
        _agentCache.Clear();
    }
}
