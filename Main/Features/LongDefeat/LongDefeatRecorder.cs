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

/// <summary>One line of <c>long_defeat_records.jsonl</c>: where the main agent stood and looked when a point was recorded. <paramref name="Tower"/> names the tower of a post or tower volley spot and is null for every other kind.</summary>
internal sealed record LongDefeatRecord(string Scene, string Kind, float X, float Y, float Z, float Fx, float Fy, float? Radius, string? Tower = null);

/// <summary>How many posts and tower volley spots are recorded for one tower of a scene.</summary>
internal sealed record LdTowerCount(string Id, int Posts, int TowerVolley);

/// <summary>
/// The block <c>taom.ld_export</c> writes for a scene, and the kinds of point still unrecorded. The sally, the tower points and the volley origins are optional: none is ever a missing piece,
/// and <paramref name="HasSally"/> says the block carries a sally. <see cref="Counts"/> is the number of records of each kind for the scene (every record, though the single-point kinds use the newest),
/// <see cref="Towers"/> the per-tower counts in the order the towers were first recorded.
/// </summary>
internal sealed record LongDefeatExport(string Json, IReadOnlyList<string> Missing, bool HasSally = false)
{
    public bool IsComplete => Missing.Count == 0;

    public IReadOnlyDictionary<string, int> Counts { get; init; } = new Dictionary<string, int>();

    public IReadOnlyList<LdTowerCount> Towers { get; init; } = Array.Empty<LdTowerCount>();

    public int CountOf(string kind) => Counts.TryGetValue(kind, out var n) ? n : 0;
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

    /// <summary>An archer's spot on a tower top, with the way he faces; recorded per tower.</summary>
    internal const string PostKind = "post";

    /// <summary>A spot behind a tower's parapet that its level 3 volley looses from; recorded per tower, no facing.</summary>
    internal const string TowerVolleyKind = "tower_volley";

    /// <summary>A spot behind the parapet along a wall that the Elven Volley looses from; no tower, no facing.</summary>
    internal const string VolleyKind = "volley";

    private const string RecordsFile = "long_defeat_records.jsonl";

    // The kinds a scene needs, and every kind that can be recorded: the sally is optional, so a scene without one is complete.
    private static readonly string[] Required = { KeepKind, MusterFootKind, MusterArchersKind };
    private static readonly string[] Kinds = { KeepKind, MusterFootKind, MusterArchersKind, SallyKind, PostKind, TowerVolleyKind, VolleyKind };

    private readonly string _directory;

    internal LongDefeatRecorder(string directory) => _directory = directory;

    internal static LongDefeatRecorder Default { get; } = new(LongDefeatRunRecords.PlayerFolder);

    internal string FilePath => Path.Combine(_directory, RecordsFile);

    internal string ExportPath(string scene) => Path.Combine(_directory, "long_defeat_" + SafeName(scene) + ".json");

    internal static bool IsKind(string kind) => Kinds.Contains(kind);

    /// <summary>Every kind that can be recorded, in the order the usage lists them.</summary>
    internal static IReadOnlyList<string> AllKinds => Kinds;

    /// <summary>True for the two kinds recorded per tower, which take a tower id after the kind.</summary>
    internal static bool IsTowerKind(string kind) => kind == PostKind || kind == TowerVolleyKind;

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
    internal string Append(string scene, string kind, float x, float y, float z, float fx, float fy, float? radius, string? tower = null)
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
        if (tower != null)
            line["tower"] = tower;
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

    /// <summary>
    /// Folds the newest record of each single-point kind for <paramref name="scene"/>, and every record of the tower and volley kinds in the order recorded,
    /// into the block to paste into fortresses.json, and writes it next to the records.
    /// </summary>
    internal LongDefeatExport Export(string scene, out string path)
    {
        var newest = new Dictionary<string, LongDefeatRecord>();
        var counts = Kinds.ToDictionary(k => k, _ => 0);
        var towerOrder = new List<string>();
        var posts = new Dictionary<string, List<LongDefeatRecord>>();
        var spots = new Dictionary<string, List<LongDefeatRecord>>();
        var origins = new List<LongDefeatRecord>();
        foreach (var record in ReadAll(out _))
        {
            if (!string.Equals(record.Scene, scene, StringComparison.OrdinalIgnoreCase))
                continue;
            counts[record.Kind]++;
            if (record.Kind == VolleyKind)
            {
                origins.Add(record);
            }
            else if (IsTowerKind(record.Kind))
            {
                if (!posts.ContainsKey(record.Tower!))
                {
                    towerOrder.Add(record.Tower!);
                    posts[record.Tower!] = new List<LongDefeatRecord>();
                    spots[record.Tower!] = new List<LongDefeatRecord>();
                }

                (record.Kind == PostKind ? posts : spots)[record.Tower!].Add(record);
            }
            else
            {
                newest[record.Kind] = record;
            }
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
        if (towerOrder.Count > 0)
            block["towers"] = new JArray(towerOrder.Select(id => TowerBlock(id, posts[id], spots[id])));
        if (origins.Count > 0)
            block["volley"] = new JObject { ["origins"] = new JArray(origins.Select(Spot)) };

        var export = new LongDefeatExport(block.ToString(Formatting.Indented), Required.Where(k => !newest.ContainsKey(k)).ToList(), newest.ContainsKey(SallyKind))
        {
            Counts = counts,
            Towers = towerOrder.Select(id => new LdTowerCount(id, posts[id].Count, spots[id].Count)).ToList(),
        };
        path = ExportPath(scene);
        Directory.CreateDirectory(_directory);
        File.WriteAllText(path, export.Json, new UTF8Encoding(false));
        return export;
    }

    /// <summary>
    /// Removes the records of <paramref name="kind"/> for <paramref name="scene"/> from the file, only those of <paramref name="tower"/> when one is given, and returns how many went.
    /// A line that is not a record stays where it was, and the file is untouched when nothing matches.
    /// </summary>
    internal int Clear(string scene, string kind, string? tower)
    {
        if (!File.Exists(FilePath))
            return 0;

        var kept = new List<string>();
        var removed = 0;
        foreach (var text in File.ReadAllLines(FilePath))
        {
            var record = string.IsNullOrWhiteSpace(text) ? null : Parse(text);
            if (record != null && record.Kind == kind && string.Equals(record.Scene, scene, StringComparison.OrdinalIgnoreCase) && (tower == null || record.Tower == tower))
                removed++;
            else
                kept.Add(text);
        }

        if (removed == 0)
            return 0;

        // Written beside the file and swapped in, so an interrupted write never leaves the records half there.
        var temp = FilePath + ".tmp";
        File.WriteAllText(temp, string.Concat(kept.Select(l => l + "\n")), new UTF8Encoding(false));
        File.Replace(temp, FilePath, null);
        return removed;
    }

    private static JObject Spot(LongDefeatRecord r) => new() { ["x"] = r.X, ["y"] = r.Y, ["z"] = r.Z };

    private static JObject TowerBlock(string id, List<LongDefeatRecord> posts, List<LongDefeatRecord> spots)
    {
        var tower = new JObject { ["id"] = id };
        if (posts.Count > 0)
            tower["posts"] = new JArray(posts.Select(r => new JObject { ["x"] = r.X, ["y"] = r.Y, ["z"] = r.Z, ["fx"] = r.Fx, ["fy"] = r.Fy }));
        if (spots.Count > 0)
            tower["volley"] = new JArray(spots.Select(Spot));
        return tower;
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

            string? tower = null;
            if (IsTowerKind(kind))
            {
                tower = o["tower"]?.Type == JTokenType.String ? (string?)o["tower"] : null;
                if (tower == null || !FortressesReader.IsTowerId(tower))
                    return null;
            }

            return new LongDefeatRecord(scene!, kind, x, y, z, fx, fy, radius, tower);
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
