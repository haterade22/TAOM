using TaleWorlds.MountAndBlade;

namespace TAOM.Adapters;

public interface IMissionAdapterFactory
{
    IAgentAdapter GetAgentAdapter(Agent agent);
    void ClearCache();
}
