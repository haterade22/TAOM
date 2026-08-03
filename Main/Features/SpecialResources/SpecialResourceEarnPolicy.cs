using TaleWorlds.Core;

namespace TAOM.Features.SpecialResources;

/// <summary>
/// The two pure decisions behind special-resource earning, extracted so they can be tested without
/// a running campaign (<c>MapEvent</c> is sealed and unconstructible in a unit test). The behavior
/// keeps the plumbing; this keeps the policy — same split as <c>PatchShieldPolicy</c>.
/// </summary>
public static class SpecialResourceEarnPolicy
{
    /// <summary>
    /// Did the player's side win?
    ///
    /// This replaced a gate that asked whether the player IS the winning side's
    /// <c>LeaderParty.LeaderHero</c>. That framing conflated participating with commanding, and the
    /// consequence was not multiplayer-only: in ordinary single-player, a player who joins any
    /// lord's army stops being the leader party's hero, so every victory they fought in paid
    /// nothing. Multiplayer only made it total — under a client/server split no player leads the
    /// authoritative side either, so nobody ever earned (field report 2026-08-03 §6).
    /// </summary>
    /// <param name="playerSide">
    /// <c>MapEvent.PlayerSide</c> — the side the player's own party is on, whoever commands it.
    /// </param>
    /// <param name="winningSide">
    /// <c>MapEvent.WinningSide</c>, which is <see cref="BattleSideEnum.None"/> unless the battle
    /// actually resolved to a victory. Under co-op, clients routinely observe an unresolved state
    /// because the server is authoritative — that must never read as a win.
    /// </param>
    public static bool IsPlayerVictory(BattleSideEnum playerSide, BattleSideEnum winningSide)
    {
        if (winningSide == BattleSideEnum.None) return false;
        if (playerSide == BattleSideEnum.None) return false;
        return playerSide == winningSide;
    }

    /// <summary>
    /// May this process credit <c>Hero.MainHero</c> with earnings?
    ///
    /// No on a dedicated server, where MainHero is the idle world-gen hero the campaign was created
    /// around rather than anybody's character. Crediting it banks income nobody can spend while the
    /// remote players who fought the battles get nothing — log-proven as dozens of
    /// <c>[SpecRes] PRISONERS: +N</c> lines against the host hero.
    ///
    /// Not tied to co-op role on purpose: a client-hosted session's host also reports IsServer but
    /// is a real player and must keep earning normally.
    /// </summary>
    public static bool MayCreditMainHero(bool isDedicatedServer) => !isDedicatedServer;
}
