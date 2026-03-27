using TAOM.Adapters;
using System;
using System.Collections.Generic;
using TaleWorlds.MountAndBlade;

namespace TAOM.Features.AdvancedCombat.Services;

public interface IBoneCollisionService
{
    BoneCheck CreateTimedBoneCheck(
        IAgentAdapter agent, List<IAgentAdapter> targets, List<sbyte> boneIds,
        float maxDuration, float boneCollisionRadius, bool stopAfterFirstHit,
        Action<IAgentAdapter, IAgentAdapter, sbyte> onCollisionCallback, Action onExpiration);

    BoneCheck CreateAnimationBoneCheck(
        ActionIndexCache action, IAgentAdapter agent, List<IAgentAdapter> targets, List<sbyte> boneIds,
        float actionProgressMin, float actionProgressMax, float boneCollisionRadius, bool stopAfterFirstHit,
        Action<IAgentAdapter, IAgentAdapter, sbyte> onCollisionCallback, Action onExpiration);

    void AddBoneCheckComponent(BoneCheck component);
    void TickBoneChecks(float dt);
    void Clear();
}
