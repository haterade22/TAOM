using System;
using TAOM.Features.RaceAbilities.Domain;
using TaleWorlds.Core;
using TaleWorlds.MountAndBlade;

namespace TAOM.Features.RaceAbilities.Hooks;

/// <summary>
/// What the shared models ask of the race abilities, one call per seam so each model body stays a one-line
/// delegate (gamemodels.md rule 4). Six models carry it: the campaign <c>TaomAgentStatCalculateModel</c>,
/// <c>TaomCombatMechanicsModel</c> and <c>TaomBattleMoraleModel</c>, and their Custom Battle twins
/// <c>TaomCustomBattleAgentStatCalculateModel</c>, <c>TaomCustomBattleDamageModel</c> and
/// <c>TaomCustomBattleMoraleModel</c>. The engine calls these from any thread; each reads one immutable
/// state from the store, asks the service, and counts what changed in the telemetry. Until
/// RaceAbilitiesModule sets <see cref="Runtime"/>, and with no ability live, every call hands its input back.
/// A hero aura (#1127, docs/features/hero-auras.md) reaches the same seams: <see cref="EffectsOf"/> merges the
/// soldier's own ability with the aura he stands in (<see cref="RaceAbilityEffectsMerge"/>). The telemetry counts
/// the events in which a live race ability took part, with the merged result: a soldier with an aura alone adds nothing
/// to it, and on a soldier with both, the aura's share is counted under his ability.
/// </summary>
public static class RaceAbilityHooks
{
    internal static RaceAbilityRuntime? Runtime { get; set; }

    // The hero aura a soldier carries now, set by HeroAurasModule. Any thread.
    internal static Func<Agent, RaceAbilityEffects?>? AuraEffects { get; set; }

    // A soldier's own live ability merged with his aura. live is his ability's state while it runs, else null: the
    // telemetry counts only what a live ability took part in.
    private static RaceAbilityEffects? EffectsOf(RaceAbilityRuntime runtime, Agent? agent, out RaceAbilityState? live)
    {
        var state = runtime.StateOf(agent);
        var own = state?.CurrentEffects;
        live = own == null ? null : state;
        var aura = agent == null ? null : AuraEffects?.Invoke(agent);
        return RaceAbilityEffectsMerge.Combine(own, aura);
    }

    // A soldier's own stats, or for a horse its rider's mount effects (a mount's own Character is null, #611).
    public static void ApplyStats(Agent agent, AgentDrivenProperties properties)
    {
        var runtime = Runtime;
        if (runtime == null)
            return;
        var effects = EffectsOf(runtime, agent.IsMount ? agent.RiderAgent : agent, out var live);
        if (effects == null)
            return;
        if (agent.IsMount)
            RaceAbilityStatApplier.ApplyMount(properties, effects);
        else
            RaceAbilityStatApplier.Apply(properties, effects);
        if (live != null)
            runtime.Telemetry.Add(live.Profile.AbilityId, RaceAbilityStat.StatPasses);
    }

    public static float KnockDownResistance(Agent agent, float resistance)
    {
        var runtime = Runtime;
        var effects = runtime == null ? null : EffectsOf(runtime, agent, out _);
        return effects == null ? resistance : runtime!.Service.ScaleResistance(resistance, effects.KnockdownResistancePercent);
    }

    public static float KnockBackResistance(Agent agent, float resistance)
    {
        var runtime = Runtime;
        var effects = runtime == null ? null : EffectsOf(runtime, agent, out _);
        return effects == null ? resistance : runtime!.Service.ScaleResistance(resistance, effects.KnockbackResistancePercent);
    }

    public static float DismountResistance(Agent agent, float resistance)
    {
        var runtime = Runtime;
        var effects = runtime == null ? null : EffectsOf(runtime, agent, out _);
        return effects == null ? resistance : runtime!.Service.ScaleResistance(resistance, effects.DismountResistancePercent);
    }

    // false: the defender holds; true: a raging swing breaks the block; null: the existing rules decide.
    public static bool? CrushVerdict(Agent attacker, Agent defender, StrikeType strikeType, bool isPassiveUsage)
    {
        var runtime = Runtime;
        if (runtime == null)
            return null;
        var attackerEffects = EffectsOf(runtime, attacker, out var attackerLive);
        var defenderEffects = EffectsOf(runtime, defender, out var defenderLive);
        var verdict = runtime.Service.CrushVerdict(attackerEffects, defenderEffects, strikeType == StrikeType.Swing, isPassiveUsage);
        if (verdict == true && attackerLive != null)
            runtime.Telemetry.Add(attackerLive.Profile.AbilityId, RaceAbilityStat.CrushesForced);
        else if (verdict == false && defenderLive != null)
            runtime.Telemetry.Add(defenderLive.Profile.AbilityId, RaceAbilityStat.CrushesHeld);
        return verdict;
    }

    public static bool ShrugsOff(Agent victim)
    {
        var runtime = Runtime;
        if (runtime == null || EffectsOf(runtime, victim, out var live)?.ShrugOffBlows != true)
            return false;
        if (live != null)
            runtime.Telemetry.Add(live.Profile.AbilityId, RaceAbilityStat.ShrugOffs);
        return true;
    }

    // The same answer without the count: Hammerhand (#895) asks on every bash or kick only to stay out of the way, and its
    // question shrugs nothing off, so it must not add to the shrug-offs telemetry.
    public static bool IsShruggingOff(Agent victim)
    {
        var runtime = Runtime;
        return runtime != null && EffectsOf(runtime, victim, out _)?.ShrugOffBlows == true;
    }

    // Long Defeat's affix read-back wants a soldier whose stats no race ability, and no hero aura, is moving.
    public static bool HasLiveEffects(Agent agent)
    {
        var runtime = Runtime;
        return runtime != null && EffectsOf(runtime, agent, out _) != null;
    }

    // Morale models: while a morale floor is live the soldier cannot panic at all.
    public static bool HoldsNerve(Agent agent)
    {
        var runtime = Runtime;
        return runtime != null && runtime.Service.HoldsNerve(EffectsOf(runtime, agent, out _));
    }

    // A rider's strike is his own; the service decides which hits a percentage reaches.
    public static float AmplifyDamage(in AttackInformation attack, in AttackCollisionData collision, float damage)
    {
        var runtime = Runtime;
        if (runtime == null)
            return damage;
        var effects = EffectsOf(runtime, attack.IsAttackerAgentMount ? attack.AttackerAgent?.RiderAgent : attack.AttackerAgent, out var live);
        if (effects == null)
            return damage;
        var result = runtime.Service.AmplifyHit(damage, effects, collision.IsMissile, collision.IsHorseCharge, collision.IsFallDamage);
        if (result != damage && live != null)
        {
            runtime.Telemetry.Add(live.Profile.AbilityId,
                collision.IsMissile ? RaceAbilityStat.RangedHitsAmplified : RaceAbilityStat.MeleeHitsAmplified);
            runtime.Telemetry.Add(live.Profile.AbilityId, RaceAbilityStat.BonusDamage, (long)System.Math.Round(result - damage));
        }
        return result;
    }

    public static float ReduceDamage(in AttackInformation attack, in AttackCollisionData collision, float damage)
    {
        var runtime = Runtime;
        if (runtime == null)
            return damage;
        var effects = EffectsOf(runtime, attack.VictimAgent, out var live);
        if (effects == null)
            return damage;
        var result = runtime.Service.Reduce(damage, effects, attack.IsVictimAgentMount, collision.IsFallDamage);
        if (result != damage && live != null)
        {
            runtime.Telemetry.Add(live.Profile.AbilityId, RaceAbilityStat.HitsReduced);
            runtime.Telemetry.Add(live.Profile.AbilityId, RaceAbilityStat.DamagePrevented, (long)System.Math.Round(damage - result));
        }
        return result;
    }
}
