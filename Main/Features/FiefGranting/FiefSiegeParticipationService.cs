using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using TAOM.Core.Logging;

namespace TAOM.Features.FiefGranting;

/// <summary>
/// The in-memory participation record (#565), persisted by
/// <c>FiefGrantingCampaignBehavior.SyncData</c> as one string per settlement (the
/// <c>SiegeDefenseService.SnapshotForSave</c> shape), so no new saveable type and no definer change.
///
/// Reuse.Singleton holding per-campaign state, so it carries the session-reset story from
/// <c>csharp-architecture.md</c>: the behavior calls <see cref="ResetForNewSession"/> when no save
/// record loaded this session, and <see cref="RestoreFromSave"/> replaces rather than merges.
/// </summary>
public sealed class FiefSiegeParticipationService : IFiefSiegeParticipationService
{
    private const char ClanSeparator = ';';
    private const char ValueSeparator = '=';

    private readonly IModLogger _logger;

    /// <summary>settlementId to (clanId to summed contribution). Long, so a sum of ints cannot wrap.</summary>
    private readonly Dictionary<string, Dictionary<string, long>> _records =
        new Dictionary<string, Dictionary<string, long>>(StringComparer.Ordinal);

    public FiefSiegeParticipationService(IModLogger logger)
    {
        _logger = logger;
    }

    public void RecordAssault(string settlementId, IReadOnlyList<KeyValuePair<string, int>> partyContributions)
    {
        if (string.IsNullOrEmpty(settlementId)) return;

        // Replace, never merge: this assault supersedes whoever stormed the place before.
        _records.Remove(settlementId);
        if (partyContributions == null) return;

        var byClan = new Dictionary<string, long>(StringComparer.Ordinal);
        var unencodable = 0;
        for (var i = 0; i < partyContributions.Count; i++)
        {
            var clanId = partyContributions[i].Key;
            var contribution = partyContributions[i].Value;
            if (string.IsNullOrEmpty(clanId) || contribution <= 0) continue;

            // An id carrying a separator could not be restored from the save string, so it is
            // dropped here rather than written as a record that silently loses a clan on load.
            if (clanId.IndexOf(ClanSeparator) >= 0 || clanId.IndexOf(ValueSeparator) >= 0)
            {
                unencodable++;
                continue;
            }

            byClan.TryGetValue(clanId, out var sum);
            byClan[clanId] = sum + contribution;
        }

        if (unencodable > 0)
            _logger?.LogWarning(
                $"[FiefGrant] {unencodable} clan id(s) in the assault on {settlementId} carry a save " +
                "separator and were not recorded.");

        if (byClan.Count > 0)
            _records[settlementId] = byClan;
    }

    public void Forget(string settlementId)
    {
        if (!string.IsNullOrEmpty(settlementId))
            _records.Remove(settlementId);
    }

    public bool HasRecord(string settlementId) =>
        !string.IsNullOrEmpty(settlementId)
        && _records.TryGetValue(settlementId, out var byClan)
        && byClan.Count > 0;

    public float GetContributionShare(string settlementId, string clanId)
    {
        if (string.IsNullOrEmpty(settlementId) || string.IsNullOrEmpty(clanId)) return 0f;
        if (!_records.TryGetValue(settlementId, out var byClan)) return 0f;
        if (!byClan.TryGetValue(clanId, out var contribution)) return 0f;

        long top = 0;
        foreach (var value in byClan.Values)
            if (value > top) top = value;

        // Every stored value is positive, so top >= contribution > 0 and the share lands in (0, 1].
        // The clamp is belt and braces for the float rounding of two near-equal longs.
        if (top <= 0) return 0f;
        var share = (float)((double)contribution / top);
        return share > 1f ? 1f : share;
    }

    public Dictionary<string, string> SnapshotForSave()
    {
        var snapshot = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var record in _records)
        {
            var parts = record.Value
                .OrderBy(kv => kv.Key, StringComparer.Ordinal)
                .Select(kv => kv.Key + ValueSeparator + kv.Value.ToString(CultureInfo.InvariantCulture));
            snapshot[record.Key] = string.Join(ClanSeparator.ToString(), parts);
        }

        return snapshot;
    }

    public void RestoreFromSave(Dictionary<string, string> snapshot)
    {
        _records.Clear();
        if (snapshot == null) return;

        var malformed = 0;
        foreach (var entry in snapshot)
        {
            if (string.IsNullOrEmpty(entry.Key))
            {
                malformed++;
                continue;
            }

            var byClan = new Dictionary<string, long>(StringComparer.Ordinal);
            if (!string.IsNullOrEmpty(entry.Value))
            {
                foreach (var part in entry.Value.Split(ClanSeparator))
                {
                    var split = part.IndexOf(ValueSeparator);
                    if (split <= 0 || split == part.Length - 1)
                    {
                        malformed++;
                        continue;
                    }

                    var clanId = part.Substring(0, split);
                    if (!long.TryParse(part.Substring(split + 1), NumberStyles.Integer,
                            CultureInfo.InvariantCulture, out var contribution)
                        || contribution <= 0)
                    {
                        malformed++;
                        continue;
                    }

                    // The writer emits each clan once (summed at record time), so a repeated id is a
                    // hand-edited string. Summing it unchecked could wrap a long into a negative share;
                    // the first occurrence stands and the rest are malformed.
                    if (byClan.ContainsKey(clanId))
                    {
                        malformed++;
                        continue;
                    }

                    byClan[clanId] = contribution;
                }
            }

            if (byClan.Count > 0)
                _records[entry.Key] = byClan;
        }

        if (malformed > 0)
            _logger?.LogWarning(
                $"[FiefGrant] {malformed} malformed part(s) in the saved siege participation record " +
                "were skipped; the affected clans are scored as absent.");
    }

    public void ResetForNewSession()
    {
        _records.Clear();
    }
}
