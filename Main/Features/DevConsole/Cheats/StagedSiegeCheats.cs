using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using TAOM.Features.StagedSiege;
using TAOM.Features.StagedSiege.Domain;
using TAOM.Features.StagedSiege.Hooks;
using TaleWorlds.Library;
using TaleWorlds.MountAndBlade;

namespace TAOM.Features.DevConsole.Cheats;

/// <summary>
/// `taom.staged_siege_*`: the recorder and the dev tools of Staged Siege (#1054, docs/features/staged-siege.md). Standing at a
/// spot in a siege mission, `staged_siege_record` appends its position and facing to the recorder file and `staged_siege_export`
/// folds the file into the shipped JSON shape. Every command needs cheat mode and a siege mission and returns a sentence instead
/// of throwing. A command that would write engine state (<c>staged_siege_stage</c>, <c>staged_siege_open_gates</c>,
/// <c>staged_siege_show</c>) only ENQUEUES: a console command can run while the async AI tick is in flight, so the mission
/// behavior applies the request on the main thread at the start of its next tick and logs the result. Record, status and export
/// only read or write files.
/// </summary>
public static class StagedSiegeCheats
{
    private const string Queued = "Queued; it applies at the start of the next mission tick and the result goes to the log.";

    private const string Usage =
        "Staged Siege (#1054): taom.staged_siege_record <A-D> point|defender|detachment [radius] | staged_siege_export | "
        + "staged_siege_show | staged_siege_open_gates | staged_siege_stage <A-D|end> | staged_siege_status";

    [CommandLineFunctionality.CommandLineArgumentFunction("staged_siege_record", "taom")]
    public static string Record(List<string> strings) =>
        TaomConsole.RunInMission(strings, Usage, args =>
        {
            if (args.Count < 2 || !TryLetter(args[0], out var stage))
                return "Expected a stage A to D and a kind.\n" + Usage;
            var kind = args[1].ToLowerInvariant();
            if (kind != "point" && kind != "defender" && kind != "detachment")
                return "Kind must be point, defender or detachment.";
            if (kind == "defender" && stage == 'A')
                return "A defender frame is recorded for B, C or D; stage A has the detachment.";
            if (kind == "detachment" && stage != 'A')
                return "The detachment frame is recorded for stage A only.";
            // A radius is written only when one is typed: a point without its own radius follows the captureRadius tunable.
            float? radius = null;
            if (kind == "point" && args.Count > 2)
            {
                var radiusRefusal = StagedSiegeRecorder.ParseRadius(args[2], out radius);
                if (radiusRefusal != null)
                    return radiusRefusal;
            }

            var refusal = TryGet(needMain: true, out var mission);
            if (refusal != null)
                return refusal;
            var look = mission!.MainAgent.LookDirection.AsVec2;
            var lookRefusal = StagedSiegeRecorder.NormalizeLook(look.x, look.y, out var fx, out var fy);
            if (lookRefusal != null)
            {
                // Only the defender and detachment frames read a facing; a point's is never read, so a steep look keeps it.
                if (kind != "point")
                    return lookRefusal;
                fx = 0f;
                fy = 1f;
            }
            var position = mission.MainAgent.Position;
            var line = StagedSiegeRecorder.Default.Append(mission.SceneName, stage, kind, position.x, position.y, position.z, fx, fy, radius);
            return $"Appended to {StagedSiegeRecorder.Default.FilePath}\n{line}";
        });

    [CommandLineFunctionality.CommandLineArgumentFunction("staged_siege_export", "taom")]
    public static string Export(List<string> strings) =>
        TaomConsole.RunInMission(strings, Usage, args =>
        {
            var refusal = TryGet(needMain: false, out var mission);
            if (refusal != null)
                return refusal;
            var fragment = StagedSiegeRecorder.Default.Export(mission!.SceneName, out var path);
            return fragment.IsComplete
                ? $"Wrote the complete scene block for {mission.SceneName} to {path}. Merge its entry into staged_siege.json."
                : $"Wrote {path}, but these pieces have no record yet: {string.Join(", ", fragment.Missing)}.";
        });

    [CommandLineFunctionality.CommandLineArgumentFunction("staged_siege_show", "taom")]
    public static string Show(List<string> strings) =>
        TaomConsole.RunInMission(strings, Usage, args =>
        {
            var refusal = TryGet(needMain: false, out var mission);
            if (refusal != null)
                return refusal;
            if (StagedSiegeSlot.ServiceFor(mission) is { } armed && armed.IsArmed)
                return "The armed staged siege already placed its flags.";
            var commands = CommandsOf(mission!);
            if (commands == null)
                return NotAttached;

            // The flags carry no radius of their own: the zone radius is the tunable default, and only the position shows.
            var radius = StagedSiegeTunables.Default.CaptureRadius;
            var points = StagedSiegeRecorder.Default.LatestPoints(mission!.SceneName, out var bad);
            var zones = points.Values.OrderBy(r => r.Stage)
                .Select(r => new StageZone((StageId)(r.Stage - 'A'), new StagePoint(r.X, r.Y, r.Z), radius))
                .ToList();
            commands.Enqueue(StagedSiegeCommand.ShowFlags(zones));
            return $"{Queued} {zones.Count} recorded point(s) for scene {mission.SceneName} ({bad} unreadable line(s)) "
                + $"from {StagedSiegeRecorder.Default.FilePath}.";
        });

    [CommandLineFunctionality.CommandLineArgumentFunction("staged_siege_open_gates", "taom")]
    public static string OpenGates(List<string> strings) =>
        TaomConsole.RunInMission(strings, Usage, args =>
        {
            var refusal = TryGet(needMain: false, out var mission);
            if (refusal != null)
                return refusal;
            var commands = CommandsOf(mission!);
            if (commands == null)
                return NotAttached;
            commands.Enqueue(StagedSiegeCommand.OpenGates());
            return Queued;
        });

    [CommandLineFunctionality.CommandLineArgumentFunction("staged_siege_stage", "taom")]
    public static string Stage(List<string> strings) =>
        TaomConsole.RunInMission(strings, Usage, args =>
        {
            if (args.Count < 1)
                return "Expected a stage A to D, or end.\n" + Usage;
            StageId? target = null;
            if (!string.Equals(args[0], "end", StringComparison.OrdinalIgnoreCase))
            {
                if (!TryLetter(args[0], out var letter))
                    return "Expected a stage A to D, or end.";
                target = (StageId)(letter - 'A');
            }

            var refusal = TryGet(needMain: false, out var mission);
            if (refusal != null)
                return refusal;
            var commands = CommandsOf(mission!);
            if (commands == null)
                return NotAttached;
            if (StagedSiegeSlot.ServiceFor(mission) == null)
                return "The staged siege is not armed in this mission (taom.staged_siege_status says why).";
            commands.Enqueue(StagedSiegeCommand.ForceStage(target));
            return Queued;
        });

    [CommandLineFunctionality.CommandLineArgumentFunction("staged_siege_status", "taom")]
    public static string Status(List<string> strings) =>
        TaomConsole.RunInMission(strings, Usage, args =>
        {
            var mission = Mission.Current;
            if (mission == null)
                return "No mission is running.";
            var behavior = mission.GetMissionBehavior<StagedSiegeMissionBehavior>();
            if (behavior == null)
                return "The StagedSiege mission behavior is not attached to this mission.";
            var sb = new StringBuilder();
            sb.AppendLine($"Mission: scene {mission.SceneName}, siege {mission.IsSiegeBattle}, player side {mission.PlayerTeam?.Side.ToString() ?? "none"}.");
            if (behavior.Hall != null)
                sb.Append(behavior.Hall.DescribeStatus());
            else if (behavior.Service != null)
                sb.Append(behavior.Service.DescribeStatus());
            else
                return "The StagedSiege mission behavior is not attached to this mission.";
            return sb.ToString();
        });

    private const string NotAttached = "The StagedSiege mission behavior is not attached to this mission.";

    private static StagedSiegeCommandQueue? CommandsOf(Mission mission) =>
        mission.GetMissionBehavior<StagedSiegeMissionBehavior>()?.Commands;

    private static bool TryLetter(string text, out char letter)
    {
        letter = text != null && text.Length == 1 ? char.ToUpperInvariant(text[0]) : '\0';
        return letter >= 'A' && letter <= 'D';
    }

    // The refusal sentence, or null with the mission filled.
    private static string? TryGet(bool needMain, out Mission? mission)
    {
        mission = Mission.Current;
        if (mission == null)
            return "No mission is running.";
        if (!mission.IsSiegeBattle)
            return "This mission is not a siege battle.";
        if (needMain && (mission.MainAgent == null || !mission.MainAgent.IsActive()))
            return "There is no main agent (you are not controlling a soldier).";
        return null;
    }
}
