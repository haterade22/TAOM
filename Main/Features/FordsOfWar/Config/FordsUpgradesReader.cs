using System;
using System.Collections.Generic;
using System.Linq;
using Newtonsoft.Json.Linq;
using TAOM.Features.FordsOfWar.Domain;

namespace TAOM.Features.FordsOfWar;

/// <summary>Reads the <c>upgrades</c> array of match.json. A bad entry is skipped with one warning; a missing or empty array means the shipped defaults.</summary>
internal static class FordsUpgradesReader
{
    private const int MaxPrice = 1_000_000;

    public static IReadOnlyList<UpgradeTrack> Read(JToken? token, IReadOnlyList<UpgradeTrack> defaults, FordsConfigScope s)
    {
        if (token == null)
            return defaults;
        if (!(token is JArray array))
        {
            s.Warn("'upgrades' is not an array; using the default upgrades");
            return defaults;
        }

        var tracks = new List<UpgradeTrack>();
        foreach (var entry in array)
        {
            var track = ReadTrack(entry, s);
            if (track == null)
                continue;
            if (tracks.Any(t => t.Id == track.Id))
            {
                s.Warn($"upgrade '{track.Id}' appears twice; the first one counts");
                continue;
            }

            tracks.Add(track);
        }

        return tracks.Count == 0 ? defaults : tracks;
    }

    private static UpgradeTrack? ReadTrack(JToken entry, FordsConfigScope s)
    {
        var id = FordsConfigScope.Text(entry["id"]);
        var name = FordsConfigScope.Text(entry["name"]);
        if (id == null || name == null)
        {
            s.Warn("an upgrade needs an 'id' and a 'name'; skipped");
            return null;
        }

        if (!Enum.TryParse<UpgradeEffect>(FordsConfigScope.Text(entry["effect"]), out var effect))
        {
            s.Warn($"upgrade '{id}' has no known 'effect'; skipped");
            return null;
        }

        if (!FordsConfigScope.TryFloat(entry["step"], 0f, 10f, out var step, out var stepWhy))
        {
            s.Warn($"upgrade '{id}' 'step' {stepWhy}; skipped");
            return null;
        }

        if (!(entry["prices"] is JArray priceArray) || priceArray.Count == 0)
        {
            s.Warn($"upgrade '{id}' needs a 'prices' array; skipped");
            return null;
        }

        var prices = new List<int>();
        foreach (var price in priceArray)
        {
            if (!FordsConfigScope.TryInt(price, 0, MaxPrice, out var value, out var priceWhy))
            {
                s.Warn($"upgrade '{id}' price {priceWhy}; skipped");
                return null;
            }

            prices.Add(value);
        }

        return new UpgradeTrack(id, name, effect, step, prices);
    }
}
