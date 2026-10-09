using TaleWorlds.Core;
using TaleWorlds.MountAndBlade;
using TAOM.Features.CombatMechanics.Hooks;
using TAOM.Features.CreatureBandits.Hooks;
using TAOM.Features.CreatureSiegeRole.Hooks;
using TAOM.Features.RaceAbilities.Hooks;
using TAOM.Features.SignatureStrikes;
using TAOM.Features.SignatureStrikes.Hooks;

namespace TAOM.Features.CombatMechanics.Models;

// Custom Battle's twin of TaomCombatMechanicsModel (#788). Custom Battle installs the engine's CustomAgentApplyDamageModel
// (CustomGame.InitializeGameModels) and the campaign model derives from SandboxAgentApplyDamageModel, which throws on a
// mounted hit outside a campaign (it reads Campaign.Current for the riding penalty), so this extends the Custom Battle base
// and carries every rule that does not need a hero or a party: the same hooks, the same facade and the same order of calls
// as the campaign model, base first wherever the campaign model is. SubModule.RegisterCustomBattleModels adds it on a
// BasicGameStarter; no feature module declares this slot, because the module step runs after that one and the last model
// added wins.
//
// Campaign-only, so absent here: the career passives (TaomAgentApplyDamageModel reads heroes, which Custom Battle does not
// have) and the Refuge reduction (reads the defender's party). Where the Custom Battle base answers differently from
// SandBox's (the stagger threshold, the crush-through flag test, the shield damage factor and the missile flags), TAOM's
// delegate sits on the Custom Battle answer, as the campaign's sits on SandBox's: docs/features/combat-mechanics.md,
// "Custom Battle".
public class TaomCustomBattleDamageModel : CustomAgentApplyDamageModel
{
    private readonly CombatMechanicsHooks _combat;
    private readonly ISignatureStrikeService _signatureStrikes;
    private readonly ISignatureAgentRoster _signatureRoster;

    public TaomCustomBattleDamageModel(
        CombatMechanicsHooks combat,
        ISignatureStrikeService signatureStrikes = null,
        ISignatureAgentRoster signatureRoster = null)
    {
        _combat = combat;
        _signatureStrikes = signatureStrikes;
        _signatureRoster = signatureRoster;
    }

    // Creature Bandits (#692, #694) then Race Abilities, after the engine's banner reductions; inert for every other victim.
    public override float ApplyDamageReductions(in AttackInformation attackInformation, in AttackCollisionData collisionData, float baseDamage)
    {
        var result = base.ApplyDamageReductions(in attackInformation, in collisionData, baseDamage);

        result = CreatureBanditDamage.Reduce(in attackInformation, in collisionData, result);

        return RaceAbilityHooks.ReduceDamage(in attackInformation, in collisionData, result);
    }

    // Race Abilities: a raging soldier's melee hits. The engine's Custom Battle model reads no driven-property damage bonus
    // (banner effects only), which is why the ability's damage rides this model rather than the stat bag.
    public override float ApplyDamageAmplifications(in AttackInformation attackInformation, in AttackCollisionData collisionData, float baseDamage)
        => RaceAbilityHooks.AmplifyDamage(in attackInformation, in collisionData,
            base.ApplyDamageAmplifications(in attackInformation, in collisionData, baseDamage));

    // Creature Siege Role: a creature's melee blow on a castle gate is multiplied after base.
    public override float ApplyDamageScaling(in AttackInformation attackInformation, in AttackCollisionData collisionData, float baseDamage)
        => CreatureSiegeHooks.ScaleGateDamage(in attackInformation, in collisionData,
            base.ApplyDamageScaling(in attackInformation, in collisionData, baseDamage));

    // Race Abilities first, then the combat rules, then the engine's own: Custom Battle's base crushes through only for a
    // CanCrushThrough weapon swung overhead with energy above 58 (x1.2 against a shield); a combat rule that answers wins.
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

    // Base = the engine's stagger check (which asks our CalculateStaggerThresholdDamage through the registered model); then
    // creature unstoppability; then Race Abilities: a live berserker or dwarf does not flinch at a weapon or missile hit.
    public override bool DecideAgentShrugOffBlow(Agent victimAgent, in AttackCollisionData collisionData, in Blow blow)
        => base.DecideAgentShrugOffBlow(victimAgent, in collisionData, in blow)
           || _combat.IsUnstoppable(victimAgent, in collisionData)
           || RaceAbilityHooks.ShrugsOff(victimAgent);

    // The per-race multiplier scales Custom Battle's flat managed-parameter threshold, where the campaign scales SandBox's
    // perk-adjusted one.
    public override float CalculateStaggerThresholdDamage(Agent defenderAgent, in Blow blow)
        => _combat.StaggerThreshold(defenderAgent, base.CalculateStaggerThresholdDamage(defenderAgent, in blow));

    // SignatureStrikes (#605) first, then the charge rules (horse charges only), then the engine's.
    public override bool DecideAgentKnockedDownByBlow(Agent attackerAgent, Agent victimAgent, in AttackCollisionData collisionData, WeaponComponentData attackerWeapon, in Blow blow)
        => SignatureStrikeVerdicts.Decide(_signatureStrikes, _signatureRoster, attackerAgent, victimAgent, in collisionData, in blow, knockdown: true)
           ?? _combat.ChargeKnockdown(attackerAgent, victimAgent, in collisionData, in blow)
           ?? base.DecideAgentKnockedDownByBlow(attackerAgent, victimAgent, in collisionData, attackerWeapon, in blow);

    // The only true this adds is a signature strike whose profile sets knockBack; every other hit is the engine's.
    public override bool DecideAgentKnockedBackByBlow(Agent attackerAgent, Agent victimAgent, in AttackCollisionData collisionData, WeaponComponentData attackerWeapon, in Blow blow)
        => SignatureStrikeVerdicts.Decide(_signatureStrikes, _signatureRoster, attackerAgent, victimAgent, in collisionData, in blow, knockdown: false)
           ?? base.DecideAgentKnockedBackByBlow(attackerAgent, victimAgent, in collisionData, attackerWeapon, in blow);

    // The Custom Battle base grants no flag (it has no Impale perk), so the shield-penetration lists start from the weapon's own.
    public override void DecideMissileWeaponFlags(Agent attackerAgent, in MissionWeapon missileWeapon, ref WeaponFlags missileWeaponFlags)
    {
        base.DecideMissileWeaponFlags(attackerAgent, in missileWeapon, ref missileWeaponFlags);
        missileWeaponFlags = _combat.PenetrationFlags(in missileWeapon, missileWeaponFlags);
    }

    // The Custom Battle base multiplies by 1.25 and applies the banner reduction; the runtime correction rides on that result.
    public override float CalculateShieldDamage(in AttackInformation attackInformation, float baseDamage)
        => _combat.ShieldDamage(in attackInformation, base.CalculateShieldDamage(in attackInformation, baseDamage));

    // Master off = the engine's own value (0.4 in Custom Battle too): the gate and its reason are in
    // CombatMechanicsHooks.HorseChargePenetration.
    public override float GetHorseChargePenetration()
        => _combat.HorseChargePenetration() ?? base.GetHorseChargePenetration();
}
