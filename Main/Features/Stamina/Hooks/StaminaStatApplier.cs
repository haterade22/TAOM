using System;
using TAOM.Core.Validation;
using TAOM.Features.Stamina.Domain;
using TaleWorlds.MountAndBlade;

namespace TAOM.Features.Stamina.Hooks;

/// <summary>
/// The post-pass that writes a soldier's stamina band onto his driven properties, after the base model, the career
/// rules, the culture aggression pass and the race abilities have set them. Both base models
/// (SandboxAgentStatCalculateModel, CustomBattleAgentStatCalculateModel) assign every property here afresh on each
/// UpdateAgentStats (v1.5.7: Sandbox :686-688, :797, :801; Custom Battle :213, :215, :323, :327; the AI values in
/// AgentStatCalculateModel.SetAiRelatedProperties), so a multiply never compounds. Runs wherever
/// Agent.UpdateAgentProperties runs, the async AI thread included: arithmetic over immutable data and nothing else.
/// </summary>
public static class StaminaStatApplier
{
    // The floor the base model keeps on AIAttackOnDecideChance (AgentStatCalculateModel.SetAiRelatedProperties).
    public const float MinAttackChance = 0.05f;

    /// <param name="mounted">A rider keeps his speed values: they may reach the horse, and mounts are left alone.</param>
    public static void Apply(AgentDrivenProperties p, StaminaBandEffects effects, bool mounted)
    {
        p.SwingSpeedMultiplier = Scale(p.SwingSpeedMultiplier, effects.Swing);
        p.ThrustOrRangedReadySpeedMultiplier = Scale(p.ThrustOrRangedReadySpeedMultiplier, effects.Swing);
        p.HandlingMultiplier = Scale(p.HandlingMultiplier, effects.Handling);
        if (!mounted)
        {
            p.MaxSpeedMultiplier = Scale(p.MaxSpeedMultiplier, effects.Move);
            p.CombatMaxSpeedMultiplier = Scale(p.CombatMaxSpeedMultiplier, effects.Move);
        }

        p.AIAttackOnDecideChance = LowerChance(p.AIAttackOnDecideChance, effects.AiAttack);
    }

    // A result that is not a finite number leaves the value as it was.
    private static float Scale(float value, float factor)
    {
        var result = value * factor;
        return FiniteFloatValidator.IsFinite(result) ? result : value;
    }

    // Lowers a chance and keeps the base model's floor, but never raises a value another rule already put below it.
    private static float LowerChance(float value, float factor)
    {
        var result = value * factor;
        if (!FiniteFloatValidator.IsFinite(result))
            return value;
        return Math.Min(value, Math.Max(MinAttackChance, result));
    }
}
