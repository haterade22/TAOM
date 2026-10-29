using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using TAOM.Features.StagedSiege.Spike;
using TaleWorlds.Library;
using TaleWorlds.MountAndBlade;

namespace TAOM.Features.DevConsole.Cheats;

/// <summary>
/// `taom.ss_*`: the Phase 0 spike of Staged Siege (#1054). Dev tools only; every command needs cheat mode and a siege mission
/// (status excepted) and returns a sentence instead of throwing.
/// </summary>
public static class StagedSiegeSpikeCheats
{
    private const string Usage =
        "Staged Siege spike (#1054): taom.ss_flag [A-E] | ss_flag_move up|down|none | ss_flag_color attacker|defender | "
        + "ss_record <A-D> point|attacker|defender|detachment [radius] | ss_flags_from_records | ss_open_gates | ss_finish | ss_status";

    [CommandLineFunctionality.CommandLineArgumentFunction("ss_flag", "taom")]
    public static string SpawnFlag(List<string> strings) =>
        TaomConsole.RunInMission(strings, Usage, args =>
        {
            var letter = 'A';
            if (args.Count > 0 && !StagedSiegeSpikeFlags.IsStageLetter(args[0], 'E', out letter)) return "Stage must be A to E.";
            var refusal = StagedSiegeSpikeContext.TryGet(true, true, true, out var c);
            if (refusal != null) return refusal;
            var look = c.Main.LookDirection.AsVec2;
            var created = StagedSiegeSpikeFlags.Spawn(c.Mission, letter, c.Main.Position, look, out var flag);
            if (created == null) return "CreateMissionObjectFromPrefab returned null (a client peer creates nothing).";
            if (flag == null) return $"Created a {created.GetType().Name}, which is not a FlagCapturePoint.";
            c.Behavior.AddFlag(flag);
            return $"Created FlagCapturePoint {letter} at {flag.Position}. Spawned flags: {c.Behavior.Flags.Count}.";
        });

    [CommandLineFunctionality.CommandLineArgumentFunction("ss_flag_move", "taom")]
    public static string MoveFlags(List<string> strings) =>
        TaomConsole.RunInMission(strings, Usage, args =>
        {
            var word = args.Count > 0 ? args[0].ToLowerInvariant() : "";
            if (word != "up" && word != "down" && word != "none") return "Say up, down or none.";
            var refusal = StagedSiegeSpikeContext.TryGet(true, false, true, out var c);
            if (refusal != null) return refusal;
            if (c.Behavior.Flags.Count == 0) return "No spawned flags yet (taom.ss_flag).";
            if (word == "none") c.Behavior.StopFlags();
            else c.Behavior.MoveFlags(word == "up" ? CaptureTheFlagFlagDirection.Up : CaptureTheFlagFlagDirection.Down);
            return $"{c.Behavior.Flags.Count} flag(s) set to {word}; progress is logged once a second to taom_debug.";
        });

    [CommandLineFunctionality.CommandLineArgumentFunction("ss_flag_color", "taom")]
    public static string ColorFlags(List<string> strings) =>
        TaomConsole.RunInMission(strings, Usage, args =>
        {
            var word = args.Count > 0 ? args[0].ToLowerInvariant() : "";
            if (word != "attacker" && word != "defender") return "Say attacker or defender.";
            var refusal = StagedSiegeSpikeContext.TryGet(true, false, true, out var c);
            if (refusal != null) return refusal;
            var team = word == "attacker" ? c.Mission.AttackerTeam : c.Mission.DefenderTeam;
            if (team == null) return $"The {word} team does not exist.";
            foreach (var flag in c.Behavior.Flags) flag.SetTeamColorsWithAllSynched(team.Color, team.Color2);
            return $"{c.Behavior.Flags.Count} flag(s) coloured as the {word}.";
        });

    [CommandLineFunctionality.CommandLineArgumentFunction("ss_record", "taom")]
    public static string Record(List<string> strings) =>
        TaomConsole.RunInMission(strings, Usage, args =>
        {
            if (args.Count < 2 || !StagedSiegeSpikeFlags.IsStageLetter(args[0], 'D', out var stage))
                return "Expected a stage A to D and a kind.\n" + Usage;
            var kind = args[1].ToLowerInvariant();
            if (kind != "point" && kind != "attacker" && kind != "defender" && kind != "detachment")
                return "Kind must be point, attacker, defender or detachment.";
            var radius = kind == "point" ? 8f : 0f;
            if (args.Count > 2 && !float.TryParse(args[2], NumberStyles.Float, CultureInfo.InvariantCulture, out radius))
                return "Radius must be a number.";
            var refusal = StagedSiegeSpikeContext.TryGet(true, true, false, out var c);
            if (refusal != null) return refusal;
            var look = c.Main.LookDirection.AsVec2;
            if (look.LengthSquared > 0.0001f) look.Normalize();
            var line = StagedSiegeSpikeRecorder.Append(c.Mission.SceneName, stage, kind, c.Main.Position, look, radius);
            return $"Appended to {StagedSiegeSpikeRecorder.FilePath}\n{line}";
        });

    [CommandLineFunctionality.CommandLineArgumentFunction("ss_flags_from_records", "taom")]
    public static string FlagsFromRecords(List<string> strings) =>
        TaomConsole.RunInMission(strings, Usage, args =>
        {
            var refusal = StagedSiegeSpikeContext.TryGet(true, false, true, out var c);
            if (refusal != null) return refusal;
            var points = StagedSiegeSpikeRecorder.LatestPoints(c.Mission.SceneName, out var bad);
            var made = 0;
            foreach (var record in points.Values.OrderBy(r => r.Stage))
            {
                StagedSiegeSpikeFlags.Spawn(c.Mission, record.Stage, record.Position, record.Facing, out var flag);
                if (flag == null) continue;
                c.Behavior.AddFlag(flag);
                made++;
            }
            return $"Spawned {made} flag(s) of {points.Count} recorded point(s) for scene {c.Mission.SceneName} "
                + $"({bad} unreadable line(s)) from {StagedSiegeSpikeRecorder.FilePath}.";
        });

    [CommandLineFunctionality.CommandLineArgumentFunction("ss_open_gates", "taom")]
    public static string OpenGates(List<string> strings) =>
        TaomConsole.RunInMission(strings, Usage, args =>
        {
            var refusal = StagedSiegeSpikeContext.TryGet(true, false, false, out var c);
            if (refusal != null) return refusal;
            var sb = new StringBuilder();
            foreach (var gate in c.Mission.ActiveMissionObjects.FindAllWithType<CastleGate>().ToList())
            {
                gate.OpenDoor();
                sb.AppendLine($"{StagedSiegeSpikeStatus.GateTag(gate)}: state {gate.State}, destroyed {gate.IsDestroyed}");
            }
            return sb.Length == 0 ? "This scene has no CastleGate." : sb.ToString().TrimEnd();
        });

    [CommandLineFunctionality.CommandLineArgumentFunction("ss_finish", "taom")]
    public static string Finish(List<string> strings) =>
        TaomConsole.RunInMission(strings, Usage, args =>
        {
            var refusal = StagedSiegeSpikeContext.TryGet(true, false, true, out var c);
            if (refusal != null) return refusal;
            c.Behavior.RequestFinish();
            return "Finish requested; both spawners stop and the mission returns " + StagedSiegeSpikeFinish.Describe(c.Mission) + ".";
        });

    [CommandLineFunctionality.CommandLineArgumentFunction("ss_status", "taom")]
    public static string Status(List<string> strings) =>
        TaomConsole.RunInMission(strings, Usage, args =>
        {
            var refusal = StagedSiegeSpikeContext.TryGet(false, false, false, out var c);
            return refusal ?? StagedSiegeSpikeStatus.Build(c);
        });
}
