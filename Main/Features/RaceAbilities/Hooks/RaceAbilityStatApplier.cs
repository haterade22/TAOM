using System;
using TAOM.Core.Validation;
using TAOM.Features.RaceAbilities.Domain;
using TaleWorlds.MountAndBlade;

namespace TAOM.Features.RaceAbilities.Hooks;

/// <summary>
/// The post-pass that writes a soldier's live race ability onto his driven properties, after the base
/// model, the career rules and the culture aggression pass have set them. Every property here is one the
/// engine assigns afresh on each <c>UpdateAgentStats</c> in both <c>SandboxAgentStatCalculateModel</c>
/// and <c>CustomBattleAgentStatCalculateModel</c> (v1.5.3: <c>UpdateHumanStats</c>, and
/// <c>SetAiRelatedProperties</c> for the AI values), so a multiply here never compounds. Armour and
/// <c>OffhandWeaponDefendSpeedMultiplier</c> are set only at spawn in Custom Battle, which is why the
/// abilities never touch them. Runs wherever <c>Agent.UpdateAgentProperties</c> runs, the async AI
/// thread included: arithmetic over immutable data and nothing else.
/// </summary>
public static class RaceAbilityStatApplier
{
    public static void Apply(AgentDrivenProperties p, RaceAbilityEffects? effects)
    {
        if (effects == null)
            return;
        p.MaxSpeedMultiplier = Times(p.MaxSpeedMultiplier, effects.MoveSpeedPercent);
        p.CombatMaxSpeedMultiplier = Times(p.CombatMaxSpeedMultiplier, effects.MoveSpeedPercent);
        p.TopSpeedReachDuration = Over(p.TopSpeedReachDuration, effects.AccelerationPercent);
        p.SwingSpeedMultiplier = Times(p.SwingSpeedMultiplier, effects.SwingSpeedPercent);
        p.ThrustOrRangedReadySpeedMultiplier = Times(p.ThrustOrRangedReadySpeedMultiplier, effects.DrawSpeedPercent);
        p.ReloadSpeed = Times(p.ReloadSpeed, effects.ReloadSpeedPercent);
        p.MissileSpeedMultiplier = Times(p.MissileSpeedMultiplier, effects.MissileSpeedPercent);
        p.AIBlockOnDecideAbility = Chance(p.AIBlockOnDecideAbility, effects.BlockAbilityPercent);
        p.AIParryOnDecideAbility = Chance(p.AIParryOnDecideAbility, effects.ParryAbilityPercent);
        p.AIAttackOnDecideChance = Chance(p.AIAttackOnDecideChance, effects.AttackEagernessPercent);
        p.AiShooterError = Times(p.AiShooterError, effects.AimErrorPercent);
    }

    // A horse ridden by a soldier whose ability is live. MountSpeed is assigned afresh on every
    // UpdateHorseStats in both models (v1.5.3 Sandbox :1266/:1271, Custom Battle :386/:391), so this never
    // compounds either.
    public static void ApplyMount(AgentDrivenProperties p, RaceAbilityEffects? effects)
    {
        if (effects == null)
            return;
        p.MountSpeed = Times(p.MountSpeed, effects.MountSpeedPercent);
    }

    // A multiplier or a rate, raised by the percentage.
    public static float Times(float value, float percent) =>
        percent == 0f ? value : Finite(value * (1f + percent / 100f), value);

    // A duration, shortened by the percentage (TopSpeedReachDuration: faster acceleration, less time).
    public static float Over(float value, float percent) =>
        percent == 0f ? value : Finite(value / (1f + percent / 100f), value);

    // A probability, raised by the percentage and kept within 0 to 1.
    public static float Chance(float value, float percent)
    {
        var result = Times(value, percent);
        return FiniteFloatValidator.IsFinite(result) ? Math.Min(1f, Math.Max(0f, result)) : result;
    }

    private static float Finite(float result, float fallback) =>
        FiniteFloatValidator.IsFinite(result) ? result : fallback;
}
