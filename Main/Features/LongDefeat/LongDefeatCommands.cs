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
        "Format is \"taom.ld_start <fortress> [waves]\".\n"
        + "Starts The Long Defeat from the main menu with no prompt. With a wave count the run ends as won once that many waves are cleared; "
        + "without one it plays until the garrison or the keep falls.";

    private const int MaxWaves = 1000;

    internal static string Usage(LongDefeatConfigCatalog catalog)
    {
        var ids = catalog.Fortresses.Select(f => f.Id).ToList();
        return StartUsageHead + "\nFortresses: " + string.Join(", ", ids) + ".\nExample: taom.ld_start " + ids[0] + " 5";
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
        if (args.Count > 1)
        {
            if (!DevConsoleArgs.TryParseCount(args[1], 1, MaxWaves, out var waves, out var error))
                return error;
            options = options with { StopAfterWaves = waves };
        }

        var setup = new LongDefeatSetup(fortress.Id, "console", options);
        if (!prompt.Start(setup))
            return "The Long Defeat could not start (no TAOM.Client, or the load failed); the log and the main menu notice say why.";

        return options.StopAfterWaves is { } target
            ? $"Starting The Long Defeat at {fortress.Id}, {target} waves."
            : $"Starting The Long Defeat at {fortress.Id}, until it is lost.";
    }

    internal static readonly string RecordUsage =
        "Format is \"taom.ld_record <keep|muster_foot|muster_archers> [radius]\".\n"
        + "Appends the main agent's position and look to long_defeat_records.jsonl in Documents\\Mount and Blade II Bannerlord\\TAOM. "
        + $"A radius ({FortressesReader.KeepRadiusMin:0} to {FortressesReader.KeepRadiusMax:0}) applies to keep only; without one the rules' keepRadius stays in charge. taom.ld_export folds the newest "
        + "records of this scene into long_defeat_<scene>.json.";

    /// <summary>The body of <c>taom.ld_record</c>: <paramref name="args"/> are the words after the command name.</summary>
    internal static string Record(IReadOnlyList<string> args, LongDefeatRecorder recorder, LdSpot spot)
    {
        var kind = args.Count == 0 ? "" : args[0].ToLowerInvariant();
        if (!LongDefeatRecorder.IsKind(kind))
            return "Expected a kind: keep, muster_foot or muster_archers.\n" + RecordUsage;

        float? radius = null;
        if (args.Count > 1)
        {
            if (kind != LongDefeatRecorder.KeepKind)
                return "Only a keep record takes a radius.";
            var refusal = LongDefeatRecorder.ParseRadius(args[1], out radius);
            if (refusal != null)
                return refusal;
        }

        if (!(Math.Abs(spot.X) <= FortressesReader.MaxCoordinate && Math.Abs(spot.Y) <= FortressesReader.MaxCoordinate
            && Math.Abs(spot.Z) <= FortressesReader.MaxCoordinate))
            return "The main agent's position is not a usable coordinate.";

        var lookRefusal = LongDefeatRecorder.NormalizeLook(spot.LookX, spot.LookY, out var fx, out var fy);
        if (lookRefusal != null && kind != LongDefeatRecorder.KeepKind)
            return lookRefusal;

        var line = recorder.Append(spot.Scene, kind, spot.X, spot.Y, spot.Z, fx, fy, radius);
        return $"Appended to {recorder.FilePath}\n{line}";
    }

    /// <summary>The body of <c>taom.ld_export</c>.</summary>
    internal static string Export(LongDefeatRecorder recorder, string scene)
    {
        var export = recorder.Export(scene, out var path);
        var head = export.IsComplete ? $"Wrote the complete block for {scene} to {path}." : $"Wrote {path}.";
        var paste = export.Missing.Contains(LongDefeatRecorder.KeepKind)
            ? ""
            : " Paste its keep into this fortress's row in fortresses.json; a game restart loads it.";
        var gaps = export.IsComplete ? "" : $" These pieces have no record yet: {string.Join(", ", export.Missing)}.";
        return head + paste + gaps;
    }
}

/// <summary>Where the main agent stood and looked, read by the console command and handed to <see cref="LongDefeatCommands.Record"/>.</summary>
internal readonly record struct LdSpot(string Scene, float X, float Y, float Z, float LookX, float LookY);
