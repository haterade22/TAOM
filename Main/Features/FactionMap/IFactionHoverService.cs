using TAOM.Features.FactionMap.Models;

namespace TAOM.Features.FactionMap;

public interface IFactionHoverService
{
    HoverStateChange? UpdateHover(string currentHoveredFaction);
    void Reset();
}
