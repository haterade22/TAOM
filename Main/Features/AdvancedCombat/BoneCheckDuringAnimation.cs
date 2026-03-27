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
        : base(agent, targets, boneIds, actionProgressMax, boneCollisionRadius, stopAfterFirstHit, onCollisionCallback, onExpiration)
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
