using TaleWorlds.Engine;
using TaleWorlds.Library;

namespace TAOM.Adapters;

public interface IAgentVisualsAdapter
{
    Skeleton GetSkeleton();
    MatrixFrame GetGlobalFrame();
}
