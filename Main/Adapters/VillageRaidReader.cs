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
/// commander; they stay in the set to match vanilla's grouping.
///
/// True means "raiding and uncontested by a lord" (#954). A lord who relieves the village attacks the
/// raider and joins the running Raid event on its defender side (v1.4.8 StartBattleAction.cs:113-115,
/// :23-30), so the commander stays the attacker in a Raid while a real lord battle is fought. The
/// defender side is therefore scanned for
/// vanilla's own lord-defender test (v1.4.8 EncounterManager.cs:157: mobile, IsLordParty, healthy troops);
/// a match makes this false, so the soldier joins. Militia, patrols and other non-lord defenders do
/// not count, so a raid they defend stays skipped. The side check reads the party's own side,
/// which also holds for an army follower (setting a leader's side copies it to attached parties).
///
/// Fail-open by contract: any throw returns false, and false means the soldier joins as before.
/// No LINQ, no allocation and no lookup by id, so it is safe on the pump.
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
            if (!(mapEvent.IsRaid || mapEvent.IsForcingSupplies || mapEvent.IsForcingVolunteers))
                return false;

            var defenders = mapEvent.DefenderSide.Parties;
            for (int i = 0; i < defenders.Count; i++)
            {
                var defender = defenders[i]?.Party;
                if (defender != null && defender.IsMobile
                    && defender.MobileParty.IsLordParty && defender.MemberRoster.TotalHealthyCount > 0)
                    return false;
            }
            return true;
        }
        catch (Exception)
        {
            return false;
        }
    }
}
