using BehaviorTrees;
using TAOM.Features.RaceAbilities.Domain;

namespace TAOM.Features.RaceAbilities.BehaviorTreeElements;

/// <summary>
/// The race-ability tree's blackboard: the trigger that made the ready decorator pass, for the unleash
/// task's log line. The builder reflection-copies it from the tree onto every node implementing this
/// interface.
/// </summary>
public interface IBTRaceAbilityBlackboard : IBTBlackboard
{
    BTBlackboardValue<RaceAbilityTriggerKind?> FiredBy { get; set; }
}
