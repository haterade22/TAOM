using BehaviorTrees;
using BehaviorTrees.Nodes;
using SandBox;
using TaleWorlds.MountAndBlade;

namespace BehaviorTreeWrapper.BlackBoardClasses;

public interface IBTBannerlordBase : IBTBlackboard
{
    BTBlackboardValue<Agent> Agent { get; set; }
}

public interface IBTMovable : IBTBlackboard
{
    BTBlackboardValue<AgentNavigator> Navigator { get; set; }
}
