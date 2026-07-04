using System.Collections.Generic;

namespace TAOM.Adapters;

public interface IAllianceAdapter
{
    IReadOnlyList<string> GetAllKingdomIds();

    /// <summary>
    /// The kingdom's Culture StringId, or null when the kingdom or its culture is
    /// unresolvable. Used by WotR-momentum enrollment to side player-founded kingdoms
    /// (whose kingdom StringId isn't in alignment.json) by their culture.
    /// </summary>
    string GetKingdomCultureId(string kingdomId);

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
