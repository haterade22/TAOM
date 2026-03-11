using TAOM.Features.FactionMap.Models;

namespace TAOM.Features.FactionMap;

public static class FactionMapStaticBridge
{
    private static IFactionRegistryService? _registry;

    public static void Initialize(IFactionRegistryService registry)
    {
        _registry = registry;
    }

    public static RegionData? GetRegion(string regionKey) => _registry?.GetRegion(regionKey);
    public static FactionData? GetFactionForRegion(string regionKey) => _registry?.GetFactionForRegion(regionKey);
    public static FactionData? GetFaction(string factionId) => _registry?.GetFaction(factionId);
}
