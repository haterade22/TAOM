using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using TAOM.Client.Adapters;
using TAOM.Client.Features.SeatOfSeeing;
using TAOM.Features.SeatOfSeeing.Domain;

namespace TAOM.Tests.Features.SeatOfSeeing;

/// <summary>The command mouse's control: which captured frames reach the engine, once, and what they write.</summary>
[TestClass]
public class SeatCommandControlTests
{
    private FakeSeatCommand _engine = null!;
    private RecordingOutcomeLog _log = null!;
    private SeatCommandControl _sut = null!;

    [TestInitialize]
    public void Setup()
    {
        _engine = new FakeSeatCommand { Selected = 1 };
        _log = new RecordingOutcomeLog();
        _sut = new SeatCommandControl(_engine, _log, "7");
    }

    private static SeatPointerFrame Frame(float seconds = 1f, float x = 400f, float y = 300f, bool leftPressed = false, bool leftReleased = false,
        bool rightPressed = false, bool rightReleased = false, bool rightDown = false, bool shift = false, bool ctrl = false, bool? leftDown = null) =>
        new(leftPressed, leftReleased, rightPressed, rightReleased, rightDown, x, y, x / 1000f, y / 1000f, shift, ctrl, seconds,
            LeftDown: leftDown ?? (leftPressed && !leftReleased));

    private void Tick(SeatPointerFrame frame)
    {
        _sut.Capture(frame);
        _sut.OnMissionTick();
    }

    private void LeftClick(bool shift = false, float x = 400f, float y = 300f) =>
        Tick(Frame(leftPressed: true, leftReleased: true, shift: shift, x: x, y: y));

    private int Lines(string result) => _log.Lines.Count(l => l.Id == SeatChecks.CommandId && l.Result == result);

    [TestMethod]
    public void LeftClick_OnAnUnselectedCompany_PicksAtThePointerAndReplacesTheSelection()
    {
        _engine.Own = new SeatPicked(true, false);

        LeftClick(x: 412f, y: 305f);

        Assert.AreEqual((412f, 305f), _engine.LastOwnPick);
        CollectionAssert.AreEqual(new[] { "pick-own", "select:ReplaceWith" }, _engine.Calls.Where(c => c != "line-cancel" && c != "box-hide").ToArray());
        Assert.AreEqual(1, Lines("pass"));
    }

    [TestMethod]
    public void LeftClick_WithShiftOnAnUnselectedCompany_AddsIt()
    {
        _engine.Own = new SeatPicked(true, false);

        LeftClick(shift: true);

        CollectionAssert.Contains(_engine.Calls, "select:Add");
    }

    [TestMethod]
    public void LeftClick_WithShiftOnASelectedCompany_RemovesIt()
    {
        _engine.Own = new SeatPicked(true, true);
        _engine.Selected = 2;

        LeftClick(shift: true);

        CollectionAssert.Contains(_engine.Calls, "select:Remove");
    }

    [TestMethod]
    public void LeftClick_OnNothing_ClearsTheSelection()
    {
        _engine.Own = default;
        _engine.Selected = 2;

        LeftClick();

        CollectionAssert.Contains(_engine.Calls, "select:ClearAll");
    }

    [TestMethod]
    public void LeftClick_OnNothingWithNothingSelected_WritesNothing()
    {
        _engine.Selected = 0;

        LeftClick();

        Assert.IsFalse(_engine.Calls.Any(c => c.StartsWith("select", System.StringComparison.Ordinal)));
        Assert.AreEqual(0, Lines("pass"));
    }

    [TestMethod]
    public void LeftRelease_WithNoPressOfOurs_DoesNothing()
    {
        // The press was over a panel button, which the screen adapter takes out of the frame: the release alone is not a click.
        _engine.Own = new SeatPicked(true, false);

        Tick(Frame(leftReleased: true));

        Assert.AreEqual(0, _engine.Calls.Count(c => c == "pick-own"));
    }

    [TestMethod]
    public void LeftDrag_IsNotAClick_ItIsABoxSelect()
    {
        Tick(Frame(leftPressed: true, seconds: 1f, x: 100f, y: 100f));
        _engine.Calls.Clear();

        Tick(Frame(leftReleased: true, seconds: 2f, x: 400f, y: 400f));

        Assert.AreEqual(0, _engine.Calls.Count(c => c == "pick-own"));
        Assert.AreEqual(1, _engine.Count("pick-box"));
    }

    [TestMethod]
    public void LeftClick_PressAndReleaseOnTwoFrames_Selects()
    {
        _engine.Own = new SeatPicked(true, false);
        Tick(Frame(leftPressed: true, seconds: 1f));

        Tick(Frame(leftReleased: true, seconds: 1.1f));

        CollectionAssert.Contains(_engine.Calls, "select:ReplaceWith");
    }

    [TestMethod]
    public void MissionTick_RepeatedByFastTime_ActsOnTheCapturedClickOnce()
    {
        _engine.Own = new SeatPicked(true, false);
        _sut.Capture(Frame(leftPressed: true, leftReleased: true));

        _sut.OnMissionTick();
        _sut.OnMissionTick();
        _sut.OnMissionTick();

        Assert.AreEqual(1, _engine.Count("select:ReplaceWith"));
        Assert.AreEqual(1, _engine.FactsReads, "the panel's gate is read once for the one frame");
    }

    [TestMethod]
    public void MissionTick_WithNothingCaptured_ReadsNothing()
    {
        _sut.OnMissionTick();

        Assert.AreEqual(0, _engine.FactsReads);
        Assert.AreEqual(0, _engine.Calls.Count);
    }

    [TestMethod]
    public void Capture_OfAFrameWithNothingToActOn_IsNotKept()
    {
        _sut.Capture(Frame());
        _sut.OnMissionTick();

        Assert.AreEqual(0, _engine.FactsReads);
    }

    [TestMethod]
    public void RightClick_OnAnEnemy_Charges()
    {
        _engine.Probe = new SeatRightProbe(true, false, true);

        Tick(Frame(rightPressed: true, rightReleased: true, x: 600f, y: 350f));

        Assert.AreEqual((600f, 350f, 0.6f, 0.35f), _engine.LastProbe);
        CollectionAssert.Contains(_engine.Calls, "charge");
        Assert.IsFalse(_engine.Calls.Contains("move"));
    }

    [TestMethod]
    public void RightClick_OnAnOrderableObject_UsesIt()
    {
        _engine.Probe = new SeatRightProbe(false, true, true);

        Tick(Frame(rightPressed: true, rightReleased: true));

        CollectionAssert.Contains(_engine.Calls, "orderable");
    }

    [TestMethod]
    public void RightClick_OnGround_MovesAsALineOfNoLength()
    {
        _engine.Probe = new SeatRightProbe(false, false, true);

        Tick(Frame(rightPressed: true, rightReleased: true));

        CollectionAssert.Contains(_engine.Calls, "move");
        Assert.AreEqual(false, _engine.LastMoveHorizontal);
    }

    [TestMethod]
    public void RightClick_WithControlOnGround_MovesWithTheHorizontalLayout()
    {
        _engine.Probe = new SeatRightProbe(false, false, true);

        Tick(Frame(rightPressed: true, rightReleased: true, ctrl: true));

        Assert.AreEqual(true, _engine.LastMoveHorizontal);
    }

    [TestMethod]
    public void RightClick_WithNothingSelected_GivesNoOrderAndWritesOneSkip()
    {
        _engine.Selected = 0;
        _engine.Probe = new SeatRightProbe(true, true, true);

        Tick(Frame(rightPressed: true, rightReleased: true));
        Tick(Frame(rightPressed: true, rightReleased: true));

        Assert.AreEqual(0, _engine.Count("charge") + _engine.Count("move") + _engine.Count("orderable"));
        Assert.AreEqual(1, Lines("skip"));
        StringAssert.Contains(_log.Lines.Single(l => l.Result == "skip").Detail, "nothing-selected");
    }

    [TestMethod]
    public void RightClick_OnGroundThatTakesNoCompany_GivesNoOrder()
    {
        _engine.Probe = new SeatRightProbe(false, false, false);

        Tick(Frame(rightPressed: true, rightReleased: true));

        Assert.AreEqual(0, _engine.Count("move"));
        StringAssert.Contains(_log.Lines.Single(l => l.Result == "skip").Detail, "no-target");
    }

    [TestMethod]
    public void RightClick_PressedAndReleasedOnTwoFrames_ProbesWhereItWentDown()
    {
        _engine.Probe = new SeatRightProbe(false, false, true);
        Tick(Frame(rightPressed: true, rightDown: true, seconds: 1f, x: 300f, y: 200f));

        Tick(Frame(rightReleased: true, seconds: 1.1f, x: 303f, y: 202f));

        Assert.AreEqual((300f, 200f, 0.3f, 0.2f), _engine.LastProbe);
        CollectionAssert.Contains(_engine.Calls, "move");
    }

    [TestMethod]
    public void RightClick_AfterAFastDrag_StillOrdersAtThePress()
    {
        // Far but under 0.3 s is a click, as the placer treats it.
        _engine.Probe = new SeatRightProbe(false, false, true);
        Tick(Frame(rightPressed: true, rightDown: true, seconds: 1f, x: 100f, y: 100f));

        Tick(Frame(rightReleased: true, seconds: 1.1f, x: 500f, y: 500f));

        Assert.AreEqual(0, _engine.Count("line-commit"));
        Assert.AreEqual((100f, 100f, 0.1f, 0.1f), _engine.LastProbe);
        CollectionAssert.Contains(_engine.Calls, "move");
    }

    [TestMethod]
    public void RightDrag_PreviewsEachFrameAndCommitsOnRelease()
    {
        Tick(Frame(rightPressed: true, rightDown: true, seconds: 1f, x: 100f, y: 100f));
        Tick(Frame(rightDown: true, seconds: 1.5f, x: 300f, y: 120f));
        Tick(Frame(rightDown: true, seconds: 1.6f, x: 320f, y: 130f));
        Tick(Frame(rightReleased: true, seconds: 1.7f, x: 340f, y: 140f));

        Assert.AreEqual(1, _engine.Count("line-begin"));
        Assert.AreEqual(2, _engine.Count("line-preview"));
        Assert.AreEqual(1, _engine.Count("line-commit"));
        Assert.AreEqual((0.34f, 0.14f, false), _engine.LastCommit);
        Assert.AreEqual(0, _engine.Count("move"), "a drag is a line, not a point");
        Assert.IsFalse(_engine.LineShown);
    }

    [TestMethod]
    public void RightDrag_WithControl_UsesTheHorizontalLayout()
    {
        Tick(Frame(rightPressed: true, rightDown: true, seconds: 1f, x: 100f, y: 100f));
        Tick(Frame(rightDown: true, seconds: 1.5f, x: 300f, y: 120f, ctrl: true));
        Tick(Frame(rightReleased: true, seconds: 1.7f, x: 340f, y: 140f, ctrl: true));

        Assert.AreEqual(true, _engine.LastPreview?.Horizontal);
        Assert.AreEqual(true, _engine.LastCommit?.Horizontal);
    }

    [TestMethod]
    public void RightDrag_StillUnderTheDragThreshold_ShowsNoLine()
    {
        Tick(Frame(rightPressed: true, rightDown: true, seconds: 1f, x: 100f, y: 100f));
        Tick(Frame(rightDown: true, seconds: 1.5f, x: 104f, y: 103f));

        Assert.AreEqual(0, _engine.Count("line-preview"));
    }

    [TestMethod]
    public void RightDrag_FromGroundThatTakesNoCompany_PreviewsAndCommitsNothing()
    {
        _engine.LineStarts = false;
        _engine.Probe = default;
        Tick(Frame(rightPressed: true, rightDown: true, seconds: 1f, x: 100f, y: 100f));
        Tick(Frame(rightDown: true, seconds: 1.5f, x: 300f, y: 120f));
        Tick(Frame(rightReleased: true, seconds: 1.7f, x: 340f, y: 140f));

        Assert.AreEqual(0, _engine.Count("line-preview"));
        Assert.AreEqual(0, _engine.Count("line-commit"));
    }

    [TestMethod]
    public void RightDrag_ReleasedWithNothingSelected_CommitsNothingAndHidesTheLine()
    {
        _engine.Selected = 0;
        Tick(Frame(rightPressed: true, rightDown: true, seconds: 1f, x: 100f, y: 100f));
        Tick(Frame(rightDown: true, seconds: 1.5f, x: 300f, y: 120f));
        Tick(Frame(rightReleased: true, seconds: 1.7f, x: 340f, y: 140f));

        Assert.AreEqual(0, _engine.Count("line-commit"));
        Assert.IsFalse(_engine.LineShown);
        StringAssert.Contains(_log.Lines.Single(l => l.Result == "skip").Detail, "nothing-selected");
    }

    [TestMethod]
    public void RightDrag_WhenTheButtonGoesUpWithNoReleaseEdge_CancelsTheLine()
    {
        Tick(Frame(rightPressed: true, rightDown: true, seconds: 1f, x: 100f, y: 100f));
        Tick(Frame(rightDown: true, seconds: 1.5f, x: 300f, y: 120f));
        Assert.IsTrue(_engine.LineShown);

        _sut.Capture(Frame(seconds: 1.6f, x: 310f, y: 120f, leftPressed: true));
        _sut.OnMissionTick();

        Assert.IsFalse(_engine.LineShown);
        Assert.AreEqual(0, _engine.Count("line-commit"));
    }

    [TestMethod]
    public void AfterTheHeroFell_AClickSelectsNothingOrdersNothingAndWritesOneSkip()
    {
        _engine.Facts = _engine.Facts with { MainAgentExists = false };
        _engine.Own = new SeatPicked(true, false);
        _engine.Probe = new SeatRightProbe(true, false, true);

        LeftClick();
        Tick(Frame(rightPressed: true, rightReleased: true));
        LeftClick();

        Assert.AreEqual(0, _engine.Calls.Count(c => c != "line-cancel" && c != "box-hide"), "no pick, no selection, no order reached the engine");
        Assert.AreEqual(1, Lines("skip"));
        StringAssert.Contains(_log.Lines.Single(l => l.Result == "skip").Detail, "hero-gone");
    }

    [TestMethod]
    public void AfterATake_TheClicksWorkAgain()
    {
        _engine.Facts = _engine.Facts with { MainAgentExists = false };
        LeftClick();
        _engine.Facts = _engine.Facts with { MainAgentExists = true };
        _engine.Own = new SeatPicked(true, false);

        LeftClick();

        CollectionAssert.Contains(_engine.Calls, "select:ReplaceWith");
    }

    [TestMethod]
    public void ARefusalMidDrag_HidesTheLineAndCommitsNothing()
    {
        Tick(Frame(rightPressed: true, rightDown: true, seconds: 1f, x: 100f, y: 100f));
        Tick(Frame(rightDown: true, seconds: 1.5f, x: 300f, y: 120f));
        _engine.Facts = _engine.Facts with { MainAgentExists = false };

        Tick(Frame(rightReleased: true, seconds: 1.7f, x: 340f, y: 140f));

        Assert.AreEqual(0, _engine.Count("line-commit"));
        Assert.IsFalse(_engine.LineShown);
    }

    [TestMethod]
    public void Reset_HidesALineAndForgetsAHeldButton()
    {
        Tick(Frame(rightPressed: true, rightDown: true, seconds: 1f, x: 100f, y: 100f));
        Tick(Frame(rightDown: true, seconds: 1.5f, x: 300f, y: 120f));

        _sut.Reset();

        Assert.IsFalse(_engine.LineShown);
        _sut.OnMissionTick();
        Assert.AreEqual(0, _engine.Count("line-commit"));
    }

    [TestMethod]
    public void RightClick_IsNotOnTheMarshalsPath_ItOnlyGivesTheVanillaOrder()
    {
        // Shift (queue) and Control on an enemy (mark) are Marshal's Word's held keys: the control gives the vanilla order and the capture patch takes it.
        _engine.Probe = new SeatRightProbe(true, false, true);

        Tick(Frame(rightPressed: true, rightReleased: true, shift: true, ctrl: true));

        Assert.AreEqual(1, _engine.Count("charge"));
        Assert.AreEqual(0, _engine.Count("move"));
    }

    [TestMethod]
    public void LeftClick_OnNothingWithThePanelOpen_KeepsTheSelection()
    {
        _engine.Facts = _engine.Facts with { OrderMenuOpen = true };
        _engine.Own = default;
        _engine.Selected = 2;

        LeftClick();

        CollectionAssert.DoesNotContain(_engine.Calls, "select:ClearAll");
    }

    [TestMethod]
    public void Finish_ForgetsTheLineWithNoNativeCall_AndDropsTheAdapter()
    {
        _engine.LineStarts = true;
        Tick(Frame(rightPressed: true, rightDown: true));
        _engine.Calls.Clear();

        _sut.Finish();

        CollectionAssert.AreEqual(new[] { "drop" }, _engine.Calls);
    }
}
