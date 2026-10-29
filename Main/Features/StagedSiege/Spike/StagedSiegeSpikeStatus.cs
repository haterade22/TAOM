using System;
using System.Linq;
using System.Text;
using TaleWorlds.Core;
using TaleWorlds.MountAndBlade;

namespace TAOM.Features.StagedSiege.Spike;

/// <summary>The text of <c>taom.ss_status</c>.</summary>
internal static class StagedSiegeSpikeStatus
{
    internal static string Build(StagedSiegeSpikeContext c)
    {
        var m = c.Mission;
        var sb = new StringBuilder();
        sb.AppendLine($"Scene: {m.SceneName}; IsSiegeBattle: {m.IsSiegeBattle}; player side: {m.PlayerTeam?.Side.ToString() ?? "none"}");
        foreach (var team in new[] { m.AttackerTeam, m.DefenderTeam })
            sb.AppendLine(team == null
                ? "Team: missing"
                : $"{team.Side}: {team.ActiveAgents.Count} active agents; TeamAI {team.TeamAI?.GetType().Name ?? "none"}");
        AppendSpawn(sb, m.GetMissionBehavior<IMissionAgentSpawnLogic>());
        foreach (var gate in m.ActiveMissionObjects.FindAllWithType<CastleGate>())
            sb.AppendLine($"Gate {GateTag(gate)}: state {gate.State}, destroyed {gate.IsDestroyed}");
        if (c.Behavior == null) sb.Append("Spike behavior: not attached.");
        else
        {
            sb.Append($"Spike flags: {c.Behavior.Flags.Count}; finish requested: {c.Behavior.FinishRequested}");
            foreach (var flag in c.Behavior.Flags) sb.Append($"\n  {flag.Position} progress {flag.GetFlagProgress():F3}");
        }
        return sb.ToString();
    }

    internal static string GateTag(CastleGate gate) =>
        gate.GameEntity.HasTag(CastleGate.OuterGateTag) ? CastleGate.OuterGateTag
        : gate.GameEntity.HasTag(CastleGate.InnerGateTag) ? CastleGate.InnerGateTag : "untagged";

    // GetAllTroopsForSide is the interface's only count; whether it means "remaining" or "all" is UNVERIFIED.
    private static void AppendSpawn(StringBuilder sb, IMissionAgentSpawnLogic spawn)
    {
        if (spawn == null) { sb.AppendLine("Spawn logic: none."); return; }
        foreach (var side in new[] { BattleSideEnum.Attacker, BattleSideEnum.Defender })
            sb.AppendLine($"Spawn {side}: enabled {spawn.IsSideSpawnEnabled(side)}, depleted {spawn.IsSideDepleted(side)}, "
                + $"troops listed {spawn.GetAllTroopsForSide(side).Count()}");
    }
}
