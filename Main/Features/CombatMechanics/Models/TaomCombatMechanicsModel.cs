using TaleWorlds.Core;
using TaleWorlds.MountAndBlade;
using TAOM.Features.CareerSystem.Abilities;
using TAOM.Features.CareerSystem.Models;
using TAOM.Features.CombatMechanics.Hooks;
using TAOM.Features.Refuge;
using TAOM.Features.Refuge.Hooks;

namespace TAOM.Features.CombatMechanics.Models;

// CombatMechanics feature. Derives from the CareerSystem model so career passives ride along via
// inheritance: only ONE AgentApplyDamageModel can be registered. Thin boundary per gamemodels.md
// rule 4: each override is base plus one call per feature, in the order that matters; each feature
// keeps its extraction, and the hits it declines, in its own Hooks/ facade (CombatMechanicsHooks for
// the four combat services, RefugeDamageHooks for the refuge defence; #737).
public class TaomCombatMechanicsModel : TaomAgentApplyDamageModel
{
    private readonly CombatMechanicsHooks _combat;
    private readonly IRefugeDefenseService _refugeDefense;

    public TaomCombatMechanicsModel(
        ICareerAgentStatService careerAgentStatService,
        CombatMechanicsHooks combat,
        IRefugeDefenseService refugeDefense = null)
        : base(careerAgentStatService)
    {
        _combat = combat;
        _refugeDefense = refugeDefense;
    }

    // Refuge (#507): defenders of a ready refuge take reduced real-time damage. base runs the
    // career-passive reduction chain first (the parent's override), then the refuge factor rides
    // on top: one service, shared with the auto-resolve path, so the two cannot drift. The source
    // module reached this line with a Harmony postfix on the SAME method TAOM's chain overrides,
    // an accidental and untested ordering; the override makes the order explicit.
    public override float ApplyDamageReductions(in AttackInformation attackInformation, in AttackCollisionData collisionData, float baseDamage)
    {
        var result = base.ApplyDamageReductions(in attackInformation, in collisionData, baseDamage);

        return RefugeDamageHooks.Reduce(_refugeDefense, in attackInformation, result);
    }

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

    // Base = vanilla stagger threshold (which re-enters our CalculateStaggerThresholdDamage via the
    // registered model) + career shrug-off passives; then creature unstoppability.
    public override bool DecideAgentShrugOffBlow(Agent victimAgent, in AttackCollisionData collisionData, in Blow blow)
        => base.DecideAgentShrugOffBlow(victimAgent, in collisionData, in blow)
           || _combat.IsUnstoppable(victimAgent, in collisionData);

    public override float CalculateStaggerThresholdDamage(Agent defenderAgent, in Blow blow)
        => _combat.StaggerThreshold(defenderAgent, base.CalculateStaggerThresholdDamage(defenderAgent, in blow));

    // The charge rules own horse charges only: CombatMechanicsHooks.ChargeKnockdown declines every
    // other hit before it builds a context, so every other hit is base.
    public override bool DecideAgentKnockedDownByBlow(Agent attackerAgent, Agent victimAgent, in AttackCollisionData collisionData, WeaponComponentData attackerWeapon, in Blow blow)
        => _combat.ChargeKnockdown(attackerAgent, victimAgent, in collisionData, in blow)
           ?? base.DecideAgentKnockedDownByBlow(attackerAgent, victimAgent, in collisionData, attackerWeapon, in blow);

    public override void DecideMissileWeaponFlags(Agent attackerAgent, in MissionWeapon missileWeapon, ref WeaponFlags missileWeaponFlags)
    {
        // Base first: preserves the vanilla Javelin + Impale perk grant.
        base.DecideMissileWeaponFlags(attackerAgent, in missileWeapon, ref missileWeaponFlags);
        missileWeaponFlags = _combat.PenetrationFlags(in missileWeapon, missileWeaponFlags);
    }

    public override float CalculateShieldDamage(in AttackInformation attackInformation, float baseDamage)
        => _combat.ShieldDamage(in attackInformation, base.CalculateShieldDamage(in attackInformation, baseDamage));

    // Master off = exactly pre-feature behavior; the gate and its reason: CombatMechanicsHooks.HorseChargePenetration.
    public override float GetHorseChargePenetration()
        => _combat.HorseChargePenetration() ?? base.GetHorseChargePenetration();
}
