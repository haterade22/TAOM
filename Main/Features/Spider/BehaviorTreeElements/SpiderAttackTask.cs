using BehaviorTrees;
using BehaviorTrees.Nodes;
using BehaviorTreeWrapper.BlackBoardClasses;
using TAOM.Adapters;
using TAOM.Features.AdvancedCombat.BaseBehaviorTree;
using TaleWorlds.MountAndBlade;

namespace TAOM.Features.Spider.BehaviorTreeElements;

/// <summary>
/// Triggers a single bite attack via <see cref="ISpiderAttackService"/>.
/// Uses IoC.Resolve at the BT-leaf boundary (engine-coupled — same pattern as WargAttackTask).
/// </summary>
public class SpiderAttackTask : BTTask, IBTBannerlordBase, IBTSpiderBlackboard
{
    private IMissionAdapterFactory _adapterFactory;
    private ISpiderAttackService _attackService;

    BTBlackboardValue<Agent> _agent;
    public BTBlackboardValue<Agent> Agent { get => _agent; set => _agent = value; }

    public override BTTaskStatus Execute()
    {
        Agent spider = Agent.GetValue();
        if (spider == null || !spider.IsActive()) return BTTaskStatus.FinishedWithFalse;

        _adapterFactory ??= IoC.Resolve<IMissionAdapterFactory>();
        _attackService ??= IoC.Resolve<ISpiderAttackService>();
        var spiderAdapter = _adapterFactory.GetAgentAdapter(spider);
        _attackService.SpiderAttack(spiderAdapter);
        return BTTaskStatus.FinishedWithTrue;
    }
}
