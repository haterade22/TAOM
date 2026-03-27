using BehaviorTrees;
using BehaviorTreeWrapper.BlackBoardClasses;
using TaleWorlds.MountAndBlade;

namespace TAOM.Features.AdvancedCombat.BaseBehaviorTree;

public class HasNoRiderDecorator : BTReturnFalseDecorator, IBTBannerlordBase
{
    BTBlackboardValue<Agent> _agent;
    public BTBlackboardValue<Agent> Agent { get => _agent; set => _agent = value; }

    public override bool Evaluate()
    {
        return Agent.GetValue().RiderAgent == null;
    }
}
