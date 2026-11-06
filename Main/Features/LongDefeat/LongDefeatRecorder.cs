using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using TAOM.Core.Validation;

namespace TAOM.Features.LongDefeat;

/// <summary>One line of <c>long_defeat_records.jsonl</c>: where the main agent stood and looked when a point was recorded.</summary>
internal sealed record LongDefeatRecord(string Scene, string Kind, float X, float Y, float Z, float Fx, float Fy, float? Radius);

/// <summary>
/// The block <c>taom.ld_export</c> writes for a scene, and the kinds of point still unrecorded. The sally is optional: it is never a missing piece, and <paramref name="HasSally"/> says the block carries one.
/// </summary>
internal sealed record LongDefeatExport(string Json, IReadOnlyList<string> Missing, bool HasSally = false)
{
    public bool IsComplete => Missing.Count == 0;
}

/// <summary>
/// The disk half of the in-game recorder behind <c>taom.ld_record</c> and <c>taom.ld_export</c>: appends one JSON line per recorded
/// point to <c>long_defeat_records.jsonl</c> in the player's Documents and writes the exported scene block next to it.
/// </summary>
internal sealed class LongDefeatRecorder
{
    internal const string KeepKind = "keep";
    internal const string MusterFootKind = "muster_foot";
    internal const string MusterArchersKind = "muster_archers";

    /// <summary>The place a mounted class rides out to at each wave's start (spike S9): stand outside the walls, inside the play area, facing the way the sally should face.</summary>
    internal const string SallyKind = "sally";

    private const string RecordsFile = "long_defeat_records.jsonl";

    // The kinds a scene needs, and every kind that can be recorded: the sally is optional, so a scene without one is complete.
    private static readonly string[] Required = { KeepKind, MusterFootKind, MusterArchersKind };
    private static readonly string[] Kinds = { KeepKind, MusterFootKind, MusterArchersKind, SallyKind };

    private readonly string _directory;

    internal LongDefeatRecorder(string directory) => _directory = directory;

    internal static LongDefeatRecorder Default { get; } = new(LongDefeatRunRecords.PlayerFolder);

    internal string FilePath => Path.Combine(_directory, RecordsFile);

    internal string ExportPath(string scene) => Path.Combine(_directory, "long_defeat_" + SafeName(scene) + ".json");

    internal static bool IsKind(string kind) => Kinds.Contains(kind);

    /// <summary>The radius typed after a <c>keep</c> record. Null text means none, and the rules' <c>keepRadius</c> stays in charge. Returns the refusal sentence, or null when usable.</summary>
    internal static string? ParseRadius(string? text, out float? radius)
    {
        radius = null;
        if (text == null)
            return null;
        if (!float.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out var parsed))
            return "Radius must be a number.";
        if (!(parsed >= FortressesReader.KeepRadiusMin && parsed <= FortressesReader.KeepRadiusMax))
            return $"Radius must be between {FortressesReader.KeepRadiusMin} and {FortressesReader.KeepRadiusMax} metres.";
        radius = parsed;
        return null;
    }

    /// <summary>The ground direction recorded with a muster point. Returns a refusal sentence when the look is too steep (or not a number) to give one.</summary>
    internal static string? NormalizeLook(float x, float y, out float fx, out float fy)
    {
        var length = Math.Sqrt((double)x * x + (double)y * y);
        if (!FiniteFloatValidator.IsFiniteInRange(length, 0.1d, float.MaxValue))
        {
            fx = 0f;
            fy = 1f;
            return "Look level at the direction to record: the camera points too far up or down to give a ground direction.";
        }

        fx = (float)(x / length);
        fy = (float)(y / length);
        return null;
    }

    /// <summary>Appends one record and returns the line written.</summary>
    internal string Append(string scene, string kind, float x, float y, float z, float fx, float fy, float? radius)
    {
        var line = new JObject
        {
            ["scene"] = scene,
            ["kind"] = kind,
            ["x"] = x,
            ["y"] = y,
            ["z"] = z,
            ["fx"] = fx,
            ["fy"] = fy,
        };
        if (radius.HasValue)
            line["radius"] = radius.Value;
        line["utc"] = DateTime.UtcNow.ToString("o", CultureInfo.InvariantCulture);

        var text = line.ToString(Formatting.None);
        Directory.CreateDirectory(_directory);
        File.AppendAllText(FilePath, text + "\n", new UTF8Encoding(false));
        return text;
    }

    /// <summary>Every usable record in the file, oldest first; <paramref name="badLines"/> counts the lines that parse to nothing.</summary>
    internal List<LongDefeatRecord> ReadAll(out int badLines)
    {
        badLines = 0;
        var records = new List<LongDefeatRecord>();
        if (!File.Exists(FilePath))
            return records;

        foreach (var text in File.ReadAllLines(FilePath))
        {
            if (string.IsNullOrWhiteSpace(text))
                continue;
            var record = Parse(text);
            if (record == null)
                badLines++;
            else
                records.Add(record);
        }

        return records;
    }

    /// <summary>Folds the newest record of each kind for <paramref name="scene"/> into the block to paste into fortresses.json, and writes it next to the records.</summary>
    internal LongDefeatExport Export(string scene, out string path)
    {
        var newest = new Dictionary<string, LongDefeatRecord>();
        foreach (var record in ReadAll(out _))
        {
            if (string.Equals(record.Scene, scene, StringComparison.OrdinalIgnoreCase))
                newest[record.Kind] = record;
        }

        var block = new JObject { ["scene"] = scene };
        if (newest.TryGetValue(KeepKind, out var keep))
        {
            var keepBlock = new JObject { ["x"] = keep.X, ["y"] = keep.Y, ["z"] = keep.Z };
            if (keep.Radius.HasValue)
                keepBlock["radius"] = keep.Radius.Value;
            block["keep"] = keepBlock;
        }

        var muster = new JObject();
        AddMuster(muster, "foot", newest, MusterFootKind);
        AddMuster(muster, "archers", newest, MusterArchersKind);
        if (muster.Count > 0)
            block["muster"] = muster;

        AddMuster(block, "sally", newest, SallyKind);
        var export = new LongDefeatExport(block.ToString(Formatting.Indented), Required.Where(k => !newest.ContainsKey(k)).ToList(), newest.ContainsKey(SallyKind));
        path = ExportPath(scene);
        Directory.CreateDirectory(_directory);
        File.WriteAllText(path, export.Json, new UTF8Encoding(false));
        return export;
    }

    private static void AddMuster(JObject muster, string name, Dictionary<string, LongDefeatRecord> newest, string kind)
    {
        if (!newest.TryGetValue(kind, out var r))
            return;
        muster[name] = new JObject { ["x"] = r.X, ["y"] = r.Y, ["z"] = r.Z, ["fx"] = r.Fx, ["fy"] = r.Fy };
    }

    private static LongDefeatRecord? Parse(string text)
    {
        try
        {
            if (JToken.Parse(text) is not JObject o)
                return null;
            var scene = (string?)o["scene"];
            var kind = (string?)o["kind"];
            if (string.IsNullOrEmpty(scene) || kind == null || !IsKind(kind))
                return null;
            if (!Number(o["x"], out var x) || !Number(o["y"], out var y) || !Number(o["z"], out var z)
                || !Number(o["fx"], out var fx) || !Number(o["fy"], out var fy))
                return null;
            float? radius = null;
            if (o["radius"] != null)
            {
                if (!Number(o["radius"], out var r))
                    return null;
                radius = r;
            }

            return new LongDefeatRecord(scene!, kind, x, y, z, fx, fy, radius);
        }
        catch (Exception)
        {
            return null;
        }
    }

    private static bool Number(JToken? token, out float value)
    {
        value = 0f;
        if (token == null || (token.Type != JTokenType.Float && token.Type != JTokenType.Integer))
            return false;
        value = (float)token;
        return FiniteFloatValidator.IsFinite(value);
    }

    private static string SafeName(string scene)
    {
        var sb = new StringBuilder(scene.Length);
        foreach (var c in scene)
            sb.Append(Array.IndexOf(Path.GetInvalidFileNameChars(), c) >= 0 ? '_' : c);
        return sb.Length == 0 ? "scene" : sb.ToString();
    }
}
