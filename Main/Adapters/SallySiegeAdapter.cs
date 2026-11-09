using System;
using System.Linq;
using TaleWorlds.CampaignSystem.Actions;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Roster;
using TaleWorlds.CampaignSystem.Settlements;
using TAOM.Features.MapEventGuard;

namespace TAOM.Adapters;

/// <summary>Reads one sally-out check's siege from the engine (#1099).</summary>
internal sealed class SallySiegeAdapter : ISallySiegeAdapter
{
    private readonly Settlement _settlement;
    private readonly MobileParty _main;

    public SallySiegeAdapter(Settlement settlement, MobileParty main)
    {
        _settlement = settlement;
        _main = main;
    }

    public SallySiegeSnapshot? Read()
    {
        var leader = _settlement?.SiegeEvent?.BesiegerCamp?.LeaderParty;
        if (leader == null || _main == null)
            return null;

        // Only a besieger leader inside the player's battle reaches the meeting: other paths never open one.
        var mapEvent = leader.MapEvent;
        if (mapEvent == null || mapEvent != _main.MapEvent)
            return new SallySiegeSnapshot(false, false, _settlement!.StringId, leader.StringId, Array.Empty<(int, bool)>());

        var roster = leader.Party.MemberRoster.GetTroopRoster()
            .Select(element => (element.Number, IsMarkedToDie(element)))
            .ToArray();
        return new SallySiegeSnapshot(
            true, mapEvent.IsSiegeOutside || mapEvent.IsBlockade, _settlement!.StringId, leader.StringId, roster);
    }

    // A hero marked to die still leads the party here: HandleMapEventEndForPartyInternal kills him later.
    private static bool IsMarkedToDie(TroopRosterElement element) =>
        element.Character.IsHero
        && element.Character.HeroObject.DeathMark == KillCharacterAction.KillCharacterActionDetail.DiedInBattle;
}
