using TAOM.Core.Logging;

namespace TAOM.Features.Diplomacy.Hooks;

public class PeaceActionHook : IOnPeaceAction
{
    private readonly IWarOfTheRingService _wotrService;
    private readonly IModLogger _logger;

    public PeaceActionHook(IWarOfTheRingService wotrService, IModLogger logger)
    {
        _wotrService = wotrService;
        _logger = logger;
    }

    public bool ShouldPreventPeace(string factionAId, string factionBId)
    {
        var shouldBlock = _wotrService.IsWarOfTheRingActive
                          && _wotrService.ShouldBlockPeace(factionAId, factionBId);
        if (shouldBlock)
            _logger.LogInfo($"[WarOfTheRing] Peace blocked: {factionAId} <-> {factionBId} (WoTR active, hostile tier)");
        else
            _logger.LogDebug($"[WarOfTheRing] Peace allowed: {factionAId} <-> {factionBId}");
        return shouldBlock;
    }
}
