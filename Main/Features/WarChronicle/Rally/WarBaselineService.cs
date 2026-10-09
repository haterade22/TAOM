using System;
using System.Collections.Generic;
using TAOM.Adapters;

namespace TAOM.Features.WarChronicle.Rally;

/// <summary>
/// The fortification points each kingdom started the war with (2 per town, 1 per castle), against
/// which the ledger and the rally measure loss (<see cref="LossOf"/>, the one derivation both use). A
/// baseline is taken once, on the first daily tick that sees the kingdom: at campaign start for every
/// kingdom, later for a rebel or player-founded one. A save from before this feature never reaches
/// SyncData (CampaignBehaviorDataStore.LoadBehaviorData calls it only for a saved record), so its
/// baselines are taken late, from that day's map, and the rally under-fires on such a campaign;
/// <c>WarChronicleTickService</c> logs that case from the data (no baselines held after day 1).
/// A process-lifetime singleton holding campaign state: <see cref="ResetForNewSession"/> runs from the
/// campaign behavior's constructor.
/// </summary>
public sealed class WarBaselineService
{
    private readonly Dictionary<string, int> _baselines = new Dictionary<string, int>(StringComparer.Ordinal);

    /// <summary>How many kingdoms hold a baseline.</summary>
    public int Count => _baselines.Count;

    public int? GetBaseline(string kingdomId) =>
        kingdomId != null && _baselines.TryGetValue(kingdomId, out var points) ? points : (int?)null;

    /// <summary>The share of the baseline lost, never negative; null without a baseline above zero.</summary>
    internal static float? LossOf(int? baseline, int points)
    {
        if (!baseline.HasValue || baseline.Value <= 0)
            return null;
        return Math.Max(0f, (baseline.Value - points) / (float)baseline.Value);
    }

    /// <summary>Takes a baseline for every kingdom that has none; returns how many were taken.</summary>
    public int EnsureBaselines(IReadOnlyList<KingdomWarSnapshot> kingdoms)
    {
        if (kingdoms == null)
            return 0;

        var taken = 0;
        foreach (var kingdom in kingdoms)
        {
            if (kingdom == null || string.IsNullOrEmpty(kingdom.Id) || _baselines.ContainsKey(kingdom.Id))
                continue;
            _baselines[kingdom.Id] = kingdom.FortificationPoints;
            taken++;
        }

        return taken;
    }

    public IReadOnlyDictionary<string, int> Snapshot() => new Dictionary<string, int>(_baselines, StringComparer.Ordinal);

    /// <summary>
    /// Restores a save, skipping a row with an empty id or a negative value. An empty map (a corrupt or
    /// out-of-range section) leaves the baselines to be taken at the next tick.
    /// </summary>
    public void RestoreFromSave(IReadOnlyDictionary<string, int> baselines)
    {
        ResetForNewSession();
        if (baselines == null)
            return;

        foreach (var pair in baselines)
        {
            if (!string.IsNullOrEmpty(pair.Key) && pair.Value >= 0)
                _baselines[pair.Key] = pair.Value;
        }
    }

    public void ResetForNewSession() => _baselines.Clear();
}
