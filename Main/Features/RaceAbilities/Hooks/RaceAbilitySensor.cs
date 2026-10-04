using TAOM.Features.RaceAbilities.Domain;
using TaleWorlds.Library;
using TaleWorlds.MountAndBlade;

namespace TAOM.Features.RaceAbilities.Hooks;

/// <summary>
/// Reads what a soldier perceives into <see cref="RaceAbilitySenses"/> for the service to judge. His own
/// health, morale, mount, weapon and last kill come first; then only what
/// <see cref="RaceAbilityService.PlanScan"/> says could still change the answer: the enemies around him from
/// the engine's enemies-only query (a rider's closing speed only as far as a CavalryClosing trigger reads),
/// then his kin and the remembered deaths of kin, once every other requirement holds. One senses object
/// serves every decision: decisions run one at a time on the main thread (the tree tick), and nothing reads
/// the senses after the decision. Boundary code, game-tested (ADR-008).
/// </summary>
public sealed class RaceAbilitySensor
{
    private readonly RaceAbilityRuntime _runtime;
    private readonly MBList<Agent> _scratch = new MBList<Agent>();
    private readonly RaceAbilitySenses _senses = new RaceAbilitySenses();
    private readonly RaceAbilitySenses _kin = new RaceAbilitySenses();

    public RaceAbilitySensor(RaceAbilityRuntime runtime) => _runtime = runtime;

    public RaceAbilitySenses Sense(Agent soldier, RaceAbilityProfile profile, float now, bool tookDamage)
    {
        var senses = _senses;
        senses.Clear();
        senses.Now = now;
        senses.HealthFraction = HealthFraction(soldier);
        senses.Morale = soldier.GetMorale();
        senses.TookDamage = tookDamage;
        senses.WieldsRanged = soldier.WieldedWeapon.CurrentUsageItem?.IsRangedWeapon == true;
        senses.Mounted = soldier.HasMount;
        senses.LastKillAt = _runtime.Store.LastKillAt(soldier);

        var team = soldier.Team;
        if (team == null)
            return senses;
        var service = _runtime.Service;
        var plan = service.PlanScan(profile, senses);
        var position = soldier.Position;
        if (plan.EnemyRange > 0f)
        {
            _scratch.Clear();
            Mission.Current.GetNearbyEnemyAgents(position.AsVec2, plan.EnemyRange, team, _scratch);
            foreach (var enemy in _scratch)
            {
                if (enemy == null || !enemy.IsActive() || !enemy.IsHuman)
                    continue;
                var distance = enemy.Position.Distance(position);
                var closing = plan.ClosingRange > 0f && distance <= plan.ClosingRange && IsClosingOn(enemy, position.AsVec2);
                senses.Enemies.Add(new EnemySense(distance, HealthFraction(enemy), closing));
            }
        }
        if (!service.RequiresHoldBeforeKin(profile, senses))
            return senses;
        if (plan.KinRange > 0f)
            CollectKin(soldier, profile, plan.KinRange, senses);
        if (plan.FallenKin)
            _runtime.Fallen.SenseKin(team, profile, _runtime.Resolver.KinRaces(profile), position.x, position.y, senses.FallenKin);
        return senses;
    }

    // Kin within the radius, for the kin bonus at activation.
    public int CountKin(Agent soldier, RaceAbilityProfile profile, float radius)
    {
        if (soldier.Team == null || !(radius > 0f))
            return 0;
        _kin.Clear();
        CollectKin(soldier, profile, radius, _kin);
        return RaceAbilityService.CountKin(_kin.KinDistances, radius);
    }

    // Mission end: a buffer still holding the battle's agents would keep them, and through their teams the
    // ended mission, alive until its next use.
    internal void Clear()
    {
        _scratch.Clear();
        _senses.Clear();
        _kin.Clear();
    }

    private void CollectKin(Agent soldier, RaceAbilityProfile profile, float range, RaceAbilitySenses senses)
    {
        var position = soldier.Position;
        _scratch.Clear();
        Mission.Current.GetNearbyAllyAgents(position.AsVec2, range, soldier.Team, _scratch);
        foreach (var other in _scratch)
            if (other != null && other != soldier && other.IsActive() && other.IsHuman
                && other.Team == soldier.Team && _runtime.IsKin(profile, other))
                senses.KinDistances.Add(other.Position.Distance(position));
    }

    private bool IsClosingOn(Agent rider, Vec2 target)
    {
        var mount = rider.MountAgent;
        if (mount == null)
            return false;
        var offset = target - rider.Position.AsVec2;
        var velocity = mount.Velocity.AsVec2;
        return _runtime.Service.IsClosing(offset.x, offset.y, velocity.x, velocity.y);
    }

    private static float HealthFraction(Agent agent) =>
        agent.HealthLimit > 0f ? agent.Health / agent.HealthLimit : 1f;
}
