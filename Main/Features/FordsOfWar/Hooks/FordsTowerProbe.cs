using System;
using TaleWorlds.Engine;
using TaleWorlds.MountAndBlade;
using TAOM.Features.FordsOfWar.Diagnostics;

namespace TAOM.Features.FordsOfWar.Hooks;

/// <summary>
/// The melee probe's engine side. <c>MissionBehavior.OnRegisterBlow</c> runs right after <c>Mission.OnEntityHit</c> for every blow, so
/// this sees a blow after vanilla scaled it. <c>BaseMagnitude</c> already carries the entity factor (<c>GetEntityDamageMultiplier</c>:
/// Cut 0.8, Pierce 0.1, a passive attack 0.2) and, for a thrust that missed with the tip, the 0.15 <c>NonTipThrustHitDamageMultiplier</c>;
/// <c>InflictedDamage</c> is that value truncated and passed through <c>AgentApplyDamageModel.CalculateDamage</c>, the raw damage
/// Patch131 was handed (a blow Patch131 then floored still reads 0 here, since the floor writes the component's damage and not the
/// blow). The probe reads the collision data into plain values and hands them to <see cref="FordsRunReport.TowerMeleeProbe"/>;
/// it changes nothing.
///
/// The tower is found the way <c>Mission.OnEntityHit</c> finds its scripts: the hit entity, then each parent, asking the report's tower
/// lookup about every <see cref="DestructableComponent"/> on the way. v1.5.6 <c>Mission.OnEntityHit</c> calls <c>OnHit</c> on every script of
/// the hit entity and stops climbing to the parent once any of them returned true. It skips blows a shield blocked; the probe climbs to the first registered tower and does not model either.
/// </summary>
internal sealed class FordsTowerProbe
{
    private readonly FordsRunReport _report;
    private readonly Action<string, Exception> _fault;

    internal FordsTowerProbe(FordsRunReport report, Action<string, Exception> fault)
    {
        _report = report;
        _fault = fault;
    }

    internal void Register(Agent attacker, Agent victim, WeakGameEntity hit, in Blow blow, in AttackCollisionData data, in MissionWeapon weapon)
    {
        if (victim != null || blow.IsMissile || !data.EntityExists || data.IsColliderAgent || !hit.IsValid)
            return;

        try
        {
            var id = TowerIdOf(hit);
            if (id == null)
                return;

            var arm = MissionCombatMechanicsHelper.HitWithAnotherBone(in data, attacker, in weapon);
            var passive = attacker != null && attacker.IsDoingPassiveAttack;
            var mounted = attacker != null && attacker.HasMount;
            var key = FordsRunReport.MeleeProbeKey(data.StrikeType, data.DamageType, data.ThrustTipHit, passive, data.IsAlternativeAttack, arm,
                mounted, data.BaseMagnitude, blow.InflictedDamage);
            if (_report.TowerMeleeProbe(id, attacker, key))
                _report.TraceTowerMelee(Describe(id, key, attacker, in blow, in data, in weapon));
        }
        catch (Exception ex)
        {
            // swallow-ok: _fault is FordsTelemetryLogic.Fault, which logs the first five failures
            _fault("OnRegisterBlow", ex);
        }
    }

    private string? TowerIdOf(WeakGameEntity hit)
    {
        for (var entity = hit; entity.IsValid; entity = entity.Parent)
        {
            var count = entity.GetScriptCount();
            for (var i = 0; i < count; i++)
            {
                if (entity.GetScriptAtIndex(i) is DestructableComponent component && _report.ProbeTowerId(component) is { } id)
                    return id;
            }
        }

        return null;
    }

    private static string Describe(string id, string key, Agent attacker, in Blow blow, in AttackCollisionData data, in MissionWeapon weapon)
    {
        var start = (data.CollisionHitResultFlags & CombatHitResultFlags.HitWithStartOfTheAnimation) != 0;
        var material = data.PhysicsMaterialIndex < 0 ? "none" : PhysicsMaterial.GetNameAtIndex(data.PhysicsMaterialIndex);
        var who = FormattableString.Invariant(
            // agent and thr are trace text only: Agent.Index recycles, so nothing keys on it
            $"tower={id} key={key} agent={attacker?.Index ?? -1} thr={Environment.CurrentManagedThreadId} troop={attacker?.Character?.StringId ?? "none"} wclass={weapon.CurrentUsageItem?.WeaponClass.ToString() ?? "none"} ");
        var numbers = FormattableString.Invariant(
            $"base={data.BaseMagnitude:F2} raw={blow.InflictedDamage} prog={data.AttackProgress:F2} dist={data.CollisionDistanceOnWeapon:F2} ");
        return who + numbers + FormattableString.Invariant($"dir={data.AttackDirection} res={data.CollisionResult} start={start} mat={material}");
    }
}
