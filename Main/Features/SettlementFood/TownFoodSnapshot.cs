using System.Collections.Generic;
using TaleWorlds.CampaignSystem.Settlements;

namespace TAOM.Features.SettlementFood;

/// <summary>
/// Primitive snapshot of a <see cref="Town"/>'s food-relevant state, built at the GameModel boundary
/// so <see cref="SettlementFoodService"/> never touches sealed TaleWorlds types (ADR-007).
///
/// <para><see cref="WeightedGarrisonCount"/> reads the (Troop-Weight-patched) <c>NumberOfAllMembers</c>
/// getter; <see cref="RawGarrisonCount"/> reads the unpatched <c>MemberRoster.TotalManCount</c>. Vanilla
/// <c>PartyBase.NumberOfAllMembers => MemberRoster.TotalManCount</c>, so their difference is exactly the
/// weight inflation the food model must undo.</para>
/// </summary>
public sealed class TownFoodSnapshot
{
    public bool IsTown { get; }
    public bool IsUnderSiege { get; }
    public int RawGarrisonCount { get; }
    public int WeightedGarrisonCount { get; }

    /// <summary>Hearth levels (0/1/2) of every bound village currently in the Normal state.</summary>
    public IReadOnlyList<int> NormalVillageHearthLevels { get; }

    public TownFoodSnapshot(
        bool isTown,
        bool isUnderSiege,
        int rawGarrisonCount,
        int weightedGarrisonCount,
        IReadOnlyList<int> normalVillageHearthLevels)
    {
        IsTown = isTown;
        IsUnderSiege = isUnderSiege;
        RawGarrisonCount = rawGarrisonCount;
        WeightedGarrisonCount = weightedGarrisonCount;
        NormalVillageHearthLevels = normalVillageHearthLevels ?? new List<int>();
    }

    /// <summary>
    /// Boundary factory — converts a sealed <see cref="Town"/> into a primitive snapshot. Uses <c>?.</c>
    /// throughout because TaleWorlds computed getters can dereference null internally (adapters.md).
    /// </summary>
    public static TownFoodSnapshot FromTown(Town town)
    {
        var garrison = town?.GarrisonParty;
        int weighted = garrison?.Party.NumberOfAllMembers ?? 0;
        int raw = garrison?.MemberRoster?.TotalManCount ?? 0;

        var hearthLevels = new List<int>();
        var boundVillages = town?.Owner?.Settlement?.BoundVillages;
        if (boundVillages != null)
        {
            foreach (var village in boundVillages)
            {
                if (village != null && village.VillageState == Village.VillageStates.Normal)
                    hearthLevels.Add(village.GetHearthLevel());
            }
        }

        return new TownFoodSnapshot(
            isTown: town?.IsTown ?? false,
            isUnderSiege: town?.IsUnderSiege ?? false,
            rawGarrisonCount: raw,
            weightedGarrisonCount: weighted,
            normalVillageHearthLevels: hearthLevels);
    }
}
