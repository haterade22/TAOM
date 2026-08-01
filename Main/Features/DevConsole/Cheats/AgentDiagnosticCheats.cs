using System;
using System.Collections.Generic;
using System.Linq;
using TaleWorlds.Library;
using TaleWorlds.MountAndBlade;

namespace TAOM.Features.DevConsole.Cheats;

/// <summary>
/// In-mission inspection: what an agent actually IS at runtime, and where the mission is.
///
/// The pairing with <c>taom.spawn_troops</c> is the point — spawn a creature, then read back its
/// race, monster, action set and skeleton without guessing from the model. That is the loop the
/// creature-mount and equipment work needs. Boundary conversion lives in
/// <see cref="AgentSnapshotBuilder"/> so these entry points stay thin (ADR-002).
/// </summary>
public static class AgentDiagnosticCheats
{
    private const int MaxListed = 100;

    private const string AgentUsage =
        "Format is \"taom.print_agent_info [agentName]\".\n"
        + "Omit the name for your own agent; pass * to list every agent in the mission.\n"
        + "Prints race, monster, action set, skeleton, mount/rider, health and spawn equipment.";

    private const string SceneUsage =
        "Format is \"taom.print_mission_scene\".\n"
        + "Prints the current mission's scene name and your (or the camera's) position.";

    [CommandLineFunctionality.CommandLineArgumentFunction("print_agent_info", "taom")]
    public static string PrintAgentInfo(List<string> strings) =>
        TaomConsole.RunInMission(strings, AgentUsage, args =>
        {
            var mission = Mission.Current;
            var wanted = args.Count > 0 ? args[0] : null;

            if (wanted == "*") return ListAgents(mission);

            var agent = string.IsNullOrWhiteSpace(wanted)
                ? mission.MainAgent
                : mission.Agents?.FirstOrDefault(a => Matches(a, wanted));

            if (agent == null)
                return string.IsNullOrWhiteSpace(wanted)
                    ? "No main agent (you may be dead or spectating). Pass a name, or * to list agents."
                    : $"No agent matching '{wanted}'. Use * to list agents.";

            return MissionReportFormatter.FormatAgent(AgentSnapshotBuilder.Build(agent));
        });

    [CommandLineFunctionality.CommandLineArgumentFunction("print_mission_scene", "taom")]
    public static string PrintMissionScene(List<string> strings) =>
        TaomConsole.RunInMission(strings, SceneUsage, args =>
        {
            var mission = Mission.Current;
            var main = mission.MainAgent;

            Vec3 position;
            var fromMainAgent = main != null;
            if (fromMainAgent) position = main.Position;
            else
            {
                try { position = mission.GetCameraFrame().origin; }
                catch { position = Vec3.Zero; }
            }

            return MissionReportFormatter.FormatMissionScene(
                mission.SceneName, position.x, position.y, position.z, fromMainAgent);
        });

    private static bool Matches(Agent agent, string wanted)
    {
        try
        {
            return agent?.Name != null
                && agent.Name.IndexOf(wanted, StringComparison.OrdinalIgnoreCase) >= 0;
        }
        catch { return false; }
    }

    private static string ListAgents(Mission mission)
    {
        var agents = mission.Agents;
        if (agents == null || agents.Count == 0) return "No agents in this mission.";

        var lines = agents.Take(MaxListed)
            .Select(a => $"[Agent]   #{Safe(() => a.Index.ToString())} {Safe(() => a.Name)}");
        var header = agents.Count > MaxListed
            ? $"[Agent] {agents.Count} agents (showing first {MaxListed}):"
            : $"[Agent] {agents.Count} agents:";

        return header + "\n" + string.Join("\n", lines);
    }

    private static string Safe(Func<string> read)
    {
        try { return read(); } catch { return "?"; }
    }
}
