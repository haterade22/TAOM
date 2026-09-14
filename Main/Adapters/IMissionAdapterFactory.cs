using TaleWorlds.MountAndBlade;

namespace TAOM.Adapters;

public interface IMissionAdapterFactory
{
    IAgentAdapter GetAgentAdapter(Agent agent);

    /// <summary>
    /// An agent was built. Call from <c>MissionBehavior.OnAgentBuild</c>: this is where a build
    /// landing on a freed index is counted, and the first one per mission is logged (#592).
    /// </summary>
    void OnAgentBuilt(Agent agent);

    /// <summary>
    /// Drop a deleted agent's adapter. Call from <c>MissionBehavior.OnAgentDeleted</c>: the engine
    /// recycles the agent's index from that point on, and the managed object's pointers keep aiming
    /// at the recycled slot (#592).
    /// </summary>
    void Evict(Agent agent);

    void ClearCache();
}
