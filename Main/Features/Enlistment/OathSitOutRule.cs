using TAOM.Adapters;

namespace TAOM.Features.Enlistment;

/// <summary>Why the oath keeps the soldier out of his commander's battle.</summary>
public enum OathSitOutReason
{
    None = 0,

    /// <summary>The commander's realm is at war with the player's own faction (#832).</summary>
    OwnCrown = 1,

    /// <summary>
    /// The player's kingdom is not at war with the foe (#960); choosing Attack there would declare a kingdom war via
    /// BeHostileAction. Since Bannerlord v1.5.7 the menu skips that action when the opposing leader party is a caravan
    /// or villager party, and the rule still sits those battles out (#1112, decision owed).
    /// </summary>
    Vassal = 2,
}

/// <summary>
/// The one rule for a battle the oath keeps him out of, read by the join (<see cref="ServiceBattleService"/>)
/// and by the status board, so the board never says "hold your place in the line" for such a battle (#1016).
/// It is not the join's other refusals (state, no side, not joinable), which the board does not mirror. A player already in a map event is never sat out: the existing
/// join path must not be stranded half-way.
/// </summary>
public static class OathSitOutRule
{
    /// <summary>The reason, and the commander party's opposing leader faction (null once the crown rule decided, or when unread).</summary>
    public static OathSitOutReason Decide(
        IEncounterAdapter encounter, IServiceDiplomacyService diplomacy, string commanderPartyId, out string foeFactionId)
    {
        foeFactionId = null;
        var mainInEvent = encounter.IsMainPartyInMapEvent;
        if (!mainInEvent && diplomacy.IsServingAgainstOwnCrown())
            return OathSitOutReason.OwnCrown;

        foeFactionId = encounter.GetOpposingLeaderFactionId(commanderPartyId);
        if (!mainInEvent && !string.IsNullOrEmpty(foeFactionId) && diplomacy.ShouldSitOutAsVassal(foeFactionId))
            return OathSitOutReason.Vassal;
        return OathSitOutReason.None;
    }
}
