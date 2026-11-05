using System;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.Core;

namespace TAOM.Adapters;

/// <summary>
/// The one place the enlistment adapters ask "is this party raiding a village?" (#831), so the
/// snapshot adapter and the encounter adapter cannot disagree.
///
/// True only when the party is in a map event, sits on the attacker side, and the event is a raid,
/// forced supplies or forced volunteers. The set matches <c>StuckBattleEventAdapter</c>; forced
/// supplies and forced volunteers are player-only events in vanilla, so they cannot occur for an AI
/// commander; they stay in the set to match vanilla's grouping. The side check reads the party's own side,
/// which also holds for an army follower (setting a leader's side copies it to attached parties).
///
/// Fail-open by contract: any throw returns false, and false means the soldier joins as before.
/// No LINQ, no allocation and no Find, so it is safe on the pump.
/// </summary>
internal static class VillageRaidReader
{
    public static bool IsRaiding(MobileParty party)
    {
        try
        {
            var mapEvent = party?.MapEvent;
            if (mapEvent == null || party.Party.Side != BattleSideEnum.Attacker)
                return false;
            return mapEvent.IsRaid || mapEvent.IsForcingSupplies || mapEvent.IsForcingVolunteers;
        }
        catch (Exception)
        {
            return false;
        }
    }
}
