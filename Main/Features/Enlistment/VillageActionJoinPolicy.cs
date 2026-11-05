namespace TAOM.Features.Enlistment;

/// <summary>
/// Is the commander's map event one the soldier should join? Pure, next to
/// <see cref="CommanderBattleMatchPolicy"/> (#831).
///
/// A raid, forced supplies and forced volunteers are village hostile actions. Joined like a battle,
/// they held the soldier in <c>EnlistedBattle</c> for many in-game hours, refused release
/// (<c>RefusedInBattle</c>) for that time, and ended in vanilla's <c>raiding_village</c> menu with
/// its Abandon Army and Leave Army exits.
///
/// ONLY THE AGGRESSOR SKIPS: the adapter's <c>VillageRaidReader</c> reports true only for a
/// commander on the attacker side, so a commander fighting off a raid on his own village still
/// joins. Militia fights inside a raid are NOT joined: the militia sits on the defender side of the
/// raid event. When it resists, that event ends as the militia falls, and the raider opens a second
/// raid event that does the looting; both are raids the commander leads, so neither is joined.
/// Sieges, sally-outs and field battles are never village actions and always join.
/// </summary>
public static class VillageActionJoinPolicy
{
    /// <summary>
    /// "The commander is in a battle the soldier joins": in a map event and not raiding. The
    /// stale-latch, join-request and assessment sites share this so they cannot drift apart.
    /// </summary>
    public static bool IsJoinableBattle(bool inMapEvent, bool commanderIsRaiding) =>
        inMapEvent && !commanderIsRaiding;
}
