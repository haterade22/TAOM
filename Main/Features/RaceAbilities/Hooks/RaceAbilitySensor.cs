using TAOM.Features.RaceAbilities.Domain;
using TaleWorlds.Core;
using TaleWorlds.Library;
using TaleWorlds.MountAndBlade;

namespace TAOM.Features.RaceAbilities.Hooks;

/// <summary>
/// Reads what a soldier perceives into <see cref="RaceAbilitySenses"/> for the service to judge: his health,
/// morale, mount and weapon, the enemies and kin around him, and remembered deaths of kin. Enemies come from
/// the engine's enemies-only query; kin are scanned only when the profile reads them. Main thread (the tree
/// tick). Boundary code, game-tested (ADR-008).
/// </summary>
public sealed class RaceAbilitySensor
{
    private readonly RaceAbilityRuntime _runtime;
    private readonly MBList<Agent> _scratch = new MBList<Agent>();
    private readonly RaceAbilitySenses _kin = new RaceAbilitySenses();

    public RaceAbilitySensor(RaceAbilityRuntime runtime) => _runtime = runtime;

    public void Sense(Agent soldier, RaceAbilityProfile profile, float now, bool tookDamage, RaceAbilitySenses senses)
    {
        senses.Clear();
        senses.Now = now;
        senses.HealthFraction = HealthFraction(soldier);
        senses.Morale = soldier.GetMorale();
        senses.TookDamage = tookDamage;
        senses.WieldsRanged = WieldsRanged(soldier);
        senses.Mounted = soldier.HasMount;
        senses.LastKillAt = _runtime.Store.LastKillAt(soldier);

        var team = soldier.Team;
        if (team == null)
            return;
        var position = soldier.Position;
        var service = _runtime.Service;
        var range = service.ScanRange(profile);
        if (range > 0f)
        {
            _scratch.Clear();
            Mission.Current.GetNearbyEnemyAgents(position.AsVec2, range, team, _scratch);
            foreach (var enemy in _scratch)
                if (enemy != null && enemy.IsActive() && enemy.IsHuman)
                    senses.Enemies.Add(new EnemySense(enemy.Position.Distance(position), HealthFraction(enemy), IsClosingOn(enemy, position.AsVec2)));
            if (ReadsKin(profile))
                CollectKin(soldier, profile, range, senses);
        }
        _runtime.Fallen.SenseKin(team, profile, _runtime.Resolver.KinRaces(profile), position.x, position.y, senses.FallenKin);
    }

    // Kin within the radius, for the kin bonus at activation.
    public int CountKin(Agent soldier, RaceAbilityProfile profile, float radius)
    {
        if (soldier.Team == null || !(radius > 0f))
            return 0;
        _kin.Clear();
        CollectKin(soldier, profile, radius, _kin);
        return _runtime.Service.CountKin(_kin.KinDistances, radius);
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

    private static bool ReadsKin(RaceAbilityProfile profile)
    {
        if (profile.KinBonus != null)
            return true;
        foreach (var trigger in profile.Requires)
            if (trigger.ParsedKind == RaceAbilityTriggerKind.KinWithin)
                return true;
        foreach (var trigger in profile.AnyOf)
            if (trigger.ParsedKind == RaceAbilityTriggerKind.KinWithin)
                return true;
        return false;
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

    private static bool WieldsRanged(Agent agent)
    {
        var index = agent.GetPrimaryWieldedItemIndex();
        if (index == EquipmentIndex.None)
            return false;
        var item = agent.Equipment[index].CurrentUsageItem;
        return item != null && item.IsRangedWeapon;
    }
}
