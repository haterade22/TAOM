using System;
using TAOM.Adapters;
using TAOM.Core.Logging;

namespace TAOM.Features.CharacterCreation;

public class CCBodyPropertiesService : ICCBodyPropertiesService
{
    private readonly ICCBodyPropertiesProvider _provider;
    private readonly IPlayerBodyPropertiesAdapter _adapter;
    private readonly IModLogger _logger;

    public CCBodyPropertiesService(
        ICCBodyPropertiesProvider provider,
        IPlayerBodyPropertiesAdapter adapter,
        IModLogger logger)
    {
        _provider = provider;
        _adapter = adapter;
        _logger = logger;
    }

    public void ApplyForCulture(string cultureId)
    {
        if (string.IsNullOrEmpty(cultureId))
            return;

        var xml = _provider.GetBodyPropertiesXml(cultureId);
        if (xml == null)
            return;

        try
        {
            if (_adapter.TryApplyFromXml(xml))
                _logger.LogInfo($"CCBodyPropertiesService: applied culture body for '{cultureId}'");
            else
                _logger.LogWarning($"CCBodyPropertiesService: adapter rejected body XML for '{cultureId}', could not apply");
        }
        catch (Exception ex)
        {
            _logger.LogError($"CCBodyPropertiesService: failed to apply body for '{cultureId}': {ex.Message}");
        }
    }
}
