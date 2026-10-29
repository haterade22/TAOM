using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using TAOM.Features.StagedSiege.Domain;

namespace TAOM.Features.StagedSiege;

/// <summary>
/// The disk half of the in-game recorder behind <c>taom.staged_siege_record</c> and <c>taom.staged_siege_export</c>. Appends one
/// JSON line per recorded point to <c>staged_siege_records.jsonl</c> in the player's Documents (Documents\Mount and Blade II
/// Bannerlord\TAOM), never in the game install, and writes the exported scene block next to it. Plain floats in and out, no
/// engine type, so the files and the line format are tested without a game. Newtonsoft writes the numbers, which is
/// culture-invariant. The parsing and folding live in <see cref="StagedSiegeRecordFragment"/>.
/// </summary>
internal sealed class StagedSiegeRecorder
{
    private const string RecordsFile = "staged_siege_records.jsonl";

    private readonly string _directory;

    internal StagedSiegeRecorder(string directory) => _directory = directory;

    internal static StagedSiegeRecorder Default { get; } = new(Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments), "Mount and Blade II Bannerlord", "TAOM"));

    internal string FilePath => Path.Combine(_directory, RecordsFile);

    internal string ExportPath(string scene) => Path.Combine(_directory, "staged_siege_" + SafeName(scene) + ".json");

    /// <summary>
    /// The radius typed after a <c>point</c> record. Null text means none was typed and the record carries no radius, so the
    /// <c>captureRadius</c> tunable stays in charge. Returns the refusal sentence, or null when the radius is usable; the range is
    /// the catalog's, so the recorder never writes a point the catalog would skip.
    /// </summary>
    internal static string? ParseRadius(string? text, out float? radius)
    {
        radius = null;
        if (text == null)
            return null;
        if (!float.TryParse(text, System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out var parsed))
            return "Radius must be a number.";
        if (!(parsed >= StagedSiegeTunables.RadiusMin && parsed <= StagedSiegeTunables.RadiusMax))
            return $"Radius must be between {StagedSiegeTunables.RadiusMin} and {StagedSiegeTunables.RadiusMax} metres.";
        radius = parsed;
        return null;
    }

    /// <summary>
    /// The ground direction recorded with a frame, normalized the way the catalog reads it. Returns a refusal sentence when the
    /// look is too close to straight up or down (or not a number) for the catalog to accept, or null with the direction filled.
    /// </summary>
    internal static string? NormalizeLook(float x, float y, out float fx, out float fy)
    {
        if (!StageFacing.TryNormalize(x, y, out var facing))
        {
            fx = 0f;
            fy = 1f;
            return "Look level at the direction to record: the camera points too far up or down to give a ground direction.";
        }

        fx = facing.X;
        fy = facing.Y;
        return null;
    }

    /// <summary>Appends one record and returns the line written.</summary>
    internal string Append(string scene, char stage, string kind, float x, float y, float z, float fx, float fy, float? radius)
    {
        var line = new JObject
        {
            ["scene"] = scene,
            ["stage"] = stage.ToString(),
            ["kind"] = kind,
            ["x"] = x,
            ["y"] = y,
            ["z"] = z,
            ["fx"] = fx,
            ["fy"] = fy,
        };
        if (radius.HasValue)
            line["radius"] = radius.Value;
        line["utc"] = DateTime.UtcNow.ToString("o", System.Globalization.CultureInfo.InvariantCulture);

        var text = line.ToString(Formatting.None);
        Directory.CreateDirectory(_directory);
        File.AppendAllText(FilePath, text + "\n", new UTF8Encoding(false));
        return text;
    }

    /// <summary>Every usable record in the file, oldest first; <paramref name="badLines"/> counts the lines that parse to nothing.</summary>
    internal List<StagedSiegeRecord> ReadAll(out int badLines)
    {
        badLines = 0;
        var records = new List<StagedSiegeRecord>();
        if (!File.Exists(FilePath))
            return records;

        foreach (var text in File.ReadAllLines(FilePath))
        {
            if (string.IsNullOrWhiteSpace(text))
                continue;
            var record = StagedSiegeRecordFragment.ParseLine(text);
            if (record == null)
                badLines++;
            else
                records.Add(record);
        }

        return records;
    }

    /// <summary>The newest <c>point</c> record per stage letter for <paramref name="scene"/>.</summary>
    internal Dictionary<char, StagedSiegeRecord> LatestPoints(string scene, out int badLines)
    {
        var latest = new Dictionary<char, StagedSiegeRecord>();
        foreach (var record in ReadAll(out badLines))
        {
            if (record.Kind == "point" && string.Equals(record.Scene, scene, StringComparison.OrdinalIgnoreCase))
                latest[record.Stage] = record;
        }

        return latest;
    }

    /// <summary>Folds the file's records of a scene into the shipped JSON shape and writes it next to the file.</summary>
    internal StagedSiegeFragment Export(string scene, out string path)
    {
        var fragment = StagedSiegeRecordFragment.Build(scene, ReadAll(out _));
        path = ExportPath(scene);
        Directory.CreateDirectory(_directory);
        File.WriteAllText(path, fragment.Json, new UTF8Encoding(false));
        return fragment;
    }

    private static string SafeName(string scene)
    {
        var sb = new StringBuilder(scene.Length);
        foreach (var c in scene)
            sb.Append(Array.IndexOf(Path.GetInvalidFileNameChars(), c) >= 0 ? '_' : c);
        return sb.Length == 0 ? "scene" : sb.ToString();
    }
}
