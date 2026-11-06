using System;
using System.Collections.Generic;
using System.Linq;
using Newtonsoft.Json.Linq;
using TAOM.Features.LongDefeat.Domain;

namespace TAOM.Features.LongDefeat;

/// <summary>
/// The <c>armoury</c>, <c>yard</c> and <c>marshal</c> blocks of one shop in shop.json (phase4-plan.md section 5). A bad value costs one warning naming the file and the key and drops the smallest thing that holds it:
/// a class id, a set, an engine row, or when the block cannot work without it, the block. A block that is absent is no warning at all, and the shop then has no such place. A block of the wrong type is dropped whole.
/// A gear set's item ids are not checked here (the object manager decides that at launch, <see cref="LongDefeatGearItems"/>), and neither is a donor. <c>tools/run_long_defeat.py</c> mirrors every rule here
/// (<c>read_locations</c>), and both suites read the cases in <c>tools/tests/long_defeat_location_cases.json</c>. A skipped set or row does not claim its id or kind.
/// </summary>
internal static class LocationsReader
{
    private const int MaxPrice = 1_000_000;
    private const float DefaultArmouryReach = 4f;
    private const float DefaultYardReach = 6f;
    private const float ReachMin = 1f;
    private const float ReachMax = 15f;

    private static readonly string[] EngineKinds = { "mangonel", "trebuchet" };

    /// <summary>
    /// The shop's places, or null when the shop has none of the three keys. <paramref name="classIds"/> gives the ids classes.json holds, and is called at most once, and only when a set lists a class, so a shop
    /// without such a set never makes the catalog load classes.json early; null means the ids are not known and a listed class is taken as it is.
    /// </summary>
    public static LocationsPlan? Read(JObject shop, string label, LongDefeatConfigScope s, Func<IReadOnlyCollection<string>>? classIds)
    {
        if (!shop.ContainsKey("armoury") && !shop.ContainsKey("yard") && !shop.ContainsKey("marshal"))
            return null;

        var known = classIds == null ? null : new Lazy<IReadOnlyCollection<string>>(classIds);
        return new LocationsPlan(ReadArmoury(shop, label, s, known), ReadYard(shop, label, s), ReadMarshal(shop, label, s));
    }

    private static JObject? Block(JObject shop, string key, string label, string what, LongDefeatConfigScope s)
    {
        if (!shop.TryGetValue(key, out var token))
            return null;
        if (token is JObject block)
            return block;

        s.Warn($"'{label}.{key}' is not an object; this shop has no {what}");
        return null;
    }

    private static ArmouryPlan? ReadArmoury(JObject shop, string label, LongDefeatConfigScope s, Lazy<IReadOnlyCollection<string>>? classIds)
    {
        var block = Block(shop, "armoury", label, "armoury", s);
        if (block == null)
            return null;

        var path = label + ".armoury";
        var reach = s.Float(block, path, "reach", DefaultArmouryReach, ReachMin, ReachMax);
        if (!(block["sets"] is JArray array))
        {
            s.Warn($"'{path}.sets' is missing or not an array; the armoury is dropped");
            return null;
        }

        var sets = new List<GearSet>();
        var seen = new HashSet<string>(StringComparer.Ordinal);
        for (var i = 0; i < array.Count; i++)
        {
            var set = ReadSet(array[i] as JObject, $"{path}.sets[{i}]", seen, classIds, s);
            if (set != null)
            {
                seen.Add(set.Id);
                sets.Add(set);
            }
        }

        if (sets.Count == 0)
        {
            s.Warn($"'{path}' has no valid set; the armoury is dropped");
            return null;
        }

        return new ArmouryPlan(reach, sets);
    }

    private static GearSet? ReadSet(JObject? o, string label, HashSet<string> seen, Lazy<IReadOnlyCollection<string>>? classIds, LongDefeatConfigScope s)
    {
        if (o == null)
        {
            s.Warn($"'{label}' is not an object; set skipped");
            return null;
        }

        var id = LongDefeatConfigScope.Text(o["id"]);
        if (id == null || !IsSetId(id))
        {
            s.Warn($"'{label}.id' needs lowercase letters, digits and underscores; set skipped");
            return null;
        }

        if (seen.Contains(id))
        {
            s.Warn($"'{label}' repeats the id '{id}'; the first set is kept");
            return null;
        }

        var kindText = LongDefeatConfigScope.Text(o["kind"]);
        var kind = kindText == "armour" ? GearKind.Armour : GearKind.Weapons;
        if (kindText != "armour" && kindText != "weapons")
        {
            s.Warn($"'{label}.kind' is '{kindText ?? o["kind"]?.ToString() ?? "missing"}', not 'armour' or 'weapons'; set '{id}' skipped");
            return null;
        }

        if (!LongDefeatConfigScope.TryInt(o["price"], 1, MaxPrice, out var price, out var why))
        {
            s.Warn($"'{label}.price' {why}; set '{id}' skipped");
            return null;
        }

        var slots = ReadSlots(o["slots"], kind, label, id, s);
        if (slots == null)
            return null;

        IReadOnlyList<string>? classes = null;
        if (o.ContainsKey("classes") || kind == GearKind.Weapons)
        {
            classes = ReadClasses(o["classes"], kind, label, id, classIds, s);
            if (classes == null)
                return null;
        }

        return new GearSet(id, LongDefeatConfigScope.Text(o["name"]) ?? id, LongDefeatConfigScope.Text(o["text"]) ?? "", kind, classes, slots, price);
    }

    /// <summary>A set id is one or more of <c>a-z</c>, <c>0-9</c> and underscore, so it can name a row, a purchase and a report field.</summary>
    internal static bool IsSetId(string id)
    {
        foreach (var c in id)
        {
            if (!(c >= 'a' && c <= 'z') && !(c >= '0' && c <= '9') && c != '_')
                return false;
        }

        return id.Length > 0;
    }

    private static IReadOnlyList<LdKitSlot>? ReadSlots(JToken? token, GearKind kind, string label, string id, LongDefeatConfigScope s)
    {
        if (!(token is JObject slots) || slots.Count == 0)
        {
            s.Warn($"'{label}.slots' needs at least one slot; set '{id}' skipped");
            return null;
        }

        var allowed = GearSlots.Of(kind);
        var read = new List<LdKitSlot>();
        foreach (var slot in slots.Properties())
        {
            if (!allowed.Contains(slot.Name, StringComparer.Ordinal))
            {
                s.Warn($"'{label}.slots.{slot.Name}' is not a slot of a{(kind == GearKind.Armour ? "n armour" : " weapons")} set ({string.Join(", ", allowed)}); set '{id}' skipped");
                return null;
            }

            var item = LongDefeatConfigScope.Text(slot.Value);
            if (item == null)
            {
                s.Warn($"'{label}.slots.{slot.Name}' must be an item id; set '{id}' skipped");
                return null;
            }

            read.Add(new LdKitSlot(slot.Name, item));
        }

        return read;
    }

    // The classes a set is offered to. Null (after its warnings) skips the set. An armour set with no `classes` key is offered to every class and never comes here.
    private static IReadOnlyList<string>? ReadClasses(JToken? token, GearKind kind, string label, string id, Lazy<IReadOnlyCollection<string>>? classIds, LongDefeatConfigScope s)
    {
        if (token == null && kind == GearKind.Weapons)
        {
            s.Warn($"'{label}.classes' is required for a weapons set; set '{id}' skipped");
            return null;
        }

        if (!(token is JArray array))
        {
            s.Warn($"'{label}.classes' is not an array of class ids; set '{id}' skipped");
            return null;
        }

        var classes = new List<string>();
        for (var i = 0; i < array.Count; i++)
        {
            var name = LongDefeatConfigScope.Text(array[i]);
            if (name == null)
                s.Warn($"'{label}.classes[{i}]' is not a class id; dropped");
            else if (classIds != null && !classIds.Value.Contains(name, StringComparer.Ordinal))
                s.Warn($"'{label}.classes[{i}]' names the class '{name}', which classes.json does not hold; dropped");
            else if (!classes.Contains(name, StringComparer.Ordinal))
                classes.Add(name);
        }

        if (classes.Count == 0)
        {
            s.Warn($"'{label}.classes' holds no known class; set '{id}' skipped");
            return null;
        }

        return classes;
    }

    private static YardPlan? ReadYard(JObject shop, string label, LongDefeatConfigScope s)
    {
        var block = Block(shop, "yard", label, "engineer's yard", s);
        if (block == null)
            return null;

        var path = label + ".yard";
        var reach = s.Float(block, path, "reach", DefaultYardReach, ReachMin, ReachMax);
        if (!(block["engines"] is JArray array))
        {
            s.Warn($"'{path}.engines' is missing or not an array; the yard is dropped");
            return null;
        }

        var engines = new List<EngineOffer>();
        for (var i = 0; i < array.Count; i++)
        {
            var engine = ReadEngine(array[i] as JObject, $"{path}.engines[{i}]", engines, s);
            if (engine != null)
                engines.Add(engine);
        }

        if (engines.Count == 0)
        {
            s.Warn($"'{path}' has no valid engine; the yard is dropped");
            return null;
        }

        return new YardPlan(reach, engines);
    }

    private static EngineOffer? ReadEngine(JObject? o, string label, List<EngineOffer> kept, LongDefeatConfigScope s)
    {
        if (o == null)
        {
            s.Warn($"'{label}' is not an object; engine skipped");
            return null;
        }

        var kind = LongDefeatConfigScope.Text(o["kind"]);
        if (kind == null || !EngineKinds.Contains(kind, StringComparer.Ordinal))
        {
            s.Warn($"'{label}.kind' is '{kind ?? o["kind"]?.ToString() ?? "missing"}', not 'mangonel' or 'trebuchet'; engine skipped");
            return null;
        }

        if (kept.Any(e => e.Kind == kind))
        {
            s.Warn($"'{label}' repeats the kind '{kind}'; the first engine is kept");
            return null;
        }

        if (!LongDefeatConfigScope.TryInt(o["price"], 1, MaxPrice, out var price, out var why))
        {
            s.Warn($"'{label}.price' {why}; engine '{kind}' skipped");
            return null;
        }

        return new EngineOffer(kind, price);
    }

    private static MarshalPlan? ReadMarshal(JObject shop, string label, LongDefeatConfigScope s)
    {
        var block = Block(shop, "marshal", label, "Marshal", s);
        if (block == null)
            return null;

        var path = label + ".marshal";
        var troop = LongDefeatConfigScope.Text(block["troop"]);
        if (troop == null)
        {
            s.Warn($"'{path}.troop' is missing or blank; the Marshal is dropped");
            return null;
        }

        if (!LongDefeatConfigScope.TryInt(block["price"], 1, MaxPrice, out var price, out var why))
        {
            s.Warn($"'{path}.price' {why}; the Marshal is dropped");
            return null;
        }

        return new MarshalPlan(troop, price, LongDefeatConfigScope.Text(block["name"]) ?? troop, LongDefeatConfigScope.Text(block["text"]) ?? "");
    }
}
