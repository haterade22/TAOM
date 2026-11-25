using System;
using System.Collections.Generic;
using System.Linq;
using TAOM.Adapters;
using TAOM.Core.Logging;

namespace TAOM.Features.FactionPower;

/// <summary>
/// Reads and writes the faction power values in the campaign's own option data, which the engine saves with it (no TAOM save
/// definer; a save still loads with TAOM removed). One int percent per realm culture that is not 100, under
/// <c>taom_fp_&lt;culture&gt;</c>; an absent key is 100, so an older save is all 100. A stored value is untrusted: outside 50 to
/// 200 it is ignored with a warning, never clamped. The snapshot is immutable and replaced whole, so a reader on the AI worker
/// thread sees the old or the new, never a mix. A read before any campaign exists is not remembered.
/// </summary>
public sealed class FactionPowerStore : IFactionPowerSettings
{
    public const string VersionKey = "taom_fp_version";
    public const string KeyPrefix = "taom_fp_";
    public const string Category = "taom_faction_power";
    public const int CurrentVersion = 1;

    private static readonly Values Empty = new Values(new Dictionary<string, int>());

    private readonly IAdvancedStartDataAdapter _data;
    private readonly IFactionPowerCatalog _catalog;
    private readonly IModLogger _logger;
    private volatile Values? _snapshot;

    public FactionPowerStore(IAdvancedStartDataAdapter data, IFactionPowerCatalog catalog, IModLogger logger)
    {
        _data = data;
        _catalog = catalog;
        _logger = logger;
    }

    public float Multiplier(string? cultureId)
    {
        if (string.IsNullOrEmpty(cultureId)) return 1f;
        return Current.Multipliers.TryGetValue(cultureId!, out var multiplier) ? multiplier : 1f;
    }

    public int Percent(string? cultureId)
    {
        if (string.IsNullOrEmpty(cultureId)) return FactionPowerMath.NeutralPercent;
        return Current.Percents.TryGetValue(cultureId!, out var percent) ? percent : FactionPowerMath.NeutralPercent;
    }

    public IReadOnlyDictionary<string, int> Snapshot() => Current.Percents;

    public void Reload()
    {
        _snapshot = null;
        var current = Current;
        if (!_data.IsAvailable) return;
        var table = current.Percents.Count == 0
            ? "all realms at 100"
            : string.Join(" ", current.Percents.OrderBy(p => p.Key, StringComparer.Ordinal).Select(p => $"{p.Key}={p.Value}"));
        _logger.LogInfo($"FactionPower.Loaded: {table}");
    }

    public bool TrySet(string cultureId, int percent)
    {
        if (!FactionPowerMath.IsValidPercent(percent)) return false;
        if (!_catalog.Entries.Any(e => string.Equals(e.CultureId, cultureId, StringComparison.Ordinal))) return false;
        if (!_data.IsAvailable) return false;

        var key = KeyPrefix + cultureId;
        if (!_data.Set(key, percent) && !_data.Add(key, Category, percent)) return false;
        if (!_data.TryGet<int>(VersionKey, out _)) _data.Add(VersionKey, Category, CurrentVersion);

        _snapshot = null;
        return true;
    }

    private Values Current
    {
        get
        {
            var snapshot = _snapshot;
            if (snapshot != null) return snapshot;
            if (!_data.IsAvailable) return Empty;
            return _snapshot = Build();
        }
    }

    private Values Build()
    {
        if (_data.TryGet<int>(VersionKey, out var version) && version > CurrentVersion)
            _logger.LogWarning($"FactionPower: this campaign was saved by a newer TAOM (version {version}, this build reads {CurrentVersion}); reading the keys it knows");

        var percents = new Dictionary<string, int>();
        foreach (var entry in _catalog.Entries)
        {
            if (!_data.TryGet<int>(KeyPrefix + entry.CultureId, out var percent)) continue;
            if (!FactionPowerMath.IsValidPercent(percent))
            {
                _logger.LogWarning($"FactionPower: stored percent {percent} for '{entry.CultureId}' is outside {FactionPowerMath.MinPercent} to {FactionPowerMath.MaxPercent}; using 100");
                continue;
            }
            if (percent != FactionPowerMath.NeutralPercent) percents[entry.CultureId] = percent;
        }

        return new Values(percents);
    }

    private sealed class Values
    {
        public Values(Dictionary<string, int> percents)
        {
            Percents = percents;
            Multipliers = percents.ToDictionary(p => p.Key, p => FactionPowerMath.ToMultiplier(p.Value));
        }

        public Dictionary<string, int> Percents { get; }
        public Dictionary<string, float> Multipliers { get; }
    }
}
