using System.Collections.Generic;
using TAOM.Features.FactionMap.Models;

namespace TAOM.Features.FactionMap;

public interface IFactionRegistryService
{
    void Initialize(Dictionary<string, RegionData> regions, Dictionary<string, FactionData> factions);
    RegionData? GetRegion(string regionKey);
    FactionData? GetFactionForRegion(string regionKey);
    IEnumerable<string> GetAllRegionKeys();
    FactionData? GetFaction(string factionId);
}
