using TAOM.Features.Execution;

namespace TAOM.Adapters;

/// <summary>
/// What War Pledge (#1113) needs from the campaign beyond the diplomacy adapters: who rules a realm, and
/// the notice a join shows (ADR-007).
/// </summary>
public interface IWarPledgeWorldAdapter
{
    /// <summary>
    /// True when a human rules the realm: this peer's clan, a co-op player's clan, or, in a co-op session
    /// whose player registry cannot be read, any clan. False for an unknown kingdom.
    /// </summary>
    bool IsPlayerRuled(string kingdomId);

    /// <summary>Tells the player that the realm joined the side: a banner on the map and a line in the message log.</summary>
    void AnnouncePledge(string kingdomId, FactionSide side);
}
