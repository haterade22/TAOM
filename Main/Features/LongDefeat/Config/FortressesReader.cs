using System;
using System.Collections.Generic;
using Newtonsoft.Json.Linq;
using TAOM.Features.LongDefeat.Domain;

namespace TAOM.Features.LongDefeat;

/// <summary>
/// fortresses.json: a fortress is skipped (one warning naming it) when it lacks an id, scene, either culture or the player troop,
/// repeats an earlier id, names a roster or shop that did not load, or has no valid garrison row. A bad garrison row is skipped.
/// A bad <c>keep</c> block is dropped with one warning and the fortress stays, since the director then uses the scene's own frame.
/// A bad <c>muster</c> block is dropped the same way, whole, and defenders then spawn at the scene's own frames.
/// An optional <c>sally</c> block (the muster point's shape: x, y, z, fx, fy) is dropped with one warning when bad, and a mounted class then rides from the start with no sally.
/// The optional <c>towers</c> and <c>volley</c> blocks are read in FortressesReader.Points.cs; a fortress row with neither loads exactly as before.
/// </summary>
internal static partial class FortressesReader
{
    /// <summary>The farthest a recorded keep coordinate may lie from the origin; the recorder refuses a point beyond it.</summary>
    internal const float MaxCoordinate = 100_000f;

    internal const float KeepRadiusMin = 2f;
    internal const float KeepRadiusMax = 50f;

    // The scene name goes to the engine's native scene loader (config-providers.md item 8); a name outside this set cannot be one of its scenes.
    internal const int MaxSceneLength = 128;

    public static IReadOnlyList<FortressEntry> Read(string? text, WavesConfig waves, IReadOnlyDictionary<string, ShopPlan> shops, LongDefeatConfigScope s)
    {
        var result = new List<FortressEntry>();
        var root = s.OpenRoot(text, "was not found or could not be read; no fortresses");
        if (root == null)
            return result;

        if (!(root["fortresses"] is JArray array))
        {
            s.Warn("'fortresses' is missing or not an array; no fortresses");
            return result;
        }

        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        for (var i = 0; i < array.Count; i++)
        {
            var entry = ReadFortress(array[i] as JObject, i, waves, shops, seen, s);
            if (entry != null)
                result.Add(entry);
        }

        s.Loaded($"{result.Count} fortress(es)");
        return result;
    }

    private static FortressEntry? ReadFortress(JObject? o, int index, WavesConfig waves, IReadOnlyDictionary<string, ShopPlan> shops,
        HashSet<string> seen, LongDefeatConfigScope s)
    {
        var id = LongDefeatConfigScope.Text(o?["id"]);
        if (o == null || id == null)
        {
            s.Warn($"fortress #{index} skipped (it has no 'id')");
            return null;
        }

        if (seen.Contains(id))
        {
            s.Warn($"fortress '{id}' skipped (duplicate id, the first one is kept)");
            return null;
        }

        var scene = LongDefeatConfigScope.Text(o["scene"]);
        var defender = LongDefeatConfigScope.Text(o["defenderCulture"]);
        var attacker = LongDefeatConfigScope.Text(o["attackerCulture"]);
        var player = LongDefeatConfigScope.Text(o["playerTroop"]);
        if (scene == null || defender == null || attacker == null || player == null)
        {
            s.Warn($"fortress '{id}' skipped (it needs 'scene', 'defenderCulture', 'attackerCulture' and 'playerTroop')");
            return null;
        }

        if (!IsSceneName(scene))
        {
            s.Warn($"fortress '{id}' skipped (scene must be at most {MaxSceneLength} letters, digits or underscores)");
            return null;
        }

        var rosterId = LongDefeatConfigScope.Text(o["waves"]) ?? "";
        if (!waves.Rosters.TryGetValue(rosterId, out var roster))
        {
            s.Warn($"fortress '{id}' skipped (waves '{rosterId}' does not name a roster with a valid wave)");
            return null;
        }

        var shopId = LongDefeatConfigScope.Text(o["shop"]) ?? "";
        if (!shops.TryGetValue(shopId, out var shop))
        {
            s.Warn($"fortress '{id}' skipped (shop '{shopId}' does not name a shop)");
            return null;
        }

        var garrison = ReadGarrison(o["garrison"] as JArray, id, s);
        if (garrison.Count == 0)
        {
            s.Warn($"fortress '{id}' skipped (it has no valid garrison row)");
            return null;
        }

        seen.Add(id);
        var towers = ReadTowers(o["towers"], id, s);
        WarnShortTowers(towers, shop, id, s);
        return new FortressEntry(id, LongDefeatConfigScope.Text(o["name"]) ?? id, scene, defender, attacker, player, garrison, roster, shop,
            ReadKeep(o["keep"], id, waves.Rules.KeepRadius, s), ReadMuster(o["muster"], id, s), ReadSally(o["sally"], id, s),
            towers, ReadVolley(o["volley"], id, s));
    }

    private static bool IsSceneName(string scene)
    {
        if (scene.Length > MaxSceneLength)
            return false;
        foreach (var c in scene)
        {
            var ascii = c < 128 && char.IsLetterOrDigit(c);
            if (!ascii && c != '_')
                return false;
        }

        return true;
    }

    private static List<GarrisonRow> ReadGarrison(JArray? array, string fortress, LongDefeatConfigScope s)
    {
        var rows = new List<GarrisonRow>();
        if (array == null)
            return rows;

        for (var i = 0; i < array.Count; i++)
        {
            var label = $"fortress '{fortress}' garrison[{i}]";
            var row = array[i] as JObject;
            var troop = LongDefeatConfigScope.Text(row?["troop"]);
            if (row == null || troop == null)
            {
                s.Warn($"{label}.troop is missing; row skipped");
            }
            else if (!LongDefeatConfigScope.TrySlot(row["class"], out var slot, out var why))
            {
                s.Warn($"{label}.class {why}; row skipped");
            }
            else if (!LongDefeatConfigScope.TryInt(row["count"], 1, 400, out var count, out why))
            {
                s.Warn($"{label}.count {why}; row skipped");
            }
            else
            {
                rows.Add(new GarrisonRow(troop, slot, count));
            }
        }

        return rows;
    }

    private static KeepPoint? ReadKeep(JToken? token, string fortress, float defaultRadius, LongDefeatConfigScope s)
    {
        if (token == null || token.Type == JTokenType.Null)
            return null;

        var label = $"fortress '{fortress}' keep";
        if (!(token is JObject o))
        {
            s.Warn($"{label} is not an object; the fortress uses the scene's own frame");
            return null;
        }

        var axes = new float[3];
        var names = new[] { "x", "y", "z" };
        for (var i = 0; i < 3; i++)
        {
            if (!LongDefeatConfigScope.TryFloat(o[names[i]], -MaxCoordinate, MaxCoordinate, out axes[i], out var why))
            {
                s.Warn($"{label}.{names[i]} {why}; the fortress uses the scene's own frame");
                return null;
            }
        }

        var radius = defaultRadius;
        if (o.TryGetValue("radius", out var radiusToken) && !LongDefeatConfigScope.TryFloat(radiusToken, KeepRadiusMin, KeepRadiusMax, out radius, out var radiusWhy))
        {
            s.Warn($"{label}.radius {radiusWhy}; the fortress uses the scene's own frame");
            return null;
        }

        return new KeepPoint(axes[0], axes[1], axes[2], radius);
    }

    // The block LongDefeatRecorder.Export writes: {"foot": {x,y,z,fx,fy}, "archers": {...}}, each point optional. One bad value reverts the whole block with one warning.
    private static MusterPoints? ReadMuster(JToken? token, string fortress, LongDefeatConfigScope s)
    {
        if (token == null || token.Type == JTokenType.Null)
            return null;

        var label = $"fortress '{fortress}' muster";
        if (!(token is JObject o))
        {
            s.Warn($"{label} is not an object; defenders use the scene's own frames");
            return null;
        }

        if (!TryReadMusterFrame(o["foot"], label + ".foot", s, out var foot, MusterFallback) || !TryReadMusterFrame(o["archers"], label + ".archers", s, out var archers, MusterFallback))
            return null;

        return foot == null && archers == null ? null : new MusterPoints(foot, archers);
    }

    private const string MusterFallback = "defenders use the scene's own frames";
    private const string SallyFallback = "a mounted class rides from the start with no sally";

    // The sally point, recorded as the muster point is: {x,y,z,fx,fy}. Absent or null is no sally and no warning; a bad one is one warning and no sally, and the fortress and its muster stay.
    private static MusterFrame? ReadSally(JToken? token, string fortress, LongDefeatConfigScope s)
    {
        if (token == null || token.Type == JTokenType.Null)
            return null;

        return TryReadMusterFrame(token, $"fortress '{fortress}' sally", s, out var frame, SallyFallback) ? frame : null;
    }

    // True with a null frame when the point is absent; false after one warning when it is present and bad.
    private static bool TryReadMusterFrame(JToken? token, string label, LongDefeatConfigScope s, out MusterFrame? frame, string fallback)
    {
        frame = null;
        if (token == null || token.Type == JTokenType.Null)
            return true;

        if (!(token is JObject o))
        {
            s.Warn($"{label} is not an object; {fallback}");
            return false;
        }

        var values = new float[5];
        var names = new[] { "x", "y", "z", "fx", "fy" };
        for (var i = 0; i < values.Length; i++)
        {
            if (!LongDefeatConfigScope.TryFloat(o[names[i]], -MaxCoordinate, MaxCoordinate, out values[i], out var why))
            {
                s.Warn($"{label}.{names[i]} {why}; {fallback}");
                return false;
            }
        }

        if (!MusterFrame.TryCreate(values[0], values[1], values[2], values[3], values[4], out var made))
        {
            s.Warn($"{label}.fx and fy do not give a ground direction (a length between 0.5 and 1.5 is expected); {fallback}");
            return false;
        }

        frame = made;
        return true;
    }
}
