using System;
using System.Collections.Generic;
using System.Linq;

namespace TAOM.Features.TrainingGrounds.Waves.Domain;

/// <summary>The campaign day on which each town last saw a wave run, so a town can allow one run a day. Saved by the dialog behavior.</summary>
internal sealed class WaveDayLedger
{
    private readonly Dictionary<string, int> _days = new(StringComparer.Ordinal);

    public bool RanToday(string townId, int today) => _days.TryGetValue(townId, out var day) && day == today;

    public void Record(string townId, int today) => _days[townId] = today;

    /// <summary>Drops the town's day: a run that fought no wave does not use it up.</summary>
    public void Forget(string townId) => _days.Remove(townId);

    /// <summary>A copy of the towns that ran today (or later): a past day is dropped, so the save never grows past the towns of one day.</summary>
    public Dictionary<string, int> Export(int today) =>
        _days.Where(pair => pair.Value >= today).ToDictionary(pair => pair.Key, pair => pair.Value, StringComparer.Ordinal);

    /// <summary>Replaces the ledger with a saved one; null (a save with no record) leaves it empty.</summary>
    public void Import(Dictionary<string, int>? days)
    {
        _days.Clear();
        if (days == null)
            return;
        foreach (var pair in days)
        {
            if (!string.IsNullOrEmpty(pair.Key))
                _days[pair.Key] = pair.Value;
        }
    }

    public void Clear() => _days.Clear();
}
