using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using TAOM.Core.Domain;
using TAOM.Core.Infrastructure;
using TAOM.Features.DevConsole;
using TaleWorlds.Library;
using static TAOM.Features.HeroRace.Configuration.RacePositionConfig;

namespace TAOM.Features.HeroRace.Cheats;

/// <summary>
/// In-game tuner for the per-race framing offsets. Thin entry points (ADR-002): argument parsing
/// and validation live in <see cref="RacePositionTuningParser"/> so they can be unit tested, since
/// nothing invoked across the engine's native console dispatch is reachable from a test.
///
/// <para>Only one of the fifteen races in a TAOM client has authored offsets, and the documented
/// alternative is edit the JSON, restart the game, look, repeat, which is why the other fourteen
/// never got done. These commands edit the live config rows and force the open tableau to redraw.</para>
///
/// <para>Routed through the dev console rather than global hotkeys deliberately: a hotkey listener
/// polls input every frame for a feature used on a handful of afternoons, and it fires for players
/// who never asked for it. The console is already gated on cheat mode.</para>
/// </summary>
public static class RacePositionTuningCheats
{
    private const string AvatarTail =
        "\nUse '.' for <race> to target the race the on-screen tableau is showing. On the 'avatar'\n"
        + "surface, prefix a race with 'mount_' to move its mount instead; the 'image' surface has no\n"
        + "mount row. Edits are live but in memory: run taom.save_race_offsets to keep them.";

    private const string PrintUsage =
        "Format is \"taom.print_race_offsets [avatar|image]\".\n"
        + "Lists the configured framing offsets. With no argument, lists both surfaces and reports\n"
        + "which race the on-screen tableau is showing.";

    private const string SetUsage =
        "Format is \"taom.set_race_offset <avatar|image> <race> <horizontal> <vertical> <zoom>\".\n"
        + "Sets all three offsets for a race outright." + AvatarTail;

    private const string NudgeUsage =
        "Format is \"taom.nudge_race_offset <avatar|image> <race> <h|v|z> <delta>\".\n"
        + "Adds delta to one axis: h = horizontal (left/right), v = vertical (up/down),\n"
        + "z = zoom (nearer/farther, negative is farther)." + AvatarTail;

    private const string SaveUsage =
        "Format is \"taom.save_race_offsets\".\n"
        + "Writes both framing configs to ModuleData/configs, keeping the previous file as .prev.";

    private const string ReloadUsage =
        "Format is \"taom.reload_race_offsets\".\n"
        + "Re-reads both framing configs from disk, discarding unsaved edits.";

    [CommandLineFunctionality.CommandLineArgumentFunction("print_race_offsets", "taom")]
    public static string PrintRaceOffsets(List<string> strings) =>
        TaomConsole.RunAnywhere(strings, PrintUsage, args =>
        {
            var store = IoC.Resolve<IRacePositionStore>();
            var report = new StringBuilder();
            report.AppendLine(DescribeLiveTableau());

            if (args.Count == 0)
            {
                report.Append(Describe(store, RacePositionSurface.Avatar));
                report.Append(Describe(store, RacePositionSurface.Image));
                return report.ToString();
            }

            if (!RacePositionTuningParser.TryParseSurface(args[0], out var surface, out var error))
                return error + "\n" + PrintUsage;

            report.Append(Describe(store, surface));
            return report.ToString();
        });

    [CommandLineFunctionality.CommandLineArgumentFunction("set_race_offset", "taom")]
    public static string SetRaceOffset(List<string> strings) =>
        TaomConsole.RunAnywhere(strings, SetUsage, args =>
        {
            if (args.Count < 5) return "Expected 5 arguments.\n" + SetUsage;
            if (!RacePositionTuningParser.TryParseSurface(args[0], out var surface, out var e0)) return e0 + "\n" + SetUsage;
            if (!ResolveRace(args[1], surface, out var race, out var e1)) return e1 + "\n" + SetUsage;
            if (!RacePositionTuningParser.TryParseOffset(args[2], "horizontal", out var h, out var e2)) return e2 + "\n" + SetUsage;
            if (!RacePositionTuningParser.TryParseOffset(args[3], "vertical", out var v, out var e3)) return e3 + "\n" + SetUsage;
            if (!RacePositionTuningParser.TryParseOffset(args[4], "zoom", out var z, out var e4)) return e4 + "\n" + SetUsage;

            var item = IoC.Resolve<IRacePositionStore>().GetOrAdd(surface, race);
            if (item == null) return $"Could not create a config row for race '{race}'.";

            item.Horizontal = h;
            item.Vertical = v;
            item.Zoom = z;

            RequestRedraw();
            return $"{surface} '{race}' set to {RacePositionTuningParser.Format(item)}. "
                 + "Unsaved: run taom.save_race_offsets.";
        });

    [CommandLineFunctionality.CommandLineArgumentFunction("nudge_race_offset", "taom")]
    public static string NudgeRaceOffset(List<string> strings) =>
        TaomConsole.RunAnywhere(strings, NudgeUsage, args =>
        {
            if (args.Count < 4) return "Expected 4 arguments.\n" + NudgeUsage;
            if (!RacePositionTuningParser.TryParseSurface(args[0], out var surface, out var e0)) return e0 + "\n" + NudgeUsage;
            if (!ResolveRace(args[1], surface, out var race, out var e1)) return e1 + "\n" + NudgeUsage;
            if (!RacePositionTuningParser.TryParseAxis(args[2], out var axis, out var e2)) return e2 + "\n" + NudgeUsage;

            if (!float.TryParse(args[3], NumberStyles.Float, CultureInfo.InvariantCulture, out var delta))
                return $"Could not read delta '{args[3]}' as a number.\n" + NudgeUsage;

            var item = IoC.Resolve<IRacePositionStore>().GetOrAdd(surface, race);
            if (item == null) return $"Could not create a config row for race '{race}'.";

            if (!RacePositionTuningParser.TryNudge(item, axis, delta, out var h, out var v, out var z))
                return $"That nudge would put '{race}' outside "
                     + $"[{Configuration.RacePositionConfigValidator.MinOffset}, "
                     + $"{Configuration.RacePositionConfigValidator.MaxOffset}]. "
                     + $"Left unchanged at {RacePositionTuningParser.Format(item)}.";

            item.Horizontal = h;
            item.Vertical = v;
            item.Zoom = z;

            RequestRedraw();
            return $"{surface} '{race}' now {RacePositionTuningParser.Format(item)}. "
                 + "Unsaved: run taom.save_race_offsets.";
        });

    [CommandLineFunctionality.CommandLineArgumentFunction("save_race_offsets", "taom")]
    public static string SaveRaceOffsets(List<string> strings) =>
        TaomConsole.RunAnywhere(strings, SaveUsage, _ =>
        {
            var store = IoC.Resolve<IRacePositionStore>();
            store.Save();

            var path = IoC.Resolve<IPathService>().ConfigPath;
            return $"Wrote CharacterAvatarPatch.json ({store.List(RacePositionSurface.Avatar).Count} rows) "
                 + $"and CharacterImagePatch.json ({store.List(RacePositionSurface.Image).Count} rows) to {path}.\n"
                 + "Previous files kept as .prev. These live in the game install, not the repo: copy them\n"
                 + "back into Main/_Module/ModuleData/configs/ to keep them.";
        });

    [CommandLineFunctionality.CommandLineArgumentFunction("reload_race_offsets", "taom")]
    public static string ReloadRaceOffsets(List<string> strings) =>
        TaomConsole.RunAnywhere(strings, ReloadUsage, _ =>
        {
            IoC.Resolve<IRacePositionStore>().Reload();
            RequestRedraw();
            return "Reloaded both framing configs from disk. Unsaved edits discarded.";
        });

    private static bool ResolveRace(string raw, RacePositionSurface surface, out string race, out string error)
    {
        var raceManager = IoC.Resolve<IRaceManager>();
        return RacePositionTuningParser.TryResolveRace(
            raw, surface, raceManager.IsValidRaceName, LiveRaceName(raceManager), out race, out error);
    }

    private static string LiveRaceName(IRaceManager raceManager)
    {
        var live = LiveTableauRef.LastRace;
        return live >= 0 && raceManager.IsValidRaceId(live) ? raceManager.GetRaceNameFromId(live) : null;
    }

    // Marking the tableau dirty makes vanilla re-run RefreshCharacterTableau on its next tick, which
    // re-applies the offsets through Patch72. Without it an edit is invisible until the player
    // changes equipment.
    private static void RequestRedraw()
    {
        try
        {
            if (LiveTableauRef.TryGet(out var tableau))
                ReflectionHelper.SetFieldValue(tableau, "_isVisualsDirty", true);
        }
        catch
        {
            // Best effort. A failed redraw means the value applies on the next natural refresh; it
            // must never turn a successful edit into a reported failure.
        }
    }

    private static string DescribeLiveTableau()
    {
        try
        {
            var raceManager = IoC.Resolve<IRaceManager>();
            var name = LiveRaceName(raceManager);
            return name == null
                ? "On-screen tableau: none."
                : $"On-screen tableau: race {LiveTableauRef.LastRace} ('{name}').";
        }
        catch
        {
            return "On-screen tableau: unknown.";
        }
    }

    private static string Describe(IRacePositionStore store, RacePositionSurface surface)
    {
        var rows = store.List(surface);
        var report = new StringBuilder();
        report.AppendLine($"--- {surface} ({rows.Count} rows) ---");

        if (rows.Count == 0)
        {
            report.AppendLine("  (none configured: every race uses vanilla framing)");
            return report.ToString();
        }

        foreach (var item in rows.Where(i => i != null).OrderBy(i => i.Race, StringComparer.Ordinal))
            report.AppendLine($"  {item.Race,-18} {RacePositionTuningParser.Format(item)}");

        return report.ToString();
    }
}
