using BehaviorTrees;
using System;
using TaleWorlds.MountAndBlade;

namespace TAOM.Features.Warg.BehaviorTreeElements;

public interface IBTWargBlackboard : IBTBlackboard
{
    BTBlackboardValue<Agent> AgentHitBy { get; set; }
    BTBlackboardValue<int> RageAttackAmount { get; set; }
    BTBlackboardValue<bool> FirstAttack { get; set; }
    BTBlackboardValue<DateTime?> RageAttackStartTime { get; set; }
}
