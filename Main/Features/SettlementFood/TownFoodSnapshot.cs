using System.Collections.Generic;
using TaleWorlds.CampaignSystem.Settlements;

namespace TAOM.Features.SettlementFood;

/// <summary>
/// Primitive snapshot of a <see cref="Town"/>'s food-relevant state, built at the GameModel boundary
/// so <see cref="SettlementFoodService"/> never touches sealed TaleWorlds types (ADR-007).
///
/// <para><see cref="WeightedGarrisonCount"/> reads <c>NumberOfAllMembers</c>; <see cref="RawGarrisonCount"/>
/// reads <c>MemberRoster.TotalManCount</c>. Historically TroopWeight patched the former to a weighted count
/// and this snapshot's difference undid the resulting garrison food inflation. Since the 2026-07-11
/// count→limit rework the getter is RAW again (<c>NumberOfAllMembers => MemberRoster.TotalManCount</c>), so
/// the two are equal and the correction is an inert no-op — vanilla food math is now correct at source.
/// Retained (harmless) rather than removed to keep the boundary DTO stable.</para>
/// </summary>
public sealed class TownFoodSnapshot
{
    public bool IsTown { get; }
    public bool IsUnderSiege { get; }
    public int RawGarrisonCount { get; }
    public int WeightedGarrisonCount { get; }

    /// <summary>Hearth levels (0/1/2) of every bound village currently in the Normal state.</summary>
    public IReadOnlyList<int> NormalVillageHearthLevels { get; }

    /// <summary>
    /// The fief's current prosperity, the input to the hinterland production term. Vanilla reads
    /// prosperity only as a CONSUMER (<c>Prosperity / 40</c>); TAOM also feeds it back into
    /// production so the food balance holds as a settlement grows.
    /// </summary>
    public float Prosperity { get; }

    public TownFoodSnapshot(
        bool isTown,
        bool isUnderSiege,
        int rawGarrisonCount,
        int weightedGarrisonCount,
        IReadOnlyList<int> normalVillageHearthLevels,
        float prosperity = 0f)
    {
        IsTown = isTown;
        IsUnderSiege = isUnderSiege;
        RawGarrisonCount = rawGarrisonCount;
        WeightedGarrisonCount = weightedGarrisonCount;
        NormalVillageHearthLevels = normalVillageHearthLevels ?? new List<int>();
        Prosperity = prosperity;
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
            normalVillageHearthLevels: hearthLevels,
            prosperity: town?.Prosperity ?? 0f);
    }
}
