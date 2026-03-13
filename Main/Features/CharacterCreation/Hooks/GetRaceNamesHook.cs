using System;
using System.Linq;
using TAOM.Core.Logging;

namespace TAOM.Features.CharacterCreation.Hooks;

public class GetRaceNamesHook : IOnGetRaceNames
{
    private readonly ICultureCreationDataProvider _dataProvider;
    private readonly IModLogger _logger;
    private readonly Func<string> _getSelectedCultureId;

    public GetRaceNamesHook(
        ICultureCreationDataProvider dataProvider,
        IModLogger logger,
        Func<string> getSelectedCultureId)
    {
        _dataProvider = dataProvider;
        _logger = logger;
        _getSelectedCultureId = getSelectedCultureId;
    }

    public string[] FilterRaceNames(string[] allRaces)
    {
        var cultureId = _getSelectedCultureId();
        if (string.IsNullOrEmpty(cultureId))
            return allRaces;

        var cultureData = _dataProvider.GetCultureData(cultureId);
        if (cultureData?.Races == null || cultureData.Races.Length == 0)
            return allRaces;

        var filtered = allRaces
            .Where(r => cultureData.Races.Any(
                allowed => string.Equals(allowed, r, StringComparison.OrdinalIgnoreCase)))
            .ToArray();

        if (filtered.Length == 0)
        {
            _logger.LogWarning($"No matching races found for culture '{cultureId}' — returning all races");
            return allRaces;
        }

        _logger.LogInfo($"Filtered races for '{cultureId}': {string.Join(", ", filtered)}");
        return filtered;
    }
}
