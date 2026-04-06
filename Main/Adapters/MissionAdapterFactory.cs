using System;
using System.Collections.Concurrent;
using TAOM.Core.Logging;
using TAOM.Features.AdvancedCombat.Services;
using TaleWorlds.MountAndBlade;

namespace TAOM.Adapters;

public class MissionAdapterFactory : IMissionAdapterFactory
{
    private readonly IModLogger _logger;
    private readonly Func<IBoneCollisionService> _boneCollisionServiceFactory;
    private readonly ConcurrentDictionary<int, IAgentAdapter> _agentCache = new();

    public MissionAdapterFactory(IModLogger logger, Func<IBoneCollisionService> boneCollisionServiceFactory)
    {
        _logger = logger;
        _boneCollisionServiceFactory = boneCollisionServiceFactory;
    }

    public IAgentAdapter GetAgentAdapter(Agent agent)
    {
        if (agent == null) return null;
        return _agentCache.GetOrAdd(agent.Index, _ => new AgentAdapter(agent, this, _logger, _boneCollisionServiceFactory));
    }

    public void ClearCache() => _agentCache.Clear();
}
