using Microsoft.VisualStudio.TestTools.UnitTesting;
using TAOM.Features.TroopWeight;

namespace TAOM.Tests.Features.TroopWeight;

/// <summary>
/// #1430 (from Silas He's v1.5.6 fix package): Transfer All's member budget in the weighted frame. Vanilla moves whole
/// rows in order and clamps the first row that does not fit, then stops; the budget is the headcount that reproduces
/// that plan when each row costs its troop weight. 0 means no room at all, which vanilla reads as "move everything",
/// as it does for a full party. With some room left, the budget is never 0.
/// </summary>
[TestClass]
public class TroopTransferPlanningTests
{
    private static TroopTransferRow Row(int count, float weight, bool eligible = true) => new(count, weight, eligible);

    [TestMethod]
    public void WholeRowsThatFit_AreAllMoved()
        => Assert.AreEqual(5, TroopTransferPlanning.WeightedFillBudget(new[] { Row(3, 1f), Row(2, 2f) }, 7.0));

    [TestMethod]
    public void TheFirstRowThatDoesNotFit_IsClampedAndTheFillStops()
        // 3 x 2 = 6 of 7 used; then 1 slot left fits one weight-1 troop of the next row, and the third row is not reached.
        => Assert.AreEqual(4, TroopTransferPlanning.WeightedFillBudget(new[] { Row(3, 2f), Row(5, 1f), Row(9, 1f) }, 7.0));

    [TestMethod]
    public void AHeavyRow_CostsItsWeight()
        // Ten weight-3 troops into 10 free slots: three fit.
        => Assert.AreEqual(3, TroopTransferPlanning.WeightedFillBudget(new[] { Row(10, 3f) }, 10.0));

    [TestMethod]
    public void IneligibleRows_AreSkippedLikeVanillaSkipsThem()
        => Assert.AreEqual(2, TroopTransferPlanning.WeightedFillBudget(new[] { Row(4, 1f, eligible: false), Row(2, 1f) }, 3.0));

    [TestMethod]
    public void NoRoom_ReturnsZero_VanillasMoveEverythingPress()
    {
        Assert.AreEqual(0, TroopTransferPlanning.WeightedFillBudget(new[] { Row(4, 1f) }, 0.0));
        Assert.AreEqual(0, TroopTransferPlanning.WeightedFillBudget(new[] { Row(4, 1f) }, -3.0));
        Assert.AreEqual(0, TroopTransferPlanning.WeightedFillBudget(new[] { Row(4, 1f) }, double.NaN));
    }

    // Some room, but the first eligible troop is heavier than it. 0 would make vanilla move the whole roster; vanilla's
    // own headcount budget at that room clamps the row to the room left, so one troop moves and the next press sees a
    // full party.
    [TestMethod]
    public void SomeRoom_ButTheFirstEligibleTroopDoesNotFit_MovesOne()
    {
        Assert.AreEqual(1, TroopTransferPlanning.WeightedFillBudget(new[] { Row(4, 2f) }, 1.5));
        Assert.AreEqual(1, TroopTransferPlanning.WeightedFillBudget(new[] { Row(9, 1f, eligible: false), Row(2, 20f) }, 3.0));
    }

    [TestMethod]
    public void SomeRoom_ButNoEligibleTroop_ReturnsZero()
        => Assert.AreEqual(0, TroopTransferPlanning.WeightedFillBudget(new[] { Row(4, 1f, eligible: false), Row(0, 1f) }, 5.0));

    [TestMethod]
    public void FloatNoise_DoesNotLoseATroop()
        // 3.0 / 0.1f is 29.9999996 in float arithmetic; thirty troops fit.
        => Assert.AreEqual(30, TroopTransferPlanning.WeightedFillBudget(new[] { Row(40, 0.1f) }, 3.0));

    [TestMethod]
    public void ABadWeight_CountsAsOne()
    {
        Assert.AreEqual(2, TroopTransferPlanning.WeightedFillBudget(new[] { Row(5, 0f) }, 2.0));
        Assert.AreEqual(2, TroopTransferPlanning.WeightedFillBudget(new[] { Row(5, -1f) }, 2.0));
        Assert.AreEqual(2, TroopTransferPlanning.WeightedFillBudget(new[] { Row(5, float.PositiveInfinity) }, 2.0));
        Assert.AreEqual(2, TroopTransferPlanning.WeightedFillBudget(new[] { Row(5, float.NaN) }, 2.0));
    }

    [TestMethod]
    public void EmptyOrMissingRows_ReturnZero()
    {
        Assert.AreEqual(0, TroopTransferPlanning.WeightedFillBudget(new TroopTransferRow[0], 10.0));
        Assert.AreEqual(0, TroopTransferPlanning.WeightedFillBudget(null, 10.0));
    }
}
