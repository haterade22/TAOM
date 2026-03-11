using System.Linq;
using TAOM.Features.FactionMap.Models;

namespace TAOM.Features.FactionMap;

public class FactionHoverService : IFactionHoverService
{
    private readonly IFactionRegistryService _registry;
    private string _lastHoveredFaction = "";

    public FactionHoverService(IFactionRegistryService registry)
    {
        _registry = registry;
    }

    public HoverStateChange? UpdateHover(string currentHoveredFaction)
    {
        string current = currentHoveredFaction ?? "";

        if (current == _lastHoveredFaction)
            return null;

        _lastHoveredFaction = current;

        if (string.IsNullOrEmpty(current))
            return new HoverStateChange { ShouldShow = false };

        string colorHex = FindColorForFactionName(current);

        return new HoverStateChange
        {
            FactionName = current,
            ColorHex = colorHex,
            ShouldShow = true,
        };
    }

    public void Reset()
    {
        _lastHoveredFaction = "";
    }

    private string FindColorForFactionName(string factionName)
    {
        var allKeys = _registry.GetAllRegionKeys();
        if (allKeys == null) return "#FFFFFFFF";

        foreach (var key in allKeys)
        {
            var faction = _registry.GetFactionForRegion(key);
            if (faction?.Name == factionName && !string.IsNullOrEmpty(faction.Color))
                return faction.Color;
        }

        return "#FFFFFFFF";
    }
}
