using System.Collections.Generic;

namespace TAOM.Adapters;

/// <summary>
/// The wars an enlisted soldier inherits from his commander, and gives back on discharge
/// (field report 5). ADR-007 boundary — <c>IFaction</c>, <c>Kingdom</c> and <c>Clan</c> stay here.
///
/// Faction ids are <c>IFaction.StringId</c>, and MINOR FACTIONS ARE INCLUDED deliberately.
/// ServeAsSoldier's own changelog admits it ignores them, which is why a SAS player keeps mercenary
/// and bandit-clan wars after discharge with no way to see where they came from.
/// </summary>
public interface IServiceDiplomacyAdapter
{
    /// <summary>Faction ids the given hero's map faction is at war with. Empty when unresolvable.</summary>
    IReadOnlyList<string> GetEnemiesOf(string heroId);

    /// <summary>Faction ids the PLAYER's map faction is at war with.</summary>
    IReadOnlyList<string> GetPlayerEnemies();

    /// <summary>
    /// Id of the faction <see cref="DeclareWarOn"/> and <see cref="MakePeaceWith"/> would act on
    /// behalf of right now, or null when unresolvable.
    ///
    /// NOT stable across a term of service, which is the entire reason it is exposed.
    /// <c>Hero.MapFaction</c> is <c>Clan.Kingdom ?? Clan</c> (verified 1.4.8) and the enlist gate
    /// admits a player whose clan is already a vassal — so a clan that joins or leaves a kingdom
    /// mid-service changes this id, and a peace made under the new one would move a whole kingdom's
    /// diplomacy on behalf of a war the player declared alone. The caller pins it at oath and
    /// refuses to unwind under a different identity.
    /// </summary>
    string GetPlayerFactionId();

    /// <summary>
    /// Put the player's map faction at war with the given faction. Uses
    /// <c>ApplyByPlayerHostility</c> — the same reason vanilla already records for a player who
    /// joins a lord's fight — so the war reads as the player's own act rather than an unexplained
    /// kingdom decision.
    /// </summary>
    bool DeclareWarOn(string factionId);

    /// <summary>Make peace between the player's map faction and the given faction.</summary>
    bool MakePeaceWith(string factionId);
}
