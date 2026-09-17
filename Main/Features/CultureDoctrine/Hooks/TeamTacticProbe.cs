using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using HarmonyLib;
using TAOM.Features.CultureDoctrine.Hooks.Behaviors;
using TAOM.Features.CultureDoctrine.Hooks.Tactics;
using TaleWorlds.MountAndBlade;

namespace TAOM.Features.CultureDoctrine.Hooks;

/// <summary>
/// The status line: which tactic a team is running and what each formation is doing. Reads
/// <c>TeamAIComponent._currentTactic</c>, the feature's one reflection read, because the engine
/// has no public getter for it (only <c>IsCurrentTactic(TacticComponent)</c>, which needs the
/// instance in hand). From <c>OnMissionTick</c> the reads never overlap the AI thread
/// (`Mission.OnPreTick` waits for the previous async tick); from the console command they can,
/// and every read is a reference, an int or a 4-byte enum, so the worst case is one stale line,
/// never a crash. The A/B protocol in <c>docs/features/culture-doctrine.md</c> parses this format.
/// </summary>
public static class TeamTacticProbe
{
    // Bound once, fail-soft: a missing field on a future engine makes the status line say
    // "unreadable" instead of throwing a TypeInitializationException out of the first read.
    private static readonly AccessTools.FieldRef<TeamAIComponent, TacticComponent>? CurrentTactic = Bind();

    private static AccessTools.FieldRef<TeamAIComponent, TacticComponent>? Bind()
    {
        try { return AccessTools.FieldRefAccess<TeamAIComponent, TacticComponent>("_currentTactic"); }
        catch { return null; }
    }

    public static string CurrentTacticName(Team team)
    {
        var ai = team.TeamAI;
        if (ai == null)
            return "no-team-ai";
        if (CurrentTactic == null)
            return "unreadable";
        var tactic = CurrentTactic(ai);
        return tactic == null ? "none" : tactic.GetType().Name;
    }

    public static string Describe(Team team, float missionTime) => Describe(team, missionTime, null);

    /// <summary><paramref name="taomTactics"/>: the TAOM tactics the mission logic registered, so
    /// the line also shows each one's phase or failure; null when the caller has none in hand.</summary>
    public static string Describe(Team team, float missionTime, IReadOnlyList<TaomTacticBase>? taomTactics)
    {
        var sb = new StringBuilder(256);
        sb.Append("[Doctrine] t=+").Append(missionTime.ToString("0", CultureInfo.InvariantCulture))
          .Append("s team=").Append(team.TeamIndex)
          .Append(" side=").Append(team.Side)
          .Append(" player=").Append(team.IsPlayerTeam ? "yes" : "no")
          .Append(" tactic=").Append(CurrentTacticName(team))
          .Append(" formations=[");
        var first = true;
        var formations = team.FormationsIncludingEmpty;
        for (var i = 0; i < formations.Count; i++)
        {
            var formation = formations[i];
            if (formation.CountOfUnits <= 0)
                continue;
            if (!first)
                sb.Append(", ");
            first = false;
            var active = formation.AI?.ActiveBehavior;
            sb.Append(formation.FormationIndex).Append(':').Append(formation.CountOfUnits)
              .Append(' ').Append(active?.GetType().Name ?? "none");
            // A TAOM behaviour also shows its stage or stance (Marching, Square, Reforming, failed: ...).
            if (active is TaomBehaviorBase taom && taom.Status.Length > 0)
                sb.Append(':').Append(taom.Status);
            sb.Append('/').Append(formation.ArrangementOrder.OrderEnum)
              .Append('/').Append(formation.FiringOrder.OrderEnum)
              .Append(formation.IsAIControlled ? "" : "(player)");
        }
        sb.Append(']');
        if (taomTactics != null)
        {
            var any = false;
            for (var i = 0; i < taomTactics.Count; i++)
            {
                var tactic = taomTactics[i];
                if (tactic.Team != team)
                    continue;
                sb.Append(any ? ", " : " taom=[").Append(tactic.GetType().Name).Append(':').Append(tactic.Status);
                any = true;
            }
            if (any)
                sb.Append(']');
        }
        return sb.ToString();
    }

    public static string DescribeAll(Mission mission) => DescribeAll(mission, null);

    public static string DescribeAll(Mission mission, IReadOnlyList<TaomTacticBase>? taomTactics)
    {
        var sb = new StringBuilder();
        var teams = mission.Teams;
        for (var i = 0; i < teams.Count; i++)
        {
            if (!teams[i].HasTeamAi)
                continue;
            if (sb.Length > 0)
                sb.Append(Environment.NewLine);
            sb.Append(Describe(teams[i], mission.CurrentTime, taomTactics));
        }
        return sb.Length == 0 ? "No team has a team AI in this mission." : sb.ToString();
    }
}
