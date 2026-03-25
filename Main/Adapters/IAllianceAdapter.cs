using System.Collections.Generic;

namespace TAOM.Adapters;

public interface IAllianceAdapter
{
    IReadOnlyList<string> GetAllKingdomIds();
    bool AreAllied(string kingdomAId, string kingdomBId);
    void StartAlliance(string kingdomAId, string kingdomBId);
}
