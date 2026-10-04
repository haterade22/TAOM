using TaleWorlds.Core;
using TaleWorlds.MountAndBlade;

namespace TAOM.Features.RaceAbilities.Hooks;

/// <summary>
/// What the shared models ask of the race abilities, one call per seam so each model body stays a one-line
/// delegate (gamemodels.md rule 4). Six models carry it: the campaign <c>TaomAgentStatCalculateModel</c>,
/// <c>TaomCombatMechanicsModel</c> and <c>TaomBattleMoraleModel</c>, and their Custom Battle twins
/// <c>TaomCustomBattleAgentStatCalculateModel</c>, <c>TaomCustomBattleCreatureDamageModel</c> and
/// <c>TaomCustomBattleMoraleModel</c>. The engine calls these from any thread; each reads one immutable
/// state from the store, asks the service, and counts what changed in the telemetry. Until
/// RaceAbilitiesModule sets <see cref="Runtime"/>, and with no ability live, every call hands its input back.
/// </summary>
public static class RaceAbilityHooks
{
    internal static RaceAbilityRuntime? Runtime { get; set; }

    // A soldier's own stats, or for a horse its rider's mount effects (a mount's own Character is null, #611).
    public static void ApplyStats(Agent agent, AgentDrivenProperties properties)
    {
        var runtime = Runtime;
        if (runtime == null)
            return;
        var state = runtime.StateOf(agent.IsMount ? agent.RiderAgent : agent);
        var effects = state?.CurrentEffects;
        if (effects == null)
            return;
        if (agent.IsMount)
            RaceAbilityStatApplier.ApplyMount(properties, effects);
        else
            RaceAbilityStatApplier.Apply(properties, effects);
        runtime.Telemetry.Add(state!.Profile.AbilityId, RaceAbilityStat.StatPasses);
    }

    public static float KnockDownResistance(Agent agent, float resistance)
    {
        var runtime = Runtime;
        var effects = runtime?.StateOf(agent)?.CurrentEffects;
        return effects == null ? resistance : runtime!.Service.ScaleResistance(resistance, effects.KnockdownResistancePercent);
    }

    public static float KnockBackResistance(Agent agent, float resistance)
    {
        var runtime = Runtime;
        var effects = runtime?.StateOf(agent)?.CurrentEffects;
        return effects == null ? resistance : runtime!.Service.ScaleResistance(resistance, effects.KnockbackResistancePercent);
    }

    public static float DismountResistance(Agent agent, float resistance)
    {
        var runtime = Runtime;
        var effects = runtime?.StateOf(agent)?.CurrentEffects;
        return effects == null ? resistance : runtime!.Service.ScaleResistance(resistance, effects.DismountResistancePercent);
    }

    // false: the defender holds; true: a raging swing breaks the block; null: the existing rules decide.
    public static bool? CrushVerdict(Agent attacker, Agent defender, StrikeType strikeType, bool isPassiveUsage)
    {
        var runtime = Runtime;
        if (runtime == null)
            return null;
        var attackerState = runtime.StateOf(attacker);
        var defenderState = runtime.StateOf(defender);
        var verdict = runtime.Service.CrushVerdict(attackerState?.CurrentEffects, defenderState?.CurrentEffects,
            strikeType == StrikeType.Swing, isPassiveUsage);
        if (verdict == true)
            runtime.Telemetry.Add(attackerState!.Profile.AbilityId, RaceAbilityStat.CrushesForced);
        else if (verdict == false)
            runtime.Telemetry.Add(defenderState!.Profile.AbilityId, RaceAbilityStat.CrushesHeld);
        return verdict;
    }

    public static bool ShrugsOff(Agent victim)
    {
        var runtime = Runtime;
        var state = runtime?.StateOf(victim);
        if (state?.CurrentEffects?.ShrugOffBlows != true)
            return false;
        runtime!.Telemetry.Add(state.Profile.AbilityId, RaceAbilityStat.ShrugOffs);
        return true;
    }

    // Morale models: while a morale floor is live the soldier cannot panic at all.
    public static bool HoldsNerve(Agent agent)
    {
        var runtime = Runtime;
        return runtime != null && runtime.Service.HoldsNerve(runtime.StateOf(agent)?.CurrentEffects);
    }

    // A rider's strike is his own; the service decides which hits a percentage reaches.
    public static float AmplifyDamage(in AttackInformation attack, in AttackCollisionData collision, float damage)
    {
        var runtime = Runtime;
        if (runtime == null)
            return damage;
        var state = runtime.StateOf(attack.IsAttackerAgentMount ? attack.AttackerAgent?.RiderAgent : attack.AttackerAgent);
        var effects = state?.CurrentEffects;
        if (effects == null)
            return damage;
        var result = runtime.Service.AmplifyHit(damage, effects, collision.IsMissile, collision.IsHorseCharge, collision.IsFallDamage);
        if (result != damage)
        {
            runtime.Telemetry.Add(state!.Profile.AbilityId,
                collision.IsMissile ? RaceAbilityStat.RangedHitsAmplified : RaceAbilityStat.MeleeHitsAmplified);
            runtime.Telemetry.Add(state.Profile.AbilityId, RaceAbilityStat.BonusDamage, (long)System.Math.Round(result - damage));
        }
        return result;
    }

    public static float ReduceDamage(in AttackInformation attack, in AttackCollisionData collision, float damage)
    {
        var runtime = Runtime;
        if (runtime == null)
            return damage;
        var state = runtime.StateOf(attack.VictimAgent);
        var effects = state?.CurrentEffects;
        if (effects == null)
            return damage;
        var result = runtime.Service.Reduce(damage, effects, attack.IsVictimAgentMount, collision.IsFallDamage);
        if (result != damage)
        {
            runtime.Telemetry.Add(state!.Profile.AbilityId, RaceAbilityStat.HitsReduced);
            runtime.Telemetry.Add(state.Profile.AbilityId, RaceAbilityStat.DamagePrevented, (long)System.Math.Round(damage - result));
        }
        return result;
    }
}
