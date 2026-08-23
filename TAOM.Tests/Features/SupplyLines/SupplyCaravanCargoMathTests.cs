using System.Collections.Generic;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using TAOM.Features.SupplyLines;

namespace TAOM.Tests.Features.SupplyLines;

/// <summary>
/// The pure cargo/provision maths inside <see cref="SupplyCaravanService"/>.
///
/// <para><b>SubtractNonCargo</b> is the delivery half of the non-cargo manifest (Codex round 2
/// #6): template guards and mercenary escorts can share a character id with purchased recruits,
/// and before the manifest existed a guard survivor was delivered as a recruit. The contract is
/// deterministic and conservative: casualties bill against the cargo first, so the player is
/// never handed a troop that might be a guard.</para>
///
/// <para><b>ComputeProvisionCount</b> exists because the caravan is not <c>IsCaravan</c> (custom
/// component), so vanilla food consumption runs against it and an escorted goods-less order
/// starved silently for the whole transit (review round B).</para>
/// </summary>
[TestClass]
public class SupplyCaravanCargoMathTests
{
    // --- SubtractNonCargo ---

    [TestMethod]
    public void SubtractNonCargo_GuardsShareCargoId_OnlyCargoRemains()
    {
        var live = new Dictionary<string, int> { ["recruit_a"] = 15 };
        var nonCargo = new Dictionary<string, int> { ["recruit_a"] = 10 };

        var result = SupplyCaravanService.SubtractNonCargo(live, nonCargo);

        Assert.AreEqual(5, result["recruit_a"], "10 of the 15 aboard are guards, never cargo");
    }

    [TestMethod]
    public void SubtractNonCargo_CasualtiesBillAgainstCargoFirst()
    {
        // 5 purchased + 10 guards set out; 5 died; 10 remain. The old code delivered all 5
        // purchased troops out of the survivors; the manifest contract delivers none, because
        // any survivor might be a guard.
        var live = new Dictionary<string, int> { ["recruit_a"] = 10 };
        var nonCargo = new Dictionary<string, int> { ["recruit_a"] = 10 };

        var result = SupplyCaravanService.SubtractNonCargo(live, nonCargo);

        Assert.IsFalse(result.ContainsKey("recruit_a"));
    }

    [TestMethod]
    public void SubtractNonCargo_DistinctIds_Untouched()
    {
        var live = new Dictionary<string, int> { ["recruit_a"] = 5, ["guard_b"] = 10 };
        var nonCargo = new Dictionary<string, int> { ["guard_b"] = 10 };

        var result = SupplyCaravanService.SubtractNonCargo(live, nonCargo);

        Assert.AreEqual(5, result["recruit_a"]);
        Assert.IsFalse(result.ContainsKey("guard_b"));
    }

    [TestMethod]
    public void SubtractNonCargo_NullManifest_LegacySaveKeepsLiveSnapshot()
    {
        // An order saved before the manifest field existed deserializes it as null; the legacy
        // guards-count-as-cargo behaviour is kept for those saves only.
        var live = new Dictionary<string, int> { ["recruit_a"] = 5 };

        Assert.AreSame(live, SupplyCaravanService.SubtractNonCargo(live, null));
        Assert.AreEqual(5, live["recruit_a"]);
    }

    [TestMethod]
    public void SubtractNonCargo_NullLive_ReturnsNull()
    {
        Assert.IsNull(SupplyCaravanService.SubtractNonCargo(null, new Dictionary<string, int> { ["x"] = 1 }));
    }

    // --- ComputeProvisionCount ---

    [TestMethod]
    public void ComputeProvisionCount_TypicalEscortedRun_CoversWorstCaseTransit()
    {
        // 20 men, 40 planned hours: worst case is 1.5x = 60h = 2.5 days; 20 men eat 1 food/day
        // (vanilla NumberOfMenOnMapToEatOneFood = 20), so ceil(2.5) + 1 spare = 4.
        Assert.AreEqual(4, SupplyCaravanService.ComputeProvisionCount(memberCount: 20, plannedHours: 40f));
    }

    [TestMethod]
    public void ComputeProvisionCount_NoMembers_NoFood()
    {
        Assert.AreEqual(0, SupplyCaravanService.ComputeProvisionCount(memberCount: 0, plannedHours: 40f));
        Assert.AreEqual(0, SupplyCaravanService.ComputeProvisionCount(memberCount: -3, plannedHours: 40f));
    }

    [TestMethod]
    public void ComputeProvisionCount_ShortTrip_StillCarriesTheSpare()
    {
        // Even a 2-hour minimum trip loads one spare food so the fractional first day is safe.
        Assert.AreEqual(2, SupplyCaravanService.ComputeProvisionCount(memberCount: 10, plannedHours: 2f));
    }

    [TestMethod]
    public void ComputeProvisionCount_NonFinitePlannedHours_TreatedAsZero()
    {
        Assert.AreEqual(1, SupplyCaravanService.ComputeProvisionCount(memberCount: 10, plannedHours: float.NaN));
        Assert.AreEqual(1, SupplyCaravanService.ComputeProvisionCount(memberCount: 10, plannedHours: float.PositiveInfinity));
        Assert.AreEqual(1, SupplyCaravanService.ComputeProvisionCount(memberCount: 10, plannedHours: -5f));
    }
}
