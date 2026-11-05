using System.Collections.Generic;
using System.Linq;
using TaleWorlds.Core;
using TaleWorlds.MountAndBlade;
using TAOM.Features.FordsOfWar.Domain;
using TAOM.Features.TrollBruteForce;
using TAOM.Features.Warg;

namespace TAOM.Adapters;

/// <summary>
/// The engine half of the commander taking control of a soldier. The mission's main agent is the only state, so no agent is held across
/// frames here: the engine clears it when the soldier leaves the mission (<c>Mission.OnAgentRemoved</c>). Call <see cref="Take"/> and
/// <see cref="Return"/> from a mission tick: the Controller setter changes the agent's component list, and a view tick overlaps the agent tick
/// (csharp-architecture.md). <see cref="Takeable"/> names a person a player can drive. Tower gunners carry no formation, a howdah or mumak seat
/// makes its crew undetachable and teleports them each frame, a soldier in flight or fading out is leaving, a troll's release and blocked clips
/// have never run in a logged battle (troll-race.md), and a behavior tree drives any creature that a rider sits on.
/// </summary>
internal static class FordsAgentControl
{
    internal static bool IsControlling(Mission mission) => mission.MainAgent != null;

    internal static IEnumerable<Agent> Takeable(Mission mission) =>
        mission.PlayerTeam?.ActiveAgents.Where(CanTake) ?? Enumerable.Empty<Agent>();

    /// <summary>The takeable unit nearest to any enemy, or null. The unattended check uses it, as no pointer exists in such a run.</summary>
    internal static Agent? Front(Mission mission)
    {
        var enemies = mission.PlayerEnemyTeam?.ActiveAgents;
        if (enemies == null || enemies.Count == 0)
            return Takeable(mission).FirstOrDefault();

        return Takeable(mission).OrderBy(unit => enemies.Min(enemy => enemy.Position.DistanceSquared(unit.Position))).FirstOrDefault();
    }

    /// <summary>
    /// The engine's own call: it ends the engine's fast forward, and the Controller setter makes the soldier the main agent. A soldier can still
    /// mount a loose warg during a fight, and its rage turns vanilla's controller off until the rage ends, so a take turns it back on.
    /// </summary>
    internal static void Take(Mission mission, Agent agent)
    {
        WargMissionBehavior.SwitchMainAgentController(false);
        mission.TakeControlOfAgent(agent);
    }

    /// <summary>
    /// Hands the soldier back to its AI. No vanilla code clears the main agent of a living agent, and the Controller setter leaves it set, so
    /// this does. The first person flag, the scripted crouch and the stand and run requests belong to the player's controller, which stops
    /// writing them here. The AI logics add new components, and <c>AddComponent</c> initializes neither: the behavior values stay on Default until
    /// the lane's next order, and morale at a flat 50 with no recovery, so this sets both. <c>MissionState.OpenNew</c> adds
    /// <c>AgentCommonAILogic</c> to every mission by default (MissionState.cs:302, :329), so the morale component is there.
    /// </summary>
    internal static void Return(Mission mission)
    {
        var agent = mission.MainAgent;
        if (agent == null)
            return;

        agent.HeadCameraMode = false;
        agent.SetScriptedFlags(agent.GetScriptedFlags() & ~Agent.AIScriptedFrameFlags.Crouch);
        if (agent.CrouchMode)
            agent.EventControlFlags |= Agent.EventControlFlag.Stand;
        if (agent.WalkMode)
            agent.EventControlFlags |= Agent.EventControlFlag.Run;

        agent.Controller = AgentControllerType.AI;
        mission.MainAgent = null;

        agent.CommonAIComponent?.Initialize();
        var formation = agent.Formation;
        if (formation != null)
            agent.HumanAIComponent?.RefreshBehaviorValues(formation.GetReadonlyMovementOrderReference().OrderEnum, formation.ArrangementOrder.OrderEnum);
    }

    private static bool CanTake(Agent agent) =>
        agent.IsHuman && agent.IsActive() && !agent.IsFadingOut() && !agent.IsRunningAway && agent.Formation != null
        && agent.IsDetachableFromFormation && !agent.IsUsingGameObject && !IsTroll(agent)
        && FordsControlPick.MayRide(agent.MountAgent?.Monster?.StringId);

    private static bool IsTroll(Agent agent)
    {
        var monster = agent.Monster?.StringId;
        return monster != null && TrollBruteForceConfig.ActionSetsByMonster.ContainsKey(monster);
    }
}
