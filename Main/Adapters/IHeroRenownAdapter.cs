namespace TAOM.Adapters;

/// <summary>
/// Grants clan renown to a hero by id. ADR-007 boundary — services never touch <c>Hero</c> or
/// <c>Clan</c>.
///
/// Id-based rather than hero-scoped (unlike <c>IQuestHeroAdapter</c>, which is constructed around
/// one hero) because the enlistment reward chokepoint already works in ids and pays whoever is
/// serving, including after discharge has cleared the record.
/// </summary>
public interface IHeroRenownAdapter
{
    /// <summary>
    /// Add <paramref name="amount"/> renown to the hero's clan. No-op for a non-positive amount or
    /// an unresolvable hero. Returns false when nothing was granted.
    /// </summary>
    bool AddClanRenown(string heroId, int amount);
}
