using System;
using System.Collections.Generic;
using System.Linq;
using TAOM.Features.DevConsole;
using TAOM.Features.LongDefeat.Domain;

namespace TAOM.Features.LongDefeat;

/// <summary>
/// The body of <c>taom.ld_start</c>, apart from the attributed entry point so it runs in a test with a catalog and a prompt
/// instead of the engine. Replies are plain English: console output is not localized (docs/features/dev-console.md).
/// </summary>
internal static class LongDefeatCommands
{
    internal const string StartUsageHead =
        "Format is \"taom.ld_start <fortress> [waves] [class]\".\n"
        + "Starts The Long Defeat from the main menu with no prompt. With a wave count the run ends as won once that many waves are cleared; "
        + "without one it plays until the garrison or the keep falls. With a class the lord carries that class's kit; without one he takes the first class on offer. "
        + "A class id may stand in the second place when there is no wave count.";

    private const int MaxWaves = 1000;

    internal static string Usage(LongDefeatConfigCatalog catalog)
    {
        var ids = catalog.Fortresses.Select(f => f.Id).ToList();
        var classes = catalog.Classes.Classes.Select(c => c.Id).ToList();
        return StartUsageHead + "\nFortresses: " + string.Join(", ", ids) + "."
            + (classes.Count == 0 ? "" : "\nClasses: " + string.Join(", ", classes) + ".")
            + "\nExample: taom.ld_start " + ids[0] + " 5" + (classes.Count == 0 ? "" : " " + classes[0]);
    }

    internal static string Start(IReadOnlyList<string> args, LongDefeatConfigCatalog catalog, LongDefeatSetupPrompt prompt)
    {
        if (catalog.Fortresses.Count == 0)
            return "The config holds no fortress (long_defeat/fortresses.json), so nothing can start; the log says why.";
        if (args.Count == 0)
            return Usage(catalog);
        if (!catalog.TryGetFortress(args[0], out var fortress))
            return $"No fortress \"{args[0]}\".\n" + Usage(catalog);

        var options = RunOptions.Default;
        var offer = catalog.ClassesFor(fortress);
        var classWord = args.Count > 2 ? args[2] : null;
        if (args.Count > 1)
        {
            // A word that is not a whole number stands in the second place as a class, because no wave count is a name; an unknown one is answered as a class below.
            if (args.Count == 2 && !int.TryParse(args[1], out _))
                classWord = args[1];
            else if (!DevConsoleArgs.TryParseCount(args[1], 1, MaxWaves, out var waves, out var error))
                return error;
            else
                options = options with { StopAfterWaves = waves };
        }

        if (classWord != null)
        {
            var asked = offer.FirstOrDefault(c => string.Equals(c.Id, classWord, StringComparison.OrdinalIgnoreCase));
            if (asked == null)
                return $"No class \"{classWord}\" at {fortress.Id}.\n" + Usage(catalog);
            options = options with { ClassId = asked.Id };
        }

        var setup = new LongDefeatSetup(fortress.Id, "console", options);
        if (!prompt.Start(setup))
            return "The Long Defeat could not start (no TAOM.Client, or the load failed); the log and the main menu notice say why.";

        return options.StopAfterWaves is { } target
            ? $"Starting The Long Defeat at {fortress.Id}, {target} waves."
            : $"Starting The Long Defeat at {fortress.Id}, until it is lost.";
    }

    private const string ClearWord = "clear";

    internal static readonly string RecordUsage =
        "Format is \"taom.ld_record <kind> [word]\" or \"taom.ld_record clear <kind> [tower]\".\n"
        + "Appends the main agent's position and look to long_defeat_records.jsonl in Documents\\Mount and Blade II Bannerlord\\TAOM. The kinds, with an example each:\n"
        + $"  keep [radius]: taom.ld_record keep 12. The zone attackers must hold. A radius ({FortressesReader.KeepRadiusMin:0} to {FortressesReader.KeepRadiusMax:0}) applies to keep only; without one the rules' keepRadius stays in charge.\n"
        + "  muster_foot: taom.ld_record muster_foot. Where new foot defenders appear; face the way the line should face.\n"
        + "  muster_archers: taom.ld_record muster_archers. The same for the archers.\n"
        + "  sally: taom.ld_record sally. Where a mounted class rides out to at each wave's start: stand outside the walls but inside the play area, and face the way the rider should face. It is optional, "
        + "and the next boot line says whether the point has a navmesh and lies inside the boundary.\n"
        + "  post <tower>: taom.ld_record post hornburg. One spot where an archer should hold on a tower top; face out over the approach. Record one per archer, about 8 a tower.\n"
        + "  tower_volley <tower>: taom.ld_record tower_volley hornburg. A spot behind that tower's parapet that its volley looses from; no facing is kept. About 2 a tower.\n"
        + "  volley: taom.ld_record volley. A spot behind the parapet along the wall walk that the Elven Volley looses from; no facing is kept. About 6, spread along the wall.\n"
        + "  armoury: taom.ld_record armoury. Where the lord walks to buy his own armour and weapons (Phase 4); face the way he should face while he shops. Optional.\n"
        + "  yard: taom.ld_record yard. Where the lord walks to buy siege engines, the engineer's yard (Phase 4); face the way he should face there. Optional.\n"
        + "  clear <kind> [tower]: taom.ld_record clear post hornburg, or taom.ld_record clear volley. Removes this scene's records of that kind (only that tower's when one is named) so a spot can be done again.\n"
        + "A <tower> is a name of lowercase letters and underscores that you choose and repeat for every record of that tower (hornburg, deeping_wall). "
        + "taom.ld_export folds this scene's records into long_defeat_<scene>.json, prints the counts per kind and per tower, and prints one block to paste for each of the keep, the muster, the sally, the towers, the volley, the armoury and the yard that is recorded.";

    /// <summary>True when the first word is <c>clear</c>, which reads only the scene, so the console asks for no controlled soldier.</summary>
    internal static bool IsClear(IReadOnlyList<string> args) => args.Count > 0 && string.Equals(args[0], ClearWord, StringComparison.OrdinalIgnoreCase);

    /// <summary>The body of <c>taom.ld_record</c>: <paramref name="args"/> are the words after the command name.</summary>
    internal static string Record(IReadOnlyList<string> args, LongDefeatRecorder recorder, LdSpot spot)
    {
        var kind = args.Count == 0 ? "" : args[0].ToLowerInvariant();
        if (IsClear(args))
            return Clear(args, recorder, spot.Scene);
        if (!LongDefeatRecorder.IsKind(kind))
            return $"Expected a kind: {string.Join(", ", LongDefeatRecorder.AllKinds)}, or clear.\n" + RecordUsage;

        float? radius = null;
        string? tower = null;
        if (LongDefeatRecorder.IsTowerKind(kind))
        {
            var towerRefusal = ParseTower(args, 1, kind, out tower);
            if (towerRefusal != null)
                return towerRefusal;
            if (args.Count > 2)
                return $"A {kind} record takes a tower only, no radius.";
        }
        else if (args.Count > 1)
        {
            if (kind == LongDefeatRecorder.VolleyKind)
                return "A volley record takes no tower or radius: its origins are one list for the whole wall.";
            if (kind != LongDefeatRecorder.KeepKind)
                return "Only a keep record takes a radius.";
            var refusal = LongDefeatRecorder.ParseRadius(args[1], out radius);
            if (refusal != null)
                return refusal;
        }

        if (!(Math.Abs(spot.X) <= FortressesReader.MaxCoordinate && Math.Abs(spot.Y) <= FortressesReader.MaxCoordinate
            && Math.Abs(spot.Z) <= FortressesReader.MaxCoordinate))
            return "The main agent's position is not a usable coordinate.";

        // A keep, a volley origin and a tower's volley spot read no facing, so a steep look still records their point.
        var lookRefusal = LongDefeatRecorder.NormalizeLook(spot.LookX, spot.LookY, out var fx, out var fy);
        var readsFacing = kind != LongDefeatRecorder.KeepKind && kind != LongDefeatRecorder.VolleyKind && kind != LongDefeatRecorder.TowerVolleyKind;
        if (lookRefusal != null && readsFacing)
            return lookRefusal;

        var line = recorder.Append(spot.Scene, kind, spot.X, spot.Y, spot.Z, fx, fy, radius, tower);
        return $"Appended to {recorder.FilePath}\n{line}";
    }

    // The tower word after a post or tower_volley kind, lower-cased; the refusal sentence (with the usage when the word is missing), or null.
    private static string? ParseTower(IReadOnlyList<string> args, int index, string kind, out string? tower)
    {
        tower = null;
        if (args.Count <= index)
            return $"A {kind} record needs the tower it belongs to, for example \"taom.ld_record {kind} hornburg\".\n" + RecordUsage;
        var word = args[index].ToLowerInvariant();
        if (!FortressesReader.IsTowerId(word))
            return $"A tower name is lowercase letters and underscores only (hornburg, deeping_wall), not \"{args[index]}\".";
        tower = word;
        return null;
    }

    private static string Clear(IReadOnlyList<string> args, LongDefeatRecorder recorder, string scene)
    {
        var kind = args.Count > 1 ? args[1].ToLowerInvariant() : "";
        if (!LongDefeatRecorder.IsKind(kind))
            return $"Expected a kind to clear: {string.Join(", ", LongDefeatRecorder.AllKinds)}.\n" + RecordUsage;
        if (args.Count > 3)
            return "Too many words after clear.\n" + RecordUsage;

        string? tower = null;
        if (args.Count > 2)
        {
            if (!LongDefeatRecorder.IsTowerKind(kind))
                return $"The {kind} kind has no tower to name.\n" + RecordUsage;
            var refusal = ParseTower(args, 2, kind, out tower);
            if (refusal != null)
                return refusal + "\n" + RecordUsage;
        }

        var what = tower == null ? kind : $"{kind} {tower}";
        var removed = recorder.Clear(scene, kind, tower);
        return removed == 0
            ? $"Nothing to clear: no {what} record for {scene}."
            : $"Cleared {removed} {what} record(s) for {scene} from {recorder.FilePath}.";
    }

    /// <summary>The body of <c>taom.ld_export</c>.</summary>
    internal static string Export(LongDefeatRecorder recorder, string scene)
    {
        var export = recorder.Export(scene, out var path);
        var head = export.IsComplete ? $"Wrote the complete block for {scene} to {path}." : $"Wrote {path}.";
        var gaps = export.IsComplete ? "" : $" These pieces have no record yet: {string.Join(", ", export.Missing)}.";
        return head + gaps + LongDefeatExportText.Details(export, scene);
    }
}

/// <summary>Where the main agent stood and looked, read by the console command and handed to <see cref="LongDefeatCommands.Record"/>.</summary>
internal readonly record struct LdSpot(string Scene, float X, float Y, float Z, float LookX, float LookY);
