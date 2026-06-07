using System.Collections.Generic;
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

    /// <summary>
    /// Pure core for the weighted battle-ready / wounded split. Sums (Number-WoundedNumber)*weight
    /// into Healthy and WoundedNumber*weight into Wounded, then ceilings each. This is the
    /// authoritative fix for the phantom-wounded display bug: weighted-healthy + weighted-wounded
    /// equals the weighted member total, so a consumer that does (AllMembers - HealthyMembers) no
    /// longer manufactures wounds out of the weight surplus. Engine-free; unit-tested.
    /// </summary>
    (int Healthy, int Wounded) ComputeWeightedHealthyAndWounded(
        IEnumerable<(string TroopId, int Number, int WoundedNumber)> elements);

    /// <summary>
    /// Reads <paramref name="party"/>'s MemberRoster and returns the weighted (Healthy, Wounded)
    /// split via <see cref="ComputeWeightedHealthyAndWounded"/>. Returns (0,0) on any error
    /// (consistent with the other roster-iterating service methods). Not unit-tested (sealed
    /// PartyBase / TroopRoster) — the math it delegates to is.
    /// </summary>
    (int Healthy, int Wounded) GetWeightedHealthAndWounded(PartyBase party);

    void ClearCache();
}
