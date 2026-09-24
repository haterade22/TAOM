using TAOM.Adapters;
using System;
using System.Collections.Generic;
using TaleWorlds.Engine;
using TaleWorlds.MountAndBlade;

namespace TAOM.Features.AdvancedCombat;

public class BoneCheckDuringAnimation : BoneCheck
{
    private readonly ActionIndexCache _action;
    private readonly float _actionProgressMin;
    private readonly float _actionProgressMax;

    public BoneCheckDuringAnimation(ActionIndexCache action, IAgentAdapter agent, List<IAgentAdapter> targets, List<sbyte> boneIds, float actionProgressMin, float actionProgressMax, float boneCollisionRadius, bool stopAfterFirstHit, Action<IAgentAdapter, IAgentAdapter, sbyte> onCollisionCallback, Action onExpiration)
        // 2026-05-24 (#219): this argument once fed the base class's distance gate, capping reach at
        // about 0.84 m. It is now maxDuration, which this class never reads (Tick below ends the check
        // on the action window instead). The distance gate is BoneCheck's _maxRangeForCheck,
        // max(20, radius squared x 20) square metres: about 4.5 m for the warg's 0.5 and 1.0 radii.
        : base(agent, targets, boneIds, 100f, boneCollisionRadius, stopAfterFirstHit, onCollisionCallback, onExpiration)
    {
        _action = action;
        _actionProgressMin = actionProgressMin;
        _actionProgressMax = actionProgressMax;
    }

    public override bool Tick(float dt)
    {
        if (_agent == null || !_agent.IsActive() || _agent.IsFadingOut())
        {
            _onExpiration?.Invoke();
            return false;
        }

        if (_targets == null || _targets.Count == 0)
        {
            _onExpiration?.Invoke();
            return false;
        }

        if (_agent.GetCurrentAction(0) != _action)
        {
            _onExpiration?.Invoke();
            return false;
        }

        // One progress read per tick, used for both bounds.
        float progress = _agent.GetCurrentActionProgress(0);
        if (progress >= _actionProgressMax)
        {
            _onExpiration?.Invoke();
            return false;
        }

        // The attacker's skeleton is fetched only inside the hit window, once per tick, and handed
        // to CheckBoneCollision: every GetSkeleton call builds a new native wrapper (a ref-count
        // call, a lock, a GCHandle and a finalizer). A wind-up frame fetches none, so a missing
        // skeleton during the wind-up ends the bite when the hit window opens (#659).
        if (progress >= _actionProgressMin)
        {
            IAgentVisualsAdapter agentVisuals = _agent.AgentVisuals;
            Skeleton agentSkeleton = agentVisuals?.GetSkeleton();
            if (agentSkeleton is null || !CheckBoneCollision(agentVisuals, agentSkeleton))
            {
                _onExpiration?.Invoke();
                return false;
            }
        }

        return true;
    }
}
