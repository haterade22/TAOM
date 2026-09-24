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
    // The attacker's tracked bone positions, refilled every tick (was a new list per tick).
    private readonly List<(sbyte, Vec3)> _agentBonePositions = new();

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
        return CheckBoneCollision(agentVisuals, agentSkeleton);
    }

    /// <summary>
    /// The collision pass for an attacker the caller has already validated this tick, reusing the
    /// skeleton it fetched: MBAgentVisuals.GetSkeleton builds a new native wrapper on every call.
    /// </summary>
    protected bool CheckBoneCollision(IAgentVisualsAdapter agentVisuals, Skeleton agentSkeleton)
    {
        MatrixFrame agentGlobalFrame = agentVisuals.GetGlobalFrame();

        _agentBonePositions.Clear();
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
            _agentBonePositions.Add((bone, agentBoneGlobalPos));
        }

        return CheckTargets(agentGlobalFrame, _agentBonePositions);
    }

    /// <summary>
    /// Tests every held target against the attacker's bone positions. Returns false when a hit ends
    /// the check (stop on first hit), true otherwise. Internal for TAOM.Tests.
    /// </summary>
    internal bool CheckTargets(MatrixFrame agentGlobalFrame, List<(sbyte, Vec3)> agentBonePositions)
    {
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

            MatrixFrame targetGlobalFrame = targetVisuals.GetGlobalFrame();
            // Range gate BEFORE the skeleton: GetSkeleton builds a new native wrapper on every call (a
            // ref-count call, a lock, a GCHandle and a finalizer), and most agents captured at 20 m are
            // outside this gate on any given frame. A target out of range stays for a later frame.
            if ((targetGlobalFrame.origin - agentGlobalFrame.origin).LengthSquared > _maxRangeForCheck)
                continue;

            Skeleton targetSkeleton = targetVisuals.GetSkeleton();
            // `is null`, not `==`: NativeObject.operator == runs NativeObject's static constructor,
            // which calls native code; the result is the same and unit tests can reach this line.
            if (targetSkeleton is null)
            {
                _targets.RemoveAt(i);
                i--;
                continue;
            }
            sbyte boneId = FindBoneInRange(agentBonePositions, targetSkeleton, targetGlobalFrame);
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

    protected sbyte FindBoneInRange(List<(sbyte boneId, Vec3 position)> agentBonePositions, Skeleton targetSkeleton, MatrixFrame targetGlobalFrame)
    {
        int targetBoneCount = targetSkeleton.GetBoneCount();
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
