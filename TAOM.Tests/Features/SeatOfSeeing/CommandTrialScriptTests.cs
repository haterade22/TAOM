using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using TAOM.Client.Features.SeatOfSeeing.Trial;
using TAOM.Features.SeatOfSeeing.Domain;
using Verdict = TAOM.Client.Features.SeatOfSeeing.Trial.CommandTrialScript.Verdict;

namespace TAOM.Tests.Features.SeatOfSeeing;

/// <summary>
/// The field trial's script (build-spec section 12) against a stand-in for the engine: the order and timing of the steps, the pass rule of
/// each, and that a broken fact fails its own step and nothing hangs. The game is the only proof of the facts themselves.
/// </summary>
[TestClass]
public class CommandTrialScriptTests
{
    private static readonly string[] StageOne =
        { "refuse_in_deployment", "enter", "fly", "hero_stands", "leave_glide", "reenter_keeps_height" };

    private CommandTrialWorld _world = null!;
    private ListLogger _log = null!;
    private List<string> _finished = null!;
    private List<string> _stepsSeen = null!;

    [TestInitialize]
    public void Setup()
    {
        _world = new CommandTrialWorld();
        _log = new ListLogger();
        _finished = new List<string>();
        _stepsSeen = new List<string>();
    }

    private CommandTrialScript Script(string? steps = null)
    {
        Assert.IsTrue(SeatTrialSteps.TryChoose(steps ?? string.Join(",", StageOne), out var chosen, out var error), error);
        return new CommandTrialScript(_world, chosen, _log, _finished.Add, _stepsSeen.Add);
    }

    private CommandTrialScript RunAll(string? steps = null, float seconds = 90f)
    {
        var script = Script(steps);
        _world.Run(script, seconds);
        return script;
    }

    private List<string> Lines() => _log.Lines.Where(l => l.StartsWith("[SeatTrial] step=", StringComparison.Ordinal)).ToList();

    private string Line(string step) => Lines().Single(l => l.StartsWith("[SeatTrial] step=" + step + " ", StringComparison.Ordinal));

    private static string Fact(string line, string key)
    {
        var match = Regex.Match(line, @"\b" + key + @"=(\S+)");
        Assert.IsTrue(match.Success, key + " is not in: " + line);
        return match.Groups[1].Value;
    }

    // ---- The whole trial ----

    [TestMethod]
    public void AFullRun_WritesOnePassLinePerStep_InTheTablesOrder_ThenFinishesAsFinal()
    {
        var script = RunAll();

        Assert.IsTrue(script.IsDone);
        var lines = Lines();
        CollectionAssert.AreEqual(StageOne, lines.Select(l => Regex.Match(l, @"step=(\w+)").Groups[1].Value).ToArray(), string.Join("\n", lines));
        foreach (var line in lines)
            StringAssert.Contains(line, "result=PASS", line);
        CollectionAssert.AreEqual(new[] { "final" }, _finished);
        Assert.AreEqual(0, _log.Errors.Count);
    }

    [TestMethod]
    public void AFullRun_ReportsEachStepAsItBegins_ForTheStatusFile()
    {
        RunAll();

        CollectionAssert.AreEqual(StageOne, _stepsSeen);
    }

    [TestMethod]
    public void AFullRun_ClosesWithASummaryLine_AndLeavesNoInputHeld()
    {
        RunAll();

        var summary = _log.Lines.Single(l => l.StartsWith("[SeatTrial] done ", StringComparison.Ordinal));
        StringAssert.Contains(summary, "steps=" + StageOne.Length);
        StringAssert.Contains(summary, "pass=" + StageOne.Length);
        StringAssert.Contains(summary, "fail=0");
        Assert.IsNull(_world.Input, "the held input is released when the trial ends");
    }

    [TestMethod]
    public void TheLines_AreTheContractsShape_WithNoDashesInThem()
    {
        RunAll();

        foreach (var line in Lines())
        {
            StringAssert.Matches(line, new Regex(@"^\[SeatTrial\] step=[a-z_]+ result=(PASS|FAIL)( \S+=\S+)*$"), line);
            Assert.IsFalse(line.Contains('—') || line.Contains('–'), line);
        }
    }

    [TestMethod]
    public void Deployment_IsFinishedOnce_NoSoonerThanFiveSeconds_AndAfterTheRefusalStepHasReported()
    {
        RunAll();

        Assert.AreEqual(1, _world.DeploymentFinishes);
        Assert.IsTrue(_world.DeploymentFinishedAt >= 5f && _world.DeploymentFinishedAt < 5.2f, "finished at " + _world.DeploymentFinishedAt);
    }

    [TestMethod]
    public void Deployment_WhenTheScriptStartsLate_StillWaitsForTheRefusalStepToReportFirst()
    {
        _world.Time = 6f;
        var script = Script();

        _world.Run(script, 0.5f);

        Assert.AreEqual(1, _world.PressesInDeployment, "the press came in Deployment, before the mode changed");
        Assert.AreEqual(1, _world.DeploymentFinishes);
        StringAssert.Contains(Line("refuse_in_deployment"), "result=PASS");
    }

    [TestMethod]
    public void Deployment_IsNotFinishedWhileTheRefusalStepStillWaitsForItsPress()
    {
        _world.Time = 6f;
        _world.PressesAreIgnored = true;
        var script = Script();

        _world.Run(script, 1.5f);

        Assert.AreEqual(0, _world.DeploymentFinishes, "the refusal step has not reported, and the mode must not change under it");
        Assert.AreEqual(0, Lines().Count);

        _world.Run(script, 1f);

        StringAssert.Contains(Line("refuse_in_deployment"), "reason=press-not-taken");
        Assert.AreEqual(1, _world.DeploymentFinishes, "once it has reported, the deployment ends");
    }

    // ---- refuse_in_deployment ----

    [TestMethod]
    public void Refuse_PressesOnceInDeployment_AndPassesWithTheStateOff()
    {
        RunAll("refuse_in_deployment");

        Assert.AreEqual(1, _world.PressesInDeployment);
        Assert.AreEqual(1, _world.Presses);
        var line = Line("refuse_in_deployment");
        StringAssert.Contains(line, "result=PASS");
        Assert.AreEqual("Off", Fact(line, "state"));
        Assert.AreEqual("Deployment", Fact(line, "mode"));
    }

    [TestMethod]
    public void Refuse_WhenThePressIsNeverTaken_FailsWithThatReason_AndTheRunStillEnds()
    {
        _world.PressesAreIgnored = true;

        RunAll();

        var line = Line("refuse_in_deployment");
        StringAssert.Contains(line, "result=FAIL");
        StringAssert.Contains(line, "reason=press-not-taken");
        Assert.AreEqual(StageOne.Length, Lines().Count, "every step still reports");
        CollectionAssert.AreEqual(new[] { "final" }, _finished);
    }

    [TestMethod]
    public void Refuse_WhenTheSeatEntersInDeployment_FailsNamingTheState()
    {
        _world.SeatIgnoresDeployment = true;

        RunAll("refuse_in_deployment", 6f);

        var line = Line("refuse_in_deployment");
        StringAssert.Contains(line, "result=FAIL");
        Assert.AreNotEqual("Off", Fact(line, "state"));
    }

    [TestMethod]
    public void Refuse_WhenTheMissionLeftDeploymentBeforeThePress_Fails()
    {
        _world.Deployment = false;

        RunAll("refuse_in_deployment", 6f);

        var line = Line("refuse_in_deployment");
        StringAssert.Contains(line, "result=FAIL");
        StringAssert.Contains(line, "reason=not-in-deployment");
    }

    // ---- enter ----

    [TestMethod]
    public void Enter_PassesAfterThreeSeconds_WithRisingThenSeated_AndTheFrameCountAndController()
    {
        RunAll();

        var line = Line("enter");
        StringAssert.Contains(line, "result=PASS");
        Assert.AreEqual("1", Fact(line, "rising"));
        Assert.AreEqual("1", Fact(line, "seated"));
        Assert.AreEqual("Player", Fact(line, "controller"));
        Assert.IsTrue(int.Parse(Fact(line, "frames")) > 100);
    }

    [TestMethod]
    public void Enter_WaitsForABattleAndALivingHero_BeforePressing()
    {
        _world.HeroPresent = false;
        var script = Script();

        _world.Run(script, 12f);

        Assert.AreEqual(1, _world.Presses, "only the deployment press: the hero is not there yet");
        Assert.AreEqual(SeatState.Off, _world.State);
        _world.HeroPresent = true;
        _world.Run(script, 1.2f);
        Assert.AreEqual(2, _world.Presses);
    }

    [TestMethod]
    public void Enter_WhenTheFramesAreFew_FailsOnTheFrameCount()
    {
        _world.FramesPerOwnedTick = 0.5f;

        RunAll();

        var line = Line("enter");
        StringAssert.Contains(line, "result=FAIL");
        Assert.IsTrue(int.Parse(Fact(line, "frames")) <= 100);
    }

    [TestMethod]
    public void Enter_WhenTheHeroIsNoLongerPlayerControlled_Fails()
    {
        _world.HeroPlayerControlled = false;

        RunAll();

        var line = Line("enter");
        StringAssert.Contains(line, "result=FAIL");
        Assert.AreNotEqual("Player", Fact(line, "controller"));
    }

    [TestMethod]
    public void Enter_WhenTheBlendIsNeverSeen_Fails()
    {
        _world.BlendSeconds = 0f;

        RunAll();

        var line = Line("enter");
        StringAssert.Contains(line, "result=FAIL");
        Assert.AreEqual("0", Fact(line, "rising"));
    }

    [TestMethod]
    public void Enter_WhenTheSeatFaults_Fails_AndEveryLaterStepFailsToo_WithoutHanging()
    {
        _world.FaultOnEntry = true;

        RunAll();

        foreach (var step in StageOne.Skip(1))
            StringAssert.Contains(Line(step), "result=FAIL", step);
        StringAssert.Contains(Line("refuse_in_deployment"), "result=PASS");
        CollectionAssert.AreEqual(new[] { "final" }, _finished);
    }

    [TestMethod]
    public void Enter_TakesAScreenshotTwoSecondsIn()
    {
        RunAll("enter");

        CollectionAssert.AreEqual(new[] { "hud_enter.bmp" }, _world.Screenshots);
        Assert.AreEqual(2f, _world.ScreenshotTimes[0] - _world.PressTimes[0], 0.1f);
    }

    [TestMethod]
    public void Enter_PressesOneSecondAfterTheBattleBegins_AndAfterTheDeploymentStep_PressesOneSecondIn()
    {
        RunAll();

        Assert.AreEqual(1f, _world.PressTimes[0], 0.1f, "the deployment press, one second in");
        Assert.AreEqual(1f, _world.PressTimes[1] - _world.DeploymentFinishedAt!.Value, 0.1f, "the entry press, a second of battle later");
    }

    // ---- fly ----

    [TestMethod]
    public void Fly_HoldsForwardForTwoSeconds_AndPassesOnTheExpectedDistance()
    {
        RunAll();

        var line = Line("fly");
        StringAssert.Contains(line, "result=PASS");
        Assert.AreEqual(29.8f, float.Parse(Fact(line, "expected")) / float.Parse(Fact(line, "seconds")), 0.5f, "10 + 0.9 * 22 m/s at the entry height, to the rounding of two one-decimal facts");
        Assert.AreEqual(float.Parse(Fact(line, "expected")), float.Parse(Fact(line, "moved")), 3f);
        Assert.AreEqual(22f, float.Parse(Fact(line, "height")), 0.1f);
        Assert.IsTrue(float.Parse(Fact(line, "seconds")) >= 2f && float.Parse(Fact(line, "seconds")) < 2.2f);
    }

    [TestMethod]
    public void Fly_TakesItsPictureOneAndAHalfSecondsIntoTheFlight()
    {
        RunAll("enter,fly");

        CollectionAssert.AreEqual(new[] { "hud_enter.bmp", "hud_fly.bmp" }, _world.Screenshots);
        Assert.AreEqual(1.5f, _world.ScreenshotTimes[1] - _world.ForwardHeldAt!.Value, 0.1f);
    }

    [TestMethod]
    public void Fly_ReleasesTheInputAfterTheWindow_AndTakesItsScreenshot()
    {
        var script = Script();
        _world.Run(script, 11.6f);

        CollectionAssert.Contains(_world.Screenshots, "hud_fly.bmp");
        Assert.AreEqual(1, Lines().Count(l => l.Contains("step=fly ")), "the flight is over and the next step runs");
        Assert.IsTrue(_world.Input.HasValue);
        Assert.AreEqual(0f, _world.Input!.Value.Forward, "forward is released once the window ends");
    }

    [TestMethod]
    public void Fly_WhenTheViewDoesNotMove_Fails()
    {
        _world.ViewFrozen = true;

        RunAll();

        var line = Line("fly");
        StringAssert.Contains(line, "result=FAIL");
        Assert.AreEqual(0f, float.Parse(Fact(line, "moved")), 0.01f);
    }

    [TestMethod]
    public void Fly_PassesAtTwentyPercentSlow_AndFailsAtFortyPercentSlow()
    {
        _world.SpeedFactor = 0.8f;
        RunAll();
        StringAssert.Contains(Line("fly"), "result=PASS");

        Setup();
        _world.SpeedFactor = 0.6f;
        RunAll();
        StringAssert.Contains(Line("fly"), "result=FAIL");
    }

    [TestMethod]
    public void Fly_PassesAtTwentyPercentFast_AndFailsAtFortyPercentFast()
    {
        _world.SpeedFactor = 1.2f;
        RunAll();
        StringAssert.Contains(Line("fly"), "result=PASS");

        Setup();
        _world.SpeedFactor = 1.4f;
        RunAll();
        StringAssert.Contains(Line("fly"), "result=FAIL");
    }

    // ---- hero_stands ----

    [TestMethod]
    public void HeroStands_PassesWhenTheHeroStaysPut()
    {
        RunAll();

        var line = Line("hero_stands");
        StringAssert.Contains(line, "result=PASS");
        Assert.AreEqual(0f, float.Parse(Fact(line, "speed")), 0.05f);
        Assert.AreEqual(1.5f, float.Parse(Fact(line, "seconds")), 0.1f, "the window");
    }

    [TestMethod]
    public void HeroStands_PassesAtFourTenthsOfAMetrePerSecond_AndFailsAtOneMetrePerSecond()
    {
        _world.HeroDriftPerSecond = 0.4f;
        RunAll();
        StringAssert.Contains(Line("hero_stands"), "result=PASS");

        Setup();
        _world.HeroDriftPerSecond = 1f;
        RunAll();
        var line = Line("hero_stands");
        StringAssert.Contains(line, "result=FAIL");
        Assert.AreEqual(1f, float.Parse(Fact(line, "speed")), 0.1f);
    }

    [TestMethod]
    public void HeroStands_WithNoHeroToMeasure_Fails()
    {
        var script = Script();
        _world.Run(script, 8f);
        _world.HeroGone = true;

        _world.Run(script, 60f);

        StringAssert.Contains(Line("hero_stands"), "result=FAIL");
    }

    // ---- leave_glide ----

    [TestMethod]
    public void LeaveGlide_PressesTheKeyOnce_SeesTheStateOff_AndTheViewReachTheHero()
    {
        RunAll();

        var line = Line("leave_glide");
        StringAssert.Contains(line, "result=PASS");
        Assert.AreEqual("1", Fact(line, "glide"));
        Assert.IsTrue(float.Parse(Fact(line, "near")) <= 10f);
        Assert.IsTrue(float.Parse(Fact(line, "before")) > 40f, "the view had flown away from the hero");
    }

    [TestMethod]
    public void LeaveGlide_WhenTheGlideWasNotArmed_Fails()
    {
        _world.GlideArms = false;

        RunAll();

        var line = Line("leave_glide");
        StringAssert.Contains(line, "result=FAIL");
        Assert.AreEqual("0", Fact(line, "glide"));
    }

    [TestMethod]
    public void LeaveGlide_WhenTheCameraSnapsAtOnce_StillPasses_AndSaysTheViewWasNearAtTheFirstLook()
    {
        _world.GlideSeconds = 0f;

        RunAll();

        var line = Line("leave_glide");
        StringAssert.Contains(line, "result=PASS");
        Assert.IsTrue(float.Parse(Fact(line, "after")) <= 10f, "a snap is a fact in the line, not a failure (build-spec 12, Known limits)");
    }

    [TestMethod]
    public void LeaveGlide_WhenTheViewNeverComesBack_FailsWithThatReason()
    {
        _world.GlideStuck = true;

        RunAll();

        var line = Line("leave_glide");
        StringAssert.Contains(line, "result=FAIL");
        StringAssert.Contains(line, "reason=never-near");
        Assert.AreEqual(4f, float.Parse(Fact(line, "seconds")), 0.1f, "the glide window");
    }

    [TestMethod]
    public void LeaveGlide_WhenThePressLeavesTheSeatUp_FailsAtOnce()
    {
        var script = Script();
        _world.Run(script, 9.5f);
        Assert.AreEqual(SeatState.Seated, _world.State);
        _world.HoldsTheSeat = true;

        _world.Run(script, 60f);

        var line = Line("leave_glide");
        StringAssert.Contains(line, "result=FAIL");
        StringAssert.Contains(line, "reason=still-seated");
    }

    // ---- reenter_keeps_height ----

    [TestMethod]
    public void Reenter_PassesWhenTheLastHeightIsKept()
    {
        RunAll();

        var line = Line("reenter_keeps_height");
        StringAssert.Contains(line, "result=PASS");
        Assert.AreEqual(float.Parse(Fact(line, "before")), float.Parse(Fact(line, "after")), 0.5f);
        Assert.AreEqual(22f, float.Parse(Fact(line, "after")), 0.5f);
    }

    [TestMethod]
    public void Reenter_WhenTheEntryHeightComesBack_Fails()
    {
        _world.ReenterHeight = 12f;

        RunAll();

        var line = Line("reenter_keeps_height");
        StringAssert.Contains(line, "result=FAIL");
        Assert.AreEqual(12f, float.Parse(Fact(line, "after")), 0.5f);
    }

    [TestMethod]
    public void Reenter_JudgesOneSecondAfterTheSeatIsBack()
    {
        RunAll();

        Assert.AreEqual(1f, _world.Time - _world.SeatedAt, 0.1f);
    }

    [TestMethod]
    public void Reenter_LeavesTheSeatUp_SoTheEndOfTheMissionExercisesTheFinishPath()
    {
        RunAll();

        Assert.AreEqual(SeatState.Seated, _world.State);
    }

    // ---- Choosing steps ----

    [TestMethod]
    public void ASubset_RunsOnlyThoseSteps_AndStillFinishes()
    {
        RunAll("enter,fly");

        CollectionAssert.AreEqual(new[] { "enter", "fly" }, Lines().Select(l => Regex.Match(l, @"step=(\w+)").Groups[1].Value).ToArray());
        CollectionAssert.AreEqual(new[] { "final" }, _finished);
        Assert.AreEqual(1, _world.DeploymentFinishes, "deployment still ends, because the battle is what the steps need");
    }

    [TestMethod]
    public void TheDeploymentStepAlone_EndsTheRunAtOnce_WithoutFinishingTheDeployment()
    {
        RunAll("refuse_in_deployment", 20f);

        Assert.AreEqual(1, Lines().Count);
        Assert.AreEqual(0, _world.DeploymentFinishes, "nothing in the list needs a battle");
        CollectionAssert.AreEqual(new[] { "final" }, _finished);
    }

    // ---- Budgets and failure ----

    [TestMethod]
    public void AStepThatNeverCompletes_FailsOnItsBudget_NamingTheTimeout()
    {
        _world.HeroPresent = false;

        RunAll();

        var line = Line("enter");
        StringAssert.Contains(line, "result=FAIL");
        StringAssert.Contains(line, "reason=timeout waiting=battle");
        CollectionAssert.AreEqual(new[] { "final" }, _finished);
    }

    [TestMethod]
    public void AfterTheRunEnds_FurtherTicksWriteNothingAndFinishNothing()
    {
        var script = RunAll();
        var lines = _log.Lines.Count;

        _world.Time += 1f;
        _world.ScreenTick(script);
        _world.MissionTick(script, 0.03f);

        Assert.AreEqual(lines, _log.Lines.Count);
        Assert.AreEqual(1, _finished.Count);
    }

    [TestMethod]
    public void AnExceptionFromTheEngine_EndsTheRunAsAScriptFailure_AndIsLoggedOnce()
    {
        _world.ClockFailsFrom = 0f;
        var failing = Script();

        failing.OnScreenTick();
        failing.OnScreenTick();

        Assert.IsTrue(failing.IsDone);
        CollectionAssert.AreEqual(new[] { "script_failed" }, _finished);
        Assert.AreEqual(1, _log.Errors.Count);
        StringAssert.Contains(_log.Errors[0], "the clock failed");
    }

    [TestMethod]
    public void AnExceptionInTheMiddleOfTheFlight_ReleasesTheHeldInput_LikeAFinishedRunDoes()
    {
        var script = Script();
        for (var i = 0; i < 400 && !(_world.Input.HasValue && _world.Input.Value.Forward > 0f); i++)
            _world.Run(script, 0.1f);
        Assert.IsTrue(_world.Input.HasValue && _world.Input.Value.Forward > 0f, "the flight is under way");

        _world.ClockFailsFrom = _world.Time;
        _world.Run(script, 1f);

        Assert.IsTrue(script.IsDone);
        CollectionAssert.AreEqual(new[] { "script_failed" }, _finished);
        Assert.IsNull(_world.Input, "an abort during the flight must not leave forward held until the mission ends");
    }

    // ---- Faults the Seat latches ----

    [TestMethod]
    public void Refuse_WhenTheSeatFaultsOnTheDeploymentPress_Fails_EvenWithTheStateOffAndTheModeDeployment()
    {
        _world.FaultInDeployment = true;

        RunAll("refuse_in_deployment");

        var line = Line("refuse_in_deployment");
        StringAssert.Contains(line, "result=FAIL");
        Assert.AreEqual("Off", Fact(line, "state"));
        Assert.AreEqual("Deployment", Fact(line, "mode"));
    }

    [TestMethod]
    public void LeaveGlide_WhenTheSeatFaultsOnTheLeave_Fails_EvenWithTheGlideArmedAndTheViewNearTheHero()
    {
        _world.FaultOnLeave = true;

        RunAll();

        var line = Line("leave_glide");
        StringAssert.Contains(line, "result=FAIL");
        Assert.AreEqual("1", Fact(line, "glide"));
        Assert.IsTrue(float.Parse(Fact(line, "near")) <= 10f);
    }

    // ---- The driver: how a step yields ----

    private CommandTrialScript ScriptOf(Func<IEnumerable<Verdict>> step, float budgetSeconds = 5f)
    {
        var rows = new[] { new SeatTrialStep("marked", Array.Empty<string>(), budgetSeconds) };
        var handlers = new Dictionary<string, Func<CommandTrialScript, IEnumerable<Verdict>>> { ["marked"] = _ => step() };
        return new CommandTrialScript(_world, rows, _log, _finished.Add, _stepsSeen.Add, handlers);
    }

    private IEnumerable<Verdict> WritesInTheMissionTick()
    {
        yield return Verdict.Waiting("setup");
        yield return Verdict.InMissionTick;
        _world.PressSeatKey();
        yield return Verdict.Passed("written=1");
    }

    [TestMethod]
    public void AStepAfterTheInMissionTickMarker_RunsInTheMissionTick_AndTheScreenTickDoesNotResumeIt()
    {
        var script = ScriptOf(WritesInTheMissionTick);
        _world.Time = 0.1f;

        script.OnScreenTick();
        script.OnScreenTick();
        script.OnScreenTick();

        Assert.AreEqual(0, _world.Presses, "the write waits for the mission tick");
        Assert.IsFalse(script.IsDone);

        script.OnMissionTick();

        Assert.AreEqual(1, _world.Presses);
        StringAssert.Contains(Line("marked"), "result=PASS");
        CollectionAssert.AreEqual(new[] { "final" }, _finished);
    }

    [TestMethod]
    public void AMissionTickStep_ThatGetsNoMissionTick_FailsOnItsBudget_NamingTheWait()
    {
        var script = ScriptOf(WritesInTheMissionTick, budgetSeconds: 2f);

        for (var i = 0; i < 4; i++)
        {
            _world.Time += 1f;
            script.OnScreenTick();
        }

        Assert.AreEqual(0, _world.Presses);
        var line = Line("marked");
        StringAssert.Contains(line, "result=FAIL");
        StringAssert.Contains(line, "reason=timeout waiting=mission-tick");
    }

    [TestMethod]
    public void AnExceptionAfterTheMarker_EndsTheRunAsAScriptFailure_InTheMissionTick()
    {
        var script = ScriptOf(() => ThrowsInTheMissionTick());
        script.OnScreenTick();
        script.OnScreenTick();
        Assert.IsFalse(script.IsDone);

        script.OnMissionTick();

        Assert.IsTrue(script.IsDone);
        CollectionAssert.AreEqual(new[] { "script_failed" }, _finished);
        StringAssert.Contains(_log.Errors.Single(), "step marked");
    }

    private static IEnumerable<Verdict> ThrowsInTheMissionTick()
    {
        yield return Verdict.InMissionTick;
        throw new InvalidOperationException("in the mission tick");
    }

    [TestMethod]
    public void AStepThatEndsWithNoVerdict_Fails()
    {
        var script = ScriptOf(() => EndsAfterOneWait());
        script.OnScreenTick();
        Assert.AreEqual(0, Lines().Count, "still waiting");

        script.OnScreenTick();

        StringAssert.Contains(Line("marked"), "result=FAIL");
        StringAssert.Contains(Line("marked"), "reason=no-verdict");
    }

    [TestMethod]
    public void AStepThatYieldsNothing_Fails()
    {
        var script = ScriptOf(() => Enumerable.Empty<Verdict>());

        script.OnScreenTick();

        StringAssert.Contains(Line("marked"), "reason=no-verdict");
    }

    private static IEnumerable<Verdict> EndsAfterOneWait()
    {
        yield return Verdict.Waiting("a wait");
    }

    [TestMethod]
    public void ACodeAfterADoneVerdict_NeverRuns()
    {
        var script = ScriptOf(() => FailsThenWrites());

        for (var i = 0; i < 5; i++)
        {
            _world.Time += 0.1f;
            script.OnScreenTick();
            script.OnMissionTick();
        }

        Assert.AreEqual(0, _world.Presses, "the driver stops at the first done verdict");
        Assert.AreEqual(1, Lines().Count(l => l.Contains("step=marked ")));
    }

    private IEnumerable<Verdict> FailsThenWrites()
    {
        yield return Verdict.Failed("reason=stop");
        _world.PressSeatKey();
    }

    [TestMethod]
    public void AStepsTimeout_NamesWhatItWasWaitingFor_NotAPhaseNumber()
    {
        var script = ScriptOf(() => WaitsForever(), budgetSeconds: 1f);

        for (var i = 0; i < 4; i++)
        {
            _world.Time += 0.5f;
            script.OnScreenTick();
        }

        var line = Line("marked");
        StringAssert.Contains(line, "reason=timeout waiting=the-thing");
        Assert.IsFalse(line.Contains("phase="), line);
    }

    private static IEnumerable<Verdict> WaitsForever()
    {
        while (true)
            yield return Verdict.Waiting("the-thing");
    }

    [TestMethod]
    public void EveryRowOfTheTable_HasAHandler_SoAStageCannotAddARowWithNoCode()
    {
        CollectionAssert.AreEquivalent(SeatTrialSteps.All.Select(s => s.Name).ToArray(), CommandTrialScript.HandlerNames.ToArray());
    }

    [TestMethod]
    public void ARowWithNoHandler_IsRefusedWhenTheScriptIsBuilt()
    {
        var orphan = new[] { new SeatTrialStep("not_written", Array.Empty<string>(), 5f) };

        Assert.ThrowsException<ArgumentException>(() => new CommandTrialScript(_world, orphan, _log, _finished.Add));
    }
}
