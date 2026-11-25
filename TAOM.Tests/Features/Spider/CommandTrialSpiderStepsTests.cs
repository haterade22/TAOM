using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text.RegularExpressions;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using TAOM.Client.Features.SeatOfSeeing.Trial;
using TAOM.Features.SeatOfSeeing.Domain;
using TAOM.Features.Spider;
using TAOM.Tests.Features.SeatOfSeeing;

namespace TAOM.Tests.Features.Spider;

/// <summary>
/// The spider gait step of the command trial (#1541) against the stand-in: it passes on spiders that stand while held and run when
/// charged, and fails with its own facts when one of those is broken. The game is the only proof of the speeds themselves.
/// </summary>
[TestClass]
public class CommandTrialSpiderStepsTests
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

    private void Run()
    {
        Assert.IsTrue(SeatTrialSteps.TryChoose("enter,spider_gaits", out var chosen, out var error), error);
        var script = new CommandTrialScript(_world, chosen, _log, _finished.Add);
        _world.Run(script, 600f, Frame);
        Assert.IsTrue(script.IsDone, "the run ended");
    }

    private string Line() => _log.Lines.Single(l => l.StartsWith("[SeatTrial] step=spider_gaits ", StringComparison.Ordinal));

    private static string Fact(string line, string key)
    {
        var match = Regex.Match(line, @"\b" + key + @"=(\S+)");
        Assert.IsTrue(match.Success, key + " is not in: " + line);
        return match.Groups[1].Value;
    }

    private static float Number(string line, string key) => float.Parse(Fact(line, key), CultureInfo.InvariantCulture);

    private void AssertFails(params string[] mentions)
    {
        var line = Line();
        StringAssert.Contains(line, "result=FAIL", line);
        foreach (var mention in mentions)
            StringAssert.Contains(line, mention, line);
    }

    // ---- spiders that work ----

    [TestMethod]
    public void TheStep_OnSpidersThatStandAndRun_PassesWithItsFacts()
    {
        Run();

        var line = Line();
        StringAssert.Contains(line, "result=PASS", line);
        Assert.AreEqual("12", Fact(line, "riders"));
        Assert.AreEqual("12", Fact(line, "mounted"));
        Assert.AreEqual("0.0", Fact(line, "stand"));
        Assert.AreEqual("8.0", Fact(line, "top"));
        Assert.AreEqual("0.2", Fact(line, "stop"));
        Assert.AreEqual("4", Fact(line, "pictures"));
        Assert.AreEqual("0.0", Fact(line, "slide"));
        Assert.AreEqual("60.0", Fact(line, "judged"));
        Assert.AreEqual("Soldier", Fact(line, "kind"));
        Assert.AreEqual("1", Fact(line, "chosen"));
        Assert.AreEqual("1", Fact(line, "letgo"));
        Assert.AreEqual("1", Fact(line, "seated"));
        Assert.AreEqual("0", Fact(line, "faulted"));
        CollectionAssert.AreEqual(new[] { "final" }, _finished);
    }

    [TestMethod]
    public void SpidersThatSlideWithStillLegs_FailWithTheSecondsInTheFacts()
    {
        _world.SpiderSlideSeconds = 19.9f;

        Run();

        AssertFails("slide=19.9", "top=8.0");
    }

    [TestMethod]
    public void ARunWhoseSlideWasNotJudged_FailsThoughTheSlideReadsZero()
    {
        _world.SpiderJudgedSeconds = 0f;

        Run();

        AssertFails("slide=0.0", "judged=0.0");
    }

    [TestMethod]
    public void TheStep_HoldsTheArmy_ThenChargesOnce_ThenHoldsAgain_AndLeavesNoKeepAndNoInput()
    {
        Run();

        Assert.AreEqual(2, _world.HoldOrders);
        Assert.AreEqual(1, _world.ChargeOrders);
        var orders = _world.Writes.Where(w => w is "hold-all" or "charge-all").ToArray();
        CollectionAssert.AreEqual(new[] { "hold-all", "charge-all", "hold-all" }, orders);
        Assert.AreEqual(SeatKeepKind.None, _world.Keeping);
        Assert.IsNull(_world.Input);
    }

    [TestMethod]
    public void TheStep_ReadsAgentsAndGivesOrders_OnlyInTheMissionTick()
    {
        Run();

        Assert.AreEqual(0, _world.SpiderReadsOutsideTheMissionTick);
        Assert.AreEqual(0, _world.WritesOutsideTheMissionTick.Count, string.Join(", ", _world.WritesOutsideTheMissionTick));
    }

    [TestMethod]
    public void TheStep_SavesFourPicturesUnderTheNameTheRunnerConverts_InTheRightPhases()
    {
        Run();

        var spider = _world.Screenshots.Zip(_world.ScreenshotTimes, (name, time) => (name, time)).Where(p => p.name.Contains("spider")).ToArray();
        CollectionAssert.AreEqual(
            new[] { "hud_spider_gaits_1.bmp", "hud_spider_gaits_2.bmp", "hud_spider_gaits_3.bmp", "hud_spider_gaits_4.bmp" }, spider.Select(p => p.name).ToArray());
        var chargedAt = _world.ChargedAtTime;
        Assert.IsTrue(spider[0].time <= chargedAt, "the first picture is while the army stands");
        Assert.AreEqual(chargedAt + 3f, spider[1].time, 0.2f);
        Assert.AreEqual(chargedAt + 8f, spider[2].time, 0.2f);
        Assert.IsTrue(spider[3].time >= chargedAt + SpiderTrialRules.ChargeSeconds, "the last picture is after the second Stop order");
    }

    [TestMethod]
    public void TheView_StaysAtTheKeepsDistance()
    {
        Run();

        Assert.IsFalse(_world.ZoomedAt.HasValue, "the wheel did not turn");
        Assert.IsNull(_world.Input);
    }

    [TestMethod]
    public void SpidersThatStillWalkIntoTheirRanks_WhenTheHoldBegins_PassOnTheLastSecondsOfTheStand()
    {
        _world.SpiderSettleSpeed = 2.7f;
        _world.SpiderSettlesAfter = 3f;

        Run();

        var line = Line();
        StringAssert.Contains(line, "result=PASS", line);
        StringAssert.Contains(line, "stand=0.0", line);
    }

    [TestMethod]
    public void SpidersThatStillMove_InTheLastSecondsOfTheStand_Fail()
    {
        _world.SpiderSettleSpeed = 2.7f;
        _world.SpiderSettlesAfter = SpiderTrialRules.StandSeconds;

        Run();

        AssertFails("stand=2.7");
    }

    [TestMethod]
    public void TheStopSpeed_IsReadOverTheLastTwoSecondsOfTheStop_NotTheFirstFour()
    {
        _world.SpiderSlowsAfter = 3.5f;

        Run();

        var line = Line();
        StringAssert.Contains(line, "result=PASS", line);
        Assert.AreEqual(0.2f, Number(line, "stop"), 0.05f);
        Assert.AreEqual(8f, Number(line, "top"), 0.05f);
    }

    [TestMethod]
    public void SpidersStillRunningAtTheEndOfTheStop_ShowTheirSpeedInTheFacts_AndStillPass()
    {
        _world.SpiderSlowsAfter = 100f;

        Run();

        var line = Line();
        StringAssert.Contains(line, "result=PASS", line);
        Assert.AreEqual(8f, Number(line, "stop"), 0.05f);
    }

    [TestMethod]
    public void MountsThatFall_AreAFactForAPerson_NotAFailure()
    {
        _world.SpiderMountsDieAfterSeconds = 9f;

        Run();

        var line = Line();
        StringAssert.Contains(line, "result=PASS", line);
        Assert.AreEqual("0", Fact(line, "mounted"));
        Assert.AreEqual("12", Fact(line, "riders"));
    }

    // ---- no spiders ----

    [TestMethod]
    public void WithNoSpiderRider_TheStepFailsAfterItsWait_AndGivesNoOrder()
    {
        _world.NoSpiders = true;

        Run();

        AssertFails("reason=no-spiders");
        Assert.AreEqual(0, _world.HoldOrders);
        Assert.AreEqual(0, _world.ChargeOrders);
    }

    [TestMethod]
    public void RidersSpawnedAFewSecondsLater_AreWaitedFor()
    {
        _world.SpidersAppearAt = 18f;

        Run();

        StringAssert.Contains(Line(), "result=PASS");
    }

    [TestMethod]
    public void WhenTheSeatWasNotEntered_TheStepFailsAtOnce()
    {
        _world.FaultOnEntry = true;

        Run();

        AssertFails("reason=not-seated");
        Assert.AreEqual(0, _world.SpiderChoices);
        Assert.AreEqual(0, _world.HoldOrders);
    }

    // ---- the facts, each broken alone ----

    [TestMethod]
    public void SpidersThatDoNotRunWhenCharged_Fail_WithTheirTop()
    {
        _world.SpiderChargeSpeed = 1.2f;

        Run();

        AssertFails("top=1.2", "stand=0.0", "pictures=4");
    }

    [TestMethod]
    public void SpidersThatMoveWhileHeld_Fail_WithTheirStand()
    {
        _world.SpiderStandSpeed = 2.4f;

        Run();

        AssertFails("stand=2.4", "top=");
    }

    [TestMethod]
    public void ASpeedThatIsNoNumber_Fails()
    {
        _world.SpiderSpeedIsNoNumber = true;

        Run();

        AssertFails("stand=NaN");
    }

    // ---- the keep ----

    [TestMethod]
    public void WhenTheKeepTakesNobody_TheStepFailsNamingTheMessage()
    {
        _world.KeepFindsNobody = true;

        Run();

        AssertFails("reason=not-kept", "said=");
        Assert.AreEqual(0, _world.ChargeOrders);
    }

    [TestMethod]
    public void WhenTheKeptOneIsNotTheChosenRider_TheStepFails()
    {
        _world.KeepsTheWrongOne = true;

        Run();

        AssertFails("chosen=0");
    }

    [TestMethod]
    public void WhenTheKeyDoesNotLetGo_TheStepFails()
    {
        _world.KeyLetsGo = false;

        Run();

        AssertFails("letgo=0");
    }

    [TestMethod]
    public void WhenTheKeptRiderFalls_TheViewGoesToAnotherRider_AndTheStepStillPasses()
    {
        _world.KeptRiderFallsAfterSeconds = 5f;

        Run();

        var line = Line();
        StringAssert.Contains(line, "result=PASS", line);
        Assert.IsTrue(_world.SpiderChoices >= 2, "another rider was chosen");
        Assert.AreEqual(3, _world.SpiderKeepPresses, "the keep, the keep again and the let-go");
        Assert.AreEqual("1", Fact(line, "letgo"));
    }

    [TestMethod]
    public void WhenTheKeptRiderFalls_AndNoRiderIsLeft_TheStepPressesNoKeyAgain()
    {
        _world.KeptRiderFallsAfterSeconds = 5f;
        _world.NoOtherRider = true;

        Run();

        Assert.AreEqual(1, _world.SpiderKeepPresses);
        Assert.AreEqual("1", Fact(Line(), "letgo"));
    }

    // ---- the table ----

    [TestMethod]
    public void TheSpiderRow_HasAHandler()
    {
        foreach (var row in SpiderTrialRows.Rows)
            CollectionAssert.Contains(CommandTrialScript.HandlerNames.ToList(), row.Name);
    }
}
