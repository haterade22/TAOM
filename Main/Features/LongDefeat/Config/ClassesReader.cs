using System;
using System.Collections.Generic;
using System.Linq;
using Newtonsoft.Json.Linq;
using TAOM.Features.LongDefeat.Domain;

namespace TAOM.Features.LongDefeat;

/// <summary>
/// classes.json (phase2-plan.md section 3): the <c>classes</c> rows in file order, each with an id, TAOM's own name and one-line kit text, whether it rides, and a kit per
/// defender culture keyed by the XML slot names of <see cref="KitSlots"/> (an empty string clears a slot). One warning per bad value naming the file and the key. A row with no
/// id, name or text, a repeated id, no usable kit, or (when mounted) a kit lacking a horse or a harness is skipped. A foot class loses a horse slot it sets, because a foot class
/// always clears both. Whether an item exists and fits its slot is the engine's to say, so it is judged at the spawn (the adapter) and read back by the <c>class</c> check.
/// </summary>
internal static class ClassesReader
{
    public static ClassPlan Read(string? text, LongDefeatConfigScope s)
    {
        var root = s.OpenRoot(text, "was not found or could not be read; the lord plays in his own gear");
        if (root == null)
            return ClassPlan.None;

        var classes = new List<RunClass>();
        var seen = new HashSet<string>(StringComparer.Ordinal);
        if (root["classes"] is JArray array)
        {
            for (var i = 0; i < array.Count; i++)
            {
                var row = ReadRow(array[i] as JObject, $"classes[{i}]", seen, s);
                if (row != null)
                    classes.Add(row);
            }
        }
        else
        {
            s.Warn("'classes' is missing or not an array; the lord plays in his own gear");
        }

        s.Loaded($"{classes.Count} class(es)");
        return new ClassPlan(classes);
    }

    private static RunClass? ReadRow(JObject? o, string label, HashSet<string> seen, LongDefeatConfigScope s)
    {
        var id = LongDefeatConfigScope.Text(o?["id"]);
        var name = LongDefeatConfigScope.Text(o?["name"]);
        var text = LongDefeatConfigScope.Text(o?["text"]);
        if (o == null || id == null || name == null || text == null)
        {
            s.Warn($"'{label}' needs an 'id', a 'name' and a 'text'; class skipped");
            return null;
        }

        if (seen.Contains(id))
        {
            s.Warn($"'{label}' repeats the id '{id}'; the first class is kept");
            return null;
        }

        var mounted = false;
        if (o.TryGetValue("mounted", out var mountedToken))
        {
            if (mountedToken.Type == JTokenType.Boolean)
                mounted = (bool)mountedToken;
            else
                s.Warn($"'{label}.mounted' is not true or false; using false for '{id}'");
        }

        var kits = ReadKits(o["kits"], label, id, mounted, s);
        if (kits == null)
            return null;

        seen.Add(id);
        return new RunClass(id, name, text, mounted, kits);
    }

    private static IReadOnlyDictionary<string, ClassKit>? ReadKits(JToken? token, string label, string id, bool mounted, LongDefeatConfigScope s)
    {
        if (!(token is JObject kits))
        {
            s.Warn($"'{label}.kits' is missing or not an object; class '{id}' skipped (it has no kit)");
            return null;
        }

        var read = new Dictionary<string, ClassKit>(StringComparer.OrdinalIgnoreCase);
        foreach (var property in kits.Properties())
        {
            if (!(property.Value is JObject slots))
            {
                s.Warn($"'{label}.kits.{property.Name}' is not an object; that kit is skipped");
                continue;
            }

            var kit = ReadKit(slots, $"{label}.kits.{property.Name}", mounted, s);
            if (mounted && !(Holds(kit, KitSlots.Horse) && Holds(kit, KitSlots.HorseHarness)))
            {
                s.Warn($"'{label}.kits.{property.Name}' of the mounted class '{id}' needs a Horse and a HorseHarness; class '{id}' skipped");
                return null;
            }

            read[property.Name] = new ClassKit(kit);
        }

        if (read.Count == 0)
        {
            s.Warn($"'{label}.kits' holds no usable kit; class '{id}' skipped");
            return null;
        }

        return read;
    }

    private static bool Holds(IReadOnlyList<LdKitSlot> kit, string slot) => kit.Any(k => k.Slot == slot && k.ItemId.Length > 0);

    private static IReadOnlyList<LdKitSlot> ReadKit(JObject slots, string label, bool mounted, LongDefeatConfigScope s)
    {
        var kit = new List<LdKitSlot>();
        foreach (var slot in slots.Properties())
        {
            var path = $"{label}.{slot.Name}";
            if (!KitSlots.IsKnown(slot.Name))
            {
                s.Warn($"'{path}' is not a kit slot ({string.Join(", ", KitSlots.Names)}); slot skipped");
                continue;
            }

            if (slot.Value.Type != JTokenType.String)
            {
                s.Warn($"'{path}' must be an item id, or an empty string to clear the slot; slot skipped");
                continue;
            }

            if (!mounted && KitSlots.IsMount(slot.Name))
            {
                s.Warn($"'{path}' sets a horse slot on a foot class, which always clears both; slot skipped");
                continue;
            }

            kit.Add(new LdKitSlot(slot.Name, ((string)slot.Value!).Trim()));
        }

        return kit;
    }
}
