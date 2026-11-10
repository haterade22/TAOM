using System;
using System.Collections.Generic;
using Newtonsoft.Json.Linq;
using TAOM.Features.HeroDefence.Domain;
using TAOM.Features.LongDefeat;

namespace TAOM.Features.HeroDefence;

/// <summary>Reads hero_defence/rosters.json, which tools/generate_hero_defence_rosters.py writes. A bad row is skipped with a warning.</summary>
internal static class HeroDefenceRosterReader
{
    private const int MaxLevel = 63;

    public static IReadOnlyDictionary<string, IReadOnlyList<RosterRow>> Read(string? text, HeroDefenceConfigScope s)
    {
        var rosters = new Dictionary<string, IReadOnlyList<RosterRow>>(StringComparer.Ordinal);
        var root = s.OpenRoot(text, "is missing; no culture can appear");
        if (root == null)
            return rosters;

        foreach (var property in root.Properties())
        {
            if (property.Value is JArray rows)
                rosters[property.Name] = ReadRows(property.Name, rows, s);
            else
                s.Warn($"'{property.Name}' is not a list; skipped");
        }

        s.Loaded($"{rosters.Count} cultures");
        return rosters;
    }

    /// <summary>The closed set of creep classes. The name matches the enum, so a typo fails here and never takes a default.</summary>
    public static bool TryClass(string? text, out CreepClass cls)
    {
        switch (text)
        {
            case "Infantry":
                cls = CreepClass.Infantry;
                return true;
            case "Ranged":
                cls = CreepClass.Ranged;
                return true;
            case "Cavalry":
                cls = CreepClass.Cavalry;
                return true;
            case "HorseArcher":
                cls = CreepClass.HorseArcher;
                return true;
            default:
                cls = CreepClass.Infantry;
                return false;
        }
    }

    /// <summary>The rows of one culture. A repeated troop id keeps its first row, because two rows would give one troop two levels or classes.</summary>
    private static List<RosterRow> ReadRows(string culture, JArray rows, HeroDefenceConfigScope s)
    {
        var read = new List<RosterRow>();
        var ids = new HashSet<string>(StringComparer.Ordinal);
        foreach (var row in rows)
        {
            if (row is not JObject o || !TryRow(o, out var parsed))
                s.Warn($"'{culture}' has a bad row, skipped: {row.ToString(Newtonsoft.Json.Formatting.None)}");
            else if (!ids.Add(parsed.TroopId))
                s.Warn($"'{culture}' lists '{parsed.TroopId}' twice; kept the first row");
            else
                read.Add(parsed);
        }

        return read;
    }

    private static bool TryRow(JObject row, out RosterRow parsed)
    {
        parsed = null!;
        var id = LongDefeatConfigScope.Text(row["id"]);
        if (id == null || !LongDefeatConfigScope.TryInt(row["level"], 1, MaxLevel, out var level, out _) || !TryClass(LongDefeatConfigScope.Text(row["class"]), out var cls))
            return false;

        parsed = new RosterRow(id, level, cls);
        return true;
    }
}
