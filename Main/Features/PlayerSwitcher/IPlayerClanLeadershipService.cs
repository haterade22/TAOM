namespace TAOM.Features.PlayerSwitcher;

/// <summary>
/// Restores the one invariant the engine assumes about the player and never re-establishes on
/// its own: <c>Clan.PlayerClan.Leader == Hero.MainHero</c>.
/// </summary>
/// <remarks>
/// Campaigns started before the takeover path reassigned clan leadership (#550) carry the AI king
/// as the player clan's leader. Vanilla keys the player's part in every kingdom election off that
/// leader, so such a player can never vote: each decision resolves inside the popup's own
/// constructor and the popup renders already decided and unclosable. The repair promotes the
/// player through vanilla succession once, at session launch, and only in exactly the state a
/// takeover would have left behind.
/// </remarks>
public interface IPlayerClanLeadershipService
{
    /// <summary>True when the player was promoted this call. False for every other state.</summary>
    bool RepairIfNeeded();
}
