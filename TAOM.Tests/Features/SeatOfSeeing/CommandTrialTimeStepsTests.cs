using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using TAOM.Client.Features.SeatOfSeeing.Trial;
using TAOM.Features.SeatOfSeeing.Domain;

namespace TAOM.Tests.Features.SeatOfSeeing;

/// <summary>
/// The field trial's steps of stages S6 and S7 (build-spec section 12) against the stand-in, whose clock does what the engine does. Each step passes on a
/// world that works and fails with its own fact when the one feature it proves is broken. Every knob starts at the working value and a test turns one.
/// The game is the only proof of the facts themselves.
/// </summary>
[TestClass]
public class CommandTrialTimeStepsTests
{
    private static readonly string[] StageSix = { "pause_flies", "slow", "fast", "speed_cleared" };
    private const string AdoptStep = "adopt_flag";
    private static readonly string[] StageSeven = { "rise_with_panel", "sink_on_close" };

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

    private CommandTrialScript Run(float frameSeconds, params string[] steps) => RunUneven(new[] { frameSeconds }, steps);

    private CommandTrialScript Run(params string[] steps) => Run(1f / 60f, steps);

    private CommandTrialScript RunUneven(float[] frameCycle, params string[] steps)
    {
        Assert.IsTrue(SeatTrialSteps.TryChoose("enter," + string.Join(",", steps), out var chosen, out var error), error);
        var script = new CommandTrialScript(_world, chosen, _log, _finished.Add);
        _world.Run(script, 300f, frameCycle);
        Assert.IsTrue(script.IsDone, "the run ended");
        return script;
    }

    private string Line(string step) => _log.Lines.Single(l => l.StartsWith("[SeatTrial] step=" + step + " ", StringComparison.Ordinal));

    private static string Fact(string line, string key)
    {
        var match = Regex.Match(line, @"\b" + key + @"=(\S+)");
        Assert.IsTrue(match.Success, key + " is not in: " + line);
        return match.Groups[1].Value;
    }

    private static float Number(string line, string key) => float.Parse(Fact(line, key), System.Globalization.CultureInfo.InvariantCulture);

    private void AssertFails(string step, string fact, string value)
    {
        var line = Line(step);
        StringAssert.Contains(line, "result=FAIL", line);
        Assert.AreEqual(value, Fact(line, fact), line);
    }

    private void AssertPasses(string step) => StringAssert.Contains(Line(step), "result=PASS", Line(step));

    [TestMethod]
    public void TheTrialsSettings_ForceTheLiftOn_ThatDefaultsOffForAPlayer()
    {
        var settings = new CommandTrialSettings();

        Assert.IsTrue(settings.RiseWithPanel, "rise_with_panel cannot pass without it, whatever the player saved");
        Assert.IsTrue(settings.Enabled, "and the time keys act only with the Seat on");
    }

    // ---- The table ----

    [TestMethod]
    public void TheRows_AreInTheContractsOrder_AfterStageFour_AndBeforeTheKillSteps()
    {
        var names = SeatTrialSteps.All.Select(s => s.Name).ToList();

        CollectionAssert.AreEqual(new[] { "pause_flies", "slow", "fast", AdoptStep, "speed_cleared", "rise_with_panel", "sink_on_close" }, names.Skip(16).Take(7).ToArray());
        Assert.AreEqual("back_to_stand", names[15], "the stage rows follow the hand-off steps, which need a living battle");
        CollectionAssert.AreEqual(new[] { "hero_falls", "take_soldier" }, names.Skip(23).Take(2).ToArray(), "the kill steps still close the Seat's rows");
    }

    [TestMethod]
    public void EachRow_NamesWhatItNeeds_AndASubsetWithoutItIsRefused()
    {
        var needs = new Dictionary<string, string>
        {
            ["pause_flies"] = "enter", ["slow"] = "enter", ["fast"] = "enter", [AdoptStep] = "enter", ["speed_cleared"] = "enter", ["rise_with_panel"] = "enter", ["sink_on_close"] = "rise_with_panel",
        };

        foreach (var pair in needs)
        {
            CollectionAssert.AreEqual(new[] { pair.Value }, SeatTrialSteps.All.Single(s => s.Name == pair.Key).Requires.ToArray(), pair.Key);
            var list = pair.Value == "enter" ? pair.Key : "enter," + pair.Key;
            Assert.IsFalse(SeatTrialSteps.TryChoose(list, out _, out var error), pair.Key);
            StringAssert.Contains(error, "needs step '" + pair.Value + "'");
        }
    }

    [DataTestMethod]
    [DataRow("enter,pause_flies,slow,fast,speed_cleared")]
    [DataRow("enter,adopt_flag")]
    [DataRow("enter,rise_with_panel,sink_on_close")]
    public void TheSubsetsTheFeatureDocNamesForStagesSixAndSeven_AreAccepted(string subset)
    {
        Assert.IsTrue(SeatTrialSteps.TryChoose(subset, out var chosen, out var error), error);
        CollectionAssert.AreEqual(subset.Split(','), chosen.Select(s => s.Name).ToArray());
    }

    [TestMethod]
    public void EveryRow_HasAHandler_AndABudgetInRealSeconds()
    {
        foreach (var step in StageSix.Concat(StageSeven).Append(AdoptStep))
        {
            CollectionAssert.Contains(CommandTrialScript.HandlerNames.ToList(), step);
            Assert.IsTrue(SeatTrialSteps.All.Single(s => s.Name == step).BudgetSeconds >= 10f, step);
        }
    }

    // ---- Whole stages ----

    [TestMethod]
    public void StageSix_OnAWorkingWorld_PassesEveryStep_AndLeavesTimeAtOneUnpaused()
    {
        Run(StageSix);

        foreach (var step in StageSix)
            AssertPasses(step);
        Assert.AreEqual(1f, _world.TimeLevel);
        Assert.IsTrue(_world.ReadTimeState().IsClean(otherClock: false));
        Assert.AreEqual("final", _finished.Single());
    }

    [TestMethod]
    public void StageSeven_OnAWorkingWorld_PassesBothSteps_AndTheOffsetEndsAtExactlyZero()
    {
        Run(StageSeven);

        foreach (var step in StageSeven)
            AssertPasses(step);
        Assert.AreEqual(0f, _world.CameraTargetOffset().Z);
        Assert.AreEqual("Off", Fact(Line("rise_with_panel"), "state"), "the steps pass only with the state Off");
        Assert.AreEqual("Off", Fact(Line("sink_on_close"), "state"));
    }

    // ---- pause_flies ----

    [TestMethod]
    public void PauseFlies_OnAWorkingWorld_ReadsAStandingClockAndAFlyingView()
    {
        Run("pause_flies");

        var line = Line("pause_flies");
        Assert.AreEqual("1", Fact(line, "paused"), line);
        Assert.AreEqual(0f, Number(line, "mission_seconds"), 0.05f);
        Assert.AreEqual(Number(line, "expected"), Number(line, "moved"), 0.25f * Number(line, "expected"), line);
        Assert.AreEqual("1", Fact(line, "resumed"), line);
    }

    [TestMethod]
    public void PauseFlies_WhenTheClockIgnoresThePause_FailsOnTheMissionSeconds()
    {
        _world.PauseIgnoredByTheClock = true;

        Run("pause_flies");

        var line = Line("pause_flies");
        StringAssert.Contains(line, "result=FAIL", line);
        Assert.IsTrue(Number(line, "mission_seconds") > 1f, "a mission clock that ran under the pause reads as moved: " + line);
        Assert.AreEqual("1", Fact(line, "paused"), "only the clock fact broke: " + line);
    }

    [TestMethod]
    public void PauseFlies_WhenTheViewStopsUnderThePause_FailsOnTheDistance()
    {
        _world.ViewFrozen = true;

        Run("pause_flies");

        var line = Line("pause_flies");
        StringAssert.Contains(line, "result=FAIL", line);
        Assert.AreEqual(0f, Number(line, "moved"), 0.01f, line);
        Assert.AreEqual("1", Fact(line, "paused"), line);
    }

    [TestMethod]
    public void PauseFlies_WhenTheKeyIsDead_FailsOnPaused()
    {
        _world.TimeKeysDead = true;

        Run("pause_flies");

        AssertFails("pause_flies", "paused", "0");
    }

    [TestMethod]
    public void PauseFlies_WhenTheSecondPressDoesNotResume_FailsOnResumed()
    {
        _world.ResumeDoesNothing = true;

        Run("pause_flies");

        AssertFails("pause_flies", "resumed", "0");
    }

    [TestMethod]
    public void PauseFlies_WhenTheSeatIsLeftByTheKey_FailsOnSeated()
    {
        _world.TimeKeyLeavesTheSeat = true;

        Run("pause_flies");

        var line = Line("pause_flies");
        StringAssert.Contains(line, "result=FAIL", line);
        Assert.AreEqual("0", Fact(line, "seated"), line);
    }

    [TestMethod]
    public void PauseFlies_WhenTheSessionFaults_FailsOnFaulted()
    {
        _world.TimeKeyFaults = true;

        Run("pause_flies");

        var line = Line("pause_flies");
        StringAssert.Contains(line, "result=FAIL", line);
        Assert.AreEqual("1", Fact(line, "faulted"), line);
    }

    // ---- slow ----

    [TestMethod]
    public void Slow_OnAWorkingWorld_ReadsHalfAndTheRequest()
    {
        Run("slow");

        var line = Line("slow");
        AssertPasses("slow");
        Assert.AreEqual(0.5f, Number(line, "ratio"), 0.1f, line);
        Assert.AreEqual("0.5", Fact(line, "level"));
        Assert.AreEqual("1", Fact(line, "request"));
    }

    [TestMethod]
    public void Slow_WhenTheClockIgnoresTheRequest_FailsOnTheRatio()
    {
        _world.SlowIgnoredByTheClock = true;

        Run("slow");

        var line = Line("slow");
        StringAssert.Contains(line, "result=FAIL", line);
        Assert.AreEqual(1f, Number(line, "ratio"), 0.1f, line);
        Assert.AreEqual("1", Fact(line, "request"), "only the clock broke: " + line);
    }

    [TestMethod]
    public void Slow_WhenAnEngineRequestIsHeldFromTheStart_FailsAtOnceWithItsReason()
    {
        _world.OtherRequestHeld = true;

        Run("slow");

        var line = Line("slow");
        StringAssert.Contains(line, "result=FAIL reason=other-request", line);
    }

    [TestMethod]
    public void Slow_WhenAnEngineRequestAppearsAfterTheSettle_FailsOnOthers()
    {
        _world.OtherRequestAppearsAfterTheSettle = true;

        Run("slow");

        AssertFails("slow", "others", "1");
    }

    [TestMethod]
    public void Slow_WhenTheRequestIsNotReported_FailsOnRequest()
    {
        _world.ReportsNoRequest = true;

        Run("slow");

        AssertFails("slow", "request", "0");
    }

    [TestMethod]
    public void Slow_WhenTheFixedStepStaysOn_FailsOnFixedStep()
    {
        _world.FixedStepStaysOnAtSlow = true;

        Run("slow");

        AssertFails("slow", "fixed_step", "1");
    }

    [TestMethod]
    public void Slow_WhenFastForwardStaysOn_FailsOnFastForward()
    {
        _world.FastForwardStaysOnAtSlow = true;

        Run("slow");

        AssertFails("slow", "fast_forward", "1");
    }

    [TestMethod]
    public void Slow_WhenThePauseStaysOn_FailsOnPaused()
    {
        _world.PausedFlagStaysOnAtSlow = true;

        Run("slow");

        AssertFails("slow", "paused", "1");
    }

    [TestMethod]
    public void Slow_WhenTheKeyIsDead_FailsOnTheLevel()
    {
        _world.TimeKeysDead = true;

        Run("slow");

        AssertFails("slow", "level", "1");
    }

    // ---- fast ----

    [TestMethod]
    public void Fast_OnAWorkingWorld_ReadsTwoFourAndAboveFive()
    {
        Run("fast");

        var line = Line("fast");
        AssertPasses("fast");
        Assert.AreEqual(2f, Number(line, "ratio2"), 0.3f, line);
        Assert.AreEqual(4f, Number(line, "ratio4"), 0.6f, line);
        Assert.IsTrue(Number(line, "ratio10") > 5f, line);
    }

    [TestMethod]
    public void Fast_AtAFrameTimeThatCapsTheStep_ExpectsTheCappedRatio()
    {
        Run(0.05f, "fast");

        AssertPasses("fast");
        Assert.AreEqual(2f, Number(Line("fast"), "ratio4"), 0.3f);
    }

    [TestMethod]
    public void Fast_OnAWorkingWorld_ReadsTheFixedStepAtTwoAndFour_AndFastForwardAtTen()
    {
        Run("fast");

        var line = Line("fast");
        Assert.AreEqual("1", Fact(line, "fixed_step2"), line);
        Assert.AreEqual("1", Fact(line, "fixed_step4"), line);
        Assert.AreEqual("0", Fact(line, "fixed_step10"), "a fixed step left on under the fast forward would carry the step into it: " + line);
        Assert.AreEqual("0", Fact(line, "fast_forward2"), line);
        Assert.AreEqual("0", Fact(line, "fast_forward4"), line);
        Assert.AreEqual("1", Fact(line, "fast_forward10"), line);
        Assert.AreEqual(2f, Number(line, "expected2"), 0.05f, line);
        Assert.AreEqual(4f, Number(line, "expected4"), 0.05f, line);
        Assert.AreEqual(1000f / 60f, Number(line, "frame2_ms"), 0.1f, line);
        Assert.AreEqual(1000f / 60f, Number(line, "frame4_ms"), 0.1f, line);
        StringAssert.DoesNotMatch(line, new Regex(@"\b(expected10|frame10_ms)="), "a level with no fixed step expects none and prints none: " + line);
    }

    [TestMethod]
    public void Fast_OnUnevenFramesThatAWorkingBuildGets_ExpectsTheCapAppliedToEachFrame()
    {
        RunUneven(new[] { 0.005f, 0.045f }, "fast");

        var line = Line("fast");
        AssertPasses("fast");
        Assert.AreEqual(2.4f, Number(line, "ratio4"), 0.2f, line);
        Assert.AreEqual(2.4f, Number(line, "expected4"), 0.1f, line);
    }

    [TestMethod]
    public void Fast_AtFramesSoLongTheLevelCannotBeTold_FailsWithItsOwnReason()
    {
        Run(0.1f, "fast");

        var line = Line("fast");
        StringAssert.Contains(line, "result=FAIL reason=level-indistinguishable", line);
        Assert.AreEqual("2", Fact(line, "level"), line);
        Assert.AreEqual(1f, Number(line, "expected"), 0.05f, line);
        Assert.AreEqual(1f, _world.TimeLevel, "the failure leaves level 2 held, and the driver's release puts the next step back at one");
        Assert.IsTrue(_world.ReadTimeState().IsClean(otherClock: false), "and leaves nothing of the Seat's on");
    }

    [TestMethod]
    public void Fast_AtFramesSoLongTheLevelCannotBeTold_FailsWhetherOrNotTheClockTakesTheStep()
    {
        _world.FastIgnoredByTheClock = true;

        Run(0.1f, "fast");

        StringAssert.Contains(Line("fast"), "result=FAIL reason=level-indistinguishable", Line("fast"));
    }

    [DataTestMethod]
    [DataRow(2)]
    [DataRow(4)]
    public void Fast_WhenTheFixedStepReadsOffAtALevel_FailsOnThatLevelsFact(int level)
    {
        _world.FixedStepReportedOffAtLevel = level;

        Run("fast");

        var line = Line("fast");
        StringAssert.Contains(line, "result=FAIL", line);
        Assert.AreEqual("0", Fact(line, "fixed_step" + level), line);
        Assert.AreEqual(level, Number(line, "ratio" + level), 0.6f, "only the state fact broke: " + line);
    }

    [DataTestMethod]
    [DataRow(2)]
    [DataRow(4)]
    public void Fast_WhenFastForwardReadsOnAtAFixedStepLevel_FailsOnThatLevelsFact(int level)
    {
        _world.FastForwardReportedOnAtLevel = level;

        Run("fast");

        var line = Line("fast");
        StringAssert.Contains(line, "result=FAIL", line);
        Assert.AreEqual("1", Fact(line, "fast_forward" + level), line);
        Assert.AreEqual(level, Number(line, "ratio" + level), 0.6f, "only the state fact broke: " + line);
        Assert.AreEqual("1", Fact(line, "fixed_step" + level), "and the other state facts read right: " + line);
    }

    [TestMethod]
    public void Fast_WhenAFixedStepStaysOnAtTen_FailsOnIt()
    {
        _world.FixedStepStaysOnAtTen = true;

        Run("fast");

        var line = Line("fast");
        StringAssert.Contains(line, "result=FAIL", line);
        Assert.AreEqual("1", Fact(line, "fixed_step10"), line);
        Assert.IsTrue(Number(line, "ratio10") > 5f, "the ratio alone cannot tell: " + line);
    }

    [TestMethod]
    public void Fast_WhenFastForwardReadsOffAtTen_FailsOnIt()
    {
        _world.FastForwardReportedOff = true;

        Run("fast");

        var line = Line("fast");
        StringAssert.Contains(line, "result=FAIL", line);
        Assert.AreEqual("0", Fact(line, "fast_forward10"), line);
    }

    [TestMethod]
    public void Fast_WhenTheStepIgnoresItsCap_FailsAtTheFrameTimeThatCapsIt()
    {
        _world.FixedStepIgnoresTheCap = true;

        Run(0.05f, "fast");

        var line = Line("fast");
        StringAssert.Contains(line, "result=FAIL", line);
        Assert.AreEqual(4f, Number(line, "ratio4"), 0.5f, line);
    }

    [TestMethod]
    public void Fast_WhenTheClockIgnoresTheFixedStep_FailsOnTheRatio()
    {
        _world.FastIgnoredByTheClock = true;

        Run("fast");

        var line = Line("fast");
        StringAssert.Contains(line, "result=FAIL", line);
        Assert.AreEqual(1f, Number(line, "ratio2"), 0.1f, line);
    }

    [TestMethod]
    public void Fast_WhenFastForwardIsOnlyThreeTimes_FailsAtTen()
    {
        _world.FastForwardRate = 3f;

        Run("fast");

        var line = Line("fast");
        StringAssert.Contains(line, "result=FAIL", line);
        Assert.AreEqual(3f, Number(line, "ratio10"), 0.5f, line);
        Assert.AreEqual(2f, Number(line, "ratio2"), 0.3f, "the lower levels still read right: " + line);
    }

    [TestMethod]
    public void Fast_WhenAnEngineRequestIsHeld_FailsAtOnceWithItsReason()
    {
        _world.OtherRequestHeld = true;

        Run("fast");

        StringAssert.Contains(Line("fast"), "reason=other-request", Line("fast"));
    }

    // ---- adopt_flag ----

    [TestMethod]
    public void AdoptFlag_OnAWorkingWorld_ReadsNoStepNoRequestTheirFlagKeptAndTenTimes_AndLevelOneWhenItFalls()
    {
        Run(AdoptStep);

        var line = Line(AdoptStep);
        AssertPasses(AdoptStep);
        foreach (var round in new[] { "four", "half" })
        {
            Assert.AreEqual("0", Fact(line, "fixed_step_" + round), line);
            Assert.AreEqual("0", Fact(line, "request_" + round), line);
            Assert.AreEqual("1", Fact(line, "flag_" + round), "their flag is not the Seat's to clear: " + line);
            Assert.AreEqual(10f, Number(line, "ratio_" + round), 1f, line);
            Assert.AreEqual("1", Fact(line, "level_after_" + round), line);
        }

        Assert.AreEqual(1f, _world.TimeLevel);
        Assert.IsTrue(_world.ReadTimeState().IsClean(otherClock: false));
    }

    [DataTestMethod]
    [DataRow(1f / 60f)]
    [DataRow(0.05f)]
    public void AdoptFlag_AtAnyFrameTime_ReadsTenWithNoFixedStepToCapIt(float frameSeconds)
    {
        Run(frameSeconds, AdoptStep);

        AssertPasses(AdoptStep);
        Assert.AreEqual(10f, Number(Line(AdoptStep), "ratio_four"), 1f, Line(AdoptStep));
    }

    [TestMethod]
    public void AdoptFlag_WritesTheirFlagFourTimes_AllAfterTheMissionTickMarker()
    {
        Run(AdoptStep);

        Assert.AreEqual(4, _world.Writes.Count(w => w == "other-fast-forward"), "a raise and a lowering for each of the two levels");
        CollectionAssert.DoesNotContain(_world.WritesOutsideTheMissionTick, "other-fast-forward");
    }

    [TestMethod]
    public void AdoptFlag_WhenTheSeatKeepsItsStepAndItsRequestUnderTheirFlag_FailsOnBothAndOnTheClock()
    {
        _world.Adoption = AdoptionFault.Ignored;

        Run(AdoptStep);

        var line = Line(AdoptStep);
        StringAssert.Contains(line, "result=FAIL", line);
        Assert.AreEqual("1", Fact(line, "fixed_step_four"), line);
        Assert.AreEqual(40f, Number(line, "ratio_four"), 5f, "the fixed step of 4 multiplies into the fast forward: " + line);
        Assert.AreEqual("1", Fact(line, "request_half"), line);
        Assert.AreEqual(5f, Number(line, "ratio_half"), 1f, "and a half-speed request makes it a half: " + line);
    }

    [TestMethod]
    public void AdoptFlag_WhenOnlyTheClockRunsTwiceAsFast_FailsOnTheRatioAlone()
    {
        _world.FastForwardRate = 20f;

        Run(AdoptStep);

        var line = Line(AdoptStep);
        StringAssert.Contains(line, "result=FAIL", line);
        Assert.AreEqual(20f, Number(line, "ratio_four"), 2f, line);
        Assert.AreEqual("0", Fact(line, "fixed_step_four"), "only the clock broke: " + line);
        Assert.AreEqual("0", Fact(line, "request_half"), line);
    }

    [TestMethod]
    public void AdoptFlag_WhenOnlyTheClockRunsHalfAsFast_FailsOnTheRatioAlone()
    {
        _world.FastForwardRate = 5f;

        Run(AdoptStep);

        var line = Line(AdoptStep);
        StringAssert.Contains(line, "result=FAIL", line);
        Assert.AreEqual(5f, Number(line, "ratio_half"), 1f, line);
        Assert.AreEqual("0", Fact(line, "request_half"), "only the clock broke: " + line);
    }

    [TestMethod]
    public void AdoptFlag_WhenTheirFlagReadsDown_FailsOnIt()
    {
        _world.FastForwardReportedOff = true;

        Run(AdoptStep);

        AssertFails(AdoptStep, "flag_four", "0");
    }

    [TestMethod]
    public void AdoptFlag_WhenOnlyTheFixedStepStillReads_FailsOnIt()
    {
        _world.Adoption = AdoptionFault.StepReadsOn;

        Run(AdoptStep);

        var line = Line(AdoptStep);
        AssertFails(AdoptStep, "fixed_step_four", "1");
        Assert.AreEqual(10f, Number(line, "ratio_four"), 1f, "only the state fact broke: " + line);
    }

    [TestMethod]
    public void AdoptFlag_WhenOnlyTheRequestStillReads_FailsOnIt()
    {
        _world.Adoption = AdoptionFault.RequestReadsHeld;

        Run(AdoptStep);

        var line = Line(AdoptStep);
        AssertFails(AdoptStep, "request_half", "1");
        Assert.AreEqual(10f, Number(line, "ratio_half"), 1f, "only the state fact broke: " + line);
    }

    [TestMethod]
    public void AdoptFlag_WhenTheLevelStaysAtTenAfterTheirFlagFalls_FailsOnTheLevel()
    {
        _world.Adoption = AdoptionFault.LevelStaysAfterTheirFlagFalls;

        Run(AdoptStep);

        var line = Line(AdoptStep);
        AssertFails(AdoptStep, "level_after_four", "10");
        Assert.AreEqual("0", Fact(line, "fixed_step_four"), "only the level broke: " + line);
    }

    [TestMethod]
    public void AdoptFlag_WhenTheSeatAnswersHalfASecondLate_WaitsForItAndStillPasses()
    {
        _world.AdoptionLagSeconds = 0.5f;

        Run(AdoptStep);

        AssertPasses(AdoptStep);
        Assert.AreEqual(10f, Number(Line(AdoptStep), "ratio_four"), 1f, "the window opens after the answer, so the frames before it do not count: " + Line(AdoptStep));
    }

    [TestMethod]
    public void AdoptFlag_WhenTheSeatAnswersAfterTheLimit_FailsOnWhatItStillHolds()
    {
        _world.AdoptionLagSeconds = SeatTrialRules.AdoptWaitSeconds + 1f;

        Run(AdoptStep);

        var line = Line(AdoptStep);
        StringAssert.Contains(line, "result=FAIL", line);
        Assert.AreEqual("1", Fact(line, "fixed_step_four"), line);
        Assert.AreEqual("1", Fact(line, "request_half"), line);
    }

    [TestMethod]
    public void AdoptFlag_WhenAKeyLeavesTheSeat_FailsOnSeated()
    {
        _world.TimeKeyLeavesTheSeat = true;

        Run(AdoptStep);

        var line = Line(AdoptStep);
        AssertFails(AdoptStep, "seated", "0");
        Assert.AreEqual("0", Fact(line, "fixed_step_four"), "only the Seat's state broke: " + line);
    }

    [TestMethod]
    public void AdoptFlag_WhenTheSessionFaults_FailsOnFaulted()
    {
        _world.TimeKeyFaults = true;

        Run(AdoptStep);

        var line = Line(AdoptStep);
        AssertFails(AdoptStep, "faulted", "1");
        Assert.AreEqual("0", Fact(line, "fixed_step_four"), "only the fault broke: " + line);
    }

    [TestMethod]
    public void AdoptFlag_WhenAnEngineRequestIsHeld_FailsAtOnceWithItsReason()
    {
        _world.OtherRequestHeld = true;

        Run(AdoptStep);

        StringAssert.Contains(Line(AdoptStep), "result=FAIL reason=other-request", Line(AdoptStep));
    }

    [TestMethod]
    public void AdoptFlag_WhenTheKeysAreDead_FailsBecauseNoLevelIsReached()
    {
        _world.TimeKeysDead = true;

        Run(AdoptStep);

        StringAssert.Contains(Line(AdoptStep), "result=FAIL reason=level-not-reached", Line(AdoptStep));
        Assert.AreEqual(0, _world.Writes.Count(w => w == "other-fast-forward"), "their flag is raised only from a level the step reached");
    }

    [TestMethod]
    public void AdoptFlag_BetweenFastAndSpeedCleared_LeavesTimeAtOneForTheNextStep()
    {
        Run("fast", AdoptStep, "speed_cleared");

        AssertPasses("fast");
        AssertPasses(AdoptStep);
        AssertPasses("speed_cleared");
    }

    [TestMethod]
    public void TheStandIn_AsksThePureLadderForTheAdoption_SoItCannotAgreeWithACopy()
    {
        var standIn = TAOM.Tests.Infrastructure.RepoPaths.ReadSource("TAOM.Tests/Features/SeatOfSeeing/CommandTrialWorld.Time.cs", stripComments: true);

        StringAssert.Contains(standIn, "SeatTimeLadder.Frame(_levelIndex, _fastForward)");
        Assert.IsFalse(Regex.IsMatch(standIn, @"SeatTimeAction\.Adopt\s*=="), "the action comes from the rule and is only followed here");
    }

    // ---- speed_cleared ----

    [TestMethod]
    public void SpeedCleared_OnAWorkingWorld_BuildsEachStateThenReadsItCleared()
    {
        Run("speed_cleared");

        var line = Line("speed_cleared");
        AssertPasses("speed_cleared");
        Assert.AreEqual("1", Fact(line, "fast_forward_before"), "the control: the flag really was up");
        Assert.AreEqual("1", Fact(line, "fixed_step_before"), "the control: the fixed step really was on");
        Assert.AreEqual("1", Fact(line, "request_slow"), "the control: the request really was held");
        Assert.AreEqual("0", Fact(line, "fast_forward_after"));
        Assert.AreEqual("0", Fact(line, "request_end"));
    }

    [TestMethod]
    public void SpeedCleared_WhenThePauseLeavesFastForward_FailsOnTheFlagAfter()
    {
        _world.PauseLeavesFastForward = true;

        Run("speed_cleared");

        AssertFails("speed_cleared", "fast_forward_after", "1");
    }

    [TestMethod]
    public void SpeedCleared_WhenTheSlowLevelLeavesTheFixedStep_FailsOnIt()
    {
        _world.FixedStepStaysOnAtSlow = true;

        Run("speed_cleared");

        AssertFails("speed_cleared", "fixed_step_slow", "1");
    }

    [TestMethod]
    public void SpeedCleared_WhenAQuickenLeavesTheRequest_FailsOnIt()
    {
        _world.RequestSurvivesQuicken = true;

        Run("speed_cleared");

        AssertFails("speed_cleared", "request_end", "1");
    }

    [TestMethod]
    public void SpeedCleared_WhenFastForwardNeverRises_FailsOnItsControl()
    {
        _world.FastForwardNeverRises = true;

        Run("speed_cleared");

        AssertFails("speed_cleared", "fast_forward_before", "0");
    }

    [TestMethod]
    public void SpeedCleared_WhenTheFixedStepNeverRises_FailsOnItsControl()
    {
        _world.FixedStepNeverRises = true;

        Run("speed_cleared");

        AssertFails("speed_cleared", "fixed_step_before", "0");
    }

    [TestMethod]
    public void SpeedCleared_WhenTheKeysAreDead_FailsBecauseNoLevelIsReached()
    {
        _world.TimeKeysDead = true;

        Run("speed_cleared");

        var line = Line("speed_cleared");
        StringAssert.Contains(line, "result=FAIL reason=level-not-reached", line);
    }

    // ---- A failed time step releases time ----

    [TestMethod]
    public void AFailedTimeStep_ReleasesTimeSoTheNextStepStartsAtOne()
    {
        _world.FastIgnoredByTheClock = true;

        Run("fast", "speed_cleared");

        StringAssert.Contains(Line("fast"), "result=FAIL");
        Assert.IsTrue(_world.TimeReleases > 0, "the driver released time after the failure");
    }

    // ---- rise_with_panel ----

    [TestMethod]
    public void RiseWithPanel_OnAWorkingWorld_ReadsALiftOfAtLeastHalfTheHeight_WithTheSeatDown()
    {
        Run("rise_with_panel");

        var line = Line("rise_with_panel");
        AssertPasses("rise_with_panel");
        Assert.AreEqual("Off", Fact(line, "state"));
        Assert.IsTrue(Number(line, "rise") >= 3.5f, line);
        Assert.AreEqual("7.0", Fact(line, "height"));
    }

    [TestMethod]
    public void RiseWithPanel_WhenTheLiftDoesNothing_FailsOnTheRise()
    {
        _world.RiseDead = true;

        Run("rise_with_panel");

        var line = Line("rise_with_panel");
        StringAssert.Contains(line, "result=FAIL", line);
        Assert.AreEqual(0f, Number(line, "rise"), 0.01f, line);
        Assert.AreEqual("1", Fact(line, "panel"), "only the lift broke: " + line);
    }

    [TestMethod]
    public void RiseWithPanel_ForAFirstPersonView_FailsWithItsReason()
    {
        _world.RiseFirstPerson = true;

        Run("rise_with_panel");

        StringAssert.Contains(Line("rise_with_panel"), "result=FAIL reason=first-person", Line("rise_with_panel"));
    }

    [TestMethod]
    public void RiseWithPanel_ForAMissionWithNoLiftHeight_FailsWithItsReason()
    {
        _world.RiseConstant = 0f;

        Run("rise_with_panel");

        StringAssert.Contains(Line("rise_with_panel"), "reason=no-lift-height", Line("rise_with_panel"));
    }

    [TestMethod]
    public void RiseWithPanel_WhenTheSeatKeyDoesNotLeave_FailsWithItsReason()
    {
        _world.HoldsTheSeat = true;

        Run("rise_with_panel");

        StringAssert.Contains(Line("rise_with_panel"), "result=FAIL reason=still-seated", Line("rise_with_panel"));
    }

    [TestMethod]
    public void RiseWithPanel_WhenThePanelCannotBeOpened_FailsOnPanel()
    {
        _world.PanelSiteMissing = true;

        Run("rise_with_panel");

        AssertFails("rise_with_panel", "panel", "0");
    }

    [TestMethod]
    public void RiseWithPanel_WhenTheSessionFaults_FailsOnFaulted()
    {
        _world.FaultMidStep = true;

        Run("rise_with_panel");

        AssertFails("rise_with_panel", "faulted", "1");
    }

    [TestMethod]
    public void RiseWithPanel_WhenTheLiftStopsShortOfHalfTheHeight_Fails()
    {
        _world.RiseConstant = 7f;
        _world.RiseReaches = 2.0f;

        Run("rise_with_panel");

        var line = Line("rise_with_panel");
        StringAssert.Contains(line, "result=FAIL", line);
        Assert.AreEqual(2f, Number(line, "rise"), 0.3f, line);
    }

    // ---- sink_on_close ----

    [TestMethod]
    public void SinkOnClose_OnAWorkingWorld_ReadsTheViewBackAndTheOffsetExactlyZero()
    {
        Run("rise_with_panel", "sink_on_close");

        var line = Line("sink_on_close");
        AssertPasses("sink_on_close");
        Assert.AreEqual("0", Fact(line, "offset_z"));
    }

    [TestMethod]
    public void SinkOnClose_OnAWorkingWorld_TakesTheSeatAgainForTheStepsThatFollow()
    {
        Run("rise_with_panel", "sink_on_close");

        Assert.AreEqual(SeatState.Seated, _world.State, "the fall that follows in a full run starts from a Seat that is up");
        Assert.AreEqual(3, _world.Presses, "the enter press, one leave in rise_with_panel and one entry in sink_on_close");
    }

    [TestMethod]
    public void SinkOnClose_WhenALeftoverStaysInTheOffset_FailsOnTheExactZero()
    {
        _world.SinkLeavesResidue = 0.001f;

        Run("rise_with_panel", "sink_on_close");

        var line = Line("sink_on_close");
        StringAssert.Contains(line, "result=FAIL", line);
        Assert.AreEqual("0.001", Fact(line, "offset_z"), line);
    }

    [TestMethod]
    public void SinkOnClose_WhenTheViewNeverSinks_FailsOnTheDistanceBack()
    {
        _world.SinkNever = true;

        Run("rise_with_panel", "sink_on_close");

        var line = Line("sink_on_close");
        StringAssert.Contains(line, "result=FAIL", line);
        Assert.IsTrue(Number(line, "after") - Number(line, "before") > 3f, line);
    }

    [TestMethod]
    public void SinkOnClose_WhenThePanelStaysOpen_FailsOnPanel()
    {
        _world.PanelStaysOpenOnClose = true;

        Run("rise_with_panel", "sink_on_close");

        AssertFails("sink_on_close", "panel", "1");
    }

    [TestMethod]
    public void SinkOnClose_AfterARiseThatCouldNotOpenThePanel_FailsWithItsReason()
    {
        _world.PanelSiteMissing = true;

        Run("rise_with_panel", "sink_on_close");

        StringAssert.Contains(Line("sink_on_close"), "result=FAIL reason=panel-not-open", Line("sink_on_close"));
    }
}
