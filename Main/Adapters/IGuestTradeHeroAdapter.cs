namespace TAOM.Adapters;

/// <summary>
/// What the host knows about a co-op guest's hero when that guest asks to trade (#1329). <see cref="Player"/> and
/// <see cref="Party"/> act on this hero, never on the host's own: TAOM's emissary and armoury services take them in
/// place of the main hero's adapters.
/// </summary>
/// <param name="IsFreeLeader">Alive, not a prisoner, and the leader of an active party.</param>
/// <param name="SettlementId">The settlement the hero's party is in, or null.</param>
/// <param name="AtWarWithSettlement">The hero's map faction is at war with the settlement's.</param>
/// <param name="Owner">The owner of <paramref name="SettlementId"/>, or null when the hero is in none.</param>
/// <param name="IsAlive">The hero is alive; a dead hero is never a free leader either.</param>
public sealed record GuestTradeHero(
    string HeroId,
    bool IsFreeLeader,
    string? SettlementId,
    bool InBattle,
    bool AtWarWithSettlement,
    SettlementOwnerInfo? Owner,
    IArmouryPlayerAdapter Player,
    IPlayerPartyAdapter Party,
    bool IsAlive = true);

/// <summary>Finds a hero by StringId on the host for guest trade (#1329).</summary>
public interface IGuestTradeHeroAdapter
{
    /// <summary>The hero, or null when no hero has this id.</summary>
    GuestTradeHero? Find(string heroId);
}
