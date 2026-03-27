using System.Collections.Concurrent;
using TAOM.Core.Logging;
using TaleWorlds.MountAndBlade;

namespace TAOM.Adapters;

public class MissionAdapterFactory : IMissionAdapterFactory
{
    private readonly IModLogger _logger;
    private readonly ConcurrentDictionary<int, IAgentAdapter> _agentCache = new();

    public MissionAdapterFactory(IModLogger logger)
    {
        _logger = logger;
    }

    public IAgentAdapter GetAgentAdapter(Agent agent)
    {
        if (agent == null) return null;
        return _agentCache.GetOrAdd(agent.Index, _ => new AgentAdapter(agent, this, _logger));
    }
}
