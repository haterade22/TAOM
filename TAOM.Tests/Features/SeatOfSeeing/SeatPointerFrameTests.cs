using Microsoft.VisualStudio.TestTools.UnitTesting;
using TAOM.Features.SeatOfSeeing.Domain;

namespace TAOM.Tests.Features.SeatOfSeeing;

/// <summary>The pointer buffer: the screen tick captures, the first mission tick takes once.</summary>
[TestClass]
public class SeatPointerFrameTests
{
    private static SeatPointerFrame Frame(float seconds = 1f, float x = 10f, float y = 20f, bool leftPressed = false, bool leftReleased = false,
        bool rightPressed = false, bool rightReleased = false, bool rightDown = false, bool shift = false, bool ctrl = false) =>
        new(leftPressed, leftReleased, rightPressed, rightReleased, rightDown, x, y, x / 1000f, y / 1000f, shift, ctrl, seconds);

    [TestMethod]
    public void TryTake_WithNothingCaptured_IsFalse()
    {
        var buffer = new SeatPointerBuffer();

        Assert.IsFalse(buffer.TryTake(out _));
    }

    [TestMethod]
    public void TryTake_AfterOneCapture_AnswersOnceNoMatterHowManyMissionTicksFollow()
    {
        var buffer = new SeatPointerBuffer();
        buffer.Capture(Frame(leftReleased: true));

        Assert.IsTrue(buffer.TryTake(out var first));
        Assert.IsTrue(first.LeftReleased);
        Assert.IsFalse(buffer.TryTake(out _), "fast time repeats the mission tick: the frame was taken");
        Assert.IsFalse(buffer.TryTake(out _));
    }

    [TestMethod]
    public void TryTake_AfterAnotherCapture_AnswersAgain()
    {
        var buffer = new SeatPointerBuffer();
        buffer.Capture(Frame(rightDown: true));
        buffer.TryTake(out _);

        buffer.Capture(Frame(rightDown: true, x: 50f));

        Assert.IsTrue(buffer.TryTake(out var frame));
        Assert.AreEqual(50f, frame.PixelX);
    }

    [TestMethod]
    public void Capture_TwiceBeforeATake_JoinsTheEdgesSoAPressAndItsReleaseAreBothKept()
    {
        var buffer = new SeatPointerBuffer();
        buffer.Capture(Frame(leftPressed: true, seconds: 1f, x: 10f));
        buffer.Capture(Frame(leftReleased: true, seconds: 1.05f, x: 12f));

        Assert.IsTrue(buffer.TryTake(out var frame));
        Assert.IsTrue(frame.LeftPressed);
        Assert.IsTrue(frame.LeftReleased);
        Assert.AreEqual(12f, frame.PixelX, "the pointer is the latest");
        Assert.AreEqual(10f, frame.LeftPressPixelX, "and the press is where the button went down");
        Assert.AreEqual(1f, frame.LeftPressSeconds);
    }

    [TestMethod]
    public void Capture_AfterATake_DoesNotCarryTheOldEdges()
    {
        var buffer = new SeatPointerBuffer();
        buffer.Capture(Frame(rightPressed: true));
        buffer.TryTake(out _);

        buffer.Capture(Frame(rightDown: true));

        buffer.TryTake(out var frame);
        Assert.IsFalse(frame.RightPressed, "the press was acted on once");
    }

    [TestMethod]
    public void Capture_RightPress_IsRememberedForTheFramesOfTheDrag()
    {
        var buffer = new SeatPointerBuffer();
        buffer.Capture(Frame(rightPressed: true, rightDown: true, seconds: 5f, x: 100f, y: 200f));
        buffer.TryTake(out _);

        buffer.Capture(Frame(rightDown: true, seconds: 5.5f, x: 400f, y: 250f));

        buffer.TryTake(out var frame);
        Assert.AreEqual(100f, frame.PressPixelX);
        Assert.AreEqual(200f, frame.PressPixelY);
        Assert.AreEqual(0.1f, frame.PressRangedX, 1e-5f);
        Assert.AreEqual(5f, frame.PressSeconds);
        Assert.AreEqual(400f, frame.PixelX);
    }

    [TestMethod]
    public void Capture_KeepsTheModifierKeysOfTheLatestFrame()
    {
        var buffer = new SeatPointerBuffer();
        buffer.Capture(Frame(leftReleased: true, shift: true));
        buffer.Capture(Frame(ctrl: true));

        buffer.TryTake(out var frame);
        Assert.IsFalse(frame.Shift);
        Assert.IsTrue(frame.Ctrl);
        Assert.IsTrue(frame.LeftReleased, "the edge of the earlier capture is kept");
    }

    [TestMethod]
    public void Clear_ForgetsAWaitingFrame()
    {
        var buffer = new SeatPointerBuffer();
        buffer.Capture(Frame(leftReleased: true));

        buffer.Clear();

        Assert.IsFalse(buffer.TryTake(out _));
    }

    [TestMethod]
    public void HasEdge_IsTrueForAnyButtonEdge()
    {
        Assert.IsFalse(Frame().HasEdge);
        Assert.IsFalse(Frame(rightDown: true).HasEdge);
        Assert.IsTrue(Frame(leftPressed: true).HasEdge);
        Assert.IsTrue(Frame(leftReleased: true).HasEdge);
        Assert.IsTrue(Frame(rightPressed: true).HasEdge);
        Assert.IsTrue(Frame(rightReleased: true).HasEdge);
    }

    [TestMethod]
    public void Edges_ARightPressOffThePanel_LetGoOverAButton_PassesTheReleaseToTheCommandMouseOnly()
    {
        var edges = new SeatPointerEdges();

        var press = edges.Take(false, false, true, false, overPanel: false, leftDown: false, rightDown: true);
        var release = edges.Take(false, false, false, true, overPanel: true, leftDown: false, rightDown: false);

        Assert.IsTrue(press.RightPressed);
        Assert.IsTrue(release.RightReleased, "the command mouse owns the whole press and release");
    }

    [TestMethod]
    public void Edges_ARightPressOnAButton_LetGoOffThePanel_NeverReachesTheCommandMouse()
    {
        var edges = new SeatPointerEdges();

        var press = edges.Take(false, false, true, false, overPanel: true, leftDown: false, rightDown: true);
        var release = edges.Take(false, false, false, true, overPanel: false, leftDown: false, rightDown: false);

        Assert.IsFalse(press.RightPressed);
        Assert.IsFalse(release.RightReleased, "an order is not given for a press the panel had");
    }

    [TestMethod]
    public void Edges_ALeftPressOnACard_IsDroppedWithItsRelease_SoNoStraySelectionFollows()
    {
        var edges = new SeatPointerEdges();

        var press = edges.Take(true, false, false, false, overPanel: true, leftDown: true, rightDown: false);
        var release = edges.Take(false, true, false, false, overPanel: true, leftDown: false, rightDown: false);

        Assert.IsFalse(press.LeftPressed);
        Assert.IsFalse(release.LeftReleased);
    }

    [TestMethod]
    public void Edges_ALeftPressOffThePanel_LetGoOverACard_StillReleasesToTheCommandMouse_WhichArmedAtThePress()
    {
        var edges = new SeatPointerEdges();

        edges.Take(true, false, false, false, overPanel: false, leftDown: true, rightDown: false);
        var release = edges.Take(false, true, false, false, overPanel: true, leftDown: false, rightDown: false);

        Assert.IsTrue(release.LeftReleased);
    }

    [TestMethod]
    public void Edges_AReleaseWithNoPressSeen_IsDropped()
    {
        var edges = new SeatPointerEdges();

        var release = edges.Take(false, true, false, true, overPanel: false, leftDown: false, rightDown: false);

        Assert.IsFalse(release.LeftReleased);
        Assert.IsFalse(release.RightReleased);
    }

    [TestMethod]
    public void Edges_APressAndReleaseInOneFrameOffThePanel_BothPass()
    {
        var edges = new SeatPointerEdges();

        var both = edges.Take(true, true, true, true, overPanel: false, leftDown: false, rightDown: false);

        Assert.IsTrue(both.LeftPressed && both.LeftReleased && both.RightPressed && both.RightReleased);
    }

    [TestMethod]
    public void Edges_AButtonThatWentUpWithNoReleaseEdge_ForgetsItsPress()
    {
        var edges = new SeatPointerEdges();

        edges.Take(false, false, true, false, overPanel: false, leftDown: false, rightDown: true);
        edges.Take(false, false, false, false, overPanel: false, leftDown: false, rightDown: false);
        var release = edges.Take(false, false, false, true, overPanel: false, leftDown: false, rightDown: false);

        Assert.IsFalse(release.RightReleased);
    }
}
