using TAOM.Features.Diplomacy.Models;

namespace TAOM.Features.Diplomacy.Hooks;

public class AllianceActionHook : IOnAllianceAction
{
    private readonly IDiplomacyService _diplomacyService;

    public AllianceActionHook(IDiplomacyService diplomacyService)
    {
        _diplomacyService = diplomacyService;
    }

    public bool ShouldPreventAllianceEnd(string kingdomAId, string kingdomBId)
    {
        return _diplomacyService.GetRelationshipTier(kingdomAId, kingdomBId) == AllianceTier.Permanent;
    }

    public bool ShouldPreventWarDeclaration(string factionAId, string factionBId)
    {
        return _diplomacyService.GetRelationshipTier(factionAId, factionBId) == AllianceTier.Permanent;
    }
}
