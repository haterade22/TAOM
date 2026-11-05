using Microsoft.VisualStudio.TestTools.UnitTesting;
using TAOM.Features.SeatOfSeeing.Domain;

namespace TAOM.Tests.Features.SeatOfSeeing;

/// <summary>The lift of the third-person view with the order panel open (build-spec section 11): the raw progress, its smoothstep, the height and the exact zero.</summary>
[TestClass]
public class SeatPanelLiftTests
{
    private const float Eps = 0.0001f;

    [TestMethod]
    public void PanelProgress_Open_RisesToOneInHalfASecond()
    {
        Assert.AreEqual(0.5f, SeatPanelLift.PanelProgress(0f, panelOpen: true, dt: 0.25f), Eps);
        Assert.AreEqual(1f, SeatPanelLift.PanelProgress(0.5f, panelOpen: true, dt: 0.25f), Eps);
    }

    [TestMethod]
    public void PanelProgress_Closed_SinksToZeroInFourTenthsOfASecond()
    {
        Assert.AreEqual(0.5f, SeatPanelLift.PanelProgress(1f, panelOpen: false, dt: 0.2f), Eps);
        Assert.AreEqual(0f, SeatPanelLift.PanelProgress(0.5f, panelOpen: false, dt: 0.2f), Eps);
    }

    [TestMethod]
    public void PanelProgress_NeverLeavesZeroToOne()
    {
        Assert.AreEqual(1f, SeatPanelLift.PanelProgress(0.9f, panelOpen: true, dt: 5f));
        Assert.AreEqual(0f, SeatPanelLift.PanelProgress(0.1f, panelOpen: false, dt: 5f));
    }

    [TestMethod]
    public void PanelProgress_AQuickCloseAndOpen_StartsFromTheCurrentValue()
    {
        var opened = SeatPanelLift.PanelProgress(0f, panelOpen: true, dt: 0.3f);
        var closed = SeatPanelLift.PanelProgress(opened, panelOpen: false, dt: 0.1f);
        var reopened = SeatPanelLift.PanelProgress(closed, panelOpen: true, dt: 0.1f);

        Assert.AreEqual(0.6f, opened, Eps);
        Assert.AreEqual(0.35f, closed, Eps);
        Assert.AreEqual(0.55f, reopened, Eps);
    }

    [DataTestMethod]
    [DataRow(0f)]
    [DataRow(-0.1f)]
    [DataRow(float.NaN)]
    [DataRow(float.PositiveInfinity)]
    public void PanelProgress_ABadFrameTime_LeavesTheValueAlone(float dt)
    {
        Assert.AreEqual(0.4f, SeatPanelLift.PanelProgress(0.4f, panelOpen: true, dt: dt));
        Assert.AreEqual(0.4f, SeatPanelLift.PanelProgress(0.4f, panelOpen: false, dt: dt));
    }

    [DataTestMethod]
    [DataRow(float.NaN)]
    [DataRow(float.PositiveInfinity)]
    [DataRow(-3f)]
    public void PanelProgress_AValueThatIsNotInRange_IsReadAsTheNearestEnd(float progress)
    {
        var next = SeatPanelLift.PanelProgress(progress, panelOpen: true, dt: 0f);

        Assert.IsTrue(next >= 0f && next <= 1f, "got " + next);
    }

    [TestMethod]
    public void PanelLiftAmount_IsASmoothstepWithExactEnds()
    {
        Assert.AreEqual(0f, SeatPanelLift.PanelLiftAmount(0f));
        Assert.AreEqual(1f, SeatPanelLift.PanelLiftAmount(1f));
        Assert.AreEqual(0.5f, SeatPanelLift.PanelLiftAmount(0.5f), Eps);
        Assert.AreEqual(0.15625f, SeatPanelLift.PanelLiftAmount(0.25f), Eps);
        Assert.AreEqual(0.84375f, SeatPanelLift.PanelLiftAmount(0.75f), Eps);
    }

    [DataTestMethod]
    [DataRow(-1f, 0f)]
    [DataRow(2f, 1f)]
    [DataRow(float.NaN, 0f)]
    public void PanelLiftAmount_OutsideZeroToOne_IsTheNearestEnd(float progress, float expected)
    {
        Assert.AreEqual(expected, SeatPanelLift.PanelLiftAmount(progress));
    }

    [TestMethod]
    public void Lift_AtZeroProgress_IsExactlyZero_ForAnyHeight()
    {
        foreach (var height in new[] { 0f, 3f, 7f, 7.0001f, 123.456f })
            Assert.AreEqual(0f, SeatPanelLift.Lift(height, 0f), "height " + height);
    }

    [TestMethod]
    public void Lift_AtFullProgress_IsTheWholeHeight()
    {
        Assert.AreEqual(7f, SeatPanelLift.Lift(7f, 1f));
        Assert.AreEqual(3f, SeatPanelLift.Lift(3f, 1f));
    }

    [TestMethod]
    public void Lift_AHeightThatIsNotANumber_IsZero()
    {
        Assert.AreEqual(0f, SeatPanelLift.Lift(float.NaN, 1f));
        Assert.AreEqual(0f, SeatPanelLift.Lift(float.PositiveInfinity, 1f));
    }

    [TestMethod]
    public void HeightFor_AFieldBattleLiftsSevenMetres_ASiegeOrASallyOutThree_AnythingElseNothing()
    {
        Assert.AreEqual(7f, SeatPanelLift.HeightFor(field: true, siege: false));
        Assert.AreEqual(3f, SeatPanelLift.HeightFor(field: false, siege: true));
        Assert.AreEqual(0f, SeatPanelLift.HeightFor(field: false, siege: false));
    }
}
