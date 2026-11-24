using System;
using System.Collections.Generic;
using System.Linq;
using TAOM.Features.FactionMap.Models;

namespace TAOM.Features.FactionPower;

/// <summary>
/// The list of sliders, built from factions.json: one per distinct non-empty game_faction (the realm culture id). Where several
/// entries share a culture, the playable entry's side and name win; with none playable, the entry whose key sorts first
/// (ordinal) wins, because the provider returns a Dictionary and file order is not stable. A culture whose entries disagree on
/// the side logs one warning. A new factions.json entry with a game_faction and a side needs no code.
/// </summary>
public static class FactionPowerCatalog
{
    public const string Evil = "evil";
    public const string Free = "free";
    public const string Neutral = "neutral";

    public static IReadOnlyList<FactionPowerEntry> Build(IEnumerable<KeyValuePair<string, FactionData>> factions, Action<string>? warn)
    {
        var result = new List<FactionPowerEntry>();
        var byCulture = factions
            .Where(f => f.Value != null && !string.IsNullOrWhiteSpace(f.Value.GameFaction))
            .GroupBy(f => f.Value.GameFaction.Trim(), StringComparer.Ordinal)
            .OrderBy(g => g.Key, StringComparer.Ordinal);

        foreach (var group in byCulture)
        {
            var winner = group
                .OrderBy(f => f.Value.Playable ? 0 : 1)
                .ThenBy(f => f.Key, StringComparer.Ordinal)
                .First();
            var side = NormaliseSide(winner.Value.Side);
            var sides = group.Select(f => NormaliseSide(f.Value.Side)).Distinct().ToList();
            if (sides.Count > 1)
                warn?.Invoke($"FactionPower: culture '{group.Key}' has factions.json entries with different sides ({string.Join(", ", sides)}); using {side} from '{winner.Key}'");
            result.Add(new FactionPowerEntry(group.Key, string.IsNullOrEmpty(winner.Value.Name) ? winner.Key : winner.Value.Name, side));
        }

        return result;
    }

    private static string NormaliseSide(string? side)
    {
        var s = (side ?? "").Trim().ToLowerInvariant();
        return s == Evil || s == Free ? s : Neutral;
    }
}
