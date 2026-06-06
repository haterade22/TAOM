using BehaviorTrees;
using BehaviorTrees.Nodes;
using BehaviorTreeWrapper.BlackBoardClasses;
using System.Collections.Generic;
using TAOM.Core.Logging;
using TAOM.Features.AdvancedCombat;
using TAOM.Features.AdvancedCombat.BaseBehaviorTree;
using TaleWorlds.Engine;
using TaleWorlds.Library;
using TaleWorlds.MountAndBlade;

namespace TAOM.Features.Spider.BehaviorTreeElements;

/// <summary>
/// Drives the DETACHED spider toward its nearest enemy via <c>Agent.SetScriptedPositionAndDirection</c>
/// (Agent.cs:2417 — a pure native frame call with NO wield/missile read, so it is SAFE on the spider's
/// uninitialized native state). Without this a detached spider has no formation movement orders and just
/// stands until an enemy wanders into its ~4m bite reach. Runs only in the "engage" branch (reached when
/// <see cref="NoEnemyNearSpiderDecorator"/> already detected an enemy within ~16m). Always returns
/// FinishedWithTrue so the following bite still attempts. HEAVILY logged with [Spider][diag] so we can
/// trace detection → advance → bite range every cycle (TEMP — strip once the spider ships).
/// </summary>
public class SpiderMoveToEnemyTask : BTTask, IBTBannerlordBase, IBTSpiderBlackboard
{
    BTBlackboardValue<Agent> _agent;
    public BTBlackboardValue<Agent> Agent { get => _agent; set => _agent = value; }

    public override BTTaskStatus Execute()
    {
        IModLogger logger = IoC.Resolve<IModLogger>();

        Agent spider = Agent.GetValue();
        if (spider == null || !spider.IsActive())
        {
            logger?.LogInfo("[Spider][diag] move: spider null/inactive — skipping move.");
            return BTTaskStatus.FinishedWithTrue;
        }
        Mission mission = Mission.Current;
        if (mission == null)
        {
            logger?.LogInfo("[Spider][diag] move: Mission.Current null — skipping move.");
            return BTTaskStatus.FinishedWithTrue;
        }

        float biteRange = SpiderConfig.TargetDetectionRange; // ~4m — the CustomAttack reach

        // Find the nearest enemy ANYWHERE in the mission, not a 16m scan. A detached spider has no
        // formation movement orders, so it must advance toward the enemy line from across the battlefield.
        // The old SpatialGrid 16m scan meant the engage branch only ran once an enemy wandered adjacent —
        // confirmed in-game the move/bite never fired (0 logs) and the spiders just idled + mount-wandered.
        Team team = spider.Team;
        Agent nearest = null;
        float nearestDistSq = float.MaxValue;
        int enemyCount = 0;
        foreach (Agent c in mission.Agents)
        {
            if (c == spider || !c.IsActive()) continue;
            if (c.Team == null || c.Team == team) continue;
            enemyCount++;
            float dSq = (c.Position - spider.Position).LengthSquared;
            if (dSq < nearestDistSq) { nearestDistSq = dSq; nearest = c; }
        }

        if (nearest == null)
        {
            logger?.LogInfo($"[Spider][diag] move: NO enemy in mission (agents={mission.Agents.Count}) " +
                            $"spider@{spider.Position} team={team?.Side.ToString() ?? "null"}.");
            return BTTaskStatus.FinishedWithTrue;
        }

        float dist = MathF.Sqrt(nearestDistSq);
        if (dist <= biteRange)
        {
            logger?.LogInfo($"[Spider][diag] move: enemy '{nearest.Name}' in bite range ({dist:0.0}m <= {biteRange:0.0}m) — " +
                            $"holding to bite. spider@{spider.Position} enemy@{nearest.Position} enemies={enemyCount}.");
            return BTTaskStatus.FinishedWithTrue;
        }

        Vec3 toEnemy = nearest.Position - spider.Position;
        Vec2 dir = toEnemy.AsVec2;
        dir.Normalize();
        float yaw = MathF.Atan2(dir.y, dir.x);
        WorldPosition target = new WorldPosition(Mission.Current.Scene, nearest.Position);

        try
        {
            spider.SetScriptedPositionAndDirection(ref target, yaw, false,
                TaleWorlds.MountAndBlade.Agent.AIScriptedFrameFlags.None);
            logger?.LogInfo($"[Spider][diag] move: ADVANCING to enemy '{nearest.Name}' dist={dist:0.0}m yaw={yaw:0.00} " +
                            $"spider@{spider.Position} -> target@{nearest.Position} vel={spider.MovementVelocity} " +
                            $"enemies={enemyCount} mount={spider.IsMount}.");
        }
        catch (System.Exception e)
        {
            logger?.LogError($"[Spider] move: SetScriptedPositionAndDirection threw {e.GetType().Name}: {e.Message}\n{e.StackTrace}");
        }
        return BTTaskStatus.FinishedWithTrue;
    }
}
