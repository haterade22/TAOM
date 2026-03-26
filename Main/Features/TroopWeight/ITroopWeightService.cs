using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Roster;

namespace TAOM.Features.TroopWeight;

public interface ITroopWeightService
{
    float GetTroopWeight(string troopStringId);
    float GetTroopWeight(CharacterObject character);
    float CalculateWeightedMemberCount(PartyBase party);
    float CalculateWeightedRosterCount(TroopRoster roster);
    float CalculateWeightedElementCount(TroopRosterElement element);
    void ClearCache();
}
