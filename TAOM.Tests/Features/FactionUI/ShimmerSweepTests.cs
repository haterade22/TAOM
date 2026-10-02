using Microsoft.VisualStudio.TestTools.UnitTesting;
using TAOM.Features.FactionUI.UI;

namespace TAOM.Tests.Features.FactionUI;

/// <summary>
/// Issue #704. The light sweep across the main-menu title, ported from Kysaro's MainMenuShimmer: the
/// position math is pure so the timing can be pinned without a widget tree.
/// </summary>
[TestClass]
public class ShimmerSweepTests
{
    private static readonly ShimmerSweep Title = new(boxWidth: 310f, firstDelay: 1.2f, cycle: 7f, sweepTime: 1.7f, margin: 60f);

    [TestMethod]
    public void PositionAt_BeforeTheFirstDelay_IsParkedOffscreen()
    {
        Assert.AreEqual(ShimmerSweep.Parked, Title.PositionAt(0.5f));
    }

    [TestMethod]
    public void PositionAt_TheStartOfASweep_IsTheLeftMargin()
    {
        Assert.AreEqual(-60f, Title.PositionAt(1.2f), 0.001f);
    }

    [TestMethod]
    public void PositionAt_HalfwayThroughASweep_IsTheMiddleOfTheBox()
    {
        Assert.AreEqual(155f, Title.PositionAt(1.2f + 0.85f), 0.001f);
    }

    [TestMethod]
    public void PositionAt_AfterTheSweepButBeforeTheNextCycle_IsParked()
    {
        Assert.AreEqual(ShimmerSweep.Parked, Title.PositionAt(1.2f + 3f));
    }

    [TestMethod]
    public void PositionAt_TheNextCycle_SweepsAgain()
    {
        Assert.AreEqual(155f, Title.PositionAt(1.2f + 7f + 0.85f), 0.01f);
    }

    [DataTestMethod]
    [DataRow(float.NaN)]
    [DataRow(float.PositiveInfinity)]
    public void PositionAt_ATimeThatIsNotFinite_IsParked(float time)
    {
        // The time accumulates the engine's frame time; a NaN must park the strip, never place it at NaN.
        Assert.AreEqual(ShimmerSweep.Parked, Title.PositionAt(time));
    }
}
