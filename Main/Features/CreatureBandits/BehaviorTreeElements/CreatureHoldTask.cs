using BehaviorTrees;
using BehaviorTrees.Nodes;
using BehaviorTreeWrapper.BlackBoardClasses;
using TAOM.Features.AdvancedCombat;
using TaleWorlds.Engine;
using TaleWorlds.MountAndBlade;
using ScriptedFlags = TaleWorlds.MountAndBlade.Agent.AIScriptedFrameFlags;

namespace TAOM.Features.CreatureBandits.BehaviorTreeElements;

/// <summary>
/// Holds a creature bandit where it stands (#692): a scripted move to its own position. The tree runs it while the
/// creature may not fight (deployment), and <see cref="CreatureHuntTask"/> uses the same hold mid-strike and when no
/// enemy is left. The creature is never left without a scripted move: a riderless Mountable agent with none falls
/// into the engine's loose-horse "running away" mode (playtest 2026-09-27), which is what a creature route A skipped
/// would do. Under the deployment teleport a move to its own position stays put (<c>Agent.cs:2462-2465</c> teleports
/// only to a different XY). Main thread; the handle is held across frames, so a recycled slot is checked first.
/// </summary>
public class CreatureHoldTask : BTTask, IBTBannerlordBase
{
    private const ScriptedFlags HoldFlags = ScriptedFlags.GoToPosition;

    private BTBlackboardValue<Agent> _agent;
    public BTBlackboardValue<Agent> Agent { get => _agent; set => _agent = value; }

    public override BTTaskStatus Execute()
    {
        Agent creature = Agent.GetValue();
        if (creature == null || !creature.IsActive() || !AgentSlotIdentity.IsCurrentOccupant(creature))
            return BTTaskStatus.FinishedWithFalse;
        Hold(creature);
        return BTTaskStatus.FinishedWithTrue;
    }

    internal static void Hold(Agent creature)
    {
        WorldPosition here = creature.GetWorldPosition();
        creature.SetScriptedPosition(ref here, addHumanLikeDelay: false, HoldFlags);
    }
}
