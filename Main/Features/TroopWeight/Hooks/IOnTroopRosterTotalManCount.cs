using TaleWorlds.CampaignSystem.Roster;

namespace TAOM.Features.TroopWeight.Hooks;

public interface IOnTroopRosterTotalManCount
{
    void OnTroopRosterTotalManCount(TroopRoster troopRoster, ref int __result);
}
