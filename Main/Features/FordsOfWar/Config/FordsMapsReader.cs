using System;
using System.Collections.Generic;
using System.Linq;
using Newtonsoft.Json.Linq;
using TAOM.Features.FordsOfWar.Domain;

namespace TAOM.Features.FordsOfWar;

/// <summary>
/// maps.json: a <c>defaults</c> block (layout options, tower and keep prefab, camp) and an ordered <c>scenes</c> array, where a
/// scene can override each block. A scene with no id, a duplicate id or an unknown layout mode is skipped with one warning; a
/// bad option, prefab or camp value reverts to the default for that block (the file's, else the built-in) with one warning and
/// keeps the scene. The first scene is the default map.
/// </summary>
internal static class FordsMapsReader
{
    private sealed record Defaults(LayoutOptions Options, TowerPrefab Tower, TowerPrefab Keep, CampDefaults Camp);

    public static IReadOnlyList<MapEntry> Read(string? text, FordsConfigScope s)
    {
        var maps = new List<MapEntry>();
        var root = s.OpenRoot(text, "was not found or could not be read; no maps");
        if (root == null)
            return maps;

        if (!(root["scenes"] is JArray scenes))
        {
            s.Warn("'scenes' is missing or not an array; no maps");
            return maps;
        }

        var defaults = ReadDefaults(s.Group(root, "defaults", ""), s);
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        for (var i = 0; i < scenes.Count; i++)
        {
            if (!(scenes[i] is JObject scene))
            {
                s.Warn($"scene #{i} skipped (it is not an object)");
                continue;
            }

            var id = FordsConfigScope.Text(scene["scene"]);
            if (id == null)
            {
                s.Warn($"scene #{i} skipped (it has no 'scene' id)");
                continue;
            }

            if (!seen.Add(id))
            {
                s.Warn($"scene '{id}' skipped (duplicate id, the first one is kept)");
                continue;
            }

            if (!TryReadMode(scene["layout"], out var mode, out var why))
            {
                s.Warn($"scene '{id}' skipped ({why})");
                continue;
            }

            maps.Add(ReadScene(id, mode, scene, defaults, s));
        }

        s.Info($"loaded, {maps.Count} map(s)");
        return maps;
    }

    private static Defaults ReadDefaults(JObject? block, FordsConfigScope s)
    {
        const string label = "defaults";
        return new Defaults(
            ReadOptions(s.Group(block, "options", label), new LayoutOptions(), label, s),
            ReadPrefab(s.Group(block, "towerPrefab", label), TowerPrefab.DefaultTower, label + ".towerPrefab", s),
            ReadPrefab(s.Group(block, "keepPrefab", label), TowerPrefab.DefaultKeep, label + ".keepPrefab", s),
            ReadCamp(s.Group(block, "camp", label), CampDefaults.Default, label + ".camp", s));
    }

    private static MapEntry ReadScene(string id, MapLayoutMode mode, JObject scene, Defaults d, FordsConfigScope s)
    {
        var label = $"scenes[{id}]";
        return new MapEntry(
            id,
            FordsConfigScope.Text(scene["name"]) ?? id,
            mode,
            ReadOptions(s.Group(scene, "options", label), d.Options, label, s),
            ReadPrefab(s.Group(scene, "towerPrefab", label), d.Tower, label + ".towerPrefab", s),
            ReadPrefab(s.Group(scene, "keepPrefab", label), d.Keep, label + ".keepPrefab", s),
            ReadCamp(s.Group(scene, "camp", label), d.Camp, label + ".camp", s));
    }

    private static bool TryReadMode(JToken? token, out MapLayoutMode mode, out string why)
    {
        mode = MapLayoutMode.Generated;
        why = "";
        if (token == null)
            return true;

        var text = FordsConfigScope.Text(token);
        if (text != null && text.Equals("generated", StringComparison.OrdinalIgnoreCase))
            return true;
        if (text != null && text.Equals("tags", StringComparison.OrdinalIgnoreCase))
        {
            mode = MapLayoutMode.Tags;
            return true;
        }

        why = $"layout '{text ?? token.ToString()}' is not 'generated' or 'tags'";
        return false;
    }

    private static LayoutOptions ReadOptions(JObject? o, LayoutOptions baseline, string label, FordsConfigScope s)
    {
        if (o == null)
            return baseline;

        var path = label + ".options";
        return new LayoutOptions(
            s.Int(o, path, "laneCount", baseline.LaneCount, 3, LaneStreams.MaxLanes),
            s.Float(o, path, "laneSpread", baseline.LaneSpread, 5f, 300f),
            s.Float(o, path, "waypointStep", baseline.WaypointStep, 2f, 100f),
            ReadFractions(o, path, baseline.TowerFractions, s),
            s.Float(o, path, "keepInset", baseline.KeepInset, 0f, 0.2f));
    }

    private static IReadOnlyList<float> ReadFractions(JObject o, string path, IReadOnlyList<float> fallback, FordsConfigScope s)
    {
        if (!o.TryGetValue("towerFractions", out var token))
            return fallback;

        var key = path + ".towerFractions";
        if (!(token is JArray array) || array.Count < 1 || array.Count > 4)
        {
            s.Warn($"'{key}' is not an array of 1 to 4 numbers; using the default");
            return fallback;
        }

        var result = new List<float>(array.Count);
        foreach (var item in array)
        {
            if (!FordsConfigScope.TryFloat(item, 0.01f, 0.49f, out var value, out var why))
            {
                s.Warn($"'{key}' has an entry that {why} (each is a fraction of the lane below the middle); using the default");
                return fallback;
            }

            result.Add(value);
        }

        return result;
    }

    private static TowerPrefab ReadPrefab(JObject? o, TowerPrefab baseline, string path, FordsConfigScope s)
    {
        if (o == null)
            return baseline;

        var prefab = baseline.Prefab;
        if (o.TryGetValue("prefab", out var token))
        {
            var text = FordsConfigScope.Text(token);
            if (text == null)
                s.Warn($"'{path}.prefab' is not a prefab name; using the default '{baseline.Prefab}'");
            else
                prefab = text;
        }

        return new TowerPrefab(prefab, s.Float(o, path, "scale", baseline.Scale, 0.1f, 10f));
    }

    private static CampDefaults ReadCamp(JObject? o, CampDefaults baseline, string path, FordsConfigScope s)
    {
        if (o == null)
            return baseline;

        var troop = baseline.TroopId;
        if (o.TryGetValue("troopId", out var token))
        {
            var text = FordsConfigScope.Text(token);
            if (text == null)
                s.Warn($"'{path}.troopId' is not a troop id; using the default '{baseline.TroopId}'");
            else
                troop = text;
        }

        return new CampDefaults(
            troop,
            s.Int(o, path, "count", baseline.Count, 0, 50),
            s.Int(o, path, "reward", baseline.Reward, 0, 1_000_000));
    }
}
