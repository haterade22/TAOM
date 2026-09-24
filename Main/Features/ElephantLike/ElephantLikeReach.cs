using TAOM.Core.Validation;

namespace TAOM.Features.ElephantLike;

/// <summary>
/// The reach multiplier for a creature whose profile scales its reach with its body
/// (<see cref="BehaviorTreeElements.ElephantLikeCombatProfile.ReachScalesWithBody"/>): the mount's live agent scale,
/// which the engine sets from the ridden item's body_length (Mission.BuildAgent: 0.01 x body_length) and TAOM from
/// the Monster's taom_body_length (docs/features/monster-size.md). The engine reads it back natively per call
/// (Agent.AgentScale), so a value that is not finite, not positive or over <see cref="MaxScale"/> falls back to 1:
/// the gate is a positive requirement, so NaN fails it (csharp-architecture.md, engine-float decision gates).
/// </summary>
public static class ElephantLikeReach
{
    /// <summary>The largest scale a Monster size produces (MonsterSizeConfig.MaxBodyLength 1000 = 10x). A larger read is
    /// corrupt, and the scan would reach across the field.</summary>
    public const float MaxScale = 10f;

    public static float Scale(float agentScale)
        => FiniteFloatValidator.IsFiniteInRange(agentScale, float.Epsilon, MaxScale) ? agentScale : 1f;
}
