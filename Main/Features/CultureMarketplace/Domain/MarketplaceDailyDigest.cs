using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace TAOM.Features.CultureMarketplace.Domain;

/// <summary>
/// Accumulates one in-game day of per-settlement marketplace passes and renders them as a single
/// log line.
/// </summary>
/// <remarks>
/// The per-town DEBUG line survived one trim already (the <c>added &gt; 0 || topUp &gt; 0</c> gate
/// that cut a 45,080-line session) and still ran at ~30 lines/min indefinitely — 1,687 lines, 36%
/// of the 2026-08-03 tournament log, in a file whose job is crash forensics from user machines.
/// Rolling the day up trades per-town granularity for a bounded, readable log.
///
/// Two things survive the roll-up that the per-town line could not give you: the total foreign-item
/// strip across every town (the old gate excluded it from the per-line decision, so it only showed
/// on lines that happened to survive for another reason), and a picks-vs-injections divergence,
/// which previously required diffing <c>picks=</c> against <c>+N injected</c> by eye across every
/// line in the session.
///
/// Not thread-safe: driven entirely from campaign tick events on the main thread.
/// </remarks>
public sealed class MarketplaceDailyDigest
{
    private const int TopTownCount = 3;

    private readonly Dictionary<string, int> _activityByTown = new(StringComparer.Ordinal);
    private int _townsTouched;
    private int _totalPicks;
    private int _totalInjected;
    private int _totalGuaranteed;
    private int _totalRemoved;

    /// <summary>Records one settlement's daily pass. Safe to call repeatedly for the same town.</summary>
    public void Record(string? settlementId, int picks, int injected, int guaranteed, int removed)
    {
        _townsTouched++;
        _totalPicks += picks;
        _totalInjected += injected;
        _totalGuaranteed += guaranteed;
        _totalRemoved += removed;

        var activity = injected + guaranteed;
        if (activity <= 0) return;

        // An unnamed settlement still counts toward the totals above; it just cannot be named in
        // the top list.
        if (string.IsNullOrEmpty(settlementId)) return;
        _activityByTown.TryGetValue(settlementId!, out var running);
        _activityByTown[settlementId!] = running + activity;
    }

    /// <summary>
    /// Renders the accumulated day and resets. Returns <c>null</c> when nothing happened, so a
    /// quiet day costs no line at all.
    /// </summary>
    public string? Flush()
    {
        if (_totalInjected == 0 && _totalGuaranteed == 0 && _totalRemoved == 0)
        {
            Reset();
            return null;
        }

        var sb = new StringBuilder()
            .Append($"day rollup: {_activityByTown.Count}/{_townsTouched} town(s) active, ")
            .Append($"+{_totalInjected} injected, +{_totalGuaranteed} guaranteed, -{_totalRemoved} foreign");

        var rejected = _totalPicks - _totalInjected;
        if (rejected > 0) sb.Append($" — {rejected} pick(s) rejected");

        if (_activityByTown.Count > 0)
        {
            // Ties broken by id so the same day never renders two different ways.
            var top = _activityByTown
                .OrderByDescending(kv => kv.Value)
                .ThenBy(kv => kv.Key, StringComparer.Ordinal)
                .Take(TopTownCount)
                .Select(kv => $"{kv.Key} +{kv.Value}");
            sb.Append($" (top: {string.Join(", ", top)})");
        }

        Reset();
        return sb.ToString();
    }

    private void Reset()
    {
        _activityByTown.Clear();
        _townsTouched = 0;
        _totalPicks = 0;
        _totalInjected = 0;
        _totalGuaranteed = 0;
        _totalRemoved = 0;
    }
}
