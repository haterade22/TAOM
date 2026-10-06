using TaleWorlds.Core;
using TaleWorlds.MountAndBlade;
using TAOM.Features.CareerSystem.Abilities;
using TAOM.Features.CareerSystem.Models;
using TAOM.Features.CombatMechanics.Hooks;
using TAOM.Features.CreatureSiegeRole.Hooks;
using TAOM.Features.RaceAbilities.Hooks;
using TAOM.Features.Refuge;
using TAOM.Features.Refuge.Hooks;
using TAOM.Features.SignatureStrikes;
using TAOM.Features.SignatureStrikes.Hooks;

namespace TAOM.Features.CombatMechanics.Models;

// CombatMechanics feature. Derives from the CareerSystem model so career
// passives ride along via inheritance — only ONE AgentApplyDamageModel can be registered.
// Thin boundary per gamemodels.md rule 4: each override is base plus one call per feature, in
// the order that matters; every feature keeps its extraction, and the hits it declines, in its
// own Hooks/ facade (CombatMechanicsHooks for the four combat services, #737).
public class TaomCombatMechanicsModel : TaomAgentApplyDamageModel
{
    private readonly CombatMechanicsHooks _combat;
    private readonly IRefugeDefenseService _refugeDefense;
    private readonly ISignatureStrikeService _signatureStrikes;
    private readonly ISignatureAgentRoster _signatureRoster;

    public TaomCombatMechanicsModel(
        ICareerAgentStatService careerAgentStatService,
        CombatMechanicsHooks combat,
        IRefugeDefenseService refugeDefense = null,
        ISignatureStrikeService signatureStrikes = null,
        ISignatureAgentRoster signatureRoster = null)
        : base(careerAgentStatService)
    {
        _combat = combat;
        _refugeDefense = refugeDefense;
        _signatureStrikes = signatureStrikes;
        _signatureRoster = signatureRoster;
    }

    // Refuge (#507): defenders of a ready refuge take reduced real-time damage. base runs the
    // career-passive reduction chain first (the parent's override), then the refuge factor rides
    // on top: one service, shared with the auto-resolve path, so the two cannot drift. The source
    // module reached this line with a Harmony postfix on the SAME method TAOM's chain overrides,
    // an accidental and untested ordering; the override makes the order explicit.
    public override float ApplyDamageReductions(in AttackInformation attackInformation, in AttackCollisionData collisionData, float baseDamage)
    {
        var result = base.ApplyDamageReductions(in attackInformation, in collisionData, baseDamage);

        result = RefugeDamageHooks.Reduce(_refugeDefense, attackInformation.VictimAgentOrigin, result);

        // Creature Bandits (#692, #694): the riderless creatures' damage-taken rules and the bandit trolls' 70%;
        // inert for every other victim.
        result = CreatureBandits.Hooks.CreatureBanditDamage.Reduce(in attackInformation, in collisionData, result);

        // Race Abilities: a soldier standing fast takes less; inert unless his ability is live.
        return RaceAbilityHooks.ReduceDamage(in attackInformation, in collisionData, result);
    }

    // Race Abilities: a raging soldier's melee hits, after the career amplification (the parent's override).
    public override float ApplyDamageAmplifications(in AttackInformation attackInformation, in AttackCollisionData collisionData, float baseDamage)
        => RaceAbilityHooks.AmplifyDamage(in attackInformation, in collisionData,
            base.ApplyDamageAmplifications(in attackInformation, in collisionData, baseDamage));

    // Creature Siege Role: a creature's melee blow on a castle gate is multiplied after base; every other hit is unchanged.
    public override float ApplyDamageScaling(in AttackInformation attackInformation, in AttackCollisionData collisionData, float baseDamage)
        => CreatureSiegeHooks.ScaleGateDamage(in attackInformation, in collisionData,
            base.ApplyDamageScaling(in attackInformation, in collisionData, baseDamage));

    // Race Abilities first: a defender standing fast holds against every crush-through, a raging swing breaks
    // any block; with neither live it has no opinion and the combat rules decide. The crush context (two
    // skill lookups and a roll) is built only when the combat rules are asked.
    public override bool DecideCrushedThrough(Agent attackerAgent, Agent defenderAgent, float totalAttackEnergy, Agent.UsageDirection attackDirection, StrikeType strikeType, WeaponComponentData defendItem, bool isPassiveUsageHit)
        => RaceAbilityHooks.CrushVerdict(attackerAgent, defenderAgent, strikeType, isPassiveUsageHit)
           ?? _combat.CrushThrough(attackerAgent, defenderAgent, totalAttackEnergy, attackDirection, strikeType, defendItem, isPassiveUsageHit)
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
    // registered model) + career shrug-off passives; then creature unstoppability; then Race Abilities: a
    // berserker or a dwarf standing fast does not flinch at a weapon or missile hit while his ability is
    // live (a horse charge never asks, and a kick or bash still knocks back).
    public override bool DecideAgentShrugOffBlow(Agent victimAgent, in AttackCollisionData collisionData, in Blow blow)
        => base.DecideAgentShrugOffBlow(victimAgent, in collisionData, in blow)
           || _combat.IsUnstoppable(victimAgent, in collisionData)
           || RaceAbilityHooks.ShrugsOff(victimAgent);

    public override float CalculateStaggerThresholdDamage(Agent defenderAgent, in Blow blow)
        => _combat.StaggerThreshold(defenderAgent, base.CalculateStaggerThresholdDamage(defenderAgent, in blow));

    // SignatureStrikes (#605) first: a signature hero's slam floors the struck agent regardless of
    // the sweet spot. Then the charge rules, which own horse charges only; every other hit is base.
    // Each facade declines the other's hits, so their order here changes no verdict.
    public override bool DecideAgentKnockedDownByBlow(Agent attackerAgent, Agent victimAgent, in AttackCollisionData collisionData, WeaponComponentData attackerWeapon, in Blow blow)
        => SignatureStrikeVerdicts.Decide(_signatureStrikes, _signatureRoster, attackerAgent, victimAgent, in collisionData, in blow, knockdown: true)
           ?? _combat.ChargeKnockdown(attackerAgent, victimAgent, in collisionData, in blow)
           ?? base.DecideAgentKnockedDownByBlow(attackerAgent, victimAgent, in collisionData, attackerWeapon, in blow);

    // Vanilla never grants KnockBack to a melee swing (SandboxAgentApplyDamageModel.CanWeaponKnockback
    // returns false for swings), so a signature strike whose profile sets knockBack (Sauron's sweep,
    // the Nine's scream) is the only true this can produce;
    // horse charges and every non-signature hit stay base (the 0.7-dot glancing gate is untouched).
    public override bool DecideAgentKnockedBackByBlow(Agent attackerAgent, Agent victimAgent, in AttackCollisionData collisionData, WeaponComponentData attackerWeapon, in Blow blow)
        => SignatureStrikeVerdicts.Decide(_signatureStrikes, _signatureRoster, attackerAgent, victimAgent, in collisionData, in blow, knockdown: false)
           ?? base.DecideAgentKnockedBackByBlow(attackerAgent, victimAgent, in collisionData, attackerWeapon, in blow);

    public override void DecideMissileWeaponFlags(Agent attackerAgent, in MissionWeapon missileWeapon, ref WeaponFlags missileWeaponFlags)
    {
        // Base first — preserves the vanilla Javelin + Impale perk grant.
        base.DecideMissileWeaponFlags(attackerAgent, in missileWeapon, ref missileWeaponFlags);
        missileWeaponFlags = _combat.PenetrationFlags(in missileWeapon, missileWeaponFlags);
    }

    public override float CalculateShieldDamage(in AttackInformation attackInformation, float baseDamage)
        => _combat.ShieldDamage(in attackInformation, base.CalculateShieldDamage(in attackInformation, baseDamage));

    // Master off = exactly pre-feature behavior; the gate and its reason: CombatMechanicsHooks.HorseChargePenetration.
    public override float GetHorseChargePenetration()
        => _combat.HorseChargePenetration() ?? base.GetHorseChargePenetration();
}
