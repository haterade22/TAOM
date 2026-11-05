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
    /// <summary>
    /// Model-aware war check (Kingdom.IsAtWarWith): also true for a pair the DiplomacyModel calls constantly at war, as TaomDiplomacyModel does for every Hostile pair in Full War while blockPeaceBetweenHostileTiers is on, so never guard a declaration with it; use <see cref="HasDeclaredWar"/>.
    /// </summary>
    bool AreAtWar(string kingdomAId, string kingdomBId);

    /// <summary>
    /// Whether the stored stance link between two kingdoms is War. This is not a pure read:
    /// <c>Kingdom.GetStanceWith</c> creates and saves a missing link (unless either kingdom is
    /// eliminated), typed from <c>DiplomacyModel.GetDefaultDiplomaticStance</c>, which is War
    /// whenever <c>IsAtConstantWar</c> is true. During Full War that covers every Hostile pair, so a
    /// Hostile pair with no link reads true and is stored as War, with no DeclareWarAction and no
    /// OnWarDeclared. Vanilla's daily tribute pass (<c>FactionHelper.GetStances</c>) stores the same
    /// War link, so true means "stored as War", not "declared". Unlike <see cref="AreAtWar"/>, a pair
    /// that already has a link is answered from the link alone, so a neutral link reads false while
    /// the model reports war. A kingdom against itself, or an eliminated kingdom, reads false.
    /// War of the Ring guards its declarations with this (#772).
    /// </summary>
    bool HasDeclaredWar(string kingdomAId, string kingdomBId);
    void DeclareWar(string kingdomAId, string kingdomBId);

    /// <summary>
    /// End an active war between two kingdoms. No-op when not at war.
    /// Required by EnforcePermanentAlliances when loading an existing save where
    /// the kingdoms were previously at war (vanilla's StartAlliance does NOT
    /// end the war on its own — would leave allied-AND-at-war contradictory state).
    /// </summary>
    void MakePeace(string kingdomAId, string kingdomBId);
}
