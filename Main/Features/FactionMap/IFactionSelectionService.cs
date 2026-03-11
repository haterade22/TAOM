using TAOM.Features.FactionMap.Models;

namespace TAOM.Features.FactionMap;

public interface IFactionSelectionService
{
    FactionSelectionResult SelectRegion(string regionName);
    string? GetCultureIdForRegion(string regionName);
    string MakeDarkPanelHex(string factionHex);
    string MakeAccentColorHex(string factionHex);
    string FormatDifficultyText(int difficulty);
}
