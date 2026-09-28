using TAOM.Features.CreatureBandits.Hooks;
using TaleWorlds.MountAndBlade;

namespace TAOM.Features.CreatureBandits.Models;

/// <summary>
/// Custom Battle's damage model with the creature bandits' damage-taken rules (#692). Custom Battle installs the engine's
/// <see cref="CustomAgentApplyDamageModel"/> (v1.5.3 <c>CustomGame.cs:96</c>) and TAOM registers none there, so this
/// extends exactly that model: every other rule stays the engine's, and only <c>ApplyDamageReductions</c> gains the
/// creature step, after the engine's own banner reductions. Declared by CreatureBanditsModule for Custom Battle only; the
/// campaign's model, TaomCombatMechanicsModel, carries the same step.
/// </summary>
public class TaomCustomBattleCreatureDamageModel : CustomAgentApplyDamageModel
{
    public override float ApplyDamageReductions(in AttackInformation attackInformation, in AttackCollisionData collisionData, float baseDamage)
        => CreatureBanditDamage.Reduce(in attackInformation, in collisionData,
            base.ApplyDamageReductions(in attackInformation, in collisionData, baseDamage));
}
