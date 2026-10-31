namespace TAOM.Features.FordsOfWar.Domain;

/// <summary>
/// One fielded hero. HeroId is an NPCCharacter id. Price is what the owner paid, the base of the buyback price.
/// RespawnAt is a match-clock time and RespawnTotal the full delay it was set with; both only mean something while the
/// hero is Dead. BuybackPending marks a dead hero whose buyback was paid: it holds its alive slot until the next respawn pass.
/// </summary>
internal sealed record HeroEntry(MatchSide Side, string HeroId, int OwnerSlot, int Lane, int Price, HeroState State, float RespawnAt,
    bool BuybackPending = false, float RespawnTotal = 0f)
{
    /// <summary>True while the hero counts against the side's alive limit: alive, or dead with a paid buyback.</summary>
    public bool HoldsSlot => State == HeroState.Alive || BuybackPending;

    /// <summary>A dead hero whose timer is still running and whose buyback is not yet paid.</summary>
    public bool CanBuyback(float now) => State == HeroState.Dead && !BuybackPending && RespawnAt > now;

    public SpawnRequest ToSpawnRequest(int requestId) =>
        new(requestId, Side, Lane, HeroId, 1, OwnerSlot, UnitRole.Hero, true);
}
