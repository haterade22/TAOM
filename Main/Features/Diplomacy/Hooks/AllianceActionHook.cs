using TAOM.Core.Logging;
using TAOM.Features.Diplomacy.Models;

namespace TAOM.Features.Diplomacy.Hooks;

public class AllianceActionHook : IOnAllianceAction
{
    private readonly IDiplomacyService _diplomacyService;
    private readonly IModLogger _logger;

    public AllianceActionHook(IDiplomacyService diplomacyService, IModLogger logger)
    {
        _diplomacyService = diplomacyService;
        _logger = logger;
    }

    public bool ShouldPreventAllianceEnd(string kingdomAId, string kingdomBId)
    {
        var isPermanent = _diplomacyService.GetRelationshipTier(kingdomAId, kingdomBId) == AllianceTier.Permanent;
        if (isPermanent)
            _logger.LogInfo($"[Diplomacy] Alliance end blocked: {kingdomAId} <-> {kingdomBId} (Permanent)");
        return isPermanent;
    }

    public bool ShouldPreventWarDeclaration(string factionAId, string factionBId)
    {
        var blocked = !_diplomacyService.IsWarAllowed(factionAId, factionBId);
        if (blocked)
            _logger.LogInfo($"[Diplomacy] War declaration blocked: {factionAId} <-> {factionBId}");
        return blocked;
    }
}
