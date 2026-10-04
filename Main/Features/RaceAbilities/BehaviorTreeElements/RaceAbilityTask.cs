using System;
using BehaviorTrees;
using BehaviorTrees.Nodes;
using BehaviorTreeWrapper.BlackBoardClasses;
using TAOM.Features.AdvancedCombat;
using TAOM.Features.RaceAbilities.Hooks;
using TaleWorlds.MountAndBlade;

namespace TAOM.Features.RaceAbilities.BehaviorTreeElements;

/// <summary>
/// One decision a pass: when the soldier's ability is off cooldown and its trigger holds, it fires, and his
/// ready kin's with it. The cooldown is checked before anything is sensed, so a soldier on cooldown costs a
/// dictionary lookup. Health is sampled on every decision (about once a second), so "took damage" means since
/// the previous one. A soldier already fleeing never fires. The ability is a timed state, not an action to
/// play, so the task always finishes on the pass it starts. It must not throw, because a throw stops the whole
/// tree for the battle (BehaviorTreesCore.RunTree): a failure is logged once per battle and fires nothing.
/// Main thread: the trees tick from BehaviorTreeMissionLogic.OnMissionTick (#592).
/// </summary>
public class RaceAbilityTask : BTTask, IBTBannerlordBase
{
    private readonly RaceAbilityRuntime _runtime;
    private BTBlackboardValue<Agent> _agent;
    private float _lastHealth = -1f;

    public RaceAbilityTask(RaceAbilityRuntime runtime) => _runtime = runtime;

    public BTBlackboardValue<Agent> Agent { get => _agent; set => _agent = value; }

    public override BTTaskStatus Execute()
    {
        try
        {
            Decide();
        }
        catch (Exception ex)
        {
            _runtime.ReportFailure(nameof(RaceAbilityTask), ex);
        }
        return BTTaskStatus.FinishedWithTrue;
    }

    private void Decide()
    {
        Agent soldier = Agent.GetValue();
        Mission mission = Mission.Current;
        // The tree holds this handle across frames; a recycled slot answers for its new tenant (#592).
        if (soldier == null || mission == null || !soldier.IsActive() || !AgentSlotIdentity.IsCurrentOccupant(soldier))
            return;
        if (!_runtime.Enabled || soldier.IsRetreating())
            return;
        var profile = _runtime.ProfileOf(soldier);
        if (profile == null)
            return;

        var now = mission.CurrentTime;
        var health = soldier.Health;
        var tookDamage = RaceAbilityService.TookDamage(_lastHealth, health);
        _lastHealth = health;
        if (!_runtime.Service.IsOffCooldown(_runtime.Store.LastFiredAt(soldier), now, profile.CooldownSeconds))
            return;

        _runtime.Telemetry.Add(profile.AbilityId, RaceAbilityStat.Decisions);
        var trigger = _runtime.Service.FiringTrigger(profile, _runtime.Sensor.Sense(soldier, profile, now, tookDamage));
        if (trigger.HasValue)
            _runtime.Activator.Unleash(soldier, profile, now, trigger.Value);
    }
}
