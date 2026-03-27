using TAOM.Adapters;
using TAOM.Core.Logging;
using System;
using System.Collections.Generic;
using TaleWorlds.Engine;
using TaleWorlds.Library;

namespace TAOM.Features.AdvancedCombat;

public class BoneCheck
{
    private static IModLogger Logger => IoC.Resolve<IModLogger>();
    protected IAgentAdapter _agent;
    protected List<IAgentAdapter> _targets;
    protected List<sbyte> _boneIds;
    protected float _collisionRadiusSquared;
    protected float _maxRangeForCheck;
    protected float _maxDuration;
    protected bool _stopOnFirstHit;
    protected float _boneCheckLifeTime = 0f;
    protected Action<IAgentAdapter, IAgentAdapter, sbyte> _onCollisionCallback;
    protected Action _onExpiration;

    public BoneCheck(IAgentAdapter agent, List<IAgentAdapter> targets, List<sbyte> boneIds, float maxDuration, float boneCollisionRadius, bool stopAfterFirstHit, Action<IAgentAdapter, IAgentAdapter, sbyte> onCollisionCallback, Action onExpiration)
    {
        _agent = agent;
        _targets = targets;
        _boneIds = boneIds;
        _collisionRadiusSquared = boneCollisionRadius * boneCollisionRadius;
        _maxRangeForCheck = Math.Max(20f, _collisionRadiusSquared * 20f);
        _maxDuration = maxDuration;
        _stopOnFirstHit = stopAfterFirstHit;
        _onCollisionCallback = onCollisionCallback;
        _onExpiration = onExpiration;
    }

    public virtual bool Tick(float dt)
    {
        _boneCheckLifeTime += dt;
        if (_boneCheckLifeTime >= _maxDuration)
        {
            _onExpiration?.Invoke();
            return false;
        }
        if (!CheckBoneCollision())
        {
            _onExpiration?.Invoke();
            return false;
        }
        return true;
    }

    protected bool CheckBoneCollision()
    {
        if (_agent == null || !_agent.IsActive() || _agent.IsFadingOut())
        {
            Logger.LogWarning($"Agent {_agent?.Name ?? "null"} is no longer valid for bone collision check");
            return false;
        }

        IAgentVisualsAdapter agentVisuals = _agent.AgentVisuals;
        if (agentVisuals == null)
        {
            Logger.LogWarning($"Failed to get visuals for {_agent.Name}");
            return false;
        }

        Skeleton agentSkeleton = agentVisuals.GetSkeleton();
        if (agentSkeleton == null)
        {
            Logger.LogWarning($"Failed to get skeleton for {_agent.Name}");
            return false;
        }
        MatrixFrame agentGlobalFrame = agentVisuals.GetGlobalFrame();

        List<(sbyte, Vec3)> agentBonePositions = new();
        int boneCount = agentSkeleton.GetBoneCount();
        foreach (sbyte bone in _boneIds)
        {
            if (bone < 0 || bone >= boneCount)
            {
                Logger.LogError($"Invalid bone index {bone} for agent {_agent.Name}");
                continue;
            }
            MatrixFrame agentBoneFrame = agentSkeleton.GetBoneEntitialFrameWithIndex(bone);
            Vec3 agentBoneGlobalPos = agentGlobalFrame.TransformToParent(agentBoneFrame.origin);
            agentBonePositions.Add((bone, agentBoneGlobalPos));
        }

        for (int i = 0; i < _targets.Count; i++)
        {
            IAgentAdapter target = _targets[i];

            if (target == null || !target.IsActive() || target.IsFadingOut())
            {
                _targets.RemoveAt(i);
                i--;
                continue;
            }

            IAgentVisualsAdapter targetVisuals = target.AgentVisuals;
            if (targetVisuals == null)
            {
                _targets.RemoveAt(i);
                i--;
                continue;
            }

            Skeleton targetSkeleton = targetVisuals.GetSkeleton();
            if (targetSkeleton == null)
            {
                _targets.RemoveAt(i);
                i--;
                continue;
            }
            MatrixFrame targetGlobalFrame = targetVisuals.GetGlobalFrame();
            sbyte boneId = FindBoneInRange(agentGlobalFrame, agentBonePositions, targetSkeleton, targetGlobalFrame);
            if (boneId != -1)
            {
                _targets.RemoveAt(i);
                _onCollisionCallback?.Invoke(_agent, target, boneId);
                if (_stopOnFirstHit)
                    return false;
            }
        }
        return true;
    }

    protected sbyte FindBoneInRange(MatrixFrame agentGlobalFrame, List<(sbyte boneId, Vec3 position)> agentBonePositions, Skeleton targetSkeleton, MatrixFrame targetGlobalFrame)
    {
        int targetBoneCount = targetSkeleton.GetBoneCount();
        if ((targetGlobalFrame.origin - agentGlobalFrame.origin).LengthSquared > _maxRangeForCheck)
            return -1;

        for (int i = 0; i < targetBoneCount; i++)
        {
            MatrixFrame targetBoneFrame = targetSkeleton.GetBoneEntitialFrameWithIndex((sbyte)i);
            Vec3 targetBoneGlobalPos = targetGlobalFrame.TransformToParent(targetBoneFrame.origin);
            foreach (var (boneId, agentBonePos) in agentBonePositions)
            {
                float distanceSquared = (targetBoneGlobalPos - agentBonePos).LengthSquared;
                if (distanceSquared <= _collisionRadiusSquared)
                    return (sbyte)i;
            }
        }
        return -1;
    }
}
