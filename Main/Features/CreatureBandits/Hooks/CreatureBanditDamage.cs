using TAOM.Features.CareerSystem.Models;
using TaleWorlds.Core;
using TaleWorlds.MountAndBlade;

namespace TAOM.Features.CreatureBandits.Hooks;

/// <summary>
/// The riderless creatures' damage-taken rules (#692), for the damage models' ApplyDamageReductions: the campaign's
/// TaomCombatMechanicsModel and Custom Battle's <see cref="Models.TaomCustomBattleCreatureDamageModel"/>. The engine runs
/// every melee and missile hit that connects through that step (<c>AgentApplyDamageModel.CalculateDamage</c>, called
/// from <c>Mission.GetAttackCollisionResults</c>, v1.5.3 <c>Mission.cs:6542</c>). Inert for every other victim: the
/// fingerprint's first read is the managed Character, null on every ordinary mount. The rules are
/// <see cref="CreatureBanditTuning.TakenFactor"/>, asked through <see cref="CreatureBanditTuning.CurrentTakenFactor"/>
/// at each hit so an MCM change applies at once, with the melee kind corrected as vanilla corrects it (a charge, kick
/// or bash is Blunt). TAOM's own scripted blows
/// (CustomAttacksUtils.TakeDamage: troll brute force, signature strikes, warg and ridden-spider bites) never reach this
/// step, so these rules do not apply to them: a known limitation, invisible at the 100% melee defaults, and a bandit
/// troll takes those blows whole, as armour never reduced them either. A bandit troll
/// (#694), humanoid and so no creature, takes <see cref="CreatureBanditsConfig.TrollBanditDamageTaken"/> of each hit
/// instead of armour; a string-set lookup per hit, and 1 for every other victim.
/// </summary>
internal static class CreatureBanditDamage
{
    internal static float Reduce(in AttackInformation attackInformation, in AttackCollisionData collisionData, float damage)
    {
        if (!CreatureBanditAgents.Is(attackInformation.VictimAgent))
            return damage * CreatureBanditRules.TrollBanditDamageTakenFactor(attackInformation.VictimAgent?.Character?.StringId);
        return damage * CreatureBanditTuning.CurrentTakenFactor(collisionData.IsMissile, (DamageTypes)collisionData.DamageType,
            TaomAgentApplyDamageModel.BluntByVanillaRule(in attackInformation, in collisionData));
    }
}
