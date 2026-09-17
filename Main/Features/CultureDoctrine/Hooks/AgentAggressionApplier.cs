using TAOM.Features.CultureDoctrine.Domain;
using TaleWorlds.MountAndBlade;

namespace TAOM.Features.CultureDoctrine.Hooks;

/// <summary>
/// The post-pass that writes a <see cref="CultureAggression"/> profile onto an agent's driven
/// properties, after the base model has derived them from skill
/// (<c>AgentStatCalculateModel.SetAiRelatedProperties</c>). The arithmetic is
/// <see cref="AggressionMath"/>; this is the one place it meets the engine's property names.
/// A vanilla profile returns before touching anything. Runs wherever
/// <c>Agent.UpdateAgentProperties</c> runs: the main thread at spawn, and the async AI thread
/// when a formation's movement or arrangement order changes its defensiveness
/// (`Formation.cs:2838-2844` -> `Agent.cs:1112-1126`), for every man in the formation; so it is
/// nine multiplies over immutable data and nothing else.
/// </summary>
public static class AgentAggressionApplier
{
    public static void Apply(AgentDrivenProperties p, CultureAggression profile)
    {
        if (profile.IsVanilla)
            return;
        p.AIAttackOnDecideChance = AggressionMath.AttackChance(p.AIAttackOnDecideChance, profile);
        p.AiDefendWithShieldDecisionChanceValue = AggressionMath.ShieldDecision(p.AiDefendWithShieldDecisionChanceValue, profile);
        p.AiUseShieldAgainstEnemyMissileProbability = AggressionMath.ShieldAgainstMissiles(p.AiUseShieldAgainstEnemyMissileProbability, profile);
        p.AiShooterError = AggressionMath.Error(p.AiShooterError, profile);
        p.AiRangerLeadErrorMin = AggressionMath.Error(p.AiRangerLeadErrorMin, profile);
        p.AiRangerLeadErrorMax = AggressionMath.Error(p.AiRangerLeadErrorMax, profile);
        p.AiRangerVerticalErrorMultiplier = AggressionMath.Error(p.AiRangerVerticalErrorMultiplier, profile);
        p.AiRangerHorizontalErrorMultiplier = AggressionMath.Error(p.AiRangerHorizontalErrorMultiplier, profile);
        p.AiChargeHorsebackTargetDistFactor = AggressionMath.ChargeDistance(p.AiChargeHorsebackTargetDistFactor, profile);
    }

    /// <summary>The culture a soldier fights for: the character's culture id, null for a mount
    /// or a character without one.</summary>
    public static string? CultureOf(Agent? agent) =>
        agent != null && agent.IsHuman ? agent.Character?.Culture?.StringId : null;
}
