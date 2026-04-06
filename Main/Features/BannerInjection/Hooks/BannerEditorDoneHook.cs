using TAOM.Core.Logging;

namespace TAOM.Features.BannerInjection.Hooks;

public class BannerEditorDoneHook : IOnBannerEditorDone
{
    private readonly IBannerExclusionService _exclusionService;
    private readonly IModLogger _logger;

    public BannerEditorDoneHook(IBannerExclusionService exclusionService, IModLogger logger)
    {
        _exclusionService = exclusionService;
        _logger = logger;
    }

    public void OnBannerEditorDone(string clanStringId, string kingdomStringId)
    {
        _exclusionService.MarkAsPlayerModified(clanStringId);
        _logger.LogInfo($"BannerInjection: Marked clan '{clanStringId}' as player-modified.");

        if (!string.IsNullOrEmpty(kingdomStringId))
        {
            _exclusionService.MarkAsPlayerModified(kingdomStringId);
            _logger.LogInfo($"BannerInjection: Marked kingdom '{kingdomStringId}' as player-modified (ruler banner).");
        }
    }
}
