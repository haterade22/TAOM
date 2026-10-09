using System.Collections.Generic;
using System.Linq;
using TaleWorlds.Library;
using TAOM.Adapters;
using TAOM.Features.CoopInterop;
using TAOM.Features.DevConsole;
using TAOM.Features.WarChronicle.Effects;
using TAOM.Features.WarChronicle.Ledger;
using TAOM.Features.WarChronicle.Rally;

namespace TAOM.Features.WarChronicle.Cheats;

/// <summary>
/// The War Chronicle's console commands (docs/features/dev-console.md), thin over the pure
/// <see cref="WarChronicleCheatReport"/>. They exist because the effects and the ledger are invisible
/// in game: nothing else shows what the registry holds or what today's ledger would say.
/// </summary>
public static class WarChronicleCheats
{
    private const string EffectsUsage =
        "Format is \"taom.war_effects\".\n"
        + "Prints every active timed war effect and each affected kingdom's volunteer and escape multiplier.";

    private const string RallyUsage =
        "Format is \"taom.rally_status\".\n"
        + "Prints, for every kingdom, its fortification points, baseline, share lost, rally tier (0 to 2) and whether it is eligible "
        + "(AI-ruled, at war, not excluded as Neutral). Read-only; the tier is the one the last daily tick left.";

    private const string LedgerUsage =
        "Format is \"taom.print_war_ledger\".\n"
        + "Prints the [WarLedger] lines the daily tick would write today. Read-only: nothing is logged or changed.";

    [CommandLineFunctionality.CommandLineArgumentFunction("war_effects", "taom")]
    public static string WarEffects(List<string> strings) =>
        TaomConsole.RunInCampaign(strings, EffectsUsage, _ =>
        {
            var effects = IoC.Resolve<IWarEffectService>();
            var nowHours = IoC.Resolve<IKingdomWarSnapshotAdapter>().GetNowHours();
            return WarChronicleCheatReport.FormatEffects(effects.Snapshot(), nowHours, effects.GetMultiplier);
        });

    [CommandLineFunctionality.CommandLineArgumentFunction("war_effect_add", "taom")]
    public static string WarEffectAdd(List<string> strings) =>
        TaomConsole.RunInCampaign(strings, WarChronicleCheatReport.AddUsage, args =>
        {
            // A save-backed write: a co-op client must not diverge the host's registry.
            if (!CoopSessionPolicy.MayWriteSaveBackedState(IoC.Resolve<ICoopSessionProvider>().IsCoopClient))
                return "Refused: a co-op client does not write the host's war effects.";

            var snapshots = IoC.Resolve<IKingdomWarSnapshotAdapter>();
            var known = snapshots.GetKingdoms().Select(k => k.Id).ToList();
            if (!WarChronicleCheatReport.TryBuildEffect(args, snapshots.GetNowHours(), known, out var effect, out var error))
                return error;

            var registry = IoC.Resolve<IWarEffectService>();
            var before = registry.GetMultiplier(effect!.KingdomId, effect.Kind);
            registry.Apply(effect);
            var after = registry.GetMultiplier(effect.KingdomId, effect.Kind);
            return WarChronicleCheatReport.FormatAdded(effect, before, after);
        });

    [CommandLineFunctionality.CommandLineArgumentFunction("rally_status", "taom")]
    public static string RallyStatus(List<string> strings) =>
        TaomConsole.RunInCampaign(strings, RallyUsage, _ =>
        {
            var kingdoms = IoC.Resolve<IKingdomWarSnapshotAdapter>().GetKingdoms();
            var rally = IoC.Resolve<RallyService>();
            return WarChronicleCheatReport.FormatRally(rally.Report(kingdoms), rally.IsActive);
        });

    [CommandLineFunctionality.CommandLineArgumentFunction("print_war_ledger", "taom")]
    public static string PrintWarLedger(List<string> strings) =>
        TaomConsole.RunInCampaign(strings, LedgerUsage, _ =>
        {
            var kingdoms = IoC.Resolve<IKingdomWarSnapshotAdapter>().GetKingdoms();
            var lines = IoC.Resolve<WarLedgerService>().BuildDailyLines(kingdoms);
            return lines.Count == 0 ? "[WarLedger] No kingdoms to report." : string.Join("\n", lines);
        });
}
