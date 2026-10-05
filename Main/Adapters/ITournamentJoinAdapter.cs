namespace TAOM.Adapters;

/// <summary>The tournament the player is about to join, as plain values.</summary>
public sealed class TournamentJoinSnapshot
{
    public TournamentJoinSnapshot(string townId, string? cultureId, string seedKey, string? prizeItemId, int? prizeTierIndex)
    {
        TownId = townId;
        CultureId = cultureId;
        SeedKey = seedKey;
        PrizeItemId = prizeItemId;
        PrizeTierIndex = prizeTierIndex;
    }

    /// <summary>The town settlement's StringId.</summary>
    public string TownId { get; }

    /// <summary>The town's culture StringId, which picks the prize pool.</summary>
    public string? CultureId { get; }

    /// <summary>Town plus creation time: the same for one tournament across menu reopenings and reloads.</summary>
    public string SeedKey { get; }

    /// <summary>The advertised prize's item id; null when the tournament has none.</summary>
    public string? PrizeItemId { get; }

    /// <summary>
    /// The advertised prize's engine tier, <c>(int)ItemObject.Tier</c> (Tier1 = 0 in v1.5.3), which classes a weapon,
    /// shield or harness the armour table does not list; null when the tournament has no prize.
    /// </summary>
    public int? PrizeTierIndex { get; }
}

/// <summary>
/// Boundary over the current settlement's <c>TournamentGame</c> (ADR-007): read it for the Join flow, and set its
/// prize, whose setter is private (<c>TournamentGame.Prize { get; private set; }</c>, v1.5.3).
/// </summary>
public interface ITournamentJoinAdapter
{
    /// <summary>The tournament in the player's current town, or null when there is none or it cannot be read.</summary>
    TournamentJoinSnapshot? GetCurrentTournament();

    /// <summary>Makes <paramref name="itemId"/> the prize of the tournament in that town; false when it cannot.</summary>
    bool SetPrize(string townId, string itemId);
}
