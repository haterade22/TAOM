using System.Collections.Generic;
using System.Linq;
using TAOM.Adapters;
using TAOM.Features.FactionPower;

namespace TAOM.Tests.Features.FactionPower;

internal sealed class FakeAdvancedStartData : IAdvancedStartDataAdapter
{
    public readonly Dictionary<string, object> Values = new Dictionary<string, object>();
    public readonly List<string> Categories = new List<string>();
    public bool IsAvailable { get; set; } = true;
    public bool IsNewCampaign { get; set; } = true;

    public bool TryGet<T>(string key, out T value)
    {
        if (IsAvailable && Values.TryGetValue(key, out var raw) && raw is T typed)
        {
            value = typed;
            return true;
        }
        value = default!;
        return false;
    }

    public bool Add<T>(string key, string categoryId, T value)
    {
        if (!IsAvailable || Values.ContainsKey(key)) return false;
        Values[key] = value!;
        Categories.Add(categoryId);
        return true;
    }

    public bool Set<T>(string key, T value)
    {
        if (!IsAvailable || !Values.TryGetValue(key, out var raw) || raw is not T) return false;
        Values[key] = value!;
        return true;
    }
}

internal sealed class FakeFactionPowerCatalog : IFactionPowerCatalog
{
    public FakeFactionPowerCatalog(params string[] cultures)
    {
        Entries = cultures.Select(c => new FactionPowerEntry(c, c, "neutral")).ToList();
    }

    public IReadOnlyList<FactionPowerEntry> Entries { get; }
}

internal sealed class FakeFactionPowerSettings : IFactionPowerSettings
{
    public readonly Dictionary<string, int> Percents = new Dictionary<string, int>();
    public int Reloads;

    public float Multiplier(string? cultureId) =>
        cultureId != null && Percents.TryGetValue(cultureId, out var p) ? p / 100f : 1f;

    public int Percent(string? cultureId) => cultureId != null && Percents.TryGetValue(cultureId, out var p) ? p : 100;
    public IReadOnlyDictionary<string, int> Snapshot() => Percents;
    public void Reload() => Reloads++;

    public bool TrySet(string cultureId, int percent)
    {
        Percents[cultureId] = percent;
        return true;
    }
}
