using System.Collections.Generic;
using SandBox.Missions.MissionLogics.Arena;
using TaleWorlds.MountAndBlade;
using TAOM.Core.Logging;
using TAOM.Features.AdvancedCombat;

namespace TAOM.Features.TrainingGrounds.Teams.Hooks;

/// <summary>
/// #1528: the engine glue of the team practice fight, called by the three patches on vanilla's practice controller.
/// Every decision is <see cref="TeamPracticeRules"/>; this class only reads the controller and writes the teams.
/// All three run on the main mission thread (the mission tick, and <c>AfterStart</c> and <c>StartPractice</c> beneath it).
/// </summary>
internal static class TeamPracticeFight
{
    internal const string ModeCheckId = "taom.team-practice.mode";
    internal const string WinCheckId = "taom.team-practice.win";

    /// <summary>
    /// Splits the six AI teams into two sides, takes friendly fire off for the mission, and, while the player
    /// practises, makes the allied teams friends of the player's team. Vanilla ran first and made every team an enemy.
    /// </summary>
    internal static void ArrangeSides(ArenaPracticeFightMissionController controller, IModLogger? logger)
    {
        var teams = TeamPracticeAccess.AiTeams(controller);
        var mission = controller.Mission;
        var player = mission?.PlayerTeam;
        if (teams == null || mission == null || player == null)
            return;

        MissionThreadGuard.NoteCall("TeamPractice.ArrangeSides", message => logger?.LogError(message));
        var count = teams.Count;
        for (var i = 0; i < count; i++)
            for (var j = i + 1; j < count; j++)
                teams[i].SetIsEnemyOf(teams[j], TeamPracticeRules.AreEnemies(i, j, count));

        if (controller.IsPlayerPracticing)
            for (var i = 0; i < count; i++)
                if (TeamPracticeRules.IsAllyTeam(i, count))
                    teams[i].SetIsEnemyOf(player, false);

        mission.ForceNoFriendlyFire = true;
        var mode = controller.IsPlayerPracticing ? "practice" : "yard";
        Outcomes.Pass(ModeCheckId, mode, $"mode={mode} teams={count} allies={TeamPracticeRules.AllyTeamCount}");
    }

    /// <summary>Outlines a freshly spawned fighter of an allied team in blue, while the player practises.</summary>
    internal static void OutlineAlly(ArenaPracticeFightMissionController controller, Team team, Agent? agent, IModLogger? logger)
    {
        if (agent == null || !controller.IsPlayerPracticing)
            return;
        var teams = TeamPracticeAccess.AiTeams(controller);
        if (teams == null || !TeamPracticeRules.ShouldOutline(true, teams.IndexOf(team), teams.Count))
            return;

        var visuals = agent.AgentVisuals;
        if (visuals == null || !visuals.IsValid())
            return;
        MissionThreadGuard.NoteCall("TeamPractice.OutlineAlly", message => logger?.LogError(message));
        visuals.SetContourColor(TeamPracticeRules.AllyOutline, true);
    }

    /// <summary>
    /// Ends the fight in the player's favour when every fighter has entered and no team of the other side has one left.
    /// It writes 0 into vanilla's alive count before vanilla's own tick reads it, so vanilla's victory path runs unchanged.
    /// Until the player is back at the master it also puts the count back at 0 when an ally's fall took it below
    /// (<see cref="TeamPracticeRules.WinCountSlipped"/>).
    /// </summary>
    internal static void TryDeclareWin(ArenaPracticeFightMissionController controller, IModLogger? logger)
    {
        if (!controller.IsPlayerPracticing || !TeamPracticeAccess.IsAvailable)
            return;

        ref var alive = ref TeamPracticeAccess.AliveCount(controller);
        var unspawned = controller.RemainingOpponentCount - alive;
        if (controller.AfterPractice)
        {
            if (TeamPracticeRules.WinCountSlipped(controller.IsPlayerSurvived, unspawned, alive))
            {
                logger?.LogInfo($"[TeamPractice] an ally fell after the team win: the count is put back at 0 (it was {alive})");
                alive = 0;
            }

            return;
        }

        if (unspawned > 0)
            return;

        var main = controller.Mission?.MainAgent;
        var standing = main != null && main.IsActive();
        if (!TeamPracticeRules.IsTeamWin(standing, unspawned, alive, TeamPracticeRules.AnyOpponentStanding(TeamsHaveBots(TeamPracticeAccess.AiTeams(controller)))))
            return;

        var allies = alive;
        alive = 0;
        Outcomes.Pass(WinCheckId, "player-side", $"alliesStanding={allies}");
    }

    /// <summary>
    /// The number the practice HUD shows as "remaining opponents" (<see cref="TeamPracticeRules.OpponentsLeft"/>), asked by the
    /// client patch on <c>MissionArenaPracticeFightVM.Tick</c> (#1536). False, with <paramref name="count"/> 0, leaves the HUD
    /// vanilla's: the player is not practising (the yard before a fight, and a wave fight, where the panel is hidden), the
    /// controller was not decided as a team fight, or the AI teams did not resolve. The allies standing are the
    /// living fighters of the teams <see cref="TeamPracticeRules.IsAllyTeam"/> accepts; <c>Team.ActiveAgents</c> loses an agent
    /// in <c>Mission.OnAgentRemoved</c> before the behaviours run, where vanilla lowers its alive count. Main mission thread.
    /// </summary>
    internal static bool TryOpponentsLeft(ArenaPracticeFightMissionController controller, out int count)
    {
        count = 0;
        if (!controller.IsPlayerPracticing || !TeamPracticeState.IsTeams(controller))
            return false;
        var teams = TeamPracticeAccess.AiTeams(controller);
        if (teams == null)
            return false;

        var alliesStanding = 0;
        for (var i = 0; i < teams.Count; i++)
            if (TeamPracticeRules.IsAllyTeam(i, teams.Count))
                alliesStanding += teams[i].ActiveAgents.Count;

        count = TeamPracticeRules.OpponentsLeft(controller.RemainingOpponentCount, alliesStanding);
        return true;
    }

    /// <summary>One has-bots flag for each AI team by index; no teams read (null) is an empty list, which the rules treat as no data.</summary>
    private static List<bool> TeamsHaveBots(List<Team>? teams)
    {
        var flags = new List<bool>();
        if (teams != null)
            foreach (var team in teams)
                flags.Add(team.HasBots);
        return flags;
    }
}
