using System;
using System.Collections.Generic;
using System.Globalization;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using TAOM.Core.Validation;

namespace TAOM.Features.StagedSiege;

/// <summary>One recorder line: a point, a defender frame or the detachment frame of one stage of one scene.</summary>
internal sealed class StagedSiegeRecord
{
    public string Scene = "";
    public char Stage;
    public string Kind = "";
    public float X;
    public float Y;
    public float Z;
    public float Fx;
    public float Fy;
    public float? Radius;
    public DateTime Utc;
}

/// <summary>The scene block built from recorder lines, plus the pieces still missing from it.</summary>
internal sealed class StagedSiegeFragment
{
    public StagedSiegeFragment(string json, IReadOnlyList<string> missing)
    {
        Json = json;
        Missing = missing;
    }

    /// <summary>A complete <c>staged_siege.json</c> document holding just this scene, ready to merge into the shipped file.</summary>
    public string Json { get; }

    /// <summary>The pieces with no record, such as "point B" or "defender C"; empty when the scene is complete.</summary>
    public IReadOnlyList<string> Missing { get; }

    public bool IsComplete => Missing.Count == 0;
}

/// <summary>
/// Pure half of the recorder: parses one JSONL line into a record and folds many records into the shipped JSON shape. The newest
/// record per stage and kind wins (the later line on a tie), so re-recording a point replaces it. Nothing here touches the
/// engine or the disk.
/// </summary>
internal static class StagedSiegeRecordFragment
{
    private const string Point = "point";
    private const string Defender = "defender";
    private const string Detachment = "detachment";

    /// <summary>The record for one line, or null when the line is not a usable record. Never throws.</summary>
    internal static StagedSiegeRecord? ParseLine(string? line)
    {
        if (string.IsNullOrWhiteSpace(line))
            return null;
        try
        {
            if (!(JToken.Parse(line) is JObject o))
                return null;

            var scene = o["scene"]?.Type == JTokenType.String ? o["scene"]!.Value<string>() : null;
            var stageText = o["stage"]?.Type == JTokenType.String ? o["stage"]!.Value<string>()!.Trim().ToUpperInvariant() : null;
            var kind = o["kind"]?.Type == JTokenType.String ? o["kind"]!.Value<string>()!.Trim().ToLowerInvariant() : null;
            if (string.IsNullOrWhiteSpace(scene) || stageText == null || stageText.Length != 1 || stageText[0] < 'A' || stageText[0] > 'D')
                return null;
            var stage = stageText[0];
            if (!KindFitsStage(kind, stage))
                return null;

            var record = new StagedSiegeRecord { Scene = scene!, Stage = stage, Kind = kind! };
            if (!TryNumber(o, "x", out record.X) || !TryNumber(o, "y", out record.Y) || !TryNumber(o, "z", out record.Z))
                return null;

            var needsFacing = kind != Point;
            var hasFacing = TryNumber(o, "fx", out record.Fx) & TryNumber(o, "fy", out record.Fy);
            if (needsFacing && !hasFacing)
                return null;

            if (o.ContainsKey("radius"))
            {
                if (!TryNumber(o, "radius", out var radius))
                    return null;
                record.Radius = radius;
            }

            if (o["utc"] != null && DateTime.TryParse(o["utc"]!.ToString(), CultureInfo.InvariantCulture,
                    DateTimeStyles.AdjustToUniversal | DateTimeStyles.AssumeUniversal, out var utc))
                record.Utc = utc;

            return record;
        }
        catch (Exception)
        {
            return null;
        }
    }

    /// <summary>A point exists at every stage, a defender frame at B to D, the detachment at A only.</summary>
    private static bool KindFitsStage(string? kind, char stage) => kind switch
    {
        Point => true,
        Defender => stage != 'A',
        Detachment => stage == 'A',
        _ => false,
    };

    private static bool TryNumber(JObject o, string key, out float value)
    {
        value = 0f;
        var token = o[key];
        if (token == null || (token.Type != JTokenType.Float && token.Type != JTokenType.Integer))
            return false;
        var d = token.Value<double>();
        if (!FiniteFloatValidator.IsFinite(d))
            return false;
        value = (float)d;
        return true;
    }

    /// <summary>Folds the records of <paramref name="scene"/> into one scene block and lists what is missing.</summary>
    internal static StagedSiegeFragment Build(string scene, IEnumerable<StagedSiegeRecord?> records)
    {
        var newest = new Dictionary<string, StagedSiegeRecord>();
        foreach (var record in records)
        {
            if (record == null || !string.Equals(record.Scene, scene, StringComparison.OrdinalIgnoreCase))
                continue;
            var key = record.Kind + record.Stage;
            // A tie goes to the later line: re-recording within one clock tick still replaces.
            if (!newest.TryGetValue(key, out var held) || record.Utc >= held.Utc)
                newest[key] = record;
        }

        var missing = new List<string>();
        var points = new JObject();
        var defenders = new JObject();
        JObject? detachment = null;

        foreach (var letter in new[] { 'A', 'B', 'C', 'D' })
        {
            if (newest.TryGetValue(Point + letter, out var p))
                points[letter.ToString()] = PointJson(p);
            else
                missing.Add(Point + " " + letter);
        }

        if (newest.TryGetValue(Detachment + 'A', out var d))
            detachment = FrameJson(d);
        else
            missing.Add(Detachment + " A");

        foreach (var letter in new[] { 'B', 'C', 'D' })
        {
            if (newest.TryGetValue(Defender + letter, out var f))
                defenders[letter.ToString()] = FrameJson(f);
            else
                missing.Add(Defender + " " + letter);
        }

        var block = new JObject { ["points"] = points };
        if (detachment != null)
            block["detachment"] = detachment;
        block["defenders"] = defenders;
        var document = new JObject { ["scenes"] = new JObject { [scene] = block } };
        return new StagedSiegeFragment(document.ToString(Formatting.Indented), missing);
    }

    private static JObject PointJson(StagedSiegeRecord r)
    {
        var o = new JObject { ["x"] = r.X, ["y"] = r.Y, ["z"] = r.Z };
        if (r.Radius.HasValue)
            o["radius"] = r.Radius.Value;
        return o;
    }

    private static JObject FrameJson(StagedSiegeRecord r) =>
        new() { ["x"] = r.X, ["y"] = r.Y, ["z"] = r.Z, ["fx"] = r.Fx, ["fy"] = r.Fy };
}
