using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using TAOM.Client.Features.SeatOfSeeing.Trial;
using TAOM.Features.SeatOfSeeing.Domain;

namespace TAOM.Tests.Features.SeatOfSeeing;

/// <summary>
/// The field trial's steps of stages S2 and S3 (build-spec section 12) against the stand-in: each step passes on a world that works, and
/// fails with its own facts when the one feature it proves is broken. The game is the only proof of the facts themselves.
/// </summary>
[TestClass]
public class CommandTrialSeatStepsTests
{
    private static readonly string[] StageTwo = { "cursor_with_panel", "focus_follows_cursor", "keep_panel_reopens", "hide_bars_restored" };
    private static readonly string[] StageThree = { "keep_to_soldier", "keep_to_company", "let_go_on_death" };

    private CommandTrialWorld _world = null!;
    private ListLogger _log = null!;
    private List<string> _finished = null!;

    [TestInitialize]
    public void Setup()
    {
        _world = new CommandTrialWorld();
        _log = new ListLogger();
        _finished = new List<string>();
    }

    private CommandTrialScript Run(params string[] steps)
    {
        var list = "enter," + string.Join(",", steps);
        Assert.IsTrue(SeatTrialSteps.TryChoose(list, out var chosen, out var error), error);
        var script = new CommandTrialScript(_world, chosen, _log, _finished.Add);
        _world.Run(script, 150f);
        Assert.IsTrue(script.IsDone, "the run ended");
        return script;
    }

    private string Line(string step) =>
        _log.Lines.Single(l => l.StartsWith("[SeatTrial] step=" + step + " ", StringComparison.Ordinal));

    private static string Fact(string line, string key)
    {
        var match = Regex.Match(line, @"\b" + key + @"=(\S+)");
        Assert.IsTrue(match.Success, key + " is not in: " + line);
        return match.Groups[1].Value;
    }

    private void AssertFails(string step, params string[] mentions)
    {
        var line = Line(step);
        StringAssert.Contains(line, "result=FAIL", line);
        foreach (var mention in mentions)
            StringAssert.Contains(line, mention, line);
    }

    // ---- The table ----

    [TestMethod]
    public void TheRows_AreInTheContractsOrder_AfterStageOne()
    {
        var names = SeatTrialSteps.All.Select(s => s.Name).ToList();

        CollectionAssert.AreEqual(StageTwo.Concat(StageThree).ToArray(), names.Skip(6).Take(7).ToArray());
    }

    [TestMethod]
    public void EachNewRow_NeedsTheEnterStep_AndASubsetWithoutItIsRefused()
    {
        foreach (var step in StageTwo.Concat(StageThree))
        {
            Assert.IsFalse(SeatTrialSteps.TryChoose(step, out _, out var error), step);
            StringAssert.Contains(error, "needs step 'enter'");
        }
    }

    [TestMethod]
    public void EveryNewRowHasAHandler_AndABudgetInRealSeconds()
    {
        foreach (var step in StageTwo.Concat(StageThree))
        {
            CollectionAssert.Contains(CommandTrialScript.HandlerNames.ToList(), step);
            Assert.IsTrue(SeatTrialSteps.All.Single(s => s.Name == step).BudgetSeconds >= 10f, step);
        }
    }

    // ---- A whole stage ----

    [TestMethod]
    public void StageTwo_PassesOnAWorldThatWorks_AndTakesItsTwoHidePictures()
    {
        Run(StageTwo);

        foreach (var step in StageTwo)
            StringAssert.Contains(Line(step), "result=PASS", Line(step));
        CollectionAssert.AreEqual(new[] { "final" }, _finished);
        CollectionAssert.IsSubsetOf(new[] { "hud_hide_bars_restored_hidden.bmp", "hud_hide_bars_restored_back.bmp" }, _world.Screenshots);
    }

    [TestMethod]
    public void StageThree_PassesOnAWorldThatWorks_AndTakesItsPicture()
    {
        Run(StageThree);

        foreach (var step in StageThree)
            StringAssert.Contains(Line(step), "result=PASS", Line(step));
        CollectionAssert.Contains(_world.Screenshots, "hud_keep_to_company.bmp");
    }

    [TestMethod]
    public void EveryStep_RunsEveryWriteInTheMissionTick_AfterItsMarker()
    {
        Run(StageTwo.Concat(StageThree).ToArray());

        Assert.IsTrue(_world.Writes.Count > 8 && _world.Kills.Count == 2, "the world saw the writes: " + _world.Writes.Count);
        CollectionAssert.AreEqual(Array.Empty<string>(), _world.WritesOutsideTheMissionTick.ToArray(),
            "an agent, formation or mission flag write ran in the screen tick");
    }

    [TestMethod]
    public void TheWholeRun_ReleasesTheHeldInput_AndLeavesNoPanelOpen()
    {
        Run(StageTwo.Concat(StageThree).ToArray());

        Assert.IsNull(_world.Input);
        Assert.IsFalse(_world.PanelOpen, "each step closes the panel it opened");
    }

    // ---- cursor_with_panel ----

    [TestMethod]
    public void CursorWithPanel_FactsNameThePanelAndTheCursorOnBothSides()
    {
        Run("cursor_with_panel");

        var line = Line("cursor_with_panel");
        Assert.AreEqual("0", Fact(line, "before"));
        Assert.AreEqual("1", Fact(line, "panel_open"));
        Assert.AreEqual("1", Fact(line, "cursor_open"));
        Assert.AreEqual("1", Fact(line, "panel_closed"));
        Assert.AreEqual("1", Fact(line, "cursor_closed"));
    }

    [TestMethod]
    public void CursorWithPanel_WhenTheCursorNeverShows_Fails()
    {
        _world.CursorWorks = false;

        Run("cursor_with_panel");

        AssertFails("cursor_with_panel", "cursor_open=0");
    }

    [TestMethod]
    public void CursorWithPanel_WhenThePanelCannotBeOpened_Fails()
    {
        _world.PanelSiteMissing = true;

        Run("cursor_with_panel");

        AssertFails("cursor_with_panel", "panel_open=0");
    }

    [TestMethod]
    public void CursorWithPanel_ClosesThePanelAsThePlayerDoes_SoKeepOpenDoesNotReopenIt()
    {
        _world.PlayerCloseReopens = false;

        Run("cursor_with_panel");

        Assert.AreEqual(1, _world.PlayerCloses);
        Assert.AreEqual(0, _world.PlainCloses);
    }

    // ---- focus_follows_cursor ----

    [TestMethod]
    public void FocusFollowsCursor_PassesWhenTheHandlersPointIsThePointerFunctionsValue()
    {
        Run("focus_follows_cursor");

        var line = Line("focus_follows_cursor");
        StringAssert.Contains(line, "result=PASS");
        StringAssert.Contains(line, "focused=none");
        Assert.AreEqual(Fact(line, "expected"), Fact(line, "field"));
    }

    [TestMethod]
    public void FocusFollowsCursor_WhenTheHandlerKeepsTheScreenCentre_Fails()
    {
        _world.FocusWorks = false;

        Run("focus_follows_cursor");

        AssertFails("focus_follows_cursor", "field=960.0,540.0");
    }

    [TestMethod]
    public void FocusFollowsCursor_WhenTheWindowHasNoFocus_FailsWithThatReason()
    {
        _world.WindowHasFocus = false;

        Run("focus_follows_cursor");

        AssertFails("focus_follows_cursor", "reason=no-window-focus");
    }

    [TestMethod]
    public void FocusFollowsCursor_WhenTheWindowLosesFocusDuringTheSettle_FailsOnTheWindowAlone()
    {
        _world.WindowLosesFocusAfterTheMove = true;

        Run("focus_follows_cursor");

        AssertFails("focus_follows_cursor", "window=0", "faulted=0", "seated=1");
        Assert.AreEqual(Fact(Line("focus_follows_cursor"), "expected"), Fact(Line("focus_follows_cursor"), "field"), "the focus itself followed");
    }

    [TestMethod]
    public void FocusFollowsCursor_WhenTheCursorDoesNotGoWhereAsked_Fails()
    {
        _world.CursorGoesWhereAsked = false;

        Run("focus_follows_cursor");

        AssertFails("focus_follows_cursor", "pixel=0.0,0.0");
    }

    [TestMethod]
    public void FocusFollowsCursor_WhenNoCursorShows_FailsBeforeMovingIt()
    {
        _world.CursorWorks = false;

        Run("focus_follows_cursor");

        AssertFails("focus_follows_cursor", "reason=no-cursor");
    }

    // ---- keep_panel_reopens ----

    [TestMethod]
    public void KeepPanelReopens_PassesWhenAPlainCloseReopensAndThePlayersCloseStays()
    {
        Run("keep_panel_reopens");

        var line = Line("keep_panel_reopens");
        StringAssert.Contains(line, "result=PASS");
        Assert.AreEqual("1", Fact(line, "stays_closed"));
        Assert.AreEqual(1, _world.PlainCloses);
        Assert.AreEqual(1, _world.PlayerCloses);
    }

    [TestMethod]
    public void KeepPanelReopens_WhenAPlainCloseStaysClosed_Fails()
    {
        _world.KeepOpenWorks = false;

        Run("keep_panel_reopens");

        AssertFails("keep_panel_reopens", "reopened=0");
    }

    [TestMethod]
    public void KeepPanelReopens_WhenThePlayersOwnCloseReopens_Fails()
    {
        _world.PlayerCloseReopens = true;

        Run("keep_panel_reopens");

        AssertFails("keep_panel_reopens", "stays_closed=0");
    }

    [TestMethod]
    public void KeepPanelReopens_StatesClosedAndFrames_AndTheSeatsOwnReopenIsOneFrame()
    {
        Run("keep_panel_reopens");

        var line = Line("keep_panel_reopens");
        Assert.AreEqual("1", Fact(line, "closed"));
        Assert.AreEqual("1", Fact(line, "frames"), "the session reopens on its next screen tick, and the step resumes after it");
    }

    [DataTestMethod]
    [DataRow(2, true)]
    [DataRow(3, true)]
    [DataRow(4, false)]
    [DataRow(30, false)]
    public void KeepPanelReopens_PassesAtThreeFramesAndFailsPastThem(int delay, bool passes)
    {
        _world.ReopenDelayFrames = delay;

        Run("keep_panel_reopens");

        if (passes)
            StringAssert.Contains(Line("keep_panel_reopens"), "result=PASS");
        else
            AssertFails("keep_panel_reopens", "reopened=");
        if (passes)
            Assert.AreEqual(delay.ToString(), Fact(Line("keep_panel_reopens"), "frames"));
    }

    [TestMethod]
    public void KeepPanelReopens_WhenTheCloseClosedNothing_FailsOnThatAlone()
    {
        _world.PlainCloseDoesNothing = true;

        Run("keep_panel_reopens");

        AssertFails("keep_panel_reopens", "closed=0", "frames=0");
    }

    [TestMethod]
    public void KeepPanelReopens_WhenThePanelCannotBeOpened_Fails()
    {
        _world.PanelSiteMissing = true;

        Run("keep_panel_reopens");

        AssertFails("keep_panel_reopens", "opened=0");
    }

    // ---- hide_bars_restored ----

    [TestMethod]
    public void HideBarsRestored_PassesWhenTheKeyHidesAndThePanelRestores()
    {
        Run("hide_bars_restored");

        var line = Line("hide_bars_restored");
        StringAssert.Contains(line, "result=PASS");
        Assert.AreEqual("0", Fact(line, "before"));
        Assert.AreEqual("1", Fact(line, "hidden"));
        Assert.AreEqual("1", Fact(line, "restored_on_panel"));
        Assert.AreEqual(1, _world.HidePresses);
    }

    [TestMethod]
    public void HideBarsRestored_WhenTheKeyHidesNothing_Fails()
    {
        _world.HideWorks = false;

        Run("hide_bars_restored");

        AssertFails("hide_bars_restored", "hidden=0");
    }

    [TestMethod]
    public void HideBarsRestored_WhenThePanelDoesNotRestoreIt_Fails()
    {
        _world.RestoreOnPanelWorks = false;

        Run("hide_bars_restored");

        AssertFails("hide_bars_restored", "restored_on_panel=0");
    }

    [TestMethod]
    public void HideBarsRestored_WhenTheInterfaceWasAlreadyHidden_FailsOnWhatItStartedFrom()
    {
        _world.UiStartsHidden = true;

        Run("hide_bars_restored");

        AssertFails("hide_bars_restored", "before=1");
    }

    [TestMethod]
    public void HideBarsRestored_TakesOnePictureHiddenAndOneBack_AfterTheEnterPicture()
    {
        Run("hide_bars_restored");

        var shots = _world.Screenshots.ToList();
        Assert.IsTrue(shots.IndexOf("hud_hide_bars_restored_hidden.bmp") > shots.IndexOf("hud_enter.bmp"));
        Assert.IsTrue(shots.IndexOf("hud_hide_bars_restored_back.bmp") > shots.IndexOf("hud_hide_bars_restored_hidden.bmp"));
    }

    // ---- keep_to_soldier ----

    [TestMethod]
    public void KeepToSoldier_AimsReleasesTheInput_KeepsAndLetsGo()
    {
        Run("keep_to_soldier");

        var line = Line("keep_to_soldier");
        StringAssert.Contains(line, "result=PASS");
        Assert.AreEqual("Soldier", Fact(line, "kind"));
        Assert.AreEqual("1", Fact(line, "chosen"));
        Assert.AreEqual("1", Fact(line, "letgo"));
        Assert.AreEqual(SeatKeepKind.None, _world.Keeping);
        Assert.IsNull(_world.Input);
    }

    [TestMethod]
    public void KeepToSoldier_WhenNobodyIsThere_Fails()
    {
        _world.SoldierAvailable = false;

        Run("keep_to_soldier");

        AssertFails("keep_to_soldier", "reason=no-soldier");
    }

    [TestMethod]
    public void KeepToSoldier_WhenTheViewNeverReachesHim_FailsAfterTheAimBudget()
    {
        _world.AimConverges = false;

        Run("keep_to_soldier");

        AssertFails("keep_to_soldier", "reason=never-aimed");
    }

    [TestMethod]
    public void KeepToSoldier_WhenThePressFindsNobody_FailsNamingTheMessage()
    {
        _world.KeepFindsNobody = true;

        Run("keep_to_soldier");

        AssertFails("keep_to_soldier", "reason=not-kept", "said=");
    }

    [TestMethod]
    public void KeepToSoldier_WhenTheKeptOneIsNotTheOneAimedAt_Fails()
    {
        _world.KeepsTheWrongOne = true;

        Run("keep_to_soldier");

        AssertFails("keep_to_soldier", "chosen=0");
    }

    [TestMethod]
    public void KeepToSoldier_WhenTheCameraStaysFar_Fails()
    {
        _world.KeptDistance = 40f;

        Run("keep_to_soldier");

        AssertFails("keep_to_soldier", "distance=40.0");
    }

    [TestMethod]
    public void KeepToSoldier_WhenTheCameraIsNearButLittleAbove_StillPasses()
    {
        _world.KeptDistance = 17.5f;

        Run("keep_to_soldier");

        StringAssert.Contains(Line("keep_to_soldier"), "result=PASS");
    }

    [TestMethod]
    public void KeepToSoldier_WhenTheKeyDoesNotLetGo_Fails()
    {
        _world.KeyLetsGo = false;

        Run("keep_to_soldier");

        AssertFails("keep_to_soldier", "letgo=0", "kind=Soldier", "chosen=1");
    }

    [TestMethod]
    public void KeepToSoldier_WhenTheKeepEndsBeforeTheOrbitIsJudged_FailsOnStillKept()
    {
        _world.LosesTheKeepAfterSeconds = 0.5f;

        Run("keep_to_soldier");

        AssertFails("keep_to_soldier", "still_kept=0");
    }

    [TestMethod]
    public void KeepToSoldier_WhenTheCameraCollapsedOntoTheTarget_Fails()
    {
        _world.KeptDistance = 8f;

        Run("keep_to_soldier");

        AssertFails("keep_to_soldier", "distance=8.0");
    }

    // ---- keep_to_company ----

    [TestMethod]
    public void KeepToCompany_WhenTheKeyDoesNotLetGo_Fails()
    {
        _world.KeyLetsGo = false;

        Run("keep_to_company");

        AssertFails("keep_to_company", "letgo=0", "kind=Company", "chosen=1");
    }

    [TestMethod]
    public void KeepToCompany_WhenTheKeepIsASoldierWhoseIdentityIsTheChosenOne_StillFailsOnTheKind()
    {
        _world.KeepsTheSoldierInstead = true;

        Run("keep_to_company");

        AssertFails("keep_to_company", "kind=Soldier", "chosen=1");
    }

    [TestMethod]
    public void KeepToCompany_WhenThePanelCannotBeOpened_FailsOnOpened()
    {
        _world.PanelSiteMissing = true;

        Run("keep_to_company");

        AssertFails("keep_to_company", "opened=0", "kind=Company");
    }

    [TestMethod]
    public void KeepToCompany_SelectsOneCompany_OpensThePanel_AndKeepsToIt()
    {
        Run("keep_to_company");

        var line = Line("keep_to_company");
        StringAssert.Contains(line, "result=PASS");
        Assert.AreEqual("Company", Fact(line, "kind"));
        Assert.AreEqual("1", Fact(line, "chosen"));
        Assert.AreEqual("30", Fact(line, "units"));
        CollectionAssert.Contains(_world.Writes, "select-company");
        var selectedAt = _world.Writes.IndexOf("select-company");
        Assert.IsTrue(_world.Writes.IndexOf("open-panel", selectedAt) > selectedAt, "the company is selected before the panel opens, so it is the only one");
    }

    [TestMethod]
    public void KeepToCompany_WhenTheKeepIsASoldierInstead_Fails()
    {
        // A panel that is not seen by the Seat keeps to the soldier under the pointer: the target is not the company.
        _world.KeepsTheWrongOne = true;

        Run("keep_to_company");

        AssertFails("keep_to_company", "chosen=0");
    }

    [TestMethod]
    public void KeepToCompany_WithNoCompany_Fails()
    {
        _world.NoCompany = true;

        Run("keep_to_company");

        AssertFails("keep_to_company", "reason=no-company");
    }

    [TestMethod]
    public void KeepToCompany_WhenTheViewStaysFar_Fails()
    {
        _world.KeptDistance = 60f;

        Run("keep_to_company");

        AssertFails("keep_to_company", "distance=60.0");
    }

    [TestMethod]
    public void KeepToCompany_WhenThePressFindsNobody_Fails()
    {
        _world.KeepFindsNobody = true;

        Run("keep_to_company");

        AssertFails("keep_to_company", "reason=not-kept");
    }

    // ---- let_go_on_death ----

    [TestMethod]
    public void LetGoOnDeath_KillsTheSoldier_ThenTheWholeSmallestCompany_AndEachLetsGoWithOneMessage()
    {
        Run("let_go_on_death");

        var line = Line("let_go_on_death");
        StringAssert.Contains(line, "result=PASS");
        Assert.AreEqual("1", Fact(line, "soldier_gone"));
        Assert.AreEqual("1", Fact(line, "soldier_messages"));
        Assert.AreEqual("12", Fact(line, "company_units"));
        Assert.AreEqual("1", Fact(line, "company_gone"));
        Assert.AreEqual("1", Fact(line, "company_messages"));
        CollectionAssert.AreEqual(new[] { "soldier", "company" }, _world.Kills);
    }

    [TestMethod]
    public void LetGoOnDeath_WhenTheSoldiersDeathKeepsTheTarget_Fails()
    {
        _world.DeathLetsGo = false;

        Run("let_go_on_death");

        AssertFails("let_go_on_death", "soldier_gone=0");
    }

    [TestMethod]
    public void LetGoOnDeath_WithNoCompanyToKill_FailsAfterTheSoldierHalf()
    {
        _world.NoCompany = true;

        Run("let_go_on_death");

        AssertFails("let_go_on_death", "reason=no-company-to-kill", "soldier_gone=1");
        CollectionAssert.AreEqual(new[] { "soldier" }, _world.Kills, "no company was killed");
    }

    [TestMethod]
    public void LetGoOnDeath_WhenNothingStartedKept_FailsBeforeKillingAnyone()
    {
        _world.KeepFindsNobody = true;

        Run("let_go_on_death");

        AssertFails("let_go_on_death", "reason=soldier-not-kept");
        Assert.AreEqual(0, _world.Kills.Count);
    }

    [TestMethod]
    public void LetGoOnDeath_WhenAnotherSoldierIsKept_FailsWithoutAKill()
    {
        _world.KeepsTheWrongOne = true;

        Run("let_go_on_death");

        AssertFails("let_go_on_death", "reason=another-soldier-kept");
        Assert.AreEqual(0, _world.Kills.Count, "the chosen soldier is not the kept one, so nobody is killed on a wrong premise");
    }

    [DataTestMethod]
    [DataRow(0)]
    [DataRow(2)]
    public void LetGoOnDeath_WhenTheSoldiersDeathSaysNothingOrTwice_FailsOnTheCount(int says)
    {
        _world.DeathSays = says;

        Run("let_go_on_death");

        AssertFails("let_go_on_death", "soldier_gone=1", "soldier_messages=" + says);
    }

    [TestMethod]
    public void LetGoOnDeath_WhenTheMessageIsTheWrongOne_FailsOnThatAlone()
    {
        _world.DeathSaysTheWrongThing = true;

        Run("let_go_on_death");

        AssertFails("let_go_on_death", "soldier_messages=1", "soldier_message=0");
    }

    [TestMethod]
    public void LetGoOnDeath_WhenTheCompanysDeathSaysTheWrongThing_FailsOnThatAlone()
    {
        _world.CompanyDeathSaysTheWrongThing = true;

        Run("let_go_on_death");

        AssertFails("let_go_on_death", "soldier_message=1", "company_messages=1", "company_message=0");
    }

    [TestMethod]
    public void LetGoOnDeath_WhenThePanelCannotBeOpened_FailsOnOpened()
    {
        _world.PanelSiteMissing = true;

        Run("let_go_on_death");

        AssertFails("let_go_on_death", "opened=0", "soldier_gone=1");
    }

    [TestMethod]
    public void LetGoOnDeath_WhenTheCompanyIsNotKept_FailsOnThat()
    {
        _world.CompanyKeepFindsNobody = true;

        Run("let_go_on_death");

        AssertFails("let_go_on_death", "company_kept=0", "soldier_gone=1");
    }

    [TestMethod]
    public void LetGoOnDeath_WhenTheCompanysDeathKeepsTheTarget_FailsOnCompanyGone()
    {
        _world.CompanyDeathLetsGo = false;

        Run("let_go_on_death");

        AssertFails("let_go_on_death", "company_kept=1", "company_gone=0", "soldier_gone=1");
    }

    [DataTestMethod]
    [DataRow(0)]
    [DataRow(2)]
    public void LetGoOnDeath_WhenTheCompanysDeathSaysNothingOrTwice_FailsOnTheCount(int says)
    {
        _world.CompanyDeathSays = says;

        Run("let_go_on_death");

        AssertFails("let_go_on_death", "company_gone=1", "company_messages=" + says, "soldier_messages=1");
    }

    // ---- The conjuncts every new step shares, and the cursor step's own ----

    [DataTestMethod]
    [DataRow("cursor_with_panel")]
    [DataRow("focus_follows_cursor")]
    [DataRow("keep_panel_reopens")]
    [DataRow("hide_bars_restored")]
    [DataRow("keep_to_soldier")]
    [DataRow("keep_to_company")]
    [DataRow("let_go_on_death")]
    public void EveryNewStep_WhenTheSeatFaultsMidStep_FailsOnFaulted(string step)
    {
        _world.FaultMidStep = true;

        Run(step);

        AssertFails(step, "faulted=1");
    }

    [TestMethod]
    public void CursorWithPanel_WhenTheCursorWasAlreadyShownBeforeThePanelOpened_Fails()
    {
        _world.CursorAlreadyShown = true;

        Run("cursor_with_panel");

        AssertFails("cursor_with_panel", "before=1");
    }

    [TestMethod]
    public void CursorWithPanel_WhenThePanelDoesNotClose_FailsOnPanelClosed()
    {
        _world.PanelStaysOpenOnClose = true;

        Run("cursor_with_panel");

        AssertFails("cursor_with_panel", "panel_closed=0");
    }

    [TestMethod]
    public void CursorWithPanel_WhenTheCursorStaysAfterTheCloseAlone_FailsOnCursorClosed()
    {
        _world.CursorStaysOnClose = true;

        Run("cursor_with_panel");

        AssertFails("cursor_with_panel", "panel_closed=1", "cursor_closed=0");
    }

    [TestMethod]
    public void CursorWithPanel_WhenTheSeatIsLeftDuringTheStep_FailsOnSeated()
    {
        _world.LeavesSeatAtTheEnd = true;

        Run("cursor_with_panel");

        AssertFails("cursor_with_panel", "panel_closed=1", "cursor_closed=1", "seated=0");
    }

    [DataTestMethod]
    [DataRow("cursor_with_panel")]
    [DataRow("focus_follows_cursor")]
    [DataRow("keep_panel_reopens")]
    [DataRow("hide_bars_restored")]
    [DataRow("keep_to_soldier")]
    [DataRow("keep_to_company")]
    [DataRow("let_go_on_death")]
    public void EveryNewStep_WhenTheSeatIsLeftDuringTheStep_FailsOnSeated(string step)
    {
        _world.LeavesSeatAtTheEnd = true;

        Run(step);

        AssertFails(step, "seated=0", "faulted=0");
    }

    // ---- A failed step leaves nothing held for the next one ----

    [TestMethod]
    public void AFailedStep_ReleasesTheLookInputItHeld()
    {
        _world.AimConverges = false;

        Run("keep_to_soldier");

        AssertFails("keep_to_soldier", "reason=never-aimed");
        var inputs = _world.InputLog;
        var lastAim = inputs.FindLastIndex(i => i.HasValue && i.Value.LookYaw != 0f);
        Assert.IsTrue(lastAim >= 0, "the aim held a look input");
        Assert.IsTrue(inputs.Skip(lastAim + 1).Any(i => i.HasValue && i.Value.LookYaw == 0f && i.Value.LookPitch == 0f),
            "the failed step released it before the next one: " + string.Join(",", inputs.Skip(lastAim).Select(i => i.HasValue ? i.Value.LookYaw.ToString() : "null")));
    }

    [TestMethod]
    public void AFailedStep_ClosesThePanelItOpened_SoTheNextStepFailsForItsOwnReason()
    {
        _world.KeepFindsNobody = true;

        Run("keep_to_company", "let_go_on_death");

        AssertFails("keep_to_company", "reason=not-kept", "opened=1");
        AssertFails("let_go_on_death", "reason=soldier-not-kept");
        StringAssert.Contains(Line("let_go_on_death"), "reason=soldier-not-kept");
        Assert.IsFalse(Line("let_go_on_death").Contains("panel-open"), "the panel of the step before is closed");
        Assert.IsFalse(_world.PanelOpen);
        CollectionAssert.AreEqual(new[] { "close-panel" }, _world.WritesOutsideTheMissionTick.ToArray(), "the release after a failure is the one write that runs in the screen tick");
    }

    [TestMethod]
    public void TheKillSteps_AreTheLastRows_SoAnEarlierRowNeverMeetsADeadBattle()
    {
        var names = SeatTrialSteps.All.Select(s => s.Name).ToList();

        // The rows that kill come after the rows that need a living army; the hero's fall and the take close the table (section 12).
        CollectionAssert.AreEqual(new[] { "hero_falls", "take_soldier" }, names.Skip(names.Count - 2).ToArray());
        Assert.IsTrue(names.IndexOf("let_go_on_death") < names.IndexOf("fight_on"), "it kills a company with no hero in it, and the hand-off steps need only the hero's company");
    }
}
