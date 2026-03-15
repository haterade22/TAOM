using System;
using System.Linq;
using TAOM.Core.Logging;

namespace TAOM.Features.CharacterCreation.Hooks;

public class GetRaceNamesHook : IOnGetRaceNames
{
    private readonly ICultureCreationDataProvider _dataProvider;
    private readonly IModLogger _logger;
    private readonly Func<string> _getSelectedCultureId;

    private string[] _unfilteredRaces;
    private string[] _lastFilteredRaces;

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
        _unfilteredRaces = allRaces;

        var cultureId = _getSelectedCultureId();
        if (string.IsNullOrEmpty(cultureId))
        {
            _lastFilteredRaces = allRaces;
            return allRaces;
        }

        var cultureData = _dataProvider.GetCultureData(cultureId);
        if (cultureData?.Races == null || cultureData.Races.Length == 0)
        {
            _lastFilteredRaces = allRaces;
            return allRaces;
        }

        var filtered = allRaces
            .Where(r => cultureData.Races.Any(
                allowed => string.Equals(allowed, r, StringComparison.OrdinalIgnoreCase)))
            .ToArray();

        if (filtered.Length == 0)
        {
            _logger.LogWarning($"No matching races found for culture '{cultureId}' — returning all races");
            _lastFilteredRaces = allRaces;
            return allRaces;
        }

        _logger.LogInfo($"Filtered races for '{cultureId}': {string.Join(", ", filtered)}");
        _lastFilteredRaces = filtered;
        return filtered;
    }

    public int MapFilteredIndexToGlobalId(int filteredIndex)
    {
        if (_unfilteredRaces == null || _lastFilteredRaces == null)
            return filteredIndex;

        if (filteredIndex < 0 || filteredIndex >= _lastFilteredRaces.Length)
            return filteredIndex;

        var raceName = _lastFilteredRaces[filteredIndex];
        var globalIndex = Array.IndexOf(_unfilteredRaces, raceName);

        return globalIndex >= 0 ? globalIndex : filteredIndex;
    }
}
