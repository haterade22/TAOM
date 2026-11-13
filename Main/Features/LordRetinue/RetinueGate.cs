using TAOM.Features.CoopInterop;

namespace TAOM.Features.LordRetinue;

/// <summary>
/// Pure gate for LordRetinue: may this peer change this party? Under co-op the engine's MainParty and
/// PlayerClan name only the local peer, so a remote player's party looks like AI: the registry answers for
/// the party and for its clan, and an unreadable registry (<see cref="PlayerControl.Unknown"/>) counts as a
/// player. Only a peer that may run authoritative campaign logic acts.
/// </summary>
public static class RetinueGate
{
    public static bool MayChange(
        bool isLordParty, bool isMainParty, bool isAuthority, bool shouldDeferToHost,
        PlayerControl partyControl, PlayerControl clanControl)
        => isLordParty && !isMainParty && isAuthority && !shouldDeferToHost
           && partyControl == PlayerControl.NotPlayer && clanControl == PlayerControl.NotPlayer;
}
