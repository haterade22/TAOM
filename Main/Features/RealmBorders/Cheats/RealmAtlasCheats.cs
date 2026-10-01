using System.Collections.Generic;
using System.Globalization;
using TaleWorlds.Library;
using TAOM.Features.DevConsole;

namespace TAOM.Features.RealmBorders.Cheats;

/// <summary>
/// The parchment map's console commands (#698 follow-up), named like the borders' own: its status line
/// and a rebuild, for a report of a map that does not show, and live paper and ink colours and fade band,
/// for tuning the look. MCM's Parchment Map switch turns it on and off. None of them touches saved state.
/// </summary>
public static class RealmAtlasCheats
{
    private const string StatusUsage =
        "Format is \"taom.print_realm_atlas\".\n"
        + "Prints the parchment map's state: whether MCM has it on, whether it is built and how long that took,\n"
        + "its material and picture, its opacity, the camera's zoom against its furthest, the fade band and the colours.";

    private const string RebuildUsage =
        "Format is \"taom.realm_atlas_rebuild\".\n"
        + "Draws the parchment map again the next time the camera is zoomed out to it, after a failed build too.";

    private static readonly string TintUsage =
        "Format is \"taom.realm_atlas_tint <paper #RRGGBB> [ink #RRGGBB]\".\n"
        + $"Redraws the parchment map with this paper colour (default #{RealmAtlasService.DefaultPaper & 0xFFFFFFu:X6}) and, if given,\n"
        + $"this ink colour, seen through the picture's dark strokes (default #{RealmAtlasService.DefaultInk & 0xFFFFFFu:X6}). Until the game restarts.";

    private static readonly string FadeUsage =
        "Format is \"taom.realm_atlas_fade <start> <full>\".\n"
        + "Sets where the parchment map fades in, as fractions of the furthest zoom, from 0 to 1 with the start\n"
        + $"below the full point. The defaults are {Fraction(RealmAtlasService.DefaultFadeStart)} and "
        + $"{Fraction(RealmAtlasService.DefaultFadeFull)}. Until the game restarts.";

    [CommandLineFunctionality.CommandLineArgumentFunction("print_realm_atlas", "taom")]
    public static string PrintRealmAtlas(List<string> strings) =>
        TaomConsole.RunInCampaign(strings, StatusUsage, _ => IoC.Resolve<RealmAtlasService>().Status());

    [CommandLineFunctionality.CommandLineArgumentFunction("realm_atlas_rebuild", "taom")]
    public static string RealmAtlasRebuild(List<string> strings) =>
        TaomConsole.RunInCampaign(strings, RebuildUsage, _ =>
        {
            IoC.Resolve<RealmAtlasService>().Rebuild();
            return "Redrawing the parchment map the next time the camera is zoomed out to it. "
                 + "taom.print_realm_atlas reports the build.";
        });

    [CommandLineFunctionality.CommandLineArgumentFunction("realm_atlas_tint", "taom")]
    public static string RealmAtlasTint(List<string> strings) =>
        TaomConsole.RunInCampaign(strings, TintUsage, args =>
        {
            if (args.Count < 1 || args.Count > 2 || !RealmPaletteProvider.TryParseColour(args[0].Trim(), out uint paper))
                return TintUsage;
            uint? ink = null;
            if (args.Count == 2)
            {
                if (!RealmPaletteProvider.TryParseColour(args[1].Trim(), out uint parsed))
                    return TintUsage;
                ink = parsed;
            }
            var atlas = IoC.Resolve<RealmAtlasService>();
            atlas.UseTint(paper, ink);
            return $"Redrawing the parchment map: paper #{atlas.Paper & 0xFFFFFFu:X6}, ink #{atlas.Ink & 0xFFFFFFu:X6}.";
        });

    [CommandLineFunctionality.CommandLineArgumentFunction("realm_atlas_fade", "taom")]
    public static string RealmAtlasFade(List<string> strings) =>
        TaomConsole.RunInCampaign(strings, FadeUsage, args =>
        {
            if (args.Count != 2)
                return FadeUsage;
            if (!DevConsoleArgs.TryParseAmount(args[0], out float start, out string error)
                || !DevConsoleArgs.TryParseAmount(args[1], out float full, out error))
                return error + "\n" + FadeUsage;
            if (!IoC.Resolve<RealmAtlasService>().SetFade(start, full))
                return FadeUsage;
            return $"The parchment map now fades in from {Fraction(start)} to {Fraction(full)} of the furthest zoom.";
        });

    private static string Fraction(float value) => value.ToString("0.###", CultureInfo.InvariantCulture);
}
