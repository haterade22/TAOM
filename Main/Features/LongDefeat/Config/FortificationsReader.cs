using System;
using System.Collections.Generic;
using System.Linq;
using Newtonsoft.Json.Linq;
using TAOM.Features.LongDefeat.Domain;

namespace TAOM.Features.LongDefeat;

/// <summary>
/// The <c>fortifications</c> block of one shop in shop.json (phase3-plan.md section 5). A bad value costs one warning naming the file and the key and drops the smallest thing that holds it: a level, or when
/// the row cannot work without it, the row. A row that is dropped is hidden in the shop; a block that is absent is no warning at all. <c>tools/run_long_defeat.py</c> mirrors every rule here
/// (<c>shipped_fortifications</c>), and both suites read the cases in <c>tools/tests/long_defeat_fort_cases.json</c>.
/// </summary>
internal static class FortificationsReader
{
    private const int MaxPrice = 1_000_000;

    /// <summary>The shop's fortifications, or null when the shop has no <c>fortifications</c> key. A key that is not an object is one warning and no rows.</summary>
    public static FortPlan? Read(JObject shop, string label, LongDefeatConfigScope s)
    {
        if (!shop.TryGetValue("fortifications", out var token))
            return null;

        var path = label + ".fortifications";
        if (!(token is JObject block))
        {
            s.Warn($"'{path}' is not an object; this shop sells no fortifications");
            return FortPlan.None;
        }

        return new FortPlan(ReadGate(block, path, s), ReadTowers(block, path, s), ReadCare(block, path, s), ReadVolley(block, path, s));
    }

    private static GatePlan? ReadGate(JObject block, string parent, LongDefeatConfigScope s)
    {
        var gate = s.Group(block, "gate", parent);
        if (gate == null)
            return null;

        var path = parent + ".gate";
        if (!LongDefeatConfigScope.TryInt(gate["repairPrice"], 1, MaxPrice, out var repair, out var why))
        {
            s.Warn($"'{path}.repairPrice' {why}; the gate row is dropped");
            return null;
        }

        var array = Levels(gate, path, "the gate row", s);
        if (array == null)
            return null;

        var levels = new List<GateLevelPlan>();
        for (var i = 0; i < array.Count; i++)
        {
            var at = $"{path}.levels[{i}]";
            if (!(array[i] is JObject level))
            {
                s.Warn($"'{at}' is not an object; level dropped");
                continue;
            }

            if (!LongDefeatConfigScope.TryFloat(level["hitPointsPercent"], 1f, 1000f, out var percent, out why))
            {
                s.Warn($"'{at}.hitPointsPercent' {why}; this level and the ones after it are dropped");
                break;
            }

            if (levels.Count > 0 && !(percent > levels[levels.Count - 1].HitPointsPercent))
            {
                s.Warn($"'{at}.hitPointsPercent' is {LongDefeatConfigScope.Format(percent)}, not above the level before it; this level and the ones after it are dropped");
                break;
            }

            if (!LongDefeatConfigScope.TryInt(level["price"], 1, MaxPrice, out var price, out why))
            {
                s.Warn($"'{at}.price' {why}; level dropped");
                continue;
            }

            levels.Add(new GateLevelPlan(percent, price));
        }

        if (levels.Count > 0)
            return new GatePlan(repair, levels);

        s.Warn($"'{path}' has no valid level; the gate row is dropped");
        return null;
    }

    private static TowerPlan? ReadTowers(JObject block, string parent, LongDefeatConfigScope s)
    {
        var towers = s.Group(block, "towers", parent);
        if (towers == null)
            return null;

        var path = parent + ".towers";
        var troop = LongDefeatConfigScope.Text(towers["troop"]);
        if (troop == null)
        {
            s.Warn($"'{path}.troop' is missing or blank; the towers row is dropped");
            return null;
        }

        var array = Levels(towers, path, "the towers row", s);
        if (array == null)
            return null;

        var levels = new List<TowerLevelPlan>();
        for (var i = 0; i < array.Count; i++)
        {
            var at = $"{path}.levels[{i}]";
            if (!(array[i] is JObject level))
            {
                s.Warn($"'{at}' is not an object; level dropped");
                continue;
            }

            if (!LongDefeatConfigScope.TryInt(level["posts"], 1, 16, out var posts, out var why))
            {
                s.Warn($"'{at}.posts' {why}; level dropped");
                continue;
            }

            if (levels.Count > 0 && posts < levels[levels.Count - 1].Posts)
            {
                s.Warn($"'{at}.posts' is {posts}, fewer than the level before it; level dropped");
                continue;
            }

            if (!LongDefeatConfigScope.TryInt(level["price"], 1, MaxPrice, out var price, out why))
            {
                s.Warn($"'{at}.price' {why}; level dropped");
                continue;
            }

            levels.Add(new TowerLevelPlan(posts, VolleyFlag(level, at, i == array.Count - 1, s), price));
        }

        if (levels.Count == 0)
        {
            s.Warn($"'{path}' has no valid level; the towers row is dropped");
            return null;
        }

        var volley = ReadTowerVolley(towers, path, s);
        if (volley == null && levels.Any(l => l.Volley))
        {
            s.Warn($"'{path}.levels' asks for the tower volley but '{path}.volley' is missing or dropped; the flag is ignored");
            levels = levels.Select(l => l with { Volley = false }).ToList();
        }

        return new TowerPlan(troop, levels, volley);
    }

    /// <summary>The level's <c>volley</c> flag: false when absent, ignored with a warning when it is not a boolean or the level is not the last in the file.</summary>
    private static bool VolleyFlag(JObject level, string at, bool isLast, LongDefeatConfigScope s)
    {
        if (!level.TryGetValue("volley", out var flag))
            return false;
        if (flag.Type != JTokenType.Boolean)
        {
            s.Warn($"'{at}.volley' is not true or false; the flag is ignored");
            return false;
        }

        if (!(bool)flag || isLast)
            return (bool)flag;

        s.Warn($"'{at}.volley' is on a level that is not the last; the flag is ignored");
        return false;
    }

    private static TowerVolleyPlan? ReadTowerVolley(JObject towers, string parent, LongDefeatConfigScope s)
    {
        var volley = s.Group(towers, "volley", parent);
        if (volley == null)
            return null;

        var item = LongDefeatConfigScope.Text(volley["item"]);
        string reason;
        if (item == null)
            return Dropped(parent + ".volley", "item", "is missing or blank", s);
        if (!LongDefeatConfigScope.TryInt(volley["arrows"], 1, 40, out var arrows, out reason))
            return Dropped(parent + ".volley", "arrows", reason, s);
        if (!LongDefeatConfigScope.TryFloat(volley["firstSeconds"], 5f, 600f, out var first, out reason))
            return Dropped(parent + ".volley", "firstSeconds", reason, s);
        if (!LongDefeatConfigScope.TryFloat(volley["cooldownSeconds"], 5f, 600f, out var cooldown, out reason))
            return Dropped(parent + ".volley", "cooldownSeconds", reason, s);
        if (!LongDefeatConfigScope.TryFloat(volley["radius"], 1f, 30f, out var radius, out reason))
            return Dropped(parent + ".volley", "radius", reason, s);
        if (!LongDefeatConfigScope.TryFloat(volley["range"], 10f, 200f, out var range, out reason))
            return Dropped(parent + ".volley", "range", reason, s);
        if (!LongDefeatConfigScope.TryFloat(volley["originHeight"], 0f, 10f, out var height, out reason))
            return Dropped(parent + ".volley", "originHeight", reason, s);

        return new TowerVolleyPlan(item, arrows, first, cooldown, radius, range, height);
    }

    private static TowerVolleyPlan? Dropped(string path, string key, string reason, LongDefeatConfigScope s)
    {
        s.Warn($"'{path}.{key}' {reason}; the tower volley block is dropped");
        return null;
    }

    private static CarePlan? ReadCare(JObject block, string parent, LongDefeatConfigScope s)
    {
        var care = s.Group(block, "fieldCare", parent);
        if (care == null)
            return null;

        var path = parent + ".fieldCare";
        if (!LongDefeatConfigScope.TryFloat(care["share"], 0f, 1f, out var share, out var why) || !(share > 0f))
        {
            s.Warn($"'{path}.share' {(why.Length > 0 ? why : "is not above 0")}; the field care row is dropped");
            return null;
        }

        if (!LongDefeatConfigScope.TryInt(care["price"], 1, MaxPrice, out var price, out why))
        {
            s.Warn($"'{path}.price' {why}; the field care row is dropped");
            return null;
        }

        return new CarePlan(share, price, s.Int(care, path, "perIntermission", 1, 1, 5));
    }

    private static VolleyPlan? ReadVolley(JObject block, string parent, LongDefeatConfigScope s)
    {
        var volley = s.Group(block, "elvenVolley", parent);
        if (volley == null)
            return null;

        var path = parent + ".elvenVolley";
        var item = LongDefeatConfigScope.Text(volley["item"]);
        if (item == null)
        {
            s.Warn($"'{path}.item' is missing or blank; the Elven Volley row is dropped");
            return null;
        }

        if (!LongDefeatConfigScope.TryFloat(volley["originHeight"], 0f, 10f, out var height, out var why))
        {
            s.Warn($"'{path}.originHeight' {why}; the Elven Volley row is dropped");
            return null;
        }

        var array = Levels(volley, path, "the Elven Volley row", s);
        if (array == null)
            return null;

        var levels = new List<VolleyLevelPlan>();
        for (var i = 0; i < array.Count; i++)
        {
            var at = $"{path}.levels[{i}]";
            if (!(array[i] is JObject level))
            {
                s.Warn($"'{at}' is not an object; level dropped");
                continue;
            }

            if (!TryVolleyLevel(level, out var plan, out why))
            {
                s.Warn($"'{at}.{why}; level dropped");
                continue;
            }

            if (levels.Count == 0 && plan.Aim != VolleyAimMode.Auto)
            {
                s.Warn($"'{at}.aim' is crosshair, but the first level aims itself; level dropped");
                continue;
            }

            levels.Add(plan);
        }

        if (levels.Count > 0)
            return new VolleyPlan(item, height, levels);

        s.Warn($"'{path}' has no valid level; the Elven Volley row is dropped");
        return null;
    }

    private static bool TryVolleyLevel(JObject level, out VolleyLevelPlan plan, out string why)
    {
        plan = null!;
        var aim = LongDefeatConfigScope.Text(level["aim"]);
        if (!LongDefeatConfigScope.TryInt(level["price"], 1, MaxPrice, out var price, out var reason))
            return Bad("price", reason, out why);
        if (!LongDefeatConfigScope.TryInt(level["arrows"], 1, 40, out var arrows, out reason))
            return Bad("arrows", reason, out why);
        if (!LongDefeatConfigScope.TryFloat(level["cooldownSeconds"], 5f, 600f, out var cooldown, out reason))
            return Bad("cooldownSeconds", reason, out why);
        if (!LongDefeatConfigScope.TryFloat(level["radius"], 1f, 30f, out var radius, out reason))
            return Bad("radius", reason, out why);
        if (!LongDefeatConfigScope.TryFloat(level["range"], 10f, 200f, out var range, out reason))
            return Bad("range", reason, out why);
        if (aim != "auto" && aim != "crosshair")
            return Bad("aim", $"is '{aim ?? level["aim"]?.ToString() ?? "missing"}', not 'auto' or 'crosshair'", out why);

        plan = new VolleyLevelPlan(price, arrows, cooldown, radius, range, aim == "auto" ? VolleyAimMode.Auto : VolleyAimMode.Crosshair);
        why = "";
        return true;
    }

    private static bool Bad(string key, string reason, out string why)
    {
        why = $"{key}' {reason}";
        return false;
    }

    private static JArray? Levels(JObject row, string path, string what, LongDefeatConfigScope s)
    {
        if (row["levels"] is JArray array)
            return array;

        s.Warn($"'{path}.levels' is missing or not an array; {what} is dropped");
        return null;
    }
}
