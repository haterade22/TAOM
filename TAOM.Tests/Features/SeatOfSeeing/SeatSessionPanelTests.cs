using System;
using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using NSubstitute;
using TAOM.Client.Features.SeatOfSeeing;
using TAOM.Core.Logging;
using TAOM.Features.SeatOfSeeing;
using TAOM.Features.SeatOfSeeing.Domain;

namespace TAOM.Tests.Features.SeatOfSeeing;

/// <summary>Stage S2 of the Seat's state machine (build-spec 6.0 to 6.5): the cursor, the reopened panel, the hidden interface and the shared cleanup.</summary>
[TestClass]
public class SeatSessionPanelTests
{
    private static readonly SeatPermissions Allowed = new(Camera: true, FightOn: true, Orders: true, Time: true, TakeSoldier: true);

    private FakeSeatScreen _screen = null!;
    private FakeSeatHero _hero = null!;
    private FakeSeatSettings _settings = null!;
    private RecordingOutcomeLog _log = null!;
    private SeatSession _sut = null!;

    [TestInitialize]
    public void Setup()
    {
        SeatPresence.Set(false);
        _screen = new FakeSeatScreen();
        _hero = new FakeSeatHero();
        _settings = new FakeSeatSettings();
        _log = new RecordingOutcomeLog();
        _sut = new SeatSession(_screen, _hero, _settings, () => Allowed, _log, Substitute.For<IModLogger>(), "7");
    }

    [TestCleanup]
    public void Cleanup() => SeatPresence.Set(false);

    private void Seated()
    {
        _sut.OnScreenTick(true);
        for (var i = 0; i < 20 && _sut.State != SeatState.Seated; i++)
            _sut.OnFrame(0.1f);
        Assert.AreEqual(SeatState.Seated, _sut.State);
    }

    private void Frame() => _sut.OnScreenTick(false);

    private int Lines(string result, string id) => _log.Count(result, id);

    // ---- The entry check ----

    [TestMethod]
    public void Key_WithARadialMenuActive_DoesNotEnter_AndSaysNothing()
    {
        _screen.Facts = _screen.Facts with { RadialMenuActive = true };

        _sut.OnScreenTick(true);

        Assert.AreEqual(SeatState.Off, _sut.State);
        Assert.AreEqual(0, _screen.Messages.Count);
        Assert.AreEqual(0, _log.Lines.Count, "silent like the other interface states");
        Assert.AreEqual(0, _screen.ControllerWrites.Count);
    }

    [TestMethod]
    public void Key_WhenTheRadialMenuIsGone_Enters()
    {
        _screen.Facts = _screen.Facts with { RadialMenuActive = true };
        _sut.OnScreenTick(true);
        _screen.Facts = _screen.Facts with { RadialMenuActive = false };

        _sut.OnScreenTick(true);

        Assert.AreEqual(SeatState.Rising, _sut.State);
    }

    // ---- The cursor ----

    [TestMethod]
    public void Cursor_WhileSeatedWithThePanelOpen_ShowsOnce_AndTellsThePatches()
    {
        Seated();
        _screen.PanelOpen = true;

        Frame();
        Frame();

        CollectionAssert.AreEqual(new[] { true }, _screen.CursorWrites);
        Assert.IsTrue(SeatPresence.CursorShown);
        Assert.AreEqual(1, Lines("pass", SeatChecks.CursorId));
    }

    [TestMethod]
    public void Cursor_WhenThePanelCloses_Hides()
    {
        Seated();
        _screen.PanelOpen = true;
        Frame();

        _screen.PanelOpen = false;
        Frame();

        CollectionAssert.AreEqual(new[] { true, false }, _screen.CursorWrites);
        Assert.IsFalse(SeatPresence.CursorShown);
    }

    [TestMethod]
    public void Cursor_WithTheSettingOff_NeverShows()
    {
        Seated();
        _settings.CursorWithPanel = false;
        _screen.PanelOpen = true;

        Frame();

        Assert.AreEqual(0, _screen.CursorWrites.Count);
        Assert.IsFalse(SeatPresence.CursorShown);
    }

    [TestMethod]
    public void Cursor_WhileRising_IsNotShown()
    {
        _sut.OnScreenTick(true);
        _screen.PanelOpen = true;

        Frame();

        Assert.AreEqual(SeatState.Rising, _sut.State);
        Assert.AreEqual(0, _screen.CursorWrites.Count);
    }

    [TestMethod]
    public void Cursor_WhileTheLookIsHeld_HidesAndHoldsItsPlaceEveryFrame()
    {
        Seated();
        _screen.PanelOpen = true;
        Frame();

        _screen.Look = true;
        Frame();
        Frame();

        CollectionAssert.AreEqual(new[] { true, false }, _screen.CursorWrites);
        Assert.AreEqual(2, _screen.Holds, "DontChangeCursorPos is asked each frame the look is held");
        Assert.IsFalse(SeatPresence.CursorShown);
    }

    [TestMethod]
    public void Cursor_WhenTheLookIsReleased_ShowsAgain()
    {
        Seated();
        _screen.PanelOpen = true;
        Frame();
        _screen.Look = true;
        Frame();

        _screen.Look = false;
        Frame();

        CollectionAssert.AreEqual(new[] { true, false, true }, _screen.CursorWrites);
    }

    [TestMethod]
    public void Cursor_WithNoPanelAndTheLookHeld_AsksForNoHold()
    {
        Seated();
        _screen.Look = true;

        Frame();

        Assert.AreEqual(0, _screen.Holds, "nothing to hold: no cursor was going to show");
    }

    [TestMethod]
    public void Cursor_WithTheLookHeldAndThePanelOpenButTheSettingOff_AsksForNoHold()
    {
        Seated();
        _settings.CursorWithPanel = false;
        _screen.PanelOpen = true;
        _screen.Look = true;

        Frame();
        Frame();

        Assert.AreEqual(0, _screen.Holds, "no cursor was going to show, so there is no position to hold");
        Assert.AreEqual(0, _screen.CursorWrites.Count);
    }

    [TestMethod]
    public void Cursor_WhenTheSeatIsLeft_IsHidden_AndTheStaticIsClear()
    {
        Seated();
        _screen.PanelOpen = true;
        Frame();

        _sut.OnScreenTick(true);

        Assert.AreEqual(SeatState.Off, _sut.State);
        Assert.IsFalse(_screen.CursorWrites.Last());
        Assert.IsFalse(SeatPresence.CursorShown);
    }

    [TestMethod]
    public void Focus_WhenThePatchSetTheField_WritesTheCheckLineOnce()
    {
        Seated();
        _screen.PanelOpen = true;
        Frame();
        Assert.AreEqual(0, Lines("pass", SeatChecks.FocusId), "nothing until the prefix has run");

        SeatPresence.MarkFocusSet();
        Frame();
        Frame();

        Assert.AreEqual(1, Lines("pass", SeatChecks.FocusId));
    }

    [TestMethod]
    public void Focus_WhenThePatchMarksEveryFrame_CallsPassOnce()
    {
        Seated();
        _screen.PanelOpen = true;
        Frame();

        for (var i = 0; i < 3; i++)
        {
            SeatPresence.MarkFocusSet();
            Frame();
        }

        Assert.AreEqual(1, Lines("pass", SeatChecks.FocusId));
        Assert.AreEqual(1, _log.Calls("pass", SeatChecks.FocusId), "the prefix marks every frame, and the line costs a call each time it is asked for");
    }

    // ---- The hidden interface ----

    [TestMethod]
    public void PressHide_WhileSeated_HidesTheInterface_AndWritesTheLine()
    {
        Seated();

        _sut.PressHide();

        CollectionAssert.AreEqual(new[] { true }, _screen.UiWrites);
        Assert.AreEqual(1, _log.Lines.Count(l => l.Id == SeatChecks.HideId && l.Key == "7:hidden"));
    }

    [TestMethod]
    public void PressHide_Again_RestoresTheValueItSaved()
    {
        Seated();
        _sut.PressHide();

        _sut.PressHide();

        CollectionAssert.AreEqual(new[] { true, false }, _screen.UiWrites);
        Assert.AreEqual(1, _log.Lines.Count(l => l.Id == SeatChecks.HideId && l.Key == "7:restored-key"));
    }

    [TestMethod]
    public void PressHide_AfterThePlayerRanToggleUi_RestoresTheirChoice()
    {
        Seated();
        _screen.UiHidden = true;
        _sut.PressHide();

        _sut.PressHide();

        Assert.IsTrue(_screen.UiHidden, "the player's own toggle_ui stays");
    }

    [TestMethod]
    public void PressHide_WhileOffOrRising_DoesNothing()
    {
        _sut.PressHide();
        _sut.OnScreenTick(true);
        _sut.PressHide();

        Assert.AreEqual(SeatState.Rising, _sut.State);
        Assert.AreEqual(0, _screen.UiWrites.Count);
    }

    [TestMethod]
    public void PressHide_DuringPhotoMode_DoesNothing()
    {
        Seated();
        _screen.Facts = _screen.Facts with { PhotoMode = true };

        _sut.PressHide();

        Assert.AreEqual(0, _screen.UiWrites.Count, "photo mode is the engine's second writer of the flag: no Seat key acts there (build-spec 5.3)");
        Assert.AreEqual(0, _log.Lines.Count(l => l.Id == SeatChecks.HideId));

        _screen.Facts = _screen.Facts with { PhotoMode = false };
        _sut.PressHide();

        CollectionAssert.AreEqual(new[] { true }, _screen.UiWrites, "the key works again when photo mode ends");
    }

    [TestMethod]
    public void PanelWork_DuringPhotoMode_TouchesNothing()
    {
        _settings.KeepPanelOpen = true;
        Seated();
        _screen.PanelOpen = true;
        Frame();
        _sut.PressHide();
        _screen.PanelOpen = false;
        _screen.EngineRefusesReopen = true;
        _screen.Dialog = true;
        _screen.Facts = _screen.Facts with { PhotoMode = true };
        var cursorWrites = _screen.CursorWrites.Count;
        var uiWrites = _screen.UiWrites.Count;
        var signalReads = _screen.SignalReads;
        var dialogReads = _screen.DialogReads;

        Frame();
        Frame();

        Assert.AreEqual(SeatState.Seated, _sut.State, "photo mode is not a leave");
        Assert.AreEqual(cursorWrites, _screen.CursorWrites.Count);
        Assert.AreEqual(uiWrites, _screen.UiWrites.Count);
        Assert.AreEqual(0, _screen.Reopens);
        Assert.AreEqual(signalReads, _screen.SignalReads);
        Assert.AreEqual(dialogReads, _screen.DialogReads);
    }

    [TestMethod]
    public void PressHide_ToRestore_DuringPhotoMode_LeavesTheHideInForce()
    {
        Seated();
        _sut.PressHide();
        _screen.Facts = _screen.Facts with { PhotoMode = true };

        _sut.PressHide();

        CollectionAssert.AreEqual(new[] { true }, _screen.UiWrites, "no restore write while photo mode runs");
    }

    [DataTestMethod]
    [DataRow("panel")]
    [DataRow("dialog")]
    [DataRow("layer")]
    public void Hidden_WhenAReasonAppears_RestoresAtOnce_NamingIt(string reason)
    {
        Seated();
        _sut.PressHide();
        _screen.PanelOpen = reason == "panel";
        _screen.Dialog = reason == "dialog";
        _screen.OtherLayer = reason == "layer";

        Frame();

        CollectionAssert.AreEqual(new[] { true, false }, _screen.UiWrites);
        Assert.AreEqual(1, _log.Lines.Count(l => l.Id == SeatChecks.HideId && l.Key == "7:restored-" + reason));
    }

    [TestMethod]
    public void Hidden_WhileNothingHappens_StaysHidden()
    {
        Seated();
        _sut.PressHide();

        Frame();
        Frame();

        CollectionAssert.AreEqual(new[] { true }, _screen.UiWrites);
    }

    [TestMethod]
    public void NotHidden_TheDialogAndLayerAreNeverRead()
    {
        Seated();

        Frame();
        Frame();

        Assert.AreEqual(0, _screen.DialogReads);
        Assert.AreEqual(0, _screen.LayerReads);
    }

    [TestMethod]
    public void Hidden_EachFrame_ReadsTheDialogAndLayerOnce()
    {
        Seated();
        _sut.PressHide();

        Frame();

        Assert.AreEqual(1, _screen.DialogReads);
        Assert.AreEqual(1, _screen.LayerReads);
    }

    [TestMethod]
    public void Hidden_WhenTheSeatIsLeft_RestoresTheSavedValue()
    {
        Seated();
        _sut.PressHide();

        _sut.OnScreenTick(true);

        CollectionAssert.AreEqual(new[] { true, false }, _screen.UiWrites);
        Assert.AreEqual(1, _log.Lines.Count(l => l.Id == SeatChecks.HideId && l.Key == "7:restored-leave"));
    }

    [TestMethod]
    public void NotHidden_WhenTheSeatIsLeft_WritesNoFlag()
    {
        Seated();

        _sut.OnScreenTick(true);

        Assert.AreEqual(0, _screen.UiWrites.Count, "only the Seat's own hide is undone");
    }

    [TestMethod]
    public void Hidden_WhenTheMissionEnds_RestoresItToo()
    {
        Seated();
        _sut.PressHide();

        _sut.Finish();

        CollectionAssert.AreEqual(new[] { true, false }, _screen.UiWrites);
    }

    [TestMethod]
    public void Hidden_WhenTheSeatFaults_RestoresIt()
    {
        Seated();
        _sut.PressHide();
        _screen.ApplyThrows = new InvalidOperationException("boom");

        _sut.OnFrame(0.1f);

        Assert.IsTrue(_sut.Faulted);
        CollectionAssert.AreEqual(new[] { true, false }, _screen.UiWrites);
        Assert.AreEqual(1, _log.Lines.Count(l => l.Id == SeatChecks.HideId && l.Key == "7:restored-fault"));
    }

    [TestMethod]
    public void PressHide_ThatThrows_LatchesTheFault()
    {
        Seated();
        _screen.UiThrows = new InvalidOperationException("boom");

        _sut.PressHide();

        Assert.IsTrue(_sut.Faulted);
        Assert.IsFalse(SeatPresence.IsSeated);
    }

    // ---- Keep the panel open ----

    private void OpenAndCloseWithKeepOn()
    {
        _settings.KeepPanelOpen = true;
        Seated();
        _screen.PanelOpen = true;
        Frame();
        _screen.PanelOpen = false;
    }

    [TestMethod]
    public void KeepOpen_AClosingEdgeWithNoPlayerKey_ReopensThePanel_AndWritesTheLine()
    {
        OpenAndCloseWithKeepOn();

        Frame();

        Assert.AreEqual(1, _screen.Reopens);
        Assert.AreEqual(1, Lines("pass", SeatChecks.PanelReopenedId));
    }

    [DataTestMethod]
    [DataRow(true, false, false, false, DisplayName = "Escape")]
    [DataRow(false, true, false, false, DisplayName = "the right mouse button")]
    [DataRow(false, false, true, false, DisplayName = "the panel key")]
    [DataRow(false, false, false, true, DisplayName = "the hold key")]
    public void KeepOpen_AClosingEdgeWithAPlayerRoute_StaysClosed(bool escape, bool right, bool viewOrders, bool holdOrder)
    {
        OpenAndCloseWithKeepOn();
        _screen.Signals = new SeatCloseSignals(escape, right, viewOrders, holdOrder);

        Frame();
        _screen.Signals = default;
        Frame();

        Assert.AreEqual(0, _screen.Reopens);
    }

    [TestMethod]
    public void KeepOpen_AnEscapeThatRanAfterTheViewsTick_IsSeenOnThePreviousFrame()
    {
        _settings.KeepPanelOpen = true;
        Seated();
        _screen.PanelOpen = true;
        Frame();

        _screen.Signals = new SeatCloseSignals(Escape: true, false, false, false);
        Frame();
        _screen.Signals = default;
        _screen.PanelOpen = false;
        Frame();

        Assert.AreEqual(0, _screen.Reopens, "the key was seen on the frame before the edge");
    }

    [TestMethod]
    public void KeepOpen_WithTheSettingOff_ReadsNoSignalAndNeverReopens()
    {
        _settings.KeepPanelOpen = false;
        Seated();
        _screen.PanelOpen = true;
        Frame();
        _screen.PanelOpen = false;

        Frame();

        Assert.AreEqual(0, _screen.Reopens);
        Assert.AreEqual(0, _screen.SignalReads);
    }

    [TestMethod]
    public void KeepOpen_WhenTheSeatIsNotSeated_DoesNothing()
    {
        _settings.KeepPanelOpen = true;
        _sut.OnScreenTick(true);
        _screen.PanelOpen = true;
        Frame();
        _screen.PanelOpen = false;

        Frame();

        Assert.AreEqual(SeatState.Rising, _sut.State);
        Assert.AreEqual(0, _screen.Reopens);
    }

    [TestMethod]
    public void KeepOpen_ReenteringAfterLeavingWithThePanelOpen_DoesNotReopen()
    {
        _settings.KeepPanelOpen = true;
        Seated();
        _screen.PanelOpen = true;
        Frame();
        _sut.OnScreenTick(true);
        Assert.AreEqual(SeatState.Off, _sut.State);
        _screen.PanelOpen = false;

        Seated();
        Frame();
        Frame();

        Assert.AreEqual(0, _screen.Reopens, "the panel the hero closed while the Seat was off is not a closing edge of the new stay");
    }

    [TestMethod]
    public void KeepOpen_APanelThatClosesAgainRightAfterAReopen_IsReopenedAgain()
    {
        OpenAndCloseWithKeepOn();
        Frame();
        Assert.AreEqual(1, _screen.Reopens);

        _screen.PanelOpen = false;
        Frame();

        Assert.AreEqual(2, _screen.Reopens, "the frame of the first reopen left the panel open, so the next close is an edge");
    }

    [TestMethod]
    public void KeepOpen_AConversationThatClosesThePanel_LeavesTheSeat_AndDoesNotReopenOverIt()
    {
        OpenAndCloseWithKeepOn();
        _screen.Facts = _screen.Facts with { Interlude = true };

        Frame();

        Assert.AreEqual(SeatState.Off, _sut.State);
        Assert.AreEqual(0, _screen.Reopens);
    }

    [TestMethod]
    public void KeepOpen_WhenTheViewModelCannotBeReached_WritesAFailLine()
    {
        OpenAndCloseWithKeepOn();
        _screen.ReopenWorks = false;

        Frame();

        Assert.AreEqual(1, Lines("fail", SeatChecks.PanelReopenedId));
        Assert.AreEqual(0, Lines("pass", SeatChecks.PanelReopenedId));
        Assert.IsFalse(_sut.Faulted, "a missing site is a check result, not a fault");
    }

    [TestMethod]
    public void KeepOpen_ThePanelThatWasOpenWhenTheSeatWasTaken_DoesNotMakeAnEdgeOnItsOwn()
    {
        _settings.KeepPanelOpen = true;
        _screen.PanelOpen = true;
        Seated();

        Frame();
        Frame();

        Assert.AreEqual(0, _screen.Reopens);
    }

    // A refused reopen is not a reopen: OpenToggleOrder returns silently when the hero or the last troop is gone.
    [TestMethod]
    public void Reopen_WhenTheEngineRefuses_WritesASkipAndNoPass()
    {
        OpenAndCloseWithKeepOn();
        _screen.EngineRefusesReopen = true;

        Frame();

        Assert.AreEqual(1, _screen.Reopens);
        Assert.AreEqual(0, Lines("pass", SeatChecks.PanelReopenedId));
        Assert.AreEqual(0, Lines("fail", SeatChecks.PanelReopenedId), "the site was reached, so this is not the missing-site failure");
        var skip = _log.Lines.Single(l => l.Result == "skip" && l.Id == SeatChecks.PanelReopenedId);
        Assert.AreEqual("7:refused", skip.Key);
        Assert.AreEqual("mission=7 reason=refused", skip.Detail);
        Assert.IsFalse(_sut.Faulted);
    }

    [TestMethod]
    public void Reopen_WhenTheEngineRefuses_DoesNotTryAgainOnTheNextFrames()
    {
        OpenAndCloseWithKeepOn();
        _screen.EngineRefusesReopen = true;

        Frame();
        Frame();
        Frame();

        Assert.AreEqual(1, _screen.Reopens);
        Assert.IsFalse(SeatPresence.CursorShown);
    }

    [TestMethod]
    public void Reopen_WhenItWorks_WritesThePassLineOnly()
    {
        OpenAndCloseWithKeepOn();

        Frame();

        Assert.AreEqual(1, Lines("pass", SeatChecks.PanelReopenedId));
        Assert.AreEqual(0, Lines("skip", SeatChecks.PanelReopenedId));
        Assert.IsTrue(_screen.PanelOpen);
    }

    [TestMethod]
    public void Cursor_OnAnEdgeThatReopens_IsNeverHidden()
    {
        OpenAndCloseWithKeepOn();
        _screen.CursorWrites.Clear();
        _screen.Calls.Clear();

        Frame();
        Frame();

        Assert.IsFalse(_screen.Calls.Contains("cursor:off"), "an order must not toggle the cursor: " + string.Join(",", _screen.Calls));
        Assert.AreEqual(0, _screen.CursorWrites.Count);
        Assert.IsTrue(SeatPresence.CursorShown);
    }

    [TestMethod]
    public void Cursor_OnAnEdgeWhoseReopenIsRefused_IsHiddenOnThatFrame()
    {
        OpenAndCloseWithKeepOn();
        _screen.EngineRefusesReopen = true;

        Frame();

        Assert.IsFalse(SeatPresence.CursorShown, "the panel stayed closed, so no cursor");
        Assert.AreEqual(false, _screen.CursorWrites.Last());
    }

    // The close signals are game keys that only the order handler registers on the scene layer, and a read of a key nobody registered
    // throws. A town, an indoor scene, a village, an arena practice, a duel and an alley build no such handler.
    [TestMethod]
    public void KeepOpen_WithThePanelNeverOpen_ReadsNoSignal()
    {
        _settings.KeepPanelOpen = true;
        Seated();

        Frame();
        Frame();
        Frame();

        Assert.AreEqual(0, _screen.SignalReads, "no panel, no handler, and a read there would throw and fault the Seat");
        Assert.IsFalse(_sut.Faulted);
    }

    [TestMethod]
    public void KeepOpen_ReadsTheSignalsWhileThePanelIsOpen_AndOnTheClosingEdge_ButNotAfterIt()
    {
        _settings.KeepPanelOpen = true;
        Seated();
        _screen.PanelOpen = true;
        Frame();
        Assert.AreEqual(1, _screen.SignalReads, "open");
        _screen.PanelOpen = false;
        _screen.EngineRefusesReopen = true;
        Frame();
        Assert.AreEqual(2, _screen.SignalReads, "the closing edge: the panel was open on the frame before");

        Frame();
        Frame();

        Assert.AreEqual(2, _screen.SignalReads, "closed on both frames");
    }

    // Transfer runs its order and then closes the panel, and OpenToggleOrder clears IsTransferActive, so a reopen cancels the transfer.
    [TestMethod]
    public void KeepOpen_AClosingEdgeWithTheTransferMenuOpen_DoesNotReopen()
    {
        OpenAndCloseWithKeepOn();
        _screen.TransferOpen = true;

        Frame();
        Frame();

        Assert.AreEqual(0, _screen.Reopens);
        Assert.AreEqual(0, _log.Lines.Count(l => l.Id == SeatChecks.PanelReopenedId));
    }

    [TestMethod]
    public void KeepOpen_TheTransferFlagIsReadOnAClosingEdgeOnly()
    {
        _settings.KeepPanelOpen = true;
        Seated();
        Frame();
        _screen.PanelOpen = true;
        Frame();
        Assert.AreEqual(0, _screen.TransferReads, "not on a frame with the panel open or never open");

        _screen.PanelOpen = false;
        _screen.EngineRefusesReopen = true;
        Frame();

        Assert.AreEqual(1, _screen.TransferReads);
    }

    [TestMethod]
    public void KeepOpen_AfterATransfer_ThePanelStaysClosedAndAnOrdinaryCloseReopensAgain()
    {
        OpenAndCloseWithKeepOn();
        _screen.TransferOpen = true;
        Frame();
        Assert.AreEqual(0, _screen.Reopens);

        _screen.TransferOpen = false;
        _screen.PanelOpen = true;
        Frame();
        _screen.PanelOpen = false;
        Frame();

        Assert.AreEqual(1, _screen.Reopens);
    }

    // ---- The shared cleanup ----

    [TestMethod]
    public void Fault_RunsEveryCleanupStepEvenWhenOneThrows()
    {
        Seated();
        _screen.PanelOpen = true;
        Frame();
        _sut.PressHide();
        _screen.KeepResult = SeatKeepKind.Soldier;
        _sut.PressKeep();
        _sut.OnMissionTick();
        _screen.CursorThrows = new InvalidOperationException("cursor");
        _screen.UiThrows = new InvalidOperationException("ui");
        _screen.ApplyThrows = new InvalidOperationException("frame");

        _sut.OnFrame(0.1f);

        Assert.IsTrue(_sut.Faulted);
        Assert.IsFalse(SeatPresence.IsSeated);
        Assert.IsFalse(SeatPresence.CursorShown);
        Assert.IsTrue(_screen.ControllerWrites.Last(), "the controller was given back");
        Assert.AreEqual(1, _screen.Unkeeps, "the kept target was dropped though the cursor and the interface steps threw");
        Assert.IsTrue(_log.Lines.Count(l => l.Result == "swallowed") >= 2, "each throw was counted, none escaped");
    }

    [TestMethod]
    public void Leave_ClearsTheStaticsThePatchesRead()
    {
        Seated();
        _screen.PanelOpen = true;
        Frame();
        Assert.IsTrue(SeatPresence.IsSeated && SeatPresence.CursorShown);

        _sut.Finish();

        Assert.IsFalse(SeatPresence.IsSeated);
        Assert.IsFalse(SeatPresence.CursorShown);
    }

    // ---- The mission tick tests each owed job on its own ----

    [TestMethod]
    public void OnMissionTick_AStoredKeepPress_RunsWithNoStandFastOwed()
    {
        Seated();
        _sut.OnMissionTick();
        var standFasts = _hero.StandFasts;

        _sut.PressKeep();
        _sut.OnMissionTick();

        Assert.AreEqual(1, _screen.KeepCalls);
        Assert.AreEqual(standFasts, _hero.StandFasts);
    }

    [TestMethod]
    public void OnMissionTick_AStandFastOwed_RunsWithNoKeepPress()
    {
        Seated();

        _sut.OnMissionTick();

        Assert.AreEqual(1, _hero.StandFasts);
        Assert.AreEqual(0, _screen.KeepCalls);
    }

    [TestMethod]
    public void OnMissionTick_BothOwed_DoBothInOneTick()
    {
        Seated();
        _sut.PressKeep();

        _sut.OnMissionTick();

        Assert.AreEqual(1, _hero.StandFasts);
        Assert.AreEqual(1, _screen.KeepCalls);
    }
}
