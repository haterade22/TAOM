using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using TAOM.Client.Features.SeatOfSeeing.Trial;
using TAOM.Features.Elephant;
using TAOM.Features.SeatOfSeeing.Domain;
using TAOM.Tests.Features.SeatOfSeeing;

namespace TAOM.Tests.Features.Elephant;

/// <summary>
/// The crew steps of the command trial (#1503) against the stand-in: each step passes on a crew that stays, shoots and rides, and
/// fails with its own facts when one of those is broken. The game is the only proof of the facts themselves.
/// </summary>
[TestClass]
public class CommandTrialCrewStepsTests
{
    private const float Frame = 1f / 30f;

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

    private void Run(params string[] steps)
    {
        var list = "enter," + string.Join(",", steps);
        Assert.IsTrue(SeatTrialSteps.TryChoose(list, out var chosen, out var error), error);
        var script = new CommandTrialScript(_world, chosen, _log, _finished.Add);
        _world.Run(script, 600f, Frame);
        Assert.IsTrue(script.IsDone, "the run ended");
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

    // ---- a crew that works ----

    [TestMethod]
    public void TheMumakilStep_OnACrewThatStaysShootsAndRides_Passes()
    {
        Run("crew_mumakil");

        var line = Line("crew_mumakil");
        StringAssert.Contains(line, "result=PASS", line);
        Assert.AreEqual("8", Fact(line, "seats"));
        Assert.AreEqual("0", Fact(line, "rescues"));
        Assert.AreEqual("alive", Fact(line, "beast"));
        Assert.AreEqual("Soldier", Fact(line, "kind"));
        Assert.AreEqual("1", Fact(line, "chosen"));
        Assert.AreEqual("1", Fact(line, "letgo"));
        Assert.AreEqual(8, Fact(line, "shots").Split(',').Length);
        CollectionAssert.AreEqual(new[] { CrewBeast.Mumakil }, _world.CrewChoices.Distinct().ToArray());
    }

    [TestMethod]
    public void TheStep_SendsTheArmyForwardOnce_AndLeavesNoKeepAndNoInput()
    {
        Run("crew_mumakil");

        Assert.AreEqual(1, _world.ChargeOrders);
        Assert.AreEqual(SeatKeepKind.None, _world.Keeping);
        Assert.IsNull(_world.Input);
    }

    [TestMethod]
    public void TheStep_ReadsAgentsAndGivesOrders_OnlyInTheMissionTick()
    {
        Run("crew_mumakil", "crew_howdah");

        Assert.AreEqual(0, _world.CrewReadsOutsideTheMissionTick);
        Assert.AreEqual(0, _world.WritesOutsideTheMissionTick.Count, string.Join(", ", _world.WritesOutsideTheMissionTick));
    }

    [TestMethod]
    public void TheHowdahStep_AfterTheMumakilStep_WatchesTheElephantsTwoSeats()
    {
        Run("crew_mumakil", "crew_howdah");

        StringAssert.Contains(Line("crew_mumakil"), "result=PASS");
        var line = Line("crew_howdah");
        StringAssert.Contains(line, "result=PASS", line);
        Assert.AreEqual("2", Fact(line, "seats"));
        CollectionAssert.AreEqual(new[] { CrewBeast.Mumakil, CrewBeast.Howdah }, _world.CrewChoices.Distinct().ToArray());
        CollectionAssert.AreEqual(new[] { "final" }, _finished);
    }

    [TestMethod]
    public void TheHowdahStep_RunsAlone()
    {
        Run("crew_howdah");

        StringAssert.Contains(Line("crew_howdah"), "result=PASS");
        Assert.AreEqual(1, _world.ChargeOrders);
    }

    // ---- the pictures ----

    [TestMethod]
    public void TheStep_SavesItsPicturesUnderTheNameTheRunnerConverts()
    {
        Run("crew_mumakil");

        var crew = _world.Screenshots.Where(name => name.Contains("crew")).ToArray();
        CollectionAssert.AreEqual(new[] { "hud_crew_mumakil_1.bmp", "hud_crew_mumakil_2.bmp", "hud_crew_mumakil_3.bmp" }, crew);
        Assert.AreEqual("3", Fact(Line("crew_mumakil"), "pictures"));
    }

    [TestMethod]
    public void TheFirstPictureIsClose_AndTheOthersAreAfterTheZoom()
    {
        Run("crew_mumakil");

        var times = _world.Screenshots.Zip(_world.ScreenshotTimes, (name, time) => (name, time)).Where(p => p.name.Contains("crew")).Select(p => p.time).ToArray();
        Assert.IsTrue(_world.ZoomedAt.HasValue, "the wheel turned");
        Assert.IsTrue(times[0] <= _world.ZoomedAt!.Value, "the first picture is before the zoom");
        Assert.IsTrue(times[1] > _world.ZoomedAt.Value, "the second picture is after the zoom");
        Assert.IsTrue(_world.KeptDistance >= CrewTrialRules.WideMetres, "the view is wide: " + _world.KeptDistance);
        Assert.IsTrue(_world.KeptDistance < CrewTrialRules.WideMetres / SeatOrbit.ZoomPerNotch, "the wheel stopped within one notch: " + _world.KeptDistance);
    }

    [TestMethod]
    public void AWheelThatDoesNothing_CostsTheZoomTime_NotTheStep()
    {
        _world.WheelDoesNothing = true;

        Run("crew_mumakil");

        var line = Line("crew_mumakil");
        StringAssert.Contains(line, "result=PASS", line);
        Assert.AreEqual("3", Fact(line, "pictures"));
        Assert.IsNull(_world.Input);
    }

    // ---- no crew ----

    [TestMethod]
    public void WithNoCrewedBeast_TheStepFailsAfterItsWait_AndGivesNoOrder()
    {
        _world.NoCrew = true;

        Run("crew_mumakil");

        AssertFails("crew_mumakil", "reason=no-crew", "beast=Mumakil");
        Assert.AreEqual(0, _world.ChargeOrders);
    }

    [TestMethod]
    public void ACrewThatIsSpawnedAFewSecondsLater_IsWaitedFor()
    {
        _world.CrewAppearsAt = 25f;

        Run("crew_mumakil");

        StringAssert.Contains(Line("crew_mumakil"), "result=PASS");
    }

    [TestMethod]
    public void WhenTheSeatWasNotEntered_TheStepFailsAtOnce()
    {
        _world.FaultOnEntry = true;

        Run("crew_mumakil");

        AssertFails("crew_mumakil", "reason=not-seated");
        Assert.AreEqual(0, _world.CrewChoices.Count);
    }

    // ---- the three facts, each broken alone ----

    [TestMethod]
    public void AnArcherWhoNeverShoots_FailsAtTheWatchLimit_WithHisZero()
    {
        _world.SilentSeat = 3;

        Run("crew_mumakil");

        var line = Line("crew_mumakil");
        AssertFails("crew_mumakil", "rescues=0");
        Assert.AreEqual("0", Fact(line, "shots").Split(',')[3]);
        Assert.IsTrue(float.Parse(Fact(line, "watched"), System.Globalization.CultureInfo.InvariantCulture) >= CrewTrialRules.WatchLimitSeconds);
    }

    [TestMethod]
    public void ARescueTeleport_Fails()
    {
        _world.RescuesOnTheFirstSeat = 2;

        Run("crew_mumakil");

        AssertFails("crew_mumakil", "rescues=2");
    }

    [TestMethod]
    public void ABeastThatStandsStill_Fails()
    {
        _world.BeastSpeed = 0f;

        Run("crew_mumakil");

        AssertFails("crew_mumakil", "moved=0.0", "rescues=0");
    }

    [TestMethod]
    public void ABeastWithAnEmptySeat_IsNotChosen_SoTheStepFindsNoCrew()
    {
        _world.EmptySeatsAtStart = 1;

        Run("crew_mumakil");

        AssertFails("crew_mumakil", "reason=no-crew");
        Assert.AreEqual(0, _world.ChargeOrders);
    }

    [TestMethod]
    public void ABeastThatFallsBeforeEveryoneShot_EndsTheWatch_AndFails()
    {
        _world.BeastDiesAfterSeconds = 4f;

        Run("crew_mumakil");

        var line = Line("crew_mumakil");
        AssertFails("crew_mumakil", "beast=gone");
        Assert.IsTrue(float.Parse(Fact(line, "watched"), System.Globalization.CultureInfo.InvariantCulture) < 10f, line);
    }

    [TestMethod]
    public void ABeastThatFallsAfterEveryoneShot_StillPasses()
    {
        _world.BeastDiesAfterSeconds = 20f;

        Run("crew_mumakil");

        var line = Line("crew_mumakil");
        StringAssert.Contains(line, "result=PASS", line);
        Assert.AreEqual("gone", Fact(line, "beast"));
    }

    // ---- the keep ----

    [TestMethod]
    public void WhenTheKeepTakesNobody_TheStepFailsNamingTheMessage()
    {
        _world.KeepFindsNobody = true;

        Run("crew_mumakil");

        AssertFails("crew_mumakil", "reason=not-kept", "said=");
    }

    [TestMethod]
    public void WhenTheKeptOneIsNotTheChosenArcher_TheStepFails()
    {
        _world.KeepsTheWrongOne = true;

        Run("crew_mumakil");

        AssertFails("crew_mumakil", "chosen=0");
    }

    [TestMethod]
    public void WhenTheKeptArcherFalls_TheViewGoesToAnotherArcher_AndHisShotsStillCount()
    {
        _world.KeptArcherDiesAfterSeconds = 12f;

        Run("crew_mumakil");

        var line = Line("crew_mumakil");
        StringAssert.Contains(line, "result=PASS", line);
        Assert.IsTrue(_world.ArcherChoices >= 1, "another archer was chosen");
        Assert.AreEqual(3, _world.KeepPresses, "the keep, the keep again and the let-go");
        Assert.AreNotEqual("0", Fact(line, "shots").Split(',')[0]);
        Assert.AreEqual("1", Fact(line, "letgo"));
    }

    [TestMethod]
    public void WhenTheKeptArcherFalls_AndNoArcherIsLeft_TheStepPressesNoKey()
    {
        _world.KeptArcherDiesAfterSeconds = 12f;
        _world.NoOtherArcher = true;

        Run("crew_mumakil");

        Assert.AreEqual(1, _world.KeepPresses);
        Assert.AreEqual("1", Fact(Line("crew_mumakil"), "letgo"));
    }

    [TestMethod]
    public void WhenTheKeyDoesNotLetGo_TheStepFails()
    {
        _world.KeyLetsGo = false;

        Run("crew_mumakil");

        AssertFails("crew_mumakil", "letgo=0");
    }

    // ---- the table ----

    [TestMethod]
    public void EveryCrewRow_HasAHandler_AndABudgetThatCoversItsLongestRun()
    {
        var longest = CrewTrialRules.CrewWaitSeconds + CrewTrialRules.WatchLimitSeconds + CrewTrialRules.ZoomSeconds + 3f * SeatTrialRules.ShortWaitSeconds;
        foreach (var row in CrewTrialRows.Rows)
        {
            CollectionAssert.Contains(CommandTrialScript.HandlerNames.ToList(), row.Name);
            Assert.IsTrue(row.BudgetSeconds >= longest + 10f, row.Name + " has " + row.BudgetSeconds + " s for a run of up to " + longest + " s");
        }
    }
}
