using System.Collections.Generic;
using System.Linq;
using TAOM.Features.FactionMap.Models;

namespace TAOM.Features.FactionMap;

public class FactionRegistryService : IFactionRegistryService
{
    private Dictionary<string, RegionData> _regions = new();
    private Dictionary<string, FactionData> _factions = new();

    public void Initialize(Dictionary<string, RegionData> regions, Dictionary<string, FactionData> factions)
    {
        _regions = regions;
        _factions = factions;
    }

    public RegionData? GetRegion(string regionKey)
    {
        if (string.IsNullOrEmpty(regionKey))
            return null;

        return _regions.TryGetValue(regionKey, out var region) ? region : null;
    }

    public FactionData? GetFactionForRegion(string regionKey)
    {
        if (string.IsNullOrEmpty(regionKey))
            return null;

        if (!_regions.TryGetValue(regionKey, out var region))
            return null;

        if (string.IsNullOrEmpty(region.FactionId))
            return null;

        return _factions.TryGetValue(region.FactionId, out var faction) ? faction : null;
    }

    public IEnumerable<string> GetAllRegionKeys()
    {
        return _regions.Keys;
    }

    public FactionData? GetFaction(string factionId)
    {
        if (string.IsNullOrEmpty(factionId))
            return null;

        return _factions.TryGetValue(factionId, out var faction) ? faction : null;
    }
}
