namespace TAOM.Adapters;

/// <summary>Boundary over the main party's roster for adding purchased troops (ADR-007 — keeps
/// <c>MobileParty</c>/<c>CharacterObject</c> out of services).</summary>
public interface IPlayerPartyAdapter
{
    /// <summary>The main hero's StringId, or null when there is no main hero.</summary>
    string GetMainHeroId();

    /// <summary>Adds <paramref name="count"/> of <paramref name="troopId"/> to the main party roster.
    /// Returns false (no charge should follow) when there is no main party, the troop id doesn't
    /// resolve to a <c>CharacterObject</c>, or the add throws.</summary>
    bool GrantTroop(string troopId, int count);
}
