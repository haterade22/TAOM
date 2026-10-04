using System;
using BehaviorTrees;
using BehaviorTrees.Nodes;
using BehaviorTreeWrapper.BlackBoardClasses;
using TAOM.Features.AdvancedCombat;
using TAOM.Features.RaceAbilities.Domain;
using TAOM.Features.RaceAbilities.Hooks;
using TaleWorlds.MountAndBlade;

namespace TAOM.Features.RaceAbilities.BehaviorTreeElements;

/// <summary>
/// Fires the soldier's ability, and his ready kin's, in one step: the ability is a timed state, not an
/// action to play, so the task finishes on the tick it starts. Main thread: the trees tick from
/// BehaviorTreeMissionLogic.OnMissionTick (#592). A failure is logged once per battle and spends nothing.
/// </summary>
public class UnleashTask : BTTask, IBTBannerlordBase, IBTRaceAbilityBlackboard
{
    private readonly RaceAbilityRuntime _runtime;
    private BTBlackboardValue<Agent> _agent;
    private BTBlackboardValue<RaceAbilityTriggerKind?> _firedBy;

    public UnleashTask(RaceAbilityRuntime runtime) => _runtime = runtime;

    public BTBlackboardValue<Agent> Agent { get => _agent; set => _agent = value; }

    public BTBlackboardValue<RaceAbilityTriggerKind?> FiredBy { get => _firedBy; set => _firedBy = value; }

    public override BTTaskStatus Execute()
    {
        try
        {
            Agent soldier = Agent.GetValue();
            Mission mission = Mission.Current;
            if (soldier == null || mission == null || !soldier.IsActive() || !AgentSlotIdentity.IsCurrentOccupant(soldier))
                return BTTaskStatus.FinishedWithFalse;
            var profile = _runtime.ProfileOf(soldier);
            if (profile == null)
                return BTTaskStatus.FinishedWithFalse;
            _runtime.Activator.Unleash(soldier, profile, mission.CurrentTime, FiredBy.GetValue());
            return BTTaskStatus.FinishedWithTrue;
        }
        catch (Exception ex)
        {
            _runtime.ReportTreeFailure(nameof(UnleashTask), ex);
            return BTTaskStatus.FinishedWithFalse;
        }
    }
}
