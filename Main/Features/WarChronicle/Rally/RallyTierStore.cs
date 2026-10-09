using System;
using System.Collections.Generic;

namespace TAOM.Features.WarChronicle.Rally;

/// <summary>
/// The current rally tier per kingdom, as saved in the payload's <c>rally.tiers</c> section. It holds
/// the saved tiers and answers <see cref="GetTier"/> (0 for a kingdom it does not know);
/// <see cref="RallyService"/> writes it daily and <c>WarLedgerService</c> reads it. A process-lifetime singleton holding campaign state:
/// <see cref="ResetForNewSession"/> runs from the campaign behavior's constructor.
/// </summary>
public sealed class RallyTierStore
{
    internal const int MaxTier = 2;

    private readonly Dictionary<string, int> _tiers = new Dictionary<string, int>(StringComparer.Ordinal);

    public int GetTier(string kingdomId) =>
        kingdomId != null && _tiers.TryGetValue(kingdomId, out var tier) ? tier : 0;

    public IReadOnlyDictionary<string, int> Snapshot() => new Dictionary<string, int>(_tiers, StringComparer.Ordinal);

    /// <summary>
    /// Sets one kingdom's tier (the rally's daily write). Tier 0 drops the row so the save carries only
    /// kingdoms that are rallying; an empty id or a tier outside 0 to 2 is ignored.
    /// </summary>
    public void SetTier(string kingdomId, int tier)
    {
        if (string.IsNullOrEmpty(kingdomId) || tier < 0 || tier > MaxTier)
            return;

        if (tier == 0)
            _tiers.Remove(kingdomId);
        else
            _tiers[kingdomId] = tier;
    }

    /// <summary>Replaces the tiers; a row with an empty id or a tier outside 0 to 2 is skipped.</summary>
    public void Restore(IReadOnlyDictionary<string, int>? tiers)
    {
        _tiers.Clear();
        if (tiers == null)
            return;

        foreach (var pair in tiers)
        {
            if (!string.IsNullOrEmpty(pair.Key) && pair.Value >= 0 && pair.Value <= MaxTier)
                _tiers[pair.Key] = pair.Value;
        }
    }

    public void ResetForNewSession() => _tiers.Clear();
}
