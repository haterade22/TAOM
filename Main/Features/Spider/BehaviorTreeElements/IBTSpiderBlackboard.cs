using BehaviorTrees;
using TaleWorlds.MountAndBlade;

namespace TAOM.Features.Spider.BehaviorTreeElements;

public interface IBTSpiderBlackboard : IBTBlackboard
{
    // Currently no spider-specific blackboard fields beyond IBTBannerlordBase.Agent.
    // Reserved as a hook for future state (last bite time, target, etc.) — kept as
    // an interface for extensibility and to mirror IBTWargBlackboard's role.
}
