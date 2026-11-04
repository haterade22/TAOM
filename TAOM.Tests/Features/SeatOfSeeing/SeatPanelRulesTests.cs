using Microsoft.VisualStudio.TestTools.UnitTesting;
using TAOM.Features.SeatOfSeeing.Domain;

namespace TAOM.Tests.Features.SeatOfSeeing;

/// <summary>The pure rules of stage S2 (build-spec 6.2 to 6.4): the reopen, the pointer and the hidden interface.</summary>
[TestClass]
public class SeatPanelRulesTests
{
    private const float Eps = 0.001f;

    // ---- Keep the panel open ----

    private static readonly SeatCloseSignals None = default;

    [TestMethod]
    public void Reopen_WithNoPlayerKeyAndNoTransfer_IsTrue()
    {
        Assert.IsTrue(SeatPanelRule.ShouldReopen(None, None, transferOpen: false));
    }

    [DataTestMethod]
    [DataRow(true, false, false, false, DisplayName = "Escape")]
    [DataRow(false, true, false, false, DisplayName = "the right mouse button")]
    [DataRow(false, false, true, false, DisplayName = "the panel key")]
    [DataRow(false, false, false, true, DisplayName = "the hold key")]
    public void Reopen_AfterAPlayerCloseRouteOnThisFrame_IsFalse(bool escape, bool right, bool viewOrders, bool holdOrder)
    {
        var now = new SeatCloseSignals(escape, right, viewOrders, holdOrder);

        Assert.IsFalse(SeatPanelRule.ShouldReopen(now, None, transferOpen: false));
    }

    [DataTestMethod]
    [DataRow(true, false, false, false, DisplayName = "Escape")]
    [DataRow(false, true, false, false, DisplayName = "the right mouse button")]
    [DataRow(false, false, true, false, DisplayName = "the panel key")]
    [DataRow(false, false, false, true, DisplayName = "the hold key")]
    public void Reopen_AfterAPlayerCloseRouteOnThePreviousFrame_IsFalse(bool escape, bool right, bool viewOrders, bool holdOrder)
    {
        var previous = new SeatCloseSignals(escape, right, viewOrders, holdOrder);

        Assert.IsFalse(SeatPanelRule.ShouldReopen(None, previous, transferOpen: false), "an Escape close runs after the views tick");
    }

    [TestMethod]
    public void Reopen_WithTheTransferMenuOpen_IsFalse()
    {
        Assert.IsFalse(SeatPanelRule.ShouldReopen(None, None, transferOpen: true),
            "OpenToggleOrder clears IsTransferActive, so a reopen cancels the transfer (MissionOrderVM.cs:723)");
    }

    [TestMethod]
    public void ClosingEdge_IsTheFrameTheOpenPanelIsFoundClosed()
    {
        Assert.IsTrue(SeatPanelRule.ClosingEdge(wasOpen: true, isOpen: false));
        Assert.IsFalse(SeatPanelRule.ClosingEdge(wasOpen: false, isOpen: false));
        Assert.IsFalse(SeatPanelRule.ClosingEdge(wasOpen: true, isOpen: true));
        Assert.IsFalse(SeatPanelRule.ClosingEdge(wasOpen: false, isOpen: true));
    }

    // ---- The pointer ----

    [TestMethod]
    public void Pointer_WithTheFullUsableArea_IsTheRangedPositionTimesTheResolution()
    {
        Assert.IsTrue(SeatPointer.TryInUsableArea(0.25f, 0.5f, 1920f, 1080f, 1f, 1f, out var x, out var y));

        Assert.AreEqual(480f, x, Eps);
        Assert.AreEqual(540f, y, Eps);
    }

    [TestMethod]
    public void Pointer_WithANinetyPercentUsableArea_SubtractsHalfTheMargin()
    {
        // WorldToScreenInsideUsableArea subtracts (W - U.X * W) / 2, which is 96 px of 1920 and 54 px of 1080 at 0.9.
        Assert.IsTrue(SeatPointer.TryInUsableArea(0.25f, 0.5f, 1920f, 1080f, 0.9f, 0.9f, out var x, out var y));

        Assert.AreEqual(384f, x, Eps);
        Assert.AreEqual(486f, y, Eps);
    }

    [TestMethod]
    public void Pointer_TheScreenCentreOfTheUsableAreaIsTheCentreOfTheHandlersOwnPoint()
    {
        // The handler's own centre is W / 2, which is off by the margin, and the pointer function does not copy that.
        Assert.IsTrue(SeatPointer.TryInUsableArea(0.5f, 0.5f, 1000f, 1000f, 0.9f, 0.9f, out var x, out var y));

        Assert.AreEqual(450f, x, Eps);
        Assert.AreEqual(450f, y, Eps);
    }

    [DataTestMethod]
    [DataRow(float.NaN, 0.5f, 1920f, 1080f, 1f, 1f)]
    [DataRow(0.5f, float.PositiveInfinity, 1920f, 1080f, 1f, 1f)]
    [DataRow(0.5f, 0.5f, float.NaN, 1080f, 1f, 1f)]
    [DataRow(0.5f, 0.5f, 1920f, float.NegativeInfinity, 1f, 1f)]
    [DataRow(0.5f, 0.5f, 1920f, 1080f, float.NaN, 1f)]
    [DataRow(0.5f, 0.5f, 1920f, 1080f, 1f, float.NaN)]
    [DataRow(0.5f, 0.5f, 0f, 1080f, 1f, 1f)]
    [DataRow(0.5f, 0.5f, 1920f, -1f, 1f, 1f)]
    [DataRow(0.5f, 0.5f, 1920f, 1080f, 0f, 1f)]
    [DataRow(0.5f, 0.5f, 1920f, 1080f, 1f, -0.5f)]
    public void Pointer_WithAnInputThatIsNotUsable_IsRefused(float rx, float ry, float w, float h, float ux, float uy)
    {
        Assert.IsFalse(SeatPointer.TryInUsableArea(rx, ry, w, h, ux, uy, out var x, out var y));
        Assert.AreEqual(0f, x);
        Assert.AreEqual(0f, y);
    }

    // ---- The hidden interface ----

    [TestMethod]
    public void HideRestore_NothingHappening_IsNull()
    {
        Assert.IsNull(SeatHideRule.RestoreReason(false, false, false));
    }

    [TestMethod]
    public void HideRestore_NamesEachReasonOnItsOwn()
    {
        Assert.AreEqual("panel", SeatHideRule.RestoreReason(true, false, false));
        Assert.AreEqual("dialog", SeatHideRule.RestoreReason(false, true, false));
        Assert.AreEqual("layer", SeatHideRule.RestoreReason(false, false, true));
    }

    [TestMethod]
    public void HideRestore_WithSeveralReasons_TheFirstOfPanelDialogLayerWins()
    {
        Assert.AreEqual("panel", SeatHideRule.RestoreReason(true, true, true));
        Assert.AreEqual("dialog", SeatHideRule.RestoreReason(false, true, true));
    }
}
