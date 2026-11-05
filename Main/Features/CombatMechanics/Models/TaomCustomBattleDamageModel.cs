using TaleWorlds.Core;
using TaleWorlds.MountAndBlade;
using TAOM.Features.CombatMechanics.Hooks;

namespace TAOM.Features.CombatMechanics.Models;

// Custom Battle's twin of TaomCombatMechanicsModel (#788). Custom Battle installs the engine's CustomAgentApplyDamageModel
// (v1.4.8 CustomGame.InitializeGameModels). The campaign model derives from SandboxAgentApplyDamageModel, whose
// ApplyDamageReductions reads DefaultSkillEffects.MountedWeaponDamagePenalty for a mounted attacker; that resolves
// through Campaign.Current (v1.4.8 SandboxAgentApplyDamageModel.cs:509-512, DefaultSkillEffects.cs:94 and :126), null
// outside a campaign. So this extends the Custom Battle base and makes the campaign model's CombatMechanicsHooks calls,
// in the same order. SubModule.RegisterCustomBattleModels adds it on a BasicGameStarter.
//
// Campaign-only, so absent here: the career passives (TaomAgentApplyDamageModel reads heroes) and the Refuge reduction
// (reads the defender's party). Where the Custom Battle base answers differently from SandBox's, TAOM's delegate sits on
// the Custom Battle answer: docs/features/combat-mechanics.md, "Custom Battle".
public class TaomCustomBattleDamageModel : CustomAgentApplyDamageModel
{
    private readonly CombatMechanicsHooks _combat;

    public TaomCustomBattleDamageModel(CombatMechanicsHooks combat)
    {
        _combat = combat;
    }

    // The combat rules first, then the engine's own: Custom Battle's base crushes through only for a CanCrushThrough
    // weapon swung overhead with energy above 58, x1.2 against a shield (v1.4.8 CustomAgentApplyDamageModel.cs:115-133).
    public override bool DecideCrushedThrough(Agent attackerAgent, Agent defenderAgent, float totalAttackEnergy, Agent.UsageDirection attackDirection, StrikeType strikeType, WeaponComponentData defendItem, bool isPassiveUsageHit)
        => _combat.CrushThrough(attackerAgent, defenderAgent, totalAttackEnergy, attackDirection, strikeType, defendItem, isPassiveUsageHit)
           ?? base.DecideCrushedThrough(attackerAgent, defenderAgent, totalAttackEnergy, attackDirection, strikeType, defendItem, isPassiveUsageHit);

    public override float CalculateRemainingMomentum(float originalMomentum, in Blow b, in AttackCollisionData collisionData, Agent attacker, Agent victim, in MissionWeapon attackerWeapon, bool isCrushThrough)
        => _combat.CleaveMomentum(attacker, originalMomentum, in collisionData)
           ?? base.CalculateRemainingMomentum(originalMomentum, in b, in collisionData, attacker, victim, in attackerWeapon, isCrushThrough);

    public override void DecideWeaponCollisionReaction(in Blow registeredBlow, in AttackCollisionData collisionData, Agent attacker, Agent defender, in MissionWeapon attackerWeapon, bool isFatalHit, bool isShruggedOff, float momentumRemaining, out MeleeCollisionReaction colReaction)
    {
        base.DecideWeaponCollisionReaction(in registeredBlow, in collisionData, attacker, defender, in attackerWeapon, isFatalHit, isShruggedOff, momentumRemaining, out colReaction);
        colReaction = _combat.CollisionReaction(attacker, momentumRemaining, in collisionData, colReaction);
    }

    // Base = the engine's stagger check (it asks our CalculateStaggerThresholdDamage through the registered model);
    // then creature unstoppability.
    public override bool DecideAgentShrugOffBlow(Agent victimAgent, in AttackCollisionData collisionData, in Blow blow)
        => base.DecideAgentShrugOffBlow(victimAgent, in collisionData, in blow)
           || _combat.IsUnstoppable(victimAgent, in collisionData);

    // The per-race multiplier scales Custom Battle's flat managed-parameter threshold (v1.4.8 :257-261).
    public override float CalculateStaggerThresholdDamage(Agent defenderAgent, in Blow blow)
        => _combat.StaggerThreshold(defenderAgent, base.CalculateStaggerThresholdDamage(defenderAgent, in blow));

    // The charge rules (horse charges only), then the engine's.
    public override bool DecideAgentKnockedDownByBlow(Agent attackerAgent, Agent victimAgent, in AttackCollisionData collisionData, WeaponComponentData attackerWeapon, in Blow blow)
        => _combat.ChargeKnockdown(attackerAgent, victimAgent, in collisionData, in blow)
           ?? base.DecideAgentKnockedDownByBlow(attackerAgent, victimAgent, in collisionData, attackerWeapon, in blow);

    // The Custom Battle base grants no flag (v1.4.8 :111-113), so the shield-penetration lists start from the weapon's own.
    public override void DecideMissileWeaponFlags(Agent attackerAgent, in MissionWeapon missileWeapon, ref WeaponFlags missileWeaponFlags)
    {
        base.DecideMissileWeaponFlags(attackerAgent, in missileWeapon, ref missileWeaponFlags);
        missileWeaponFlags = _combat.PenetrationFlags(in missileWeapon, missileWeaponFlags);
    }

    // The Custom Battle base multiplies by 1.25 and applies the banner reduction (v1.4.8 :294-305).
    public override float CalculateShieldDamage(in AttackInformation attackInformation, float baseDamage)
        => _combat.ShieldDamage(in attackInformation, base.CalculateShieldDamage(in attackInformation, baseDamage));

    // Feature off = the engine's own value, 0.4 in Custom Battle too (v1.4.8 :252-255).
    public override float GetHorseChargePenetration()
        => _combat.HorseChargePenetration() ?? base.GetHorseChargePenetration();
}
