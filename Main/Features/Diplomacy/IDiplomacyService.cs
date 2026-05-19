using TAOM.Features.Diplomacy.Models;

namespace TAOM.Features.Diplomacy;

public interface IDiplomacyService
{
    AllianceTier GetRelationshipTier(string kingdomAId, string kingdomBId);
    float GetAllianceScoreModifier(string kingdomAId, string kingdomBId);
    bool IsAllianceAllowed(string kingdomAId, string kingdomBId);
    bool IsWarAllowed(string kingdomAId, string kingdomBId);
    void EstablishInitialAlliances();
    void EnforcePermanentAlliances();
}
