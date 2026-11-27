using Microsoft.VisualStudio.TestTools.UnitTesting;
using TAOM.Features.SeatOfSeeing.Domain;

namespace TAOM.Tests.Features.SeatOfSeeing;

/// <summary>The relay's decision from a press and a release on the panel, and the shield's yield rule (Marshal's Word stage M7).</summary>
[TestClass]
public class SeatPanelRelayRuleTests
{
    private static readonly object One = new();
    private static readonly object Two = new();

    private static SeatPanelHit Over(SeatPanelKind kind, object? target) => new(kind, target);

    private static readonly SeatPanelHit Nothing = default;

    [DataTestMethod]
    [DataRow((int)SeatPanelKind.Card, (int)SeatPanelAction.SelectCard)]
    [DataRow((int)SeatPanelKind.OrderSet, (int)SeatPanelAction.SelectOrderSet)]
    [DataRow((int)SeatPanelKind.OrderItem, (int)SeatPanelAction.ExecuteOrderItem)]
    public void PressAndReleaseOnTheSameTarget_ActsByKind(int kindValue, int expectedValue)
    {
        var kind = (SeatPanelKind)kindValue;
        var expected = (SeatPanelAction)expectedValue;
        var rule = new SeatPanelRelayRule();

        Assert.AreEqual(SeatPanelAction.None, rule.Step(leftPressed: true, leftReleased: false, rightReleased: false, Over(kind, One), returnsToSet: false));
        Assert.AreEqual(expected, rule.Step(leftPressed: false, leftReleased: true, rightReleased: false, Over(kind, One), returnsToSet: false));
    }

    [TestMethod]
    public void AReturnItem_DeselectsTheSet_InsteadOfExecuting()
    {
        var rule = new SeatPanelRelayRule();
        rule.Step(true, false, false, Over(SeatPanelKind.OrderItem, One), returnsToSet: true);

        Assert.AreEqual(SeatPanelAction.DeselectOrderSet, rule.Step(false, true, false, Over(SeatPanelKind.OrderItem, One), returnsToSet: true));
    }

    [TestMethod]
    public void PressAndReleaseInOneFrame_Acts()
    {
        var rule = new SeatPanelRelayRule();

        Assert.AreEqual(SeatPanelAction.SelectCard, rule.Step(true, true, false, Over(SeatPanelKind.Card, One), false));
    }

    [TestMethod]
    public void ReleaseOnAnotherTarget_ActsOnNothing()
    {
        var rule = new SeatPanelRelayRule();
        rule.Step(true, false, false, Over(SeatPanelKind.Card, One), false);

        Assert.AreEqual(SeatPanelAction.None, rule.Step(false, true, false, Over(SeatPanelKind.Card, Two), false));
    }

    [TestMethod]
    public void ReleaseOffThePanel_ActsOnNothing()
    {
        var rule = new SeatPanelRelayRule();
        rule.Step(true, false, false, Over(SeatPanelKind.OrderItem, One), false);

        Assert.AreEqual(SeatPanelAction.None, rule.Step(false, true, false, Nothing, false));
    }

    [TestMethod]
    public void ReleaseWithNoPressSeen_ActsOnNothing()
    {
        var rule = new SeatPanelRelayRule();

        Assert.AreEqual(SeatPanelAction.None, rule.Step(false, true, false, Over(SeatPanelKind.Card, One), false));
    }

    [TestMethod]
    public void AReleaseSpendsThePress_SoASecondReleaseActsOnNothing()
    {
        var rule = new SeatPanelRelayRule();
        rule.Step(true, true, false, Over(SeatPanelKind.Card, One), false);

        Assert.AreEqual(SeatPanelAction.None, rule.Step(false, true, false, Over(SeatPanelKind.Card, One), false));
    }

    [TestMethod]
    public void APressOffThePanel_ForgetsAnEarlierPress()
    {
        var rule = new SeatPanelRelayRule();
        rule.Step(true, false, false, Over(SeatPanelKind.Card, One), false);
        rule.Step(true, false, false, Nothing, false);

        Assert.AreEqual(SeatPanelAction.None, rule.Step(false, true, false, Over(SeatPanelKind.Card, One), false));
    }

    [TestMethod]
    public void ATargetWithNoViewModel_NeverActs()
    {
        var rule = new SeatPanelRelayRule();
        rule.Step(true, false, false, Over(SeatPanelKind.Card, null), false);

        Assert.AreEqual(SeatPanelAction.None, rule.Step(false, true, false, Over(SeatPanelKind.Card, null), false));
    }

    [TestMethod]
    public void ARightReleaseOverTheShield_IsEscape()
    {
        var rule = new SeatPanelRelayRule();

        rule.Step(false, false, false, Over(SeatPanelKind.OrderSet, One), false, rightPressed: true);

        Assert.AreEqual(SeatPanelAction.Escape, rule.Step(false, false, true, Over(SeatPanelKind.OrderSet, One), false));
    }

    [TestMethod]
    public void ARightReleaseOffThePanel_IsNothing()
    {
        var rule = new SeatPanelRelayRule();

        Assert.AreEqual(SeatPanelAction.None, rule.Step(false, false, true, Nothing, false));
    }

    [TestMethod]
    public void Reset_ForgetsThePress()
    {
        var rule = new SeatPanelRelayRule();
        rule.Step(true, false, false, Over(SeatPanelKind.Card, One), false);
        rule.Reset();

        Assert.AreEqual(SeatPanelAction.None, rule.Step(false, true, false, Over(SeatPanelKind.Card, One), false));
    }

    // ---- The yield rule ----

    [TestMethod]
    public void Shield_HoldsOnlyWithCursorPanelAndATargetUnderThePointer()
    {
        Assert.IsTrue(SeatPanelYieldRule.Holds(cursorShown: true, panelOpen: true, lookHeld: false, overTarget: true, anotherLayerClaims: false));
    }

    [DataTestMethod]
    [DataRow(false, true, false, true, false, DisplayName = "no cursor")]
    [DataRow(true, false, false, true, false, DisplayName = "panel closed")]
    [DataRow(true, true, true, true, false, DisplayName = "looking with the middle button")]
    [DataRow(true, true, false, false, false, DisplayName = "pointer on the ground")]
    [DataRow(true, true, false, true, true, DisplayName = "another overlay takes the mouse there")]
    public void Shield_YieldsWhenAnyConditionFails(bool cursor, bool panel, bool look, bool over, bool other)
    {
        Assert.IsFalse(SeatPanelYieldRule.Holds(cursor, panel, look, over, other));
    }

    [DataTestMethod]
    [DataRow(true, true, false, false, false, true, DisplayName = "an overlay with the mouse buttons claims")]
    [DataRow(false, true, false, false, false, false, DisplayName = "an inactive layer does not")]
    [DataRow(true, false, false, false, false, false, DisplayName = "a layer with no mouse buttons does not")]
    [DataRow(true, true, true, false, false, false, DisplayName = "the scene layer does not")]
    [DataRow(true, true, false, true, false, false, DisplayName = "the order layer does not")]
    [DataRow(true, true, false, false, true, false, DisplayName = "the shield itself does not")]
    public void LayerMayClaim_SkipsTheSceneTheOrderLayerAndTheShield(bool active, bool mouseButtons, bool scene, bool order, bool self, bool expected)
    {
        Assert.AreEqual(expected, SeatPanelYieldRule.LayerMayClaim(active, mouseButtons, scene, order, self));
    }

    // ---- The zoom wheel ----

    [DataTestMethod]
    [DataRow(2f, 5f, true, true, 2f, DisplayName = "the scene layer's own read wins")]
    [DataRow(0f, 5f, true, true, 5f, DisplayName = "over a panel button the raw wheel stands in")]
    [DataRow(0f, 5f, false, true, 0f, DisplayName = "no shield, no stand-in")]
    [DataRow(0f, 5f, true, false, 0f, DisplayName = "a popup or a menu has the focus: no zoom")]
    public void WheelRule_UsesTheRawWheelOnlyWhileTheShieldIsUpAndTheSceneOrShieldHasFocus(float scene, float raw, bool shield, bool focus, float expected)
    {
        Assert.AreEqual(expected, SeatWheelRule.Read(scene, raw, shield, focus));
    }

    [TestMethod]
    public void ARightPressOffThePanel_LetGoOverAButton_IsNotEscape_TheReleaseBelongsToThePressThatSawIt()
    {
        var rule = new SeatPanelRelayRule();

        rule.Step(false, false, false, Nothing, false, rightPressed: true);

        Assert.AreEqual(SeatPanelAction.None, rule.Step(false, false, true, Over(SeatPanelKind.Card, One), false));
    }

    [TestMethod]
    public void ARightPressAndReleaseInOneFrameOverAButton_IsEscape()
    {
        var rule = new SeatPanelRelayRule();

        Assert.AreEqual(SeatPanelAction.Escape, rule.Step(false, false, true, Over(SeatPanelKind.Card, One), false, rightPressed: true));
    }

    [TestMethod]
    public void ARightReleaseTwice_IsEscapeOnlyOnce()
    {
        var rule = new SeatPanelRelayRule();

        rule.Step(false, false, false, Over(SeatPanelKind.Card, One), false, rightPressed: true);
        rule.Step(false, false, true, Over(SeatPanelKind.Card, One), false);

        Assert.AreEqual(SeatPanelAction.None, rule.Step(false, false, true, Over(SeatPanelKind.Card, One), false));
    }
}
