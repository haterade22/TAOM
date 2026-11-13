using System;
using System.Collections.Generic;
using System.Linq;
using Newtonsoft.Json.Linq;
using TAOM.Features.LongDefeat.Domain;

namespace TAOM.Features.LongDefeat;

/// <summary>
/// A shop's <c>upgrades</c> list (#1161): each row is <c>from</c>, <c>to</c>, <c>price</c> (gold per soldier, 1 to 100,000) and an optional <c>name</c>. A missing list is no step; a list that is
/// not an array, a row that is not an object, lacks a troop, names one troop twice, has a bad price, repeats a step (the first is kept) or would lead back to its own <c>from</c> is skipped with
/// one warning, so every line ends. Troop ids are compared without case, as the engine's object manager does.
/// </summary>
internal static class UpgradesReader
{
    public static IReadOnlyList<UpgradeStep> Read(JObject shop, string label, LongDefeatConfigScope s)
    {
        var steps = new List<UpgradeStep>();
        var token = shop["upgrades"];
        if (token == null || token.Type == JTokenType.Null)
            return steps;
        if (!(token is JArray rows))
        {
            s.Warn($"'{label}.upgrades' is not an array; this shop sells no upgrade");
            return steps;
        }

        for (var i = 0; i < rows.Count; i++)
        {
            var step = ReadStep(rows[i], $"{label}.upgrades[{i}]", steps, s);
            if (step != null)
                steps.Add(step);
        }

        return steps;
    }

    private static UpgradeStep? ReadStep(JToken row, string where, IReadOnlyList<UpgradeStep> kept, LongDefeatConfigScope s)
    {
        if (!(row is JObject o))
        {
            s.Warn($"'{where}' is not an object; step skipped");
            return null;
        }

        var from = LongDefeatConfigScope.Text(o["from"]);
        var to = LongDefeatConfigScope.Text(o["to"]);
        if (from == null || to == null)
        {
            s.Warn($"'{where}' needs a 'from' and a 'to' troop; step skipped");
            return null;
        }

        if (string.Equals(from, to, StringComparison.OrdinalIgnoreCase))
        {
            s.Warn($"'{where}' upgrades '{from}' into itself; step skipped");
            return null;
        }

        if (!LongDefeatConfigScope.TryInt(o["price"], 1, 100_000, out var price, out var why))
        {
            s.Warn($"'{where}.price' {why}; step '{from}>{to}' skipped");
            return null;
        }

        if (kept.Any(k => Same(k.FromTroop, from) && Same(k.ToTroop, to)))
        {
            s.Warn($"'{where}' repeats the step '{from}>{to}'; the first is kept");
            return null;
        }

        if (Reaches(kept, to, from))
        {
            s.Warn($"'{where}' would make a loop: '{to}' already leads to '{from}'; step skipped");
            return null;
        }

        return new UpgradeStep($"{from}>{to}", LongDefeatConfigScope.Text(o["name"]) ?? to, from, to, price);
    }

    /// <summary>Whether the kept steps lead from <paramref name="start"/> to <paramref name="target"/> (a walk over at most every step once).</summary>
    private static bool Reaches(IReadOnlyList<UpgradeStep> kept, string start, string target)
    {
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var open = new Stack<string>();
        open.Push(start);
        while (open.Count > 0)
        {
            var troop = open.Pop();
            if (Same(troop, target))
                return true;
            if (!seen.Add(troop))
                continue;
            foreach (var step in kept.Where(k => Same(k.FromTroop, troop)))
                open.Push(step.ToTroop);
        }

        return false;
    }

    private static bool Same(string a, string b) => string.Equals(a, b, StringComparison.OrdinalIgnoreCase);
}
