using System.Collections.Generic;
using System.IO;
using TaleWorlds.Library;
using TAOM.Features.DevConsole;
using TAOM.Features.RealmBorders.Domain;

namespace TAOM.Features.RealmBorders.Cheats;

/// <summary>
/// The realm borders' console commands (#698), for the look session and for a report of a border in
/// the wrong place: the status line, the province map as a picture, a full rebuild, and a live switch
/// of the material the borders are drawn from. None of them touches saved state; the borders are
/// drawn from the campaign every session.
/// </summary>
public static class RealmBordersCheats
{
    private const string MapFileName = "taom_realm_provinces.bmp";

    private const string StatusUsage =
        "Format is \"taom.print_realm_borders\".\n"
        + "Prints whether the realm borders are on, the map mode, how far the province build has got and\n"
        + "how long it and the last repaint took, the tiles drawn, the slowest tile upload, the fade, and\n"
        + "the material and blend mode they are drawn with.";

    private const string MapUsage =
        "Format is \"taom.print_realm_province_map\".\n"
        + "Writes the province map to Logs/" + MapFileName + ": each realm in its colour with its edges dark,\n"
        + "a fief without an owner white, wild land parchment, water slate blue. North is up.";

    private const string RebuildUsage =
        "Format is \"taom.realm_borders_rebuild\".\n"
        + "Samples the map's terrain again, recomputes every province and redraws the borders.";

    private const string MaterialUsage =
        "Format is \"taom.realm_borders_material <name>\".\n"
        + "Redraws the borders from another engine material. Refused when no material has that name.";

    private const string BlendUsage =
        "Format is \"taom.realm_borders_blend <mode>\".\n"
        + "Redraws the borders with another engine blend mode, until the MCM Border Blend Mode is changed:\n"
        + "NoAlphaBlend, Modulate, AddAlpha, Multiply, Add, Max, Factor, AddModulateCombined, NoAlphaBlendNoWrite,\n"
        + "ModulateNoWrite, GbufferAlphaBlend, GbufferAlphaBlendWithVtResolve, NoAlphaBlendNoAlphaWrite.\n"
        + "taom.print_realm_borders shows the one in use.";

    [CommandLineFunctionality.CommandLineArgumentFunction("print_realm_borders", "taom")]
    public static string PrintRealmBorders(List<string> strings) =>
        TaomConsole.RunInCampaign(strings, StatusUsage, _ => IoC.Resolve<RealmBorderService>().Status());

    [CommandLineFunctionality.CommandLineArgumentFunction("print_realm_province_map", "taom")]
    public static string PrintRealmProvinceMap(List<string> strings) =>
        TaomConsole.RunInCampaign(strings, MapUsage, _ =>
        {
            var borders = IoC.Resolve<RealmBorderService>();
            var image = borders.ProvinceImage();
            if (image == null)
                return "The provinces are not built yet: open the campaign map and give it a few seconds.\n" + borders.Status();

            var (columns, rows, pixels) = image.Value;
            Directory.CreateDirectory("Logs");
            string path = Path.Combine("Logs", MapFileName);
            File.WriteAllBytes(path, ProvinceBitmap.Encode(columns, rows, pixels));
            return $"Wrote the {columns}x{rows} province map to {Path.GetFullPath(path)}";
        });

    [CommandLineFunctionality.CommandLineArgumentFunction("realm_borders_rebuild", "taom")]
    public static string RealmBordersRebuild(List<string> strings) =>
        TaomConsole.RunInCampaign(strings, RebuildUsage, _ =>
        {
            IoC.Resolve<RealmBorderService>().Rebuild();
            return "Rebuilding the provinces; the borders redraw over the next few seconds on the map. "
                 + "taom.print_realm_borders reports the timings.";
        });

    [CommandLineFunctionality.CommandLineArgumentFunction("realm_borders_blend", "taom")]
    public static string RealmBordersBlend(List<string> strings) =>
        TaomConsole.RunInCampaign(strings, BlendUsage, args =>
        {
            if (args.Count != 1)
                return BlendUsage;
            return IoC.Resolve<RealmBorderService>().UseBlendMode(args[0])
                ? $"Redrawing the borders with blend mode '{args[0]}'."
                : $"No blend mode named '{args[0]}'.\n" + BlendUsage;
        });

    [CommandLineFunctionality.CommandLineArgumentFunction("realm_borders_material", "taom")]
    public static string RealmBordersMaterial(List<string> strings) =>
        TaomConsole.RunInCampaign(strings, MaterialUsage, args =>
        {
            if (args.Count != 1)
                return MaterialUsage;
            return IoC.Resolve<RealmBorderService>().UseMaterial(args[0])
                ? $"Redrawing the borders from '{args[0]}'."
                : $"No material named '{args[0]}'; the borders keep their current one.";
        });
}
