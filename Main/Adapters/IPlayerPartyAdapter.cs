namespace TAOM.Adapters;

/// <summary>Boundary over the main hero and party (ADR-007 — keeps <c>Hero</c>, <c>MobileParty</c> and
/// <c>CharacterObject</c> out of services): the hero id, the party's daily wage and purchased troops.</summary>
public interface IPlayerPartyAdapter
{
    /// <summary>The main hero's StringId, or null when there is no main hero.</summary>
    string GetMainHeroId();

    /// <summary>
    /// The main party's daily wage, the number the clan finance model charges the player (#839), or 0
    /// when there is no party or the read fails (logged once until a read succeeds).
    /// </summary>
    int GetMainPartyDailyWage();

    /// <summary>How many more party slots the main party has before it passes its size limit. 0 when the party
    /// is full or over the limit, when there is no party, or when the read fails (logged once until a read
    /// succeeds). Under Troop Weight the engine limit is already reduced by the party's weight surplus, so
    /// these slots are weighted; <see cref="GetPartySlotsPerTroop"/> gives what one troop fills.</summary>
    int GetMainPartyFreeSlots();

    /// <summary>The party slots one <paramref name="troopId"/> fills in the main party: its troop weight while
    /// Troop Weight is on, else 1. The caller treats a value that is not above 1 as 1.</summary>
    float GetPartySlotsPerTroop(string troopId);

    /// <summary>Adds <paramref name="count"/> of <paramref name="troopId"/> to the main party roster.
    /// Returns false (no charge should follow) when there is no main party, the troop id doesn't
    /// resolve to a <c>CharacterObject</c>, or the add throws.</summary>
    bool GrantTroop(string troopId, int count);
}
