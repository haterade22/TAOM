using System.Collections.Generic;

namespace TAOM.Adapters;

public interface IAllianceAdapter
{
    IReadOnlyList<string> GetAllKingdomIds();
    bool AreAllied(string kingdomAId, string kingdomBId);
    void StartAlliance(string kingdomAId, string kingdomBId);
    bool AreAtWar(string kingdomAId, string kingdomBId);
    void DeclareWar(string kingdomAId, string kingdomBId);

    /// <summary>
    /// End an active war between two kingdoms. No-op when not at war.
    /// Required by EnforcePermanentAlliances when loading an existing save where
    /// the kingdoms were previously at war (vanilla's StartAlliance does NOT
    /// end the war on its own — would leave allied-AND-at-war contradictory state).
    /// </summary>
    void MakePeace(string kingdomAId, string kingdomBId);
}
