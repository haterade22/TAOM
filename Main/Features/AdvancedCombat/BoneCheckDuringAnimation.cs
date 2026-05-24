using TAOM.Adapters;
using System;
using System.Collections.Generic;
using TaleWorlds.MountAndBlade;

namespace TAOM.Features.AdvancedCombat;

public class BoneCheckDuringAnimation : BoneCheck
{
    private readonly ActionIndexCache _action;
    private readonly float _actionProgressMin;
    private readonly float _actionProgressMax;

    public BoneCheckDuringAnimation(ActionIndexCache action, IAgentAdapter agent, List<IAgentAdapter> targets, List<sbyte> boneIds, float actionProgressMin, float actionProgressMax, float boneCollisionRadius, bool stopAfterFirstHit, Action<IAgentAdapter, IAgentAdapter, sbyte> onCollisionCallback, Action onExpiration)
        // 2026-05-24 (#219): base() previously received `actionProgressMax` (a 0.0-1.0
        // progress fraction) as the maxDuration parameter, which the base class then
        // used as `_maxRangeForCheck` (a squared-meters distance gate). Result: a hard
        // 0.84m cap on agent-to-agent distance (sqrt(0.7)≈0.84) before bone iteration
        // could even run. At 8-10 m/s the warg crossed that gate in <100ms — usually
        // too narrow a window for the per-frame check to land. Passing 100f (≈10m
        // distance cap) makes the gate a real perf optimization (skip distant agents)
        // instead of an unintended hit-rate killer. Spider attacks use this same
        // class and benefit from the same fix.
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

        IAgentVisualsAdapter agentVisuals = _agent.AgentVisuals;
        if (_targets == null || _targets.Count == 0
            || agentVisuals?.GetSkeleton() == null
            || _agent.GetCurrentAction(0) != _action
            || _agent.GetCurrentActionProgress(0) >= _actionProgressMax)
        {
            _onExpiration?.Invoke();
            return false;
        }

        if (_agent.GetCurrentActionProgress(0) >= _actionProgressMin)
        {
            if (!CheckBoneCollision())
            {
                _onExpiration?.Invoke();
                return false;
            }
        }

        return true;
    }
}
