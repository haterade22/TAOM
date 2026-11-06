using BehaviorTrees;

namespace TAOM.Features.DwarfAttacks.BehaviorTreeElements;

/// <summary>
/// The dwarf tree's blackboard: when each attack last started, in mission seconds (null = never), and the attack the
/// ready decorator chose for the task to play. The builder reflection-copies it from the tree onto every node
/// implementing this interface.
/// </summary>
public interface IBTDwarfAttacksBlackboard : IBTBlackboard
{
    BTBlackboardValue<float?> HeadButtLastFired { get; set; }
    BTBlackboardValue<float?> DropKickLastFired { get; set; }
    BTBlackboardValue<DwarfAttack?> Chosen { get; set; }
}
