namespace TAOM.Adapters;

/// <summary>
/// Engine boundary for <see cref="TAOM.Features.LordSpawnGuard.ILordSpawnGuardService"/> — everything
/// vanilla's <c>HeroSpawnCampaignBehavior.SpawnLordParty</c> needs to know about a hero's faction,
/// expressed in string ids (ADR-007: <c>Hero</c>, <c>Clan</c>, <c>Kingdom</c> and <c>Settlement</c>
/// are sealed and never leave the adapter).
///
/// The five <c>Get*SettlementId</c> members are candidate <i>sources</i>, not a policy — the
/// precedence order lives in the service so it stays unit-testable. Each returns null when that
/// particular source has nothing to offer.
/// </summary>
public interface ILordSpawnGuardAdapter
{
    /// <summary>
    /// String id of the hero's map faction — its kingdom when it belongs to one, otherwise its clan.
    /// Null when the hero or its faction can't be resolved.
    /// </summary>
    string GetHeroMapFactionId(string heroId);

    /// <summary>Culture id of the hero itself (not of its clan — vanilla reads <c>hero.Culture</c>).</summary>
    string GetHeroCultureId(string heroId);

    /// <summary>True when the hero's map faction already has an <c>InitialHomeSettlement</c>.</summary>
    bool FactionHasInitialHomeSettlement(string heroId);

    /// <summary>True when at least one settlement in the world carries the hero's culture.</summary>
    bool AnySettlementHasHeroCulture(string heroId);

    string GetHeroHomeSettlementId(string heroId);

    string GetHeroBornSettlementId(string heroId);

    string GetClanLeaderSettlementId(string heroId);

    /// <summary>Closest settlement whose owner is not at war with the hero's faction.</summary>
    string GetNearestFriendlySettlementId(string heroId);

    /// <summary>Closest settlement of any allegiance — the last resort.</summary>
    string GetNearestSettlementId(string heroId);

    /// <summary>
    /// Writes the anchor onto the hero's map faction. False when the faction can't be written
    /// (unresolvable hero, unknown settlement id, or a faction type with no writable setter).
    /// </summary>
    bool SetFactionInitialHomeSettlement(string heroId, string settlementId);
}
