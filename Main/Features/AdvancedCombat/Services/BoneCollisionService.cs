using TAOM.Adapters;
using System;
using System.Collections.Generic;
using TaleWorlds.MountAndBlade;

namespace TAOM.Features.AdvancedCombat.Services;

public class BoneCollisionService : IBoneCollisionService
{
    private readonly List<BoneCheck> _boneCheckComponents = new();

    public BoneCheck CreateTimedBoneCheck(
        IAgentAdapter agent, List<IAgentAdapter> targets, List<sbyte> boneIds,
        float maxDuration, float boneCollisionRadius, bool stopAfterFirstHit,
        Action<IAgentAdapter, IAgentAdapter, sbyte> onCollisionCallback, Action onExpiration)
    {
        return new BoneCheck(agent, targets, boneIds, maxDuration, boneCollisionRadius, stopAfterFirstHit, onCollisionCallback, onExpiration);
    }

    public BoneCheck CreateAnimationBoneCheck(
        ActionIndexCache action, IAgentAdapter agent, List<IAgentAdapter> targets, List<sbyte> boneIds,
        float actionProgressMin, float actionProgressMax, float boneCollisionRadius, bool stopAfterFirstHit,
        Action<IAgentAdapter, IAgentAdapter, sbyte> onCollisionCallback, Action onExpiration)
    {
        return new BoneCheckDuringAnimation(action, agent, targets, boneIds, actionProgressMin, actionProgressMax, boneCollisionRadius, stopAfterFirstHit, onCollisionCallback, onExpiration);
    }

    public void AddBoneCheckComponent(BoneCheck component)
    {
        if (component == null)
            throw new ArgumentNullException(nameof(component));
        _boneCheckComponents.Add(component);
    }

    public void TickBoneChecks(float dt)
    {
        for (int i = _boneCheckComponents.Count - 1; i >= 0; i--)
        {
            bool isAlive = _boneCheckComponents[i].Tick(dt);
            if (!isAlive)
                _boneCheckComponents.RemoveAt(i);
        }
    }

    public void Clear()
    {
        _boneCheckComponents.Clear();
    }
}
