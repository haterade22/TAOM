using System.Linq;
using System.Text;
using TAOM.Features.DevConsole.Domain;

namespace TAOM.Features.DevConsole;

/// <summary>
/// Pure renderers for the mission-side console reports. Every input is an engine-free snapshot, so
/// each branch — including the ones a live mission would make hard to reach — is unit-testable.
/// </summary>
internal static class MissionReportFormatter
{
    private const string Unknown = "?";

    internal static string FormatAgent(AgentSnapshot a)
    {
        if (a == null) return "No agent.";

        var sb = new StringBuilder();
        sb.AppendLine(
            $"[Agent] #{a.Index} {Or(a.Name)} ({Or(a.CharacterId)}) kind={DescribeKind(a)} "
            + $"team={Or(a.TeamLabel)} formation={Or(a.FormationLabel)} hp={Hp(a.Health)}/{Hp(a.MaxHealth)}");

        // A null RaceName means the boundary found the id was NOT in the registry. It deliberately did
        // not call GetRaceNameFromId, which coerces unknown ids to "human" — a diagnostic printing a
        // confident wrong race is worse than one that says it does not know.
        var race = a.RaceName == null ? $"INVALID (id {a.RaceId} not in the race registry)" : $"{a.RaceName} (id {a.RaceId})";
        sb.AppendLine($"[Agent] race={race} monster={Or(a.MonsterId)}");
        sb.AppendLine($"[Agent] actionSet={Or(a.ActionSetName)} skeleton={Or(a.SkeletonName)}");

        if (!string.IsNullOrEmpty(a.MountMonsterId))
            sb.AppendLine($"[Agent] mount={a.MountMonsterId}");
        if (!string.IsNullOrEmpty(a.RiderName))
            sb.AppendLine($"[Agent] rider={a.RiderName}");

        if (a.EquipmentSlots == null || a.EquipmentSlots.Count == 0)
        {
            sb.Append("[Agent] no equipment slots populated");
        }
        else
        {
            // "at spawn" is load-bearing: this is SpawnEquipment, what the agent was built with, not
            // what it is currently wielding after a weapon switch or a dropped shield.
            sb.AppendLine("[Agent] equipment (at spawn):");
            foreach (var slot in a.EquipmentSlots)
                sb.AppendLine($"[Agent]   {slot}");
        }

        return sb.ToString().TrimEnd();
    }

    internal static string FormatSpawn(SpawnOutcome outcome)
    {
        if (outcome == null) return "Spawn failed.";

        // A hard failure reports the reason and nothing else — claiming "0/5 spawned" alongside it
        // would imply the spawn was attempted per-troop when it never got that far.
        if (!string.IsNullOrEmpty(outcome.FailureReason))
            return $"[Spawn] {outcome.FailureReason}";

        var line = $"[Spawn] {outcome.Spawned}/{outcome.Requested} {outcome.TroopId} spawned onto {outcome.TeamLabel}";
        return outcome.Spawned == outcome.Requested
            ? line
            : line + $" — {outcome.Requested - outcome.Spawned} failed, see taom_debug for the exceptions";
    }

    internal static string FormatBattleScene(BattleSceneQuery q)
    {
        if (q == null) return "No map position.";

        var candidates = q.CandidateSceneIds ?? new string[0];
        var sb = new StringBuilder();
        sb.AppendLine($"[BattleScene] party at ({q.X:0.##}, {q.Y:0.##}) — map patch sceneIndex {q.MapIndex}");

        if (candidates.Count == 0)
        {
            // The reason this command exists. Vanilla renames and removes battle scenes between
            // versions, and a TAOM map position pointing at an index no scene claims produces a
            // fallback or an assert at battle start rather than an error anyone would notice.
            sb.Append(
                $"[BattleScene] NO battle scene declares map index {q.MapIndex} — a battle here will fall back "
                + "or assert. Check sp_battle_scenes.xml against the installed vanilla data.");
        }
        else
        {
            sb.AppendLine($"[BattleScene] {candidates.Count} candidate scene(s):");
            foreach (var id in candidates.OrderBy(c => c, System.StringComparer.Ordinal))
                sb.AppendLine($"[BattleScene]   {id}");
        }

        return sb.ToString().TrimEnd();
    }

    /// <summary>
    /// The trailing caveat is not decoration. The command's name invites the assumption that it
    /// exercises TAOM's damage models; it does not — a synthetic blow goes straight to
    /// <c>HandleBlow</c>, downstream of where <c>DecideAgentShrugOffBlow</c> runs. Saying so in the
    /// output is what stops the next session re-deriving the wrong purpose from the name.
    /// </summary>
    internal static string FormatDamage(string agentLabel, float amount, float? before, float? after)
    {
        var delta = before.HasValue && after.HasValue
            ? $"{before.Value:0.#} -> {after.Value:0.#}"
            : "health unreadable";

        return $"[Damage] {agentLabel}: {amount:0.#} applied ({delta})\n"
             + "[Damage] NOTE: synthetic blow — shrug-off / unstoppable / knockdown models did NOT run.";
    }

    internal static string FormatMissionScene(string sceneName, float x, float y, float z, bool fromMainAgent)
    {
        var source = fromMainAgent ? "player" : "camera (no live main agent)";
        return $"[Mission] scene={Or(sceneName)}\n"
             + $"[Mission] {source} position=({x:0.##}, {y:0.##}, {z:0.##})";
    }

    private static string Or(string value) => string.IsNullOrEmpty(value) ? Unknown : value;

    // Never renders a failed read as 0 — see AgentSnapshot.Health.
    private static string Hp(float? value) => value.HasValue ? value.Value.ToString("0.#") : Unknown;

    private static string DescribeKind(AgentSnapshot a)
    {
        if (a.IsMount) return "mount";
        return a.IsHuman ? "human" : "non-human";
    }
}
