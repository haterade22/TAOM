using System;
using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using TAOM.Client.Features.SeatOfSeeing.Trial;
using TAOM.Features.SeatOfSeeing.Domain;

namespace TAOM.Tests.Features.SeatOfSeeing;

/// <summary>The field trial's eight command mouse steps against the scripted stand-in, and the verdict rules they decide by.</summary>
[TestClass]
public class CommandTrialMouseStepsTests
{
    private static readonly string[] Steps =
        { "select_click", "select_shift", "move_right_click", "drag_facing", "charge_enemy", "queue_with_mouse", "select_box", "transfer_ctrl_number" };

    private CommandTrialWorld _world = null!;
    private ListLogger _log = null!;

    [TestInitialize]
    public void Setup()
    {
        _world = new CommandTrialWorld();
        _log = new ListLogger();
    }

    private void Run(params string[] steps)
    {
        var list = "enter," + string.Join(",", steps);
        Assert.IsTrue(SeatTrialSteps.TryChoose(list, out var chosen, out var error), error);
        var script = new CommandTrialScript(_world, chosen, _log, _ => { });
        _world.Run(script, 150f);
        Assert.IsTrue(script.IsDone, "the run ended");
    }

    private string Line(string step) => _log.Lines.Single(l => l.StartsWith("[SeatTrial] step=" + step + " ", StringComparison.Ordinal));

    private void AssertPasses(string step) => StringAssert.Contains(Line(step), "result=PASS", Line(step));

    private void AssertFails(string step) => StringAssert.Contains(Line(step), "result=FAIL", Line(step));

    // ---- the rows ----

    [TestMethod]
    public void TheRows_FollowTheCameraSteps_BeforeTheKillSteps_AndEachNeedsTheEnterStep()
    {
        var names = SeatTrialSteps.All.Select(s => s.Name).ToList();

        CollectionAssert.AreEqual(Steps.Concat(new[] { "hero_falls" }).ToArray(), names.Skip(names.IndexOf("zoom_smooth") + 1).Take(9).ToArray());
        foreach (var name in Steps)
        {
            Assert.IsFalse(SeatTrialSteps.TryChoose(name, out _, out var error), name);
            StringAssert.Contains(error, "enter");
        }
    }

    // ---- select_click ----

    [TestMethod]
    public void SelectClick_SelectsTheClickedSoldiersCompanyAlone_AndGivesTheCommandMouseBack()
    {
        Run("select_click");

        AssertPasses("select_click");
        StringAssert.Contains(Line("select_click"), "only_chosen=1");
        StringAssert.Contains(Line("select_click"), "pixel=");
        Assert.IsFalse(_world.CommandLive, "the step turns the value off at its end");
        Assert.IsTrue(_world.MouseReleases >= 1);
    }

    [TestMethod]
    public void SelectClick_WhenTheClickSelectsNothing_Fails()
    {
        _world.SelectDoesNothing = true;

        Run("select_click");

        AssertFails("select_click");
        StringAssert.Contains(Line("select_click"), "only_chosen=0");
    }

    [TestMethod]
    public void SelectClick_WhenTheCommandMouseNeverGoesLive_Fails_AndSaysSo()
    {
        _world.CommandNeverLive = true;

        Run("select_click");

        AssertFails("select_click");
        StringAssert.Contains(Line("select_click"), "reason=not-live");
    }

    [TestMethod]
    public void SelectClick_WhenTheSoldierIsOffTheScreen_Fails_AndSaysSo()
    {
        _world.SoldierOffScreen = true;

        Run("select_click");

        AssertFails("select_click");
        StringAssert.Contains(Line("select_click"), "reason=off-screen");
    }

    [TestMethod]
    public void SelectClick_WithNoSoldier_Fails()
    {
        _world.SoldierAvailable = false;

        Run("select_click");

        AssertFails("select_click");
        StringAssert.Contains(Line("select_click"), "reason=no-soldier");
    }

    [TestMethod]
    public void MoveRightClick_WhenTheSessionFaultsMidStep_Fails()
    {
        _world.FaultMidStep = true;

        Run("move_right_click");

        AssertFails("move_right_click");
    }

    // ---- select_shift ----

    [TestMethod]
    public void SelectShift_AddsASecondCompany_AndATwiceShiftedOneComesOffAgain()
    {
        Run("select_shift");

        AssertPasses("select_shift");
        StringAssert.Contains(Line("select_shift"), "both_after_add=1");
        StringAssert.Contains(Line("select_shift"), "first_only_after_remove=1");
    }

    [TestMethod]
    public void SelectShift_WhenShiftActsAsAPlainClick_FailsOnTheAdd()
    {
        _world.ShiftActsAsPlain = true;

        Run("select_shift");

        AssertFails("select_shift");
        StringAssert.Contains(Line("select_shift"), "both_after_add=0");
    }

    [TestMethod]
    public void SelectShift_WithNoSecondCompany_Fails()
    {
        _world.NoSecondCompany = true;

        Run("select_shift");

        AssertFails("select_shift");
        StringAssert.Contains(Line("select_shift"), "reason=no-second-company");
    }

    // ---- move_right_click ----

    [TestMethod]
    public void MoveRightClick_SendsTheSelectedCompanyToTheClickedPoint()
    {
        Run("move_right_click");

        AssertPasses("move_right_click");
        StringAssert.Contains(Line("move_right_click"), "distance=0");
    }

    [TestMethod]
    public void MoveRightClick_WhenTheOrderLandsFiveMetresOff_Fails()
    {
        _world.MoveOffByMetres = 5f;

        Run("move_right_click");

        AssertFails("move_right_click");
    }

    [TestMethod]
    public void MoveRightClick_WhenTheClickGivesNoOrder_Fails()
    {
        _world.MoveIgnoresTheClick = true;

        Run("move_right_click");

        AssertFails("move_right_click");
        StringAssert.Contains(Line("move_right_click"), "distance=nan");
    }

    [TestMethod]
    public void MoveRightClick_WhenTheGroundIsOffTheScreen_Fails_AndSaysSo()
    {
        _world.GroundOffScreen = true;

        Run("move_right_click");

        AssertFails("move_right_click");
        StringAssert.Contains(Line("move_right_click"), "reason=no-ground");
    }

    // ---- drag_facing ----

    [TestMethod]
    public void DragFacing_TurnsTheCompanyToTheLinesNormal()
    {
        Run("drag_facing");

        AssertPasses("drag_facing");
        StringAssert.Contains(Line("drag_facing"), "error_deg=0");
    }

    [TestMethod]
    public void DragFacing_WhenTheDragIsOnlyAClick_FailsForWantOfAFacing()
    {
        _world.DragActsAsClick = true;

        Run("drag_facing");

        AssertFails("drag_facing");
    }

    [TestMethod]
    public void DragFacing_WhenTheCompanyFacesAlongTheLine_Fails()
    {
        _world.DragFacesAlongTheLine = true;

        Run("drag_facing");

        AssertFails("drag_facing");
        StringAssert.Contains(Line("drag_facing"), "error_deg=90");
    }

    // ---- charge_enemy ----

    [TestMethod]
    public void ChargeEnemy_GivesTheChargeAgainstTheClickedEnemyCompany()
    {
        Run("charge_enemy");

        AssertPasses("charge_enemy");
        StringAssert.Contains(Line("charge_enemy"), "charge_on_enemy=1");
    }

    [TestMethod]
    public void ChargeEnemy_WhenTheClickGivesNoCharge_Fails()
    {
        _world.ChargeGoesNowhere = true;

        Run("charge_enemy");

        AssertFails("charge_enemy");
        StringAssert.Contains(Line("charge_enemy"), "charge_on_enemy=0");
    }

    [TestMethod]
    public void ChargeEnemy_WithNoEnemy_Fails()
    {
        _world.NoEnemy = true;

        Run("charge_enemy");

        AssertFails("charge_enemy");
        StringAssert.Contains(Line("charge_enemy"), "reason=no-enemy");
    }

    // ---- queue_with_mouse ----

    [TestMethod]
    public void QueueWithMouse_TakesTheSecondShiftedClickIntoTheQueue_WithThePanelShut()
    {
        Run("queue_with_mouse");

        AssertPasses("queue_with_mouse");
        StringAssert.Contains(Line("queue_with_mouse"), "armed_at_order=1");
        StringAssert.Contains(Line("queue_with_mouse"), "panel=0");
        StringAssert.Contains(Line("queue_with_mouse"), "captured=1");
    }

    [TestMethod]
    public void QueueWithMouse_WhenTheKeyDoesNotArmWithTheCommandMouse_Fails()
    {
        _world.QueueKeyArms = false;

        Run("queue_with_mouse");

        AssertFails("queue_with_mouse");
    }

    [TestMethod]
    public void QueueWithMouse_WhenTheCaptureTakesNothing_Fails()
    {
        _world.QueueCaptures = false;

        Run("queue_with_mouse");

        AssertFails("queue_with_mouse");
        StringAssert.Contains(Line("queue_with_mouse"), "captured=0");
    }

    // ---- select_box ----

    [TestMethod]
    public void SelectBox_ADragRoundTheSoldier_ShowsTheRectangleHidesItAndSelectsTheCompany()
    {
        Run("select_box");

        AssertPasses("select_box");
        StringAssert.Contains(Line("select_box"), "first_selected=1");
        StringAssert.Contains(Line("select_box"), "box_shown=1");
        StringAssert.Contains(Line("select_box"), "box_hidden_after=1");
    }

    [TestMethod]
    public void SelectBox_WhenTheRectangleNeverShows_Fails()
    {
        _world.BoxNeverShown = true;

        Run("select_box");

        AssertFails("select_box");
        StringAssert.Contains(Line("select_box"), "box_shown=0");
    }

    [TestMethod]
    public void SelectBox_WhenTheRectangleStaysAfterTheRelease_Fails()
    {
        _world.BoxStaysShown = true;

        Run("select_box");

        AssertFails("select_box");
        StringAssert.Contains(Line("select_box"), "box_hidden_after=0");
    }

    [TestMethod]
    public void SelectBox_WhenTheBoxSelectsNothing_Fails()
    {
        _world.BoxSelectsNothing = true;

        Run("select_box");

        AssertFails("select_box");
        StringAssert.Contains(Line("select_box"), "first_selected=0");
    }

    [TestMethod]
    public void SelectBox_WithTheCommandMouseNeverLive_FailsOnThat()
    {
        _world.CommandNeverLive = true;

        Run("select_box");

        AssertFails("select_box");
        StringAssert.Contains(Line("select_box"), "reason=not-live");
    }

    // ---- transfer_ctrl_number ----

    [TestMethod]
    public void TransferCtrlNumber_MovesTheFirstCompanyIntoTheSecond_AndPutsThemBack()
    {
        Run("transfer_ctrl_number");

        AssertPasses("transfer_ctrl_number");
        StringAssert.Contains(Line("transfer_ctrl_number"), "first=60>0 second=40>100");
        Assert.AreEqual(60, _world.MouseFirstCount, "the soldiers were put back");
        Assert.AreEqual(40, _world.MouseSecondCount);
    }

    [TestMethod]
    public void TransferCtrlNumber_WhenTheHeroStaysInTheCompany_StillPasses()
    {
        _world.TransferLeftBehind = 1;

        Run("transfer_ctrl_number");

        AssertPasses("transfer_ctrl_number");
        Assert.AreEqual(60, _world.MouseFirstCount);
    }

    [TestMethod]
    public void TransferCtrlNumber_WhenNothingMoves_Fails()
    {
        _world.TransferDoesNothing = true;

        Run("transfer_ctrl_number");

        AssertFails("transfer_ctrl_number");
        StringAssert.Contains(Line("transfer_ctrl_number"), "first=60>60");
    }

    [TestMethod]
    public void TransferCtrlNumber_WhenSoldiersAreLostOnTheWay_Fails()
    {
        _world.TransferLosesSoldiers = true;

        Run("transfer_ctrl_number");

        AssertFails("transfer_ctrl_number");
    }

    [TestMethod]
    public void TransferCtrlNumber_WithOnlyOneCompany_FailsOnThat()
    {
        _world.NoSecondCompany = true;

        Run("transfer_ctrl_number");

        AssertFails("transfer_ctrl_number");
        StringAssert.Contains(Line("transfer_ctrl_number"), "reason=no-second-company");
    }

    [TestMethod]
    public void TheNewVerdicts_NeverPassOnABrokenMeasure()
    {
        Assert.IsTrue(SeatTrialMouse.BoxSelected(true, true, true, false));
        Assert.IsFalse(SeatTrialMouse.BoxSelected(true, true, true, true));
        Assert.IsTrue(SeatTrialMouse.Transferred(60, 0, 40, 100, false));
        Assert.IsTrue(SeatTrialMouse.Transferred(60, 1, 40, 99, false), "the hero may stay");
        Assert.IsFalse(SeatTrialMouse.Transferred(60, 2, 40, 98, false), "more than the hero stayed");
        Assert.IsFalse(SeatTrialMouse.Transferred(60, 60, 40, 40, false), "nothing moved");
        Assert.IsFalse(SeatTrialMouse.Transferred(0, 0, 40, 40, false), "no source soldiers");
        Assert.IsFalse(SeatTrialMouse.Transferred(60, 0, 40, 95, false), "soldiers lost");
        Assert.IsFalse(SeatTrialMouse.Transferred(60, 0, 40, 100, true), "a faulted Seat never passes");
    }

    // ---- the verdict rules ----

    [TestMethod]
    public void NormalError_IsZeroAlongTheNormal_AndNinetyAlongTheLine()
    {
        Assert.AreEqual(0f, SeatTrialMouse.NormalError(1f, 0f, 0f, 1f), 1e-3f);
        Assert.AreEqual(0f, SeatTrialMouse.NormalError(1f, 0f, 0f, -1f), 1e-3f, "either side of the line");
        Assert.AreEqual(90f, SeatTrialMouse.NormalError(1f, 0f, 1f, 0f), 1e-3f);
        Assert.AreEqual(45f, SeatTrialMouse.NormalError(1f, 0f, 1f, 1f), 1e-3f);
    }

    [TestMethod]
    public void NormalError_WithNoLineOrNoFacing_IsNotANumber()
    {
        Assert.IsTrue(float.IsNaN(SeatTrialMouse.NormalError(0f, 0f, 0f, 1f)));
        Assert.IsTrue(float.IsNaN(SeatTrialMouse.NormalError(1f, 0f, 0f, 0f)));
    }

    [TestMethod]
    public void TheVerdicts_NeverPassOnANotANumber()
    {
        Assert.IsFalse(SeatTrialMouse.Moved(float.NaN, false));
        Assert.IsFalse(SeatTrialMouse.FacedTheNormal(float.NaN, false));
        Assert.IsTrue(SeatTrialMouse.Moved(3f, false));
        Assert.IsFalse(SeatTrialMouse.Moved(3.1f, false));
        Assert.IsTrue(SeatTrialMouse.FacedTheNormal(15f, false));
        Assert.IsFalse(SeatTrialMouse.FacedTheNormal(15.5f, false));
        Assert.IsFalse(SeatTrialMouse.Moved(0f, true), "a faulted Seat never passes");
    }

    [TestMethod]
    public void Queued_NeedsTheKeyArmedAtTheOrderAndAStepBehindIt()
    {
        Assert.IsTrue(SeatTrialMouse.Queued(1, 1, true, false));
        Assert.IsFalse(SeatTrialMouse.Queued(1, 1, false, false));
        Assert.IsFalse(SeatTrialMouse.Queued(0, 1, true, false));
        Assert.IsFalse(SeatTrialMouse.Queued(1, 0, true, false));
    }
}
