using TaleWorlds.CampaignSystem.Roster;

namespace TAOM.Features.TroopWeight.Hooks;

public interface IOnTroopRosterTotalHealthyCount
{
    void OnTroopRosterTotalHealthyCount(TroopRoster troopRoster, ref int __result);
}
