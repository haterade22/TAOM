using System;
using TaleWorlds.MountAndBlade;

namespace TAOM.Features.FordsOfWar.Hooks;

/// <summary>
/// Blows on tower gunners. A gunner is invulnerable, so such a blow never becomes a tower blow. The engine cancels a melee blow on it
/// (<c>OnMeleeHit</c> arrives with isCanceled true and zero damage), and an unblocked one never reaches <c>OnAgentHit</c>; a blow the
/// gunner blocks still does, with zero damage. A missile vanishes on the gunner (<c>Mission.MissileHitCallback</c> sets
/// <c>MissileCollisionReaction.BecomeInvisible</c> for an invulnerable victim) and <c>OnMissileHit</c> still fires with isCanceled
/// false. The two callbacks below report both. They are counted per tower, for enemy attackers only, so the report can tell
/// attackers who swing at the tower from attackers who swing at its garrison.
/// </summary>
internal sealed partial class FordsTelemetryLogic
{
    public override void OnMeleeHit(Agent attacker, Agent victim, bool isCanceled, AttackCollisionData collisionData) =>
        GunnerHit(attacker, victim, false);

    public override void OnMissileHit(Agent attacker, Agent victim, bool isCanceled, AttackCollisionData collisionData) =>
        GunnerHit(attacker, victim, true);

    private void GunnerHit(Agent attacker, Agent victim, bool ranged)
    {
        try
        {
            _report.GunnerHit(victim, attacker, ranged);
        }
        catch (Exception ex)
        {
            Fault(ranged ? "OnMissileHit" : "OnMeleeHit", ex);
        }
    }
}
