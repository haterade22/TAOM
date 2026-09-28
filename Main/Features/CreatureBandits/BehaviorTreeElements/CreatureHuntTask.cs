using System;
using BehaviorTrees;
using BehaviorTrees.Nodes;
using BehaviorTreeWrapper.BlackBoardClasses;
using TAOM.Features.AdvancedCombat;
using TAOM.Features.CreatureBandits.Diagnostics;
using TaleWorlds.Engine;
using TaleWorlds.Library;
using TaleWorlds.MountAndBlade;
using ScriptedFlags = TaleWorlds.MountAndBlade.Agent.AIScriptedFrameFlags;

namespace TAOM.Features.CreatureBandits.BehaviorTreeElements;

/// <summary>
/// Moves a riderless creature bandit toward the nearest enemy (#692). No rider steers it, and it is in no formation,
/// so nothing else would. Scripted position with <c>GoToPosition</c>, which paths on the navmesh and suspends the
/// agent's own movement AI for the move (v1.5.3 <c>Agent.cs:2460</c>, <c>3244</c>); <c>NeverSlowDown</c> keeps a chase
/// at speed. The search covers the whole mission, not a radius: the June spider wandered at battle start because its
/// 16 m engage gate saw no enemy (<c>rca-spider-troop-2026-06-04.md:63</c>). The search walks the hostile teams'
/// active agents: one native <c>IsEnemyOf</c> per team, not per soldier. While the creature's own attack clip plays the
/// task holds it where it stands (<see cref="CreatureHoldTask.Hold"/>), so a bite is not dragged across the ground;
/// the next re-aim restores the chase. It never releases the scripted move mid-fight: a riderless Mountable agent with
/// none falls into the engine's loose-horse "running away" mode (playtest 2026-09-27: runningAway=1 in every sample
/// with scripted=None, 0 with GoToPosition). The tree runs it only once deployment is over
/// (<see cref="CreatureMayFightDecorator"/>).
///
/// Main thread: trees tick from <c>BehaviorTreeMissionLogic.OnMissionTick</c> (#592). The creature handle is held
/// across frames, so a recycled slot is checked first. Must not throw (a throw stops the tree for the battle).
/// </summary>
public class CreatureHuntTask : BTTask, IBTBannerlordBase
{
    private const ScriptedFlags ChaseFlags = ScriptedFlags.GoToPosition | ScriptedFlags.NeverSlowDown;

    private readonly Func<Agent, bool> _isAttacking;

    private BTBlackboardValue<Agent> _agent;
    public BTBlackboardValue<Agent> Agent { get => _agent; set => _agent = value; }

    /// <param name="isAttacking">True while the creature's own attack clip plays.</param>
    public CreatureHuntTask(Func<Agent, bool> isAttacking) => _isAttacking = isAttacking;

    public override BTTaskStatus Execute()
    {
        Agent creature = Agent.GetValue();
        Mission mission = Mission.Current;
        if (creature == null || mission == null || !creature.IsActive())
            return BTTaskStatus.FinishedWithFalse;
        if (!AgentSlotIdentity.IsCurrentOccupant(creature))
        {
            CreatureBanditDiag.NoteStaleHandle(creature);
            return BTTaskStatus.FinishedWithFalse;
        }
        if (_isAttacking(creature))
        {
            CreatureHoldTask.Hold(creature);
            return BTTaskStatus.FinishedWithTrue;
        }

        Agent? target = FindNearestEnemy(mission, creature);
        if (target == null)
        {
            CreatureBanditDiag.NoteHunt(creature, null, float.NaN, mission.CurrentTime);
            CreatureHoldTask.Hold(creature);
            return BTTaskStatus.FinishedWithFalse;
        }

        CreatureBanditDiag.NoteHunt(creature, target, target.Position.Distance(creature.Position), mission.CurrentTime);
        WorldPosition destination = target.GetWorldPosition();
        creature.SetScriptedPosition(ref destination, addHumanLikeDelay: false, ChaseFlags);
        return BTTaskStatus.FinishedWithTrue;
    }

    /// <summary>The nearest active humanoid on a team hostile to the creature's; mounts are never hunted.</summary>
    private static Agent? FindNearestEnemy(Mission mission, Agent creature)
    {
        Team team = creature.Team;
        if (team == null) return null;

        Vec3 origin = creature.Position;
        Agent? best = null;
        float bestDistanceSquared = float.MaxValue;
        foreach (Team other in mission.Teams)
        {
            if (other == team || !other.IsEnemyOf(team)) continue;
            foreach (Agent agent in other.ActiveAgents)
            {
                if (!agent.IsHuman || !agent.IsActive()) continue;
                float distanceSquared = agent.Position.DistanceSquared(origin);
                if (distanceSquared < bestDistanceSquared)
                {
                    bestDistanceSquared = distanceSquared;
                    best = agent;
                }
            }
        }
        return best;
    }
}
