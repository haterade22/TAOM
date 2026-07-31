using System.Collections.Generic;
using System.Linq;
using System.Text;
using TaleWorlds.CampaignSystem;
using TaleWorlds.Library;
using TAOM.Core.Domain;

namespace TAOM.Features.DevConsole.Cheats;

/// <summary>
/// Process-level diagnostics: the Harmony patch table and the race registry. Neither needs a campaign
/// or a mission to be meaningful, so both use the cheat-mode-only gate.
/// </summary>
public static class DiagnosticCheats
{
    private const string TaomHarmonyId = "com.taom.mod";

    private const string PatchesUsage =
        "Format is \"taom.print_patches [filter]\".\n"
        + "Lists every [HarmonyPatchCategory] TAOM declares and whether Harmony actually applied it.\n"
        + "Optional filter is a case-insensitive substring match on the category name.\n"
        + "Some categories are deliberately never registered — NOT APPLIED is not automatically a fault.";

    private const string RacesUsage =
        "Format is \"taom.print_races\".\n"
        + "Lists the race registry in FaceGen index order, and the main hero's current race.";

    /// <summary>
    /// Answers "did Patch63 actually apply?" — currently only answerable by grepping taom_debug, and
    /// the first question worth asking when a feature silently does nothing after an engine bump.
    /// </summary>
    [CommandLineFunctionality.CommandLineArgumentFunction("print_patches", "taom")]
    public static string PrintPatches(List<string> strings) =>
        TaomConsole.RunAnywhere(strings, PatchesUsage, args =>
        {
            var filter = args.Count > 0 ? args[0] : null;
            var result = HarmonyPatchInspector.Collect(TaomHarmonyId, typeof(TaomConsole).Assembly);

            return PatchReportFormatter.Format(
                result.Declared, result.AppliedCounts, result.Uncategorized,
                result.OtherOwners, result.TotalPatchedMethods, filter);
        });

    [CommandLineFunctionality.CommandLineArgumentFunction("print_races", "taom")]
    public static string PrintRaces(List<string> strings) =>
        TaomConsole.RunAnywhere(strings, RacesUsage, args =>
        {
            var races = IoC.Resolve<IRaceManager>();
            var ordered = races.GetOrderedRaceNames();
            if (ordered == null || ordered.Count == 0) return "The race registry is empty.";

            var sb = new StringBuilder();
            sb.AppendLine($"[Races] {ordered.Count} races (index == FaceGen race id):");
            for (var i = 0; i < ordered.Count; i++)
                sb.AppendLine($"[Races]   {i} = {ordered[i]}");

            sb.Append($"[Races] main hero: {DescribeHeroRace(races)}");
            return sb.ToString();
        });

    // Validate-before-lookup: GetRaceNameFromId coerces an unknown id to "human" with only a warning,
    // so a diagnostic that called it blind would confidently report the wrong race — the worst
    // possible failure mode for a command whose only job is to tell you the truth.
    private static string DescribeHeroRace(IRaceManager races)
    {
        var hero = Hero.MainHero;
        if (hero?.CharacterObject == null) return "(no main hero)";

        var raceId = hero.CharacterObject.Race;
        return races.IsValidRaceId(raceId)
            ? $"{races.GetRaceNameFromId(raceId)} (id {raceId})"
            : $"INVALID race id {raceId} — not in the registry";
    }
}
