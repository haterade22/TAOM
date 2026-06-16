using System;
using System.Collections.Generic;
using TaleWorlds.Core;
using TaleWorlds.Library;
using TaleWorlds.MountAndBlade;

namespace TAOM.Adapters;

public interface IAgentAdapter
{
    int Index { get; }
    IAgentVisualsAdapter AgentVisuals { get; }
    string Name { get; }
    bool IsHuman { get; }
    bool HasMount { get; }
    Vec3 Position { get; }
    Vec2 MovementVelocity { get; }
    MatrixFrame Frame { get; }
    bool IsMount { get; }
    IAgentAdapter RiderAgent { get; }
    IAgentAdapter MountAgent { get; }
    bool IsAIControlled { get; }

    ActionIndexCache GetCurrentAction(int actionChannelNo);
    float GetCurrentActionProgress(int actionChannelNo);
    bool IsWarg();
    bool IsHorse();
    bool IsCamel();
    bool IsActive();
    bool IsFadingOut();
    int Health { get; }
    AgentState State { get; }
    bool IsSameTeam(IAgentAdapter other);
    int GetBaseArmorEffectivenessForBodyPart(BoneBodyPartType bodyPart);
    void ProjectAgent(Vec3 position, DamageAnimation animation);
    bool IsAttackLikelyToHit(IAgentAdapter attacker, float coneAngle, float attackDistance);
    void CustomAttack(
        ActionIndexCache action,
        List<sbyte> bonesIdsForCollision,
        float actionProgressMin,
        float actionProgressMax,
        float targetDetectionRange,
        float boneCollisionRadius,
        bool stopOnFirstHit,
        Action<IAgentAdapter, IAgentAdapter, sbyte> onHitCallback,
        Action onExpirationCallback = null);

    /// <summary>Plays <paramref name="action"/> then fires <paramref name="onHitCallback"/> instantly for every
    /// enemy within <paramref name="strikeRadius"/> whose horizontal bearing from this agent's look-direction is
    /// inside [<paramref name="arcCenterBearingDeg"/> ± <paramref name="arcHalfAngleDeg"/>] (signed, + = LEFT).
    /// The reliable radial alternative to bone-collision for large mounts (elephant-style); excludes self + rider.</summary>
    void RadialStrike(
        ActionIndexCache action,
        float strikeRadius,
        float arcHalfAngleDeg,
        float arcCenterBearingDeg,
        Action<IAgentAdapter, IAgentAdapter, sbyte> onHitCallback);
}
