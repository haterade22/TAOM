using TAOM.Core.Logging;
using TAOM.Features.CoopInterop;

namespace TAOM.Features.Diplomacy.Hooks;

public class PeaceActionHook : IOnPeaceAction
{
    private readonly IWarOfTheRingService _wotrService;
    private readonly IModLogger _logger;
    private readonly ICoopSessionProvider _coop;

    public PeaceActionHook(
        IWarOfTheRingService wotrService, IModLogger logger, ICoopSessionProvider coop)
    {
        _wotrService = wotrService;
        _logger = logger;
        _coop = coop;
    }

    public bool ShouldPreventPeace(string factionAId, string factionBId)
    {
        // #370 — see AllianceActionHook.DeferToHost for the full rationale. Vetoing a peace the
        // host already applied leaves the client at war and the host at peace.
        if (_coop.ShouldDeferToHost)
        {
            _logger.LogDebug(
                $"[WarOfTheRing][coop] Peace veto skipped for {factionAId} <-> {factionBId} — " +
                "host is authoritative");
            return false;
        }

        var shouldBlock = _wotrService.IsWarOfTheRingActive
                          && _wotrService.ShouldBlockPeace(factionAId, factionBId);
        if (shouldBlock)
            _logger.LogInfo($"[WarOfTheRing] Peace blocked: {factionAId} <-> {factionBId} (WoTR active, hostile tier)");
        else
            _logger.LogDebug($"[WarOfTheRing] Peace allowed: {factionAId} <-> {factionBId}");
        return shouldBlock;
    }
}
