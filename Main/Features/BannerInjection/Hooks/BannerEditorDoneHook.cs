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

    public void OnBannerEditorDone(string clanStringId)
    {
        _exclusionService.MarkAsPlayerModified(clanStringId);
        _logger.LogInfo($"BannerInjection: Marked clan '{clanStringId}' as player-modified.");
    }
}
