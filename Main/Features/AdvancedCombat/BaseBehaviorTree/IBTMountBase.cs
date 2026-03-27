using BehaviorTrees;
using BehaviorTreeWrapper.BlackBoardClasses;
using TaleWorlds.MountAndBlade;

namespace TAOM.Features.AdvancedCombat.BaseBehaviorTree;

public interface IBTMountBase : IBTBannerlordBase
{
    BTBlackboardValue<Agent> Rider { get; set; }
}
