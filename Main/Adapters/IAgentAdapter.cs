using System;
using System.Collections.Generic;
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
}
