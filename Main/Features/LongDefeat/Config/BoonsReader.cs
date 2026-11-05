using System;
using System.Collections.Generic;
using Newtonsoft.Json.Linq;
using TAOM.Features.LongDefeat.Domain;

namespace TAOM.Features.LongDefeat;

/// <summary>
/// boons.json (phase2-plan.md section 3): a <c>legendWeight</c> and the <c>boons</c> rows in file order. A bad weight keeps the shipped 1, a duplicate id
/// skips the later row, and a row with no known category, no name or text, or no valid effect is skipped. An effect key outside the closed vocabulary of
/// <see cref="BoonKeys"/>, or a value outside its range (a whole key needs a JSON integer), skips that effect. A drawback uses the same keys, never
/// <c>gold</c> or <c>affixCount</c>, and needs its own <c>drawbackText</c>, so a cost is never hidden from the player. A stat, damage or health key also needs a
/// <c>target</c> (infantry, archers, cavalry or player); without a valid one that effect is skipped.
/// </summary>
internal static class BoonsReader
{
    private const int MaxLegendWeight = 100;

    private static readonly IReadOnlyDictionary<string, BoonCategory> Categories = new Dictionary<string, BoonCategory>(StringComparer.Ordinal)
    {
        ["class"] = BoonCategory.Class,
        ["infantry"] = BoonCategory.Infantry,
        ["archers"] = BoonCategory.Archers,
        ["cavalry"] = BoonCategory.Cavalry,
        ["fortress"] = BoonCategory.Fortress,
        ["warchest"] = BoonCategory.WarChest,
        ["legend"] = BoonCategory.Legend,
    };

    public static BoonPlan Read(string? text, LongDefeatConfigScope s)
    {
        var root = s.OpenRoot(text, "was not found or could not be read; no boon will be offered");
        if (root == null)
            return BoonPlan.None;

        var legendWeight = s.Int(root, "", "legendWeight", 1, 1, MaxLegendWeight);
        var rows = new List<BoonRow>();
        var seen = new HashSet<string>(StringComparer.Ordinal);
        if (root["boons"] is JArray array)
        {
            for (var i = 0; i < array.Count; i++)
            {
                var row = ReadRow(array[i] as JObject, $"boons[{i}]", seen, s);
                if (row != null)
                    rows.Add(row);
            }
        }
        else
        {
            s.Warn("'boons' is missing or not an array; no boon will be offered");
        }

        s.Loaded($"{rows.Count} boon(s)");
        return new BoonPlan(rows, legendWeight);
    }

    private static BoonRow? ReadRow(JObject? o, string label, HashSet<string> seen, LongDefeatConfigScope s)
    {
        var id = LongDefeatConfigScope.Text(o?["id"]);
        var name = LongDefeatConfigScope.Text(o?["name"]);
        var text = LongDefeatConfigScope.Text(o?["text"]);
        if (o == null || id == null || name == null || text == null)
        {
            s.Warn($"'{label}' needs an 'id', a 'name' and a 'text'; boon skipped");
            return null;
        }

        if (seen.Contains(id))
        {
            s.Warn($"'{label}' repeats the id '{id}'; the first boon is kept");
            return null;
        }

        var categoryText = LongDefeatConfigScope.Text(o["category"]);
        if (categoryText == null || !Categories.TryGetValue(categoryText, out var category))
        {
            s.Warn($"'{label}.category' is '{categoryText ?? "missing"}', not one of {string.Join(", ", Categories.Keys)}; boon '{id}' skipped");
            return null;
        }

        var classId = "";
        if (category == BoonCategory.Class)
        {
            classId = LongDefeatConfigScope.Text(o["class"]) ?? "";
            if (classId.Length == 0)
            {
                s.Warn($"'{label}' is a class boon with no 'class'; boon '{id}' skipped");
                return null;
            }
        }

        var effects = ReadEffects(o["effects"], $"{label}.effects", false, s);
        if (effects.Count == 0)
        {
            s.Warn($"'{label}.effects' holds no valid effect; boon '{id}' skipped");
            return null;
        }

        var drawback = ReadEffects(o["drawback"], $"{label}.drawback", true, s);
        var drawbackText = LongDefeatConfigScope.Text(o["drawbackText"]) ?? "";
        if (drawback.Count > 0 && drawbackText.Length == 0)
        {
            s.Warn($"'{label}' has a drawback but no 'drawbackText' to show it; boon '{id}' skipped");
            return null;
        }

        seen.Add(id);
        return new BoonRow(id, category, name, text, effects) { ClassId = classId, Drawback = drawback, DrawbackText = drawback.Count > 0 ? drawbackText : "" };
    }

    private static IReadOnlyList<BoonEffect> ReadEffects(JToken? token, string label, bool drawback, LongDefeatConfigScope s)
    {
        var effects = new List<BoonEffect>();
        if (!(token is JArray array))
        {
            if (token != null)
                s.Warn($"'{label}' is not an array; no effect read from it");
            return effects;
        }

        for (var i = 0; i < array.Count; i++)
        {
            var path = $"{label}[{i}]";
            var key = LongDefeatConfigScope.Text((array[i] as JObject)?["key"]);
            if (key == null || !BoonKeys.Rules.TryGetValue(key, out var rule))
            {
                s.Warn($"'{path}.key' is '{key ?? "missing"}', not one of the boon keys ({string.Join(", ", BoonKeys.Rules.Keys)}); effect skipped");
                continue;
            }

            if (drawback && !BoonKeys.AllowedAsDrawback(key))
            {
                s.Warn($"'{path}' uses '{key}', which a drawback may not; effect skipped");
                continue;
            }

            if (!TryNumber(rule, ((JObject)array[i])["value"], out var number, out var why))
            {
                s.Warn($"'{path}.value' {why}; effect '{key}' skipped");
                continue;
            }

            BoonTarget? target = null;
            if (BoonKeys.IsTargeted(key))
            {
                if (!BoonTargets.TryParse(LongDefeatConfigScope.Text(((JObject)array[i])["target"]), out var parsed))
                {
                    s.Warn($"'{path}.target' is missing or not one of infantry, archers, cavalry, player; effect '{key}' skipped");
                    continue;
                }

                target = parsed;
            }

            effects.Add(new BoonEffect(key, number, target));
        }

        return effects;
    }

    private static bool TryNumber(BoonKeyRule rule, JToken? value, out float number, out string why)
    {
        if (!rule.Whole)
            return LongDefeatConfigScope.TryFloat(value, rule.Min, rule.Max, out number, out why);

        var ok = LongDefeatConfigScope.TryInt(value, (int)rule.Min, (int)rule.Max, out var whole, out why);
        number = whole;
        return ok;
    }
}
