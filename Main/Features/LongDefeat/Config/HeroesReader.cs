using System;
using System.Collections.Generic;
using System.Linq;
using Newtonsoft.Json.Linq;
using TAOM.Features.LongDefeat.Domain;

namespace TAOM.Features.LongDefeat;

/// <summary>
/// A shop's <c>heroes</c> list (#1325): each row is <c>id</c>, <c>troop</c> (the hero's character), <c>price</c> (gold, 1 to 100,000), an
/// optional <c>healthFactor</c> (1 to 10; 1 when absent), and optional <c>name</c> and <c>text</c> lines for the shop row. A missing list is
/// no hero; a list that is not an array, a row that is not an object, lacks an id or a troop, has an id that is not a set id (lowercase
/// letters, digits and underscores, <c>LocationsReader.IsSetId</c>: the check line writes the id as one token, so a space would break the judge's match), has a bad price or
/// factor, or repeats an id or a troop (the first is kept) is skipped with one warning, so every line ends. Troops are compared without case, as the
/// engine's object manager does. Whether the troop exists is the launch's check (<c>RunPlanTroops.AllTroopIds</c>).
/// </summary>
internal static class HeroesReader
{
    public static IReadOnlyList<HeroOffer> Read(JObject shop, string label, LongDefeatConfigScope s)
    {
        var heroes = new List<HeroOffer>();
        var token = shop["heroes"];
        if (token == null || token.Type == JTokenType.Null)
            return heroes;
        if (!(token is JArray rows))
        {
            s.Warn($"'{label}.heroes' is not an array; this shop sells no hero");
            return heroes;
        }

        for (var i = 0; i < rows.Count; i++)
        {
            var where = $"{label}.heroes[{i}]";
            var row = ReadRow(rows[i], where, heroes.Select(h => (h.Id, h.TroopId)), s);
            if (row == null)
                continue;

            if (!LongDefeatConfigScope.TryInt(rows[i]["price"], 1, 100_000, out var price, out var why))
            {
                s.Warn($"'{where}.price' {why}; hero '{row.Id}' skipped");
                continue;
            }

            heroes.Add(new HeroOffer(row.Id, LongDefeatConfigScope.Text(rows[i]["name"]) ?? row.Id, LongDefeatConfigScope.Text(rows[i]["text"]) ?? string.Empty,
                row.TroopId, price, row.HealthFactor));
        }

        return heroes;
    }

    /// <summary>
    /// A hero row's shared part: <c>id</c>, <c>troop</c> and the optional <c>healthFactor</c>. The shop reads its own <c>price</c>, <c>name</c> and <c>text</c>;
    /// a story mission's free heroes have none. <paramref name="kept"/> holds the ids and troops already taken, so a repeat is skipped with one warning.
    /// </summary>
    internal static ReadHeroRow? ReadRow(JToken row, string where, IEnumerable<(string Id, string TroopId)> kept, LongDefeatConfigScope s)
    {
        if (!(row is JObject o))
        {
            s.Warn($"'{where}' is not an object; hero skipped");
            return null;
        }

        var id = LongDefeatConfigScope.Text(o["id"]);
        var troop = LongDefeatConfigScope.Text(o["troop"]);
        if (id == null || troop == null)
        {
            s.Warn($"'{where}' needs an 'id' and a 'troop'; hero skipped");
            return null;
        }

        if (!LocationsReader.IsSetId(id))
        {
            s.Warn($"'{where}' has an 'id' that needs lowercase letters, digits and underscores, so the check line can carry it; hero skipped");
            return null;
        }

        var healthFactor = 1f;
        var factor = o["healthFactor"];
        if (factor != null && factor.Type != JTokenType.Null && !LongDefeatConfigScope.TryFloat(factor, 1f, 10f, out healthFactor, out var why))
        {
            s.Warn($"'{where}.healthFactor' {why}; hero '{id}' skipped");
            return null;
        }

        if (kept.Any(k => Same(k.Id, id) || Same(k.TroopId, troop)))
        {
            s.Warn($"'{where}' repeats the hero '{id}' or his troop '{troop}'; the first is kept");
            return null;
        }

        return new ReadHeroRow(id, troop, healthFactor);
    }

    internal sealed record ReadHeroRow(string Id, string TroopId, float HealthFactor);

    private static bool Same(string a, string b) => string.Equals(a, b, StringComparison.OrdinalIgnoreCase);
}
