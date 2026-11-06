using System.Collections.Generic;
using TaleWorlds.Library;
using TaleWorlds.MountAndBlade;
using TAOM.Features.LongDefeat;

namespace TAOM.Features.DevConsole.Cheats;

/// <summary>
/// <c>taom.ld_start &lt;fortress&gt; [waves]</c> starts The Long Defeat (#1079) from the main menu through the prompt-free path
/// the autorun uses; <c>taom.ld_record</c> and <c>taom.ld_export</c> are its recorder, in the Staged Siege shape. It only routes to <see cref="LongDefeatCommands"/>; the services come from the container because a static
/// console method has no constructor to inject through (docs/features/dev-console.md).
/// </summary>
public static class LongDefeatCheats
{
    [CommandLineFunctionality.CommandLineArgumentFunction("ld_start", "taom")]
    public static string LdStart(List<string> strings) =>
        TaomConsole.RunAtMainMenu(strings, LongDefeatCommands.StartUsageHead, args =>
            LongDefeatCommands.Start(args, IoC.Resolve<LongDefeatConfigCatalog>(), IoC.Resolve<LongDefeatSetupPrompt>()));

    [CommandLineFunctionality.CommandLineArgumentFunction("ld_record", "taom")]
    public static string LdRecord(List<string> strings) =>
        TaomConsole.RunInMission(strings, LongDefeatCommands.RecordUsage, args =>
        {
            // A clear reads only the scene, so it works with no soldier to control (as ld_export does).
            var clearing = LongDefeatCommands.IsClear(args);
            var refusal = TryGet(needMain: !clearing, out var mission);
            if (refusal != null)
                return refusal;
            if (clearing)
                return LongDefeatCommands.Record(args, LongDefeatRecorder.Default, new LdSpot(mission!.SceneName, 0f, 0f, 0f, 0f, 1f));
            var look = mission!.MainAgent.LookDirection.AsVec2;
            var position = mission.MainAgent.Position;
            return LongDefeatCommands.Record(args, LongDefeatRecorder.Default,
                new LdSpot(mission.SceneName, position.x, position.y, position.z, look.x, look.y));
        });

    [CommandLineFunctionality.CommandLineArgumentFunction("ld_export", "taom")]
    public static string LdExport(List<string> strings) =>
        TaomConsole.RunInMission(strings, LongDefeatCommands.RecordUsage, args =>
        {
            var refusal = TryGet(needMain: false, out var mission);
            return refusal ?? LongDefeatCommands.Export(LongDefeatRecorder.Default, mission!.SceneName);
        });

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
