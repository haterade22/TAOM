using TaleWorlds.Engine;
using TaleWorlds.Library;
using TaleWorlds.MountAndBlade;

namespace TAOM.Adapters;

public class AgentVisualsAdapter : IAgentVisualsAdapter
{
    private readonly MBAgentVisuals _visuals;

    public AgentVisualsAdapter(MBAgentVisuals visuals)
    {
        _visuals = visuals;
    }

    public Skeleton GetSkeleton() => _visuals.GetSkeleton();
    public MatrixFrame GetGlobalFrame() => _visuals.GetGlobalFrame();
}
