using System;
using System.Collections.Generic;
using System.IO;
using Newtonsoft.Json.Linq;
using TAOM.Features.LongDefeat.Domain;

namespace TAOM.Features.LongDefeat;

/// <summary>
/// hero_lines.json (#1569): <c>heroes</c> maps a hero id (a set id, <c>LocationsReader.IsSetId</c>) to its lines, each line an array of takes, each take a path of an
/// OGG file under the module's <c>ModuleSounds</c> folder. A take reaches a native sound call, so it is bounded: a string of at most <see cref="MaxTakeLength"/> characters from
/// <c>A-Z a-z 0-9 _ . / \ -</c>, not rooted, with no <c>..</c> segment, ending in <c>.ogg</c>, and naming a file <c>resolveSound</c> finds (config-providers.md rule 8). A bad take,
/// line or hero is dropped with one warning naming its key; a line with no valid take and a hero with no valid line go with it. Null text is no lines and no warning: the
/// heroes are then silent.
/// </summary>
internal static class HeroLinesReader
{
    internal const int MaxTakeLength = 128;

    public static HeroLinePlan Read(string? json, LongDefeatConfigScope s, Func<string, string?> resolveSound)
    {
        if (json == null)
            return HeroLinePlan.None;

        var root = s.OpenRoot(json, "");
        if (root == null)
            return HeroLinePlan.None;

        var heroes = new Dictionary<string, IReadOnlyList<IReadOnlyList<string>>>(StringComparer.Ordinal);
        var token = root["heroes"];
        if (token != null && !(token is JObject))
            s.Warn("'heroes' is not an object; no hero speaks");
        else if (token is JObject table)
        {
            foreach (var entry in table)
            {
                var lines = ReadHero(entry.Key, entry.Value, s, resolveSound);
                if (lines != null)
                    heroes[entry.Key] = lines;
            }
        }

        s.Loaded($"{heroes.Count} hero(es) with lines");
        return heroes.Count == 0 ? HeroLinePlan.None : new HeroLinePlan(heroes);
    }

    private static IReadOnlyList<IReadOnlyList<string>>? ReadHero(string id, JToken? value, LongDefeatConfigScope s, Func<string, string?> resolveSound)
    {
        var label = "heroes." + id;
        if (!LocationsReader.IsSetId(id))
        {
            s.Warn($"'{label}' is not a set id (lowercase letters, digits and underscores); hero dropped");
            return null;
        }

        if (!(value is JArray rows) || rows.Count == 0)
        {
            s.Warn($"'{label}' is not a non-empty array of lines; hero dropped");
            return null;
        }

        var lines = new List<IReadOnlyList<string>>();
        for (var i = 0; i < rows.Count; i++)
        {
            var line = ReadLine(rows[i], $"{label}[{i}]", s, resolveSound);
            if (line != null)
                lines.Add(line);
        }

        if (lines.Count == 0)
            s.Warn($"'{label}' has no valid line; hero dropped");
        return lines.Count == 0 ? null : lines;
    }

    private static IReadOnlyList<string>? ReadLine(JToken line, string label, LongDefeatConfigScope s, Func<string, string?> resolveSound)
    {
        if (!(line is JArray takes) || takes.Count == 0)
        {
            s.Warn($"'{label}' is not a non-empty array of takes; line dropped");
            return null;
        }

        var paths = new List<string>();
        for (var i = 0; i < takes.Count; i++)
        {
            var take = takes[i];
            var where = $"{label}[{i}]";
            var text = take.Type == JTokenType.String ? (string?)take ?? "" : null;
            var why = text == null ? "is not a string" : Check(text);
            if (why.Length > 0)
            {
                s.Warn($"'{where}' {why}; take dropped");
                continue;
            }

            var full = resolveSound(text!);
            if (full == null)
            {
                s.Warn($"'{where}' names no file under ModuleSounds; take dropped");
                continue;
            }

            paths.Add(full);
        }

        if (paths.Count == 0)
            s.Warn($"'{label}' has no valid take; line dropped");
        return paths.Count == 0 ? null : paths;
    }

    private static string Check(string text)
    {
        if (text.Length > MaxTakeLength)
            return $"is longer than {MaxTakeLength} characters";
        foreach (var c in text)
        {
            if (!IsPathChar(c))
                return "holds a character outside A-Z a-z 0-9 _ . / \\ -";
        }

        if (Path.IsPathRooted(text))
            return "is rooted, not relative to ModuleSounds";
        foreach (var part in text.Split('/', '\\'))
        {
            if (part == "..")
                return "holds a '..' segment";
        }

        return text.EndsWith(".ogg", StringComparison.OrdinalIgnoreCase) ? "" : "is not an .ogg file";
    }

    private static bool IsPathChar(char c) =>
        (c >= 'a' && c <= 'z') || (c >= 'A' && c <= 'Z') || (c >= '0' && c <= '9') || c == '_' || c == '.' || c == '/' || c == '\\' || c == '-';
}
