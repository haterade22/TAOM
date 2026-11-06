using System;
using System.Collections.Generic;
using Newtonsoft.Json.Linq;
using TAOM.Features.LongDefeat.Domain;

namespace TAOM.Features.LongDefeat;

/// <summary>
/// shop.json: shops by id. A bad starting gold falls back to 0; an offer with no id or troop, a duplicate id (the first is
/// kept), a bad class, pack size or price is skipped. A shop left with no offer still loads, so a fortress may start with only
/// its gold and nothing to buy. A shop's <c>fortifications</c> block is read by <see cref="FortificationsReader"/>.
/// </summary>
internal static class ShopReader
{
    public static IReadOnlyDictionary<string, ShopPlan> Read(string? text, LongDefeatConfigScope s)
    {
        var shops = new Dictionary<string, ShopPlan>(StringComparer.OrdinalIgnoreCase);
        var root = s.OpenRoot(text, "was not found or could not be read; no shops");
        if (root == null)
            return shops;

        var block = s.Group(root, "shops", "");
        if (block == null)
        {
            if (!root.ContainsKey("shops"))
                s.Warn("'shops' is missing; no shops");
            return shops;
        }

        foreach (var property in block.Properties())
        {
            if (!(property.Value is JObject shop))
            {
                s.Warn($"'shops.{property.Name}' is not an object; shop skipped");
                continue;
            }

            shops[property.Name] = ReadShop(property.Name, shop, s);
        }

        s.Loaded($"{shops.Count} shop(s)");
        return shops;
    }

    private static ShopPlan ReadShop(string id, JObject shop, LongDefeatConfigScope s)
    {
        var label = $"shops.{id}";
        var offers = new List<ShopOffer>();
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        if (shop["offers"] is JArray array)
        {
            for (var i = 0; i < array.Count; i++)
            {
                var offer = ReadOffer(array[i] as JObject, $"{label}.offers[{i}]", seen, s);
                if (offer != null)
                    offers.Add(offer);
            }
        }
        else
        {
            s.Warn($"'{label}.offers' is missing or not an array; this shop sells nothing");
        }

        return new ShopPlan(s.Int(shop, label, "startingGold", 0, 0, 1_000_000), offers, FortificationsReader.Read(shop, label, s));
    }

    private static ShopOffer? ReadOffer(JObject? o, string label, HashSet<string> seen, LongDefeatConfigScope s)
    {
        var id = LongDefeatConfigScope.Text(o?["id"]);
        var troop = LongDefeatConfigScope.Text(o?["troop"]);
        if (o == null || id == null || troop == null)
        {
            s.Warn($"'{label}' needs an 'id' and a 'troop'; offer skipped");
            return null;
        }

        if (seen.Contains(id))
        {
            s.Warn($"'{label}' repeats the id '{id}'; the first offer is kept");
            return null;
        }

        if (!LongDefeatConfigScope.TrySlot(o["class"], out var slot, out var why))
        {
            s.Warn($"'{label}.class' {why}; offer '{id}' skipped");
            return null;
        }

        if (!LongDefeatConfigScope.TryInt(o["packSize"], 1, 50, out var pack, out why))
        {
            s.Warn($"'{label}.packSize' {why}; offer '{id}' skipped");
            return null;
        }

        if (!LongDefeatConfigScope.TryInt(o["price"], 1, 1_000_000, out var price, out why))
        {
            s.Warn($"'{label}.price' {why}; offer '{id}' skipped");
            return null;
        }

        seen.Add(id);
        return new ShopOffer(id, LongDefeatConfigScope.Text(o["name"]) ?? id, troop, slot, pack, price);
    }
}
