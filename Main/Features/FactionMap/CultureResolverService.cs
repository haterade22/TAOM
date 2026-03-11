using TAOM.Adapters;
using TAOM.Core.Logging;

namespace TAOM.Features.FactionMap;

public class CultureResolverService : ICultureResolverService
{
    private readonly ICultureObjectAdapter _cultureAdapter;
    private readonly IModLogger _logger;

    public CultureResolverService(ICultureObjectAdapter cultureAdapter, IModLogger logger)
    {
        _cultureAdapter = cultureAdapter;
        _logger = logger;
    }

    public object? ResolveCulture(string cultureId)
    {
        var result = _cultureAdapter.ResolveCulture(cultureId);

        if (result == null && !string.IsNullOrEmpty(cultureId))
        {
            _logger.LogError($"Failed to resolve culture '{cultureId}' - culture not found in object manager");
        }

        return result;
    }

    public bool IsCultureAvailable(string cultureId)
    {
        return ResolveCulture(cultureId) != null;
    }
}
