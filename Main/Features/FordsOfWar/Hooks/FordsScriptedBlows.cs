using TaleWorlds.Core;
using TaleWorlds.Engine;
using TaleWorlds.MountAndBlade;

namespace TAOM.Features.FordsOfWar.Hooks;

/// <summary>
/// A scripted blow (<c>CustomAttacksUtils.TakeDamage</c>: a warg or spider bite, a creature's attack, a signature strike's
/// splash) writes its damage straight into the blow, which the engine registers without its damage model. In a Fords of War
/// match it goes through the registered model here first (Mike, 2026-11-07), so the army upgrades, the hero factors and the
/// rest of the model reach it; armour still does not, as before. Outside a match the damage is unchanged. Main thread only, as
/// <c>TakeDamage</c> is.
/// </summary>
internal static class FordsScriptedBlows
{
    internal static int Damage(Agent attacker, Agent victim, int damage, DamageTypes damageType, in AttackCollisionData collision)
    {
        if (!FordsUpgradeHooks.IsLive)
            return damage;

        var weapon = MissionWeapon.Invalid;
        var attack = new AttackInformation(attacker, victim, WeakGameEntity.Invalid, in collision, in weapon);
        var modelled = MissionGameModels.Current.AgentApplyDamageModel.CalculateDamage(in attack, in collision, damage);
        return FordsPierceHooks.ScriptedDamage(modelled, damageType, victim);
    }
}
