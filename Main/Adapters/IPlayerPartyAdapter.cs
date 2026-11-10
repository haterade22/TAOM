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

    /// <summary>The main hero's clan kingdom StringId, or null (no hero, no clan, no kingdom).</summary>
    string GetMainHeroKingdomId();

    /// <summary>The main hero's culture StringId, or null when there is no main hero.</summary>
    string GetMainHeroCultureId();

    /// <summary>The main hero's gold, or 0 when there is no main hero.</summary>
    int GetMainHeroGold();

    /// <summary>Takes <paramref name="amount"/> gold from the main hero. Returns false, and takes
    /// nothing, when the amount is not positive, there is no main hero, or the hero holds less.</summary>
    bool TryDebitMainHeroGold(int amount);
}
