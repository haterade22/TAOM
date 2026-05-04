using BehaviorTrees;
using BehaviorTreeWrapper;
using BehaviorTreeWrapper.AbstractDecoratorsListeners;
using BehaviorTreeWrapper.BlackBoardClasses;
using TAOM.Core.Logging;
using TAOM.Features.AdvancedCombat.BaseBehaviorTree;
using TaleWorlds.MountAndBlade;

namespace TAOM.Features.Spider.BehaviorTreeElements;

/// <summary>
/// Constant event listener that fires when the spider's Agent component is removed
/// (death, despawn). Logs at debug level for diagnostics — the BT framework
/// auto-cleans the BehaviorTreeAgentComponent itself.
/// </summary>
public class OnSpiderDied : BannerlordConstantEventListener, IBTBannerlordBase, IBTSpiderBlackboard
{
    BTBlackboardValue<Agent> _agent;
    public BTBlackboardValue<Agent> Agent { get => _agent; set => _agent = value; }

    public OnSpiderDied() : base(SubscriptionPossibilities.OnSelfRemoved) { }

    public override void Notify(object[] data)
    {
        try
        {
            IoC.Resolve<IModLogger>()?.LogDebug($"[Spider] BT cleanup for {Agent.GetValue()?.Name ?? "(null)"}");
        }
        catch
        {
            // Swallow — BT teardown should never throw.
        }
    }
}
