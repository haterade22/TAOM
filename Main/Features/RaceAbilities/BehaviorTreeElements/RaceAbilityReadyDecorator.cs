using System;
using BehaviorTrees;
using BehaviorTreeWrapper.BlackBoardClasses;
using TAOM.Features.AdvancedCombat;
using TAOM.Features.RaceAbilities.Domain;
using TAOM.Features.RaceAbilities.Hooks;
using TaleWorlds.MountAndBlade;

namespace TAOM.Features.RaceAbilities.BehaviorTreeElements;

/// <summary>
/// Passes when the soldier's ability is off cooldown and its trigger holds. The cooldown is checked before
/// the proximity scan, so a soldier on cooldown costs a dictionary lookup. Health is sampled on every
/// decision (about once a second), so "took damage" means since the previous one. A soldier already
/// fleeing never fires. It must not throw, because a throw stops the whole tree for the battle
/// (BehaviorTreesCore.RunTree): a failure is logged once per battle and reads as "not ready".
/// </summary>
public class RaceAbilityReadyDecorator : BTReturnFalseDecorator, IBTBannerlordBase, IBTRaceAbilityBlackboard
{
    private readonly RaceAbilityRuntime _runtime;
    private readonly RaceAbilitySenses _senses = new RaceAbilitySenses();
    private BTBlackboardValue<Agent> _agent;
    private BTBlackboardValue<RaceAbilityTriggerKind?> _firedBy;
    private float _lastHealth = -1f;

    public RaceAbilityReadyDecorator(RaceAbilityRuntime runtime) => _runtime = runtime;

    public BTBlackboardValue<Agent> Agent { get => _agent; set => _agent = value; }

    public BTBlackboardValue<RaceAbilityTriggerKind?> FiredBy { get => _firedBy; set => _firedBy = value; }

    public override bool Evaluate()
    {
        try
        {
            Agent soldier = Agent.GetValue();
            Mission mission = Mission.Current;
            // The tree holds this handle across frames; a recycled slot answers for its new tenant (#592).
            if (soldier == null || mission == null || !soldier.IsActive() || !AgentSlotIdentity.IsCurrentOccupant(soldier))
                return false;
            if (!_runtime.Enabled || soldier.IsRetreating())
                return false;
            var profile = _runtime.ProfileOf(soldier);
            if (profile == null)
                return false;

            var now = mission.CurrentTime;
            var health = soldier.Health;
            var tookDamage = _lastHealth >= 0f && health < _lastHealth;
            _lastHealth = health;
            if (!_runtime.Service.IsOffCooldown(_runtime.Store.LastFiredAt(soldier), now, profile.CooldownSeconds))
                return false;

            _runtime.Telemetry.Add(profile.AbilityId, RaceAbilityStat.Decisions);
            _runtime.Sensor.Sense(soldier, profile, now, tookDamage, _senses);
            var trigger = _runtime.Service.FiringTrigger(profile, _senses);
            FiredBy.SetValue(trigger);
            return trigger.HasValue;
        }
        catch (Exception ex)
        {
            _runtime.ReportTreeFailure(nameof(RaceAbilityReadyDecorator), ex);
            return false;
        }
    }
}
