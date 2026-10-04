using TAOM.Features.CreatureBandits.Hooks;
using TAOM.Features.RaceAbilities.Hooks;
using TaleWorlds.Core;
using TaleWorlds.MountAndBlade;

namespace TAOM.Features.CreatureBandits.Models;

/// <summary>
/// Custom Battle's damage model with the creature bandits' damage-taken rules (#692). Custom Battle installs the engine's
/// <see cref="CustomAgentApplyDamageModel"/> (v1.5.3 <c>CustomGame.cs:96</c>) and TAOM registers none there, so this
/// extends exactly that model: every other rule stays the engine's, and only <c>ApplyDamageReductions</c> gains the
/// creature step, after the engine's own banner reductions. Declared by CreatureBanditsModule for Custom Battle only; the
/// campaign's model, TaomCombatMechanicsModel, carries the same step.
///
/// The race abilities (RaceAbilities) share the slot: their damage, crush-through and shrug-off hooks, the same calls
/// the campaign model makes, so a Custom Battle smoke shows them. The engine's own Custom Battle model reads no
/// driven-property damage bonus (<c>ApplyDamageAmplifications</c> applies banner effects only), which is why the
/// abilities' damage rides this model rather than the stat bag. The rest of Combat Mechanics stays campaign-only.
/// </summary>
public class TaomCustomBattleCreatureDamageModel : CustomAgentApplyDamageModel
{
    public override float ApplyDamageReductions(in AttackInformation attackInformation, in AttackCollisionData collisionData, float baseDamage)
        => RaceAbilityHooks.ReduceDamage(in attackInformation,
            CreatureBanditDamage.Reduce(in attackInformation, in collisionData,
                base.ApplyDamageReductions(in attackInformation, in collisionData, baseDamage)));

    public override float ApplyDamageAmplifications(in AttackInformation attackInformation, in AttackCollisionData collisionData, float baseDamage)
        => RaceAbilityHooks.AmplifyDamage(in attackInformation, in collisionData,
            base.ApplyDamageAmplifications(in attackInformation, in collisionData, baseDamage));

    public override bool DecideCrushedThrough(Agent attackerAgent, Agent defenderAgent, float totalAttackEnergy, Agent.UsageDirection attackDirection, StrikeType strikeType, WeaponComponentData defendItem, bool isPassiveUsage)
        => RaceAbilityHooks.CrushVerdict(attackerAgent, defenderAgent, strikeType, isPassiveUsage)
            ?? base.DecideCrushedThrough(attackerAgent, defenderAgent, totalAttackEnergy, attackDirection, strikeType, defendItem, isPassiveUsage);

    public override bool DecideAgentShrugOffBlow(Agent victimAgent, in AttackCollisionData collisionData, in Blow blow)
        => base.DecideAgentShrugOffBlow(victimAgent, in collisionData, in blow) || RaceAbilityHooks.ShrugsOff(victimAgent);
}
