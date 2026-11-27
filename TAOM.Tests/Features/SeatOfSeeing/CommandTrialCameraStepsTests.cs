using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using TAOM.Client.Features.SeatOfSeeing.Trial;
using TAOM.Features.SeatOfSeeing.Domain;

namespace TAOM.Tests.Features.SeatOfSeeing;

/// <summary>The field trial's three camera feel steps (edge_pan, rotate_pivot, zoom_smooth) against the scripted stand-in, and the verdict rules they decide by.</summary>
[TestClass]
public class CommandTrialCameraStepsTests
{
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

    private static float Fact(string line, string key)
    {
        var match = Regex.Match(line, @"\b" + key + @"=(\S+)");
        Assert.IsTrue(match.Success, key + " is not in: " + line);
        return float.Parse(match.Groups[1].Value, System.Globalization.CultureInfo.InvariantCulture);
    }

    private void AssertFails(string step)
    {
        var line = Line(step);
        StringAssert.Contains(line, "result=FAIL", line);
    }

    [TestMethod]
    public void TheRows_FollowThePanelClickSteps_BeforeTheKillSteps_AndEachNeedsTheEnterStep()
    {
        var names = SeatTrialSteps.All.Select(s => s.Name).ToList();

        CollectionAssert.AreEqual(new[] { "edge_pan", "rotate_pivot", "zoom_smooth", "hero_falls" }, names.Skip(names.IndexOf("panel_click_order") + 1).Take(4).ToArray());
        foreach (var name in new[] { "edge_pan", "rotate_pivot", "zoom_smooth" })
        {
            Assert.IsFalse(SeatTrialSteps.TryChoose(name, out _, out var error), name);
            StringAssert.Contains(error, "enter");
        }
    }

    // ---- edge_pan ----

    [TestMethod]
    public void EdgePan_WithThePointerAtTheRightEdge_FliesTheViewToTheRight_AndGivesEverythingBack()
    {
        Run("edge_pan");

        var line = Line("edge_pan");
        StringAssert.Contains(line, "result=PASS", line);
        Assert.IsTrue(Fact(line, "right") >= SeatTrialCamera.EdgeMinMetres, line);
        Assert.IsTrue(Math.Abs(Fact(line, "forward")) < 0.5f, line);
        Assert.IsFalse(_world.EdgeScrollOn, "the trial's own edge scroll value goes back to off");
        Assert.IsFalse(_world.PanelOpen, "the step closes the panel it opened");
        Assert.IsTrue(_world.CameraReleases >= 1);
    }

    [TestMethod]
    public void EdgePan_WhenTheEdgeDoesNothing_Fails()
    {
        _world.EdgeDoesNothing = true;

        Run("edge_pan");

        AssertFails("edge_pan");
        Assert.AreEqual(0f, Fact(Line("edge_pan"), "right"), 0.01f);
    }

    [TestMethod]
    public void EdgePan_WhenTheViewFliesTheWrongWay_Fails()
    {
        _world.EdgeFliesLeft = true;

        Run("edge_pan");

        AssertFails("edge_pan");
        Assert.IsTrue(Fact(Line("edge_pan"), "right") < 0f);
    }

    [TestMethod]
    public void EdgePan_WhenThePanelCannotShowTheCursor_Fails_AndSaysSo()
    {
        _world.CursorWorks = false;

        Run("edge_pan");

        AssertFails("edge_pan");
        StringAssert.Contains(Line("edge_pan"), "cursor=0");
    }

    [TestMethod]
    public void EdgePan_WhenTheSessionFaultsMidStep_Fails()
    {
        _world.FaultMidStep = true;

        Run("edge_pan");

        AssertFails("edge_pan");
    }

    // ---- rotate_pivot ----

    [TestMethod]
    public void RotatePivot_SwingsTheCameraRoundThePoint_AndGivesTheButtonBack()
    {
        Run("rotate_pivot");

        var line = Line("rotate_pivot");
        StringAssert.Contains(line, "result=PASS", line);
        Assert.IsTrue(Fact(line, "swung") >= SeatTrialCamera.RotateMinSwingMetres, line);
        Assert.IsTrue(Fact(line, "drift_held") <= SeatTrialCamera.PivotDriftMetres, line);
        Assert.IsTrue(Fact(line, "drift_after") <= SeatTrialCamera.PivotDriftMetres, line);
    }

    [TestMethod]
    public void RotatePivot_WhenTheTurnIsInPlace_FailsOnTheDriftOfTheCentre()
    {
        _world.RotateInPlace = true;

        Run("rotate_pivot");

        AssertFails("rotate_pivot");
        Assert.IsTrue(Fact(Line("rotate_pivot"), "drift_held") > SeatTrialCamera.PivotDriftMetres);
    }

    [TestMethod]
    public void RotatePivot_WhenTheCentreDriftsWhileTurning_Fails()
    {
        _world.RotateCentreDrifts = true;

        Run("rotate_pivot");

        AssertFails("rotate_pivot");
    }

    [TestMethod]
    public void RotatePivot_WhenLettingGoJumpsTheView_FailsOnTheDriftAfter()
    {
        _world.RotateJumpsOnRelease = true;

        Run("rotate_pivot");

        var line = Line("rotate_pivot");
        AssertFails("rotate_pivot");
        Assert.IsTrue(Fact(line, "drift_held") <= SeatTrialCamera.PivotDriftMetres, "only the hand-back is wrong: " + line);
        Assert.IsTrue(Fact(line, "drift_after") > SeatTrialCamera.PivotDriftMetres, line);
    }

    [TestMethod]
    public void RotatePivot_WhenThereIsNoPivot_FailsWithItsReason()
    {
        _world.RotateHasNoPivot = true;

        Run("rotate_pivot");

        AssertFails("rotate_pivot");
        StringAssert.Contains(Line("rotate_pivot"), "reason=no-pivot");
    }

    // ---- zoom_smooth ----

    [TestMethod]
    public void ZoomSmooth_OneNotch_EasesTheHeightDownTo088_AndNeverRisesOnTheWay()
    {
        Run("zoom_smooth");

        var line = Line("zoom_smooth");
        StringAssert.Contains(line, "result=PASS", line);
        var start = Fact(line, "start");
        Assert.AreEqual(start * 0.88f, Fact(line, "end"), start * 0.03f, line);
        Assert.IsTrue(Fact(line, "first") > start * 0.88f * 1.02f, "the first frame covers only part of the way: " + line);
        Assert.IsTrue(Fact(line, "arrived") <= 0.5f, line);
    }

    [TestMethod]
    public void ZoomSmooth_WhenTheWheelDoesNothing_Fails()
    {
        _world.ZoomDoesNothing = true;

        Run("zoom_smooth");

        AssertFails("zoom_smooth");
    }

    [TestMethod]
    public void ZoomSmooth_WhenTheHeightJumpsAtOnce_FailsAsNotSmooth()
    {
        _world.ZoomJumps = true;

        Run("zoom_smooth");

        var line = Line("zoom_smooth");
        AssertFails("zoom_smooth");
        Assert.AreEqual(Fact(line, "target"), Fact(line, "first"), 0.01f, "the first read is already at the target: " + line);
    }

    [TestMethod]
    public void ZoomSmooth_WhenTheHeightBouncesBack_FailsOnTheRise()
    {
        _world.ZoomBounces = true;

        Run("zoom_smooth");

        var line = Line("zoom_smooth");
        AssertFails("zoom_smooth");
        Assert.IsTrue(Fact(line, "max_rise") > Fact(line, "start") * SeatTrialCamera.ZoomSlack, line);
    }

    [TestMethod]
    public void ZoomSmooth_WhenTheViewIsTooLowToHaveAStepDown_FailsWithItsReason()
    {
        _world.FlightHeight = 1.6f;

        Run("zoom_smooth");

        AssertFails("zoom_smooth");
        StringAssert.Contains(Line("zoom_smooth"), "reason=too-low");
    }

    [TestMethod]
    public void TheCameraSteps_AfterAFailure_GiveBackThePointerAndTheEdgeScroll()
    {
        _world.EdgeDoesNothing = true;

        Run("edge_pan", "rotate_pivot");

        Assert.IsFalse(_world.EdgeScrollOn);
        Assert.IsTrue(_world.CameraReleases >= 2, "the step's own release and the failure's");
    }

    // ---- the rules ----

    [TestMethod]
    public void EdgePanned_NeedsTheRightDistance_TheCursor_AndLittleForwardDrift()
    {
        Assert.IsTrue(SeatTrialCamera.EdgePanned(10f, 1f, true, false));
        Assert.IsFalse(SeatTrialCamera.EdgePanned(1.9f, 0f, true, false), "short");
        Assert.IsFalse(SeatTrialCamera.EdgePanned(10f, 6f, true, false), "it went forward as far as right");
        Assert.IsFalse(SeatTrialCamera.EdgePanned(10f, 0f, false, false), "no cursor, so no edge scroll was due");
        Assert.IsFalse(SeatTrialCamera.EdgePanned(10f, 0f, true, true), "a fault");
        Assert.IsFalse(SeatTrialCamera.EdgePanned(float.NaN, 0f, true, false));
        Assert.IsFalse(SeatTrialCamera.EdgePanned(10f, float.NaN, true, false));
    }

    [TestMethod]
    public void PivotHeld_NeedsASwing_AndTheCentreWithinTwoMetresWhileHeldAndAfter()
    {
        Assert.IsTrue(SeatTrialCamera.PivotHeld(0.5f, 8f, 0.5f, false));
        Assert.IsFalse(SeatTrialCamera.PivotHeld(0.5f, 0.5f, 0.5f, false), "the camera did not swing");
        Assert.IsFalse(SeatTrialCamera.PivotHeld(2.5f, 8f, 0.5f, false));
        Assert.IsFalse(SeatTrialCamera.PivotHeld(0.5f, 8f, 2.5f, false));
        Assert.IsFalse(SeatTrialCamera.PivotHeld(0.5f, 8f, 0.5f, true));
        Assert.IsFalse(SeatTrialCamera.PivotHeld(float.NaN, 8f, 0.5f, false));
        Assert.IsFalse(SeatTrialCamera.PivotHeld(0.5f, float.NaN, 0.5f, false));
        Assert.IsFalse(SeatTrialCamera.PivotHeld(0.5f, 8f, float.NaN, false));
    }

    [TestMethod]
    public void ZoomEased_NeedsAPartwayFirstFrame_NoRise_ArrivalInHalfASecond_AndTheTargetAtTheEnd()
    {
        Assert.IsTrue(SeatTrialCamera.ZoomEased(20f, 19.5f, 0f, 0.3f, 17.6f, false));
        Assert.IsFalse(SeatTrialCamera.ZoomEased(20f, 17.6f, 0f, 0.0f, 17.6f, false), "a jump is not smooth");
        Assert.IsFalse(SeatTrialCamera.ZoomEased(20f, 20f, 0f, 0.3f, 17.6f, false), "nothing moved on the first frame");
        Assert.IsFalse(SeatTrialCamera.ZoomEased(20f, 19.5f, 0.5f, 0.3f, 17.6f, false), "it rose");
        Assert.IsFalse(SeatTrialCamera.ZoomEased(20f, 19.5f, 0f, 0.6f, 17.6f, false), "too slow");
        Assert.IsFalse(SeatTrialCamera.ZoomEased(20f, 19.5f, 0f, float.PositiveInfinity, 17.6f, false), "never arrived");
        Assert.IsFalse(SeatTrialCamera.ZoomEased(20f, 19.5f, 0f, 0.3f, 16.0f, false), "ended away from the target");
        Assert.IsFalse(SeatTrialCamera.ZoomEased(20f, 19.5f, 0f, 0.3f, 17.6f, true), "a fault");
        Assert.IsFalse(SeatTrialCamera.ZoomEased(float.NaN, 19.5f, 0f, 0.3f, 17.6f, false));
        Assert.IsFalse(SeatTrialCamera.ZoomEased(20f, float.NaN, 0f, 0.3f, 17.6f, false));
    }
}
