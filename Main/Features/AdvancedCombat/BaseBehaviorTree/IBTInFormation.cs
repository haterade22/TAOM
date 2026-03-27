using BehaviorTrees;
using TaleWorlds.MountAndBlade;

namespace TAOM.Features.AdvancedCombat.BaseBehaviorTree;

internal interface IBTInFormation : IBTBlackboard
{
    BTBlackboardValue<Formation> AgentsFormation { get; set; }
}
