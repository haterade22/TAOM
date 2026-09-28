using System;
using System.Collections.Generic;
using System.Linq;
using TaleWorlds.CampaignSystem;
using TaleWorlds.Library;
using TAOM.Core.Domain;
using TAOM.Core.Logging;

namespace TAOM.Features.DevConsole.Cheats;

/// <summary>
/// <c>taom.print_face [hero_id]</c>: the exact <c>&lt;BodyProperties .../&gt;</c> string a lord's
/// <c>&lt;face&gt;</c> needs. v1.5.3's face editor has no copy/export action (no clipboard code in
/// <c>FaceGenVM</c>), vanilla ships no console command for it, and the in-game console text is hard
/// to copy — so every line is also written to the TAOM debug log under <c>[PrintFace]</c>, and that
/// is what a builder should paste from.
///
/// Read-only, Tier A. No argument reports <see cref="Hero.MainHero"/>; a hero string id reports that
/// hero, validated before lookup so an unknown id prints a clear message instead of resolving to a
/// wrong hero or throwing.
/// </summary>
public static class FaceReportCheats
{
    private const string Usage =
        "Format is \"taom.print_face [hero_id]\".\n"
        + "With no argument, reports the player hero (Hero.MainHero). With a hero string id, reports\n"
        + "that hero instead. Prints name, string id, race, IsFemale, Age and the exact\n"
        + "<BodyProperties .../> string — the face editor has no export, so copy that line from the\n"
        + "TAOM debug log (tagged [PrintFace]) into a lord's <face>.";

    [CommandLineFunctionality.CommandLineArgumentFunction("print_face", "taom")]
    public static string PrintFace(List<string> strings) =>
        TaomConsole.RunInCampaign(strings, Usage, args =>
        {
            var requestedId = args.Count > 0 ? args[0] : null;
            var snapshot = Gather(requestedId);
            var lines = FaceReportFormatter.Render(snapshot);

            var logger = IoC.Resolve<IModLogger>();
            foreach (var line in lines)
                logger.LogInfo(line);

            return string.Join("\n", lines);
        });

    // Wrapped as a whole: BodyProperties, CharacterObject.Race and Age are all computed TaleWorlds
    // getters that can throw before a null check applies (.claude/rules/adapters.md). One try/catch
    // is enough here — unlike PlayerStateDumpCheats there is a single hero to describe, not four
    // independent links, so there is nothing partial worth salvaging from a mid-gather failure.
    private static FaceReportSnapshot Gather(string requestedId)
    {
        var snapshot = new FaceReportSnapshot();
        try
        {
            var hero = string.IsNullOrWhiteSpace(requestedId)
                ? Hero.MainHero
                : Hero.AllAliveHeroes.FirstOrDefault(h => h.StringId == requestedId);

            if (hero == null)
            {
                snapshot.Found = false;
                snapshot.ErrorMessage = string.IsNullOrWhiteSpace(requestedId)
                    ? "No player hero (Hero.MainHero is null)."
                    : $"No hero found for id '{requestedId}'.";
                return snapshot;
            }

            var races = IoC.Resolve<IRaceManager>();
            var raceId = hero.CharacterObject?.Race ?? -1;
            var raceName = races.IsValidRaceId(raceId)
                ? races.GetRaceNameFromId(raceId)
                : $"INVALID race id {raceId}";

            snapshot.Found = true;
            snapshot.HeroId = hero.StringId;
            snapshot.HeroName = hero.Name?.ToString() ?? hero.StringId;
            snapshot.RaceName = raceName;
            snapshot.IsFemale = hero.IsFemale;
            snapshot.Age = hero.Age;
            snapshot.BodyPropertiesText = hero.BodyProperties.ToString();
            return snapshot;
        }
        catch (Exception ex)
        {
            snapshot.Found = false;
            snapshot.ErrorMessage = $"{ex.GetType().Name} reading hero: {ex.Message}";
            return snapshot;
        }
    }
}
