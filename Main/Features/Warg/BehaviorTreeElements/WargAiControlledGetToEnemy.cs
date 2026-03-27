using BehaviorTrees;
using BehaviorTrees.Nodes;
using BehaviorTreeWrapper.BlackBoardClasses;
using System;
using TaleWorlds.Engine;
using TaleWorlds.Library;
using TaleWorlds.MountAndBlade;

namespace TAOM.Features.Warg.BehaviorTreeElements;

public class WargAiControlledGetToEnemy : BTTask, IBTBannerlordBase, IBTWargBlackboard
{
    BTBlackboardValue<Agent> agent;
    BTBlackboardValue<Agent> _agentHitBy;
    BTBlackboardValue<int> _rageAttackAmount;
    BTBlackboardValue<DateTime?> _rageAttackStartTime;
    BTBlackboardValue<bool> _firstAttack;
    public BTBlackboardValue<Agent> Agent { get => agent; set => agent = value; }
    public BTBlackboardValue<Agent> AgentHitBy { get => _agentHitBy; set => _agentHitBy = value; }
    public BTBlackboardValue<int> RageAttackAmount { get => _rageAttackAmount; set => _rageAttackAmount = value; }
    public BTBlackboardValue<DateTime?> RageAttackStartTime { get => _rageAttackStartTime; set => _rageAttackStartTime = value; }
    public BTBlackboardValue<bool> FirstAttack { get => _firstAttack; set => _firstAttack = value; }

    public override BTTaskStatus Execute()
    {
        Agent warg = Agent.GetValue();
        Agent enemy = AgentHitBy.GetValue();
        warg.RiderAgent.SetIsAIPaused(false);
        warg.RiderAgent.Formation = null;
        Vec3 toPlayer = (enemy.Position - warg.RiderAgent.Position).NormalizedCopy();
        Vec2 dir = toPlayer.AsVec2;
        dir.Normalize();
        float yaw = MathF.Atan2(dir.y, dir.x);
        float handleRiderToWargOffset = yaw - 1.5f;
        WorldPosition position = new(Mission.Current.Scene, enemy.Position);
        warg.RiderAgent.SetScriptedPositionAndDirection(ref position, handleRiderToWargOffset, false, TaleWorlds.MountAndBlade.Agent.AIScriptedFrameFlags.None);
        return BTTaskStatus.FinishedWithTrue;
    }
}
