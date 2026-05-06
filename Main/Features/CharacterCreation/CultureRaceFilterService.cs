using System;
using System.Collections.Generic;
using System.Linq;
using TAOM.Core.Logging;

namespace TAOM.Features.CharacterCreation;

public class CultureRaceFilterService : ICultureRaceFilterService
{
    private readonly ICultureCreationDataProvider _provider;
    private readonly IModLogger _logger;
    private readonly HashSet<string> _warnedCultures = new(StringComparer.OrdinalIgnoreCase);

    public CultureRaceFilterService(ICultureCreationDataProvider provider, IModLogger logger)
    {
        _provider = provider;
        _logger = logger;
    }

    public bool HasFilter(string cultureId)
    {
        if (string.IsNullOrEmpty(cultureId)) return false;
        var data = _provider.GetCultureData(cultureId);
        return data?.Races != null && data.Races.Length > 0;
    }

    public IReadOnlyList<string> GetAllowedRaces(string cultureId)
    {
        if (string.IsNullOrEmpty(cultureId)) return Array.Empty<string>();

        var data = _provider.GetCultureData(cultureId);
        if (data?.Races != null && data.Races.Length > 0)
            return data.Races;

        if (_warnedCultures.Add(cultureId))
            _logger.LogWarning($"CultureRaceFilterService: no race filter for culture '{cultureId}'; dropdown will not be filtered.");

        return Array.Empty<string>();
    }

    public bool IsRaceAllowed(string cultureId, string raceName)
    {
        if (string.IsNullOrEmpty(raceName)) return false;
        var allowed = GetAllowedRaces(cultureId);
        if (allowed.Count == 0) return true;
        return allowed.Any(r => string.Equals(r, raceName, StringComparison.OrdinalIgnoreCase));
    }
}
