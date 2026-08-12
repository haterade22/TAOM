using System.Collections.Generic;

namespace TAOM.Features.Enlistment;

/// <summary>
/// Is the commander's battle THIS battle? Pure over party ids so it is testable without a
/// <c>MapEvent</c> — the same shape as <see cref="BattleCommandPolicy"/> and
/// <c>BattleFormationPolicy</c>.
///
/// WHY THE ARMY LEADER COUNTS (field report, 2026-08-09: "my lord's army respawned, I was with the
/// party but was not pulled into the fight"). A <c>MapEvent</c> lists the parties that ENTERED it.
/// When a lord is a follower in someone else's army, the army LEADER is the party that enters, and
/// the follower fights under it — so matching only the commander's own party misses every battle he
/// fights while attached. The feature doc named this as the outstanding risk before it was ever
/// seen: *"an attached army member may not appear there."*
///
/// The fallout was not just the missed join. The hourly recovery is a backstop that fires up to an
/// in-game hour later, and a live session measured **6 of 10 joins arriving through it** rather than
/// through the <c>MapEventStarted</c> edge (#408) — which is this defect, counted.
///
/// The commander's OWN id is still what the caller receives on a match. Downstream seeds the
/// encounter from it, and the encounter has to be with the commander, not with whoever happens to
/// lead his army.
/// </summary>
public static class CommanderBattleMatchPolicy
{
    /// <summary>
    /// True when <paramref name="involvedPartyIds"/> contains the commander's own party or, when he
    /// is a follower, the party leading the army he belongs to.
    ///
    /// <paramref name="armyLeaderPartyId"/> is null when the commander leads no army or belongs to
    /// none, and equals <paramref name="commanderPartyId"/> when he leads one himself — both cases
    /// collapse harmlessly into the own-party check.
    /// </summary>
    public static bool IsCommanderInvolved(
        string commanderPartyId,
        string armyLeaderPartyId,
        IEnumerable<string> involvedPartyIds)
    {
        if (string.IsNullOrEmpty(commanderPartyId) || involvedPartyIds == null)
            return false;

        foreach (var id in involvedPartyIds)
        {
            if (string.IsNullOrEmpty(id))
                continue;
            if (id == commanderPartyId)
                return true;
            if (!string.IsNullOrEmpty(armyLeaderPartyId) && id == armyLeaderPartyId)
                return true;
        }

        return false;
    }
}
