using System;
using TaleWorlds.CampaignSystem;
using TaleWorlds.ObjectSystem;
using TAOM.Core.Logging;

namespace TAOM.Adapters;

public class CultureObjectAdapter : ICultureObjectAdapter
{
    private readonly IModLogger _logger;

    public CultureObjectAdapter(IModLogger logger)
    {
        _logger = logger;
    }

    public object? ResolveCulture(string cultureId)
    {
        if (string.IsNullOrEmpty(cultureId)) return null;
        try
        {
            return MBObjectManager.Instance?.GetObject<CultureObject>(cultureId);
        }
        catch (Exception ex)
        {
            _logger.LogError($"CultureObjectAdapter: Failed to resolve '{cultureId}': {ex.Message}");
            return null;
        }
    }
}
