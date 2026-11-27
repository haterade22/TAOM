using System.Collections.Generic;
using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using TAOM.Client.Adapters;
using TAOM.Client.Features.SeatOfSeeing;
using TAOM.Features.SeatOfSeeing.Domain;

namespace TAOM.Tests.Features.SeatOfSeeing;

/// <summary>The command mouse's box select and Ctrl and number transfer, through the control with a scripted engine.</summary>
[TestClass]
public class SeatBoxControlTests
{
    private FakeSeatCommand _engine = null!;
    private RecordingOutcomeLog _log = null!;
    private SeatCommandControl _sut = null!;
    private List<SeatMessage> _said = null!;

    [TestInitialize]
    public void Setup()
    {
        _engine = new FakeSeatCommand { Selected = 1 };
        _log = new RecordingOutcomeLog();
        _said = new List<SeatMessage>();
        _sut = new SeatCommandControl(_engine, _log, "7", _said.Add);
    }

    private static SeatPointerFrame Frame(float seconds, float x, float y, bool leftPressed = false, bool leftReleased = false, bool leftDown = false,
        bool shift = false) =>
        new(leftPressed, leftReleased, false, false, false, x, y, x / 1000f, y / 1000f, shift, false, seconds, LeftDown: leftDown);

    private void Tick(SeatPointerFrame frame, int transfer = -1)
    {
        _sut.Capture(frame);
        _sut.OnMissionTick(transfer);
    }

    private void Press(float x = 100f, float y = 100f) => Tick(Frame(1f, x, y, leftPressed: true, leftDown: true));

    private int Lines(string result) => _log.Lines.Count(l => l.Id == SeatChecks.CommandId && l.Result == result);

    // ---- the rectangle follows the drag ----

    [TestMethod]
    public void Drag_PastTheClickTest_ShowsTheRectangleFromThePressToThePointer()
    {
        Press(100f, 200f);

        Tick(Frame(1.5f, 400f, 50f, leftDown: true));

        Assert.IsTrue(_engine.BoxShown);
        Assert.AreEqual(new SeatRect(100f, 50f, 400f, 200f), _engine.LastBoxShown);
    }

    [TestMethod]
    public void Drag_WithinTheClickTest_ShowsNoRectangle()
    {
        Press(100f, 100f);

        Tick(Frame(1.1f, 105f, 103f, leftDown: true));

        Assert.AreEqual(0, _engine.Count("box-show"));
    }

    [TestMethod]
    public void Drag_FarButQuick_IsStillAClick_AsTheOrderPlacerCountsIt()
    {
        Press(100f, 100f);

        Tick(Frame(1.1f, 400f, 400f, leftDown: true));

        Assert.AreEqual(0, _engine.Count("box-show"));
    }

    [TestMethod]
    public void Drag_FramesWithNoEdge_AreKeptWhileTheButtonIsHeld()
    {
        Press();

        _sut.Capture(Frame(1.5f, 300f, 300f, leftDown: true));
        _sut.OnMissionTick();

        Assert.AreEqual(1, _engine.Count("box-show"));
    }

    // ---- release ----

    [TestMethod]
    public void Release_AfterADrag_HidesTheRectangleAndSelectsTheCompaniesInside()
    {
        _engine.BoxFound = 2;
        Press(100f, 100f);
        Tick(Frame(1.5f, 400f, 400f, leftDown: true));

        Tick(Frame(2f, 400f, 400f, leftReleased: true));

        Assert.IsFalse(_engine.BoxShown);
        Assert.AreEqual(new SeatRect(100f, 100f, 400f, 400f), _engine.LastBoxPick);
        CollectionAssert.Contains(_engine.Calls, "select-box:ReplaceWith");
        Assert.AreEqual(0, _engine.Count("pick-own"));
        Assert.AreEqual(1, Lines("pass"));
    }

    [TestMethod]
    public void Release_AfterADragUpAndLeft_UsesTheNormalisedRectangle()
    {
        _engine.BoxFound = 1;
        Press(400f, 400f);

        Tick(Frame(2f, 100f, 150f, leftReleased: true));

        Assert.AreEqual(new SeatRect(100f, 150f, 400f, 400f), _engine.LastBoxPick);
    }

    [TestMethod]
    public void Release_WithShift_AddsToTheSelection()
    {
        _engine.BoxFound = 1;
        Press();

        Tick(Frame(2f, 400f, 400f, leftReleased: true, shift: true));

        CollectionAssert.Contains(_engine.Calls, "select-box:Add");
    }

    [TestMethod]
    public void Release_OnNothing_ClearsLikeAClick()
    {
        _engine.BoxFound = 0;
        _engine.Selected = 2;
        Press();

        Tick(Frame(2f, 400f, 400f, leftReleased: true));

        CollectionAssert.Contains(_engine.Calls, "select-box:ClearAll");
    }

    [TestMethod]
    public void Release_OnNothingWithThePanelOpen_KeepsTheSelection()
    {
        _engine.Facts = _engine.Facts with { OrderMenuOpen = true };
        _engine.BoxFound = 0;
        _engine.Selected = 2;
        Press();

        Tick(Frame(2f, 400f, 400f, leftReleased: true));

        Assert.IsFalse(_engine.Calls.Any(c => c.StartsWith("select-box", System.StringComparison.Ordinal)));
        Assert.AreEqual(0, Lines("pass"));
    }

    [TestMethod]
    public void Release_OnNothingWithShift_WritesNothing()
    {
        _engine.BoxFound = 0;
        Press();

        Tick(Frame(2f, 400f, 400f, leftReleased: true, shift: true));

        Assert.IsFalse(_engine.Calls.Any(c => c.StartsWith("select-box", System.StringComparison.Ordinal)));
    }

    [TestMethod]
    public void ButtonUpWithNoReleaseEdge_HidesTheRectangleAndSelectsNothing()
    {
        _engine.BoxFound = 2;
        Press();
        Tick(Frame(1.5f, 400f, 400f, leftDown: true));

        Tick(Frame(1.6f, 410f, 410f, leftDown: false));

        Assert.IsFalse(_engine.BoxShown);
        Assert.AreEqual(0, _engine.Count("pick-box"));
    }

    [TestMethod]
    public void ReleaseWithNoPressOfOurs_DrawsAndSelectsNothing()
    {
        // A press over a panel button never arms the control, so the drag and the release over the field are not a box.
        Tick(Frame(1.5f, 400f, 400f, leftDown: true));
        Tick(Frame(2f, 400f, 400f, leftReleased: true));

        Assert.AreEqual(0, _engine.Count("box-show"));
        Assert.AreEqual(0, _engine.Count("pick-box"));
    }

    // ---- hidden at every end ----

    [TestMethod]
    public void Reset_HidesTheRectangleAndForgetsTheDrag()
    {
        Press();
        Tick(Frame(1.5f, 400f, 400f, leftDown: true));

        _sut.Reset();

        Assert.IsFalse(_engine.BoxShown);
        Tick(Frame(2f, 400f, 400f, leftReleased: true));
        Assert.AreEqual(0, _engine.Count("pick-box"));
    }

    [TestMethod]
    public void Finish_MakesNoNativeCall_OnlyDropsTheAdapter()
    {
        Press();
        Tick(Frame(1.5f, 400f, 400f, leftDown: true));
        _engine.Calls.Clear();

        _sut.Finish();

        CollectionAssert.AreEqual(new[] { "drop" }, _engine.Calls);
    }

    [TestMethod]
    public void ARefusalMidDrag_HidesTheRectangle()
    {
        Press();
        Tick(Frame(1.5f, 400f, 400f, leftDown: true));
        _engine.Facts = _engine.Facts with { MainAgentExists = false };

        Tick(Frame(1.6f, 410f, 410f, leftDown: true));

        Assert.IsFalse(_engine.BoxShown);
    }

    // ---- Ctrl and a number ----

    [TestMethod]
    public void Transfer_WithCompaniesSelected_MovesTheirSoldiersIntoTheTarget()
    {
        _engine.TransferFacts = new SeatTransferFacts(true, false, 2, 90, 10);

        _sut.OnMissionTick(transferTo: 3);

        Assert.AreEqual((3, 90), _engine.LastTransfer);
        CollectionAssert.Contains(_engine.Calls, "read-transfer:3");
        Assert.AreEqual(1, Lines("pass"));
        Assert.AreEqual(0, _said.Count);
    }

    [TestMethod]
    public void Transfer_WithTheTargetSelectedToo_MovesOnlyTheOthers()
    {
        _engine.TransferFacts = new SeatTransferFacts(true, true, 3, 100, 25);

        _sut.OnMissionTick(transferTo: 0);

        Assert.AreEqual((0, 75), _engine.LastTransfer);
    }

    [TestMethod]
    public void Transfer_IntoAnEmptyCompany_IsGiven()
    {
        _engine.TransferFacts = new SeatTransferFacts(true, false, 1, 40, 0);

        _sut.OnMissionTick(transferTo: 7);

        Assert.AreEqual((7, 40), _engine.LastTransfer);
    }

    [TestMethod]
    public void Transfer_WithNoSource_IsRefusedWithOneMessageAndOneSkipLine()
    {
        _engine.TransferFacts = new SeatTransferFacts(true, true, 1, 50, 50);

        _sut.OnMissionTick(transferTo: 1);

        Assert.IsNull(_engine.LastTransfer);
        CollectionAssert.AreEqual(new[] { SeatMessage.TransferRefused }, _said);
        Assert.AreEqual(1, Lines("skip"));
    }

    [TestMethod]
    public void Transfer_WithTheHeroGone_IsRefusedByTheCommandRuleAndGivesNothing()
    {
        _engine.Facts = _engine.Facts with { MainAgentExists = false };

        _sut.OnMissionTick(transferTo: 1);

        Assert.IsNull(_engine.LastTransfer);
        Assert.AreEqual(0, _engine.Count("read-transfer:1"));
        CollectionAssert.AreEqual(new[] { SeatMessage.TransferUnavailable }, _said);
        Assert.AreEqual(1, Lines("skip"));
    }

    [TestMethod]
    public void Transfer_RefusedTwice_SaysTwiceButWritesOneSkipLine()
    {
        _engine.Facts = _engine.Facts with { MainAgentExists = false };

        _sut.OnMissionTick(transferTo: 1);
        _sut.OnMissionTick(transferTo: 2);

        Assert.AreEqual(2, _said.Count);
        Assert.AreEqual(1, Lines("skip"));
    }

    [TestMethod]
    public void Transfer_InDeployment_IsRefusedWithTheUnavailableMessage()
    {
        _engine.TransferFacts = new SeatTransferFacts(true, false, 2, 90, 10, Deployment: true);

        _sut.OnMissionTick(transferTo: 3);

        Assert.IsNull(_engine.LastTransfer);
        CollectionAssert.AreEqual(new[] { SeatMessage.TransferUnavailable }, _said);
        Assert.AreEqual(1, Lines("skip"));
    }

    [TestMethod]
    public void Transfer_WithTheHeroInASelectedCompany_MovesOneLess()
    {
        _engine.TransferFacts = new SeatTransferFacts(true, false, 2, 90, 10, SelectedHasHero: true);

        _sut.OnMissionTick(transferTo: 3);

        Assert.AreEqual((3, 89), _engine.LastTransfer);
    }

    [TestMethod]
    public void Transfer_WithOnlyTheHerosCompany_IsRefused()
    {
        _engine.TransferFacts = new SeatTransferFacts(true, false, 1, 1, 10, SelectedHasHero: true);

        _sut.OnMissionTick(transferTo: 3);

        Assert.IsNull(_engine.LastTransfer);
        CollectionAssert.AreEqual(new[] { SeatMessage.TransferRefused }, _said);
    }

    // ---- looking mid-drag ----

    [TestMethod]
    public void LookStartedMidDrag_HidesTheRectangleAndDropsTheDrag()
    {
        Press();
        Tick(Frame(1.5f, 400f, 400f, leftDown: true));
        Assert.IsTrue(_engine.BoxShown);

        _sut.LookStarted();

        Assert.IsFalse(_engine.BoxShown);
        Tick(Frame(2f, 400f, 400f, leftReleased: true));
        Assert.AreEqual(0, _engine.Count("pick-box"));
        Assert.AreEqual(0, _engine.Count("select:ReplaceWith"));
    }

    [TestMethod]
    public void NoRequestAndNoFrame_DoesNoWork()
    {
        _sut.OnMissionTick();

        Assert.AreEqual(0, _engine.FactsReads);
    }

    [TestMethod]
    public void Transfer_AndAClickInOneTick_BothRun_TransferFirst()
    {
        _engine.Own = new SeatPicked(true, false);

        Tick(Frame(1f, 400f, 300f, leftPressed: true, leftReleased: true), transfer: 2);

        var calls = _engine.Calls.Where(c => c == "transfer" || c == "select:ReplaceWith").ToArray();
        CollectionAssert.AreEqual(new[] { "transfer", "select:ReplaceWith" }, calls);
    }
}
