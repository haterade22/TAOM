using System;
using System.Collections.Generic;
using TAOM.Features.IncidentVoices.Models;

namespace TAOM.Features.IncidentVoices;

/// <summary>
/// Picks the place a vanilla incident names ("a caravan from PLACE") from the player's own side. The caller ranks each candidate:
/// 1 the player's own realm, 2 the player's own people, 3 the same side and voice, 0 not the player's side. The best rank that has
/// a candidate wins, and the seed (the engine's incident seed and the incident id) picks one of its places, so two calls in the same
/// roll agree. No ranked candidate gives null and the caller closes the incident.
/// </summary>
internal static class IncidentPlacePicker
{
    public static PlaceFacts? Pick(IReadOnlyList<PlaceFacts> candidates, Func<PlaceFacts, int> rank, long seed, string incidentId)
    {
        var best = int.MaxValue;
        var pool = new List<PlaceFacts>();
        foreach (var place in candidates)
        {
            var r = rank(place);
            if (r <= 0 || r > best)
                continue;
            if (r < best)
            {
                best = r;
                pool.Clear();
            }
            pool.Add(place);
        }
        if (pool.Count == 0)
            return null;

        pool.Sort((a, b) => string.CompareOrdinal(a.Name, b.Name));
        var index = (int)(Math.Abs((seed + Hash(incidentId)) % pool.Count));
        return pool[index];
    }

    /// <summary>A stable string hash (FNV-1a), the same on every run.</summary>
    internal static long Hash(string text)
    {
        unchecked
        {
            uint hash = 2166136261;
            foreach (var c in text)
                hash = (hash ^ c) * 16777619;
            return hash;
        }
    }
}
